namespace GPC.Checkers.CompositeBridge;

/// <summary>A converged situation on one common effective geometry. This is not construction-history analysis.</summary>
public sealed record CumulativeSituation<TState, TContribution>(int PhaseIndex, TState State,
    IReadOnlyList<TContribution> Contributions, int Iterations, double Residual);

/// <summary>
/// Geometry-independent iteration of cumulative load increments. An adapter owns the section state,
/// the stress solver, plate reductions and a dimensionless distance. Every included increment is recomputed on the same trial geometry;
/// each situation starts from the gross geometry or, with warm start, from the converged geometry of the previous situation.
/// State may contain any number of panels.
/// </summary>
public static class CumulativePhaseAnalysis
{
    /// <summary>The smallest relaxation factor of the Aitken acceleration</summary>
    public const double MinimumRelaxation = GPC.Checkers.Steel.EffectiveSections.EffectiveSectionIteration.MinimumRelaxation;

    /// <param name="phases">The load increments, in order</param>
    /// <param name="grossState">The gross geometry</param>
    /// <param name="solve">The contribution of one increment on a trial geometry</param>
    /// <param name="reduce">The effective geometry of the stresses of the contributions</param>
    /// <param name="distance">The dimensionless distance of two geometries</param>
    /// <param name="relax">current (1 - f) + next f</param>
    /// <param name="phaseName">The name of a phase, for the messages</param>
    /// <param name="cancellation">The cancellation</param>
    /// <param name="maximumIterations">The maximum number of iterations of a situation</param>
    /// <param name="tolerance">The distance of convergence</param>
    /// <param name="relaxation">The relaxation factor (the first one with the Aitken acceleration)</param>
    /// <param name="coordinates">Dimensionless coordinates of a geometry: when given, the relaxation factor is updated at every iteration
    /// with the Aitken (Irons-Tuck) formula w = -w r(k-1)·(r(k) - r(k-1)) / |r(k) - r(k-1)|², r = next - current, limited to
    /// [<see cref="MinimumRelaxation"/>, 1] so that every trial geometry is a combination of two valid ones</param>
    /// <param name="warmStart">True: each situation after the first starts from the converged geometry of the previous one</param>
    public static IEnumerable<CumulativeSituation<TState, TContribution>> Analyze<TPhase, TState, TContribution>(
        IReadOnlyList<TPhase> phases, Func<TState> grossState,
        Func<TState, TPhase, TContribution> solve,
        Func<IReadOnlyList<TContribution>, TState> reduce,
        Func<TState, TState, double> distance,
        Func<TState, TState, double, TState> relax,
        Func<TPhase, string> phaseName,
        CancellationToken cancellation = default,
        int maximumIterations = 120, double tolerance = 1e-7, double relaxation = .55,
        Func<TState, double[]>? coordinates = null, bool warmStart = false)
    {
        if (maximumIterations < 1) throw new ArgumentOutOfRangeException(nameof(maximumIterations));
        BridgeNumbers.Require(tolerance, nameof(tolerance), strict: true);
        if (!BridgeNumbers.IsFinite(relaxation) || relaxation <= 0 || relaxation > 1) throw new ArgumentOutOfRangeException(nameof(relaxation));
        // Capture membership/order; adapters must not mutate phase or state objects during analysis.
        var snapshot = phases.ToArray();
        TState? previous = default; bool hasPrevious = false;
        for (int end = 0; end < snapshot.Length; end++)
        {
            cancellation.ThrowIfCancellationRequested();
            var state = warmStart && hasPrevious ? previous! : grossState();
            GPC.Checkers.Steel.EffectiveSections.EffectiveIterationResult<TState,TContribution[]> result;
            try {
                result=GPC.Checkers.Steel.EffectiveSections.EffectiveSectionIteration.Solve(state,
                    trial=>snapshot.Take(end+1).Select(p=>solve(trial,p)).ToArray(),
                    contributions=>reduce(contributions),distance,relax,maximumIterations,tolerance,relaxation,coordinates,cancellation);
            } catch(GPC.Checkers.Steel.EffectiveSections.EffectiveSectionConvergenceException ex) {
                throw new InvalidOperationException($"{phaseName(snapshot[end])}: sezione efficace non convergente dopo {ex.Iterations} iterazioni. Nessun esito utilizzabile.",ex);
            }
            previous=result.State;hasPrevious=true;
            yield return new(end,result.State,Array.AsReadOnly(result.Response),result.Iterations,result.Residual);
        }
    }
}
