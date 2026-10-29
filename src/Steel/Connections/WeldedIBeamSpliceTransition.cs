using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Different welded I on the RIGHT member of a bolted double-cover splice (the force input's
    /// Section is the left one). Qualified domain: same overall depth, common outer flange planes and web
    /// mid-plane, equal flange widths; flange/web thickness changes are filled by parallel full-footprint
    /// packings per side so that every continuous flat cover has ONE contact plane on both members.
    /// Offsets are declared, never inferred; a nonzero offset is refused. Units mm.</summary>
    public sealed class WeldedSpliceSectionTransition
    {
        public WeldedIBeamSpliceSection Right { get; }
        /// <summary>Packing between the covers and the RIGHT member; the left one is the force input's Packing.</summary>
        public WeldedSplicePacking RightPacking { get; }
        /// <summary>Height of the right bottom outer flange face above the left one.</summary>
        public double OuterPlaneOffset { get; }
        /// <summary>Transverse (Z) offset of the right web mid-plane from the left one.</summary>
        public double WebPlaneOffset { get; }
        public WeldedSpliceSectionTransition(WeldedIBeamSpliceSection right, WeldedSplicePacking rightPacking,
            double outerPlaneOffset = 0, double webPlaneOffset = 0)
        {
            Right = right ?? throw new ArgumentNullException(nameof(right));
            ConnectionNumbers.Finite(outerPlaneOffset, nameof(outerPlaneOffset));
            ConnectionNumbers.Finite(webPlaneOffset, nameof(webPlaneOffset));
            RightPacking = rightPacking ?? WeldedSplicePacking.None;
            OuterPlaneOffset = outerPlaneOffset; WebPlaneOffset = webPlaneOffset;
        }

        internal bool SamePhysical(WeldedSpliceSectionTransition other) => other != null && (ReferenceEquals(this, other)
            || SpliceIdentity.Plate(Right.TopFlange, other.Right.TopFlange) && SpliceIdentity.Plate(Right.Web, other.Right.Web)
            && SpliceIdentity.Plate(Right.BottomFlange, other.Right.BottomFlange) && Right.TopWeldToe == other.Right.TopWeldToe
            && Right.BottomWeldToe == other.Right.BottomWeldToe && RightPacking.SamePhysical(other.RightPacking)
            && OuterPlaneOffset == other.OuterPlaneOffset && WebPlaneOffset == other.WebPlaneOffset);

        /// <summary>Geometric admissibility of the transition; every code names the refused feature.</summary>
        internal static IEnumerable<string> GeometryIssues(WeldedIBeamSpliceForceInput p)
        {
            var t = p.Transition; var l = p.Section; var r = t.Right; var a = p.Packing; var b = t.RightPacking;
            var issues = new List<string>();
            if (t.OuterPlaneOffset != 0) issues.Add("TRANSITION_OUTER_FLANGE_PLANE_OFFSET_NOT_QUALIFIED");
            if (t.WebPlaneOffset != 0) issues.Add("TRANSITION_OUT_OF_PLANE_WEB_OFFSET_NOT_QUALIFIED");
            if (!Same(l.Height, r.Height)) issues.Add("TRANSITION_OVERALL_DEPTH_CHANGE_NOT_QUALIFIED");
            // A width change spreads the flange force in-plane (shear lag in the outstands): another mechanism.
            if (l.TopFlange.Width != r.TopFlange.Width || l.BottomFlange.Width != r.BottomFlange.Width)
                issues.Add("TRANSITION_FLANGE_WIDTH_CHANGE_NOT_QUALIFIED");
            // One contact plane per continuous flat cover: the packings must fill the thickness change exactly.
            // No fit-up gap is credited and no plate bending of the cover is assumed to close it.
            Plane(a.TopOuter, b.TopOuter, "TOP_OUTER");
            Plane(l.TopFlange.Thickness + a.TopInner, r.TopFlange.Thickness + b.TopInner, "TOP_INNER");
            Plane(a.BottomOuter, b.BottomOuter, "BOTTOM_OUTER");
            Plane(l.BottomFlange.Thickness + a.BottomInner, r.BottomFlange.Thickness + b.BottomInner, "BOTTOM_INNER");
            Plane(l.Web.Thickness / 2 + a.EachWeb, r.Web.Thickness / 2 + b.EachWeb, "WEB");
            // The web cover is centred on the left web mid-height and must clear the right seams as well.
            double centre = l.BottomFlange.Thickness + l.Web.Width / 2, half = p.EachWebCover.Width / 2;
            if (centre + half > r.BottomFlange.Thickness + r.Web.Width - r.TopWeldToe || centre - half < r.BottomFlange.Thickness + r.BottomWeldToe)
                issues.Add("Right.WEB_COVER_INTERSECTS_LONGITUDINAL_SEAMS");
            return issues;
            void Plane(double left, double right, string face)
            { if (!Same(left, right)) issues.Add("TRANSITION_" + face + "_COVER_PLANE_MISMATCH"); }
        }
        /// <summary>Equality of two stack dimensions up to floating-point summation, never a fit-up tolerance.</summary>
        private static bool Same(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
    }

    /// <summary>Section-transition assessment of ONE cover distribution. Transfer holds the resistance checks
    /// of the internal flange/web redistribution in the other member; Domain the applicability of the
    /// two-distribution envelope. Both distributions of every action state are required by the workflow.</summary>
    public sealed class WeldedIBeamSpliceTransitionResult
    {
        /// <summary>Member whose elastic distribution the continuous covers carry.</summary>
        public SpliceSide CoverDistribution { get; }
        /// <summary>Member that redistributes the difference between its own elastic share and the cover forces.</summary>
        public SpliceSide RedistributingMember => CoverDistribution == SpliceSide.Left ? SpliceSide.Right : SpliceSide.Left;
        public ConnectionVerification<WeldedIBeamSpliceParentInput> Transfer { get; }
        public ConnectionVerification<WeldedIBeamSpliceParentInput> Domain { get; }
        internal WeldedIBeamSpliceTransitionResult(SpliceSide distribution, ConnectionVerification<WeldedIBeamSpliceParentInput> transfer,
            ConnectionVerification<WeldedIBeamSpliceParentInput> domain)
        { CoverDistribution = distribution; Transfer = transfer; Domain = domain; }
    }

    /// <summary>Equilibrium of the transition detail and its sufficient checks. The covers carry the elastic
    /// share of the distributing member; each continuous cover is a free body loaded only by its own bolts.
    /// In the other member S the top flange, web and bottom flange of the lap zone are free bodies: cover
    /// forces at the joint end, own elastic share at the lap end, junction forces in between. The longitudinal
    /// junction forces T and the vertical junction couples dM close every sub-body; the web closes them all.</summary>
    internal static class WeldedIBeamSpliceTransfer
    {
        private const double Tolerance = 1e-10;
        internal static WeldedIBeamSpliceTransitionResult Verify(WeldedIBeamSpliceParentInput p, WeldedIBeamSpliceForceResult f,
            WeldedIBeamSplicePlateResult plates, SplicePlateStabilityMethod stability)
        {
            var d = p.Detail.Plates.Bolts.Detail;
            if (d.Transition == null) throw new ArgumentException("A section transition is required.", nameof(p));
            if (!ReferenceEquals(d, f.Input) || !ReferenceEquals(p.Detail.Plates, plates.Input)) throw new ArgumentException("Prepared results belong to another input.");
            var weldCode = p.Detail.Plates.Bolts.Code; var plateCode = p.Detail.Plates.Policy.Code; var factors = plateCode.Plates;
            var issues = new List<string>(f.Diagnostics); var checks = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>(); var reported = new List<SectionResistanceValue>();
            var domainIssues = new List<string>(f.Diagnostics.Concat(plates.Diagnostics)); var domainChecks = new List<ConnectionCheck>();
            var domainTrace = new List<SectionResistanceValue>();
            if (stability == SplicePlateStabilityMethod.EffectiveCompression || stability == SplicePlateStabilityMethod.EffectiveCompressionWithLowShear)
                domainIssues.Add("TRANSITION_EFFECTIVE_COMPRESSION_NOT_QUALIFIED");
            var reference = d.CoverDistribution;
            var member = reference == SpliceSide.Left ? SpliceSide.Right : SpliceSide.Left;
            Value("CoverDistribution", reference == SpliceSide.Left ? 0 : 1, "1", true);
            if (f.IsBalanced)
            {
                CoverEquilibrium();
                Redistribution();
            }
            if (plates.Diagnostics.Count == 0 && f.IsBalanced)
            {
                // Every cover N, V, M is affine in the distribution mixture; with |V|<=Vpl/2 at every cut of
                // BOTH distributions rho=0 everywhere and every cover check is convex: the two variants bound
                // all intermediate joint distributions.
                var layout = plates.Input.Prepared.Layout;
                for (int ci = 0; ci < plates.Covers.Count; ci++)
                {
                    var c = plates.Covers[ci]; var pc = layout.Covers[ci];
                    double v = c.Cuts.Count == 0 ? 0 : c.Cuts.Max(cut => Math.Abs(cut.V));
                    domainChecks.Add(new ConnectionCheck(c.Cover + ".CoverShearInConvexEnvelopeDomain", v, .5 * Math.Min(pc.Vg, pc.Vn),
                        "Library envelope rule: SCI P398 p56 rho=0 for V<=0.5Vpl, EN1993-1-1:2005 6.2.8(2); convex checks bound the mixture of the two distributions", "N",
                        ConnectionCheckRole.Applicability));
                }
            }
            var transfer = new ConnectionVerification<WeldedIBeamSpliceParentInput>(p, weldCode, "Steel.Connection.WeldedIBeamSplice.SectionTransfer",
                "Internal flange/web redistribution of the " + member + " member when the continuous covers carry the " + reference
                + " elastic distribution: junction fillets, web junction and perforated parent plates in the lap zone. Sufficient elastic checks; not a contact, shear-lag or fatigue solution.",
                checks, trace, issues.Distinct(), new[] {
                    "Each continuous cover carries ONE force field: the elastic share of the CoverDistribution section, applied to both members at the common cover planes. Two independent elastic distributions are never balanced only in total; the free-edge residual of every cover is reported.",
                    "In the redistributing member the difference between its own elastic share and the delivered cover forces is a self-equilibrated set: T_top, T_bottom pass from web to flanges through the longitudinal junctions, the vertical junction couples dM close the flange moment about their own centroids, and the web free body closes the axial and moment residuals.",
                    "T is introduced uniformly over the flange cover lap of that member, from its butt face to the cover end; dM by two opposite uniform vertical line loads over the two halves of the lap (q=4dM/L^2). Fillets use EN1993-1-8:2005 4.5.3 directional method with the long-joint factor 4.11 betaLw,1 on the transfer length; the beam shear flow VQ/I is added conservatively.",
                    "Parent plates are checked at their perforated sections with the sum of absolute delivered and transferred actions plus the elastic share of V times the lap length: a bound for any monotone delivery and transfer along the lap. The web junction uses EN1993-1-1 6.2.1(5) with |sx sz| conservatively added.",
                    "The workflow requires both cover distributions (left and right section) for every action state; intermediate joint distributions are bounded by the two variants because every retained check is convex in the cover forces within the Domain conditions. Cover and member stiffness are not modelled." }, reported);
            var domain = new ConnectionVerification<WeldedIBeamSpliceParentInput>(p, plateCode, "Steel.Connection.WeldedIBeamSplice.TransitionDomain",
                "Applicability of the two-distribution envelope across a section transition: low cover shear at every cut and a convex plate-stability method.",
                domainChecks, domainTrace, domainIssues.Distinct(), new[] {
                    "EffectiveCompression methods depend non-linearly on the stress state and are not combined with the transition envelope.",
                    "Same depth, common outer flange planes and web mid-plane, equal flange widths and exact packing of every cover plane are force-model prerequisites (TRANSITION_* codes)." },
                role: ConnectionCheckRole.Applicability);
            return new WeldedIBeamSpliceTransitionResult(reference, transfer, domain);

            void CoverEquilibrium()
            {
                foreach (WeldedSpliceCover cover in Enum.GetValues(typeof(WeldedSpliceCover)))
                {
                    double fx = 0, fy = 0, mz = 0, sx = 0, sy = 0, sm = 0;
                    foreach (var g in f.CoverGroups) if (g.Cover == cover)
                    {
                        double centre = (g.TransverseMinimum + g.TransverseMaximum) / 2;
                        foreach (var b in g.Distribution.Forces)
                        {
                            fx += b.Fx; fy += b.Fy; mz += b.Position.X * b.Fy - (b.Position.Y - centre) * b.Fx;
                            sx += Math.Abs(b.Fx); sy += Math.Abs(b.Fy); sm += Math.Abs(b.Position.X * b.Fy) + Math.Abs((b.Position.Y - centre) * b.Fx);
                        }
                    }
                    string name = cover + ".ContinuousCoverResidual.";
                    Value(name + "Fx", fx, "N"); Value(name + "Fy", fy, "N"); Value(name + "Mz", mz, "Nmm");
                    if (!(Math.Abs(fx) <= Tolerance * sx) || !(Math.Abs(fy) <= Tolerance * sy) || !(Math.Abs(mz) <= Tolerance * sm))
                        issues.Add(cover + ".CONTINUOUS_COVER_EQUILIBRIUM");
                }
            }

            void Redistribution()
            {
                var s = d.SectionOf(member); var pack = d.PackingOf(member); var j = d.SectionOf(reference);
                var w = p.Welds; double v = d.Actions.Fy;
                var top = f.Flange(SpliceSide.Left, true); var bottom = f.Flange(SpliceSide.Left, false);
                string m = member + ".";
                // Delivered cover forces about the member's own flange centroids (common cover planes).
                double yoT = (s.TopFlange.Thickness + d.Top.Outer.Thickness) / 2 + pack.TopOuter;
                double yiT = -((s.TopFlange.Thickness + d.Top.EachInner.Thickness) / 2 + pack.TopInner);
                double yoB = -((s.BottomFlange.Thickness + d.Bottom.Outer.Thickness) / 2 + pack.BottomOuter);
                double yiB = (s.BottomFlange.Thickness + d.Bottom.EachInner.Thickness) / 2 + pack.BottomInner;
                double deliveredTopM = -(yoT * top.OuterForce + yiT * top.BothInnerForce);
                double deliveredBottomM = -(yoB * bottom.OuterForce + yiB * bottom.BothInnerForce);
                // The distributing web force acts at its own centroid: real vertical eccentricity in this web.
                double e = j.BottomFlange.Thickness + j.Web.Width / 2 - (s.BottomFlange.Thickness + s.Web.Width / 2);
                double deliveredWebM = f.WebLocalMoment - e * f.WebAxialForce;
                double ownTopN = f.OwnShare(member, 0), ownTopM = f.OwnShare(member, 1), ownBottomN = f.OwnShare(member, 2),
                    ownBottomM = f.OwnShare(member, 3), ownWebN = f.OwnShare(member, 4), ownWebM = f.OwnShare(member, 5);
                // Junction forces from the web into each flange, and the vertical couples closing the flange moments.
                double tTop = ownTopN - top.ParentAxial, tBottom = ownBottomN - bottom.ParentAxial;
                double dmTop = ownTopM - deliveredTopM - tTop * s.TopFlange.Thickness / 2;
                double dmBottom = ownBottomM - deliveredBottomM + tBottom * s.BottomFlange.Thickness / 2;
                double h = s.Web.Width;
                double residualN = ownWebN - f.WebAxialForce + tTop + tBottom;
                double residualM = ownWebM - deliveredWebM - h / 2 * (tTop - tBottom) + dmTop + dmBottom;
                double scaleN = Math.Abs(ownWebN) + Math.Abs(f.WebAxialForce) + Math.Abs(ownTopN) + Math.Abs(top.ParentAxial) + Math.Abs(ownBottomN) + Math.Abs(bottom.ParentAxial);
                double scaleM = Math.Abs(ownTopM) + Math.Abs(deliveredTopM) + Math.Abs(ownBottomM) + Math.Abs(deliveredBottomM) + Math.Abs(ownWebM) + Math.Abs(deliveredWebM)
                    + s.Height * scaleN;
                Value(m + "Top.DeliveredLocalMoment", deliveredTopM, "Nmm", true); Value(m + "Bottom.DeliveredLocalMoment", deliveredBottomM, "Nmm", true);
                Value(m + "Web.DeliveredEccentricity", e, "mm", true); Value(m + "Web.DeliveredLocalMoment", deliveredWebM, "Nmm", true);
                Value(m + "Top.TransferFromWeb", tTop, "N", true); Value(m + "Bottom.TransferFromWeb", tBottom, "N", true);
                Value(m + "Web.AxialChange", ownWebN - f.WebAxialForce, "N", true);
                Value(m + "Top.JunctionCouple", dmTop, "Nmm", true); Value(m + "Bottom.JunctionCouple", dmBottom, "Nmm", true);
                Value(m + "Web.Residual.Fx", residualN, "N"); Value(m + "Web.Residual.Mz", residualM, "Nmm");
                if (!(Math.Abs(residualN) <= Tolerance * scaleN) || !(Math.Abs(residualM) <= Tolerance * scaleM))
                    issues.Add(m + "TRANSFER_FREE_BODY_EQUILIBRIUM");
                if (issues.Count != 0) return;

                double half = Math.Max(d.WebBolts.FirstBoltDistance + d.WebBolts.LongitudinalSpan + d.WebCoverEnd,
                    Math.Max(d.Top.Bolts.FirstBoltDistance + d.Top.Bolts.LongitudinalSpan + d.Top.EndDistance,
                    d.Bottom.Bolts.FirstBoltDistance + d.Bottom.Bolts.LongitudinalSpan + d.Bottom.EndDistance));
                double gradient = Math.Abs(v) * half / s.StrongAxisSecondMoment;
                // Web bound for any monotone delivery/transfer along the lap, plus its elastic share of V*half.
                double webN = Math.Abs(f.WebAxialForce) + Math.Abs(ownWebN - f.WebAxialForce) + gradient * s.Web.Width * s.Web.Thickness * Math.Abs(s.WebY);
                double webM = Math.Abs(deliveredWebM) + Math.Abs(ownWebM - deliveredWebM) + gradient * s.Web.Thickness * Math.Pow(s.Web.Width, 3) / 12;
                double webSigma = webN / (s.Web.Width * s.Web.Thickness) + webM / (s.Web.Thickness * s.Web.Width * s.Web.Width / 6);
                Value(m + "Web.GrossFibreStressBound", webSigma, "MPa");
                Junction(true, tTop, dmTop, webSigma); Junction(false, tBottom, dmBottom, webSigma);
                Flange(true, top.ParentAxial, deliveredTopM, tTop, dmTop);
                Flange(false, bottom.ParentAxial, deliveredBottomM, tBottom, dmBottom);
                Web(webN, webM);

                void Junction(bool isTop, double t, double dm, double sigmaX)
                {
                    var cover = isTop ? d.Top : d.Bottom; var plate = isTop ? s.TopFlange : s.BottomFlange;
                    string name = m + (isTop ? "Top" : "Bottom");
                    double throat = isTop ? w.TopThroat : w.BottomThroat; var grade = isTop ? w.TopGrade : w.BottomGrade;
                    double length = cover.Bolts.FirstBoltDistance + cover.Bolts.LongitudinalSpan + cover.EndDistance - d.ButtGap / 2;
                    double flow = Math.Abs(v * plate.Width * plate.Thickness * (isTop ? s.TopY : s.BottomY) / s.StrongAxisSecondMoment);
                    double longitudinal = flow + Math.Abs(t) / length, vertical = 4 * Math.Abs(dm) / (length * length);
                    double beta = length <= 150 * throat ? 1 : 1.2 - .2 * length / (150 * throat);
                    Value(name + ".TransferLength", length, "mm", true); Value(name + ".LongitudinalJunctionFlow", longitudinal, "N/mm", true);
                    Value(name + ".VerticalJunctionLoad", vertical, "N/mm", true); Value(name + ".BetaLw1", beta, "1", true);
                    if (!(beta > 0)) issues.Add(name + ".TRANSFER_LENGTH_BEYOND_LONG_WELD_RULE");
                    else
                    {
                        double parallel = longitudinal / (2 * throat), normal = vertical / (2 * Math.Sqrt(2) * throat);
                        var r = FilletWeldResistance.Evaluate(weldCode, new FilletWeldMaterial(grade, plate.Fu, plate.Thickness),
                            new FilletWeldMaterial(w.WebGrade, s.Web.Fu, s.Web.Thickness), normal, normal, parallel, FilletWeldMethod.Directional, beta);
                        foreach (var issue in r.Diagnostics) issues.Add(name + ".TransferFillets:" + issue);
                        foreach (var check in r.Checks) checks.Add(check.WithId(name + ".TransferFillets." + check.Id));
                    }
                    double tw = s.Web.Thickness, sigmaZ = vertical / tw, tau = longitudinal / tw;
                    double equivalent = Math.Sqrt(sigmaX * sigmaX + sigmaZ * sigmaZ + sigmaX * sigmaZ + 3 * tau * tau);
                    Check(name + ".WebJunction.Equivalent", equivalent, s.Web.Fy / factors.GammaM0,
                        "EN1993-1-1:2005 6.2.1(5) eq6.1 with |sx*sz| added; web longitudinal shear and vertical junction load of the transfer");
                }
                void Flange(bool isTop, double delivered, double deliveredM, double t, double dm)
                {
                    var cover = isTop ? d.Top : d.Bottom; var plate = isTop ? s.TopFlange : s.BottomFlange;
                    string name = m + (isTop ? "Top" : "Bottom") + ".Parent";
                    double width = plate.Width - cover.Bolts.TransverseRows * p.Detail.Plates.Bolts.FlangeBolt.HoleDiameter;
                    double area = width * plate.Thickness, modulus = width * plate.Thickness * plate.Thickness / 6;
                    double n = Math.Abs(delivered) + Math.Abs(t) + gradient * plate.Width * plate.Thickness * Math.Abs(isTop ? s.TopY : s.BottomY);
                    double moment = Math.Abs(deliveredM) + Math.Abs(t) * plate.Thickness / 2 + Math.Abs(dm) + gradient * plate.Width * Math.Pow(plate.Thickness, 3) / 12;
                    if (!(area > 0)) { issues.Add(name + ".INVALID_NET_SECTION"); return; }
                    Value(name + ".AxialBound", n, "N", true); Value(name + ".LocalMomentBound", moment, "Nmm", true);
                    Check(name + ".TransferNetElastic", n / area + moment / modulus, Math.Min(plate.Fy / factors.GammaM0, .9 * plate.Fu / factors.GammaM2), NetReference);
                }
                void Web(double n, double moment)
                {
                    var web = s.Web; double hole = p.Detail.Plates.Bolts.WebBolt.HoleDiameter, offset = d.WebCoverOffset(member);
                    var ys = d.WebBolts.Points(SpliceSide.Left).Select(q => q.Y).Distinct().Select(y => y + offset).ToArray();
                    double area = (web.Width - ys.Length * hole) * web.Thickness;
                    double shift = offset == 0 ? 0 : -ys.Sum(y => hole * web.Thickness * y) / area;
                    double inertia = web.Thickness * Math.Pow(web.Width, 3) / 12 - ys.Sum(y => web.Thickness * (Math.Pow(hole, 3) / 12 + hole * y * y)) - area * shift * shift;
                    if (!(area > 0) || !(inertia > 0)) { issues.Add(m + "Web.Parent.INVALID_NET_SECTION"); return; }
                    double modulus = inertia / (web.Width / 2 + Math.Abs(shift));
                    double sigma = n / area + (moment + n * Math.Abs(shift)) / modulus, tau = 1.5 * Math.Abs(v) / area;
                    Value(m + "Web.Parent.NetCentroidShift", shift, "mm", true);
                    Check(m + "Web.Parent.TransferNetElastic", ElasticBoltGroup.Hypot(sigma, Math.Sqrt(3) * tau),
                        Math.Min(web.Fy / factors.GammaM0, .9 * web.Fu / factors.GammaM2), NetReference);
                }
            }
            void Check(string id, double demand, double resistance, string source)
            {
                if (!WeldedIBeamSplicePlates.Finite(demand) || !WeldedIBeamSplicePlates.Finite(resistance)) issues.Add(id + ".NUMERIC_RANGE");
                else checks.Add(new ConnectionCheck(id, demand, resistance, source + (plateCode.IsNtc ? "; supplementary model with NTC2018 factors" : ""), "MPa"));
            }
            void Value(string name, double value, string unit, bool report = false)
            {
                if (!WeldedIBeamSplicePlates.Finite(value)) { issues.Add(name + ".NUMERIC_RANGE"); return; }
                var entry = new SectionResistanceValue("Transition." + name, value, unit);
                trace.Add(entry); if (report) reported.Add(entry);
            }
        }
        private const string NetReference = "EN1993-1-1:2005 6.2.1(5),6.2.3; perforated parent plate in the transfer zone, sum of absolute delivered and transferred actions, elastic sufficient check";
    }
}
