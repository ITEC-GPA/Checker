using GPC.Design.Actions.Construction;
using GPC.Model.Actions.Climate;
using GPC.Model.Actions.Construction;
using GPC.Model.Actions.General;
using GPC.Model.Stages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace Design.Checks;

[TestClass]
public sealed class ConstructionActionsTests
{
    private static ConstructionActionsInput En(ConstructionLoadModel model)
    {
        var i=new ConstructionActionsInput{Standard=ClimateStandard.EurocodeFirstGeneration,NationalParameters="Valori raccomandati verificati, esempio di prova",RecommendedEnLoadsConfirmed=true};
        i.ConstructionLoads=new(){new ConstructionLoadInput{Model=model}};return i;
    }
    [TestMethod]
    public void StagesActivateAndRemoveLoadsBeforeTheirReportedState()
    {
        var r=ConstructionActions.Calculate(new ConstructionActionsInput());
        Assert.AreEqual(2,r.Stages.Count);Assert.AreEqual(0,r.Stages[0].StartDay);Assert.AreEqual(14,r.Stages[0].EndDay);Assert.AreEqual(28,r.Stages[1].EndDay);
        Assert.AreEqual(250,r.Stages[0].Loads.Single().VerticalDownKn,1e-12);Assert.AreEqual(ActionNature.Variable,r.Stages[0].Loads.Single().Nature);
        Assert.AreEqual(ActionNature.PermanentG1,r.Stages[1].Loads.Single().Nature);
        var events=r.Stages[1].Definition.GroupChanges;Assert.AreEqual(2,events.Count);Assert.AreEqual(StageGroupAction.Deactivate,events[0].Action);Assert.AreEqual(StageGroupAction.Activate,events[1].Action);Assert.IsTrue(events.All(e=>e.Timing==StageEventTiming.First&&e.Kind==StageGroupKind.Load));
    }
    [DataTestMethod]
    [DataRow(.2,312.5)]
    [DataRow(.4,564.75)]
    [DataRow(1.0,1319.25)]
    public void CastingIncludesGlobalWeightAndOnlyTheIncrementInsideTheWorkingArea(double thickness,double expected)
    {
        // Independent EN 1991-1-6:2005 Table 4.2 arithmetic:
        // 50*(25*t+.5+.75) + 9*(clamp(.1*25*t,.75,1.5)-.75).
        var i=En(ConstructionLoadModel.EnConcreteCasting);i.ConstructionLoads[0].ThicknessM=thickness;
        var r=ConstructionActions.Calculate(i).Stages[0].Loads.Single();Assert.AreEqual(expected,r.VerticalDownKn,1e-10);
        Assert.AreEqual(2,r.Patches.Count);Assert.AreEqual(0,r.PointLoadKn);
    }
    [TestMethod]
    public void CastingPatchPositionChangesMomentsAndSmallFootprintsAreClipped()
    {
        var i=En(ConstructionLoadModel.EnConcreteCasting);var l=i.ConstructionLoads[0];l.ThicknessM=.4;l.WorkXOffsetM=4;l.WorkYOffsetM=2;
        var r=ConstructionActions.Calculate(i).Stages[0].Loads.Single();Assert.AreEqual(-1414.125,r.MomentXKnM,1e-10);Assert.AreEqual(2824.875,r.MomentYKnM,1e-10);
        l.LengthM=2;l.WidthM=1;l.ThicknessM=1;l.WorkXOffsetM=l.WorkYOffsetM=0;
        r=ConstructionActions.Calculate(i).Stages[0].Loads.Single();Assert.AreEqual(54,r.VerticalDownKn,1e-12);Assert.AreEqual(2,r.Patches[1].LengthM);Assert.AreEqual(1,r.Patches[1].WidthM);
    }
    [TestMethod]
    public void PersonnelAndBridgeStorageHaveDistinctRecommendedLoads()
    {
        var i=En(ConstructionLoadModel.EnPersonnel);Assert.AreEqual(50,ConstructionActions.Calculate(i).Stages[0].Loads.Single().VerticalDownKn,1e-12);
        i.ConstructionLoads[0].AssignedPressureKpa=2;Assert.AreEqual(100,ConstructionActions.Calculate(i).Stages[0].Loads.Single().VerticalDownKn,1e-12);
        i=En(ConstructionLoadModel.EnBridgeStorage);var r=ConstructionActions.Calculate(i).Stages[0].Loads.Single();Assert.AreEqual(110,r.VerticalDownKn,1e-12);Assert.AreEqual(100,r.PointLoadKn);
    }
    [TestMethod]
    public void ExternalMomentsUseRightHandSignsAndDynamicsAreAppliedOnce()
    {
        var i=new ConstructionActionsInput{ConstructionLoads=new(){new ConstructionLoadInput{Model=ConstructionLoadModel.Assigned,PointLoadKn=10,PointXM=2,PointYM=3,HorizontalHeightM=4,HorizontalXKn=5,HorizontalYKn=6,Source="Caso analitico"}}};
        var l=i.ConstructionLoads[0];var r=ConstructionActions.Calculate(i).Stages[0].Loads.Single();Assert.AreEqual(-54,r.MomentXKnM,1e-12);Assert.AreEqual(40,r.MomentYKnM,1e-12);Assert.AreEqual(-3,r.MomentZKnM,1e-12);
        l.DynamicFactor=1.5;r=ConstructionActions.Calculate(i).Stages[0].Loads.Single();Assert.AreEqual(15,r.VerticalDownKn,1e-12);Assert.AreEqual(-81,r.MomentXKnM,1e-12);Assert.AreEqual(60,r.MomentYKnM,1e-12);
    }
    [TestMethod]
    public void AssignedLiftingActionsKeepTheirUpwardSign()
    {
        var i=new ConstructionActionsInput{ConstructionLoads=new(){new ConstructionLoadInput{Model=ConstructionLoadModel.Assigned,PointLoadKn=-10,PointXM=2,PointYM=3,Source="Sollevamento assegnato"}}};
        var r=ConstructionActions.Calculate(i).Stages[0].Loads.Single();Assert.AreEqual(-10,r.VerticalDownKn,1e-12);Assert.AreEqual(30,r.MomentXKnM,1e-12);Assert.AreEqual(-20,r.MomentYKnM,1e-12);
    }
    [TestMethod]
    public void TranslationAndInputRoundTripPreserveForcesAndAddTheCorrectLeverArm()
    {
        var i=new ConstructionActionsInput();var a=ConstructionActions.Calculate(i).Stages[0].Loads.Single();
        i.ConstructionLoads[0].LeftXM=7;i.ConstructionLoads[0].BottomYM=-3;
        string before=JsonSerializer.Serialize(i);var copy=JsonSerializer.Deserialize<ConstructionActionsInput>(before)!;var b=ConstructionActions.Calculate(copy).Stages[0].Loads.Single();
        Assert.AreEqual(a.VerticalDownKn,b.VerticalDownKn);Assert.AreEqual(a.MomentXKnM+750,b.MomentXKnM,1e-12);Assert.AreEqual(a.MomentYKnM+1750,b.MomentYKnM,1e-12);Assert.AreEqual(before,JsonSerializer.Serialize(copy));
    }
    [TestMethod]
    public void EmptyStagesAndZeroDurationAreRepresentableWithoutInventingStructuralResults()
    {
        var i=new ConstructionActionsInput{ConstructionLoads=new()};i.ConstructionStages[0].DurationDays=0;
        var r=ConstructionActions.Calculate(i);Assert.AreEqual(0,r.Stages[1].StartDay);Assert.AreEqual(0,r.Stages[0].Loads.Count);Assert.AreEqual(0,r.Stages[0].Definition.GroupChanges.Count);
    }
    [TestMethod]
    public void InvalidInputsAndDoubleCountingPersonnelDuringCastingAreRejected()
    {
        var i=new ConstructionActionsInput();i.ConstructionLoads[0].StopStage=1;Assert.ThrowsException<ArgumentException>(()=>ConstructionActions.Calculate(i));
        i=new ConstructionActionsInput();i.ConstructionStages[1].Name=i.ConstructionStages[0].Name;Assert.ThrowsException<ArgumentException>(()=>ConstructionActions.Calculate(i));
        i=new ConstructionActionsInput();i.ConstructionLoads[0].LengthM=double.NaN;Assert.ThrowsException<ArgumentException>(()=>ConstructionActions.Calculate(i));
        i=En(ConstructionLoadModel.EnConcreteCasting);i.RecommendedEnLoadsConfirmed=false;Assert.ThrowsException<ArgumentException>(()=>ConstructionActions.Calculate(i));
        i=En(ConstructionLoadModel.EnConcreteCasting);i.Standard=ClimateStandard.Ntc2018;Assert.ThrowsException<ArgumentException>(()=>ConstructionActions.Calculate(i));
        i=En(ConstructionLoadModel.EnConcreteCasting);i.ConstructionLoads[0].WorkXOffsetM=8;Assert.ThrowsException<ArgumentException>(()=>ConstructionActions.Calculate(i));
        i=En(ConstructionLoadModel.EnConcreteCasting);i.ConstructionLoads.Add(new ConstructionLoadInput{Name="Personale",Model=ConstructionLoadModel.EnPersonnel});Assert.ThrowsException<ArgumentException>(()=>ConstructionActions.Calculate(i));
        i.ConstructionLoads[1].LeftXM=20;Assert.AreEqual(2,ConstructionActions.Calculate(i).Stages[0].Loads.Count);
    }
}
