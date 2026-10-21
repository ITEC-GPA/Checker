namespace GPC.Checkers.Steel.Sections
{
    // Italy, D.M.17/01/2018, ordinary static structural steel. These are
    // prescriptions for the currently implemented NTC paths, not EC3 defaults.
    // Resistance factors: Table4.2.VII; imperfection factors: Table4.2.VIII.
    internal static class Ntc2018DesignPolicy
    {
        internal const double GammaM0 = 1.05;
        internal const double GammaM1 = 1.05;
        internal const double GammaM2 = 1.25;
        internal const double AlphaA0 = .13, AlphaA = .21, AlphaB = .34, AlphaC = .49, AlphaD = .76;
        internal static bool IsNominalGrade(int grade)
            => grade == 235 || grade == 275 || grade == 355 || grade == 420 || grade == 460;
        internal static bool IsImperfectionFactor(double alpha)
            => alpha == AlphaA0 || alpha == AlphaA || alpha == AlphaB || alpha == AlphaC || alpha == AlphaD;
        // Table4.2.V. Keep the direct epsilon-squared arithmetic already used by
        // the NTC circular-section paths; no stress-based class relaxation here.
        internal static int CircularClass(double diameter, double thickness, double fy)
        {
            double ratio = diameter / thickness, epsilonSquared = 235 / fy;
            return ratio <= 50 * epsilonSquared ? 1 : ratio <= 70 * epsilonSquared ? 2 : ratio <= 90 * epsilonSquared ? 3 : 4;
        }
    }
}
