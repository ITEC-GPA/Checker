namespace GPC.Checkers.Geotechnics.Seepage;

public enum SeepageStatus { Converged, NonConvergent, IllConditioned }

public sealed class SeepageOptions
{
    public int MaximumIterations { get; set; } = 250;
    public int MaximumLinearIterations { get; set; } = 10000;
    public double HeadTolerance { get; set; } = 1e-5;
    public double RelativeFlowTolerance { get; set; } = 1e-8;
    /// <summary>Absolute nodal discharge residual per out-of-plane length, mm²/s.</summary>
    public double AbsoluteFlowTolerance { get; set; } = 1e-9;
}

public sealed class SeepageResult
{
    public string CaseId { get; internal set; } = "";
    public SeepageStatus Status { get; internal set; }
    public bool IsComplete => Status == SeepageStatus.Converged;
    public string Diagnostic { get; internal set; } = "";
    public int Iterations { get; internal set; }
    public int LinearIterations { get; internal set; }
    public double MaximumResidual { get; internal set; }
    /// <summary>Boundary flows, mm²/s, including prescribed fluxes and fixed-head reactions.</summary>
    public double Inflow { get; internal set; }
    public double Outflow { get; internal set; }
    public double Balance => Inflow - Outflow;
    /// <summary>Empty on failure; failed iterates are never usable as pressures.</summary>
    public IReadOnlyList<SeepageNodeResult> Nodes { get; internal set; } = Array.Empty<SeepageNodeResult>();
    public IReadOnlyList<SeepageElementResult> Elements { get; internal set; } = Array.Empty<SeepageElementResult>();
}
public sealed class SeepageNodeResult
{
    public int Index { get; internal set; }
    public double X { get; internal set; }
    public double Z { get; internal set; }
    public double Head { get; internal set; }
    /// <summary>Gauge pore pressure γw(h−z), MPa. Suction is negative, never silently truncated.</summary>
    public double Pressure { get; internal set; }
    /// <summary>Positive inward. Reaction at a fixed head or active seepage face, mm²/s.</summary>
    public double BoundaryReaction { get; internal set; }
    public bool Draining { get; internal set; }
}
public sealed class SeepageElementResult
{
    public int Index { get; internal set; }
    public int A { get; internal set; }
    public int B { get; internal set; }
    public int C { get; internal set; }
    /// <summary>Components of ∇h; Darcy flux = −K∇h, mm/s.</summary>
    public double GradientX { get; internal set; }
    public double GradientZ { get; internal set; }
    public double FluxX { get; internal set; }
    public double FluxZ { get; internal set; }
    public double RelativeConductivity { get; internal set; }
}
public sealed class SeepageProgress
{
    public int Iteration { get; }
    public double Residual { get; }
    internal SeepageProgress(int iteration, double residual) { Iteration = iteration; Residual = residual; }
}
