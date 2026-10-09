using GPC.Model.Geotechnics;
using GPC.Model.Standards;

namespace GPC.Checkers.Geotechnics
{
    /// <summary>
    /// Design parameters of a soil for a material factor set (M1, M2 …) of a Model geotechnical standard.
    /// tan φ'd = tan φ'k / γφ', c'd = c'k / γc', cu,d = cu,k / γcu, γd = γk / γγ. Units of Model: rad, MPa, N/mm³.
    /// </summary>
    public sealed class DesignSoil
    {
        public Soil Characteristic { get; }
        public GeotechnicalMaterialFactors Factors { get; }
        public DesignSoil(Soil characteristic, GeotechnicalMaterialFactors factors)
        {
            Characteristic = characteristic ?? throw new ArgumentNullException(nameof(characteristic));
            Factors = factors ?? throw new ArgumentNullException(nameof(factors));
        }
        public double FrictionAngle => Math.Atan(Math.Tan(Characteristic.FrictionAngle) / Factors.TanFrictionAngle);
        public double EffectiveCohesion => Characteristic.EffectiveCohesion / Factors.EffectiveCohesion;
        /// <summary>Null when the soil has no undrained strength: undrained methods must report missing data.</summary>
        public double? UndrainedShearStrength => Characteristic.UndrainedShearStrength / Factors.UndrainedShearStrength;
        public double UnitWeight => Characteristic.UnitWeight / Factors.UnitWeight;
        public double SaturatedUnitWeight => Characteristic.SaturatedUnitWeight / Factors.UnitWeight;
    }

    /// <summary>Category of an action for the geotechnical partial factors.</summary>
    public enum GeotechnicalActionCategory { Permanent, NonStructuralPermanent, Variable }

    public static class GeotechnicalActions
    {
        /// <summary>Partial factor of an action of the given category, unfavourable or favourable, in an action set (A1, A2, EQU …).</summary>
        public static double Factor(GeotechnicalActionFactors set, GeotechnicalActionCategory category, bool unfavourable)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            return category switch
            {
                GeotechnicalActionCategory.Permanent => unfavourable ? set.PermanentUnfavourable : set.PermanentFavourable,
                GeotechnicalActionCategory.NonStructuralPermanent => unfavourable ? set.NonStructuralUnfavourable : set.NonStructuralFavourable,
                GeotechnicalActionCategory.Variable => unfavourable ? set.VariableUnfavourable : set.VariableFavourable,
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };
        }

        /// <summary>
        /// The combinations of a check, with an explicit error when the standard does not define it: a geotechnical method never runs
        /// with the factors of another check or standard.
        /// </summary>
        public static IReadOnlyList<GeotechnicalCombination> Required(StandardGeotechnical standard, GeotechnicalCheck check, GeotechnicalSituation situation)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            var combinations = standard.Combinations(check, situation);
            if (combinations.Count == 0) throw new NotSupportedException($"{standard.Name} {standard.Edition}: no combination for {check} in the {situation} situation.");
            return combinations;
        }
    }
}

namespace System.Runtime.CompilerServices
{
    // netstandard2.0 polyfill for records and init accessors.
    internal static class IsExternalInit { }
}
