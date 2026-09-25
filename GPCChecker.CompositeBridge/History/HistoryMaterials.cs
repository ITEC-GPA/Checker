using GPC.Model.Materials;

namespace GPC.Checkers.CompositeBridge.History;

/// <summary>Immutable committed state. Custom laws may derive a record with additional internal variables.</summary>
public record HistoryMaterialState(double MechanicalStrain, double Stress);
public sealed record HistoryPlasticState(double MechanicalStrain, double Stress, double PlasticStrain, double AccumulatedPlasticStrain)
    : HistoryMaterialState(MechanicalStrain, Stress);
public sealed record HistoryMaterialResponse(double Stress, double Tangent, HistoryMaterialState State);

/// <summary>Pure trial evaluation: never mutate the committed state. The solver owns commit/rollback.</summary>
public interface IHistoryMaterialLaw
{
    double InitialModulus { get; }
    bool IsLinear { get; }
    bool SupportsModulusFactors { get; }
    HistoryMaterialState InitialState();
    HistoryMaterialResponse Evaluate(double mechanicalStrain, HistoryMaterialState committed, double modulusFactor);
}

/// <summary>Incremental elasticity: sigma = sigma_committed + E_phase*(epsilon - epsilon_committed).
/// Changing E affects new increments only; this is not a time-dependent creep kernel.</summary>
public sealed class HistoryElasticLaw : IHistoryMaterialLaw
{
    public double InitialModulus { get; }
    public bool IsLinear => true;
    public bool SupportsModulusFactors => true;
    public HistoryElasticLaw(double modulus) => InitialModulus = BridgeNumbers.Require(modulus, nameof(modulus), strict: true);
    public HistoryMaterialState InitialState() => new(0, 0);
    public HistoryMaterialResponse Evaluate(double strain, HistoryMaterialState committed, double modulusFactor)
    {
        double e = InitialModulus * BridgeNumbers.Require(modulusFactor, nameof(modulusFactor), strict: true);
        double stress = committed.Stress + e * (strain - committed.MechanicalStrain);
        return new(stress, e, new(strain, stress));
    }
}

/// <summary>Uniaxial return mapping with isotropic hardening; elastic unloading preserves plastic strain.</summary>
public sealed class HistoryBilinearSteelLaw : IHistoryMaterialLaw
{
    public double InitialModulus { get; }
    public double YieldStress { get; }
    public double HardeningModulus { get; }
    public double UltimateStrain { get; }
    public bool IsLinear => false;
    public bool SupportsModulusFactors => false;
    public HistoryBilinearSteelLaw(double modulus, double yieldStress, double postYieldTangent = 0, double ultimateStrain = double.PositiveInfinity)
    {
        InitialModulus = BridgeNumbers.Require(modulus, nameof(modulus), strict: true);
        YieldStress = BridgeNumbers.Require(yieldStress, nameof(yieldStress), strict: true);
        BridgeNumbers.Require(postYieldTangent, nameof(postYieldTangent));
        if (postYieldTangent >= modulus || double.IsNaN(ultimateStrain) || ultimateStrain <= yieldStress / modulus) throw new ArgumentOutOfRangeException(nameof(postYieldTangent));
        HardeningModulus = modulus * postYieldTangent / (modulus - postYieldTangent);
        UltimateStrain = ultimateStrain;
    }
    public static HistoryBilinearSteelLaw FromModel(SteelMaterial material)
    {
        double ey = material.Fyk / material.ElasticModulusTension;
        double tangent = material.StressStrainCurve == SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic ? 0 :
            (material.Fu - material.Fyk) / (material.StrainUTension - ey);
        return new(material.ElasticModulusTension, material.Fyk, tangent, material.StrainUTension);
    }
    public HistoryMaterialState InitialState() => new HistoryPlasticState(0, 0, 0, 0);
    public HistoryMaterialResponse Evaluate(double strain, HistoryMaterialState committed, double modulusFactor)
    {
        if (modulusFactor != 1) throw new NotSupportedException("La legge plastica richiede un modello viscoso esplicito per modificare E.");
        if (Math.Abs(strain) > UltimateStrain) throw new HistoryMaterialRangeException("Deformazione ultima dell'acciaio superata.");
        var old = (HistoryPlasticState)committed;
        double trial = InitialModulus * (strain - old.PlasticStrain);
        double excess = Math.Abs(trial) - (YieldStress + HardeningModulus * old.AccumulatedPlasticStrain);
        if (excess <= 0) return new(trial, InitialModulus, old with { MechanicalStrain = strain, Stress = trial });
        double dp = excess / (InitialModulus + HardeningModulus), sign = Math.Sign(trial);
        double stress = trial - InitialModulus * dp * sign;
        return new(stress, InitialModulus * HardeningModulus / (InitialModulus + HardeningModulus),
            new HistoryPlasticState(strain, stress, old.PlasticStrain + sign * dp, old.AccumulatedPlasticStrain + dp));
    }
}

/// <summary>Snapshot of Model's tabulated constitutive envelopes. Nonlinear, path independent;
/// unloading follows the envelope, without invented concrete damage/hysteresis. No automatic design factors.</summary>
public sealed class HistoryModelEnvelopeLaw : IHistoryMaterialLaw
{
    private readonly StressStrainTable compression, tension;
    private readonly double[] compressionStrains, compressionStresses, tensionStrains, tensionStresses;
    public double InitialModulus { get; }
    public bool IsLinear => false;
    public bool SupportsModulusFactors => false;
    public HistoryModelEnvelopeLaw(Material material)
    {
        InitialModulus = BridgeNumbers.Require(material.ElasticModulusCompression, "E", strict: true);
        compressionStrains = material.StressStrainTableCompression.Strains; compressionStresses = material.StressStrainTableCompression.Stresses;
        tensionStrains = material.StressStrainTableTension.Strains; tensionStresses = material.StressStrainTableTension.Stresses;
        compression = new(compressionStresses, compressionStrains); tension = new(tensionStresses, tensionStrains);
    }
    public HistoryMaterialState InitialState() => new(0, 0);
    public HistoryMaterialResponse Evaluate(double strain, HistoryMaterialState committed, double modulusFactor)
    {
        if (modulusFactor != 1) throw new NotSupportedException("Il diagramma Model non definisce l'evoluzione viscosa: usare phi=0 o una legge storica dedicata.");
        var e = strain > 0 ? tensionStrains : compressionStrains;
        var s = strain > 0 ? tensionStresses : compressionStresses;
        // An empty/zero tensile branch represents no tensile resistance, not material rupture.
        if (s.Any(v => v != 0) && Math.Abs(strain) > Math.Abs(e.Last()))
            throw new HistoryMaterialRangeException("Deformazione oltre il diagramma costitutivo Model.");
        double stress = (strain > 0 ? tension : compression).GetStress(strain), tangent = 0;
        for (int i = 1; i < e.Length; i++)
            if (Math.Abs(strain) <= Math.Abs(e[i])) { tangent = (s[i] - s[i - 1]) / (e[i] - e[i - 1]); break; }
        return new(stress, tangent, new(strain, stress));
    }
}

/// <summary>A trial outside a constitutive domain may be retried with a shorter Newton step.</summary>
public sealed class HistoryMaterialRangeException : InvalidOperationException
{
    public HistoryMaterialRangeException(string message) : base(message) { }
}
