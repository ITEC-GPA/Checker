using GPC.Checker.Helper;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;
using System;
using System.Collections.Generic;

namespace GPC.Checker.SectionSolvers
{
    /// <summary>
    /// Robust search of the point of the failure domain by nested one-dimensional root finding on the same ultimate strain planes of the
    /// iterative strategy (angle teta of the neutral axis, failure zone and immersion).<br/>
    /// For a given teta the planes run from the uniform tension to the uniform compression with the parameter s in [0, 6] (zones F1, F2A, F2B,
    /// F3A, F3B, F4): the axial force (or the elevation of the force, for the constant eccentricity) is found on s by sampling and regula falsi
    /// (Illinois); teta is then found with the same method on the direction of the moments (or on the constant moment).<br/>
    /// It is used when the iterative strategy does not converge: it is slower, but the returned forces are those of the returned strain plane,
    /// without the linear interpolation between the points of a discretized domain of the intersection strategy (which lies inside the domain).
    /// The constant Mx-My analysis is not handled (null)
    /// </summary>
    internal class DomainPointStrategyBisection : IDomainPointStrategy
    {
        #region Constants

        private static readonly SectionSolver.FailureZones[] Zones = new[]
        {
            SectionSolver.FailureZones.F1, SectionSolver.FailureZones.F2A, SectionSolver.FailureZones.F2B,
            SectionSolver.FailureZones.F3A, SectionSolver.FailureZones.F3B, SectionSolver.FailureZones.F4
        };

        /// <summary>Samples of s in each zone to bracket the root on a meridian</summary>
        private const int SamplesPerZone = 4;

        /// <summary>Samples of teta on the whole turn to bracket the root on the angle</summary>
        private const int TetaSamples = 36;

        /// <summary>Iterations of the regula falsi</summary>
        private const int MaxIterations = 100;

        #endregion

        #region Nested types

        /// <summary>The rotation points of the ultimate strain planes for an angle teta</summary>
        private sealed class Meridian
        {
            public double Teta;
            public List<DeformationFieldsPoint> Tension, TensionF1, Compression;
            public double MinDistanceCompression, ElasticEpsilonTension;
        }

        /// <summary>A point of the failure surface with its strain plane</summary>
        private sealed class SurfacePoint
        {
            public ForceTuple Forces;
            public StrainPlane Plane;
            public SectionSolver.FailureZones Zone;
            public double Eta;
        }

        #endregion

        private readonly SectionSolver _solver;

        public SectionSolver Solver => _solver;

        public DomainPointStrategyBisection(in SectionSolver solver)
        {
            _solver = solver;
        }

        public FailureDomain.FailureDomainPoint CalculateDomainPoint(ResultBeamForces force, SectionSolver.FailureAnalysisTypes? failureAnalysisTypeOverride = null)
        {
            var target = new ForceTuple(force.ToCoordinateSystemWithEccentricity(_solver.SolverAxes));
            var failureAnalysisType = failureAnalysisTypeOverride ?? _solver.SectionOption.FailureAnalysisType;
            var rebarIsInsideAssociation = _solver.ConcreteSection.GetRebarIsInsideAssociation();
            double distanceTolerance = _solver.FailureAnalysisDistanceTolerance;
            double angularTolerance = _solver.FailureAnalysisAngularTolerance;

            SurfacePoint Point(Meridian meridian, double s)
            {
                int k = Math.Max(0, Math.Min(Zones.Length - 1, (int)Math.Floor(s)));
                double eta = Math.Max(0, Math.Min(1, s - k));
                var zone = Zones[k];
                StrainPlane plane = _solver.CalculateStrainPlaneMultiPoints(meridian.Teta, zone, eta,
                    zone == SectionSolver.FailureZones.F1 ? meridian.TensionF1 : meridian.Tension, meridian.Compression,
                    meridian.MinDistanceCompression, meridian.ElasticEpsilonTension);
                if (plane == null)
                    return null;
                ForceTuple forces = _solver.GetExternalForces(_solver.CalculateForceResultantForDomain(plane, rebarIsInsideAssociation),
                    _solver.SectionOption.ForceReferenceCoordinateSystem);
                return new SurfacePoint { Forces = forces, Plane = plane, Zone = zone, Eta = eta };
            }

            Meridian GetMeridian(double teta)
            {
                _solver.CalculateRotationPointsPerMaterial(teta, _solver.SectionOption.FailureDomainType, out List<DeformationFieldsPoint> tension,
                    out List<DeformationFieldsPoint> tensionF1, out List<DeformationFieldsPoint> compression, out double minDistanceCompression,
                    out double elasticEpsilonTension);
                return new Meridian
                {
                    Teta = teta, Tension = tension, TensionF1 = tensionF1, Compression = compression,
                    MinDistanceCompression = minDistanceCompression, ElasticEpsilonTension = elasticEpsilonTension
                };
            }

            // the root of g on the meridian of teta, from the uniform tension to the uniform compression
            SurfacePoint OnMeridian(double teta, Func<SurfacePoint, double> g, double tolerance, double acceptance)
            {
                Meridian meridian = GetMeridian(teta);
                return Root(s => Point(meridian, s), g, 0, Zones.Length, Zones.Length * SamplesPerZone, tolerance, acceptance, false);
            }

            double Adimensional(double n, double mx, double my, int component)
            {
                ForceTuple a = _solver.ConvertToAdimensionalForces(new ForceTuple(n, mx, my));
                return component == 0 ? a.N : component == 1 ? a.Mx : a.My;
            }

            // angle between the moments of the point and the target moments, in (-pi, pi]
            double MomentDirection(SurfacePoint p) => Wrap(Math.Atan2(p.Forces.My, p.Forces.Mx) - Math.Atan2(target.My, target.Mx));

            Func<SurfacePoint, double> onMeridian;
            Func<SurfacePoint, double> onTeta;
            Func<SurfacePoint, bool> admissible = p => true;
            // tolerances of the roots; acceptances on a jump of the forces: those of the closest point of the iterative strategy
            double meridianTolerance, tetaTolerance, meridianAcceptance = 10 * distanceTolerance, tetaAcceptance = 20 * angularTolerance;
            bool angular = true;

            switch (failureAnalysisType)
            {
                case SectionSolver.FailureAnalysisTypes.ConstantN:
                    if (target.Mx == 0 && target.My == 0)
                        return null;
                    onMeridian = p => Adimensional(p.Forces.N - target.N, 0, 0, 0);
                    meridianTolerance = 0.1 * distanceTolerance;
                    onTeta = MomentDirection;
                    tetaTolerance = 0.5 * angularTolerance;
                    break;

                case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                    {
                        ForceTuple a = _solver.ConvertToAdimensionalForces(target);
                        double moment = Math.Sqrt(a.Mx * a.Mx + a.My * a.My);
                        if (moment < 1e-12)
                            return null;
                        double elevation = Math.Atan2(a.N, moment);
                        onMeridian = p =>
                        {
                            ForceTuple pa = _solver.ConvertToAdimensionalForces(p.Forces);
                            return Math.Atan2(pa.N, Math.Sqrt(pa.Mx * pa.Mx + pa.My * pa.My)) - elevation;
                        };
                        meridianTolerance = 0.1 * angularTolerance;
                        meridianAcceptance = 20 * angularTolerance;
                        onTeta = MomentDirection;
                        tetaTolerance = 0.5 * angularTolerance;
                    }
                    break;

                case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                    if (target.My == 0)
                        return null;
                    onMeridian = p => Adimensional(p.Forces.N - target.N, 0, 0, 0);
                    meridianTolerance = 0.1 * distanceTolerance;
                    onTeta = p => Adimensional(0, p.Forces.Mx - target.Mx, 0, 1);
                    tetaTolerance = 0.5 * distanceTolerance;
                    tetaAcceptance = 10 * distanceTolerance;
                    admissible = p => Math.Sign(p.Forces.My) == Math.Sign(target.My);
                    angular = false;
                    break;

                case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                    if (target.Mx == 0)
                        return null;
                    onMeridian = p => Adimensional(p.Forces.N - target.N, 0, 0, 0);
                    meridianTolerance = 0.1 * distanceTolerance;
                    onTeta = p => Adimensional(0, 0, p.Forces.My - target.My, 2);
                    tetaTolerance = 0.5 * distanceTolerance;
                    tetaAcceptance = 10 * distanceTolerance;
                    admissible = p => Math.Sign(p.Forces.Mx) == Math.Sign(target.Mx);
                    angular = false;
                    break;

                default:
                    return null;
            }

            // first guess of the iterative strategy: the direction of the moments
            double teta0 = Math.Atan2(target.My, target.Mx);
            SurfacePoint result = Root(teta => OnMeridian(teta, onMeridian, meridianTolerance, meridianAcceptance),
                p => admissible(p) ? onTeta(p) : double.NaN, teta0, teta0 + 2 * Math.PI, TetaSamples, tetaTolerance, tetaAcceptance, angular);
            if (result == null)
                return null;

            return new FailureDomain.FailureDomainPoint(result.Forces, result.Zone, result.Plane, result.Eta);
        }

        /// <summary>
        /// A root of g(point(x)) on [from, to]: the interval is sampled to bracket a change of sign, then refined with the regula falsi
        /// (Illinois). Points that do not exist (null) or with g not a number are skipped
        /// </summary>
        /// <param name="point">The point for the parameter x</param>
        /// <param name="g">The function whose root is searched</param>
        /// <param name="from">The start of the interval</param>
        /// <param name="to">The end of the interval</param>
        /// <param name="samples">The number of sub-intervals of the sampling</param>
        /// <param name="tolerance">The tolerance on |g|</param>
        /// <param name="acceptance">The largest |g| accepted when the bracket collapses on a jump of g: the integration on the Gauss points of a
        /// stress-strain diagram with steps (stress block) gives forces that change by steps</param>
        /// <param name="angular">True if g is an angle in (-pi, pi]: a change of sign across ±pi is not a root</param>
        /// <returns>The point, or null</returns>
        private static SurfacePoint Root(Func<double, SurfacePoint> point, Func<SurfacePoint, double> g, double from, double to, int samples,
            double tolerance, double acceptance, bool angular)
        {
            double previousX = double.NaN, previousG = double.NaN;
            SurfacePoint previous = null;
            for (int i = 0; i <= samples; i++)
            {
                double x = from + (to - from) * i / samples;
                SurfacePoint p = point(x);
                double value = p == null ? double.NaN : g(p);
                if (double.IsNaN(value))
                {
                    previous = null;
                    continue;
                }
                if (Math.Abs(value) <= tolerance)
                    return p;
                if (previous != null && Math.Sign(value) != Math.Sign(previousG) && (!angular || Math.Abs(value) + Math.Abs(previousG) < Math.PI))
                {
                    SurfacePoint refined = Refine(point, g, previousX, previous, previousG, x, p, value, tolerance, acceptance);
                    if (refined != null)
                        return refined;
                }
                previous = p;
                previousX = x;
                previousG = value;
            }
            return null;
        }

        /// <summary>
        /// Regula falsi with the Illinois modification on a bracket [a, b] with g(a) g(b) &lt; 0. When the bracket collapses (g with a jump)
        /// on a jump of g within the acceptance, the forces are interpolated across the jump
        /// </summary>
        /// <returns>The point, or null (points that do not exist, jump of g larger than the acceptance)</returns>
        private static SurfacePoint Refine(Func<double, SurfacePoint> point, Func<SurfacePoint, double> g, double a, SurfacePoint pa, double ga,
            double b, SurfacePoint pb, double gb, double tolerance, double acceptance)
        {
            // the true values at the ends (the Illinois step halves the stored ones)
            double trueGa = ga, trueGb = gb;
            int side = 0;
            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                if (Math.Abs(b - a) < 1e-12 * Math.Max(1, Math.Abs(a)))
                    break;
                double x = (a * gb - b * ga) / (gb - ga);
                if (!(x > Math.Min(a, b) && x < Math.Max(a, b)))
                    x = 0.5 * (a + b);
                SurfacePoint p = point(x);
                double value = p == null ? double.NaN : g(p);
                if (double.IsNaN(value))
                    return null;
                if (Math.Abs(value) <= tolerance)
                    return p;
                if (Math.Sign(value) == Math.Sign(gb))
                {
                    b = x;
                    pb = p;
                    gb = trueGb = value;
                    if (side == -1)
                        ga *= 0.5;
                    side = -1;
                }
                else
                {
                    a = x;
                    pa = p;
                    ga = trueGa = value;
                    if (side == 1)
                        gb *= 0.5;
                    side = 1;
                }
            }
            if (Math.Min(Math.Abs(trueGa), Math.Abs(trueGb)) > acceptance)
                return null;
            // the step of the integral on the Gauss points is crossed by a linear interpolation of the forces between its two sides (the exact
            // integral of the diagram at the first order); the strain plane is the one of the nearer side
            double t = trueGa / (trueGa - trueGb);
            SurfacePoint nearer = t <= 0.5 ? pa : pb;
            return new SurfacePoint { Forces = pa.Forces + (pb.Forces - pa.Forces) * t, Plane = nearer.Plane, Zone = nearer.Zone, Eta = nearer.Eta };
        }
        /// <summary>The angle in (-pi, pi]</summary>
        private static double Wrap(double angle)
        {
            while (angle > Math.PI)
                angle -= 2 * Math.PI;
            while (angle <= -Math.PI)
                angle += 2 * Math.PI;
            return angle;
        }
    }
}
