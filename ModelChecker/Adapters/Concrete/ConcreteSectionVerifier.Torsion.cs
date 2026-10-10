using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Shear;
using GPC.Checkers.Concrete.Torsion;
using GPC.Model.Materials;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    /// <summary>
    /// Torsion with the interaction of the shear of both directions through <see cref="SectionTorsionCalculator"/>, on the same cot θ
    /// (ShearCotTheta, required). Resisting profile and longitudinal bars from <see cref="Sections.Concrete.ConcreteTorsionData"/>, closed
    /// links and shear directions from <see cref="Sections.Concrete.ConcreteShearData"/>. Not implemented: prestress, composite sections,
    /// inclined links, seismic/accidental combinations.
    /// </summary>
    public sealed partial class ConcreteSectionVerifier
    {
        private bool SupportsTorsion(SectionCheckSpecification check)
            => check.Category == CombinationCategory.Ultimate && (TorsionProfiles.TryResolve(_standard, out _)
                || TorsionProfiles.NotApplicableReason(_standard) != null || TorsionProfiles.NotSupportedReason(_standard) != null);

        partial void AddTorsionConfiguration(SortedDictionary<string, string> entries)
            => entries["TorsionProfile"] = TorsionProfiles.TryResolve(_standard, out var profile) ? profile.ToString() : "unsupported";

        private CheckResult Torsion(BeamCheckInput input, CancellationToken cancellationToken)
        {
            var notApplicable = TorsionProfiles.NotApplicableReason(_standard);
            if (notApplicable != null) return NotApplicable(notApplicable);
            var notSupported = TorsionProfiles.NotSupportedReason(_standard);
            if (notSupported != null) return Unavailable(DataStatus.NotSupported, "TorsionMethodNotImplemented", notSupported);
            var section = input.Section; var f = input.Forces;

            // Torsion negligible with respect to the other components of the same state: nothing to verify, no data required.
            double scale = Math.Max(1, Math.Max(Math.Abs(f.M1), Math.Abs(f.M2)) + (Math.Abs(f.V1) + Math.Abs(f.V2)) * Math.Sqrt(section.Area));
            if (Math.Abs(f.T) <= 1e-6 * scale)
            {
                var none = new CheckResult { Execution = ExecutionStatus.Completed, Data = DataStatus.Ready, Outcome = EngineeringOutcome.Satisfied, Utilization = 0,
                    EngineVersion = Version, Standard = StandardContext,
                    Details = new TorsionCheckDetails(SectionTorsionCalculator.MethodId, 0, new[] { new CheckMetric("T", Math.Abs(f.T), null, "Nmm", 0, true, "no torsion demand") }) };
                none.Diagnostics.Add(new ModelDiagnostic { Code = "NoTorsionDemand", Severity = DiagnosticSeverity.Information,
                    Message = "Concomitant torsion negligible: the shear tasks cover this state." });
                return WithEdition(none);
            }
            if (section.SteelSections.Count > 0) return Unavailable(DataStatus.NotSupported, "CompositeTorsionNotImplemented", "Steel sections inside the concrete.");
            if (section.Rebars.Any(r => r.EpsilonP != 0 || r.RebarMaterial.SteelType == SteelMaterial.SteelTypes.Tendon))
                return Unavailable(DataStatus.NotSupported, "PrestressedTorsionNotImplemented", "Prestress components are not included in the torsion methods.");
            if (!(section.ConcreteMaterial is ConcreteMaterialEuropeanCommon concrete)) return Unavailable(DataStatus.NotSupported, "UnsupportedConcreteMaterial", null);
            if (section.RebarsCount == 0) return Unavailable(DataStatus.Insufficient, "MissingReinforcement", null);
            var torsion = section.TorsionData;
            if (torsion == null) return Unavailable(DataStatus.Insufficient, "MissingTorsionData", "Resisting profile and longitudinal bars for torsion of the section.");
            var data = section.ShearData;
            if (data == null) return Unavailable(DataStatus.Insufficient, "MissingShearData", "Closed links: shear data of the section.");
            var links = data.Reinforcement; // null: member explicitly without links, zero torsional resistance
            if (links != null && !torsion.ClosedLinksConfirmed)
                return Unavailable(DataStatus.Insufficient, "ClosedLinksNotConfirmed", "Confirm closed links and longitudinal bars inside the profile with a bar in each corner.");
            if (links != null && links.AngleDegrees != 90) return Unavailable(DataStatus.NotSupported, "InclinedLinksNotSupported", "Torsion is implemented with links at 90°.");
            if (torsion.LongitudinalArea > section.AreaRebars)
                return Unavailable(DataStatus.Insufficient, "TorsionDataExceedsReinforcement", "ΣAsl for torsion exceeds the section reinforcement.");
            if (!_shearCotTheta.HasValue)
                return Unavailable(DataStatus.Insufficient, "CotThetaRequired", "Torsion and shear share cot θ: assign ShearCotTheta.");

            var axes = new SectionShearInput[2]; bool fibres = concrete.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC;
            if (links != null)
                for (int i = 0; i < 2; i++)
                {
                    bool axis1 = i == 0; var direction = axis1 ? data.Axis1 : data.Axis2; double v = axis1 ? f.V1 : f.V2;
                    if (direction == null)
                    {
                        if (v != 0) return Unavailable(DataStatus.Insufficient, "MissingShearData", "Shear data of the section for " + (axis1 ? "Axis1" : "Axis2"));
                        continue;
                    }
                    if (direction.Legs == 0) return Unavailable(DataStatus.Insufficient, "ClosedLinksWithoutLegs", "Closed links need legs in both directions: Legs = 0 for " + (axis1 ? "Axis1" : "Axis2") + ".");
                    var prepared = PrepareShear(input, axis1, out var failure);
                    if (failure != null) return failure;
                    axes[i] = prepared.Input;
                }

            var longitudinalFyd = section.Rebars.Min(r => Math.Abs(r.RebarMaterial.CalculateFyd(_standard)));
            var linkFyd = links != null ? Math.Abs(links.Material.CalculateFyd(_standard)) : longitudinalFyd;
            SectionTorsionResult r;
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var torsionInput = new SectionTorsionInput(_standard, f.T, new TorsionGeometry(torsion.EnclosedArea, torsion.Perimeter, torsion.WallThickness),
                    Math.Abs(concrete.Fck), Math.Abs(concrete.CalculateFcd(_standard)), _standard.GammaC, linkFyd, longitudinalFyd, links != null ? links.BarArea : 0,
                    links != null ? links.Spacing : 1, torsion.LongitudinalArea, _shearCotTheta.Value, links?.AngleDegrees ?? 90, torsion.HollowWithReinforcementOnBothFaces);
                r = SectionTorsionCalculator.Calculate(torsionInput, axes[0], axes[1]);
            }
            catch (ArgumentException ex) { return Unavailable(DataStatus.Insufficient, "TorsionOutsideMethodRange", ex.Message); }
            catch (NotSupportedException ex) { return Unavailable(DataStatus.NotSupported, "TorsionMethodNotImplemented", ex.Message); }

            var trace = new List<CheckCalculationValue>
            {
                new CheckCalculationValue("Ak", torsion.EnclosedArea, "mm2", torsion.Source), new CheckCalculationValue("uk", torsion.Perimeter, "mm", torsion.Source),
                new CheckCalculationValue("tef", torsion.WallThickness, "mm", torsion.Source), new CheckCalculationValue("SumAsl", torsion.LongitudinalArea, "mm2", torsion.Source),
                new CheckCalculationValue("Ast", links?.BarArea ?? 0, "mm2", links != null ? "one leg of the closed links, " + data.Source : "no links"),
                new CheckCalculationValue("s", links?.Spacing, "mm", data.Source),
                new CheckCalculationValue("TRcd", r.TRcd, "Nmm", "strut"), new CheckCalculationValue("TRsd", r.TRsd, "Nmm", "links"),
                new CheckCalculationValue("TRld", r.TRld, "Nmm", "longitudinal bars"),
                new CheckCalculationValue("V1", f.V1, "N", Resistances(r.Axis1Shear)), new CheckCalculationValue("V2", f.V2, "N", Resistances(r.Axis2Shear))
            };
            trace.AddRange(r.Details.Select(d => new CheckCalculationValue(d.Symbol, d.Value, d.Unit, d.Expression)));
            var metrics = new[]
            {
                new CheckMetric("T", r.Demand, r.TRd, "Nmm", r.TorsionRatio, r.TorsionRatio <= 1, r.Reference),
                new CheckMetric("ConcreteStrutInteraction", r.ConcreteInteraction, 1, "1", r.ConcreteInteraction, r.ConcreteInteraction <= 1,
                    r.QuadraticInteraction ? "√[(T/TRcd)² + (ΣV/VRcd)²]" : "T/TRcd + ΣV/VRcd"),
                new CheckMetric("LinkInteraction", r.LinkInteraction, 1, "1", r.LinkInteraction, r.LinkInteraction <= 1, "T/TRsd + max(V/VRsd)")
            };
            var result = new CheckResult { EngineVersion = Version, Standard = StandardContext, Execution = ExecutionStatus.Completed, Data = DataStatus.Ready,
                Utilization = r.Ratio, Outcome = r.Verdict == TorsionVerdict.Satisfied ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied,
                Details = new TorsionCheckDetails(SectionTorsionCalculator.MethodId + "." + r.Profile, r.RequiredLongitudinalArea, metrics, trace) };
            if (links == null) result.Diagnostics.Add(new ModelDiagnostic { Code = "NoClosedLinks", Severity = DiagnosticSeverity.Warning, Message = r.Status });
            if (fibres)
                result.Diagnostics.Add(new ModelDiagnostic { Code = "FibreContributionNotUsed", Severity = DiagnosticSeverity.Warning,
                    Message = r.Profile + " does not include the residual strength of the fibres in torsion." });
            if (r.Profile == TorsionProfile.CnrDT200)
                result.Diagnostics.Add(new ModelDiagnostic { Code = "NoFrpStrengthening", Severity = DiagnosticSeverity.Warning,
                    Message = "The section has no FRP data: TRd,f = 0, resistance of the reinforced concrete member." });
            foreach (var limitation in r.Limitations)
                result.Diagnostics.Add(new ModelDiagnostic { Code = "TorsionRuleNotApplied", Severity = DiagnosticSeverity.Warning, Message = limitation });
            return WithEdition(result);
        }

        private static string Resistances(SectionShearResult shear) => shear == null ? "no shear in this direction"
            : "same cot θ: VRcd = " + shear.VRcd.ToString("G6", CultureInfo.InvariantCulture) + " N, VRsd = " + shear.VRsd.ToString("G6", CultureInfo.InvariantCulture) + " N";
    }
}
