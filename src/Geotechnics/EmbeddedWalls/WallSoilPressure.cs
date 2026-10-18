using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Geotechnics.Walls;

namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

public sealed class WallPressureLimits
{
    public bool HasSoil { get; internal set; }
    public double Stiffness { get; internal set; }
    public double AtRest { get; internal set; }
    public double Active { get; internal set; }
    public double Passive { get; internal set; }
    public double Water { get; internal set; }
    public double VerticalTotal { get; internal set; }
    public double VerticalEffective { get; internal set; }
}

/// <summary>Horizontal layers, vertical smooth wall, uniform surcharge. Drained Rankine in effective stresses;
/// undrained Rankine in total stresses (phi=0, cu) includes water in the pressure law itself. No seismic coefficients are inferred.</summary>
public static class WallSoilPressure
{
    public static WallPressureLimits At(IReadOnlyList<EmbeddedWallSoil> layers, EmbeddedWallGround ground, double elevation)
    {
        if (layers == null || ground == null || !Finite(elevation) || !Finite(ground.Elevation) || !Finite(ground.Surcharge)
            || ground.Surcharge < 0 || ground.WaterElevation.HasValue && !Finite(ground.WaterElevation.Value))
            throw new ArgumentException("Finite elevations and nonnegative surcharge required.");
        double water = ground.WaterElevation.HasValue ? SoilUnits.WaterUnitWeight * Math.Max(0, ground.WaterElevation.Value - elevation) : 0;
        if (elevation >= ground.Elevation || layers.Count == 0) return new WallPressureLimits { Water = water };
        var layer = layers.FirstOrDefault(l => elevation <= l.Top && elevation >= l.Bottom)
            ?? throw new ArgumentException("Soil profile does not cover the wall station.");
        double total = ground.Surcharge + SoilUnits.WaterUnitWeight * Math.Max(0, (ground.WaterElevation ?? ground.Elevation) - ground.Elevation);
        foreach (var s in layers)
        {
            double top = Math.Min(s.Top, ground.Elevation), bottom = Math.Max(s.Bottom, elevation);
            if (top <= bottom) continue;
            double wet = Math.Max(0, Math.Min(top, ground.WaterElevation ?? double.NegativeInfinity) - bottom);
            total += (top - bottom - wet) * s.Soil.UnitWeight + wet * s.Soil.SaturatedUnitWeight;
        }
        double effective = total - water;
        if (effective < -1e-12) throw new ArgumentException("Negative effective overburden: profile or hydraulic model incompatible.");
        effective = Math.Max(0, effective);
        var result = new WallPressureLimits { HasSoil = true, Stiffness = layer.HorizontalModulus,
            AtRest = layer.AtRestCoefficient * effective, Water = water, VerticalTotal = total, VerticalEffective = effective };
        if (layer.Drainage == EmbeddedWallDrainage.UndrainedTotalStress)
        {
            double cu = layer.Soil.UndrainedShearStrength ?? throw new ArgumentException("Undrained shear strength is required.");
            result.Active = Math.Max(0, total - 2 * cu); result.Passive = total + 2 * cu;
            result.AtRest += water; result.Water = 0; // Total stress law: never add hydrostatic pressure a second time.
        }
        else
        {
            double ka = EarthPressure.RankineActive(layer.Soil.FrictionAngle), kp = EarthPressure.RankinePassive(layer.Soil.FrictionAngle);
            result.Active = Math.Max(0, ka * effective - 2 * layer.Soil.EffectiveCohesion * Math.Sqrt(ka));
            result.Passive = kp * effective + 2 * layer.Soil.EffectiveCohesion * Math.Sqrt(kp);
        }
        if (result.AtRest < result.Active - 1e-12 || result.AtRest > result.Passive + 1e-12)
            throw new ArgumentException("K0 initial stress lies outside the active/passive bounds; revise the initial stress model. No silent clipping is permitted.");
        return result;
    }
    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
