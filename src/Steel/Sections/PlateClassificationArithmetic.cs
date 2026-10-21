using System;

namespace GPC.Checkers.Steel.Sections
{
    // Arithmetic shared with the facade. Alpha/elastic stresses are reconstructed
    // by the caller, independently of these Table 5.2 limits.
    internal static class PlateClassificationArithmetic
    {
        internal static void InternalLimits(double alpha, double psi, double epsilon,
            out double first, out double second, out double third)
        {
            if (alpha >= 1)
            {
                SectionClassificationArithmetic.Limits(CompressionPartKind.InternalFlat, epsilon, out first, out second, out third);
                return;
            }
            first = alpha > .5 ? 396.0 * epsilon / (13.0 * alpha - 1.0) : 36.0 * epsilon / alpha;
            second = alpha > .5 ? 456.0 * epsilon / (13.0 * alpha - 1.0) : 41.5 * epsilon / alpha;
            third = psi > -1 ? 42.0 * epsilon / (0.67 + 0.33 * psi)
                : 62.0 * epsilon * (1 - psi) * Math.Sqrt(-psi);
        }

        internal static int Internal(double c, double t, double alpha, double psi, double epsilon)
        {
            if (alpha <= 0) return 1;
            if (double.IsNaN(alpha)) throw new Exception("Section classification error");
            InternalLimits(alpha, psi, epsilon, out var first, out var second, out var third);
            return Classify(c / t, first, second, third);
        }

        internal static void OutstandLimits(double alpha, bool tipInCompression, double sigmaRoot, double sigmaTip,
            double epsilon, out double first, out double second, out double third, out double psi, out double usedPsi)
        {
            alpha = Math.Min(alpha, 1);
            double divisor = tipInCompression ? alpha : alpha * Math.Sqrt(alpha);
            first = 9 * epsilon / divisor; second = 10 * epsilon / divisor;
            if (sigmaRoot == sigmaTip)
            {
                third = 14 * epsilon; psi = usedPsi = 1; return;
            }
            double k;
            if (sigmaTip > sigmaRoot)
            {
                psi = sigmaRoot / sigmaTip; usedPsi = Math.Max(-3, psi);
                k = 0.57 - 0.21 * usedPsi + 0.07 * usedPsi * usedPsi;
            }
            else
            {
                psi = sigmaTip / sigmaRoot; usedPsi = Math.Max(-1, psi);
                k = usedPsi > 0 ? 0.578 / (usedPsi + 0.34) : 1.7 - 5 * usedPsi + 17.1 * usedPsi * usedPsi;
            }
            third = 21 * epsilon * Math.Sqrt(k);
        }

        internal static int Outstand(double c, double t, double alpha, bool tipInCompression,
            double sigmaRoot, double sigmaTip, double epsilon)
        {
            if (alpha <= 0 || Math.Max(sigmaRoot, sigmaTip) <= 0) return 1;
            OutstandLimits(alpha, tipInCompression, sigmaRoot, sigmaTip, epsilon,
                out var first, out var second, out var third, out _, out _);
            return Classify(c / t, first, second, third);
        }

        internal static int Classify(double ratio, double first, double second, double third)
            => ratio <= first ? 1 : ratio <= second ? 2 : ratio <= third ? 3 : 4;
    }
}
