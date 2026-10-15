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

/// <summary>Snapshot of Model's tabulated constitutive envelopes, for the concrete. Nonlinear, path independent;
/// unloading follows the envelope, without invented concrete damage/hysteresis. No automatic design factors.
/// The tensile branch ends at cracking: beyond its last strain the concrete is cracked, σ = 0 and tangent 0. Compression beyond the last strain
/// of the compressive branch (εcu) is outside the domain (<see cref="HistoryMaterialRangeException"/>).</summary>
public sealed class HistoryModelEnvelopeLaw : IHistoryMaterialLaw
{
    private readonly StressStrainTable compression, tension;
    private readonly double[] compressionStrains, compressionStresses, tensionStrains, tensionStresses;
    public double InitialModulus { get; }
    public bool IsLinear => false;
    public bool SupportsModulusFactors => false;
    /// <summary>True: no tensile resistance, σ = 0 and tangent 0 for any tension (the tensile branch of the material is ignored)</summary>
    public bool NeglectsTension { get; }
    public HistoryModelEnvelopeLaw(Material material) : this(material, false) { }
    /// <summary>The envelope of the material</summary>
    /// <param name="material">The Model material</param>
    /// <param name="neglectTension">True: the tensile strength is neglected (EN 1992-1-1 §6.1(2)P; the cracked section of EN 1994-2 §§5.4.2.3,
    /// 7.2.1). Continuous at ε = 0, so that the equilibrium iterations converge through the cracking of the slab; with the tensile branch the
    /// stress drops from fct to 0 at cracking, a discontinuity that the Newton iterations of a load increment cannot cross</param>
    public HistoryModelEnvelopeLaw(Material material, bool neglectTension)
    {
        InitialModulus = BridgeNumbers.Require(material.ElasticModulusCompression, "E", strict: true);
        compressionStrains = material.StressStrainTableCompression.Strains; compressionStresses = material.StressStrainTableCompression.Stresses;
        tensionStrains = material.StressStrainTableTension.Strains; tensionStresses = material.StressStrainTableTension.Stresses;
        compression = new(compressionStresses, compressionStrains); tension = new(tensionStresses, tensionStrains);
        NeglectsTension = neglectTension;
    }
    public HistoryMaterialState InitialState() => new(0, 0);
    public HistoryMaterialResponse Evaluate(double strain, HistoryMaterialState committed, double modulusFactor)
    {
        if (modulusFactor != 1) throw new NotSupportedException("Il diagramma Model non definisce l'evoluzione viscosa: usare phi=0 o una legge storica dedicata.");
        // Tension: no resistance when neglected, and beyond the end of the tensile branch (cracked concrete). Before, a strain beyond the
        // cracking strain was rejected as outside the domain, and the analyses stopped at the cracking of the slab.
        if (strain > 0 && (NeglectsTension || tensionStrains.Length == 0 || strain > tensionStrains.Last())) return new(0, 0, new(strain, 0));
        var e = strain > 0 ? tensionStrains : compressionStrains;
        var s = strain > 0 ? tensionStresses : compressionStresses;
        // Compression beyond the ultimate strain is rupture, not zero resistance.
        if (s.Any(v => v != 0) && Math.Abs(strain) > Math.Abs(e.Last()))
            throw new HistoryMaterialRangeException("Deformazione di compressione oltre il diagramma costitutivo Model (εcu).");
        double stress = (strain > 0 ? tension : compression).GetStress(strain), tangent = 0;
        for (int i = 1; i < e.Length; i++)
            if (Math.Abs(strain) <= Math.Abs(e[i])) { tangent = (s[i] - s[i - 1]) / (e[i] - e[i - 1]); break; }
        return new(stress, tangent, new(strain, stress));
    }
}

/// <summary>
/// Concrete with tension stiffening after cracking: in tension the law of Model <see cref="ConcreteTensionStiffening"/> (the single definition of
/// the library, <see cref="ConcreteMaterial.TensionResponseDiagrams.TensionStiffening"/>): σ = Ecm ε up to εcr = fctm / Ecm, then the average
/// tensile stress of cracked concrete of Collins and Mitchell (1991), modified compression field theory (Vecchio and Collins 1986),
/// σ = fctm / (1 + √(500 ε)), with the tangent dσ/dε = −250 fctm / (√(500 ε) (1 + √(500 ε))²). Compression on the Model envelope (beyond εcu
/// outside the domain). A function of the strain only: unloading follows the same curve, without damage or crack memory. No automatic design factors.
/// At εcr the stress drops from fctm to fctm / (1 + √(500 εcr)) (about 0,82 fctm for ordinary concretes), as in the original formulation.
/// Until 16/10/2026 the law was written here; the expressions of Model are the same, so the results are unchanged bit for bit.
/// </summary>
public sealed class HistoryTensionStiffeningConcreteLaw : IHistoryMaterialLaw
{
    private readonly HistoryModelEnvelopeLaw compression;
    /// <summary>The law in tension, from Model: modulus <see cref="InitialModulus"/> and strength <see cref="TensileStrength"/></summary>
    public ConcreteTensionStiffening Tension { get; }
    /// <summary>Ecm, the modulus of the uncracked branch</summary>
    public double InitialModulus => Tension.ElasticModulus;
    /// <summary>fctm, the stress at cracking</summary>
    public double TensileStrength => Tension.TensileStrength;
    /// <summary>εcr = fctm / Ecm</summary>
    public double CrackingStrain => Tension.CrackingStrain;
    public bool IsLinear => false;
    public bool SupportsModulusFactors => false;
    /// <param name="material">The Model material: Ecm (elastic modulus in compression) and the compressive envelope</param>
    /// <param name="tensileStrength">fctm</param>
    public HistoryTensionStiffeningConcreteLaw(Material material, double tensileStrength)
    {
        compression = new HistoryModelEnvelopeLaw(material, neglectTension: true);
        Tension = new ConcreteTensionStiffening(compression.InitialModulus, BridgeNumbers.Require(tensileStrength, "fctm", strict: true));
    }
    public HistoryMaterialState InitialState() => new(0, 0);
    public HistoryMaterialResponse Evaluate(double strain, HistoryMaterialState committed, double modulusFactor)
    {
        if (modulusFactor != 1) throw new NotSupportedException("Il legame con tension stiffening non definisce l'evoluzione viscosa: usare phi=0 o una legge storica dedicata.");
        if (strain <= 0) return compression.Evaluate(strain, committed, 1);
        Tension.Evaluate(strain, out double stress, out double tangent);
        return new(stress, tangent, new(strain, stress));
    }
}

/// <summary>A trial outside a constitutive domain may be retried with a shorter Newton step.</summary>
public sealed class HistoryMaterialRangeException : InvalidOperationException
{
    public HistoryMaterialRangeException(string message) : base(message) { }
}
