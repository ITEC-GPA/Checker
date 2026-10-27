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
            var copy = section.CreateSectionCopy();
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
        internal WeldedIBeamSpliceForceResult(WeldedIBeamSpliceForceInput input, PlanarConnectionActions actions,
            IList<WeldedSpliceCoverForceGroup> groups, IList<SectionResistanceValue> trace, IList<string> issues)
        {
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
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var groups = new List<WeldedSpliceCoverForceGroup>(); var s = p.Section; var a = p.Actions;
            PlanarConnectionActions actions = null;
            void Value(string name, double value, string unit)
            { if (!Finite(value)) issues.Add(name + ".NUMERIC_RANGE"); else trace.Add(new SectionResistanceValue(name, value, unit)); }
            WeldedIBeamSpliceForceResult Result() => new WeldedIBeamSpliceForceResult(p, actions, groups, trace, issues);
            if (a.Fz != 0 || a.Mx != 0 || a.My != 0 || a.ApplicationZ != 0) issues.Add("IN_PLANE_N_V_M_ONLY");
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
            double moment = a.Mz + a.ApplicationX * a.Fy - a.ApplicationY * a.Fx;
            Value("Section.Area", s.Area, "mm2"); Value("Section.CentroidYFromBottom", s.CentroidY, "mm");
            Value("Section.StrongAxisSecondMoment", s.StrongAxisSecondMoment, "mm4");
            Value("Actions.MzAtJoint", moment, "Nmm");
            if (issues.Count != 0) return Result();
            actions = new PlanarConnectionActions(a.Fx, a.Fy, 0, 0, 0, moment);
            // Exact integrals of sigma=N/A-M*y/I on the three rectangular parent plates.
            double webN = Normal(s.Web, s.WebY), webM = moment * (s.Web.Thickness * Math.Pow(s.Web.Width, 3) / (12 * s.StrongAxisSecondMoment));
            Value("Web.AxialForce", webN, "N"); Value("Web.LocalMoment", webM, "Nmm");
            Value("Web.OffsetY", s.WebY, "mm");
            if (issues.Count != 0) return Result();
            foreach (SpliceSide side in new[] { SpliceSide.Left, SpliceSide.Right })
            {
                double sign = side == SpliceSide.Left ? 1 : -1;
                AddFlange(side, sign, true, p.Top, s.TopFlange, s.TopY);
                AddFlange(side, sign, false, p.Bottom, s.BottomFlange, s.BottomY);
                foreach (int face in new[] { -1, 1 })
                {
                    var d = ElasticBoltGroup.Calculate(p.WebBolts.Points(side),
                        new PlanarConnectionActions(sign * webN / 2, sign * a.Fy / 2, 0, 0, 0, sign * webM / 2));
                    Add(side, face < 0 ? WeldedSpliceCover.WebNegative : WeldedSpliceCover.WebPositive,
                        p.EachWebCover, s.WebY, face * ((s.Web.Thickness + p.EachWebCover.Thickness) / 2 + p.Packing.EachWeb),
                        -p.EachWebCover.Width / 2, p.EachWebCover.Width / 2, d);
                }
            }
            if (issues.Count != 0) return Result();
            foreach (SpliceSide side in new[] { SpliceSide.Left, SpliceSide.Right })
            {
                var sum = new double[6]; var scale = new double[6];
                foreach (var g in groups.Where(g => g.Side == side)) foreach (var f in g.Distribution.Forces)
                {
                    double x = f.Position.X, y = g.OriginY + (g.IsWeb ? f.Position.Y : 0),
                        z = g.OriginZ + (g.IsWeb ? 0 : f.Position.Y);
                    double fx = f.Fx, fy = g.IsWeb ? f.Fy : 0, fz = g.IsWeb ? 0 : f.Fy;
                    var terms = new[] { fx, fy, fz, y * fz - z * fy, z * fx - x * fz, x * fy - y * fx };
                    var magnitudes = new[] { Math.Abs(fx), Math.Abs(fy), Math.Abs(fz),
                        Math.Abs(y * fz) + Math.Abs(z * fy), Math.Abs(z * fx) + Math.Abs(x * fz), Math.Abs(x * fy) + Math.Abs(y * fx) };
                    for (int i = 0; i < 6; i++) { sum[i] += terms[i]; scale[i] += magnitudes[i]; }
                }
                double sign = side == SpliceSide.Left ? 1 : -1;
                var expected = new[] { sign * a.Fx, sign * a.Fy, 0, 0, 0, sign * moment };
                var names = new[] { "Fx", "Fy", "Fz", "Mx", "My", "Mz" };
                for (int i = 0; i < 6; i++)
                {
                    double residual = sum[i] - expected[i];
                    Value(side + ".Residual." + names[i], residual, i < 3 ? "N" : "Nmm");
                    if (!Finite(residual) || !Finite(scale[i]) || Math.Abs(residual) > ElasticBoltGroup.EquilibriumRelativeTolerance * (scale[i] + Math.Abs(expected[i])))
                        issues.Add(side + ".GLOBAL_EQUILIBRIUM_" + names[i]);
                }
            }
            return Result();

            double Normal(ConnectionPlate plate, double y) => (plate.Width * plate.Thickness / s.Area) * a.Fx
                - moment * (plate.Width * plate.Thickness / s.StrongAxisSecondMoment) * y;
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
            void AddFlange(SpliceSide side, double sign, bool top, SpliceFlangeCoverDetail d, ConnectionPlate parent, double y)
            {
                double n = Normal(parent, y), m = moment * (parent.Width * Math.Pow(parent.Thickness, 3) / (12 * s.StrongAxisSecondMoment));
                double outward = top ? 1 : -1;
                double yo = outward * ((parent.Thickness + d.Outer.Thickness) / 2 + (top ? p.Packing.TopOuter : p.Packing.BottomOuter)),
                    yi = -outward * ((parent.Thickness + d.EachInner.Thickness) / 2 + (top ? p.Packing.TopInner : p.Packing.BottomInner));
                // Fo+Fi=N; -yo*Fo-yi*Fi=M. No assumed N/2 or area-based split.
                double outer = (-m - yi * n) / (yo - yi), inner = n - outer;
                string name = side + (top ? ".Top" : ".Bottom");
                Value(name + ".ParentAxial", sign * n, "N"); Value(name + ".ParentLocalMoment", sign * m, "Nmm");
                Value(name + ".OuterForce", sign * outer, "N"); Value(name + ".BothInnerForce", sign * inner, "N");
                if (issues.Count != 0) return;
                Add(side, top ? WeldedSpliceCover.TopOuter : WeldedSpliceCover.BottomOuter, d.Outer, y + yo, 0,
                    -d.Outer.Width / 2, d.Outer.Width / 2,
                    ElasticBoltGroup.Calculate(d.Bolts.Points(side), new PlanarConnectionActions(sign * outer, 0, 0, 0, 0, 0)));
                foreach (int transverseSide in new[] { -1, 1 })
                {
                    double min = transverseSide < 0 ? -d.InnerGap / 2 - d.EachInner.Width : d.InnerGap / 2;
                    double max = min + d.EachInner.Width, centre = (min + max) / 2;
                    var points = d.Bolts.Points(side).Where(point => Math.Sign(point.Y) == transverseSide);
                    var cover = top ? (transverseSide < 0 ? WeldedSpliceCover.TopInnerNegative : WeldedSpliceCover.TopInnerPositive)
                        : (transverseSide < 0 ? WeldedSpliceCover.BottomInnerNegative : WeldedSpliceCover.BottomInnerPositive);
                    Add(side, cover, d.EachInner, y + yi, 0, min, max,
                        ElasticBoltGroup.Calculate(points, new PlanarConnectionActions(sign * inner / 2, 0, 0, 0, 0, 0, applicationY: centre)));
                }
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
