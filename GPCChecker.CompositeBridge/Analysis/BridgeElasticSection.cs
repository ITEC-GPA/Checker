namespace GPC.Checkers.CompositeBridge;

/// <summary>Prepared geometric moments of a bridge effective state. No Model section/mesh objects are needed for elastic N-Mx.</summary>
internal sealed class BridgeElasticSection
{
    private readonly BridgeGeometry geometry;
    private readonly BridgeMaterialSet materials;
    private readonly double steelArea, steelY, steelI, flangeI, barArea, barY, barPointI, barOwnI;

    internal static bool Supports(BridgeMaterialSet m) =>
        m.Steel.ElasticModulusTension == m.Steel.ElasticModulusCompression
        && m.Rebar.ElasticModulusTension == m.Rebar.ElasticModulusCompression
        && m.Concrete.ElasticModulusTension == m.Concrete.ElasticModulusCompression;

    internal BridgeElasticSection(BridgeGeometry g, BridgeEffective e, BridgeMaterialSet m)
    {
        geometry = g; materials = m;
        var parts = new List<(double A, double Y, double I)>();
        double flanges = 0;
        void Plate(double width, double height, double bottom, bool web = false) {
            // Same vanishing-strip convention as the native steel pieces.
            if (width <= 1e-8 || height <= 1e-8) return;
            double own = width * height * height * height / 12;
            parts.Add((width * height, bottom + height / 2, own));
            if (!web) flanges += own;
        }
        Plate(e.TopWidth, g.TopThickness, -g.TopThickness);
        Plate(g.WebThickness, e.WebTop, -g.TopThickness - e.WebTop, true);
        Plate(g.WebThickness, e.WebBottom, -g.TopThickness - g.WebHeight, true);
        Plate(e.BottomWidth, g.Bottom1Thickness, -g.TopThickness - g.WebHeight - g.Bottom1Thickness);
        if (g.Bottom2Thickness > 0) Plate(e.SecondBottomWidth, g.Bottom2Thickness, -g.Height);
        steelArea = parts.Sum(p => p.A); steelY = parts.Sum(p => p.A * p.Y) / steelArea;
        steelI = parts.Sum(p => p.I + p.A * Math.Pow(p.Y - steelY, 2)); flangeI = flanges;
        barArea = g.Bars.Sum(b => b.Area);
        barY = barArea == 0 ? 0 : g.Bars.Sum(b => b.Area * b.Y) / barArea;
        barPointI = g.Bars.Sum(b => b.Area * Math.Pow(b.Y - barY, 2));
        barOwnI = g.Bars.Sum(b => Math.PI * Math.Pow(b.Diameter, 4) / 64);
    }

    internal (double Area, double Y, double Inertia, double IntegrationInertia) Properties(BridgePhaseKind kind, double n, double effectivePhi)
    {
        if (kind == BridgePhaseKind.SteelOnly) return (steelArea, steelY, steelI, steelI);
        double ea = materials.Steel.ElasticModulusTension, es = materials.Rebar.ElasticModulusTension;
        bool composite = kind == BridgePhaseKind.Composite;
        // Preserve Model's transformation into concrete followed by the bridge's division by n. In ModularRatio mode n and the
        // ratio reconstructed from phi need not be bit-identical (including phi clamped at zero near n0).
        double ec = materials.Concrete.ElasticModulusCompression;
        double steelFactor = composite ? ea / ec * (1 + effectivePhi) : 1;
        double barFactor = composite ? es / ec * (1 + effectivePhi) - 1 : es / ea;
        double concreteArea = composite ? geometry.Width * geometry.SlabHeight : 0;
        double concreteY = geometry.SlabHeight / 2;
        double a = steelArea * steelFactor + concreteArea + barArea * barFactor;
        double y = (steelArea * steelFactor * steelY + concreteArea * concreteY + barArea * barFactor * barY) / a;
        double inertia = (steelI + steelArea * Math.Pow(steelY - y, 2)) * steelFactor
            + concreteArea * (geometry.SlabHeight * geometry.SlabHeight / 12 + Math.Pow(concreteY - y, 2))
            + (barPointI + barOwnI + barArea * Math.Pow(barY - y, 2)) * barFactor;
        if (!composite) return (a, y, inertia, inertia);
        a /= n; inertia /= n;
        return (a, y, inertia, inertia - flangeI - barOwnI * (es / ea - 1 / n));
    }
}
