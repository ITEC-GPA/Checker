using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One real friction interface, with its own coefficient and preparation evidence.</summary>
    public sealed class WeldedSpliceFrictionInterface
    {
        public double Coefficient { get; }
        public string QualificationSource { get; }
        public WeldedSpliceFrictionInterface(double coefficient, string qualificationSource)
        {
            ConnectionValidation.Positive(coefficient, nameof(coefficient));
            if (coefficient > 1) throw new ArgumentOutOfRangeException(nameof(coefficient));
            Coefficient = coefficient; QualificationSource = qualificationSource;
        }
    }
    /// <summary>Actual serial contact interfaces, ordered from cover towards parent,
    /// common to both sides of the butt unless declared per side (section transitions, where the
    /// packing layers differ). Inner strips/web faces may have distinct coefficients.</summary>
    public sealed class WeldedSpliceFrictionSurface
    {
        public WeldedSpliceCover Cover { get; }
        /// <summary>Interfaces on the left member (on both members unless declared per side).</summary>
        public IReadOnlyList<WeldedSpliceFrictionInterface> Interfaces { get; }
        /// <summary>Interfaces on the right member; the same list as Interfaces unless declared per side.</summary>
        public IReadOnlyList<WeldedSpliceFrictionInterface> RightInterfaces { get; }
        public double Coefficient => Interfaces[0].Coefficient;
        public string QualificationSource => Interfaces[0].QualificationSource;
        public double? PackingToParentCoefficient => Interfaces.Count > 1 ? (double?)Interfaces[Interfaces.Count - 1].Coefficient : null;
        public string PackingToParentQualificationSource => Interfaces.Count > 1 ? Interfaces[Interfaces.Count - 1].QualificationSource : null;
        public WeldedSpliceFrictionSurface(WeldedSpliceCover cover, double coefficient, string qualificationSource)
            : this(cover, new[] { new WeldedSpliceFrictionInterface(coefficient, qualificationSource) }) { }
        public WeldedSpliceFrictionSurface(WeldedSpliceCover cover, double coverToPackingCoefficient, string coverToPackingSource,
            double packingToParentCoefficient, string packingToParentSource)
            : this(cover, new[] { new WeldedSpliceFrictionInterface(coverToPackingCoefficient, coverToPackingSource),
                new WeldedSpliceFrictionInterface(packingToParentCoefficient, packingToParentSource) }) { }
        public WeldedSpliceFrictionSurface(WeldedSpliceCover cover, IEnumerable<WeldedSpliceFrictionInterface> interfaces)
        {
            if (!Enum.IsDefined(typeof(WeldedSpliceCover), cover)) throw new ArgumentOutOfRangeException(nameof(cover));
            Cover = cover; Interfaces = Copy(interfaces, nameof(interfaces)); RightInterfaces = Interfaces;
        }
        /// <summary>Different serial interfaces on the two members of the same cover (e.g. packing only on
        /// the thinner member of a section transition). Each list is ordered from cover towards parent.</summary>
        public WeldedSpliceFrictionSurface(WeldedSpliceCover cover, IEnumerable<WeldedSpliceFrictionInterface> leftInterfaces,
            IEnumerable<WeldedSpliceFrictionInterface> rightInterfaces)
        {
            if (!Enum.IsDefined(typeof(WeldedSpliceCover), cover)) throw new ArgumentOutOfRangeException(nameof(cover));
            Cover = cover; Interfaces = Copy(leftInterfaces, nameof(leftInterfaces)); RightInterfaces = Copy(rightInterfaces, nameof(rightInterfaces));
        }
        private static IReadOnlyList<WeldedSpliceFrictionInterface> Copy(IEnumerable<WeldedSpliceFrictionInterface> interfaces, string name)
        {
            if (interfaces == null) throw new ArgumentNullException(name);
            var values = interfaces.ToArray();
            if (values.Length == 0 || values.Any(v => v == null)) throw new ArgumentException("Every physical interface is required.", name);
            return Array.AsReadOnly(values);
        }
        internal IReadOnlyList<WeldedSpliceFrictionInterface> On(SpliceSide side) => side == SpliceSide.Right ? RightInterfaces : Interfaces;
        internal string InterfaceName(int index) => InterfaceName(Interfaces.Count, index);
        internal string InterfaceName(SpliceSide side, int index) => InterfaceName(On(side).Count, index);
        private static string InterfaceName(int count, int index) => count == 1 ? "" : index == 0 ? "CoverToPacking"
            : index == count - 1 ? "PackingToParent" : "Packing" + index + "ToPacking" + (index + 1);
    }

    public sealed class WeldedIBeamSpliceSlipInput
    {
        public BoltSlipPolicy Policy { get; }
        public WeldedIBeamSpliceBoltInput Bolts { get; }
        public BoltSlipLimitState LimitState { get; }
        public IReadOnlyList<WeldedSpliceFrictionSurface> Surfaces { get; }
        public BoltTighteningControl TighteningControl { get; }
        public string AssemblyQualificationSource { get; }
        public string ActionCombinationSource { get; }
        public WeldedIBeamSpliceSlipInput(BoltSlipPolicy policy, WeldedIBeamSpliceBoltInput bolts, BoltSlipLimitState limitState,
            IEnumerable<WeldedSpliceFrictionSurface> surfaces, BoltTighteningControl tighteningControl,
            string assemblyQualificationSource, string actionCombinationSource)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy)); Bolts = bolts ?? throw new ArgumentNullException(nameof(bolts));
            if (!Enum.IsDefined(typeof(BoltSlipLimitState), limitState)) throw new ArgumentOutOfRangeException(nameof(limitState));
            if (!Enum.IsDefined(typeof(BoltTighteningControl), tighteningControl)) throw new ArgumentOutOfRangeException(nameof(tighteningControl));
            if (surfaces == null) throw new ArgumentNullException(nameof(surfaces));
            var array = surfaces.ToArray();
            if (array.Any(s => s == null) || array.Select(s => s.Cover).Distinct().Count() != array.Length)
                throw new ArgumentException("Each physical surface must occur at most once and cannot be null.", nameof(surfaces));
            Surfaces = Array.AsReadOnly(array); LimitState = limitState; TighteningControl = tighteningControl;
            AssemblyQualificationSource = assemblyQualificationSource; ActionCombinationSource = actionCombinationSource;
        }
    }

    internal static class WeldedIBeamSpliceSlip
    {
        public static ConnectionVerification<WeldedIBeamSpliceSlipInput> Verify(WeldedIBeamSpliceSlipInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var prepared = WeldedIBeamSpliceBolts.Verify(p.Bolts);
            // Only applicability/geometry diagnostics are inherited. ULS bolt resistance
            // is not silently applied to an SLS case and its result is not used as slip.
            var issues = new List<string>(prepared.ComponentAssessment.Diagnostics);
            var trace = new List<SectionResistanceValue>(); var checks = new List<ConnectionCheck>();
            if (!SpliceIdentity.Code(p.Policy.Code, p.Bolts.Code)) issues.Add("SLIP_BOLT_POLICY_MISMATCH");
            foreach (WeldedSpliceCover cover in Enum.GetValues(typeof(WeldedSpliceCover)))
                if (!p.Surfaces.Any(s => s.Cover == cover)) issues.Add(cover + ".FRICTION_SURFACE_REQUIRED");
            if (issues.Count == 0) foreach (var g in prepared.Forces.CoverGroups)
            {
                var surface = p.Surfaces.Single(s => s.Cover == g.Cover);
                // Each member has its own packing layers across a section transition.
                int layerCount = p.Bolts.Detail.PackingOf(g.Side).Layers(g.Cover).Count;
                bool packed = layerCount > 0;
                var interfaces = surface.On(g.Side);
                if (interfaces.Count != layerCount + 1)
                { issues.Add(g.Cover + ".PACKING_FRICTION_INTERFACES_MISMATCH"); continue; }
                var grid = WeldedIBeamSplicePlates.Grid(p.Bolts.Detail, g);
                double span = g.Distribution.Points.Max(b => b.Y) - g.Distribution.Points.Min(b => b.Y);
                double length = ElasticBoltGroup.Hypot(grid.LongitudinalSpan, span);
                var bolt = g.IsWeb ? p.Bolts.WebBolt : p.Bolts.FlangeBolt;
                foreach (var force in g.Distribution.Forces)
                {
                    // ONE real interface with its own force; the common bolt preload
                    // clamps each interface. Neither Fp/2 nor a pooled 2*mu*Fp capacity.
                    for (int layer = 0; layer < interfaces.Count; ++layer)
                    {
                        var r = BoltSlip.Verify(new BoltSlipInput(p.Policy, bolt, p.LimitState, force.Shear, 0, 1,
                            interfaces[layer].Coefficient,
                            p.TighteningControl, p.AssemblyQualificationSource,
                            interfaces[layer].QualificationSource,
                            p.ActionCombinationSource, p.Bolts.HoleClearanceServiceabilitySource, length));
                        string id = g.Side + "." + g.Cover + ".Bolt" + force.Index
                            + (packed ? "." + surface.InterfaceName(g.Side, layer) : "");
                        foreach (var issue in r.Diagnostics) issues.Add(id + ":" + issue);
                        foreach (var c in r.Checks) checks.Add(c.WithId(id + "." + c.Id));
                        foreach (var t in r.Trace) trace.Add(new SectionResistanceValue(id + "." + t.Name, t.Value, t.Unit));
                    }
                }
            }
            return new ConnectionVerification<WeldedIBeamSpliceSlipInput>(p, p.Policy.Code,
                "Steel.Connection.WeldedIBeamSplice.Slip", "Slip COMPONENT of every real cover/parent interface at its explicit limit state; no whole-joint or local contact/prying qualification.",
                checks, trace, issues.Distinct(), new[] {
                    "Each interface receives its actual vector demand, friction coefficient and the common bolt preload once. Unused resistance on the other face cannot rescue a slipping face.",
                    "No SLS/ULS conversion. Normal round holes; diagonal long-joint bound, no uniform-transfer exemption. The established EC/NTC BoltSlip kernel supplies the distinct factors.",
                    "Zero applied bolt-axis tension is the in-plane component assumption. Local opening/prying and the normal-contact reactions are not solved or certified by this result; see face-transfer diagnostics.",
                    "Assembly and surface provenance document the input; they are not an execution certification. All eight faces are required even when a current load is zero. A face with k packing layers has k+1 SERIAL interfaces, each with the full transmitted force and common preload; their resistances are never added." });
        }

        /// <summary>Category-C AXIAL net-plastic components at one actual ULS state.
        /// Combined net-plastic NVM, classification/stability and fracture remain separate.</summary>
        public static ConnectionVerification<WeldedIBeamSplicePlateInput> VerifyNetAxialPlastic(WeldedIBeamSplicePlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            return VerifyNetAxialPlastic(p, WeldedIBeamSplicePlates.Verify(p));
        }
        internal static ConnectionVerification<WeldedIBeamSplicePlateInput> VerifyNetAxialPlastic(WeldedIBeamSplicePlateInput p, WeldedIBeamSplicePlateResult r)
        {
            if (!ReferenceEquals(p, r.Input)) throw new ArgumentException("Prepared plates belong to another input.");
            var issues = new List<string>(r.Diagnostics);
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var forces = r.PreparedBolts.Forces;
            foreach (var cover in r.Covers)
            {
                if (!cover.Sections.IsComplete) { issues.AddRange(cover.Sections.Diagnostics); continue; }
                double net = cover.NetArea;
                var f = forces.CoverGroups.First(g => g.Cover == cover.Cover);
                double n = cover.Cuts.Max(c => Math.Max(0, c.N));
                Add(cover.Cover.ToString(), n, net, f.Plate.Fy);
            }
            if (forces.IsBalanced && p.Bolts.Detail.Transition != null)
            {
                // Each member's own plates. The redistributing member is bounded over the whole lap by the
                // delivered tension plus the absolute transfer to its own elastic share.
                var detail = p.Bolts.Detail;
                foreach (var side in new[] { SpliceSide.Left, SpliceSide.Right })
                {
                    var s = detail.SectionOf(side); bool own = side == detail.CoverDistribution;
                    foreach (bool top in new[] { true, false })
                    {
                        var cover = top ? detail.Top : detail.Bottom; var plate = top ? s.TopFlange : s.BottomFlange;
                        double net = (plate.Width - cover.Bolts.TransverseRows * p.Bolts.FlangeBolt.HoleDiameter) * plate.Thickness;
                        double n = forces.Flange(SpliceSide.Left, top).ParentAxial;
                        Add(side + "." + (top ? "Top" : "Bottom") + ".Parent", Math.Max(0, n) + (own ? 0 : Math.Abs(forces.OwnShare(side, top ? 0 : 2) - n)), net, plate.Fy);
                    }
                    double webN = forces.WebAxialForce;
                    Add(side + ".Web.Parent", Math.Max(0, webN) + (own ? 0 : Math.Abs(forces.OwnShare(side, 4) - webN)),
                        (s.Web.Width - detail.WebBolts.TransverseRows * p.Bolts.WebBolt.HoleDiameter) * s.Web.Thickness, s.Web.Fy);
                }
            }
            else if (forces.IsBalanced)
            {
                foreach (bool top in new[] { true, false })
                {
                    string name = top ? "Top" : "Bottom"; var detail = top ? p.Bolts.Detail.Top : p.Bolts.Detail.Bottom;
                    var plate = top ? p.Bolts.Detail.Section.TopFlange : p.Bolts.Detail.Section.BottomFlange;
                    double net = (plate.Width - detail.Bolts.TransverseRows * p.Bolts.FlangeBolt.HoleDiameter) * plate.Thickness;
                    double n = forces.Flange(SpliceSide.Left, top).ParentAxial;
                    Add(name + ".Parent", Math.Max(0, n), net, plate.Fy);
                }
                var web = p.Bolts.Detail.Section.Web;
                Add("Web.Parent", Math.Max(0, forces.WebAxialForce),
                    (web.Width - p.Bolts.Detail.WebBolts.TransverseRows * p.Bolts.WebBolt.HoleDiameter) * web.Thickness, web.Fy);
            }
            else issues.AddRange(forces.Diagnostics);
            return new ConnectionVerification<WeldedIBeamSplicePlateInput>(p, p.Policy.Code,
                "Steel.Connection.WeldedIBeamSplice.NetAxialPlastic", "Category-C net-plastic AXIAL component only, all eight covers and three parent plates; NOT N-V-M net-section qualification.",
                checks, trace, issues.Distinct(), new[] { "Anet*fy/gammaM0, not fracture resistance. Every ULS state is assessed separately; a negative N does not activate this tensile component. Web/flange self-moments and shear still require the mixed-stress net check." });
            void Add(string id, double demand, double net, double fy)
            {
                double resistance = net * fy / p.Policy.Code.Plates.GammaM0;
                if (!(net > 0) || !WeldedIBeamSplicePlates.Finite(resistance)) { issues.Add(id + ".NET_PLASTIC_NUMERIC_RANGE"); return; }
                checks.Add(new ConnectionCheck(id + ".NetAxialPlastic", demand, resistance,
                    "EN1993-1-1:2005 6.2.3(4) eq6.8; EN1993-1-8:2005 3.4.1(1)c; supplementary category-C criterion with NTC gammaM0 when selected"));
                trace.Add(new SectionResistanceValue(id + ".NetArea", net, "mm2"));
            }
        }
    }
}
