using System.Runtime.Serialization;
using System.Text;
using GPC.Checkers.CompositeBridge;
using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Models;
using GPC.Model.Structure.Assignments;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GPCChecker.Test.CompositeBridge;

[TestClass]
public class BridgeWorkflowDefinitionTests
{
    private static (Model Model,BridgeWorkflowDefinition Workflow) Sample()
    {
        var a=new NodeElement(new Point3d(0,0,0)); var b=new NodeElement(new Point3d(1000,0,0));
        var beam=new BeamElement(a.Position,b.Position,null); beam.ConnectNodes(a,b); beam.Assignments.Formulation=BeamFormulation.StraightTwoNode;
        var model=ModelAssembly.Assemble(new[]{beam},Guid.NewGuid()); var load=new LoadCaseBase("dead"); model.AddLoadCase(load);
        var action=new BridgeWorkflowActionDefinition {Key="steel-dead",Behavior=BridgeActionBehavior.Steel,ForceCaseGuid=load.Guid,SourceCaseGuid=Guid.NewGuid(),SourceModelGuid=Guid.NewGuid(),SourceAnalysisFingerprint="source-revision",BeamGuids=new[]{beam.Guid}};
        var workflow=new BridgeWorkflowDefinition {ModelGuid=model.Guid,AnalysisFingerprint=model.AnalysisFingerprint(),Phases=new[]{new BridgeWorkflowPhaseDefinition {Id=Guid.NewGuid(),Order=1,Name="Steel",State=BridgeWorkflowSectionState.Steel,Day=0,Actions=new[]{action}}}};
        workflow.Validate(model); return(model,workflow);
    }
    [TestMethod] public void WorkflowRoundTripKeepsStableReferencesAndDetachesSelections()
    {
        var (model,w)=Sample(); var fingerprint=w.Fingerprint(); var copy=w.Copy(); copy.Validate(model);
        Assert.AreEqual(fingerprint,copy.Fingerprint()); Assert.AreNotSame(w.Phases[0].Actions[0].BeamGuids,copy.Phases[0].Actions[0].BeamGuids);
        copy.Phases[0].Actions[0].BeamGuids[0]=Guid.NewGuid(); Assert.ThrowsException<ArgumentException>(()=>copy.Validate(model));
        Assert.AreEqual(fingerprint,w.Fingerprint()); Assert.AreNotEqual(fingerprint,copy.Fingerprint());
    }
    [TestMethod] public void WorkflowCannotReuseAnIncrementOnOverlappingBeams()
    {
        var (model,w)=Sample(); var copy=w.Copy().Phases[0].Actions[0]; copy.Key="duplicate";
        w.Phases[0].Actions=new[]{w.Phases[0].Actions[0],copy};
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>w.Validate(model)).Message,"RepeatedBridgeForceIncrement");
    }
    [TestMethod] public void WorkflowRejectsStalePhysicalModelAndUnknownCases()
    {
        var (model,w)=Sample(); w.Phases[0].Actions[0].ForceCaseGuid=Guid.NewGuid();
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>w.Validate(model)).Message,"UnknownBridgeWorkflowCase");
        model.NodesElements.Values.First().Position.X+=1;
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>w.Validate(model)).Message,"StaleBridgeWorkflowModel");
    }
    [TestMethod] public void ReimportingASourceCaseCannotApplyTheSameIncrementTwice()
    {
        var (model,w)=Sample(); var copy=w.Copy().Phases[0].Actions[0]; copy.Key="reimported";
        var importedCase=new LoadCaseBase("dead-copy"); model.AddLoadCase(importedCase);
        copy.ForceCaseGuid=importedCase.Guid;
        w.AnalysisFingerprint=model.AnalysisFingerprint();
        w.Phases[0].Actions=new[]{w.Phases[0].Actions[0],copy};
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>w.Validate(model)).Message,"RepeatedBridgeSourceIncrement");
    }
    [TestMethod] public void SourceProvenanceRequiresTheExactRevisionAndCase()
    {
        var (model,w)=Sample(); var (source,_)=Sample(); var action=w.Phases[0].Actions[0];
        action.SourceModelGuid=source.Guid; action.SourceAnalysisFingerprint=source.AnalysisFingerprint();
        action.SourceCaseGuid=source.LoadCases.Values.Single().Guid;
        w.ValidateSources(new[]{source});
        action.SourceCaseGuid=Guid.NewGuid();
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>w.ValidateSources(new[]{source})).Message,"UnknownBridgeSourceCase");
        action.SourceCaseGuid=source.LoadCases.Values.Single().Guid;
        source.NodesElements.Values.First().Position.X+=1;
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>w.ValidateSources(new[]{source})).Message,"MissingOrStaleBridgeSourceModel");
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>w.ValidateSources(Array.Empty<Model>())).Message,"MissingOrStaleBridgeSourceModel");
    }
    [TestMethod] public void PrimaryShrinkageHasExplicitSignAndNoRecursiveSourceModel()
    {
        var (model,w)=Sample(); var primary=new BridgeWorkflowActionDefinition {Key="shrink",Behavior=BridgeActionBehavior.Shrinkage,PrimaryOnly=true,Strain=-0.0002};
        w.Phases=w.Phases.Concat(new[]{new BridgeWorkflowPhaseDefinition {Id=Guid.NewGuid(),Order=2,Name="Slab",State=BridgeWorkflowSectionState.Composite,Day=28,Actions=new[]{primary}}}).ToArray();
        w.Validate(model); var bytes=w.Save(); Assert.IsTrue(bytes.Length<10000); w.Copy().Validate(model);
        primary.Strain=0.0002; Assert.ThrowsException<ArgumentException>(()=>w.Validate(model));
    }
    [TestMethod] public void FutureWorkflowSchemaAndInvalidChronologyAreRejected()
    {
        var (model,w)=Sample(); var xml=Encoding.UTF8.GetString(w.Save()); Assert.IsTrue(xml.Contains("<schema>1</schema>"));
        Assert.ThrowsException<SerializationException>(()=>BridgeWorkflowDefinition.Read(Encoding.UTF8.GetBytes(xml.Replace("<schema>1</schema>","<schema>2</schema>"))));
        w.Phases[0].Day=-1; Assert.ThrowsException<ArgumentException>(()=>w.Validate(model));
    }
    [TestMethod] public void SectionStatesRequireCurrentPhysicalPropertyAndConsistentExclusions()
    {
        var (model,w)=Sample(); var beam=model.BeamElements.Values.Single(); var action=w.Phases[0].Actions[0];
        w.Phases[0].State=BridgeWorkflowSectionState.Composite; action.Behavior=BridgeActionBehavior.ShortTerm;
        action.SectionStates=new[]{new BridgeBeamSectionState {BeamGuid=beam.Guid,Behavior=BridgeActionBehavior.ShortTerm,ModularRatio=7,PhysicalPropertyFingerprint=BridgeWorkflowDefinition.PropertyFingerprint(beam)}};
        w.Validate(model); var copy=w.Copy(); copy.Validate(model); Assert.AreEqual(w.Fingerprint(),copy.Fingerprint());
        action.SectionStates[0].PhysicalPropertyFingerprint="stale";
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>w.Validate(model)).Message,"StaleBridgeSectionState");
    }
}
