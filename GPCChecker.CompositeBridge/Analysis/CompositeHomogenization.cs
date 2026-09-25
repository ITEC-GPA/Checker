using GPC.Model.Sections.Concrete;
namespace GPC.Checkers.CompositeBridge;

/// <summary>Homogenization shared by any section topology; phiEffective = psiL * phi.</summary>
public static class CompositeHomogenization
{
    public static (double N0, double N, double PhiEffective, double Phi) Calculate(BridgeMaterialSet materials, BridgePhase phase)
    {
        double n0 = materials.Steel.ElasticModulusTension / materials.Concrete.ElasticModulusCompression;
        if (phase.HomogenizationSource != BridgeHomogenizationSource.Phi && phase.HomogenizationSource != BridgeHomogenizationSource.ModularRatio) throw new ArgumentException("Modo di omogeneizzazione sconosciuto.");
        double psi = BridgeNumbers.Require(phase.PsiL, "psi", strict: true);
        if (phase.HomogenizationName == "Da n")
        {
            double n = BridgeNumbers.Require(phase.N, "n", strict: true);
            double effective = ReinforcedConcreteSection.CalculateHomogenizedFactorPhi(n, materials.Steel, materials.Concrete);
            if (effective < -1e-9) throw new ArgumentException("n deve essere almeno n₀ = Ea/Ecm per una viscosità non negativa.");
            return (n0, n, Math.Max(0, effective), Math.Max(0, effective) / psi);
        }
        double phi = BridgeNumbers.Require(phase.Phi, "phi"); return (n0, n0 * (1 + psi * phi), psi * phi, phi);
    }
}
