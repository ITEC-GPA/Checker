using GPC.Checkers.Geotechnics.Anchors;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics.Anchors;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;

namespace GPC.Design.EmbeddedWalls;

/// <summary>One physical anchor. Profile geometry must agree across investigations. StageScopeSource documents
/// the bonded zone's validity after all relevant excavations/groundwater changes. No reuse of factored local-wall
/// reactions is allowed unless the complete history was run with the anchor A1+M1 action basis.</summary>
public sealed record EmbeddedWallAnchor(string SupportId, bool Permanent, GroundAnchorEvidence Evidence,
    IReadOnlyList<GroundAnchorBond> CalculatedProfiles, IReadOnlyList<GroundAnchorResistanceSample> MeasuredTests,
    string ExperimentalSource, string StageScopeSource);

public sealed record EmbeddedWallAnchorResult(string CaseId, string StageId, string SupportId, double NormalForce,
    IReadOnlyList<GroundAnchorBondPrediction> Predictions, GroundAnchorPulloutResult? Verification,
    EngineeringOutcome Outcome, string Diagnostic);

public static class EmbeddedWallAnchors
{
    public static IReadOnlyList<EmbeddedWallAnchorResult> Check(WallAnalysisResult analysis,
        IReadOnlyList<EmbeddedWallAnchor> definitions, StandardGeotechnical standard, GeotechnicalSituation situation,
        bool anchorA1M1HistoryConfirmed, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(analysis); ArgumentNullException.ThrowIfNull(definitions); ArgumentNullException.ThrowIfNull(standard);
        if (!analysis.IsComplete) throw new InvalidOperationException("An incomplete nonlinear history cannot be certified by anchor checks.");
        var data = new Dictionary<string, EmbeddedWallAnchor>(StringComparer.Ordinal);
        foreach (var item in definitions)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.SupportId) || !Enum.IsDefined(item.Evidence)
                || item.CalculatedProfiles is null || item.MeasuredTests is null || !data.TryAdd(item.SupportId, item with
                { CalculatedProfiles = Array.AsReadOnly(item.CalculatedProfiles.ToArray()), MeasuredTests = Array.AsReadOnly(item.MeasuredTests.ToArray()) }))
                throw new ArgumentException("Unique anchors with explicit evidence collections required.");
        }
        var result = new List<EmbeddedWallAnchorResult>();
        foreach (var stage in analysis.Stages)
        foreach (var support in stage.Supports)
        {
            cancellation.ThrowIfCancellationRequested();
            if (support.Kind == EmbeddedWallSupportKind.Strut) continue;
            GroundAnchorPulloutResult? check = null; var predictions = new List<GroundAnchorBondPrediction>(); string diagnostic = "";
            if (!data.TryGetValue(support.Id, out var definition)) diagnostic = "Definizione geotecnica del tirante assente.";
            else if (!anchorA1M1HistoryConfirmed) diagnostic = "Serve una storia indipendente con azioni A1 e parametri M1 per lo sfilamento; non si rifattorizzano reazioni non lineari a posteriori.";
            else if (support.NormalForce < 0) diagnostic = "Il sostegno è compresso: la verifica di sfilamento a trazione non è applicabile.";
            else if (string.IsNullOrWhiteSpace(definition.StageScopeSource)) diagnostic = "Documentare l'applicabilità della geometria del bulbo e delle indagini a tutte le fasi selezionate.";
            else
            {
                var samples = new List<GroundAnchorResistanceSample>();
                if (definition.Evidence == GroundAnchorEvidence.CalculatedProfiles)
                {
                    if (definition.MeasuredTests.Count > 0) throw new ArgumentException("Calculated and measured samples cannot be pooled into one correlation population.");
                    GroundAnchorBond? geometry = null;
                    foreach (var profile in definition.CalculatedProfiles)
                    {
                        if (profile is null) throw new ArgumentException("Null anchor investigation profile.");
                        geometry ??= profile;
                        if (profile.FreeLength != geometry.FreeLength || profile.BondLength != geometry.BondLength
                            || profile.DrillDiameter != geometry.DrillDiameter || profile.MinimumCover != geometry.MinimumCover || profile.Injection != geometry.Injection)
                            throw new ArgumentException("Independent investigation profiles must predict the same physical anchor geometry and injection system.");
                        var prediction = GroundAnchorPullout.Predict(profile, cancellation); predictions.Add(prediction);
                        if (prediction.Applicable) samples.Add(new(profile.Id, prediction.Resistance!.Value,
                            "Bustamante-Doix / " + profile.ApplicabilitySource + "; " + string.Join("; ", profile.Layers.Select(l => l.PressureSource))));
                    }
                    if (predictions.Any(p => !p.Applicable)) diagnostic = "Uno o più profili non soddisfano il campo del metodo: capacità preliminari conservate, verifica non eseguita.";
                }
                else
                {
                    if (definition.CalculatedProfiles.Count > 0) throw new ArgumentException("Measured-anchor evidence must not contain calculated profile samples.");
                    samples.AddRange(definition.MeasuredTests);
                }
                if (diagnostic.Length == 0 && samples.Count == 0) diagnostic = "Mancano profili completi o prove preliminari documentate.";
                if (diagnostic.Length == 0)
                {
                    var factors = standard.GroundAnchorProfile(situation, definition.Permanent, definition.Evidence, samples.Count);
                    check = GroundAnchorPullout.Check(support.NormalForce, samples, factors, definition.ExperimentalSource, cancellation);
                    diagnostic = check.Diagnostic + " Campo per fasi: " + definition.StageScopeSource;
                }
            }
            if (support.AtCapacity) diagnostic += " Tirante al limite costitutivo assegnato nell'analisi; resta necessario verificare il meccanismo complessivo.";
            result.Add(new(analysis.CaseId, stage.Id, support.Id, support.NormalForce, predictions.AsReadOnly(), check,
                check?.Outcome ?? EngineeringOutcome.NotEvaluated, diagnostic));
        }
        return result.AsReadOnly();
    }
}
