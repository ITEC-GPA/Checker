using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public sealed class WeldedIBeamSpliceStandardPlateResult
    {
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> NetSections { get; }
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> BlockHierarchy { get; }
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> DirectBlockTearing { get; }
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> CompactPlateDomain { get; }
        /// <summary>A failed hierarchy is an unsuccessful sufficient applicability test,
        /// not proof that the actual loaded block has failed.</summary>
        public bool IsQualified => NetSections.IsComplete && (BlockHierarchy.Checks.Count == 0 ? BlockHierarchy.Diagnostics.Count == 0 : BlockHierarchy.Status == ConnectionStatus.Passed)
            && (DirectBlockTearing.Checks.Count == 0 ? DirectBlockTearing.Diagnostics.Count == 0 : DirectBlockTearing.Status == ConnectionStatus.Passed)
            && CompactPlateDomain.Status == ConnectionStatus.Passed;
        internal WeldedIBeamSpliceStandardPlateResult(ConnectionVerification<WeldedIBeamSpliceDetailInput> net,
            ConnectionVerification<WeldedIBeamSpliceDetailInput> block, ConnectionVerification<WeldedIBeamSpliceDetailInput> compact, ConnectionVerification<WeldedIBeamSpliceDetailInput> direct)
        { NetSections = net; BlockHierarchy = block; CompactPlateDomain = compact; DirectBlockTearing = direct; }
    }

    /// <summary>Sufficient conservative checks for the standard splice, added to the
    /// existing gross sections, strip buckling, bolts and detailing. No plastic redistribution.
    /// The net stress check deliberately retains holes under compression as well.</summary>
    public static class WeldedIBeamSpliceStandardPlates
    {
        public static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p) => Verify(p, false);

        /// <summary>Opt-in direct action checks for collinear longitudinal bolt forces.
        /// Mixed-direction groups retain the sufficient fastener hierarchy.</summary>
        public static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p, bool actualCollinearBlockForces)
            => Verify(p, actualCollinearBlockForces, SpliceFlangeFaceModel.BalancedEqualFaces);
        public static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p, bool actualCollinearBlockForces, SpliceFlangeFaceModel faceModel)
            => Verify(p, actualCollinearBlockForces, faceModel, SplicePlateStabilityMethod.CompactCovers);
        public static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p, bool actualCollinearBlockForces,
            SpliceFlangeFaceModel faceModel, SplicePlateStabilityMethod plateStability)
        {
            if (!Enum.IsDefined(typeof(SplicePlateStabilityMethod), plateStability)) throw new ArgumentOutOfRangeException(nameof(plateStability));
            if (p == null) throw new ArgumentNullException(nameof(p));
            var plates = WeldedIBeamSplicePlates.Verify(p.Plates);
            return Verify(p, actualCollinearBlockForces, plateStability, plates,
                WeldedIBeamSpliceStandardModel.Verify(p, faceModel, plates.PreparedBolts.Forces));
        }
        internal static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p, bool actualCollinearBlockForces,
            SplicePlateStabilityMethod plateStability, WeldedIBeamSplicePlateResult plates, ConnectionVerification<WeldedIBeamSpliceDetailInput> applicability)
        {
            if (!Enum.IsDefined(typeof(SplicePlateStabilityMethod), plateStability)) throw new ArgumentOutOfRangeException(nameof(plateStability));
            if (!ReferenceEquals(p.Plates, plates.Input) || !ReferenceEquals(p, applicability.Input)) throw new ArgumentException("Prepared results belong to another input.");
            var d = p.Plates.Bolts.Detail; var code = p.Plates.Policy.Code;
            var f = plates.PreparedBolts.Forces;
            var issues = new List<string>(applicability.Diagnostics.Concat(plates.Diagnostics));
            if (!WeldedIBeamSplicePlates.SameCode(code, p.Plates.Bolts.Code)) issues.Add("PLATE_CODE_MISMATCH");
            var net = new List<ConnectionCheck>(); var blocks = new List<ConnectionCheck>(); var compact = new List<ConnectionCheck>();
            var direct = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>(); var reported = new List<SectionResistanceValue>();
            if (issues.Count == 0)
            {
                foreach (var c in plates.Covers)
                {
                    var groups = f.CoverGroups.Where(item => item.Cover == c.Cover).ToArray();
                    var g = groups.Single(x => x.Side == SpliceSide.Left); var plate = g.Plate;
                    var bolt = g.IsWeb ? p.Plates.Bolts.WebBolt : p.Plates.Bolts.FlangeBolt;
                    var grid = WeldedIBeamSplicePlates.Grid(d, g);
                    double centre = (g.TransverseMinimum + g.TransverseMaximum) / 2;
                    var holes = g.Distribution.Points.Select(x => x.Y - centre).Distinct().ToArray();
                    double a = (plate.Width - holes.Length * bolt.HoleDiameter) * plate.Thickness;
                    double i = plate.Thickness * Math.Pow(plate.Width, 3) / 12
                        - holes.Sum(y => plate.Thickness * (Math.Pow(bolt.HoleDiameter, 3) / 12 + bolt.HoleDiameter * y * y));
                    double w = i / (plate.Width / 2);
                    if (!(a > 0) || !(w > 0)) { issues.Add(c.Cover + ".INVALID_NET_SECTION"); continue; }
                    Value(c.Cover + ".Anet", a, "mm2"); Value(c.Cover + ".Inet", i, "mm4"); Value(c.Cover + ".Wnet", w, "mm3");
                    double limit = Math.Min(plate.Fy / code.Plates.GammaM0, .9 * plate.Fu / code.Plates.GammaM2);
                    int k = 0;
                    foreach (var cut in c.Cuts)
                    {
                        double sigma = Math.Abs(cut.N) / a + Math.Abs(cut.M) / w;
                        double tau = 1.5 * Math.Abs(cut.V) / a;
                        net.Add(new ConnectionCheck(c.Cover + ".Cut" + k++ + ".NetElasticNVM",
                            ElasticBoltGroup.Hypot(sigma, Math.Sqrt(3) * tau), limit,
                            "EN1993-1-1:2005 6.2.1(5),6.2.3; conservative elastic NET stress screen, also sufficient for category C net yielding", "MPa"));
                    }
                    // Sufficient no-shear-buckling domain, eta=1.2 conservatively for all admitted grades.
                    // Longitudinal compression and transverse outstands are checked independently by Plates.
                    bool zeroShear = c.Cuts.All(cut => cut.V == 0);
                    // Exact absence of shear is an independent sufficient condition;
                    // no force tolerance and no waiver of compression/net-section checks.
                    Value(c.Cover + ".ZeroShearAction", zeroShear ? 1 : 0, "1");
                    if (plateStability == SplicePlateStabilityMethod.StandardBoltedCovers)
                    {
                        // Published conventional bolted-cover procedure, not a free web
                        // panel with fictitious continuous supports through the bolt rows.
                        // Extra conservative class3 outstand bounds also apply at zero N.
                        string prefix = c.Cover + ".StandardBoltedCover.";
                        double eps = Math.Sqrt(235 / plate.Fy), t = plate.Thickness;
                        Domain("MaximumLongitudinalGap", grid.MaximumLongitudinalGap, Math.Min(14 * t, 200), "mm");
                        if (holes.Length > 1)
                        {
                            Domain("TransversePitch", grid.TransversePitch, Math.Min(14 * t, 200), "mm");
                            Domain("InternalStripClass3", grid.TransversePitch / t, 42 * eps);
                        }
                        Domain("LowerOutstandClass3", (g.Distribution.Points.Min(q => q.Y) - g.TransverseMinimum) / t, 14 * eps);
                        Domain("UpperOutstandClass3", (g.TransverseMaximum - g.Distribution.Points.Max(q => q.Y)) / t, 14 * eps);
                        Domain("EndOutstandClass3", WeldedIBeamSplicePlates.End(d, g) / t, 14 * eps);
                        Value(c.Cover + ".UsesStandardBoltedCoverMethod", 1, "1");
                        Value(c.Cover + ".WholeWidthThicknessRatio", plate.Width / t, "1");
                        void Domain(string id, double demand, double resistance, string unit = "1") => compact.Add(new ConnectionCheck(prefix + id,
                            demand, resistance, "SCI P398 4.3 pp57-59 conventional bolted cover procedure; EC Table3.3 fastener spacing; supplementary conservative class3 bounds on every outstand", unit));
                    }
                    else compact.Add(new ConnectionCheck(c.Cover + ".NoShearBucklingDomain", zeroShear ? 0 : plate.Width / plate.Thickness,
                        60 * Math.Sqrt(235 / plate.Fy), "EN1993-1-1:2005 6.2.6(6), EN1993-1-5:2006 5.1; eta=1.2 sufficient domain, or exactly zero shear at every cut", "1"));
                    double half = grid.FirstBoltDistance + grid.LongitudinalSpan + WeldedIBeamSplicePlates.End(d, g);
                    Hierarchy(c.Cover.ToString(), plate, bolt, groups.SelectMany(x => x.Distribution.Points).Distinct().ToArray(),
                        -half, half, g.TransverseMinimum, g.TransverseMaximum, 1, groups.SelectMany(x => x.Distribution.Forces));
                }
                ParentNet();
                // The unnotched beam WEB is not a block-tearing case (SCI P398 p59).
                // Flange blocks are bounded using both shear planes and the actual free butt edge.
                foreach (bool top in new[] { true, false }) foreach (SpliceSide side in new[] { SpliceSide.Left, SpliceSide.Right })
                {
                    var flange = top ? d.Top : d.Bottom; var parent = top ? d.Section.TopFlange : d.Section.BottomFlange;
                    var points = flange.Bolts.Points(side).ToArray();
                    double far = flange.Bolts.FirstBoltDistance + flange.Bolts.LongitudinalSpan + flange.EndDistance;
                    // Artificial far free edge is conservative; the real parent continues.
                    Hierarchy((top ? "Top" : "Bottom") + ".Parent." + side, parent, p.Plates.Bolts.FlangeBolt,
                        points, side == SpliceSide.Left ? -far : d.ButtGap / 2,
                        side == SpliceSide.Left ? -d.ButtGap / 2 : far, -parent.Width / 2, parent.Width / 2, 2,
                        f.CoverGroups.Where(g => g.Side == side && (top
                            ? g.Cover == WeldedSpliceCover.TopOuter || g.Cover == WeldedSpliceCover.TopInnerNegative || g.Cover == WeldedSpliceCover.TopInnerPositive
                            : g.Cover == WeldedSpliceCover.BottomOuter || g.Cover == WeldedSpliceCover.BottomInnerNegative || g.Cover == WeldedSpliceCover.BottomInnerPositive))
                        .SelectMany(g => g.Distribution.Forces));
                }
            }
            return new WeldedIBeamSpliceStandardPlateResult(Result("NetSections", net), Result("BlockHierarchy", blocks), Result("CompactDomain", compact), Result("DirectBlockTearing", direct));

            ConnectionVerification<WeldedIBeamSpliceDetailInput> Result(string name, IEnumerable<ConnectionCheck> checks)
                => new ConnectionVerification<WeldedIBeamSpliceDetailInput>(p, code, "Steel.Connection.WeldedIBeamSplice.Standard." + name,
                    "DirectBlockTearing uses collinear actual bolt forces and eccentric resistance, without cancellation. Other block groups retain the hierarchy. Conservative standard-detail sufficient checks. Block/compact-domain failure means outside this method, not an actual load-capacity failure.",
                    checks, trace, issues.Distinct(), new[] {
                        "Net rectangular holes remove both area and second moment. Every cover cut conservatively uses the perforated section, including the unperforated centre. Stress maxima are combined conservatively with 1.5 V/Anet; limit=min(fy/gammaM0,0.9fu/gammaM2). This elastic sufficient criterion is not a new plastic rupture interaction formula.",
                        "Block hierarchy enumerates orthogonal rectangular U and edge L paths, all free-edge prefixes and contiguous transverse subsets, in both directions. Eccentric expression 0.5 Ant fu/gammaM2 + Anv fy/(sqrt(3) gammaM0) is always used. Demand is the SUM of UNREDUCED design shear resistances of the enclosed bolt planes, not the load resultant. No cancellation or plastic bolt redistribution is credited.",
                        "Opt-in direct checks are limited to longitudinal collinear bolt actions (including sign reversal). All rectangular prefixes and transverse subsets are checked using sum(abs(Fx)); opposite loads on a continuous cover do not cancel. The eccentric coefficient 0.5 is retained for every subset. No mixed-direction N-V-M rupture interaction is inferred. Non-collinear groups use the unchanged hierarchy.",
                        "Hierarchy requires every path to be stronger than all its fasteners. It is a sufficient exclusion of block tearing for this regular detail; a failed hierarchy requires a separate loaded-block method and prevents whole-detail qualification.",
                        "Parent net-section check assumes all flange/web holes aligned longitudinally and the full member actions retained in the parent, with no cover credit. Maximum lap-end moment is included. Unnotched parent-web block tearing is inapplicable per SCI P398 p59.",
                        "CompactCovers retains the whole-width no-shear-buckling screen. StandardBoltedCovers explicitly uses SCI P398 pp57-59 for continuous flat rectangular covers with regular closely spaced filled normal fasteners, class1-3 strips/outstands in both directions and every existing compression/shear/bending/net/block check. It is not an EN1993-1-5 free-panel buckling or tension-field method; locally class4 strips remain outside StandardBoltedCovers. EffectiveCompressionWithLowShear is assessed separately and retains the whole-width screen. No continuous line supports are inferred from discrete bolts." }, reported);
            void Value(string name, double value, string unit)
            { if (WeldedIBeamSplicePlates.Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit)); else issues.Add(name + ".NUMERIC_RANGE"); }
            void ParentNet()
            {
                var s = d.Section; double df = p.Plates.Bolts.FlangeBolt.HoleDiameter, dw = p.Plates.Bolts.WebBolt.HoleDiameter;
                double at = (s.TopFlange.Width - d.Top.Bolts.TransverseRows * df) * s.TopFlange.Thickness;
                double ab = (s.BottomFlange.Width - d.Bottom.Bolts.TransverseRows * df) * s.BottomFlange.Thickness;
                double aw = (s.Web.Width - d.WebBolts.TransverseRows * dw) * s.Web.Thickness;
                double area = at + ab + aw, yc = (at * s.TopY + aw * s.WebY + ab * s.BottomY) / area;
                double iw = s.Web.Thickness * Math.Pow(s.Web.Width, 3) / 12
                    - d.WebBolts.Points(SpliceSide.Left).Select(x => x.Y).Distinct().Sum(y => s.Web.Thickness * (Math.Pow(dw, 3) / 12 + dw * y * y));
                double inertia = at * (s.TopFlange.Thickness * s.TopFlange.Thickness / 12 + Math.Pow(s.TopY - yc, 2))
                    + ab * (s.BottomFlange.Thickness * s.BottomFlange.Thickness / 12 + Math.Pow(s.BottomY - yc, 2))
                    + iw + aw * Math.Pow(s.WebY - yc, 2);
                if (!(at > 0 && ab > 0 && aw > 0 && inertia > 0)) { issues.Add("INVALID_PARENT_NET_SECTION"); return; }
                double half = Math.Max(d.WebBolts.FirstBoltDistance + d.WebBolts.LongitudinalSpan + d.WebCoverEnd,
                    Math.Max(d.Top.Bolts.FirstBoltDistance + d.Top.Bolts.LongitudinalSpan + d.Top.EndDistance,
                    d.Bottom.Bolts.FirstBoltDistance + d.Bottom.Bolts.LongitudinalSpan + d.Bottom.EndDistance));
                double n = f.ActionsAtJoint.Fx, v = f.ActionsAtJoint.Fy;
                double m = Math.Abs(f.ActionsAtJoint.Mz + yc * n) + half * Math.Abs(v);
                double sigma = Math.Abs(n) / area + m * Math.Max(s.Height - s.CentroidY - yc, s.CentroidY + yc) / inertia;
                double tau = 1.5 * Math.Abs(v) / aw;
                double limit = new[] { s.TopFlange, s.Web, s.BottomFlange }.Min(q => Math.Min(q.Fy / code.Plates.GammaM0, .9 * q.Fu / code.Plates.GammaM2));
                net.Add(new ConnectionCheck("Parent.NetElasticNVM", ElasticBoltGroup.Hypot(sigma, Math.Sqrt(3) * tau), limit,
                    "EN1993-1-1:2005 6.2.1,6.2.3; full-load, all-holes-aligned elastic sufficient check, no splice-cover credit", "MPa"));
                Value("Parent.Anet", area, "mm2"); Value("Parent.NetCentroidOffsetY", yc, "mm"); Value("Parent.Inet", inertia, "mm4");
                Value("Parent.BoundingMomentAboutNetCentroid", m, "Nmm");
            }
            void Hierarchy(string name, ConnectionPlate plate, ConnectionBolt bolt, ConnectionPoint2D[] points,
                double xmin, double xmax, double ymin, double ymax, int planes, IEnumerable<ElasticBoltForce> applied)
            {
                // For a parent block, opposite face forces must not cancel: retain the sum
                // of absolute actions on all physical planes (also bounds a local flange couple).
                var forces = applied.GroupBy(x => x.Position).ToDictionary(g => g.Key,
                    g => Tuple.Create(g.Sum(x => Math.Abs(x.Fx)), g.Sum(x => Math.Abs(x.Fy))));
                double scale = forces.Values.Sum(x => Math.Abs(x.Item1) + Math.Abs(x.Item2));
                bool collinear = actualCollinearBlockForces && forces.Values.All(x => Math.Abs(x.Item2) <= 32 * 2.2204460492503131e-16 * scale);
                var method = new SectionResistanceValue(name + ".ActualCollinearBlockMethod", collinear ? 1 : 0, "1");
                trace.Add(method); reported.Add(method);
                var capacity = BoltStrength.Verify(new BoltStrengthInput(code, bolt, 0, 0, planes, 0, 0, p.Plates.Bolts.HoleClearanceServiceabilitySource), out var capacityValues);
                if (!capacity.IsComplete) { issues.AddRange(capacity.Diagnostics); return; }
                double rb = capacityValues.ShearResistance;
                for (int axis = 0; axis < (collinear ? 1 : 2); axis++) foreach (int sign in new[] { -1, 1 })
                {
                    double[] us = points.Select(q => sign * (axis == 0 ? q.X : q.Y)).Distinct().OrderBy(x => x).ToArray();
                    double[] vs = points.Select(q => axis == 0 ? q.Y : q.X).Distinct().OrderBy(x => x).ToArray();
                    double start = sign == 1 ? (axis == 0 ? xmin : ymin) : -(axis == 0 ? xmax : ymax);
                    double vmin = axis == 0 ? ymin : xmin, vmax = axis == 0 ? ymax : xmax;
                    int id = 0;
                    for (int k = 1; k <= us.Length; k++) for (int first = 0; first < vs.Length; first++) for (int last = first; last < vs.Length; last++)
                    {
                        int count = last - first + 1;
                        double lv = us[k - 1] - start - (k - .5) * bolt.HoleDiameter;
                        Path(vs[last] - vs[first] - (count - 1) * bolt.HoleDiameter, 2 * lv, "U");
                        if (first == 0) Path(vs[last] - vmin - (count - .5) * bolt.HoleDiameter, lv, "Lmin");
                        if (last == vs.Length - 1) Path(vmax - vs[first] - (count - .5) * bolt.HoleDiameter, lv, "Lmax");
                        void Path(double lt, double ls, string mode)
                        {
                            double resistance = (.5 * lt * plate.Fu / code.Plates.GammaM2 + PlasticShear.Resistance(ls, plate.Fy, code.Plates.GammaM0)) * plate.Thickness;
                            if (lt < 0 || ls <= 0 || !WeldedIBeamSplicePlates.Finite(resistance)) { issues.Add(name + ".INVALID_BLOCK_PATH"); return; }
                            double demand = collinear ? points.Where(q => sign * q.X <= us[k - 1] && q.Y >= vs[first] && q.Y <= vs[last])
                                .Sum(q => Math.Abs(forces[q].Item1)) : k * count * rb;
                            (collinear ? direct : blocks).Add(new ConnectionCheck(name + ".Axis" + axis + ".Direction" + sign + ".Path" + ++id + "." + mode,
                                demand, resistance, collinear
                                    ? "EN1993-1-8:2005 3.10.2(3) eq3.10; collinear actual force sum, eccentric factor0.5; SCI P398 pp136,139 path geometry"
                                    : "EN1993-1-8:2005 3.10.2(3) eq3.10; sufficient fastener/block hierarchy, NOT actual action demand"));
                        }
                    }
                }
            }
        }
    }
}
