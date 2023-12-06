using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;

namespace GPC.Checker.SectionSolvers
{
    /// <summary>
    /// This Strategy interface declares operations common to all supported versions of some domain point calculations.
    /// The Context uses this interface to call the algorithm defined by Concrete Strategies.
    /// </summary>
    public interface IDomainPointStrategy
    {
        /// <summary>
        /// Reference to the SectionSolver that uses this calculation.
        /// </summary>
        SectionSolver Solver { get; }

        /// <summary>
        /// Calculate resistance domain point.
        /// </summary>
        /// <param name="targetLocalForces"></param>
        /// <param name="failureAnalysisTypeOverride"></param>
        /// <returns></returns>
        FailureDomain.FailureDomainPoint CalculateDomainPoint(ResultBeamForces targetLocalForces, SectionSolver.FailureAnalysisTypes? failureAnalysisTypeOverride = null);
    }
}
