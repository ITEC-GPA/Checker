using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    public enum SpliceBoltCategory { A, B, C }
    public sealed class WeldedSpliceUltimateCase
    {
        public string Id { get; }
        public WeldedIBeamSpliceDetailInput Detail { get; }
        public WeldedSpliceUltimateCase(string id, WeldedIBeamSpliceDetailInput detail)
        { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Case identifier required.", nameof(id)); Id = id; Detail = detail ?? throw new ArgumentNullException(nameof(detail)); }
    }
    public sealed class WeldedSpliceSlipCase
    {
        public string Id { get; }
        public WeldedIBeamSpliceSlipInput Input { get; }
        public string UltimateCaseId { get; }
        public WeldedSpliceSlipCase(string id, WeldedIBeamSpliceSlipInput input, string ultimateCaseId = null)
        { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Case identifier required.", nameof(id)); Id = id; Input = input ?? throw new ArgumentNullException(nameof(input)); UltimateCaseId = ultimateCaseId; }
    }
    public sealed class WeldedIBeamSpliceEnvelopeInput
    {
        public IReadOnlyList<WeldedSpliceUltimateCase> UltimateCases { get; }
        public IReadOnlyList<WeldedSpliceSlipCase> SlipCases { get; }
        public SpliceBoltCategory Category { get; }
        public bool RequiresCategoryC => Category == SpliceBoltCategory.C || SlipCases.Any(s => s.Input.LimitState == BoltSlipLimitState.Ultimate);
        public WeldedIBeamSpliceEnvelopeInput(IEnumerable<WeldedSpliceUltimateCase> ultimateCases,
            SpliceBoltCategory category, IEnumerable<WeldedSpliceSlipCase> slipCases = null)
        {
            if (ultimateCases == null) throw new ArgumentNullException(nameof(ultimateCases));
            if (!Enum.IsDefined(typeof(SpliceBoltCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            var u = ultimateCases.ToArray(); var s = (slipCases ?? Enumerable.Empty<WeldedSpliceSlipCase>()).ToArray();
            if (u.Length == 0 || u.Any(x => x == null) || s.Any(x => x == null)) throw new ArgumentException("At least one ULS case and no null cases required.");
            var ids = u.Select(x => x.Id).Concat(s.Select(x => x.Id)).ToArray();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new ArgumentException("Globally unique ordinal case identifiers required.");
            UltimateCases = Array.AsReadOnly(u); SlipCases = Array.AsReadOnly(s); Category = category;
        }
    }
    public sealed class WeldedSpliceUltimateCaseResult
    {
        public WeldedSpliceUltimateCase Input { get; }
        public WeldedIBeamSpliceBoltResult Bolts { get; }
        public WeldedIBeamSplicePlateResult Plates { get; }
        public WeldedIBeamSpliceDetailResult Detailing { get; }
        public ConnectionVerification<WeldedIBeamSplicePlateInput> NetAxialPlastic { get; }
        public IReadOnlyList<string> BindingDiagnostics { get; }
        public bool IsComplete => BindingDiagnostics.Count == 0 && Bolts.ComponentAssessment.IsComplete && Detailing.Detailing.IsComplete
            && Plates.Diagnostics.Count == 0 && Plates.Covers.Count == 8
            && Plates.Covers.All(c => c.Sections.IsComplete && c.LongitudinalCompression.IsComplete && c.AxialBlockTearing.IsComplete)
            && (NetAxialPlastic == null || NetAxialPlastic.IsComplete);
        public bool HasFailedComponent => Bolts.ComponentAssessment.Status == ConnectionStatus.Failed || Plates.HasFailedComponent
            || Detailing.Detailing.Status == ConnectionStatus.Failed || NetAxialPlastic?.Status == ConnectionStatus.Failed;
        internal WeldedSpliceUltimateCaseResult(WeldedSpliceUltimateCase input, bool categoryC, IEnumerable<string> issues)
        {
            Input = input; BindingDiagnostics = Array.AsReadOnly(issues.ToArray());
            Bolts = WeldedIBeamSpliceBolts.Verify(input.Detail.Plates.Bolts); Plates = WeldedIBeamSplicePlates.Verify(input.Detail.Plates, Bolts);
            Detailing = WeldedIBeamSpliceDetailing.Verify(input.Detail, Plates);
            NetAxialPlastic = categoryC ? WeldedIBeamSpliceSlip.VerifyNetAxialPlastic(input.Detail.Plates, Plates) : null;
        }
    }
    public sealed class WeldedSpliceSlipCaseResult
    {
        public WeldedSpliceSlipCase Input { get; }
        public ConnectionVerification<WeldedIBeamSpliceSlipInput> Assessment { get; }
        public IReadOnlyList<string> BindingDiagnostics { get; }
        public bool IsBound => BindingDiagnostics.Count == 0;
        internal WeldedSpliceSlipCaseResult(WeldedSpliceSlipCase input, IEnumerable<string> issues)
        { Input = input; BindingDiagnostics = Array.AsReadOnly(issues.ToArray()); Assessment = WeldedIBeamSpliceSlip.Verify(input.Input); }
    }
    public sealed class WeldedSpliceCheckSource
    {
        public string CaseId { get; }
        public string Component { get; }
        public bool IsServiceability { get; }
        public string UltimateCaseId { get; }
        public ConnectionCheck Check { get; }
        internal WeldedSpliceCheckSource(string id, string component, bool sls, string ultimate, ConnectionCheck check)
        { CaseId = id; Component = component; IsServiceability = sls; UltimateCaseId = ultimate; Check = check; }
    }
    public sealed class WeldedIBeamSpliceEnvelopeResult
    {
        public WeldedIBeamSpliceEnvelopeInput Input { get; }
        public IReadOnlyList<WeldedSpliceUltimateCaseResult> UltimateCases { get; }
        public IReadOnlyList<WeldedSpliceSlipCaseResult> SlipCases { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<WeldedSpliceCheckSource> CheckSources { get; }
        public IReadOnlyList<string> PendingWholeJointChecks { get; }
        public bool CanQualifyWholeJoint => false;
        public bool HasFailedComponent => UltimateCases.Any(c => c.HasFailedComponent) || SlipCases.Any(c => c.Assessment.Status == ConnectionStatus.Failed);
        public bool IsComponentEnvelopeComplete { get; }
        public ConnectionStatus ComponentStatus => !IsComponentEnvelopeComplete ? ConnectionStatus.NotSupported : HasFailedComponent ? ConnectionStatus.Failed : ConnectionStatus.Passed;
        public WeldedSpliceCheckSource GoverningSource => IsComponentEnvelopeComplete ? CheckSources.OrderByDescending(s => s.Check.Utilization).FirstOrDefault() : null;
        internal WeldedIBeamSpliceEnvelopeResult(WeldedIBeamSpliceEnvelopeInput input, IEnumerable<WeldedSpliceUltimateCaseResult> u,
            IEnumerable<WeldedSpliceSlipCaseResult> s, IEnumerable<string> issues, IEnumerable<WeldedSpliceCheckSource> checks)
        {
            Input = input; UltimateCases = Array.AsReadOnly(u.ToArray()); SlipCases = Array.AsReadOnly(s.ToArray());
            Diagnostics = Array.AsReadOnly(issues.Distinct().ToArray());
            IsComponentEnvelopeComplete = Diagnostics.Count == 0 && UltimateCases.All(c => c.IsComplete) && SlipCases.All(c => c.IsBound && c.Assessment.IsComplete);
            CheckSources = Array.AsReadOnly(IsComponentEnvelopeComplete ? checks.ToArray() : Array.Empty<WeldedSpliceCheckSource>());
            PendingWholeJointChecks = Array.AsReadOnly(new[] {
                "MIXED_STRESS_NET_SECTION_AND_BLOCK_FRACTURE", "COVER_SHEAR_AND_LATERAL_PLATE_STABILITY",
                "SECONDARY_BENDING_CONTACT_PRYING_AND_FORCE_INTRODUCTION", "LONGITUDINAL_MEMBER_WELDS",
                "PARENT_MEMBER_SECTION_LOCAL_GLOBAL_STABILITY", "GLOBAL_CONTINUITY_STIFFNESS_AND_SECOND_ORDER_ACTIONS"
            }.Concat(input.RequiresCategoryC ? new[] { "CATEGORY_C_COMBINED_NET_PLASTIC_NVM" } : Array.Empty<string>()).ToArray());
        }
    }

    /// <summary>Typed component envelope of ONE physical splice under all supplied
    /// concomitant ULS/SLS cases. Neither global force maxima nor missing SLS are synthesized.</summary>
    public static class WeldedIBeamSpliceEnvelope
    {
        public static WeldedIBeamSpliceEnvelopeResult Verify(WeldedIBeamSpliceEnvelopeInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var ultimate = new List<WeldedSpliceUltimateCaseResult>(); var slips = new List<WeldedSpliceSlipCaseResult>();
            var issues = new List<string>(); var sources = new List<WeldedSpliceCheckSource>();
            var reference = p.UltimateCases[0].Detail;
            foreach (var c in p.UltimateCases)
            {
                var binding = new List<string>();
                if (!SamePhysical(reference.Plates.Bolts, c.Detail.Plates.Bolts)) binding.Add("ULTIMATE_PHYSICAL_DETAIL_OR_CODE_MISMATCH");
                if (reference.Exposure != c.Detail.Exposure || reference.Plates.Policy.GammaM1 != c.Detail.Plates.Policy.GammaM1)
                    binding.Add("ULTIMATE_EXPOSURE_OR_BUCKLING_POLICY_MISMATCH");
                var r = new WeldedSpliceUltimateCaseResult(c, p.RequiresCategoryC, binding); ultimate.Add(r);
                foreach (var issue in binding) issues.Add(c.Id + ":" + issue);
                Include(c.Id, "Bolts", r.Bolts.ComponentAssessment);
                Include(c.Id, "Detailing", r.Detailing.Detailing);
                foreach (var cover in r.Plates.Covers)
                { Include(c.Id, cover.Cover + ".Sections", cover.Sections); Include(c.Id, cover.Cover + ".Compression", cover.LongitudinalCompression); Include(c.Id, cover.Cover + ".Block", cover.AxialBlockTearing); }
                foreach (var issue in r.Plates.Diagnostics) issues.Add(c.Id + ":" + issue);
                if (r.NetAxialPlastic != null) Include(c.Id, "NetAxialPlastic", r.NetAxialPlastic);
            }
            foreach (var c in p.SlipCases)
            {
                var binding = new List<string>();
                if (!SamePhysical(reference.Plates.Bolts, c.Input.Bolts)) binding.Add("SLIP_PHYSICAL_DETAIL_OR_CODE_MISMATCH");
                if (p.SlipCases.Count > 0 && !SameSlipExecution(p.SlipCases[0].Input, c.Input)) binding.Add("SLIP_SURFACE_ASSEMBLY_OR_FACTOR_MISMATCH");
                if (c.Input.LimitState == BoltSlipLimitState.Ultimate)
                {
                    var linked = p.UltimateCases.SingleOrDefault(u => string.Equals(u.Id, c.UltimateCaseId, StringComparison.Ordinal));
                    if (linked == null) binding.Add("LINKED_ULTIMATE_CASE_REQUIRED");
                    else if (!SameActions(linked.Detail.Plates.Bolts.Detail.Actions, c.Input.Bolts.Detail.Actions)) binding.Add("LINKED_ULTIMATE_ACTIONS_OR_POLE_MISMATCH");
                }
                else if (c.UltimateCaseId != null) binding.Add("SERVICEABILITY_MUST_NOT_BIND_TO_ULTIMATE_CASE");
                var r = new WeldedSpliceSlipCaseResult(c, binding); slips.Add(r);
                foreach (var issue in binding) issues.Add(c.Id + ":" + issue);
                Include(c.Id, "Slip", r.Assessment, c.Input.LimitState == BoltSlipLimitState.Serviceability, c.UltimateCaseId);
            }
            if (p.Category == SpliceBoltCategory.B && !slips.Any(s => s.IsBound && s.Input.Input.LimitState == BoltSlipLimitState.Serviceability))
                issues.Add("CATEGORY_B_SERVICEABILITY_CASE_REQUIRED");
            if (p.RequiresCategoryC) foreach (var c in p.UltimateCases)
                if (!slips.Any(s => s.IsBound && s.Input.Input.LimitState == BoltSlipLimitState.Ultimate && s.Input.UltimateCaseId == c.Id))
                    issues.Add(c.Id + ":CATEGORY_C_BOUND_ULTIMATE_SLIP_REQUIRED");
            return new WeldedIBeamSpliceEnvelopeResult(p, ultimate, slips, issues, sources);

            void Include<T>(string id, string component, ConnectionVerification<T> result, bool sls = false, string linked = null) where T : class
            {
                foreach (var issue in result.Diagnostics) issues.Add(id + ":" + component + ":" + issue);
                if (!result.IsComplete && result.Diagnostics.Count == 0) issues.Add(id + ":" + component + ":INCOMPLETE_COMPONENT");
                foreach (var check in result.Checks) sources.Add(new WeldedSpliceCheckSource(id, component, sls, linked, check));
            }
        }
        private static bool SameActions(PlanarConnectionActions a, PlanarConnectionActions b)
            => a.Fx == b.Fx && a.Fy == b.Fy && a.Fz == b.Fz && a.Mx == b.Mx && a.My == b.My && a.Mz == b.Mz
            && a.ApplicationX == b.ApplicationX && a.ApplicationY == b.ApplicationY && a.ApplicationZ == b.ApplicationZ;
        private static bool SameSlipExecution(WeldedIBeamSpliceSlipInput a, WeldedIBeamSpliceSlipInput b)
            => a.TighteningControl == b.TighteningControl && a.Policy.GammaM3Ultimate == b.Policy.GammaM3Ultimate
            && a.Policy.GammaM3Serviceability == b.Policy.GammaM3Serviceability && a.Surfaces.Count == b.Surfaces.Count
            && a.Surfaces.All(x => b.Surfaces.Any(y => x.Cover == y.Cover && x.Interfaces.Select(i => i.Coefficient).SequenceEqual(y.Interfaces.Select(i => i.Coefficient))));
        private static bool SamePhysical(WeldedIBeamSpliceBoltInput a, WeldedIBeamSpliceBoltInput b)
        {
            var x = a.Detail; var y = b.Detail;
            return WeldedIBeamSplicePlates.SameCode(a.Code, b.Code) && Bolt(a.FlangeBolt, b.FlangeBolt) && Bolt(a.WebBolt, b.WebBolt)
                && a.FlangeHardwareDiameter == b.FlangeHardwareDiameter && a.WebHardwareDiameter == b.WebHardwareDiameter
                && Plate(x.Section.TopFlange, y.Section.TopFlange) && Plate(x.Section.Web, y.Section.Web) && Plate(x.Section.BottomFlange, y.Section.BottomFlange)
                && x.Section.TopWeldToe == y.Section.TopWeldToe && x.Section.BottomWeldToe == y.Section.BottomWeldToe
                && Flange(x.Top, y.Top) && Flange(x.Bottom, y.Bottom) && Plate(x.EachWebCover, y.EachWebCover) && Grid(x.WebBolts, y.WebBolts)
                && x.WebCoverEnd == y.WebCoverEnd && x.ButtGap == y.ButtGap && x.Packing.SamePhysical(y.Packing);
        }
        private static bool Plate(ConnectionPlate a, ConnectionPlate b) => a.Width == b.Width && a.Thickness == b.Thickness && a.Fy == b.Fy && a.Fu == b.Fu;
        private static bool Bolt(ConnectionBolt a, ConnectionBolt b) => a.Diameter == b.Diameter && a.HoleDiameter == b.HoleDiameter && a.TensileArea == b.TensileArea && a.Grade == b.Grade && a.ThreadedShearPlanes == b.ThreadedShearPlanes;
        private static bool Grid(SpliceBoltLayout a, SpliceBoltLayout b) => a.LongitudinalRows == b.LongitudinalRows && a.TransverseRows == b.TransverseRows && (a.LongitudinalRows == 1 || a.LongitudinalPitch == b.LongitudinalPitch) && (a.TransverseRows == 1 || a.TransversePitch == b.TransversePitch) && a.FirstBoltDistance == b.FirstBoltDistance;
        private static bool Flange(SpliceFlangeCoverDetail a, SpliceFlangeCoverDetail b) => Plate(a.Outer, b.Outer) && Plate(a.EachInner, b.EachInner) && a.InnerGap == b.InnerGap && a.EndDistance == b.EndDistance && Grid(a.Bolts, b.Bolts);
    }
}
