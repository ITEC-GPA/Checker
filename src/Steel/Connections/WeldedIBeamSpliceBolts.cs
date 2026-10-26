using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public sealed class WeldedIBeamSpliceBoltInput
    {
        public ConnectionDesignCode Code { get; }
        public WeldedIBeamSpliceForceInput Detail { get; }
        public ConnectionBolt FlangeBolt { get; }
        public ConnectionBolt WebBolt { get; }
        public double FlangeHardwareDiameter { get; }
        public double WebHardwareDiameter { get; }
        public string UltimateCombinationSource { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public WeldedIBeamSpliceBoltInput(ConnectionDesignCode code, WeldedIBeamSpliceForceInput detail,
            ConnectionBolt flangeBolt, ConnectionBolt webBolt, double flangeHardwareDiameter, double webHardwareDiameter,
            string ultimateCombinationSource, string holeClearanceServiceabilitySource = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code)); Detail = detail ?? throw new ArgumentNullException(nameof(detail));
            FlangeBolt = flangeBolt ?? throw new ArgumentNullException(nameof(flangeBolt)); WebBolt = webBolt ?? throw new ArgumentNullException(nameof(webBolt));
            ConnectionValidation.Positive(flangeHardwareDiameter, nameof(flangeHardwareDiameter));
            ConnectionValidation.Positive(webHardwareDiameter, nameof(webHardwareDiameter));
            FlangeHardwareDiameter = flangeHardwareDiameter; WebHardwareDiameter = webHardwareDiameter;
            UltimateCombinationSource = ultimateCombinationSource; HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }

    /// <summary>Actual double-shear bolt planes and all three bearing plies. The parent-ply
    /// demand is the VECTOR sum of the two cover demands at the same physical bolt.
    /// A bolt/component Passed is not a complete splice or a category B/C check.</summary>
    public sealed class WeldedIBeamSpliceBoltResult
    {
        public WeldedIBeamSpliceForceResult Forces { get; }
        public ConnectionVerification<WeldedIBeamSpliceBoltInput> ComponentAssessment { get; }
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<string> PendingChecks { get; } = Array.AsReadOnly(new[] {
            "SLIP_AND_PRELOADING_FOR_THE_REQUESTED_CATEGORY", "NET_GROSS_BLOCK_AND_STABILITY_OF_ALL_PLIES",
            "FLANGE_SECONDARY_BENDING_AND_LOCAL_FORCE_INTRODUCTION", "MEMBER_AND_GLOBAL_MODEL_REQUIREMENTS", "FATIGUE_NOT_IN_SCOPE" });
        internal WeldedIBeamSpliceBoltResult(WeldedIBeamSpliceForceResult forces, ConnectionVerification<WeldedIBeamSpliceBoltInput> assessment)
        { Forces = forces; ComponentAssessment = assessment; }
    }

    public static class WeldedIBeamSpliceBolts
    {
        public static WeldedIBeamSpliceBoltResult Verify(WeldedIBeamSpliceBoltInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var f = WeldedIBeamSpliceForces.Calculate(p.Detail); var d = p.Detail;
            var issues = new List<string>(f.Diagnostics); var trace = new List<SectionResistanceValue>(f.Trace);
            var checks = new List<ConnectionCheck>();
            if (string.IsNullOrWhiteSpace(p.UltimateCombinationSource)) issues.Add("ULTIMATE_COMBINATION_SOURCE_REQUIRED");
            if (p.FlangeHardwareDiameter < p.FlangeBolt.HoleDiameter || p.WebHardwareDiameter < p.WebBolt.HoleDiameter)
                issues.Add("HARDWARE_ENVELOPE_MUST_COVER_HOLE");
            foreach (var item in new[] { Tuple.Create(d.Top.Bolts, p.FlangeHardwareDiameter),
                Tuple.Create(d.Bottom.Bolts, p.FlangeHardwareDiameter), Tuple.Create(d.WebBolts, p.WebHardwareDiameter) })
                if (item.Item1.LongitudinalPitch < item.Item2 || item.Item1.TransversePitch < item.Item2
                    || 2 * item.Item1.FirstBoltDistance < item.Item2) issues.Add("ADJACENT_BOLT_HARDWARE_OVERLAP");
            if (issues.Count == 0)
            {
                foreach (var g in f.CoverGroups)
                {
                    bool top = g.Cover == WeldedSpliceCover.TopOuter || g.Cover == WeldedSpliceCover.TopInnerNegative || g.Cover == WeldedSpliceCover.TopInnerPositive;
                    var flange = top ? d.Top : d.Bottom; var grid = g.IsWeb ? d.WebBolts : flange.Bolts;
                    var bolt = g.IsWeb ? p.WebBolt : p.FlangeBolt;
                    double end = g.IsWeb ? d.WebCoverEnd : flange.EndDistance;
                    double halfLength = grid.FirstBoltDistance + grid.LongitudinalSpan + end;
                    var groupedColumns = g.Distribution.Points.GroupBy(point => point.X).ToArray();
                    int across = groupedColumns[0].Count();
                    double transverseSpan = g.Distribution.Points.Max(point => point.Y) - g.Distribution.Points.Min(point => point.Y);
                    // A changing force direction is bounded by the rectangle diagonal.
                    // Retain this conservative length for every group, without a uniform-transfer exemption.
                    double jointLength = ElasticBoltGroup.Hypot(grid.LongitudinalSpan, transverseSpan);
                    trace.Add(new SectionResistanceValue(g.Side + "." + g.Cover + ".ConservativeJointLength", jointLength, "mm"));
                    trace.Add(new SectionResistanceValue(g.Side + "." + g.Cover + ".PackingThicknessForShear", d.Packing.ThickerFace(g.Cover), "mm"));
                    trace.Add(new SectionResistanceValue(g.Side + "." + g.Cover + ".GripThickness", d.Packing.Grip(d, g.Cover), "mm"));
                    foreach (var force in g.Distribution.Forces)
                    {
                        string id = g.Side + "." + g.Cover + ".Bolt" + force.Index;
                        // One check per physical shear plane: unequal/opposite forces are not replaced by half of the resultant.
                        var strength = BoltStrength.Verify(new BoltStrengthInput(p.Code, bolt, force.Shear, 0, 1,
                            jointLength, d.Packing.ThickerFace(g.Cover), p.HoleClearanceServiceabilitySource));
                        foreach (string issue in strength.Diagnostics) issues.Add(id + ":" + issue);
                        if (strength.IsComplete)
                        {
                            var shear = strength.Checks.Single(c => c.Id == "Bolt.Shear");
                            checks.Add(new ConnectionCheck(id + ".ShearPlane", shear.Demand, shear.Resistance, shear.Reference));
                        }
                        Bearing(g.Plate, bolt, grid, across, halfLength - Math.Abs(force.Position.X),
                            Math.Min(force.Position.Y - g.TransverseMinimum, g.TransverseMaximum - force.Position.Y),
                            force.Fx, force.Fy, g.IsWeb ? p.WebHardwareDiameter : p.FlangeHardwareDiameter, id + ".Cover");
                    }
                }
                foreach (SpliceSide side in new[] { SpliceSide.Left, SpliceSide.Right })
                foreach (IBeamSplicePart part in new[] { IBeamSplicePart.TopFlange, IBeamSplicePart.BottomFlange, IBeamSplicePart.Web })
                {
                    bool web = part == IBeamSplicePart.Web, top = part == IBeamSplicePart.TopFlange;
                    var grid = web ? d.WebBolts : top ? d.Top.Bolts : d.Bottom.Bolts;
                    var bolt = web ? p.WebBolt : p.FlangeBolt;
                    var parent = web ? d.Section.Web : top ? d.Section.TopFlange : d.Section.BottomFlange;
                    var groups = f.CoverGroups.Where(g => g.Side == side && (web ? g.IsWeb : top
                        ? g.Cover == WeldedSpliceCover.TopOuter || g.Cover == WeldedSpliceCover.TopInnerNegative || g.Cover == WeldedSpliceCover.TopInnerPositive
                        : g.Cover == WeldedSpliceCover.BottomOuter || g.Cover == WeldedSpliceCover.BottomInnerNegative || g.Cover == WeldedSpliceCover.BottomInnerPositive));
                    var locations = groups.SelectMany(g => g.Distribution.Forces).GroupBy(force => force.Position);
                    int index = 0;
                    foreach (var location in locations)
                    {
                        index++; var pos = location.Key; double fx = location.Sum(force => force.Fx), fy = location.Sum(force => force.Fy);
                        string id = side + "." + part + ".Parent.Bolt" + index;
                        if (location.Count() != 2) { issues.Add(id + ".EXACTLY_TWO_SHEAR_PLANES_REQUIRED"); continue; }
                        Bearing(parent, bolt, grid, grid.TransverseRows, Math.Abs(pos.X) - d.ButtGap / 2,
                            parent.Width / 2 - Math.Abs(pos.Y), fx, fy, web ? p.WebHardwareDiameter : p.FlangeHardwareDiameter, id);
                        trace.Add(new SectionResistanceValue(id + ".Fx", fx, "N")); trace.Add(new SectionResistanceValue(id + ".Fy", fy, "N"));
                    }
                }
            }
            var assessment = new ConnectionVerification<WeldedIBeamSpliceBoltInput>(p, p.Code, "Steel.Connection.WeldedIBeamSplice.Bolts",
                "BOLT AND BEARING COMPONENTS ONLY: each real shear plane and each cover/parent ply in the welded-I double-cover splice. "
                + "No net/block, plate stability, slip, execution, fatigue or whole-joint qualification.", checks, trace, issues,
                new[] { "Same immutable geometry and one concomitant ULS case. Both sides and every physical bolt plane are assessed; no redistribution to hide a failed plane.",
                    "Cover centroid force split is an equilibrium model, not a certification of secondary plate bending or local load introduction.",
                    "Bearing uses the conservative linear interaction |Fx|/Fb,x+|Fy|/Fb,y<=1, with the smaller end/interior resistance for each direction. This is a declared design model, not an EC oblique-bearing equation.",
                    "Round holes, no countersinks, identical thread condition in the two planes. Single continuous non-structural packings use EN1993-1-8 3.6.1(12)-(13), the thicker face for BOTH shear planes; no packing resistance is credited. The group rectangle diagonal conservatively bounds the changing force-direction transfer length for the long-joint reduction; no uniform-transfer exemption.",
                    "Minimum physical edge, hole/pitch and hardware clearances are checked. Maximum pitch/exposure detailing and member seam resistance belong to the remaining plate/detail checks." });
            return new WeldedIBeamSpliceBoltResult(f, assessment);

            void Bearing(ConnectionPlate plate, ConnectionBolt bolt, SpliceBoltLayout grid, int across,
                double ex, double ey, double fx, double fy, double hardware, string id)
            {
                double minimum = Math.Max(1.2 * bolt.HoleDiameter, hardware / 2);
                if (ex < minimum || ey < minimum) { issues.Add(id + ".PHYSICAL_EDGE_OR_HARDWARE_CLEARANCE"); return; }
                double rx = Capacity(ex, ey, grid.LongitudinalPitch, across > 1 ? (double?)grid.TransversePitch : null, grid.LongitudinalRows, across);
                double ry = Capacity(ey, ex, across > 1 ? (double?)grid.TransversePitch : null, grid.LongitudinalPitch, across, grid.LongitudinalRows);
                if (!(rx > 0) || !(ry > 0)) return;
                double ratio = Math.Abs(fx) / rx + Math.Abs(fy) / ry;
                if (double.IsNaN(ratio) || double.IsInfinity(ratio)) { issues.Add(id + ".BEARING_NUMERIC_RANGE"); return; }
                checks.Add(new ConnectionCheck(id + ".BearingInteraction", ratio, 1,
                    (p.Code.IsNtc ? "NTC2018 4.2.8.1.1 eq4.2.67" : "EN1993-1-8:2005 Table3.4") + "; conservative directional linear interaction", "1"));
                trace.Add(new SectionResistanceValue(id + ".BearingX", rx, "N")); trace.Add(new SectionResistanceValue(id + ".BearingY", ry, "N"));
                double Capacity(double e1, double e2, double? p1, double? p2, int alongCount, int acrossCount)
                {
                    double resistance = double.PositiveInfinity;
                    foreach (bool endBolt in alongCount > 1 ? new[] { true, false } : new[] { true })
                    foreach (bool edgeBolt in acrossCount > 2 ? new[] { true, false } : new[] { true })
                    {
                        var r = BoltBearing.Verify(new BoltBearingInput(p.Code, bolt, plate, 0, e1, e2, p1, p2,
                            endBolt, edgeBolt, p.HoleClearanceServiceabilitySource));
                        foreach (string issue in r.Diagnostics) issues.Add(id + ":" + issue);
                        if (!r.IsComplete) return 0;
                        resistance = Math.Min(resistance, r.Checks.Single().Resistance);
                    }
                    return resistance;
                }
            }
        }
    }
}
