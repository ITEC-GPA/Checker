using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    /// <summary>
    /// Intersection strategy (chosen by the user in ANTHEA, also the last fallback of the solver). Its forces are the intersection of the demand
    /// ray with the discretized domain; its strain plane is interpolated between the planes of the vertices of the mesh face and does not integrate
    /// to those forces. The strain-plane guard of 62ecbc83/c611b709 (Concrete 0.20.0.0) rejected these points: 329 of the 480 forces of this grid
    /// were NotConverged (ANTHEA grid 12d: 164 of 300) and verified sections became incomplete. Since 24/10/2026 (Concrete 0.20.0.1) an
    /// intersection point keeps the check of the point alone, as before 62ecbc83; directly searched points keep the plane guard
    /// (DomainPointFallbackPlaneTests).
    /// </summary>
    [TestClass]
    public class DomainPointIntersectionPlaneTests
    {
        /// <summary>Largest relative error of the utilization admitted for the intersection method in FailureDomainIntesectVsDirect</summary>
        private const double IntersectionError = 0.09;

        private static SectionCheckerModelCode2010 Checker(ReinforcedConcreteSection section, SectionSolver.FailureAnalysisTypes criterion,
            SectionSolver.DomainPointStrategyTypes strategy, CoordinateSystem axes = null)
        {
            var standard = new StandardNTC2018Concrete { AlphaCC = 0.85, GammaC = 1.5, GammaS = 1.15 };
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes ?? DomainPointContractTests.LocalAxes(section), criterion,
                SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
            checker.SetDomainPointStrategy(strategy);
            return checker;
        }

        /// <summary>R1 of the F2.1 bench (300 x 500, 3Ø20 + 2Ø16, C30/37), parabola-rectangle, ULS</summary>
        private static ReinforcedConcreteSection Section() =>
            DomainPointContractTests.BuildSection(DomainPointContractTests.Sections[0], ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);

        /// <summary>Four axial forces from tension to compression, twelve directions, two moments (kN, kNm)</summary>
        private static IEnumerable<ResultBeamForces> Grid(ReinforcedConcreteSection section) =>
            from n in new double[] { 250, 0, -500, -1500 }
            from degrees in Enumerable.Range(0, 12).Select(i => 30.0 * i)
            from m in new double[] { 60, 200 }
            select DomainPointContractTests.Force(DomainPointContractTests.LocalAxes(section), n,
                m * Math.Cos(degrees * Math.PI / 180), m * Math.Sin(degrees * Math.PI / 180));

        /// <summary>The point with the forces of its strain plane, integrated by the solver and reported in the force reference axes</summary>
        private static FailureDomain.FailureDomainPoint Reintegrated(SectionSolver solver, FailureDomain.FailureDomainPoint point)
        {
            var f = solver.GetExternalForces(solver.CalculateForceResultantForDomain(point.StrainPlane), solver.SectionOption.ForceReferenceCoordinateSystem);
            var r = new ResultBeamForces(f.N, 0, 0, 0, f.Mx, f.My, solver.SolverAxes).ToCoordinateSystem(solver.SectionOption.ForceReferenceCoordinateSystem);
            return new FailureDomain.FailureDomainPoint(new ForceTuple(r.N, r.M1, r.M2), point.FailureIndex, point.StrainPlane, point.Immersione);
        }

        [DataTestMethod]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantEccentricity)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantN)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantMxMy)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantNMx)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantNMy)]
        public void IntersectionPointOnTheDemandRayIsCompleted(SectionSolver.FailureAnalysisTypes criterion)
        {
            var section = Section();
            var intersection = Checker(section, criterion, SectionSolver.DomainPointStrategyTypes.Intersection);
            var direct = Checker(section, criterion, SectionSolver.DomainPointStrategyTypes.Iterative);
            var solver = intersection.SectionSolver;
            int onRay = 0, planeOffRay = 0;
            foreach (var force in Grid(section))
            {
                string id = criterion + " N=" + force.N + " M1=" + force.M1 + " M2=" + force.M2;
                var point = solver.CalculateConvergedDomainPoint(force, criterion, CancellationToken.None);
                // Only intersection points that satisfy the search constraints with their own forces (the check before 62ecbc83).
                if (point?.StrainPlane == null || !point.InterpolatedStrainPlane || !DomainPointConvergence.Evaluate(solver, force, point, criterion).Accepted)
                    continue;
                onRay++;
                if (!DomainPointConvergence.Evaluate(solver, force, Reintegrated(solver, point), criterion).Accepted) planeOffRay++;
                var response = new LegacySectionCalculation(intersection).SolveResistance(new SectionAnalysisInput(force));
                Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status, id + ": " + response.Diagnostics.Message);
                Assert.IsTrue(response.Diagnostics.ResistanceConvergence.Accepted, id);
                Assert.AreEqual(point.NRd, response.N.Value, id); Assert.AreEqual(point.MxRd, response.M1.Value, id); Assert.AreEqual(point.MyRd, response.M2.Value, id);
                if (criterion != SectionSolver.FailureAnalysisTypes.ConstantEccentricity && criterion != SectionSolver.FailureAnalysisTypes.ConstantN) continue;
                // On the domain within the accuracy of the strategy: the face is a chord of the convex domain, so the utilization is on the safe
                // side of the direct search (1e-3 for its stopping tolerances) and within the error admitted for the intersection method.
                var reference = new LegacySectionCalculation(direct).SolveResistance(new SectionAnalysisInput(force));
                Assert.AreEqual(CalculationStatus.Completed, reference.Diagnostics.Status, id + " (direct): " + reference.Diagnostics.Message);
                double ratio = response.Utilization.Value / reference.Utilization.Value;
                Assert.IsTrue(ratio >= 1 - 1e-3 && ratio <= 1 + IntersectionError, id + ": intersection/direct utilization " + ratio);
            }
            // The grid exercises the case: intersection points on the ray whose interpolated plane integrates off the ray.
            Assert.IsTrue(onRay >= 30, criterion + ": intersection points on the ray " + onRay);
            Assert.IsTrue(planeOffRay > 0, criterion + ": interpolated planes off the ray " + planeOffRay);
        }

        /// <summary>Force reference axes +X, +Y: the point rotated from the solver axes keeps its origin in the intersection strategy</summary>
        [TestMethod]
        public void IntersectionPointInRotatedForceAxesIsCompleted()
        {
            var section = Section();
            var axes = new CoordinateSystem(section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            var checker = Checker(section, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.DomainPointStrategyTypes.Intersection, axes);
            foreach (double degrees in Enumerable.Range(0, 12).Select(i => 30.0 * i))
            {
                var force = new ResultBeamForces(-500e3, 0, 0, 0, 60e6 * Math.Cos(degrees * Math.PI / 180), 60e6 * Math.Sin(degrees * Math.PI / 180), axes);
                var response = new LegacySectionCalculation(checker).SolveResistance(new SectionAnalysisInput(force));
                Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status, degrees + " degrees: " + response.Diagnostics.Message);
                Assert.IsTrue(response.Diagnostics.ResistanceConvergence.Accepted, degrees + " degrees");
            }
        }

        /// <summary>The constant-eccentricity probe of an assigned axial force beyond the range (bug SLU-2) keeps its intersection point</summary>
        [TestMethod]
        public void AxialForceBeyondTheRangeKeepsTheIntersectionPoint()
        {
            var section = Section();
            var checker = Checker(section, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.DomainPointStrategyTypes.Intersection);
            var local = DomainPointContractTests.LocalAxes(section);
            foreach (var direction in DomainPointContractTests.Directions)
            {
                // Beyond the centred compression resistance: the point at constant eccentricity of the demand, utilization above 1.
                var beyond = new LegacySectionCalculation(checker).SolveResistance(new SectionAnalysisInput(
                    DomainPointContractTests.Force(local, -5000, direction.Mx, direction.My)));
                string id = "N=-5000 kN " + direction.Label;
                Assert.AreEqual(CalculationStatus.Completed, beyond.Diagnostics.Status, id);
                Assert.IsTrue(beyond.IsAxialForceOutsideDomain, id);
                Assert.AreEqual(SectionResistanceResponse.AxialForceOutsideDomainMessage, beyond.Diagnostics.Message, id);
                Assert.IsTrue(beyond.Utilization.Value > 1, id);
            }
        }
    }
}
