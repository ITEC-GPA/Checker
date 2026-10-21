using System;

namespace GPC.Checkers.Steel.Members
{
    // EN1993-1-1:2005 Annex A scalar arithmetic, not cross-section resistance.
    // Inputs must already satisfy the method's family, load and restraint scope.
    // Critical loads and section capacities have distinct parameters throughout.
    internal static class MemberInteractionArithmetic
    {
        internal static bool IsCombinedAction(double n, double m1, double m2)
            => (m1 != 0 || m2 != 0) && (n != 0 || (m1 != 0 && m2 != 0));
        internal static double Mu(double Ned, double Ncr, double Chi)
        {
            return (1.0 - Ned / Ncr) / (1.0 - Chi * Ned / Ncr);
        }

        internal static double Lambda0Limit(double normalForce, double kc, double NcrFlexTors, double NcrTorsional)
        {
            return 0.2 * Math.Pow(Math.Pow(kc, -2.0), 0.5) * Math.Pow((1.0 - Math.Abs(normalForce) / NcrTorsional) * (1.0 - Math.Abs(normalForce) / NcrFlexTors), 0.25);
        }

        internal static void LateralTerms(double alphaLT, double lambda0, double lambda2, double moment1, double moment2,
            double chiMod, double mrd1, double mrd2, double cmx, double cmy,
            out double bLT, out double cLT, out double dLT, out double eLT)
        {
            bLT = 0.5 * alphaLT * lambda0 * lambda0 * Math.Abs(moment1) * Math.Abs(moment2) / (chiMod * mrd1 * mrd2);
            cLT = 10.0 * alphaLT * lambda0 * lambda0 * Math.Abs(moment1) / ((5.0 + Math.Pow(lambda2, 4.0)) * cmx * chiMod * mrd1);
            dLT = 2.0 * alphaLT * lambda0 * Math.Abs(moment1) * Math.Abs(moment2) / ((0.1 + Math.Pow(lambda2, 4.0)) * cmx * chiMod * mrd1 * cmy * mrd2);
            eLT = 1.7 * alphaLT * lambda0 * Math.Abs(moment1) / ((0.1 + Math.Pow(lambda2, 4.0)) * cmx * chiMod * mrd1);

        }

        internal static void Coefficients(double npl, double lambdaMax, double wx, double wy, double cmx, double cmy,
            double bLT, double cLT, double dLT, double eLT, double wel1, double wpl1, double wel2, double wpl2,
            out double cxx, out double cxy, out double cyx, out double cyy)
        {
            cxx = Math.Max(1.0 + (wx - 1.0) * ((2.0 - 1.6 / wx * Math.Pow(cmx, 2) * lambdaMax - 1.6 / wx * Math.Pow(cmx, 2) * Math.Pow(lambdaMax, 2)) * npl - bLT),
                wel1 / wpl1);
            cxy = Math.Max(1.0 + (wy - 1.0) * ((2.0 - 14.0 * Math.Pow(cmy, 2) * Math.Pow(lambdaMax, 2) / Math.Pow(wy, 5.0)) * npl - cLT),
                0.6 * Math.Sqrt(wy / wx) * wel2 / wpl2);
            cyx = Math.Max(1.0 + (wx - 1.0) * ((2.0 - 14.0 * Math.Pow(cmx, 2) * Math.Pow(lambdaMax, 2) / Math.Pow(wx, 5.0)) * npl - dLT),
                0.6 * Math.Sqrt(wx / wy) * wel1 / wpl1);
            cyy = Math.Max(1.0 + (wy - 1.0) * ((2.0 - 1.6 / wy * Math.Pow(cmy, 2) * lambdaMax - 1.6 / wy * Math.Pow(cmy, 2) * Math.Pow(lambdaMax, 2)) - eLT) * npl,
                wel2 / wpl2);
        }

        internal static void KFactors(bool plasticClass, double normalForce, double ncr1, double ncr2,
            double cmx, double cmy, double cmLT, double mux, double muy, double wx, double wy,
            double cxx, double cxy, double cyx, double cyy,
            out double kxx, out double kxy, out double kyx, out double kyy)
        {
            if (plasticClass)
            {
                kxx = cmx * cmLT * mux / (1.0 - Math.Abs(normalForce) / ncr1) * 1.0 / cxx;
                kxy = cmy * mux / (1.0 - Math.Abs(normalForce) / ncr2) * 1.0 / cxy * 0.6 * Math.Sqrt(wy / wx);
                kyx = cmx * cmLT * muy / (1.0 - Math.Abs(normalForce) / ncr1) * 1.0 / cyx * 0.6 * Math.Sqrt(wx / wy);
                kyy = cmy * muy / (1.0 - Math.Abs(normalForce) / ncr2) * 1.0 / cyy;
            }
            else
            {
                kxx = cmx * cmLT * mux / (1.0 - Math.Abs(normalForce) / ncr1);
                kxy = cmy * mux / (1.0 - Math.Abs(normalForce) / ncr2);
                kyx = cmx * cmLT * muy / (1.0 - Math.Abs(normalForce) / ncr1);
                kyy = cmy * muy / (1.0 - Math.Abs(normalForce) / ncr2);
            }
        }
    }
}
