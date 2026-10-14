using GPC.Model.Materials;
namespace GPC.Design.Bridges;
public static partial class BridgeConcept
{
    private const double SteelTonnesPerCubicMetre = Material.SteelDensityTonnesPerCubicMetre;
    private static double ConcreteModulus(double fck)
    {
        var material = new ConcreteMaterialEN1992("Concept concrete", fck, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        return EuropeanConcreteElasticity.SecantModulusMpa(material.Fcm);
    }
}
