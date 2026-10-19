using GPC.Model.Geotechnics.EmbeddedWalls;

namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

public static partial class EmbeddedWallAnalysis
{
    private static WallStageResult Recover(EmbeddedWallModel model, Mesh mesh, EmbeddedWallStage stage, double[] u, Evaluation evaluation, int increments, int iterations)
    {
        var points = new List<WallPointResult>(); var reactions = new List<WallBoundaryReaction>();
        double forceResidual = 0, momentResidual = 0;
        for (int node = 0; node < mesh.Depth.Length; node++)
        {
            int row = 2 * node;
            double rf = mesh.Fixed[row] ? evaluation.Residual[row] : 0;
            double rm = mesh.Fixed[row + 1] ? evaluation.Residual[row + 1] * mesh.RotationScale : 0;
            double f = evaluation.External[row] + evaluation.Soil[row] + evaluation.Support[row] + rf;
            double m = (evaluation.External[row + 1] + evaluation.Soil[row + 1] + evaluation.Support[row + 1]) * mesh.RotationScale + rm;
            forceResidual += f; momentResidual += f * mesh.Depth[node] + m;
            if (mesh.Fixed[row] || mesh.Fixed[row + 1]) reactions.Add(new WallBoundaryReaction { Elevation = model.Top - mesh.Depth[node], Force = rf, Moment = rm });
        }
        for (int e = 0; e < mesh.Depth.Length - 1; e++)
        {
            double h = mesh.Depth[e + 1] - mesh.Depth[e], ei = mesh.Sections[e].FlexuralRigidity * stage.StiffnessFactor;
            var end = new double[4];
            for (int g = 0; g < 4; g++)
            {
                var n = Shape(Gauss[g], h, mesh.RotationScale); var b = Curvature(Gauss[g], h, mesh.RotationScale);
                for (int i = 0; i < 4; i++) end[i] += Weights[g] * h * (ei * Dot(b, u, 2 * e) * b[i] - evaluation.Q[4 * e + g] * n[i]);
            }
            end[1] *= mesh.RotationScale; end[3] *= mesh.RotationScale;
            var q = Interpolate(evaluation.Q.Skip(4 * e).Take(4).ToArray());
            var left = Interpolate(evaluation.Left.Skip(4 * e).Take(4).ToArray());
            var right = Interpolate(evaluation.Right.Skip(4 * e).Take(4).ToArray());
            var v = Integral(q, h, end[0]); var moment = Integral(v, h, -end[1]);
            // Include all roots of V for the governing moment; subdivisions isolate roots of this degree-four polynomial.
            var samples = new SortedSet<double>(Gauss.Concat(new[] { 0d, .125, .25, .375, .5, .625, .75, .875, 1 }));
            foreach (double root in PolynomialRoots(v)) samples.Add(root);
            for (int g = 0; g < 4; g++) if (evaluation.LeftPlastic[4 * e + g] || evaluation.RightPlastic[4 * e + g]) samples.Add(Gauss[g]);
            foreach (double t in samples)
            {
                double depth = mesh.Depth[e] + h * t, z = model.Top - depth;
                string side = t == 0 ? "Below" : t == 1 ? "Above" : "Interior";
                double[] rotation = { (-6 * t + 6 * t * t) / h, (1 - 4 * t + 3 * t * t) / mesh.RotationScale, (6 * t - 6 * t * t) / h, (-2 * t + 3 * t * t) / mesh.RotationScale };
                double axial = model.Segments.Sum(s => s.WeightPerLength * Math.Max(0, s.Top - Math.Max(s.Bottom, z)));
                foreach (var p in stage.PointLoads) if (p.Elevation > z || p.Elevation == z && side != "Above") axial += p.DownwardForce;
                foreach (var support in evaluation.SupportResults)
                {
                    var definition = model.Supports.First(s => s.Id == support.Id);
                    if (definition.Elevation > z || definition.Elevation == z && side != "Above") axial += support.NormalForce * Math.Sin(definition.Inclination) * model.TributaryWidth / definition.Spacing;
                }
                int nearest = Enumerable.Range(0, 4).OrderBy(g => Math.Abs(t - Gauss[g])).First();
                points.Add(new WallPointResult { Elevation = z, Side = side, SectionId = mesh.Sections[e].SectionId,
                    Displacement = Dot(Shape(t, h, mesh.RotationScale), u, 2 * e), Rotation = Dot(rotation, u, 2 * e),
                    NormalForce = axial, Shear = Value(v, t), Moment = Value(moment, t),
                    LeftPressure = Value(left, t), RightPressure = Value(right, t), NetDistributedLoad = Value(q, t),
                    LeftPlastic = evaluation.LeftPlastic[4 * e + nearest], RightPlastic = evaluation.RightPlastic[4 * e + nearest] });
            }
        }
        return new WallStageResult { Id = stage.Id, CaseId = model.CaseId, AcceptedIncrements = increments, Iterations = iterations,
            ForceResidual = forceResidual, MomentResidual = momentResidual, MaximumScaledResidual = evaluation.MaximumResidual,
            Points = points.AsReadOnly(), Reactions = reactions.AsReadOnly(), Supports = evaluation.SupportResults.AsReadOnly() };
    }

    private static double[] Interpolate(double[] values)
    {
        var coefficients = new double[4];
        for (int i = 0; i < 4; i++)
        {
            var basis = new[] { 1d }; double denominator = 1;
            for (int j = 0; j < 4; j++) if (i != j)
            {
                var next = new double[basis.Length + 1];
                for (int k = 0; k < basis.Length; k++) { next[k] -= Gauss[j] * basis[k]; next[k + 1] += basis[k]; }
                basis = next; denominator *= Gauss[i] - Gauss[j];
            }
            for (int k = 0; k < 4; k++) coefficients[k] += values[i] * basis[k] / denominator;
        }
        return coefficients;
    }
    private static double[] Integral(double[] p, double length, double initial)
    { var v = new double[p.Length + 1]; v[0] = initial; for (int i = 0; i < p.Length; i++) v[i + 1] = length * p[i] / (i + 1); return v; }
    private static double Value(double[] p, double t) { double v = 0; for (int i = p.Length - 1; i >= 0; i--) v = v * t + p[i]; return v; }

    private static IEnumerable<double> PolynomialRoots(double[] source)
    {
        int degree = source.Length - 1; double scale = source.Max(v => Math.Abs(v));
        if (scale == 0) return Array.Empty<double>();
        while (degree > 0 && Math.Abs(source[degree]) <= scale * 1e-13) degree--;
        if (degree == 0) return Array.Empty<double>();
        if (degree == 1) { double root = -source[0] / source[1]; return root > 0 && root < 1 ? new[] { root } : Array.Empty<double>(); }
        var p = source.Take(degree + 1).ToArray(); var derivative = Enumerable.Range(1, degree).Select(i => i * p[i]).ToArray();
        var boundaries = new[] { 0d }.Concat(PolynomialRoots(derivative)).Concat(new[] { 1d }).OrderBy(v => v).ToArray();
        var roots = new List<double>();
        foreach (double t in boundaries) if (t > 0 && t < 1 && Math.Abs(Value(p, t)) <= scale * 1e-12) roots.Add(t);
        for (int i = 1; i < boundaries.Length; i++)
        {
            double a = boundaries[i - 1], b = boundaries[i], fa = Value(p, a), fb = Value(p, b);
            if (fa * fb >= 0) continue;
            for (int step = 0; step < 60; step++) { double mid = (a + b) / 2, fm = Value(p, mid); if (fa * fm <= 0) b = mid; else { a = mid; fa = fm; } }
            roots.Add((a + b) / 2);
        }
        return roots;
    }
}
