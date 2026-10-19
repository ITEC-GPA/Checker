namespace GPC.Checkers.Geotechnics.Slopes;

/// <summary>Prescribed stationary field in slope coordinates (mm, MPa, N/mm³). Implementations must be immutable,
/// reject points outside the hydraulic domain and provide a complete nonoverlapping bottom-to-top column partition.
/// Body volumes are excluded before calling Column. Does not add hydraulic forces on exposed boundaries.</summary>
public interface ISlopeHydraulicField
{
    string Source { get; }
    double WaterUnitWeight { get; }
    double Pressure(double x, double y);
    IReadOnlyList<SlopeMoistureInterval> Column(double x, double bottom, double top);
}
public readonly struct SlopeMoistureInterval
{
    public double Bottom { get; }
    public double Top { get; }
    public bool Saturated { get; }
    public SlopeMoistureInterval(double bottom, double top, bool saturated) { Bottom = bottom; Top = top; Saturated = saturated; }
}
