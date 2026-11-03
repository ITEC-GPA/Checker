using CompositeBridgeTests;
using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Models;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Results.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GPCChecker.Test.CompositeBridge;

[TestClass]
public class BridgeWorkflowExecutionTests
{
    private static (Model Model,BridgeWorkflowDefinition Workflow,BridgeWorkflowEvaluation Evaluation) Sample()
    {
        var beam=new BeamElement(new Point3d(0,0,0),new Point3d(1000,0,0),null);
        beam.ConnectNodes(new NodeElement(beam.StartPoint),new NodeElement(beam.EndPoint));
        beam.Assignments.Formulation=GPC.Model.Structure.Assignments.BeamFormulation.StraightTwoNode;
        var model=ModelAssembly.Assemble(new[]{beam},Guid.NewGuid());beam=model.BeamElements.Values.Single();
        var loads=new[]{new LoadCaseBase("G1"),new LoadCaseBase("G2"),new LoadCaseBase("Q")};foreach(var load in loads)model.AddLoadCase(load);
        string fp=model.AnalysisFingerprint();var axes=new CoordinateSystem(new Point3d(500,0,0),new Vector3d(0,1,0),new Vector3d(0,0,1));
        model.Datasets.Add("analysis",new AnalysisDataset {Id="analysis",InputFingerprint=fp,NormalizedUnits="N,mm,rad",Semantics=AnalysisSemantics.LinearStatic});
        double[] moments={1500,2000,3000};
        for(int i=0;i<loads.Length;i++)
        {
            var sample=new StationResultBeamForces(loads[i],new ResultBeamForces(0,0,0,0,moments[i]*1e6,0,axes),.5)
            {Side=SectionSide.Left,Body=ActionBody.PositiveSectionFace,State=new ResultState {DatasetId="analysis",InputFingerprint=fp,
                Semantics=AnalysisSemantics.LinearStatic,IsCombined=false,IsCumulative=true,ConcomitantStateId=loads[i].Name,
                Components=Enumerable.Repeat(ComponentAvailability.Available,6).ToArray()}};
            beam.Results.Add(new BeamResult(new[]{sample}));
        }
        var workflow=new BridgeWorkflowDefinition {ModelGuid=model.Guid,AnalysisFingerprint=fp,
            Phases=loads.Select((load,i)=>new BridgeWorkflowPhaseDefinition {Id=Guid.NewGuid(),Order=i,Name=load.Name,
                State=i==0?BridgeWorkflowSectionState.Steel:BridgeWorkflowSectionState.Composite,
                Actions=new[]{new BridgeWorkflowActionDefinition {Key=load.Name,ForceCaseGuid=load.Guid,SourceCaseGuid=load.Guid,
                    SourceModelGuid=model.Guid,SourceAnalysisFingerprint=fp,Behavior=i==0?BridgeActionBehavior.Steel:i==1?BridgeActionBehavior.LongTerm:BridgeActionBehavior.ShortTerm}}}).ToArray()};
        var evaluation=new BridgeWorkflowEvaluation {BeamGuid=beam.Guid,PhysicalPropertyFingerprint=BridgeWorkflowDefinition.PropertyFingerprint(beam),
            SectionInput=CompositeBridgeApiTests.Input() with {Phases=Array.Empty<BridgePhase>()},Station=.5,Side=SectionSide.Left,SectionAxes=axes,LongTermPhi=2,
            Factors=loads.ToDictionary(l=>l.Guid,l=>1d),Results=loads.ToDictionary(l=>l.Guid,l=>new ResultSelection {Dataset="analysis",Case=l.Name})};
        return(model,workflow,evaluation);
    }
    private static StationResultBeamForces First(Model m)=>(StationResultBeamForces)m.BeamElements.Values.Single().Results.First().Results.First();
    private static HBridgeInput Compile(Model m,BridgeWorkflowDefinition w,BridgeWorkflowEvaluation e)=>BridgeWorkflowExecutor.BuildInput(w,m,new[]{m},e);

    [DataTestMethod][DataRow(BridgeWorkflowMethod.Compatible)][DataRow(BridgeWorkflowMethod.LinearHistory)]
    public void TypedWorkflowProducesTheSameSectionResultAsExplicitManualPhases(BridgeWorkflowMethod method)
    {
        var (m,w,e)=Sample();w.Method=method;var compiled=Compile(m,w,e);
        var manual=CompositeBridgeApiTests.Input();manual=manual with {Phases=manual.Phases.Select(p=>p with {PsiL=1}).ToArray()};
        for(int i=0;i<3;i++){Assert.AreEqual(manual.Phases[i].MomentKNm,compiled.Phases[i].MomentKNm);Assert.AreEqual(manual.Phases[i].Phi,compiled.Phases[i].Phi);}
        var expected=method==BridgeWorkflowMethod.Compatible?HBridgeSection.Calculate(manual):HBridgeHistoryResults.Calculate(manual);
        var actual=BridgeWorkflowExecutor.Calculate(w,m,new[]{m},e);
        Assert.AreEqual(expected.Stages.Count,actual.Stages.Count);
        for(int i=0;i<actual.Stages.Count;i++)
        {
            Assert.AreEqual(expected.Stages[i].Points.Count,actual.Stages[i].Points.Count);
            for(int j=0;j<actual.Stages[i].Points.Count;j++)Assert.AreEqual(expected.Stages[i].Points[j].Stress,actual.Stages[i].Points[j].Stress,1e-9);
        }
        Assert.AreEqual(0,e.SectionInput.Phases.Length);Assert.AreEqual(1500e6,First(m).ResultBeamForces.M1);
    }
    [TestMethod]public void ReferencePointTransportAndNmmConversionAreExplicit()
    {
        var (m,w,e)=Sample();var f=First(m).ResultBeamForces;
        f.N=1000;f.M1=0;f.CoordinateSystem=new CoordinateSystem(new Point3d(500,0,100),e.SectionAxes.V1,e.SectionAxes.V2);
        var phase=Compile(m,w,e).Phases[0];Assert.AreEqual(1,phase.ForceKN);Assert.AreEqual(.1,phase.MomentKNm,1e-12);
        Assert.AreEqual(0,f.M1);Assert.AreEqual(100,f.CoordinateSystem.Origin.Z);
    }
    [TestMethod]public void MissingOrAmbiguousStationsDoNotInterpolate()
    {
        var (m,w,e)=Sample();e.Station=.25;e.SectionAxes.Origin.X=250;
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>Compile(m,w,e)).Message,"MissingBridgeResultStation");
        e.Station=.5;e.SectionAxes.Origin.X=500;
        m.BeamElements.Values.Single().Results.Add(new BeamResult(new[]{First(m)}));
        Assert.ThrowsException<InvalidOperationException>(()=>Compile(m,w,e));
    }
    [TestMethod]public void StaleAndIncompleteResultsAndDifferentCaseBindingsAreRejected()
    {
        var (m,w,e)=Sample();var state=First(m).State;state.InputFingerprint="stale";
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>Compile(m,w,e)).Message,"StaleBridgeResultState");
        state.InputFingerprint=w.AnalysisFingerprint;state.Components[0]=ComponentAvailability.Missing;
        Assert.ThrowsException<ArgumentException>(()=>Compile(m,w,e));
        state.Components[0]=ComponentAvailability.Available;e.Results.First().Value.Case="wrong";
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>Compile(m,w,e)).Message,"BridgeResultCaseBindingMismatch");
    }
    [TestMethod]public void NonlinearTotalsAndUnsupportedOutOfPlaneActionsAreRejected()
    {
        var (m,w,e)=Sample();var sample=First(m);sample.State.Semantics=AnalysisSemantics.NonlinearStatic;
        Assert.ThrowsException<NotSupportedException>(()=>Compile(m,w,e));
        sample.State.Semantics=AnalysisSemantics.LinearStatic;sample.ResultBeamForces.V1=10;
        StringAssert.Contains(Assert.ThrowsException<NotSupportedException>(()=>Compile(m,w,e)).Message,"OutOfPlane");
    }
    [TestMethod]public void LocalSectionStateAndPrimaryShrinkageSurviveWorkflowRoundTrip()
    {
        var (m,w,e)=Sample();var action=w.Phases[1].Actions[0];
        action.SectionStates=new[]{new BridgeBeamSectionState {BeamGuid=e.BeamGuid,Behavior=action.Behavior,ModularRatio=18,PhysicalPropertyFingerprint=e.PhysicalPropertyFingerprint}};
        w.Phases[1].Actions=w.Phases[1].Actions.Concat(new[]{new BridgeWorkflowActionDefinition {Key="shrinkage",Behavior=BridgeActionBehavior.Shrinkage,PrimaryOnly=true,Strain=-.0002}}).ToArray();
        var phases=Compile(m,w.Copy(),e).Phases;
        Assert.AreEqual(4,phases.Length);Assert.AreEqual(BridgeHomogenizationSource.ModularRatio,phases[1].HomogenizationSource);Assert.AreEqual(18,phases[1].N);
        Assert.AreEqual(BridgePhaseKind.Shrinkage,phases[2].Kind);Assert.AreEqual(-200,phases[2].ShrinkageMicrostrain,1e-12);
    }
    [TestMethod]public void ZeroCoefficientsKeepConstructionActivationAndCoupledStrainMustAgree()
    {
        var (m,w,e)=Sample();e.Factors=e.Factors.ToDictionary(p=>p.Key,p=>0d);e.Results=new Dictionary<Guid,ResultSelection>();
        var phases=Compile(m,w,e).Phases;Assert.AreEqual(3,phases.Length);Assert.IsTrue(phases.All(p=>p.MomentKNm==0));
        var action=w.Phases[1].Actions[0];action.Strain=-.0002;action.Behavior=BridgeActionBehavior.ShortTerm;action.StrainCaseGuid=m.LoadCases.Values.Last().Guid;
        e.Factors=new Dictionary<Guid,double>{{action.ForceCaseGuid!.Value,1}};
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>Compile(m,w,e)).Message,"BridgeForceAndStrainFactorsDiffer");
    }
}
