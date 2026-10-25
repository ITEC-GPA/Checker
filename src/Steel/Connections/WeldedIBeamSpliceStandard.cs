using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Pipeline;

namespace GPC.Checkers.Steel.Connections
{
    public sealed class WeldedSpliceStandardCase
    {
        public string Id { get; }
        public WeldedIBeamSpliceParentInput Parents { get; }
        public WeldedSpliceStandardCase(string id, WeldedIBeamSpliceParentInput parents)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Case identifier required.", nameof(id));
            Id = id; Parents = parents ?? throw new ArgumentNullException(nameof(parents));
        }
    }
    public enum SpliceBlockVerificationMethod { FastenerHierarchy, ActualCollinearForcesWithHierarchyFallback }

    public sealed class WeldedIBeamSpliceStandardInput
    {
        public IReadOnlyList<WeldedSpliceStandardCase> Cases { get; }
        public WeldedIBeamSpliceEnvelopeInput Envelope { get; }
        public SpliceBlockVerificationMethod BlockMethod { get; }
        public WeldedIBeamSpliceStandardInput(IEnumerable<WeldedSpliceStandardCase> ultimateCases, SpliceBoltCategory category,
            IEnumerable<WeldedSpliceSlipCase> slipCases = null)
            : this(ultimateCases, category, slipCases, SpliceBlockVerificationMethod.FastenerHierarchy) { }
        public WeldedIBeamSpliceStandardInput(IEnumerable<WeldedSpliceStandardCase> ultimateCases, SpliceBoltCategory category,
            IEnumerable<WeldedSpliceSlipCase> slipCases, SpliceBlockVerificationMethod blockMethod)
        {
            if (!Enum.IsDefined(typeof(SpliceBlockVerificationMethod), blockMethod)) throw new ArgumentOutOfRangeException(nameof(blockMethod));
            BlockMethod = blockMethod;
            if (ultimateCases == null) throw new ArgumentNullException(nameof(ultimateCases));
            var u = ultimateCases.ToArray();
            if (u.Length == 0 || u.Any(c => c == null)) throw new ArgumentException("At least one non-null ULS case required.");
            Cases = Array.AsReadOnly(u);
            Envelope = new WeldedIBeamSpliceEnvelopeInput(u.Select(c => new WeldedSpliceUltimateCase(c.Id, c.Parents.Detail)), category, slipCases);
        }
    }
    public sealed class WeldedSpliceStandardCaseResult
    {
        public WeldedSpliceStandardCase Input { get; }
        public WeldedSpliceUltimateCaseResult Components { get; }
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> Model { get; }
        public WeldedIBeamSpliceStandardPlateResult AdditionalPlates { get; }
        public WeldedIBeamSpliceParentResult Parents { get; }
        internal WeldedSpliceStandardCaseResult(WeldedSpliceStandardCase input, WeldedSpliceUltimateCaseResult components, SpliceBlockVerificationMethod blockMethod)
        {
            Input = input; Components = components; Model = WeldedIBeamSpliceStandardModel.Verify(input.Parents.Detail);
            AdditionalPlates = WeldedIBeamSpliceStandardPlates.Verify(input.Parents.Detail, blockMethod == SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback); Parents = WeldedIBeamSpliceParents.Verify(input.Parents);
        }
    }
    /// <summary>Reference to the original leaf check, never a recomputed resistance.
    /// Section checks without scalar utilization retain their original failure.</summary>
    public sealed class WeldedSpliceStandardCheckSource
    {
        public string CaseId { get; }
        public string Component { get; }
        public bool IsServiceability { get; }
        public ConnectionCheck ComponentCheck { get; }
        public SteelCheckExecution SectionCheck { get; }
        public double? Utilization => ComponentCheck != null ? (double?)ComponentCheck.Utilization : SectionCheck.Utilization;
        public bool Failed => ComponentCheck != null ? !ComponentCheck.Passed : SectionCheck.Status == SteelExecutionStatus.Failed;
        internal WeldedSpliceStandardCheckSource(string id, string component, bool sls, ConnectionCheck check)
        { CaseId = id; Component = component; IsServiceability = sls; ComponentCheck = check; }
        internal WeldedSpliceStandardCheckSource(string id, string component, SteelCheckExecution check)
        { CaseId = id; Component = component; SectionCheck = check; }
    }
    public sealed class WeldedIBeamSpliceStandardResult
    {
        public WeldedIBeamSpliceStandardInput Input { get; }
        public IReadOnlyList<WeldedSpliceStandardCaseResult> Cases { get; }
        public IReadOnlyList<WeldedSpliceSlipCaseResult> SlipCases { get; }
        public IReadOnlyList<string> MissingRequirements { get; }
        public IReadOnlyList<WeldedSpliceStandardCheckSource> CheckSources { get; }
        public bool IsStaticResistanceComplete => MissingRequirements.Count == 0;
        public bool HasFailedComponent { get; }
        public bool HasFailedLinkedMember => Cases.Any(c => c.Parents.HasFailedMember);
        public bool HasBothMemberAssessmentsForEveryCase => Cases.All(c => c.Parents.HasBothMemberAssessments);
        public ConnectionStatus Status => !IsStaticResistanceComplete ? ConnectionStatus.NotSupported
            : HasFailedComponent ? ConnectionStatus.Failed : ConnectionStatus.Passed;
        public WeldedSpliceStandardCheckSource GoverningSource => !IsStaticResistanceComplete ? null
            : CheckSources.OrderByDescending(c => c.Failed && !c.Utilization.HasValue)
                .ThenByDescending(c => c.Utilization ?? 0).FirstOrDefault();
        public bool CoversFatigue => false;
        public bool CalculatesRotationalStiffness => false;
        public bool CertifiesExecution => false;
        public string Scope => "Static resistance of the declared balanced welded-I double-cover typology under the supplied concomitant ULS/SLS cases. "
            + "Includes bolts/bearing, selected slip category, gross/net plates, sufficient block hierarchy, compact-cover stability, gross/net parent section and continuous longitudinal fillets. "
            + "Conditional on declared execution and structural continuity/second-order analysis. Linked whole-member outcomes are separate; no fatigue or rotational stiffness calculation.";
        internal WeldedIBeamSpliceStandardResult(WeldedIBeamSpliceStandardInput input, IEnumerable<WeldedSpliceStandardCaseResult> cases,
            IEnumerable<WeldedSpliceSlipCaseResult> slips, IEnumerable<string> missing, IEnumerable<WeldedSpliceStandardCheckSource> sources, bool failed)
        {
            Input = input; Cases = Array.AsReadOnly(cases.ToArray()); SlipCases = Array.AsReadOnly(slips.ToArray());
            MissingRequirements = Array.AsReadOnly(missing.Distinct().ToArray()); CheckSources = Array.AsReadOnly(sources.ToArray()); HasFailedComponent = failed;
        }
    }

    /// <summary>Single entry point for the qualified standard typology. The older
    /// component envelope remains deliberately unqualified. No legacy diagnostic or
    /// expected value is altered: this workflow replaces its incomplete block/net
    /// components with explicit sufficient checks, and links actual parent results.</summary>
    public static class WeldedIBeamSpliceStandard
    {
        public static WeldedIBeamSpliceStandardResult Verify(WeldedIBeamSpliceStandardInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var envelope = WeldedIBeamSpliceEnvelope.Verify(p.Envelope);
            var missing = new List<string>(); var sources = new List<WeldedSpliceStandardCheckSource>();
            var cases = new List<WeldedSpliceStandardCaseResult>(); bool failed = false;
            foreach (var input in p.Cases)
            {
                var c = new WeldedSpliceStandardCaseResult(input, envelope.UltimateCases.Single(x => x.Input.Id == input.Id), p.BlockMethod);
                cases.Add(c); string id = input.Id;
                foreach (var issue in c.Components.BindingDiagnostics.Concat(c.Parents.BindingDiagnostics)) missing.Add(id + ":" + issue);
                if (!SameWelds(p.Cases[0].Parents.Welds, input.Parents.Welds)) missing.Add(id + ":LONGITUDINAL_WELD_DETAIL_MISMATCH");
                if (c.Model.Status != ConnectionStatus.Passed) missing.AddRange(c.Model.Diagnostics.Select(x => id + ":Model:" + x));
                Include(id, "Bolts", c.Components.Bolts.ComponentAssessment);
                Include(id, "Detailing", c.Components.Detailing.Detailing);
                foreach (var plate in c.Components.Plates.Covers)
                {
                    Include(id, plate.Cover + ".GrossSections", plate.Sections);
                    Include(id, plate.Cover + ".Compression", plate.LongitudinalCompression);
                }
                if (c.Components.Plates.Covers.Count != 8) missing.Add(id + ":EIGHT_COVER_ASSESSMENTS_REQUIRED");
                foreach (var issue in c.Components.Plates.Diagnostics) missing.Add(id + ":Plates:" + issue);
                Include(id, "CombinedNetSections", c.AdditionalPlates.NetSections);
                if (c.AdditionalPlates.BlockHierarchy.Checks.Count != 0 || c.AdditionalPlates.BlockHierarchy.Diagnostics.Count != 0)
                    Prerequisite("BlockHierarchy", c.AdditionalPlates.BlockHierarchy);
                if (c.AdditionalPlates.DirectBlockTearing.Checks.Count != 0 || c.AdditionalPlates.DirectBlockTearing.Diagnostics.Count != 0)
                    Include(id, "DirectBlockTearing", c.AdditionalPlates.DirectBlockTearing);
                Prerequisite("CompactPlateDomain", c.AdditionalPlates.CompactPlateDomain);
                if (c.Components.NetAxialPlastic != null) Include(id, "CategoryC.NetAxial", c.Components.NetAxialPlastic);
                Include(id, "LongitudinalWelds", c.Parents.LongitudinalWelds);
                Parent("Left.Section", input.Parents.LeftSection); Parent("Right.Section", input.Parents.RightSection);
                void Prerequisite(string component, ConnectionVerification<WeldedIBeamSpliceDetailInput> r)
                {
                    if (r.Status != ConnectionStatus.Passed)
                        missing.Add(id + ":" + component + ":SUFFICIENT_DOMAIN_NOT_ESTABLISHED");
                    foreach (var issue in r.Diagnostics) missing.Add(id + ":" + component + ":" + issue);
                    // A hierarchy/domain ratio is not an actual load utilization.
                }
                void Parent(string part, SteelVerificationResult r)
                {
                    if (!r.IsComplete) missing.Add(id + ":" + part + ":INCOMPLETE_STEEL_ASSESSMENT");
                    foreach (var check in r.Checks)
                    {
                        if (check.IsEvaluated) sources.Add(new WeldedSpliceStandardCheckSource(id, part, check));
                        else if (check.Status != SteelExecutionStatus.NotRequired) missing.Add(id + ":" + part + ":" + check.DiagnosticCode);
                        if (check.Status == SteelExecutionStatus.Failed) failed = true;
                    }
                }
            }
            foreach (var slip in envelope.SlipCases)
            {
                foreach (var issue in slip.BindingDiagnostics) missing.Add(slip.Input.Id + ":" + issue);
                Include(slip.Input.Id, "Slip", slip.Assessment, slip.Input.Input.LimitState == BoltSlipLimitState.Serviceability);
            }
            if (p.Envelope.Category == SpliceBoltCategory.B && !envelope.SlipCases.Any(s => s.IsBound && s.Input.Input.LimitState == BoltSlipLimitState.Serviceability))
                missing.Add("CATEGORY_B_SERVICEABILITY_CASE_REQUIRED");
            if (p.Envelope.RequiresCategoryC) foreach (var c in p.Cases)
                if (!envelope.SlipCases.Any(s => s.IsBound && s.Input.Input.LimitState == BoltSlipLimitState.Ultimate && s.Input.UltimateCaseId == c.Id))
                    missing.Add(c.Id + ":CATEGORY_C_BOUND_ULTIMATE_SLIP_REQUIRED");
            return new WeldedIBeamSpliceStandardResult(p, cases, envelope.SlipCases, missing, sources, failed);

            void Include<T>(string id, string component, ConnectionVerification<T> r, bool sls = false) where T : class
            {
                if (!r.IsComplete) missing.Add(id + ":" + component + ":INCOMPLETE_COMPONENT");
                foreach (var issue in r.Diagnostics) missing.Add(id + ":" + component + ":" + issue);
                foreach (var check in r.Checks) sources.Add(new WeldedSpliceStandardCheckSource(id, component, sls, check));
                if (r.Status == ConnectionStatus.Failed) failed = true;
            }
        }
        private static bool SameWelds(SpliceLongitudinalFillets a, SpliceLongitudinalFillets b)
            => a.TopThroat == b.TopThroat && a.BottomThroat == b.BottomThroat && a.EffectiveLengthEachSide == b.EffectiveLengthEachSide
                && a.TopGrade == b.TopGrade && a.WebGrade == b.WebGrade && a.BottomGrade == b.BottomGrade;
    }
}
