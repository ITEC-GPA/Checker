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
        double water = Water(ground, elevation);
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
    public static double Water(EmbeddedWallGround ground, double elevation)
    {
        ValidatePorePressures(ground);
        var p = ground.PorePressures;
        if (p.Count == 0 || elevation > p[0].Elevation && elevation >= ground.Elevation)
            return ground.WaterElevation.HasValue ? SoilUnits.WaterUnitWeight * Math.Max(0, ground.WaterElevation.Value - elevation) : 0;
        if (elevation > p[0].Elevation || elevation < p[p.Count - 1].Elevation)
            throw new ArgumentException("Il profilo di pressione interstiziale non copre la quota della paratia; nessuna estrapolazione.");
        int lo = 0, hi = p.Count - 1;
        while (hi - lo > 1) { int mid = (lo + hi) / 2; if (p[mid].Elevation >= elevation) lo = mid; else hi = mid; }
        double f = (p[lo].Elevation - elevation) / (p[lo].Elevation - p[hi].Elevation);
        return p[lo].Pressure * (1 - f) + p[hi].Pressure * f;
    }
    internal static void ValidatePorePressures(EmbeddedWallGround ground)
    {
        if (ground.PorePressures == null) throw new ArgumentException("Collezione delle pressioni interstiziali mancante.");
        var p = ground.PorePressures;
        if (p.Count == 0) return;
        if (p.Count < 2 || string.IsNullOrWhiteSpace(ground.PorePressureSource)) throw new ArgumentException("Profilo interstiziale: almeno due quote e una fonte obbligatorie.");
        for (int i = 0; i < p.Count; i++)
            if (!Finite(p[i].Elevation) || !Finite(p[i].Pressure) || p[i].Pressure < 0 || i > 0 && p[i - 1].Elevation <= p[i].Elevation)
                throw new ArgumentException("Pressioni interstiziali non negative e quote strettamente decrescenti richieste. La resistenza per suzione non è modellata.");
    }
    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
