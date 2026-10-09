using GPC.Checkers.CompositeBridge.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass]
public class MethodRegressionTests
{
    [TestMethod]
    public void HistoryStrainPlaneAt_PositiveCurvatureReducesStrainAtPositiveY()
    { Assert.AreEqual(.0008, new HistoryStrainPlane(.001, .000002).At(100), 1e-14); }
    [TestMethod]
    public void HistoryStrainPlaneScale_ScalesBothComponentsWithoutMutatingOriginal()
    {
        var source = new HistoryStrainPlane(.001, .000002); var copy = source.Scale(-2);
        Assert.AreEqual(-.002, copy.AxialStrain, 1e-14); Assert.AreEqual(-.000004, copy.Curvature, 1e-14); Assert.AreEqual(.001, source.AxialStrain);
    }
    [TestMethod]
    public void ElasticEvaluate_ModulusFactorAppliesOnlyToNewStrainIncrement()
    {
        var law = new HistoryElasticLaw(200000); var committed = new HistoryMaterialState(.001, 200);
        var trial = law.Evaluate(.002, committed, .5);
        Assert.AreEqual(300.0, trial.Stress, 1e-10); Assert.AreEqual(100000.0, trial.Tangent);
        Assert.AreEqual(200.0, committed.Stress); Assert.AreEqual(.001, committed.MechanicalStrain);
    }
    [TestMethod]
    public void BilinearEvaluate_ElasticUnloadingRetainsPlasticStrain()
    {
        var law = new HistoryBilinearSteelLaw(200000, 250);
        var committed = law.Evaluate(.003, law.InitialState(), 1).State;
        var unloaded = law.Evaluate(.002, committed, 1);
        Assert.AreEqual(50.0, unloaded.Stress, 1e-9); Assert.AreEqual(200000.0, unloaded.Tangent);
        Assert.AreEqual(.00175, ((HistoryPlasticState)unloaded.State).PlasticStrain, 1e-14);
        Assert.AreEqual(250.0, committed.Stress, 1e-10);
    }
    [TestMethod]
    public void BilinearEvaluate_RejectsUnsupportedModulusChange()
    { var law = new HistoryBilinearSteelLaw(200000, 250); Assert.ThrowsException<NotSupportedException>(() => law.Evaluate(.001, law.InitialState(), .5)); }
    [TestMethod]
    public void BilinearEvaluate_RejectsBeyondUltimateStrainWithoutChangingCommittedState()
    {
        var law = new HistoryBilinearSteelLaw(200000, 250, ultimateStrain: .01); var state = law.InitialState();
        Assert.ThrowsException<HistoryMaterialRangeException>(() => law.Evaluate(.01001, state, 1));
        Assert.AreEqual(0.0, state.Stress); Assert.AreEqual(0.0, ((HistoryPlasticState)state).PlasticStrain);
    }
}
