
namespace GPC.Design.Bridges;

public static partial class BridgeConcept
{
    public enum OptimizationObjective { Cost, Carbon, Balanced }
    public sealed record OptimizationOptions
    {
        public OptimizationObjective Objective { get; init; } = OptimizationObjective.Cost;
        public bool KeepFamily { get; init; }
        public bool KeepSpans { get; init; }
        public bool KeepDepth { get; init; }
        public bool KeepSection { get; init; }
        public bool KeepPier { get; init; }
        public bool KeepFoundation { get; init; }
        public bool KeepContinuity { get; init; } = true;
        public int MinSpans { get; init; } = 1;
        public int MaxSpans { get; init; } = 12;
        public double MaxDepth { get; init; } = 0;
        public double MinDepth { get; init; } = 0;
        public double DepthPercentMin { get; init; } = 100;
        public double DepthPercentMax { get; init; } = 115;
        public double DepthPercentStep { get; init; } = 15;
        public double PilePercentMin { get; init; } = 100;
        public double PilePercentMax { get; init; } = 150;
        public double PilePercentStep { get; init; } = 25;
        public int Alternatives { get; init; } = 10;
    }
    public sealed record OptimizationCandidate(BridgeModel Data, Result Result, double Score, bool Pareto, string Changes);
    public sealed record OptimizationResult(Result Baseline, OptimizationCandidate[] Candidates, int Evaluated, int Admissible,
        IReadOnlyDictionary<string, int> Excluded, string[] BaselineExclusions, OptimizationOptions Options)
    {
        public OptimizationTrial[] Trials { get; init; } = [];
        public OptimizationSolution[] Solutions { get; init; } = [];
    }
    public sealed record OptimizationTrial(int Iteration, string Family, int Spans, double? Depth, double? PileLength,
        double? Cost, double? Carbon, string Pier, string Foundation, bool Continuous, string[] Exclusions)
    { public bool Admissible => Exclusions.Length == 0; }
    public sealed record OptimizationSolution(int Rank, OptimizationTrial Trial, BridgeModel Data, double Score, bool Pareto);
    public sealed record OptimizationProgress(int Evaluated, int Planned, int Admissible, OptimizationTrial[] NewTrials,
        OptimizationCandidate? Best);
    public const string OptimizationScope = "Migliore soluzione tra le combinazioni esplorate dal modello ANTHEA. Filtri geometrici e soglie orientative; non è un progetto verificato né un ottimo strutturale globale.";

    /// <summary>Deterministic discrete search, no UI and no mutations of the caller's data.</summary>
    public static OptimizationResult Optimize(BridgeModel source, OptimizationOptions options, CancellationToken cancellation = default,
        IProgress<OptimizationProgress>? progress = null)
    {
        if (!Enum.IsDefined(options.Objective) || options.MinSpans < 1 || options.MaxSpans > 30 || options.MaxSpans < options.MinSpans
            || !double.IsFinite(options.MaxDepth) || options.MaxDepth < 0 || options.MaxDepth > 15
            || !double.IsFinite(options.MinDepth) || options.MinDepth < 0 || options.MinDepth > 15
            || (options.MaxDepth > 0 && options.MinDepth > options.MaxDepth) || options.Alternatives is < 1 or > 50)
            throw new ArgumentException("Limiti di ricerca non validi: 1–30 campate, altezza massima 0–15 m, 1–50 alternative.");
        if (options.KeepSection && !options.KeepFamily)
            throw new ArgumentException("Per mantenere la sezione occorre mantenere anche la tipologia.");
        var depths = OptimizationRange(options.DepthPercentMin, options.DepthPercentMax, options.DepthPercentStep);
        var piles = OptimizationRange(options.PilePercentMin, options.PilePercentMax, options.PilePercentStep);
        cancellation.ThrowIfCancellationRequested();
        var baselineData = source;
        var baseline = Calculate(baselineData); var input = baselineData.Input;
        var accepted = new List<(BridgeModel Data, Result Result, OptimizationTrial Trial)>();
        var trials = new List<OptimizationTrial>();
        var excluded = new Dictionary<string, int>(); var seen = new HashSet<BridgeInput>(); int evaluated = 0;
        var families = (options.KeepFamily ? Families.Where(f => f.Id == baseline.Family.Id) : Families).ToArray();
        int[] spans = options.KeepSpans ? [baseline.Spans.Count] : Enumerable.Range(options.MinSpans, options.MaxSpans - options.MinSpans + 1).ToArray();
        bool[] continuity = options.KeepContinuity ? [input.Continuous] : [true, false];
        string[] piers = options.KeepPier ? [input.Pier] : Piers.ToArray();
        string[] foundations = options.KeepFoundation ? [baseline.Foundation] : Foundations.Skip(1).ToArray();
        bool Eligible(Family f, int n) => HasTowers(f.Id) ? n == 3 && baseline.Length / 2 >= f.MinSpan && baseline.Length / 2 <= f.MaxSpan
            : baseline.Length / n >= f.MinSpan && baseline.Length / n <= f.MaxSpan;
        int planned = 1 + families.Sum(f => spans.Count(n => Eligible(f, n)))
            * continuity.Length * piers.Length * (options.KeepDepth ? 1 : depths.Length)
            * foundations.Sum(f => options.KeepFoundation || f == "Plinto diretto" ? 1 : piles.Length);
        if (planned > 50000) throw new ArgumentException($"Griglia troppo estesa ({planned:N0} tentativi): restringere gli intervalli o aumentare il passo. Limite: 50.000.");
        var watch = System.Diagnostics.Stopwatch.StartNew(); long lastReport = -200; int reported = 0;
        double Score(Result r, double minCost, double minCarbon) => options.Objective switch
        {
            OptimizationObjective.Cost => r.TotalCost,
            OptimizationObjective.Carbon => r.Carbon,
            _ => .5 * (r.TotalCost / Math.Max(1, minCost) + r.Carbon / Math.Max(1e-9, minCarbon))
        };
        void Report(bool final = false)
        {
            if (progress is null || (!final && watch.ElapsedMilliseconds - lastReport < 200)) return;
            OptimizationCandidate? best = null;
            if (accepted.Count > 0)
            {
                double mc = accepted.Min(c => c.Result.TotalCost), me = accepted.Min(c => c.Result.Carbon);
                var c = accepted.OrderBy(c => Score(c.Result, mc, me)).ThenBy(c => c.Result.TotalCost).ThenBy(c => c.Result.Carbon)
                    .ThenBy(c => c.Result.Family.Id, StringComparer.Ordinal).ThenBy(c => c.Result.Spans.Count).First();
                best = new(c.Data, c.Result, Score(c.Result, mc, me), false, DescribeChanges(input, c.Data.Input));
            }
            progress.Report(new(evaluated, planned, accepted.Count, trials.Skip(reported).ToArray(), best));
            reported = trials.Count; lastReport = watch.ElapsedMilliseconds;
        }
        void Add(BridgeModel candidate)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!seen.Add(candidate.Input)) return;
            evaluated++;
            Result? r = null; string[] reasons;
            try
            {
                r = Calculate(candidate); reasons = OptimizationExclusions(candidate, r, options);
            }
            catch (ArgumentException ex)
            {
                reasons = ["Dati incompatibili: " + ex.Message];
            }
            var ci = candidate.Input;
            var trial = new OptimizationTrial(evaluated, ci.Family, r?.Spans.Count ?? (int)ci.Spans, r?.Depth,
                r?.PileLength, r?.TotalCost, r?.Carbon, HasTowers(ci.Family) ? "Antenna · 2 fusti quadrati" : ci.Pier, r?.Foundation ?? ci.Foundation, ci.Continuous, reasons);
            trials.Add(trial);
            if (reasons.Length == 0 && r is not null) accepted.Add((candidate, r, trial));
            else foreach (var reason in reasons) excluded[reason] = excluded.GetValueOrDefault(reason) + 1;
            Report();
        }
        // Always include the current solution if it satisfies the requested limits.
        Add(baselineData);
        foreach (var family in families)
        foreach (int count in spans)
        {
            cancellation.ThrowIfCancellationRequested();
            // Necessary span-range condition; exact spans and obstacle clearance are checked after calculation.
            if (!Eligible(family, count)) continue;
            if (options.KeepPier && HasTowers(family.Id) != HasTowers(baseline.Family.Id)) continue;
            foreach (bool continuous in continuity)
            foreach (string pier in piers)
            foreach (string foundation in foundations)
            foreach (double depthFactor in options.KeepDepth ? new[] { 1d } : depths)
            foreach (double pileFactor in options.KeepFoundation || foundation == "Plinto diretto" ? new[] { 1d } : piles)
            {
                if (family.Id is "tied_arch" or "truss" && continuous || HasTowers(family.Id) && (!continuous || input.StartPier || input.EndPier)) continue;
                var d = baselineData; var i = d.Input;
                i = i with { Family = family.Id }; i = i with { Spans = count }; i = i with { Continuous = continuous };
                i = i with { Depth = options.KeepDepth ? baseline.Depth : 0 };
                if (options.KeepSection)
                {
                    if (baseline.Family.Id != "filler_beam") i = i with { Slab = baseline.Slab };
                    if (baseline.Advanced?.Orthotropic != true) i = i with { Spacing = baseline.Spacing };
                    i = i with { Web = baseline.Web };
                    i = i with { Bottom = baseline.Bottom }; i = i with { Boxes = baseline.Family.Id == "steel_box" || baseline.Advanced?.Orthotropic == true ? baseline.Girders : input.Boxes };
                }
                else
                {
                    // Explore the family's standard section, not arbitrary reductions of plate thickness.
                    i = ResetSection(i);
                }
                i = i with { Pier = pier };
                if (!options.KeepPier) i = i with { PierSize = 0 };
                i = i with { Foundation = foundation };
                if (options.KeepFoundation)
                {
                    i = i with { PileLength = baseline.PileLength };
                    // An automatic count/footing size remains an automatic design rule at each support.
                    // Existing manually imposed dimensions and counts are preserved exactly.
                }
                else
                {
                    i = i with { PileLength = foundation == "Plinto diretto" ? 0 : Math.Min(80, new[] { 10d, 15, 22, 30 }[Soils.ToList().IndexOf(i.Soil)] * pileFactor) };
                    i = i with { PileCount = 0 }; i = i with { FootingSize = 0 };
                }
                if (!options.KeepDepth && depthFactor != 1)
                {
                    // Span coordinates can put an exact 5 cm step a few ulps
                    // above its boundary; do not add an unintended extra step.
                    try { i = i with { Depth = Math.Ceiling(Calculate(d with { Input = i }).Depth * depthFactor * 20 - 1e-10) / 20 }; }
                    catch (ArgumentException) { /* Add records the invalid candidate and its reason. */ }
                }
                Add(d with { Input = i });
            }
        }
        cancellation.ThrowIfCancellationRequested(); Report(true); cancellation.ThrowIfCancellationRequested();
        var baseReasons = OptimizationExclusions(baselineData, baseline, options);
        if (accepted.Count == 0) return new(baseline, [], evaluated, 0, excluded, baseReasons, options) { Trials = trials.ToArray() };
        double minCost = accepted.Min(c => c.Result.TotalCost), minCarbon = accepted.Min(c => c.Result.Carbon);
        var ordered = accepted.OrderBy(c => Score(c.Result, minCost, minCarbon)).ThenBy(c => c.Result.TotalCost).ThenBy(c => c.Result.Carbon)
            .ThenBy(c => c.Result.Family.Id, StringComparer.Ordinal).ThenBy(c => c.Result.Spans.Count).ToArray();
        // Do not fill the shortlist twice with automatic and explicit representations of the same geometry.
        var distinct = ordered.DistinctBy(c => new GeometryIdentity(c.Result,c.Data.Input.Continuous)).ToArray();
        var pareto = new HashSet<int>(); double previousCarbon = double.PositiveInfinity;
        foreach (var group in distinct.GroupBy(c => c.Result.TotalCost).OrderBy(g => g.Key))
        {
            double carbon = group.Min(c => c.Result.Carbon);
            if (carbon < previousCarbon)
                foreach (var c in group.Where(c => c.Result.Carbon == carbon)) pareto.Add(c.Trial.Iteration);
            previousCarbon = Math.Min(previousCarbon, carbon);
        }
        var candidates = distinct.Take(options.Alternatives).Select(c => new OptimizationCandidate(c.Data, c.Result, Score(c.Result, minCost, minCarbon),
            pareto.Contains(c.Trial.Iteration), DescribeChanges(input, c.Data.Input))).ToArray();
        return new(baseline, candidates, evaluated, accepted.Count, excluded, baseReasons, options) {
            Trials = trials.ToArray(), Solutions = distinct.Select((c, k) => new OptimizationSolution(k + 1, c.Trial, c.Data,
                Score(c.Result, minCost, minCarbon), pareto.Contains(c.Trial.Iteration))).ToArray()
        };
    }

    /// <summary>Inclusive percentage grid, with the upper endpoint included even when the step does not divide the interval.</summary>
    public static double[] OptimizationRange(double min, double max, double step)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || !double.IsFinite(step) || min < 100 || max > 200 || min > max || step < 1 || step > 100)
            throw new ArgumentException("Intervalli percentuali: 100 ≤ minimo ≤ massimo ≤ 200; passo da 1 a 100 punti percentuali.");
        if (Math.Ceiling((max - min) / step) + 1 > 11)
            throw new ArgumentException("Massimo 11 valori per intervallo percentuale: aumentare il passo.");
        var values = new List<double>();
        for (int k = 0; min + k * step < max - 1e-9; k++) values.Add((min + k * step) / 100);
        values.Add(max / 100); return values.ToArray();
    }

    public static string[] OptimizationExclusions(BridgeModel data, Result r, OptimizationOptions options)
    {
        var reasons = new List<string>(); var i = data.Input;
        if (r.Spans.Count < options.MinSpans || r.Spans.Count > options.MaxSpans) reasons.Add("Numero campate fuori dai limiti richiesti");
        double minSpan = HasTowers(r.Family.Id) ? r.Spans[1] : r.Spans.Min();
        if (minSpan < r.Family.MinSpan - 1e-8 || r.Spans.Max() > r.Family.MaxSpan + 1e-8) reasons.Add("Luci fuori dal campo usuale della tipologia");
        if (r.Depth < options.MinDepth - 1e-8) reasons.Add("Altezza in campata inferiore al minimo richiesto");
        if (options.MaxDepth > 0 && r.PierDepth > options.MaxDepth + 1e-8) reasons.Add("Altezza massima superata (incluse le pile)");
        double requiredDepth = ReferenceDepth(r.Family, r.Spans.Max(), i.Continuous);
        if (r.Depth < requiredDepth - 1e-8) reasons.Add("Altezza inferiore alla regola di predimensionamento");
        if (i.Obstacle != "Nessuno" && i.ObstacleWidth > 0)
        {
            double left = (r.Length - i.ObstacleWidth) / 2 - 1, right = r.Length - left;
            if (r.Supports.Skip(1).SkipLast(1).Any(s => s.X > left + 1e-8 && s.X < right - 1e-8)) reasons.Add("Pile interferenti con ostacolo e margine di 1 m");
        }
        double DetailValue(string name) => r.Details.Single(d => d.Name == name).Value;
        if (DetailValue("Rapporto carico / riferimento fondazione") > 1 + 1e-8) reasons.Add("Fondazione oltre la soglia assiale indicativa");
        if (DetailValue("Snellezza massima delle pile") > 100 + 1e-8) reasons.Add("Snellezza pile superiore a 100");
        if (DetailValue("Compressione media pile / fc") > .3 + 1e-8) reasons.Add("Compressione media pile superiore a 0,30 fc");
        if (r.Details.Any(d => d.Name == "Compressione antenne / riferimento" && d.Value > 1 + 1e-8)) reasons.Add("Compressione antenna oltre la tensione di riferimento");
        if (r.Supports.Any(s => s.Type != "Spalla" && s.FootingSize < s.PierSize - 1e-8)) reasons.Add("Fusto non contenuto nel plinto");
        if (r.Supports.Any(s => s.Piles > 64)) reasons.Add("Più di 64 pali per appoggio");
        if (r.Family.Id != "suspension" && r.Supports.Any(s => s.Reaction < -1e-8)) reasons.Add("Reazione verso l’alto: dispositivi antisollevamento non dimensionati");
        if (r.PileDiameter > 0 && r.Supports.Any(s => s.FootingSize + .001 < (Math.Ceiling(Math.Sqrt(s.Piles)) - 1) * 3 * r.PileDiameter + 2 * r.PileDiameter)) reasons.Add("Pali non contenuti nel plinto a interasse 3Ø");
        if (r.Family.Id == "psc_u" && i.UTop * r.Girders > r.Width) reasons.Add("Sovrapposizione travi a U");
        if (r.Family.Id == "steel_box")
        {
            double wh = r.Depth - r.Slab - 2 * i.FlangeMm / 1000;
            if (r.Width * .4 / r.Girders + 2 * wh * i.WebSlope / 4 + i.FlangeWidth > r.Width / r.Girders) reasons.Add("Cassoni metallici oltre la larghezza disponibile");
        }
        if (!double.IsFinite(r.TotalCost) || !double.IsFinite(r.Carbon) || r.TotalCost < 0 || r.Carbon < 0) reasons.Add("Indicatore economico o ambientale non valido");
        return reasons.ToArray();
    }
    private static BridgeInput ResetSection(BridgeInput input)
    {
        double Value(string key) => Section.First(p => p.Key == key).Default;
        return input with
        {
            Slab = Value("slab"),
            Spacing = Value("spacing"),
            Web = Value("web"),
            Bottom = Value("bottom"),
            BottomRatio = Value("bottom_ratio"),
            Cells = Value("cells"),
            Boxes = Value("boxes"),
            FlangeWidth = Value("flange_width"),
            FlangeMm = Value("flange_mm"),
            WebMm = Value("web_mm"),
            WebSlope = Value("web_slope"),
            Haunch = Value("haunch"),
            UTop = Value("u_top"),
            UBottom = Value("u_bottom")
        };
    }
    private static string DescribeChanges(BridgeInput before,BridgeInput after)
    {
        var labels=Site.Concat(Layout).Concat(Section).Concat(AdvancedSection).Concat(Substructure).ToDictionary(p=>p.Key,p=>p.Label.Split('·')[0].Trim());
        labels["family"]="Tipologia";labels["pier"]="Pila";labels["foundation"]="Fondazione";labels["continuous"]="Continuità";
        var changes=new List<string>();
        void Add(string key,object a,object b){if(!Equals(a,b))changes.Add(labels.GetValueOrDefault(key,key));}
        Add("length",before.Length,after.Length);
        Add("height",before.Height,after.Height);
        Add("obstacle_width",before.ObstacleWidth,after.ObstacleWidth);
        Add("lanes",before.Lanes,after.Lanes);
        Add("lane_width",before.LaneWidth,after.LaneWidth);
        Add("shoulder",before.Shoulder,after.Shoulder);
        Add("median",before.Median,after.Median);
        Add("barrier_width",before.BarrierWidth,after.BarrierWidth);
        Add("spans",before.Spans,after.Spans);
        Add("depth",before.Depth,after.Depth);
        Add("slab",before.Slab,after.Slab);
        Add("spacing",before.Spacing,after.Spacing);
        Add("web",before.Web,after.Web);
        Add("bottom",before.Bottom,after.Bottom);
        Add("bottom_ratio",before.BottomRatio,after.BottomRatio);
        Add("cells",before.Cells,after.Cells);
        Add("boxes",before.Boxes,after.Boxes);
        Add("flange_width",before.FlangeWidth,after.FlangeWidth);
        Add("flange_mm",before.FlangeMm,after.FlangeMm);
        Add("web_mm",before.WebMm,after.WebMm);
        Add("web_slope",before.WebSlope,after.WebSlope);
        Add("haunch",before.Haunch,after.Haunch);
        Add("u_top",before.UTop,after.UTop);
        Add("u_bottom",before.UBottom,after.UBottom);
        Add("fc",before.Fc,after.Fc);
        Add("arch_rise",before.ArchRise,after.ArchRise);
        Add("tower_height",before.TowerHeight,after.TowerHeight);
        Add("cable_sag",before.CableSag,after.CableSag);
        Add("suspender_spacing",before.SuspenderSpacing,after.SuspenderSpacing);
        Add("truss_ratio",before.TrussRatio,after.TrussRatio);
        Add("embedded_cover",before.EmbeddedCover,after.EmbeddedCover);
        Add("deck_plate_mm",before.DeckPlateMm,after.DeckPlateMm);
        Add("rib_height",before.RibHeight,after.RibHeight);
        Add("rib_top",before.RibTop,after.RibTop);
        Add("rib_bottom",before.RibBottom,after.RibBottom);
        Add("rib_mm",before.RibMm,after.RibMm);
        Add("rib_spacing",before.RibSpacing,after.RibSpacing);
        Add("pile_length",before.PileLength,after.PileLength);
        Add("pier_size",before.PierSize,after.PierSize);
        Add("pile_count",before.PileCount,after.PileCount);
        Add("footing_size",before.FootingSize,after.FootingSize);
        Add("fc_sub",before.FcSub,after.FcSub);
        Add("deck_type",before.DeckType,after.DeckType);
        Add("family",before.Family,after.Family);
        Add("obstacle",before.Obstacle,after.Obstacle);
        Add("soil",before.Soil,after.Soil);
        Add("pier",before.Pier,after.Pier);
        Add("foundation",before.Foundation,after.Foundation);
        Add("continuous",before.Continuous,after.Continuous);
        Add("start_pier",before.StartPier,after.StartPier);
        Add("end_pier",before.EndPier,after.EndPier);
        Add("low_carbon",before.LowCarbon,after.LowCarbon);
        Add("recycled",before.Recycled,after.Recycled);
        return changes.Count==0?"Configurazione corrente":string.Join(", ",changes);
    }
    private sealed class GeometryIdentity(Result value,bool continuous) : IEquatable<GeometryIdentity>
    {
        private readonly Result r=value;private readonly bool c=continuous;
        public bool Equals(GeometryIdentity? other)
        {
            if(other is null)return false;var b=other.r;
            return c==other.c && r.Family.Id==b.Family.Id && r.Depth==b.Depth && r.PierDepth==b.PierDepth && r.Slab==b.Slab
                && r.Spacing==b.Spacing && r.Girders==b.Girders && r.Web==b.Web && r.Bottom==b.Bottom && r.Inertia==b.Inertia
                && r.PileDiameter==b.PileDiameter && r.PileLength==b.PileLength && r.Spans.SequenceEqual(b.Spans)
                && r.Supports.SequenceEqual(b.Supports) && r.Quantities.SequenceEqual(b.Quantities);
        }
        public override bool Equals(object? other)=>other is GeometryIdentity identity&&Equals(identity);
        public override int GetHashCode()=>HashCode.Combine(r.Family.Id,r.Depth,r.PierDepth,r.Slab,r.Girders,r.Inertia,r.PileLength,c);
    }
}
