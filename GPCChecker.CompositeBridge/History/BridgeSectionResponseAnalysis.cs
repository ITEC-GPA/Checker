namespace GPC.Checkers.CompositeBridge.History;

/// <summary>Separate deformation-controlled analysis, using the same material laws and immutable committed
/// states as BridgeHistoryAnalysis. Arbitrary fiber sections, fixed membership and gross areas during the curve.</summary>
public static class BridgeSectionResponseAnalysis
{
    public static SectionResponseResult Calculate(HistorySection section, IEnumerable<double> targets,
        SectionResponseOptions? options = null, HistoryStageResult? initialState = null, CancellationToken cancellation = default)
        => new Solver(section, options ?? new(), initialState, cancellation).Run(targets);

    private sealed record Point(string Component, HistoryFiber Fiber, IHistoryMaterialLaw Law, bool Active,
        double Activation, double Imposed, double Factor);
    private sealed record Trial(HistoryStrainPlane Plane, double N, double M, double K00, double K01, double K11,
        HistoryMaterialResponse[] Responses, int Iterations = 0);
    private sealed class EquilibriumFailure : Exception { internal EquilibriumFailure(string message) : base(message) { } }

    private sealed class Solver
    {
        private readonly SectionResponseOptions options;
        private readonly HistorySolverOptions settings;
        private readonly CancellationToken cancellation;
        private readonly Point[] points;
        private readonly List<SectionResponsePoint> output = new();
        private readonly double constant, initialV, elasticEA, elasticY;
        private HistoryMaterialState[] committed;
        private HistoryStrainPlane plane;
        private Trial current;
        private SectionResponseStop stop = SectionResponseStop.Completed;
        private string message = "Percorso completato. Non costituisce una verifica di resistenza o instabilità.";
        private int? failedIndex;
        private SectionResponsePoint? lastAccepted;

        internal Solver(HistorySection section, SectionResponseOptions o, HistoryStageResult? initial, CancellationToken token)
        {
            options = o; settings = o.Solver ?? throw new ArgumentException("Opzioni del solutore mancanti."); cancellation = token;
            cancellation.ThrowIfCancellationRequested();
            if (section is null || section.Components is null) throw new ArgumentNullException(nameof(section));
            if (section.EffectiveAreaModel is not null) throw new NotSupportedException("Le curve a deformazione imposta richiedono aree lorde fisse: non applicano la riduzione di classe 4.");
            if (!Enum.IsDefined(typeof(SectionResponseControl), o.Control) || o.SubstepsPerTarget < 1 || o.MaximumSubdivisions < 0 || o.MaximumSubdivisions > 16)
                throw new ArgumentException("Controllo, sottopassi o suddivisioni non validi.");
            Finite(o.ReferenceY, "y riferimento"); if (o.AxialForce.HasValue) Finite(o.AxialForce.Value, "N");
            if (o.FixedCurvature.HasValue) Finite(o.FixedCurvature.Value, "curvatura");
            if (o.Control == SectionResponseControl.MomentCurvature && o.FixedCurvature.HasValue ||
                o.Control == SectionResponseControl.AxialForceStrain && o.AxialForce.HasValue)
                throw new ArgumentException("Vincolo incompatibile con il tipo di curva.");
            if (settings.MaximumNewtonIterations < 1 || settings.MaximumLineSearchIterations < 1) throw new ArgumentException("Limiti iterativi non validi.");
            BridgeNumbers.Require(settings.ForceTolerance, "Tolleranza N", strict: true); BridgeNumbers.Require(settings.RelativeTolerance, "Tolleranza relativa");
            var components = section.Components.ToArray();
            if (components.Length == 0 || components.Any(c => c is null || string.IsNullOrWhiteSpace(c.Id) || c.Material is null || c.Fibers is null) ||
                components.Select(c => c.Id).Distinct().Count() != components.Length) throw new ArgumentException("Componenti mancanti o duplicati.");
            var source = components.SelectMany(c => c.Fibers.Select(f => (c, f))).ToArray();
            if (source.Length == 0 || source.Any(p => p.f is null || string.IsNullOrWhiteSpace(p.f.Id)) || source.Select(p => p.f.Id).Distinct().Count() != source.Length)
                throw new ArgumentException("Fibre mancanti o duplicate.");
            var old = initial?.Fibers.ToDictionary(f => f.Fiber.Id);
            if (old is not null && old.Count != source.Length) throw new ArgumentException("Lo stato iniziale appartiene a una discretizzazione diversa.");
            var rows = new List<Point>(); var states = new List<HistoryMaterialState>();
            foreach (var (c, f) in source)
            {
                Finite(f.X, "x"); Finite(f.Y, "y"); BridgeNumbers.Require(f.Area, "A", strict: true); BridgeNumbers.Require(c.Material.InitialModulus, "E", strict: true);
                HistoryFiberResult? previous = null;
                if (old is not null && (!old.TryGetValue(f.Id, out previous) || previous.ComponentId != c.Id || previous.Fiber != f))
                    throw new ArgumentException("Identità o geometria delle fibre diversa dallo stato iniziale.");
                bool active = previous?.Active ?? c.InitiallyActive;
                if (previous is not null && active && Math.Abs(previous.EffectiveArea - f.Area) > 1e-8 * f.Area)
                    throw new NotSupportedException("Lo stato iniziale ha aree ridotte: ricalcolare lo storico sulla sezione lorda prima della curva.");
                var state = previous?.MaterialState ?? c.Material.InitialState();
                double factor = previous?.ModulusFactor ?? 1;
                BridgeNumbers.Require(factor, "fattore E", strict: true);
                if (factor != 1 && !c.Material.SupportsModulusFactors) throw new ArgumentException("Legge costitutiva incompatibile con il fattore E dello stato iniziale.");
                if (previous is not null)
                {
                    var response = c.Material.Evaluate(state.MechanicalStrain, state, factor);
                    if (Math.Abs(response.Stress - state.Stress) > 1e-7 * Math.Max(1, Math.Abs(state.Stress)))
                        throw new ArgumentException("Legge costitutiva incompatibile con lo stato iniziale.");
                    if (active && Math.Abs(initial!.TotalPlane.At(f.Y) - previous.ActivationStrain - previous.ImposedStrain - state.MechanicalStrain) > 1e-10)
                        throw new ArgumentException("Piano e deformazioni dello stato iniziale non coerenti.");
                }
                rows.Add(new(c.Id, f, c.Material, active, previous?.ActivationStrain ?? 0, previous?.ImposedStrain ?? 0, factor)); states.Add(state);
            }
            points = rows.ToArray(); committed = states.ToArray();
            if (!points.Any(p => p.Active)) throw new ArgumentException("Nessuna fibra attiva.");
            elasticEA = points.Where(p => p.Active).Sum(p => p.Fiber.Area * p.Law.InitialModulus * p.Factor);
            elasticY = points.Where(p => p.Active).Sum(p => p.Fiber.Area * p.Law.InitialModulus * p.Factor * p.Fiber.Y) / elasticEA;
            plane = initial?.TotalPlane ?? new(); initialV = initial?.V ?? 0;
            constant = o.Control == SectionResponseControl.MomentCurvature ? o.AxialForce ?? initial?.N ?? 0 : o.FixedCurvature ?? plane.Curvature;
            current = Evaluate(plane);
            // Inspection of the initial point must not advance constitutive memory.
            current = current with { Responses = current.Responses.Select((r, i) => r with { State = committed[i], Stress = committed[i].Stress }).ToArray() };
        }

        internal SectionResponseResult Run(IEnumerable<double> targets)
        {
            if (targets is null) throw new ArgumentNullException(nameof(targets));
            double startControl = options.Control == SectionResponseControl.MomentCurvature ? plane.Curvature : plane.At(options.ReferenceY);
            // Precondition to the requested constant constraint while holding the starting control coordinate.
            // This loading is part of the memory, not a silent change of the starting stress state.
            double startConstant = options.Control == SectionResponseControl.MomentCurvature ? current.N : plane.Curvature;
            for (int s = 1; s <= options.SubstepsPerTarget; s++)
            {
                double c = startConstant + (constant - startConstant) * s / options.SubstepsPerTarget;
                if (!Advance(startControl, c, -1, s == options.SubstepsPerTarget, 0)) return Result();
            }
            // Keep only the fully preconditioned start in the public curve; failed preconditioning remains explicit.
            var start = output.Last(); output.Clear(); output.Add(start with { Index = 0, TargetIndex = -1, ReachedTarget = true });
            int index = 0;
            foreach (double target in targets)
            {
                cancellation.ThrowIfCancellationRequested(); Finite(target, "Deformazione imposta");
                double from = Coordinate(plane);
                for (int s = 1; s <= options.SubstepsPerTarget; s++)
                    if (!Advance(from + (target - from) * s / options.SubstepsPerTarget, constant, index, s == options.SubstepsPerTarget, 0)) return Result();
                index++;
            }
            if (index == 0) throw new ArgumentException("Specificare almeno un punto del percorso.");
            return Result();
        }
        private SectionResponseResult Result()
        {
            if (stop != SectionResponseStop.Completed && lastAccepted is not null && (output.Count == 0 || output.Last() != lastAccepted)) output.Add(lastAccepted);
            return new(options, constant, Array.AsReadOnly(output.ToArray()), stop, failedIndex, message);
        }
        private double Coordinate(HistoryStrainPlane p) => options.Control == SectionResponseControl.MomentCurvature ? p.Curvature : p.At(options.ReferenceY);

        private bool Advance(double target, double constraint, int targetIndex, bool endpoint, int depth)
        {
            cancellation.ThrowIfCancellationRequested();
            Trial trial;
            try
            {
                trial = options.Control == SectionResponseControl.MomentCurvature ? SolveAxial(target, constraint) :
                    Evaluate(new(target + constraint * options.ReferenceY, constraint));
            }
            catch (Exception ex) when (ex is HistoryMaterialRangeException || ex is EquilibriumFailure)
            {
                double a = Coordinate(plane), b = options.Control == SectionResponseControl.MomentCurvature ? current.N : plane.Curvature;
                if (depth < options.MaximumSubdivisions && (target != a || constraint != b))
                {
                    if (!Advance((a + target) / 2, (b + constraint) / 2, targetIndex, false, depth + 1)) return false;
                    return Advance(target, constraint, targetIndex, endpoint, depth + 1);
                }
                stop = ex is HistoryMaterialRangeException ? SectionResponseStop.MaterialDomain : SectionResponseStop.EquilibriumNotFound;
                failedIndex = targetIndex; message = (targetIndex < 0 ? "Precarico non completato. " : "Curva interrotta al punto richiesto " + (targetIndex + 1) + ". ") + ex.Message + " Sono riportati solo gli stati accettati; l'arresto non identifica automaticamente la capacità ultima.";
                return false;
            }
            var old = committed; var oldPlane = plane;
            committed = trial.Responses.Select(r => r.State).ToArray(); plane = trial.Plane; current = trial;
            var fibers = points.Select((p, j) => {
                var state = committed[j]; double total = p.Active ? plane.At(p.Fiber.Y) : p.Activation + p.Imposed + state.MechanicalStrain;
                return new HistoryFiberResult(p.Component, p.Fiber, p.Active, p.Active ? p.Fiber.Area : 0, p.Activation,
                    total, p.Imposed, state.MechanicalStrain, state.MechanicalStrain - old[j].MechanicalStrain,
                    state.Stress, state.Stress - old[j].Stress, state, p.Factor);
            }).ToArray();
            double n = options.Control == SectionResponseControl.MomentCurvature ? constraint : trial.N;
            var stateResult = new HistoryStageResult(output.Count, targetIndex < 0 ? "Stato iniziale" : "Punto " + (targetIndex + 1), plane, plane - oldPlane,
                n, trial.M, initialV, options.ReferenceY, trial.N, trial.M, trial.N - n, 0, trial.Iterations, 0, 0,
                Array.AsReadOnly(fibers), Array.Empty<HistoryPanelResult>(), new() { Name = "Controllo di deformazione" });
            double? bending = Math.Abs(trial.K00) > elasticEA * 1e-14 ? trial.K11 - trial.K01 * trial.K01 / trial.K00 :
                Math.Abs(trial.K01) < elasticEA * 1e-14 ? trial.K11 : null;
            lastAccepted = new(output.Count, targetIndex, endpoint, Coordinate(plane), plane.At(options.ReferenceY), trial.M + trial.N * options.ReferenceY,
                trial.K00, bending, stateResult);
            if (endpoint || options.IncludeSubsteps) output.Add(lastAccepted);
            return true;
        }

        private Trial Evaluate(HistoryStrainPlane trialPlane)
        {
            double n = 0, m = 0, k00 = 0, k01 = 0, k11 = 0;
            var responses = new HistoryMaterialResponse[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                if ((i & 127) == 0) cancellation.ThrowIfCancellationRequested();
                var p = points[i];
                if (!p.Active) { responses[i] = new(committed[i].Stress, 0, committed[i]); continue; }
                double mechanical = trialPlane.At(p.Fiber.Y) - p.Activation - p.Imposed;
                var r = responses[i] = p.Law.Evaluate(mechanical, committed[i], p.Factor);
                if (!BridgeNumbers.IsFinite(r.Stress) || !BridgeNumbers.IsFinite(r.Tangent) || !BridgeNumbers.IsFinite(r.State.MechanicalStrain))
                    throw new EquilibriumFailure("Risposta costitutiva non finita.");
                double a = p.Fiber.Area, y = p.Fiber.Y - options.ReferenceY;
                n += r.Stress * a; m -= r.Stress * a * p.Fiber.Y;
                k00 += r.Tangent * a; k01 -= r.Tangent * a * y; k11 += r.Tangent * a * y * y;
            }
            return new(trialPlane, n, m, k00, k01, k11, responses);
        }

        private Trial SolveAxial(double curvature, double n)
        {
            // Hold strain at the elastic centroid for the first guess; solve only the axial strain.
            var trial = Evaluate(new(plane.AxialStrain + (curvature - plane.Curvature) * elasticY, curvature));
            double tolerance = settings.ForceTolerance + settings.RelativeTolerance * Math.Abs(n);
            for (int iteration = 0; iteration <= settings.MaximumNewtonIterations; iteration++)
            {
                cancellation.ThrowIfCancellationRequested(); double residual = n - trial.N;
                if (Math.Abs(residual) <= tolerance) return trial with { Iterations = iteration };
                if (iteration == settings.MaximumNewtonIterations) break;
                double tangent = iteration == 0 ? elasticEA : trial.K00;
                if (Math.Abs(tangent) < elasticEA * 1e-14) throw new EquilibriumFailure("Rigidezza assiale tangente nulla senza equilibrio di N.");
                double increment = residual / tangent; bool improved = false; double alpha = 1;
                for (int search = 0; search < settings.MaximumLineSearchIterations; search++, alpha *= .5)
                {
                    try
                    {
                        var next = Evaluate(trial.Plane with { AxialStrain = trial.Plane.AxialStrain + alpha * increment });
                        if (Math.Abs(next.N - n) < Math.Abs(residual) || Math.Abs(next.N - n) <= tolerance)
                        { trial = next; improved = true; break; }
                    }
                    catch (HistoryMaterialRangeException) { /* A failed trial never commits material memory. */ }
                }
                if (!improved) throw new EquilibriumFailure("Il residuo assiale non diminuisce; limite costitutivo o ramo di equilibrio non raggiungibile.");
            }
            throw new EquilibriumFailure("Numero massimo di iterazioni per l'equilibrio assiale raggiunto.");
        }
        private static void Finite(double value, string name) => BridgeNumbers.Require(value, name, double.NegativeInfinity);
    }
}
