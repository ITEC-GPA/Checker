using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Piles
{
    /// <summary>
    /// Base bearing factor of a pile and the steps of its interpolation: curves i and j of the slenderness z/D around the adopted ratio,
    /// friction angles adopted on each curve (degrees, clipped to the visible part of the curve), their values and the weight t.
    /// </summary>
    public sealed class NqResult
    {
        /// <summary>"Nq" for diameters up to 0.80 m, "Nq*" above.</summary>
        public string Factor { get; }
        public double FrictionAngleDegrees { get; }
        public double Slenderness { get; }
        public double AdoptedSlenderness { get; }
        public double Ratio1 { get; }
        public double Ratio2 { get; }
        public double FrictionAngle1 { get; }
        public double FrictionAngle2 { get; }
        public double Value1 { get; }
        public double Value2 { get; }
        public double Weight { get; }
        public double Nq { get; }
        /// <summary>φ outside the visible part of a curve: the border value is used (warning, no extrapolation).</summary>
        public bool FrictionAngleClipped { get; }
        /// <summary>z/D outside the curves: the border curve is used (warning, no extrapolation).</summary>
        public bool SlendernessClipped { get; }
        internal NqResult(string factor, double phi, double ratio, double adopted, double r1, double r2, double p1, double p2, double n1, double n2, double t, double nq, bool phiClipped, bool ratioClipped)
        {
            Factor = factor; FrictionAngleDegrees = phi; Slenderness = ratio; AdoptedSlenderness = adopted; Ratio1 = r1; Ratio2 = r2; FrictionAngle1 = p1; FrictionAngle2 = p2;
            Value1 = n1; Value2 = n2; Weight = t; Nq = nq; FrictionAngleClipped = phiClipped; SlendernessClipped = ratioClipped;
        }
    }

    /// <summary>
    /// Base bearing factors Nq (D ≤ 0.80 m) and Nq* (D &gt; 0.80 m) of the curves given by the user on 09/09/2026, parametrised version
    /// NQ-2026-09-09 transferred from ANTHEA (Anthea.Calculations.Nq, commit fe4652c; sources in ANTHEA/supporto/documentazione/riferimenti_nq).
    /// Equations, not tables: up to 0.80 m Nq = 10^(1 + (φ − φ10)/(φ100 − φ10)) with the anchors φ10, φ100 given by the user for L/D = 5, 10, 20, 50
    /// (straight lines of the semi-logarithmic figure); above 0.80 m piecewise cubics in u = φ − 34°, C2 at 34° and 38°, fitted to the figure for
    /// L/D = 4 and 32. Between the curves the ratio z/D is interpolated logarithmically (geometric on Nq, arithmetic on Nq*); outside the visible
    /// ranges the border is used. φ is not reduced.
    /// </summary>
    public static class BearingCapacityFactors
    {
        public const string Version = "NQ-2026-09-09";
        public const string Description = "Parametrizzata NQ-2026-09-09: D ≤ 0,80 m, Nq = 10^(a+b·φ), b=1/(φ100-φ10), a=1-b·φ10. D > 0,80 m: Nq* da cubiche raccordate a 34° e 38°, adattate alla figura fornita. φ in gradi, senza riduzioni. Per una quota z si usa z/D; alla punta finale z=L. Tra le curve: t=ln(r/r1)/ln(r2/r1), interpolazione geometrica di Nq per i pali medi e aritmetica di Nq* per i grandi. Questa regola tra L/D è una scelta numerica mantenuta dal metodo precedente, non desunta dalla figura. Fuori dagli intervalli visibili si usa il bordo, con segnalazione; i valori limitati non costituiscono una validazione fuori campo.";
        /// <summary>Diameter above which Nq* applies, mm.</summary>
        public const double LargeDiameter = 800;
        public static readonly IReadOnlyList<double> MediumRatios = new[] { 5.0, 10, 20, 50 };
        public static readonly IReadOnlyList<double> LargeRatios = new[] { 4.0, 32 };
        // φ10, φ100 (degrees) and the visible range of each medium curve.
        private static readonly (double P10, double P100, double Low, double High)[] Medium = { (23, 35.6, 23.4, 38.8), (24.6, 37, 23.6, 40), (25.8, 37.8, 23.6, 41), (27.5, 38.8, 24.8, 41.6) };
        // Nq* = c0 + c1 u + c2 u² + c3 u³ + c4 max(u, 0)³ + c5 max(u − 4, 0)³, u = φ − 34°.
        private static readonly double[][] Cubics = {
            new[] { 27.53212302495899, 3.245021450080038, 0.3465310545007305, 0.01982055661210097, -0.011218279222357186, -0.10608183901207004 },
            new[] { 23.394960047407167, 2.9357818054550844, 0.2768404988616615, 0.017268449836249766, 0.001955243085702829, -0.12061595971538405 } };

        /// <summary>Value of one curve at φ in degrees, without clipping: index 0-3 of the medium ratios or 0-1 of the large ratios.</summary>
        public static double Curve(double frictionAngleDegrees, int index, bool large)
        {
            if (!large) { var p = Medium[index]; return Math.Pow(10, 1 + (frictionAngleDegrees - p.P10) / (p.P100 - p.P10)); }
            var c = Cubics[index]; double u = frictionAngleDegrees - 34;
            return ((c[3] * u + c[2]) * u + c[1]) * u + c[0] + c[4] * Math.Pow(Math.Max(u, 0), 3) + c[5] * Math.Pow(Math.Max(u - 4, 0), 3);
        }

        /// <summary>
        /// Nq or Nq* for the friction angle (rad; converted to degrees and rounded to 1e-10° so that the bounds of the curves are compared on the
        /// values of the figure) and the slenderness z/D &gt; 0.
        /// </summary>
        public static NqResult Nq(double frictionAngle, double slenderness, bool largeDiameter)
        {
            if (double.IsNaN(frictionAngle) || double.IsInfinity(frictionAngle) || double.IsNaN(slenderness) || double.IsInfinity(slenderness) || slenderness <= 0)
                throw new ArgumentException("Nq: finite φ and positive finite z/D are required.");
            double phi = Math.Round(frictionAngle / SoilUnits.Degree, 10);
            var rr = largeDiameter ? LargeRatios : MediumRatios;
            double r = Clamp(slenderness, rr[0], rr[rr.Count - 1]);
            int i = -1, j;
            for (int k = 0; k < rr.Count; k++) if (rr[k] == r) { i = k; break; }
            if (i >= 0) j = i;
            else { i = Enumerable.Range(0, rr.Count - 1).First(k => rr[k] < r && r < rr[k + 1]); j = i + 1; }
            double p1 = Clamp(phi, largeDiameter ? 26 : Medium[i].Low, largeDiameter ? 42 : Medium[i].High);
            double p2 = Clamp(phi, largeDiameter ? 26 : Medium[j].Low, largeDiameter ? 42 : Medium[j].High);
            double n1 = Curve(p1, i, largeDiameter), n2 = Curve(p2, j, largeDiameter), t = i == j ? 0 : Math.Log(r / rr[i]) / Math.Log(rr[j] / rr[i]);
            double nq = i == j ? n1 : largeDiameter ? n1 + t * (n2 - n1) : Math.Exp(Math.Log(n1) + t * (Math.Log(n2) - Math.Log(n1)));
            return new NqResult(largeDiameter ? "Nq*" : "Nq", phi, slenderness, r, rr[i], rr[j], p1, p2, n1, n2, t, nq, p1 != phi || p2 != phi, r != slenderness);
        }

        internal static double Clamp(double value, double low, double high) => value < low ? low : value > high ? high : value;
    }
}
