using System;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.Sections
{
    /// <summary>Same elastic stress bound for EC3:2005 6.2.1(4)/6.2.9.2 and
    /// NTC 2018 4.2.3.2/4.2.4.1.2.8. N, mm, MPa, Nmm; principal centroidal axes.
    /// |N|/A+|M1|/Wel1+|M2|/Wel2 bounds all longitudinal fiber stresses.
    /// Wel values are the MINIMUM extreme-fiber moduli. This sufficient bound is
    /// conservative for asymmetric sections and is not an exact plastic interaction.
    /// Gross unperforated class 1-3; local buckling, shear stress and torsion excluded.</summary>
    public static class SectionNormalInteraction
    {
        public static double Elastic(SectionResistanceCode code, double normalForce, double moment1, double moment2,
            double area, double minimumModulus1, double minimumModulus2, double yieldStrength, int sectionClass)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (sectionClass < 1 || sectionClass > 4) throw new ArgumentOutOfRangeException(nameof(sectionClass));
            if (sectionClass == 4) throw new NotSupportedException("Class 4 requires effective properties.");
            foreach (double v in new[] { normalForce, moment1, moment2 }) SteelInputValidation.Finite(v, "action");
            foreach (double v in new[] { area, minimumModulus1, minimumModulus2, yieldStrength }) SteelInputValidation.Positive(v, "property");
            double result = (Math.Abs(normalForce) / area + Math.Abs(moment1) / minimumModulus1
                + Math.Abs(moment2) / minimumModulus2) * code.GammaM0 / yieldStrength;
            if (double.IsNaN(result) || double.IsInfinity(result)) throw new ArithmeticException("Nonfinite elastic normal interaction.");
            return result;
        }
    }
}
