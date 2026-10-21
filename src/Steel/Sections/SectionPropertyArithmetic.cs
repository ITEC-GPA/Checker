using System;

namespace GPC.Checkers.Steel.Sections
{
    // Scalar derived properties in mm/mm2. Code applicability, geometry and
    // validation belong to the caller. Operation order preserves facade results.
    internal static class SectionPropertyArithmetic
    {
        internal static double FlangeArea(double bTop, double tTop, double bBottom, double tBottom)
            => bTop * tTop + bBottom * tBottom;
        internal static double WebArea(double thickness, double height) => thickness * height;
        internal static double RolledWebArea(double area, double bTop, double tTop,
            double bBottom, double tBottom, double tw, double rootAllowance)
            => area - bTop * tTop - bBottom * tBottom + (tw + rootAllowance) * (tTop + tBottom) / 2;
        internal static double RolledTeeWebArea(double area, double b, double tf, double tw, double r)
            => area - b * tf + (tw + 2 * r) * tf / 2;
        internal static double WeldedTeeWebArea(double height, double tf, double tw)
            => tw * (height - tf / 2);
        internal static double NtcTeeWebArea(double area, double b, double tf) => 0.9 * (area - b * tf);
        internal static double HollowAreaShare(double area, double dimension, double sumDimensions)
            => area * dimension / sumDimensions;
        internal static double CircularShearArea(double area) => 2 * area / Math.PI;
    }
}
