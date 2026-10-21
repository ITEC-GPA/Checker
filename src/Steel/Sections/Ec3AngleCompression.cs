using System;

namespace GPC.Checkers.Steel.Sections
{
    /// <summary>Uniform compression of an isolated equal-thickness angle, mm and MPa.
    /// EN 1993-1-1:2005/AC:2009 Table 5.2 sheets 2 and 3, printed pp.43-44.
    /// Returns a conservative class 3 or 4, never a claim of plastic rotation capacity.
    /// Continuous contact, unequal thickness, stress gradients and effective properties
    /// need a different model. The root radius is subtracted only from clear outstands.</summary>
    public static class Ec3AngleCompression
    {
        public static int Classify(double leg1, double leg2, double thickness, double yieldStrength, double rootRadius = 0)
        {
            Positive(leg1, nameof(leg1)); Positive(leg2, nameof(leg2));
            Positive(thickness, nameof(thickness)); Positive(yieldStrength, nameof(yieldStrength));
            if (!(rootRadius >= 0) || double.IsInfinity(rootRadius)) throw new ArgumentOutOfRangeException(nameof(rootRadius));
            if (thickness + rootRadius >= Math.Min(leg1, leg2))
                throw new ArgumentException("Angle legs must contain a positive clear outstand.");
            double epsilon = Math.Sqrt(235 / yieldStrength);
            double h = Math.Max(leg1, leg2), b = Math.Min(leg1, leg2);
            // Whole-angle dimensions are external, NOT the clear plate widths.
            bool wholeAngle = h / thickness <= 15 * epsilon &&
                (h / thickness + b / thickness) / 2 <= 11.5 * epsilon;
            bool outstands = (h - thickness - rootRadius) / thickness <= 14 * epsilon;
            return wholeAngle && outstands ? 3 : 4;
        }

        internal static void Positive(double value, string name)
        {
            if (!(value > 0) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
    }
}
