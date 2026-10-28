using System;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Members
{
    // The one table of imperfection factors and the one Perry-Robertson reduction of the member
    // routes: prismatic compression and LTB, beam-column and the EC3 6.3.4 general method.
    // EN 1993-1-1:2005 Tables 6.1 (a0..d) and 6.3 (LTB a..d); NTC 2018 Tables 4.2.VI/4.2.VIII
    // have the same values. Both curve enums declare A0, A, B, C, D in this order (public values).
    // BucklingReductionArithmetic keeps the legacy facade's Math.Pow operation order on purpose:
    // the two orders are not guaranteed to round alike, and results must stay bit-identical.
    internal static class BucklingCurves
    {
        private static readonly double[] Alphas = { .13, .21, .34, .49, .76 };

        internal static double Alpha(SteelBucklingCurve curve) => Factor((int)curve);
        internal static double Alpha(Ec3GeneralMethodCurve curve) => Factor((int)curve);
        private static double Factor(int curve)
            => (uint)curve < (uint)Alphas.Length ? Alphas[curve] : throw new ArgumentOutOfRangeException(nameof(curve));

        /// <summary>chi = 1 / (phi + sqrt(phi^2 - lambda^2)), phi = 0.5 [1 + alpha (lambda - 0.2) + lambda^2]
        /// (EN 1993-1-1 6.49, and 6.56 for the general LTB case). No plateau and no 1.0 bound: the routes apply them.</summary>
        internal static double PerryRobertson(double alpha, double lambda)
        {
            double phi = .5 * (1 + alpha * (lambda - .2) + lambda * lambda);
            return 1 / (phi + Math.Sqrt(phi * phi - lambda * lambda));
        }

        /// <summary>Member-route reduction: finite positive slenderness, plateau 0.2 and bound 1.0
        /// (prismatic compression, conservative LTB general case, beam-column).</summary>
        internal static double Reduction(double slenderness, SteelBucklingCurve curve)
        {
            Positive(slenderness, "Slenderness"); if (slenderness <= .2) return 1;
            double chi = PerryRobertson(Alpha(curve), slenderness); Positive(chi, "Reduction"); return Math.Min(1, chi);
        }

        /// <summary>LTB curve of the conservative general case: EN 1993-1-1:2005 Table 6.4 (recommended),
        /// NTC 2018 Table 4.2.IX. Sections other than H use curve d.</summary>
        internal static SteelBucklingCurve LateralTorsional(ISteelSection s, bool ntc)
        {
            if (s.SectionShape.GetType() != typeof(SectionH)) return SteelBucklingCurve.D;
            var h = (SectionH)s.SectionShape;
            bool deep = h.Height / h.LenghtTopFlange > 2;
            return s.SectionType == Section.SectionTypes.Welded ? (deep ? SteelBucklingCurve.D : SteelBucklingCurve.C)
                : ntc ? (deep ? SteelBucklingCurve.C : SteelBucklingCurve.B) : (deep ? SteelBucklingCurve.B : SteelBucklingCurve.A);
        }

        private static void Positive(double value, string name)
        { if (!(value > 0) || double.IsInfinity(value)) throw new ArithmeticException(name + " outside finite positive range."); }
    }
}
