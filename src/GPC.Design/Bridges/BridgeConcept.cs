namespace GPC.Design.Bridges;

public static partial class BridgeConcept
{
    public const string Scope = "Predimensionamento a spanne · geometria ideale, stime parametriche e carichi equivalenti. Non costituisce verifica NTC, Eurocodici o AASHTO.";
    public sealed record Parameter(string Key, string Label, string Unit, double Default, double Min, double Max, double Step = .1);
    public sealed record Family(string Id, string Name, double MinSpan, double MaxSpan, double Ratio, double MinDepth, double Slab, double Spacing, bool Steel = false, bool Prestressed = false);
    public static readonly IReadOnlyList<Family> Families = Array.AsReadOnly(new Family[] { 
        new("slab", "Soletta piena in c.a.", 6, 15, 18, .35, 0, 1),
        new("tee", "Travi a T in c.a.", 12, 30, 17, .70, .22, 2.5),
        new("psc_i", "Travi a I in c.a.p.", 20, 50, 22.22, 1, .22, 2.5, Prestressed: true),
        new("psc_u", "Travi a U in c.a.p.", 25, 50, 22.22, 1.1, .22, 4, Prestressed: true),
        new("psc_box", "Cassone in c.a.p.", 35, 80, 22.22, 1.3, .25, 0, Prestressed: true),
        new("fcm", "Cassone a conci · altezza variabile", 80, 200, 45, 2, .28, 0, Prestressed: true),
        new("steel_i", "Travi a I acciaio–cls", 30, 90, 25, 1, .25, 3.2, Steel: true),
        new("steel_box", "Cassone acciaio–cls", 40, 150, 25, 1.2, .25, 0, Steel: true),
        new("filler_beam", "Travi incorporate nel calcestruzzo", 8, 40, 28, .45, 0, .8, Steel: true),
        new("orthotropic", "Cassone con piastra ortotropa", 40, 200, 30, 1.2, 0, 0, Steel: true),
        new("tied_arch", "Arco metallico con catena", 40, 250, 120, .8, .25, 3, Steel: true),
        new("cable_stayed", "Ponte strallato · due antenne", 100, 700, 150, 1, .25, 3, Steel: true),
        new("suspension", "Ponte sospeso · ancoraggi a terra", 200, 1200, 200, 1.2, 0, 3, Steel: true),
        new("truss", "Ponte reticolare metallico", 30, 150, 100, .7, .25, 3, Steel: true)
     });
    public static readonly IReadOnlyList<Parameter> Site = Array.AsReadOnly(new Parameter[] { 
        new("length", "Lunghezza totale", "m", 105, 10, 2000, 5),
        new("height", "Quota impalcato sul terreno", "m", 20, 3, 100, 1),
        new("obstacle_width", "Larghezza ostacolo", "m", 20, 0, 1000, 1),
        new("lanes", "Corsie complessive", "n.", 4, 1, 8, 1),
        new("lane_width", "Larghezza corsia", "m", 3.65, 2.5, 4.5, .05),
        new("shoulder", "Banchina per lato", "m", 1.5, 0, 4, .1),
        new("median", "Spartitraffico", "m", 1.6, 0, 5, .1),
        new("barrier_width", "Ingombro barriera per lato", "m", .5, .2, 1.5, .05)
     });
    // Zero denotes an automatic dimension. It is preserved in the archive, never replaced by a computed value.
    public static readonly IReadOnlyList<Parameter> Layout = Array.AsReadOnly(new Parameter[] { new("spans", "Numero campate · 0 = auto", "n.", 5, 0, 30, 1) });
    public static readonly IReadOnlyList<Parameter> Section = Array.AsReadOnly(new Parameter[] { 
        new("depth", "Altezza totale · 0 = auto", "m", 0, 0, 15, .05),
        new("slab", "Spessore soletta · 0 = auto", "m", 0, 0, 1, .01),
        new("spacing", "Interasse travi · 0 = auto", "m", 0, 0, 8, .1),
        new("web", "Spessore anima cls · 0 = auto", "m", 0, 0, 1.5, .01),
        new("bottom", "Soletta inferiore · 0 = auto", "m", 0, 0, 1.5, .01),
        new("bottom_ratio", "Larghezza fondo / impalcato", "—", .55, .3, .85, .05),
        new("cells", "Celle del cassone", "n.", 2, 1, 5, 1),
        new("boxes", "Cassoni metallici · 0 = auto", "n.", 0, 0, 6, 1),
        new("flange_width", "Larghezza piattabanda", "m", .55, .2, 2, .05),
        new("flange_mm", "Spessore piattabande", "mm", 24, 8, 150, 1),
        new("web_mm", "Spessore anima acciaio", "mm", 12, 6, 80, 1),
        new("web_slope", "Inclinazione anime · H per 4V", "—", 1, 0, 2, .1),
        new("haunch", "Rialzo sotto soletta", "m", .05, 0, .3, .01),
        new("u_top", "Larghezza superiore U", "m", 2.4, 1, 6, .1),
        new("u_bottom", "Larghezza inferiore U", "m", 1.4, .5, 5, .1),
        new("fc", "Resistenza cls impalcato", "MPa", 45, 20, 90, 5)
     });
    public static readonly IReadOnlyList<Parameter> Substructure = Array.AsReadOnly(new Parameter[] { 
        new("pile_length", "Lunghezza pali · 0 = auto", "m", 0, 0, 80, 1),
        new("pier_size", "Diametro / spessore pila · 0 = auto", "m", 0, 0, 8, .1),
        new("pile_count", "Pali per appoggio · 0 = auto", "n.", 0, 0, 64, 1),
        new("footing_size", "Lato fondazione · 0 = auto", "m", 0, 0, 40, .25),
        new("fc_sub", "Resistenza cls sottostrutture", "MPa", 35, 20, 60, 5)
     });
    public static readonly IReadOnlyList<Parameter> Rates = Array.AsReadOnly(new Parameter[] { 
        new("concrete_deck", "Cls impalcato", "€/m³", 260, 0, 10000, 5),
        new("concrete_sub", "Cls sottostrutture e plinti", "€/m³", 240, 0, 10000, 5),
        new("rebar", "Armatura ordinaria", "€/t", 1660, 0, 50000, 50),
        new("prestress", "Acciaio da precompressione", "€/t", 3600, 0, 50000, 50),
        new("steel", "Carpenteria metallica", "€/t", 3500, 0, 50000, 50),
        new("steel_box", "Carpenteria cassoni", "€/t", 4000, 0, 50000, 50),
        new("formwork", "Casseforme", "€/m²", 50, 0, 1000, 5),
        new("pile_1", "Palo Ø 1,0 · esclusa armatura", "€/m", 300, 0, 10000, 10),
        new("pile_15", "Palo Ø 1,5 · esclusa armatura", "€/m", 550, 0, 10000, 10),
        new("bearing", "Apparecchio d'appoggio", "€/cad", 5000, 0, 100000, 100),
        new("joint", "Giunto di dilatazione", "€/m", 2400, 0, 50000, 100),
        new("barrier", "Barriera", "€/m", 360, 0, 10000, 10),
        new("surfacing", "Pavimentazione", "€/m²", 32, 0, 1000, 1),
        new("steel_ortho", "Carpenteria impalcato ortotropo", "€/t", 5000, 0, 50000, 50),
        new("cables", "Cavi e pendini installati, inclusi terminali", "€/t", 16000, 0, 100000, 100),
        new("erection_special", "Montaggio aggiuntivo strutture speciali", "€/t", 1000, 0, 50000, 50)
     });
    public static readonly IReadOnlyList<Parameter> Assumptions = Array.AsReadOnly(new Parameter[] { 
        new("prelims", "Oneri aggiuntivi non computati", "%", 12, 0, 100, 1),
        new("contingency", "Imprevisti", "%", 15, 0, 100, 1),
        new("uncertainty", "Intervallo indicativo costo ±", "%", 30, 0, 90, 5),
        new("co2_concrete", "CO₂ cls ordinario", "kg/m³", 320, 0, 2000, 10),
        new("co2_rebar", "CO₂ armatura", "kg/kg", 1.4, 0, 10, .1),
        new("co2_steel", "CO₂ carpenteria", "kg/kg", 2, 0, 10, .1),
        new("co2_pt", "CO₂ precompressione", "kg/kg", 2.5, 0, 10, .1),
        new("co2_site", "Trasporti e cantiere / materiali", "%", 15, 0, 100, 1),
        new("low_carbon_factor", "Fattore cls a ridotta CO₂", "—", .6, .1, 1, .05),
        new("recycled_factor", "Fattore acciaio riciclato", "—", .35, .1, 1, .05),
        new("rebar_rc", "Armatura impalcato c.a.", "kg/m³", 140, 30, 500, 10),
        new("rebar_psc", "Armatura impalcato c.a.p.", "kg/m³", 110, 30, 500, 10),
        new("rebar_sub", "Armatura sottostrutture", "kg/m³", 150, 30, 500, 10),
        new("rebar_found", "Armatura fondazioni", "kg/m³", 120, 30, 500, 10),
        new("pt_density", "Precompressione", "kg/m³", 30, 5, 150, 5),
        new("wear_load", "Permanenti portati", "kN/m²", 2.5, 0, 20, .5),
        new("traffic_load", "Traffico uniforme equivalente", "kN/m²", 9, 0, 50, .5),
        new("gamma_g", "Moltiplicatore permanenti", "—", 1.35, 1, 2, .05),
        new("gamma_q", "Moltiplicatore traffico", "—", 1.5, 1, 2, .05),
        new("soil_pressure", "Pressione terreno · 0 = classe", "kPa", 0, 0, 5000, 25),
        new("pile_skin", "Resistenza laterale · 0 = classe", "kPa", 0, 0, 500, 5),
        new("pile_base", "Resistenza punta · 0 = classe", "kPa", 0, 0, 20000, 100),
        new("months_base", "Durata: avvio cantiere", "mesi", 4, 0, 36, .5),
        new("months_span", "Durata per campata", "mesi", 1.2, .1, 12, .1),
        new("months_pier", "Durata per pila", "mesi", .4, 0, 6, .1)
     });
    public static readonly IReadOnlyList<string> Obstacles = Array.AsReadOnly(new string[] { "Nessuno", "Fiume", "Strada / ferrovia" });
    public static readonly IReadOnlyList<string> Soils = Array.AsReadOnly(new string[] { "Roccia", "Sabbia / ghiaia densa", "Terreno medio", "Argilla soffice" });
    public static readonly IReadOnlyList<string> Piers = Array.AsReadOnly(new string[] { "Telaio a colonne", "Colonna circolare", "Setto", "Testa a martello" });
    public static readonly IReadOnlyList<string> Foundations = Array.AsReadOnly(new string[] { "Automatica", "Plinto diretto", "Pali Ø 1,0 m", "Pali Ø 1,5 m" });
    public static readonly IReadOnlyList<string> DeckTypes = Array.AsReadOnly(new string[] { "Soletta in cls", "Piastra ortotropa" });
    public static bool IsExtended(string family) => family is "filler_beam" or "orthotropic" or "tied_arch" or "cable_stayed" or "suspension" or "truss";
    public static bool HasUpperStructure(string family) => family is "tied_arch" or "cable_stayed" or "suspension" or "truss";
    public static bool HasTowers(string family) => family is "cable_stayed" or "suspension";
    public static readonly IReadOnlyList<Parameter> AdvancedSection = Array.AsReadOnly(new Parameter[] { 
        new("arch_rise", "Freccia arco / luce", "—", .2, .1, .35, .01),
        new("tower_height", "Antenna sopra impalcato · 0 = auto", "m", 0, 0, 300, 1),
        new("cable_sag", "Freccia cavo / luce centrale", "—", .1, .06, .2, .01),
        new("suspender_spacing", "Passo indicativo pendini / stralli", "m", 8, 3, 20, 1),
        new("truss_ratio", "Altezza reticolare / luce", "—", .12, .08, .25, .01),
        new("embedded_cover", "Cls sopra e sotto trave incorporata", "m", .06, .03, .15, .01),
        new("deck_plate_mm", "Lamiera piastra ortotropa", "mm", 16, 12, 40, 1),
        new("rib_height", "Altezza canaletta ortotropa", "m", .3, .15, .6, .01),
        new("rib_top", "Larghezza superiore canaletta", "m", .3, .15, .6, .01),
        new("rib_bottom", "Larghezza inferiore canaletta", "m", .18, .08, .45, .01),
        new("rib_mm", "Spessore canaletta", "mm", 8, 6, 20, 1),
        new("rib_spacing", "Interasse canalette", "m", .6, .3, 1.2, .05)
     });
    public static readonly IReadOnlyList<Parameter> AdvancedAssumptions = Array.AsReadOnly(new Parameter[] { 
        new("concept_tension", "Tensione di riferimento carpenteria", "MPa", 180, 50, 300, 10),
        new("concept_compression", "Compressione di riferimento archi / aste", "MPa", 100, 30, 200, 10),
        new("concept_cable", "Tensione di riferimento cavi", "MPa", 600, 200, 900, 25),
        new("concept_tower", "Compressione di riferimento antenne cls", "MPa", 6, 2, 12, .5),
        new("anchor_friction", "Attrito convenzionale blocchi ancoraggio", "—", .5, .2, .8, .05),
        new("special_connections", "Aggiunta collegamenti arco / reticolare", "%", 20, 0, 50, 5),
        new("co2_cable", "CO₂ cavi e pendini", "kg/kg", 2.5, 0, 10, .1)
     });
    public sealed record StructuralLine(string Kind, double X1, double Z1, double X2, double Z2, int Count, double Area, double Force)
    { public double Length => Math.Sqrt(Math.Pow(X2 - X1, 2) + Math.Pow(Z2 - Z1, 2)); public double Mass => Count * Area * Length * SteelTonnesPerCubicMetre; }
    public sealed record AdvancedGeometry(bool Orthotropic, bool GlobalBeam, double MainSpan, double Rise, double TowerHeight,
        double DeckSteel, double StructureSteel, double CableSteel, double AnchorVolume, double TowerVolume, double HorizontalForce,
        IReadOnlyList<StructuralLine> Members, IReadOnlyList<TechnicalItem> Dimensions)
    { public IReadOnlyList<SupportGeometry> FoundationSchedule { get; init; } = []; }

    public static double ReferenceDepth(Family family, double maxSpan, bool continuous) => Math.Max(family.MinDepth,
        maxSpan / family.Ratio * (HasUpperStructure(family.Id) ? 1 : continuous ? .95 : 1.1));
    private static void Validate(BridgeModel data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(data.Input);ArgumentNullException.ThrowIfNull(data.Rates);ArgumentNullException.ThrowIfNull(data.Assumptions);
        void Range(Parameter p,double n)
        { if(!double.IsFinite(n)||n<p.Min||n>p.Max||p.Unit=="n."&&n!=Math.Truncate(n)) throw new ArgumentException($"{p.Label}: valore richiesto tra {p.Min} e {p.Max} {p.Unit}."); }
        foreach(var p in Site.Concat(Layout).Concat(Section).Concat(AdvancedSection).Concat(Substructure)) Range(p,data.Input.Value(p.Key));
        foreach(var p in Rates)Range(p,data.Rates.Value(p.Key));
        foreach(var p in Assumptions.Concat(AdvancedAssumptions))Range(p,data.Assumptions.Value(p.Key));
        var i=data.Input;
        if (!Enum.IsDefined(i.TrafficModel)) throw new ArgumentException("Modello di traffico non riconosciuto.");
        if(!DeckTypes.Contains(i.DeckType))throw new ArgumentException("Tipo di impalcato non riconosciuto.");
        if(!Families.Any(f=>f.Id==i.Family))throw new ArgumentException("Tipologia impalcato non riconosciuta.");
        foreach(var (key,value,choices) in new[]{("obstacle",i.Obstacle,Obstacles),("soil",i.Soil,Soils),("pier",i.Pier,Piers),("foundation",i.Foundation,Foundations)})
            if(!choices.Contains(value))throw new ArgumentException("Scelta non valida: "+key);
        if(i.Obstacle!="Nessuno"&&i.ObstacleWidth>=i.Length-4)throw new ArgumentException("L'ostacolo deve lasciare almeno 2 m per lato alle estremità del ponte.");
    }
    public sealed record Quantity(string Group, string Item, double Amount, string Unit, double Rate, double Cost, double Carbon);
    public sealed record Detail(string Name, double Value, string Unit, string Rule);
    public sealed record Station(int Span, double X, double Moment, double Shear, double DeflectionMm)
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public double? MomentMinimum { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public double? ShearMinimum { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public double? DeflectionMinimumMm { get; init; }
    }
    public sealed record Support(int Index, double X, string Type, double Reaction, double PierHeight, double PierSize, int Columns, double FootingSize, double FootingWidth, int Piles)
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public double? MinimumReaction { get; init; }
    }
    public sealed record Result(Family Family, double Width, double Depth, double PierDepth, double Slab, double Spacing, int Girders,
        double Web, double Bottom, IReadOnlyList<double> Spans, IReadOnlyList<Support> Supports, string Foundation, double PileDiameter, double PileLength,
        double Concrete, double Steel, double Rebar, double Prestress, double Inertia, double DirectCost, double TotalCost,
        double CostLow, double CostHigh, double Carbon, double Duration, IReadOnlyList<Quantity> Quantities, IReadOnlyList<Detail> Details, IReadOnlyList<Station> Stations, IReadOnlyList<string> Warnings)
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public BridgeTrafficSummary? Traffic { get; init; }
        public AdvancedGeometry? Advanced { get; init; }
        // Kept separate from the legacy result serialization: the explicit pricing document owns its persisted snapshot.
        [System.Text.Json.Serialization.JsonIgnore]
        public IReadOnlyList<GPC.Design.Costs.CostQuantity> CostQuantities { get; init; } = [];
        [System.Text.Json.Serialization.JsonIgnore]
        public string? PriceRevision { get; init; }
        public double Length => Spans.Sum();
        public double Area => Width * Length;
    }
}
