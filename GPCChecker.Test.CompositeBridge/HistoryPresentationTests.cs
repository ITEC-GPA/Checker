using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass, DoNotParallelize]
public class HistoryPresentationTests
{
    static HBridgeInput Input() { var d = CompositeBridgeApiTests.Input(); return d with { Options = d.Options with { Class4 = false } }; }
    static HBridgeHistoryOptions Settings(bool nonlinear) => new() { MaterialMode = nonlinear ? HistoryMaterialMode.Nonlinear : HistoryMaterialMode.Linear,
        InstantaneousConcrete = nonlinear, WebLayers = 40, FlangeLayers = 4, ConcreteLayers = 24, SubstepsPerPhase = 8 };
    [DataTestMethod, DataRow(false), DataRow(true)]
    public void ProfilesAndDifferencesReconstructEachCommittedState(bool nonlinear)
    {
        var r = HBridgeHistoryResults.Calculate(Input(), Settings(nonlinear));
        foreach (var s in r.Stages)
        {
            foreach (var f in s.GetHistory()!.State.Fibers.Where(f => f.Active && f.ComponentId.StartsWith("Steel.")))
                Assert.AreEqual(f.Stress, s.Contributions.Sum(c => c.SteelStress(f.Fiber.Y)), 1e-7);
            foreach (var p in s.Points) Assert.AreEqual(p.Stress, p.Contributions.Sum(), 1e-7);
        }
    }
    [TestMethod] public void NonlinearResultsDoNotClaimClass4OrAccessoryVerification()
    {
        var r = HBridgeHistoryResults.Calculate(Input(), Settings(true));
        Assert.IsTrue(r.Stages.All(s => s.GetHistory()!.Nonlinear && s.Shear is null && s.Studs is null && s.Points.All(p => p.Utilization is null)));
        Assert.IsTrue(r.Scope.Contains("non verificati"));
    }
    [TestMethod] public void InstantaneousAnalysisKeepsArchivedPhiAndUsesUnitConcreteFactor()
    {
        var d = Input(); var r = HBridgeHistoryResults.Calculate(d, Settings(true));
        Assert.AreEqual(2, d.Phases[1].Phi);
        Assert.AreEqual(2, r.Input.Phases[1].Phi);
        Assert.IsTrue(r.Stages.SelectMany(s => s.GetHistory()!.State.Fibers).All(f => f.ModulusFactor == 1));
        Assert.AreEqual(0, r.Stages[1].Contributions.Last().Phi);
    }
    [TestMethod] public void StrictNonlinearModeRejectsCreep()
    {
        Assert.ThrowsException<NotSupportedException>(() => HBridgeHistoryResults.Calculate(Input(), Settings(true) with { InstantaneousConcrete = false }));
    }
    [TestMethod] public void MoreThanTwentyStatesSurvivePresentationAndEarlierStatesRemainIntact()
    {
        var d = Input() with { Phases = Enumerable.Range(0, 35).Select(i => new BridgePhase { Name = "P" + i, MomentKNm = 10 }).ToArray() };
        var r = HBridgeHistoryResults.Calculate(d, Settings(false));
        Assert.AreEqual(35, r.Stages.Count); Assert.AreEqual(1, r.Stages[0].Contributions.Count); Assert.AreEqual(35, r.Stages.Last().Contributions.Count);
        Assert.AreEqual(35e7, r.Stages.Last().GetHistory()!.State.MomentAtOrigin, .01);
    }
    [TestMethod] public void DeactivationClearsDisplayedConcreteButRetainsDormantMemory()
    {
        var d = Input() with { Phases = [new() { MomentKNm = 500 }, new() { Kind = BridgePhaseKind.ConcreteExcluded }] };
        var s = HBridgeHistoryResults.Calculate(d, Settings(false)).Stages.Last();
        Assert.IsTrue(s.Points.Where(p => p.Material == "CLS").All(p => !p.Active && p.Stress == 0));
        Assert.IsTrue(s.GetHistory()!.State.Fibers.Where(f => f.ComponentId == "Concrete").Any(f => !f.Active && f.Stress != 0));
    }
    [DataTestMethod, DataRow(false), DataRow(true)]
    public void MultipleShrinkagePhasesAccumulateOnlyOnConcrete(bool nonlinear)
    {
        var d = Input() with { Phases = [new() { MomentKNm = 100 }, new() { Kind = BridgePhaseKind.Shrinkage, ShrinkageMicrostrain = -50 }, new() { Kind = BridgePhaseKind.Shrinkage, ShrinkageMicrostrain = -75 }] };
        var r = HBridgeHistoryResults.Calculate(d, Settings(nonlinear));
        foreach (var f in r.Stages.Last().GetHistory()!.State.Fibers)
            Assert.AreEqual(f.ComponentId == "Concrete" ? -125e-6 : 0, f.ImposedStrain, 1e-12);
    }
    [TestMethod] public void ShearIsStoredWithoutChangingNormalStressSolution()
    {
        var d = Input(); var a = HBridgeHistoryResults.Calculate(d, Settings(true));
        var b = HBridgeHistoryResults.Calculate(d with { Phases = d.Phases.Select(p => p with { ShearKN = 100 }).ToArray() }, Settings(true));
        Assert.AreEqual(300000, b.Stages.Last().GetHistory()!.State.V);
        CollectionAssert.AreEqual(a.Stages.Last().Points.Select(p => p.Stress).ToArray(), b.Stages.Last().Points.Select(p => p.Stress).ToArray());
    }
    [TestMethod] public void NonlinearProfileActuallyDiffersFromEndpointChord()
    {
        var d = Input() with { Phases = [new() { ForceKN = -4000, MomentKNm = 6000 }] };
        var r = HBridgeHistoryResults.Calculate(d, Settings(true)); var p = r.Stages.Last().GetHistory()!.Profile;
        double mid = p.Stress("CLS", 125), chord = (p.Stress("CLS", 0) + p.Stress("CLS", 250)) / 2;
        Assert.IsTrue(Math.Abs(mid - chord) > .001, "Concrete nonlinear curve must not become an endpoint straight line.");
    }
    [TestMethod] public void NonlinearMeshRefinementStabilizesCurvature()
    {
        var d = Input() with { Phases = [new() { ForceKN = -4000, MomentKNm = 6000 }] };
        double Curvature(int n) => HBridgeHistoryResults.Calculate(d, Settings(true) with { ConcreteLayers = n, WebLayers = n * 2, FlangeLayers = n / 4 }).Stages.Last().GetHistory()!.State.TotalPlane.Curvature;
        double a = Curvature(32), b = Curvature(128);
        Assert.IsTrue(Math.Abs(a - b) / Math.Abs(b) < .002);
    }
}
