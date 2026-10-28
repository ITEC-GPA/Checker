using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Distribution only: Balanced is not a resistance verification of a bolt or joint.</summary>
    public enum ElasticBoltGroupStatus { Balanced, RequiresContact, UnsupportedGeometry, NumericalFailure }

    /// <summary>Force transmitted at one bolt, in N. Positive Fz is bolt tension.</summary>
    public sealed class ElasticBoltForce
    {
        public int Index { get; }
        public ConnectionPoint2D Position { get; }
        public double Fx { get; }
        public double Fy { get; }
        public double Fz { get; }
        public double Shear => ElasticBoltGroup.Hypot(Fx, Fy);
        internal ElasticBoltForce(int index, ConnectionPoint2D position, double fx, double fy, double fz)
        { Index = index; Position = position; Fx = fx; Fy = fy; Fz = fz; }
    }

    public sealed class ElasticBoltGroupResult
    {
        public IReadOnlyList<ConnectionPoint2D> Points { get; }
        public PlanarConnectionActions InputActions { get; }
        public ConnectionPoint2D Centroid { get; }
        /// <summary>Zero for one point; one for a line; two for a planar group.</summary>
        public int GeometryRank { get; }
        public ElasticBoltGroupStatus Status { get; }
        public bool IsSupported => Status == ElasticBoltGroupStatus.Balanced;
        /// <summary>Empty outside the supported field: no compression is returned as a bolt force.</summary>
        public IReadOnlyList<ElasticBoltForce> Forces { get; }
        public PlanarConnectionActions ActionsAtCentroid { get; }
        /// <summary>Sum of transmitted actions minus applied actions, at the centroid. Null if unsupported.</summary>
        public PlanarConnectionActions EquilibriumResidual { get; }
        public double? MaximumShear => IsSupported ? (double?)Forces.Max(f => f.Shear) : null;
        public double? MaximumTension => IsSupported ? (double?)Forces.Max(f => f.Fz) : null;
        /// <summary>Signed minimum of the elastic trial solution, retained to explain required contact.</summary>
        public double? MinimumTrialFz { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public string Scope => "Equal-stiffness point bolts and rigid connected parts; small-displacement elastic "
            + "distribution only. No bolt resistance, plate flexibility, prying, slip or compression contact model.";
        internal ElasticBoltGroupResult(IReadOnlyList<ConnectionPoint2D> points, PlanarConnectionActions input,
            ConnectionPoint2D centroid, int rank, ElasticBoltGroupStatus status, IList<ElasticBoltForce> forces,
            PlanarConnectionActions atCentroid, PlanarConnectionActions residual, double? minimumTrialFz, string diagnostic)
        {
            // An immutable copy of the positions, shared by the results of the same prepared group.
            Points = points; InputActions = input;
            Centroid = centroid; GeometryRank = rank; Status = status;
            Forces = Array.AsReadOnly(forces.ToArray()); ActionsAtCentroid = atCentroid;
            EquilibriumResidual = residual; MinimumTrialFz = minimumTrialFz;
            Diagnostics = Array.AsReadOnly(diagnostic == null ? new string[0] : new[] { diagnostic });
        }
    }

    /// <summary>Elastic distribution on identical bolts in XY. Units N, mm and Nmm; right-handed moments.
    /// Equilibrium equations and a linear normal-force field are solved, without normative resistance factors.</summary>
    internal static class ElasticBoltGroup
    {
        // No action floor in N/Nmm: checks scale with their own terms, including tiny demands.
        public const double EquilibriumRelativeTolerance = 1e-10;
        private const double CollinearityTolerance = 1.4210854715202004e-14; // 64 binary64 epsilons.
        private const double MinimumInertiaRatio = 1e-12;

        public static ElasticBoltGroupResult Calculate(IEnumerable<ConnectionPoint2D> points, PlanarConnectionActions actions)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            return Evaluate(Prepare(points), actions);
        }

        /// <summary>Geometry-only part of the elastic distribution, shared by every action state
        /// on the same immutable bolt positions. Evaluate repeats exactly the floating-point
        /// operations of a direct calculation, so prepared and direct results are bit-identical.</summary>
        internal sealed class Prepared
        {
            internal ConnectionPoint2D[] Points;
            internal IReadOnlyList<ConnectionPoint2D> ReadOnlyPoints;
            internal int Count;
            internal bool CentroidFinite;
            internal double Cx, Cy;
            internal ConnectionPoint2D Centroid;
            internal double[] X, Y;     // relative coordinates, divided by Scale when Scale != 0
            internal double Scale;
            internal bool ScaleFinite;
            internal int Rank;          // 1 or 2 when Scale != 0
            internal double Ux, Uy, Qq, Polar;
            internal double[] Q, R;     // rank-1 basis (Q = X, R = Y for rank 2)
            internal ElasticBoltGroupStatus? GeometryRejection;
            internal string GeometryReason;
            internal double Xx, Yy, Xy, XyDeterminant;
        }

        internal static Prepared Prepare(IEnumerable<ConnectionPoint2D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            var p = points.ToArray();
            if (p.Length == 0) throw new ArgumentException("At least one bolt is required.", nameof(points));
            var distinct = new HashSet<Tuple<double, double>>();
            foreach (var point in p)
            {
                if (!Finite(point.X) || !Finite(point.Y)) throw new ArgumentException("Coordinates must be finite.", nameof(points));
                if (!distinct.Add(Tuple.Create(point.X, point.Y))) throw new ArgumentException("Repeated bolt positions are not permitted.", nameof(points));
            }
            var g = new Prepared { Points = p, ReadOnlyPoints = Array.AsReadOnly((ConnectionPoint2D[])p.Clone()), Count = p.Length, Centroid = p[0] };
            // Referencing the first bolt avoids loss of small spacing under large translations.
            double cx = p[0].X + Sum(p.Select(v => (v.X - p[0].X) / p.Length));
            double cy = p[0].Y + Sum(p.Select(v => (v.Y - p[0].Y) / p.Length));
            g.Cx = cx; g.Cy = cy;
            g.CentroidFinite = Finite(cx) && Finite(cy);
            if (!g.CentroidFinite) return g;
            g.Centroid = new ConnectionPoint2D(cx, cy);
            var x = p.Select(v => v.X - cx).ToArray(); var y = p.Select(v => v.Y - cy).ToArray();
            double scale = Math.Max(x.Max(v => Math.Abs(v)), y.Max(v => Math.Abs(v)));
            g.Scale = scale; g.ScaleFinite = Finite(scale); g.X = x; g.Y = y;
            g.Q = new double[p.Length]; g.R = new double[p.Length];
            if (!g.ScaleFinite || scale == 0) return g;
            var q = g.Q; var r = g.R;
            for (int i = 0; i < p.Length; i++) { x[i] /= scale; y[i] /= scale; }
            int furthest = 0;
            for (int i = 1; i < p.Length; i++) if (Hypot(x[i], y[i]) > Hypot(x[furthest], y[furthest])) furthest = i;
            double radius = Hypot(x[furthest], y[furthest]);
            double ux = x[furthest] / radius, uy = y[furthest] / radius;
            for (int i = 0; i < p.Length; i++) { q[i] = ux * x[i] + uy * y[i]; r[i] = -uy * x[i] + ux * y[i]; }
            double qq = Sum(q.Select(v => v * v)), rr = Sum(r.Select(v => v * v));
            double qr = Sum(q.Select((v, i) => v * r[i]));
            g.Polar = Sum(x.Select((v, i) => v * v + y[i] * y[i]));
            g.Ux = ux; g.Uy = uy; g.Qq = qq;
            if (r.Max(v => Math.Abs(v)) <= CollinearityTolerance * radius) { g.Rank = 1; return g; }
            g.Rank = 2;
            double determinant = qq * rr - qr * qr;
            double largest = (qq + rr + Hypot(qq - rr, 2 * qr)) / 2;
            if (!(determinant / largest / largest > MinimumInertiaRatio))
            {
                g.GeometryRejection = ElasticBoltGroupStatus.UnsupportedGeometry;
                g.GeometryReason = "The planar group is too close to collinear for a reliable full-rank elastic solution (inertia ratio <= 1e-12).";
                return g;
            }
            // Solve in the original XY basis after the stable conditioning assessment.
            // This preserves exact neutral-row zeros for axis-aligned symmetric groups.
            g.Xx = Sum(x.Select(v => v * v)); g.Yy = Sum(y.Select(v => v * v));
            g.Xy = Sum(x.Select((v, i) => v * y[i]));
            g.XyDeterminant = g.Xx * g.Yy - g.Xy * g.Xy;
            if (!(g.XyDeterminant > 0))
            {
                g.GeometryRejection = ElasticBoltGroupStatus.NumericalFailure;
                g.GeometryReason = "The normal-force matrix lost positive definiteness numerically.";
            }
            return g;
        }

        internal static ElasticBoltGroupResult Evaluate(Prepared g, PlanarConnectionActions actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            var p = g.Points;
            var centroid = g.CentroidFinite ? g.Centroid : p[0]; int rank = 0;
            PlanarConnectionActions load = null;
            ElasticBoltGroupResult Reject(ElasticBoltGroupStatus status, string reason, double? minimumTrial = null)
                => new ElasticBoltGroupResult(g.ReadOnlyPoints, actions, centroid, rank, status, new ElasticBoltForce[0], load, null, minimumTrial, reason);

            if (!g.CentroidFinite) return Reject(ElasticBoltGroupStatus.NumericalFailure, "Centroid calculation exceeds the numeric range.");
            double cx = g.Cx, cy = g.Cy;
            double rx = actions.ApplicationX - cx, ry = actions.ApplicationY - cy, rz = actions.ApplicationZ;
            double mx = actions.Mx + ry * actions.Fz - rz * actions.Fy;
            double my = actions.My + rz * actions.Fx - rx * actions.Fz;
            double mz = actions.Mz + rx * actions.Fy - ry * actions.Fx;
            if (!Finite(mx) || !Finite(my) || !Finite(mz)) return Reject(ElasticBoltGroupStatus.NumericalFailure, "Moment transport exceeds the numeric range.");
            load = new PlanarConnectionActions(actions.Fx, actions.Fy, actions.Fz, mx, my, mz, cx, cy, 0);
            var x = g.X; var y = g.Y; double scale = g.Scale;
            if (!g.ScaleFinite) return Reject(ElasticBoltGroupStatus.NumericalFailure, "Bolt spacing exceeds the numeric range.");
            if (scale == 0 && (mx != 0 || my != 0 || mz != 0))
                return Reject(ElasticBoltGroupStatus.UnsupportedGeometry, "A single point bolt cannot transmit a free moment, however small.");

            var q = g.Q; var r = g.R;
            double cq = 0, cr = 0, shearRotation = 0;
            if (scale != 0)
            {
                double ux = g.Ux, uy = g.Uy;
                shearRotation = (mz / scale) / g.Polar;
                // Normal forces produce (Mx, My) = (sum(y*T), -sum(x*T)).
                double targetQ = (-ux * my + uy * mx) / scale;
                if (g.Rank == 1)
                {
                    rank = 1;
                    // A relative cancellation test uses only the terms in the unsupported projection.
                    // A tiny Mx on a horizontal line therefore cannot disappear beside a large supported My.
                    double nullMoment = uy * my + ux * mx;
                    if (!NearZero(nullMoment, Math.Abs(uy * my) + Math.Abs(ux * mx), CollinearityTolerance))
                        return Reject(ElasticBoltGroupStatus.UnsupportedGeometry, "The bending moment has a component about the bolt line which normal bolt forces cannot transmit.");
                    cq = targetQ / g.Qq;
                }
                else
                {
                    rank = 2;
                    if (g.GeometryRejection.HasValue) return Reject(g.GeometryRejection.Value, g.GeometryReason);
                    cq = (g.Yy * (-my / scale) - g.Xy * (mx / scale)) / g.XyDeterminant;
                    cr = (g.Xx * (mx / scale) - g.Xy * (-my / scale)) / g.XyDeterminant;
                    q = x; r = y;
                }
            }
            var forces = new List<ElasticBoltForce>(p.Length);
            for (int i = 0; i < p.Length; i++)
            {
                double fx = actions.Fx / p.Length - shearRotation * y[i];
                double fy = actions.Fy / p.Length + shearRotation * x[i];
                double fz = actions.Fz / p.Length + cq * q[i] + cr * r[i];
                if (!Finite(fx) || !Finite(fy) || !Finite(fz) || !Finite(Hypot(fx, fy)))
                    return Reject(ElasticBoltGroupStatus.NumericalFailure, "The elastic trial forces exceed the numeric range.");
                forces.Add(new ElasticBoltForce(i, p[i], fx, fy, fz));
            }
            double minimum = forces.Min(f => f.Fz);
            if (minimum < 0)
                return Reject(ElasticBoltGroupStatus.RequiresContact, "The elastic trial solution requires compression. A plate/contact model is needed; negative bolt forces are not clipped or returned as a valid distribution.", minimum);

            var terms = new[]
            {
                forces.Select(f => f.Fx).ToArray(), forces.Select(f => f.Fy).ToArray(), forces.Select(f => f.Fz).ToArray(),
                forces.Select((f, i) => y[i] * scale * f.Fz).ToArray(),
                forces.Select((f, i) => -x[i] * scale * f.Fz).ToArray(),
                forces.Select((f, i) => (x[i] * f.Fy - y[i] * f.Fx) * scale).ToArray()
            };
            var demand = new[] { actions.Fx, actions.Fy, actions.Fz, mx, my, mz };
            var residual = new double[6];
            for (int i = 0; i < 6; i++)
            {
                residual[i] = Sum(terms[i]) - demand[i];
                double magnitude = Sum(terms[i].Select(Math.Abs)) + Math.Abs(demand[i]);
                if (!Finite(residual[i]) || !Finite(magnitude) || !NearZero(residual[i], magnitude, EquilibriumRelativeTolerance))
                    return Reject(ElasticBoltGroupStatus.NumericalFailure, "The reconstructed bolt forces do not meet the six equilibrium equations within relative tolerance.", minimum);
            }
            return new ElasticBoltGroupResult(g.ReadOnlyPoints, actions, centroid, rank, ElasticBoltGroupStatus.Balanced, forces, load,
                new PlanarConnectionActions(residual[0], residual[1], residual[2], residual[3], residual[4], residual[5], cx, cy, 0), minimum, null);
        }

        private static bool NearZero(double value, double scale, double tolerance) => value == 0 || (scale > 0 && Math.Abs(value) / scale <= tolerance);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static double Hypot(double a, double b)
        {
            a = Math.Abs(a); b = Math.Abs(b); double high = Math.Max(a, b), low = Math.Min(a, b);
            return high == 0 ? 0 : high * Math.Sqrt(1 + (low / high) * (low / high));
        }
        private static double Sum(IEnumerable<double> values)
        {
            double sum = 0, correction = 0;
            foreach (double value in values)
            {
                double next = sum + value;
                correction += Math.Abs(sum) >= Math.Abs(value) ? (sum - next) + value : (value - next) + sum;
                sum = next;
            }
            return sum + correction;
        }
    }
}
