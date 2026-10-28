using System;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Explicit alternative to a chart reading: inverse of SCI P398
    /// AppendixG (p163). Never silently substituted for supplied EN chart alpha.</summary>
    internal static class TStubSciAlpha
    {
        public static TStubChartAlpha Calculate(double m, double e, double m2, string geometrySource)
        {
            ConnectionValidation.Positive(m, nameof(m)); ConnectionValidation.Positive(e, nameof(e));
            ConnectionValidation.Positive(m2, nameof(m2));
            if (string.IsNullOrWhiteSpace(geometrySource)) throw new ArgumentException("Physical geometry source required.");
            double lambda1 = m / (m + e), lambda2 = m2 / (m + e);
            if (!(lambda1 > 0) || lambda1 > .9 || !(lambda2 > 0) || lambda2 > 1.4)
                throw new NotSupportedException("SCI alpha point outside the qualified chart domain.");
            double low = 4.45, high = 8;
            if (lambda1 < Curve(high, lambda2) - 1e-12 || lambda1 > Curve(low, lambda2) + 1e-12)
                throw new NotSupportedException("SCI alpha solution outside4.45..8; no extrapolation or clipping.");
            for (int i = 0; i < 70; i++)
            {
                double a = (low + high) / 2;
                if (Curve(a, lambda2) > lambda1) low = a; else high = a;
            }
            return new TStubChartAlpha((low + high) / 2, "SCI P398 AppendixG numerical inversion; " + geometrySource);
        }
        private static double Curve(double alpha, double lambda2)
        {
            double one = 1.25 / (alpha - 2.75), two = alpha * one / 2;
            return lambda2 >= two ? one : one + (1 - one) * Math.Pow((two - lambda2) / two, .185 * Math.Pow(alpha, 1.785));
        }
    }
}
