using GPC.Geometry;

namespace GPC.Checkers.Geotechnics.Slopes
{
    /// <summary>
    /// Geometry of the slip circles and of the section, without strengths or equilibrium. Transferred from ANTHEA
    /// (Anthea.Calculations.Geotechnics.SlopeGeometry, commit fe4652c): the tolerances of the legacy code (metres) are expressed in mm.
    /// </summary>
    public static class SlopeGeometry
    {
        /// <summary>Elevation of a polyline at x (linear interpolation; at a vertical step, the lower end of the step), mm.</summary>
        public static double Height(IReadOnlyList<SlopePoint> line, double x)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (line.Count < 2 || x < line[0].X - 1e-5 || x > line[line.Count - 1].X + 1e-5) throw new ArgumentException("The profile does not reach the abscissa " + x + " mm.");
            for (int i = 1; i < line.Count; i++)
                if (line[i].X > line[i - 1].X && x <= line[i].X + 1e-7)
                    return line[i - 1].Y + (line[i].Y - line[i - 1].Y) * (x - line[i - 1].X) / (line[i].X - line[i - 1].X);
            return line[line.Count - 1].Y;
        }

        /// <summary>Area (mm²) and centroid of a polygon, with GPC.Geometry.</summary>
        public static (double Area, SlopePoint Centroid) Properties(IReadOnlyList<SlopePoint> points)
        {
            var polygon = new Polygon2d(points.Select(p => new Point2d(p.X, p.Y)));
            var center = polygon.GetCentroid();
            return (Math.Abs(polygon.GetSignedArea()), new SlopePoint(center.X, center.Y));
        }

        /// <summary>Circle through the exit a and the entry b with its lowest point at the elevation bottom; null when it does not exist.</summary>
        public static SlipCircle? Through(SlopePoint a, SlopePoint b, double bottom)
        {
            double ha = a.Y - bottom, hb = b.Y - bottom;
            if (ha <= 0 || hb <= 0 || b.X <= a.X) return null;
            double r0 = Math.Max(ha, hb), r1 = Math.Max(1000, r0 * 2);
            double Span(double r) => Math.Sqrt(Math.Max(0, 2 * r * ha - ha * ha)) + Math.Sqrt(Math.Max(0, 2 * r * hb - hb * hb));
            if (Span(r0) > b.X - a.X + 1e-7) return null;
            while (Span(r1) < b.X - a.X && r1 < 1e9) r1 *= 2;
            for (int i = 0; i < 65; i++) { double r = (r0 + r1) / 2; if (Span(r) < b.X - a.X) r0 = r; else r1 = r; }
            double radius = (r0 + r1) / 2;
            var circle = new SlipCircle(a.X + Math.Sqrt(2 * radius * ha - ha * ha), bottom + radius, radius, a.X, b.X);
            // The arc stays single valued also with a vertical tangent at an end: the slice bases are evaluated inside the intervals.
            return circle.Y < Math.Max(a.Y, b.Y) - 1e-7 ? null : circle;
        }

        /// <summary>Border of the family of lower arcs: the circle with a vertical tangent at the higher entry; null when it does not exist.</summary>
        public static SlipCircle? TangentAtEntry(SlopePoint exit, SlopePoint entry)
        {
            double dx = entry.X - exit.X, dy = entry.Y - exit.Y;
            if (dx <= 0 || dy < 0 || dy >= dx) return null;
            double radius = (dx * dx + dy * dy) / (2 * dx);
            return new SlipCircle(entry.X - radius, entry.Y, radius, exit.X, entry.X);
        }

        /// <summary>Abscissae where the lower arc crosses the horizontal y, strictly inside the arc.</summary>
        public static IEnumerable<double> Crossings(SlipCircle c, double y)
        {
            if (y > c.Y || y < c.Y - c.Radius) yield break;
            double dx = Math.Sqrt(Math.Max(0, c.Radius * c.Radius - (c.Y - y) * (c.Y - y)));
            foreach (double x in new[] { c.X - dx, c.X + dx }) if (x > c.Left + 1e-5 && x < c.Right - 1e-5) yield return x;
        }

        /// <summary>Bottom and top of a polygon on the vertical x; null outside; the polygon must be simple with respect to the verticals.</summary>
        public static (double Bottom, double Top)? VerticalInterval(IReadOnlyList<SlopePoint> polygon, double x)
        {
            var hits = new List<double>();
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                if (x >= Math.Min(a.X, b.X) && x < Math.Max(a.X, b.X)) hits.Add(a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X));
            }
            if (hits.Count == 0) return null;
            if (hits.Count != 2) throw new ArgumentException("Rigid body not simple with respect to the vertical at x = " + x + " mm.");
            return (hits.Min(), hits.Max());
        }

        /// <summary>
        /// Slice boundaries of a circle: count equal parts plus the vertices of the surface, of the water line and of the bodies, the crossings of the
        /// layer interfaces, the split of the columns and the ends of the distributed loads; points closer than 1e-4 mm are merged.
        /// </summary>
        public static double[] Divisions(SlopeSection section, SlipCircle circle, int count)
        {
            var x = Enumerable.Range(0, count + 1).Select(i => circle.Left + (circle.Right - circle.Left) * i / count)
                .Concat(section.Surface.Select(p => p.X)).Concat(section.Water.Select(p => p.X))
                .Concat(section.Bodies.SelectMany(b => b.Polygon.Select(p => p.X)))
                .Concat(section.Columns.SelectMany(l => l).SelectMany(s => Crossings(circle, s.Bottom)))
                .Concat(section.ValleyLayers.Count > 0 ? new[] { section.SoilSplitX } : new double[0])
                .Concat(section.Loads.Where(l => l.Distributed).SelectMany(l => new[] { l.Left, l.Right }))
                .Where(v => v >= circle.Left && v <= circle.Right).OrderBy(v => v).ToArray();
            var merged = new List<double>();
            foreach (double v in x) if (merged.Count == 0 || v - merged[merged.Count - 1] > 1e-4) merged.Add(v);
            return merged.ToArray();
        }

        /// <summary>
        /// The circle exits left of the required abscissa, enters right of it, stays above the deepest layer, below the ground surface and below
        /// every vertex of the bodies.
        /// </summary>
        public static bool Admissible(SlopeSection section, SlipCircle circle)
        {
            if (circle.Left >= section.RequiredLeft || circle.Right <= section.RequiredRight || circle.Y - circle.Radius < section.CoveredBottom) return false;
            // Line minus convex lower arc is concave: the minima are at the ends of the segments.
            foreach (var p in section.Surface.Where(p => p.X > circle.Left && p.X < circle.Right)) if (circle.Base(p.X) > p.Y - 1e-4) return false;
            foreach (var body in section.Bodies)
                foreach (var p in body.Polygon) if (circle.Base(p.X) >= p.Y - 1e-3) return false;
            return true;
        }
    }
}
