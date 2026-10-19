namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

/// <summary>Elastic-perfectly-plastic pressure law with return mapping. Pressure MPa, inward movement/plastic offset mm,
/// stiffness N/mm³. Trial evaluations never mutate committed history. Unloading uses the same declared elastic modulus.</summary>
public static class WallConstitutiveLaw
{
    public static WallSpringResponse Evaluate(double inwardMovement, double plasticOffset, double stiffness,
        double atRestPressure, double minimumPressure, double maximumPressure)
    {
        if (new[] { inwardMovement, plasticOffset, stiffness, atRestPressure, minimumPressure, maximumPressure }.Any(v => double.IsNaN(v) || double.IsInfinity(v))
            || stiffness <= 0 || minimumPressure < 0 || maximumPressure < minimumPressure)
            throw new ArgumentException("Finite values, positive stiffness and 0 ≤ minimum ≤ maximum required.");
        double trial = atRestPressure + stiffness * (inwardMovement - plasticOffset);
        double pressure = Math.Max(minimumPressure, Math.Min(maximumPressure, trial));
        bool plastic = trial < minimumPressure || trial > maximumPressure;
        return new WallSpringResponse(pressure, plastic ? 0 : stiffness,
            plastic ? inwardMovement - (pressure - atRestPressure) / stiffness : plasticOffset, plastic);
    }
}

public sealed class WallSpringResponse
{
    public double Pressure { get; }
    public double Tangent { get; }
    public double PlasticOffset { get; }
    public bool IsPlastic { get; }
    internal WallSpringResponse(double pressure, double tangent, double plasticOffset, bool isPlastic)
    { Pressure = pressure; Tangent = tangent; PlasticOffset = plasticOffset; IsPlastic = isPlastic; }
}
