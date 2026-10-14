using System;
namespace GPC.Checkers.Concrete.Detailing
{
    /// <summary>Shared elementary spacing and reinforcement rules used by section and wall-detail checks.
    /// mm and mm²; callers select the applicable member/region and validate inputs.</summary>
    public static class PlateDetailingRules
    {
        public const double WallHorizontalMainRatio = .25;
        public const double WallHorizontalConcreteRatio = .001;
        public const double WallHorizontalMaximumSpacing = 400;
        public static double MinimumClearSpacing(double diameter, double aggregate) => Math.Max(20, Math.Max(diameter, aggregate + 5));
        public static double WallMainSpacing(double thickness) => Math.Min(3 * thickness, 400);
        public static double SlabMainSpacing(double thickness, bool critical) => Math.Min((critical ? 2 : 3) * thickness, critical ? 250 : 400);
        public static double SlabSecondarySpacing(double thickness, bool critical) => Math.Min((critical ? 3 : 3.5) * thickness, critical ? 400 : 450);
        public static double SlabSecondaryArea(double primaryArea) => .2 * primaryArea;
    }
}
