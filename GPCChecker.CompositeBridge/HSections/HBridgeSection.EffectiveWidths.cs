namespace GPC.Checkers.CompositeBridge;

// Local plate reductions used by the bridge iteration. No UI or JSON dependencies.
public static partial class HBridgeSection
{
    public static BridgeEffective EffectiveWidths(BridgeGeometry g, Func<double, double> stress, double fy)
    {
        double upper = stress(-g.TopThickness), lower = stress(-g.TopThickness - g.WebHeight);
        var web = InternalPlate(g.WebHeight, g.WebThickness, upper, lower, fy);
        var top = Outstand((g.TopWidth - g.WebThickness) / 2, g.TopThickness, Math.Min(stress(0), upper), fy);
        var bottom = Outstand((g.BottomEquivalentWidth - g.WebThickness) / 2, g.BottomEquivalentThickness, Math.Min(lower, stress(-g.Height)), fy);
        return new(web.EffectiveAtStart, web.EffectiveAtEnd, g.WebThickness + 2 * top.EffectiveAtStart,
            g.WebThickness + 2 * bottom.EffectiveAtStart, web, top, bottom);
    }
    public static BridgePlate InternalPlate(double b, double t, double startStress, double endStress, double fy) =>
        EffectivePlateReduction.InternalPlate(b, t, startStress, endStress, fy);
    private static BridgePlate Outstand(double b, double t, double stress, double fy) => EffectivePlateReduction.Outstand(b, t, stress, fy);
}
