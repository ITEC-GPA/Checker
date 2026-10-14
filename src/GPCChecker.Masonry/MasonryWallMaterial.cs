using System;

namespace GPC.Checkers.Masonry
{
    /// <summary>Equivalent port of the existing gravity-wall masonry policy. MPa throughout.
    /// This limited compressed, uncracked-wall model is not a general masonry verifier.</summary>
    public sealed class MasonryWallMaterial
    {
        public double Compression { get; }
        public double EffectiveModulus { get; }
        public double Gamma { get; }
        public double CharacteristicShear { get; }
        public double CharacteristicShearCap { get; }
        public double FoundationShear => CharacteristicShear / Gamma;
        public MasonryWallMaterial(double fk, double gammaM, double confidence, double modulus, double creep, double fvk0, double fvkLimit)
        {
            if (!Positive(fk) || !Positive(gammaM) || !Positive(confidence) || !Positive(modulus) || !Positive(fvkLimit)
                || !Finite(creep) || creep < 0 || !Finite(fvk0) || fvk0 < 0)
                throw new ArgumentOutOfRangeException(nameof(fk), "Finite positive material data, nonnegative creep and initial shear required.");
            Gamma = gammaM * confidence; Compression = .85 * fk / Gamma;
            EffectiveModulus = modulus / (1 + creep); CharacteristicShear = fvk0; CharacteristicShearCap = fvkLimit;
        }
        public double ShearStress(double compressionStress)
        {
            if (!Finite(compressionStress) || compressionStress < 0) throw new ArgumentOutOfRangeException(nameof(compressionStress));
            return Math.Min(CharacteristicShear + .4 * compressionStress, CharacteristicShearCap) / Gamma;
        }
        static bool Positive(double x) => Finite(x) && x > 0;
        static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
