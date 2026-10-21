using System;

namespace GPC.Checkers.Steel.Members
{
    // Elastic bifurcation calculations only: N,mm,MPa,Nmm. No yield strength,
    // buckling curve, partial factor, resistance or Model types. The facade owns
    // restraint applicability, input validation and geometric reconstruction.
    internal static class ElasticStabilityArithmetic
    {
        internal static double Euler(double length, double J, double elasticModulus)
        {
            return Math.Pow(Math.PI, 2.0) * elasticModulus * J / (Math.Pow(length, 2.0));
        }

        internal static double Torsional(double elasticModulus, double poissonRatio, double r1, double r2,
            double dx, double dy, double torsionConstant, double warpingConstant, double effectiveLength)
        {
            double G = elasticModulus / (2.0 * (1.0 + poissonRatio));
            double i0 = Math.Pow(Math.Pow(r1, 2.0) + Math.Pow(r2, 2.0) +
                Math.Pow(dx, 2.0) + Math.Pow(dy, 2.0), 0.5);

            return 1.0 / Math.Pow(i0, 2.0) * (G * torsionConstant + Math.Pow(Math.PI, 2.0) * elasticModulus * warpingConstant / Math.Pow(effectiveLength, 2.0));
        }

        internal static double FlexuralTorsional(double Ncr1, double Ncr2, double NcrT, double x0, double y0, double i02)
        {
            // Coupled elastic characteristic determinant. Exact for a shared sine
            // mode with fork ends and free warping (coupling integrals equal 1).
            // Snijder/Balaz/Stroetmann, ce/papers8(2025),eq17,p618. Other effective
            // lengths remain an approximation; no general restraint eigenproblem
            // is claimed here. Normalize to avoid force-unit sensitivity and poles.
            double ax = x0 * x0 / i02, ay = y0 * y0 / i02;
            double lower = 0, upper = Math.Min(Ncr1, Math.Min(Ncr2, NcrT));
            for (int i = 0; i < 80; i++)
            {
                double n = lower + (upper - lower) / 2;
                double u = n / Ncr1, v = n / Ncr2, w = n / NcrT;
                double determinant = (1 - u) * (1 - v) * (1 - w)
                    - u * w * ax * (1 - v) - v * w * ay * (1 - u);
                if (determinant > 0) lower = n;
                else upper = n;
            }
            return lower + (upper - lower) / 2;
        }

        internal static double LateralTorsional(double elasticModulus, double G, double minorInertia, double torsionConstant,
            double warpingConstant, double length, double k, double kw, double c1, double loadTerm)
        {
            return c1 * (Math.Pow(Math.PI, 2.0) * elasticModulus * minorInertia / Math.Pow(k * length, 2.0)) *
                (Math.Pow(Math.Pow(k / kw, 2.0) * warpingConstant / minorInertia +
                Math.Pow(k * length, 2.0) * G * torsionConstant / (Math.Pow(Math.PI, 2.0) * elasticModulus * minorInertia) +
                Math.Pow(loadTerm, 2.0), 0.5) - (loadTerm));
        }

    }
}
