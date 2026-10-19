using System.Threading;
using GPC.Model.Geotechnics.EmbeddedWalls;

namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

/// <summary>Incremental small-displacement Euler-Bernoulli beam with two-sided, distributed elastoplastic Winkler soil.
/// Four-point Gauss integration, consistent loads, band Cholesky, Newton with line search and adaptive load increments.
/// An entire failed stage is rolled back; only completed stages are returned. No artificial toe restraint.</summary>
public static partial class EmbeddedWallAnalysis
{
    private static readonly double[] Gauss = { .06943184420297371, .33000947820757187, .6699905217924281, .9305681557970262 };
    private static readonly double[] Weights = { .17392742256872693, .32607257743127305, .32607257743127305, .17392742256872693 };

    public static WallAnalysisResult Calculate(EmbeddedWallModel input, WallSolverOptions? options = null,
        CancellationToken cancellation = default, IProgress<WallAnalysisProgress>? progress = null)
    {
        cancellation.ThrowIfCancellationRequested(); Validate(input);
        var m = Snapshot(input); var given = options ?? new WallSolverOptions();
        var opt = new WallSolverOptions { InitialSubsteps = given.InitialSubsteps, MaximumIterations = given.MaximumIterations,
            MinimumIncrement = given.MinimumIncrement, RelativeResidualTolerance = given.RelativeResidualTolerance, AbsoluteForceTolerance = given.AbsoluteForceTolerance };
        Require(opt.InitialSubsteps >= 1 && opt.InitialSubsteps <= 1024 && opt.MaximumIterations >= 1 && opt.MaximumIterations <= 1000
            && Finite(opt.MinimumIncrement) && opt.MinimumIncrement > 0 && opt.MinimumIncrement <= 1
            && Finite(opt.RelativeResidualTolerance) && opt.RelativeResidualTolerance > 0 && opt.RelativeResidualTolerance < .01
            && Finite(opt.AbsoluteForceTolerance) && opt.AbsoluteForceTolerance > 0, "Invalid iteration controls.");
        var mesh = new Mesh(m); var u = new double[mesh.Dofs]; var history = new History(mesh.Springs, m.Supports.Count);
        var complete = new List<WallStageResult>();
        var result = new WallAnalysisResult { CaseId = m.CaseId, Stages = complete.AsReadOnly(), Status = WallAnalysisStatus.Converged };
        EmbeddedWallStage? previous = null;
        foreach (var stage in m.Stages)
        {
            cancellation.ThrowIfCancellationRequested();
            var start = previous ?? new EmbeddedWallStage { Id = "Initial", Left = stage.Left, Right = stage.Right, StiffnessFactor = stage.StiffnessFactor };
            var reference = (double[])history.SupportReference.Clone();
            var stageHistory = history.Copy();
            for (int i = 0; i < m.Supports.Count; i++)
                if (!start.ActiveSupports.Any(a => a.SupportId == m.Supports[i].Id) && stage.ActiveSupports.Any(a => a.SupportId == m.Supports[i].Id))
                { reference[i] = u[2 * mesh.Node(m.Supports[i].Elevation)]; stageHistory.SupportPlastic[i] = 0; }
            stageHistory.SupportReference = reference;
            double fraction = 0, increment = 1d / opt.InitialSubsteps;
            int accepted = 0, iterations = 0; var trialU = (double[])u.Clone(); Evaluation? last = null;
            WallAnalysisStatus failure = WallAnalysisStatus.NonConvergent; string diagnostic = "";
            while (fraction < 1 - 1e-14)
            {
                cancellation.ThrowIfCancellationRequested(); double target = Math.Min(1, fraction + increment);
                var context = new Step(m, mesh, start, stage, target, stageHistory, trialU);
                var attempt = (double[])trialU.Clone();
                if (Newton(context, attempt, opt, cancellation, out var evaluation, out int used, out failure, out diagnostic))
                {
                    trialU = attempt; stageHistory = evaluation!.History; last = evaluation;
                    fraction = target; accepted++; iterations += used;
                    progress?.Report(new WallAnalysisProgress(stage.Id, fraction, used));
                    increment = Math.Min(1d / opt.InitialSubsteps, increment * 2);
                }
                else
                {
                    increment /= 2;
                    if (increment < opt.MinimumIncrement)
                    {
                        result.Status = failure; result.FailedStage = stage.Id;
                        result.Diagnostic = stage.Id + ": " + diagnostic + "; stage rolled back, last accepted fraction " + fraction.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
                        return result;
                    }
                }
            }
            u = trialU; history = stageHistory;
            complete.Add(Recover(m, mesh, stage, u, last!, accepted, iterations)); previous = stage;
        }
        result.Diagnostic = "All stages converged. Small-displacement beam; no axial soil transfer or second-order effects.";
        return result;
    }

    private static bool Newton(Step step, double[] u, WallSolverOptions options, CancellationToken cancellation,
        out Evaluation? accepted, out int iterations, out WallAnalysisStatus status, out string diagnostic)
    {
        accepted = null; status = WallAnalysisStatus.NonConvergent; diagnostic = "Maximum Newton iterations reached";
        for (iterations = 1; iterations <= options.MaximumIterations; iterations++)
        {
            cancellation.ThrowIfCancellationRequested(); var current = step.Evaluate(u);
            double norm = Norm(current.Residual, step.Mesh.Fixed), tolerance = options.AbsoluteForceTolerance + options.RelativeResidualTolerance * current.Scale;
            double[] delta;
            try { delta = current.Tangent.Solve(current.Residual.Select(v => -v).ToArray(), step.Mesh.Fixed); }
            catch (InvalidOperationException ex) { status = WallAnalysisStatus.Unstable; diagnostic = ex.Message; return false; }
            if (norm <= tolerance) { current.MaximumResidual = norm; accepted = current; return true; }
            bool improved = false;
            for (double factor = 1; factor >= 1d / 1024; factor /= 2)
            {
                cancellation.ThrowIfCancellationRequested();
                var candidate = u.Select((v, i) => v + factor * delta[i]).ToArray();
                var check = step.Evaluate(candidate);
                if (Norm(check.Residual, step.Mesh.Fixed) <= norm * (1 - 1e-4 * factor) || Norm(check.Residual, step.Mesh.Fixed) <= tolerance)
                { Array.Copy(candidate, u, u.Length); improved = true; break; }
            }
            if (!improved) { diagnostic = "Line search did not reduce the equilibrium residual"; return false; }
        }
        return false;
    }
    private static double Norm(double[] residual, bool[] fixedDofs)
    { double value = 0; for (int i = 0; i < residual.Length; i++) if (!fixedDofs[i]) value = Math.Max(value, Math.Abs(residual[i])); return value; }

    private sealed class Mesh
    {
        internal double[] Depth;
        internal int Dofs => 2 * Depth.Length;
        internal int Springs => (Depth.Length - 1) * 8;
        internal bool[] Fixed;
        internal double RotationScale;
        internal EmbeddedWallSegment[] Sections;
        private readonly double top;
        internal Mesh(EmbeddedWallModel m)
        {
            top = m.Top; var boundaries = new SortedSet<double> { m.Top, m.Bottom };
            foreach (double z in m.Segments.SelectMany(s => new[] { s.Top, s.Bottom })
                .Concat(m.LeftSoils.Concat(m.RightSoils).SelectMany(s => new[] { s.Top, s.Bottom }))
                .Concat(m.Supports.Select(s => s.Elevation))
                .Concat(m.Stages.SelectMany(s => new[] { s.Left.Elevation, s.Right.Elevation, s.Left.WaterElevation ?? m.Bottom, s.Right.WaterElevation ?? m.Bottom }))
                .Concat(m.Stages.SelectMany(s => s.Left.PorePressures.Concat(s.Right.PorePressures).Select(p => p.Elevation)))
                .Concat(m.Stages.SelectMany(s => s.PointLoads.Select(p => p.Elevation)))
                .Concat(m.Stages.SelectMany(s => s.DistributedLoads.SelectMany(q => new[] { q.Top, q.Bottom }))))
                if (z >= m.Bottom && z <= m.Top) boundaries.Add(z);
            var depths = boundaries.Select(z => m.Top - z).OrderBy(v => v).ToArray();
            var nodes = new List<double> { 0 };
            for (int j = 1; j < depths.Length; j++)
            {
                double length = depths[j] - depths[j - 1];
                Require(length > 1e-8, "Coincident discontinuities closer than 1e-8 mm; merge the input stations explicitly.");
                int count = (int)Math.Ceiling(length / m.MaximumElementLength);
                Require(count > 0 && count <= 4000 && nodes.Count + count <= 4001, "Mesh exceeds 4000 elements.");
                for (int i = 1; i <= count; i++) nodes.Add(i == count ? depths[j] : depths[j - 1] + length * i / count);
            }
            Depth = nodes.ToArray(); RotationScale = m.MaximumElementLength;
            Fixed = new bool[Dofs]; Fixed[0] = m.FixTopDisplacement; Fixed[1] = m.FixTopRotation;
            Fixed[Dofs - 2] = m.FixBottomDisplacement; Fixed[Dofs - 1] = m.FixBottomRotation;
            Sections = Enumerable.Range(0, Depth.Length - 1).Select(e => m.Segments.First(s => m.Top - (Depth[e] + Depth[e + 1]) / 2 <= s.Top && m.Top - (Depth[e] + Depth[e + 1]) / 2 >= s.Bottom)).ToArray();
        }
        internal int Node(double z)
        {
            int i = Array.BinarySearch(Depth, top - z);
            if (i < 0) throw new InvalidOperationException("Action station not in mesh.");
            return i;
        }
    }

    private sealed class History
    {
        internal double[] Plastic, SupportPlastic, SupportReference;
        internal bool[] Active;
        internal History(int springs, int supports)
        { Plastic = new double[springs]; Active = new bool[springs]; SupportPlastic = new double[supports]; SupportReference = new double[supports]; }
        internal History Copy() => new History(Plastic.Length, SupportPlastic.Length)
        { Plastic = (double[])Plastic.Clone(), Active = (bool[])Active.Clone(), SupportPlastic = (double[])SupportPlastic.Clone(), SupportReference = (double[])SupportReference.Clone() };
    }

    private sealed class Evaluation
    {
        internal WallBandMatrix Tangent;
        internal double[] Residual, External, Soil, Support, Q, Left, Right;
        internal bool[] LeftPlastic, RightPlastic;
        internal History History;
        internal double Scale, MaximumResidual;
        internal List<WallSupportResult> SupportResults = new List<WallSupportResult>();
        internal Evaluation(Mesh mesh, History history)
        {
            Tangent = new WallBandMatrix(mesh.Dofs); Residual = new double[mesh.Dofs]; External = new double[mesh.Dofs]; Soil = new double[mesh.Dofs]; Support = new double[mesh.Dofs];
            Q = new double[mesh.Springs / 2]; Left = new double[Q.Length]; Right = new double[Q.Length];
            LeftPlastic = new bool[Q.Length]; RightPlastic = new bool[Q.Length]; History = history.Copy();
        }
    }

    private sealed class Step
    {
        internal readonly Mesh Mesh;
        private readonly EmbeddedWallModel model;
        private readonly EmbeddedWallStage from, to;
        private readonly double fraction;
        private readonly History history;
        private readonly double[] committedU;
        private readonly WallPressureLimits[,] oldPressures, newPressures;
        internal Step(EmbeddedWallModel m, Mesh mesh, EmbeddedWallStage from, EmbeddedWallStage to, double fraction, History history, double[] committedU)
        {
            model = m; Mesh = mesh; this.from = from; this.to = to; this.fraction = fraction; this.history = history; this.committedU = committedU;
            int count = mesh.Springs / 2; oldPressures = new WallPressureLimits[count, 2]; newPressures = new WallPressureLimits[count, 2];
            for (int e = 0; e < mesh.Depth.Length - 1; e++) for (int g = 0; g < 4; g++)
            {
                int at = 4 * e + g; double z = m.Top - mesh.Depth[e] - Gauss[g] * (mesh.Depth[e + 1] - mesh.Depth[e]);
                oldPressures[at, 0] = WallSoilPressure.At(m.LeftSoils, from.Left, z); oldPressures[at, 1] = WallSoilPressure.At(m.RightSoils, from.Right, z);
                newPressures[at, 0] = WallSoilPressure.At(m.LeftSoils, to.Left, z); newPressures[at, 1] = WallSoilPressure.At(m.RightSoils, to.Right, z);
            }
        }
        internal Evaluation Evaluate(double[] u)
        {
            var r = new Evaluation(Mesh, history);
            double eiFactor = Blend(from.StiffnessFactor, to.StiffnessFactor, fraction);
            double uniform = Blend(from.DistributedLoad, to.DistributedLoad, fraction);
            for (int e = 0; e < Mesh.Depth.Length - 1; e++)
            {
                double h = Mesh.Depth[e + 1] - Mesh.Depth[e], ei = Mesh.Sections[e].FlexuralRigidity * eiFactor;
                for (int g = 0; g < 4; g++)
                {
                    int at = 4 * e + g; double t = Gauss[g], weight = Weights[g] * h;
                    double z = model.Top - Mesh.Depth[e] - t * h;
                    double qExternal = uniform + Blend(IntervalLoad(from, z), IntervalLoad(to, z), fraction);
                    double[] shape = Shape(t, h, Mesh.RotationScale), curvature = Curvature(t, h, Mesh.RotationScale);
                    double displacement = Dot(shape, u, 2 * e), bending = ei * Dot(curvature, u, 2 * e);
                    double pressure = 0, tangent = 0;
                    for (int side = 0; side < 2; side++)
                    {
                        var a = oldPressures[at, side]; var b = newPressures[at, side];
                        int state = 2 * at + side, sign = side == 0 ? 1 : -1;
                        double k = Blend(a.Stiffness, b.Stiffness, fraction), water = Blend(a.Water, b.Water, fraction);
                        double p = 0; bool plastic = false;
                        if (k > 0)
                        {
                            double offset = history.Active[state] ? history.Plastic[state] : -sign * Dot(shape, committedU, 2 * e);
                            var response = WallConstitutiveLaw.Evaluate(-sign * displacement, offset, k,
                                Blend(a.AtRest, b.AtRest, fraction), Blend(a.Active, b.Active, fraction), Blend(a.Passive, b.Passive, fraction));
                            p = response.Pressure; tangent += response.Tangent; plastic = response.IsPlastic;
                            r.History.Plastic[state] = response.PlasticOffset; r.History.Active[state] = true;
                        }
                        else { r.History.Active[state] = false; r.History.Plastic[state] = 0; }
                        pressure += sign * (p + water);
                        if (side == 0) { r.Left[at] = p + water; r.LeftPlastic[at] = plastic; }
                        else { r.Right[at] = p + water; r.RightPlastic[at] = plastic; }
                    }
                    double qSoil = pressure * model.TributaryWidth; r.Q[at] = qSoil + qExternal;
                    for (int i = 0; i < 4; i++)
                    {
                        int row = 2 * e + i;
                        r.Residual[row] += weight * (bending * curvature[i] - (qSoil + qExternal) * shape[i]);
                        r.External[row] += weight * qExternal * shape[i]; r.Soil[row] += weight * qSoil * shape[i];
                        r.Scale = Math.Max(r.Scale, weight * (Math.Abs(qExternal) + (r.Left[at] + r.Right[at]) * model.TributaryWidth) * Math.Abs(shape[i]));
                        for (int j = 0; j <= i; j++) r.Tangent.Add(row, 2 * e + j,
                            weight * (ei * curvature[i] * curvature[j] + tangent * model.TributaryWidth * shape[i] * shape[j]));
                    }
                }
            }
            AddPoints(from, 1 - fraction, r); AddPoints(to, fraction, r);
            for (int i = 0; i < model.Supports.Count; i++)
            {
                var support = model.Supports[i]; var a = from.ActiveSupports.FirstOrDefault(s => s.SupportId == support.Id); var b = to.ActiveSupports.FirstOrDefault(s => s.SupportId == support.Id);
                double activation = Blend(a == null ? 0 : 1, b == null ? 0 : 1, fraction);
                if (activation == 0) { r.History.SupportPlastic[i] = 0; continue; }
                int node = Mesh.Node(support.Elevation), row = 2 * node;
                double cos = Math.Cos(support.Inclination), multiplicity = model.TributaryWidth / support.Spacing;
                double lockOff = Blend(a?.Prestress ?? 0, b?.Prestress ?? 0, fraction);
                double stiffness = activation * support.AxialStiffness;
                double extension = support.ResistingDirection * cos * (u[row] - history.SupportReference[i]);
                double trial = lockOff + stiffness * (extension - history.SupportPlastic[i]);
                double limit = activation * (support.Capacity ?? double.PositiveInfinity);
                double force = Math.Max(support.Kind == EmbeddedWallSupportKind.Bilateral ? -limit : 0, Math.Min(limit, trial));
                bool plastic = force != trial;
                if (plastic) r.History.SupportPlastic[i] = extension - (force - lockOff) / stiffness;
                double reaction = -support.ResistingDirection * cos * force * multiplicity;
                r.Residual[row] -= reaction; r.Support[row] += reaction;
                if (!plastic) r.Tangent.Add(row, row, stiffness * cos * cos * multiplicity);
                r.Scale = Math.Max(r.Scale, Math.Abs(reaction));
                r.SupportResults.Add(new WallSupportResult { Id = support.Id, Kind = support.Kind, AxialStiffness = support.AxialStiffness, AxialForce = force / activation, HorizontalReaction = reaction,
                    ReferenceDisplacement = history.SupportReference[i], AtCapacity = support.Capacity.HasValue && Math.Abs(force) >= limit * (1 - 1e-10) });
            }
            return r;
        }
        private void AddPoints(EmbeddedWallStage stage, double factor, Evaluation r)
        {
            foreach (var p in stage.PointLoads)
            {
                int row = 2 * Mesh.Node(p.Elevation); double f = factor * p.HorizontalForce, moment = factor * p.Moment / Mesh.RotationScale;
                r.Residual[row] -= f; r.Residual[row + 1] -= moment; r.External[row] += f; r.External[row + 1] += moment;
                r.Scale = Math.Max(r.Scale, Math.Max(Math.Abs(f), Math.Abs(moment)));
            }
        }
    }
    private static double Blend(double a, double b, double t) => a * (1 - t) + b * t;
    private static double Dot(double[] a, double[] b, int offset) { double v = 0; for (int i = 0; i < a.Length; i++) v += a[i] * b[offset + i]; return v; }
    private static double[] Shape(double t, double h, double scale) => new[] { 1 - 3 * t * t + 2 * t * t * t, h * (t - 2 * t * t + t * t * t) / scale, 3 * t * t - 2 * t * t * t, h * (-t * t + t * t * t) / scale };
    private static double[] Curvature(double t, double h, double scale) => new[] { (-6 + 12 * t) / (h * h), (-4 + 6 * t) / (h * scale), (6 - 12 * t) / (h * h), (-2 + 6 * t) / (h * scale) };
}
