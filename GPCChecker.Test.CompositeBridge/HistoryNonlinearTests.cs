using GPC.Checkers.CompositeBridge.History;
using GPC.Model.Data.Concrete;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static CompositeBridgeTests.HistoryBehaviorTests;

namespace CompositeBridgeTests;

[TestClass, DoNotParallelize]
public class HistoryNonlinearTests
{
    static HistorySection Plastic(double tangent = 10000) => new([Component("steel", 200000) with { Material = new HistoryBilinearSteelLaw(200000, 200, tangent) }]);
    [TestMethod] public void PlasticLoadingAndElasticUnloadingLeavePermanentStrain()
    {
        var r = BridgeHistoryAnalysis.Calculate(Plastic(), [new HistoryPhase { DeltaN = 60000 }, new HistoryPhase { DeltaN = -60000 }]).Stages;
        Near(.011, r[0].TotalPlane.AxialStrain, 1e-12); Near(.0095, r[1].TotalPlane.AxialStrain, 1e-12);
        foreach (var f in r[1].Fibers) { Near(0, f.Stress); Near(.0095, ((HistoryPlasticState)f.MaterialState).PlasticStrain, 1e-12); }
        Equilibrium(r[0]); Equilibrium(r[1]);
    }
    [TestMethod] public void PlasticMemorySurvivesReverseLoading()
    {
        var r = BridgeHistoryAnalysis.Calculate(Plastic(), [new HistoryPhase { DeltaN = 60000 }, new HistoryPhase { DeltaN = -140000, Substeps = 8 }]).Stages;
        var first = (HistoryPlasticState)r[0].Fibers[0].MaterialState;
        var last = (HistoryPlasticState)r[1].Fibers[0].MaterialState;
        Assert.IsTrue(last.AccumulatedPlasticStrain > first.AccumulatedPlasticStrain);
        Assert.IsTrue(last.PlasticStrain < first.PlasticStrain); Equilibrium(r[1]);
    }
    [TestMethod] public void MonotonicPlasticResponseIsIndependentOfSubsteps()
    {
        var a = Last(Plastic(), new HistoryPhase { DeltaN = 60000 });
        var b = Last(Plastic(), new HistoryPhase { DeltaN = 60000, Substeps = 25 });
        Near(a.TotalPlane.AxialStrain, b.TotalPlane.AxialStrain, 1e-12);
        Near(((HistoryPlasticState)a.Fibers[0].MaterialState).PlasticStrain, ((HistoryPlasticState)b.Fibers[0].MaterialState).PlasticStrain, 1e-12);
    }
    [TestMethod] public void FailureBeyondPlasticCapacityDoesNotCommitTheFailedStage()
    {
        using var e = BridgeHistoryAnalysis.Enumerate(Plastic(0), [new HistoryPhase { DeltaN = 20000 }, new HistoryPhase { Name = "Beyond capacity", DeltaN = 60000 }]).GetEnumerator();
        Assert.IsTrue(e.MoveNext()); var valid = e.Current;
        var ex = Assert.ThrowsException<HistoryConvergenceException>(() => e.MoveNext());
        Assert.AreSame(valid, ex.LastCompletedStage); Near(100, valid.Fibers[0].Stress); Assert.AreEqual(1, ex.PhaseIndex);
    }
    [TestMethod] public void ModelConcreteEnvelopeFindsKnownNonlinearStrainPlane()
    {
        var material = ConcreteMaterialEN1992Data.C35_45;
        var c = Component("concrete", material.ElasticModulusCompression) with { Material = new HistoryModelEnvelopeLaw(material) };
        var expected = new HistoryStrainPlane(-.00075, 2.5e-6);
        double n = c.Fibers.Sum(f => f.Area * material.GetStress(expected.At(f.Y)));
        double m = -c.Fibers.Sum(f => f.Area * f.Y * material.GetStress(expected.At(f.Y)));
        var s = Last(new([c]), new HistoryPhase { DeltaN = n, DeltaM = m, Substeps = 8 }); Equilibrium(s);
        Near(expected.AxialStrain, s.TotalPlane.AxialStrain, 1e-10); Near(expected.Curvature, s.TotalPlane.Curvature, 1e-12);
    }
    [TestMethod] public void ModelEnvelopeHasExplicitDomainFailureInsteadOfFalseZeroResistance()
    {
        var law = new HistoryModelEnvelopeLaw(ConcreteMaterialEN1992Data.C35_45);
        Assert.ThrowsException<HistoryMaterialRangeException>(() => law.Evaluate(-1, law.InitialState(), 1));
    }
    [TestMethod] public void NonlinearLawsRejectImplicitViscosityScaling()
    {
        Assert.ThrowsException<NotSupportedException>(() => Last(Plastic(), new HistoryPhase { ModulusFactors = new Dictionary<string, double> { ["steel"] = .5 } }));
    }
    private sealed record CountState(double MechanicalStrain, double Stress, int Commits) : HistoryMaterialState(MechanicalStrain, Stress);
    private sealed class CountingLaw : IHistoryMaterialLaw
    {
        public double InitialModulus => 200000; public bool IsLinear => false; public bool SupportsModulusFactors => false;
        public HistoryMaterialState InitialState() => new CountState(0, 0, 0);
        public HistoryMaterialResponse Evaluate(double e, HistoryMaterialState previous, double factor)
        {
            double s = InitialModulus * (e + 10000 * e * e * e);
            return new(s, InitialModulus * (1 + 30000 * e * e), new CountState(e, s, ((CountState)previous).Commits + 1));
        }
    }
    [TestMethod] public void NewtonAndLineSearchTrialsNeverCommitMaterialState()
    {
        var section = new HistorySection([Component("steel", 200000) with { Material = new CountingLaw() }]);
        var r = BridgeHistoryAnalysis.Calculate(section, [new HistoryPhase { DeltaN = 600000 }, new HistoryPhase { DeltaN = -300000 }]).Stages;
        Assert.IsTrue(r[0].NewtonIterations > 2);
        Assert.AreEqual(1, ((CountState)r[0].Fibers[0].MaterialState).Commits);
        Assert.AreEqual(2, ((CountState)r[1].Fibers[0].MaterialState).Commits); Equilibrium(r[1]);
    }
    private sealed class ReducedArea : IHistoryEffectiveAreaModel
    {
        public HistoryEffectiveAreaResponse Evaluate(IReadOnlyList<HistoryFiberResult> trial) => new(trial.Select(_ => .5).ToArray(), Array.Empty<HistoryPanelResult>());
    }
    [TestMethod] public void EffectiveAreaOuterTrialsAlsoUseLastCommittedMaterialState()
    {
        var section = new HistorySection([Component("steel", 200000) with { Material = new CountingLaw() }], new ReducedArea());
        var s = Last(section, new HistoryPhase { DeltaN = 600000 });
        Assert.IsTrue(s.EffectiveIterations > 5); Assert.AreEqual(1, ((CountState)s.Fibers[0].MaterialState).Commits); Equilibrium(s);
    }
    [TestMethod] public void GeometricAreaLossRedistributesExistingLoadWithoutResettingStrain()
    {
        var s = Last(new([Component("steel", 200000)], new ReducedArea()), new HistoryPhase { DeltaN = 20000 });
        Near(.001, s.TotalPlane.AxialStrain, 1e-9); Near(200, s.Fibers[0].Stress, 1e-6); Equilibrium(s);
    }
}
