using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Checkers.Concrete.SectionSolvers;

namespace GPC.Checkers.Concrete.Analysis
{
    public sealed class NumericalResidual
    {
        public string Quantity { get; }
        public string Unit { get; }
        public double Residual { get; }
        public double Tolerance { get; }
        public bool Accepted => Math.Abs(Residual) <= Tolerance;
        public NumericalResidual(string quantity, string unit, double residual, double tolerance)
        {
            if (string.IsNullOrWhiteSpace(quantity) || string.IsNullOrWhiteSpace(unit) || !SectionAnalysisInput.Finite(residual)
                || !SectionAnalysisInput.Finite(tolerance) || tolerance < 0) throw new ArgumentException("InvalidNumericalResidual");
            Quantity = quantity; Unit = unit; Residual = residual; Tolerance = tolerance;
        }
    }
    /// <summary>Checks the returned point against the constraints of the search, not against a structural resistance limit.
    /// Fixed moments refer to ConstraintAxes; this is the native solver basis, not an inferred global frame.</summary>
    public sealed class ResistanceConvergence
    {
        private readonly CoordinateSystem _axes;
        public CoordinateSystem ConstraintAxes => (CoordinateSystem)_axes.Clone();
        public SectionSolver.FailureAnalysisTypes Criterion { get; }
        public SectionAnalysisInput Demand { get; }
        public SectionAnalysisInput Capacity { get; }
        public IReadOnlyList<NumericalResidual> Residuals { get; }
        public bool HasSearchDirection { get; }
        public bool Accepted => HasSearchDirection && Residuals.All(r => r.Accepted);
        private ResistanceConvergence(SectionSolver.FailureAnalysisTypes criterion, SectionAnalysisInput demand,
            SectionAnalysisInput capacity, CoordinateSystem axes, IEnumerable<NumericalResidual> residuals, bool direction)
        {
            Criterion = criterion; Demand = demand; Capacity = capacity; _axes = (CoordinateSystem)axes.Clone();
            Residuals = Array.AsReadOnly(residuals.ToArray()); HasSearchDirection = direction;
        }
        /// <summary>Forces in N, moments in Nmm, angles in radians. Tolerances are numerical acceptance data, not code factors.
        /// Angular collinearity uses the historical N/1000 and M/1000000 scaling of the domain search.</summary>
        public static ResistanceConvergence Evaluate(SectionSolver.FailureAnalysisTypes criterion, SectionAnalysisInput demand,
            SectionAnalysisInput capacity, CoordinateSystem constraintAxes, double axialTolerance, double moment1Tolerance,
            double moment2Tolerance, double angularTolerance)
        {
            if (demand == null || capacity == null || constraintAxes == null) throw new ArgumentNullException();
            if (!Enum.IsDefined(typeof(SectionSolver.FailureAnalysisTypes), criterion)) throw new ArgumentOutOfRangeException(nameof(criterion));
            if (new[] { axialTolerance, moment1Tolerance, moment2Tolerance, angularTolerance }.Any(t => !SectionAnalysisInput.Finite(t) || t < 0)
                || angularTolerance >= Math.PI / 2) throw new ArgumentException("InvalidConvergenceTolerances");
            GPC.Model.PostProcessing.Axes.Validate(constraintAxes);
            var d = demand.Forces.ToCoordinateSystemWithEccentricity(constraintAxes);
            var r = capacity.Forces.ToCoordinateSystemWithEccentricity(constraintAxes);
            var residuals = new List<NumericalResidual>(); bool direction = true;
            Action axial = () => residuals.Add(new NumericalResidual("N", "N", r.N - d.N, axialTolerance));
            Action mx = () => residuals.Add(new NumericalResidual("M1", "Nmm", r.M1 - d.M1, moment1Tolerance));
            Action my = () => residuals.Add(new NumericalResidual("M2", "Nmm", r.M2 - d.M2, moment2Tolerance));
            Action<double, double> sense = (target, value) => {
                direction = target != 0 && value != 0;
                residuals.Add(new NumericalResidual("SearchDirection", "1", direction && Math.Sign(target) == Math.Sign(value) ? 0 : 1, 0));
            };
            Action<Vector3d, Vector3d> angle = (target, value) => {
                direction = target.Length > 0 && value.Length > 0;
                residuals.Add(new NumericalResidual("SearchAngle", "rad", direction ? target.AngleTo(value) : Math.PI, angularTolerance));
            };
            switch (criterion)
            {
                case SectionSolver.FailureAnalysisTypes.ConstantN:
                    axial(); angle(new Vector3d(d.M1 / 1e6, d.M2 / 1e6, 0), new Vector3d(r.M1 / 1e6, r.M2 / 1e6, 0)); break;
                case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                    angle(new Vector3d(d.M1 / 1e6, d.M2 / 1e6, d.N / 1000), new Vector3d(r.M1 / 1e6, r.M2 / 1e6, r.N / 1000)); break;
                case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                    mx(); my(); sense(d.N, r.N); break;
                case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                    axial(); mx(); sense(d.M2, r.M2); break;
                case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                    axial(); my(); sense(d.M1, r.M1); break;
            }
            return new ResistanceConvergence(criterion, demand, capacity, constraintAxes, residuals, direction);
        }
    }
}
