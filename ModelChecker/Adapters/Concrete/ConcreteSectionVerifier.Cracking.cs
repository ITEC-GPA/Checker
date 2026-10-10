using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Core;
using GPC.Model.Sections.Concrete;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    /// <summary>
    /// Crack control (Serviceability/CrackWidth) through <see cref="SectionCrackCheck"/>: linear cracked analysis without tensile concrete with the
    /// creep coefficient of the options, uncracked linear analysis with tensile concrete for decompression and crack formation. Exposure, sensitivity,
    /// cover, bond and bar layout come from <see cref="ConcreteCrackData"/> of the section; load duration and design wlim from the options.
    /// Not implemented: prestressed and composite sections.
    /// </summary>
    public sealed partial class ConcreteSectionVerifier
    {
        private readonly CrackLoadDuration _crackLoadDuration;
        private readonly double? _crackDesignLimit;
        private readonly Dictionary<string, ConcreteCalculationSession> _crackCheckers = new Dictionary<string, ConcreteCalculationSession>();

        private bool SupportsCracking(SectionCheckSpecification check)
            => (check.Category == CombinationCategory.Characteristic || check.Category == CombinationCategory.Frequent || check.Category == CombinationCategory.QuasiPermanent)
                && (CrackProfiles.TryResolve(_standard, out _) || CrackProfiles.NotApplicableReason(_standard) != null || CrackProfiles.NotSupportedReason(_standard) != null);

        partial void AddCrackConfiguration(SortedDictionary<string, string> entries)
        {
            entries["CrackProfile"] = CrackProfiles.TryResolve(_standard, out var profile) ? profile.ToString() : "unsupported";
            entries["CrackLoadDuration"] = _crackLoadDuration.ToString();
            entries["CrackDesignLimit"] = _crackDesignLimit.HasValue ? _crackDesignLimit.Value.ToString("R", CultureInfo.InvariantCulture) : "standard";
        }

        /// <summary>Linear stress analysis with or without tensile concrete, cached per section and configuration. Call under <see cref="Sync"/>.</summary>
        private ConcreteCalculationSession LinearChecker(ReinforcedConcreteSection section, CoordinateSystem reference, bool tensileConcrete)
        {
            var key = ModelValues.Fingerprint(new object[] { section, Configuration, "linear-crack", tensileConcrete });
            if (!_crackCheckers.TryGetValue(key, out var checker))
            {
                checker = _calculationFactory.Create(section, _standard, new ConcreteCalculationOptions(reference, _criterion,
                    SectionSolver.StressAnalysisTypes.Linear, tensileConcrete, _angularDivisions, _psiRebar, _psiTendon))
                    ?? throw new InvalidOperationException("NullNumericalSession");
                _crackCheckers[key] = checker; _createdCheckers++;
            }
            return checker;
        }

        private CheckResult Cracking(BeamCheckInput input, CombinationCategory category, CancellationToken cancellationToken)
        {
            var notApplicable = CrackProfiles.NotApplicableReason(_standard);
            if (notApplicable != null) return NotApplicable(notApplicable);
            var notSupported = CrackProfiles.NotSupportedReason(_standard);
            if (notSupported != null) return Unavailable(DataStatus.NotSupported, "CrackMethodNotImplemented", notSupported);
            var section = input.Section;
            if (section.SteelSections.Count > 0) return Unavailable(DataStatus.NotSupported, "CompositeCrackingNotImplemented", "Steel sections inside the concrete.");
            if (section.Rebars.Any(r => r.EpsilonP != 0 || r.RebarMaterial.SteelType == SteelMaterial.SteelTypes.Tendon))
                return Unavailable(DataStatus.NotSupported, "PrestressedCrackingNotImplemented", "Crack control of prestressed sections is not implemented.");
            if (!(section.ConcreteMaterial is ConcreteMaterialEuropeanCommon concrete)) return Unavailable(DataStatus.NotSupported, "UnsupportedConcreteMaterial", null);
            if (section.RebarsCount == 0) return Unavailable(DataStatus.Insufficient, "MissingReinforcement", null);
            var data = section.CrackData;
            if (data == null) return Unavailable(DataStatus.Insufficient, "MissingCrackData", "Exposure, reinforcement sensitivity, cover and bond of the section.");
            var combination = category == CombinationCategory.Characteristic ? ServiceabilityCombination.Characteristic
                : category == CombinationCategory.Frequent ? ServiceabilityCombination.Frequent : ServiceabilityCombination.QuasiPermanent;
            var forces = SectionForces(input, out var reference);
            SectionCrackResult r;
            lock (Sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stress = LinearChecker(section, reference, false).Response.Solve(new SectionAnalysisInput(forces), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (stress == null || stress.Diagnostics.Status != CalculationStatus.Completed) return Failure("CheckerStressAnalysisNotConverged");
                if (!MatchesResponse(stress, forces, SectionSolver.StressAnalysisTypes.Linear)) return Failure("NumericalResponseContractMismatch");
                try
                {
                    var crackInput = new SectionCrackInput(_standard, combination, data.Exposure, data.SensitiveReinforcement, _crackDesignLimit,
                        CrackSectionGeometry.From(section, data.ConcentricRings), stress.Strain.ToStrainPlane(), stress.Bars.Select(b => b.Stress).ToArray(), true, false, false,
                        section.Rebars.First().RebarMaterial.E, Math.Abs(concrete.Ecm), Math.Abs(concrete.Fctm), _crackLoadDuration == CrackLoadDuration.ShortTerm, data.RibbedBars,
                        data.Cover, null, data.MaximumBarSpacing, () =>
                        {
                            var uncracked = LinearChecker(section, reference, true).Response.Solve(new SectionAnalysisInput(forces), cancellationToken);
                            if (uncracked == null || uncracked.Diagnostics.Status != CalculationStatus.Completed) throw new InvalidOperationException("Uncracked analysis not completed.");
                            if (!MatchesResponse(uncracked, forces, SectionSolver.StressAnalysisTypes.Linear)) throw new InvalidOperationException("NumericalResponseContractMismatch");
                            return uncracked.Concrete.Max(v => v.Stress);
                        });
                    r = SectionCrackCheck.Evaluate(crackInput);
                }
                catch (ArgumentException ex) { return Unavailable(DataStatus.Insufficient, "CrackOutsideMethodRange", ex.Message); }
                catch (NotSupportedException ex) { return Unavailable(DataStatus.NotSupported, "CrackMethodNotImplemented", ex.Message); }
            }

            if (r.Verdict == CrackVerdict.NotRequired)
                return NotApplicable(r.Profile + ": crack control is not required in the " + category + " combination"
                    + (r.Requirement.RequiredCombination.HasValue ? " (required in the " + r.Requirement.RequiredCombination + " combination)." : "."));
            var trace = new List<CheckCalculationValue>
            {
                new CheckCalculationValue("Criterion", null, "", r.Requirement.Criterion.ToString()),
                new CheckCalculationValue("Exposure", null, "", (data.Exposure ?? "not given") + (data.SensitiveReinforcement ? ", sensitive reinforcement" : ", little sensitive reinforcement")),
                new CheckCalculationValue("c", data.Cover, "mm", data.Source), new CheckCalculationValue("LoadDuration", null, "", _crackLoadDuration.ToString()),
                new CheckCalculationValue("wlim", r.Limit, "mm", _crackDesignLimit.HasValue && r.Requirement.Criterion == CrackCriterion.CrackWidth
                    && r.Profile != CrackProfile.Ntc2018 && r.Profile != CrackProfile.CnrDT200 && r.Profile != CrackProfile.UniEN1992p11 ? "design limit" : "limit of the standard"),
                new CheckCalculationValue("Ac,eff", r.EffectiveArea, "mm2", r.GoverningRegion ?? ""), new CheckCalculationValue("As,eff", r.EffectiveSteel, "mm2", r.GoverningRegion ?? ""),
                new CheckCalculationValue("s", r.BarSpacing, "mm", r.SpacingSource ?? "")
            };
            trace.AddRange(r.Regions.Select(g => new CheckCalculationValue("Region " + g.Key, g.Width, "mm",
                "Ac,eff " + g.Area.ToString("G6", CultureInfo.InvariantCulture) + " mm2, As,eff " + g.SteelArea.ToString("G6", CultureInfo.InvariantCulture) + " mm2, bars " + string.Join(" ", g.BarIndices))));
            trace.AddRange(r.Details.Select(d => new CheckCalculationValue(d.Symbol, d.Value, d.Unit, d.Expression)));
            if (r.Verdict == CrackVerdict.NotEvaluated)
            {
                bool insufficient = r.Outcome == CrackOutcome.MissingExposure || r.Outcome == CrackOutcome.MissingDesignLimit || r.Outcome == CrackOutcome.SpacingUndetermined;
                var failed = Unavailable(insufficient ? DataStatus.Insufficient : DataStatus.NotSupported, "Crack" + r.Outcome, r.Status);
                failed.EngineVersion = Version; failed.Standard = StandardContext;
                return WithEdition(failed);
            }
            bool passed = r.Verdict == CrackVerdict.Satisfied;
            CheckMetric metric; string criterion;
            if (r.Requirement.Criterion == CrackCriterion.CrackWidth)
            {
                metric = new CheckMetric("CrackWidth", r.Width, r.Limit, "mm", r.Ratio, passed, r.Reference); criterion = "crack-width";
            }
            else
            {
                double limit = r.StressLimit.Value, maximum = r.UncrackedMaximumStress.Value;
                metric = new CheckMetric("UncrackedConcreteTension", maximum, limit, "MPa", limit > 0 ? Math.Max(0, maximum) / limit : (double?)null, passed, r.Reference);
                criterion = r.Requirement.Criterion == CrackCriterion.Decompression ? "decompression" : "crack-formation";
            }
            var result = new CheckResult { Execution = ExecutionStatus.Completed, Data = DataStatus.Ready, Outcome = passed ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied,
                Utilization = metric.Utilization, EngineVersion = Version, Standard = StandardContext,
                Details = new ServiceabilityCheckDetails(SectionCrackCheck.MethodId + "." + r.Profile, criterion, category.ToString(), new[] { metric }, trace) };
            return WithEdition(result);
        }
    }
}
