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
            if (!(standard is StandardModelCode2010) || !ConcreteParabolaRectangleLaw.TryCreate(material, out var characteristic)) return false;
            double stress = material.CalculateDesignStressFromCharacteristic(standard, characteristic.Stress);
            if (!(stress < 0) || double.IsInfinity(stress)) return false;
            law = new ParabolaRectangleLaw(characteristic.Ultimate, characteristic.Peak, stress);
            return true;
        }
    }
}
