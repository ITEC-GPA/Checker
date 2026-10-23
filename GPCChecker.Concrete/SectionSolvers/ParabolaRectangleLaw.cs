using System;
using GPC.Model.Materials;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    /// <summary>Analytic n=2 law for an unmodified, standard European parabola-rectangle table.</summary>
    internal readonly struct ParabolaRectangleLaw
    {
        internal readonly double Ultimate, Peak, Stress;
        internal ParabolaRectangleLaw(double ultimate, double peak, double stress)
        { Ultimate = ultimate; Peak = peak; Stress = stress; }

        internal double At(double strain)
        {
            if (strain >= 0 || strain < Ultimate) return 0;
            if (strain <= Peak) return Stress;
            double r = strain / Peak;
            return Stress * r * (2 - r);
        }

        internal static bool TryCreate(ConcreteMaterial material, Standard standard, out ParabolaRectangleLaw law)
        {
            law = default;
            if (!(material is ConcreteMaterialEuropeanCommon european) || !(standard is StandardModelCode2010) ||
                material.CompressionStressStrainDiagram != ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle ||
                !(european.Fck < 0 && european.Fck >= -50)) return false;
            // Public table setters (including in-place SetStress/SetStrain) may replace the generated law.
            // Check its actual contents; never silently reinterpret a custom or high-strength diagram.
            var e = material.StressStrainTableCompression.Strains;
            var s = material.StressStrainTableCompression.Stresses;
            double peak = material.StrainYCompression, ultimate = material.StrainUCompression;
            if (e.Length != 10 || s.Length != 10 || !(ultimate < peak && peak < 0) ||
                double.IsInfinity(ultimate) || e[0] != 0 || s[0] != 0 || e[9] != ultimate || s[9] != european.Fck) return false;
            for (int i = 1; i <= 8; i++)
            {
                double r = i / 8.0;
                if (Math.Abs(e[i] - peak * r) > 1e-13 * Math.Abs(peak) ||
                    Math.Abs(s[i] - european.Fck * r * (2 - r)) > 1e-13 * Math.Abs(european.Fck) ||
                    double.IsNaN(e[i]) || double.IsNaN(s[i])) return false;
            }
            double stress = material.CalculateDesignStressFromCharacteristic(standard, european.Fck);
            if (!(stress < 0) || double.IsInfinity(stress)) return false;
            law = new ParabolaRectangleLaw(ultimate, peak, stress);
            return true;
        }
    }
}
