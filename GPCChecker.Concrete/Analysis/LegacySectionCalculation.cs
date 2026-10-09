using System;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;

namespace GPC.Checkers.Concrete.Analysis
{
    /// <summary>Boundary adapter preserving the existing formulas and tolerances. The caller owns the checker exclusively.
    /// Construction and calls across sessions must retain existing synchronization. Cancellation is checked before and after the legacy call.</summary>
    public sealed class LegacySectionCalculation : ISectionResponseSolver, ISectionResistanceSolver
    {
        private readonly SectionChecker _checker;
        private readonly object _sync = new object();
        public const string EngineId = "Concrete.LegacySectionSolver";
        public static string Version => typeof(SectionChecker).Assembly.GetName().Version.ToString();
        public LegacySectionCalculation(SectionChecker checker) { _checker = checker ?? throw new ArgumentNullException(nameof(checker)); }
        private static SolverDiagnostics Diagnostic(CalculationStatus status, string message = null) => new SolverDiagnostics(status, EngineId, Version, message);
        public SectionResponse Solve(SectionAnalysisInput input, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            lock (_sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = _checker.GetTensionAnalysisResult(input.Forces);
                cancellationToken.ThrowIfCancellationRequested();
                if (result == null) return new SectionResponse(input, Diagnostic(CalculationStatus.NotConverged, "Missing response."), null, false, 0, 0, null, null);
                return Capture(result, input);
            }
        }
        public static SectionResponse Capture(StressAnalysisResult result, SectionAnalysisInput input = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            input = input ?? new SectionAnalysisInput(result.Force);
            var strain = SectionStrain.From(result.StrainPlane);
            var psi = result.PsiRebar ?? 0; var psiTendon = result.PsiTendon ?? 0;
            if (strain == null) return new SectionResponse(input, Diagnostic(CalculationStatus.NotConverged, "No strain plane."),
                null, result.LinearElasticAnalysis, psi, psiTendon, null, null);
            var concrete = result.LinearElasticAnalysis ? result.GetConcreteVerticesTension(psi) : result.GetConcreteVerticesTension();
            var bars = result.LinearElasticAnalysis ? result.GetRebarsTension(psi, psiTendon) : result.GetRebarsTension();
            if (concrete.Length == 0 || concrete.Any(p => !SectionAnalysisInput.Finite(p.tension)) || bars.Any(p => !SectionAnalysisInput.Finite(p.tension)))
                return new SectionResponse(input, Diagnostic(CalculationStatus.NotConverged, "Missing or non-finite stresses."), null, result.LinearElasticAnalysis, psi, psiTendon, null, null);
            return new SectionResponse(input, Diagnostic(CalculationStatus.Completed, "Finite legacy response; equilibrium residual and iteration count unavailable."),
                strain, result.LinearElasticAnalysis, psi, psiTendon,
                concrete.Select((p, i) => new SectionStressPoint("C" + (i + 1), p.point.X, p.point.Y, p.tension)),
                bars.Select((p, i) => new SectionStressPoint((p.rebar.EpsilonP != 0 ? "P" : "B") + (i + 1), p.rebar.Position.X, p.rebar.Position.Y, p.tension)));
        }
        public SectionResistanceResponse SolveResistance(SectionAnalysisInput input, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            lock (_sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var force = input.Forces; var criterion = _checker.SectionCheckerOptions.FailureAnalysisType;
                if (force.N == 0 && force.M1 == 0 && force.M2 == 0 && _checker.SectionSolver.ConcreteSection.Rebars.All(r => r.EpsilonP == 0))
                    return SectionResistanceResponse.ForZeroDemand(input, Diagnostic(CalculationStatus.Completed,
                        "Zero N-M1-M2 demand without initial rebar strain. No domain search or resistance capacity is claimed; shear/torsion are separate checks."), criterion.ToString());
                var point = _checker.SectionSolver.CalculateConvergedDomainPoint(force, criterion, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (point?.StrainPlane == null) return new SectionResistanceResponse(Diagnostic(CalculationStatus.NotConverged, "Missing domain point/strain plane."), criterion.ToString());
                if (new[] { point.NRd, point.MxRd, point.MyRd }.Any(v => !SectionAnalysisInput.Finite(v)))
                    return new SectionResistanceResponse(Diagnostic(CalculationStatus.NotConverged, "Non-finite domain point."), criterion.ToString());
                var convergence = DomainPointConvergence.Evaluate(_checker.SectionSolver, force, point, criterion);
                AxialEquilibriumEvidence equilibrium = null;
                var axial = convergence.Residuals.FirstOrDefault(r => r.Quantity == "N");
                if (axial != null) equilibrium = new AxialEquilibriumEvidence(force.N, point.NRd, axial.Tolerance);
                if (!convergence.Accepted)
                    return new SectionResistanceResponse(new SolverDiagnostics(CalculationStatus.NotConverged, EngineId, Version,
                        "The returned point does not satisfy the search constraints.", equilibrium, convergence), criterion.ToString());
                var ratio = point.CalculateWorkingRatio(criterion, force, 1e6, 1000);
                if (!SectionAnalysisInput.Finite(ratio) || ratio < 0) return new SectionResistanceResponse(Diagnostic(CalculationStatus.NotConverged, "Invalid working ratio."), criterion.ToString());
                return new SectionResistanceResponse(new SolverDiagnostics(CalculationStatus.Completed, EngineId, Version,
                    "Returned point satisfies the search constraints.", equilibrium, convergence), criterion.ToString(), SectionStrain.From(point.StrainPlane),
                    point.NRd, point.MxRd, point.MyRd, ratio, point.FailureIndex.ToString());
            }
        }
    }
}
