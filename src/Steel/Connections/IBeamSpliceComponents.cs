using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One explicit ULS case plus zero/more explicit slip combinations
    /// of the SAME physical detail and code. Null requiredSlipLimitState means
    /// no slip check is requested, not that an unprovided preloaded category passes.
    /// Non-null requires at least one case at that state; no ULS/SLS conversion.</summary>
    public sealed class IBeamSpliceComponentInput
    {
        public IBeamSplicePlateInput Plates { get; }
        public BoltSlipLimitState? RequiredSlipLimitState { get; }
        public IReadOnlyList<IBeamSpliceBoltSlipInput> SlipCases { get; }
        public IBeamSpliceComponentInput(IBeamSplicePlateInput plates, BoltSlipLimitState? requiredSlipLimitState = null,
            IEnumerable<IBeamSpliceBoltSlipInput> slipCases = null)
        {
            Plates = plates ?? throw new ArgumentNullException(nameof(plates));
            if (requiredSlipLimitState.HasValue && !Enum.IsDefined(typeof(BoltSlipLimitState), requiredSlipLimitState.Value))
                throw new ArgumentOutOfRangeException(nameof(requiredSlipLimitState));
            RequiredSlipLimitState = requiredSlipLimitState;
            var cases = (slipCases ?? Enumerable.Empty<IBeamSpliceBoltSlipInput>()).ToArray();
            if (cases.Any(c => c == null)) throw new ArgumentException("Slip cases cannot contain null.", nameof(slipCases));
            SlipCases = Array.AsReadOnly(cases);
        }
    }

    /// <summary>Typed component results and their envelope, never a complete
    /// splice certification. Missing checks cannot be bypassed with a boolean
    /// supplied by a caller or with a successful bolt/slip component.</summary>
    public sealed class IBeamSpliceComponentResult
    {
        public ConnectionVerification<IBeamSpliceComponentInput> ComponentAssessment { get; }
        public ConnectionVerification<IBeamSpliceBoltStrengthInput> BoltStrength { get; }
        public ConnectionVerification<IBeamSplicePlateInput> Bearing { get; }
        public ConnectionVerification<IBeamSplicePlateInput> Flanges { get; }
        public ConnectionVerification<IBeamSplicePlateInput> WebSections { get; }
        public ConnectionVerification<IBeamSplicePlateInput> WebLongitudinal { get; }
        public IReadOnlyList<ConnectionVerification<IBeamSpliceBoltSlipInput>> SlipCases { get; }
        /// <summary>Category-C net-plastic flange checks for the primary ULS state
        /// followed by each separately supplied, validly bound ULS slip state.
        /// Empty when no category-C requirement is requested; never includes SLS.</summary>
        public IReadOnlyList<ConnectionVerification<IBeamSplicePlateInput>> FlangeNetPlasticCases { get; }
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<string> MissingWholeJointChecks { get; }
        internal IBeamSpliceComponentResult(ConnectionVerification<IBeamSpliceComponentInput> assessment,
            ConnectionVerification<IBeamSpliceBoltStrengthInput> strength, ConnectionVerification<IBeamSplicePlateInput> bearing,
            ConnectionVerification<IBeamSplicePlateInput> flanges, ConnectionVerification<IBeamSplicePlateInput> web,
            ConnectionVerification<IBeamSplicePlateInput> webLongitudinal,
            IEnumerable<ConnectionVerification<IBeamSpliceBoltSlipInput>> slip, bool ultimateSlip,
            IEnumerable<ConnectionVerification<IBeamSplicePlateInput>> flangeNetPlasticCases)
        {
            ComponentAssessment = assessment; BoltStrength = strength; Bearing = bearing; Flanges = flanges; WebSections = web;
            WebLongitudinal = webLongitudinal;
            SlipCases = Array.AsReadOnly(slip.ToArray());
            FlangeNetPlasticCases = Array.AsReadOnly(flangeNetPlasticCases.ToArray());
            var missing = new List<string> {
                "WEB_COVER_BLOCK_TEARING_UNDER_N_V_M", "WEB_COVER_LOCAL_BUCKLING",
                "MEMBER_CLASSIFICATION_AND_LOCAL_GLOBAL_RESISTANCE", "SINGLE_COVER_SECONDARY_BENDING_AND_LOAD_INTRODUCTION",
                "GLOBAL_MODEL_STIFFNESS_ROTATION_AND_CONTINUITY_REQUIREMENTS"
            };
            if (ultimateSlip)
            {
                // Retain the existing overall-gap identifier: only the flange part
                // is now covered. The web still requires a net-plastic N-V-M model.
                missing.Add("ULTIMATE_SLIP_CATEGORY_NET_PLASTIC_RESISTANCE");
                missing.Add("WEB_ULTIMATE_SLIP_NET_PLASTIC_N_V_M");
            }
            MissingWholeJointChecks = Array.AsReadOnly(missing.ToArray());
        }
    }

    /// <summary>Approximate SCI force distribution for rolled I/H splices, kept for compatibility. Shared
    /// kernels (bolt strength and bearing with betaLf, plastic shear, detail identity) are the same as in
    /// the qualified welded-I path; that path integrates the parent stresses exactly and is the one to use.</summary>
    [Obsolete("Approximate component envelope. Use BridgeSpliceApplication.CreateBolted -> WeldedIBeamSpliceStandard.Verify (exact integrals, complete standard typology).")]
    public static class IBeamSpliceComponents
    {
        public static IBeamSpliceComponentResult Verify(IBeamSpliceComponentInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            var plates = p.Plates; var code = plates.Policy.Code;
            var strength = IBeamSpliceBolts.VerifyStrength(new IBeamSpliceBoltStrengthInput(code, plates.Forces, plates.Bolts, plates.UltimateCombinationSource));
            var bearing = IBeamSplicePlates.VerifyBearing(plates);
            var flanges = IBeamSplicePlates.VerifyFlanges(plates);
            var web = IBeamSplicePlates.VerifyWebSections(plates);
            var webLongitudinal = IBeamSpliceWebLongitudinal.Verify(plates);
            Include("BoltStrength", strength, checks, trace, issues);
            Include("Bearing", bearing, checks, trace, issues);
            Include("Flanges", flanges, checks, trace, issues);
            Include("WebSections", web, checks, trace, issues);
            Include("WebLongitudinal", webLongitudinal, checks, trace, issues);
            if (p.RequiredSlipLimitState.HasValue && !p.SlipCases.Any(s => s.LimitState == p.RequiredSlipLimitState.Value))
                issues.Add("REQUIRED_SLIP_LIMIT_STATE_CASE_MISSING:" + p.RequiredSlipLimitState.Value);
            bool ultimateSlip = p.RequiredSlipLimitState == BoltSlipLimitState.Ultimate
                || p.SlipCases.Any(s => s.LimitState == BoltSlipLimitState.Ultimate);
            var netPlasticCases = new List<ConnectionVerification<IBeamSplicePlateInput>>();
            if (ultimateSlip) AddFlangeNetPlastic("PrimaryULS", plates);
            var slips = new List<ConnectionVerification<IBeamSpliceBoltSlipInput>>();
            foreach (var s in p.SlipCases)
            {
                var caseIssues = new List<string>();
                if (!SpliceIdentity.Code(code, s.Policy.Code)) caseIssues.Add("SLIP_CASE_DESIGN_CODE_MISMATCH");
                if (!SpliceIdentity.IBeamGeometry(plates.Forces, s.Forces) || !SpliceIdentity.IBeamBolts(plates.Bolts, s.Bolts))
                    caseIssues.Add("SLIP_CASE_PHYSICAL_DETAIL_MISMATCH");
                var result = caseIssues.Count == 0 ? IBeamSpliceBolts.VerifySlip(s)
                    : new ConnectionVerification<IBeamSpliceBoltSlipInput>(s, code, "Steel.Connection.IBeamSplice.InvalidSlipBinding",
                        "Slip case binding to the assessed physical detail and code.", Array.Empty<ConnectionCheck>(), Array.Empty<SectionResistanceValue>(), caseIssues);
                slips.Add(result); Include("SlipCase" + slips.Count, result, checks, trace, issues);
                if (s.LimitState == BoltSlipLimitState.Ultimate && caseIssues.Count == 0)
                {
                    // Reuse the SAME physical plates/materials with this complete
                    // concomitant action state and its own ULS source. Do not
                    // assume the primary case envelopes other explicit ULS cases.
                    var casePlates = new IBeamSplicePlateInput(plates.Policy, s.Forces, plates.Bolts,
                        plates.FlangeCover, plates.WebCover, plates.FlangeCoverEnd, plates.WebCoverEnd,
                        plates.ButtGap, plates.BeamFlangeFy, plates.BeamFlangeFu, plates.Exposed,
                        s.ActionCombinationSource);
                    AddFlangeNetPlastic("SlipCase" + slips.Count, casePlates);
                }
            }
            var assessment = new ConnectionVerification<IBeamSpliceComponentInput>(p, code, "Steel.Connection.IBeamSplice.Components",
                "COMPONENT ENVELOPE ONLY: ULS bolt strength, plate bearing, flange components, web section N-V-M and longitudinal web-cover buckling, "
                + "plus explicitly supplied slip cases and category-C flange net-plastic checks at every supplied ULS state. "
                + "Does not qualify the whole splice; read MissingWholeJointChecks.",
                checks, trace, issues, new[] {
                    "Primary strength components share one immutable ULS input. Slip demands belong to separately declared concomitant combinations; none are scaled or synthesized. Category-C flange net-plastic checks additionally cover each supplied ULS slip state with its own actions and source.",
                    "Physical geometry, bolt properties/hardware and design factors must agree exactly across cases; independently created equal snapshots are accepted.",
                    "Read each typed component's Scope, Assumptions and source trace. Their Passed envelope is not a whole-joint Passed.",
                    "WebLongitudinal checks only longitudinal compression between fasteners with its actual section-force envelope. WEB_COVER_LOCAL_BUCKLING remains open for transverse/shear and lateral modes; combined N-V-M block tearing is also still unqualified.",
                    "A required SLS/ULS slip check cannot be satisfied by a case at the other limit state. Category-C flange net-plastic resistance is checked for the primary ULS and every valid ULS slip case; category-B/SLS alone does not activate it. Web net-plastic N-V-M resistance remains unqualified."
                });
            return new IBeamSpliceComponentResult(assessment, strength, bearing, flanges, web, webLongitudinal, slips,
                ultimateSlip, netPlasticCases);

            void AddFlangeNetPlastic(string name, IBeamSplicePlateInput state)
            {
                var result = IBeamSplicePlates.VerifyFlangeNetPlastic(state);
                netPlasticCases.Add(result);
                Include("FlangeNetPlastic." + name, result, checks, trace, issues);
            }
        }

        private static void Include<T>(string name, ConnectionVerification<T> result, IList<ConnectionCheck> checks,
            IList<SectionResistanceValue> trace, IList<string> issues) where T : class
        {
            foreach (var d in result.Diagnostics) issues.Add(name + ":" + d);
            foreach (var c in result.Checks) checks.Add(c.WithId(name + "." + c.Id));
            foreach (var t in result.Trace) trace.Add(new SectionResistanceValue(name + "." + t.Name, t.Value, t.Unit));
        }
    }
}
