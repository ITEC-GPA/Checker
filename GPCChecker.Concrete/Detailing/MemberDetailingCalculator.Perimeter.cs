using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;

namespace GPC.Checkers.Concrete.Detailing
{
    public static partial class MemberDetailingCalculator
    {
        /// <summary>
        /// NTC column rules with spacing measured along a supplied closed path of bar centres (mm).
        /// The path must follow the actual reinforcement, including the discretized curved portions.
        /// Bars outside that path leave only the spacing check pending; all other checks still run.
        /// This does not certify link restraint, seismic confinement or anchorage.
        /// </summary>
        public static MemberDetailingResult CalculateColumnOnPerimeter(DetailingProfile profile,
            MemberDetailingInput input, IReadOnlyList<Point2d> barPath)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Kind != MemberDetailingKind.Column || !DetailingProfiles.IsNtc(profile))
                throw new ArgumentException("Perimeter detailing is available for NTC columns.");
            if (barPath == null) throw new ArgumentNullException(nameof(barPath));
            if (barPath.Count < 3 || barPath.Any(p => double.IsNaN(p.X + p.Y) || double.IsInfinity(p.X + p.Y)))
                throw new ArgumentException("Column bar path requires at least three finite points.");
            double total = 0;
            for (int i = 0; i < barPath.Count; i++)
                total += Distance(barPath[i].X - barPath[(i + 1) % barPath.Count].X, barPath[i].Y - barPath[(i + 1) % barPath.Count].Y);
            if (double.IsNaN(total) || double.IsInfinity(total) || total <= 1e-6)
                throw new ArgumentException("Column bar path requires a finite, positive length.");
            var result = Calculate(profile, input);
            double? maximum = SpacingAlongPath(input.Geometry.Bars, barPath);
            result.Checks = result.Checks.Select(c => c.Key != "LongitudinalSpacing" ? c :
                new DetailingCheck(c.Key, maximum, NtcColumnMaximumSpacing, "mm",
                    maximum.HasValue ? (bool?)(maximum.Value <= NtcColumnMaximumSpacing) : null, c.Reference,
                    maximum.HasValue ? "Maximum spacing along the closed bar-centre path; curved portions discretized."
                    : "One or more bars are outside the declared perimeter path: verify longitudinal spacing separately.")).ToArray();
            return result;
        }

        /// <summary>
        /// Largest gap between consecutive bars along the closed path, mm; null with fewer than two bars or when a bar is farther than 2e-5 mm from the
        /// path (bar not on the path).
        /// </summary>
        private static double? SpacingAlongPath(IReadOnlyList<Cracking.CrackBar> bars, IReadOnlyList<Point2d> path)
        {
            var lengths = new double[path.Count];
            for (int i = 0; i < lengths.Length; i++)
                lengths[i] = Distance(path[i].X - path[(i + 1) % path.Count].X, path[i].Y - path[(i + 1) % path.Count].Y);
            double total = lengths.Sum();
            var positions = new List<double>();
            foreach (var bar in bars)
            {
                double nearest = double.PositiveInfinity, position = 0, accumulated = 0;
                for (int i = 0; i < path.Count; i++)
                {
                    var a = path[i]; var b = path[(i + 1) % path.Count]; double length = lengths[i];
                    if (length <= 1e-12) continue;
                    double dx = b.X - a.X, dy = b.Y - a.Y;
                    double t = Math.Max(0, Math.Min(1, ((bar.X - a.X) * dx + (bar.Y - a.Y) * dy) / (length * length)));
                    double distance = Distance(bar.X - a.X - t * dx, bar.Y - a.Y - t * dy);
                    if (distance < nearest) { nearest = distance; position = accumulated + t * length; }
                    accumulated += length;
                }
                if (nearest > 2e-5) return null;
                positions.Add(position);
            }
            if (positions.Count < 2) return null;
            positions.Sort();
            double gap = total - positions[positions.Count - 1] + positions[0];
            for (int i = 1; i < positions.Count; i++) gap = Math.Max(gap, positions[i] - positions[i - 1]);
            return gap;
        }

        /// <summary>
        /// Spacing of NTC columns along the perimeter of the bars (R12, F5.15): the convex hull of the bar centres, when every bar lies on it (bars along the
        /// faces of rectangular sections); null when a bar lies inside it (T and hollow sections, inner bars) or the bars are collinear.
        /// </summary>
        private static double? PerimeterSpacing(IReadOnlyList<Cracking.CrackBar> bars)
        {
            var points = bars.Select(b => new Point2d(b.X, b.Y)).OrderBy(q => q.X).ThenBy(q => q.Y).ToArray();
            if (points.Length < 3) return null;
            double Cross(Point2d o, Point2d a, Point2d b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
            // Andrew's monotone chain; collinear points are dropped, the bars on the edges are found again by SpacingAlongPath.
            var hull = new List<Point2d>();
            foreach (var pass in new[] { points, points.Reverse().ToArray() })
            {
                int start = hull.Count;
                foreach (var q in pass)
                {
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], q) <= 1e-9) hull.RemoveAt(hull.Count - 1);
                    hull.Add(q);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull.Count < 3 ? (double?)null : SpacingAlongPath(bars, hull);
        }

        private static double Distance(double x, double y) => Cracking.CrackSectionGeometry.Hypot(x, y);
    }
}
