namespace GPC.Checkers.CompositeBridge.History;

public static partial class HBridgeHistoryResults
{
    /// <summary>
    /// Linear construction histories with shared initial phases, e.g. G1 + G2 + shrinkage followed by independent Q branches.
    /// Inputs with identical data except phases share their prepared section and longest common prefix. Each branch owns its mutable
    /// continuation state. The result order is the input order; an invalid input keeps its own error, cancellation stops the whole batch.
    /// One parallel budget covers both station groups and Q branches. Materials/custom laws must not be mutated during the call.
    /// </summary>
    public static IReadOnlyList<HBridgeBatchResult> CalculateBatch(IReadOnlyList<HBridgeInput> inputs,
        HBridgeHistoryOptions? options = null, int maximumDegreeOfParallelism = 1, CancellationToken cancellation = default)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        if (maximumDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException(nameof(maximumDegreeOfParallelism));
        var o = options ?? new();
        if (o.MaterialMode != HistoryMaterialMode.Linear) throw new NotSupportedException("Il batch con prefissi condivisi e' disponibile per lo storico lineare.");
        var results = new HBridgeBatchResult[inputs.Count];
        var keys = new BridgeBitwise(); var groups = new Dictionary<string, LinearHistoryGroup>(StringComparer.Ordinal);
        for (int i = 0; i < inputs.Count; i++) {
            cancellation.ThrowIfCancellationRequested();
            try {
                var data = PrepareInput(inputs[i]);
                // Presentation warns on torsion anywhere in the complete sequence, including a later Q.
                string key = keys.Key(data, nameof(HBridgeInput.Phases)) + "|torsion:" + data.Phases.Any(p => p.Active && p.TorsionKNm != 0);
                if (!groups.TryGetValue(key, out var group)) groups.Add(key, group = new());
                var active = data.Phases.Where(p => p.Active).ToArray();
                if (active.Length == 0) throw new ArgumentException("Attivare almeno una fase.");
                group.Inputs.Add((i, data, active, active.Select(p => keys.Key(p)).ToArray()));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { results[i] = new(null, error); }
        }
        // Empty groups can result from invalid phase arrays; they never reach the solver.
        var work = groups.Values.Where(g => g.Inputs.Count != 0).ToArray();
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = maximumDegreeOfParallelism, CancellationToken = cancellation };
        void Prepare(LinearHistoryGroup group) {
            try { group.Prepare(o, cancellation); }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { group.Error = error; }
        }
        if (maximumDegreeOfParallelism == 1) foreach (var group in work) Prepare(group);
        else Parallel.ForEach(work, parallel, Prepare);
        var branches = work.SelectMany(g => g.Inputs.Select(i => (Group: g, Input: i))).ToArray();
        void Calculate(int branch) {
            var (group, input) = branches[branch];
            cancellation.ThrowIfCancellationRequested();
            if (group.Error is not null) { results[input.Index] = new(null, group.Error); return; }
            try {
                // Convert the FULL sequence before skipping: activations at casting must be those of the original sequence.
                var converted = HBridgeHistoryAnalysis.ConvertPhases(input.Data, input.Active, o);
                var tail = BridgeHistoryAnalysis.Continue(group.State!, converted.Skip(group.PrefixCount), o.Solver, cancellation);
                var history = new HistoryAnalysisResult(Array.AsReadOnly(group.Prefix!.Concat(tail).ToArray()));
                results[input.Index] = new(Present(input.Data, o, group.Section!, history, cancellation, group.Geometry, group.Presentation), null);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { results[input.Index] = new(null, error); }
        }
        if (maximumDegreeOfParallelism == 1) for (int i = 0; i < branches.Length; i++) Calculate(i);
        else Parallel.For(0, branches.Length, parallel, Calculate);
        return results;
    }

    private sealed class LinearHistoryGroup
    {
        internal readonly List<(int Index, HBridgeInput Data, BridgePhase[] Active, string[] Keys)> Inputs = new();
        internal HistorySection? Section;
        internal BridgeGeometry? Geometry;
        internal BridgeHistoryAnalysis.Checkpoint? State;
        internal IReadOnlyList<HistoryStageResult>? Prefix;
        internal HBridgeAnalysisResult? Presentation;
        internal int PrefixCount;
        internal Exception? Error;

        internal void Prepare(HBridgeHistoryOptions options, CancellationToken cancellation)
        {
            var first = Inputs[0];
            PrefixCount = Inputs.Min(i => i.Active.Length);
            for (int j = 0; j < PrefixCount; j++) {
                if (Inputs.Any(i => i.Keys[j] != first.Keys[j])) { PrefixCount = j; break; }
            }
            Section = HBridgeHistoryAnalysis.CreateSection(first.Data, options);
            if (Section.Components.Any(c => !c.Material.IsLinear)) throw new NotSupportedException("Il batch con prefissi condivisi richiede legami lineari.");
            Geometry = HBridgeSection.Geometry(first.Data with { Phases = new[] { new BridgePhase() } });
            var prepared = BridgeHistoryAnalysis.PreparePrefix(Section,
                HBridgeHistoryAnalysis.ConvertPhases(first.Data, first.Active.Take(PrefixCount), options), options.Solver, cancellation);
            Prefix = prepared.Stages; State = prepared.State;
            if (PrefixCount > 0) Presentation = Present(first.Data, options, Section,
                new HistoryAnalysisResult(Prefix), cancellation, Geometry);
        }
    }
}
