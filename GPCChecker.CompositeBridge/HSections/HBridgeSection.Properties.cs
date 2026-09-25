namespace GPC.Checkers.CompositeBridge;

public static partial class HBridgeSection
{
    public static BridgeSectionProperties RectangleProperties(string name, double width, double height, double bottom, double x) =>
        CompositeSectionProperties.RectangleProperties(name, width, height, bottom, x);
    public static BridgeSectionProperties CombineProperties(string name, IEnumerable<BridgeSectionProperties> source) =>
        CompositeSectionProperties.CombineProperties(name, source);
    public static List<BridgeSectionProperties> SteelPartProperties(BridgeGeometry g, bool equivalent = false)
    {
        var p = new List<BridgeSectionProperties> {
            RectangleProperties("Piattabanda superiore", g.TopWidth, g.TopThickness, -g.TopThickness, g.Width / 2),
            RectangleProperties("Anima", g.WebThickness, g.WebHeight, -g.TopThickness - g.WebHeight, g.Width / 2) };
        if (equivalent) p.Add(RectangleProperties("Piattabanda inferiore equivalente", g.BottomEquivalentWidth, g.BottomEquivalentThickness, -g.Height, g.Width / 2));
        else
        {
            p.Add(RectangleProperties("Piattabanda inferiore 1", g.Bottom1Width, g.Bottom1Thickness, -g.TopThickness - g.WebHeight - g.Bottom1Thickness, g.Width / 2));
            if (g.Bottom2Thickness > 0) p.Add(RectangleProperties("Piattabanda inferiore 2", g.Bottom2Width, g.Bottom2Thickness, -g.Height, g.Width / 2));
        }
        return p;
    }
    public static BridgeSectionProperties GrossPhaseProperties(HBridgeInput data, BridgePhase phase, bool slabOnly = false)
    {
        var g = Geometry(data); var m = Materials(data); string kind = phase.KindName;
        if (slabOnly || HasConcrete(kind))
        {
            var section = NativeSection(data); if (slabOnly) section.SteelSections.Clear();
            var h = Homogenization(data, phase);
            // Model returns zeros for a concrete-only section without bars or structural steel.
            if (slabOnly && g.Bars.Length == 0) return RectangleProperties("Soletta omogeneizzata al CLS", g.Width, g.SlabHeight, 0, g.Width / 2);
            var props = section.GetHomogeneizedMechanicalProperties(h.PhiEffective); double factor = slabOnly ? 1 : h.N;
            return new(slabOnly ? "Soletta omogeneizzata al CLS" : "Sezione omogeneizzata all’acciaio", props.areaH / factor, props.centroidH.X, props.centroidH.Y,
                props.JxxH / factor, props.JyyH / factor, g.SlabHeight, slabOnly ? 0 : -g.Height, g.Width);
        }
        var pieces = SteelPartProperties(g, true);
        if (kind == "Soletta esclusa")
            pieces.AddRange(g.Bars.Select(b => new BridgeSectionProperties(b.Id, b.Area * m.Rebar.ElasticModulusTension / m.Steel.ElasticModulusTension,
                b.X, b.Y, Math.PI * Math.Pow(b.Diameter, 4) / 64 * m.Rebar.ElasticModulusTension / m.Steel.ElasticModulusTension,
                Math.PI * Math.Pow(b.Diameter, 4) / 64 * m.Rebar.ElasticModulusTension / m.Steel.ElasticModulusTension, b.Y + b.Diameter / 2, b.Y - b.Diameter / 2, b.Diameter)));
        return CombineProperties(kind, pieces);
    }
}
