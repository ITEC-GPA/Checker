namespace GPC.Checkers.CompositeBridge.History;

public enum SectionResponseControl { MomentCurvature, AxialForceStrain }
public enum SectionResponseStop { Completed, MaterialDomain, EquilibriumNotFound }

/// <summary>Absolute targets, not increments. N in N, y in mm, epsilon dimensionless, curvature in 1/mm.
/// M-kappa holds N; N-epsilon holds kappa and measures epsilon at ReferenceY. M(y)=M(0)+N*y.</summary>
public sealed record SectionResponseOptions
{
    public SectionResponseControl Control { get; init; }
    public double ReferenceY { get; init; }
    /// <summary>For M-kappa only. Null retains the starting state's N (zero for virgin sections).</summary>
    public double? AxialForce { get; init; }
    /// <summary>For N-epsilon only. Null retains the starting curvature (zero for virgin sections).</summary>
    public double? FixedCurvature { get; init; }
    public int SubstepsPerTarget { get; init; } = 4;
    public int MaximumSubdivisions { get; init; } = 8;
    /// <summary>False stores requested endpoints plus the last accepted state on failure. True also stores internal substeps.</summary>
    public bool IncludeSubsteps { get; init; }
    public HistorySolverOptions Solver { get; init; } = new();
}

public sealed record SectionResponsePoint(int Index, int TargetIndex, bool ReachedTarget,
    double ControlValue, double ReferenceStrain, double MomentAtReference,
    double AxialTangent, double? BendingTangentAtConstantN, HistoryStageResult State);

/// <summary>Contains accepted states only, including the starting state. A stopped curve is NOT a capacity certificate.</summary>
public sealed record SectionResponseResult(SectionResponseOptions Options, double ConstantValue,
    IReadOnlyList<SectionResponsePoint> Points, SectionResponseStop Stop, int? FailedTargetIndex, string Message)
{
    public bool Completed => Stop == SectionResponseStop.Completed;
}
