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

    /// <summary>
    /// Limit point at the requested axial force: resistance N, Mx, My (N, Nmm, or the units of the caller with the overload of
    /// <see cref="MomentCurvatureAnalysis.Calculate(MomentCurvatureRequest, Func{double, double, double, MomentCurvatureLimit}, Func{double, double, double, MomentCurvatureStrains}, double, MomentCurvatureUnits, CancellationToken)"/>
    /// with units) and its strain state.
    /// </summary>
    public sealed class MomentCurvatureLimit
    {
        private readonly Func<MomentCurvatureStrains> strainsFunction;
        private MomentCurvatureStrains strains;
        public double AxialForce { get; }
        public double Mx { get; }
        public double My { get; }
        /// <summary>
        /// Strain state of the limit point. With the lazy constructor it is evaluated at the first access and kept in the instance: the analysis reads it
        /// only at the limit step (end fraction 1), inside the step, so an error there interrupts the curve at that step; with an end fraction below 1 it is
        /// never evaluated. A null state from the function is an <see cref="ArgumentException"/>.
        /// </summary>
        public MomentCurvatureStrains Strains
        {
            get
            {
                if (strains == null) strains = strainsFunction() ?? throw new ArgumentException("Response: strain state of the limit point not available.");
                return strains;
            }
        }
        public MomentCurvatureLimit(double axialForce, double mx, double my, MomentCurvatureStrains strains)
        { AxialForce = axialForce; Mx = mx; My = my; this.strains = strains ?? throw new ArgumentNullException(nameof(strains)); }
        /// <summary>
        /// Limit point with the strain state evaluated lazily (0.0.18.0), as ANTHEA reads the limit state only at the limit step. The instance is not meant
        /// to be shared between threads. A literal null as last argument is ambiguous between the two constructors (source only).
        /// </summary>
        public MomentCurvatureLimit(double axialForce, double mx, double my, Func<MomentCurvatureStrains> strains)
        { AxialForce = axialForce; Mx = mx; My = my; strainsFunction = strains ?? throw new ArgumentNullException(nameof(strains)); }
    }

    /// <summary>Unit labels of forces and moments in the texts of the curve (Status and messages); the values are always those of the caller.</summary>
    public sealed class MomentCurvatureUnits
    {
        /// <summary>N and Nmm: the labels of 0.0.17.0, used by the overloads without units.</summary>
        public static readonly MomentCurvatureUnits NewtonMillimetre = new MomentCurvatureUnits("N", "Nmm");
        public string Force { get; }
        public string Moment { get; }
        public MomentCurvatureUnits(string force, string moment)
        { Force = force ?? throw new ArgumentNullException(nameof(force)); Moment = moment ?? throw new ArgumentNullException(nameof(moment)); }
    }

    /// <summary>Reason of a rejected moment-curvature request.</summary>
    public enum MomentCurvatureRejection
    {
        /// <summary>Steps outside 10-500, end fraction outside (0; 1], N, direction, tolerance or yield strain not valid, bisections outside 0-30.</summary>
        InvalidRequest,
        /// <summary>The limit function returned no point at the requested N.</summary>
        LimitPointNotAvailable,
        /// <summary>|N of the limit point − requested N| beyond the axial tolerance.</summary>
        AxialResidual,
        /// <summary>Limit moment in the requested direction not positive or not finite.</summary>
        NonPositiveLimitMoment
    }

    /// <summary>
    /// Typed rejection of the overload with units (0.0.18.0): an <see cref="ArgumentException"/> with the message of 0.0.17.0 (unit labels of the
    /// overload), the reason and the values in the units of the caller (NaN when not reached). The overloads of 0.0.17.0 keep throwing the exact type
    /// <see cref="ArgumentException"/> with the same message; both carry the reason in <c>Data[</c><see cref="MomentCurvatureAnalysis.RejectionKey"/><c>]</c>.
    /// </summary>
    public sealed class MomentCurvatureException : ArgumentException
    {
        public MomentCurvatureRejection Reason { get; }
        /// <summary>Requested axial force.</summary>
        public double AxialForce { get; }
        /// <summary>Axial force of the limit point; NaN when no limit point was available.</summary>
        public double LimitAxialForce { get; }
        public double AxialTolerance { get; }
        /// <summary>Limit moment in the requested direction; NaN when not computed.</summary>
        public double LimitMoment { get; }
        internal MomentCurvatureException(string message, MomentCurvatureRejection reason, double axialForce, double limitAxialForce, double axialTolerance, double limitMoment)
            : base(message)
        {
            Reason = reason; AxialForce = axialForce; LimitAxialForce = limitAxialForce; AxialTolerance = axialTolerance; LimitMoment = limitMoment;
            Data[MomentCurvatureAnalysis.RejectionKey] = reason;
        }
    }

    /// <summary>First yield refined by bisection (0.0.18.0): outcome of the refinement that the Status describes.</summary>
    public sealed class MomentCurvatureYieldRefinement
    {
        /// <summary>True when the bisections were completed; false when a response failed and the yield stays at the first yielded sample.</summary>
        public bool Applied { get; }
        /// <summary>Number of bisections of the request.</summary>
        public int Bisections { get; }
        /// <summary>Refined first yield moment, in the units of the caller; null when not applied.</summary>
        public double? Moment { get; }
        /// <summary>Raw message of the exception that interrupted the refinement; null when applied.</summary>
        public string InterruptionMessage { get; }
        internal MomentCurvatureYieldRefinement(bool applied, int bisections, double? moment, string interruptionMessage)
        { Applied = applied; Bisections = bisections; Moment = moment; InterruptionMessage = interruptionMessage; }
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
        /// <summary>Raw message of the exception that stopped the curve at <see cref="InterruptedAtStep"/>; null when complete (0.0.18.0).</summary>
        public string InterruptionMessage { get; internal set; }
        /// <summary>Refinement of the first yield; null when not attempted (no bisections requested, no yield, or yield at the first point) (0.0.18.0).</summary>
        public MomentCurvatureYieldRefinement YieldRefinement { get; internal set; }
        public string Status { get; internal set; }
    }

    /// <summary>
    /// Moment-curvature orchestration transferred from ANTHEA (MomentCurvatureCalculator and ConcreteCurvatureAnalysis, commit fe4652c) with N/Nmm and
    /// 1/mm units, or the coherent units of the caller with the overload with <see cref="MomentCurvatureUnits"/> (0.0.18.0). The equilibrium is the native
    /// section solver: limit point at constant N on the failure domain, nonlinear stress analyses. Fixtures: GPCChecker.Test.Concrete/Fixtures/curvature-legacy.csv.
    /// With a literal <c>null</c> in the position of the units the overload with units is chosen (and rejects it).
    /// </summary>
    public static class MomentCurvatureAnalysis
    {
        /// <summary>Key of <see cref="Exception.Data"/> holding the <see cref="MomentCurvatureRejection"/> of a rejected request (0.0.18.0).</summary>
        public const string RejectionKey = "GPC.MomentCurvatureRejection";

        public static MomentCurvatureResult Calculate(MomentCurvatureRequest p, Func<double, double, double, MomentCurvatureLimit> limit,
            Func<double, double, double, MomentCurvatureStrains> response, double steelYieldStrain, CancellationToken token = default(CancellationToken))
            => Run(p, limit, response, steelYieldStrain, MomentCurvatureUnits.NewtonMillimetre, false, token);

        /// <summary>
        /// Curve with the units of the caller (0.0.18.0): forces, moments and <see cref="MomentCurvatureRequest.AxialTolerance"/> in coherent units of the
        /// caller (for example kN and kNm), which the Status and the messages label with <paramref name="units"/>; the curvature is the modulus of the
        /// gradient of the strains returned by the functions. Same algorithm and texts as the overload without units, which uses N and Nmm. Rejections
        /// are <see cref="MomentCurvatureException"/> (an <see cref="ArgumentException"/>) with the reason and the values; interruptions and the
        /// refinement of the first yield are also given as fields (<see cref="MomentCurvatureResult.InterruptionMessage"/>,
        /// <see cref="MomentCurvatureResult.YieldRefinement"/>).
        /// </summary>
        public static MomentCurvatureResult Calculate(MomentCurvatureRequest p, Func<double, double, double, MomentCurvatureLimit> limit,
            Func<double, double, double, MomentCurvatureStrains> response, double steelYieldStrain, MomentCurvatureUnits units, CancellationToken token = default(CancellationToken))
            => Run(p, limit, response, steelYieldStrain, units ?? throw new ArgumentNullException(nameof(units)), true, token);

        private static Exception Rejection(bool typed, string message, MomentCurvatureRejection reason, double axialForce, double limitAxialForce, double tolerance, double limitMoment)
        {
            if (typed) return new MomentCurvatureException(message, reason, axialForce, limitAxialForce, tolerance, limitMoment);
            var ex = new ArgumentException(message); ex.Data[RejectionKey] = reason; return ex;
        }

        private static MomentCurvatureResult Run(MomentCurvatureRequest p, Func<double, double, double, MomentCurvatureLimit> limit,
            Func<double, double, double, MomentCurvatureStrains> response, double steelYieldStrain, MomentCurvatureUnits units, bool typed, CancellationToken token)
        {
            if (p == null || limit == null || response == null) throw new ArgumentNullException();
            if (double.IsNaN(p.AxialForce + p.DirectionDegrees + p.EndFraction + p.AxialTolerance + steelYieldStrain)
                || double.IsInfinity(p.AxialForce + p.DirectionDegrees + p.EndFraction + p.AxialTolerance + steelYieldStrain)
                || p.Steps < 10 || p.Steps > 500 || p.EndFraction <= 0 || p.EndFraction > 1 || steelYieldStrain <= 0 || p.AxialTolerance <= 0
                || p.YieldRefinementSteps < 0 || p.YieldRefinementSteps > 30)
                throw Rejection(typed, "Moment-curvature: 10-500 steps, end fraction in (0; 1], finite N and direction.", MomentCurvatureRejection.InvalidRequest,
                    p.AxialForce, double.NaN, p.AxialTolerance, double.NaN);
            double angle = p.DirectionDegrees * Math.PI / 180, c = Math.Cos(angle), s = Math.Sin(angle);
            token.ThrowIfCancellationRequested();
            var rd = limit(p.AxialForce, c, s) ?? throw Rejection(typed, "Limit point not available at the assigned N.", MomentCurvatureRejection.LimitPointNotAvailable,
                p.AxialForce, double.NaN, p.AxialTolerance, double.NaN);
            if (Math.Abs(rd.AxialForce - p.AxialForce) > p.AxialTolerance)
                throw Rejection(typed, $"Residual N at the limit point beyond the tolerance {p.AxialTolerance:G6} {units.Force}: N = {p.AxialForce:G9} {units.Force}, limit N = {rd.AxialForce:G9} {units.Force}.",
                    MomentCurvatureRejection.AxialResidual, p.AxialForce, rd.AxialForce, p.AxialTolerance, double.NaN);
            double mr = rd.Mx * c + rd.My * s;
            if (mr <= 0 || double.IsNaN(mr) || double.IsInfinity(mr))
                throw Rejection(typed, "Non-positive limit moment in the requested direction.", MomentCurvatureRejection.NonPositiveLimitMoment, p.AxialForce, rd.AxialForce,
                    p.AxialTolerance, mr);
            var points = new List<MomentCurvaturePoint>(); double? yield = null, ultimate = null; int? interrupted = null;
            string interruption = null; MomentCurvatureYieldRefinement refinement = null;
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
                catch (Exception ex) when (!(ex is OperationCanceledException))
                { status += $" Interrupted at step {i}: {ex.Message}"; interrupted = i; interruption = ex.Message; break; }
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
                    status += $" First yield refined with {p.YieldRefinementSteps} bisections, My = {high.Moment:G7} {units.Moment}.";
                    refinement = new MomentCurvatureYieldRefinement(true, p.YieldRefinementSteps, high.Moment, null);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    status += " Yield taken at the first sample: refinement interrupted, " + ex.Message;
                    refinement = new MomentCurvatureYieldRefinement(false, p.YieldRefinementSteps, null, ex.Message);
                }
            }
            return new MomentCurvatureResult { Points = points.AsReadOnly(), LimitMoment = mr, YieldCurvature = yield, UltimateCurvature = ultimate,
                LimitAxialForce = rd.AxialForce, AxialResidual = rd.AxialForce - p.AxialForce, InterruptedAtStep = interrupted, InterruptionMessage = interruption,
                YieldRefinement = refinement,
                Status = status + $" N at the limit point = {rd.AxialForce:G9} {units.Force}; residual = {rd.AxialForce - p.AxialForce:G6} {units.Force}. No post-peak branch." };
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
