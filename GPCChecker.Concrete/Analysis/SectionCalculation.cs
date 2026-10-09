using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Checkers.Concrete.Results;

namespace GPC.Checkers.Concrete.Analysis
{
    public enum CalculationStatus { Completed, NotConverged, Unsupported, Failed }
    /// <summary>Numerical status independent of the engineering verdict. Missing residuals and iteration counts are not invented.</summary>
    public sealed class SolverDiagnostics
    {
        public ResistanceConvergence ResistanceConvergence { get; }
        public AxialEquilibriumEvidence AxialEquilibrium { get; }
        public CalculationStatus Status { get; }
        public string Engine { get; }
        public string Version { get; }
        public string Message { get; }
        public SolverDiagnostics(CalculationStatus status, string engine, string version, string message = null)
            : this(status, engine, version, message, null) { }
        public SolverDiagnostics(CalculationStatus status, string engine, string version, string message, AxialEquilibriumEvidence axialEquilibrium)
            : this(status, engine, version, message, axialEquilibrium, null) { }
        public SolverDiagnostics(CalculationStatus status, string engine, string version, string message,
            AxialEquilibriumEvidence axialEquilibrium, ResistanceConvergence resistanceConvergence)
        {
            ResistanceConvergence = resistanceConvergence;
            if (status == CalculationStatus.Completed && resistanceConvergence != null && !resistanceConvergence.Accepted)
                throw new ArgumentException("Completed calculation cannot contain rejected resistance convergence.");
            AxialEquilibrium = axialEquilibrium;
            if (status == CalculationStatus.Completed && axialEquilibrium != null && !axialEquilibrium.Accepted)
                throw new ArgumentException("Completed calculation cannot contain rejected axial equilibrium.");
            if (!Enum.IsDefined(typeof(CalculationStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            Status = status; Engine = engine ?? throw new ArgumentNullException(nameof(engine)); Version = version; Message = message;
        }
    }
    /// <summary>Immutable actions in N, N.mm and coordinates in mm. Each access returns a fresh force/coordinate system.</summary>
    public sealed class SectionAnalysisInput
    {
        private readonly ResultBeamForces _forces;
        public ResultBeamForces Forces => Copy(_forces);
        public SectionAnalysisInput(ResultBeamForces forces)
        {
            if (forces == null) throw new ArgumentNullException(nameof(forces));
            if (new[] { forces.N, forces.V1, forces.V2, forces.T, forces.M1, forces.M2 }.Any(v => !Finite(v)))
                throw new ArgumentException("Finite section actions required.", nameof(forces));
            _forces = Copy(forces);
        }
        private static ResultBeamForces Copy(ResultBeamForces f) => new ResultBeamForces(f.N, f.V1, f.V2, f.T, f.M1, f.M2,
            (CoordinateSystem)f.CoordinateSystem.Clone());
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
    /// <summary>epsilon(x,y) = epsilon0 + ChiX*(x-X) + ChiY*(y-Y), compression negative, mm and 1/mm.</summary>
    public sealed class SectionStrain
    {
        public double X { get; }
        public double Y { get; }
        public double Epsilon { get; }
        public double ChiX { get; }
        public double ChiY { get; }
        public SectionStrain(double x, double y, double epsilon, double chiX, double chiY)
        {
            if (new[] { x, y, epsilon, chiX, chiY }.Any(v => !SectionAnalysisInput.Finite(v))) throw new ArgumentException("Non-finite strain plane.");
            X = x; Y = y; Epsilon = epsilon; ChiX = chiX; ChiY = chiY;
        }
        public StrainPlane ToStrainPlane() => new StrainPlane(ChiX, ChiY, new Point2d(X, Y), Epsilon);
        public static SectionStrain From(StrainPlane plane) => plane?.ReferencePoint == null ? null
            : new SectionStrain(plane.ReferencePoint.X, plane.ReferencePoint.Y, plane.StrainReferencePoint, plane.ChiX, plane.ChiY);
    }
    public sealed class SectionStressPoint
    {
        public string Id { get; }
        public double X { get; }
        public double Y { get; }
        public double Stress { get; }
        public SectionStressPoint(string id, double x, double y, double stress)
        {
            if (string.IsNullOrWhiteSpace(id) || new[] { x, y, stress }.Any(v => !SectionAnalysisInput.Finite(v))) throw new ArgumentException("Invalid stress point.");
            Id = id; X = x; Y = y; Stress = stress;
        }
    }
    /// <summary>Detached sampled response: no solver, section, material or standard reference; no interpolation of unsampled stresses.</summary>
    public sealed class SectionResponse
    {
        public SectionAnalysisInput Input { get; }
        public SolverDiagnostics Diagnostics { get; }
        public SectionStrain Strain { get; }
        public bool Linear { get; }
        public double PsiRebar { get; }
        public double PsiTendon { get; }
        public IReadOnlyList<SectionStressPoint> Concrete { get; }
        public IReadOnlyList<SectionStressPoint> Bars { get; }
        public SectionResponse(SectionAnalysisInput input, SolverDiagnostics diagnostics, SectionStrain strain, bool linear, double psiRebar, double psiTendon,
            IEnumerable<SectionStressPoint> concrete, IEnumerable<SectionStressPoint> bars)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input)); Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            Strain = strain; Linear = linear; PsiRebar = psiRebar; PsiTendon = psiTendon;
            Concrete = Array.AsReadOnly((concrete ?? Enumerable.Empty<SectionStressPoint>()).ToArray());
            Bars = Array.AsReadOnly((bars ?? Enumerable.Empty<SectionStressPoint>()).ToArray());
            if (!SectionAnalysisInput.Finite(psiRebar) || !SectionAnalysisInput.Finite(psiTendon) || Concrete.Any(p => p == null) || Bars.Any(p => p == null))
                throw new ArgumentException("Invalid response data.");
            if (diagnostics.Status == CalculationStatus.Completed && (strain == null || Concrete.Count == 0)) throw new ArgumentException("Completed response requires strain and concrete samples.");
            if (Concrete.Select(p => p.Id).Distinct().Count() != Concrete.Count || Bars.Select(p => p.Id).Distinct().Count() != Bars.Count)
                throw new ArgumentException("Duplicate stress point identity.");
        }
    }
    public sealed class SectionResistanceResponse
    {
        public SolverDiagnostics Diagnostics { get; }
        public SectionStrain Strain { get; }
        public double? N { get; }
        public double? M1 { get; }
        public double? M2 { get; }
        public double? Utilization { get; }
        public string Criterion { get; }
        public string FailureMode { get; }
        /// <summary>No domain search or resistance capacity is claimed for an unloaded section without initial strains.</summary>
        public SectionAnalysisInput ZeroDemandInput { get; }
        public bool IsZeroDemand => ZeroDemandInput != null;
        private SectionResistanceResponse(SectionAnalysisInput input, SolverDiagnostics diagnostics, string criterion)
        {
            ZeroDemandInput = input; Diagnostics = diagnostics; Criterion = criterion; Utilization = 0; FailureMode = "ZeroDemand";
        }
        public static SectionResistanceResponse ForZeroDemand(SectionAnalysisInput input, SolverDiagnostics diagnostics, string criterion)
        {
            if (input == null || diagnostics == null) throw new ArgumentNullException();
            var force = input.Forces;
            if (force.N != 0 || force.M1 != 0 || force.M2 != 0 || diagnostics.Status != CalculationStatus.Completed)
                throw new ArgumentException("Completed zero sectional demand required.");
            return new SectionResistanceResponse(input, diagnostics, criterion);
        }
        public SectionResistanceResponse(SolverDiagnostics diagnostics, string criterion, SectionStrain strain = null,
            double? n = null, double? m1 = null, double? m2 = null, double? utilization = null, string failureMode = null)
        {
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)); Criterion = criterion; Strain = strain;
            N = n; M1 = m1; M2 = m2; Utilization = utilization; FailureMode = failureMode;
            if (diagnostics.Status == CalculationStatus.Completed && (strain == null || new[] { n, m1, m2, utilization }.Any(v => !v.HasValue || !SectionAnalysisInput.Finite(v.Value)) || utilization < 0))
                throw new ArgumentException("Invalid completed resistance response.");
        }
    }
    /// <summary>A session owns a fixed section, resolved design/constitutive parameters and numerical options; implementations declare synchronization requirements.</summary>
    public interface ISectionResponseSolver { SectionResponse Solve(SectionAnalysisInput input, CancellationToken cancellationToken = default(CancellationToken)); }
    public interface ISectionResistanceSolver { SectionResistanceResponse SolveResistance(SectionAnalysisInput input, CancellationToken cancellationToken = default(CancellationToken)); }
}
