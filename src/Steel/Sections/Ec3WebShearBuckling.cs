using System;

namespace GPC.Checkers.Steel.Sections
{
    /// <summary>EN1993-1-1:2005 6.2.6(6) and EN1993-1-5:2006 5.2/5.3.
    /// N, mm, MPa. Flat web panel; transverse stiffeners at supports only.
    /// The caller must verify the support stiffeners and their welds separately.
    /// No flange contribution, rigid-end-post benefit, openings or transverse loads.</summary>
    public static class Ec3WebShearBuckling
    {
        /// <summary>Whether the unstiffened web requires a separate shear-buckling check.
        /// clearHeight is between flanges, not the classification width excluding fillets.</summary>
        public static bool RequiresCheck(double clearHeight, double thickness, double yieldStrength, double eta)
        {
            Validate(clearHeight, thickness, yieldStrength, eta);
            return clearHeight / thickness > 72 * Math.Sqrt(235 / yieldStrength) / eta;
        }

        /// <summary>Web contribution Vbw,Rd, non-rigid end post, Table5.1 and eq5.5.
        /// Returns a component resistance, not a complete section/member verdict.</summary>
        public static double NonRigidEndPostResistance(double clearHeight, double thickness, double yieldStrength, double gammaM1, double eta)
        {
            Validate(clearHeight, thickness, yieldStrength, eta); Positive(gammaM1, nameof(gammaM1));
            double lambda = clearHeight / (86.4 * thickness * Math.Sqrt(235 / yieldStrength));
            if (!(lambda > 0) || double.IsInfinity(lambda)) throw new ArithmeticException("WebShearBucklingNumericalRange");
            double chi = Math.Min(eta, .83 / lambda);
            double resistance = chi * yieldStrength * clearHeight * thickness / (Math.Sqrt(3) * gammaM1);
            if (!(resistance > 0) || double.IsInfinity(resistance)) throw new ArithmeticException("WebShearBucklingNumericalRange");
            return resistance;
        }

        private static void Validate(double h, double t, double fy, double eta)
        { Positive(h, nameof(h)); Positive(t, nameof(t)); Positive(fy, nameof(fy)); Positive(eta, nameof(eta)); }
        private static void Positive(double x, string name)
        {
            if (!(x > 0) || double.IsInfinity(x)) throw new ArgumentOutOfRangeException(name, "A finite positive value is required.");
        }
    }
}
