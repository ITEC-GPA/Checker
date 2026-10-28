using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One complete concomitant ULS state, with a caller-defined ordinal
    /// identifier. Plates, actions, application point and provenance are retained.</summary>
    public sealed class IBeamSpliceUltimateCase
    {
        public string Id { get; }
        public IBeamSplicePlateInput Plates { get; }
        public IBeamSpliceUltimateCase(string id, IBeamSplicePlateInput plates)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A nonempty case identifier is required.", nameof(id));
            Id = id; Plates = plates ?? throw new ArgumentNullException(nameof(plates));
        }
    }

    /// <summary>A separately supplied slip state. An ULS slip must name exactly
    /// one UltimateCaseId with identical actions AND application point. An SLS
    /// slip has no ULS binding. Missing/wrong bindings produce diagnostics.</summary>
    public sealed class IBeamSpliceSlipCase
    {
        public string Id { get; }
        public IBeamSpliceBoltSlipInput Input { get; }
        public string UltimateCaseId { get; }
        public IBeamSpliceSlipCase(string id, IBeamSpliceBoltSlipInput input, string ultimateCaseId = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A nonempty case identifier is required.", nameof(id));
            Id = id; Input = input ?? throw new ArgumentNullException(nameof(input)); UltimateCaseId = ultimateCaseId;
        }
    }

    /// <summary>All supplied cases of ONE physical splice and policy. Dimensions
    /// mm, strengths MPa, forces N, moments Nmm. Ids are globally unique using
    /// StringComparer.Ordinal. No independent maxima of N/V/M are synthesized.
    /// Category C is requested by RequiredSlipLimitState=Ultimate OR any ULS slip;
    /// it requires one explicitly bound ULS slip for every supplied ULS case.</summary>
    public sealed class IBeamSpliceEnvelopeInput
    {
        public IReadOnlyList<IBeamSpliceUltimateCase> UltimateCases { get; }
        public IReadOnlyList<IBeamSpliceSlipCase> SlipCases { get; }
        public BoltSlipLimitState? RequiredSlipLimitState { get; }
        public bool RequiresCategoryC => RequiredSlipLimitState == BoltSlipLimitState.Ultimate
            || SlipCases.Any(s => s.Input.LimitState == BoltSlipLimitState.Ultimate);
        public IBeamSpliceEnvelopeInput(IEnumerable<IBeamSpliceUltimateCase> ultimateCases,
            BoltSlipLimitState? requiredSlipLimitState = null, IEnumerable<IBeamSpliceSlipCase> slipCases = null)
        {
            if (ultimateCases == null) throw new ArgumentNullException(nameof(ultimateCases));
            if (requiredSlipLimitState.HasValue && !Enum.IsDefined(typeof(BoltSlipLimitState), requiredSlipLimitState.Value))
                throw new ArgumentOutOfRangeException(nameof(requiredSlipLimitState));
            var uls = ultimateCases.ToArray(); var slips = (slipCases ?? Enumerable.Empty<IBeamSpliceSlipCase>()).ToArray();
            if (uls.Length == 0 || uls.Any(c => c == null)) throw new ArgumentException("At least one non-null ULS case is required.", nameof(ultimateCases));
            if (slips.Any(c => c == null)) throw new ArgumentException("Slip cases cannot contain null.", nameof(slipCases));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in uls.Select(c => c.Id).Concat(slips.Select(c => c.Id)))
                if (!ids.Add(id)) throw new ArgumentException("Case identifiers must be globally unique with ordinal comparison.");
            UltimateCases = Array.AsReadOnly(uls); SlipCases = Array.AsReadOnly(slips);
            RequiredSlipLimitState = requiredSlipLimitState;
        }
    }

    /// <summary>The original results calculated once for one ULS case. A failed
    /// binding never deletes a failure in that case's independently evaluated
    /// components. Status covers these components/binding, not the whole joint.</summary>
    public sealed class IBeamSpliceUltimateCaseResult
    {
        public IBeamSpliceUltimateCase Input { get; }
        public IBeamSpliceComponentResult Components { get; }
        public ConnectionVerification<IBeamSplicePlateInput> FlangeNetPlastic { get; }
        public IReadOnlyList<string> BindingDiagnostics { get; }
        public bool IsBound => BindingDiagnostics.Count == 0;
        public bool HasFailedComponent { get; }
        public bool IsComplete { get; }
        public ConnectionStatus Status { get; }
        internal IBeamSpliceUltimateCaseResult(IBeamSpliceUltimateCase input, IBeamSpliceComponentResult components,
            ConnectionVerification<IBeamSplicePlateInput> net, IEnumerable<string> bindingDiagnostics)
        {
            Input = input; Components = components; FlangeNetPlastic = net;
            BindingDiagnostics = Array.AsReadOnly(bindingDiagnostics.ToArray());
            HasFailedComponent = components.ComponentAssessment.Status == ConnectionStatus.Failed
                || components.BoltStrength.Status == ConnectionStatus.Failed || components.Bearing.Status == ConnectionStatus.Failed
                || components.Flanges.Status == ConnectionStatus.Failed || components.WebSections.Status == ConnectionStatus.Failed
                || components.WebLongitudinal.Status == ConnectionStatus.Failed || net?.Status == ConnectionStatus.Failed;
            // A known failure wins over a failed binding or an incomplete component.
            IsComplete = IsBound && components.ComponentAssessment.IsComplete && (net == null || net.IsComplete);
            Status = HasFailedComponent ? ConnectionStatus.Failed : IsComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        }
    }

    /// <summary>Original slip result and separate binding evidence. A result for
    /// another detail remains inspectable, but cannot qualify this envelope.</summary>
    public sealed class IBeamSpliceSlipCaseResult
    {
        public IBeamSpliceSlipCase Input { get; }
        public ConnectionVerification<IBeamSpliceBoltSlipInput> Assessment { get; }
        public IReadOnlyList<string> BindingDiagnostics { get; }
        public bool IsBound => BindingDiagnostics.Count == 0;
        public bool HasFailedComponent => Assessment.Status == ConnectionStatus.Failed;
        public bool IsComplete => IsBound && Assessment.IsComplete;
        public ConnectionStatus Status => HasFailedComponent ? ConnectionStatus.Failed : IsComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        internal IBeamSpliceSlipCaseResult(IBeamSpliceSlipCase input,
            ConnectionVerification<IBeamSpliceBoltSlipInput> assessment, IEnumerable<string> diagnostics)
        { Input = input; Assessment = assessment; BindingDiagnostics = Array.AsReadOnly(diagnostics.ToArray()); }
    }

    /// <summary>Links one aggregate check to its ORIGINAL immutable leaf check.
    /// IsServiceability distinguishes SLS from ULS without parsing an Id. A slip
    /// source uses its slip Id, and LinkedUltimateCaseId identifies its ULS case.</summary>
    public sealed class IBeamSpliceEnvelopeCheckSource
    {
        public string AggregateCheckId { get; }
        public string CaseId { get; }
        public string Component { get; }
        public bool IsServiceability { get; }
        public string LinkedUltimateCaseId { get; }
        public ConnectionCheck SourceCheck { get; }
        internal IBeamSpliceEnvelopeCheckSource(string aggregateId, string caseId, string component,
            bool serviceability, string linkedUltimateCaseId, ConnectionCheck sourceCheck)
        {
            AggregateCheckId = aggregateId; CaseId = caseId; Component = component;
            IsServiceability = serviceability; LinkedUltimateCaseId = linkedUltimateCaseId; SourceCheck = sourceCheck;
        }
    }

    /// <summary>Envelope of supplied, concomitant cases for qualified COMPONENTS
    /// only. NS suppresses favourable aggregate checks/ratios; every original
    /// case and known failure remains available. It never qualifies a full joint
    /// or proves that the caller supplied every necessary structural combination.</summary>
    public sealed class IBeamSpliceEnvelopeResult
    {
        public ConnectionVerification<IBeamSpliceEnvelopeInput> ComponentAssessment { get; }
        public IReadOnlyList<IBeamSpliceUltimateCaseResult> UltimateCases { get; }
        public IReadOnlyList<IBeamSpliceSlipCaseResult> SlipCases { get; }
        public IReadOnlyList<IBeamSpliceSlipCaseResult> UltimateSlipCases { get; }
        public IReadOnlyList<IBeamSpliceSlipCaseResult> ServiceabilityCases { get; }
        /// <summary>Qualified aggregate sources only; empty on NS. Original leaf
        /// checks remain in the typed case results even when this list is empty.</summary>
        public IReadOnlyList<IBeamSpliceEnvelopeCheckSource> CheckSources { get; }
        public IBeamSpliceEnvelopeCheckSource GoverningSource { get; }
        public string GoverningCaseId => GoverningSource?.CaseId;
        public ConnectionCheck GoverningSourceCheck => GoverningSource?.SourceCheck;
        public bool HasFailedComponent { get; }
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<string> MissingWholeJointChecks { get; }
        internal IBeamSpliceEnvelopeResult(ConnectionVerification<IBeamSpliceEnvelopeInput> assessment,
            IEnumerable<IBeamSpliceUltimateCaseResult> ultimate, IEnumerable<IBeamSpliceSlipCaseResult> slips,
            IEnumerable<IBeamSpliceEnvelopeCheckSource> sources)
        {
            ComponentAssessment = assessment; UltimateCases = Array.AsReadOnly(ultimate.ToArray());
            SlipCases = Array.AsReadOnly(slips.ToArray());
            UltimateSlipCases = Array.AsReadOnly(SlipCases.Where(c => c.Input.Input.LimitState == BoltSlipLimitState.Ultimate).ToArray());
            ServiceabilityCases = Array.AsReadOnly(SlipCases.Where(c => c.Input.Input.LimitState == BoltSlipLimitState.Serviceability).ToArray());
            CheckSources = Array.AsReadOnly(assessment.IsComplete ? sources.ToArray() : Array.Empty<IBeamSpliceEnvelopeCheckSource>());
            // Both sorts are stable and use the same insertion order: ties preserve
            // caller ULS order, component order, then supplied slip order.
            GoverningSource = assessment.IsComplete ? CheckSources.OrderByDescending(s => s.SourceCheck.Utilization).First() : null;
            HasFailedComponent = UltimateCases.Any(c => c.HasFailedComponent) || SlipCases.Any(c => c.HasFailedComponent);
            var missing = UltimateCases.SelectMany(c => c.Components.MissingWholeJointChecks).ToList();
            if (assessment.Input.RequiresCategoryC)
            {
                // Strength-only child calls intentionally do not set category C.
                // Preserve BOTH gaps even when a binding or required case is NS.
                missing.Add("ULTIMATE_SLIP_CATEGORY_NET_PLASTIC_RESISTANCE");
                missing.Add("WEB_ULTIMATE_SLIP_NET_PLASTIC_N_V_M");
            }
            MissingWholeJointChecks = Array.AsReadOnly(missing.Distinct(StringComparer.Ordinal).ToArray());
        }
    }

    public static class IBeamSpliceEnvelope
    {
        public const string MethodId = "Steel.Connection.IBeamSplice.ConcomitantCaseEnvelope";
        public static IBeamSpliceEnvelopeResult Verify(IBeamSpliceEnvelopeInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>(); var sources = new List<IBeamSpliceEnvelopeCheckSource>();
            var ultimate = new List<IBeamSpliceUltimateCaseResult>(); var slips = new List<IBeamSpliceSlipCaseResult>();
            var anchor = p.UltimateCases[0].Plates;
            var byId = p.UltimateCases.ToDictionary(c => c.Id, StringComparer.Ordinal);
            bool categoryC = p.RequiresCategoryC;
            if (p.RequiredSlipLimitState == BoltSlipLimitState.Serviceability
                && !p.SlipCases.Any(s => s.Input.LimitState == BoltSlipLimitState.Serviceability))
                issues.Add("REQUIRED_SERVICEABILITY_SLIP_CASE_MISSING");

            for (int i = 0; i < p.UltimateCases.Count; i++)
            {
                var item = p.UltimateCases[i]; var plates = item.Plates; string prefix = "ULS" + (i + 1);
                var binding = new List<string>();
                if (!SamePlatePolicy(anchor.Policy, plates.Policy)) binding.Add("ULTIMATE_CASE_PLATE_POLICY_MISMATCH");
                if (!SamePlateDetail(anchor, plates)) binding.Add("ULTIMATE_CASE_PHYSICAL_DETAIL_MISMATCH");
                if (categoryC)
                {
                    int count = p.SlipCases.Count(s => s.Input.LimitState == BoltSlipLimitState.Ultimate
                        && StringComparer.Ordinal.Equals(s.UltimateCaseId, item.Id));
                    if (count != 1) binding.Add(count == 0 ? "CATEGORY_C_BOUND_ULTIMATE_SLIP_MISSING" : "CATEGORY_C_DUPLICATE_ULTIMATE_SLIP_BINDING");
                }
                // Every complete ULS is calculated in its OWN unchanged input,
                // even if binding to the common detail failed. No early return.
                var components = IBeamSpliceComponents.Verify(new IBeamSpliceComponentInput(plates));
                var net = categoryC ? IBeamSplicePlates.VerifyFlangeNetPlastic(plates) : null;
                ultimate.Add(new IBeamSpliceUltimateCaseResult(item, components, net, binding));
                foreach (string d in binding) issues.Add(prefix + ":" + d);
                foreach (string d in components.ComponentAssessment.Diagnostics) issues.Add(prefix + ":" + d);
                if (components.ComponentAssessment.Status == ConnectionStatus.NotSupported
                    && components.ComponentAssessment.Diagnostics.Count == 0) issues.Add(prefix + ":COMPONENTS_NOT_SUPPORTED");
                // Leaf checks only. Do not append ComponentAssessment as well as
                // these children, or primary/net/slip checks would be duplicated.
                Include(prefix, item.Id, "BoltStrength", false, null, components.BoltStrength);
                Include(prefix, item.Id, "Bearing", false, null, components.Bearing);
                Include(prefix, item.Id, "Flanges", false, null, components.Flanges);
                Include(prefix, item.Id, "WebSections", false, null, components.WebSections);
                Include(prefix, item.Id, "WebLongitudinal", false, null, components.WebLongitudinal);
                if (net != null) Include(prefix, item.Id, "FlangeNetPlastic", false, null, net);
            }

            var slipAnchor = p.SlipCases.FirstOrDefault()?.Input;
            int ultimateSlipIndex = 0, serviceabilityIndex = 0;
            foreach (var item in p.SlipCases)
            {
                var s = item.Input; var binding = new List<string>();
                bool serviceability = s.LimitState == BoltSlipLimitState.Serviceability;
                string prefix = serviceability ? "SLS" + (++serviceabilityIndex) : "SlipULS" + (++ultimateSlipIndex);
                if (!SpliceIdentity.Code(anchor.Policy.Code, s.Policy.Code)) binding.Add("SLIP_CASE_DESIGN_CODE_MISMATCH");
                if (!SpliceIdentity.IBeamGeometry(anchor.Forces, s.Forces) || !SpliceIdentity.IBeamBolts(anchor.Bolts, s.Bolts))
                    binding.Add("SLIP_CASE_PHYSICAL_DETAIL_MISMATCH");
                if (!SameSlipPolicyAndSurface(slipAnchor, s)) binding.Add("SLIP_POLICY_SURFACE_OR_TIGHTENING_MISMATCH");
                if (serviceability)
                {
                    if (item.UltimateCaseId != null) binding.Add("SERVICEABILITY_CASE_MUST_NOT_BIND_TO_ULTIMATE");
                }
                else
                {
                    IBeamSpliceUltimateCase linked;
                    if (item.UltimateCaseId == null || !byId.TryGetValue(item.UltimateCaseId, out linked))
                        binding.Add("ULTIMATE_SLIP_CASE_BINDING_MISSING_OR_UNKNOWN");
                    else if (!SpliceIdentity.Actions(linked.Plates.Forces.Actions, s.Forces.Actions))
                        binding.Add("ULTIMATE_SLIP_CONCOMITANT_ACTIONS_OR_APPLICATION_POINT_MISMATCH");
                }
                var result = IBeamSpliceBolts.VerifySlip(s);
                slips.Add(new IBeamSpliceSlipCaseResult(item, result, binding));
                foreach (string d in binding) issues.Add(prefix + ":" + d);
                Include(prefix, item.Id, "Slip", serviceability, item.UltimateCaseId, result);
            }

            var assessment = new ConnectionVerification<IBeamSpliceEnvelopeInput>(p, anchor.Policy.Code, MethodId,
                "Envelope of every supplied concomitant ULS strength case, explicit SLS/ULS slip and category-C flange net plastic checks. "
                + "COMPONENTS ONLY: no synthesis of separate N/V/M maxima, no automatic SLS/ULS conversion and no whole-joint qualification.",
                checks, trace, issues.Distinct(StringComparer.Ordinal), new[] {
                    "Each complete ULS action tuple and its physical application point are preserved. The wrapper reuses existing qualified components without new resistance equations.",
                    "All cases describe one identical physical splice, material set and policy, including gammaM1. Slip policy, surfaces and tightening are also common; exact comparisons do not absorb changed engineering inputs.",
                    "Category C requires one explicitly bound ULS slip per supplied ULS; its flange net-plastic check is evaluated once per ULS, never again for the same slip link. SLS never activates net-plastic strength.",
                    "Independent results remain visible after binding errors or other NS cases. A Failed child is retained even when its parent summary is NS. A result for a mismatched detail does not qualify the common detail.",
                    "Aggregate checks are leaf checks only; each has a case/component mapping to the original immutable check. Ties follow input ULS order, component order, then slip order.",
                    "This envelope covers only cases supplied by the caller. It neither generates structural combinations nor establishes that the supplied set is exhaustive.",
                    "All residual member, block tearing, stability, secondary bending and global-model limitations remain; category-C web net-plastic N-V-M is still unqualified, including on NS bindings."
                });
            return new IBeamSpliceEnvelopeResult(assessment, ultimate, slips, sources);

            void Include<T>(string prefix, string id, string component, bool serviceability, string linkedId,
                ConnectionVerification<T> child) where T : class
            {
                foreach (string d in child.Diagnostics) issues.Add(prefix + "." + component + ":" + d);
                if (child.Status == ConnectionStatus.NotSupported && child.Diagnostics.Count == 0)
                    issues.Add(prefix + "." + component + ":COMPONENT_NOT_SUPPORTED");
                foreach (var value in child.Trace) trace.Add(new SectionResistanceValue(prefix + "." + component + "." + value.Name, value.Value, value.Unit));
                foreach (var check in child.Checks)
                {
                    string aggregateId = prefix + "." + component + "." + check.Id;
                    checks.Add(check.WithId(aggregateId));
                    sources.Add(new IBeamSpliceEnvelopeCheckSource(aggregateId, id, component, serviceability, linkedId, check));
                }
            }
        }

        private static bool SamePlatePolicy(IBeamSplicePlatePolicy a, IBeamSplicePlatePolicy b)
            => ReferenceEquals(a, b) || SpliceIdentity.Code(a.Code, b.Code) && a.GammaM1 == b.GammaM1 && a.ElasticModulus == b.ElasticModulus;
        private static bool SamePlateDetail(IBeamSplicePlateInput a, IBeamSplicePlateInput b)
            => SpliceIdentity.IBeamGeometry(a.Forces, b.Forces) && SpliceIdentity.IBeamBolts(a.Bolts, b.Bolts)
                && SpliceIdentity.Plate(a.FlangeCover, b.FlangeCover) && SpliceIdentity.Plate(a.WebCover, b.WebCover)
                && a.FlangeCoverEnd == b.FlangeCoverEnd && a.WebCoverEnd == b.WebCoverEnd && a.ButtGap == b.ButtGap
                && a.BeamFlangeFy == b.BeamFlangeFy && a.BeamFlangeFu == b.BeamFlangeFu && a.Exposed == b.Exposed;
        private static bool SameSlipPolicyAndSurface(IBeamSpliceBoltSlipInput a, IBeamSpliceBoltSlipInput b)
            => SpliceIdentity.Code(a.Policy.Code, b.Policy.Code) && a.Policy.GammaM3Ultimate == b.Policy.GammaM3Ultimate
                && a.Policy.GammaM3Serviceability == b.Policy.GammaM3Serviceability
                && a.FlangeFrictionCoefficient == b.FlangeFrictionCoefficient && a.WebFrictionCoefficient == b.WebFrictionCoefficient
                && a.TighteningControl == b.TighteningControl;
    }
}
