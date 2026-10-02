using GPC.Model.Standards;

namespace GPC.Checkers.Geotechnics.Piles
{
    /// <summary>Behaviour of a layer in the pile methods: granular (drained, φ') or cohesive (undrained cu; drained c', φ' for the shaft).</summary>
    public enum SoilBehaviour { Granular, Cohesive }

    /// <summary>
    /// Correlation factors ξ3 (mean) and ξ4 (minimum) of the investigated profiles and the resistance factor γR of the transverse capacity.
    /// </summary>
    public sealed class LateralPileFactors
    {
        public double Xi3 { get; }
        public double Xi4 { get; }
        public double ResistanceFactor { get; }
        public LateralPileFactors(double xi3, double xi4, double resistanceFactor)
        {
            if (!Positive(xi3) || !Positive(xi4) || !Positive(resistanceFactor)) throw new ArgumentOutOfRangeException(nameof(xi3), "Positive finite factors are required.");
            Xi3 = xi3; Xi4 = xi4; ResistanceFactor = resistanceFactor;
        }

        /// <summary>
        /// ξ3, ξ4 of the Model standard for the number of investigated profiles and γR of <see cref="GeotechnicalCheck.PileTransverse"/> (NTC 2018
        /// Tab. 6.4.IV and 6.4.VI: γR = 1.3); a standard without the transverse check gives NotSupportedException.
        /// </summary>
        public static LateralPileFactors FromStandard(StandardGeotechnical standard, int investigatedProfiles)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            var xi = standard.PileCorrelationFactors(investigatedProfiles) ?? throw new NotSupportedException(standard.Name + ": no correlation factors of the piles.");
            var combination = GeotechnicalActions.Required(standard, GeotechnicalCheck.PileTransverse, GeotechnicalSituation.PersistentTransient);
            if (combination.Count != 1) throw new NotSupportedException(standard.Name + ": more than one combination for the transverse capacity; give the factors explicitly.");
            return new LateralPileFactors(xi.Item1, xi.Item2, combination[0].ResistanceFactor);
        }

        internal static bool Positive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;
    }

    /// <summary>Partial and correlation factors of the axial capacity: γs (shaft in compression), γst (shaft in tension), γb (base), γG of the pile weight, ξ3, ξ4.</summary>
    public sealed class PileResistanceFactors
    {
        public double ShaftCompression { get; }
        public double ShaftTension { get; }
        public double Base { get; }
        public double WeightUnfavourable { get; }
        public double WeightFavourable { get; }
        public double Xi3 { get; }
        public double Xi4 { get; }
        public PileResistanceFactors(double shaftCompression, double shaftTension, double baseFactor, double weightUnfavourable, double weightFavourable, double xi3, double xi4)
        {
            if (!new[] { shaftCompression, shaftTension, baseFactor, weightUnfavourable, xi3, xi4 }.All(LateralPileFactors.Positive)
                || double.IsNaN(weightFavourable) || double.IsInfinity(weightFavourable) || weightFavourable < 0)
                throw new ArgumentOutOfRangeException(nameof(shaftCompression), "Positive finite factors are required.");
            ShaftCompression = shaftCompression; ShaftTension = shaftTension; Base = baseFactor; WeightUnfavourable = weightUnfavourable; WeightFavourable = weightFavourable;
            Xi3 = xi3; Xi4 = xi4;
        }

        /// <summary>
        /// The factors of a Model standard: combination <paramref name="combination"/> of the base, shaft and shaft-in-tension checks (NTC 2018: one
        /// combination A1+M1+R3, bored piles γb 1.35, γs 1.15, γst 1.25), γG of its action set, ξ3 and ξ4 of the investigated profiles.
        /// </summary>
        public static PileResistanceFactors FromStandard(StandardGeotechnical standard, int investigatedProfiles, int combination = 0)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            var xi = standard.PileCorrelationFactors(investigatedProfiles) ?? throw new NotSupportedException(standard.Name + ": no correlation factors of the piles.");
            GeotechnicalCombination C(GeotechnicalCheck check)
            {
                var all = GeotechnicalActions.Required(standard, check, GeotechnicalSituation.PersistentTransient);
                if (combination < 0 || combination >= all.Count) throw new ArgumentOutOfRangeException(nameof(combination));
                return all[combination];
            }
            var b = C(GeotechnicalCheck.PileBase); var s = C(GeotechnicalCheck.PileShaftCompression); var t = C(GeotechnicalCheck.PileShaftTension);
            return new PileResistanceFactors(s.ResistanceFactor, t.ResistanceFactor, b.ResistanceFactor, b.Actions.PermanentUnfavourable, b.Actions.PermanentFavourable, xi.Item1, xi.Item2);
        }
    }
}
