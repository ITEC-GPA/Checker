using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics.EmbeddedWalls;

namespace GPC.Design.EmbeddedWalls;

/// <summary>An aggregate criterion for the entire case history. Stage/elevation identify its governing observation,
/// when relevant. Demand, limit and utilization keep their declared units; this record applies no factors.</summary>
public sealed record WallEmbedmentCriterion(string CaseId, string Id, EngineeringOutcome Outcome,
    double? Demand, double? Limit, double? Utilization, string Unit, string Method, string Diagnostic,
    string? StageId = null, double? Elevation = null);

/// <summary>All independently computed histories and aggregate criteria at this toe. Payload carries the same
/// complete application result to the caller; the search never recalculates it for display or reporting.</summary>
public sealed record WallEmbedmentEvaluation<T>(double ToeElevation, IReadOnlyList<WallAnalysisResult> Histories,
    IReadOnlyList<WallEmbedmentCriterion> Criteria, T Payload);

public sealed record WallEmbedmentCandidate<T>(double Embedment, double ToeElevation, EngineeringOutcome Outcome,
    WallEmbedmentEvaluation<T>? Evaluation, string Diagnostic);

public sealed record WallEmbedmentSearchProgress(int Evaluated, int Planned, double Embedment, EngineeringOutcome Outcome);

public sealed record WallEmbedmentSearchResult<T>(bool Completed, bool Cancelled, bool MinimumCertified,
    double ReferenceElevation, double Increment, string Source, IReadOnlyList<double> PlannedEmbedments,
    IReadOnlyList<WallEmbedmentCandidate<T>> Candidates, WallEmbedmentCandidate<T>? Best, string Diagnostic);

/// <summary>Exhaustive finite search over assigned constructible depths. No monotonicity, bisection,
/// interpolation of nonlinear results or continuation of one candidate's state into another candidate.
/// The evaluator must reconstruct the geometry, seepage and every complete history from the inputs.</summary>
public static class EmbeddedWallEmbedment
{
    private const int MaximumCandidates = 4096;
    private sealed record History(string CaseId, string[] Stages, string[] Checks);
    private sealed record Plan(double Reference, double Top, double Increment, string Source, double[] Depths, History[] Histories);

    /// <summary>Upper endpoint included explicitly even when the increment does not divide the interval.
    /// Returns an independent, read-only grid and validates the full history/check contract.</summary>
    public static IReadOnlyList<double> Depths(EmbeddedWallEmbedmentSearch definition) => Array.AsReadOnly(Prepare(definition).Depths);

    /// <summary>All requested checks in one family, across stages and sections. Empty/unsupported families never pass.
    /// A demonstrated failure excludes a candidate even if another check is unavailable. No capacities or factors recomputed.</summary>
    public static WallEmbedmentCriterion Aggregate(string caseId, string id, IEnumerable<WallEmbedmentCriterion> observations)
    {
        Guard.NotNull(observations);
        if (string.IsNullOrWhiteSpace(caseId) || string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Case and criterion identifiers required.");
        var rows = observations.ToArray();
        if (rows.Any(c => c is null || c.CaseId != caseId || !PortableEnum.IsDefined(c.Outcome)
            || new[] { c.Demand, c.Limit, c.Utilization, c.Elevation }.Any(v => v.HasValue && !PortableMath.IsFinite(v.Value))
            || c.Utilization < 0 || c.Outcome == EngineeringOutcome.Satisfied && c.Utilization > 1))
            throw new ArgumentException("Inconsistent check family or nonfinite result.");
        if (rows.Length == 0) return new(caseId, id, EngineeringOutcome.NotEvaluated, null, null, null, "", id, "Nessuna verifica disponibile nella famiglia richiesta.");
        var governing = rows.OrderBy(c => c.Outcome == EngineeringOutcome.NotSatisfied ? 0 : c.Outcome == EngineeringOutcome.NotEvaluated ? 1 : 2)
            .ThenByDescending(c => c.Utilization).First();
        return governing with { Id = id, Method = id + ": " + governing.Method,
            Diagnostic = rows.Length + " controlli; esito governante: " + governing.Diagnostic };
    }

    public static WallEmbedmentSearchResult<T> Run<T>(EmbeddedWallEmbedmentSearch definition,
        Func<double, CancellationToken, WallEmbedmentEvaluation<T>> evaluate, CancellationToken cancellation = default,
        IProgress<WallEmbedmentSearchProgress>? progress = null)
    {
        Guard.NotNull(evaluate);
        var plan = Prepare(definition); var rows = new List<WallEmbedmentCandidate<T>>(); bool cancelled = false;
        foreach (double depth in plan.Depths)
        {
            if (cancellation.IsCancellationRequested) { cancelled = true; break; }
            double toe = plan.Reference - depth;
            WallEmbedmentEvaluation<T>? evaluation = null; EngineeringOutcome outcome; string diagnostic;
            try
            {
                evaluation = evaluate(toe, cancellation);
                cancellation.ThrowIfCancellationRequested();
                (outcome, diagnostic) = Inspect(plan, toe, evaluation);
                if (evaluation is not null)
                    evaluation = evaluation with { Histories = Array.AsReadOnly(evaluation.Histories.ToArray()), Criteria = Array.AsReadOnly(evaluation.Criteria.ToArray()) };
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { cancelled = true; break; }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or ArithmeticException)
            {
                outcome = EngineeringOutcome.NotEvaluated; diagnostic = error.GetType().Name + ": " + error.Message;
                evaluation = null;
            }
            rows.Add(new(depth, toe, outcome, evaluation, diagnostic));
            progress?.Report(new(rows.Count, plan.Depths.Length, depth, outcome));
        }
        // A cancellation from the last progress callback still makes the search non-final.
        cancelled |= cancellation.IsCancellationRequested;
        bool completed = !cancelled && rows.Count == plan.Depths.Length;
        var best = rows.FirstOrDefault(r => r.Outcome == EngineeringOutcome.Satisfied);
        bool certified = completed && best is not null && rows.TakeWhile(r => !ReferenceEquals(r, best)).All(r => r.Outcome == EngineeringOutcome.NotSatisfied);
        string summary = cancelled ? "Ricerca annullata; valutazioni concluse conservate, nessun minimo definitivo."
            : best is null ? rows.Any(r => r.Outcome == EngineeringOutcome.NotEvaluated)
                ? "Nessun candidato accettato; esiti non valutabili impediscono una conclusione definitiva."
                : "Nessun candidato soddisfa tutti i criteri assegnati nell'intervallo esplorato."
            : certified ? "Minimo dimostrato sulla griglia assegnata, per le storie e i criteri richiesti."
                : "Candidato accettabile trovato; profondità inferiori non valutabili impediscono di certificarne il minimo.";
        return new(completed, cancelled, certified, plan.Reference, plan.Increment, plan.Source,
            Array.AsReadOnly(plan.Depths), rows.AsReadOnly(), best, summary);
    }

    /// <summary>Absolute displacement at the HEAD, maximum over all completed stages; mm.
    /// This is not a maximum over the entire wall, and is not a normative limit selected by the library.</summary>
    public static WallEmbedmentCriterion HeadDisplacement(WallAnalysisResult analysis, double limit, string limitSource)
    {
        Guard.NotNull(analysis);
        if (!PortableMath.IsFinite(limit) || limit <= 0 || string.IsNullOrWhiteSpace(limitSource))
            throw new ArgumentException("Positive head-displacement limit and its engineering source required.");
        const string id = "HeadDisplacement";
        if (!analysis.IsComplete || analysis.Stages.Count == 0 || analysis.Stages.Any(s => s.Points.Count == 0))
            return new(analysis.CaseId, id, EngineeringOutcome.NotEvaluated, null, limit, null, "mm", limitSource, "Storia incompleta: spostamento non verificabile.");
        var governing = analysis.Stages.Select(s => (Stage: s, Point: s.Points.OrderByDescending(p => p.Elevation).First()))
            .OrderByDescending(p => Math.Abs(p.Point.Displacement)).First();
        double demand = Math.Abs(governing.Point.Displacement), utilization = demand / limit;
        if (!PortableMath.IsFinite(demand) || !PortableMath.IsFinite(utilization))
            return new(analysis.CaseId, id, EngineeringOutcome.NotEvaluated, null, limit, null, "mm", limitSource, "Spostamento non finito.");
        return new(analysis.CaseId, id, demand <= limit ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied,
            demand, limit, utilization, "mm", limitSource, "Massimo assoluto in testa fra le fasi della stessa storia.", governing.Stage.Id, governing.Point.Elevation);
    }

    private static Plan Prepare(EmbeddedWallEmbedmentSearch definition)
    {
        Guard.NotNull(definition);
        double reference = definition.ReferenceElevation, top = definition.WallTop, minimum = definition.MinimumEmbedment,
            maximum = definition.MaximumEmbedment, step = definition.Increment;
        if (!PortableMath.IsFinite(reference) || !PortableMath.IsFinite(top) || reference > top || !PortableMath.IsFinite(minimum) || minimum <= 0
            || !PortableMath.IsFinite(maximum) || maximum < minimum || !PortableMath.IsFinite(step) || step <= 0
            || !PortableMath.IsFinite(reference - maximum) || !PortableMath.IsFinite(top - (reference - maximum))
            || string.IsNullOrWhiteSpace(definition.Source))
            throw new ArgumentException("Finite elevations, positive embedments/increment, ordered interval and provenance required.");
        double divisions = Math.Floor((maximum - minimum) / step);
        if (!PortableMath.IsFinite(divisions) || divisions >= MaximumCandidates)
            throw new ArgumentException("The exhaustive search supports at most 4096 candidates.");
        var depths = new List<double>();
        for (int i = 0; i <= (int)divisions; i++)
        {
            double value = minimum + i * step;
            if (value < maximum) depths.Add(value);
        }
        depths.Add(maximum);
        if (depths.Count > MaximumCandidates || depths.Zip(depths.Skip(1), (first, second) => (First: first, Second: second)).Any(p => p.First >= p.Second)
            || depths.Any(d => reference - d >= top) || depths.Select(d => reference - d).Distinct().Count() != depths.Count)
            throw new ArgumentException("The requested grid loses distinct depths at machine precision or exceeds the candidate limit.");
        if (definition.Histories is null || definition.Histories.Count == 0) throw new ArgumentException("At least one complete history required.");
        var histories = new List<History>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var history in definition.Histories)
        {
            if (history is null || string.IsNullOrWhiteSpace(history.CaseId) || !ids.Add(history.CaseId))
                throw new ArgumentException("Unique, nonempty history identifiers required.");
            histories.Add(new(history.CaseId, Identifiers(history.StageIds, "stages"), Identifiers(history.RequiredCheckIds, "criteria")));
        }
        return new(reference, top, step, definition.Source, depths.ToArray(), histories.ToArray());
    }

    private static string[] Identifiers(IReadOnlyList<string> input, string name)
    {
        if (input is null || input.Count == 0 || input.Any(string.IsNullOrWhiteSpace) || input.Distinct(StringComparer.Ordinal).Count() != input.Count)
            throw new ArgumentException("Nonempty, unique " + name + " required.");
        return input.ToArray();
    }

    private static (EngineeringOutcome, string) Inspect<T>(Plan plan, double toe, WallEmbedmentEvaluation<T>? evaluation)
    {
        if (evaluation is null || !PortableMath.IsFinite(evaluation.ToeElevation) || evaluation.ToeElevation != toe
            || evaluation.Histories is null || evaluation.Criteria is null)
            throw new ArgumentException("The evaluator did not return the requested toe, histories and criteria.");
        if (evaluation.Histories.Any(h => h is null)) throw new ArgumentException("Null analysis in the evaluated history set.");
        var histories = evaluation.Histories.ToDictionary(h => h.CaseId, StringComparer.Ordinal);
        if (histories.Count != plan.Histories.Length || plan.Histories.Any(h => !histories.ContainsKey(h.CaseId)))
            throw new ArgumentException("The evaluated histories do not match the complete requested case set.");
        foreach (var expected in plan.Histories)
        {
            var actual = histories[expected.CaseId];
            if (!actual.IsComplete) return (EngineeringOutcome.NotEvaluated, expected.CaseId + ": " + actual.Status + "; " + actual.Diagnostic);
            if (!actual.Stages.Select(s => s.Id).SequenceEqual(expected.Stages))
                throw new ArgumentException("A converged result omits or reorders the requested construction stages.");
            foreach (var stage in actual.Stages)
                if (stage.CaseId != expected.CaseId || stage.Points.Count == 0
                    || stage.Points.Any(p => !PortableMath.IsFinite(p.Elevation))
                    || Math.Abs(stage.Points.Min(p => p.Elevation) - toe) > 1e-6
                    || Math.Abs(stage.Points.Max(p => p.Elevation) - plan.Top) > 1e-6)
                    throw new ArgumentException("The stage result does not cover the requested wall geometry/case.");
        }
        var keys = new HashSet<(string, string)>();
        foreach (var check in evaluation.Criteria)
        {
            if (check is null || !histories.ContainsKey(check.CaseId) || string.IsNullOrWhiteSpace(check.Id)
                || !keys.Add((check.CaseId, check.Id)) || !PortableEnum.IsDefined(check.Outcome)
                || string.IsNullOrWhiteSpace(check.Method) || string.IsNullOrWhiteSpace(check.Diagnostic)
                || new[] { check.Demand, check.Limit, check.Utilization, check.Elevation }.Any(v => v.HasValue && !PortableMath.IsFinite(v.Value))
                || check.Utilization < 0 || check.Outcome == EngineeringOutcome.Satisfied && check.Utilization > 1
                || check.StageId is not null && !histories[check.CaseId].Stages.Any(s => s.Id == check.StageId))
                throw new ArgumentException("Invalid, duplicated or inconsistent aggregate criterion from the evaluator.");
        }
        if (plan.Histories.Any(h => h.Checks.Any(id => !keys.Contains((h.CaseId, id)))))
            return (EngineeringOutcome.NotEvaluated, "Mancano criteri obbligatori per una o più storie.");
        if (evaluation.Criteria.Any(c => c.Outcome == EngineeringOutcome.NotSatisfied))
            return (EngineeringOutcome.NotSatisfied, "Almeno un criterio assegnato non è soddisfatto.");
        if (evaluation.Criteria.Any(c => c.Outcome == EngineeringOutcome.NotEvaluated))
            return (EngineeringOutcome.NotEvaluated, "Almeno un criterio assegnato non è valutabile.");
        return (EngineeringOutcome.Satisfied, "Tutte le storie sono complete e tutti i criteri assegnati sono soddisfatti.");
    }
}
