using GPC.Design.Costs;

namespace GPC.Design.Bridges;

public static partial class BridgeConcept
{
    public static Result Calculate(BridgeModel data)
    {
        var result = CalculateCore(data);
        return Own(data.Prices is null ? result : ApplyPrices(result, data.Assumptions, data.Prices));
    }

    private static Result CalculateCore(BridgeModel data)
    {
        Validate(data);
        if (IsExtended(data.Input.Family)) return CalculateAdvanced(data);
        var i = data.Input; var a = data.Assumptions; var rates = data.Rates;
        var family = Families.Single(f => f.Id == i.Family); var warnings = new List<string>();
        double length = i.Length, height = i.Height;
        double width = i.Lanes * i.LaneWidth + 2 * i.Shoulder + i.Median + 2 * i.BarrierWidth;
        int count = (int)i.Spans;
        if (count == 0) count = PortableMath.Clamp((int)Math.Ceiling(length / ((family.MinSpan + family.MaxSpan) / 2)), 1, 30);
        double[] weights = Enumerable.Range(0, count).Select(k => i.Continuous && count > 2 && (k == 0 || k == count - 1) ? .8 : 1).ToArray();
        double[] positions = new double[count + 1];
        for (int k = 0; k < count; k++) positions[k + 1] = positions[k] + length * weights[k] / weights.Sum();
        positions[positions.Length - 1] = length;
        bool obstacle = i.Obstacle != "Nessuno" && i.ObstacleWidth > 0;
        if (obstacle)
        {
            double left = (length - i.ObstacleWidth) / 2 - 1, right = length - left;
            var proposed = (double[])positions.Clone();
            for (int k = 1; k < count; k++)
                if (proposed[k] > left && proposed[k] < right) proposed[k] = proposed[k] < length / 2 ? left : right;
            // Never merge supports or create zero-length spans to make the sketch fit an obstacle.
            if (Enumerable.Range(0, count).All(k => proposed[k + 1] - proposed[k] >= 2)) positions = proposed;
            else warnings.Add("Ostacolo interferente con le pile: ridurre il numero di campate o rivedere il tracciato. Disposizione automatica non risolta.");
        }
        var spans = Enumerable.Range(0, count).Select(k => positions[k + 1] - positions[k]).ToArray();
        if (spans.Min() < 2) throw new ArgumentException("Le campate devono avere lunghezza non inferiore a 2 m.");
        double maxSpan = spans.Max();
        if (maxSpan > family.MaxSpan || spans.Min() < family.MinSpan)
            warnings.Add($"Tipologia fuori dal campo usuale {family.MinSpan:0}–{family.MaxSpan:0} m: campate {spans.Min():0.0}–{maxSpan:0.0} m.");
        double depth = i.Depth > 0 ? i.Depth : Math.Max(family.MinDepth, maxSpan / family.Ratio * (i.Continuous ? .95 : 1.1));
        double pierDepth = family.Id == "fcm" ? Math.Max(depth, maxSpan / 18) : depth;
        double slab = family.Id == "slab" ? depth : Auto(i.Slab, family.Slab);
        double web = Auto(i.Web, family.Id == "tee" ? .3 : .35), bottom = Auto(i.Bottom, .22);
        double spacing = Auto(i.Spacing, family.Spacing);
        int girders = family.Id is "psc_box" or "fcm" or "slab" ? 1 : family.Id == "steel_box" ? (int)Auto(i.Boxes, Math.Max(1, Math.Round(width / 10))) : Math.Max(2, (int)Math.Floor(width / spacing));
        if (girders > 64) throw new ArgumentException("Interasse troppo piccolo: sono previste più di 64 travi.");
        if (family.Id != "slab" && depth <= slab + (family.Steel ? 2 * i.FlangeMm / 1000 : bottom) + .1)
            throw new ArgumentException("Altezza impalcato insufficiente per soletta, fondo / piattabande e anima.");
        if (i.UBottom > i.UTop && family.Id == "psc_u") throw new ArgumentException("La larghezza inferiore della U non può superare quella superiore.");
        double effectiveDepth = family.Id == "fcm" ? depth + (pierDepth - depth) / 3 : depth;
        // Rectangular components give consistent gross section quantities and stiffness. Sloped webs use their true length in mass only.
        var parts = new List<(double Area, double Y, double Inertia, double Modular)>();
        double ec = ConcreteModulus(i.Fc), es = 200000, n = es / ec;
        void Rect(double b, double h, double y, int repeat = 1, bool steel = false)
        { if (b > 0 && h > 0) parts.Add((b * h * repeat, y, b * h * h * h / 12 * repeat, steel ? n : 1)); }
        Rect(width, slab, effectiveDepth - slab / 2);
        double girderArea = 0, steelArea = 0;
        double haunch = family.Id == "psc_i" ? i.Haunch : 0;
        double h = effectiveDepth - slab - haunch, bf = i.FlangeWidth, tf = i.FlangeMm / 1000, tw = i.WebMm / 1000;
        if (family.Id != "slab" && h <= Math.Max(.15, 2 * tf)) throw new ArgumentException("Spazio insufficiente per le travi sotto la soletta e il rialzo.");
        switch (family.Id)
        {
            case "tee":
                Rect(web, h, h / 2, girders); girderArea = web * h * girders; break;
            case "psc_i":
                double flange = Math.Min(.18, h / 4), bw = Math.Min(.7, width / girders * .7), wi = Math.Min(.2, bw / 2);
                Rect(bw, flange, flange / 2, girders); Rect(bw, flange, h - flange / 2, girders);
                Rect(wi, h - 2 * flange, h / 2, girders); Rect(bw, haunch, h + haunch / 2, girders);
                girderArea = girders * (2 * bw * flange + wi * (h - 2 * flange) + bw * haunch); break;
            case "psc_u":
                double ub = i.UBottom, ut = i.UTop;
                if (ut * girders > width) warnings.Add("Le travi a U si sovrappongono: aumentare l'interasse o ridurre la larghezza superiore.");
                Rect(ub, bottom, bottom / 2, girders);
                double uWebLength = Math.Sqrt(Math.Pow(h - bottom, 2) + Math.Pow((ut - ub) / 2, 2));
                parts.Add((2 * girders * web * uWebLength, (h + bottom) / 2, 2 * girders * web * uWebLength * Math.Pow(h - bottom, 2) / 12, 1));
                girderArea = girders * (ub * bottom + 2 * web * Math.Sqrt(Math.Pow(h - bottom, 2) + Math.Pow((ut - ub) / 2, 2))); break;
            case "psc_box": case "fcm":
                double boxWidth = width * i.BottomRatio; int webs = (int)i.Cells + 1;
                if (web * webs >= boxWidth) throw new ArgumentException("Spessori delle anime incompatibili con la larghezza del cassone.");
                Rect(boxWidth, bottom, bottom / 2); Rect(web, h - bottom, (h + bottom) / 2, webs);
                girderArea = boxWidth * bottom + webs * web * (h - bottom); break;
            case "steel_i":
                Rect(bf, tf, tf / 2, girders, true); Rect(bf, tf, h - tf / 2, girders, true); Rect(tw, h - 2 * tf, h / 2, girders, true);
                steelArea = girders * (2 * bf * tf + tw * (h - 2 * tf)); break;
            case "steel_box":
                double sb = width * .4 / girders;
                double wh = h - 2 * tf, run = wh * i.WebSlope / 4, wl = Math.Sqrt(wh * wh + run * run);
                Rect(sb, tf, tf / 2, girders, true); Rect(bf, tf, h - tf / 2, 2 * girders, true);
                parts.Add((2 * girders * tw * wl, h / 2, 2 * girders * tw * wl * wh * wh / 12, n));
                steelArea = girders * (sb * tf + 2 * bf * tf + 2 * tw * wl);
                if (sb + 2 * run + bf > width / girders) warnings.Add("Le anime inclinate / piattabande dei cassoni superano lo spazio disponibile.");
                break;
        }
        double areaConcrete = width * slab + girderArea, volumeDeck = areaConcrete * length;
        // 15% accounts for diaphragms, stiffeners and connections in concept steel tonnage, not flexural stiffness.
        double massSteel = steelArea * length * SteelTonnesPerCubicMetre * 1.15;
        double centroid = parts.Sum(p => p.Area * p.Modular * p.Y) / parts.Sum(p => p.Area * p.Modular);
        double inertia = parts.Sum(p => p.Modular * (p.Inertia + p.Area * Math.Pow(p.Y - centroid, 2)));
        double deckRebar = volumeDeck * (family.Steel ? 120 : family.Prestressed ? a.RebarPsc : a.RebarRc) / 1000;
        double pt = family.Prestressed ? volumeDeck * a.PtDensity / 1000 : 0;
        double permanent = areaConcrete * 25 + massSteel / length * 9.81 + width * a.WearLoad + (i.Median > 0 ? 3 : 2) * 8;
        double live = width * a.TrafficLoad, q = permanent + live;
        var service = Loads(data, spans, permanent, ec * 1000 * inertia, false);
        var factored = Loads(data, spans, permanent, ec * 1000 * inertia, true);
        int soil = Soils.ToList().IndexOf(i.Soil);
        double pressure = Auto(a.SoilPressure, new double[] { 1000, 400, 200, 100 }[soil]);
        double skin = Auto(a.PileSkin, new double[] { 150, 70, 45, 25 }[soil]);
        double tip = Auto(a.PileBase, new double[] { 8000, 2500, 1500, 500 }[soil]);
        string foundation = i.Foundation;
        if (foundation == "Automatica") foundation = soil == 0 || soil == 1 && height < 15 ? "Plinto diretto" : "Pali Ø 1,0 m";
        double pileDiameter = foundation == "Plinto diretto" ? 0 : foundation == "Pali Ø 1,5 m" ? 1.5 : 1;
        double pileLength = pileDiameter == 0 ? 0 : Auto(i.PileLength, new double[] { 10, 15, 22, 30 }[soil]);
        double pileResistance = pileDiameter == 0 ? 0 : Math.PI * pileDiameter * pileLength * skin + Math.PI * pileDiameter * pileDiameter / 4 * tip;
        double subVolume = 0, foundVolume = 0, pilesVolume = 0, totalPileLength = 0, maxFoundationRatio = 0;
        var supports = new List<Support>(); double maxSlenderness = 0, maxPierStressRatio = 0;
        double subForms = 0, foundationForms = 0;
        for (int k = 0; k <= count; k++)
        {
            bool pier = k > 0 && k < count || k == 0 && i.StartPier || k == count && i.EndPier;
            double ph = pier ? height - pierDepth : Math.Min(7, height - depth);
            if (ph < 1) throw new ArgumentException("Quota dell'impalcato insufficiente per altezza della sezione e appoggi.");
            int columns = pier && i.Pier == "Telaio a colonne" ? Math.Max(2, (int)Math.Ceiling(width / 7)) : 1;
            bool wall = i.Pier == "Setto";
            double capVolume = width * (i.Pier == "Testa a martello" ? 2 * 1.8 : 1.5 * 1.4);
            double size = AutoPierSize(i, ph, service.Reactions[k], capVolume, width, columns, wall);
            double columnArea = wall ? size * Math.Max(1, width - 2) : Math.PI * size * size / 4 * columns;
            double sv = pier ? columnArea * ph + capVolume : width * (ph * Math.Max(.6, ph / 7) + 3);
            double reaction = service.Reactions[k], axial = reaction + 25 * sv;
            if (reaction < -1e-8) warnings.Add($"Appoggio {k + 1}: reazione verso l'alto; vincoli e dispositivi antisollevamento non sono dimensionati dal modello.");
            if (axial <= 0)
                throw new ArgumentException($"Appoggio {k + 1}: carico assiale di fondazione nullo o di sollevamento. Il modello dimensiona solo fondazioni compresse; rivedere campate e vincoli.");
            if (pier) maxPierStressRatio = Math.Max(maxPierStressRatio, axial / columnArea / 1000 / i.FcSub);
            var foundationSize = SizeFoundation(i, axial, pressure, pileDiameter, pileResistance, pier ? size + 1 : 0,
                pier && !wall && columns == 1 ? size + 1 : width);
            int pileCount = foundationSize.Piles;
            double fs = foundationSize.Length, transverse = foundationSize.Width, ft = foundationSize.Thickness, baseSize = foundationSize.Grid;
            double fv = fs * transverse * ft, ratio = foundationSize.Ratio;
            maxFoundationRatio = Math.Max(maxFoundationRatio, ratio);
            if (pileDiameter > 0 && fs + .001 < baseSize) warnings.Add($"Appoggio {k + 1}: plinto troppo piccolo per disporre {pileCount} pali a interasse 3Ø.");
            if (pileCount > 64) warnings.Add($"Appoggio {k + 1}: {pileCount} pali stimati, soluzione da rivedere.");
            if (pier) maxSlenderness = Math.Max(maxSlenderness, 2 * ph / (wall ? size / Math.Sqrt(12) : size / 4));
            subVolume += sv; foundVolume += fv; totalPileLength += pileCount * pileLength;
            double capB = i.Pier == "Testa a martello" ? 2 : 1.5, capT = i.Pier == "Testa a martello" ? 1.8 : 1.4;
            subForms += pier ? (wall ? 2 * (size + Math.Max(1, width - 2)) : columns * Math.PI * size) * ph
                + width * capB + 2 * (width + capB) * capT : 2 * (width + Math.Max(.6, ph / 7)) * ph;
            foundationForms += 2 * (fs + transverse) * ft;
            supports.Add(new(k, positions[k], pier ? i.Pier : "Spalla", reaction, ph, size, columns, fs, transverse, pileCount));
        }
        pilesVolume = totalPileLength * Math.PI * pileDiameter * pileDiameter / 4;
        double subRebar = subVolume * a.RebarSub / 1000, foundRebar = (foundVolume + pilesVolume) * a.RebarFound / 1000;
        double cf = a.Co2Concrete * (i.LowCarbon ? a.LowCarbonFactor : 1);
        double sf = a.Co2Steel * (i.Recycled ? a.RecycledFactor : 1);
        var quantities = new List<Quantity>(); var costQuantities = new List<CostQuantity>();
        string? Add(string group, string item, double amount, string unit, string rateKey, double carbon)
        {
            if (amount <= 0) return null;
            quantities.Add(new(group, item, amount, unit, rates.Value(rateKey), amount * rates.Value(rateKey), carbon));
            AddCostQuantity(costQuantities, quantities.Count, rateKey, amount, unit); return costQuantities[costQuantities.Count - 1].Id;
        }
        Add("Impalcato", "Calcestruzzo", volumeDeck, "m³", "concrete_deck", volumeDeck * cf / 1000);
        Add("Impalcato", "Armatura ordinaria", deckRebar, "t", "rebar", deckRebar * a.Co2Rebar);
        Add("Impalcato", "Precompressione", pt, "t", "prestress", pt * a.Co2Pt);
        Add("Impalcato", "Carpenteria metallica", massSteel, "t", family.Id == "steel_box" ? "steel_box" : "steel", massSteel * sf);
        Add("Impalcato", "Casseforme equivalenti", width * length * (family.Id == "fcm" ? 1.6 : 1), "m²", "formwork", 0);
        Add("Sottostrutture", "Calcestruzzo pile e spalle", subVolume, "m³", "concrete_sub", subVolume * cf / 1000);
        Add("Sottostrutture", "Armatura pile e spalle", subRebar, "t", "rebar", subRebar * a.Co2Rebar);
        Add("Sottostrutture", "Casseforme equivalenti pile e spalle", subForms, "m²", "formwork", 0);
        Add("Fondazioni", "Casseforme laterali plinti", foundationForms, "m²", "formwork", 0);
        Add("Fondazioni", "Calcestruzzo plinti", foundVolume, "m³", "concrete_sub", foundVolume * cf / 1000);
        string? pileOwner = Add("Fondazioni", $"Pali trivellati Ø {pileDiameter:0.0} m", totalPileLength, "m", pileDiameter > 1 ? "pile_15" : "pile_1", pilesVolume * cf / 1000);
        Add("Fondazioni", "Armatura plinti e pali", foundRebar, "t", "rebar", foundRebar * a.Co2Rebar);
        SplitFoundationRebar(costQuantities, pileOwner, pilesVolume * a.RebarFound / 1000);
        double bearingCount = (i.Continuous ? count + 1 : count * 2) * (family.Id == "steel_box" ? girders * 2 : Math.Max(2, girders));
        Add("Finiture", "Apparecchi d'appoggio", bearingCount, "cad", "bearing", 0);
        Add("Finiture", "Giunti di dilatazione", width * (i.Continuous ? 2 : count + 1), "m", "joint", 0);
        Add("Finiture", "Barriere", length * (i.Median > 0 ? 3 : 2), "m", "barrier", 0);
        Add("Finiture", "Pavimentazione", (width - 2 * i.BarrierWidth) * length, "m²", "surfacing", 0);
        double direct = quantities.Sum(v => v.Cost), total = direct * (1 + a.Prelims / 100) * (1 + a.Contingency / 100);
        double carbon = quantities.Sum(v => v.Carbon) * (1 + a.Co2Site / 100);
        int piers = supports.Count(s => s.Type != "Spalla");
        double months = Math.Ceiling(a.MonthsBase + a.MonthsSpan * count + a.MonthsPier * piers + (pileDiameter > 0 ? 1.5 : 0) + (family.Id == "fcm" ? count * 1.5 : 0));
        if (maxFoundationRatio > 1) warnings.Add("Pressione / carico assiale di fondazione oltre il valore indicativo: aumentare dimensioni, numero o lunghezza pali.");
        if (maxSlenderness > 100) warnings.Add("Pila snella: gli effetti del secondo ordine richiedono un modello specifico.");
        if (maxPierStressRatio > .3) warnings.Add("Compressione media nelle pile oltre 0,30 fc: soglia orientativa del predimensionamento, richiede verifica pressoflessionale specifica.");
        if (family.Id == "fcm") warnings.Add("Cassone variabile: quantità e rigidezza ricavate con altezza media d + (d_pila − d)/3; fasi a sbalzo non analizzate.");
        if (i.Obstacle == "Fiume") warnings.Add("Franco idraulico ed erosione non valutati: il terreno rappresentato è schematico.");
        if (i.TrafficModel == TrafficModel.LegacyUniform) CostWarnings(warnings, length, factored.Reactions.Where(v => v > 0).Sum(), bearingCount, BearingRate(data));
        else EnvelopeCostWarnings(warnings, length, factored.Reactions, family.Id == "steel_box" ? girders * 2 : Math.Max(2, girders), i.Continuous, BearingRate(data));
        warnings.Add("CO₂ indicativa: cls e acciai + maggiorazione trasporti/cantiere; finiture, esercizio e fine vita esclusi. Fattori non riferiti a EPD.");
        var details = new List<Detail> {
            new("Larghezza impalcato", width, "m", "corsie × larghezza + 2 banchine + spartitraffico + 2 barriere"),
            new("Altezza in campata", depth, "m", i.Depth > 0 ? "Impostata manualmente" : $"max({family.MinDepth:0.00}; Lmax/{family.Ratio:0.##} × {(i.Continuous ? ".95" : "1.10")})"),
            new("Altezza sulle pile", pierDepth, "m", family.Id == "fcm" ? "max(d; Lmax/18)" : "Altezza costante"),
            new("Area cls impalcato", areaConcrete, "m²", "Soletta + componenti della famiglia selezionata"),
            new("Inerzia lorda equivalente", inertia, "m⁴", "Componenti rettangolari, acciaio omogeneizzato con Es/Ec; sezione non fessurata"),
            new("Modulo elastico cls", ec, "MPa", "22000 × ((fc + 8)/10)^0.3; stima, senza viscosità"),
            new("Permanenti sull'intero impalcato", permanent, "kN/m", "25 Acls + 9.81 massa acciaio/m + g2 W + 8 per barriera"),
            new("Traffico equivalente sull'intero impalcato", live, "kN/m", "q equivalente × W; carico uniforme su TUTTE le campate, senza assi mobili"),
            new("Momento positivo indicativo", Math.Max(0, factored.Stations.Max(s => s.Moment)), "kNm", "Intero impalcato: γG G + γQ Q; tre momenti, EI costante"),
            new("Momento negativo indicativo", Math.Min(0, factored.Stations.Min(s => s.Moment)), "kNm", "Intero impalcato: carico uniforme su tutte le campate; non è inviluppo di traffico"),
            new("Freccia elastica indicativa", service.Stations.Max(s => Math.Abs(s.DeflectionMm)), "mm", "Doppia integrazione M/EI, G + Q; esclusi precompressione, fessurazione e viscosità"),
            new("Pressione terreno adottata", pressure, "kPa", "Parametro editabile o classe convenzionale; nessuna indagine geotecnica"),
            new("Carico assiale indicativo per palo", pileResistance, "kN", "π Ø L qs + π Ø² qb/4; valori di classe già convenzionalmente ridotti"),
            new("Rapporto carico / riferimento fondazione", maxFoundationRatio, "—", "Assiale centrato con peso pila e plinto; esclusi momenti, azioni orizzontali, cedimenti"),
            new("Snellezza massima delle pile", maxSlenderness, "—", "2H/r, schema a mensola; r = Ø/4 o t/√12"),
            new("Compressione media pile / fc", maxPierStressRatio, "—", "max [(R G+Q + peso pila) / A / fc sottostrutture]; carico centrato, senza armatura, non è una verifica di resistenza"),
            new("Calcestruzzo totale inclusi pali", volumeDeck + subVolume + foundVolume + pilesVolume, "m³", "Pali inclusi nel prezzo al metro, senza duplicare il costo del cls"),
            new("Costo diretto", direct, "€", "Σ quantità × prezzo unitario"),
            new("Costo totale indicativo", total, "€", "Diretto × (1 + oneri/100) × (1 + imprevisti/100); IVA esclusa"),
            new("Durata indicativa", months, "mesi", "Avvio + campate × coeff. + pile × coeff. + fondazioni / conci; incertezza ±25%")
        };
        DescribeTraffic(details, data, service, factored, warnings);
        if (i.TrafficModel != TrafficModel.LegacyUniform)
            for (int k = 0; k < supports.Count; k++) supports[k] = supports[k] with { MinimumReaction = service.MinimumReactions[k] };
        return new(family, width, depth, pierDepth, slab, spacing, girders, web, bottom, spans, supports.ToArray(), foundation, pileDiameter, pileLength,
            volumeDeck + subVolume + foundVolume + pilesVolume, massSteel, deckRebar + subRebar + foundRebar, pt, inertia, direct, total,
            total * (1 - a.Uncertainty / 100), total * (1 + a.Uncertainty / 100), carbon, months, quantities.ToArray(), details.ToArray(), factored.Stations, warnings.Distinct().ToArray()) { CostQuantities = costQuantities, Traffic = TrafficSummary(data) };
    }
    private static double Auto(double value, double automatic) => value > 0 ? value : automatic;

    /// <summary>Exact prismatic Euler–Bernoulli beam under one uniform load on all spans; no moving-load envelope.</summary>
    public static (double[] Reactions, Station[] Stations) Analyze(double[] spans, double q, double ei, bool continuous)
    {
        if (spans.Length == 0 || spans.Any(l => !PortableMath.IsFinite(l) || l <= 0) || !PortableMath.IsFinite(q) || !PortableMath.IsFinite(ei) || ei <= 0)
            throw new ArgumentException("Dati della trave non validi.");
        int n = spans.Length; var moments = new double[n + 1];
        if (continuous && n > 1)
        {
            var diag = new double[n - 1]; var rhs = new double[n - 1];
            for (int k = 0; k < n - 1; k++)
            {
                diag[k] = 2 * (spans[k] + spans[k + 1]); rhs[k] = -q * (Math.Pow(spans[k], 3) + Math.Pow(spans[k + 1], 3)) / 4;
                if (k > 0) { double f = spans[k] / diag[k - 1]; diag[k] -= f * spans[k]; rhs[k] -= f * rhs[k - 1]; }
            }
            for (int k = n - 2; k >= 0; k--) moments[k + 1] = (rhs[k] - (k < n - 2 ? spans[k + 1] * moments[k + 2] : 0)) / diag[k];
        }
        var reactions = new double[n + 1]; var stations = new List<Station>(); double offset = 0;
        for (int k = 0; k < n; k++)
        {
            double l = spans[k], ml = moments[k], r = q * l / 2 + (moments[k + 1] - ml) / l;
            reactions[k] += r; reactions[k + 1] += q * l - r;
            double c = -(ml * l * l / 2 + r * l * l * l / 6 - q * Math.Pow(l, 4) / 24) / l;
            var xs = Enumerable.Range(0, 41).Select(t => l * t / 40).ToList();
            // Keep the analytical maximum unless its abscissa already belongs to the sampling grid.
            if (q != 0 && r / q > 0 && r / q < l && !xs.Any(x => Math.Abs(x - r / q) <= l * 1e-12)) xs.Add(r / q);
            foreach (double x in xs.Distinct().OrderBy(orderValue => orderValue))
                stations.Add(new(k + 1, offset + x, ml + r * x - q * x * x / 2, r - q * x,
                    -(ml * x * x / 2 + r * x * x * x / 6 - q * Math.Pow(x, 4) / 24 + c * x) / ei * 1000));
            offset += l;
        }
        return (reactions, stations.ToArray());
    }
}
