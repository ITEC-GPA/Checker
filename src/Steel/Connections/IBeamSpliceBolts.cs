using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One external cover per flange (one shear/friction plane), two equal
    /// web covers (two equal planes). No packing or bolt axial force in this model.
    /// Hardware envelope diameters include the actual head/nut/washer footprint;
    /// they are caller-supplied, not inferred from bolt diameter. Dimensions mm.</summary>
    public sealed class IBeamSpliceBoltSet
    {
        public ConnectionBolt FlangeBolt { get; }
        public ConnectionBolt WebBolt { get; }
        public double FlangeHardwareEnvelopeDiameter { get; }
        public double WebHardwareEnvelopeDiameter { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public IBeamSpliceBoltSet(ConnectionBolt flangeBolt, ConnectionBolt webBolt,
            double flangeHardwareEnvelopeDiameter, double webHardwareEnvelopeDiameter,
            string holeClearanceServiceabilitySource = null)
        {
            FlangeBolt = flangeBolt ?? throw new ArgumentNullException(nameof(flangeBolt));
            WebBolt = webBolt ?? throw new ArgumentNullException(nameof(webBolt));
            ConnectionValidation.Positive(flangeHardwareEnvelopeDiameter, nameof(flangeHardwareEnvelopeDiameter));
            ConnectionValidation.Positive(webHardwareEnvelopeDiameter, nameof(webHardwareEnvelopeDiameter));
            if (flangeHardwareEnvelopeDiameter < flangeBolt.HoleDiameter || webHardwareEnvelopeDiameter < webBolt.HoleDiameter)
                throw new ArgumentException("Hardware envelopes must enclose the holes.");
            FlangeHardwareEnvelopeDiameter = flangeHardwareEnvelopeDiameter; WebHardwareEnvelopeDiameter = webHardwareEnvelopeDiameter;
            HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }
    public sealed class IBeamSpliceBoltStrengthInput
    {
        public ConnectionDesignCode Code { get; }
        public IBeamSpliceForceInput Forces { get; }
        public IBeamSpliceBoltSet Bolts { get; }
        public string UltimateCombinationSource { get; }
        public IBeamSpliceBoltStrengthInput(ConnectionDesignCode code, IBeamSpliceForceInput forces,
            IBeamSpliceBoltSet bolts, string ultimateCombinationSource)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code)); Forces = forces ?? throw new ArgumentNullException(nameof(forces));
            Bolts = bolts ?? throw new ArgumentNullException(nameof(bolts)); UltimateCombinationSource = ultimateCombinationSource;
        }
    }
    /// <summary>Explicit actions for this slip limit state. No conversion from an
    /// ultimate combination and no implicit relationship between different cases.</summary>
    public sealed class IBeamSpliceBoltSlipInput
    {
        public BoltSlipPolicy Policy { get; }
        public IBeamSpliceForceInput Forces { get; }
        public IBeamSpliceBoltSet Bolts { get; }
        public BoltSlipLimitState LimitState { get; }
        public double FlangeFrictionCoefficient { get; }
        public double WebFrictionCoefficient { get; }
        public BoltTighteningControl TighteningControl { get; }
        public string AssemblyQualificationSource { get; }
        public string SurfaceQualificationSource { get; }
        public string ActionCombinationSource { get; }
        public IBeamSpliceBoltSlipInput(BoltSlipPolicy policy, IBeamSpliceForceInput forces,
            IBeamSpliceBoltSet bolts, BoltSlipLimitState limitState, double flangeFrictionCoefficient,
            double webFrictionCoefficient, BoltTighteningControl tighteningControl,
            string assemblyQualificationSource, string surfaceQualificationSource, string actionCombinationSource)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy)); Forces = forces ?? throw new ArgumentNullException(nameof(forces));
            Bolts = bolts ?? throw new ArgumentNullException(nameof(bolts));
            if (!Enum.IsDefined(typeof(BoltSlipLimitState), limitState)) throw new ArgumentOutOfRangeException(nameof(limitState));
            if (!Enum.IsDefined(typeof(BoltTighteningControl), tighteningControl)) throw new ArgumentOutOfRangeException(nameof(tighteningControl));
            foreach (double value in new[] { flangeFrictionCoefficient, webFrictionCoefficient })
            { ConnectionValidation.Positive(value, nameof(value)); if (value > 1) throw new ArgumentOutOfRangeException(nameof(value)); }
            LimitState = limitState; FlangeFrictionCoefficient = flangeFrictionCoefficient; WebFrictionCoefficient = webFrictionCoefficient;
            TighteningControl = tighteningControl; AssemblyQualificationSource = assemblyQualificationSource;
            SurfaceQualificationSource = surfaceQualificationSource; ActionCombinationSource = actionCombinationSource;
        }
    }
    /// <summary>Scoped bolt components, not a whole splice. Plate bearing/net/block,
    /// flange-cover compression/buckling, web N-V-M and member checks remain separate.</summary>
    public static class IBeamSpliceBolts
    {
        public static ConnectionVerification<IBeamSpliceBoltStrengthInput> VerifyStrength(IBeamSpliceBoltStrengthInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>(); var checks = new List<ConnectionCheck>();
            var distribution = Prepare(p.Forces, p.Bolts, issues, trace);
            if (string.IsNullOrWhiteSpace(p.UltimateCombinationSource)) issues.Add("ULTIMATE_COMBINATION_SOURCE_REQUIRED");
            if (issues.Count == 0) foreach (var g in distribution.BoltGroups)
            {
                bool web = g.Part == IBeamSplicePart.Web;
                var layout = web ? p.Forces.WebBolts : p.Forces.FlangeBolts;
                foreach (var f in g.Distribution.Forces)
                {
                    // Member axial force acts in the plate plane, hence bolt SHEAR,
                    // even at a tension flange. It is never passed as bolt tension.
                    var component = BoltStrength.Verify(new BoltStrengthInput(p.Code,
                        web ? p.Bolts.WebBolt : p.Bolts.FlangeBolt, f.Shear, 0, shearPlanes: web ? 2 : 1,
                        jointLength: JointLength(layout, web), holeClearanceServiceabilitySource: p.Bolts.HoleClearanceServiceabilitySource));
                    Include(g, f, component, checks, trace, issues);
                }
            }
            return new ConnectionVerification<IBeamSpliceBoltStrengthInput>(p, p.Code,
                "Steel.Connection.IBeamSplice.BoltStrength", "Bolt strength at the explicit ULS combination on six groups, "
                + "one external flange cover and two equal web covers. BOLT COMPONENTS ONLY: no bearing/plate/member/stability or whole-joint assessment.",
                checks, trace, issues, Assumptions());
        }

        public static ConnectionVerification<IBeamSpliceBoltSlipInput> VerifySlip(IBeamSpliceBoltSlipInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>(); var checks = new List<ConnectionCheck>();
            var distribution = Prepare(p.Forces, p.Bolts, issues, trace);
            if (issues.Count == 0) foreach (var g in distribution.BoltGroups)
            {
                bool web = g.Part == IBeamSplicePart.Web;
                var layout = web ? p.Forces.WebBolts : p.Forces.FlangeBolts;
                foreach (var f in g.Distribution.Forces)
                {
                    var component = BoltSlip.Verify(new BoltSlipInput(p.Policy, web ? p.Bolts.WebBolt : p.Bolts.FlangeBolt,
                        p.LimitState, f.Shear, 0, web ? 2 : 1, web ? p.WebFrictionCoefficient : p.FlangeFrictionCoefficient,
                        p.TighteningControl, p.AssemblyQualificationSource, p.SurfaceQualificationSource,
                        p.ActionCombinationSource, p.Bolts.HoleClearanceServiceabilitySource, JointLength(layout, web)));
                    Include(g, f, component, checks, trace, issues);
                }
            }
            return new ConnectionVerification<IBeamSpliceBoltSlipInput>(p, p.Policy.Code,
                "Steel.Connection.IBeamSplice.BoltSlip", "Bolt slip at the explicitly named SLS or ULS combination on six groups, "
                + "one friction plane at each flange and two at the web. BOLT COMPONENTS ONLY: no strength/plate/member/stability or whole-joint assessment.",
                checks, trace, issues, Assumptions());
        }

        private static IBeamSpliceForceResult Prepare(IBeamSpliceForceInput p, IBeamSpliceBoltSet bolts,
            IList<string> issues, IList<SectionResistanceValue> trace)
        {
            var r = IBeamSpliceForces.Calculate(p);
            foreach (var d in r.Diagnostics) issues.Add(d);
            foreach (var t in r.Trace) trace.Add(new SectionResistanceValue("Distribution." + t.Name, t.Value, t.Unit));
            if (!r.IsSupported) return r;
            var s = p.Section;
            foreach (bool web in new[] { false, true })
            {
                var grid = web ? p.WebBolts : p.FlangeBolts;
                double envelope = web ? bolts.WebHardwareEnvelopeDiameter : bolts.FlangeHardwareEnvelopeDiameter;
                double available = web ? s.ClearWeb : s.Width;
                string part = web ? "Web" : "Flange";
                if (grid.TransverseSpan + envelope > available) issues.Add(part + ".HARDWARE_OUTSIDE_AVAILABLE_STEEL");
                if ((grid.LongitudinalRows > 1 && grid.LongitudinalPitch < envelope)
                    || (grid.TransverseRows > 1 && grid.TransversePitch < envelope)
                    || 2 * grid.FirstBoltDistance < envelope) issues.Add(part + ".HARDWARE_OVERLAP");
                if (!web)
                {
                    // The inner nut/washer must clear the web and rolled root.
                    double nearestToWeb = grid.TransverseRows % 2 == 0 ? grid.TransversePitch / 2 : 0;
                    if (nearestToWeb - envelope / 2 < s.WebThickness / 2 + s.RootRadius)
                        issues.Add("Flange.HARDWARE_INTERSECTS_WEB_OR_ROOT");
                }
                trace.Add(new SectionResistanceValue(part + ".ConservativeJointLength", JointLength(grid, web), "mm"));
            }
            return r;
        }
        // Web bolt force direction changes along the group. Its diagonal bounds
        // every projected transfer length; this is a conservative policy, not an
        // exact clause-defined diagonal. Flange transfer is longitudinal only.
        private static double JointLength(SpliceBoltLayout layout, bool web) => web
            ? ElasticBoltGroup.Hypot(layout.LongitudinalSpan, layout.TransverseSpan) : layout.LongitudinalSpan;
        private static void Include<T>(IBeamSpliceBoltGroup g, ElasticBoltForce f, ConnectionVerification<T> component,
            IList<ConnectionCheck> checks, IList<SectionResistanceValue> trace, IList<string> issues) where T : class
        {
            string prefix = g.Side + "." + g.Part + ".Bolt" + (f.Index + 1);
            foreach (var d in component.Diagnostics) issues.Add(prefix + ":" + d);
            foreach (var c in component.Checks) checks.Add(new ConnectionCheck(prefix + "." + c.Id, c.Demand, c.Resistance, c.Reference, c.Unit));
            foreach (var t in component.Trace) trace.Add(new SectionResistanceValue(prefix + "." + t.Name, t.Value, t.Unit));
            trace.Add(new SectionResistanceValue(prefix + ".Fx", f.Fx, "N"));
            trace.Add(new SectionResistanceValue(prefix + ".Fy", f.Fy, "N"));
        }
        private static IEnumerable<string> Assumptions() => new[] {
            "Force distribution: SCI P398 section4 Step1 and EN1993-1-8:2005 6.2.7.1(16); EC3 and NTC share mechanics, not partial factors.",
            "Equal web covers share each bolt resultant equally. Shear/friction planes are counted once, with the TOTAL bolt force as demand.",
            "Flange axial force is bolt shear, including a tension flange. No out-of-plane bolt tension or favourable contact-bearing is assumed.",
            "Long-joint length: longitudinal span at the flanges, conservatively the rectangular-grid diagonal at the web for varying force directions; no exemption.",
            "No packing. Hardware envelopes must be supplied; this fit check does not qualify installation/tool access or code minimum/maximum edge distances.",
            "Plate dimensions/strength, bearing, net/block, local secondary bending, compression/buckling and web N-V-M remain necessary. These component checks cannot qualify a whole joint.",
            "Slip actions and their limit state are explicit. No ULS-to-SLS scaling, no ULS strength inferred from a passed slip check."
        };
    }
}
