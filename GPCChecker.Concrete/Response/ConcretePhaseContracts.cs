using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Analysis;
using GPC.Model.Results;
using GPC.Geometry;
using GPC.Model.Core.Coordinates;

namespace GPC.Checkers.Concrete.Response
{
    public enum ConcretePhaseKind { Load, Shrinkage }
    public enum ConcretePhaseStop { Completed, EquilibriumNotFound, MaterialDomain }
    public enum ConcretePhasePointKind { ConcreteVertex, ConcreteIntegrationPoint, Rebar, StructuralSteel }

    /// <summary>One increment of N-M1-M2 (N, Nmm, with its own force axes), OR uniform free concrete strain.
    /// Shrinkage is negative, dimensionless; it is added once, never interpreted as a cumulative value.
    /// Section geometry, materials and all reinforcements remain active throughout the sequence.</summary>
    public sealed class ConcretePhase
    {
        private readonly SectionAnalysisInput load;
        public string Name { get; }
        public ConcretePhaseKind Kind { get; }
        public ResultBeamForces LoadIncrement => load == null ? null : load.Forces;
        public double ShrinkageIncrement { get; }
        public int Substeps { get; }
        private ConcretePhase(string name, SectionAnalysisInput load, double shrinkage, int substeps)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A phase name is required.", nameof(name));
            if (!Finite(shrinkage)) throw new ArgumentOutOfRangeException(nameof(shrinkage));
            if (substeps < 1 || substeps > 4096) throw new ArgumentOutOfRangeException(nameof(substeps));
            Name = name; this.load = load; ShrinkageIncrement = shrinkage; Substeps = substeps;
            Kind = load == null ? ConcretePhaseKind.Shrinkage : ConcretePhaseKind.Load;
        }
        public static ConcretePhase Load(string name, ResultBeamForces increment, int substeps = 1)
        {
            var copy = new SectionAnalysisInput(increment);
            var f = copy.Forces;
            ValidateAxes(f.CoordinateSystem);
            if (f.V1 != 0 || f.V2 != 0 || f.T != 0)
                throw new ArgumentException("Phase analysis supports N, M1 and M2 only; shear and torsion must be zero.", nameof(increment));
            return new ConcretePhase(name, copy, 0, substeps);
        }
        public static ConcretePhase Shrinkage(string name, double increment, int substeps = 1)
            => new ConcretePhase(name, null, increment, substeps);
        /// <summary>Convenience factory matching the bridge input: -200 means -200 microstrain.</summary>
        public static ConcretePhase ShrinkageMicrostrain(string name, double increment, int substeps = 1)
            => Shrinkage(name, increment * 1e-6, substeps);
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static void ValidateAxes(CoordinateSystem axes)
        {
            Axes.Validate(axes);
            if (Math.Abs(axes.V3.Z - 1) > 1e-10)
                throw new ArgumentException("Phase force axes must lie in the section plane, with the same positive longitudinal direction.");
        }
    }

    /// <summary>Absolute residual tolerances in N and Nmm, plus a relative term scaled by the target actions and section size.
    /// Numerical substeps carry no material memory. The same constitutive envelope is used in loading/unloading.</summary>
    public sealed class ConcretePhaseOptions
    {
        public int MaximumIterations { get; }
        public int MaximumLineSearchIterations { get; }
        public int MaximumSubdivisions { get; }
        public int MaximumAttemptsPerPhase { get; }
        public double RelativeTolerance { get; }
        public double ForceTolerance { get; }
        public double MomentTolerance { get; }
        public bool IncludeIntegrationPoints { get; }
        public ConcretePhaseOptions(int maximumIterations = 60, int maximumLineSearchIterations = 24,
            int maximumSubdivisions = 8, int maximumAttemptsPerPhase = 4096,
            double relativeTolerance = 1e-9, double forceTolerance = 1e-3, double momentTolerance = .1,
            bool includeIntegrationPoints = true)
        {
            if (maximumIterations < 1 || maximumLineSearchIterations < 1 || maximumLineSearchIterations > 60
                || maximumSubdivisions < 0 || maximumSubdivisions > 20 || maximumAttemptsPerPhase < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumIterations), "Invalid iteration/subdivision limits.");
            if (!ConcretePhase.Finite(relativeTolerance) || relativeTolerance < 0
                || !ConcretePhase.Finite(forceTolerance) || forceTolerance <= 0
                || !ConcretePhase.Finite(momentTolerance) || momentTolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(relativeTolerance), "Finite positive absolute tolerances and nonnegative relative tolerance required.");
            MaximumIterations = maximumIterations; MaximumLineSearchIterations = maximumLineSearchIterations;
            MaximumSubdivisions = maximumSubdivisions; MaximumAttemptsPerPhase = maximumAttemptsPerPhase;
            RelativeTolerance = relativeTolerance; ForceTolerance = forceTolerance; MomentTolerance = momentTolerance;
            IncludeIntegrationPoints = includeIntegrationPoints;
        }
    }

    /// <summary>Detached point response. Coordinates in mm, stresses in MPa. MechanicalStrain = TotalStrain - FreeStrain.
    /// For tendons the existing EpsilonP is included as negative FreeStrain, not accumulated plastic strain.
    /// Concrete vertices plus optional integration points are samples, not a mesh-independent certificate of stress extrema.</summary>
    public sealed class ConcretePhasePoint
    {
        public string Id { get; }
        public ConcretePhasePointKind Kind { get; }
        public double X { get; }
        public double Y { get; }
        public double TotalStrain { get; }
        public double FreeStrain { get; }
        public double MechanicalStrain => TotalStrain - FreeStrain;
        public double Stress { get; }
        internal ConcretePhasePoint(string id, ConcretePhasePointKind kind, double x, double y,
            double strain, double free, double stress)
        { Id = id; Kind = kind; X = x; Y = y; TotalStrain = strain; FreeStrain = free; Stress = stress; }
    }

    /// <summary>Ax+By+C=0 in section coordinates (mm), from the MECHANICAL strain plane of concrete.
    /// Null for uniform strain: no unique neutral axis, including the completely undeformed plane.</summary>
    public sealed class ConcretePhaseNeutralAxis
    {
        public double A { get; }
        public double B { get; }
        public double C { get; }
        internal ConcretePhaseNeutralAxis(SectionStrain plane)
        { A = plane.ChiX; B = plane.ChiY; C = plane.Epsilon - A * plane.X - B * plane.Y; }
    }

    /// <summary>One equilibrated state. Actions are returned in the checker's force reference axes; strain gradients are in section coordinates.
    /// Tangent rows: N,M1,M2 in those force axes. Columns: epsilon at the integration reference point, ChiX,ChiY (1/mm).
    /// It is a numerical derivative of the same integrated constitutive response, not three uncoupled EI values.</summary>
    public sealed class ConcretePhaseState
    {
        private readonly SectionAnalysisInput applied, integrated;
        private readonly double[,] tangent;
        public ResultBeamForces AppliedForces => applied.Forces;
        public ResultBeamForces IntegratedForces => integrated.Forces;
        public double Shrinkage { get; }
        public SectionStrain TotalStrain { get; }
        public SectionStrain ConcreteMechanicalStrain { get; }
        public ConcretePhaseNeutralAxis ConcreteNeutralAxis { get; }
        /// <summary>Null if no finite numerical tangent can be evaluated at this state, e.g. at a material endpoint.</summary>
        public double[,] Tangent => tangent == null ? null : (double[,])tangent.Clone();
        public double ForceResidual => integrated.Forces.N - applied.Forces.N;
        public double Moment1Residual => integrated.Forces.M1 - applied.Forces.M1;
        public double Moment2Residual => integrated.Forces.M2 - applied.Forces.M2;
        /// <summary>Largest absolute residual divided by its allowed tolerance, in the output force axes. Accepted states have a value at most one.</summary>
        public double NormalizedResidual { get; }
        public IReadOnlyList<ConcretePhasePoint> Points { get; }
        internal ConcretePhaseState(ResultBeamForces target, ResultBeamForces force, double shrinkage,
            SectionStrain total, double[,] stiffness, IEnumerable<ConcretePhasePoint> points, double normalizedResidual)
        {
            applied = new SectionAnalysisInput(target); integrated = new SectionAnalysisInput(force); Shrinkage = shrinkage;
            TotalStrain = total; ConcreteMechanicalStrain = new SectionStrain(total.X, total.Y, total.Epsilon - shrinkage, total.ChiX, total.ChiY);
            ConcreteNeutralAxis = total.ChiX == 0 && total.ChiY == 0 ? null : new ConcretePhaseNeutralAxis(ConcreteMechanicalStrain);
            tangent = stiffness == null ? null : (double[,])stiffness.Clone(); Points = Array.AsReadOnly(points.ToArray());
            NormalizedResidual = normalizedResidual;
        }
    }

    /// <summary>Result of an attempted phase. On failure State is the last ACCEPTED substep and ReachedFraction is below one.
    /// The requested complete phase is in Phase; failed trials are never presented as equilibrated states.</summary>
    public sealed class ConcretePhaseResult
    {
        public int Index { get; }
        public ConcretePhase Phase { get; }
        public ConcretePhaseState State { get; }
        public ConcretePhaseStop Stop { get; }
        public bool Completed => Stop == ConcretePhaseStop.Completed;
        public double ReachedFraction { get; }
        public int Iterations { get; }
        public int Attempts { get; }
        public int AcceptedSubsteps { get; }
        public int ForceEvaluations { get; }
        public double ElapsedMilliseconds { get; }
        public string Message { get; }
        internal ConcretePhaseResult(int index, ConcretePhase phase, ConcretePhaseState state, ConcretePhaseStop stop,
            double fraction, int iterations, int attempts, int accepted, int evaluations, double elapsed, string message)
        { Index = index; Phase = phase; State = state; Stop = stop; ReachedFraction = fraction; Iterations = iterations;
            Attempts = attempts; AcceptedSubsteps = accepted; ForceEvaluations = evaluations; ElapsedMilliseconds = elapsed; Message = message; }
    }

    /// <summary>The initial equilibrium includes fixed tendon prestrains. A complete history is an equilibrium analysis, not an ULS certificate.</summary>
    public sealed class ConcretePhaseAnalysisResult
    {
        public ConcretePhaseState InitialState { get; }
        public IReadOnlyList<ConcretePhaseResult> Phases { get; }
        public bool Completed { get; }
        public ConcretePhaseStop Stop { get; }
        public string Message { get; }
        public int InitialIterations { get; }
        public int InitialForceEvaluations { get; }
        public double InitialElapsedMilliseconds { get; }
        /// <summary>Initial equilibrium plus all attempted phases, including response sampling and tangents; excludes checker construction.</summary>
        public double ElapsedMilliseconds => InitialElapsedMilliseconds + Phases.Sum(p => p.ElapsedMilliseconds);
        internal ConcretePhaseAnalysisResult(ConcretePhaseState initial, IEnumerable<ConcretePhaseResult> phases,
            ConcretePhaseStop stop, string message, int initialIterations, int initialEvaluations, double initialMilliseconds)
        {
            InitialState = initial; Phases = Array.AsReadOnly(phases.ToArray()); Stop = stop; Completed = stop == ConcretePhaseStop.Completed; Message = message;
            InitialIterations = initialIterations; InitialForceEvaluations = initialEvaluations; InitialElapsedMilliseconds = initialMilliseconds;
        }
    }
}
