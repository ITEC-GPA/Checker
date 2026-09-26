using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Sections.Steel;
namespace GPC.Checkers.CompositeBridge;

/// <summary>H-section adapter. Numerical services below are independent of ANTHEA.</summary>
public static partial class HBridgeSection
{
    public const string Method = "Sezione composta N–Mx · larghezze efficaci EN 1993-1-5:2006 · v1";
    public static readonly string[] Standards = ["NTC 2018 / EN 1994-2:2005", "EN 1994-2:2005"];
    public const string ShrinkageKind = "Ritiro";
    public static readonly string[] PhaseKinds = ["Solo acciaio", "Composta", "Soletta esclusa", ShrinkageKind];
    public static bool HasConcrete(string kind) => kind is "Composta" or ShrinkageKind;
    public static readonly string[] HomoModes = ["Da φ", "Da n"];
    public const string CommonLoadReference = "Quota comune", GrossLoadReference = "Baricentro omogeneizzato lordo", EffectiveLoadReference = "Baricentro efficace · iterativo";
    public static readonly string[] LoadReferences = [GrossLoadReference, EffectiveLoadReference, CommonLoadReference];
    public const string Scope = "Analisi elastica N–Mx con connessione completa, sezione simmetrica e anima senza irrigidimenti longitudinali. " +
        "b_eff della soletta è un dato di ingresso. Le fasi sono incrementi di carico già combinati; per ogni situazione la sezione efficace è comune ai contributi sommati. " +
        "Il ritiro uniforme assegnato al CLS produce effetti primari locali. Non è un'analisi evolutiva con redistribuzione viscosa. Taglio e pioli hanno controlli locali dedicati. Irrigidimenti, appoggi, saldature continue, soletta trasversale e fatica dei pioli hanno verifiche opzionali con campo dichiarato. Torsione e instabilità globale del ponte restano escluse.";
    public static string LoadReference(BridgePhase phase) => phase.ReferenceName;
    public static double GrossPhaseCentroid(HBridgeInput data, BridgePhase phase)
    {
        var g = Geometry(data);
        if (HasConcrete(phase.KindName)) return NativeSection(data, g).GetHomogeneizedMechanicalProperties(Homogenization(data, phase).PhiEffective).centroidH.Y;
        if (phase.KindName == "Solo acciaio") return g.SteelCentroid;
        if (phase.KindName != "Soletta esclusa") throw new ArgumentException("Tipo di fase sconosciuto.");
        var m = Materials(data); double ratio = m.Rebar.ElasticModulusTension / m.Steel.ElasticModulusTension;
        return (g.SteelArea * g.SteelCentroid + g.Bars.Sum(b => b.Area * ratio * b.Y)) / (g.SteelArea + g.Bars.Sum(b => b.Area * ratio));
    }
    private static void ValidateShape(HBridgeInput input)
    {
        if (input.Phases.Length is < 1 or > 20) throw new ArgumentException("Da 1 a 20 fasi richieste.");
        if (input.Materials is null) throw new ArgumentException("Materiali Model richiesti.");
    }
    private static BridgeMaterialSet Materials(HBridgeInput input) => input.Materials;
    public static BridgeGeometry Geometry(HBridgeInput d)
    {
        ValidateShape(d);
        
        double b = BridgeNumbers.Require(d.Geometry.SlabWidth, "b_cls", strict: true), tc = BridgeNumbers.Require(d.Geometry.SlabHeight, "h_cls", strict: true), hw = BridgeNumbers.Require(d.Geometry.WebHeight, "h_web", strict: true), tw = BridgeNumbers.Require(d.Geometry.WebThickness, "t_web", strict: true), bt = BridgeNumbers.Require(d.Geometry.TopWidth, "b_top", strict: true), tt = BridgeNumbers.Require(d.Geometry.TopThickness, "t_top", strict: true), b1 = BridgeNumbers.Require(d.Geometry.BottomWidth, "b_bottom", strict: true), t1 = BridgeNumbers.Require(d.Geometry.BottomThickness, "t_bottom", strict: true);
        double b2 = d.Geometry.SecondBottomEnabled ? BridgeNumbers.Require(d.Geometry.SecondBottomWidth, "b_bottom2", strict: true) : 0, t2 = d.Geometry.SecondBottomEnabled ? BridgeNumbers.Require(d.Geometry.SecondBottomThickness, "t_bottom2", strict: true) : 0;
        if (Math.Min(bt, b1) <= tw || b2 > b1 || b2 != 0 && b2 <= tw || hw <= tw)
            throw new ArgumentException("Controllare anima e piattabande: b > tw; la seconda piastra inferiore deve essere contenuta nella prima.");
        var mat = Materials(d);
        double tb = t1 + t2, bb = (b1 * t1 + b2 * t2) / tb, h = hw + tt + tb;
        IRebarSection? Bar(string side)
        {
            var row = side == "top" ? d.TopRebars : d.BottomRebars;
            if (!row.Enabled) return null;
            double diameter = BridgeNumbers.Require(row.Diameter, "diameter", strict: true), pitch = BridgeNumbers.Require(row.Pitch, "pitch", strict: true), cover = BridgeNumbers.Require(row.AxisDistance, "axis distance", strict: true);
            if (pitch < diameter || cover < diameter / 2 || cover + diameter / 2 > tc || diameter >= b || b / pitch > 500)
                throw new ArgumentException("Armatura " + side + ": controllare diametro, passo e distanza dell'asse dalla faccia (massimo 501 barre/fila).");
            return new RebarSectionCircular(diameter, mat.Rebar);
        }
        var top = Bar("top"); var bottom = Bar("bottom");
        if (top is not null && bottom is not null && tc - d.TopRebars.AxisDistance - d.BottomRebars.AxisDistance < (top.Diameter + bottom.Diameter) / 2)
            throw new ArgumentException("Le file di armature devono avere assi distinti e distanza almeno pari alla somma dei raggi.");
        // two bottom plates: the exact section of the four plates (before, the equivalent rectangle with the same area and thickness)
        var shape = SteelShape(h, tw, bt, tt, b1, t1, b2, t2);
        var section = ReinforcedConcreteSection.CreateBridgeSection(b, tc, mat.Concrete, top!, d.TopRebars.Pitch, d.TopRebars.AxisDistance,
            bottom!, d.BottomRebars.Pitch, shape, mat.Steel, d.BottomRebars.AxisDistance);
        var bars = section.Rebars.Select((r, i) => new BridgeBar("B" + (i + 1), r.Position.X, r.Position.Y, r.RebarSection.Diameter, r.Area)).ToArray();
        for (int i = 0; i < bars.Length; i++) for (int j = 0; j < i; j++)
            if (BridgeNumbers.Hypot(bars[i].X - bars[j].X, bars[i].Y - bars[j].Y) < (bars[i].Diameter + bars[j].Diameter) / 2 - 1e-8)
                throw new ArgumentException("Le due file di armature si sovrappongono: correggere le quote degli assi.");
        // Resultant rectangle preserves area and overall thickness; exact two-plate inertia is reported separately.
        double y1 = -tt - hw - t1 / 2, y2 = -h + t2 / 2;
        double ab = b1 * t1 + b2 * t2, yb = (b1 * t1 * y1 + b2 * t2 * y2) / ab;
        double ib = b1 * Math.Pow(t1, 3) / 12 + b1 * t1 * Math.Pow(y1 - yb, 2) + b2 * Math.Pow(t2, 3) / 12 + b2 * t2 * Math.Pow(y2 - yb, 2);
        return new(b, tc, hw, tw, bt, tt, b1, t1, b2, t2, bb, tb, h, ab, yb, ib, shape.Area, shape.Jxx, shape.Centroid.Y - h, bars);
    }
    public static ReinforcedConcreteSection NativeSection(HBridgeInput d) => NativeSection(d, Geometry(d));
    /// <summary>The native section on an already calculated geometry (avoids building the geometry twice)</summary>
    private static ReinforcedConcreteSection NativeSection(HBridgeInput d, BridgeGeometry g)
    {
        var m = Materials(d);
        return ReinforcedConcreteSection.CreateBridgeSection(g.Width, g.SlabHeight, m.Concrete,
            d.TopRebars.Enabled ? new RebarSectionCircular(d.TopRebars.Diameter, m.Rebar) : null!, d.TopRebars.Pitch, d.TopRebars.AxisDistance,
            d.BottomRebars.Enabled ? new RebarSectionCircular(d.BottomRebars.Diameter, m.Rebar) : null!, d.BottomRebars.Pitch,
            SteelShape(g.Height, g.WebThickness, g.TopWidth, g.TopThickness, g.Bottom1Width, g.Bottom1Thickness, g.Bottom2Width, g.Bottom2Thickness), m.Steel, d.BottomRebars.AxisDistance);
    }
    /// <summary>The welded steel section: an H with one bottom plate, or with two (<see cref="SectionHDoubleBottomFlange"/>)</summary>
    private static Section SteelShape(double h, double tw, double bt, double tt, double b1, double t1, double b2, double t2) =>
        t2 > 0 ? new SectionHDoubleBottomFlange(h, tw, bt, tt, b1, t1, b2, t2, "H saldato · due piastre inferiori") : new SectionH(h, tw, bt, tt, b1, t1, "H saldato");
    public static (double N0, double N, double PhiEffective, double Phi) Homogenization(HBridgeInput data, BridgePhase phase) =>
        CompositeHomogenization.Calculate(data.Materials, phase);
}
