using System;

namespace GPC.Checkers.Steel.Sections
{
    /// <summary>Trace of one flat part under bending/compression, N-mm-MPa.
    /// Plastic alpha and elastic stresses are distinct inputs, not inferred from each other.</summary>
    public sealed class PlateClassification
    {
        public CompressionPartClassification Nominal { get; }
        public double? PlasticCompressedFraction { get; }
        public bool? PlasticTipInCompression { get; }
        public double? SigmaRoot { get; }
        public double? SigmaTip { get; }
        public double? ElasticStressRatio { get; }
        public double? UsedElasticStressRatio { get; }
        public bool UsesConservativeStressRatio => ElasticStressRatio.HasValue && UsedElasticStressRatio.HasValue && UsedElasticStressRatio != ElasticStressRatio;
        internal PlateClassification(CompressionPartClassification nominal, double? alpha, double? psi,
            double? usedPsi, bool? tip = null, double? root = null, double? end = null)
        {
            Nominal = nominal; PlasticCompressedFraction = alpha; ElasticStressRatio = psi;
            UsedElasticStressRatio = usedPsi; PlasticTipInCompression = tip; SigmaRoot = root; SigmaTip = end;
        }
    }

    /// <summary>EN1993-1-1:2005 Table 5.2, flat internal/outstand parts.
    /// Alpha is the plastic compressed width/c; psi is the elastic minimum/maximum
    /// longitudinal edge stress ratio with compression positive. For internal psi&lt;-1
    /// the caller must establish the applicability condition in Table 5.2 sheet 1.
    /// Alpha=1 uses conservative uniform compression for an internal part, as the facade.
    /// No geometry reconstruction, effective widths, NTC policy or member verification.</summary>
    public static class Ec3PlateClassification
    {
        public static PlateClassification Internal(double? width, double? thickness, double? fy,
            double? alpha, double? psi)
        {
            var basic = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, width, thickness, fy);
            if (!basic.IsEvaluated) return new PlateClassification(basic, alpha, psi, null);
            var problem = Validate(basic.Kind, alpha, "Alpha", 0, 1) ?? Validate(basic.Kind, psi, "Psi", null, 1);
            if (problem != null) return new PlateClassification(problem, alpha, psi, null);
            if (alpha == 0) return new PlateClassification(Uncompressed(basic), alpha, psi, null);
            PlateClassificationArithmetic.InternalLimits(alpha.Value, psi.Value, basic.Epsilon.Value,
                out var first, out var second, out var third);
            return new PlateClassification(Complete(basic, first, second, third), alpha, psi, alpha == 1 ? 1 : psi);
        }

        /// <summary>Root is the supported edge; tip is free. Elastic stresses in MPa,
        /// compression positive. PlasticTipInCompression describes the PLASTIC block.
        /// Elastic psi below the tabulated domain is conservatively clamped to -3
        /// for maximum compression at the tip, -1 at the root (trace retained).</summary>
        public static PlateClassification Outstand(double? width, double? thickness, double? fy,
            double? alpha, bool tipInCompression, double? sigmaRoot, double? sigmaTip)
        {
            var basic = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.OutstandFlat, width, thickness, fy);
            if (!basic.IsEvaluated) return new PlateClassification(basic, alpha, null, null, tipInCompression, sigmaRoot, sigmaTip);
            var problem = Validate(basic.Kind, alpha, "Alpha", 0, 1)
                ?? Validate(basic.Kind, sigmaRoot, "SigmaRoot", null, null) ?? Validate(basic.Kind, sigmaTip, "SigmaTip", null, null);
            if (problem != null) return new PlateClassification(problem, alpha, null, null, tipInCompression, sigmaRoot, sigmaTip);
            if (alpha == 0 || Math.Max(sigmaRoot.Value, sigmaTip.Value) <= 0)
                return new PlateClassification(Uncompressed(basic), alpha, null, null, tipInCompression, sigmaRoot, sigmaTip);
            PlateClassificationArithmetic.OutstandLimits(alpha.Value, tipInCompression, sigmaRoot.Value, sigmaTip.Value,
                basic.Epsilon.Value, out var first, out var second, out var third, out var psi, out var usedPsi);
            var result = !Finite(psi) ? Failure(basic.Kind, SectionClassificationStatus.CalculationError, "StressRatio.NumericalRange")
                : Complete(basic, first, second, third);
            return new PlateClassification(result, alpha, psi, usedPsi, tipInCompression, sigmaRoot, sigmaTip);
        }

        private static CompressionPartClassification Complete(CompressionPartClassification b, double first, double second, double third)
        {
            if (!Positive(first) || !Positive(second) || !Positive(third))
                return Failure(b.Kind, SectionClassificationStatus.CalculationError, "Limits.NumericalRange");
            return new CompressionPartClassification(b.Kind, SectionClassificationStatus.Completed,
                nominalClass: PlateClassificationArithmetic.Classify(b.WidthThicknessRatio.Value, first, second, third),
                width: b.WidthOrDiameter, thickness: b.Thickness, fy: b.YieldStrength, ratio: b.WidthThicknessRatio,
                epsilon: b.Epsilon, first: first, second: second, third: third);
        }

        private static CompressionPartClassification Uncompressed(CompressionPartClassification b)
            => new CompressionPartClassification(b.Kind, SectionClassificationStatus.Completed,
                diagnostic: "NoCompressedPart", nominalClass: 1, width: b.WidthOrDiameter,
                thickness: b.Thickness, fy: b.YieldStrength, ratio: b.WidthThicknessRatio, epsilon: b.Epsilon);

        private static CompressionPartClassification Validate(CompressionPartKind kind, double? value, string name, double? min, double? max)
        {
            if (!value.HasValue) return Failure(kind, SectionClassificationStatus.MissingInput, name + ".Required");
            if (!Finite(value.Value) || (min.HasValue && value < min) || (max.HasValue && value > max))
                return Failure(kind, SectionClassificationStatus.InvalidInput, name + ".OutOfRange");
            return null;
        }
        private static CompressionPartClassification Failure(CompressionPartKind kind, SectionClassificationStatus status, string code)
            => new CompressionPartClassification(kind, status, code, code);
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        private static bool Positive(double v) => v > 0 && Finite(v);
    }
}
