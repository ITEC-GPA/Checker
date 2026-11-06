using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;

namespace GPC.Checkers.Concrete.Response
{
    /// <summary>Sequential N-M1-M2 equilibrium with load or uniform shrinkage increments and memoryless constitutive laws.
    /// Geometry and materials are fixed throughout. The checker must not be mutated or used concurrently during a call.
    /// Previous states are numerical predictors only. Neither plastic strains nor damage/crack history are accumulated.
    /// Nonlinear softening may admit multiple equilibria: this is continuation, not a proof of global uniqueness/stability.</summary>
    public static class ConcretePhaseAnalysis
    {
        public static ConcretePhaseAnalysisResult Calculate(SectionChecker checker, IEnumerable<ConcretePhase> phases,
            ConcretePhaseOptions options = null, CancellationToken cancellationToken = default)
        {
            if (checker == null) throw new ArgumentNullException(nameof(checker));
            if (phases == null) throw new ArgumentNullException(nameof(phases));
            var input = phases.ToArray();
            if (input.Any(p => p == null)) throw new ArgumentException("Null phase.", nameof(phases));
            cancellationToken.ThrowIfCancellationRequested();
            return new Runner(new SectionSolver.PhaseSection(checker.SectionSolver), options ?? new ConcretePhaseOptions(), cancellationToken).Run(input);
        }

        /// <summary>Same engine and diagnostics as Calculate. Cancellation throws OperationCanceledException; it never returns a completed partial history.</summary>
        public static Task<ConcretePhaseAnalysisResult> CalculateAsync(SectionChecker checker, IEnumerable<ConcretePhase> phases,
            ConcretePhaseOptions options = null, CancellationToken cancellationToken = default)
        {
            if (phases == null) throw new ArgumentNullException(nameof(phases));
            var input = phases.ToArray();
            return Task.Run(() => Calculate(checker, input, options, cancellationToken), cancellationToken);
        }

        private sealed class Runner
        {
            private readonly SectionSolver.PhaseSection section;
            private readonly ConcretePhaseOptions options;
            private readonly CancellationToken token;
            private double[] q = new double[3];
            private double shrinkage;
            private ForceTuple target, force;
            private int iterations, attempts, accepted;
            private ConcretePhaseStop stop;

            internal Runner(SectionSolver.PhaseSection section, ConcretePhaseOptions options, CancellationToken token)
            { this.section = section; this.options = options; this.token = token; }

            internal ConcretePhaseAnalysisResult Run(ConcretePhase[] phases)
            {
                var results = new List<ConcretePhaseResult>();
                var initialWatch = Stopwatch.StartNew();
                // Fixed prestrains, if any, must first equilibrate at zero external actions.
                if (!Solve(target, 0, q, out q, out force))
                    return new ConcretePhaseAnalysisResult(null, results, stop, "Initial equilibrium with fixed prestrains was not found.", iterations, section.Evaluations, initialWatch.Elapsed.TotalMilliseconds);
                var initial = Snapshot();
                initialWatch.Stop();
                int initialIterations = iterations, initialEvaluations = section.Evaluations;
                for (int i = 0; i < phases.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var watch = Stopwatch.StartNew();
                    iterations = attempts = accepted = 0;
                    int evaluations = section.Evaluations;
                    var phase = phases[i];
                    var increment = phase.Kind == ConcretePhaseKind.Load ? section.Load(phase.LoadIncrement) : default;
                    var startForce = target;
                    double startShrinkage = shrinkage, fraction = 0;
                    stop = ConcretePhaseStop.Completed;

                    bool Advance(double end, int depth)
                    {
                        token.ThrowIfCancellationRequested();
                        if (attempts >= options.MaximumAttemptsPerPhase)
                        { stop = ConcretePhaseStop.EquilibriumNotFound; return false; }
                        attempts++;
                        var endForce = startForce + increment * end;
                        double endShrinkage = startShrinkage + phase.ShrinkageIncrement * end;
                        if (!ConcretePhase.Finite(endForce.N) || !ConcretePhase.Finite(endForce.Mx) || !ConcretePhase.Finite(endForce.My) || !ConcretePhase.Finite(endShrinkage))
                            throw new ArgumentException("Cumulative phase actions or shrinkage overflowed.");
                        var guess = (double[])q.Clone();
                        guess[0] += endShrinkage - shrinkage;
                        if (Solve(endForce, endShrinkage, guess, out var solution, out var integrated))
                        {
                            q = solution; target = endForce; force = integrated; shrinkage = endShrinkage;
                            fraction = end; accepted++; stop = ConcretePhaseStop.Completed;
                            return true;
                        }
                        if (depth >= options.MaximumSubdivisions) return false;
                        double middle = .5 * (fraction + end);
                        return Advance(middle, depth + 1) && Advance(end, depth + 1);
                    }

                    for (int k = 1; k <= phase.Substeps; k++)
                        if (!Advance((double)k / phase.Substeps, 0)) break;
                    var state = Snapshot();
                    watch.Stop();
                    string message = stop == ConcretePhaseStop.Completed ? null
                        : stop == ConcretePhaseStop.MaterialDomain ? "Equilibrium not reached within the material strain domain; State is the last accepted substep."
                        : "Equilibrium not reached within the numerical limits; State is the last accepted substep.";
                    results.Add(new ConcretePhaseResult(i, phase, state, stop, fraction, iterations, attempts, accepted,
                        section.Evaluations - evaluations, watch.Elapsed.TotalMilliseconds, message));
                    if (stop != ConcretePhaseStop.Completed) return new ConcretePhaseAnalysisResult(initial, results, stop, message, initialIterations, initialEvaluations, initialWatch.Elapsed.TotalMilliseconds);
                }
                return new ConcretePhaseAnalysisResult(initial, results, ConcretePhaseStop.Completed, null, initialIterations, initialEvaluations, initialWatch.Elapsed.TotalMilliseconds);
            }

            private ConcretePhaseState Snapshot()
                => section.State(q, shrinkage, target, force, Jacobian(q, shrinkage, force), options.IncludeIntegrationPoints, Merit(force, target));

            // Residual merit is assessed in the USER's force axes, including origin shifts.
            // N and M tolerances have compatible relative scales even when one target component vanishes.
            private double Merit(ForceTuple value, ForceTuple requested)
            {
                var f = section.External(value); var t = section.External(requested);
                double scale = Math.Max(Math.Abs(t.N), Math.Max(Math.Abs(t.M1), Math.Abs(t.M2)) / section.Length);
                double nt = options.ForceTolerance + options.RelativeTolerance * scale;
                double mt = options.MomentTolerance + options.RelativeTolerance * scale * section.Length;
                return Math.Max(Math.Abs(f.N - t.N) / nt, Math.Max(Math.Abs(f.M1 - t.M1), Math.Abs(f.M2 - t.M2)) / mt);
            }

            private bool Solve(ForceTuple requested, double imposed, double[] guess, out double[] solution, out ForceTuple integrated)
            {
                solution = (double[])guess.Clone(); integrated = default;
                stop = ConcretePhaseStop.EquilibriumNotFound;
                if (!section.Evaluate(solution, imposed, out integrated))
                { stop = ConcretePhaseStop.MaterialDomain; return false; }
                bool restarted = false;
                for (int i = 0; i <= options.MaximumIterations; i++)
                {
                    token.ThrowIfCancellationRequested();
                    double merit = Merit(integrated, requested);
                    if (merit <= 1) { stop = ConcretePhaseStop.Completed; return true; }
                    if (i == options.MaximumIterations) return false;
                    iterations++;
                    var jacobian = Jacobian(solution, imposed, integrated);
                    double[] residual = { requested.N - integrated.N, (requested.Mx - integrated.Mx) / section.Length, (requested.My - integrated.My) / section.Length };
                    double[] correction;
                    if (jacobian == null || !Solve3(jacobian, residual, out correction))
                    {
                        // A flat envelope (yield plateau or zero tensile concrete) is not an irreversible state.
                        // Try the reference tangent to return to a responsive branch; acceptance still uses the actual law.
                        var reference = new double[3];
                        if (!section.Evaluate(reference, 0, out var referenceForce)) return false;
                        var referenceTangent = Jacobian(reference, 0, referenceForce);
                        if (referenceTangent == null || !Solve3(referenceTangent, residual, out correction)) return false;
                    }
                    // Keep a full Newton correction from jumping several material branches at once.
                    double largest = correction.Max(v => Math.Abs(v));
                    double factor = largest > .002 ? .002 / largest : 1;
                    bool moved = false, outside = false;
                    for (int j = 0; j < options.MaximumLineSearchIterations; j++, factor *= .5)
                    {
                        var trial = new[] { solution[0] + factor * correction[0], solution[1] + factor * correction[1], solution[2] + factor * correction[2] };
                        if (!section.Evaluate(trial, imposed, out var trialForce)) { outside = true; continue; }
                        double trialMerit = Merit(trialForce, requested);
                        if (trialMerit <= 1 || trialMerit < merit * (1 - 1e-4 * factor))
                        { solution = trial; integrated = trialForce; moved = true; break; }
                    }
                    if (!moved)
                    {
                        // A small unloading correction can remain entirely on a flat plateau, even with the
                        // reference tangent. Restart once from free concrete strain; no constitutive state is lost.
                        var reference = new[] { imposed, 0.0, 0.0 };
                        if (!restarted && section.Evaluate(reference, imposed, out var referenceForce))
                        { restarted = true; solution = reference; integrated = referenceForce; continue; }
                        stop = outside ? ConcretePhaseStop.MaterialDomain : ConcretePhaseStop.EquilibriumNotFound;
                        return false;
                    }
                }
                return false;
            }

            // Derivatives of the integrated response preserve analytic parabola integration and displaced-concrete subtraction.
            // All unknowns are dimensionless: epsilon0, ChiX*L, ChiY*L. One-sided derivatives at domain endpoints.
            private double[,] Jacobian(double[] state, double imposed, ForceTuple value)
            {
                var k = new double[3, 3];
                for (int j = 0; j < 3; j++)
                {
                    double h = 1e-8 * Math.Max(1, Math.Abs(state[j]));
                    var plus = (double[])state.Clone(); var minus = (double[])state.Clone();
                    plus[j] += h; minus[j] -= h;
                    bool p = section.Evaluate(plus, imposed, out var fp), m = section.Evaluate(minus, imposed, out var fm);
                    if (!p && !m) return null;
                    double denominator = p && m ? 2 * h : h;
                    if (!p) fp = value; if (!m) fm = value;
                    k[0, j] = (fp.N - fm.N) / denominator;
                    k[1, j] = (fp.Mx - fm.Mx) / (denominator * section.Length);
                    k[2, j] = (fp.My - fm.My) / (denominator * section.Length);
                }
                return k;
            }

            // Partial pivoting on a dimensionally scaled 3x3 matrix; never invert the stiffness matrix.
            private static bool Solve3(double[,] matrix, double[] rhs, out double[] x)
            {
                x = new double[3]; var a = (double[,])matrix.Clone(); var b = (double[])rhs.Clone();
                double scale = 0;
                foreach (double v in a) scale = Math.Max(scale, Math.Abs(v));
                if (!ConcretePhase.Finite(scale) || scale == 0) return false;
                for (int j = 0; j < 3; j++)
                {
                    int pivot = j;
                    for (int i = j + 1; i < 3; i++) if (Math.Abs(a[i, j]) > Math.Abs(a[pivot, j])) pivot = i;
                    if (Math.Abs(a[pivot, j]) < 1e-14 * scale) return false;
                    if (pivot != j)
                    {
                        for (int c = j; c < 3; c++) { double t = a[j, c]; a[j, c] = a[pivot, c]; a[pivot, c] = t; }
                        double tB = b[j]; b[j] = b[pivot]; b[pivot] = tB;
                    }
                    for (int i = j + 1; i < 3; i++)
                    {
                        double f = a[i, j] / a[j, j];
                        for (int c = j + 1; c < 3; c++) a[i, c] -= f * a[j, c];
                        b[i] -= f * b[j];
                    }
                }
                for (int j = 2; j >= 0; j--)
                {
                    double v = b[j]; for (int c = j + 1; c < 3; c++) v -= a[j, c] * x[c];
                    x[j] = v / a[j, j]; if (!ConcretePhase.Finite(x[j])) return false;
                }
                return true;
            }
        }
    }
}
