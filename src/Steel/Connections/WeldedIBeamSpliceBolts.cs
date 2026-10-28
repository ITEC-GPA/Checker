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
        /// <summary>Same code, hardware and hole evidence as <paramref name="physical"/> for another action state on the
        /// same physical parts: the demand-independent bolt preparation is shared.</summary>
        internal WeldedIBeamSpliceBoltInput(WeldedIBeamSpliceBoltInput physical, WeldedIBeamSpliceForceInput detail, string ultimateCombinationSource)
        {
            if (!ReferenceEquals(physical.Detail.Geometry, detail.Geometry)) throw new ArgumentException("The case must share the physical parts of the prepared detail.", nameof(detail));
            Code = physical.Code; Detail = detail; FlangeBolt = physical.FlangeBolt; WebBolt = physical.WebBolt;
            FlangeHardwareDiameter = physical.FlangeHardwareDiameter; WebHardwareDiameter = physical.WebHardwareDiameter;
            UltimateCombinationSource = ultimateCombinationSource; HoleClearanceServiceabilitySource = physical.HoleClearanceServiceabilitySource;
            prepared = physical.Prepared;
        }
        private SpliceBoltPreparation prepared;
        /// <summary>Demand-independent bolt and bearing data of this physical detail.</summary>
        internal SpliceBoltPreparation Prepared => prepared ?? System.Threading.LazyInitializer.EnsureInitialized(ref prepared, () => new SpliceBoltPreparation(this));
        /// <summary>True when both inputs are action states of one prepared physical detail (reference identity).</summary>
        internal bool SharesPreparation(WeldedIBeamSpliceBoltInput other) => prepared != null && ReferenceEquals(prepared, other.prepared);
    }

    /// <summary>Bolt strengths, bearing resistances, identifiers and geometric trace of one physical
    /// detail: none depends on the actions. Evaluation adds the demands in the original order.</summary>
    internal sealed class SpliceBoltPreparation
    {
        internal sealed class Bearing
        {
            internal string[] Issues;
            internal bool Valid;
            internal double Rx, Ry;
            internal string CheckId, NumericIssue;
            internal SectionResistanceValue X, Y, Beta;
        }
        internal sealed class Bolt { internal string Id, ShearId; internal Bearing Cover; }
        internal sealed class Group
        {
            internal SectionResistanceValue Joint, Packing, Grip;
            internal ConnectionBolt BoltType;
            internal double JointLength, PackingThickness;
            internal bool StrengthComplete;
            internal double ShearResistance;
            internal string Reference;
            internal Bolt[] Bolts;
        }
        internal sealed class Location
        {
            internal string Id, CountIssue, FxName, FyName;
            internal int Count;
            internal int[] Groups, Forces;
            internal Bearing Bearing;
        }
        internal sealed class Layout { internal Group[] Groups; internal Location[] Locations; }

        internal readonly string[] HardwareIssues;
        internal readonly string BearingReference;
        private readonly Lazy<Layout> layout;
        internal Layout Prepared => layout.Value;

        internal SpliceBoltPreparation(WeldedIBeamSpliceBoltInput p)
        {
            var d = p.Detail; var issues = new List<string>();
            if (p.FlangeHardwareDiameter < p.FlangeBolt.HoleDiameter || p.WebHardwareDiameter < p.WebBolt.HoleDiameter)
                issues.Add("HARDWARE_ENVELOPE_MUST_COVER_HOLE");
            foreach (var item in new[] { Tuple.Create(d.Top.Bolts, p.FlangeHardwareDiameter),
                Tuple.Create(d.Bottom.Bolts, p.FlangeHardwareDiameter), Tuple.Create(d.WebBolts, p.WebHardwareDiameter) })
                if (item.Item1.LongitudinalRows > 1 && item.Item1.LongitudinalPitch < item.Item2
                    || item.Item1.TransverseRows > 1 && item.Item1.TransversePitch < item.Item2
                    || 2 * item.Item1.FirstBoltDistance < item.Item2) issues.Add("ADJACENT_BOLT_HARDWARE_OVERLAP");
            HardwareIssues = issues.ToArray();
            BearingReference = (p.Code.IsNtc ? "NTC2018 4.2.8.1.1 eq4.2.67" : "EN1993-1-8:2005 Table3.4") + "; conservative directional linear interaction; SCI P398 p55 long-joint reduction";
            layout = new Lazy<Layout>(() => Build(p), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>Built on first use, when the force groups of this geometry are known to be supported.</summary>
        private Layout Build(WeldedIBeamSpliceBoltInput p)
        {
            var d = p.Detail; var descriptors = d.Geometry.Groups;
            var bearings = new Dictionary<(ConnectionPlate, ConnectionBolt, double, double, double?, double?, bool, bool), ConnectionVerification<BoltBearingInput>>();
            var groups = new Group[descriptors.Length];
            for (int gi = 0; gi < descriptors.Length; gi++)
            {
                var g = descriptors[gi]; var points = g.Group.Value.Points;
                bool isWeb = g.Cover == WeldedSpliceCover.WebNegative || g.Cover == WeldedSpliceCover.WebPositive;
                bool top = g.Cover == WeldedSpliceCover.TopOuter || g.Cover == WeldedSpliceCover.TopInnerNegative || g.Cover == WeldedSpliceCover.TopInnerPositive;
                var flange = top ? d.Top : d.Bottom; var grid = isWeb ? d.WebBolts : flange.Bolts;
                var bolt = isWeb ? p.WebBolt : p.FlangeBolt;
                double end = isWeb ? d.WebCoverEnd : flange.EndDistance;
                double halfLength = grid.FirstBoltDistance + grid.LongitudinalSpan + end;
                var groupedColumns = points.GroupBy(point => point.X).ToArray();
                int across = groupedColumns[0].Count();
                double transverseSpan = points.Max(point => point.Y) - points.Min(point => point.Y);
                // A changing force direction is bounded by the rectangle diagonal.
                // Retain this conservative length for every group, without a uniform-transfer exemption.
                double jointLength = ElasticBoltGroup.Hypot(grid.LongitudinalSpan, transverseSpan);
                string prefix = g.Side + "." + g.Cover;
                var group = new Group
                {
                    Joint = new SectionResistanceValue(prefix + ".ConservativeJointLength", jointLength, "mm"),
                    Packing = new SectionResistanceValue(prefix + ".PackingThicknessForShear", d.Packing.ThickerFace(g.Cover), "mm"),
                    Grip = new SectionResistanceValue(prefix + ".GripThickness", d.Packing.Grip(d, g.Cover), "mm"),
                    BoltType = bolt, JointLength = jointLength, PackingThickness = d.Packing.ThickerFace(g.Cover)
                };
                // Fv,Rd depends on the bolt, joint length and packing of the group, not on the demand:
                // evaluate it once per detail. With diagnostics keep the per-bolt path and its messages.
                var strength = BoltStrength.Verify(new BoltStrengthInput(p.Code, bolt, 0, 0, 1,
                    jointLength, group.PackingThickness, p.HoleClearanceServiceabilitySource), out var values);
                group.StrengthComplete = strength.IsComplete; group.ShearResistance = values.ShearResistance; group.Reference = values.Reference;
                double hardware = isWeb ? p.WebHardwareDiameter : p.FlangeHardwareDiameter;
                double betaLong = BoltLongJoint.Reduction(jointLength, bolt.Diameter);
                group.Bolts = new Bolt[points.Length];
                for (int k = 0; k < points.Length; k++)
                {
                    var position = points[k]; string id = prefix + ".Bolt" + k;
                    group.Bolts[k] = new Bolt
                    {
                        Id = id, ShearId = id + ".ShearPlane",
                        Cover = PrepareBearing(g.Plate, bolt, grid, across, halfLength - Math.Abs(position.X),
                            Math.Min(position.Y - g.Minimum, g.Maximum - position.Y), hardware, id + ".Cover", betaLong)
                    };
                }
                groups[gi] = group;
            }
            var locations = new List<Location>();
            foreach (SpliceSide side in new[] { SpliceSide.Left, SpliceSide.Right })
            foreach (IBeamSplicePart part in new[] { IBeamSplicePart.TopFlange, IBeamSplicePart.BottomFlange, IBeamSplicePart.Web })
            {
                bool web = part == IBeamSplicePart.Web, top = part == IBeamSplicePart.TopFlange;
                var grid = web ? d.WebBolts : top ? d.Top.Bolts : d.Bottom.Bolts;
                var bolt = web ? p.WebBolt : p.FlangeBolt;
                var parent = web ? d.Section.Web : top ? d.Section.TopFlange : d.Section.BottomFlange;
                var members = Enumerable.Range(0, descriptors.Length).Where(gi => descriptors[gi].Side == side && (web
                    ? descriptors[gi].Cover == WeldedSpliceCover.WebNegative || descriptors[gi].Cover == WeldedSpliceCover.WebPositive
                    : top ? descriptors[gi].Cover == WeldedSpliceCover.TopOuter || descriptors[gi].Cover == WeldedSpliceCover.TopInnerNegative || descriptors[gi].Cover == WeldedSpliceCover.TopInnerPositive
                    : descriptors[gi].Cover == WeldedSpliceCover.BottomOuter || descriptors[gi].Cover == WeldedSpliceCover.BottomInnerNegative || descriptors[gi].Cover == WeldedSpliceCover.BottomInnerPositive));
                var located = members.SelectMany(gi => descriptors[gi].Group.Value.Points.Select((point, k) => (gi, k, point))).GroupBy(x => x.point);
                double betaLong = BoltLongJoint.Reduction(ElasticBoltGroup.Hypot(grid.LongitudinalSpan, grid.TransverseSpan), bolt.Diameter);
                double hardware = web ? p.WebHardwareDiameter : p.FlangeHardwareDiameter;
                int index = 0;
                foreach (var location in located)
                {
                    index++; var pos = location.Key; var items = location.ToArray();
                    string id = side + "." + part + ".Parent.Bolt" + index;
                    var entry = new Location
                    {
                        Id = id, CountIssue = id + ".EXACTLY_TWO_SHEAR_PLANES_REQUIRED", FxName = id + ".Fx", FyName = id + ".Fy",
                        Count = items.Length, Groups = items.Select(x => x.gi).ToArray(), Forces = items.Select(x => x.k).ToArray()
                    };
                    if (entry.Count == 2) entry.Bearing = PrepareBearing(parent, bolt, grid, grid.TransverseRows, Math.Abs(pos.X) - d.ButtGap / 2,
                        parent.Width / 2 - Math.Abs(pos.Y), hardware, id, betaLong);
                    locations.Add(entry);
                }
            }
            return new Layout { Groups = groups, Locations = locations.ToArray() };

            Bearing PrepareBearing(ConnectionPlate plate, ConnectionBolt bolt, SpliceBoltLayout grid, int across,
                double ex, double ey, double hardware, string id, double betaLong)
            {
                var issues = new List<string>(); var b = new Bearing();
                double minimum = Math.Max(1.2 * bolt.HoleDiameter, hardware / 2);
                if (ex < minimum || ey < minimum) issues.Add(id + ".PHYSICAL_EDGE_OR_HARDWARE_CLEARANCE");
                else
                {
                    // Same policy as IBeamSplicePlates: SCI P398 p55 extends the long-joint reduction to bearing.
                    double rx = betaLong * Capacity(ex, ey, grid.LongitudinalRows > 1 ? (double?)grid.LongitudinalPitch : null, across > 1 ? (double?)grid.TransversePitch : null, grid.LongitudinalRows, across);
                    double ry = betaLong * Capacity(ey, ex, across > 1 ? (double?)grid.TransversePitch : null, grid.LongitudinalRows > 1 ? (double?)grid.LongitudinalPitch : null, across, grid.LongitudinalRows);
                    if (rx > 0 && ry > 0)
                    {
                        b.Valid = true; b.Rx = rx; b.Ry = ry; b.CheckId = id + ".BearingInteraction"; b.NumericIssue = id + ".BEARING_NUMERIC_RANGE";
                        b.X = new SectionResistanceValue(id + ".BearingX", rx, "N"); b.Y = new SectionResistanceValue(id + ".BearingY", ry, "N");
                        b.Beta = new SectionResistanceValue(id + ".BearingBetaLongJoint", betaLong, "1");
                    }
                }
                b.Issues = issues.ToArray();
                return b;
                double Capacity(double e1, double e2, double? p1, double? p2, int alongCount, int acrossCount)
                {
                    double resistance = double.PositiveInfinity;
                    foreach (bool endBolt in alongCount > 1 ? new[] { true, false } : new[] { true })
                    foreach (bool edgeBolt in acrossCount > 2 ? new[] { true, false } : new[] { true })
                    {
                        // Same ply, bolt, distances and pitches give the same resistance: evaluate once per detail.
                        var memo = (plate, bolt, e1, e2, p1, p2, endBolt, edgeBolt);
                        if (!bearings.TryGetValue(memo, out var r))
                            bearings.Add(memo, r = BoltBearing.Verify(new BoltBearingInput(p.Code, bolt, plate, 0, e1, e2, p1, p2,
                                endBolt, edgeBolt, p.HoleClearanceServiceabilitySource)));
                        foreach (string issue in r.Diagnostics) issues.Add(id + ":" + issue);
                        if (!r.IsComplete) return 0;
                        resistance = Math.Min(resistance, r.Checks.Single().Resistance);
                    }
                    return resistance;
                }
            }
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
            var f = WeldedIBeamSpliceForces.Calculate(p.Detail); var d = p.Detail; var prepared = p.Prepared;
            var issues = new List<string>(f.Diagnostics); var trace = new List<SectionResistanceValue>(f.Trace);
            var checks = new List<ConnectionCheck>();
            if (string.IsNullOrWhiteSpace(p.UltimateCombinationSource)) issues.Add("ULTIMATE_COMBINATION_SOURCE_REQUIRED");
            issues.AddRange(prepared.HardwareIssues);
            if (issues.Count == 0)
            {
                var layout = prepared.Prepared;
                for (int gi = 0; gi < f.CoverGroups.Count; gi++)
                {
                    var g = f.CoverGroups[gi]; var pg = layout.Groups[gi];
                    trace.Add(pg.Joint); trace.Add(pg.Packing); trace.Add(pg.Grip);
                    var forces = g.Distribution.Forces;
                    for (int k = 0; k < forces.Count; k++)
                    {
                        var force = forces[k]; var pb = pg.Bolts[k];
                        // One check per physical shear plane: unequal/opposite forces are not replaced by half of the resultant.
                        if (pg.StrengthComplete)
                            checks.Add(new ConnectionCheck(pb.ShearId, force.Shear, pg.ShearResistance, pg.Reference));
                        else
                        {
                            var strength = BoltStrength.Verify(new BoltStrengthInput(p.Code, pg.BoltType, force.Shear, 0, 1,
                                pg.JointLength, pg.PackingThickness, p.HoleClearanceServiceabilitySource), out var values);
                            foreach (string issue in strength.Diagnostics) issues.Add(pb.Id + ":" + issue);
                            if (strength.IsComplete)
                                checks.Add(new ConnectionCheck(pb.ShearId, force.Shear, values.ShearResistance, values.Reference));
                        }
                        Bearing(pb.Cover, force.Fx, force.Fy);
                    }
                }
                foreach (var location in layout.Locations)
                {
                    double fx = 0, fy = 0;
                    for (int j = 0; j < location.Count; j++) fx += f.CoverGroups[location.Groups[j]].Distribution.Forces[location.Forces[j]].Fx;
                    for (int j = 0; j < location.Count; j++) fy += f.CoverGroups[location.Groups[j]].Distribution.Forces[location.Forces[j]].Fy;
                    if (location.Count != 2) { issues.Add(location.CountIssue); continue; }
                    Bearing(location.Bearing, fx, fy);
                    trace.Add(new SectionResistanceValue(location.FxName, fx, "N")); trace.Add(new SectionResistanceValue(location.FyName, fy, "N"));
                }
            }
            var assessment = new ConnectionVerification<WeldedIBeamSpliceBoltInput>(p, p.Code, "Steel.Connection.WeldedIBeamSplice.Bolts",
                "BOLT AND BEARING COMPONENTS ONLY: each real shear plane and each cover/parent ply in the welded-I double-cover splice. "
                + "No net/block, plate stability, slip, execution, fatigue or whole-joint qualification.", checks, trace, issues,
                new[] { "Same immutable geometry and one concomitant ULS case. Both sides and every physical bolt plane are assessed; no redistribution to hide a failed plane.",
                    "Cover centroid force split is an equilibrium model, not a certification of secondary plate bending or local load introduction.",
                    "Bearing uses the conservative linear interaction |Fx|/Fb,x+|Fy|/Fb,y<=1, with the smaller end/interior resistance for each direction. This is a declared design model, not an EC oblique-bearing equation.",
                    "Round holes, no countersinks, identical thread condition in the two planes. Full-footprint parallel non-structural packing stacks use EN1993-1-8 3.6.1(12)-(13), the greater TOTAL packing thickness on a face for BOTH shear planes; no packing resistance is credited. The group rectangle diagonal conservatively bounds the changing force-direction transfer length for the long-joint reduction; no uniform-transfer exemption.",
                    "Minimum physical edge, hole/pitch and hardware clearances are checked. Maximum pitch/exposure detailing and member seam resistance belong to the remaining plate/detail checks." });
            return new WeldedIBeamSpliceBoltResult(f, assessment);

            void Bearing(SpliceBoltPreparation.Bearing b, double fx, double fy)
            {
                issues.AddRange(b.Issues);
                if (!b.Valid) return;
                double ratio = Math.Abs(fx) / b.Rx + Math.Abs(fy) / b.Ry;
                if (double.IsNaN(ratio) || double.IsInfinity(ratio)) { issues.Add(b.NumericIssue); return; }
                checks.Add(new ConnectionCheck(b.CheckId, ratio, 1, prepared.BearingReference, "1"));
                trace.Add(b.X); trace.Add(b.Y); trace.Add(b.Beta);
            }
        }
    }
}
