namespace GPC.Checkers.CompositeBridge;

/// <summary>A converged situation on one common effective geometry. This is not construction-history analysis.</summary>
public sealed record CumulativeSituation<TState, TContribution>(int PhaseIndex, TState State,
    IReadOnlyList<TContribution> Contributions, int Iterations, double Residual);

/// <summary>
/// Geometry-independent iteration of cumulative load increments. An adapter owns the section state,
/// the stress solver, plate reductions and a dimensionless distance. Each situation starts from gross geometry;
/// every included increment is recomputed on the same trial geometry. State may contain any number of panels.
/// </summary>
public static class CumulativePhaseAnalysis
{
    public static IEnumerable<CumulativeSituation<TState, TContribution>> Analyze<TPhase, TState, TContribution>(
        IReadOnlyList<TPhase> phases, Func<TState> grossState,
        Func<TState, TPhase, TContribution> solve,
        Func<IReadOnlyList<TContribution>, TState> reduce,
        Func<TState, TState, double> distance,
        Func<TState, TState, double, TState> relax,
        Func<TPhase, string> phaseName,
        CancellationToken cancellation = default,
        int maximumIterations = 120, double tolerance = 1e-7, double relaxation = .55)
    {
        if (maximumIterations < 1) throw new ArgumentOutOfRangeException(nameof(maximumIterations));
        BridgeNumbers.Require(tolerance, nameof(tolerance), strict: true);
        if (!BridgeNumbers.IsFinite(relaxation) || relaxation <= 0 || relaxation > 1) throw new ArgumentOutOfRangeException(nameof(relaxation));
        // Capture membership/order; adapters must not mutate phase or state objects during analysis.
        var snapshot = phases.ToArray();
        for (int end = 0; end < snapshot.Length; end++)
        {
            cancellation.ThrowIfCancellationRequested();
            var state = grossState();
            bool converged = false;
            for (int iteration = 1; iteration <= maximumIterations; iteration++)
            {
                cancellation.ThrowIfCancellationRequested();
                var contributions = snapshot.Take(end + 1).Select(p => solve(state, p)).ToArray();
                var next = reduce(contributions);
                double error = distance(state, next);
                if (!BridgeNumbers.IsFinite(error) || error < 0) throw new InvalidOperationException("Residuo della sezione efficace non valido.");
                if (error < tolerance)
                {
                    // Return the state actually used to calculate stresses, not the next trial state.
                    converged = true;
                    yield return new(end, state, Array.AsReadOnly(contributions), iteration, error);
                    break;
                }
                state = relax(state, next, relaxation);
            }
            if (!converged) throw new InvalidOperationException($"{phaseName(snapshot[end])}: sezione efficace non convergente dopo {maximumIterations} iterazioni. Nessun esito utilizzabile.");
        }
    }
}
