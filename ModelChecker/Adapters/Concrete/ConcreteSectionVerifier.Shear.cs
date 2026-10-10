using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Shear;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    /// <summary>
    /// Shear in the two section directions through <see cref="SectionShearCalculator"/>. Geometry and reinforcement come from the
    /// explicit <see cref="Sections.Concrete.ConcreteShearData"/> of the section; nothing is derived from the outline here.
    /// Not implemented: prestress, composite steel–concrete sections, seismic/accidental combinations. The interaction with the
    /// concomitant torsion is the Torsion task (same cot θ when ShearCotTheta is assigned).
    /// </summary>
    public sealed partial class ConcreteSectionVerifier
    {
        private readonly double? _shearCotTheta;

        private bool SupportsShear(SectionCheckSpecification check)
            => check.Category == CombinationCategory.Ultimate && (ShearProfiles.TryResolve(_standard, out _) || ShearProfiles.NotApplicableReason(_standard) != null);

        partial void AddShearConfiguration(SortedDictionary<string, string> entries)
        {
            entries["ShearCotTheta"] = _shearCotTheta.HasValue ? _shearCotTheta.Value.ToString("R", CultureInfo.InvariantCulture) : "method";
            entries["ShearProfile"] = ShearProfiles.TryResolve(_standard, out var profile) ? profile.ToString() : "unsupported";
        }

        private static CheckResult Unavailable(DataStatus data, string code, string message)
            => new CheckResult { Data = data, Outcome = EngineeringOutcome.NotEvaluated, Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error(code, message: message) } };

        /// <summary>Data of one shear direction ready for the core, or the reason why the task cannot run.</summary>
        private sealed class ShearPreparation
        {
            public SectionShearInput Input; public ConcreteShearData Data; public ConcreteShearDirection Direction;
            public ShearProfile Profile; public double Lever; public bool Stirrups, Fibres;
        }

        /// <summary>Checks the data of one direction and builds the input of the shear core (used by the shear and torsion tasks).</summary>
        private ShearPreparation PrepareShear(BeamCheckInput input, bool axis1, out CheckResult failure)
        {
            failure = null;
            var section = input.Section; var data = section.ShearData;
            var direction = data == null ? null : axis1 ? data.Axis1 : data.Axis2;
            if (direction == null) { failure = Unavailable(DataStatus.Insufficient, "MissingShearData", "Shear data of the section for " + (axis1 ? "Axis1" : "Axis2")); return null; }
            if (section.SteelSections.Count > 0) { failure = Unavailable(DataStatus.NotSupported, "CompositeShearNotImplemented", "Steel sections inside the concrete."); return null; }
            if (section.Rebars.Any(r => r.EpsilonP != 0 || r.RebarMaterial.SteelType == SteelMaterial.SteelTypes.Tendon))
            { failure = Unavailable(DataStatus.NotSupported, "PrestressedShearNotImplemented", "Prestress components are not included in the shear methods."); return null; }
            if (!(section.ConcreteMaterial is ConcreteMaterialEuropeanCommon concrete)) { failure = Unavailable(DataStatus.NotSupported, "UnsupportedConcreteMaterial", null); return null; }
            var profile = ShearProfiles.Resolve(_standard);
            var bars = section.Rebars.ToArray();
            if (bars.Length == 0) { failure = Unavailable(DataStatus.Insufficient, "MissingReinforcement", null); return null; }

            bool stirrups = data.Reinforcement != null && direction.Legs > 0;
            bool usesAsl = !stirrups || profile == ShearProfile.ModelCode2010;
            double asl = usesAsl ? direction.TensionReinforcementArea : 0;
            if (usesAsl && !direction.TensionReinforcementAnchored)
            { failure = Unavailable(DataStatus.Insufficient, "TensionReinforcementAnchorageNotConfirmed", "Asl enters the method: confirm its anchorage."); return null; }
            if (asl > section.AreaRebars) { failure = Unavailable(DataStatus.Insufficient, "ShearDataExceedsReinforcement", "Asl exceeds the section reinforcement."); return null; }
            var stirrupMaterial = stirrups ? data.Reinforcement.Material : bars[0].RebarMaterial;
            if (stirrups && profile == ShearProfile.DsEN1992p11 && (stirrupMaterial.StrainUTension < .05 || stirrupMaterial.Fu < 1.08 * stirrupMaterial.Fyk))
            { failure = Unavailable(DataStatus.NotSupported, "DuctilityClassBRequired", "DS: shear reinforcement of ductility class B or C required (εuk ≥ 5%, fu/fyk ≥ 1.08)."); return null; }
            double lever = direction.LeverFactor;
            if (profile == ShearProfile.DinEN1992p11)
            {
                if (!direction.LongitudinalCover.HasValue) { failure = Unavailable(DataStatus.Insufficient, "MissingLongitudinalCover", "DIN: cv is required for the lever arm limit."); return null; }
                double cv = direction.LongitudinalCover.Value, d = direction.EffectiveDepth;
                lever = Math.Min(lever, Math.Max(d - cv - 30, d - 2 * cv) / d);
                if (lever <= 0) { failure = Unavailable(DataStatus.Insufficient, "InvalidLeverArm", "DIN: z ≤ 0 with the given d and cv."); return null; }
            }
            bool needsAggregate = profile == ShearProfile.ModelCode2010 || profile == ShearProfile.NsEN1992p11;
            if (needsAggregate && !data.AggregateSize.HasValue) { failure = Unavailable(DataStatus.Insufficient, "MissingAggregateSize", "dg is required by " + profile + "."); return null; }

            bool fibres = concrete.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC;
            var f = input.Forces;
            double v = axis1 ? f.V1 : f.V2;
            double m = profile == ShearProfile.ModelCode2010 ? (axis1 ? f.M2 : f.M1) : 0; // moment about the axis normal to V
            var shearInput = new SectionShearInput(_standard, f.N, v, m, section.Area, direction.WebWidth, direction.EffectiveDepth, asl,
                Math.Abs(concrete.Fck), Math.Abs(concrete.CalculateFcd(_standard)), Math.Abs(stirrupMaterial.CalculateFyd(_standard)), _standard.GammaC,
                bars[0].RebarMaterial.E, stirrups ? direction.Legs * data.Reinforcement.BarArea : 0, stirrups ? data.Reinforcement.Spacing : 1,
                stirrups ? data.Reinforcement.AngleDegrees : 90, _shearCotTheta, lever, needsAggregate ? data.AggregateSize.Value : 20,
                profile == ShearProfile.ModelCode2010 ? direction.AxialEccentricity : 0,
                // FRC: Fctu is the characteristic ultimate residual strength; the matrix fctk is the 5% fractile from fck.
                fibres ? Math.Abs(concrete.Fctu) : 0, Math.Abs(concrete.Fctk05));
            return new ShearPreparation { Input = shearInput, Data = data, Direction = direction, Profile = profile, Lever = lever, Stirrups = stirrups, Fibres = fibres };
        }

        private CheckResult Shear(BeamCheckInput input, SectionCheckSpecification check, CancellationToken cancellationToken)
        {
            var notApplicable = ShearProfiles.NotApplicableReason(_standard);
            if (notApplicable != null) return NotApplicable(notApplicable);
            bool axis1 = check.Direction == SectionCheckDirection.Axis1;
            var prepared = PrepareShear(input, axis1, out var failure);
            if (failure != null) return failure;
            var shearInput = prepared.Input; var direction = prepared.Direction; var data = prepared.Data; var profile = prepared.Profile;
            cancellationToken.ThrowIfCancellationRequested();
            SectionShearResult shear;
            try { shear = SectionShearCalculator.Calculate(shearInput); }
            catch (ArgumentException ex) { return Unavailable(DataStatus.Insufficient, "ShearOutsideMethodRange", ex.Message); }
            catch (NotSupportedException ex) { return Unavailable(DataStatus.NotSupported, "ShearMethodNotImplemented", ex.Message); }

            var trace = new List<CheckCalculationValue>
            {
                new CheckCalculationValue("VRsd", shear.VRsd, "N", "shear reinforcement"), new CheckCalculationValue("VRcd", shear.VRcd, "N", "concrete"),
                new CheckCalculationValue("Asw", shearInput.Asw, "mm2", prepared.Stirrups ? direction.Legs + " legs" : "no shear reinforcement"),
                new CheckCalculationValue("bw", direction.WebWidth, "mm", data.Source), new CheckCalculationValue("d", direction.EffectiveDepth, "mm", data.Source),
                new CheckCalculationValue("z/d", prepared.Lever, "1", profile == ShearProfile.DinEN1992p11 ? "min(z/d; max(d − cv − 30; d − 2cv)/d)" : "given")
            };
            trace.AddRange(shear.Details.Select(d => new CheckCalculationValue(d.Symbol, d.Value, d.Unit, d.Expression)));
            var result = new CheckResult { EngineVersion = Version, Standard = StandardContext };
            if (prepared.Fibres && profile != ShearProfile.CnrDT204)
                result.Diagnostics.Add(new ModelDiagnostic { Code = "FibreContributionNotUsed", Severity = DiagnosticSeverity.Warning,
                    Message = profile + " does not include the residual strength of the fibres: plain-concrete resistance." });
            if (profile == ShearProfile.CnrDT200)
                result.Diagnostics.Add(new ModelDiagnostic { Code = "NoFrpStrengthening", Severity = DiagnosticSeverity.Warning,
                    Message = "The section has no FRP data: VRd,f = 0, resistance of the reinforced concrete member." });
            var f = input.Forces;
            if (Math.Abs(f.T) > 1e-6 * Math.Max(1, Math.Abs(shearInput.V) * direction.EffectiveDepth))
                result.Diagnostics.Add(new ModelDiagnostic { Code = "ConcomitantTorsionNotChecked", Severity = DiagnosticSeverity.Warning,
                    Message = "Shear only: the interaction with the concomitant torsion is the Torsion task (same cot θ when ShearCotTheta is assigned)." });
            if (shear.Verdict == ShearVerdict.NotEvaluated)
            {
                result.Data = DataStatus.NotSupported; result.Outcome = EngineeringOutcome.NotEvaluated;
                result.Diagnostics.Add(ModelDiagnostic.Error("ShearMethodNotApplicable", message: shear.Status)); return WithEdition(result);
            }
            bool passed = shear.Verdict == ShearVerdict.Satisfied;
            var metric = new CheckMetric("V", shear.Demand, shear.VRd, "N", shear.Ratio, passed, shear.Reference);
            result.Execution = ExecutionStatus.Completed; result.Data = DataStatus.Ready; result.Utilization = shear.Ratio;
            result.Outcome = passed ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
            result.Details = new ShearCheckDetails(SectionShearCalculator.MethodId + "." + profile, axis1 ? 1 : 2, shear.CotTheta, new[] { metric }, trace);
            return WithEdition(result);
        }
    }
}
