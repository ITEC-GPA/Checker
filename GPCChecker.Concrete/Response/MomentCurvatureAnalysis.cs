using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Response
{
    /// <summary>
    /// Moment-curvature request at constant axial force: N in N (compression negative), moment direction in degrees in the section axes,
    /// 10-500 steps up to <see cref="EndFraction"/> of the limit moment, quadratic sampling, tolerance on N at the limit point (N) and bisections
    /// of the first yield.
    /// </summary>
    public sealed class MomentCurvatureRequest
    {
        public double AxialForce { get; }
        public double DirectionDegrees { get; }
        public int Steps { get; }
        public double EndFraction { get; }
        public bool QuadraticSampling { get; }
        public double AxialTolerance { get; }
        public int YieldRefinementSteps { get; }
        public MomentCurvatureRequest(double axialForce, double directionDegrees, int steps, double endFraction = 1, bool quadraticSampling = true,
            double axialTolerance = 1000, int yieldRefinementSteps = 12)
        {
            AxialForce = axialForce; DirectionDegrees = directionDegrees; Steps = steps; EndFraction = endFraction; QuadraticSampling = quadraticSampling;
            AxialTolerance = axialTolerance; YieldRefinementSteps = yieldRefinementSteps;
        }
    }

    /// <summary>Strain state of one response: gradient χx, χy (1/mm), strain at the reference point, largest |bar strain|, smallest concrete vertex strain.</summary>
    public sealed class MomentCurvatureStrains
    {
        public double ChiX { get; }
        public double ChiY { get; }
        public double ReferenceStrain { get; }
        public double MaximumBarStrain { get; }
        public double MinimumConcreteStrain { get; }
        public MomentCurvatureStrains(double chiX, double chiY, double referenceStrain, double maximumBarStrain, double minimumConcreteStrain)
        { ChiX = chiX; ChiY = chiY; ReferenceStrain = referenceStrain; MaximumBarStrain = maximumBarStrain; MinimumConcreteStrain = minimumConcreteStrain; }

        /// <summary>Strains of a strain plane on the bars and on the outline vertices of the section.</summary>
        public static MomentCurvatureStrains From(StrainPlane plane, ReinforcedConcreteSection section)
        {
            if (plane == null) throw new ArgumentException("Response: strain plane not available (analysis not converged).");
            var outline = section.ConcreteShape.Fill2d.Points;
            return new MomentCurvatureStrains(plane.ChiX, plane.ChiY, plane.StrainReferencePoint,
                section.Rebars.Select(b => Math.Abs(plane.GetStrain(b.Position))).DefaultIfEmpty(0).Max(), outline.Min(v => plane.GetStrain(v)));
        }
    }

    /// <summary>Limit point at the requested axial force: resistance N, Mx, My (N, Nmm) and its strain state.</summary>
    public sealed class MomentCurvatureLimit
    {
        public double AxialForce { get; }
        public double Mx { get; }
        public double My { get; }
        public MomentCurvatureStrains Strains { get; }
        public MomentCurvatureLimit(double axialForce, double mx, double my, MomentCurvatureStrains strains)
        { AxialForce = axialForce; Mx = mx; My = my; Strains = strains ?? throw new ArgumentNullException(nameof(strains)); }
    }

    /// <summary>Point of the curve: moment (Nmm) and components, curvature |∇ε| (1/mm), strains (absolute, compression of concrete positive).</summary>
    public sealed class MomentCurvaturePoint
    {
        public double Moment { get; internal set; }
        public double Mx { get; internal set; }
        public double My { get; internal set; }
        public double Curvature { get; internal set; }
        public double GradientX { get; internal set; }
        public double GradientY { get; internal set; }
        public double ReferenceStrain { get; internal set; }
        public double ConcreteCompressionStrain { get; internal set; }
        public double SteelStrain { get; internal set; }
        public bool Yielded { get; internal set; }
        public bool Limit { get; internal set; }
    }

    /// <summary>
    /// Numerical response, not a check: increasing-moment branch at constant N up to the limit point, first yield refined by bisection, no
    /// post-peak branch. The curvature is the modulus of the gradient; with an asymmetric section its direction may differ from the moment's.
    /// </summary>
    public sealed class MomentCurvatureResult
    {
        public IReadOnlyList<MomentCurvaturePoint> Points { get; internal set; }
        public double LimitMoment { get; internal set; }
        public double? YieldCurvature { get; internal set; }
        public double? UltimateCurvature { get; internal set; }
        public double LimitAxialForce { get; internal set; }
        public double AxialResidual { get; internal set; }
        /// <summary>Step at which a response failed and the curve stopped; null when complete.</summary>
        public int? InterruptedAtStep { get; internal set; }
        public string Status { get; internal set; }
    }

    /// <summary>
    /// Moment-curvature orchestration transferred from ANTHEA (MomentCurvatureCalculator and ConcreteCurvatureAnalysis, commit fe4652c) with N/Nmm and
    /// 1/mm units. The equilibrium is the native section solver: limit point at constant N on the failure domain, nonlinear stress analyses.
    /// Fixtures: GPCChecker.Test.Concrete/Fixtures/curvature-legacy.csv.
    /// </summary>
    public static class MomentCurvatureAnalysis
    {
        public static MomentCurvatureResult Calculate(MomentCurvatureRequest p, Func<double, double, double, MomentCurvatureLimit> limit,
            Func<double, double, double, MomentCurvatureStrains> response, double steelYieldStrain, CancellationToken token = default(CancellationToken))
        {
            if (p == null || limit == null || response == null) throw new ArgumentNullException();
            if (double.IsNaN(p.AxialForce + p.DirectionDegrees + p.EndFraction + p.AxialTolerance + steelYieldStrain)
                || double.IsInfinity(p.AxialForce + p.DirectionDegrees + p.EndFraction + p.AxialTolerance + steelYieldStrain)
                || p.Steps < 10 || p.Steps > 500 || p.EndFraction <= 0 || p.EndFraction > 1 || steelYieldStrain <= 0 || p.AxialTolerance <= 0
                || p.YieldRefinementSteps < 0 || p.YieldRefinementSteps > 30)
                throw new ArgumentException("Moment-curvature: 10-500 steps, end fraction in (0; 1], finite N and direction.");
            double angle = p.DirectionDegrees * Math.PI / 180, c = Math.Cos(angle), s = Math.Sin(angle);
            token.ThrowIfCancellationRequested();
            var rd = limit(p.AxialForce, c, s) ?? throw new ArgumentException("Limit point not available at the assigned N.");
            if (Math.Abs(rd.AxialForce - p.AxialForce) > p.AxialTolerance)
                throw new ArgumentException($"Residual N at the limit point beyond the tolerance {p.AxialTolerance:G6} N: N = {p.AxialForce:G9} N, limit N = {rd.AxialForce:G9} N.");
            double mr = rd.Mx * c + rd.My * s;
            if (mr <= 0 || double.IsNaN(mr) || double.IsInfinity(mr)) throw new ArgumentException("Non-positive limit moment in the requested direction.");
            var points = new List<MomentCurvaturePoint>(); double? yield = null, ultimate = null; int? interrupted = null;
            MomentCurvaturePoint Point(MomentCurvatureStrains state, double moment, bool isLimit)
            {
                double curvature = Math.Sqrt(state.ChiX * state.ChiX + state.ChiY * state.ChiY), strain = state.MaximumBarStrain;
                if (double.IsNaN(curvature + strain) || double.IsInfinity(curvature + strain)) throw new ArgumentException("Response not finite.");
                return new MomentCurvaturePoint { Moment = moment, Mx = moment * c, My = moment * s, Curvature = curvature, GradientX = state.ChiX, GradientY = state.ChiY,
                    ReferenceStrain = state.ReferenceStrain, ConcreteCompressionStrain = Math.Max(0, -state.MinimumConcreteStrain), SteelStrain = strain,
                    Yielded = strain >= steelYieldStrain, Limit = isLimit };
            }
            string status = "Increasing-moment branch at constant N; material laws and factors of the section and standard.";
            for (int i = 0; i <= p.Steps; i++)
            {
                token.ThrowIfCancellationRequested();
                double f = (double)i / p.Steps; if (p.QuadraticSampling) f *= f; f *= p.EndFraction;
                bool isLimit = i == p.Steps && p.EndFraction == 1; double m = mr * f;
                try
                {
                    var point = Point(isLimit ? rd.Strains : response(p.AxialForce, m * c, m * s), m, isLimit);
                    if (point.Yielded && yield == null) yield = point.Curvature;
                    if (isLimit) ultimate = point.Curvature;
                    points.Add(point);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException)) { status += $" Interrupted at step {i}: {ex.Message}"; interrupted = i; break; }
            }
            int firstYield = points.FindIndex(v => v.Yielded);
            if (firstYield > 0 && p.YieldRefinementSteps > 0)
            {
                var high = points[firstYield]; double low = points[firstYield - 1].Moment;
                try
                {
                    for (int i = 0; i < p.YieldRefinementSteps; i++)
                    {
                        token.ThrowIfCancellationRequested(); double m = (low + high.Moment) / 2;
                        var candidate = Point(response(p.AxialForce, m * c, m * s), m, false);
                        if (candidate.Yielded) high = candidate; else low = m;
                    }
                    yield = high.Curvature; if (high.Moment < points[firstYield].Moment) points.Insert(firstYield, high);
                    status += $" First yield refined with {p.YieldRefinementSteps} bisections, My = {high.Moment:G7} Nmm.";
                }
                catch (Exception ex) when (!(ex is OperationCanceledException)) { status += " Yield taken at the first sample: refinement interrupted, " + ex.Message; }
            }
            return new MomentCurvatureResult { Points = points.AsReadOnly(), LimitMoment = mr, YieldCurvature = yield, UltimateCurvature = ultimate,
                LimitAxialForce = rd.AxialForce, AxialResidual = rd.AxialForce - p.AxialForce, InterruptedAtStep = interrupted,
                Status = status + $" N at the limit point = {rd.AxialForce:G9} N; residual = {rd.AxialForce - p.AxialForce:G6} N. No post-peak branch." };
        }

        /// <summary>
        /// Curve on a native checker with nonlinear stress analysis: the limit point at constant N comes from its failure domain, the responses from
        /// its stress analyses. Axes: the coordinate system of the forces passed to the checker; steel yield strain fyd/Es.
        /// </summary>
        public static MomentCurvatureResult Calculate(MomentCurvatureRequest p, SectionCheckerModelCode2010 checker, ReinforcedConcreteSection section,
            CoordinateSystem axes, double steelYieldStrain, CancellationToken token = default(CancellationToken))
        {
            if (checker == null || section == null || axes == null) throw new ArgumentNullException();
            if (checker.SectionCheckerOptions.StressAnalysisType != SectionSolver.StressAnalysisTypes.NonLinear)
                throw new ArgumentException("Moment-curvature: the checker must use the nonlinear stress analysis.");
            var domain = checker.GetFailureDomainResult() ?? throw new ArgumentException("Failure domain not available.");
            domain.FailureAnalysisType = SectionSolver.FailureAnalysisTypes.ConstantN;
            MomentCurvatureLimit Limit(double n, double cx, double sy)
            {
                // Direction of the moment as in ANTHEA: unit components in kNm.
                var point = domain.CalculateForce(new ResultBeamForces(n, 0, 0, 0, cx * 1e6, sy * 1e6, axes));
                if (point?.StrainPlane == null) return null;
                return new MomentCurvatureLimit(point.NRd, point.MxRd, point.MyRd, MomentCurvatureStrains.From(point.StrainPlane, section));
            }
            MomentCurvatureStrains Response(double n, double mx, double my)
            {
                var stress = checker.GetTensionAnalysisResult(new ResultBeamForces(n, 0, 0, 0, mx, my, axes));
                if (stress?.StrainPlane == null || section.ConcreteShape.GetPoints2d().Any(v => double.IsNaN(stress.StrainPlane.GetStrain(v)) || double.IsInfinity(stress.StrainPlane.GetStrain(v))))
                    throw new ArgumentException("Stress analysis not converged.");
                return MomentCurvatureStrains.From(stress.StrainPlane, section);
            }
            return Calculate(p, Limit, Response, steelYieldStrain, token);
        }
    }
}
