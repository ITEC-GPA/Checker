
namespace GPC.Design.Bridges;

public static partial class BridgeConcept
{
    public sealed record TechnicalItem(string Component, string Symbol, double Value, string Unit, string Origin, string Note);
    public sealed record SpanGeometry(int Number, double Start, double End, double Length, int Elements, double DevelopedLength);
    public sealed record SupportGeometry(int Number, string Type, double X, double Height, int Columns, double Size,
        double WallWidth, double CapLength, double CapWidth, double CapThickness, double FootingLength, double FootingWidth,
        double FootingThickness, int Piles, double PileDiameter, double PileLength);

    /// <summary>Adopted dimensions of the calculation, independent of formatting and UI.</summary>
    public static TechnicalItem[] TechnicalSchedule(BridgeModel data, Result r)
    {
        var i = data.Input; var rows = new List<TechnicalItem>();
        string Source(string key) => i.Value(key) > 0 ? "Impostato" : "Automatico";
        void Add(string component, string symbol, double value, string unit, string origin, string note = "") =>
            rows.Add(new(component, symbol, value, unit, origin, note));
        Add("Geometria generale", "L", r.Length, "m", "Impostato", "Lunghezza tra gli appoggi estremi");
        Add("Geometria generale", "W", r.Width, "m", "Derivato", "Corsie, banchine, spartitraffico e barriere");
        Add("Geometria generale", "N", r.Spans.Count, "n.", Source("spans"), "Numero campate");
        Add("Geometria generale", "Lmax", r.Spans.Max(), "m", "Derivato", "Luce massima tra gli assi degli appoggi");
        Add("Impalcato", "d", r.Depth, "m", Source("depth"), "Altezza totale in campata, inclusa soletta");
        Add("Impalcato", "d_pila", r.PierDepth, "m", r.Family.Id == "fcm" ? "Automatico" : "Derivato", "Altezza totale in corrispondenza delle pile");
        double equivalent = r.Family.Id == "fcm" ? r.Depth + (r.PierDepth - r.Depth) / 3 : r.Depth;
        if (r.Family.Id == "fcm") Add("Impalcato", "d_eq", equivalent, "m", "Derivato", "Altezza media usata per quantità e rigidezza: d + (d_pila − d)/3");
        Add(r.Advanced?.Orthotropic == true ? "Lamiera di impalcato" : "Soletta", "t_s", r.Slab * 1000, "mm", Source(r.Advanced?.Orthotropic == true ? "deck_plate_mm" : r.Family.Id is "slab" or "filler_beam" ? "depth" : "slab"), "Spessore effettivamente usato nel calcolo");
        Add("Materiali", "fc", i.Fc, "MPa", "Impostato", "Resistenza convenzionale del cls impalcato");
        Add("Materiali", "fc_sub", i.FcSub, "MPa", "Impostato", "Resistenza convenzionale del cls sottostrutture");
        if (r.Advanced is { } advanced) { rows.AddRange(advanced.Dimensions); return rows.ToArray(); }
        if (r.Family.Id == "slab") return rows.ToArray();
        string component = r.Family.Id is "psc_box" or "fcm" or "steel_box" ? "Cassone" : "Trave";
        Add(component, "n", r.Girders, "n.", r.Family.Id == "steel_box" ? Source("boxes") : "Derivato", "Elementi longitudinali per sezione trasversale");
        if (r.Spacing > 0) Add(component, "s_rif", r.Spacing, "m", Source("spacing"), "Interasse di riferimento per stimare n = floor(W/s), minimo 2; non definisce gli sbalzi laterali");
        double haunch = r.Family.Id == "psc_i" ? i.Haunch : 0;
        double h = equivalent - r.Slab - haunch;
        Add(component, "h", h, "m", "Derivato", "Altezza sotto soletta e rialzo; per FCM riferita alla sezione media");
        Add(component, "Σ n·Li", r.Length * r.Girders, "m", "Derivato", "Sviluppo longitudinale teorico complessivo; non distinta di fabbricazione");
        switch (r.Family.Id)
        {
            case "tee":
                Add(component, "b_w", r.Web * 1000, "mm", Source("web"), "Larghezza anima sotto soletta"); break;
            case "psc_i":
                double flange = Math.Min(.18, h / 4), bw = Math.Min(.7, r.Width / r.Girders * .7), wi = Math.Min(.2, bw / 2);
                Add(component, "b_f", bw * 1000, "mm", "Automatico", "Larghezza delle due flange rettangolari equivalenti");
                Add(component, "t_f", flange * 1000, "mm", "Automatico", "Spessore delle due flange; profilo ideale, non serie prefabbricata");
                Add(component, "b_w", wi * 1000, "mm", "Automatico", "Spessore anima del profilo ideale");
                Add(component, "h_w", h - 2 * flange, "m", "Derivato", "Altezza netta dell'anima");
                Add("Rialzo", "h_r", haunch * 1000, "mm", "Impostato", "Rialzo tra trave e soletta"); break;
            case "psc_u":
                Add(component, "b_sup", i.UTop, "m", "Impostato", "Larghezza superiore della U");
                Add(component, "b_inf", i.UBottom, "m", "Impostato", "Larghezza inferiore della U");
                Add(component, "t_w", r.Web * 1000, "mm", Source("web"), "Spessore delle due anime inclinate");
                Add(component, "t_inf", r.Bottom * 1000, "mm", Source("bottom"), "Spessore fondo");
                Add(component, "l_w", Math.Sqrt(Math.Pow(h - r.Bottom, 2) + Math.Pow((i.UTop - i.UBottom) / 2, 2)), "m", "Derivato", "Sviluppo di ciascuna anima usato nel volume"); break;
            case "psc_box": case "fcm":
                Add(component, "b_inf", r.Width * i.BottomRatio, "m", "Derivato", "Larghezza del fondo = W × rapporto impostato");
                Add(component, "n_celle", i.Cells, "n.", "Impostato", "Numero celle; anime = celle + 1");
                Add(component, "t_w", r.Web * 1000, "mm", Source("web"), "Spessore anime verticali del modello");
                Add(component, "t_inf", r.Bottom * 1000, "mm", Source("bottom"), "Spessore soletta inferiore");
                Add(component, "h_w", h - r.Bottom, "m", "Derivato", "Altezza netta anime alla sezione di calcolo"); break;
            case "steel_i": case "steel_box":
                double tf = i.FlangeMm / 1000, wh = h - 2 * tf;
                Add(component, "b_f", i.FlangeWidth * 1000, "mm", "Impostato", r.Family.Id == "steel_box" ? "Larghezza di ciascuna delle due piattabande superiori" : "Larghezza di ciascuna piattabanda");
                Add(component, "t_f", i.FlangeMm, "mm", "Impostato", "Spessore piattabande e, per il cassone, della lamiera di fondo");
                Add(component, "t_w", i.WebMm, "mm", "Impostato", "Spessore lamiera d'anima");
                Add(component, "h_w", wh, "m", "Derivato", "Altezza verticale netta tra piattabande");
                if (r.Family.Id == "steel_box")
                {
                    double run = wh * i.WebSlope / 4;
                    Add(component, "b_inf", r.Width * .4 / r.Girders, "m", "Automatico", "Larghezza lamiera inferiore per cassone = 0,4 W/n");
                    Add(component, "H/4V", i.WebSlope, "—", "Impostato", "0 = anime verticali; 1 = inclinazione 1H:4V");
                    Add(component, "Δx", run, "m", "Derivato", "Scarto orizzontale di ciascuna anima");
                    Add(component, "l_w", Math.Sqrt(wh * wh + run * run), "m", "Derivato", "Sviluppo reale dell'anima inclinata usato nella massa");
                }
                Add("Carpenteria", "m_tot", r.Steel, "t", "Derivato", "Include maggiorazione 15% per diaframmi, irrigidimenti e collegamenti; non inclusa nella rigidezza"); break;
        }
        return rows.ToArray();
    }

    public static SpanGeometry[] SpanSchedule(Result r) => r.Spans.Select((l, k) =>
        new SpanGeometry(k + 1, r.Supports[k].X, r.Supports[k + 1].X, l, r.Girders, l * r.Girders)).ToArray();

    public static SupportGeometry[] SupportSchedule(BridgeModel data, Result r) => r.Advanced?.FoundationSchedule.ToArray() ?? r.Supports.Select(s =>
    {
        bool abutment = s.Type == "Spalla", hammer = s.Type == "Testa a martello";
        return new SupportGeometry(s.Index + 1, s.Type, s.X, s.PierHeight, abutment ? 0 : s.Columns, s.PierSize,
            s.Type == "Setto" ? Math.Max(1, r.Width - 2) : 0, abutment ? 0 : r.Width, abutment ? 0 : hammer ? 2 : 1.5,
            abutment ? 0 : hammer ? 1.8 : 1.4, s.FootingSize, s.FootingWidth,
            r.PileDiameter > 0 ? 1.5 * r.PileDiameter : Math.Max(.6, s.FootingSize / 6), s.Piles, r.PileDiameter, r.PileLength);
    }).ToArray();
}
