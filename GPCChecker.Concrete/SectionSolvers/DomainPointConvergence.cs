using System;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Model.Results;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    /// <summary>Post-validates all five searches on the returned point. Moment and angle tolerances are the solver stopping tolerances;
    /// the documented DomainPointAxialTolerance applies to N. Fallbacks do not widen these acceptance thresholds.</summary>
    public static class DomainPointConvergence
    {
        public static ResistanceConvergence Evaluate(SectionSolver solver, ResultBeamForces demand,
            FailureDomain.FailureDomainPoint point, SectionSolver.FailureAnalysisTypes criterion)
        {
            if (solver == null || demand == null || point == null) throw new ArgumentNullException();
            double moment1 = solver.FailureAnalysisDistanceTolerance / solver.ConvertToAdimensionalForces(new ForceTuple(0, 1, 0)).Mx;
            double moment2 = solver.FailureAnalysisDistanceTolerance / solver.ConvertToAdimensionalForces(new ForceTuple(0, 0, 1)).My;
            var capacity = new ResultBeamForces(point.NRd, 0, 0, 0, point.MxRd, point.MyRd, solver.SectionOption.ForceReferenceCoordinateSystem);
            return ResistanceConvergence.Evaluate(criterion, new SectionAnalysisInput(demand), new SectionAnalysisInput(capacity), solver.SolverAxes,
                DomainPointAxialTolerance.Calculate(solver, demand.ToCoordinateSystemWithEccentricity(solver.SolverAxes).N), moment1, moment2, solver.FailureAnalysisAngularTolerance);
        }
    }
}
