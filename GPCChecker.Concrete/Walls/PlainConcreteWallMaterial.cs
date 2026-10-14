using System;
using GPC.Checkers.Concrete.Serviceability;

namespace GPC.Checkers.Concrete.Walls
{
    /// <summary>Existing NTC plain-concrete wall policy. Strengths/modulus in MPa, creep dimensionless.
    /// Compression is supplied by the applicable material/standard service; no second fcd calculation.</summary>
    public sealed class PlainConcreteWallMaterial
    {
        public double Compression { get; }
        public double Tension { get; }
        public double EffectiveModulus { get; }
        public PlainConcreteWallMaterial(double compression, double characteristicTension, double gammaC, double modulus, double creep)
        {
            if (!Positive(compression) || !Positive(characteristicTension) || !Positive(gammaC) || !Positive(modulus)
                || !WallStrip.Finite(creep) || creep < 0) throw new ArgumentOutOfRangeException(nameof(compression), "Finite positive material data and nonnegative creep required.");
            Compression = compression; Tension = .85 * characteristicTension / gammaC;
            EffectiveModulus = modulus / Homogenization.CreepFactor(creep);
        }
        /// <summary>Shear stress limit on a compressed section, NTC 4.1.11 policy inherited from the wall engine.</summary>
        public double ShearStress(double compressionStress)
        {
            if (!WallStrip.Finite(compressionStress) || compressionStress < 0) throw new ArgumentOutOfRangeException(nameof(compressionStress));
            double limit = Compression - 2 * Math.Sqrt(Tension * Tension + Compression * Tension);
            double delta = Math.Max(0, compressionStress - limit);
            return Math.Sqrt(Math.Max(0, Tension * Tension + compressionStress * Tension - delta * delta / 4));
        }
        static bool Positive(double x) => WallStrip.Finite(x) && x > 0;
    }
}
