using GPC.Checkers.CompositeBridge.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass, DoNotParallelize]
public class HistoryBehaviorTests
{
    internal static HistoryComponent Component(string id, double e, double area = 100, bool active = true, double offset = 0) =>
        new(id, new HistoryElasticLaw(e), [new(id + "L", 0, offset - 100, area), new(id + "H", 0, offset + 100, area)], active);
    internal static HistorySection Steel() => new([Component("steel", 200000)]);
    internal static HistorySection Composite(bool cast = true) => new([Component("steel", 200000), Component("concrete", 100000, 200, cast)]);
    internal static HistoryPhase Shrink(double strain, string id = "concrete") => new HistoryPhase { ImposedStrainIncrements = new Dictionary<string, HistoryStrainPlane> { [id] = new(strain, 0) } };
    internal static HistoryStageResult Last(HistorySection s, params HistoryPhase[] phases) => BridgeHistoryAnalysis.Calculate(s, phases).Stages.Last();
    internal static void Near(double expected, double actual, double tolerance = 1e-9) => Assert.AreEqual(expected, actual, tolerance * Math.Max(1, Math.Abs(expected)));
    internal static void Equilibrium(HistoryStageResult s)
    {
        // Absolute floors and a relative scale in both actions, including pure moment/pure force.
        // Zero resultant does not mean zero cancellation roundoff in stresses integrated over a bridge section.
        double length = Math.Max(1, s.Fibers.Max(f => f.Fiber.Y) - s.Fibers.Min(f => f.Fiber.Y));
        double nt = .0002 + 2e-9 * Math.Max(Math.Abs(s.N), Math.Abs(s.MomentAtOrigin) / length);
        double mt = .02 + 2e-9 * Math.Max(Math.Abs(s.MomentAtOrigin), Math.Abs(s.N) * length);
        Assert.AreEqual(s.N, s.Fibers.Sum(f => f.Stress * f.EffectiveArea), nt);
        Assert.AreEqual(s.MomentAtOrigin, -s.Fibers.Sum(f => f.Stress * f.EffectiveArea * f.Fiber.Y), mt);
        Assert.IsTrue(Math.Abs(s.ForceResidual) <= nt); Assert.IsTrue(Math.Abs(s.MomentResidual) <= mt);
        foreach (var f in s.Fibers.Where(f => f.Active)) Near(f.TotalStrain, f.ActivationStrain + f.ImposedStrain + f.MechanicalStrain, 1e-12);
    }

    [TestMethod] public void AxialLoadMatchesUniformSteelSolution()
    {
        var s = Last(Steel(), new HistoryPhase { DeltaN = 20000 }); Equilibrium(s);
        Near(.0005, s.TotalPlane.AxialStrain, 1e-12); Near(0, s.TotalPlane.Curvature, 1e-12);
        foreach (var f in s.Fibers) Near(100, f.Stress);
    }
    [TestMethod] public void PositiveBendingCompressesUpperFiber()
    {
        var s = Last(Steel(), new HistoryPhase { DeltaM = 1e6 }); Equilibrium(s);
        Near(2.5e-6, s.TotalPlane.Curvature, 1e-12); Near(-50, s.Fibers.Last().Stress);
    }
    [TestMethod] public void EccentricAxialForceIsTransportedToFixedOrigin()
    {
        var s = Last(Steel(), new HistoryPhase { DeltaN = 20000, ApplicationY = 50 }); Equilibrium(s);
        Near(-1e6, s.MomentAtOrigin); Near(150, s.Fibers.Last().Stress); Near(50, s.Fibers.First().Stress);
    }
    [TestMethod] public void ZeroIncrementPreservesPlaneStressesAndMaterialMemory()
    {
        var stages = BridgeHistoryAnalysis.Calculate(Steel(), [new HistoryPhase { DeltaN = 20000, DeltaM = 1e6 }, new()]).Stages;
        Assert.AreEqual(stages[0].TotalPlane, stages[1].TotalPlane);
        foreach (var f in stages[1].Fibers) { Near(0, f.StressIncrement); Near(0, f.MechanicalStrainIncrement); }
    }
    [TestMethod] public void FreshConcreteIsStressFreeOnAlreadyDeformedSteel()
    {
        var r = BridgeHistoryAnalysis.Calculate(Composite(false), [new HistoryPhase { DeltaN = 40000, DeltaM = 1e6 }, new HistoryPhase { Activate = ["concrete"] }]).Stages;
        Assert.AreEqual(r[0].TotalPlane, r[1].TotalPlane);
        foreach (var f in r[1].Fibers.Where(f => f.ComponentId == "concrete")) { Near(0, f.Stress); Near(r[0].TotalPlane.At(f.Fiber.Y), f.ActivationStrain, 1e-12); }
        Equilibrium(r[1]);
    }
    [TestMethod] public void OnlyNewLoadsAreSharedAfterCasting()
    {
        var s = Last(Composite(false), new HistoryPhase { DeltaN = 40000 }, new HistoryPhase { Activate = ["concrete"], DeltaN = 40000 });
        foreach (var f in s.Fibers) Near(f.ComponentId == "steel" ? 300 : 50, f.Stress);
        Near(.0015, s.TotalPlane.AxialStrain, 1e-12); Equilibrium(s);
    }
    [TestMethod] public void ConstructionOrderChangesFinalStressAtEqualExternalLoad()
    {
        var staged = Last(Composite(false), new HistoryPhase { DeltaN = 40000 }, new HistoryPhase { Activate = ["concrete"], DeltaN = 40000 });
        var allAtOnce = Last(Composite(), new HistoryPhase { DeltaN = 80000 });
        Near(staged.N, allAtOnce.N); Near(300, staged.Fibers[0].Stress); Near(200, allAtOnce.Fibers[0].Stress);
    }
    [TestMethod] public void UnloadingAfterCastingLeavesSelfEquilibratedResidualStress()
    {
        var s = Last(Composite(false), new HistoryPhase { DeltaN = 40000 }, new HistoryPhase { Activate = ["concrete"] }, new HistoryPhase { DeltaN = -40000 });
        Near(0, s.N); Near(100, s.Fibers[0].Stress); Near(-50, s.Fibers.Last().Stress); Equilibrium(s);
    }
    [TestMethod] public void ElasticLoadSubdivisionDoesNotChangeFinalState()
    {
        var a = Last(Steel(), new HistoryPhase { DeltaN = 15000, DeltaM = 2e6 });
        var b = Last(Steel(), Enumerable.Range(0, 30).Select(_ => new HistoryPhase { DeltaN = 500, DeltaM = 2e6 / 30 }).ToArray());
        Near(a.TotalPlane.AxialStrain, b.TotalPlane.AxialStrain, 1e-12); Near(a.TotalPlane.Curvature, b.TotalPlane.Curvature, 1e-12);
    }
    [TestMethod] public void FreeUniformShrinkageHasZeroStress()
    {
        var s = Last(new([Component("concrete", 30000)]), Shrink(-.0003)); Equilibrium(s);
        Near(-.0003, s.TotalPlane.AxialStrain, 1e-12); foreach (var f in s.Fibers) Near(0, f.Stress);
    }
    [TestMethod] public void ConcreteShrinkageCompressesSteelAndTensionsConcrete()
    {
        var s = Last(Composite(), Shrink(-.0001)); Equilibrium(s);
        Near(-.00005, s.TotalPlane.AxialStrain, 1e-12); Near(-10, s.Fibers.First().Stress); Near(5, s.Fibers.Last().Stress);
    }
    [TestMethod] public void EccentricSlabShrinkageMatchesIndependentTwoByTwoEquilibrium()
    {
        var section = new HistorySection([Component("steel", 200000), Component("concrete", 30000, 200, offset: 300)]);
        var s = Last(section, Shrink(-.0002));
        var p = section.Components.SelectMany(c => c.Fibers.Select(f => (EA: c.Material.InitialModulus * f.Area, f.Y, Eigen: c.Id == "concrete" ? -.0002 : 0))).ToArray();
        double a = p.Sum(x => x.EA), b = -p.Sum(x => x.EA * x.Y), d = p.Sum(x => x.EA * x.Y * x.Y);
        double fn = p.Sum(x => x.EA * x.Eigen), fm = -p.Sum(x => x.EA * x.Y * x.Eigen), det = a * d - b * b;
        Near((d * fn - b * fm) / det, s.TotalPlane.AxialStrain, 1e-12);
        Near((a * fm - b * fn) / det, s.TotalPlane.Curvature, 1e-12); Equilibrium(s);
    }
    [TestMethod] public void MultipleShrinkagesAreIncrementalAndAdditive()
    {
        var split = Last(Composite(), Shrink(-.0001), Shrink(-.0002));
        var total = Last(Composite(), Shrink(-.0003));
        Near(total.TotalPlane.AxialStrain, split.TotalPlane.AxialStrain, 1e-12);
        foreach (var f in split.Fibers.Where(f => f.ComponentId == "concrete")) Near(-.0003, f.ImposedStrain, 1e-12);
    }
    [TestMethod] public void ShrinkageAndLoadingInterleaveWithoutResettingReferences()
    {
        var r = BridgeHistoryAnalysis.Calculate(Composite(false), [new HistoryPhase { DeltaN = 10000 }, new HistoryPhase { Activate = ["concrete"] }, Shrink(-.0001),
            new HistoryPhase { DeltaM = 2e6 }, Shrink(-.0002)]).Stages;
        foreach (var s in r) Equilibrium(s);
        Near(r[1].Fibers.Last().ActivationStrain, r.Last().Fibers.Last().ActivationStrain, 1e-12);
        Near(-.0003, r.Last().Fibers.Last().ImposedStrain, 1e-12);
    }
    [TestMethod] public void ChangingEffectiveModulusWithoutActionDoesNotRewriteHistory()
    {
        var r = BridgeHistoryAnalysis.Calculate(Composite(), [new HistoryPhase { DeltaN = 80000 },
            new HistoryPhase { ModulusFactors = new Dictionary<string, double> { ["concrete"] = .25 } }]).Stages;
        Near(r[0].Fibers.Last().Stress, r[1].Fibers.Last().Stress);
        Assert.AreEqual(r[0].TotalPlane, r[1].TotalPlane);
    }
    [TestMethod] public void ModulusFactorActsOnNewIncrementAndPersists()
    {
        var r = BridgeHistoryAnalysis.Calculate(Composite(), [new HistoryPhase { DeltaN = 80000 },
            new HistoryPhase { DeltaN = 50000, ModulusFactors = new Dictionary<string, double> { ["concrete"] = .25 } }, new HistoryPhase { DeltaN = 50000 }]).Stages;
        // EA steel=40e6, EA concrete(new)=10e6; each increment produces epsilon=0.001.
        Near(.001, r[1].IncrementPlane.AxialStrain, 1e-12); Near(.001, r[2].IncrementPlane.AxialStrain, 1e-12);
        Near(25, r[1].Fibers.Last().StressIncrement); Near(25, r[2].Fibers.Last().StressIncrement);
    }
    [TestMethod] public void MoreThanOneThousandPhasesAndShrinkagesAreAccepted()
    {
        var r = BridgeHistoryAnalysis.Calculate(Composite(), Enumerable.Range(0, 1201).Select(_ => Shrink(-1e-7)));
        Assert.AreEqual(1201, r.Stages.Count); Near(-1201e-7, r.Stages.Last().Fibers.Last().ImposedStrain, 1e-12); Equilibrium(r.Stages.Last());
    }
    [TestMethod] public void StreamingDoesNotEnumerateFuturePhases()
    {
        int reads = 0;
        IEnumerable<HistoryPhase> Generate() { while (true) { reads++; yield return new HistoryPhase { DeltaN = 1 }; } }
        var s = BridgeHistoryAnalysis.Enumerate(Steel(), Generate()).Take(151).Last();
        Assert.AreEqual(151, reads); Near(151, s.N);
    }
    [TestMethod] public void DeactivationRedistributesCommittedStressAndReactivationRetainsBirthReference()
    {
        var r = BridgeHistoryAnalysis.Calculate(Composite(false), [new HistoryPhase { DeltaN = 20000 }, new HistoryPhase { Activate = ["concrete"], DeltaN = 20000 },
            new HistoryPhase { Deactivate = ["concrete"] }, new HistoryPhase { Activate = ["concrete"] }]).Stages;
        Near(200, r[2].Fibers.First().Stress); Near(0, r[2].Fibers.Last().EffectiveArea);
        Near(r[1].Fibers.Last().ActivationStrain, r[3].Fibers.Last().ActivationStrain, 1e-12);
        Near(r[1].Fibers.Last().Stress, r[3].Fibers.Last().Stress); Equilibrium(r[3]);
    }
    [TestMethod] public void InvalidPhaseReferencesAndInactiveShrinkageAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(() => Last(Steel(), new HistoryPhase { Activate = ["unknown"] }));
        Assert.ThrowsException<ArgumentException>(() => Last(Composite(false), Shrink(-.001)));
        Assert.ThrowsException<ArgumentException>(() => Last(Steel(), new HistoryPhase { DeltaN = double.NaN }));
    }
    [TestMethod] public void CancellationStopsTheNextStageAndLeavesReturnedSnapshotUntouched()
    {
        using var token = new CancellationTokenSource();
        using var e = BridgeHistoryAnalysis.Enumerate(Steel(), [new HistoryPhase { DeltaN = 20000 }, new HistoryPhase { DeltaN = 10000 }], cancellation: token.Token).GetEnumerator();
        Assert.IsTrue(e.MoveNext()); var first = e.Current; token.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => e.MoveNext()); Near(100, first.Fibers[0].Stress);
    }
    [TestMethod] public void DifferentComponentsKeepIndependentImposedStrainHistories()
    {
        var s = Last(Composite(), Shrink(-.0002), Shrink(.0001, "steel"));
        Near(.0001, s.Fibers.First().ImposedStrain, 1e-12); Near(-.0002, s.Fibers.Last().ImposedStrain, 1e-12); Equilibrium(s);
    }
    [TestMethod] public void LaterCentroidDoesNotMovePreviouslyAppliedAxialLoad()
    {
        var section = new HistorySection([Component("steel", 200000), Component("concrete", 100000, 200, false, 300)]);
        var r = BridgeHistoryAnalysis.Calculate(section, [new() { DeltaN = 40000, ApplicationY = 30 },
            new() { Activate = ["concrete"], DeltaN = 10000, LoadReference = HistoryLoadReference.GrossElasticCentroid }]).Stages;
        Near(150, r[1].IncrementApplicationY); Near(-40000 * 30 - 10000 * 150, r[1].MomentAtOrigin); Equilibrium(r[1]);
    }
    [TestMethod] public void FreeEigenstrainGradientProducesItsOwnCurvatureWithoutStress()
    {
        var eigenstrain = new HistoryStrainPlane(-.0002, 1e-6);
        var s = Last(Steel(), new HistoryPhase { ImposedStrainIncrements = new Dictionary<string, HistoryStrainPlane> { ["steel"] = eigenstrain } });
        Near(eigenstrain.AxialStrain, s.TotalPlane.AxialStrain, 1e-12); Near(eigenstrain.Curvature, s.TotalPlane.Curvature, 1e-12);
        foreach (var f in s.Fibers) Near(0, f.Stress); Equilibrium(s);
    }
    [TestMethod] public void ResultSnapshotsDoNotShareMutablePhaseCollections()
    {
        var factors = new Dictionary<string, double> { ["steel"] = .5 };
        var s = Last(Steel(), new HistoryPhase { DeltaN = 10000, ModulusFactors = factors });
        factors["steel"] = .01;
        Near(.5, s.AppliedPhase.ModulusFactors["steel"]); Near(.5, s.Fibers[0].ModulusFactor);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<HistoryFiberResult>)s.Fibers)[0] = s.Fibers[0]);
    }
}
