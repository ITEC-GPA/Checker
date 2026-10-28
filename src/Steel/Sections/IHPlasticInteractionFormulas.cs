using System;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.Sections
{
    /// <summary>Closed-form plastic N-M1-M2 resistance of doubly symmetric I/H sections,
    /// rolled (with root radii) or welded with equal flanges, class 1/2, no bolt holes:
    /// EN 1993-1-1:2005 6.2.9.1(5)-(6), equations (6.36)-(6.38) and (6.41) with alpha=2,
    /// beta=5n>=1; NTC 2018 4.2.4.1.2.7 prescribes the same expressions.
    /// a=(A-2b tf)/A&lt;=0.5. Plastic moduli are the section's own (fillets included).
    /// Utilization scales both moments at fixed N, as the exact plastic H route.
    /// Low shear and zero torsion are preconditions of the caller. N, mm, MPa, Nmm.</summary>
    internal static class IHPlasticInteractionFormulas
    {
        internal sealed class Result
        {
            internal double NplRd, N, A, Mpl1Rd, Mpl2Rd, MN1Rd, MN2Rd, Beta, Utilization;
            internal bool AxialCapacityExhausted;
        }

        internal static Result Evaluate(double area, double flangeWidth, double flangeThickness,
            double wpl1, double wpl2, double fy, double gammaM0, double normalForce, double moment1, double moment2)
        {
            foreach (double v in new[] { area, flangeWidth, flangeThickness, wpl1, wpl2, fy, gammaM0 }) SteelInputValidation.Positive(v, "property");
            foreach (double v in new[] { normalForce, moment1, moment2 }) SteelInputValidation.Finite(v, "action");
            var r = new Result();
            double fd = fy / gammaM0;
            r.NplRd = area * fd; r.Mpl1Rd = wpl1 * fd; r.Mpl2Rd = wpl2 * fd;
            r.N = Math.Abs(normalForce) / r.NplRd;
            r.A = Math.Min((area - 2 * flangeWidth * flangeThickness) / area, 0.5);
            if (!(r.A >= 0)) throw new ArgumentException("Flanges larger than the section area: not an I/H section.");
            r.Beta = Math.Max(5 * r.N, 1);
            if (r.N >= 1)
            {
                r.AxialCapacityExhausted = true; r.Utilization = double.PositiveInfinity;
                return r;
            }
            r.MN1Rd = Math.Min(r.Mpl1Rd, r.Mpl1Rd * (1 - r.N) / (1 - 0.5 * r.A));
            r.MN2Rd = r.N <= r.A ? r.Mpl2Rd : r.Mpl2Rd * (1 - Math.Pow((r.N - r.A) / (1 - r.A), 2));
            double q1 = Math.Abs(moment1) / r.MN1Rd, q2 = Math.Abs(moment2) / r.MN2Rd;
            r.Utilization = q2 == 0 ? q1 : q1 == 0 ? q2 : MomentScale(q1, q2, r.Beta);
            if (double.IsNaN(r.Utilization) || double.IsInfinity(r.Utilization)) throw new ArithmeticException("Nonfinite plastic I/H interaction.");
            return r;
        }

        // u such that (q1/u)^2 + (q2/u)^beta = 1: the moments scaled by 1/u lie on the (6.41) boundary.
        private static double MomentScale(double q1, double q2, double beta)
        {
            double Boundary(double u) => Math.Pow(q1 / u, 2) + Math.Pow(q2 / u, beta) - 1;
            double lo = Math.Max(q1, q2), hi = lo * 2;
            while (Boundary(hi) > 0) hi *= 2;
            for (int i = 0; i < 200 && hi - lo > 1e-15 * hi; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (Boundary(mid) > 0) lo = mid; else hi = mid;
            }
            return hi;
        }
    }
}
