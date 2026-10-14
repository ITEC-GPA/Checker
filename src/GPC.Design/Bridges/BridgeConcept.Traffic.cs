using System.Collections.Concurrent;
using System.Globalization;

namespace GPC.Design.Bridges;

public sealed record BridgeTrafficSummary(TrafficModel Model, string Reference, int Lanes, double UniformKnPerMetre,
    double TandemKn, double GammaG, double GammaQ, string Scope);

public static partial class BridgeConcept
{
    private sealed record BeamLoadResult(double[] Reactions, double[] MinimumReactions, Station[] Stations);
    private static readonly ConcurrentDictionary<string, Lazy<TrafficEnvelopeResult>> TrafficCache = new();
    private static readonly ConcurrentQueue<string> TrafficCacheOrder = new();
    private static RoadTraffic TrafficLayout(BridgeInput i) => i.Median > 0
        ? RoadTraffic.Ntc2018(i.Lanes * i.LaneWidth / 2 + i.Shoulder, i.Lanes * i.LaneWidth / 2 + i.Shoulder)
        : RoadTraffic.Ntc2018(i.Lanes * i.LaneWidth + 2 * i.Shoulder);

    private static TrafficEnvelopeResult TrafficEnvelope(double[] spans, bool continuous, RoadTraffic traffic)
    {
        string key = (continuous ? "C/" : "S/") + string.Join("/", spans.Concat([traffic.UniformKnPerMetre, traffic.AxleKn]).Select(x => x.ToString("R", CultureInfo.InvariantCulture)));
        var proposed = new Lazy<TrafficEnvelopeResult>(() => new BeamTrafficEnvelope(spans, continuous, traffic).Calculate(1));
        var cached = TrafficCache.GetOrAdd(key, proposed);
        if (ReferenceEquals(cached, proposed))
        {
            TrafficCacheOrder.Enqueue(key);
            while (TrafficCache.Count > 128 && TrafficCacheOrder.TryDequeue(out var oldest)) TrafficCache.TryRemove(oldest, out _);
        }
        return cached.Value;
    }
    private static BeamLoadResult Loads(BridgeModel model, double[] spans, double permanent, double ei, bool factored)
    {
        var i = model.Input; var a = model.Assumptions;
        double gg = factored ? a.GammaG : 1, gq = factored ? a.GammaQ : 1;
        if (i.TrafficModel == TrafficModel.LegacyUniform)
        {
            double width = i.Lanes * i.LaneWidth + 2 * i.Shoulder + i.Median + 2 * i.BarrierWidth;
            double live = width * a.TrafficLoad;
            var legacy = Analyze(spans, gg * permanent + gq * live, ei, i.Continuous);
            return new(legacy.Reactions, legacy.Reactions, legacy.Stations);
        }
        var g = Analyze(spans, gg * permanent, ei, i.Continuous);
        var q = TrafficEnvelope(spans, i.Continuous, TrafficLayout(i));
        var maxima = g.Reactions.Zip(q.Reactions, (p, t) => p + gq * t.Maximum).ToArray();
        var minima = g.Reactions.Zip(q.Reactions, (p, t) => p + gq * t.Minimum).ToArray();
        var fixedStations = g.Stations.ToDictionary(s => (s.Span, s.X));
        // Legacy Analyze keeps its original arithmetic, including L*40/40 at the final station.
        // Alias that ordinate to the exact geometric endpoint; do not perturb legacy results.
        double offset = 0;
        for (int k = 0; k < spans.Length; k++)
        {
            offset += spans[k];
            fixedStations[(k + 1, offset)] = g.Stations.Last(s => s.Span == k + 1);
        }
        var stations = q.Sections.Select(t =>
        {
            var p = fixedStations[(t.Span, t.X)];
            return new Station(t.Span, t.X, p.Moment + gq * t.Moment.Maximum, p.Shear + gq * t.Shear.Maximum,
                p.DeflectionMm + gq * t.DeflectionMm.Maximum / ei)
            { MomentMinimum = p.Moment + gq * t.Moment.Minimum, ShearMinimum = p.Shear + gq * t.Shear.Minimum,
                DeflectionMinimumMm = p.DeflectionMm + gq * t.DeflectionMm.Minimum / ei };
        }).ToArray();
        return new(maxima, minima, stations);
    }
    private static BridgeTrafficSummary? TrafficSummary(BridgeModel data, bool upper = false)
    {
        if (data.Input.TrafficModel == TrafficModel.LegacyUniform) return null;
        var t = TrafficLayout(data.Input);
        return new(TrafficModel.Ntc2018Schema1, RoadTraffic.Reference, t.Lanes.Count, t.UniformKnPerMetre, 2 * t.AxleKn,
            data.Assumptions.GammaG, data.Assumptions.GammaQ, upper
                ? "Strutture superiori: azioni globali equivalenti e carichi locali sui due piani; stima parametrica, non analisi di arco/cavi."
                : "Inviluppi longitudinali su trave prismatica a EI costante, 41 sezioni/campata. Estremi non concomitanti; nessuna ripartizione trasversale o verifica locale.");
    }
    private static void DescribeTraffic(List<Detail> details, BridgeModel data, BeamLoadResult service, BeamLoadResult factored, List<string> warnings)
    {
        if (data.Input.TrafficModel == TrafficModel.LegacyUniform) return;
        var t = TrafficLayout(data.Input);
        int at = details.FindIndex(d => d.Name == "Traffico equivalente sull'intero impalcato");
        if (at >= 0) details[at] = new("Schema 1 · carico distribuito", t.UniformKnPerMetre, "kN/m", RoadTraffic.Reference + "; solo porzioni sfavorevoli");
        details.Add(new("Schema 1 · peso totale tandem", 2 * t.AxleKn, "kN", $"{t.Lanes.Count} corsie convenzionali, due assi a 1.20 m; massimo/minimo indipendente per ogni effetto"));
        details.Add(new("Moltiplicatore traffico adottato", data.Assumptions.GammaQ, "—", "Valore esplicito del progetto; NTC 2018 Tabella 5.1.V, A1 sfavorevole: 1.35"));
        for (int k = 0; k < details.Count; k++)
        {
            var d = details[k];
            if (d.Name == "Momento positivo indicativo") details[k] = d with { Rule = "Inviluppo massimo γG G + γQ Q, Schema 1, EI costante" };
            if (d.Name == "Momento negativo indicativo") details[k] = d with { Value = Math.Min(0, factored.Stations.Min(s => s.MomentMinimum ?? s.Moment)), Rule = "Inviluppo minimo γG G + γQ Q, Schema 1, EI costante" };
            if (d.Name == "Freccia elastica indicativa") details[k] = d with { Value = service.Stations.Max(s => Math.Max(Math.Abs(s.DeflectionMm), Math.Abs(s.DeflectionMinimumMm ?? s.DeflectionMm))), Rule = "Massima assoluta dell'inviluppo G + Q; sezione lorda non fessurata, senza viscosità/precompressione" };
        }
        if (service.MinimumReactions.Any(x => x < -1e-8)) warnings.Add("Inviluppo minimo G + Q: possibile sollevamento; dispositivi antisollevamento non dimensionati.");
        warnings.Add("Schema 1: massimi/minimi di sezioni e appoggi non concomitanti; non sommare le reazioni dell'inviluppo. G2 trattato come permanente portato ben definito col γG esplicito.");
        if (data.Input.Length > 200) warnings.Add("Lunghezza caricata oltre 200 m: richiede una valutazione specifica del traffico; Schemi 6 e grandi luci non trattati dal modello parametrico.");
    }
    private static void EnvelopeCostWarnings(List<string> warnings, double length, double[] reactions, int devicesPerEnd, bool continuous, double bearingRate)
    {
        double maximum = reactions.Select((r, k) => r / (devicesPerEnd * (continuous || k == 0 || k == reactions.Length - 1 ? 1 : 2))).Max();
        CostWarnings(warnings, length, maximum, 1, bearingRate, true);
    }
}
