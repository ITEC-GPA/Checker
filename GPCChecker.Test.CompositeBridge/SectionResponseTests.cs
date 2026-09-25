using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass, DoNotParallelize]
public class SectionResponseTests
{
    internal static HistorySection Rectangle(IHistoryMaterialLaw? law = null, double bottom = -100, int layers = 80) =>
        new([new("steel", law ?? new HistoryElasticLaw(200000), HBridgeHistoryAnalysis.Rectangle("steel", 0, bottom, 100, 200, layers), true)]);
    static SectionResponseOptions MC(double n = 0) => new() { AxialForce = n, SubstepsPerTarget = 1 };
    static SectionResponseOptions NE(double k = 0, double y = 0) => new() { Control = SectionResponseControl.AxialForceStrain, FixedCurvature = k, ReferenceY = y, SubstepsPerTarget = 1 };
    static SectionResponsePoint Last(SectionResponseResult r) { Assert.IsTrue(r.Completed, r.Message); return r.Points.Last(); }
    static void Near(double expected, double actual, double tolerance = 1e-7) => Assert.AreEqual(expected, actual, tolerance);

    [TestMethod] public void ElasticMomentCurvatureHasExactEI()
    {
        var p = Last(BridgeSectionResponseAnalysis.Calculate(Rectangle(), [1e-6], MC()));
        double ei = 200000d * 100 * Math.Pow(200, 3) / 12;
        Near(ei * 1e-6, p.MomentAtReference, 1e-6); Near(ei, p.BendingTangentAtConstantN!.Value, .01); Near(0, p.State.IntegratedN);
    }
    [TestMethod] public void ElasticAxialForceStrainHasExactEA()
    {
        var p = Last(BridgeSectionResponseAnalysis.Calculate(Rectangle(), [.001], NE()));
        Near(4e6, p.State.N); Near(4e9, p.AxialTangent); Near(0, p.MomentAtReference, 1e-6);
    }
    [TestMethod] public void EccentricReferenceTransformsBothStrainAndMoment()
    {
        var p = Last(BridgeSectionResponseAnalysis.Calculate(Rectangle(bottom: 200), [2e-6], MC(1e6) with { ReferenceY = 280 }));
        Near(.00025, p.State.TotalPlane.At(300), 1e-12);
        Near(.00029, p.ReferenceStrain, 1e-12);
        Near(200000d * 100 * Math.Pow(200, 3) / 12 * 2e-6 - 1e6 * 20, p.MomentAtReference, 1e-6);
    }
    [TestMethod] public void AxialCurveAtNonzeroCurvatureReportsTheReactionMoment()
    {
        var p = Last(BridgeSectionResponseAnalysis.Calculate(Rectangle(), [.0002], NE(1e-6, 50)));
        Near(.00025, p.State.TotalPlane.AxialStrain, 1e-12); Near(1e6, p.State.N);
        Near(200000d * 100 * Math.Pow(200, 3) / 12 * 1e-6 + 1e6 * 50, p.MomentAtReference, 1e-6);
    }
    [TestMethod] public void PrescribedStrainTraversesPerfectPlasticPlateau()
    {
        var p = Last(BridgeSectionResponseAnalysis.Calculate(Rectangle(new HistoryBilinearSteelLaw(200000, 250)), [.01], NE()));
        Near(5e6, p.State.N); Near(0, p.AxialTangent); Assert.IsTrue(p.State.Fibers.All(f => ((HistoryPlasticState)f.MaterialState).PlasticStrain > 0));
    }
    [TestMethod] public void PerfectPlasticRectangleApproachesAnalyticalPlasticMoment()
    {
        var p = Last(BridgeSectionResponseAnalysis.Calculate(Rectangle(new HistoryBilinearSteelLaw(200000, 250), layers: 600), [2e-4], MC()));
        double ky = 250d / 200000 / 100, mp = 250d * 100 * 200 * 200 / 4;
        Near(mp * (1 - ky * ky / (3 * 2e-4 * 2e-4)), p.MomentAtReference, mp * 2e-6);
    }
    [TestMethod] public void PlasticUnloadingLeavesResidualStrainAtZeroForce()
    {
        var r = BridgeSectionResponseAnalysis.Calculate(Rectangle(new HistoryBilinearSteelLaw(200000, 250)), [.005, .00375], NE());
        var p = Last(r); Near(0, p.State.N); Near(.00375, p.State.Fibers[0].MaterialState is HistoryPlasticState s ? s.PlasticStrain : -1, 1e-12);
    }
    [TestMethod] public void PlasticReverseLoadingPreservesAndIncreasesAccumulatedPlasticStrain()
    {
        var r = BridgeSectionResponseAnalysis.Calculate(Rectangle(new HistoryBilinearSteelLaw(200000, 250, 2000)), [.005, -.005], NE());
        var a = (HistoryPlasticState)r.Points[1].State.Fibers[0].MaterialState; var b = (HistoryPlasticState)Last(r).State.Fibers[0].MaterialState;
        Assert.IsTrue(b.PlasticStrain < 0 && b.AccumulatedPlasticStrain > a.AccumulatedPlasticStrain);
    }
    [TestMethod] public void ResumeFromPlasticHistoryPreservesItsStateAndDoesNotMutateIt()
    {
        var section = Rectangle(new HistoryBilinearSteelLaw(200000, 250, 2000));
        var h = BridgeHistoryAnalysis.Calculate(section, [new HistoryPhase { DeltaN = 6e6 }]).Stages[0];
        var state = h.Fibers[0].MaterialState;
        var p = Last(BridgeSectionResponseAnalysis.Calculate(section, [h.TotalPlane.AxialStrain - .0015], NE(), h));
        Near(0, p.State.N, .001); Assert.AreSame(state, h.Fibers[0].MaterialState); Near(300, state.Stress);
    }
    [TestMethod] public void ResumeRetainsCastingAndRepeatedShrinkageReferences()
    {
        var section = new HistorySection([Rectangle().Components[0], new("slab", new HistoryElasticLaw(30000), HBridgeHistoryAnalysis.Rectangle("slab", 0, 100, 300, 50, 10))]);
        var h = BridgeHistoryAnalysis.Calculate(section, [new HistoryPhase { DeltaM = 1e7 }, new HistoryPhase { Activate = ["slab"], ImposedStrainIncrements = new Dictionary<string, HistoryStrainPlane> { ["slab"] = new(-.0001, 0) } }, new HistoryPhase { ImposedStrainIncrements = new Dictionary<string, HistoryStrainPlane> { ["slab"] = new(-.0001, 0) } }]).Stages.Last();
        var p = Last(BridgeSectionResponseAnalysis.Calculate(section, [h.TotalPlane.Curvature], new() { SubstepsPerTarget = 1 }, h));
        foreach (var f in p.State.Fibers) { var old = h.Fibers.Single(a => a.Fiber.Id == f.Fiber.Id); Near(old.Stress, f.Stress, 1e-6); Near(old.ActivationStrain, f.ActivationStrain, 1e-12); Near(old.ImposedStrain, f.ImposedStrain, 1e-12); }
    }
    [TestMethod] public void DormantConcreteStaysDormantAfterResume()
    {
        var section = new HistorySection([Rectangle().Components[0], new("slab", new HistoryElasticLaw(30000), [new("slab.0", 0, 200, 100)])]);
        var h = BridgeHistoryAnalysis.Calculate(section, [new HistoryPhase { DeltaM = 1e6 }]).Stages[0];
        var p = Last(BridgeSectionResponseAnalysis.Calculate(section, [2e-6], MC(), h));
        Assert.IsFalse(p.State.Fibers.Last().Active); Near(0, p.State.Fibers.Last().EffectiveArea);
    }
    [TestMethod] public void MaterialDomainStopsWithoutAcceptingFailedTarget()
    {
        var r = BridgeSectionResponseAnalysis.Calculate(Rectangle(new HistoryBilinearSteelLaw(200000, 250, 0, .01)), [.002, .02], NE() with { MaximumSubdivisions = 6 });
        Assert.AreEqual(SectionResponseStop.MaterialDomain, r.Stop); Assert.AreEqual(1, r.FailedTargetIndex);
        Assert.IsTrue(r.Points.All(p => p.ReferenceStrain <= .01)); Assert.IsFalse(r.Points.Last().ReachedTarget);
    }
    [TestMethod] public void ImpossibleAxialPreloadIsExplicitlyIncomplete()
    {
        var r = BridgeSectionResponseAnalysis.Calculate(Rectangle(new HistoryBilinearSteelLaw(200000, 250)), [1e-5], MC(6e6) with { MaximumSubdivisions = 2 });
        Assert.IsFalse(r.Completed); Assert.AreEqual(-1, r.FailedTargetIndex); Assert.IsTrue(r.Points.All(p => p.State.N <= 5e6));
    }
    [TestMethod] public void InputGeometryMismatchRejectsResume()
    {
        var h = BridgeHistoryAnalysis.Calculate(Rectangle(), [new HistoryPhase()]).Stages[0];
        Assert.ThrowsException<ArgumentException>(() => BridgeSectionResponseAnalysis.Calculate(Rectangle(bottom: 0), [1e-6], MC(), h));
    }
    private sealed class Reduction : IHistoryEffectiveAreaModel { public HistoryEffectiveAreaResponse Evaluate(IReadOnlyList<HistoryFiberResult> f) => new(f.Select(_ => .5).ToArray(), []); }
    [TestMethod] public void EffectiveAreaModelIsRejectedExplicitly() => Assert.ThrowsException<NotSupportedException>(() => BridgeSectionResponseAnalysis.Calculate(Rectangle() with { EffectiveAreaModel = new Reduction() }, [1e-6]));
    [TestMethod] public void NegativeCurvatureProducesOppositeMomentForVirginSymmetricSection()
    {
        var a = Last(BridgeSectionResponseAnalysis.Calculate(Rectangle(new HistoryBilinearSteelLaw(200000, 250)), [3e-5], MC()));
        var b = Last(BridgeSectionResponseAnalysis.Calculate(Rectangle(new HistoryBilinearSteelLaw(200000, 250)), [-3e-5], MC())); Near(-a.MomentAtReference, b.MomentAtReference, 1e-6);
    }
    [TestMethod] public void ThereIsNoFixedLimitOnTargetCount()
    { var r = BridgeSectionResponseAnalysis.Calculate(Rectangle(), Enumerable.Range(1, 350).Select(i => i * 1e-9), MC()); Assert.IsTrue(r.Completed); Assert.AreEqual(351, r.Points.Count); }
    [TestMethod] public void CancellationIsHonoured() => Assert.ThrowsException<OperationCanceledException>(() => BridgeSectionResponseAnalysis.Calculate(Rectangle(), [1e-6], cancellation: new(true)));
    [DataTestMethod] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void NonfiniteTargetsAreRejected(double target) => Assert.ThrowsException<ArgumentException>(() => BridgeSectionResponseAnalysis.Calculate(Rectangle(), [target]));
    [TestMethod] public void EmptyTargetsAreRejected() => Assert.ThrowsException<ArgumentException>(() => BridgeSectionResponseAnalysis.Calculate(Rectangle(), []));
    [TestMethod] public void IncompatibleConstraintsAreRejected() => Assert.ThrowsException<ArgumentException>(() => BridgeSectionResponseAnalysis.Calculate(Rectangle(), [1e-6], MC() with { FixedCurvature = 0 }));
    [TestMethod] public void HAdapterStartsVirginOrReplaysSelectedHistoryWithoutChangingInput()
    {
        var input = CompositeBridgeApiTests.Input() with { Options = new() { Class4 = false } };
        var settings = new HBridgeHistoryOptions { MaterialMode = HistoryMaterialMode.Nonlinear, InstantaneousConcrete = true, SubstepsPerPhase = 8 };
        var virgin = HBridgeSectionResponse.Calculate(input, [1e-7], MC(), settings);
        var history = HBridgeSectionResponse.Calculate(input, [1e-7], MC(), settings, 0);
        Assert.IsTrue(virgin.Points[0].State.Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete).All(f => f.Active));
        Assert.IsTrue(history.Points[0].State.Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete).All(f => !f.Active));
        Near(2, input.Phases[1].Phi);
    }
}
