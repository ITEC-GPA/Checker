using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checker.Helper;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;

namespace GPC.Checker.SectionSolvers
{
    /// <summary>
    /// Safeguarded Newton search on the ultimate strain surface for continuous diagrams and geometrically integrated stress blocks.
    /// Two residuals are the orthogonal projection onto the complement of the demand line, in section-scaled force coordinates.
    /// Only the actual, updated resultant can satisfy convergence. All state and caches belong to one call, including parallel searches.
    /// </summary>
    internal sealed class DomainPointStrategyNewton : IDomainPointStrategy
    {
        private readonly SectionSolver _solver;
        public SectionSolver Solver => _solver;
        public DomainPointStrategyNewton(SectionSolver solver) { _solver = solver; }

        private struct V
        {
            public double N, X, Y;
            public V(double n, double x, double y) { N = n; X = x; Y = y; }
            public double Norm => Math.Sqrt(Dot(this, this));
            public bool Finite => IsFinite(N) && IsFinite(X) && IsFinite(Y);
            public static V operator +(V a, V b) => new V(a.N + b.N, a.X + b.X, a.Y + b.Y);
            public static V operator -(V a, V b) => new V(a.N - b.N, a.X - b.X, a.Y - b.Y);
            public static V operator *(V a, double b) => new V(a.N * b, a.X * b, a.Y * b);
            public static double Dot(V a, V b) => a.N * b.N + a.X * b.X + a.Y * b.Y;
            public static V Cross(V a, V b) => new V(a.X * b.Y - a.Y * b.X, a.Y * b.N - a.N * b.Y, a.N * b.X - a.X * b.N);
        }
        private sealed class Meridian
        {
            public double Theta, MinDistance, ElasticTension;
            public List<DeformationFieldsPoint> Tension, TensionF1, Compression;
        }
        private sealed class Point
        {
            public Meridian Meridian;
            public double S, Eta, R1, R2;
            public double Merit => R1 * R1 + R2 * R2;
            public ForceTuple Forces;
            public V Scaled;
            public StrainPlane Plane;
            public SectionSolver.FailureZones Zone;
        }
        private static bool IsFinite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);

        public FailureDomain.FailureDomainPoint CalculateDomainPoint(ResultBeamForces force, SectionSolver.FailureAnalysisTypes? failureAnalysisTypeOverride = null)
        {
            var type = failureAnalysisTypeOverride ?? _solver.SectionOption.FailureAnalysisType;
            var target = new ForceTuple(force.ToCoordinateSystemWithEccentricity(_solver.SolverAxes));
            var domain = _solver.SectionOption.FailureDomainType;
            var discretizations = _solver.GetFailureZonesDiscretization(domain);
            if (discretizations == null) return null;
            var zones = discretizations.Select(z => z.Item1).ToArray();
            var association = _solver.ConcreteSection.GetRebarIsInsideAssociation();
            // Conversion is linear and the geometry does not change during a search.
            var scale = _solver.ConvertToAdimensionalForces(new ForceTuple(1, 1, 1));
            V Scale(ForceTuple f) => new V(f.N * scale.N, f.Mx * scale.Mx, f.My * scale.My);
            V demand = Scale(target), origin = new V(), direction;
            if (!demand.Finite) return null;
            switch (type)
            {
                case SectionSolver.FailureAnalysisTypes.ConstantEccentricity: direction = demand; break;
                case SectionSolver.FailureAnalysisTypes.ConstantN:
                    origin.N = demand.N; direction = new V(0, demand.X, demand.Y); break;
                case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                    origin = new V(demand.N, demand.X, 0); direction = new V(0, 0, demand.Y); break;
                case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                    origin = new V(demand.N, 0, demand.Y); direction = new V(0, demand.X, 0); break;
                case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                    origin = new V(0, demand.X, demand.Y); direction = new V(demand.N, 0, 0); break;
                default: return null;
            }
            if (!(direction.Norm > 0)) return null;
            direction *= 1 / direction.Norm;
            // Cross with the least aligned coordinate axis to avoid a small projection basis.
            V axis = Math.Abs(direction.N) <= Math.Min(Math.Abs(direction.X), Math.Abs(direction.Y)) ? new V(1, 0, 0)
                : Math.Abs(direction.X) <= Math.Abs(direction.Y) ? new V(0, 1, 0) : new V(0, 0, 1);
            V u = V.Cross(direction, axis); u *= 1 / u.Norm;
            V v = V.Cross(direction, u);
            // Leave a numerical margin within the public acceptance limits, without changing those limits.
            double distanceTolerance = 0.1 * _solver.FailureAnalysisDistanceTolerance;
            double angularTolerance = 0.1 * _solver.FailureAnalysisAngularTolerance;
            double nominalAxialTolerance = DomainPointAxialTolerance.Calculate(_solver, target.N) * scale.N;

            Meridian GetMeridian(double theta)
            {
                var m = new Meridian { Theta = theta };
                _solver.CalculateRotationPointsPerMaterial(theta, domain, out m.Tension, out m.TensionF1, out m.Compression,
                    out m.MinDistance, out m.ElasticTension);
                return m;
            }
            Point Evaluate(Meridian m, double s, int id = -1)
            {
                s = Math.Max(0, Math.Min(zones.Length, s));
                int position = Math.Min(zones.Length - 1, (int)Math.Floor(s));
                var zone = zones[position];
                double eta = s - position;
                var plane = _solver.CalculateStrainPlaneMultiPoints(m.Theta, zone, eta,
                    zone == SectionSolver.FailureZones.F1 ? m.TensionF1 : m.Tension, m.Compression, m.MinDistance, m.ElasticTension, id);
                if (plane == null) return null;
                var f = _solver.GetExternalForces(_solver.CalculateForceResultantForDomain(plane, association),
                    _solver.SectionOption.ForceReferenceCoordinateSystem);
                V a = Scale(f);
                if (!a.Finite) return null;
                V difference = a - origin;
                return new Point { Meridian = m, S = s, Eta = eta, Zone = zone, Plane = plane, Forces = f, Scaled = a,
                    R1 = V.Dot(u, difference), R2 = V.Dot(v, difference) };
            }
            bool Converged(Point p, bool strict = true)
            {
                if (p == null || V.Dot(p.Scaled - origin, direction) <= 0) return false;
                V difference = p.Scaled - demand;
                double nTolerance = strict ? distanceTolerance : nominalAxialTolerance;
                double mTolerance = strict ? distanceTolerance : _solver.FailureAnalysisDistanceTolerance;
                double angleTolerance = strict ? angularTolerance : _solver.FailureAnalysisAngularTolerance;
                switch (type)
                {
                    case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                        return Angle(new V(target.N / 1000, target.Mx / 1e6, target.My / 1e6),
                            new V(p.Forces.N / 1000, p.Forces.Mx / 1e6, p.Forces.My / 1e6)) <= angleTolerance;
                    case SectionSolver.FailureAnalysisTypes.ConstantN:
                        return Math.Abs(difference.N) <= nTolerance && Angle(new V(0, target.Mx, target.My),
                            new V(0, p.Forces.Mx, p.Forces.My)) <= angleTolerance;
                    case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                        return Math.Abs(difference.N) <= nTolerance && Math.Abs(difference.X) <= mTolerance;
                    case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                        return Math.Abs(difference.N) <= nTolerance && Math.Abs(difference.Y) <= mTolerance;
                    case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                        return Math.Abs(difference.X) <= mTolerance && Math.Abs(difference.Y) <= mTolerance;
                    default: return false;
                }
            }
            FailureDomain.FailureDomainPoint Result(Point p) => new FailureDomain.FailureDomainPoint(p.Forces, p.Zone, p.Plane, p.Eta);
            double Coordinate(SectionSolver.FailureZones zone, double eta)
            {
                int position = Array.IndexOf(zones, zone);
                if (position >= 0) return position + eta;
                position = Array.FindIndex(zones, z => z > zone);
                return position < 0 ? zones.Length - 0.5 : position;
            }

            double theta0 = Math.Atan2(target.My, target.Mx);
            double thetaScaled = Math.Atan2(demand.Y * scale.My, demand.X * scale.Mx);
            double compression = -demand.N;
            double initial = compression <= 0 ? Coordinate(SectionSolver.FailureZones.F3A, 0.4)
                : compression < 0.2 ? Coordinate(SectionSolver.FailureZones.F3A, 0.7)
                : compression < 0.4 ? Coordinate(SectionSolver.FailureZones.F3B, 0.5)
                : Coordinate(SectionSolver.FailureZones.F4, 0.5);
            // A proportional load defines a ray: its magnitude must not select the initial failure field.
            // Section-scaled M/N measures eccentricity relative to the corresponding section dimensions.
            if (type == SectionSolver.FailureAnalysisTypes.ConstantEccentricity)
            {
                double eccentricity = demand.N == 0 ? double.PositiveInfinity
                    : Math.Sqrt(demand.X * demand.X + demand.Y * demand.Y) / Math.Abs(demand.N);
                initial = demand.N < 0
                    ? eccentricity < 0.15 ? Coordinate(SectionSolver.FailureZones.F4, 0.5)
                        : eccentricity < 0.5 ? Coordinate(SectionSolver.FailureZones.F3B, 0.8)
                        : Coordinate(SectionSolver.FailureZones.F3A, 0.7)
                    : Coordinate(SectionSolver.FailureZones.F3A, demand.N == 0 ? 0.6 : 0.4);
            }
            // With both moments fixed, N is the search direction, not an assigned axial level.
            // Start on its compression/tension branch instead of classifying the magnitude of the demanded N.
            if (type == SectionSolver.FailureAnalysisTypes.ConstantMxMy)
                initial = target.N < 0 ? Coordinate(SectionSolver.FailureZones.F4, 0.9)
                    : Coordinate(SectionSolver.FailureZones.F3A, 0.1);
            // At the poles theta has no physical meaning and its Jacobian column vanishes.
            if (type == SectionSolver.FailureAnalysisTypes.ConstantEccentricity || type == SectionSolver.FailureAnalysisTypes.ConstantMxMy)
            {
                if (target.Mx == 0 && target.My == 0)
                {
                    var pole = Evaluate(GetMeridian(theta0), target.N > 0 ? 0 : zones.Length, 0);
                    if (Converged(pole)) return Result(pole);
                }
            }

            // Deterministic, independent starts. No warm state is shared by concurrent calls.
            var starts = new[] { (thetaScaled, initial), (theta0, initial), (theta0, Coordinate(SectionSolver.FailureZones.F3A, 0.5)),
                (theta0 + Math.PI, initial), (theta0, Coordinate(SectionSolver.FailureZones.F2A, 0.5)) };
            int totalIterations = 0;
            foreach (var start in starts.Distinct())
            {
                Point current = Evaluate(GetMeridian(start.Item1), start.Item2, totalIterations);
                Point nominal = null;
                void RetainNominal(Point p)
                {
                    if (Converged(p, false) && (nominal == null || p.Merit < nominal.Merit)) nominal = p;
                }
                int stagnant = 0;
                for (int iteration = 0; iteration < 40 && current != null; iteration++)
                {
                    RetainNominal(current);
                    if (Converged(current)) return Result(current);
                    // Domain search iteration.
                    totalIterations++;
                    // Central differences use the actual abscissae after clipping to the admissible interval.
                    // A meridian is reused for both immersion perturbations and for the accepted point.
                    // Near a root shrink the stencil to stay local at changes of the active material/rotation point.
                    double ht = Math.Max(1e-7, Math.Min(1e-4, 0.1 * Math.Sqrt(current.Merit))), hs = ht;
                    Point tp = Evaluate(GetMeridian(current.Meridian.Theta + ht), current.S);
                    Point tm = Evaluate(GetMeridian(current.Meridian.Theta - ht), current.S);
                    double lo = Math.Max(0, current.S - hs), hi = Math.Min(zones.Length, current.S + hs);
                    Point sp = Evaluate(current.Meridian, hi), sm = Evaluate(current.Meridian, lo);
                    if (tp == null || tm == null || sp == null || sm == null) break;
                    double a = (tp.R1 - tm.R1) / (2 * ht), c = (tp.R2 - tm.R2) / (2 * ht);
                    double b = (sp.R1 - sm.R1) / (hi - lo), d = (sp.R2 - sm.R2) / (hi - lo);
                    if (!Solve(a, b, c, d, -current.R1, -current.R2, out double dt, out double ds)) break;
                    // Scale the whole step, preserving the Newton descent direction.
                    double limit = Math.Max(1, Math.Max(Math.Abs(dt) / (Math.PI / 8), Math.Abs(ds) / 0.5));
                    dt /= limit; ds /= limit;
                    Point trial = null;
                    double alpha = 1;
                    for (int backtrack = 0; backtrack < 10; backtrack++, alpha *= 0.5)
                    {
                        double s = Math.Max(0, Math.Min(zones.Length, current.S + alpha * ds));
                        Meridian m = dt == 0 ? current.Meridian : GetMeridian(current.Meridian.Theta + alpha * dt);
                        var candidate = Evaluate(m, s, totalIterations);
                        RetainNominal(candidate);
                        if (candidate != null && (Converged(candidate) || candidate.Merit < current.Merit * (1 - 1e-4 * alpha)))
                        { trial = candidate; break; }
                    }
                    if (trial == null) break;
                    stagnant = trial.Merit > current.Merit * 0.999 ? stagnant + 1 : 0;
                    current = trial;
                    if (Converged(current)) return Result(current);
                    if (stagnant >= 4) break;
                }
                // The tighter target is a refinement objective, not an additional public acceptance condition.
                // At a surface kink retain a real evaluated plane satisfying every unchanged nominal tolerance.
                if (nominal != null) return Result(nominal);
            }
            _solver.AddLog("Safeguarded iterative strategy did not converge: fallback required");
            return null;
        }

        private static double Angle(V a, V b)
        {
            double na = a.Norm, nb = b.Norm;
            if (!(na > 0) || !(nb > 0)) return Math.PI;
            a *= 1 / na; b *= 1 / nb;
            return Math.Atan2(V.Cross(a, b).Norm, V.Dot(a, b));
        }

        /// <summary>Column-scaled 2x2 solve. The normalized determinant measures the angle between columns, not their units.</summary>
        private static bool Solve(double a, double b, double c, double d, double r1, double r2, out double x, out double y)
        {
            x = y = 0;
            double first = Math.Sqrt(a * a + c * c), second = Math.Sqrt(b * b + d * d);
            if (!(first > 1e-14) || !(second > 1e-14)) return false;
            a /= first; c /= first; b /= second; d /= second;
            double determinant = a * d - b * c;
            if (Math.Abs(determinant) < 1e-10) return false;
            x = (r1 * d - b * r2) / determinant / first;
            y = (a * r2 - r1 * c) / determinant / second;
            return IsFinite(x) && IsFinite(y);
        }
    }
}
