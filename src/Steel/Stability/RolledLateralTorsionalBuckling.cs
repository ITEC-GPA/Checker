using System;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Members
{
    // LTB of rolled or equivalent welded doubly symmetric I/H: EN 1993-1-1:2005 6.3.2.3, equations (6.57)-(6.58),
    // Table 6.5 curves and Table 6.6 kc; NTC 2018 4.2.4.1.3.2 with Tab. 4.2.IX curves and the same kc rows (Tab. 4.2.X;
    // number and f equation [4.2.52] checked on the GU text 30/10/2026, the kc values are an image there: da verificare).
    // alphaLT is passed in: the one table of BucklingCurves (EC3 Table 6.3) or a declared national value. Field and input
    // checks stay with the caller.
    internal static class RolledLateralTorsionalBuckling
    {
        internal sealed class Result
        {
            internal double Phi { get; set; }
            /// <summary>1/(Phi + sqrt(Phi^2 - beta lambda^2)) before any bound; 1 on the plateau lambda &lt;= lambdaLT0.</summary>
            internal double Base { get; set; }
            internal double F { get; set; }
            internal double Chi { get; set; }
        }

        /// <summary>EN 1993-1-1:2005 Table 6.5 = NTC 2018 Tab. 4.2.IX: rolled I h/b &lt;= 2 b, &gt; 2 c; welded I h/b &lt;= 2 c,
        /// &gt; 2 d. Null outside exact doubly symmetric I/H with horizontal major axis: those keep the general method.</summary>
        internal static SteelBucklingCurve? Curve(ISteelSection s)
        {
            if (s.SectionShape.GetType() != typeof(SectionH)) return null;
            var h = (SectionH)s.SectionShape;
            if (h.LenghtTopFlange != h.LenghtBottomFlange || h.ThicknessTopFlange != h.ThicknessBottomFlange
                || Math.Abs(h.AngleX1) > 1e-10 || s.J11 < s.J22) return null;
            bool deep = h.Height / h.LenghtTopFlange > 2;
            if (s.SectionType == Section.SectionTypes.Rolled) return deep ? SteelBucklingCurve.C : SteelBucklingCurve.B;
            if (s.SectionType == Section.SectionTypes.Welded) return deep ? SteelBucklingCurve.D : SteelBucklingCurve.C;
            return null;
        }

        /// <summary>EN 1993-1-1:2005 Table 6.6: psi=1 1.0; linear 1/(1.33-0.33 psi); simply supported, fixed-ended and
        /// one-end-fixed diagrams 0.94/0.90/0.91 (distributed load) and 0.86/0.77/0.82 (central point load).
        /// NotEstablished is the conservative 1.0.</summary>
        internal static double Kc(SteelLtbMomentDistribution distribution, double? psi)
        {
            switch (distribution)
            {
                case SteelLtbMomentDistribution.NotEstablished: case SteelLtbMomentDistribution.Uniform: return 1;
                case SteelLtbMomentDistribution.Linear:
                    if (!psi.HasValue || !(psi >= -1 && psi <= 1)) throw new ArgumentOutOfRangeException(nameof(psi));
                    return psi.Value == 1 ? 1 : 1 / (1.33 - .33 * psi.Value);
                case SteelLtbMomentDistribution.SimplySupportedUniformLoad: return .94;
                case SteelLtbMomentDistribution.FixedEndsUniformLoad: return .90;
                case SteelLtbMomentDistribution.OneEndFixedUniformLoad: return .91;
                case SteelLtbMomentDistribution.SimplySupportedCentralLoad: return .86;
                case SteelLtbMomentDistribution.FixedEndsCentralLoad: return .77;
                case SteelLtbMomentDistribution.OneEndFixedCentralLoad: return .82;
                default: throw new ArgumentOutOfRangeException(nameof(distribution));
            }
        }

        /// <summary>f = 1 - 0.5 (1 - kc) [1 - 2 (lambdaLT - 0.8)^2], f &lt;= 1 (EC3 6.3.2.3(2) recommended; NTC 4.2.4.1.3.2).</summary>
        internal static double F(double kc, double lambda) => Math.Min(1, 1 - .5 * (1 - kc) * (1 - 2 * (lambda - .8) * (lambda - .8)));

        /// <summary>Phi = 0.5 [1 + alphaLT (lambda - lambdaLT0) + beta lambda^2], Base = 1/(Phi + sqrt(Phi^2 - beta lambda^2)).
        /// EC3 (6.57)/(6.58): chiLT = min(Base, 1, 1/lambda^2), chiLT,mod = min(chiLT/f, 1, 1/lambda^2).
        /// NTC 4.2.4.1.3.2: chiLT = min(Base/f, 1, 1/(f lambda^2)). Plateau: lambda &lt;= lambdaLT0 gives 1 (EC3 6.3.2.2(4)).</summary>
        internal static Result Reduce(double lambda, double alpha, double beta, double lambda0, double kc, bool ntc)
        {
            Positive(lambda, "Slenderness");
            double f = F(kc, lambda), phi = .5 * (1 + alpha * (lambda - lambda0) + beta * lambda * lambda);
            Positive(f, "Moment correction f"); Positive(phi, "PhiLT");
            if (lambda <= lambda0) return new Result { Phi = phi, Base = 1, F = f, Chi = 1 };
            double basis = 1 / (phi + Math.Sqrt(phi * phi - beta * lambda * lambda)), cap = 1 / (lambda * lambda);
            Positive(basis, "Reduction");
            double chi = ntc ? Math.Min(Math.Min(basis / f, 1), cap / f) : Math.Min(Math.Min(Math.Min(Math.Min(basis, 1), cap) / f, 1), cap);
            return new Result { Phi = phi, Base = basis, F = f, Chi = chi };
        }

        private static void Positive(double value, string name)
        { if (!(value > 0) || double.IsInfinity(value)) throw new ArithmeticException(name + " outside finite positive range."); }
    }
}
