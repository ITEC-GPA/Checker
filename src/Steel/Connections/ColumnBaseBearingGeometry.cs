using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Connections
{
    internal static class ColumnBaseBearingGeometry
    {
        // Exact area and first moment of a disk clipped by an axis-aligned rectangle.
        // Integrate horizontal strips. Between the circle/rectangle intersections their
        // width is c + k*sqrt(1-y*y); work on the unit disk to avoid dimensional cancellation.
        // This is geometry, not an additional resistance formula. EN1993-1-8 6.2.6.12(2)
        // requires the centroid of the actual compressed bearing area for the anchor lever arm.
        internal static void CircleIntersection(double xLow, double xHigh, double yLow, double yHigh,
            double cx, double cy, double radius, out double area, out double firstMomentY)
        {
            area = firstMomentY = 0;
            double xl = Math.Max(-1, (xLow - cx) / radius), xr = Math.Min(1, (xHigh - cx) / radius);
            double yl = Math.Max(-1, (yLow - cy) / radius), yr = Math.Min(1, (yHigh - cy) / radius);
            if (xl >= xr || yl >= yr) return;
            if (xl == -1 && xr == 1 && yl == -1 && yr == 1)
            { area = Math.PI * radius * radius; firstMomentY = area * cy; return; }
            var cuts = new List<double> { yl, yr };
            AddCut(0);
            foreach (double x in new[] { xl, xr })
            {
                double y = Math.Sqrt(Math.Max(0, 1 - x * x));
                AddCut(y); AddCut(-y);
            }
            cuts.Sort(); double unitArea = 0, unitMoment = 0;
            for (int i = 1; i < cuts.Count; i++)
            {
                double a = cuts[i - 1], b = cuts[i];
                if (a == b) continue;
                double mid = (a + b) / 2, s = Math.Sqrt(Math.Max(0, 1 - mid * mid));
                if (Math.Min(xr, s) <= Math.Max(xl, -s)) continue;
                double constant = (xr < s ? xr : 0) - (xl > -s ? xl : 0);
                double roots = (xr < s ? 0 : 1) + (xl > -s ? 0 : 1);
                unitArea += constant * (b - a) + roots * (AreaPrimitive(b) - AreaPrimitive(a));
                unitMoment += constant * (b * b - a * a) / 2
                    + roots * (MomentPrimitive(b) - MomentPrimitive(a));
            }
            area = Math.Max(0, unitArea) * radius * radius;
            firstMomentY = area * cy + unitMoment * radius * radius * radius;
            void AddCut(double y) { if (y > yl && y < yr) cuts.Add(y); }
        }

        private static double AreaPrimitive(double y)
            => .5 * (y * Math.Sqrt(Math.Max(0, 1 - y * y)) + Math.Asin(y));
        private static double MomentPrimitive(double y)
            => -Math.Pow(Math.Max(0, 1 - y * y), 1.5) / 3;
    }
}
