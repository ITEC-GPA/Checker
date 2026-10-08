using System;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Serviceability
{
    /// <summary>
    /// Standards to which <see cref="ThinCasting.Factor(Standard, ThinCastingRule)"/> applies the thin-casting factor 0.8. The
    /// default value (0) keeps the values ANTHEA used before step F2.7.
    /// </summary>
    public enum ThinCastingRule
    {
        /// <summary>
        /// Default: NTC 2018 only (exact class <see cref="StandardNTC2018Concrete"/>), the values ANTHEA used before step F2.7
        /// (NTC 2018 §4.1.2.1.1.1 for fcd, §4.1.2.2.5.1 for the serviceability concrete stress limits).
        /// </summary>
        AntheaBeforeF27 = 0,
        /// <summary>
        /// NTC 2018 and UNI EN 1992-1-1 with the Italian National Annex, DM 31/07/2012 7.2 (exact classes
        /// <see cref="StandardNTC2018Concrete"/> and <see cref="StandardUNIEN1992p11"/>). The method page ca.sle-tensioni
        /// (Checker docs/metodi/ca.sle-tensioni.md, lines 143-144 and the fs row of the coefficient table, line 219) gives
        /// fs = 0.8 for plane elements cast in situ thinner than 50 mm under both NTC 2018 §4.1.2.2.5.1 and DM 31/07/2012 7.2.
        /// </summary>
        Ntc2018AndItalianAnnex = 1
    }

    /// <summary>
    /// Reduction for plane elements (slabs, walls…) cast in situ with ordinary concrete and thickness below 50 mm. NTC 2018
    /// §4.1.2.1.1.1 reduces the design compressive strength to 0.80 fcd and §4.1.2.2.5.1 the serviceability concrete stress
    /// limits by 20 %; the method page ca.sle-tensioni (Checker docs/metodi/ca.sle-tensioni.md:143-144, :219) gives the same
    /// fs = 0.8 for DM 31/07/2012 7.2 (UNI EN 1992-1-1 with the Italian National Annex), applied only with
    /// <see cref="ThinCastingRule.Ntc2018AndItalianAnnex"/>. The caller knows whether the element is a thin casting and applies
    /// the factor only then: to the concrete limit (argument concreteLimitFactor of <see cref="StressLimitCheck.Evaluate"/>) and
    /// to αcc / fcd. The steel limits do not change.
    /// </summary>
    /// <remarks>
    /// The standard is recognised by its exact class. ANTHEA creates «UNI EN 1992-1-1» as <see cref="StandardUNIEN1992p11"/>
    /// (ConcreteStandards.Create), and that class is by definition UNI EN 1992-1-1:2005 with the Italian National Annex
    /// (DM 31 July 2012, remarks of the class in Model): it has no member that selects or names an annex, so the exact type is
    /// enough and no check on the annex is needed. Custom coefficients change the properties of the instance, not its class,
    /// so they keep the factor. A class derived from a listed one gets 1, as CNR-DT 200 (derived from NTC 2018) does today.
    /// </remarks>
    public static class ThinCasting
    {
        /// <summary>
        /// Factor of a thin casting under the default rule <see cref="ThinCastingRule.AntheaBeforeF27"/>: 0.8 for NTC 2018,
        /// 1 otherwise. Same as <see cref="Factor(Standard, ThinCastingRule)"/> with <see cref="ThinCastingRule.AntheaBeforeF27"/>.
        /// </summary>
        public static double Factor(Standard standard) => Factor(standard, ThinCastingRule.AntheaBeforeF27);

        /// <summary>
        /// Factor of a thin casting: 0.8 for the exact classes named by <paramref name="rule"/>, 1 for every other class, including
        /// classes derived from those (CNR-DT 200 derives from NTC 2018 and gets 1).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="standard"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="rule"/> is not a defined value.</exception>
        public static double Factor(Standard standard, ThinCastingRule rule)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            if (!Enum.IsDefined(typeof(ThinCastingRule), rule)) throw new ArgumentOutOfRangeException(nameof(rule));
            var type = standard.GetType();
            if (type == typeof(StandardNTC2018Concrete)) return 0.8;
            if (rule == ThinCastingRule.Ntc2018AndItalianAnnex && type == typeof(StandardUNIEN1992p11)) return 0.8;
            return 1;
        }
    }
}
