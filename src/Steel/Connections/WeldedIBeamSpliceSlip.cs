using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Actual cover/parent contact surface, common to both sides of the butt.
    /// Inner strips and the two web faces can have distinct qualified coefficients.</summary>
    public sealed class WeldedSpliceFrictionSurface
    {
        public WeldedSpliceCover Cover { get; }
        public double Coefficient { get; }
        public string QualificationSource { get; }
        public double? PackingToParentCoefficient { get; }
        public string PackingToParentQualificationSource { get; }
        public WeldedSpliceFrictionSurface(WeldedSpliceCover cover, double coefficient, string qualificationSource)
        {
            if (!Enum.IsDefined(typeof(WeldedSpliceCover), cover)) throw new ArgumentOutOfRangeException(nameof(cover));
            ConnectionValidation.Positive(coefficient, nameof(coefficient));
            if (coefficient > 1) throw new ArgumentOutOfRangeException(nameof(coefficient));
            Cover = cover; Coefficient = coefficient; QualificationSource = qualificationSource;
        }
        public WeldedSpliceFrictionSurface(WeldedSpliceCover cover, double coverToPackingCoefficient, string coverToPackingSource,
            double packingToParentCoefficient, string packingToParentSource)
            : this(cover, coverToPackingCoefficient, coverToPackingSource)
        {
            ConnectionValidation.Positive(packingToParentCoefficient, nameof(packingToParentCoefficient));
            if (packingToParentCoefficient > 1) throw new ArgumentOutOfRangeException(nameof(packingToParentCoefficient));
            PackingToParentCoefficient = packingToParentCoefficient; PackingToParentQualificationSource = packingToParentSource;
        }
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

    public static class WeldedIBeamSpliceSlip
    {
        public static ConnectionVerification<WeldedIBeamSpliceSlipInput> Verify(WeldedIBeamSpliceSlipInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var prepared = WeldedIBeamSpliceBolts.Verify(p.Bolts);
            // Only applicability/geometry diagnostics are inherited. ULS bolt resistance
            // is not silently applied to an SLS case and its result is not used as slip.
            var issues = new List<string>(prepared.ComponentAssessment.Diagnostics);
            var trace = new List<SectionResistanceValue>(); var checks = new List<ConnectionCheck>();
            if (!WeldedIBeamSplicePlates.SameCode(p.Policy.Code, p.Bolts.Code)) issues.Add("SLIP_BOLT_POLICY_MISMATCH");
            foreach (WeldedSpliceCover cover in Enum.GetValues(typeof(WeldedSpliceCover)))
                if (!p.Surfaces.Any(s => s.Cover == cover)) issues.Add(cover + ".FRICTION_SURFACE_REQUIRED");
            if (issues.Count == 0) foreach (var g in prepared.Forces.CoverGroups)
            {
                var surface = p.Surfaces.Single(s => s.Cover == g.Cover);
                bool packed = p.Bolts.Detail.Packing.Thickness(g.Cover) > 0;
                if (packed != surface.PackingToParentCoefficient.HasValue)
                { issues.Add(g.Cover + ".PACKING_FRICTION_INTERFACES_MISMATCH"); continue; }
                var grid = WeldedIBeamSplicePlates.Grid(p.Bolts.Detail, g);
                double span = g.Distribution.Points.Max(b => b.Y) - g.Distribution.Points.Min(b => b.Y);
                double length = ElasticBoltGroup.Hypot(grid.LongitudinalSpan, span);
                var bolt = g.IsWeb ? p.Bolts.WebBolt : p.Bolts.FlangeBolt;
                foreach (var force in g.Distribution.Forces)
                {
                    // ONE real interface with its own force; the common bolt preload
                    // clamps each interface. Neither Fp/2 nor a pooled 2*mu*Fp capacity.
                    foreach (int layer in packed ? new[] { 0, 1 } : new[] { 0 })
                    {
                        var r = BoltSlip.Verify(new BoltSlipInput(p.Policy, bolt, p.LimitState, force.Shear, 0, 1,
                            layer == 0 ? surface.Coefficient : surface.PackingToParentCoefficient.Value,
                            p.TighteningControl, p.AssemblyQualificationSource,
                            layer == 0 ? surface.QualificationSource : surface.PackingToParentQualificationSource,
                            p.ActionCombinationSource, p.Bolts.HoleClearanceServiceabilitySource, length));
                        string id = g.Side + "." + g.Cover + ".Bolt" + force.Index
                            + (packed ? layer == 0 ? ".CoverToPacking" : ".PackingToParent" : "");
                        foreach (var issue in r.Diagnostics) issues.Add(id + ":" + issue);
                        foreach (var c in r.Checks) checks.Add(new ConnectionCheck(id + "." + c.Id, c.Demand, c.Resistance, c.Reference, c.Unit));
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
                    "Assembly and surface provenance document the input; they are not an execution certification. All eight faces are required even when a current load is zero. A packed face has two SERIAL interfaces, each with the full transmitted force and common preload; their resistances are never added." });
        }

        /// <summary>Category-C AXIAL net-plastic components at one actual ULS state.
        /// Combined net-plastic NVM, classification/stability and fracture remain separate.</summary>
        public static ConnectionVerification<WeldedIBeamSplicePlateInput> VerifyNetAxialPlastic(WeldedIBeamSplicePlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var r = WeldedIBeamSplicePlates.Verify(p); var issues = new List<string>(r.Diagnostics);
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var forces = WeldedIBeamSpliceForces.Calculate(p.Bolts.Detail);
            foreach (var cover in r.Covers)
            {
                if (!cover.Sections.IsComplete) { issues.AddRange(cover.Sections.Diagnostics); continue; }
                double net = cover.Sections.Trace.Single(t => t.Name == "NetArea").Value;
                var f = forces.CoverGroups.First(g => g.Cover == cover.Cover);
                double n = cover.Cuts.Max(c => Math.Max(0, c.N));
                Add(cover.Cover.ToString(), n, net, f.Plate.Fy);
            }
            if (forces.IsBalanced)
            {
                foreach (bool top in new[] { true, false })
                {
                    string name = top ? "Top" : "Bottom"; var detail = top ? p.Bolts.Detail.Top : p.Bolts.Detail.Bottom;
                    var plate = top ? p.Bolts.Detail.Section.TopFlange : p.Bolts.Detail.Section.BottomFlange;
                    double net = (plate.Width - detail.Bolts.TransverseRows * p.Bolts.FlangeBolt.HoleDiameter) * plate.Thickness;
                    double n = forces.Trace.Single(t => t.Name == "Left." + name + ".ParentAxial").Value;
                    Add(name + ".Parent", Math.Max(0, n), net, plate.Fy);
                }
                var web = p.Bolts.Detail.Section.Web;
                Add("Web.Parent", Math.Max(0, forces.Trace.Single(t => t.Name == "Web.AxialForce").Value),
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
