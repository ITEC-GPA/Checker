namespace GPC.Checkers.CompositeBridge;

// Local plate reductions used by the bridge iteration. No UI or JSON dependencies.
public static partial class HBridgeSection
{
    public static BridgeEffective EffectiveWidths(BridgeGeometry g, Func<double, double> stress, double fy) =>
        EffectiveWidths(g, stress, fy, new BridgeAnalysisOptions());

    /// <summary>
    /// Effective widths (EN 1993-1-5 4.4) on the steel stress field: the web as an internal plate between the flanges, the flanges as
    /// outstands from the web. With two bottom plates each plate is an outstand of its own thickness from the web (the lamination of the
    /// plates is conservatively ignored). A part whose buckling is excluded by the options stays fully effective (rho = 1).
    /// </summary>
    public static BridgeEffective EffectiveWidths(BridgeGeometry g, Func<double, double> stress, double fy, BridgeAnalysisOptions options)
    {
        double top = -g.TopThickness, bottom = top - g.WebHeight;
        double upper = stress(top), lower = stress(bottom);
        var web = InternalPlate(g.WebHeight, g.WebThickness, upper, lower, fy);
        if (!options.WebBuckling) web = web with { Rho = 1, EffectiveAtStart = g.WebHeight / 2, EffectiveAtEnd = g.WebHeight / 2 };
        var topFlange = Outstand((g.TopWidth - g.WebThickness) / 2, g.TopThickness, Math.Min(stress(0), upper), fy, options.TopFlangeBuckling);
        if (g.Bottom2Thickness > 0)
        {
            double between = bottom - g.Bottom1Thickness;
            var first = Outstand((g.Bottom1Width - g.WebThickness) / 2, g.Bottom1Thickness, Math.Min(lower, stress(between)), fy, options.BottomFlangeBuckling);
            var second = Outstand((g.Bottom2Width - g.WebThickness) / 2, g.Bottom2Thickness, Math.Min(stress(between), stress(-g.Height)), fy, options.BottomFlangeBuckling);
            return new(web.EffectiveAtStart, web.EffectiveAtEnd, g.WebThickness + 2 * topFlange.EffectiveAtStart, g.WebThickness + 2 * first.EffectiveAtStart,
                web, topFlange, first, g.WebThickness + 2 * second.EffectiveAtStart, second);
        }
        var bottomFlange = Outstand((g.BottomEquivalentWidth - g.WebThickness) / 2, g.BottomEquivalentThickness, Math.Min(lower, stress(-g.Height)), fy, options.BottomFlangeBuckling);
        return new(web.EffectiveAtStart, web.EffectiveAtEnd, g.WebThickness + 2 * topFlange.EffectiveAtStart,
            g.WebThickness + 2 * bottomFlange.EffectiveAtStart, web, topFlange, bottomFlange);
    }
    public static BridgePlate InternalPlate(double b, double t, double startStress, double endStress, double fy) =>
        EffectivePlateReduction.InternalPlate(b, t, startStress, endStress, fy);
    private static BridgePlate Outstand(double b, double t, double stress, double fy, bool buckling = true)
    {
        var plate = EffectivePlateReduction.Outstand(b, t, stress, fy);
        return buckling ? plate : plate with { Rho = 1, EffectiveAtStart = b };
    }
}
