using System;

namespace GPC.Checkers.Concrete.Shear
{
    /// <summary>Lever-arm choices already supported by the section adapter, in mm.
    /// The circular pier approximation is an explicit NTC modelling choice, not inferred from geometry.</summary>
    public static class ShearLeverArm
    {
        public const double RectangularRatio = .9;
        public static double NtcCircularPierRatio(bool hollow) => hollow ? .60 : .75;

        public static double Ratio(ShearProfile profile, double effectiveDepth, double clearCover, double linkDiameter,
            bool circular = false, bool hollow = false, bool ntcPier = false, double? assignedRatio = null)
        {
            if (!Enum.IsDefined(typeof(ShearProfile), profile)) throw new ArgumentException("Unknown shear profile.");
            if (!Finite(effectiveDepth) || effectiveDepth <= 0 || !Finite(clearCover) || clearCover < 0 || !Finite(linkDiameter) || linkDiameter < 0)
                throw new ArgumentException("Finite section depths, covers and link diameters required.");
            if (circular && ntcPier && profile != ShearProfile.Ntc2018) throw new ArgumentException("The circular pier approximation is specific to NTC 2018.");
            double ratio = circular ? ntcPier ? NtcCircularPierRatio(hollow) : assignedRatio ?? double.NaN : RectangularRatio;
            if (!Finite(ratio) || ratio <= 0 || ratio > RectangularRatio) throw new ArgumentException("Assign a lever ratio between zero (excluded) and 0.9.");
            if (profile == ShearProfile.DinEN1992p11)
            {
                double cv = clearCover + linkDiameter;
                ratio = Math.Min(ratio, Math.Max(effectiveDepth - cv - 30, effectiveDepth - 2 * cv) / effectiveDepth);
            }
            if (ratio <= 0) throw new ArgumentException("Cover leaves no positive shear lever arm.");
            return ratio;
        }

        /// <summary>Prerequisite of the supported Danish shear model: ultimate strain in per mille, fy/fu in the same stress unit.</summary>
        public static bool DanishSteelDuctility(double yieldStrength, double ultimateStrength, double ultimateStrainPerMille)
            => Finite(yieldStrength) && yieldStrength > 0 && Finite(ultimateStrength) && Finite(ultimateStrainPerMille)
                && ultimateStrainPerMille >= 50 && ultimateStrength >= 1.08 * yieldStrength;

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
