using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.Seepage;

namespace GPC.Checkers.Geotechnics.Seepage;

/// <summary>Galerkin linear triangles, stationary Darcy/Richards equation. Picard iteration with
/// residual line search; active-set atmospheric faces; diagonally preconditioned conjugate gradients.
/// All input and iteration state is owned by one call. No outputs usable as pressures on failure.</summary>
public static class SeepageAnalysis
{
    public static SeepageResult Calculate(SeepageModel model, SeepageOptions? options = null,
        CancellationToken cancellation = default, IProgress<SeepageProgress>? progress = null)
    {
        cancellation.ThrowIfCancellationRequested(); var mesh = SeepageMesh.Create(model);
        var supplied = options ?? new();
        var opt = new SeepageOptions { MaximumIterations = supplied.MaximumIterations, MaximumLinearIterations = supplied.MaximumLinearIterations,
            HeadTolerance = supplied.HeadTolerance, RelativeFlowTolerance = supplied.RelativeFlowTolerance, AbsoluteFlowTolerance = supplied.AbsoluteFlowTolerance };
        if (opt.MaximumIterations < 1 || opt.MaximumLinearIterations < 1 || !Positive(opt.HeadTolerance)
            || !Positive(opt.RelativeFlowTolerance) || !Positive(opt.AbsoluteFlowTolerance)) throw new ArgumentException("Tolleranze o limiti di iterazione non validi.");
        int n = mesh.Nodes.Length, linearIterations = 0;
        var head = mesh.Initial.Length == n ? (double[])mesh.Initial.Clone() : Enumerable.Repeat(mesh.Heads.Average(p => p.Head), n).ToArray();
        var fixedNodes = new bool[n]; var draining = new bool[n];
        foreach (var h in mesh.Heads) { head[h.Node] = h.Head; fixedNodes[h.Node] = true; }
        double previousChange = 0;
        var result = new SeepageResult { CaseId = mesh.CaseId, Status = SeepageStatus.NonConvergent };
        try
        {
            for (int iteration = 0; iteration < opt.MaximumIterations; iteration++)
            {
                cancellation.ThrowIfCancellationRequested(); result.Iterations = iteration;
                var op = Operator.At(mesh, head, cancellation); var residual = op.Apply(head);
                for (int i = 0; i < n; i++) residual[i] -= mesh.Loads[i];
                double scale = FlowScale(residual, mesh.Loads, fixedNodes), tolerance = opt.AbsoluteFlowTolerance + opt.RelativeFlowTolerance * scale;
                bool changed = false;
                foreach (int i in mesh.Faces)
                {
                    bool active = draining[i] ? residual[i] <= tolerance : head[i] > mesh.Nodes[i].Z + opt.HeadTolerance;
                    if (active == draining[i]) continue;
                    draining[i] = fixedNodes[i] = active; if (active) head[i] = mesh.Nodes[i].Z; changed = true;
                }
                if (changed) { previousChange = double.PositiveInfinity; continue; }
                double error = MaxFree(residual, fixedNodes); result.MaximumResidual = error;
                progress?.Report(new(iteration, error));
                double balance = Math.Abs(residual.Where((r, i) => !fixedNodes[i]).Sum());
                if (error <= tolerance && balance <= tolerance && previousChange <= opt.HeadTolerance)
                {
                    Complete(result, mesh, op, head, residual, fixedNodes, draining);
                    result.LinearIterations = linearIterations; return result;
                }
                var rhs = residual.Select((r, i) => fixedNodes[i] ? 0 : -r).ToArray();
                var delta = Solve(op, rhs, fixedNodes, opt, cancellation, out int count); linearIterations += count;
                double change = delta.Select(Math.Abs).Max();
                if (error <= tolerance && change <= opt.HeadTolerance) { previousChange = change; continue; }
                bool accepted = false;
                for (double fraction = 1; fraction >= 1.0 / 1024; fraction /= 2)
                {
                    cancellation.ThrowIfCancellationRequested(); var trial = head.Select((h, i) => h + fraction * delta[i]).ToArray();
                    var trialOp = Operator.At(mesh, trial, cancellation); var trialResidual = trialOp.Apply(trial);
                    for (int i = 0; i < n; i++) trialResidual[i] -= mesh.Loads[i];
                    if (MaxFree(trialResidual, fixedNodes) < error || MaxFree(trialResidual, fixedNodes) <= tolerance)
                    { head = trial; previousChange = fraction * change; accepted = true; break; }
                }
                if (!accepted) { result.Diagnostic = "Filtrazione: il residuo non diminuisce. Rivedere mesh, condizioni al contorno e inizializzazione."; break; }
            }
        }
        catch (ArithmeticException e) { result.Status = SeepageStatus.IllConditioned; result.Diagnostic = e.Message; }
        result.LinearIterations = linearIterations;
        if (result.Diagnostic.Length == 0) result.Diagnostic = "Filtrazione non convergente entro il limite di iterazioni. Nessun campo di pressione pubblicato.";
        return result;
    }

    private static void Complete(SeepageResult result, SeepageMesh mesh, Operator op, double[] head, double[] residual, bool[] fixedNodes, bool[] draining)
    {
        double inflow = mesh.SpecifiedInflow, outflow = mesh.SpecifiedOutflow;
        var nodes = new SeepageNodeResult[head.Length];
        for (int i = 0; i < head.Length; i++)
        {
            double reaction = fixedNodes[i] ? residual[i] : 0;
            if (reaction >= 0) inflow += reaction; else outflow -= reaction;
            nodes[i] = new() { Index = i, X = mesh.Nodes[i].X, Z = mesh.Nodes[i].Z, Head = head[i], Pressure = SoilUnits.WaterUnitWeight * (head[i] - mesh.Nodes[i].Z),
                BoundaryReaction = reaction, Draining = draining[i] };
        }
        var elements = new SeepageElementResult[mesh.Elements.Length];
        for (int e = 0; e < elements.Length; e++)
        {
            var t = mesh.Elements[e]; var k = t.Material;
            double gx = 0, gz = 0;
            for (int j = 1; j < 3; j++) { double dh = head[t.Nodes[j]] - head[t.Nodes[0]]; gx += t.X[j] * dh; gz += t.Z[j] * dh; }
            elements[e] = new() { Index = e, A = t.Nodes[0], B = t.Nodes[1], C = t.Nodes[2], GradientX = gx, GradientZ = gz,
                RelativeConductivity = op.Relative[e], FluxX = -op.Relative[e] * (k.Kxx * gx + k.Kxz * gz), FluxZ = -op.Relative[e] * (k.Kxz * gx + k.Kzz * gz) };
        }
        result.Status = SeepageStatus.Converged; result.Diagnostic = "Equilibrio idraulico convergente; verificare sensibilità alla mesh e condizioni al contorno.";
        result.Nodes = Array.AsReadOnly(nodes); result.Elements = Array.AsReadOnly(elements); result.Inflow = inflow; result.Outflow = outflow;
    }

    private sealed class Operator
    {
        private readonly SeepageMesh mesh;
        internal double[] Relative { get; }
        internal double[] Diagonal { get; }
        private Operator(SeepageMesh mesh) { this.mesh = mesh; Relative = new double[mesh.Elements.Length]; Diagonal = new double[mesh.Nodes.Length]; }
        internal static Operator At(SeepageMesh mesh, double[] head, CancellationToken cancellation)
        {
            var op = new Operator(mesh);
            for (int e = 0; e < mesh.Elements.Length; e++)
            {
                if (e % 256 == 0) cancellation.ThrowIfCancellationRequested();
                var t = mesh.Elements[e]; double relative = 1;
                if (t.Material.Unsaturated != null)
                {
                    relative = 0;
                    // Three symmetric second-order quadrature points; coefficients vary with h-z.
                    for (int q = 0; q < 3; q++)
                    {
                        double psi = 0;
                        for (int j = 0; j < 3; j++) psi += (j == q ? 2.0 / 3 : 1.0 / 6) * (head[t.Nodes[j]] - mesh.Nodes[t.Nodes[j]].Z);
                        relative += UnsaturatedConductivity.At(t.Material.Unsaturated, psi).RelativeConductivity / 3;
                    }
                }
                if (!Positive(relative)) throw new ArithmeticException("Conducibilità non satura nulla o non finita: dominio fuori dal campo numerico; nessuna permeabilità minima introdotta.");
                op.Relative[e] = relative;
                for (int j = 0; j < 3; j++) op.Diagonal[t.Nodes[j]] += relative * t.Matrix[4 * j];
            }
            if (op.Diagonal.Any(d => !Positive(d))) throw new ArithmeticException("Matrice idraulica non definita positiva o fuori campo numerico.");
            return op;
        }
        internal double[] Apply(double[] vector)
        {
            var result = new double[vector.Length];
            for (int e = 0; e < mesh.Elements.Length; e++)
            {
                var t = mesh.Elements[e];
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
                    result[t.Nodes[i]] += Relative[e] * t.Matrix[3 * i + j] * (vector[t.Nodes[j]] - vector[t.Nodes[i]]);
            }
            if (result.Any(x => !UnsaturatedConductivity.Finite(x))) throw new ArithmeticException("Portate fuori campo numerico.");
            return result;
        }
    }

    private static double[] Solve(Operator op, double[] rhs, bool[] fixedNodes, SeepageOptions options, CancellationToken cancellation, out int iterations)
    {
        int n = rhs.Length; var x = new double[n]; var r = (double[])rhs.Clone(); var z = new double[n];
        for (int i = 0; i < n; i++) z[i] = r[i] / op.Diagonal[i];
        var p = (double[])z.Clone(); double rz = Dot(r, z);
        double tolerance = Math.Max(options.AbsoluteFlowTolerance * .001, 1e-11 * Math.Sqrt(Dot(rhs, rhs)));
        iterations = 0;
        if (Math.Sqrt(Dot(r, r)) <= tolerance) return x;
        for (; iterations < options.MaximumLinearIterations; iterations++)
        {
            cancellation.ThrowIfCancellationRequested(); var ap = op.Apply(p);
            for (int i = 0; i < n; i++) if (fixedNodes[i]) ap[i] = 0;
            double pap = Dot(p, ap);
            if (!Positive(pap) || !Positive(rz)) throw new ArithmeticException("Sistema idraulico singolare o mal condizionato.");
            double alpha = rz / pap;
            for (int i = 0; i < n; i++) { x[i] += alpha * p[i]; r[i] -= alpha * ap[i]; }
            if (Math.Sqrt(Dot(r, r)) <= tolerance) { iterations++; return x; }
            for (int i = 0; i < n; i++) z[i] = r[i] / op.Diagonal[i];
            double next = Dot(r, z), beta = next / rz; rz = next;
            for (int i = 0; i < n; i++) p[i] = z[i] + beta * p[i];
        }
        throw new ArithmeticException("Soluzione lineare idraulica non convergente entro il limite di iterazioni.");
    }
    private static double Dot(double[] a, double[] b) { double value = 0; for (int i = 0; i < a.Length; i++) value += a[i] * b[i]; return value; }
    private static bool Positive(double x) => UnsaturatedConductivity.Finite(x) && x > 0;
    private static double MaxFree(double[] r, bool[] fixedNodes) { double max = 0; for (int i = 0; i < r.Length; i++) if (!fixedNodes[i]) max = Math.Max(max, Math.Abs(r[i])); return max; }
    private static double FlowScale(double[] r, double[] loads, bool[] fixedNodes)
    { double scale = loads.Sum(Math.Abs); for (int i = 0; i < r.Length; i++) if (fixedNodes[i]) scale += Math.Abs(r[i]); return scale; }
}
