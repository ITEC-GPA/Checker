using System;

namespace GPC.Checkers.Steel.Members
{
    // EN1993-1-1:2005 +AC2009 Table6.2, p58. This selects the residual-stress
    // curve; it does not compute Ncr or infer the load/restraint system.
    internal static class Ec3WeldedBucklingPolicy
    {
        internal static bool BoxRequiresCurveC(double width, double height, double flangeThickness,
            double webThickness, double? weldThroat)
        {
            Positive(width, nameof(width)); Positive(height, nameof(height));
            Positive(flangeThickness, nameof(flangeThickness)); Positive(webThickness, nameof(webThickness));
            if (weldThroat.HasValue) Positive(weldThroat.Value, nameof(weldThroat));
            // The exception requires ALL three strict inequalities. If the weld
            // size is missing and the geometric conditions hold, c envelopes b/c.
            return width / flangeThickness < 30 && height / webThickness < 30
                && (!weldThroat.HasValue || weldThroat.Value > .5 * flangeThickness);
        }

        private static void Positive(double x, string name)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || x <= 0)
                throw new ArgumentOutOfRangeException(name, "A finite positive dimension is required.");
        }
    }
}
