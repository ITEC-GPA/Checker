using GPC.Model.Materials;
namespace GPC.Design.Bridges;
public static partial class BridgeConcept
{
    // Captured concept conventions. Preserve arithmetic during the initial port; Model Ecm unification is a separate measured step.
    private const double SteelTonnesPerCubicMetre = 7.85;
    private static double ConcreteModulus(double fck) => 22000 * Math.Pow((fck + 8) / 10, .3);
}
