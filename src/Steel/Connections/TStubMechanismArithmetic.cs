using System;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Arithmetic only. The calling model establishes plastic moments,
    /// bolt resistance, effective geometry, prying applicability and design policy.
    /// No material strength/factor or accidental/ordinary case is inferred here.</summary>
    internal static class TStubMechanismArithmetic
    {
        internal static TStubMechanismResistances Calculate(double plasticMoment1, double plasticMoment2,
            double m, double n, double contactEw, double totalBoltResistance,
            bool prying, TStubMode1Method method)
        {
            double plate = !prying ? 2 * plasticMoment1 / m
                : method == TStubMode1Method.PointForce ? 4 * plasticMoment1 / m
                : (8 * n - 2 * contactEw) * plasticMoment1 / (2 * m * n - contactEw * (m + n));
            double? combined = prying ? (double?)((2 * plasticMoment2 + n * totalBoltResistance) / (m + n)) : null;
            return new TStubMechanismResistances(plate, combined, totalBoltResistance);
        }
    }

    internal sealed class TStubMechanismResistances
    {
        internal double PlateMode1 { get; }
        internal double? CombinedMode2 { get; }
        internal double BoltsMode3 { get; }
        internal bool IsFinitePositive => Positive(PlateMode1) && Positive(BoltsMode3)
            && (!CombinedMode2.HasValue || Positive(CombinedMode2.Value));
        internal TStubMechanismResistances(double plateMode1, double? combinedMode2, double boltsMode3)
        { PlateMode1 = plateMode1; CombinedMode2 = combinedMode2; BoltsMode3 = boltsMode3; }
        private static bool Positive(double value) => value > 0 && !double.IsInfinity(value);
    }
}
