using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics.Anchors;
using GPC.Model.Standards;

namespace GPC.Checkers.Geotechnics.Anchors;

public sealed record GroundAnchorBondSegment(int Layer, double Start, double End, double LimitPressure,
    double InjectionPressure, double DiameterFactor, double BulbDiameter, string Curve, double UnitResistance,
    double Resistance, string PressureSource);

/// <summary>Prediction, not a characteristic/design resistance or a complete anchor verification.</summary>
public sealed record GroundAnchorBondPrediction(GroundAnchorBond Input, double? Resistance,
    IReadOnlyList<GroundAnchorBondSegment> Segments, bool Applicable, IReadOnlyList<string> Diagnostics);

public sealed record GroundAnchorResistanceSample(string Id, double Resistance, string Source);

public sealed record GroundAnchorPulloutResult(double Demand, double? MeanResistance, double? MinimumResistance,
    double? CharacteristicResistance, double? DesignResistance, double? Utilization, GroundAnchorResistanceProfile? Profile,
    IReadOnlyList<GroundAnchorResistanceSample> Samples, EngineeringOutcome Outcome, string ExperimentalSource, string Diagnostic);

public static class GroundAnchorPullout
{
    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    /// <summary>Bounded Bustamante-Doix prediction from the existing digitised Viggiani charts. Static isolated,
    /// sufficiently confined grouted anchors; no end-bearing term, no extrapolation, no safety factor here.
    /// The deliberately bounded implementation requires >=5 m cover and >=4 m bonded axis; it does not assert
    /// that these are universal geometric limits of the original method. Near-surface extensions remain separate.</summary>
    public static GroundAnchorBondPrediction Predict(GroundAnchorBond anchor, CancellationToken cancellation = default)
    {
        if (anchor is null) throw new ArgumentNullException(nameof(anchor)); cancellation.ThrowIfCancellationRequested();
        var segments = new List<GroundAnchorBondSegment>(); var notes = new List<string>(); bool complete = true;
        if (anchor.MinimumCover < 5000) notes.Add("Campo corrente limitato a bulbi con ricoprimento minimo di 5 m rispetto a tutte le superfici libere; effetti superficiali non modellati.");
        if (anchor.BondLength < 4000) notes.Add("Campo corrente limitato a una lunghezza cementata di almeno 4 m; riduzioni per bulbi corti non modellate.");
        if (string.IsNullOrWhiteSpace(anchor.ExecutionSource)) notes.Add("Documentare tecnologia, quantità di miscela e controllo dell'esecuzione.");
        if (string.IsNullOrWhiteSpace(anchor.ApplicabilitySource)) notes.Add("Documentare confinamento, collocazione in terreno stabile, isolamento del bulbo e applicabilità empirica.");
        double end = anchor.FreeLength + anchor.BondLength;
        for (int i = 0; i < anchor.Layers.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested(); var layer = anchor.Layers[i];
            double start = Math.Max(anchor.FreeLength, layer.Start), finish = Math.Min(end, layer.End);
            if (finish <= start) continue;
            var soil = (BustamanteDoixSoil)Enum.Parse(typeof(BustamanteDoixSoil), layer.Soil.ToString());
            var injection = anchor.Injection == GroundAnchorInjection.IRS ? MicropileInjection.IRS : MicropileInjection.IGU;
            if (string.IsNullOrWhiteSpace(layer.PressureSource)) notes.Add($"Strato {i + 1}: manca la fonte della pressione limite pressiometrica; non è la pressione d'iniezione.");
            bool pressureValid = injection == MicropileInjection.IRS ? layer.InjectionPressure >= layer.LimitPressure
                : layer.InjectionPressure >= .5 * layer.LimitPressure && layer.InjectionPressure <= layer.LimitPressure;
            if (!pressureValid) notes.Add($"Strato {i + 1}: pressione d'iniezione fuori dal campo tecnologico dichiarato da Viggiani (IRS: pi≥pl; IGU: 0.5pl≤pi≤pl).");
            var range = BustamanteDoix.AlphaRange(soil, injection);
            if (layer.DiameterFactor < range.Min || layer.DiameterFactor > range.Max)
                notes.Add($"Strato {i + 1}: fattore del diametro fuori dall'intervallo documentato {range.Min:g}–{range.Max:g}; nessuna validazione implicita.");
            try
            {
                // Deliberately do NOT call Segments(..., injectionPressure): it preserves a historical micropile convention.
                var chart = BustamanteDoix.UnitShaftResistance(soil, injection, layer.LimitPressure, layer.DiameterFactor);
                double diameter = anchor.DrillDiameter * layer.DiameterFactor;
                double resistance = Math.PI * diameter * (finish - start) * chart.UnitResistance;
                if (!Finite(resistance)) throw new ArgumentException("Nonfinite calculated bond resistance.");
                segments.Add(new(i + 1, start, finish, layer.LimitPressure, layer.InjectionPressure, layer.DiameterFactor,
                    diameter, chart.Curve, chart.UnitResistance, resistance, layer.PressureSource));
            }
            catch (ArgumentException e) { complete = false; notes.Add($"Strato {i + 1}: " + e.Message); }
        }
        double? total = complete ? segments.Sum(s => s.Resistance) : null;
        if (total.HasValue && !Finite(total.Value)) throw new ArgumentException("Nonfinite total bond resistance.");
        return new(anchor, total, segments.AsReadOnly(), complete && notes.Count == 0, notes.AsReadOnly());
    }

    /// <summary>NTC2018 anchor pullout from complete unfactored samples. Demand is already a design action in N,
    /// positive tension, from a separately analysed A1+M1 history. No pile factors, action factors or layer-wise
    /// extrema are applied. ExperimentalSource documents the required site confirmation; an empty source keeps
    /// a numerically sufficient analytical prediction NotEvaluated.</summary>
    public static GroundAnchorPulloutResult Check(double demand, IReadOnlyList<GroundAnchorResistanceSample> samples,
        GroundAnchorResistanceProfile? profile, string experimentalSource, CancellationToken cancellation = default)
    {
        if (samples is null) throw new ArgumentNullException(nameof(samples)); cancellation.ThrowIfCancellationRequested();
        if (!Finite(demand) || demand < 0) throw new ArgumentException("A finite nonnegative design tensile action is required.");
        var copy = samples.ToArray(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sample in copy)
            if (sample == null || string.IsNullOrWhiteSpace(sample.Id) || !ids.Add(sample.Id) || !Finite(sample.Resistance)
                || sample.Resistance <= 0 || string.IsNullOrWhiteSpace(sample.Source))
                throw new ArgumentException("Unique, positive unfactored resistance samples with complete profile/test sources required.");
        var owned = Array.AsReadOnly(copy);
        if (profile is null) return new(demand, null, null, null, null, null, null, owned, EngineeringOutcome.NotEvaluated,
            experimentalSource ?? "", "Profilo normativo di sfilamento non disponibile per la situazione richiesta.");
        if (copy.Length != profile.EvidenceCount) throw new ArgumentException("Exactly one whole resistance sample per complete investigation profile or preliminary anchor test is required.");
        // Sum scaled terms to avoid overflow of a finite sample mean.
        double mean = copy.Sum(s => s.Resistance / copy.Length), minimum = copy.Min(s => s.Resistance);
        double characteristic = Math.Min(mean / profile.MeanCorrelationFactor, minimum / profile.MinimumCorrelationFactor);
        double design = characteristic / profile.ResistanceFactor, utilization = demand / design;
        if (!Finite(mean) || !Finite(design) || design <= 0 || !Finite(utilization))
            throw new ArgumentException("Resistance/demand scaling exceeds the finite arithmetic range.");
        var outcome = utilization > 1 ? EngineeringOutcome.NotSatisfied : string.IsNullOrWhiteSpace(experimentalSource)
            ? EngineeringOutcome.NotEvaluated : EngineeringOutcome.Satisfied;
        string diagnostic = "Verifica del solo sfilamento terreno–bulbo, A1+M1+R3. Acciaio, aderenza armatura–miscela, testa, stabilità globale, gruppo, durabilità e collaudo sono distinti.";
        if (string.IsNullOrWhiteSpace(experimentalSource)) diagnostic += " Manca la conferma sperimentale in sito richiesta da NTC §6.6.1; valori analitici utilizzabili per predimensionamento, esito non certificato.";
        cancellation.ThrowIfCancellationRequested();
        return new(demand, mean, minimum, characteristic, design, utilization, profile, owned, outcome, experimentalSource ?? "", diagnostic);
    }
}
