namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

/// <summary>Units respectively mm, radians, N, N, Nmm, MPa, MPa and N/mm, on the declared strip.</summary>
public enum WallResultQuantity { Displacement, Rotation, NormalForce, Shear, Moment, LeftPressure, RightPressure, NetDistributedLoad }
public enum WallResultSide { Above, Below }

/// <summary>Frozen polynomials in t=(Top-z)/(Top-Bottom), captured by the solver. Not an independent input model.
/// The pressure and line-load recovery is cubic; V quartic and M quintic. Displacement remains Hermite cubic.
/// Plastic flags report the nearest integration point, as in the original recovered table.</summary>
public sealed class WallRecoveredElement
{
    public double Top { get; }
    public double Bottom { get; }
    public string SectionId { get; }
    internal double[][] Coefficients { get; }
    internal bool[] LeftPlastic { get; }
    internal bool[] RightPlastic { get; }
    internal WallRecoveredElement(double top, double bottom, string sectionId, double[][] coefficients, bool[] left, bool[] right)
    {
        Top = top; Bottom = bottom; SectionId = sectionId;
        Coefficients = coefficients.Select(p => (double[])p.Clone()).ToArray();
        LeftPlastic = (bool[])left.Clone(); RightPlastic = (bool[])right.Clone();
    }
}

/// <summary>One complete concomitant observation from one actual phase and case; never assembled from separate maxima.</summary>
public sealed class WallEnvelopeObservation
{
    public string CaseId { get; internal set; } = "";
    public string StageId { get; internal set; } = "";
    public double Value { get; internal set; }
    public WallPointResult Point { get; internal set; } = new();
}
public sealed class WallEnvelopeStation
{
    public double Elevation { get; internal set; }
    public string Side { get; internal set; } = "";
    public WallEnvelopeObservation Minimum { get; internal set; } = new();
    public WallEnvelopeObservation Maximum { get; internal set; } = new();
}
public sealed class WallEnvelopeSeries
{
    public WallResultQuantity Quantity { get; internal set; }
    public IReadOnlyList<WallEnvelopeStation> Stations { get; internal set; } = Array.Empty<WallEnvelopeStation>();
    public WallEnvelopeObservation Minimum { get; internal set; } = new();
    public WallEnvelopeObservation Maximum { get; internal set; } = new();
}
public sealed class WallEnvelopeHistory
{
    public string CaseId { get; internal set; } = "";
    public bool Complete { get; internal set; }
    public string? FailedStage { get; internal set; }
    public IReadOnlyList<string> IncludedStages { get; internal set; } = Array.Empty<string>();
    public string Diagnostic { get; internal set; } = "";
}
public sealed class WallEnvelopeResult
{
    public bool IsComplete { get; internal set; }
    public double Top { get; internal set; }
    public double Bottom { get; internal set; }
    public double TributaryWidth { get; internal set; }
    public int Subdivisions { get; internal set; }
    public string Diagnostic { get; internal set; } = "";
    public IReadOnlyList<WallEnvelopeHistory> Histories { get; internal set; } = Array.Empty<WallEnvelopeHistory>();
    public IReadOnlyList<WallEnvelopeSeries> Series { get; internal set; } = Array.Empty<WallEnvelopeSeries>();
}
