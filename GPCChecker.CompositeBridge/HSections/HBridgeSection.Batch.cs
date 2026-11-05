namespace GPC.Checkers.CompositeBridge;

public static partial class HBridgeSection
{
    /// <summary>
    /// Cumulative analyses with a bounded parallel budget. Distinct section groups run independently. When there are fewer groups than
    /// workers, each group solves a seed once, then forks its prefix/solution cache for independent Q branches. Worker dictionaries and
    /// lazy native sections are private; only immutable computed values are shared. Results retain input order and independent objects.
    /// Use degree 1 when an outer station loop already owns parallelism; cancellation stops the whole batch.
    /// </summary>
    public static IReadOnlyList<HBridgeBatchResult> CalculateBatch(IReadOnlyList<HBridgeInput> inputs,
        int maximumDegreeOfParallelism = 1, CancellationToken cancellation = default)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        if (maximumDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException(nameof(maximumDegreeOfParallelism));
        if (maximumDegreeOfParallelism == 1) return Calculate(inputs, cancellation);
        var results = new HBridgeBatchResult[inputs.Count];
        var keys = new BridgeBitwise();
        var groups = new Dictionary<string, (BridgeBatchGroup Group, List<(int Index, BridgePreparedInput Input)> Inputs)>(StringComparer.Ordinal);
        var centroids = new Dictionary<string, double>(StringComparer.Ordinal);
        for (int i = 0; i < inputs.Count; i++) {
            cancellation.ThrowIfCancellationRequested();
            try {
                string? dataKey = null;
                double Centroid(HBridgeInput data, BridgePhase phase) {
                    string key = (dataKey ??= keys.Key(data, nameof(HBridgeInput.Phases))) + "\n" + GrossCentroidKey(phase);
                    if (!centroids.TryGetValue(key, out double value)) centroids.Add(key, value = GrossPhaseCentroid(data, phase));
                    return value;
                }
                var prepared = Prepare(inputs[i], SolutionReuse ? Centroid : null);
                string key = SolutionReuse ? dataKey ?? keys.Key(prepared.Data, nameof(HBridgeInput.Phases)) : "#" + i;
                if (!groups.TryGetValue(key, out var group)) {
                    group = (new(prepared, keys), new()); groups.Add(key, group);
                }
                group.Inputs.Add((i, prepared));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { results[i] = new(null, error); }
        }
        var jobs = new List<Action>();
        int workersPerGroup = Math.Max(1, maximumDegreeOfParallelism / Math.Max(1, groups.Count));
        foreach (var item in groups.Values) {
            var group = item.Group; var list = item.Inputs;
            if (workersPerGroup == 1 || list.Count < 3 || !SolutionReuse) {
                // Independent key builders: the original BridgeBitwise contains a mutable StringBuilder.
                var worker = group.Fork();
                jobs.Add(() => { foreach (var input in list) results[input.Index] = worker.Calculate(input.Input, cancellation); });
                continue;
            }
            results[list[0].Index] = group.Calculate(list[0].Input, cancellation);
            int count = Math.Min(workersPerGroup, list.Count - 1);
            for (int partition = 0; partition < count; partition++) {
                int offset = partition; var worker = group.Fork();
                jobs.Add(() => {
                    for (int j = 1 + offset; j < list.Count; j += count)
                        results[list[j].Index] = worker.Calculate(list[j].Input, cancellation);
                });
            }
        }
        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = maximumDegreeOfParallelism, CancellationToken = cancellation }, job => job());
        return results;
    }
}
