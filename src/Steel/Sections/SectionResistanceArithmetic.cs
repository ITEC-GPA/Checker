using System;

namespace GPC.Checkers.Steel.Sections
{
    // Scalar arithmetic only. Geometry, applicability and code choices belong
    // to the caller. Legacy entry points deliberately keep their own guards.
    internal static class SectionResistanceArithmetic
    {
        internal static double Normal(double areaOrModulus, double strength, double gamma)
            => areaOrModulus * strength / gamma;

        internal static double NetTension(double netArea, double ultimateStrength, double gammaM2)
            => .9 * netArea * ultimateStrength / gammaM2;

        internal static double Shear(double shearArea, double yieldStrength, double gammaM0)
            => shearArea * (yieldStrength / Math.Sqrt(3)) / gammaM0;
    }
}
