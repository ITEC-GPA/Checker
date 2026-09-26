namespace GPC.Checkers.CompositeBridge.History;

public enum HistoryMaterialMode { Linear, Nonlinear }
public sealed record HBridgeHistoryOptions
{
    public HistoryMaterialMode MaterialMode { get; init; }
    /// <summary>Explicit instantaneous analysis: ignore archived creep inputs without modifying them.</summary>
    public bool InstantaneousConcrete { get; init; }
    public int WebLayers { get; init; } = 160;
    public int FlangeLayers { get; init; } = 4;
    public int ConcreteLayers { get; init; } = 32;
    public int SubstepsPerPhase { get; init; } = 1;
    public HistorySolverOptions Solver { get; init; } = new();
    public IHistoryMaterialLaw? SteelLaw { get; init; }
    public IHistoryMaterialLaw? ConcreteLaw { get; init; }
    public IHistoryMaterialLaw? RebarLaw { get; init; }
}

/// <summary>H bridge adapter to the separate history engine. No calls to the cumulative phase solver.</summary>
public static class HBridgeHistoryAnalysis
{
    public const string Concrete = "Concrete", Rebars = "Rebars";
    public const string Top = "Steel.Top", Web = "Steel.Web", Bottom = "Steel.Bottom", Bottom2 = "Steel.Bottom2";
    public static HistoryAnalysisResult Calculate(HBridgeInput input, HBridgeHistoryOptions? options = null, CancellationToken cancellation = default) =>
        Calculate(input, input.Phases, options, cancellation);
    public static HistoryAnalysisResult Calculate(HBridgeInput input, IEnumerable<BridgePhase> phases,
        HBridgeHistoryOptions? options = null, CancellationToken cancellation = default)
    {
        var settings = options ?? new();
        return BridgeHistoryAnalysis.Calculate(CreateSection(input, settings), ConvertPhases(input, phases, settings), settings.Solver, cancellation);
    }

    public static HistorySection CreateSection(HBridgeInput input, HBridgeHistoryOptions? options = null)
    {
        var o = options ?? new();
        if (o.WebLayers < 1 || o.FlangeLayers < 1 || o.ConcreteLayers < 1 || o.SubstepsPerPhase < 1 || !Enum.IsDefined(typeof(HistoryMaterialMode), o.MaterialMode))
            throw new ArgumentException("Discretizzazione o modo materiale non valido.");
        // Geometry validation is reused, but the legacy API's 20-phase limit has no place in this engine.
        var g = HBridgeSection.Geometry(input with { Phases = new[] { new BridgePhase() } });
        var m = input.Materials;
        bool linear = o.MaterialMode == HistoryMaterialMode.Linear;
        IHistoryMaterialLaw steel = o.SteelLaw ?? (linear ? new HistoryElasticLaw(m.Steel.ElasticModulusTension) : HistoryBilinearSteelLaw.FromModel(m.Steel));
        IHistoryMaterialLaw concrete = o.ConcreteLaw ?? (linear ? new HistoryElasticLaw(m.Concrete.ElasticModulusCompression) : new HistoryModelEnvelopeLaw(m.Concrete));
        IHistoryMaterialLaw rebar = o.RebarLaw ?? (linear ? new HistoryElasticLaw(m.Rebar.ElasticModulusTension) : HistoryBilinearSteelLaw.FromModel(m.Rebar));
        if (input.Options.Class4 && (!steel.IsLinear || !concrete.IsLinear || !rebar.IsLinear))
            throw new NotSupportedException("Le larghezze efficaci automatiche sono disponibili per i legami lineari. Il non lineare richiede Class4=false oppure un modello efficace dedicato nell'API generica.");
        var components = new List<HistoryComponent> {
            new(Top, steel, Rectangle(Top, g.Width / 2, -g.TopThickness, g.TopWidth, g.TopThickness, o.FlangeLayers), true),
            new(Web, steel, Rectangle(Web, g.Width / 2, -g.TopThickness - g.WebHeight, g.WebThickness, g.WebHeight, o.WebLayers), true) };
        if (g.Bottom2Thickness > 0)
        {
            // the two real bottom plates (before, the equivalent rectangle)
            components.Add(new(Bottom, steel, Rectangle(Bottom, g.Width / 2, -g.TopThickness - g.WebHeight - g.Bottom1Thickness, g.Bottom1Width, g.Bottom1Thickness, o.FlangeLayers), true));
            components.Add(new(Bottom2, steel, Rectangle(Bottom2, g.Width / 2, -g.Height, g.Bottom2Width, g.Bottom2Thickness, o.FlangeLayers), true));
        }
        else components.Add(new(Bottom, steel, Rectangle(Bottom, g.Width / 2, -g.Height, g.BottomEquivalentWidth, g.BottomEquivalentThickness, o.FlangeLayers), true));
        components.Add(new(Concrete, concrete, ConcreteFibers(g, o.ConcreteLayers)));
        if (g.Bars.Length > 0)
            components.Add(new(Rebars, rebar, Array.AsReadOnly(g.Bars.SelectMany(b => new[] {
                new HistoryFiber(b.Id + ".0", b.X, b.Y - b.Diameter / 4, b.Area / 2),
                new HistoryFiber(b.Id + ".1", b.X, b.Y + b.Diameter / 4, b.Area / 2) }).ToArray())));
        return new(Array.AsReadOnly(components.ToArray()), input.Options.Class4 ? new HEffectiveModel(g, m.Steel.Fyk, input.Options) : null);
    }

    /// <summary>Maps any length phase sequence. Each shrinkage is a new increment, with its own phi/n.</summary>
    public static IEnumerable<HistoryPhase> ConvertPhases(HBridgeInput input, IEnumerable<BridgePhase> phases, HBridgeHistoryOptions? options = null)
    {
        var o = options ?? new(); bool concreteActive = false, barsActive = false, compositeSeen = false;
        bool hasBars = input.TopRebars.Enabled || input.BottomRebars.Enabled;
        foreach (var p in phases)
        {
            if (!p.Active) continue;
            if (!Enum.IsDefined(typeof(BridgePhaseKind), p.Kind) || !Enum.IsDefined(typeof(BridgeLoadReference), p.Reference)) throw new ArgumentException("Tipo/riferimento della fase non valido.");
            if (p.Kind == BridgePhaseKind.SteelOnly && compositeSeen) throw new ArgumentException("Le fasi di solo acciaio devono precedere il getto.");
            bool hasConcrete = p.Kind is BridgePhaseKind.Composite or BridgePhaseKind.Shrinkage;
            bool hasRebars = hasBars && p.Kind != BridgePhaseKind.SteelOnly;
            var activate = new List<string>(); var deactivate = new List<string>();
            if (hasConcrete && !concreteActive) activate.Add(Concrete);
            if (!hasConcrete && concreteActive) deactivate.Add(Concrete);
            if (hasRebars && !barsActive) activate.Add(Rebars);
            concreteActive = hasConcrete; barsActive |= hasRebars; compositeSeen |= p.Kind != BridgePhaseKind.SteelOnly;
            var factors = new Dictionary<string, double>();
            if (hasConcrete)
            {
                var h = CompositeHomogenization.Calculate(input.Materials, o.InstantaneousConcrete ? p with { Phi = 0, HomogenizationSource = BridgeHomogenizationSource.Phi } : p);
                factors[Concrete] = 1 / (1 + h.PhiEffective);
            }
            bool shrinkage = p.Kind == BridgePhaseKind.Shrinkage;
            var eigenstrain = new Dictionary<string, HistoryStrainPlane>();
            if (shrinkage) eigenstrain.Add(Concrete, new(p.ShrinkageMicrostrain * 1e-6, 0));
            yield return new() {
                Name = p.Name, DeltaN = shrinkage ? 0 : p.ForceKN * 1000, DeltaM = shrinkage ? 0 : p.MomentKNm * 1e6,
                DeltaV = shrinkage ? 0 : p.ShearKN * 1000, ApplicationY = input.Options.CommonLoadY,
                LoadReference = p.Reference switch { BridgeLoadReference.GrossCentroid => HistoryLoadReference.GrossElasticCentroid,
                    BridgeLoadReference.EffectiveCentroid => HistoryLoadReference.EffectiveElasticCentroid, _ => HistoryLoadReference.FixedElevation },
                Activate = activate, Deactivate = deactivate, ModulusFactors = factors,
                ImposedStrainIncrements = eigenstrain, Substeps = o.SubstepsPerPhase };
        }
    }

    /// <summary>Two Gauss points per strip: exact area, first and second moments for rectangular plates.</summary>
    public static IReadOnlyList<HistoryFiber> Rectangle(string id, double x, double bottom, double width, double height, int layers)
    {
        BridgeNumbers.Require(width, nameof(width), strict: true); BridgeNumbers.Require(height, nameof(height), strict: true);
        if (layers < 1) throw new ArgumentOutOfRangeException(nameof(layers));
        var fibers = new List<HistoryFiber>(); double step = height / layers;
        for (int i = 0; i < layers; i++)
        {
            double lo = bottom + i * step, hi = lo + step, cy = (lo + hi) / 2, dy = step / Math.Sqrt(12);
            fibers.Add(new(id + "." + i + ".0", x, cy - dy, width * step / 2, lo, hi));
            fibers.Add(new(id + "." + i + ".1", x, cy + dy, width * step / 2, lo, hi));
        }
        return Array.AsReadOnly(fibers.ToArray());
    }

    private static IReadOnlyList<HistoryFiber> ConcreteFibers(BridgeGeometry g, int layers)
    {
        var rows = new List<HistoryFiber>();
        // Integrate the NET concrete area/moments, subtracting the actual circular bars once.
        // Two moment-fitted points per strip make the elastic area/centroid/inertia exact.
        for (int i = 0; i < layers; i++)
        {
            double lo = g.SlabHeight * i / layers, hi = g.SlabHeight * (i + 1) / layers;
            double a = g.Width * (hi - lo), s = a * (hi + lo) / 2, j = g.Width * (hi * hi * hi - lo * lo * lo) / 3;
            foreach (var bar in g.Bars)
            {
                double r = bar.Diameter / 2;
                double l = Math.Max(-r, lo - bar.Y), h = Math.Min(r, hi - bar.Y);
                if (h <= l) continue;
                (double A, double S, double J) Integral(double u)
                {
                    double angle = Math.Asin(BridgeNumbers.Clamp(u / r, -1, 1)), t = Math.Max(0, r * r - u * u);
                    return (u * Math.Sqrt(t) + r * r * angle, -2d / 3 * Math.Pow(t, 1.5), Math.Pow(r, 4) / 4 * (angle - Math.Sin(4 * angle) / 4));
                }
                var p = Integral(l); var q = Integral(h);
                double da = q.A - p.A, ds = q.S - p.S;
                a -= da; s -= ds + bar.Y * da; j -= q.J - p.J + 2 * bar.Y * ds + bar.Y * bar.Y * da;
            }
            if (a <= 0) throw new ArgumentException("Armature eccessive nella striscia di calcestruzzo.");
            double cy = s / a, variance = j / a - cy * cy;
            if (variance < -1e-7) throw new ArgumentException("Momenti geometrici del CLS netto non validi.");
            double dy = Math.Sqrt(Math.Max(0, variance));
            rows.Add(new(Concrete + "." + i + ".0", g.Width / 2, cy - dy, a / 2, lo, hi));
            rows.Add(new(Concrete + "." + i + ".1", g.Width / 2, cy + dy, a / 2, lo, hi));
        }
        return Array.AsReadOnly(rows.ToArray());
    }

    private sealed class HEffectiveModel : IHistoryEffectiveAreaModel
    {
        private readonly BridgeGeometry g; private readonly double fy; private readonly BridgeAnalysisOptions options;
        internal HEffectiveModel(BridgeGeometry geometry, double yield, BridgeAnalysisOptions analysisOptions) { g = geometry; fy = yield; options = analysisOptions; }
        public HistoryEffectiveAreaResponse Evaluate(IReadOnlyList<HistoryFiberResult> trial)
        {
            var steel = trial.Where(p => p.ComponentId == Web).OrderBy(p => p.Fiber.Y).ToArray();
            var low = steel.First(); var high = steel.Last();
            double slope = (high.Stress - low.Stress) / (high.Fiber.Y - low.Fiber.Y);
            double Sigma(double y) => low.Stress + slope * (y - low.Fiber.Y);
            // the same reductions and options of the cumulative method, on the linear steel stress field of the web
            var e = HBridgeSection.EffectiveWidths(g, Sigma, fy, options);
            double top = -g.TopThickness, bottom = top - g.WebHeight;
            double Factor(HistoryFiberResult p)
            {
                if (p.ComponentId == Top) return e.TopWidth / g.TopWidth;
                if (p.ComponentId == Bottom) return e.BottomWidth / (g.Bottom2Thickness > 0 ? g.Bottom1Width : g.BottomEquivalentWidth);
                if (p.ComponentId == Bottom2) return e.SecondBottomWidth / g.Bottom2Width;
                if (p.ComponentId != Web) return 1;
                double lo = p.Fiber.StripBottom, hi = p.Fiber.StripTop;
                double Overlap(double a, double b) => Math.Max(0, Math.Min(hi, b) - Math.Max(lo, a));
                return BridgeNumbers.Clamp((Overlap(top - e.WebTop, top) + Overlap(bottom, bottom + e.WebBottom)) / (hi - lo), 0, 1);
            }
            var panels = new List<HistoryPanelResult> { new(Web, e.Web), new(Top, e.Top), new(Bottom, e.Bottom) };
            if (e.SecondBottom is not null) panels.Add(new(Bottom2, e.SecondBottom));
            return new(Array.AsReadOnly(trial.Select(Factor).ToArray()), Array.AsReadOnly(panels.ToArray()));
        }
    }
}
