using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Serviceability
{
    /// <summary>Real polynomial in ascending powers. Along a straight line of the section sigma is linear and the
    /// thin-walled shear stress at most quadratic, so sigma^2+3tau^2 is a quartic: its maximum on an interval is
    /// exact from the end points and the real roots of the derivative (no sampling).</summary>
    internal sealed class StressPolynomial
    {
        private readonly double[] c;
        internal static readonly StressPolynomial Zero = new StressPolynomial(0);

        internal StressPolynomial(params double[] coefficients)
        {
            int n = coefficients.Length;
            while (n > 1 && coefficients[n - 1] == 0) n--;
            c = new double[Math.Max(1, n)];
            Array.Copy(coefficients, c, n);
        }

        internal int Degree => c.Length - 1;
        internal double this[int power] => power < c.Length ? c[power] : 0;

        internal double At(double u)
        {
            double r = 0;
            for (int i = c.Length - 1; i >= 0; i--) r = r * u + c[i];
            return r;
        }

        internal StressPolynomial Derivative()
        {
            if (c.Length == 1) return Zero;
            var d = new double[c.Length - 1];
            for (int i = 1; i < c.Length; i++) d[i - 1] = i * c[i];
            return new StressPolynomial(d);
        }

        /// <summary>Integral from 0 to u.</summary>
        internal StressPolynomial Integral()
        {
            var r = new double[c.Length + 1];
            for (int i = 0; i < c.Length; i++) r[i + 1] = c[i] / (i + 1);
            return new StressPolynomial(r);
        }

        /// <summary>p(a + b u).</summary>
        internal StressPolynomial Compose(double a, double b)
        {
            var linear = new StressPolynomial(a, b);
            var r = new StressPolynomial(c[c.Length - 1]);
            for (int i = c.Length - 2; i >= 0; i--) r = r * linear + new StressPolynomial(c[i]);
            return r;
        }

        public static StressPolynomial operator +(StressPolynomial a, StressPolynomial b)
        {
            var r = new double[Math.Max(a.c.Length, b.c.Length)];
            for (int i = 0; i < r.Length; i++) r[i] = a[i] + b[i];
            return new StressPolynomial(r);
        }

        public static StressPolynomial operator *(StressPolynomial a, StressPolynomial b)
        {
            var r = new double[a.c.Length + b.c.Length - 1];
            for (int i = 0; i < a.c.Length; i++)
                for (int j = 0; j < b.c.Length; j++) r[i + j] += a.c[i] * b.c[j];
            return new StressPolynomial(r);
        }

        public static StressPolynomial operator *(double k, StressPolynomial a)
        {
            var r = new double[a.c.Length];
            for (int i = 0; i < r.Length; i++) r[i] = k * a.c[i];
            return new StressPolynomial(r);
        }

        /// <summary>Real roots in the open interval (lo, hi), ascending. The roots of the derivative split the
        /// interval into monotone pieces; a piece holds at most one root, found by bisection on a sign change.
        /// A root of even multiplicity (no sign change) may be missed: as a root of a derivative it is an
        /// inflection, never an extremum, so the maximum search below does not need it.</summary>
        internal List<double> Roots(double lo, double hi)
        {
            var roots = new List<double>();
            if (!(hi > lo) || Degree == 0) return roots;
            if (Degree == 1)
            {
                double r = -c[0] / c[1];
                if (r > lo && r < hi) roots.Add(r);
                return roots;
            }
            var cuts = new List<double> { lo };
            cuts.AddRange(Derivative().Roots(lo, hi));
            cuts.Add(hi);
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                double a = cuts[i], b = cuts[i + 1], fa = At(a), fb = At(b);
                if (fa == 0)
                {
                    if (a > lo && (roots.Count == 0 || roots[roots.Count - 1] < a)) roots.Add(a);
                    continue;
                }
                if (fb == 0 || Math.Sign(fa) == Math.Sign(fb)) continue;
                for (int k = 0; k < 200; k++)
                {
                    double m = .5 * (a + b);
                    if (!(m > a && m < b)) break;
                    double fm = At(m);
                    if (fm == 0) { a = b = m; break; }
                    if (Math.Sign(fm) == Math.Sign(fa)) { a = m; fa = fm; } else b = m;
                }
                roots.Add(.5 * (a + b));
            }
            return roots;
        }

        /// <summary>Abscissa of the maximum on [lo, hi]; the first one in ascending order on ties.</summary>
        internal double ArgMax(double lo, double hi)
        {
            double best = lo, value = At(lo);
            var candidates = Derivative().Roots(lo, hi);
            candidates.Add(hi);
            foreach (double u in candidates)
            {
                double v = At(u);
                if (v > value) { value = v; best = u; }
            }
            return best;
        }
    }
}
