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
    public enum SplicePlateStabilityMethod
    {
        CompactCovers,
        EffectiveCompression,
        /// <summary>SCI P398 conventional flat bolted covers with regular close fasteners,
        /// class1-3 inter-fastener strips/outstands and all N-M-V checks. Not free-panel postbuckling.</summary>
        StandardBoltedCovers,
        /// <summary>Effective compression plus concomitant shear up to half the
        /// conservatively bounded shear resistance; whole-width shear buckling excluded.</summary>
        EffectiveCompressionWithLowShear,
        /// <summary>Effective compression plus concomitant shear of any magnitude: whole-width EN1993-1-5
        /// section5 shear-buckling resistance (k_tau=5.34, non-rigid end post, eta=1, no reserve beyond
        /// tau_cr) and the 7.1 interaction. A wholly compressed cut at high shear stays unsupported (section10).</summary>
        EffectiveCompressionWithShearBuckling
    }

    public enum SpliceBlockVerificationMethod
    {
        FastenerHierarchy,
        ActualCollinearForcesWithHierarchyFallback,
        /// <summary>Collinear groups as ActualCollinearForcesWithHierarchyFallback. In a mixed-direction group every
        /// path that fails the sufficient hierarchy is verified with its actual bolt actions: U paths by the force
        /// component in their only admissible tearing direction, edge L paths by the linear interaction of the
        /// two Veff,2 tearing resistances and a static lower bound of the pure-moment resistance.</summary>
        ActualForcesWithMixedBlockInteraction
    }

    public sealed class WeldedIBeamSpliceStandardInput
    {
        public IReadOnlyList<WeldedSpliceStandardCase> Cases { get; }
        public WeldedIBeamSpliceEnvelopeInput Envelope { get; }
        public SpliceBlockVerificationMethod BlockMethod { get; }
        public SpliceFlangeFaceModel FaceModel { get; }
        public SplicePlateStabilityMethod PlateStability { get; }
        public WeldedIBeamSpliceStandardInput(IEnumerable<WeldedSpliceStandardCase> ultimateCases, SpliceBoltCategory category,
            IEnumerable<WeldedSpliceSlipCase> slipCases = null)
            : this(ultimateCases, category, slipCases, SpliceBlockVerificationMethod.FastenerHierarchy) { }
        public WeldedIBeamSpliceStandardInput(IEnumerable<WeldedSpliceStandardCase> ultimateCases, SpliceBoltCategory category,
            IEnumerable<WeldedSpliceSlipCase> slipCases, SpliceBlockVerificationMethod blockMethod)
            : this(ultimateCases, category, slipCases, blockMethod, SpliceFlangeFaceModel.BalancedEqualFaces) { }
        public WeldedIBeamSpliceStandardInput(IEnumerable<WeldedSpliceStandardCase> ultimateCases, SpliceBoltCategory category,
            IEnumerable<WeldedSpliceSlipCase> slipCases, SpliceBlockVerificationMethod blockMethod, SpliceFlangeFaceModel faceModel)
            : this(ultimateCases, category, slipCases, blockMethod, faceModel, SplicePlateStabilityMethod.CompactCovers) { }
        public WeldedIBeamSpliceStandardInput(IEnumerable<WeldedSpliceStandardCase> ultimateCases, SpliceBoltCategory category,
            IEnumerable<WeldedSpliceSlipCase> slipCases, SpliceBlockVerificationMethod blockMethod, SpliceFlangeFaceModel faceModel,
            SplicePlateStabilityMethod plateStability)
        {
            if (!Enum.IsDefined(typeof(SplicePlateStabilityMethod), plateStability)) throw new ArgumentOutOfRangeException(nameof(plateStability));
            PlateStability = plateStability;
            if (!Enum.IsDefined(typeof(SpliceFlangeFaceModel), faceModel)) throw new ArgumentOutOfRangeException(nameof(faceModel));
            FaceModel = faceModel;
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
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> EffectiveCompression { get; }
        internal WeldedSpliceStandardCaseResult(WeldedSpliceStandardCase input, WeldedSpliceUltimateCaseResult components, SpliceBlockVerificationMethod blockMethod, SpliceFlangeFaceModel faceModel, SplicePlateStabilityMethod plateStability)
        {
            EffectiveCompression = WeldedIBeamSpliceEffectiveCompression.UsesEffectiveWidths(plateStability)
                ? WeldedIBeamSpliceEffectiveCompression.Verify(input.Parents.Detail, plateStability, components.Plates) : null;
            Input = input; Components = components; Model = WeldedIBeamSpliceStandardModel.Verify(input.Parents.Detail, faceModel, components.Bolts.Forces);
            AdditionalPlates = WeldedIBeamSpliceStandardPlates.Verify(input.Parents.Detail, blockMethod, plateStability, components.Plates, Model);
            Parents = WeldedIBeamSpliceParents.Verify(input.Parents, components.Bolts.Forces);
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
        public string Scope => "Static resistance of the declared welded-I double-cover typology (face model: " + Input.FaceModel + ") under the supplied concomitant ULS/SLS cases. "
            + "Includes bolts/bearing, selected slip category, gross/net plates, selected collinear block checks/hierarchy"
            + (Input.BlockMethod == SpliceBlockVerificationMethod.ActualForcesWithMixedBlockInteraction ? " with actual-force mixed-direction block interaction (" + Input.BlockMethod + ")" : "")
            + ", cover stability (" + Input.PlateStability + "), gross/net parent section and continuous longitudinal fillets. "
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
                var c = new WeldedSpliceStandardCaseResult(input, envelope.UltimateCases.Single(x => x.Input.Id == input.Id), p.BlockMethod, p.FaceModel, p.PlateStability);
                cases.Add(c); string id = input.Id;
                foreach (var issue in c.Components.BindingDiagnostics.Concat(c.Parents.BindingDiagnostics)) missing.Add(id + ":" + issue);
                if (!SpliceIdentity.Welds(p.Cases[0].Parents.Welds, input.Parents.Welds)) missing.Add(id + ":LONGITUDINAL_WELD_DETAIL_MISMATCH");
                if (c.Model.Status != ConnectionStatus.Passed) missing.AddRange(c.Model.Diagnostics.Select(x => id + ":Model:" + x));
                Assess(id, "Bolts", c.Components.Bolts.ComponentAssessment);
                Assess(id, "Detailing", c.Components.Detailing.Detailing);
                foreach (var plate in c.Components.Plates.Covers)
                {
                    Assess(id, plate.Cover + ".GrossSections", plate.Sections);
                    if (c.EffectiveCompression == null) Assess(id, plate.Cover + ".Compression", plate.LongitudinalCompression);
                }
                if (c.EffectiveCompression != null) Assess(id, "EffectiveCoverCompression", c.EffectiveCompression);
                if (c.Components.Plates.Covers.Count != 8) missing.Add(id + ":EIGHT_COVER_ASSESSMENTS_REQUIRED");
                foreach (var issue in c.Components.Plates.Diagnostics) missing.Add(id + ":Plates:" + issue);
                Assess(id, "CombinedNetSections", c.AdditionalPlates.NetSections);
                // Optional groups are assessed only when they were enumerated; their role decides
                // whether a failure is a load failure or an unestablished sufficient domain.
                if (c.AdditionalPlates.BlockHierarchy.Checks.Count != 0 || c.AdditionalPlates.BlockHierarchy.Diagnostics.Count != 0)
                    Assess(id, "BlockHierarchy", c.AdditionalPlates.BlockHierarchy);
                if (c.AdditionalPlates.DirectBlockTearing.Checks.Count != 0 || c.AdditionalPlates.DirectBlockTearing.Diagnostics.Count != 0)
                    Assess(id, "DirectBlockTearing", c.AdditionalPlates.DirectBlockTearing);
                Assess(id, "CompactPlateDomain", c.AdditionalPlates.CompactPlateDomain);
                if (c.Components.NetAxialPlastic != null) Assess(id, "CategoryC.NetAxial", c.Components.NetAxialPlastic);
                Assess(id, "LongitudinalWelds", c.Parents.LongitudinalWelds);
                Parent("Left.Section", input.Parents.LeftSection); Parent("Right.Section", input.Parents.RightSection);
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
                Assess(slip.Input.Id, "Slip", slip.Assessment, slip.Input.Input.LimitState == BoltSlipLimitState.Serviceability);
            }
            if (p.Envelope.Category == SpliceBoltCategory.B && !envelope.SlipCases.Any(s => s.IsBound && s.Input.Input.LimitState == BoltSlipLimitState.Serviceability))
                missing.Add("CATEGORY_B_SERVICEABILITY_CASE_REQUIRED");
            if (p.Envelope.RequiresCategoryC) foreach (var c in p.Cases)
                if (!envelope.SlipCases.Any(s => s.IsBound && s.Input.Input.LimitState == BoltSlipLimitState.Ultimate && s.Input.UltimateCaseId == c.Id))
                    missing.Add(c.Id + ":CATEGORY_C_BOUND_ULTIMATE_SLIP_REQUIRED");
            return new WeldedIBeamSpliceStandardResult(p, cases, envelope.SlipCases, missing, sources, failed);

            void Assess<T>(string id, string component, ConnectionVerification<T> r, bool sls = false) where T : class
            {
                if (ConnectionOutcome.IsDomainPrerequisite(r.Role)) Prerequisite(id, component, r);
                else Include(id, component, r, sls);
            }
            void Prerequisite<T>(string id, string component, ConnectionVerification<T> r) where T : class
            {
                if (r.Status != ConnectionStatus.Passed)
                    missing.Add(id + ":" + component + ":SUFFICIENT_DOMAIN_NOT_ESTABLISHED");
                foreach (var issue in r.Diagnostics) missing.Add(id + ":" + component + ":" + issue);
                // A hierarchy/domain ratio is not an actual load utilization.
            }
            void Include<T>(string id, string component, ConnectionVerification<T> r, bool sls) where T : class
            {
                if (!r.IsComplete) missing.Add(id + ":" + component + ":INCOMPLETE_COMPONENT");
                foreach (var issue in r.Diagnostics) missing.Add(id + ":" + component + ":" + issue);
                foreach (var check in r.Checks) sources.Add(new WeldedSpliceStandardCheckSource(id, component, sls, check));
                if (r.Status == ConnectionStatus.Failed) failed = true;
            }
        }
    }
}
