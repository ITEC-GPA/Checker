using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Response;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class ConcretePhaseBatchTests
    {
        private static SectionChecker Checker(bool linear = false, bool asymmetric = false)
        {
            var shape = DomainPointContractTests.Sections.Single(s => s.Name == (asymmetric ? "R3" : "R2"));
            var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(DomainPointContractTests.LocalAxes(section),
                SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
                linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, linear, 32);
            return new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options,
                new StandardNTC2018Concrete { AlphaCC = .85, GammaC = 1.5, GammaS = 1.15 }, false);
        }
        private static ConcretePhaseHistory History(int index, int phases = 6)
        {
            var axes = new CoordinateSystem(Point3d.Origin, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
            var input = Enumerable.Range(0, phases).Select(k => k % 2 == 0
                ? ConcretePhase.Load("load " + k, new ResultBeamForces(-150000 - index * 500, 0, 0, 0, 2e6 + index * 10000, -1e6, axes))
                : ConcretePhase.ShrinkageMicrostrain("shrink " + k, -30)).ToArray();
            return new ConcretePhaseHistory("history " + index, input, new ConcretePhaseOptions(includeIntegrationPoints: index % 2 == 0));
        }
        private static void Equivalent(ConcretePhaseAnalysisResult expected, ConcretePhaseAnalysisResult actual)
        {
            Assert.AreEqual(expected.Stop, actual.Stop); Assert.AreEqual(expected.Phases.Count, actual.Phases.Count);
            Assert.AreEqual(expected.InitialForceEvaluations, actual.InitialForceEvaluations, "No counters leak between reused workers.");
            foreach (var pair in expected.Phases.Zip(actual.Phases, (e, a) => new { e, a }))
            {
                Assert.AreEqual(pair.e.Iterations, pair.a.Iterations);
                Assert.AreEqual(pair.e.State.TotalStrain.Epsilon, pair.a.State.TotalStrain.Epsilon, 1e-12);
                Assert.AreEqual(pair.e.State.TotalStrain.ChiX, pair.a.State.TotalStrain.ChiX, 1e-14);
                Assert.AreEqual(pair.e.State.TotalStrain.ChiY, pair.a.State.TotalStrain.ChiY, 1e-14);
                Assert.AreEqual(pair.e.State.Shrinkage, pair.a.State.Shrinkage);
                Assert.AreEqual(pair.e.State.Points.Count, pair.a.State.Points.Count);
                Assert.IsTrue(pair.a.State.NormalizedResidual <= 1);
                for (int i = 0; i < pair.e.State.Points.Count; i++)
                    Assert.AreEqual(pair.e.State.Points[i].Stress, pair.a.State.Points[i].Stress, 1e-7);
            }
        }

        [DataTestMethod]
        [DataRow(4, 1, false)] [DataRow(5, 2, false)] [DataRow(6, 4, false)] [DataRow(6, 2, true)]
        public void SameSectionBatchMatchesSingleHistoriesAndReusesPreparation(int phases, int degree, bool linear)
        {
            var histories = Enumerable.Range(0, 24).Select(i => History(i, phases)).ToArray();
            int created = 0;
            var batch = ConcretePhaseAnalysis.CalculateBatch(histories, () => { Interlocked.Increment(ref created); return Checker(linear); }, degree);
            Assert.IsTrue(batch.Completed); Assert.AreEqual(histories.Length, batch.Items.Count);
            Assert.IsTrue(created > 0 && created <= degree); Assert.AreEqual(created, batch.PreparedSections);
            Assert.AreEqual(degree, batch.MaxDegreeOfParallelism);
            var checker = Checker(linear);
            for (int i = 0; i < histories.Length; i++)
            {
                Assert.AreEqual(i, batch.Items[i].Index); Assert.AreEqual(histories[i].Name, batch.Items[i].Name);
                Equivalent(ConcretePhaseAnalysis.Calculate(checker, histories[i].Phases, histories[i].Options), batch.Items[i].Result);
            }
        }

        [TestMethod]
        public async Task DifferentSectionsAsyncPreservesOrderAndPerItemConfiguration()
        {
            var requests = Enumerable.Range(0, 12).Select(i => new ConcretePhaseBatchRequest(History(i, 4 + i % 3), () => Checker(i % 2 == 0, i % 3 == 0))).ToArray();
            var batch = await ConcretePhaseAnalysis.CalculateBatchAsync(requests, 3);
            Assert.IsTrue(batch.Completed); Assert.AreEqual(requests.Length, batch.PreparedSections);
            for (int i = 0; i < requests.Length; i++)
            {
                Assert.AreEqual(requests[i].History.Name, batch.Items[i].Name);
                Equivalent(ConcretePhaseAnalysis.Calculate(requests[i].CheckerFactory(), requests[i].History.Phases, requests[i].History.Options), batch.Items[i].Result);
            }
        }

        [TestMethod]
        public void FactoriesOverlapButNeverExceedTheWorkerLimit()
        {
            int active = 0, peak = 0;
            using (var firstTwo = new CountdownEvent(2))
            {
                var requests = Enumerable.Range(0, 8).Select(i => new ConcretePhaseBatchRequest(History(i, 4), () =>
                {
                    int count = Interlocked.Increment(ref active);
                    int previous;
                    do { previous = peak; } while (count > previous && Interlocked.CompareExchange(ref peak, count, previous) != previous);
                    try
                    {
                        if (i < 2) { firstTwo.Signal(); if (!firstTwo.Wait(10000)) throw new TimeoutException("Workers did not overlap."); }
                        return Checker();
                    }
                    finally { Interlocked.Decrement(ref active); }
                })).ToArray();
                var batch = ConcretePhaseAnalysis.CalculateBatch(requests, 2);
                Assert.IsTrue(batch.Completed); Assert.AreEqual(2, peak); Assert.AreEqual(0, active);
            }
        }

        [TestMethod]
        public void FailedItemsDoNotDiscardOtherResults()
        {
            var good = History(0, 4); var checker = Checker();
            var overload = new ConcretePhaseHistory("overload", new[] { ConcretePhase.Load("overload",
                new ResultBeamForces(-1e9, 0, 0, 0, 0, 0, checker.SectionCheckerOptions.ForceReferenceCoordinateSystem)) },
                new ConcretePhaseOptions(maximumSubdivisions: 2));
            var batch = ConcretePhaseAnalysis.CalculateBatch(new[] {
                new ConcretePhaseBatchRequest(good, () => Checker()),
                new ConcretePhaseBatchRequest(good, () => throw new InvalidOperationException("factory failed")),
                new ConcretePhaseBatchRequest(overload, () => Checker()),
                new ConcretePhaseBatchRequest(good, () => null),
                new ConcretePhaseBatchRequest(good, () => Checker()) }, 2);
            Assert.IsFalse(batch.Completed); Assert.IsTrue(batch.Items[0].Completed); Assert.IsTrue(batch.Items[4].Completed);
            Assert.IsNull(batch.Items[1].Result); Assert.AreEqual("factory failed", batch.Items[1].ErrorMessage);
            Assert.IsNotNull(batch.Items[2].Result); Assert.IsFalse(batch.Items[2].Result.Completed); Assert.IsNull(batch.Items[2].ErrorType);
            Assert.IsNull(batch.Items[3].Result); Assert.IsNotNull(batch.Items[3].ErrorType);
        }

        [TestMethod]
        public async Task AsyncSnapshotsInputsAndCancellationIsNotAnItemFailure()
        {
            using (var entered = new ManualResetEventSlim()) using (var proceed = new ManualResetEventSlim())
            {
                var original = History(0, 4); var inputs = new[] { original };
                var task = ConcretePhaseAnalysis.CalculateBatchAsync(inputs, () => { entered.Set(); proceed.Wait(); return Checker(); }, 1);
                Assert.IsTrue(entered.Wait(10000)); inputs[0] = History(99, 6); proceed.Set();
                var batch = await task; Assert.IsTrue(batch.Completed); Assert.AreEqual(original.Name, batch.Items[0].Name);
            }
            using (var cancellation = new CancellationTokenSource())
            {
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await ConcretePhaseAnalysis.CalculateBatchAsync(
                    new[] { History(0) }, () => { cancellation.Cancel(); return Checker(); }, 1, cancellation.Token));
            }
            Assert.ThrowsException<OperationCanceledException>(() => ConcretePhaseAnalysis.CalculateBatch(new ConcretePhaseBatchRequest[0], cancellationToken: new CancellationToken(true)));
        }

        [TestMethod]
        public void EmptyBatchAndInvalidRequestsAreExplicit()
        {
            int calls = 0;
            var result = ConcretePhaseAnalysis.CalculateBatch(new ConcretePhaseHistory[0], () => { calls++; return Checker(); }, 2);
            Assert.IsTrue(result.Completed); Assert.AreEqual(0, calls); Assert.AreEqual(0, result.Items.Count);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ConcretePhaseAnalysis.CalculateBatch(new ConcretePhaseBatchRequest[0], -1));
            Assert.ThrowsException<ArgumentException>(() => ConcretePhaseAnalysis.CalculateBatch(new ConcretePhaseBatchRequest[] { null }));
            Assert.ThrowsException<ArgumentNullException>(() => new ConcretePhaseBatchRequest(History(0), null));
            var tilted = new CoordinateSystem(Point3d.Origin, new Vector3d(Math.Cos(1e-6), 0, Math.Sin(1e-6)), Vector3d.YAxis);
            Assert.ThrowsException<ArgumentException>(() => ConcretePhase.Load("out of plane", new ResultBeamForces(1, 0, 0, 0, 0, 0, tilted)));
            var automatic = ConcretePhaseAnalysis.CalculateBatch(Enumerable.Range(0, 8).Select(i => History(i, 4)), () => Checker());
            Assert.IsTrue(automatic.Completed);
            Assert.AreEqual(Math.Min(4, Environment.ProcessorCount), automatic.MaxDegreeOfParallelism);
        }
    }
}
