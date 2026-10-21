using System;

namespace GPC.Checkers.Steel.Sections
{
    // Shared arithmetic only. The legacy facade keeps its original input behavior;
    // the scalar API validates dimensions and finite arithmetic separately.
    internal static class SectionClassificationArithmetic
    {
        internal static void Limits(CompressionPartKind kind, double epsilon, out double first, out double second, out double third)
        {
            switch (kind)
            {
                case CompressionPartKind.InternalFlat:
                    first = 33.0 * epsilon; second = 38.0 * epsilon; third = 42.0 * epsilon; return;
                case CompressionPartKind.OutstandFlat:
                    first = 9.0 * epsilon; second = 10.0 * epsilon; third = 14.0 * epsilon; return;
                case CompressionPartKind.CircularHollow:
                    // Preserve the multiplication order used by the EC3 facade.
                    first = 50.0 * epsilon * epsilon; second = 70.0 * epsilon * epsilon; third = 90.0 * epsilon * epsilon; return;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        internal static int Classify(CompressionPartKind kind, double widthOrDiameter, double thickness, double epsilon)
        {
            Limits(kind, epsilon, out var first, out var second, out var third);
            double ratio = widthOrDiameter / thickness;
            return ratio <= first ? 1 : ratio <= second ? 2 : ratio <= third ? 3 : 4;
        }
    }
}
