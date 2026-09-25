namespace GPC.Checkers.CompositeBridge.History;

/// <summary>Physical strain: epsilon(y) = AxialStrain - Curvature*y. Dimensionless and 1/mm.</summary>
public readonly record struct HistoryStrainPlane(double AxialStrain, double Curvature)
{
    public double At(double y) => AxialStrain - Curvature * y;
    public static HistoryStrainPlane operator +(HistoryStrainPlane a, HistoryStrainPlane b) => new(a.AxialStrain + b.AxialStrain, a.Curvature + b.Curvature);
    public static HistoryStrainPlane operator -(HistoryStrainPlane a, HistoryStrainPlane b) => new(a.AxialStrain - b.AxialStrain, a.Curvature - b.Curvature);
    public HistoryStrainPlane Scale(double factor) => new(AxialStrain * factor, Curvature * factor);
}

public enum HistoryLoadReference { FixedElevation, GrossElasticCentroid, EffectiveElasticCentroid }

/// <summary>One fixed integration point; positive area in mm². Optional strip bounds support effective-width integration.</summary>
public sealed record HistoryFiber(string Id, double X, double Y, double Area, double StripBottom = double.NaN, double StripTop = double.NaN);
public sealed record HistoryComponent(string Id, IHistoryMaterialLaw Material, IReadOnlyList<HistoryFiber> Fibers, bool InitiallyActive = false);
public sealed record HistorySection(IReadOnlyList<HistoryComponent> Components, IHistoryEffectiveAreaModel? EffectiveAreaModel = null);

/// <summary>Incremental actions, in N and Nmm. Positive N is tension; positive M compresses upper fibres.</summary>
public sealed record HistoryPhase
{
    public string Name { get; init; } = "Phase";
    public double DeltaN { get; init; }
    public double DeltaM { get; init; }
    public double DeltaV { get; init; }
    public double ApplicationY { get; init; }
    public HistoryLoadReference LoadReference { get; init; }
    public IReadOnlyList<string> Activate { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Deactivate { get; init; } = Array.Empty<string>();
    /// <summary>Incremental eigenstrain by component, e.g. uniform shrinkage (-200e-6, 0). Never a cumulative value.</summary>
    public IReadOnlyDictionary<string, HistoryStrainPlane> ImposedStrainIncrements { get; init; } = new Dictionary<string, HistoryStrainPlane>();
    /// <summary>Elastic modulus multipliers for this and following phases until replaced. They do not retroactively rescale committed stresses.</summary>
    public IReadOnlyDictionary<string, double> ModulusFactors { get; init; } = new Dictionary<string, double>();
    public int Substeps { get; init; } = 1;
}

public sealed record HistorySolverOptions
{
    public int MaximumNewtonIterations { get; init; } = 80;
    public int MaximumEffectiveIterations { get; init; } = 160;
    public int MaximumLineSearchIterations { get; init; } = 24;
    public double RelativeTolerance { get; init; } = 1e-9;
    public double ForceTolerance { get; init; } = 1e-4;
    public double MomentTolerance { get; init; } = .01;
    public double EffectiveTolerance { get; init; } = 1e-7;
    public double EffectiveRelaxation { get; init; } = .55;
}

public sealed record HistoryFiberResult(string ComponentId, HistoryFiber Fiber, bool Active, double EffectiveArea,
    double ActivationStrain, double TotalStrain, double ImposedStrain, double MechanicalStrain,
    double MechanicalStrainIncrement, double Stress, double StressIncrement, HistoryMaterialState MaterialState, double ModulusFactor);

public sealed record HistoryPanelResult(string Name, BridgePlate Reduction);
public sealed record HistoryStageResult(int Index, string Name, HistoryStrainPlane TotalPlane, HistoryStrainPlane IncrementPlane,
    double N, double MomentAtOrigin, double V, double IncrementApplicationY,
    double IntegratedN, double IntegratedMomentAtOrigin, double ForceResidual, double MomentResidual,
    int NewtonIterations, int EffectiveIterations, double EffectiveResidual,
    IReadOnlyList<HistoryFiberResult> Fibers, IReadOnlyList<HistoryPanelResult> Panels, HistoryPhase AppliedPhase);
public sealed record HistoryAnalysisResult(IReadOnlyList<HistoryStageResult> Stages);

/// <summary>Stable fiber identities/order across all trials. Factors in [0,1]; state is committed only after equilibrium and area convergence.</summary>
public interface IHistoryEffectiveAreaModel
{
    HistoryEffectiveAreaResponse Evaluate(IReadOnlyList<HistoryFiberResult> trial);
}
public sealed record HistoryEffectiveAreaResponse(IReadOnlyList<double> Factors, IReadOnlyList<HistoryPanelResult> Panels);

public sealed class HistoryConvergenceException : InvalidOperationException
{
    public int PhaseIndex { get; }
    public string PhaseName { get; }
    public int Substep { get; }
    public HistoryStageResult? LastCompletedStage { get; }
    public HistoryConvergenceException(int index, string name, int substep, string reason, HistoryStageResult? last)
        : base($"Fase {index + 1} ({name}), sottopasso {substep}: {reason}. Stato della fase non accettato.")
    { PhaseIndex = index; PhaseName = name; Substep = substep; LastCompletedStage = last; }
}
