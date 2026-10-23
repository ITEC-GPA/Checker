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
        public IReadOnlyList<ConnectionVerification<IBeamSpliceBoltSlipInput>> SlipCases { get; }
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<string> MissingWholeJointChecks { get; }
        internal IBeamSpliceComponentResult(ConnectionVerification<IBeamSpliceComponentInput> assessment,
            ConnectionVerification<IBeamSpliceBoltStrengthInput> strength, ConnectionVerification<IBeamSplicePlateInput> bearing,
            ConnectionVerification<IBeamSplicePlateInput> flanges, ConnectionVerification<IBeamSplicePlateInput> web,
            IEnumerable<ConnectionVerification<IBeamSpliceBoltSlipInput>> slip, bool ultimateSlip)
        {
            ComponentAssessment = assessment; BoltStrength = strength; Bearing = bearing; Flanges = flanges; WebSections = web;
            SlipCases = Array.AsReadOnly(slip.ToArray());
            var missing = new List<string> {
                "WEB_COVER_BLOCK_TEARING_UNDER_N_V_M", "WEB_COVER_LOCAL_BUCKLING",
                "MEMBER_CLASSIFICATION_AND_LOCAL_GLOBAL_RESISTANCE", "SINGLE_COVER_SECONDARY_BENDING_AND_LOAD_INTRODUCTION",
                "GLOBAL_MODEL_STIFFNESS_ROTATION_AND_CONTINUITY_REQUIREMENTS"
            };
            if (ultimateSlip) missing.Add("ULTIMATE_SLIP_CATEGORY_NET_PLASTIC_RESISTANCE");
            MissingWholeJointChecks = Array.AsReadOnly(missing.ToArray());
        }
    }

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
            Include("BoltStrength", strength, checks, trace, issues);
            Include("Bearing", bearing, checks, trace, issues);
            Include("Flanges", flanges, checks, trace, issues);
            Include("WebSections", web, checks, trace, issues);
            if (p.RequiredSlipLimitState.HasValue && !p.SlipCases.Any(s => s.LimitState == p.RequiredSlipLimitState.Value))
                issues.Add("REQUIRED_SLIP_LIMIT_STATE_CASE_MISSING:" + p.RequiredSlipLimitState.Value);
            var slips = new List<ConnectionVerification<IBeamSpliceBoltSlipInput>>();
            foreach (var s in p.SlipCases)
            {
                var caseIssues = new List<string>();
                if (!SameCode(code, s.Policy.Code)) caseIssues.Add("SLIP_CASE_DESIGN_CODE_MISMATCH");
                if (!SameGeometry(plates.Forces, s.Forces) || !SameBolts(plates.Bolts, s.Bolts))
                    caseIssues.Add("SLIP_CASE_PHYSICAL_DETAIL_MISMATCH");
                var result = caseIssues.Count == 0 ? IBeamSpliceBolts.VerifySlip(s)
                    : new ConnectionVerification<IBeamSpliceBoltSlipInput>(s, code, "Steel.Connection.IBeamSplice.InvalidSlipBinding",
                        "Slip case binding to the assessed physical detail and code.", Array.Empty<ConnectionCheck>(), Array.Empty<SectionResistanceValue>(), caseIssues);
                slips.Add(result); Include("SlipCase" + slips.Count, result, checks, trace, issues);
            }
            var assessment = new ConnectionVerification<IBeamSpliceComponentInput>(p, code, "Steel.Connection.IBeamSplice.Components",
                "COMPONENT ENVELOPE ONLY: ULS bolt strength, plate bearing, flange components and web section N-V-M, "
                + "plus explicitly supplied slip cases of this same detail. Does not qualify the whole splice; read MissingWholeJointChecks.",
                checks, trace, issues, new[] {
                    "All strength components share one immutable ULS input. Slip demands belong to separately declared concomitant combinations; none are scaled or synthesized.",
                    "Physical geometry, bolt properties/hardware and design factors must agree exactly across cases; independently created equal snapshots are accepted.",
                    "Read each typed component's Scope, Assumptions and source trace. Their Passed envelope is not a whole-joint Passed.",
                    "A required SLS/ULS slip check cannot be satisfied by a case at the other limit state. Ultimate slip alone does not qualify the net-plastic checks of a complete category C connection."
                });
            return new IBeamSpliceComponentResult(assessment, strength, bearing, flanges, web, slips,
                p.RequiredSlipLimitState == BoltSlipLimitState.Ultimate || p.SlipCases.Any(s => s.LimitState == BoltSlipLimitState.Ultimate));
        }

        private static void Include<T>(string name, ConnectionVerification<T> result, IList<ConnectionCheck> checks,
            IList<SectionResistanceValue> trace, IList<string> issues) where T : class
        {
            foreach (var d in result.Diagnostics) issues.Add(name + ":" + d);
            foreach (var c in result.Checks) checks.Add(new ConnectionCheck(name + "." + c.Id, c.Demand, c.Resistance, c.Reference, c.Unit));
            foreach (var t in result.Trace) trace.Add(new SectionResistanceValue(name + "." + t.Name, t.Value, t.Unit));
        }
        private static bool SameCode(ConnectionDesignCode a, ConnectionDesignCode b) => a.IsNtc == b.IsNtc
            && a.Plates.GammaM0 == b.Plates.GammaM0 && a.Plates.GammaM2 == b.Plates.GammaM2 && a.GammaM2Connection == b.GammaM2Connection;
        private static bool SameGeometry(IBeamSpliceForceInput a, IBeamSpliceForceInput b)
        {
            var x = a.Section; var y = b.Section;
            return a.StrongAxisSecondMoment == b.StrongAxisSecondMoment && x.Height == y.Height && x.Width == y.Width
                && x.WebThickness == y.WebThickness && x.FlangeThickness == y.FlangeThickness && x.RootRadius == y.RootRadius
                && x.Area == y.Area && x.Fy == y.Fy && x.Fu == y.Fu && SameGrid(a.FlangeBolts, b.FlangeBolts) && SameGrid(a.WebBolts, b.WebBolts);
        }
        private static bool SameGrid(SpliceBoltLayout a, SpliceBoltLayout b) => a.LongitudinalRows == b.LongitudinalRows
            && a.TransverseRows == b.TransverseRows && a.LongitudinalPitch == b.LongitudinalPitch
            && a.TransversePitch == b.TransversePitch && a.FirstBoltDistance == b.FirstBoltDistance;
        private static bool SameBolts(IBeamSpliceBoltSet a, IBeamSpliceBoltSet b) => SameBolt(a.FlangeBolt, b.FlangeBolt)
            && SameBolt(a.WebBolt, b.WebBolt) && a.FlangeHardwareEnvelopeDiameter == b.FlangeHardwareEnvelopeDiameter
            && a.WebHardwareEnvelopeDiameter == b.WebHardwareEnvelopeDiameter;
        private static bool SameBolt(ConnectionBolt a, ConnectionBolt b) => a.Diameter == b.Diameter && a.HoleDiameter == b.HoleDiameter
            && a.TensileArea == b.TensileArea && a.Grade == b.Grade && a.ThreadedShearPlanes == b.ThreadedShearPlanes;
    }
}
