namespace GPC.Checkers.CompositeBridge.History;

/// <summary>Discrete stress field. Interpolation stays inside each component; end values use
/// the nearest integration point (no extrapolation beyond a nonlinear material envelope).</summary>
public sealed class HistoryStressProfile
{
    private readonly Dictionary<string, (double Y, double Stress)[]> samples;
    internal HistoryStressProfile(IEnumerable<HistoryFiberResult> fibers, Func<HistoryFiberResult, double> value)
    {
        samples = fibers.GroupBy(f => f.ComponentId).ToDictionary(g => g.Key,
            g => g.GroupBy(f => f.Fiber.Y).OrderBy(p => p.Key).Select(p => (p.Key, p.Average(value))).ToArray());
    }
    public double Stress(string material, double y)
    {
        string key = material == "CLS" ? HBridgeHistoryAnalysis.Concrete : material == "Armatura" ? HBridgeHistoryAnalysis.Rebars :
            samples.Where(s => s.Key.StartsWith("Steel.")).OrderBy(s => Distance(s.Value, y)).Select(s => s.Key).FirstOrDefault() ?? "";
        if (!samples.TryGetValue(key, out var a) || a.Length == 0) return 0;
        if (y <= a[0].Y) return a[0].Stress;
        for (int i = 1; i < a.Length; i++) if (y <= a[i].Y)
            return a[i - 1].Stress + (a[i].Stress - a[i - 1].Stress) * (y - a[i - 1].Y) / (a[i].Y - a[i - 1].Y);
        return a[a.Length - 1].Stress;
    }
    private static double Distance((double Y, double Stress)[] a, double y) => y < a[0].Y ? a[0].Y - y : y > a[a.Length - 1].Y ? y - a[a.Length - 1].Y : 0;
}

public sealed record HistoryStageView(HistoryStageResult State, bool Nonlinear, HistoryStressProfile Profile,
    double TransformedArea, double ElasticCentroid, double TransformedInertia);

public static class HBridgeHistoryResults
{
    public const string LinearMethod = "Storico lineare · deformazioni e ritiro incrementali";
    public const string NonlinearMethod = "Storico non lineare · fibre e plasticità dell’acciaio";
    public const string Scope = "Compatibilità delle deformazioni con riferimento al getto; equilibrio N–Mx a ogni fase. " +
        "Ritiri incrementali illimitati. Moduli efficaci del lineare applicati ai soli incrementi, senza integrazione della viscosità nel tempo. " +
        "Non lineare: acciaio con memoria plastica isotropa, CLS sull’inviluppo Model (scarico sullo stesso inviluppo, senza danno ciclico). " +
        "Legami caratteristici senza coefficienti di resistenza. Non lineare sulla sezione lorda: instabilità locale non inclusa. " +
        "V è registrato; taglio, pioli, irrigidimenti e interazione N–M–V non verificati da questo metodo.";

    /// <summary>Unified presentation result without calling/recalculating the legacy cumulative solver.</summary>
    public static HBridgeAnalysisResult Calculate(HBridgeInput input, HBridgeHistoryOptions? options = null, CancellationToken cancellation = default)
    {
        var o = options ?? new();
        var data = input.Snapshot();
        BridgeNumbers.Require(data.Options.GammaM0, "gamma_m0", strict: true);
        BridgeNumbers.Require(data.Options.GammaC, "gamma_c", strict: true);
        BridgeNumbers.Require(data.Options.GammaS, "gamma_s", strict: true);
        BridgeNumbers.Require(data.Options.AlphaCC, "alpha_cc", strict: true);
        if (!Enum.IsDefined(typeof(BridgeLimitState), data.Options.LimitState)) throw new ArgumentException("Stato limite sconosciuto.");
        var section = HBridgeHistoryAnalysis.CreateSection(data, o);
        bool nonlinear = section.Components.Any(c => !c.Material.IsLinear);
        var history = BridgeHistoryAnalysis.Calculate(section, HBridgeHistoryAnalysis.ConvertPhases(data, data.Phases, o), o.Solver, cancellation);
        if (history.Stages.Count == 0) throw new ArgumentException("Attivare almeno una fase.");
        var g = HBridgeSection.Geometry(data with { Phases = new[] { new BridgePhase() } });
        var m = data.Materials;
        var values = new BridgeMaterialValues(m.Concrete.Name, Math.Abs(m.Concrete.Fck), m.Concrete.ElasticModulusCompression,
            m.Steel.Name, m.Steel.Fyk, m.Steel.ElasticModulusTension, m.Rebar.Name, m.Rebar.Fyk, m.Rebar.ElasticModulusTension);
        var phases = data.Phases.Where(p => p.Active).ToArray();
        var stages = new List<BridgeStage>(); var contributions = new List<BridgeContribution>();
        Dictionary<string, double> previous = new();
        foreach (var s in history.Stages)
        {
            cancellation.ThrowIfCancellationRequested();
            var p = phases[s.Index]; var active = s.Fibers.Where(f => f.Active).ToArray();
            var profile = new HistoryStressProfile(s.Fibers, f => f.Active ? f.Stress : 0);
            double E(HistoryFiberResult f) => f.ComponentId == HBridgeHistoryAnalysis.Concrete ? values.Ec * f.ModulusFactor : f.ComponentId == HBridgeHistoryAnalysis.Rebars ? values.Es : values.Ea;
            double area = active.Sum(f => f.EffectiveArea * E(f) / values.Ea);
            double cy = active.Sum(f => f.EffectiveArea * E(f) / values.Ea * f.Fiber.Y) / area;
            double inertia = active.Sum(f => f.EffectiveArea * E(f) / values.Ea * Math.Pow(f.Fiber.Y - cy, 2));
            var steel = active.Where(f => f.ComponentId.StartsWith("Steel.")).ToArray();
            double aa = steel.Sum(f => f.EffectiveArea), ya = steel.Sum(f => f.EffectiveArea * f.Fiber.Y) / aa;
            var steelProperties = new BridgeSteelProperties(aa, ya, steel.Sum(f => f.EffectiveArea * Math.Pow(f.Fiber.Y - ya, 2)));
            var eff = BridgeEffective.Full(g);
            if (s.Panels.Count > 0)
            {
                var web = s.Panels.Single(x => x.Name == HBridgeHistoryAnalysis.Web).Reduction;
                var top = s.Panels.Single(x => x.Name == HBridgeHistoryAnalysis.Top).Reduction;
                var bottom = s.Panels.Single(x => x.Name == HBridgeHistoryAnalysis.Bottom).Reduction;
                eff = new(web.EffectiveAtStart, web.EffectiveAtEnd, g.WebThickness + 2 * top.EffectiveAtStart,
                    g.WebThickness + 2 * bottom.EffectiveAtStart, web, top, bottom);
            }
            var h = p.Kind is BridgePhaseKind.Composite or BridgePhaseKind.Shrinkage ? CompositeHomogenization.Calculate(m,
                o.InstantaneousConcrete ? p with { Phi = 0, HomogenizationSource = BridgeHomogenizationSource.Phi } : p) : (N0: 0d, N: 0d, PhiEffective: 0d, Phi: 0d);
            var ap = s.AppliedPhase;
            var c = new BridgeContribution(s.Name, p.KindName, ap.DeltaN / 1000, ap.DeltaM / 1e6, h.N0, h.N, h.Phi, h.PhiEffective,
                area, cy, inertia, values.Ea * s.IncrementPlane.At(cy), -values.Ea * s.IncrementPlane.Curvature, values.Es / values.Ea,
                active.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Rebars).Sum(f => f.EffectiveArea), inertia / Math.Abs(-g.Height - cy),
                Math.Abs(g.SlabHeight - cy) < 1e-12 ? null : inertia / Math.Abs(g.SlabHeight - cy), inertia,
                Math.Max(Math.Abs(s.ForceResidual) / Math.Max(1, Math.Abs(s.N)), Math.Abs(s.MomentResidual) / Math.Max(1, Math.Abs(s.MomentAtOrigin))),
                s.IncrementApplicationY, p.ReferenceName, ap.DeltaV / 1000, p.Kind == BridgePhaseKind.Shrinkage ? p.ShrinkageMicrostrain * 1e-6 : 0);
            c.HistoryProfile = new HistoryStressProfile(s.Fibers, f => (f.Active ? f.Stress : 0) - (previous.TryGetValue(f.Fiber.Id, out double v) ? v : 0));
            contributions.Add(c);
            previous = s.Fibers.ToDictionary(f => f.Fiber.Id, f => f.Active ? f.Stress : 0);
            double lc = data.Options.LimitState == BridgeLimitState.Ultimate ? data.Options.AlphaCC * values.Fck / data.Options.GammaC :
                (data.Options.LimitState == BridgeLimitState.Rare ? .6 : .45) * values.Fck;
            double la = data.Options.LimitState == BridgeLimitState.Ultimate ? values.Fy / data.Options.GammaM0 : values.Fy;
            double ls = data.Options.LimitState == BridgeLimitState.Ultimate ? values.Fys / data.Options.GammaS : .8 * values.Fys;
            var points = new List<BridgeStressPoint>();
            void Point(string name, string material, double y, double limit, bool enabled)
            {
                double sigma = profile.Stress(material, y);
                var delta = contributions.Select(x => x.Stress(material, y)).ToArray();
                points.Add(new(name, material, y, sigma, limit, !enabled || nonlinear || material == "CLS" && sigma > 1e-6 ? null : Math.Abs(sigma) / limit, enabled, delta));
            }
            bool concreteActive = active.Any(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete);
            Point("Soletta · estradosso (fibra prossima)", "CLS", g.SlabHeight, lc, concreteActive);
            Point("Soletta · intradosso (fibra prossima)", "CLS", 0, lc, concreteActive);
            foreach (var row in g.Bars.GroupBy(b => b.Y)) Point("Armatura · y=" + row.Key.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), "Armatura", row.Key, ls, active.Any(f => f.ComponentId == HBridgeHistoryAnalysis.Rebars));
            Point("Acciaio · estradosso (fibra prossima)", "Acciaio", 0, la, true);
            Point("Anima · sommità", "Acciaio", -g.TopThickness, la, true);
            Point("Anima · piede", "Acciaio", -g.TopThickness - g.WebHeight, la, true);
            Point("Acciaio · intradosso (fibra prossima)", "Acciaio", -g.Height, la, true);
            // Add actual integration extrema: a curved stress profile need not peak at a section endpoint.
            foreach (var group in active.GroupBy(f => f.ComponentId))
                foreach (var extreme in new[] { group.OrderBy(f => f.Stress).First(), group.OrderByDescending(f => f.Stress).First() }.Distinct())
                {
                    string material = group.Key == HBridgeHistoryAnalysis.Concrete ? "CLS" : group.Key == HBridgeHistoryAnalysis.Rebars ? "Armatura" : "Acciaio";
                    Point(group.Key + " · estremo integrato", material, extreme.Fiber.Y, material == "CLS" ? lc : material == "Armatura" ? ls : la, true);
                }
            var warnings = new List<string> { "Metodo con storico: V registrato; verifiche di taglio, connessione, irrigidimenti e N–M–V da completare. Nessun esito globale favorevole.",
                "Tensioni alle facce: valore della fibra di integrazione più vicina; controllare la convergenza della discretizzazione." };
            if (nonlinear) warnings.Add("Non lineare su sezione lorda, materiali caratteristici: analisi N–Mx, non verifica SLU di classe 4. CLS sull’inviluppo Model, senza danno né isteresi ciclica; acciaio con deformazioni plastiche residue.");
            if (o.InstantaneousConcrete) warnings.Add("Analisi istantanea esplicita: φ=0 nel calcolo; i φ/n dell’archivio restano memorizzati per il metodo lineare.");
            else warnings.Add("φ/n modifica il modulo dei nuovi incrementi; non viene integrata una legge di viscosità dipendente dall’età.");
            if (!data.Options.Class4 && !nonlinear) warnings.Add("Riduzioni di classe 4 disattivate: sezione lorda.");
            var stage = new BridgeStage(s.Name, s.NewtonIterations, s.EffectiveResidual, eff, steelProperties, contributions.ToList(), points, warnings);
            stage.HistoryView = new(s, nonlinear, profile, area, cy, inertia);
            stages.Add(stage);
        }
        return new(nonlinear ? NonlinearMethod : LinearMethod, Scope, data, g, values, stages);
    }
}
