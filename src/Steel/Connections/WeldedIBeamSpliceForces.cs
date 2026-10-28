using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Three rectangular plates forming a straight welded I, symmetric about its web.
    /// Web.Width is its clear height. Flanges may differ. Coordinates start at the bottom face;
    /// weld reinforcement is not credited in section properties. Units mm, N, MPa.</summary>
    public sealed class WeldedIBeamSpliceSection
    {
        /// <summary>Reads the existing Model geometry, without creating a Model section hierarchy.
        /// The caller still supplies actions in the documented physical splice frame, not
        /// automatically in Model principal axes. Weld toes and seam resistance are separate.</summary>
        public static WeldedIBeamSpliceSection FromModel(SteelSectionInput section, double topWeldToe, double bottomWeldToe)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            var copy = section.ReadOnlySection;
            if (copy.SectionShape.GetType() != typeof(SectionH) || copy.SectionType != Section.SectionTypes.Welded)
                throw new NotSupportedException("An exact welded Model SectionH is required.");
            var h = (SectionH)copy.SectionShape;
            if (h.R != 0) throw new NotSupportedException("The three-plate splice model does not credit root radii or weld reinforcement.");
            return new WeldedIBeamSpliceSection(
                new ConnectionPlate(h.LenghtTopFlange, h.ThicknessTopFlange, section.YieldStrength, section.UltimateStrength),
                new ConnectionPlate(h.HeightWeb, h.ThicknessWeb, section.YieldStrength, section.UltimateStrength),
                new ConnectionPlate(h.LenghtBottomFlange, h.ThicknessBottomFlange, section.YieldStrength, section.UltimateStrength),
                topWeldToe, bottomWeldToe);
        }
        public ConnectionPlate TopFlange { get; }
        public ConnectionPlate Web { get; }
        public ConnectionPlate BottomFlange { get; }
        public double TopWeldToe { get; }
        public double BottomWeldToe { get; }
        public double Height { get; }
        public double Area { get; }
        public double CentroidY { get; }
        public double StrongAxisSecondMoment { get; }
        public double TopY => Height - TopFlange.Thickness / 2 - CentroidY;
        public double WebY => BottomFlange.Thickness + Web.Width / 2 - CentroidY;
        public double BottomY => BottomFlange.Thickness / 2 - CentroidY;
        public WeldedIBeamSpliceSection(ConnectionPlate topFlange, ConnectionPlate web,
            ConnectionPlate bottomFlange, double topWeldToe, double bottomWeldToe)
        {
            TopFlange = topFlange ?? throw new ArgumentNullException(nameof(topFlange));
            Web = web ?? throw new ArgumentNullException(nameof(web));
            BottomFlange = bottomFlange ?? throw new ArgumentNullException(nameof(bottomFlange));
            ConnectionValidation.Nonnegative(topWeldToe, nameof(topWeldToe));
            ConnectionValidation.Nonnegative(bottomWeldToe, nameof(bottomWeldToe));
            TopWeldToe = topWeldToe; BottomWeldToe = bottomWeldToe;
            if (web.Thickness >= Math.Min(topFlange.Width, bottomFlange.Width)
                || topWeldToe + bottomWeldToe >= web.Width)
                throw new ArgumentException("The web and seam toes must fit between and inside the flanges.");
            Height = bottomFlange.Thickness + web.Width + topFlange.Thickness;
            double at = topFlange.Width * topFlange.Thickness, aw = web.Width * web.Thickness,
                ab = bottomFlange.Width * bottomFlange.Thickness;
            Area = at + aw + ab;
            CentroidY = (at * (Height - topFlange.Thickness / 2)
                + aw * (bottomFlange.Thickness + web.Width / 2) + ab * bottomFlange.Thickness / 2) / Area;
            StrongAxisSecondMoment = at * (topFlange.Thickness * topFlange.Thickness / 12 + TopY * TopY)
                + aw * (web.Width * web.Width / 12 + WebY * WebY)
                + ab * (bottomFlange.Thickness * bottomFlange.Thickness / 12 + BottomY * BottomY);
            foreach (double value in new[] { Height, Area, CentroidY, StrongAxisSecondMoment })
                ConnectionValidation.Positive(value, nameof(value));
        }
    }

    /// <summary>One outer cover and two identical inner strips on one flange.
    /// InnerGap is the clear transverse distance between the strips, not their centroid spacing.
    /// Equal numbers of transverse bolt lines on the two inner strips; no packing or credited butt contact.</summary>
    public sealed class SpliceFlangeCoverDetail
    {
        public ConnectionPlate Outer { get; }
        public ConnectionPlate EachInner { get; }
        public double InnerGap { get; }
        public double EndDistance { get; }
        public SpliceBoltLayout Bolts { get; }
        public SpliceFlangeCoverDetail(ConnectionPlate outer, ConnectionPlate eachInner,
            double innerGap, double endDistance, SpliceBoltLayout bolts)
        {
            Outer = outer ?? throw new ArgumentNullException(nameof(outer));
            EachInner = eachInner ?? throw new ArgumentNullException(nameof(eachInner));
            Bolts = bolts ?? throw new ArgumentNullException(nameof(bolts));
            ConnectionValidation.Positive(innerGap, nameof(innerGap));
            ConnectionValidation.Positive(endDistance, nameof(endDistance));
            InnerGap = innerGap; EndDistance = endDistance;
        }
    }

    public sealed class WeldedIBeamSpliceForceInput
    {
        public WeldedIBeamSpliceSection Section { get; }
        public SpliceFlangeCoverDetail Top { get; }
        public SpliceFlangeCoverDetail Bottom { get; }
        public ConnectionPlate EachWebCover { get; }
        public SpliceBoltLayout WebBolts { get; }
        public double WebCoverEnd { get; }
        public double ButtGap { get; }
        public PlanarConnectionActions Actions { get; }
        public WeldedSplicePacking Packing { get; }
        /// <summary>Two identical, aligned I sections. Actions act TO the left member,
        /// in X longitudinal, Y up, Z transverse, at the supplied pole relative to the section centroid.
        /// Positive N is tension; positive Mz compresses the top flange. Equal steel E is assumed.</summary>
        public WeldedIBeamSpliceForceInput(WeldedIBeamSpliceSection section, SpliceFlangeCoverDetail top,
            SpliceFlangeCoverDetail bottom, ConnectionPlate eachWebCover, SpliceBoltLayout webBolts,
            double webCoverEnd, double buttGap, PlanarConnectionActions actions)
            : this(section, top, bottom, eachWebCover, webBolts, webCoverEnd, buttGap, actions, null) { }
        public WeldedIBeamSpliceForceInput(WeldedIBeamSpliceSection section, SpliceFlangeCoverDetail top,
            SpliceFlangeCoverDetail bottom, ConnectionPlate eachWebCover, SpliceBoltLayout webBolts,
            double webCoverEnd, double buttGap, PlanarConnectionActions actions, WeldedSplicePacking packing)
        {
            Section = section ?? throw new ArgumentNullException(nameof(section));
            Top = top ?? throw new ArgumentNullException(nameof(top));
            Bottom = bottom ?? throw new ArgumentNullException(nameof(bottom));
            EachWebCover = eachWebCover ?? throw new ArgumentNullException(nameof(eachWebCover));
            WebBolts = webBolts ?? throw new ArgumentNullException(nameof(webBolts));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            ConnectionValidation.Positive(webCoverEnd, nameof(webCoverEnd));
            ConnectionValidation.Nonnegative(buttGap, nameof(buttGap));
            WebCoverEnd = webCoverEnd; ButtGap = buttGap; Packing = packing ?? WeldedSplicePacking.None;
        }
        /// <summary>Another action state on exactly the same immutable physical parts: the
        /// geometry-only preparation is shared, never recomputed.</summary>
        internal WeldedIBeamSpliceForceInput(WeldedIBeamSpliceForceInput physical, PlanarConnectionActions actions)
        {
            Section = physical.Section; Top = physical.Top; Bottom = physical.Bottom; EachWebCover = physical.EachWebCover;
            WebBolts = physical.WebBolts; WebCoverEnd = physical.WebCoverEnd; ButtGap = physical.ButtGap; Packing = physical.Packing;
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            geometry = physical.Geometry;
        }
        private SpliceForceGeometry geometry;
        /// <summary>Geometry-only preparation of these physical parts; identical for every action state built from them.</summary>
        internal SpliceForceGeometry Geometry => geometry ?? System.Threading.LazyInitializer.EnsureInitialized(ref geometry, () => new SpliceForceGeometry(this));
    }

    /// <summary>Everything of the force distribution that does not depend on the actions:
    /// validation, section coefficients, cover offsets, names and prepared bolt groups.
    /// The evaluation repeats the original floating-point operations in the original order.</summary>
    internal sealed class SpliceForceGeometry
    {
        internal sealed class FlangePart
        {
            internal bool Top;
            internal SpliceFlangeCoverDetail Cover;
            internal ConnectionPlate Parent;
            internal double Y, AreaRatio, InertiaRatio, BendingRatio, Yo, Yi;
            internal string ParentAxial, ParentLocalMoment, OuterForce, BothInnerForce;
            internal WeldedSpliceCover OuterCover, NegativeCover, PositiveCover;
            internal double NegativeMin, NegativeMax, NegativeCentre, PositiveMin, PositiveMax, PositiveCentre;
            internal Lazy<ElasticBoltGroup.Prepared> Outer, Negative, Positive;
        }
        internal sealed class SidePart
        {
            internal SpliceSide Side;
            internal double Sign;
            internal FlangePart TopFlange, BottomFlange;
            internal Lazy<ElasticBoltGroup.Prepared> Web;
            internal string[] ResidualNames;
        }
        internal readonly string[] Issues;
        internal readonly SectionResistanceValue Area, Centroid, Inertia, WebOffset;
        internal readonly double WebAreaRatio, WebInertiaRatio, WebBendingRatio, WebY, WebFaceOffset;
        internal readonly bool WebOffsetFinite;
        internal readonly SidePart[] Sides;
        /// <summary>The sixteen cover groups in the order the force calculation creates them.</summary>
        internal sealed class GroupDescriptor
        {
            internal SpliceSide Side; internal WeldedSpliceCover Cover; internal ConnectionPlate Plate;
            internal double Minimum, Maximum; internal Lazy<ElasticBoltGroup.Prepared> Group;
        }
        internal readonly GroupDescriptor[] Groups;
        internal static readonly string[] ResidualComponents = { "Fx", "Fy", "Fz", "Mx", "My", "Mz" };

        internal SpliceForceGeometry(WeldedIBeamSpliceForceInput p)
        {
            var s = p.Section; var issues = new List<string>();
            ValidateFlange(p.Top, s.TopFlange, s.TopWeldToe, "Top");
            ValidateFlange(p.Bottom, s.BottomFlange, s.BottomWeldToe, "Bottom");
            if (p.WebBolts.TransverseRows < 2) issues.Add("WEB_AT_LEAST_TWO_TRANSVERSE_BOLTS_REQUIRED");
            if (p.EachWebCover.Width > s.Web.Width - 2 * Math.Max(s.TopWeldToe, s.BottomWeldToe))
                issues.Add("WEB_COVER_INTERSECTS_LONGITUDINAL_SEAMS");
            if (p.Packing.HasPacking)
            {
                double webOuterZ = s.Web.Thickness / 2 + p.Packing.EachWeb + p.EachWebCover.Thickness;
                double topStack = p.Top.EachInner.Thickness + p.Packing.TopInner;
                double bottomStack = p.Bottom.EachInner.Thickness + p.Packing.BottomInner;
                if (topStack + bottomStack > s.Web.Width
                    || webOuterZ > p.Top.InnerGap / 2 && p.EachWebCover.Width / 2 + topStack > s.Web.Width / 2
                    || webOuterZ > p.Bottom.InnerGap / 2 && p.EachWebCover.Width / 2 + bottomStack > s.Web.Width / 2)
                    issues.Add("PACKED_INNER_FLANGES_INTERSECT_WEB_COVERS");
            }
            if (p.WebBolts.TransverseSpan >= p.EachWebCover.Width) issues.Add("WEB_BOLTS_OUTSIDE_COVER");
            if (p.WebBolts.FirstBoltDistance <= p.ButtGap / 2) issues.Add("WEB_BOLTS_INSIDE_BUTT_GAP");
            Issues = issues.ToArray();
            Area = Entry("Section.Area", s.Area, "mm2"); Centroid = Entry("Section.CentroidYFromBottom", s.CentroidY, "mm");
            Inertia = Entry("Section.StrongAxisSecondMoment", s.StrongAxisSecondMoment, "mm4");
            WebAreaRatio = s.Web.Width * s.Web.Thickness / s.Area; WebInertiaRatio = s.Web.Width * s.Web.Thickness / s.StrongAxisSecondMoment;
            WebBendingRatio = s.Web.Thickness * Math.Pow(s.Web.Width, 3) / (12 * s.StrongAxisSecondMoment);
            WebY = s.WebY; WebOffset = Entry("Web.OffsetY", s.WebY, "mm"); WebOffsetFinite = WebOffset != null;
            WebFaceOffset = (s.Web.Thickness + p.EachWebCover.Thickness) / 2 + p.Packing.EachWeb;
            Sides = new[] { Side(SpliceSide.Left, 1), Side(SpliceSide.Right, -1) };
            var descriptors = new List<GroupDescriptor>();
            foreach (var side in Sides)
            {
                foreach (var f in new[] { side.TopFlange, side.BottomFlange })
                {
                    descriptors.Add(new GroupDescriptor { Side = side.Side, Cover = f.OuterCover, Plate = f.Cover.Outer, Minimum = -f.Cover.Outer.Width / 2, Maximum = f.Cover.Outer.Width / 2, Group = f.Outer });
                    descriptors.Add(new GroupDescriptor { Side = side.Side, Cover = f.NegativeCover, Plate = f.Cover.EachInner, Minimum = f.NegativeMin, Maximum = f.NegativeMax, Group = f.Negative });
                    descriptors.Add(new GroupDescriptor { Side = side.Side, Cover = f.PositiveCover, Plate = f.Cover.EachInner, Minimum = f.PositiveMin, Maximum = f.PositiveMax, Group = f.Positive });
                }
                foreach (var cover in new[] { WeldedSpliceCover.WebNegative, WeldedSpliceCover.WebPositive })
                    descriptors.Add(new GroupDescriptor { Side = side.Side, Cover = cover, Plate = p.EachWebCover, Minimum = -p.EachWebCover.Width / 2, Maximum = p.EachWebCover.Width / 2, Group = side.Web });
            }
            Groups = descriptors.ToArray();

            SidePart Side(SpliceSide side, double sign) => new SidePart
            {
                Side = side, Sign = sign,
                TopFlange = Flange(side, true, p.Top, s.TopFlange, s.TopY),
                BottomFlange = Flange(side, false, p.Bottom, s.BottomFlange, s.BottomY),
                Web = Group(() => p.WebBolts.Points(side)),
                ResidualNames = ResidualComponents.Select(c => side + ".Residual." + c).ToArray()
            };
            FlangePart Flange(SpliceSide side, bool top, SpliceFlangeCoverDetail d, ConnectionPlate parent, double y)
            {
                double outward = top ? 1 : -1;
                string name = side + (top ? ".Top" : ".Bottom");
                var f = new FlangePart
                {
                    Top = top, Cover = d, Parent = parent, Y = y,
                    AreaRatio = parent.Width * parent.Thickness / s.Area, InertiaRatio = parent.Width * parent.Thickness / s.StrongAxisSecondMoment,
                    BendingRatio = parent.Width * Math.Pow(parent.Thickness, 3) / (12 * s.StrongAxisSecondMoment),
                    Yo = outward * ((parent.Thickness + d.Outer.Thickness) / 2 + (top ? p.Packing.TopOuter : p.Packing.BottomOuter)),
                    Yi = -outward * ((parent.Thickness + d.EachInner.Thickness) / 2 + (top ? p.Packing.TopInner : p.Packing.BottomInner)),
                    ParentAxial = name + ".ParentAxial", ParentLocalMoment = name + ".ParentLocalMoment",
                    OuterForce = name + ".OuterForce", BothInnerForce = name + ".BothInnerForce",
                    OuterCover = top ? WeldedSpliceCover.TopOuter : WeldedSpliceCover.BottomOuter,
                    NegativeCover = top ? WeldedSpliceCover.TopInnerNegative : WeldedSpliceCover.BottomInnerNegative,
                    PositiveCover = top ? WeldedSpliceCover.TopInnerPositive : WeldedSpliceCover.BottomInnerPositive,
                    Outer = Group(() => d.Bolts.Points(side)),
                    Negative = Group(() => d.Bolts.Points(side).Where(point => Math.Sign(point.Y) == -1)),
                    Positive = Group(() => d.Bolts.Points(side).Where(point => Math.Sign(point.Y) == 1))
                };
                f.NegativeMin = -d.InnerGap / 2 - d.EachInner.Width; f.NegativeMax = f.NegativeMin + d.EachInner.Width;
                f.NegativeCentre = (f.NegativeMin + f.NegativeMax) / 2;
                f.PositiveMin = d.InnerGap / 2; f.PositiveMax = f.PositiveMin + d.EachInner.Width;
                f.PositiveCentre = (f.PositiveMin + f.PositiveMax) / 2;
                return f;
            }
            void ValidateFlange(SpliceFlangeCoverDetail d, ConnectionPlate parent, double toe, string name)
            {
                if (d.Bolts.TransverseRows < 2 || d.Bolts.TransverseRows % 2 != 0 || d.Bolts.LongitudinalRows < 2)
                    issues.Add(name + ".EVEN_TRANSVERSE_BOLT_LINES_AND_MULTIPLE_LONGITUDINAL_ROWS_REQUIRED");
                if (d.InnerGap < s.Web.Thickness + 2 * toe) issues.Add(name + ".INNER_COVER_INTERSECTS_WEB_OR_SEAM");
                if (d.Outer.Width > parent.Width || d.InnerGap + 2 * d.EachInner.Width > parent.Width) issues.Add(name + ".COVERS_OUTSIDE_FLANGE");
                double z = d.Bolts.TransversePitch / 2, far = d.Bolts.TransverseSpan / 2;
                if (far >= d.Outer.Width / 2 || z <= d.InnerGap / 2 || far >= d.InnerGap / 2 + d.EachInner.Width)
                    issues.Add(name + ".BOLTS_OUTSIDE_INNER_OR_OUTER_COVER");
                if (d.Bolts.FirstBoltDistance <= p.ButtGap / 2) issues.Add(name + ".BOLTS_INSIDE_BUTT_GAP");
            }
        }
        private static Lazy<ElasticBoltGroup.Prepared> Group(Func<IEnumerable<ConnectionPoint2D>> points)
            => new Lazy<ElasticBoltGroup.Prepared>(() => ElasticBoltGroup.Prepare(points()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        private static SectionResistanceValue Entry(string name, double value, string unit)
            => double.IsNaN(value) || double.IsInfinity(value) ? null : new SectionResistanceValue(name, value, unit);
    }

    public enum WeldedSpliceCover { TopOuter, TopInnerNegative, TopInnerPositive,
        BottomOuter, BottomInnerNegative, BottomInnerPositive, WebNegative, WebPositive }

    /// <summary>Forces transmitted to one member through ONE cover/shear plane.
    /// Web local (u,v)=(X,Y); flange local (u,v)=(X,Z). Positions include the actual
    /// cover centre-plane offset. Do not add the two planes as if each took the full bolt force.</summary>
    public sealed class WeldedSpliceCoverForceGroup
    {
        public SpliceSide Side { get; }
        public WeldedSpliceCover Cover { get; }
        public bool IsWeb => Cover == WeldedSpliceCover.WebNegative || Cover == WeldedSpliceCover.WebPositive;
        public ConnectionPlate Plate { get; }
        public double OriginY { get; }
        public double OriginZ { get; }
        public double TransverseMinimum { get; }
        public double TransverseMaximum { get; }
        public ElasticBoltGroupResult Distribution { get; }
        internal WeldedSpliceCoverForceGroup(SpliceSide side, WeldedSpliceCover cover, ConnectionPlate plate,
            double y, double z, double min, double max, ElasticBoltGroupResult distribution)
        { Side = side; Cover = cover; Plate = plate; OriginY = y; OriginZ = z;
            TransverseMinimum = min; TransverseMaximum = max; Distribution = distribution; }
    }

    /// <summary>Typed parent-flange forces of one member side; the trace entries of the same names are derived from them.</summary>
    internal sealed class SpliceFlangeForces
    {
        internal double ParentAxial { get; }
        internal double ParentLocalMoment { get; }
        internal double OuterForce { get; }
        internal double BothInnerForce { get; }
        internal SpliceFlangeForces(double parentAxial, double parentLocalMoment, double outerForce, double bothInnerForce)
        { ParentAxial = parentAxial; ParentLocalMoment = parentLocalMoment; OuterForce = outerForce; BothInnerForce = bothInnerForce; }
    }

    public sealed class WeldedIBeamSpliceForceResult
    {
        public WeldedIBeamSpliceForceInput Input { get; }
        public bool IsBalanced => Diagnostics.Count == 0;
        public bool CanQualifyWholeJoint => false;
        public PlanarConnectionActions ActionsAtJoint { get; }
        public IReadOnlyList<WeldedSpliceCoverForceGroup> CoverGroups { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public string Scope => "Elastic force distribution for identical welded I sections, including flange self-bending, "
            + "unequal upper/lower flanges, two faces of every splice and all cover centroid offsets. "
            + "Not a resistance, stiffness, load-introduction, execution or fatigue qualification.";
        private readonly SpliceFlangeForces[] flanges;
        /// <summary>Typed web axial force and local moment behind Web.AxialForce and Web.LocalMoment.</summary>
        internal double WebAxialForce { get; }
        internal double WebLocalMoment { get; }
        /// <summary>Typed parent-flange forces behind the trace entries Side.Top/Bottom.*; null before they were calculated.</summary>
        internal SpliceFlangeForces Flange(SpliceSide side, bool top) => flanges[(side == SpliceSide.Left ? 0 : 2) + (top ? 0 : 1)];
        internal WeldedIBeamSpliceForceResult(WeldedIBeamSpliceForceInput input, PlanarConnectionActions actions,
            IList<WeldedSpliceCoverForceGroup> groups, IList<SectionResistanceValue> trace, IList<string> issues,
            SpliceFlangeForces[] flanges = null, double webAxialForce = double.NaN, double webLocalMoment = double.NaN)
        {
            this.flanges = flanges ?? new SpliceFlangeForces[4]; WebAxialForce = webAxialForce; WebLocalMoment = webLocalMoment;
            Input = input; ActionsAtJoint = issues.Count == 0 ? actions : null;
            Diagnostics = Array.AsReadOnly(issues.ToArray());
            CoverGroups = Array.AsReadOnly(issues.Count == 0 ? groups.ToArray() : Array.Empty<WeldedSpliceCoverForceGroup>());
            Trace = Array.AsReadOnly(trace.Where(v => Finite(v.Value)).ToArray());
        }
        private static bool Finite(double v) => !double.IsInfinity(v) && !double.IsNaN(v);
    }

    public static class WeldedIBeamSpliceForces
    {
        public static WeldedIBeamSpliceForceResult Calculate(WeldedIBeamSpliceForceInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var g = p.Geometry;
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var groups = new List<WeldedSpliceCoverForceGroup>(14); var a = p.Actions;
            PlanarConnectionActions actions = null;
            var flanges = new SpliceFlangeForces[4]; double webAxial = double.NaN, webMoment = double.NaN;
            void Value(string name, double value, string unit)
            { if (!Finite(value)) issues.Add(name + ".NUMERIC_RANGE"); else trace.Add(new SectionResistanceValue(name, value, unit)); }
            void Prepared(SectionResistanceValue entry, string name) { if (entry == null) issues.Add(name + ".NUMERIC_RANGE"); else trace.Add(entry); }
            WeldedIBeamSpliceForceResult Result() => new WeldedIBeamSpliceForceResult(p, actions, groups, trace, issues, flanges, webAxial, webMoment);
            if (a.Fz != 0 || a.Mx != 0 || a.My != 0 || a.ApplicationZ != 0) issues.Add("IN_PLANE_N_V_M_ONLY");
            issues.AddRange(g.Issues);
            double moment = a.Mz + a.ApplicationX * a.Fy - a.ApplicationY * a.Fx;
            Prepared(g.Area, "Section.Area"); Prepared(g.Centroid, "Section.CentroidYFromBottom");
            Prepared(g.Inertia, "Section.StrongAxisSecondMoment");
            Value("Actions.MzAtJoint", moment, "Nmm");
            if (issues.Count != 0) return Result();
            actions = new PlanarConnectionActions(a.Fx, a.Fy, 0, 0, 0, moment);
            // Exact integrals of sigma=N/A-M*y/I on the three rectangular parent plates.
            double webN = g.WebAreaRatio * a.Fx - moment * g.WebInertiaRatio * g.WebY, webM = moment * g.WebBendingRatio;
            Value("Web.AxialForce", webN, "N"); Value("Web.LocalMoment", webM, "Nmm"); webAxial = webN; webMoment = webM;
            Prepared(g.WebOffset, "Web.OffsetY");
            if (issues.Count != 0) return Result();
            foreach (var side in g.Sides)
            {
                double sign = side.Sign;
                AddFlange(side, side.TopFlange);
                AddFlange(side, side.BottomFlange);
                // The two identical web covers share half of the web actions: one distribution for both faces.
                var d = ElasticBoltGroup.Evaluate(side.Web.Value,
                    new PlanarConnectionActions(sign * webN / 2, sign * a.Fy / 2, 0, 0, 0, sign * webM / 2));
                Add(side.Side, WeldedSpliceCover.WebNegative, p.EachWebCover, g.WebY, -1 * g.WebFaceOffset, -p.EachWebCover.Width / 2, p.EachWebCover.Width / 2, d);
                Add(side.Side, WeldedSpliceCover.WebPositive, p.EachWebCover, g.WebY, 1 * g.WebFaceOffset, -p.EachWebCover.Width / 2, p.EachWebCover.Width / 2, d);
            }
            if (issues.Count != 0) return Result();
            foreach (var side in g.Sides)
            {
                // Same terms and summation order as the original six-component arrays.
                var sum = new double[6]; var scale = new double[6];
                foreach (var group in groups) if (group.Side == side.Side) foreach (var f in group.Distribution.Forces)
                {
                    double x = f.Position.X, y = group.OriginY + (group.IsWeb ? f.Position.Y : 0),
                        z = group.OriginZ + (group.IsWeb ? 0 : f.Position.Y);
                    double fx = f.Fx, fy = group.IsWeb ? f.Fy : 0, fz = group.IsWeb ? 0 : f.Fy;
                    sum[0] += fx; sum[1] += fy; sum[2] += fz;
                    sum[3] += y * fz - z * fy; sum[4] += z * fx - x * fz; sum[5] += x * fy - y * fx;
                    scale[0] += Math.Abs(fx); scale[1] += Math.Abs(fy); scale[2] += Math.Abs(fz);
                    scale[3] += Math.Abs(y * fz) + Math.Abs(z * fy); scale[4] += Math.Abs(z * fx) + Math.Abs(x * fz);
                    scale[5] += Math.Abs(x * fy) + Math.Abs(y * fx);
                }
                double sign = side.Sign;
                var expected = new[] { sign * a.Fx, sign * a.Fy, 0, 0, 0, sign * moment };
                for (int i = 0; i < 6; i++)
                {
                    double residual = sum[i] - expected[i];
                    Value(side.ResidualNames[i], residual, i < 3 ? "N" : "Nmm");
                    if (!Finite(residual) || !Finite(scale[i]) || Math.Abs(residual) > ElasticBoltGroup.EquilibriumRelativeTolerance * (scale[i] + Math.Abs(expected[i])))
                        issues.Add(side.Side + ".GLOBAL_EQUILIBRIUM_" + SpliceForceGeometry.ResidualComponents[i]);
                }
            }
            return Result();

            void AddFlange(SpliceForceGeometry.SidePart side, SpliceForceGeometry.FlangePart part)
            {
                double sign = side.Sign;
                double n = part.AreaRatio * a.Fx - moment * part.InertiaRatio * part.Y, m = moment * part.BendingRatio;
                double yo = part.Yo, yi = part.Yi;
                // Fo+Fi=N; -yo*Fo-yi*Fi=M. No assumed N/2 or area-based split.
                double outer = (-m - yi * n) / (yo - yi), inner = n - outer;
                Value(part.ParentAxial, sign * n, "N"); Value(part.ParentLocalMoment, sign * m, "Nmm");
                Value(part.OuterForce, sign * outer, "N"); Value(part.BothInnerForce, sign * inner, "N");
                flanges[(side.Side == SpliceSide.Left ? 0 : 2) + (part.Top ? 0 : 1)] = new SpliceFlangeForces(sign * n, sign * m, sign * outer, sign * inner);
                if (issues.Count != 0) return;
                var d = part.Cover;
                Add(side.Side, part.OuterCover, d.Outer, part.Y + yo, 0, -d.Outer.Width / 2, d.Outer.Width / 2,
                    ElasticBoltGroup.Evaluate(part.Outer.Value, new PlanarConnectionActions(sign * outer, 0, 0, 0, 0, 0)));
                Add(side.Side, part.NegativeCover, d.EachInner, part.Y + yi, 0, part.NegativeMin, part.NegativeMax,
                    ElasticBoltGroup.Evaluate(part.Negative.Value, new PlanarConnectionActions(sign * inner / 2, 0, 0, 0, 0, 0, applicationY: part.NegativeCentre)));
                Add(side.Side, part.PositiveCover, d.EachInner, part.Y + yi, 0, part.PositiveMin, part.PositiveMax,
                    ElasticBoltGroup.Evaluate(part.Positive.Value, new PlanarConnectionActions(sign * inner / 2, 0, 0, 0, 0, 0, applicationY: part.PositiveCentre)));
            }
            void Add(SpliceSide side, WeldedSpliceCover cover, ConnectionPlate plate, double y, double z, double min, double max, ElasticBoltGroupResult d)
            {
                if (!d.IsSupported) issues.AddRange(d.Diagnostics.Select(issue => side + "." + cover + ":" + issue));
                if (!Finite(y) || !Finite(z)) issues.Add("COVER_CENTRE_PLANE_NUMERIC_RANGE");
                groups.Add(new WeldedSpliceCoverForceGroup(side, cover, plate, y, z, min, max, d));
            }
        }
        private static bool Finite(double value) => !double.IsInfinity(value) && !double.IsNaN(value);
    }
}