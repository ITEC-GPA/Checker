using System;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;

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
                bool assignedAxialForce = criterion == SectionSolver.FailureAnalysisTypes.ConstantN || criterion == SectionSolver.FailureAnalysisTypes.ConstantNMx
                    || criterion == SectionSolver.FailureAnalysisTypes.ConstantNMy;
                // Bug SLU-2 of 15/10/2026: with constant N and no moment the direction of the search is null and no point is ever accepted.
                if (criterion == SectionSolver.FailureAnalysisTypes.ConstantN && HasNoMomentDemand(force)
                    && _checker.SectionSolver.ConcreteSection.Rebars.All(r => r.EpsilonP == 0))
                {
                    var typed = AssignedAxialForceOutcome(force, criterion, true, cancellationToken);
                    if (typed != null) return typed;
                }
                var point = _checker.SectionSolver.CalculateConvergedDomainPoint(force, criterion, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                // Bug SLU-2 of 15/10/2026: at an assigned N outside [NRd,c; NRd,t] no strategy finds a point; the outcome is typed, not NotConverged.
                Func<SectionResistanceResponse, SectionResistanceResponse> rejected = response => assignedAxialForce
                    ? AssignedAxialForceOutcome(force, criterion, false, cancellationToken) ?? response : response;
                if (point?.StrainPlane == null) return rejected(new SectionResistanceResponse(Diagnostic(CalculationStatus.NotConverged, "Missing domain point/strain plane."), criterion.ToString()));
                if (new[] { point.NRd, point.MxRd, point.MyRd }.Any(v => !SectionAnalysisInput.Finite(v)))
                    return rejected(new SectionResistanceResponse(Diagnostic(CalculationStatus.NotConverged, "Non-finite domain point."), criterion.ToString()));
                var convergence = ValidateReturnedPointAndPlane(force, point, criterion);
                AxialEquilibriumEvidence equilibrium = null;
                var axial = convergence.Residuals.FirstOrDefault(r => r.Quantity == "N");
                if (axial != null) equilibrium = new AxialEquilibriumEvidence(force.N, convergence.Capacity.Forces.N, axial.Tolerance);
                if (!convergence.Accepted)
                    return rejected(new SectionResistanceResponse(new SolverDiagnostics(CalculationStatus.NotConverged, EngineId, Version,
                        "The returned point or its strain plane does not satisfy the search constraints.", equilibrium, convergence), criterion.ToString()));
                var ratio = point.CalculateWorkingRatio(criterion, force, 1e6, 1000);
                if (!SectionAnalysisInput.Finite(ratio) || ratio < 0) return new SectionResistanceResponse(Diagnostic(CalculationStatus.NotConverged, "Invalid working ratio."), criterion.ToString());
                return new SectionResistanceResponse(new SolverDiagnostics(CalculationStatus.Completed, EngineId, Version,
                    "Returned point satisfies the search constraints.", equilibrium, convergence), criterion.ToString(), SectionStrain.From(point.StrainPlane),
                    point.NRd, point.MxRd, point.MyRd, ratio, point.FailureIndex.ToString());
            }
        }

        /// <summary>
        /// Additional convergence guard for continuous diagrams: the exported strain plane must satisfy the same search constraints.
        /// This does not certify equality of all exported and reintegrated forces along the demand ray. Raw APIs and the discontinuous
        /// stress-block interpolation policy remain unchanged. Every typed export of a searched point uses this guard, including the
        /// constant-eccentricity and unit-moment probes of AssignedAxialForceOutcome. Axial range ends are directly integrated planes.
        /// </summary>
        private ResistanceConvergence ValidateReturnedPointAndPlane(ResultBeamForces demand,
            FailureDomain.FailureDomainPoint point, SectionSolver.FailureAnalysisTypes criterion)
        {
            var solver = _checker.SectionSolver;
            var result = DomainPointConvergence.Evaluate(solver, demand, point, criterion);
            if (!result.Accepted || solver.ConcreteMaterial.CompressionStressStrainDiagram ==
                GPC.Model.Materials.ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock) return result;
            var integrated = solver.GetExternalForces(solver.CalculateForceResultantForDomain(point.StrainPlane),
                solver.SectionOption.ForceReferenceCoordinateSystem);
            // GetExternalForces transports the origin, retaining the solver directions (-X,-Y). Rotate once to the reported frame.
            var forces = new ResultBeamForces(integrated.N, 0, 0, 0, integrated.Mx, integrated.My, solver.SolverAxes)
                .ToCoordinateSystem(solver.SectionOption.ForceReferenceCoordinateSystem);
            var fromPlane = new FailureDomain.FailureDomainPoint(
                new GPC.Checkers.Concrete.Helper.ForceTuple(forces.N, forces.M1, forces.M2),
                point.FailureIndex, point.StrainPlane, point.Immersione);
            var planeResult = DomainPointConvergence.Evaluate(solver, demand, fromPlane, criterion);
            return planeResult.Accepted ? result : planeResult;
        }

        /// <summary>Moment of the point declared for a zero moment demand at constant N: 1 kNm about the first axis of the force reference axes.</summary>
        private const double UnitMomentNmm = 1e6;

        /// <summary>True if the demand has no moment in the solver axes, where the direction of the search at constant N is null.</summary>
        private bool HasNoMomentDemand(ResultBeamForces force)
        {
            var demand = force.ToCoordinateSystemWithEccentricity(_checker.SectionSolver.SolverAxes);
            return demand.M1 == 0 && demand.M2 == 0;
        }

        /// <summary>
        /// Typed outcome of a criterion at assigned N (bug SLU-2 of 15/10/2026), or null to keep the response of the search:
        /// <list type="bullet">
        /// <item>N outside the range [NRd,c; NRd,t] of the domain (<see cref="SectionSolver.CalculateAxialForceRange"/>): completed response
        /// <see cref="SectionResistanceResponse.AxialForceOutsideDomainMode"/> with the point at constant eccentricity of the demand and its
        /// utilization (above 1: the demand is outside a domain that contains the origin); if that search gives no accepted point beyond the demand
        /// (for instance a prestressed section whose domain does not contain the origin), the end of the range on the side of N with utilization
        /// |N / NRd,lim|;</item>
        /// <item>only with <paramref name="zeroMoment"/> (constant N, no moment, no initial strains) and N inside the range: completed response
        /// <see cref="SectionResistanceResponse.ZeroMomentDemandMode"/> with utilization 0 and the point at the assigned N for a moment of 1 kNm
        /// about the first axis of the force reference axes, accepted by the constraints of the search (its demand is in the convergence).</item>
        /// </list>
        /// N inside the range with a moment keeps the response of the search (cases of register F2-17, option A).
        /// </summary>
        private SectionResistanceResponse AssignedAxialForceOutcome(ResultBeamForces force, SectionSolver.FailureAnalysisTypes criterion, bool zeroMoment,
            CancellationToken cancellationToken)
        {
            var solver = _checker.SectionSolver;
            cancellationToken.ThrowIfCancellationRequested();
            var range = solver.CalculateAxialForceRange();
            cancellationToken.ThrowIfCancellationRequested();
            if (range == null) return null;
            var compression = range.Value.Compression; var tension = range.Value.Tension;
            var axes = solver.SectionOption.ForceReferenceCoordinateSystem;
            double n = force.ToCoordinateSystemWithEccentricity(axes).N;
            if (n < compression.NRd || n > tension.NRd)
            {
                var point = solver.CalculateConvergedDomainPoint(force, SectionSolver.FailureAnalysisTypes.ConstantEccentricity, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (point?.StrainPlane != null && new[] { point.NRd, point.MxRd, point.MyRd }.All(SectionAnalysisInput.Finite))
                {
                    var convergence = ValidateReturnedPointAndPlane(force, point, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
                    double ratio = point.CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, force, 1e6, 1000);
                    if (convergence.Accepted && SectionAnalysisInput.Finite(ratio) && ratio > 1)
                        return SectionResistanceResponse.ForAssignedAxialForce(new SolverDiagnostics(CalculationStatus.Completed, EngineId, Version,
                            SectionResistanceResponse.AxialForceOutsideDomainMessage, null, convergence), criterion.ToString(),
                            SectionResistanceResponse.AxialForceOutsideDomainMode, SectionStrain.From(point.StrainPlane), point.NRd, point.MxRd, point.MyRd,
                            ratio, compression.NRd, tension.NRd);
                }
                var end = n < compression.NRd ? compression : tension;
                return SectionResistanceResponse.ForAssignedAxialForce(Diagnostic(CalculationStatus.Completed,
                    SectionResistanceResponse.AxialForceOutsideDomainEndMessage), criterion.ToString(), SectionResistanceResponse.AxialForceOutsideDomainMode,
                    SectionStrain.From(end.StrainPlane), end.NRd, end.MxRd, end.MyRd, Math.Abs(n / end.NRd), compression.NRd, tension.NRd);
            }
            if (!zeroMoment) return null;
            var probe = new ResultBeamForces(n, 0, 0, 0, UnitMomentNmm, 0, (GPC.Geometry.CoordinateSystem)axes.Clone());
            var probePoint = solver.CalculateConvergedDomainPoint(probe, SectionSolver.FailureAnalysisTypes.ConstantN, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (probePoint?.StrainPlane == null || !new[] { probePoint.NRd, probePoint.MxRd, probePoint.MyRd }.All(SectionAnalysisInput.Finite)) return null;
            var probeConvergence = ValidateReturnedPointAndPlane(probe, probePoint, SectionSolver.FailureAnalysisTypes.ConstantN);
            var axial = probeConvergence.Residuals.FirstOrDefault(r => r.Quantity == "N");
            if (!probeConvergence.Accepted || axial == null) return null;
            var equilibrium = new AxialEquilibriumEvidence(force.N, probeConvergence.Capacity.Forces.N, axial.Tolerance);
            if (!equilibrium.Accepted) return null;
            return SectionResistanceResponse.ForAssignedAxialForce(new SolverDiagnostics(CalculationStatus.Completed, EngineId, Version,
                SectionResistanceResponse.ZeroMomentDemandMessage, equilibrium, probeConvergence), criterion.ToString(), SectionResistanceResponse.ZeroMomentDemandMode,
                SectionStrain.From(probePoint.StrainPlane), probePoint.NRd, probePoint.MxRd, probePoint.MyRd, 0, compression.NRd, tension.NRd);
        }
    }
}
