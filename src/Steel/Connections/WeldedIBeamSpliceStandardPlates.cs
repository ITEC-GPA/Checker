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
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> CompactPlateDomain { get; }
        /// <summary>A failed hierarchy is an unsuccessful sufficient applicability test,
        /// not proof that the actual loaded block has failed.</summary>
        public bool IsQualified => NetSections.IsComplete && BlockHierarchy.Status == ConnectionStatus.Passed
            && CompactPlateDomain.Status == ConnectionStatus.Passed;
        internal WeldedIBeamSpliceStandardPlateResult(ConnectionVerification<WeldedIBeamSpliceDetailInput> net,
            ConnectionVerification<WeldedIBeamSpliceDetailInput> block, ConnectionVerification<WeldedIBeamSpliceDetailInput> compact)
        { NetSections = net; BlockHierarchy = block; CompactPlateDomain = compact; }
    }

    /// <summary>Sufficient conservative checks for the standard splice, added to the
    /// existing gross sections, strip buckling, bolts and detailing. No plastic redistribution.
    /// The net stress check deliberately retains holes under compression as well.</summary>
    public static class WeldedIBeamSpliceStandardPlates
    {
        public static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var applicability = WeldedIBeamSpliceStandardModel.Verify(p);
            var d = p.Plates.Bolts.Detail; var code = p.Plates.Policy.Code;
            var f = WeldedIBeamSpliceForces.Calculate(d); var plates = WeldedIBeamSplicePlates.Verify(p.Plates);
            var issues = new List<string>(applicability.Diagnostics.Concat(plates.Diagnostics));
            if (!WeldedIBeamSplicePlates.SameCode(code, p.Plates.Bolts.Code)) issues.Add("PLATE_CODE_MISMATCH");
            var net = new List<ConnectionCheck>(); var blocks = new List<ConnectionCheck>(); var compact = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>();
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
                    compact.Add(new ConnectionCheck(c.Cover + ".NoShearBucklingDomain", plate.Width / plate.Thickness,
                        60 * Math.Sqrt(235 / plate.Fy), "EN1993-1-1:2005 6.2.6(6), EN1993-1-5:2006 5.1; eta=1.2 sufficient domain", "1"));
                    double half = grid.FirstBoltDistance + grid.LongitudinalSpan + WeldedIBeamSplicePlates.End(d, g);
                    Hierarchy(c.Cover.ToString(), plate, bolt, groups.SelectMany(x => x.Distribution.Points).Distinct().ToArray(),
                        -half, half, g.TransverseMinimum, g.TransverseMaximum, 1);
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
                        side == SpliceSide.Left ? -d.ButtGap / 2 : far, -parent.Width / 2, parent.Width / 2, 2);
                }
            }
            return new WeldedIBeamSpliceStandardPlateResult(Result("NetSections", net), Result("BlockHierarchy", blocks), Result("CompactDomain", compact));

            ConnectionVerification<WeldedIBeamSpliceDetailInput> Result(string name, IEnumerable<ConnectionCheck> checks)
                => new ConnectionVerification<WeldedIBeamSpliceDetailInput>(p, code, "Steel.Connection.WeldedIBeamSplice.Standard." + name,
                    "Conservative standard-detail sufficient checks. Block/compact-domain failure means outside this method, not an actual load-capacity failure.",
                    checks, trace, issues.Distinct(), new[] {
                        "Net rectangular holes remove both area and second moment. Every cover cut conservatively uses the perforated section, including the unperforated centre. Stress maxima are combined conservatively with 1.5 V/Anet; limit=min(fy/gammaM0,0.9fu/gammaM2). This elastic sufficient criterion is not a new plastic rupture interaction formula.",
                        "Block hierarchy enumerates orthogonal rectangular U and edge L paths, all free-edge prefixes and contiguous transverse subsets, in both directions. Eccentric expression 0.5 Ant fu/gammaM2 + Anv fy/(sqrt(3) gammaM0) is always used. Demand is the SUM of UNREDUCED design shear resistances of the enclosed bolt planes, not the load resultant. No cancellation or plastic bolt redistribution is credited.",
                        "Hierarchy requires every path to be stronger than all its fasteners. It is a sufficient exclusion of block tearing for this regular detail; a failed hierarchy requires a separate loaded-block method and prevents whole-detail qualification.",
                        "Parent net-section check assumes all flange/web holes aligned longitudinally and the full member actions retained in the parent, with no cover credit. Maximum lap-end moment is included. Unnotched parent-web block tearing is inapplicable per SCI P398 p59.",
                        "The compact-domain screen excludes shear-buckling-sensitive covers. Existing maximum longitudinal compression, 0.6p buckling strips, outstand/internal class3 limits and detailing remain mandatory." });
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
                double xmin, double xmax, double ymin, double ymax, int planes)
            {
                var capacity = BoltStrength.Verify(new BoltStrengthInput(code, bolt, 0, 0, planes, 0, 0, p.Plates.Bolts.HoleClearanceServiceabilitySource));
                if (!capacity.IsComplete) { issues.AddRange(capacity.Diagnostics); return; }
                double rb = capacity.Checks.Single(c => c.Id == "Bolt.Shear").Resistance;
                for (int axis = 0; axis < 2; axis++) foreach (int sign in new[] { -1, 1 })
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
                            double resistance = (.5 * lt * plate.Fu / code.Plates.GammaM2 + ls * plate.Fy / (Math.Sqrt(3) * code.Plates.GammaM0)) * plate.Thickness;
                            if (lt < 0 || ls <= 0 || !WeldedIBeamSplicePlates.Finite(resistance)) { issues.Add(name + ".INVALID_BLOCK_PATH"); return; }
                            blocks.Add(new ConnectionCheck(name + ".Axis" + axis + ".Direction" + sign + ".Path" + ++id + "." + mode,
                                k * count * rb, resistance, "EN1993-1-8:2005 3.10.2(3) eq3.10; sufficient fastener/block hierarchy, NOT actual action demand"));
                        }
                    }
                }
            }
        }
    }
}
