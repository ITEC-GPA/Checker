using GPC.Model.Actions.Hydraulics;
using C = GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Hydraulics;

/// <summary>Hydrostatics and stationary drag from a supplied hydraulic study. No flood, seepage or scour solver.</summary>
public static class HydraulicActions
{
    private const double Gravity = 9.81;
    private const string Hydrostatic = "Equilibrio idrostatico: p = γw max(H − z, 0)";
    private const string Integration = "Integrazione delle pressioni sulla superficie effettivamente bagnata";
    private const string Drag = "Resistenza stazionaria: p = ρ Cd v |v| / 2; Cd da studio idraulico";

    public static HydraulicActionsResult Calculate(HydraulicActionsInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        C.EnumValue(input.Element); C.Positive(input.UnitWeightKnM3, "Peso specifico dell'acqua");
        Finite(input.BottomElevationM, "Quota inferiore"); C.Positive(input.WidthM, "Larghezza");
        C.Require(input.WaterCases is { Count: > 0 }, "Inserire almeno uno scenario idraulico.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in input.WaterCases)
        {
            C.Require(c is not null && !string.IsNullOrWhiteSpace(c.Name), "Scenario senza nome.");
            C.Require(names.Add(c!.Name.Trim()), "I nomi degli scenari devono essere distinti.");
            Finite(c.LeftLevelM, "Livello sinistro / piezometrico inferiore");
            Finite(c.RightLevelM, "Livello destro / superiore");
            Finite(c.CurrentVelocityMs, "Velocità della corrente");
            C.Range(c.ProfileMultiplier, 0, double.MaxValue, "Moltiplicatore del profilo");
        }
        if (input.Element == HydraulicElement.HorizontalSlab)
        {
            C.EnumValue(input.Uplift); C.Positive(input.SlabLengthM, "Lunghezza della platea");
            C.Positive(input.SlabThicknessM, "Spessore della platea");
            if (input.Uplift == UpliftDistribution.AssignedPressureProfile)
            {
                C.Require(!string.IsNullOrWhiteSpace(input.PressureSource), "Indicare la provenienza del profilo di sottospinta (studio, filtrazione o misura).");
                C.Require(input.PressureProfile is { Count: >= 2 } && input.PressureProfile.All(p => p is not null), "Il profilo richiede almeno due punti.");
                foreach (var p in input.PressureProfile) { C.Range(p.PositionM, 0, input.SlabLengthM, "x sottospinta"); C.Range(p.PressureKpa, 0, double.MaxValue, "Pressione sottospinta"); }
                Ordered(input.PressureProfile.Select(p => p.PositionM).ToArray(), 0, input.SlabLengthM, "sottospinta");
            }
        }
        else
        {
            Finite(input.TopElevationM, "Quota superiore");
            C.Require(input.TopElevationM > input.BottomElevationM, "Quota superiore non maggiore della quota inferiore.");
            if (input.Element == HydraulicElement.RiverPier)
            {
                C.Positive(input.DragCoefficient, "Coefficiente Cd");
                C.Require(!string.IsNullOrWhiteSpace(input.DragSource), "Indicare fonte e applicabilità di Cd alla forma, orientamento e regime della corrente.");
                if (input.UseVelocityProfile)
                {
                    C.Require(input.VelocityProfile is { Count: >= 2 } && input.VelocityProfile.All(p => p is not null), "Il profilo della corrente richiede almeno due punti.");
                    foreach (var p in input.VelocityProfile) { Finite(p.ElevationM, "Quota corrente"); Finite(p.VelocityMs, "Velocità corrente"); }
                    Ordered(input.VelocityProfile.Select(p => p.ElevationM).ToArray(), input.BottomElevationM, input.TopElevationM, "velocità");
                }
            }
        }
        var result = new HydraulicActionsResult();
        result.Summary.Standard = "Idrostatica e modello di resistenza stazionaria · Italia, NTC 2018 §§5.1.2.3 e 5.1.3.8 per le pile in alveo";
        result.Summary.Scope = input.Element switch
        {
            HydraulicElement.VerticalWall => "Parete piana verticale: pressione netta, risultante e momento alla base.",
            HydraulicElement.HorizontalSlab => "Platea rettangolare orizzontale: sottospinta meno pressione superiore, escluse le azioni del peso proprio.",
            _ => "Pila verticale: resistenza stazionaria della corrente sulla larghezza proiettata assegnata."
        };
        result.Summary.Notes.Add("Scenari alternativi, non da sommare. Risultati non maggiorati: natura dell'azione e combinazioni da definire per il progetto. Livelli, quote e profili usano un unico riferimento geometrico.");
        result.Summary.Notes.Add("Il foglio non determina piene, rigurgito, filtrazione, scalzamento, onde o urti di detriti. Per le pile in alveo, livelli, velocità e geometria esposta devono provenire dallo studio di compatibilità idraulica NTC 2018 §5.1.2.3.");
        result.Summary.Notes.Add("Convenzioni: parete positiva da sinistra a destra; platea positiva verso l'alto; corrente con il segno della velocità. Il momento riportato è il primo momento con lo stesso segno della forza, rispetto alla base o a x=0.");
        if (!string.IsNullOrWhiteSpace(input.ProjectSource)) result.Summary.Notes.Add("Studio del progetto: " + input.ProjectSource);
        result.Summary.Parameters.Add(C.V("γw", "Peso specifico del fluido", input.UnitWeightKnM3, "kN/m³", "assegnato", "Dato del progetto"));
        result.Summary.Parameters.Add(C.V("b", "Larghezza caricata / proiettata", input.WidthM, "m", "assegnata", "Geometria"));
        if (input.Element == HydraulicElement.RiverPier)
        {
            result.Summary.Parameters.Add(C.V("ρ", "Densità coerente con il peso specifico", input.UnitWeightKnM3 * 1000 / Gravity, "kg/m³", "γw × 1000 / 9,81", "g = 9,81 m/s²"));
            result.Summary.Parameters.Add(C.V("Cd", "Coefficiente di resistenza assegnato", input.DragCoefficient, "", "assegnato", input.DragSource));
            result.Summary.Notes.Add("Il momento della corrente integra esattamente ogni tratto a velocità lineare, separando le inversioni di segno. L'eventuale spinta di Archimede non è aggiunta automaticamente alla pila vincolata al fondale.");
        }
        if (input.Element == HydraulicElement.HorizontalSlab && input.Uplift == UpliftDistribution.AssignedPressureProfile)
            result.Summary.Notes.Add("Profilo di sottospinta: " + input.PressureSource + ". La pressione superiore idrostatica viene sottratta separatamente.");
        foreach (var scenario in input.WaterCases)
        {
            var row = input.Element switch
            {
                HydraulicElement.VerticalWall => Wall(input, scenario),
                HydraulicElement.HorizontalSlab => Slab(input, scenario),
                _ => Pier(input, scenario)
            };
            row.Name = scenario.Name.Trim();
            row.NetForceKn = row.PositiveForceKn - row.OpposingForceKn;
            double scale = Math.Max(1, Math.Abs(row.PositiveForceKn) + Math.Abs(row.OpposingForceKn));
            if (Math.Abs(row.NetForceKn) > 1e-12 * scale)
                row.ApplicationCoordinateM = row.MomentKnM / row.NetForceKn + (input.Element == HydraulicElement.HorizontalSlab ? 0 : input.BottomElevationM);
            result.Cases.Add(row);
            var c = C.Case(result.Summary, row.Name, result.Summary.Scope);
            string reference = input.Element == HydraulicElement.RiverPier ? Drag : Hydrostatic;
            c.Values.Add(C.V("F+", "Contributo nel verso positivo", row.PositiveForceKn, "kN", "∫ p+ b ds", reference));
            c.Values.Add(C.V("F−", "Contributo opposto, in modulo", row.OpposingForceKn, "kN", "∫ p− b ds", reference));
            c.Values.Add(C.V("F", "Risultante netta con segno", row.NetForceKn, "kN", "F+ − F−", Integration));
            c.Values.Add(C.V("M", "Momento con segno rispetto alla base / x=0", row.MomentKnM, "kN·m", "∫ (p+ − p−) b s ds", Integration));
            if (row.ApplicationCoordinateM.HasValue)
                c.Values.Add(C.V(input.Element == HydraulicElement.HorizontalSlab ? "xR" : "zR", "Coordinata della risultante", row.ApplicationCoordinateM.Value, "m", input.Element == HydraulicElement.HorizontalSlab ? "M/F" : "zbase + M/F", Integration));
            else c.Description += " Risultante nulla entro la tolleranza: il punto di applicazione non è definito; il momento resta disponibile.";
        }
        return result;
    }

    private static HydraulicCaseResult Wall(HydraulicActionsInput x, HydraulicWaterCase c)
    {
        var nodes = new[] { x.BottomElevationM, x.TopElevationM, c.LeftLevelM, c.RightLevelM }
            .Where(z => z >= x.BottomElevationM && z <= x.TopElevationM).Distinct().OrderBy(z => z).ToArray();
        return LinearDiagram(nodes, z => x.UnitWeightKnM3 * Math.Max(0, c.LeftLevelM - z), z => x.UnitWeightKnM3 * Math.Max(0, c.RightLevelM - z), x.WidthM, x.BottomElevationM);
    }
    private static HydraulicCaseResult Slab(HydraulicActionsInput x, HydraulicWaterCase c)
    {
        double upper = x.UnitWeightKnM3 * Math.Max(0, c.RightLevelM - x.BottomElevationM - x.SlabThicknessM);
        if (x.Uplift == UpliftDistribution.HydrostaticHead)
            return LinearDiagram(new[] { 0.0, x.SlabLengthM }, _ => x.UnitWeightKnM3 * Math.Max(0, c.LeftLevelM - x.BottomElevationM), _ => upper, x.WidthM, 0);
        return LinearDiagram(x.PressureProfile.Select(p => p.PositionM).ToArray(), s => Interpolate(x.PressureProfile.Select(p => p.PositionM).ToArray(), x.PressureProfile.Select(p => p.PressureKpa).ToArray(), s) * c.ProfileMultiplier, _ => upper, x.WidthM, 0);
    }
    private static HydraulicCaseResult LinearDiagram(double[] nodes, Func<double, double> positive, Func<double, double> opposite, double width, double origin)
    {
        var r = new HydraulicCaseResult();
        foreach (double s in nodes) r.Diagram.Add(Point(s, positive(s), opposite(s), width));
        for (int i = 1; i < nodes.Length; i++)
        {
            var a = r.Diagram[i - 1]; var b = r.Diagram[i]; double length = b.PositionM - a.PositionM;
            r.PositiveForceKn += width * length * (a.PositivePressureKpa + b.PositivePressureKpa) / 2;
            r.OpposingForceKn += width * length * (a.OpposingPressureKpa + b.OpposingPressureKpa) / 2;
            double p0 = a.NetPressureKpa, p1 = b.NetPressureKpa;
            r.MomentKnM += width * (length * (a.PositionM - origin) * (p0 + p1) / 2 + length * length * (p0 + 2 * p1) / 6);
        }
        return r;
    }
    private static HydraulicCaseResult Pier(HydraulicActionsInput x, HydraulicWaterCase c)
    {
        var r = new HydraulicCaseResult(); double bottom = x.BottomElevationM, top = Math.Min(x.TopElevationM, c.LeftLevelM);
        if (top <= bottom) { r.Diagram.Add(Point(bottom, 0, 0, x.WidthM)); r.Diagram.Add(Point(x.TopElevationM, 0, 0, x.WidthM)); return r; }
        double[] z = x.UseVelocityProfile ? x.VelocityProfile.Select(p => p.ElevationM).ToArray() : new[] { bottom, x.TopElevationM };
        double[] v = x.UseVelocityProfile ? x.VelocityProfile.Select(p => p.VelocityMs * c.ProfileMultiplier).ToArray() : new[] { c.CurrentVelocityMs, c.CurrentVelocityMs };
        var nodes = z.Where(s => s > bottom && s < top).Concat(new[] { bottom, top }).ToList();
        for (int i = 1; i < z.Length; i++)
            if (v[i - 1] * v[i] < 0)
            { double crossing = z[i - 1] - v[i - 1] * (z[i] - z[i - 1]) / (v[i] - v[i - 1]); if (crossing > bottom && crossing < top) nodes.Add(crossing); }
        double[] breaks = nodes.Distinct().OrderBy(s => s).ToArray();
        double coefficient = .5 * x.UnitWeightKnM3 / Gravity * x.DragCoefficient;
        double Pressure(double s) { double velocity = Interpolate(z, v, s); return coefficient * velocity * Math.Abs(velocity); }
        foreach (double s in breaks)
        { double p = Pressure(s); r.Diagram.Add(Point(s, Math.Max(0, p), Math.Max(0, -p), x.WidthM)); }
        for (int i = 1; i < breaks.Length; i++)
        {
            double half = (breaks[i] - breaks[i - 1]) / 2, middle = (breaks[i] + breaks[i - 1]) / 2;
            foreach (double s in new[] { middle - half / Math.Sqrt(3), middle + half / Math.Sqrt(3) })
            {
                double force = half * x.WidthM * Pressure(s);
                r.PositiveForceKn += Math.Max(0, force); r.OpposingForceKn += Math.Max(0, -force);
                r.MomentKnM += force * (s - bottom);
            }
        }
        // Intermediate ordinates show the quadratic pressure within a linearly varying velocity segment.
        r.Diagram.Clear();
        for (int i = 1; i < breaks.Length; i++)
            for (int k = i == 1 ? 0 : 1; k <= 8; k++)
            { double s = breaks[i - 1] + (breaks[i] - breaks[i - 1]) * k / 8; double p = Pressure(s); r.Diagram.Add(Point(s, Math.Max(0, p), Math.Max(0, -p), x.WidthM)); }
        if (top < x.TopElevationM) { r.Diagram.Add(Point(top, 0, 0, x.WidthM)); r.Diagram.Add(Point(x.TopElevationM, 0, 0, x.WidthM)); }
        return r;
    }
    private static HydraulicDiagramPoint Point(double s, double p, double other, double width) => new()
    { PositionM = s, PositivePressureKpa = p, OpposingPressureKpa = other, NetPressureKpa = p - other, NetLineLoadKnM = width * (p - other) };
    private static double Interpolate(double[] x, double[] y, double at)
    {
        if (at <= x[0]) return y[0];
        for (int i = 1; i < x.Length; i++) if (at <= x[i]) return y[i - 1] + (y[i] - y[i - 1]) * (at - x[i - 1]) / (x[i] - x[i - 1]);
        return y[y.Length - 1];
    }
    private static void Ordered(double[] values, double start, double end, string name)
    {
        C.Require(values[0] == start && values[values.Length - 1] == end, "Il profilo " + name + " deve coprire esattamente gli estremi della geometria.");
        for (int i = 1; i < values.Length; i++) C.Require(values[i] > values[i - 1], "Coordinate del profilo " + name + " non strettamente crescenti.");
    }
    private static void Finite(double value, string name) => C.Range(value, -double.MaxValue, double.MaxValue, name);
}
