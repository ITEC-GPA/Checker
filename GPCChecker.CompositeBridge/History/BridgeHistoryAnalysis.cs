using System.Collections.ObjectModel;

namespace GPC.Checkers.CompositeBridge.History;

/// <summary>Sequential fiber analysis with activation references, accumulated eigenstrains and committed material memory.
/// N–Mx, perfect bond, small strains. No phase-count limit and no automatic time/creep evolution.</summary>
public static class BridgeHistoryAnalysis
{
    public static HistoryAnalysisResult Calculate(HistorySection section, IEnumerable<HistoryPhase> phases,
        HistorySolverOptions? options = null, CancellationToken cancellation = default) =>
        new(Array.AsReadOnly(Enumerate(section, phases, options, cancellation).ToArray()));

    /// <summary>Streaming alternative: retains only the current material state and the last completed stage.</summary>
    public static IEnumerable<HistoryStageResult> Enumerate(HistorySection section, IEnumerable<HistoryPhase> phases,
        HistorySolverOptions? options = null, CancellationToken cancellation = default)
    {
        var o = options ?? new(); ValidateOptions(o);
        if (section is null || phases is null) throw new ArgumentNullException(section is null ? nameof(section) : nameof(phases));
        var components = section.Components.Select(c => c with { Fibers = Array.AsReadOnly(c.Fibers.ToArray()) }).ToArray();
        if (components.Length == 0 || components.Any(c => string.IsNullOrWhiteSpace(c.Id)) || components.Select(c => c.Id).Distinct().Count() != components.Length)
            throw new ArgumentException("Componenti assenti o identificativi duplicati.");
        var indices = components.Select((c, i) => (c.Id, i)).ToDictionary(p => p.Id, p => p.i);
        var points = components.SelectMany((c, i) => c.Fibers.Select(f => new Point(c.Id, i, f, c.Material))).ToArray();
        if (points.Length == 0 || points.Select(p => p.Fiber.Id).Distinct().Count() != points.Length) throw new ArgumentException("Fibre assenti o identificativi duplicati.");
        foreach (var p in points)
        {
            if (string.IsNullOrWhiteSpace(p.Fiber.Id) || p.Law is null) throw new ArgumentException("Fibra o materiale mancante.");
            Finite(p.Fiber.X, "x"); Finite(p.Fiber.Y, "y");
            BridgeNumbers.Require(p.Fiber.Area, "A", strict: true); BridgeNumbers.Require(p.Law.InitialModulus, "E", strict: true);
        }
        double area = points.Sum(p => p.Fiber.Area), origin = points.Sum(p => p.Fiber.Area * p.Fiber.Y) / area;
        double length = Math.Max(1, points.Max(p => p.Fiber.Y) - points.Min(p => p.Fiber.Y));
        var active = components.Select(c => c.InitiallyActive).ToArray(); var born = active.ToArray();
        var factors = Enumerable.Repeat(1d, components.Length).ToArray();
        var states = points.Select(p => p.Law.InitialState()).ToArray();
        var references = new double[points.Length]; var imposed = new double[points.Length];
        var weights = Enumerable.Repeat(1d, points.Length).ToArray();
        var plane = new HistoryStrainPlane();
        double n = 0, m = 0, v = 0; int index = 0;
        HistoryStageResult? last = null;
        foreach (var source in phases)
        {
            cancellation.ThrowIfCancellationRequested();
            if (source is null) throw new ArgumentException("Fase nulla.");
            // Snapshot collections so retries cannot change phase membership or imposed increments.
            var phase = source with { Activate = Array.AsReadOnly(source.Activate.ToArray()), Deactivate = Array.AsReadOnly(source.Deactivate.ToArray()),
                ModulusFactors = new ReadOnlyDictionary<string, double>(source.ModulusFactors.ToDictionary(p => p.Key, p => p.Value)),
                ImposedStrainIncrements = new ReadOnlyDictionary<string, HistoryStrainPlane>(source.ImposedStrainIncrements.ToDictionary(p => p.Key, p => p.Value)) };
            ValidatePhase(phase, indices);
            var startPlane = plane; var startStates = states.ToArray(); var startImposed = imposed.ToArray();
            double startN = n, startM = m;
            foreach (string id in phase.Deactivate)
            {
                int ci = indices[id]; if (!active[ci]) throw new ArgumentException("Componente già inattivo: " + id);
                active[ci] = false;
            }
            foreach (string id in phase.Activate)
            {
                int ci = indices[id]; if (active[ci]) throw new ArgumentException("Componente già attivo: " + id);
                active[ci] = true;
                if (!born[ci])
                {
                    for (int j = 0; j < points.Length; j++) if (points[j].ComponentIndex == ci) references[j] = plane.At(points[j].Fiber.Y);
                    born[ci] = true;
                }
                // Reactivation retains the original activation reference and material history.
            }
            foreach (var pair in phase.ModulusFactors)
            {
                int ci = indices[pair.Key];
                if (pair.Value != 1 && !components[ci].Material.SupportsModulusFactors)
                    throw new NotSupportedException("Il materiale di " + pair.Key + " non ammette fattori E incrementali.");
                factors[ci] = pair.Value;
            }
            foreach (var pair in phase.ImposedStrainIncrements)
                if (!active[indices[pair.Key]]) throw new ArgumentException("Deformazione imposta a componente inattivo: " + pair.Key);
            if (!active.Any(a => a)) throw new ArgumentException("Nessun componente attivo.");
            double fixedY = phase.LoadReference == HistoryLoadReference.GrossElasticCentroid ? Centroid(points, active, factors, null) : phase.ApplicationY;
            double loadY = fixedY, effectiveError = 0;
            int newtonCount = 0, effectiveCount = 0;
            IReadOnlyList<HistoryPanelResult> panels = Array.Empty<HistoryPanelResult>();
            Evaluation? final = null;
            for (int substep = 1; substep <= phase.Substeps; substep++)
            {
                cancellation.ThrowIfCancellationRequested();
                double fraction = (double)substep / phase.Substeps;
                for (int j = 0; j < points.Length; j++)
                {
                    var p = points[j];
                    imposed[j] = startImposed[j] + (phase.ImposedStrainIncrements.TryGetValue(p.Component, out var eps) ? eps.At(p.Fiber.Y) * fraction : 0);
                }
                bool accepted = false;
                for (int outer = 0; outer < o.MaximumEffectiveIterations; outer++)
                {
                    cancellation.ThrowIfCancellationRequested(); effectiveCount++;
                    loadY = phase.LoadReference == HistoryLoadReference.EffectiveElasticCentroid ? Centroid(points, active, factors, weights) : fixedY;
                    n = startN + phase.DeltaN * fraction;
                    m = startM + (phase.DeltaM - phase.DeltaN * loadY) * fraction;
                    try
                    {
                        final = Solve(points, states, active, references, imposed, factors, weights, plane,
                            n, m, origin, length, o, cancellation, out int iterations);
                        newtonCount += iterations;
                    }
                    catch (HistorySolveFailure failure)
                    { throw new HistoryConvergenceException(index, phase.Name, substep, failure.Message, last); }
                    plane = final.Plane;
                    if (section.EffectiveAreaModel is null) { accepted = true; break; }
                    var trial = FiberResults(points, active, weights, references, imposed, factors, final, states);
                    var reduction = section.EffectiveAreaModel.Evaluate(trial);
                    if (reduction.Factors.Count != points.Length) throw new ArgumentException("Numero di fattori efficaci diverso dal numero di fibre.");
                    effectiveError = 0;
                    for (int j = 0; j < weights.Length; j++)
                    {
                        double next = reduction.Factors[j];
                        if (!BridgeNumbers.IsFinite(next) || next < 0 || next > 1) throw new ArgumentException("Fattore di area efficace fuori [0,1].");
                        effectiveError = Math.Max(effectiveError, Math.Abs(next - weights[j]));
                    }
                    panels = Array.AsReadOnly(reduction.Panels.ToArray());
                    if (effectiveError <= o.EffectiveTolerance) { accepted = true; break; }
                    for (int j = 0; j < weights.Length; j++) weights[j] += o.EffectiveRelaxation * (reduction.Factors[j] - weights[j]);
                    // All outer trials use the SAME committed material states, including plastic variables.
                }
                if (!accepted) throw new HistoryConvergenceException(index, phase.Name, substep, "Sezione efficace non convergente", last);
                states = final!.Responses.Select(r => r.State).ToArray(); // commit only an equilibrated substep
            }
            v += phase.DeltaV;
            last = new(index++, phase.Name, plane, plane - startPlane, n, m, v, loadY, final!.N, final.M,
                final.N - n, final.M - m, newtonCount, effectiveCount, effectiveError,
                FiberResults(points, active, weights, references, imposed, factors, final, startStates), panels, phase);
            yield return last;
        }
    }

    private sealed record Point(string Component, int ComponentIndex, HistoryFiber Fiber, IHistoryMaterialLaw Law);
    private sealed record Evaluation(HistoryStrainPlane Plane, double N, double M, double K00, double K01, double K11, HistoryMaterialResponse[] Responses);
    private sealed class HistorySolveFailure : Exception { public HistorySolveFailure(string message) : base(message) { } }

    private static Evaluation Solve(Point[] points, HistoryMaterialState[] states, bool[] active, double[] references,
        double[] imposed, double[] factors, double[] weights, HistoryStrainPlane guess,
        double targetN, double targetM, double origin, double length, HistorySolverOptions o, CancellationToken cancellation, out int iterations)
    {
        double forceScale = o.ForceTolerance + o.RelativeTolerance * Math.Max(Math.Abs(targetN), Math.Abs(targetM) / length);
        double momentScale = o.MomentTolerance + o.RelativeTolerance * Math.Max(Math.Abs(targetM), Math.Abs(targetN) * length);
        double Norm(Evaluation e) => Math.Max(Math.Abs(e.N - targetN) / forceScale, Math.Abs(e.M - targetM) / momentScale);
        Evaluation Evaluate(HistoryStrainPlane plane)
        {
            double fn = 0, fm = 0, k00 = 0, k01 = 0, k11 = 0;
            var responses = new HistoryMaterialResponse[points.Length];
            for (int j = 0; j < points.Length; j++)
            {
                if ((j & 127) == 0) cancellation.ThrowIfCancellationRequested();
                var p = points[j];
                if (!active[p.ComponentIndex]) { responses[j] = new(states[j].Stress, 0, states[j]); continue; }
                double mechanical = plane.At(p.Fiber.Y) - references[j] - imposed[j];
                var r = responses[j] = p.Law.Evaluate(mechanical, states[j], factors[p.ComponentIndex]);
                if (!BridgeNumbers.IsFinite(r.Stress) || !BridgeNumbers.IsFinite(r.Tangent) || !BridgeNumbers.IsFinite(r.State.MechanicalStrain))
                    throw new HistorySolveFailure("Risposta costitutiva non finita");
                double a = p.Fiber.Area * weights[j], b = -(p.Fiber.Y - origin) / length;
                fn += r.Stress * a; fm -= r.Stress * a * p.Fiber.Y;
                k00 += r.Tangent * a; k01 += r.Tangent * a * b; k11 += r.Tangent * a * b * b;
            }
            return new(plane, fn, fm, k00, k01, k11, responses);
        }
        Evaluation current;
        try { current = Evaluate(guess); }
        catch (HistoryMaterialRangeException ex) { throw new HistorySolveFailure(ex.Message); }
        for (iterations = 0; iterations <= o.MaximumNewtonIterations; iterations++)
        {
            cancellation.ThrowIfCancellationRequested();
            double norm = Norm(current);
            if (norm <= 1) return current;
            if (iterations == o.MaximumNewtonIterations) break;
            double k00 = current.K00, k01 = current.K01, k11 = current.K11;
            // At a committed yield corner roundoff can classify adjacent, equally loaded
            // fibres on opposite tangent branches. An elastic predictor selects the loading
            // direction consistently; subsequent iterations use the constitutive tangent.
            if (iterations == 0)
            {
                k00 = k01 = k11 = 0;
                for (int j = 0; j < points.Length; j++)
                {
                    var p = points[j]; if (!active[p.ComponentIndex]) continue;
                    double a = p.Fiber.Area * weights[j] * p.Law.InitialModulus * factors[p.ComponentIndex];
                    double b = -(p.Fiber.Y - origin) / length;
                    k00 += a; k01 += a * b; k11 += a * b * b;
                }
            }
            double determinant = k00 * k11 - k01 * k01;
            if (!BridgeNumbers.IsFinite(determinant) || Math.Abs(determinant) <= 1e-14 * Math.Max(1, Math.Abs(k00 * k11)))
                throw new HistorySolveFailure("Rigidezza tangente singolare; carico oltre capacità o sezione priva di rigidezza N–Mx");
            double r0 = targetN - current.N, r1 = (targetM - current.M + r0 * origin) / length;
            double de = (k11 * r0 - k01 * r1) / determinant;
            double dk = (k00 * r1 - k01 * r0) / determinant / length;
            var change = new HistoryStrainPlane(de + dk * origin, dk);
            bool improved = false; double alpha = 1;
            for (int search = 0; search < o.MaximumLineSearchIterations; search++, alpha *= .5)
            {
                try
                {
                    var trial = Evaluate(current.Plane + change.Scale(alpha));
                    if (Norm(trial) < norm || Norm(trial) <= 1) { current = trial; improved = true; break; }
                }
                catch (HistoryMaterialRangeException) { /* Shorten the trial; do not commit a failed material state. */ }
            }
            if (!improved) throw new HistorySolveFailure($"Newton non riduce il residuo (N={current.N - targetN:E4} N, M={current.M - targetM:E4} Nmm, norma={norm:E4}, Δε={change.AxialStrain:E4}, Δκ={change.Curvature:E4}); verificare capacità, diagrammi e sottopassi");
        }
        throw new HistorySolveFailure("Numero massimo di iterazioni Newton raggiunto");
    }

    private static IReadOnlyList<HistoryFiberResult> FiberResults(Point[] points, bool[] active, double[] weights,
        double[] references, double[] imposed, double[] factors, Evaluation result, HistoryMaterialState[] previous)
    {
        var rows = new HistoryFiberResult[points.Length];
        for (int j = 0; j < rows.Length; j++)
        {
            var p = points[j]; bool a = active[p.ComponentIndex]; var state = result.Responses[j].State;
            // Dormant fibers retain their last connected strain/stress; they have zero participating area.
            double total = a ? result.Plane.At(p.Fiber.Y) : references[j] + imposed[j] + state.MechanicalStrain;
            rows[j] = new(p.Component, p.Fiber, a, a ? p.Fiber.Area * weights[j] : 0, references[j], total, imposed[j], state.MechanicalStrain,
                state.MechanicalStrain - previous[j].MechanicalStrain, state.Stress, state.Stress - previous[j].Stress, state, factors[p.ComponentIndex]);
        }
        return Array.AsReadOnly(rows);
    }

    private static double Centroid(Point[] points, bool[] active, double[] factors, double[]? weights)
    {
        double a = 0, s = 0;
        for (int j = 0; j < points.Length; j++)
        {
            var p = points[j]; if (!active[p.ComponentIndex]) continue;
            double ea = p.Fiber.Area * p.Law.InitialModulus * factors[p.ComponentIndex] * (weights is null ? 1 : weights[j]);
            a += ea; s += ea * p.Fiber.Y;
        }
        if (a <= 0 || !BridgeNumbers.IsFinite(a)) throw new ArgumentException("Sezione priva di rigidezza elastica.");
        return s / a;
    }
    private static void Finite(double value, string name) => BridgeNumbers.Require(value, name, double.NegativeInfinity);
    private static void ValidateOptions(HistorySolverOptions o)
    {
        if (o.MaximumNewtonIterations < 1 || o.MaximumEffectiveIterations < 1 || o.MaximumLineSearchIterations < 1) throw new ArgumentException("Limiti iterativi positivi richiesti.");
        BridgeNumbers.Require(o.RelativeTolerance, nameof(o.RelativeTolerance)); BridgeNumbers.Require(o.ForceTolerance, nameof(o.ForceTolerance), strict: true);
        BridgeNumbers.Require(o.MomentTolerance, nameof(o.MomentTolerance), strict: true); BridgeNumbers.Require(o.EffectiveTolerance, nameof(o.EffectiveTolerance), strict: true);
        BridgeNumbers.Require(o.EffectiveRelaxation, nameof(o.EffectiveRelaxation), strict: true);
        if (o.EffectiveRelaxation > 1) throw new ArgumentOutOfRangeException(nameof(o.EffectiveRelaxation));
    }
    private static void ValidatePhase(HistoryPhase p, Dictionary<string, int> ids)
    {
        Finite(p.DeltaN, "N"); Finite(p.DeltaM, "M"); Finite(p.DeltaV, "V"); Finite(p.ApplicationY, "yN");
        if (p.Substeps < 1 || !Enum.IsDefined(typeof(HistoryLoadReference), p.LoadReference)) throw new ArgumentException("Sottopassi o riferimento carichi non validi.");
        foreach (string id in p.Activate.Concat(p.Deactivate).Concat(p.ModulusFactors.Keys).Concat(p.ImposedStrainIncrements.Keys))
            if (!ids.ContainsKey(id)) throw new ArgumentException("Componente sconosciuto: " + id);
        if (p.Activate.Distinct().Count() != p.Activate.Count || p.Deactivate.Distinct().Count() != p.Deactivate.Count || p.Activate.Intersect(p.Deactivate).Any())
            throw new ArgumentException("Attivazioni/disattivazioni duplicate o incompatibili.");
        foreach (var factor in p.ModulusFactors.Values) BridgeNumbers.Require(factor, "fattore E", strict: true);
        foreach (var strain in p.ImposedStrainIncrements.Values) { Finite(strain.AxialStrain, "epsilon imposta"); Finite(strain.Curvature, "curvatura imposta"); }
    }
}
