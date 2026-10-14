using System;

namespace GPC.Checkers.Concrete.Detailing
{
    // Shared coefficients of the existing ordinary-concrete detailing methods.
    // Profile dispatch and national deviations remain with MemberDetailingCalculator.
    internal static class LongitudinalReinforcementLimits
    {
        internal const double MaximumRatioOutsideLaps = .04;
        internal const double MinimumWallVerticalRatio = .002;
        internal static double MinimumBendingRatio(double fctm, double fyk) => Math.Max(.26 * fctm / fyk, .0013);
    }
}
