namespace GPC.Checkers.CompositeBridge.History;

public static class HBridgeSectionResponse
{
    /// <summary>Uniform signed ramp from the starting state's control coordinate. Increment is in 1/mm or strain.</summary>
    public static SectionResponseResult Trace(HBridgeInput input, double increment, int numberOfPoints,
        SectionResponseOptions responseOptions, HBridgeHistoryOptions? historyOptions = null, int? startPhaseIndex = null,
        CancellationToken cancellation = default)
    {
        BridgeNumbers.Require(increment, nameof(increment), double.NegativeInfinity);
        if (numberOfPoints < 1) throw new ArgumentOutOfRangeException(nameof(numberOfPoints));
        var (section, initial) = Prepare(input, historyOptions, startPhaseIndex, cancellation);
        double start = responseOptions.Control == SectionResponseControl.MomentCurvature ? initial?.TotalPlane.Curvature ?? 0 : initial?.TotalPlane.At(responseOptions.ReferenceY) ?? 0;
        return BridgeSectionResponseAnalysis.Calculate(section, Enumerable.Range(1, numberOfPoints).Select(i => start + increment * i / numberOfPoints), responseOptions, initial, cancellation);
    }
    /// <summary>Null startPhaseIndex creates a virgin, fully composite section. Otherwise replays the active
    /// phases through that zero-based index with the requested material laws, preserving all internal strains.
    /// The input must explicitly select Class4=false. Targets are absolute, in the generic API's units.</summary>
    public static SectionResponseResult Calculate(HBridgeInput input, IEnumerable<double> targets,
        SectionResponseOptions responseOptions, HBridgeHistoryOptions? historyOptions = null, int? startPhaseIndex = null,
        CancellationToken cancellation = default)
    {
        var (section, initial) = Prepare(input, historyOptions, startPhaseIndex, cancellation);
        return BridgeSectionResponseAnalysis.Calculate(section, targets, responseOptions, initial, cancellation);
    }
    private static (HistorySection Section, HistoryStageResult? Initial) Prepare(HBridgeInput input,
        HBridgeHistoryOptions? historyOptions, int? startPhaseIndex, CancellationToken cancellation)
    {
        input = input.Snapshot();
        if (input.Options.Class4) throw new NotSupportedException("Le curve M–κ e N–ε richiedono Class4=false: analisi sulla sezione lorda.");
        var o = historyOptions ?? new() { MaterialMode = HistoryMaterialMode.Nonlinear };
        var section = HBridgeHistoryAnalysis.CreateSection(input, o);
        HistoryStageResult? initial = null;
        if (startPhaseIndex.HasValue)
        {
            var phases = input.Phases.Where(p => p.Active).ToArray();
            if (startPhaseIndex < 0 || startPhaseIndex >= phases.Length) throw new ArgumentOutOfRangeException(nameof(startPhaseIndex));
            initial = BridgeHistoryAnalysis.Calculate(section,
                HBridgeHistoryAnalysis.ConvertPhases(input, phases.Take(startPhaseIndex.Value + 1), o), o.Solver, cancellation).Stages.Last();
        }
        else section = section with { Components = Array.AsReadOnly(section.Components.Select(c => c with { InitiallyActive = true }).ToArray()) };
        return (section, initial);
    }
}
