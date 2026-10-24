using System;
using System.Linq;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Standards;
using GPC.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class DomainPointFallbackPlaneTests
    {
        // Closed-form forward state: all four bars are in linear elastic tension;
        // concrete is entirely in tension and excluded. The outside fibre of the
        // upper bars reaches fyd/E; the centre stress is still elastic.
        [DataTestMethod]
        [DataRow(-1.0, 0.0, 0.0)]
        [DataRow(1.0, 0.0, 0.0)]
        [DataRow(1.0, 17.0, -23.0)]
        public void AnalyticElasticTensionPlaneRemainsCompletedInPhysicalReferenceFrames(double axisSign, double ox, double oy)
        {
            const double width = 300, height = 500, fck = 25, diameter = 16, es = 200000, epsilon = .0015;
            double area = Math.PI * diameter * diameter / 4;
            double gradientY = (450 / 1.15 / es - epsilon) / (200 + diameter / 2);
            double n = 4 * area * es * epsilon;
            double solverM1 = -4 * area * es * gradientY * 200 * 200;
            // Origin transport and rotation written as statics, without the production force-conversion API.
            double m1 = -axisSign * (solverM1 + n * oy);
            double m2 = -axisSign * (-n * ox);
            var shape = new DomainPointContractTests.BenchSection
            {
                Name = "Elastic all-tension rectangle", Width = width, Height = height, Fck = fck,
                Bars = new[] { new[] { -100.0, -200.0, diameter }, new[] { 100.0, -200.0, diameter },
                    new[] { -100.0, 200.0, diameter }, new[] { 100.0, 200.0, diameter } }
            };
            var section = DomainPointContractTests.BuildSection(shape,
                ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var axes = new CoordinateSystem(new Point3d(ox, oy, 0), new Vector3d(axisSign, 0, 0), new Vector3d(0, axisSign, 0));
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes,
                SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Elastic,
                SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options, new StandardNTC2018Concrete(), false);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            var demand = new ResultBeamForces(n, 0, 0, 0, .7 * m1, .7 * m2, axes);
            var response = new LegacySectionCalculation(checker).SolveResistance(new SectionAnalysisInput(demand));
            Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status, response.Diagnostics.Message);
            Assert.IsTrue(response.Diagnostics.ResistanceConvergence.Accepted);
            // Existing solver nominal tolerances, not newly fitted tolerances or captured capacities.
            var solver = checker.SectionSolver;
            double tn = DomainPointAxialTolerance.ConvergenceTolerance(solver);
            double tm1 = solver.FailureAnalysisDistanceTolerance / solver.ConvertToAdimensionalForces(new ForceTuple(0, 1, 0)).Mx;
            double tm2 = solver.FailureAnalysisDistanceTolerance / solver.ConvertToAdimensionalForces(new ForceTuple(0, 0, 1)).My;
            Assert.AreEqual(n, response.N.Value, tn);
            Assert.AreEqual(m1, response.M1.Value, tm1 + Math.Abs(oy) * tn);
            Assert.AreEqual(m2, response.M2.Value, tm2 + Math.Abs(ox) * tn);
            Assert.AreEqual(1 / .7, Math.Abs(response.M1.Value / demand.M1), (tm1 + Math.Abs(oy) * tn) / Math.Abs(demand.M1));
        }

        // Independent safety condition, not a frozen solver path: if a future physical solver
        // finds a consistent plane, Completed is allowed. Interpolated forces alone cannot prove it.
        [DataTestMethod]
        [DataRow(30.0)]
        [DataRow(210.0)]
        public void SlenderTensionCaseCannotBeCompletedWithAnUnconvergedReturnedPlane(double degrees)
        {
            var shape = new DomainPointContractTests.BenchSection
            {
                Name = "S1", Width = 1000, Height = 200, Fck = 25,
                Bars = Enumerable.Range(0, 5).SelectMany(i => new[]
                {
                    new[] { -400 + 200.0 * i, -60.0, 12.0 },
                    new[] { -400 + 200.0 * i, 60.0, 12.0 }
                }).ToArray()
            };
            var section = DomainPointContractTests.BuildSection(shape,
                ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var checker = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic);
            var solver = checker.SectionSolver;
            var force = DomainPointContractTests.Force(solver.SolverAxes, 221.277,
                Math.Cos(degrees * Math.PI / 180), Math.Sin(degrees * Math.PI / 180));
            var response = new LegacySectionCalculation(checker).SolveResistance(new SectionAnalysisInput(force));

            if (response.Diagnostics.Status == CalculationStatus.Completed)
            {
                // Integrate the actual exposed strain state; do not fetch a second solver candidate.
                var s = response.Strain;
                Assert.IsNotNull(s);
                var plane = s.ToStrainPlane();
                var f = solver.GetExternalForces(solver.CalculateForceResultantForDomain(plane),
                    solver.SectionOption.ForceReferenceCoordinateSystem);
                var reference = new ResultBeamForces(f.N, 0, 0, 0, f.Mx, f.My, solver.SolverAxes)
                    .ToCoordinateSystem(solver.SectionOption.ForceReferenceCoordinateSystem);
                var point = new FailureDomain.FailureDomainPoint(new ForceTuple(reference.N, reference.M1, reference.M2),
                    SectionSolver.FailureZones.F3A, plane, 0);
                var validation = DomainPointConvergence.Evaluate(solver, force, point,
                    SectionSolver.FailureAnalysisTypes.ConstantN);
                string residuals = string.Join("; ", validation.Residuals.Select(r =>
                    r.Quantity + "=" + r.Residual + "/" + r.Tolerance));
                Assert.IsTrue(validation.Accepted, degrees + " degrees: Completed must be backed by its returned plane. " + residuals);
            }
            else
            {
                Assert.AreEqual(CalculationStatus.NotConverged, response.Diagnostics.Status);
                Assert.IsNull(response.N); Assert.IsNull(response.M1); Assert.IsNull(response.M2);
                Assert.IsNull(response.Strain); Assert.IsNull(response.Utilization);
                StringAssert.Contains(response.Diagnostics.Message, "strain plane");
                Assert.IsNotNull(response.Diagnostics.ResistanceConvergence);
                Assert.IsFalse(response.Diagnostics.ResistanceConvergence.Accepted);
            }
        }
    }
}
