using System;

namespace GPC.Checkers.Steel.Members
{
    // Scalar slenderness and reduction arithmetic. Code/family/curve and effective
    // property selection stay with the caller. No geometry or elastic restraint
    // model is inferred here. Original operation order is retained for compatibility.
    internal static class BucklingReductionArithmetic
    {
        internal static double AxialSlenderness(double area, double fy, double criticalForce)
            => Math.Pow(area * fy / criticalForce, 0.5);
        internal static double MomentSlenderness(double modulus, double fy, double criticalMoment)
            => Math.Sqrt(modulus * fy / criticalMoment);

        internal static double AxialChi(double phi, double lambdaSigned, double beta = 1.0)
        {
            return Math.Min(1.0 / (phi + Math.Pow(Math.Pow(phi, 2.0) - beta * Math.Pow(lambdaSigned, 2.0), 0.5)), 1.0);
        }

        internal static double Phi(double alphaImperfectionFactor, double lambdaSigned, double beta = 1.0, double lambda_LT0 = 0.2)
        {
            return 0.5 * (1 + alphaImperfectionFactor * (lambdaSigned - lambda_LT0) + beta * Math.Pow(lambdaSigned, 2.0));
        }

        internal static double MomentGradientFactor(double kc, double lambdaLT)
        {
            return Math.Min(1.0, 1.0 - 0.5 * (1.0 - kc) * (1.0 - 2.0 * Math.Pow(lambdaLT - 0.8, 2.0)));
        }

        internal static double LateralChi(double Phi, double lambdaSigned, double beta = 1.0, double f = 1.0)
        {
            double Chi = 1.0 / (Phi + Math.Pow(Math.Pow(Phi, 2.0) - beta * Math.Pow(lambdaSigned, 2.0), 0.5));
            Chi = LegacyMinimum(new double[] { Chi, 1.0, 1.0 / Math.Pow(lambdaSigned, 2.0) });

            double ChiLTMod = Math.Min(Chi / f, 1.0);
            ChiLTMod = Math.Min(ChiLTMod, 1.0 / Math.Pow(lambdaSigned, 2.0));
            return ChiLTMod;
        }

        // Preserve the facade's minimum/NaN behavior; validation remains external.
        private static double LegacyMinimum(double[] values)
        {
            double result = double.MaxValue;
            foreach (double value in values) if (value < result) result = value;
            return result;
        }
    }
}
