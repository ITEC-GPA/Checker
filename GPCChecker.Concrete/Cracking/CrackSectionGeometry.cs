using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Cracking
{
    /// <summary>Ordinary bar of the crack check: position in the section plane, diameter and area (mm, mm²).</summary>
    public sealed class CrackBar
    {
        public double X { get; }
        public double Y { get; }
        public double Diameter { get; }
        public double Area { get; }
        public CrackBar(double x, double y, double diameter, double area) { X = x; Y = y; Diameter = diameter; Area = area; }
    }

    /// <summary>How the automatic maximum bar spacing is measured.</summary>
    public enum CrackBarLayout
    {
        /// <summary>Collinear rows along x and y, never bridging outside the concrete (rectangular, T and hollow rectangular sections).</summary>
        Rows,
        /// <summary>Circular section, one ring of bars: arc between adjacent bars.</summary>
        Ring,
        /// <summary>Circular section, concentric rings declared as such: arcs on each ring, never between rings.</summary>
        ConcentricRings
    }

    /// <summary>
    /// Section geometry for crack control in the plane of the section (the coordinates of the strain plane): outline, holes (mm) and ordinary bars
    /// in the order of the section rebars. The polygon constructor assumes circles centred at the origin;
    /// FromCurves preserves the analytic centre and the section/strain-plane coordinates.
    /// </summary>
    public sealed class CrackSectionGeometry
    {
        public IReadOnlyList<Point2d> Outline { get; }
        public IReadOnlyList<IReadOnlyList<Point2d>> Holes { get; }
        public IReadOnlyList<CrackBar> Bars { get; }
        public bool Circular { get; }
        public CrackBarLayout Layout { get; }
        /// <summary>Extent of the outline along x and y.</summary>
        public double Width { get; }
        public double Height { get; }

        internal double CenterX { get; }
        internal double CenterY { get; }
        internal bool HasCircularInnerRing { get; }

        public CrackSectionGeometry(IEnumerable<Point2d> outline, IEnumerable<IEnumerable<Point2d>> holes, IEnumerable<CrackBar> bars, CrackBarLayout layout)
            : this(outline, holes, bars, layout, 0, 0, true) { }

        private CrackSectionGeometry(IEnumerable<Point2d> outline, IEnumerable<IEnumerable<Point2d>> holes, IEnumerable<CrackBar> bars,
            CrackBarLayout layout, double centerX, double centerY, bool hasCircularInnerRing)
        {
            Outline = (outline ?? throw new ArgumentNullException(nameof(outline))).ToArray();
            Holes = (holes ?? Enumerable.Empty<IEnumerable<Point2d>>()).Select(h => (IReadOnlyList<Point2d>)h.ToArray()).ToArray();
            Bars = (bars ?? throw new ArgumentNullException(nameof(bars))).ToArray();
            if (Outline.Count < 3 || Holes.Any(h => h.Count < 3)) throw new ArgumentException("Cracking: polygons with at least three vertices are required.");
            if (Bars.Any(b => !Positive(b.Diameter) || !Positive(b.Area) || double.IsNaN(b.X + b.Y) || double.IsInfinity(b.X + b.Y)))
                throw new ArgumentException("Cracking: invalid bar.");
            Layout = layout; Circular = layout != CrackBarLayout.Rows;
            CenterX = centerX; CenterY = centerY; HasCircularInnerRing = hasCircularInnerRing;
            Width = Outline.Max(p => p.X) - Outline.Min(p => p.X); Height = Outline.Max(p => p.Y) - Outline.Min(p => p.Y);
        }

        /// <summary>
        /// Geometry of a Model section: concrete outline and holes, ordinary bars (tendons excluded) in the rebar order. A circle is recognised when
        /// all outline vertices lie at the same distance from the origin (relative 1e-6, at least 16 vertices); bars on more than one radius are
        /// concentric rings only when <paramref name="concentricRings"/> confirms it, otherwise the automatic spacing is not determined.
        /// </summary>
        public static CrackSectionGeometry From(ReinforcedConcreteSection section, bool concentricRings = false)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            var shape = section.ConcreteShape ?? throw new NotSupportedException("Cracking: the section has no concrete shape.");
            if (shape.Childs2d != null && shape.Childs2d.Length > 0) throw new NotSupportedException("Cracking: shapes with children inside the holes are not supported.");
            var outline = shape.Fill2d.Points.ToArray();
            var holes = (shape.Holes2d ?? new Polygon2d[0]).Select(h => (IEnumerable<Point2d>)h.Points.ToArray()).ToArray();
            var bars = section.Rebars.Where(r => r.RebarMaterial.SteelType != SteelMaterial.SteelTypes.Tendon && r.EpsilonP == 0)
                .Select(r => new CrackBar(r.Position.X, r.Position.Y, r.RebarSection.Diameter, r.Area)).ToArray();
            var radii = outline.Select(p => Math.Sqrt(p.X * p.X + p.Y * p.Y)).ToArray();
            bool circle = outline.Length >= 16 && radii.Max() - radii.Min() <= 1e-6 * radii.Max();
            var layout = !circle ? CrackBarLayout.Rows : concentricRings ? CrackBarLayout.ConcentricRings : CrackBarLayout.Ring;
            return new CrackSectionGeometry(outline, holes, bars, layout);
        }

        /// <summary>
        /// Explicitly samples the native Model curves with the given chord tolerance (mm) and maximum segment length.
        /// Preserves section coordinates and ordinary bar order; tendons and prestressed bars are excluded as in From.
        /// Disconnected regions and material islands are not supported. Existing From keeps its polygon contract.
        /// </summary>
        public static CrackSectionGeometry FromCurves(ReinforcedConcreteSection section, double chordTolerance,
            double maxSegmentLength = double.PositiveInfinity, bool concentricRings = false)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            var outlines = section.GetCurveOutlines();
            if (outlines.Count != 1) throw new NotSupportedException("Cracking: exactly one concrete region is required.");
            var bars = section.Rebars.Where(r => r.RebarMaterial.SteelType != SteelMaterial.SteelTypes.Tendon && r.EpsilonP == 0)
                .Select(r => new CrackBar(r.Position.X, r.Position.Y, r.RebarSection.Diameter, r.Area));
            return FromCurves(outlines[0], bars, chordTolerance, maxSegmentLength, concentricRings);
        }

        /// <summary>
        /// Samples one closed XY region, including holes, without moving its coordinates. Tolerance controls chord
        /// deviation, not the error of the crack calculation. Circles use their analytic centre for radial checks;
        /// other outlines (including ellipses) use rows, with explicit spacing when rows cannot determine it.
        /// </summary>
        public static CrackSectionGeometry FromCurves(SectionCurveOutline outline, IEnumerable<CrackBar> bars, double chordTolerance,
            double maxSegmentLength = double.PositiveInfinity, bool concentricRings = false)
        {
            if (outline == null) throw new ArgumentNullException(nameof(outline));
            if (outline.Children.Count != 0) throw new NotSupportedException("Cracking: shapes with children inside the holes are not supported.");
            var shape = outline.ToShape(chordTolerance, maxSegmentLength);
            var center = CircleCenter(outline.Boundary);
            var holes = outline.Holes;
            var inner = holes.Count == 1 ? CircleCenter(holes[0]) : null;
            bool innerRing = center != null && inner != null && Hypot(center.X - inner.X, center.Y - inner.Y) <= 1e-8;
            var layout = center == null ? CrackBarLayout.Rows : concentricRings ? CrackBarLayout.ConcentricRings : CrackBarLayout.Ring;
            return new CrackSectionGeometry(shape.Fill2d.Points, (shape.Holes2d ?? new Polygon2d[0]).Select(h => h.Points), bars,
                layout, center?.X ?? 0, center?.Y ?? 0, innerRing);
        }

        private static Point3d CircleCenter(Curve3d curve)
        {
            if (curve is ArcCurve3d arc && arc.IsClosed) return arc.Center;
            if (curve is EllipseCurve3d ellipse && ellipse.IsClosed && ellipse.SemiAxisX == ellipse.SemiAxisY) return ellipse.Center;
            return null;
        }

        internal double Radius(double x, double y) => Hypot(x - CenterX, y - CenterY);
        internal double Angle(CrackBar bar) => Math.Atan2(bar.Y - CenterY, bar.X - CenterX);

        internal static bool Positive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;

        // ---- Polygons (ANTHEA SectionRegions / SectionGeometry) ----

        /// <summary>Part of the polygon with qx·x + qy·y ≥ level (half-plane clipping, vertices kept in order).</summary>
        public static Point2d[] Clip(IReadOnlyList<Point2d> polygon, double qx, double qy, double level)
        {
            var result = new List<Point2d>();
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                double da = qx * a.X + qy * a.Y - level, db = qx * b.X + qy * b.Y - level;
                if (da >= 0) result.Add(new Point2d(a.X, a.Y));
                if ((da < 0 && db > 0) || (da > 0 && db < 0)) { double t = da / (da - db); result.Add(new Point2d(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y))); }
            }
            return result.ToArray();
        }

        public static double Area(IReadOnlyList<Point2d> polygon) => polygon.Count < 3 ? 0 : Math.Abs(new Polygon2d(polygon).GetSignedArea());

        /// <summary>Effective region beyond the level along (qx, qy): clipped outline minus clipped holes, with the given bars.</summary>
        public CrackRegion Region(string key, double qx, double qy, double level, int[] bars, double? width = null)
        {
            var outline = Clip(Outline, qx, qy, level);
            var holes = Holes.Select(h => Clip(h, qx, qy, level)).Where(h => h.Length >= 3).ToArray();
            return new CrackRegion(key, qx, qy, level, Area(outline) - holes.Sum(h => Area(h)), bars, bars.Sum(i => Bars[i].Area), width, outline, holes);
        }

        /// <summary>Signed clearance of a bar to the outline and holes minus its radius (negative outside the concrete).</summary>
        public double BarCover(CrackBar bar)
        {
            var point = new Point2d(bar.X, bar.Y);
            var outline = new Polygon2d(Outline);
            double clearance = outline.DistanceTo(point);
            bool inside = outline.IsPointInside(point, 0);
            foreach (var hole in Holes.Select(h => new Polygon2d(h)))
            {
                clearance = Math.Min(clearance, hole.DistanceTo(point));
                if (hole.IsPointInside(point, 0)) inside = false;
            }
            return (inside ? clearance : -clearance) - bar.Diameter / 2;
        }

        // ---- Maximum spacing of the effective tensile bars (ANTHEA TensionBarSpacing) ----

        /// <summary>Maximum distance between adjacent selected bars; null when fewer than two bars or when it cannot be determined.</summary>
        public double? MaximumSpacing(IReadOnlyList<int> tensileBarIndices)
        {
            var selected = new HashSet<int>(tensileBarIndices);
            if (selected.Count < 2) return null;
            var distances = new List<double>();
            if (Circular)
            {
                var radii = Bars.Select(b => Radius(b.X, b.Y)).ToArray();
                if (radii.Max() - radii.Min() > 1e-4 && Layout != CrackBarLayout.ConcentricRings) return null;
                foreach (var group in Bars.Select((b, i) => new { Bar = b, Index = i, Angle = Angle(b) }).GroupBy(b => Math.Round(radii[b.Index], 5)))
                {
                    var ring = group.OrderBy(b => b.Angle).ToArray(); double radius = group.Average(b => radii[b.Index]);
                    for (int i = 0; i < ring.Length; i++)
                    {
                        var a = ring[i]; var b = ring[(i + 1) % ring.Length];
                        if (!selected.Contains(a.Index) || !selected.Contains(b.Index)) continue;
                        double angle = b.Angle - a.Angle; if (angle <= 0) angle += 2 * Math.PI;
                        distances.Add(radius * angle);
                    }
                }
            }
            else
            {
                foreach (bool horizontal in new[] { true, false })
                {
                    var rows = Bars.Select((b, i) => new { Bar = b, Index = i }).GroupBy(b => Math.Round(horizontal ? b.Bar.Y : b.Bar.X, 6));
                    foreach (var row in rows)
                    {
                        var ordered = row.OrderBy(b => horizontal ? b.Bar.X : b.Bar.Y).ToArray();
                        for (int i = 1; i < ordered.Length; i++)
                        {
                            var a = ordered[i - 1]; var b = ordered[i];
                            if (!selected.Contains(a.Index) || !selected.Contains(b.Index)) continue;
                            if (!Inside((a.Bar.X + b.Bar.X) / 2, (a.Bar.Y + b.Bar.Y) / 2)) continue;
                            distances.Add(Hypot(b.Bar.X - a.Bar.X, b.Bar.Y - a.Bar.Y));
                        }
                    }
                }
            }
            return distances.Count > 0 ? distances.Max() : (double?)null;
        }

        private bool Inside(double x, double y)
        {
            foreach (var hole in Holes) if (Crossings(hole, x, y)) return false;
            return Crossings(Outline, x, y);
        }

        private static bool Crossings(IReadOnlyList<Point2d> polygon, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[j]; var b = polygon[i];
                if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return inside;
        }

        internal static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);

        // ---- Effective depth hc,eff (ANTHEA ConcreteCodeChecks.EffectiveCrackDepth) ----

        /// <summary>
        /// Depth of the effective tension area measured from the most tensioned fibre: min[2.5 (h − d); (h − x)/3 (h when entirely tensile); h/2].
        /// DIN (NCI 7.3.2(3)): (2 + 0.1 h/(h − d)) in [2.5; 5] times (h − d), at most h/2, and (h − x)/3 when not smaller than c + 20 mm.
        /// DS (DK NA Fig. 7.100): the concrete band whose centroid is at the tensile reinforcement.
        /// </summary>
        public double EffectiveDepth(CrackProfile profile, double qx, double qy, double top, double height, double coverToCenter, double tensileDepth, bool entirelyTensile,
            double nominalCover)
        {
            double hc = Math.Min(2.5 * coverToCenter, Math.Min(entirelyTensile ? height : tensileDepth / 3, height / 2));
            if (profile == CrackProfile.DinEN1992p11)
            {
                double coefficient = Math.Min(5, Math.Max(2.5, 2 + .1 * height / coverToCenter));
                hc = Math.Min(coefficient * coverToCenter, height / 2);
                if (!entirelyTensile && tensileDepth / 3 >= nominalCover + 20) hc = Math.Min(hc, tensileDepth / 3);
            }
            if (profile == CrackProfile.DsEN1992p11)
            {
                double target = top - coverToCenter, lo = 0, hi = entirelyTensile ? height / 2 : Math.Min(height, tensileDepth);
                for (int i = 0; i < 65; i++)
                {
                    double h = (lo + hi) / 2, area = 0, moment = 0;
                    void Accumulate(IReadOnlyList<Point2d> points, double sign)
                    {
                        var clip = Clip(points, qx, qy, top - h); if (clip.Length < 3) return;
                        var polygon = new Polygon2d(clip); double a = Math.Abs(polygon.GetSignedArea()); if (a < 1e-12) return;
                        var c = polygon.GetCentroid(); area += sign * a; moment += sign * a * (qx * c.X + qy * c.Y);
                    }
                    Accumulate(Outline, 1); foreach (var hole in Holes) Accumulate(hole, -1);
                    if (area <= 1e-12 || moment / area > target) lo = h; else hi = h;
                }
                hc = (lo + hi) / 2;
            }
            return hc;
        }
    }

    /// <summary>Effective region of a crack check: direction, level, area (mm²), bars and steel area, computed width (mm) and clipped polygons.</summary>
    public sealed class CrackRegion
    {
        /// <summary>Stable key: TensileZone, Face+x, Face−x, Face+y, Face−y, Radial(angle°), DsCoarseSystem, InnerWall+x.., InnerRing.</summary>
        public string Key { get; }
        public double Qx { get; }
        public double Qy { get; }
        public double Level { get; }
        public double Area { get; }
        public IReadOnlyList<int> BarIndices { get; }
        public double SteelArea { get; }
        public double? Width { get; internal set; }
        public IReadOnlyList<Point2d> Outline { get; }
        public IReadOnlyList<IReadOnlyList<Point2d>> Holes { get; }
        public CrackRegion(string key, double qx, double qy, double level, double area, IEnumerable<int> bars, double steelArea, double? width,
            IEnumerable<Point2d> outline, IEnumerable<IEnumerable<Point2d>> holes)
        {
            Key = key; Qx = qx; Qy = qy; Level = level; Area = area; BarIndices = bars.ToArray(); SteelArea = steelArea; Width = width;
            Outline = outline.ToArray(); Holes = holes.Select(h => (IReadOnlyList<Point2d>)h.ToArray()).ToArray();
        }
        internal CrackRegion WithWidth(double? width) => new CrackRegion(Key, Qx, Qy, Level, Area, BarIndices, SteelArea, width, Outline, Holes);
    }
}
