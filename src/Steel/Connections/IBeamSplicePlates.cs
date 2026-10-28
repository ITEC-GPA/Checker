using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Factors for the plate components. EC gammaM1 is explicit; NTC
    /// retains its own factors. E=210000 MPa for the declared carbon-steel scope.</summary>
    public sealed class IBeamSplicePlatePolicy
    {
        public ConnectionDesignCode Code { get; }
        public double GammaM1 { get; }
        public double ElasticModulus => 210000;
        private IBeamSplicePlatePolicy(ConnectionDesignCode code, double gammaM1)
        { Code = code; ConnectionValidation.Positive(gammaM1, nameof(gammaM1)); GammaM1 = gammaM1; }
        public static IBeamSplicePlatePolicy Eurocode3(ConnectionDesignCode code, double gammaM1)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (code.IsNtc) throw new ArgumentException("Use the NTC factory for NTC factors.", nameof(code));
            return new IBeamSplicePlatePolicy(code, gammaM1);
        }
        public static IBeamSplicePlatePolicy Ntc2018()
            => new IBeamSplicePlatePolicy(ConnectionDesignCode.Ntc2018(), Ntc2018DesignPolicy.GammaM1);
        /// <summary>NTC 2018 Table 4.2.VII for road and railway bridges: gammaM1=1.10.</summary>
        public static IBeamSplicePlatePolicy Ntc2018Bridges()
            => new IBeamSplicePlatePolicy(ConnectionDesignCode.Ntc2018(), Ntc2018DesignPolicy.GammaM1Bridges);
    }

    /// <summary>One external cover per flange and two identical web covers,
    /// centred on the splice. WebCover.Width is its vertical height. End distances
    /// refer to the OUTER free cover ends; first bolt positions refer to the joint
    /// centre, so the member end distance is firstBoltDistance-ButtGap/2.
    /// Flange strengths are separate from the section's web strengths, assigned
    /// at the actual product thickness. No packing. N, mm, MPa.</summary>
    public sealed class IBeamSplicePlateInput
    {
        public IBeamSplicePlatePolicy Policy { get; }
        public IBeamSpliceForceInput Forces { get; }
        public IBeamSpliceBoltSet Bolts { get; }
        public ConnectionPlate FlangeCover { get; }
        public ConnectionPlate WebCover { get; }
        public double FlangeCoverEnd { get; }
        public double WebCoverEnd { get; }
        public double ButtGap { get; }
        public double BeamFlangeFy { get; }
        public double BeamFlangeFu { get; }
        public bool Exposed { get; }
        public string UltimateCombinationSource { get; }
        public IBeamSplicePlateInput(IBeamSplicePlatePolicy policy, IBeamSpliceForceInput forces,
            IBeamSpliceBoltSet bolts, ConnectionPlate flangeCover, ConnectionPlate webCover,
            double flangeCoverEnd, double webCoverEnd, double buttGap, double beamFlangeFy,
            double beamFlangeFu, bool exposed, string ultimateCombinationSource)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Forces = forces ?? throw new ArgumentNullException(nameof(forces));
            Bolts = bolts ?? throw new ArgumentNullException(nameof(bolts));
            FlangeCover = flangeCover ?? throw new ArgumentNullException(nameof(flangeCover));
            WebCover = webCover ?? throw new ArgumentNullException(nameof(webCover));
            ConnectionValidation.Positive(flangeCoverEnd, nameof(flangeCoverEnd));
            ConnectionValidation.Positive(webCoverEnd, nameof(webCoverEnd));
            ConnectionValidation.Nonnegative(buttGap, nameof(buttGap));
            ConnectionValidation.Positive(beamFlangeFy, nameof(beamFlangeFy));
            ConnectionValidation.Positive(beamFlangeFu, nameof(beamFlangeFu));
            if (beamFlangeFu < beamFlangeFy) throw new ArgumentException("Flange fu must not be below fy.");
            FlangeCoverEnd = flangeCoverEnd; WebCoverEnd = webCoverEnd; ButtGap = buttGap;
            BeamFlangeFy = beamFlangeFy; BeamFlangeFu = beamFlangeFu;
            Exposed = exposed; UltimateCombinationSource = ultimateCombinationSource;
        }
    }

    /// <summary>Plate COMPONENTS, not the complete splice. Bearing uses the
    /// conservative minimum end/interior capacities for every bolt and a linear
    /// interaction of the two in-plane force components (SCI P398 p55).</summary>
    internal static partial class IBeamSplicePlates
    {
        public static ConnectionVerification<IBeamSplicePlateInput> VerifyBearing(IBeamSplicePlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var checks = new List<ConnectionCheck>();
            var distribution = Prepare(p, issues, trace);
            if (issues.Count == 0) foreach (var g in distribution.BoltGroups)
            {
                bool web = g.Part == IBeamSplicePart.Web;
                var grid = web ? p.Forces.WebBolts : p.Forces.FlangeBolts;
                var bolt = web ? p.Bolts.WebBolt : p.Bolts.FlangeBolt;
                double beta = BoltLongJoint.Reduction(IBeamSpliceBolts.JointLength(grid, web), bolt.Diameter);
                foreach (bool cover in new[] { false, true })
                {
                    var plate = cover ? (web ? p.WebCover : p.FlangeCover) : Main(p, web);
                    double end = cover ? (web ? p.WebCoverEnd : p.FlangeCoverEnd) : grid.FirstBoltDistance - p.ButtGap / 2;
                    double edge = (plate.Width - grid.TransverseSpan) / 2;
                    string prefix = g.Side + "." + g.Part + (cover ? ".Cover" : ".Member");
                    double rx = BearingCapacity(p, bolt, plate, end, edge, grid.LongitudinalPitch,
                        grid.TransversePitch, grid.LongitudinalRows, grid.TransverseRows, prefix + ".X", issues) * beta;
                    double ry = web ? BearingCapacity(p, bolt, plate, edge, end, grid.TransversePitch,
                        grid.LongitudinalPitch, grid.TransverseRows, grid.LongitudinalRows, prefix + ".Y", issues) * beta : 0;
                    trace.Add(new SectionResistanceValue(prefix + ".BearingX", rx, "N"));
                    if (web) trace.Add(new SectionResistanceValue(prefix + ".BearingY", ry, "N"));
                    trace.Add(new SectionResistanceValue(prefix + ".BetaLongJoint", beta, "1"));
                    if (issues.Count != 0) continue;
                    foreach (var f in g.Distribution.Forces)
                    {
                        double share = web && cover ? .5 : 1;
                        string id = prefix + ".Bolt" + (f.Index + 1);
                        string reference = (p.Policy.Code.IsNtc ? "NTC2018 4.2.8.1.1 (4.2.67)" : "EN1993-1-8:2005 Table3.4")
                            + "; SCI P398 section4 Step2 p55 (conservative directional bearing and long-joint reduction)";
                        checks.Add(new ConnectionCheck(id + ".BearingX", Math.Abs(f.Fx) * share, rx, reference));
                        if (web)
                        {
                            checks.Add(new ConnectionCheck(id + ".BearingY", Math.Abs(f.Fy) * share, ry, reference));
                            double ratio = share * (Math.Abs(f.Fx) / rx + Math.Abs(f.Fy) / ry);
                            if (Finite(ratio)) checks.Add(new ConnectionCheck(id + ".BearingInteraction", ratio, 1, reference, "1"));
                            else issues.Add(id + ".BEARING_INTERACTION_OUTSIDE_NUMERIC_RANGE");
                        }
                    }
                }
            }
            return Result(p, "Bearing", "BEARING COMPONENTS ONLY: all six groups, member and each cover. "
                + "No net/block, plate/member buckling, slip, secondary bending or whole-joint qualification.", checks, trace, issues,
                "Linear |Fx|/Fb,x+|Fy|/Fb,y <= 1, not an elliptical interaction. Worst end/interior and edge/interior capacities apply to every bolt; no plastic redistribution.",
                "Each web cover takes HALF of the actual total bolt force; member web and flange cover take the full force. Signs reverse without changing conservative capacities.",
                "SCI P398 p55 extends the long-joint reduction to bearing. Web length is conservatively the group diagonal; the same policy is supplementary guidance for NTC.");
        }

        public static ConnectionVerification<IBeamSplicePlateInput> VerifyFlanges(IBeamSplicePlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var checks = new List<ConnectionCheck>();
            var distribution = Prepare(p, issues, trace);
            if (issues.Count == 0)
            {
                foreach (bool top in new[] { true, false })
                {
                    double n = top ? distribution.Resultants.TopFlangeAxial : distribution.Resultants.BottomFlangeAxial;
                    foreach (bool cover in new[] { false, true })
                    {
                        var plate = cover ? p.FlangeCover : Main(p, false);
                        double end = cover ? p.FlangeCoverEnd : p.Forces.FlangeBolts.FirstBoltDistance - p.ButtGap / 2;
                        string id = (top ? "TopFlange" : "BottomFlange") + (cover ? ".Cover" : ".Member");
                        Flange(p, plate, end, cover, n, id, checks, trace, issues);
                    }
                }
            }
            return Result(p, "Flanges", "FLANGE COMPONENTS ONLY: gross/net tension, two block paths, gross compression, "
                + "local longitudinal cover buckling between bolt rows. No member classification/buckling, secondary bending, "
                + "web checks, slip, rotational stiffness or whole-joint qualification.", checks, trace, issues,
                "SCI P398 section4 Steps3-4 pp56-57; one external flange cover, two transverse bolt columns. No contact-bearing credit at the butt.",
                "Compression cover: Lcr=0.6*max(pitch,2*firstBoltDistance), i=t/sqrt(12), EC buckling curve c; holes are filled. If p/t<=9*epsilon local buckling is ignored.",
                "The cover buckling model and block paths are SCI/EC supplementary guidance for NTC, using NTC partial factors. Member flange compression here is only gross cross-section resistance.",
                "Tension and compression use the physical flange resultant, not the opposing forces acting on the left/right pieces of the same splice.");
        }

        private static void Flange(IBeamSplicePlateInput p, ConnectionPlate plate, double end, bool cover,
            double n, string id, IList<ConnectionCheck> checks, IList<SectionResistanceValue> trace, IList<string> issues)
        {
            var grid = p.Forces.FlangeBolts; double d0 = p.Bolts.FlangeBolt.HoleDiameter;
            var factors = p.Policy.Code.Plates;
            double area = plate.Width * plate.Thickness, net = (plate.Width - 2 * d0) * plate.Thickness;
            double edge = (plate.Width - grid.TransverseSpan) / 2;
            double antInside = (grid.TransverseSpan - d0) * plate.Thickness;
            double antOutside = (2 * edge - d0) * plate.Thickness;
            // Exactly the declared number of rows; P398 D.1 p136's substitution
            // uses four although the drawing and n1 declaration specify three.
            double anv = 2 * plate.Thickness * (end + grid.LongitudinalSpan - (grid.LongitudinalRows - .5) * d0);
            double yield = SectionResistanceArithmetic.Normal(area, plate.Fy, factors.GammaM0);
            double rupture = SectionResistanceArithmetic.NetTension(net, plate.Fu, factors.GammaM2);
            double blockInside = antInside * plate.Fu / factors.GammaM2 + PlasticShear.Resistance(anv, plate.Fy, factors.GammaM0);
            double blockOutside = antOutside * plate.Fu / factors.GammaM2 + PlasticShear.Resistance(anv, plate.Fy, factors.GammaM0);
            var capacities = new[] { yield, rupture, blockInside, blockOutside };
            if (capacities.Any(c => !Finite(c) || c <= 0)) { issues.Add(id + ".RESISTANCE_OUTSIDE_NUMERIC_RANGE"); return; }
            string normal = (p.Policy.Code.IsNtc ? "NTC2018 4.2.4.1.2" : "EN1993-1-1:2005 6.2.3-6.2.4") + "; SCI P398 pp56-57";
            string block = "EN1993-1-8:2005 3.10.2; SCI P398 p56, D.1 p136 (actual row count)"
                + (p.Policy.Code.IsNtc ? "; supplementary method with NTC factors" : "");
            double tension = Math.Max(0, n), compression = Math.Max(0, -n);
            checks.Add(new ConnectionCheck(id + ".GrossTension", tension, yield, normal));
            checks.Add(new ConnectionCheck(id + ".NetTension", tension, rupture, normal));
            checks.Add(new ConnectionCheck(id + ".BlockInside", tension, blockInside, block));
            checks.Add(new ConnectionCheck(id + ".BlockOutside", tension, blockOutside, block));
            checks.Add(new ConnectionCheck(id + ".GrossCompression", compression, yield, normal));
            trace.Add(new SectionResistanceValue(id + ".GrossArea", area, "mm2"));
            trace.Add(new SectionResistanceValue(id + ".NetArea", net, "mm2"));
            trace.Add(new SectionResistanceValue(id + ".BlockShearNetArea", anv, "mm2"));
            trace.Add(new SectionResistanceValue(id + ".BlockTensionInsideNetArea", antInside, "mm2"));
            trace.Add(new SectionResistanceValue(id + ".BlockTensionOutsideNetArea", antOutside, "mm2"));
            if (!cover) return;
            double pitch = Math.Max(grid.LongitudinalPitch, 2 * grid.FirstBoltDistance);
            double epsilon = Math.Sqrt(235 / plate.Fy), length = .6 * pitch;
            double inertia = plate.Width * Math.Pow(plate.Thickness, 3) / 12;
            double ncr = Math.PI * Math.PI * p.Policy.ElasticModulus * inertia / (length * length);
            double lambda = BucklingReductionArithmetic.AxialSlenderness(area, plate.Fy, ncr);
            bool exempt = pitch / plate.Thickness <= 9 * epsilon;
            double chi = exempt ? 1 : BucklingReductionArithmetic.AxialChi(BucklingReductionArithmetic.Phi(.49, lambda), lambda);
            double buckling = exempt ? yield : chi * area * plate.Fy / p.Policy.GammaM1;
            if (!Finite(buckling) || !Finite(lambda) || buckling <= 0) { issues.Add(id + ".BUCKLING_OUTSIDE_NUMERIC_RANGE"); return; }
            checks.Add(new ConnectionCheck(id + ".CoverBuckling", compression, buckling,
                "SCI P398 p57; EN1993-1-1:2005 6.3.1 curve c; gammaM1=" + p.Policy.GammaM1.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            trace.Add(new SectionResistanceValue(id + ".MaximumUnrestrainedPitch", pitch, "mm"));
            trace.Add(new SectionResistanceValue(id + ".EffectiveBucklingLength", length, "mm"));
            trace.Add(new SectionResistanceValue(id + ".EulerForce", ncr, "N"));
            trace.Add(new SectionResistanceValue(id + ".Slenderness", lambda, "1"));
            trace.Add(new SectionResistanceValue(id + ".Chi", chi, "1"));
            trace.Add(new SectionResistanceValue(id + ".BucklingExempt", exempt ? 1 : 0, "1"));
        }

        internal static IBeamSpliceForceResult Prepare(IBeamSplicePlateInput p, IList<string> issues, IList<SectionResistanceValue> trace)
        {
            var distribution = IBeamSpliceBolts.Prepare(p.Forces, p.Bolts, issues, trace);
            trace.Add(new SectionResistanceValue("GammaM0", p.Policy.Code.Plates.GammaM0, "1"));
            trace.Add(new SectionResistanceValue("GammaM1", p.Policy.GammaM1, "1"));
            trace.Add(new SectionResistanceValue("GammaM2Plate", p.Policy.Code.Plates.GammaM2, "1"));
            trace.Add(new SectionResistanceValue("GammaM2Connection", p.Policy.Code.GammaM2Connection, "1"));
            trace.Add(new SectionResistanceValue("ElasticModulus", p.Policy.ElasticModulus, "MPa"));
            if (string.IsNullOrWhiteSpace(p.UltimateCombinationSource)) issues.Add("ULTIMATE_COMBINATION_SOURCE_REQUIRED");
            if (p.Forces.FlangeBolts.TransverseRows != 2 || p.Forces.FlangeBolts.LongitudinalRows < 2)
                issues.Add("FLANGE_REQUIRES_TWO_TRANSVERSE_COLUMNS_AND_AT_LEAST_TWO_LONGITUDINAL_ROWS");
            if (p.Forces.WebBolts.TransverseRows < 2 || p.Forces.WebBolts.LongitudinalRows < 2)
                issues.Add("WEB_REQUIRES_AT_LEAST_TWO_ROWS_IN_BOTH_DIRECTIONS");
            foreach (bool web in new[] { false, true })
            {
                var grid = web ? p.Forces.WebBolts : p.Forces.FlangeBolts;
                var bolt = web ? p.Bolts.WebBolt : p.Bolts.FlangeBolt;
                var main = Main(p, web); var cover = web ? p.WebCover : p.FlangeCover;
                string name = web ? "Web" : "Flange";
                double end = web ? p.WebCoverEnd : p.FlangeCoverEnd;
                double mainEnd = grid.FirstBoltDistance - p.ButtGap / 2;
                double envelope = web ? p.Bolts.WebHardwareEnvelopeDiameter : p.Bolts.FlangeHardwareEnvelopeDiameter;
                // Table3.3 footnote3: thinner OUTER connected part. The web is
                // sandwiched between two covers and is not an outer part.
                double t = web ? cover.Thickness : Math.Min(main.Thickness, cover.Thickness), d0 = bolt.HoleDiameter;
                if (main.Fy > 460 || cover.Fy > 460) issues.Add(name + ".CARBON_STEEL_UP_TO_S460_ONLY");
                if (cover.Width > main.Width) issues.Add(name + ".COVER_OUTSIDE_AVAILABLE_STEEL");
                double mainEdge = (main.Width - grid.TransverseSpan) / 2, coverEdge = (cover.Width - grid.TransverseSpan) / 2;
                if (new[] { mainEnd, end, mainEdge, coverEdge }.Any(v => v < Math.Max(1.2 * d0, envelope / 2)))
                    issues.Add(name + ".MINIMUM_END_OR_EDGE_DISTANCE");
                if (grid.LongitudinalPitch < (web ? 2.4 : 2.2) * d0 || grid.TransversePitch < 2.4 * d0)
                    issues.Add(name + ".MINIMUM_BOLT_PITCH");
                double maximumPitch = Math.Min(14 * t, 200);
                if (grid.LongitudinalPitch > maximumPitch || grid.TransversePitch > maximumPitch || 2 * grid.FirstBoltDistance > maximumPitch)
                    issues.Add(name + ".MAXIMUM_BOLT_PITCH");
                if (p.Exposed && new[] { mainEnd, end, coverEdge, web ? 0 : mainEdge }.Any(e => e > 4 * t + 40))
                    issues.Add(name + ".MAXIMUM_EXPOSED_EDGE_DISTANCE");
                BoltComponentRules.RoundHole(bolt, issues);
                BoltComponentRules.NtcHole(p.Policy.Code, bolt, p.Bolts.HoleClearanceServiceabilitySource, issues);
                trace.Add(new SectionResistanceValue(name + ".MemberEndDistance", mainEnd, "mm"));
                trace.Add(new SectionResistanceValue(name + ".CoverLength", 2 * (grid.FirstBoltDistance + grid.LongitudinalSpan + end), "mm"));
            }
            return distribution;
        }

        internal static ConnectionPlate Main(IBeamSplicePlateInput p, bool web) => web
            ? new ConnectionPlate(p.Forces.Section.ClearWeb, p.Forces.Section.WebThickness, p.Forces.Section.Fy, p.Forces.Section.Fu)
            : new ConnectionPlate(p.Forces.Section.Width, p.Forces.Section.FlangeThickness, p.BeamFlangeFy, p.BeamFlangeFu);

        private static double BearingCapacity(IBeamSplicePlateInput p, ConnectionBolt bolt, ConnectionPlate plate,
            double end, double edge, double pitch, double gauge, int along, int across, string id, IList<string> issues)
        {
            double capacity = double.PositiveInfinity;
            foreach (bool endBolt in along > 1 ? new[] { true, false } : new[] { true })
                foreach (bool edgeBolt in across > 2 ? new[] { true, false } : new[] { true })
                {
                    var r = BoltBearing.Verify(new BoltBearingInput(p.Policy.Code, bolt, plate, 0, end, edge,
                        pitch, gauge, endBolt, edgeBolt, p.Bolts.HoleClearanceServiceabilitySource));
                    foreach (var issue in r.Diagnostics) issues.Add(id + "." + issue);
                    if (r.IsComplete) capacity = Math.Min(capacity, r.Checks.Single().Resistance);
                }
            return capacity;
        }

        internal static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        internal static ConnectionVerification<IBeamSplicePlateInput> Result(IBeamSplicePlateInput p,
            string method, string scope, IList<ConnectionCheck> checks, List<SectionResistanceValue> trace,
            IList<string> issues, params string[] assumptions)
        {
            if (trace.Any(t => !Finite(t.Value)))
            { issues.Add("PLATE_CALCULATION_OUTSIDE_NUMERIC_RANGE"); trace.RemoveAll(t => !Finite(t.Value)); }
            return new ConnectionVerification<IBeamSplicePlateInput>(p, p.Policy.Code, "Steel.Connection.IBeamSplice." + method,
                scope, checks, trace, issues, assumptions.Concat(new[] {
                    "Normal round holes; filled holes in compression; no packing or end contact. Material strengths belong to the actual product/thickness.",
                    "Conservative detailing: pitches including the central bolt spacing <=min(14*tOuter,200); exposed free edges <=4*tOuter+40. tOuter is the thinner OUTER connected part (EN1993-1-8 Table3.3 footnote3); the sandwiched web is not an outer part. The table also supplements NTC.",
                    "Component Passed does not qualify the complete joint. Member/local stability, load introduction, secondary bending and global-model compatibility require their own checks."
                }));
        }
    }
}
