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
    /// Inclined webs: the plate is the real one, of length hw / cos α and thickness tw, with the stresses of its ends; its effective strips are
    /// brought back to vertical heights. Box: each top flange has two outstands from its web; the bottom flange is an internal plate between the
    /// webs (uniform stress) plus the outstands beyond the webs.
    /// </summary>
    public static BridgeEffective EffectiveWidths(BridgeGeometry g, Func<double, double> stress, double fy, BridgeAnalysisOptions options)
    {
        double top = -g.TopThickness, bottom = top - g.WebHeight, twh = g.WebHorizontalThickness;
        double upper = stress(top), lower = stress(bottom);
        var web = InternalPlate(g.PlateLength, g.PlateThickness, upper, lower, fy);
        if (!options.WebBuckling) web = web with { Rho = 1, EffectiveAtStart = g.PlateLength / 2, EffectiveAtEnd = g.PlateLength / 2 };
        var topFlange = Outstand((g.TopFlangeWidth - twh) / 2, g.TopThickness, Math.Min(stress(0), upper), fy, options.TopFlangeBuckling);
        if (g.SectionType == BridgeSteelSectionType.Box)
        {
            double sigma = Math.Min(lower, stress(-g.Height));
            var internalPlate = InternalPlate(g.BottomInternalWidth, g.Bottom1Thickness, sigma, sigma, fy);
            if (!options.BottomFlangeBuckling)
                internalPlate = internalPlate with { Rho = 1, EffectiveAtStart = g.BottomInternalWidth / 2, EffectiveAtEnd = g.BottomInternalWidth / 2 };
            var outstand = g.BottomOutstandWidth > 0 ? Outstand(g.BottomOutstandWidth, g.Bottom1Thickness, sigma, fy, options.BottomFlangeBuckling) : null;
            return AssembleEffective(g, web, topFlange, internalPlate, null, outstand);
        }
        if (g.Bottom2Thickness > 0)
        {
            double between = bottom - g.Bottom1Thickness;
            var first = Outstand((g.Bottom1Width - twh) / 2, g.Bottom1Thickness, Math.Min(lower, stress(between)), fy, options.BottomFlangeBuckling);
            var second = Outstand((g.Bottom2Width - twh) / 2, g.Bottom2Thickness, Math.Min(stress(between), stress(-g.Height)), fy, options.BottomFlangeBuckling);
            return AssembleEffective(g, web, topFlange, first, second);
        }
        var bottomFlange = Outstand((g.BottomEquivalentWidth - twh) / 2, g.BottomEquivalentThickness, Math.Min(lower, stress(-g.Height)), fy, options.BottomFlangeBuckling);
        return AssembleEffective(g, web, topFlange, bottomFlange);
    }

    /// <summary>
    /// The effective geometry of the section from the reductions of the plates: vertical heights of the strips of the web (the strips along the
    /// plate times cos α) and total widths of the flanges (H: web + two outstands; box: two top flanges, bottom flange with two webs, internal
    /// plate and two outstands)
    /// </summary>
    /// <param name="g">The geometry</param>
    /// <param name="web">The web, along its plane</param>
    /// <param name="top">The outstand of a top flange</param>
    /// <param name="bottom">The outstand of the bottom flange (box: the internal plate)</param>
    /// <param name="second">The outstand of the second bottom plate (H with two plates)</param>
    /// <param name="bottomOutstand">Box: the outstand of the bottom flange beyond a web</param>
    /// <returns>The effective geometry</returns>
    public static BridgeEffective AssembleEffective(BridgeGeometry g, BridgePlate web, BridgePlate top, BridgePlate bottom, BridgePlate? second = null,
        BridgePlate? bottomOutstand = null)
    {
        double twh = g.WebHorizontalThickness, cos = Math.Cos(g.WebAngle);
        double topWidth = g.TopFlangeCount == 1 ? twh + 2 * top.EffectiveAtStart : g.TopFlangeCount * (twh + 2 * top.EffectiveAtStart);
        double bottomWidth = g.SectionType == BridgeSteelSectionType.Box
            ? 2 * twh + bottom.EffectiveAtStart + bottom.EffectiveAtEnd + 2 * (bottomOutstand?.EffectiveAtStart ?? 0)
            : twh + 2 * bottom.EffectiveAtStart;
        return new(web.EffectiveAtStart * cos, web.EffectiveAtEnd * cos, topWidth, bottomWidth, web, top, bottom,
            second is null ? 0 : twh + 2 * second.EffectiveAtStart, second, bottomOutstand);
    }

    public static BridgePlate InternalPlate(double b, double t, double startStress, double endStress, double fy) =>
        EffectivePlateReduction.InternalPlate(b, t, startStress, endStress, fy);
    private static BridgePlate Outstand(double b, double t, double stress, double fy, bool buckling = true)
    {
        var plate = EffectivePlateReduction.Outstand(b, t, stress, fy);
        return buckling ? plate : plate with { Rho = 1, EffectiveAtStart = b };
    }
}
