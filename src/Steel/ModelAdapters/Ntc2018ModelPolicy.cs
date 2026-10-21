using GPC.Checkers.Steel.Sections;
using GPC.Model.Standards;

namespace GPC.Checkers.Steel.ModelAdapters
{
    // Validate Model's mutable code context against explicit Checker policy.
    // Compare only parameters actually used by each supported method. A future
    // change to Model defaults cannot silently alter this qualified NTC profile.
    internal static class Ntc2018ModelPolicy
    {
        internal static bool SupportsSection(StandardEN1993p11 code)
            => code != null && code.GetType() == typeof(StandardNTC2018Steel)
                && code.GammaM0 == Ntc2018DesignPolicy.GammaM0;

        internal static bool SupportsAxial(StandardEN1993p11 code)
            => SupportsSection(code) && code.GammaM1 == Ntc2018DesignPolicy.GammaM1
                && code.GammaM2 == Ntc2018DesignPolicy.GammaM2
                && code.AlphaImperfectionFactorForCurveA0 == Ntc2018DesignPolicy.AlphaA0
                && code.AlphaImperfectionFactorForCurveA == Ntc2018DesignPolicy.AlphaA
                && code.AlphaImperfectionFactorForCurveC == Ntc2018DesignPolicy.AlphaC;
    }
}
