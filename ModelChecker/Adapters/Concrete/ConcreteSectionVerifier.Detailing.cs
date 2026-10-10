using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Detailing;
using GPC.Checkers.Concrete.Durability;
using GPC.Model.Materials;
using GPC.Model.Core;
using GPC.Model.Sections.Concrete;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    /// <summary>
    /// 1D detailing of beams and columns as a physical-member task (one per member and state, never per FEM sample) through
    /// <see cref="MemberDetailingCalculator"/>. Every distinct section of the member is checked with its <see cref="ConcreteDetailingData"/> and the
    /// links of its <see cref="ConcreteShearData"/>; the column NEd is the largest compression among the member samples of the state.
    /// Anchorage and laps need the zone data of bars and ends: available as a Checker API (AnchorageCalculator), not planned here.
    /// </summary>
    public sealed partial class ConcreteSectionVerifier : IPhysicalMemberVerifier
    {
        public const string BeamDetailingMethod = "Concrete.Detailing1D.Beam";
        public const string ColumnDetailingMethod = "Concrete.Detailing1D.Column";

        CheckStandardContext IPhysicalMemberVerifier.Standard => StandardContext;

        public bool Supports(string methodId, CheckMechanism mechanism)
            => mechanism == CheckMechanism.Detailing && (methodId == BeamDetailingMethod || methodId == ColumnDetailingMethod)
                && (DetailingProfiles.TryResolve(_standard, out _) || DetailingProfiles.NotApplicableReason(_standard) != null || DetailingProfiles.NotSupportedReason(_standard) != null);

        public CheckResult Verify(PhysicalMemberCheckInput input, CancellationToken cancellationToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!Supports(input.MethodId, input.Mechanism)) return Unavailable(DataStatus.NotSupported, "UnsupportedMemberCheck", input.MethodId);
            var notApplicable = DetailingProfiles.NotApplicableReason(_standard);
            if (notApplicable != null) return NotApplicable(notApplicable);
            var notSupported = DetailingProfiles.NotSupportedReason(_standard);
            if (notSupported != null) return Unavailable(DataStatus.NotSupported, "DetailingMethodNotImplemented", notSupported);
            var profile = DetailingProfiles.Resolve(_standard);
            var kind = input.MethodId == ColumnDetailingMethod ? MemberDetailingKind.Column : MemberDetailingKind.Beam;
            // Prepared cuts own independent copies; equivalent physical inputs must be checked only once.
            var sections = input.Sections.Select(s => s.Section).Where(s => s != null)
                .GroupBy(s => ModelValues.Fingerprint(new object[] { s }), StringComparer.Ordinal).Select(g => g.First()).ToArray();
            if (sections.Length == 0) return Unavailable(DataStatus.Insufficient, "MissingMemberSections", null);
            double compression = input.Sections.Select(s => Math.Max(0, -s.Forces.N)).DefaultIfEmpty(0).Max();
            var metrics = new List<CheckMetric>(); var trace = new List<CheckCalculationValue>
            {
                new CheckCalculationValue("Kind", null, "", kind.ToString()), new CheckCalculationValue("NEd", compression, "N", "largest compression among the member samples of the state")
            };
            var pending = new List<string>(); bool failed = false, dataPending = false;
            for (int k = 0; k < sections.Length; k++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var section = sections[k];
                // Names are labels, not keys: different sections may legitimately share a name.
                string suffix = sections.Length > 1 ? "@" + k.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + section.Name : "";
                if (section.SteelSections.Count > 0) return Unavailable(DataStatus.NotSupported, "CompositeDetailingNotImplemented", "Steel sections inside the concrete.");
                if (section.Rebars.Any(r => r.EpsilonP != 0 || r.RebarMaterial.SteelType == SteelMaterial.SteelTypes.Tendon))
                    return Unavailable(DataStatus.NotSupported, "PrestressedDetailingNotImplemented", "Detailing of prestressed sections is not implemented.");
                if (!(section.ConcreteMaterial is ConcreteMaterialEuropeanCommon concrete)) return Unavailable(DataStatus.NotSupported, "UnsupportedConcreteMaterial", null);
                if (section.RebarsCount == 0) return Unavailable(DataStatus.Insufficient, "MissingReinforcement", section.Name);
                var data = section.DetailingData;
                if (data == null) return Unavailable(DataStatus.Insufficient, "MissingDetailingData", "Covers, widths, aggregate and confirmations of section " + section.Name + ".");
                var shear = section.ShearData;
                if (shear == null) return Unavailable(DataStatus.Insufficient, "MissingShearData", "Links of section " + section.Name + ": shear data required (null links = member without links).");
                var links = shear.Reinforcement; int legs = (shear.Axis2 ?? shear.Axis1)?.Legs ?? 0;
                var bar = section.Rebars.First().RebarMaterial;
                var geometry = CrackSectionGeometry.From(section);
                trace.Add(new CheckCalculationValue("Section" + suffix, null, "", (section.Name ?? "") + ", " + data.Source));
                double? cdur = data.MinimumDurabilityCover; double addition = 0, ground = 0;
                if (section.DurabilityData != null)
                {
                    var durability = Durability(section.DurabilityData, data, Math.Abs(concrete.Fck), links != null ? links.Diameter : geometry.Bars.Max(b => b.Diameter), suffix,
                        metrics, trace, pending, ref failed);
                    if (durability.Unavailable != null) return durability.Unavailable;
                    if (durability.Cover != null)
                    {
                        cdur = Math.Max(cdur ?? 0, durability.Cover.Durability);
                        addition = (section.DurabilityData.RoughSurface ? 5 : 0) + section.DurabilityData.Abrasion; ground = section.DurabilityData.Ground;
                    }
                }
                MemberDetailingResult r;
                try
                {
                    r = MemberDetailingCalculator.Calculate(profile, new MemberDetailingInput(kind, geometry, section.Area, Math.Abs(concrete.Fck),
                        Math.Abs(concrete.Fctm), Math.Abs(bar.Fyk), Math.Abs(bar.CalculateFyd(_standard)), data.TopWidth, data.BottomWidth, data.WebWidth, compression, links != null,
                        links?.Diameter ?? 0, links?.Spacing ?? 0, legs, data.Aggregate, data.NominalCover, cdur, data.CoverDeviation, data.LapZone,
                        data.CompressionBarsRestrained, data.EndZonesConfirmed, addition, ground));
                }
                catch (ArgumentException ex) { return Unavailable(DataStatus.Insufficient, "DetailingOutsideMethodRange", ex.Message); }
                catch (NotSupportedException ex) { return Unavailable(DataStatus.NotSupported, "DetailingMethodNotImplemented", ex.Message); }
                foreach (var check in r.Checks)
                {
                    if (check.Passed.HasValue)
                    {
                        metrics.Add(new CheckMetric(check.Key + suffix, check.Actual, check.Limit, check.Unit, null, check.Passed, check.Reference));
                        failed |= check.Passed == false;
                    }
                    else
                    {
                        pending.Add(check.Key + suffix + ": " + check.Explanation); dataPending |= !check.NotImplemented;
                        trace.Add(new CheckCalculationValue("Pending " + check.Key + suffix, null, "", check.Reference + " · " + check.Explanation));
                    }
                }
            }
            var result = new CheckResult { EngineVersion = Version, Standard = StandardContext,
                Details = new DetailingCheckDetails(MemberDetailingCalculator.MethodId + "." + profile + "." + kind, metrics, trace) };
            if (failed)
            {
                result.Execution = ExecutionStatus.Completed; result.Data = DataStatus.Ready; result.Outcome = EngineeringOutcome.NotSatisfied;
                foreach (var item in pending) result.Diagnostics.Add(new ModelDiagnostic { Code = "DetailingPending", Severity = DiagnosticSeverity.Warning, Message = item });
            }
            else if (pending.Count > 0)
            {
                result.Data = dataPending ? DataStatus.Insufficient : DataStatus.NotSupported; result.Outcome = EngineeringOutcome.NotEvaluated;
                result.Diagnostics.Add(ModelDiagnostic.Error(dataPending ? "DetailingPending" : "DetailingRuleNotImplemented", message: string.Join("; ", pending)));
            }
            else { result.Execution = ExecutionStatus.Completed; result.Data = DataStatus.Ready; result.Outcome = EngineeringOutcome.Satisfied; }
            return WithEdition(result);
        }

        /// <summary>
        /// Durability of a section (GPCChecker.Concrete Durability): cmin,dur from the exposure classes, design life and modifiers with the bond
        /// diameter of the outermost steel, and the minimum strength of the exposures as a metric. NTC and CNR-DT 200: without a pertinent Cmin
        /// the UNI 11104 class of the exposures is used (Circolare C4.1.6.1.3, as the ANTHEA material sheet). Rules not implemented stay pending.
        /// </summary>
        private (CoverResult Cover, CheckResult Unavailable) Durability(ConcreteDurabilityData durability, ConcreteDetailingData data, double fck, double bondDiameter,
            string suffix, List<CheckMetric> metrics, List<CheckCalculationValue> trace, List<string> pending, ref bool failed)
        {
            if (!DurabilityProfiles.TryResolve(_standard, out var profile))
            {
                string reason = DurabilityProfiles.NotSupportedReason(_standard);
                pending.Add("Durability" + suffix + ": " + reason); trace.Add(new CheckCalculationValue("Pending Durability" + suffix, null, "", reason));
                return (null, null);
            }
            bool ntc = profile == DurabilityProfile.Ntc2018 || profile == DurabilityProfile.CnrDT200;
            double? cmin = ntc ? durability.PertinentCmin ?? ExposureClasses.Uni11104MinimumStrength(durability.Exposures) : (double?)null;
            CoverResult cover;
            try
            {
                cover = CoverRequirements.Calculate(profile, new CoverInput(durability.Exposures, fck, durability.DesignLife, durability.StrengthReduction, false,
                    durability.SpecialQualityControl, bondDiameter, data.Aggregate, data.CoverDeviation, durability.RoughSurface, durability.Abrasion, durability.Ground, false,
                    durability.NtcQualityReduction, cmin));
            }
            catch (ArgumentException ex) { return (null, Unavailable(DataStatus.Insufficient, "DurabilityOutsideMethodRange", ex.Message)); }
            catch (NotSupportedException ex)
            {
                pending.Add("DurabilityCover" + suffix + ": " + ex.Message); trace.Add(new CheckCalculationValue("Pending DurabilityCover" + suffix, null, "", ex.Message));
                cover = null;
            }
            trace.Add(new CheckCalculationValue("Exposures" + suffix, null, "", string.Join("+", durability.Exposures) + ", " + durability.DesignLife + " years, " + durability.Source));
            if (cover != null)
            {
                trace.Add(new CheckCalculationValue("cmin,dur" + suffix, cover.Durability, "mm", cover.Reference + (cover.Lines.Count > 0
                    ? ": " + string.Join(", ", cover.Lines.Select(l => l.Exposure + (l.StructuralClass.HasValue ? " S" + l.StructuralClass : "") + " " + l.Durability))
                    : ": environment " + cover.NtcEnvironment + ", Cmin " + cover.NtcCmin + (durability.PertinentCmin.HasValue ? "" : " (UNI 11104)") + ", C0 " + cover.NtcC0
                      + ", table " + cover.NtcTable + " + life " + cover.NtcLifeExtra + " + strength " + cover.NtcLowStrengthExtra + " − quality " + cover.NtcQualityReduction)));
                if (data.MinimumDurabilityCover.HasValue)
                    trace.Add(new CheckCalculationValue("cmin,dur given" + suffix, data.MinimumDurabilityCover.Value, "mm", "detailing data; the larger value governs"));
            }
            var strength = ExposureClasses.MinimumStrength(profile, durability.Exposures);
            if (strength.Fck.HasValue)
            {
                bool passed = fck >= strength.Fck.Value - 1e-9;
                metrics.Add(new CheckMetric("MinimumStrength" + suffix, fck, strength.Fck.Value, "MPa", null, passed, strength.Reference));
                failed |= !passed;
            }
            if (strength.Undefined.Count > 0)
                trace.Add(new CheckCalculationValue("MinimumStrength not defined" + suffix, null, "", string.Join("+", strength.Undefined) + ": not in " + strength.Reference));
            return (cover, null);
        }
    }
}
