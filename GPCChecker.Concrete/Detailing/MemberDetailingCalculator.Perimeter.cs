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
            var lengths = new double[barPath.Count];
            for (int i = 0; i < lengths.Length; i++)
                lengths[i] = Distance(barPath[i].X - barPath[(i + 1) % barPath.Count].X,
                    barPath[i].Y - barPath[(i + 1) % barPath.Count].Y);
            double total = lengths.Sum();
            if (double.IsNaN(total) || double.IsInfinity(total) || total <= 1e-6)
                throw new ArgumentException("Column bar path requires a finite, positive length.");
            var positions = new List<double>();
            bool recognised = true;
            foreach (var bar in input.Geometry.Bars)
            {
                double nearest = double.PositiveInfinity, position = 0, accumulated = 0;
                for (int i = 0; i < barPath.Count; i++)
                {
                    var a = barPath[i]; var b = barPath[(i + 1) % barPath.Count]; double length = lengths[i];
                    if (length <= 1e-12) continue;
                    double dx = b.X - a.X, dy = b.Y - a.Y;
                    double t = Math.Max(0, Math.Min(1, ((bar.X - a.X) * dx + (bar.Y - a.Y) * dy) / (length * length)));
                    double distance = Distance(bar.X - a.X - t * dx, bar.Y - a.Y - t * dy);
                    if (distance < nearest) { nearest = distance; position = accumulated + t * length; }
                    accumulated += length;
                }
                if (nearest > 2e-5) recognised = false;
                positions.Add(position);
            }
            var result = Calculate(profile, input);
            double? maximum = null;
            if (recognised && positions.Count >= 2)
            {
                positions.Sort();
                double gap = total - positions[positions.Count - 1] + positions[0];
                for (int i = 1; i < positions.Count; i++) gap = Math.Max(gap, positions[i] - positions[i - 1]);
                maximum = gap;
            }
            result.Checks = result.Checks.Select(c => c.Key != "LongitudinalSpacing" ? c :
                new DetailingCheck(c.Key, maximum, NtcColumnMaximumSpacing, "mm",
                    maximum.HasValue ? (bool?)(maximum.Value <= NtcColumnMaximumSpacing) : null, c.Reference,
                    maximum.HasValue ? "Maximum spacing along the closed bar-centre path; curved portions discretized."
                    : "One or more bars are outside the declared perimeter path: verify longitudinal spacing separately.")).ToArray();
            return result;
        }

        private static double Distance(double x, double y) => Cracking.CrackSectionGeometry.Hypot(x, y);
    }
}
