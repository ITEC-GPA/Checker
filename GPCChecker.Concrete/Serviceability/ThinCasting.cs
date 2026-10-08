using System;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Serviceability
{
    /// <summary>Standards to which <see cref="ThinCasting.Factor(Standard, ThinCastingRule)"/> applies the thin-casting reduction.</summary>
    public enum ThinCastingRule
    {
        /// <summary>
        /// Default: NTC 2018 only (exact class <see cref="StandardNTC2018Concrete"/>), the values ANTHEA used before step F2.7.
        /// </summary>
        Ntc2018 = 0,
        /// <summary>
        /// NTC 2018 and UNI EN 1992-1-1 with the Italian National Annex, DM 31/07/2012 7.2 (exact classes
        /// <see cref="StandardNTC2018Concrete"/> and <see cref="StandardUNIEN1992p11"/>).
        /// </summary>
        Ntc2018AndUniEn1992 = 1
    }

    /// <summary>
    /// Reduction for plane elements (slabs, walls…) cast in situ with ordinary concrete and thickness below 50 mm. NTC 2018
    /// §4.1.2.1.1.1 reduces the design compressive strength to 0.80 fcd and §4.1.2.2.5.1 the serviceability concrete stress
    /// limits by 20 %. The caller knows whether the element is a thin casting and applies the factor only then: to the concrete
    /// limit (argument concreteLimitFactor of <see cref="StressLimitCheck.Evaluate"/>) and to αcc / fcd. The steel limits do not
    /// change.
    /// </summary>
    public static class ThinCasting
    {
        /// <summary>Factor of a thin casting under the default rule <see cref="ThinCastingRule.Ntc2018"/>: 0.8 for NTC 2018, 1 otherwise.</summary>
        public static double Factor(Standard standard) => Factor(standard, ThinCastingRule.Ntc2018);

        /// <summary>
        /// Factor of a thin casting: 0.8 for the exact classes named by <paramref name="rule"/>, 1 for every other class, including
        /// classes derived from those (CNR-DT 200 derives from NTC 2018 and gets 1).
        /// </summary>
        public static double Factor(Standard standard, ThinCastingRule rule)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            if (!Enum.IsDefined(typeof(ThinCastingRule), rule)) throw new ArgumentOutOfRangeException(nameof(rule));
            var type = standard.GetType();
            if (type == typeof(StandardNTC2018Concrete)) return 0.8;
            if (rule == ThinCastingRule.Ntc2018AndUniEn1992 && type == typeof(StandardUNIEN1992p11)) return 0.8;
            return 1;
        }
    }
}
