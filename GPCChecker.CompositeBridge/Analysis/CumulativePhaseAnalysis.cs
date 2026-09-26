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
    public const double MinimumRelaxation = .05;

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
            bool converged = false;
            double factor = relaxation; double[]? lastResidual = null;
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
                    converged = true; previous = state; hasPrevious = true;
                    yield return new(end, state, Array.AsReadOnly(contributions), iteration, error);
                    break;
                }
                if (coordinates is not null)
                {
                    double[] a = coordinates(state), b = coordinates(next), residual = new double[a.Length];
                    for (int i = 0; i < a.Length; i++) residual[i] = b[i] - a[i];
                    if (lastResidual is not null)
                    {
                        double numerator = 0, denominator = 0;
                        for (int i = 0; i < residual.Length; i++)
                        {
                            double difference = residual[i] - lastResidual[i];
                            numerator += lastResidual[i] * difference; denominator += difference * difference;
                        }
                        if (denominator > 0 && BridgeNumbers.IsFinite(numerator / denominator))
                            factor = BridgeNumbers.Clamp(-factor * numerator / denominator, MinimumRelaxation, 1);
                    }
                    lastResidual = residual;
                }
                state = relax(state, next, factor);
            }
            if (!converged) throw new InvalidOperationException($"{phaseName(snapshot[end])}: sezione efficace non convergente dopo {maximumIterations} iterazioni. Nessun esito utilizzabile.");
        }
    }
}
