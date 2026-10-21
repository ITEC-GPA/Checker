using System;

namespace GPC.Checkers.Steel.Sections
{
    public enum CompressionPartKind { InternalFlat, OutstandFlat, CircularHollow }
    public enum SectionClassificationStatus { Completed, MissingInput, InvalidInput, CalculationError }

    /// <summary>Nominal class of one part. Class 4 is a completed classification,
    /// not a resistance failure or an effective-section calculation. Units: mm, MPa.</summary>
    public sealed class CompressionPartClassification
    {
        public CompressionPartKind Kind { get; }
        public SectionClassificationStatus Status { get; }
        public int? NominalClass { get; }
        public string Reference { get; }
        public string DiagnosticCode { get; }
        public string Message { get; }
        public double? WidthOrDiameter { get; }
        public double? Thickness { get; }
        public double? YieldStrength { get; }
        public double? WidthThicknessRatio { get; }
        public double? Epsilon { get; }
        public double? Class1Limit { get; }
        public double? Class2Limit { get; }
        public double? Class3Limit { get; }
        public bool IsEvaluated => Status == SectionClassificationStatus.Completed;

        internal CompressionPartClassification(CompressionPartKind kind, SectionClassificationStatus status,
            string diagnostic = null, string message = null, int? nominalClass = null,
            double? width = null, double? thickness = null, double? fy = null, double? ratio = null,
            double? epsilon = null, double? first = null, double? second = null, double? third = null,
            string reference = "EN 1993-1-1:2005 Table 5.2")
        {
            Kind = kind; Status = status; DiagnosticCode = diagnostic; Message = message;
            NominalClass = nominalClass; WidthOrDiameter = width; Thickness = thickness; YieldStrength = fy;
            WidthThicknessRatio = ratio; Epsilon = epsilon; Class1Limit = first; Class2Limit = second; Class3Limit = third;
            Reference = reference;
        }
    }

    /// <summary>Nominal classification of internal/outstand flat parts under uniform
    /// compression, and CHS in bending and/or compression. Width is the clear plate
    /// width c (already excluding the relevant radii/welds), or CHS outer diameter d.
    /// Does not reconstruct geometry, qualify a complete angle, apply 5.5.2(9),
    /// calculate effective properties, or verify resistance/member stability.</summary>
    public static class Ec3CompressionPartClassification
    {
        public static CompressionPartClassification Evaluate(CompressionPartKind kind,
            double? widthOrDiameter, double? thickness, double? yieldStrength)
            => CompressionPartEvaluator.Evaluate(kind, widthOrDiameter, thickness, yieldStrength, false);
    }

    // Shared numerical rules, with separately identified EC3 and NTC prescriptions.
    internal static class CompressionPartEvaluator
    {
        internal static CompressionPartClassification Evaluate(CompressionPartKind kind,
            double? widthOrDiameter, double? thickness, double? yieldStrength, bool ntc)
        {
            if (!Enum.IsDefined(typeof(CompressionPartKind), kind))
                return Failure(kind, SectionClassificationStatus.InvalidInput, "PartKind.Invalid", "Unknown compression-part kind.", ntc);
            string[] names = { "WidthOrDiameter", "Thickness", "Fy" };
            double?[] values = { widthOrDiameter, thickness, yieldStrength };
            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i].HasValue)
                    return Failure(kind, SectionClassificationStatus.MissingInput, names[i] + ".Required", names[i] + " is required.", ntc);
                if (!(values[i].Value > 0) || double.IsInfinity(values[i].Value))
                    return Failure(kind, SectionClassificationStatus.InvalidInput, names[i] + ".NotPositiveFinite", names[i] + " must be positive and finite.", ntc);
            }
            double width = widthOrDiameter.Value, t = thickness.Value, fy = yieldStrength.Value;
            if (kind == CompressionPartKind.CircularHollow && t >= width / 2)
                return Failure(kind, SectionClassificationStatus.InvalidInput, "Chs.NoHollowCore", "CHS thickness must be less than half the outer diameter.", ntc);
            double ratio = width / t, epsilon = Math.Sqrt(235 / fy);
            SectionClassificationArithmetic.Limits(kind, epsilon, out var first, out var second, out var third);
            if (ntc && kind == CompressionPartKind.CircularHollow)
            { first = 50 * (235 / fy); second = 70 * (235 / fy); third = 90 * (235 / fy); }
            if (!PositiveFinite(ratio) || !PositiveFinite(epsilon) || !PositiveFinite(first) || !PositiveFinite(second) || !PositiveFinite(third))
                return Failure(kind, SectionClassificationStatus.CalculationError, "NumericalRange", "The ratio or classification limits are outside the positive finite range.", ntc);
            return new CompressionPartClassification(kind, SectionClassificationStatus.Completed,
                nominalClass: ratio <= first ? 1 : ratio <= second ? 2 : ratio <= third ? 3 : 4,
                width: width, thickness: t, fy: fy, ratio: ratio, epsilon: epsilon, first: first, second: second, third: third,
                reference: ntc ? "NTC 2018 4.2.3.1 Tables 4.2.III-V" : "EN 1993-1-1:2005 Table 5.2");
        }
        private static bool PositiveFinite(double v) => v > 0 && !double.IsInfinity(v);
        private static CompressionPartClassification Failure(CompressionPartKind kind, SectionClassificationStatus status, string code, string message, bool ntc)
            => new CompressionPartClassification(kind, status, code, message,
                reference: ntc ? "NTC 2018 4.2.3.1 Tables 4.2.III-V" : "EN 1993-1-1:2005 Table 5.2");
    }
}
