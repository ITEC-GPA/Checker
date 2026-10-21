using System;

namespace GPC.Checkers.Steel.Sections
{
    // Cross-section interactions only. No member length, buckling resistance or
    // curve enters these functions. Family/class/action applicability is external.
    internal static class SectionInteractionArithmetic
    {
        internal static double LinearAxialMoment(double n, double a, double momentResistance)
            => Math.Min(momentResistance, momentResistance * (1 - n) / (1 - 0.5 * a));
        internal static double MinorIMoment(double n, double a, double momentResistance)
            => n <= a ? momentResistance : momentResistance * (1 - Math.Pow((n - a) / (1 - a), 2));
        internal static double MajorIMomentWithShear(double wpl, double rho, double webArea, double tw, double fy, double gammaM0)
            => (wpl - rho * Math.Pow(webArea, 2) / (4 * tw)) * fy / gammaM0;
        internal static double UniformMomentWithShear(double rho, double momentResistance)
            => (1 - rho) * momentResistance;
        internal static double ElasticNormalRatio(double normalForce, double moment1, double moment2,
            double area, double modulus1, double modulus2, double yieldStrength, double gammaM0)
            => (Math.Abs(normalForce) / area + Math.Abs(moment1) / modulus1 + Math.Abs(moment2) / modulus2) * gammaM0 / yieldStrength;

        internal static double ShearReductionRho(double shear, double capacity)
        {
            if (double.IsNaN(shear) || double.IsInfinity(shear) ||
                double.IsNaN(capacity) || double.IsInfinity(capacity) || capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Finite shear and positive resistance required.");
            double ratio = Math.Abs(shear) / capacity;
            return ratio <= 0.5 ? 0 : Math.Min(1, Math.Pow(2 * ratio - 1, 2));
        }
    }
}
