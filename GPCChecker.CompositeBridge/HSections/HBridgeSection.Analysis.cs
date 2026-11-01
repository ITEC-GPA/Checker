using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.CompositeBridge;

// H-specific geometry and result assembly; reusable cumulative iteration and native solver live in Analysis/.
public static partial class HBridgeSection
{
    /// <summary>False: every solution of <see cref="Calculate(HBridgeInput, CancellationToken)"/> is computed again and the inputs of a batch are
    /// not shared, as before the reuse (tests compare the two paths)</summary>
    internal static bool SolutionReuse { get; set; } = true;

    public static HBridgeAnalysisResult Calculate(HBridgeInput data, CancellationToken cancellation = default)
    {
        var result = Calculate(new[] { data }, cancellation)[0];
        if (result.Error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Error).Throw();
        return result.Analysis!;
    }

    /// <summary>
    /// The analyses of several inputs, for example the load combinations of a station of a girder. The inputs with the same data apart from
    /// the phases (equal bit for bit, the Model materials the same objects) share their computations: the situations of the same leading
    /// phases are iterated once and each (effective state, phase, solver) is solved once. Every input gets the result of its own
    /// <see cref="Calculate(HBridgeInput, CancellationToken)"/>, with the same numbers and its own objects, or the exception it would throw;
    /// a cancellation stops the whole batch.
    /// </summary>
    /// <param name="inputs">The inputs</param>
    /// <param name="cancellation">The cancellation</param>
    /// <returns>For each input, in order, its analysis or its error</returns>
    public static IReadOnlyList<HBridgeBatchResult> Calculate(IReadOnlyList<HBridgeInput> inputs, CancellationToken cancellation = default)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        return Analyze(inputs, null, cancellation);
    }

    /// <summary>
    /// The analysis of an input that differs from the input of an earlier analysis only in data that the phase analysis does not read: studs,
    /// stiffeners, transverse reinforcement, fatigue, limit state, and the common load elevation when no phase refers to it (for example the
    /// details and the ultimate limit state added to the result of a check). The converged situations of the earlier analysis are kept and only
    /// the stress points, shear, studs, torsion and details are computed again: the result is the one of
    /// <see cref="Calculate(HBridgeInput, CancellationToken)"/>, with the same numbers. Any other input, or an earlier analysis not computed by
    /// this library in this process, is calculated in full. The Model materials are compared as objects, as in a batch: they must not change
    /// in between.
    /// </summary>
    /// <param name="earlier">The earlier analysis of the section (null: none)</param>
    /// <param name="data">The input</param>
    /// <param name="cancellation">The cancellation</param>
    /// <returns>The analysis</returns>
    public static HBridgeAnalysisResult Recalculate(HBridgeAnalysisResult? earlier, HBridgeInput data, CancellationToken cancellation = default)
    {
        var result = Recalculate(new[] { earlier }, new[] { data }, cancellation)[0];
        if (result.Error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Error).Throw();
        return result.Analysis!;
    }

    /// <summary>
    /// <see cref="Recalculate(HBridgeAnalysisResult?, HBridgeInput, CancellationToken)"/> of several inputs, shared as in
    /// <see cref="Calculate(IReadOnlyList{HBridgeInput}, CancellationToken)"/>
    /// </summary>
    /// <param name="earlier">For each input, its earlier analysis (null: none)</param>
    /// <param name="inputs">The inputs</param>
    /// <param name="cancellation">The cancellation</param>
    /// <returns>For each input, in order, its analysis or its error</returns>
    public static IReadOnlyList<HBridgeBatchResult> Recalculate(IReadOnlyList<HBridgeAnalysisResult?> earlier, IReadOnlyList<HBridgeInput> inputs,
        CancellationToken cancellation = default)
    {
        if (earlier is null) throw new ArgumentNullException(nameof(earlier));
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        if (earlier.Count != inputs.Count) throw new ArgumentException("Un'analisi precedente (anche nulla) per ogni ingresso.", nameof(earlier));
        return Analyze(inputs, earlier, cancellation);
    }

    private static IReadOnlyList<HBridgeBatchResult> Analyze(IReadOnlyList<HBridgeInput> inputs, IReadOnlyList<HBridgeAnalysisResult?>? earlier,
        CancellationToken cancellation)
    {
        var results = new HBridgeBatchResult[inputs.Count];
        var keys = new BridgeBitwise();
        var groups = new Dictionary<string, BridgeBatchGroup>(StringComparer.Ordinal);
        for (int i = 0; i < inputs.Count; i++)
        {
            BridgePreparedInput prepared;
            try { prepared = Prepare(inputs[i]); }
            catch (Exception e) { results[i] = new(null, e); continue; }
            // without the reuse every input is computed on its own, as before
            string key = SolutionReuse ? keys.Key(prepared.Data, nameof(HBridgeInput.Phases)) : "#" + i;
            if (!groups.TryGetValue(key, out var group)) groups.Add(key, group = new(prepared, keys));
            if (SolutionReuse && earlier?[i] is { } analysis && Computed.TryGetValue(analysis, out var situations) && situations.Accepts(prepared, keys))
            {
                Interlocked.Increment(ref recalculations);
                results[i] = group.Recalculate(prepared, situations, cancellation);
            }
            else results[i] = group.Calculate(prepared, cancellation);
        }
        return results;
    }

    private static long recalculations;
    /// <summary>The inputs calculated from the situations of an earlier analysis since the start of the process (tests)</summary>
    internal static long Recalculations => Interlocked.Read(ref recalculations);

    /// <summary>What the converged situation of a stage gives to the stage: the stage is computed again from it by
    /// <see cref="Recalculate(HBridgeAnalysisResult?, HBridgeInput, CancellationToken)"/></summary>
    private sealed record BridgeSituationStage(int PhaseIndex, int Iterations, double Residual, BridgeEffective Effective, BridgeSteelProperties EffectiveSteel,
        BridgeContribution[] Contributions);

    /// <summary>The situations of an analysis computed here, with its input (its own copy of the phases)</summary>
    private sealed class BridgeComputedSituations
    {
        internal BridgeComputedSituations(HBridgeInput input, BridgeSituationStage[] stages) { Input = input with { Phases = input.Phases.ToArray() }; Stages = stages; }
        internal HBridgeInput Input { get; }
        internal BridgeSituationStage[] Stages { get; }

        /// <summary>True: the input differs from the one of these situations only in data that the phase analysis does not read (those of the
        /// stress points, shear, studs, torsion and details)</summary>
        internal bool Accepts(BridgePreparedInput input, BridgeBitwise keys)
        {
            var d = input.Data; var e = Input;
            if (Stages.Length != input.Phases.Length) return false;
            // the common elevation is the application point of the phases that refer to it
            bool common = d.Phases.Any(p => p.Reference == BridgeLoadReference.CommonElevation);
            var same = d with { Studs = e.Studs, Intermediate = e.Intermediate, Support = e.Support, Transverse = e.Transverse, Fatigue = e.Fatigue,
                Options = d.Options with { LimitState = e.Options.LimitState, CommonLoadY = common ? d.Options.CommonLoadY : e.Options.CommonLoadY } };
            return keys.Key(same) == keys.Key(e);
        }
    }

    /// <summary>The situations of the analyses computed here, for as long as the analyses live</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HBridgeAnalysisResult, BridgeComputedSituations> Computed = new();

    /// <summary>The analysis of an input with its stages, kept with their situations</summary>
    private static HBridgeBatchResult Result(HBridgeInput data, BridgeGeometry g, BridgeMaterialSet mat, List<BridgeStage> stages, BridgeSituationStage[] situations)
    {
        var analysis = new HBridgeAnalysisResult(Method, Scope, data, g,
            new(mat.Concrete.Name, Math.Abs(mat.Concrete.Fck), mat.Concrete.ElasticModulusCompression, mat.Steel.Name, mat.Steel.Fyk, mat.Steel.ElasticModulusTension,
                mat.Rebar.Name, mat.Rebar.Fyk, mat.Rebar.ElasticModulusTension), stages);
        Computed.Add(analysis, new(data, situations));
        return new(analysis, null);
    }

    /// <summary>An input checked as by <see cref="Calculate(HBridgeInput, CancellationToken)"/>: its copy, geometry, materials, active phases
    /// and the application points of their axial forces</summary>
    private sealed record BridgePreparedInput(HBridgeInput Data, BridgeGeometry Geometry, BridgeMaterialSet Materials, BridgePhase[] Phases,
        Dictionary<BridgePhase, double?> ApplicationPoints);

    /// <summary>The checks of an input, in the order of <see cref="Calculate(HBridgeInput, CancellationToken)"/></summary>
    private static BridgePreparedInput Prepare(HBridgeInput data)
    {
        data = data.Snapshot(); var g = Geometry(data); var mat = Materials(data);
        if (!Standards.Contains(data.Options.StandardName)) throw new ArgumentException("Normativa non supportata.");
        if (data.Options.LimitStateName is not ("SLU" or "SLE rara" or "SLE quasi permanente")) throw new ArgumentException("Stato limite non supportato.");
        BridgeNumbers.Require(data.Options.GammaM0, "gamma_m0", strict: true); BridgeNumbers.Require(data.Options.GammaC, "gamma_c", strict: true);
        BridgeNumbers.Require(data.Options.GammaS, "gamma_s", strict: true); BridgeNumbers.Require(data.Options.AlphaCC, "alpha_cc", strict: true);
        var phases = data.Phases.Where(p => p.Active).ToArray();
        if (phases.Length == 0) throw new ArgumentException("Attivare almeno una fase.");
        bool compositeSeen = false;
        foreach (var p in phases)
        {
            if (!PhaseKinds.Contains(p.KindName)) throw new ArgumentException("Tipo di fase sconosciuto.");
            // the same rule and message of the histories: every phase other than «Solo acciaio» (composite, cracked slab, shrinkage) follows the
            // casting of the slab. Before, a «Ritiro» did not count, and a later steel-only phase was calculated on the bare steel
            if (p.KindName == "Solo acciaio" && compositeSeen) throw new ArgumentException("Le fasi di solo acciaio devono precedere il getto.");
            compositeSeen |= p.KindName != "Solo acciaio";
            if (p.KindName == ShrinkageKind) Number(p, "epsilon_cs");
            else { Number(p, "N"); Number(p, "Mx"); Number(p, "V"); }
            if (!LoadReferences.Contains(LoadReference(p))) throw new ArgumentException("Riferimento di N sconosciuto.");
        }
        ValidateTorsion(data, g, phases);
        // null means follow the effective centroid inside Solve; fixed points are resolved once.
        var applicationPoints = phases.ToDictionary(p => p, p => p.KindName == ShrinkageKind ? null : LoadReference(p) switch
        {
            CommonLoadReference => (double?)BridgeNumbers.Require(data.Options.CommonLoadY, "y_ref", double.NegativeInfinity),
            GrossLoadReference => GrossPhaseCentroid(data, p),
            _ => null
        }, new BridgePhaseIdentity());
        return new(data, g, mat, phases, applicationPoints);
    }

    /// <summary>
    /// The inputs of a batch with the same data apart from the phases. Their phases equal bit for bit are replaced by one object, so that the
    /// situations of the same leading phases are one node of a tree and are iterated once, and the solutions are shared by reference.
    /// </summary>
    private sealed class BridgeBatchGroup
    {
        private readonly HBridgeInput data; private readonly BridgeGeometry g; private readonly BridgeMaterialSet mat;
        private readonly BridgeBitwise keys;
        private readonly Dictionary<string, BridgePhase> canonical = new(StringComparer.Ordinal);
        private readonly Dictionary<BridgePhase, double?> applicationPoints = new(new BridgePhaseIdentity());
        private readonly BridgeSituationNode root = new(Array.Empty<BridgePhase>());
        // Solve depends only on its arguments, and the analysis passes the same objects again: with the warm start each situation evaluates
        // first the converged state of the previous one, on which the previous phases were already solved, and when it converges there the
        // final solutions of those phases are the previous ones too. Each (state, phase, solver) is solved once; the callers get copies, as
        // separate solutions were, with the same numbers (the same calculation on the same objects).
        private readonly Dictionary<(BridgeEffective, BridgePhase, bool), BridgeContribution> solutions = new(new BridgeSolutionIdentity());
        // The steel pieces of a state and the native section on them are functions of the input and of the state only, and the solutions only
        // read them: built once per state for all its phases (before, once per phase).
        private readonly Dictionary<BridgeEffective, BridgeStateSection> sections = new(new BridgeStateIdentity());
        // The gross properties of a phase do not depend on the situation: computed once per phase (before, once per situation including it).
        private readonly Dictionary<BridgePhase, BridgeSectionProperties> grossProperties = new(new BridgePhaseIdentity());

        internal BridgeBatchGroup(BridgePreparedInput first, BridgeBitwise keys)
        {
            data = first.Data; g = first.Geometry; mat = first.Materials; this.keys = keys;
        }

        private BridgeContribution Solution(BridgeEffective effective, BridgePhase phase, bool nativeSolver)
        {
            if (!SolutionReuse) return Solve(data, g, effective, phase, applicationPoints[phase], nativeSolver);
            if (!solutions.TryGetValue((effective, phase, nativeSolver), out var solution))
            {
                if (!sections.TryGetValue(effective, out var section)) sections.Add(effective, section = new(SteelPieces(g, effective, mat.Steel)));
                solutions.Add((effective, phase, nativeSolver), solution = Solve(data, g, effective, phase, applicationPoints[phase], nativeSolver, section));
            }
            return solution with { };
        }

        private BridgeSectionProperties GrossProperties(BridgePhase phase) =>
            grossProperties.TryGetValue(phase, out var properties) ? properties : grossProperties[phase] = GrossPhaseProperties(data, phase);

        /// <summary>The situation of the leading phases of the node followed by the phase, iterated once (an error is kept with it)</summary>
        private BridgeSituationNode Situation(BridgeSituationNode parent, BridgePhase phase, CancellationToken cancellation)
        {
            if (parent.Children.TryGetValue(phase, out var node)) return node;
            node = new(parent.Included.Append(phase).ToArray());
            try
            {
                // The effective-width iterations use the transformed-section field with the Checker integration inertia, which is the Checker
                // linear field to machine precision (relative differences 1e-15); the converged situation is solved again with the Checker
                // solver, which gives the reported stresses and the equilibrium audit. Before, every iteration built a Checker solver for every
                // composite phase (about 2 ms each: 63 solutions and 115 ms for three phases, now 3 solutions).
                bool warmStart = data.Options.AcceleratedIteration;
                node.Situation = CumulativePhaseAnalysis.AnalyzeSituation(node.Included, warmStart && parent != root ? parent.Situation!.State : BridgeEffective.Full(g),
                    (effective, p) => Solution(effective, p, nativeSolver: !data.Options.Class4),
                    contributions => data.Options.Class4 ? EffectiveWidths(g, y => contributions.Sum(c => c.SteelStress(y)), mat.Steel.Fyk, data.Options) : BridgeEffective.Full(g),
                    (current, next) => current.Distance(next, g), (current, next, factor) => current.Relax(next, factor),
                    p => p.Name, cancellation, coordinates: warmStart ? e => e.Coordinates(g) : null);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { node.Error = e; }
            parent.Children.Add(phase, node);
            return node;
        }

        /// <summary>The analysis of an input of the group, from its own data and phases and the shared situations and solutions</summary>
        internal HBridgeBatchResult Calculate(BridgePreparedInput input, CancellationToken cancellation)
        {
            var data = input.Data; var g = input.Geometry; var mat = input.Materials; var phases = input.Phases;
            var shared = Shared(input);
            var stages = new List<BridgeStage>();
            var situations = new BridgeSituationStage[phases.Length];
            var node = root;
            try
            {
                for (int end = 0; end < phases.Length; end++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    node = Situation(node, shared[phases[end]], cancellation);
                    if (node.Error is not null) return new(null, node.Error);
                    situations[end] = Converged(data, g, mat, phases.Take(end + 1).ToArray(), node.Situation!, shared);
                    stages.Add(Stage(data, g, mat, phases.Take(end + 1).ToArray(), situations[end], shared));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { return new(null, e); }
            return Result(data, g, mat, stages, situations);
        }

        /// <summary>The analysis of an input of the group from the converged situations of an earlier analysis of the same section
        /// (<see cref="BridgeComputedSituations.Accepts"/>): its stages, computed again from them, are the ones of <see cref="Calculate"/></summary>
        internal HBridgeBatchResult Recalculate(BridgePreparedInput input, BridgeComputedSituations earlier, CancellationToken cancellation)
        {
            var data = input.Data; var g = input.Geometry; var mat = input.Materials; var phases = input.Phases;
            var shared = Shared(input);
            var stages = new List<BridgeStage>();
            // copies, as the stages of a new analysis
            var situations = earlier.Stages.Select(s => s with { Effective = s.Effective with { }, EffectiveSteel = s.EffectiveSteel with { },
                Contributions = s.Contributions.Select(c => c with { }).ToArray() }).ToArray();
            try
            {
                for (int end = 0; end < phases.Length; end++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    stages.Add(Stage(data, g, mat, phases.Take(end + 1).ToArray(), situations[end], shared));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { return new(null, e); }
            return Result(data, g, mat, stages, situations);
        }

        /// <summary>The canonical phase of each phase of an input</summary>
        private Dictionary<BridgePhase, BridgePhase> Shared(BridgePreparedInput input)
        {
            var shared = new Dictionary<BridgePhase, BridgePhase>(new BridgePhaseIdentity());
            foreach (var p in input.Phases)
            {
                string key = keys.Key(p);
                if (!canonical.TryGetValue(key, out var c)) { canonical.Add(key, c = p); applicationPoints.Add(c, input.ApplicationPoints[p]); }
                shared[p] = c;
            }
            return shared;
        }

        /// <summary>What the converged situation of an input gives to its stage: the contributions and the effective steel on the converged
        /// geometry</summary>
        private BridgeSituationStage Converged(HBridgeInput data, BridgeGeometry g, BridgeMaterialSet mat, BridgePhase[] included,
            CumulativeSituation<BridgeEffective, BridgeContribution> situation, Dictionary<BridgePhase, BridgePhase> shared)
        {
            int end = situation.PhaseIndex, iter = situation.Iterations;
            double error = situation.Residual;
            var effective = situation.State;
            // gross section: the only iteration already used the Checker solver (copies: the situation may belong to other inputs too)
            var contributions = data.Options.Class4 ? included.Select(p => Solution(effective, shared[p], nativeSolver: true)).ToList()
                : situation.Contributions.Select(c => c with { }).ToList();
            // Keep properties and stresses on the same converged geometry. Diagnostic plate data are recomputed on those stresses.
            var diagnostics = data.Options.Class4 ? EffectiveWidths(g, y => contributions.Sum(c => c.SteelStress(y)), mat.Steel.Fyk, data.Options) : BridgeEffective.Full(g);
            // all the plates, including the outstands of the bottom flange of the box (before, BottomOutstand kept the gross plate: results, report
            // and drawing showed fully effective outstands, while BottomWidth included their reduction)
            effective = effective with { Web = diagnostics.Web, Top = diagnostics.Top, Bottom = diagnostics.Bottom, SecondBottom = diagnostics.SecondBottom,
                BottomOutstand = diagnostics.BottomOutstand };
            var effectiveParts = SteelPieces(g, effective, mat.Steel);
            double effectiveArea = effectiveParts.Sum(p => p.Section.Area);
            double effectiveCentroid = effectiveParts.Sum(p => p.Section.Area * p.PositionToGlobal(p.Section.Centroid).Y) / effectiveArea;
            double effectiveInertia = effectiveParts.Sum(p => p.Section.Jxx * Math.Pow(Math.Cos(p.Rotation), 2) + p.Section.Jyy * Math.Pow(Math.Sin(p.Rotation), 2)
                + p.Section.Area * Math.Pow(p.PositionToGlobal(p.Section.Centroid).Y - effectiveCentroid, 2));
            return new(end, iter, error, effective, new(effectiveArea, effectiveCentroid, effectiveInertia), contributions.ToArray());
        }

        /// <summary>The stage of an input from what its converged situation gives (<see cref="Converged"/>): the stress points, shear, studs,
        /// torsion and details. Only these read the studs, stiffeners, transverse reinforcement, fatigue and limit state of the input</summary>
        private BridgeStage Stage(HBridgeInput data, BridgeGeometry g, BridgeMaterialSet mat, BridgePhase[] included, BridgeSituationStage situation,
            Dictionary<BridgePhase, BridgePhase> shared)
        {
            int end = situation.PhaseIndex, iter = situation.Iterations;
            double error = situation.Residual;
            var effective = situation.Effective;
            var contributions = situation.Contributions.ToList();
            double effectiveArea = situation.EffectiveSteel.Area, effectiveInertia = situation.EffectiveSteel.Inertia;
            // the cracked section of the «Soletta esclusa» contributions: all are solved on the same effective geometry of the situation
            var cracked = contributions.FirstOrDefault(c => c.Kind == ConcreteExcludedKind);
            var stiffening = TensionStiffeningApplies(data.Options.LimitState, contributions, g)
                ? BridgeTensionStiffening.Calculate(mat.Concrete.Fctm, g.Width * g.SlabHeight, g.Bars.Sum(b => b.Area), cracked!.Area, cracked.Inertia, effectiveArea, effectiveInertia) : null;
            var points = StressPoints(g, contributions, data, Math.Abs(mat.Concrete.Fck), mat.Steel.Fyk, mat.Rebar.Fyk, stiffening);
            var warnings = new List<string>();
            if (stiffening is not null) warnings.Add(stiffening.Note());
            if (points.Any(p => p.Material == "CLS" && p.Stress > 1e-6)) warnings.Add("CLS teso nel modello non fessurato: valutare una situazione con soletta esclusa; non è attestata la verifica del CLS in trazione.");
            if (!data.Options.Class4) warnings.Add("Sezione lorda: riduzioni locali disattivate. Risultato di confronto, non verifica di classe 4.");
            if (contributions.Any(c => c.IsShrinkage)) warnings.Add("Ritiro uniforme imposto al solo CLS: effetti primari autoequilibrati. Eventuali azioni secondarie da vincoli esterni vanno inserite come fasi N–Mx separate.");
            if (data.Options.Class4) warnings.AddRange(LocalBucklingWarnings(data.Options));
            warnings.AddRange(SectionTypeWarnings(g, data.Box.Enabled));
            if (g.Bottom2Thickness > 0) warnings.Add("Due piastre inferiori modellate con la geometria reale. Instabilità locale: ciascuna piastra come sbalzo dall'anima con il proprio spessore, senza il beneficio dell'accoppiamento (a favore di sicurezza).");
            var materialValues = new BridgeMaterialValues(mat.Concrete.Name, Math.Abs(mat.Concrete.Fck), mat.Concrete.ElasticModulusCompression,
                mat.Steel.Name, mat.Steel.Fyk, mat.Steel.ElasticModulusTension, mat.Rebar.Name, mat.Rebar.Fyk, mat.Rebar.ElasticModulusTension);
            var accessory = CalculateShear(data, g, materialValues, effective, contributions, included, warnings, p => GrossProperties(shared[p]),
                TorsionFlows(data, g, materialValues, included));
            return new(included[end].Name, iter, error, effective, situation.EffectiveSteel, contributions, points, warnings,
                accessory.Shear, accessory.Studs, accessory.Torsion);
        }
    }

    /// <summary>A situation of the leading phases of a batch group, after the parent's ones; its children follow with one more phase</summary>
    private sealed class BridgeSituationNode
    {
        internal BridgeSituationNode(BridgePhase[] included) { Included = included; }
        internal BridgePhase[] Included { get; }
        internal Dictionary<BridgePhase, BridgeSituationNode> Children { get; } = new(new BridgePhaseIdentity());
        internal CumulativeSituation<BridgeEffective, BridgeContribution>? Situation { get; set; }
        internal Exception? Error { get; set; }
    }

    /// <summary>The warnings for the parts whose local buckling is excluded by the options</summary>
    public static IEnumerable<string> LocalBucklingWarnings(BridgeAnalysisOptions options)
    {
        var parts = new List<string>();
        if (!options.TopFlangeBuckling) parts.Add("piattabanda superiore");
        if (!options.BottomFlangeBuckling) parts.Add("piattabanda inferiore");
        if (!options.WebBuckling) parts.Add("anima");
        if (parts.Count > 0)
            yield return "Instabilità locale esclusa per: " + string.Join(", ", parts) + " (interamente efficaci, ρ = 1). Giustificare l'esclusione, ad esempio con classe 1–3 o con il vincolo della soletta connessa.";
    }
    private static double Number(BridgePhase p, string k) => BridgeNumbers.Require(k switch {
        "N" => p.ForceKN, "Mx" => p.MomentKNm, "V" => p.ShearKN, "q_conn" => p.AdditionalConnectionFlow,
        "epsilon_cs" => p.ShrinkageMicrostrain, _ => throw new ArgumentException(k) }, k, double.NegativeInfinity);
    /// <summary>The stress field of one load increment on the effective geometry e</summary>
    /// <param name="d">The input</param>
    /// <param name="g">The geometry</param>
    /// <param name="e">The effective steel geometry</param>
    /// <param name="p">The phase</param>
    /// <param name="applicationPoint">y of the axial force; null: the effective centroid</param>
    /// <param name="nativeSolver">False: the transformed-section field with the Checker integration inertia instead of the Checker solver
    /// (the same field to machine precision, used for the effective-width iterations)</param>
    /// <param name="shared">The steel pieces of e and the native section on them, shared by the phases of the state (null: built here)</param>
    private static BridgeContribution Solve(HBridgeInput d, BridgeGeometry g, BridgeEffective e, BridgePhase p, double? applicationPoint, bool nativeSolver = true,
        BridgeStateSection? shared = null)
    {
        if (p.KindName == ShrinkageKind) return SolveShrinkage(d, g, e, p, nativeSolver, shared);
        var mat = Materials(d); string kind = p.KindName;
        var homo = kind == "Composta" ? Homogenization(d, p) : (0d, 0d, 0d, 0d);
        double n = homo.Item2, area, cy, inertia;
        ReinforcedConcreteSection? composite = null;
        var steel = shared?.Steel ?? SteelPieces(g, e, mat.Steel);
        if (kind == "Composta")
        {
            var section = composite = shared?.Composite;
            if (section is null)
            {
                section = composite = NativeSection(d, g); section.SteelSections.Clear();
                foreach (var part in steel) section.AddSteelSection(part);
                if (shared is not null) shared.Composite = section;
            }
            // Native Model transforms into concrete; divide by n to report in structural steel.
            var props = section.GetHomogeneizedMechanicalProperties(homo.Item3);
            area = props.areaH / n; cy = props.centroidH.Y; inertia = props.JxxH / n;
        }
        else
        {
            var parts = steel.Select(s => (A: s.Section.Area, Y: s.PositionToGlobal(s.Section.Centroid).Y,
                I: s.Section.Jxx * Math.Pow(Math.Cos(s.Rotation), 2) + s.Section.Jyy * Math.Pow(Math.Sin(s.Rotation), 2))).ToList();
            if (kind == "Soletta esclusa")
                parts.AddRange(g.Bars.Select(b => (b.Area * mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension, b.Y,
                    Math.PI * Math.Pow(b.Diameter, 4) / 64 * mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension)));
            area = parts.Sum(x => x.A); cy = parts.Sum(x => x.A * x.Y) / area;
            inertia = parts.Sum(x => x.I + x.A * Math.Pow(x.Y - cy, 2));
        }
        if (!BridgeNumbers.IsFinite(inertia) || inertia <= 0 || !BridgeNumbers.IsFinite(area) || area <= 0) throw new InvalidOperationException("Proprietà della sezione non valide.");
        double yref = applicationPoint ?? cy;
        double force = Number(p, "N") * 1000, moment = Number(p, "Mx") * 1e6;
        double mg = moment + force * (cy - yref), uniform = force / area, slope = -mg / inertia;
        double solverInertia = inertia;
        if (composite is not null)
        {
            // Independent equilibrium audit of Checker's line-wall / point-rebar integration. The integration inertia is a property of the
            // section: before, it was corrected only for a loaded phase (an unloaded composite phase reported the Model inertia)
            solverInertia -= e.TopWidth * Math.Pow(g.TopThickness, 3) / 12
                + (g.Bottom2Thickness > 0 ? e.BottomWidth * Math.Pow(g.Bottom1Thickness, 3) / 12 + e.SecondBottomWidth * Math.Pow(g.Bottom2Thickness, 3) / 12
                    : e.BottomWidth * Math.Pow(g.BottomEquivalentThickness, 3) / 12)
                + g.Bars.Sum(b => Math.PI * Math.Pow(b.Diameter, 4) / 64) * (mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension - 1 / n);
            if (!nativeSolver) slope = -mg / solverInertia;
            else if (force != 0 || moment != 0)
            {
                var field = CompositeLinearStressSolver.Solve(composite, force, moment, g.Width / 2, yref, homo.Item3, d.Options.Standard);
                slope = field.Slope;
                uniform = field.Stress(cy);
            }
        }
        double EquilibriumResidual()
        {
            double calculatedN = uniform * area, calculatedM = -slope * solverInertia - calculatedN * (cy - yref);
            double span = g.Height + g.SlabHeight;
            return Math.Max(Math.Abs(calculatedN - force) / Math.Max(1, Math.Max(Math.Abs(force), Math.Abs(moment) / span)),
                Math.Abs(calculatedM - moment) / Math.Max(1, Math.Max(Math.Abs(moment), Math.Abs(force) * span)));
        }
        double equilibrium = EquilibriumResidual();
        if (composite is not null && nativeSolver && (force != 0 || moment != 0)
            && (!BridgeNumbers.IsFinite(equilibrium) || equilibrium > 1e-5))
        {
            // Native convergence is scaled to section capacity, while this audit is scaled to the applied loads.
            // Very small FEM resultants may therefore be accepted as zero by the native solver. Retry only a failed
            // audit: preserve already accepted results and the independent 1e-5 equilibrium acceptance criterion.
            var field = CompositeLinearStressSolver.Solve(composite, force, moment, g.Width / 2, yref, homo.Item3, d.Options.Standard, 1e-12);
            slope = field.Slope;
            uniform = field.Stress(cy);
            equilibrium = EquilibriumResidual();
        }
        if (!BridgeNumbers.IsFinite(equilibrium) || equilibrium > 1e-5) throw new InvalidOperationException($"Equilibrio della fase {p.Name} non soddisfatto (residuo {equilibrium:E2}).");
        return new(p.Name, kind, force / 1000, moment / 1e6, homo.Item1, n, homo.Item4, homo.Item3,
            area, cy, inertia, uniform, slope, mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension,
            kind == "Solo acciaio" ? 0 : g.Bars.Sum(b => b.Area), inertia / Math.Abs(-g.Height - cy), Math.Abs(cy) < 1e-9 ? null : inertia / Math.Abs(cy), solverInertia, equilibrium, yref, LoadReference(p), Number(p, "V"),
            ConnectionFlowExtra: kind != "Solo acciaio" ? Number(p, "q_conn") : 0);
    }
    private static BridgeContribution SolveShrinkage(HBridgeInput d, BridgeGeometry g, BridgeEffective effective, BridgePhase phase, bool nativeSolver = true,
        BridgeStateSection? shared = null)
    {
        // Input in microstrain: contraction is negative. Bars do not receive an eigenstrain.
        double strain = Number(phase, "epsilon_cs") * 1e-6;
        double ec = Materials(d).Steel.ElasticModulusTension / Homogenization(d, phase).N;
        double ac = g.Width * g.SlabHeight - g.Bars.Sum(b => b.Area);
        double yc = (g.Width * g.SlabHeight * g.SlabHeight / 2 - g.Bars.Sum(b => b.Area * b.Y)) / ac;
        double equivalentForce = ec * ac * strain;
        var fictitious = phase with { Kind = BridgePhaseKind.Composite, ForceKN = equivalentForce / 1000, MomentKNm = 0, ShearKN = 0 };
        var response = Solve(d, g, effective, fictitious, yc, nativeSolver, shared);
        // The equivalent force solves compatibility. Removing the free-strain stress in
        // concrete restores zero external N and M; omitting this term is NOT shrinkage.
        return response with { Kind = ShrinkageKind, N = 0, Mx = 0, V = 0, ShrinkageStrain = strain,
            ConcreteStressOffset = -ec * strain, EquivalentN = equivalentForce / 1000,
            EquivalentMomentAtInterface = -equivalentForce * yc / 1e6, LoadReference = "Ritiro · CLS netto", LoadY = yc };
    }
    /// <summary>The steel pieces of an effective state and the native composite section on them (built by the first composite phase)</summary>
    private sealed class BridgeStateSection
    {
        internal BridgeStateSection(List<SteelSectionPosition> steel) { Steel = steel; }
        internal List<SteelSectionPosition> Steel { get; }
        internal ReinforcedConcreteSection? Composite { get; set; }
    }
    private static List<SteelSectionPosition> SteelPieces(BridgeGeometry g, BridgeEffective e, SteelMaterial material)
    {
        var pieces = new List<SteelSectionPosition>();
        void Add(double width, double height, double y, bool web = false)
        {
            if (width <= 1e-8 || height <= 1e-8) return;
            // Checker integrates along the ThinWall centreline: orient vertical webs along their length,
            // never represent them as a horizontal short line with the web height as its thickness.
            var rectangle = web ? new SectionRectangular(width, height) : new SectionRectangular(height, width);
            pieces.Add(new SteelSectionPosition(new SteelSection(rectangle, material), Point2d.Origin, web ? Math.PI / 2 : 0,
                new Point2d(g.Width / 2, y + height / 2), InsertionPointType.Centroid) { IsInsideConcrete = false });
        }
        Add(e.TopWidth, g.TopThickness, -g.TopThickness);
        Add(g.WebThickness, e.WebTop, -g.TopThickness - e.WebTop, true);
        Add(g.WebThickness, e.WebBottom, -g.TopThickness - g.WebHeight, true);
        if (g.Bottom2Thickness > 0)
        {
            // the two real bottom plates (before, the equivalent rectangle with the same area and total thickness)
            Add(e.BottomWidth, g.Bottom1Thickness, -g.TopThickness - g.WebHeight - g.Bottom1Thickness);
            Add(e.SecondBottomWidth, g.Bottom2Thickness, -g.Height);
        }
        else Add(e.BottomWidth, g.BottomEquivalentThickness, -g.Height);
        return pieces;
    }
    internal const string ConcreteExcludedKind = "Soletta esclusa";
    /// <summary>EN 1994-2 §7.2.1(1): in the SLE states the stresses of the bars of the cracked slab include the tension stiffening, EN 1994-1-1
    /// §7.4.3(3). The slab is cracked in the situations with contributions «Soletta esclusa»</summary>
    internal static bool TensionStiffeningApplies(BridgeLimitState state, IEnumerable<BridgeContribution> contributions, BridgeGeometry g) =>
        state is BridgeLimitState.Rare or BridgeLimitState.QuasiPermanent && g.Bars.Length > 0 && contributions.Any(c => c.Kind == ConcreteExcludedKind);
    private static List<BridgeStressPoint> StressPoints(BridgeGeometry g, List<BridgeContribution> c, HBridgeInput d, double fck, double fy, double fys,
        BridgeTensionStiffening? stiffening)
    {
        var points = new List<BridgeStressPoint>();
        double lc = d.Options.LimitStateName == "SLU" ? d.Options.AlphaCC * fck / d.Options.GammaC : (d.Options.LimitStateName == "SLE rara" ? .6 : .45) * fck;
        double la = d.Options.LimitStateName == "SLU" ? fy / d.Options.GammaM0 : fy;
        double ls = d.Options.LimitStateName == "SLU" ? fys / d.Options.GammaS : .8 * fys;
        void Add(string label, string material, double y, double limit)
        {
            var values = c.Select(p => p.Stress(material, y)).ToArray(); double stress = values.Sum();
            bool active = material switch { "Acciaio" => true, "CLS" => c.Any(x => x.HasConcrete), _ => c.Any(x => x.Kind != "Solo acciaio") };
            // the tensile bars of the cracked slab: σs = σs,0 + Δσs (the contributions give σs,0)
            var bar = material == "Armatura" && active ? stiffening?.At(stress) : null;
            if (bar is not null) stress = bar.Stress;
            double? utilization = !active || material == "CLS" && stress > 1e-6 ? null : Math.Abs(stress) / limit;
            points.Add(new(label, material, y, stress, limit, utilization, active, values) { TensionStiffening = bar });
        }
        Add("Soletta · estradosso", "CLS", g.SlabHeight, lc); Add("Soletta · intradosso", "CLS", 0, lc);
        foreach (var row in g.Bars.GroupBy(b => b.Y).OrderByDescending(x => x.Key)) Add("Armatura · y=" + row.Key.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), "Armatura", row.Key, ls);
        Add("Acciaio · estradosso", "Acciaio", 0, la); Add("Anima · sommità", "Acciaio", -g.TopThickness, la);
        Add("Anima · piede", "Acciaio", -g.TopThickness - g.WebHeight, la); Add("Acciaio · intradosso", "Acciaio", -g.Height, la);
        return points;
    }
}
