using System;

namespace GPC.Checkers.Steel.Members
{
    public enum Ec3GeneralMethodCurve { A0, A, B, C, D }

    /// <summary>EN 1993-1-1:2005 6.3.4(4)(a), minimum of the general flexural
    /// and LTB curves. In-plane compression/mono-axial bending only, no rotated
    /// plastic hinges. National Annex applicability must be established by the caller.
    /// This scalar API does not perform or validate the supplied structural analyses.</summary>
    public static class Ec3GeneralMethod
    {
        /// <param name="alphaUltK">Characteristic in-plane load amplifier INCLUDING
        /// relevant global/local imperfections and second-order effects, 6.3.4(2).</param>
        /// <param name="alphaCrOp">Elastic out-of-plane bifurcation amplifier of the
        /// SAME proportional load pattern, excluding in-plane flexural buckling.</param>
        /// <param name="analysisReference">Traceable description/reference of both analyses.</param>
        public static Ec3GeneralMethodResult EvaluateInPlane(double alphaUltK, double alphaCrOp,
            Ec3GeneralMethodCurve lateralCurve, Ec3GeneralMethodCurve lateralTorsionalCurve,
            double gammaM1, string parameterSet, string analysisReference)
        {
            Positive(alphaUltK, nameof(alphaUltK)); Positive(alphaCrOp, nameof(alphaCrOp));
            Positive(gammaM1, nameof(gammaM1));
            if (lateralTorsionalCurve == Ec3GeneralMethodCurve.A0)
                throw new ArgumentOutOfRangeException(nameof(lateralTorsionalCurve), "EC3 Table 6.3 defines LTB curves a/b/c/d, not a0.");
            if (string.IsNullOrWhiteSpace(parameterSet) || string.IsNullOrWhiteSpace(analysisReference))
                throw new ArgumentException("Explicit parameter-set and analysis provenance required.");
            double lambda = Math.Sqrt(alphaUltK) / Math.Sqrt(alphaCrOp);
            double chi = Reduction(lambda, lateralCurve), chiLT = Reduction(lambda, lateralTorsionalCurve);
            double resistanceFactor = Math.Min(chi, chiLT) * alphaUltK / gammaM1;
            Positive(resistanceFactor, "design load amplifier");
            return new Ec3GeneralMethodResult(alphaUltK, alphaCrOp, lambda, chi, chiLT,
                resistanceFactor, gammaM1, lateralCurve, lateralTorsionalCurve, parameterSet, analysisReference);
        }

        internal static double ElasticBowDivisor(Ec3GeneralMethodCurve curve)
        {
            // EN 1993-1-1:2005 Table 5.1, elastic analysis column.
            switch (curve)
            {
                case Ec3GeneralMethodCurve.A0: return 350;
                case Ec3GeneralMethodCurve.A: return 300;
                case Ec3GeneralMethodCurve.B: return 250;
                case Ec3GeneralMethodCurve.C: return 200;
                case Ec3GeneralMethodCurve.D: return 150;
                default: throw new ArgumentOutOfRangeException(nameof(curve));
            }
        }
        private static double Reduction(double lambda, Ec3GeneralMethodCurve curve)
        {
            double alpha = BucklingCurves.Alpha(curve);
            if (lambda <= .2) return 1;
            return BucklingCurves.PerryRobertson(alpha, lambda);
        }
        internal static void Positive(double value, string name)
        { if (!(value > 0) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
        internal static void Finite(double value, string name)
        { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
    }

    public sealed class Ec3GeneralMethodResult
    {
        public double AlphaUltK { get; }
        public double AlphaCrOp { get; }
        public double Slenderness { get; }
        public double Chi { get; }
        public double ChiLT { get; }
        public double ChiOp => Math.Min(Chi, ChiLT);
        public double DesignLoadAmplifier { get; }
        public double Utilization => 1 / DesignLoadAmplifier;
        public bool IsSatisfied => Utilization <= 1;
        public double GammaM1 { get; }
        public Ec3GeneralMethodCurve LateralCurve { get; }
        public Ec3GeneralMethodCurve LateralTorsionalCurve { get; }
        public string ParameterSet { get; }
        public string AnalysisReference { get; }
        public string Reference => "EN 1993-1-1:2005 6.3.4(2)-(4)(a), equations 6.63-6.64";
        internal Ec3GeneralMethodResult(double au, double ac, double lambda, double chi, double chiLT,
            double factor, double gamma, Ec3GeneralMethodCurve lateral, Ec3GeneralMethodCurve lt, string parameters, string analysis)
        { AlphaUltK=au; AlphaCrOp=ac; Slenderness=lambda; Chi=chi; ChiLT=chiLT; DesignLoadAmplifier=factor;
            GammaM1=gamma; LateralCurve=lateral; LateralTorsionalCurve=lt; ParameterSet=parameters; AnalysisReference=analysis; }
    }
}
