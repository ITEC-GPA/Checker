namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

public enum WallAnalysisStatus { Converged, NonConvergent, Unstable }

public sealed class WallSolverOptions
{
    public int InitialSubsteps { get; set; } = 8;
    public int MaximumIterations { get; set; } = 60;
    public double MinimumIncrement { get; set; } = 1.0 / 4096;
    public double RelativeResidualTolerance { get; set; } = 1e-8;
    public double AbsoluteForceTolerance { get; set; } = 1e-5;
}

public sealed class WallAnalysisProgress
{
    public string StageId { get; }
    public double Fraction { get; }
    public int Iterations { get; }
    internal WallAnalysisProgress(string stageId, double fraction, int iterations)
    { StageId = stageId; Fraction = fraction; Iterations = iterations; }
}

public sealed class WallAnalysisResult
{
    public string CaseId { get; internal set; } = "";
    public double Top { get; internal set; }
    public double Bottom { get; internal set; }
    public double TributaryWidth { get; internal set; }
    public WallAnalysisStatus Status { get; internal set; }
    public string Diagnostic { get; internal set; } = "";
    public string? FailedStage { get; internal set; }
    public IReadOnlyList<WallStageResult> Stages { get; internal set; } = Array.Empty<WallStageResult>();
    public bool IsComplete => Status == WallAnalysisStatus.Converged;
}

public sealed class WallStageResult
{
    public string Id { get; internal set; } = "";
    public string CaseId { get; internal set; } = "";
    public int AcceptedIncrements { get; internal set; }
    public int Iterations { get; internal set; }
    public double ForceResidual { get; internal set; }
    public double MomentResidual { get; internal set; }
    public double MaximumScaledResidual { get; internal set; }
    public IReadOnlyList<WallPointResult> Points { get; internal set; } = Array.Empty<WallPointResult>();
    /// <summary>Frozen recovered fields for sampling without interpolation of tabulated extrema or a new analysis.</summary>
    public IReadOnlyList<WallRecoveredElement> Elements { get; internal set; } = Array.Empty<WallRecoveredElement>();
    public IReadOnlyList<WallSupportResult> Supports { get; internal set; } = Array.Empty<WallSupportResult>();
    public IReadOnlyList<WallBoundaryReaction> Reactions { get; internal set; } = Array.Empty<WallBoundaryReaction>();
}

/// <summary>Concomitant actions for the represented strip: N positive compression, V N, M Nmm; rotation = du/d(depth).
/// At a concentrated action both element-end values are retained (Above/Below).</summary>
public sealed class WallPointResult
{
    public double Elevation { get; internal set; }
    public string Side { get; internal set; } = "";
    public string SectionId { get; internal set; } = "";
    public double Displacement { get; internal set; }
    public double Rotation { get; internal set; }
    public double NormalForce { get; internal set; }
    public double Shear { get; internal set; }
    public double Moment { get; internal set; }
    public double LeftPressure { get; internal set; }
    public double RightPressure { get; internal set; }
    public double NetDistributedLoad { get; internal set; }
    public bool LeftPlastic { get; internal set; }
    public bool RightPlastic { get; internal set; }
}

public sealed class WallSupportResult
{
    public string Id { get; internal set; } = "";
    public GPC.Model.Geotechnics.EmbeddedWalls.EmbeddedWallSupportKind Kind { get; internal set; }
    /// <summary>Force per individual support, not per strip. Positive: tension for Anchor/Bilateral, compression for Strut.</summary>
    public double AxialForce { get; internal set; }
    /// <summary>Signed physical axial force per individual support, N, positive tension.</summary>
    public double NormalForce => Kind == GPC.Model.Geotechnics.EmbeddedWalls.EmbeddedWallSupportKind.Strut ? -AxialForce : AxialForce;
    public double AxialStiffness { get; internal set; }
    public double HorizontalReaction { get; internal set; }
    public double ReferenceDisplacement { get; internal set; }
    public bool AtCapacity { get; internal set; }
}

public sealed class WallBoundaryReaction
{
    public double Elevation { get; internal set; }
    public double Force { get; internal set; }
    public double Moment { get; internal set; }
}
