namespace GPC.Checkers.Geotechnics.Piles;

/// <summary>Vertical layer thickness in mm and the Bustamante–Doix parameters actually needed by a micropile.</summary>
public sealed class MicropileDepthLayer
{
    public double Thickness { get; }
    public BustamanteDoixSoil Soil { get; }
    public double Alpha { get; }
    public bool ShaftActive { get; }
    public MicropileDepthLayer(double thickness, BustamanteDoixSoil soil, double alpha, bool shaftActive = true)
    {
        if (double.IsNaN(thickness) || double.IsInfinity(thickness) || thickness <= 0) throw new ArgumentException("Positive finite layer thickness required.");
        if (!Enum.IsDefined(typeof(BustamanteDoixSoil), soil)) throw new ArgumentException("Unknown Bustamante-Doix soil.");
        if (shaftActive && (double.IsNaN(alpha) || double.IsInfinity(alpha) || alpha <= 0)) throw new ArgumentException("Positive finite alpha required for an active layer.");
        Thickness = thickness; Soil = soil; Alpha = alpha; ShaftActive = shaftActive;
    }
}

/// <summary>Micropile survey without invented mechanical soil properties. Layers start at the pile head.</summary>
public sealed class MicropileDepthSurvey
{
    public IReadOnlyList<MicropileDepthLayer> Layers { get; }
    public MicropileDepthSurvey(IEnumerable<MicropileDepthLayer> layers)
    {
        if (layers is null) throw new ArgumentNullException(nameof(layers));
        var owned = layers.ToArray();
        if (owned.Length == 0 || owned.Any(l => l is null)) throw new ArgumentException("At least one non-null layer required.");
        Layers = Array.AsReadOnly(owned);
    }
    /// <summary>Convert vertical depths to axis coordinates s = z/cos(theta), with theta in radians from the vertical.</summary>
    public MicropileLayer[] AlongAxis(double inclination)
    {
        double cosine = MicropileTube.AxisCosine(inclination), top = 0;
        return Layers.Select(layer =>
        {
            double bottom = top + layer.Thickness;
            if (double.IsInfinity(bottom)) throw new ArgumentException("Cumulative layer depth exceeds the numeric range.");
            var result = new MicropileLayer(top / cosine, bottom / cosine, layer.Soil, layer.Alpha, layer.ShaftActive);
            top = bottom; return result;
        }).ToArray();
    }
}
