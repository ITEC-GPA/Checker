using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Geometry;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class DetachedCalculationTests
    {
        private static (SectionCheckerModelCode2010 checker, ResultBeamForces force, StandardModelCode2010 standard, ReinforcedConcreteSection section) Setup(bool linear)
        {
            using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "stress-sections.xml"));
            var section = (ReinforcedConcreteSection)ModelArchive.Load(file).BeamProperties["R300x500"];
            var axes = new CoordinateSystem(section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            var standard = new StandardNTC2018Concrete();
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Plastic, linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear, 2, 0, false, 32);
            return (new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options, standard, false),
                new ResultBeamForces(-300000, 0, 0, 0, 120e6, 0, axes), standard, section);
        }
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task DetachedResponseAndAsyncDispatchMatchOriginal(bool linear)
        {
            var f = Setup(linear);
            var original = f.checker.GetTensionAnalysisResult(f.force);
            var asyncResult = await f.checker.GetTensionAnalysisResultAsync(f.force);
            var response = new LegacySectionCalculation(f.checker).Solve(new SectionAnalysisInput(f.force));
            Assert.AreEqual(linear, response.Linear); Assert.AreEqual(linear, asyncResult.LinearElasticAnalysis);
            Assert.AreEqual(original.StrainPlane.ChiX, response.Strain.ChiX, 1e-12);
            Assert.AreEqual(original.StrainPlane.ChiY, asyncResult.StrainPlane.ChiY, 1e-12);
            var context = StressLimitContext.Resolve(f.standard, f.section);
            var expected = StressLimitCheck.Evaluate(original, ServiceabilityCombination.Characteristic);
            Assert.AreEqual(expected.Ratio.Value, StressLimitCheck.Evaluate(response, context, ServiceabilityCombination.Characteristic).Ratio.Value, 1e-12);
            var saved = response.Bars.Select(p => p.Stress).ToArray(); var n = response.Input.Forces.N;
            f.force.N = 123; response.Input.Forces.N = 999;
            f.standard.ServiceabilityStressConcreteCoefficientForCharacteristicCombination = 0.01;
            f.section.Rebars.First().Position.X += 20;
            CollectionAssert.AreEqual(saved, response.Bars.Select(p => p.Stress).ToArray());
            Assert.AreEqual(n, response.Input.Forces.N);
            Assert.AreEqual(expected.Ratio.Value, StressLimitCheck.Evaluate(response, context, ServiceabilityCombination.Characteristic).Ratio.Value, 1e-12);
        }
        [TestMethod]
        public void FailureAndCancellationCannotProducePassingCheck()
        {
            var f = Setup(true); var input = new SectionAnalysisInput(f.force); var solver = new LegacySectionCalculation(f.checker);
            Assert.ThrowsException<OperationCanceledException>(() => solver.Solve(input, new CancellationToken(true)));
            Assert.ThrowsException<OperationCanceledException>(() => solver.SolveResistance(input, new CancellationToken(true)));
            var failed = new SectionResponse(input, new SolverDiagnostics(CalculationStatus.NotConverged, "test", "1"), null, true, 0, 0, null, null);
            Assert.ThrowsException<InvalidOperationException>(() => StressLimitCheck.Evaluate(failed, StressLimitContext.Resolve(f.standard, f.section), ServiceabilityCombination.Characteristic));
            Assert.ThrowsException<ArgumentException>(() => new SectionResponse(input, new SolverDiagnostics(CalculationStatus.Completed, "test", "1"), null, true, 0, 0, null, null));
        }
        [TestMethod]
        public void ResistancePreservesLegacyCriterionAndScale()
        {
            var f = Setup(false); var point = f.checker.CalculateFailureDomainPoint(f.force);
            var response = new LegacySectionCalculation(f.checker).SolveResistance(new SectionAnalysisInput(f.force));
            Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status);
            Assert.AreEqual(point.NRd, response.N.Value, 1e-6);
            Assert.AreEqual(point.CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes.ConstantN, f.force, 1e6, 1000), response.Utilization.Value, 1e-12);
        }
        [TestMethod]
        public void AlternativeSolverFeedsSameMethodWithBoundaryJudgment()
        {
            var f = Setup(true); var context = StressLimitContext.Resolve(f.standard, f.section);
            var strain = new SectionStrain(0, 0, 0, 0, 0); var input = new SectionAnalysisInput(f.force);
            foreach (var ratio in new[] { 0.999999999, 1.0, 1.000000001 })
            {
                var response = new SectionResponse(input, new SolverDiagnostics(CalculationStatus.Completed, "independent-test-engine", "1"), strain, true, 0, 0,
                    new[] { new SectionStressPoint("C1", 0, 0, -context.ConcreteCharacteristic * ratio) },
                    context.SteelCharacteristic.Select(p => new SectionStressPoint(p.Key, 0, 0, 0)));
                var result = StressLimitCheck.Evaluate(response, context, ServiceabilityCombination.Characteristic);
                Assert.AreEqual(ratio, result.Ratio.Value, 1e-14); Assert.AreEqual(ratio <= 1, result.Satisfied.Value);
            }
        }
    }
}
