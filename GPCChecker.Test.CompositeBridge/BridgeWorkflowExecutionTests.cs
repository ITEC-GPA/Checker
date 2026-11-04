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
        // The manual phases keep their own ψL (G2 permanent: 1.1); the workflow carries it per action.
        var (m,w,e)=Sample();w.Method=method;w.Phases[1].Actions[0].PsiL=1.1;var compiled=Compile(m,w,e);
        var manual=CompositeBridgeApiTests.Input();
        for(int i=0;i<3;i++)
        {
            Assert.AreEqual(manual.Phases[i].MomentKNm,compiled.Phases[i].MomentKNm);Assert.AreEqual(manual.Phases[i].Phi,compiled.Phases[i].Phi);
            Assert.AreEqual(manual.Phases[i].PsiL,compiled.Phases[i].PsiL);
        }
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
    [DataTestMethod][DataRow(BridgeWorkflowMethod.Compatible)][DataRow(BridgeWorkflowMethod.LinearHistory)]
    public void ConcreteExcludedBeamOfAnActionUsesTheConcreteExcludedSectionWithBothMethods(BridgeWorkflowMethod method)
    {
        // BridGHe's local FEM states: the beams near the supports have their concrete excluded in the long- and short-term actions,
        // and its checks (Compatible) calculate those increments on the concrete-excluded section.
        var (m,w,e)=Sample();w.Method=method;w.Phases[1].Actions[0].PsiL=1.1;w.Phases[2].Actions[0].ConcreteExcludedBeamGuids=new[]{e.BeamGuid};
        var compiled=Compile(m,w,e);
        Assert.AreEqual(BridgePhaseKind.ConcreteExcluded,compiled.Phases[2].Kind);Assert.AreEqual(3000,compiled.Phases[2].MomentKNm);
        Assert.AreEqual(BridgePhaseKind.Composite,compiled.Phases[1].Kind);
        var manual=CompositeBridgeApiTests.Input();manual=manual with {Phases=manual.Phases.Select((p,i)=>i==2?p with {Kind=BridgePhaseKind.ConcreteExcluded}:p).ToArray()};
        var expected=method==BridgeWorkflowMethod.Compatible?HBridgeSection.Calculate(manual):HBridgeHistoryResults.Calculate(manual);
        var actual=BridgeWorkflowExecutor.Calculate(w,m,new[]{m},e);
        Assert.AreEqual(expected.Stages.Count,actual.Stages.Count);
        for(int i=0;i<actual.Stages.Count;i++)
            for(int j=0;j<actual.Stages[i].Points.Count;j++)Assert.AreEqual(expected.Stages[i].Points[j].Stress,actual.Stages[i].Points[j].Stress,1e-9);
        // Different from the composite increment: the exclusion is not ignored.
        var plain=w.Copy();plain.Phases[2].Actions[0].ConcreteExcludedBeamGuids=Array.Empty<Guid>();
        var composite=BridgeWorkflowExecutor.Calculate(plain,m,new[]{m},e);
        Assert.IsTrue(composite.Stages.Last().Points.Zip(actual.Stages.Last().Points).Any(p=>Math.Abs(p.First.Stress-p.Second.Stress)>1e-3));
    }

    [TestMethod]
    public void UnloadedIncrementsOfALocalStateActivateItOncePerRun()
    {
        // Example 50: the short-term phase has the 31 positions of a moving load, one loaded per combination; on a concrete-excluded
        // beam each unloaded position would be a phase of its own and the section engine accepts at most 20.
        var (m,w,e)=Sample();var beam=m.BeamElements.Values.Single();var q=w.Phases[2];var axes=e.SectionAxes;string fp=m.AnalysisFingerprint();
        var positions=Enumerable.Range(1,25).Select(i=>new LoadCaseBase("Q"+i.ToString("00"))).ToArray();
        foreach(var load in positions)
        {
            m.AddLoadCase(load);
            beam.Results.Add(new BeamResult(new[]{new StationResultBeamForces(load,new ResultBeamForces(0,0,0,0,100e6,0,axes),.5)
            {Side=SectionSide.Left,Body=ActionBody.PositiveSectionFace,State=new ResultState {DatasetId="analysis",Semantics=AnalysisSemantics.LinearStatic,
                IsCombined=false,IsCumulative=true,ConcomitantStateId=load.Name,Components=Enumerable.Repeat(ComponentAvailability.Available,6).ToArray()}}}));
        }
        fp=m.AnalysisFingerprint();m.Datasets["analysis"].InputFingerprint=fp;
        foreach(var r in beam.Results.SelectMany(r=>r.Results))r.State.InputFingerprint=fp;
        w.AnalysisFingerprint=fp;foreach(var a in w.Phases.SelectMany(p=>p.Actions))a.SourceAnalysisFingerprint=fp;
        q.Actions=q.Actions.Concat(positions.Select(l=>new BridgeWorkflowActionDefinition {Key=l.Name,ForceCaseGuid=l.Guid,SourceCaseGuid=l.Guid,
            SourceModelGuid=m.Guid,SourceAnalysisFingerprint=fp,Behavior=BridgeActionBehavior.ShortTerm})).ToArray();
        foreach(var a in q.Actions)a.ConcreteExcludedBeamGuids=new[]{e.BeamGuid};
        var factors=e.Factors.ToDictionary(p=>p.Key,p=>p.Value);foreach(var l in positions)factors[l.Guid]=l.Name=="Q05"?1:0;
        var results=e.Results.ToDictionary(p=>p.Key,p=>p.Value);foreach(var l in positions)results[l.Guid]=new ResultSelection {Dataset="analysis",Case=l.Name};
        e.Factors=factors;e.Results=results;
        var compiled=Compile(m,w,e);
        // Q, Q01 (first unloaded, activation), Q02-Q04 (same state: none), Q05 (loaded), Q06 (activation), Q07-Q25 (none).
        CollectionAssert.AreEqual(new[]{"Q","Q01","Q05","Q06"},compiled.Phases.Skip(2).Select(p=>p.Name.Split(" / ").Last()).ToArray());
        var actual=BridgeWorkflowExecutor.Calculate(w,m,new[]{m},e);
        // The same stresses as the loaded increments alone: the activations carry no action.
        var loaded=w.Copy();loaded.Phases[2].Actions=loaded.Phases[2].Actions.Where(a=>a.Key is "Q" or "Q05").ToArray();
        var expected=BridgeWorkflowExecutor.Calculate(loaded,m,new[]{m},e);
        for(int j=0;j<actual.Stages.Last().Points.Count;j++)Assert.AreEqual(expected.Stages.Last().Points[j].Stress,actual.Stages.Last().Points[j].Stress,1e-9);
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
    [TestMethod]public void NonlinearTotalsAndStrictOutOfPlaneActionsAreRejected()
    {
        var (m,w,e)=Sample();var sample=First(m);sample.State.Semantics=AnalysisSemantics.NonlinearStatic;
        Assert.ThrowsException<NotSupportedException>(()=>Compile(m,w,e));
        sample.State.Semantics=AnalysisSemantics.LinearStatic;sample.ResultBeamForces.V1=10;e.StrictOutOfPlaneActions=true;
        StringAssert.Contains(Assert.ThrowsException<NotSupportedException>(()=>Compile(m,w,e)).Message,"OutOfPlane");
    }
    [TestMethod]public void OutOfPlaneActionsAreNeglectedByDefaultWithTheBridgheAssumption()
    {
        // A real 3D grillage: V1, M2 and T on an H section. BridGHe's adapter neglects them (ignoreSecondaryActions = true) and warns.
        var (m,w,e)=Sample();var forces=First(m).ResultBeamForces;forces.V1=10e3;forces.M2=40e6;forces.T=25e6;
        var phases=BridgeWorkflowExecutor.BuildInput(w,m,new[]{m},e,out var warnings).Phases;
        Assert.AreEqual(1500,phases[0].MomentKNm);Assert.AreEqual(0,phases[0].TorsionKNm);
        Assert.AreEqual(BridgeWorkflowExecutor.PlanarAssumption,warnings[0]);
        StringAssert.Contains(warnings[1],"1 incrementi");StringAssert.Contains(warnings[1],"|M2| fino a 4E+07 N mm");
        var result=BridgeWorkflowExecutor.Calculate(w,m,new[]{m},e);
        Assert.IsTrue(result.Stages.All(s=>s.Warnings.Contains(BridgeWorkflowExecutor.PlanarAssumption)));
        forces.V1=0;forces.M2=0;forces.T=0;
        var clean=BridgeWorkflowExecutor.Calculate(w,m,new[]{m},e).Stages;
        for(int i=0;i<clean.Count;i++)for(int j=0;j<clean[i].Points.Count;j++)Assert.AreEqual(clean[i].Points[j].Stress,result.Stages[i].Points[j].Stress);
    }
    [TestMethod]public void PermanentAndImposedDeformationCreepMultipliersCoexist()
    {
        var (m,w,e)=Sample();w.Phases[1].Actions[0].PsiL=1.1;w.Phases[2].Actions[0].PsiL=1.5;w.Phases[2].Actions[0].Behavior=BridgeActionBehavior.LongTerm;
        var phases=Compile(m,w.Copy(),e).Phases;
        Assert.AreEqual(1,phases[0].PsiL);Assert.AreEqual(1.1,phases[1].PsiL);Assert.AreEqual(1.5,phases[2].PsiL);
        Assert.AreEqual(2,phases[1].Phi);Assert.AreEqual(2,phases[2].Phi);
        w.Phases[2].Actions[0].PsiL=0;
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>Compile(m,w,e)).Message,"InvalidBridgeCreepMultiplier");
        // Recipes without ψL keep the bytes of schema 1 (and their fingerprints: the member is omitted when null).
        foreach(var a in w.Phases.SelectMany(p=>p.Actions))a.PsiL=null;Assert.IsFalse(System.Text.Encoding.UTF8.GetString(w.Save()).Contains("PsiL"));
    }
    [TestMethod]public void LocalSectionStateAndPrimaryShrinkageSurviveWorkflowRoundTrip()
    {
        var (m,w,e)=Sample();var action=w.Phases[1].Actions[0];
        action.SectionStates=new[]{new BridgeBeamSectionState {BeamGuid=e.BeamGuid,Behavior=action.Behavior,ModularRatio=18,PhysicalPropertyFingerprint=e.PhysicalPropertyFingerprint}};
        w.Phases[1].Actions=w.Phases[1].Actions.Concat(new[]{new BridgeWorkflowActionDefinition {Key="shrinkage",Behavior=BridgeActionBehavior.Shrinkage,PrimaryOnly=true,
            Strain=-.0002,StrainCaseGuid=action.ForceCaseGuid,PsiL=.55}}).ToArray();
        var phases=Compile(m,w.Copy(),e).Phases;
        Assert.AreEqual(4,phases.Length);Assert.AreEqual(BridgeHomogenizationSource.ModularRatio,phases[1].HomogenizationSource);Assert.AreEqual(18,phases[1].N);
        Assert.AreEqual(BridgePhaseKind.Shrinkage,phases[2].Kind);Assert.AreEqual(-200,phases[2].ShrinkageMicrostrain,1e-12);Assert.AreEqual(.55,phases[2].PsiL);
    }
    [TestMethod]public void PrimaryShrinkageWithoutStrainCaseIsRejected()
    {
        // Review L4: a primary strain without its strain case was applied with 1 before 3/11, then with 0 (L3): its coefficient is undetermined.
        // BridGHe always gives it a strain case ("<key> [strain]"), so the recipe is rejected with the reason instead of changing the result.
        var (m,w,e)=Sample();
        w.Phases[1].Actions=w.Phases[1].Actions.Concat(new[]{new BridgeWorkflowActionDefinition {Key="shrinkage",Behavior=BridgeActionBehavior.Shrinkage,PrimaryOnly=true,Strain=-.0002}}).ToArray();
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>BridgeWorkflowExecutor.BuildInput(w,m,new[]{m},e,out _)).Message,
            "BridgePrimaryStrainRequiresStrainCase: shrinkage");
        Assert.ThrowsException<ArgumentException>(()=>BridgeWorkflowExecutor.Calculate(w,m,new[]{m},e));
        // A saved recipe in that state still opens, so that it can be completed.
        var copy=BridgeWorkflowDefinition.Read(w.Save()); Assert.AreEqual(-.0002,copy.Phases[1].Actions[1].Strain);
    }
    [TestMethod]public void TheOverloadWithoutWarningsNeverNeglectsActions()
    {
        // Review L4: BuildInput without warnings dropped V1, M2 and T in silence after the planar assumption became the default (L3).
        var (m,w,e)=Sample();var forces=First(m).ResultBeamForces;forces.V1=10e3;forces.M2=40e6;
        StringAssert.Contains(Assert.ThrowsException<NotSupportedException>(()=>Compile(m,w,e)).Message,"BridgeSectionCannotDiscardOutOfPlaneActions");
        Assert.IsFalse(e.StrictOutOfPlaneActions);
        // The overload with warnings keeps BridGHe's planar assumption and says so.
        BridgeWorkflowExecutor.BuildInput(w,m,new[]{m},e,out var warnings);Assert.AreEqual(BridgeWorkflowExecutor.PlanarAssumption,warnings[0]);
        // Without out-of-plane actions both overloads give the same phases.
        forces.V1=0;forces.M2=0;
        CollectionAssert.AreEqual(BridgeWorkflowExecutor.BuildInput(w,m,new[]{m},e,out _).Phases.Select(p=>p.MomentKNm).ToArray(),Compile(m,w,e).Phases.Select(p=>p.MomentKNm).ToArray());
    }
    [TestMethod]public void ForceCaseMustBeTheDeclaredSourceCase()
    {
        // Same source model: the G2 increment cannot declare Q as its source.
        var (m,w,e)=Sample();var q=m.LoadCases.Values.Single(c=>c.Name=="Q");
        w.Phases[2].Actions[0].SourceCaseGuid=w.Phases[1].Actions[0].ForceCaseGuid;w.Phases[1].Actions[0].SourceCaseGuid=q.Guid;
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>Compile(m,w,e)).Message,"BridgeForceCaseNotFromSourceCase: G2");
    }
    [TestMethod]public void ZeroCoefficientsKeepConstructionActivationAndCoupledStrainMustAgree()
    {
        var (m,w,e)=Sample();e.Factors=e.Factors.ToDictionary(p=>p.Key,p=>0d);e.Results=new Dictionary<Guid,ResultSelection>();
        var phases=Compile(m,w,e).Phases;Assert.AreEqual(3,phases.Length);Assert.IsTrue(phases.All(p=>p.MomentKNm==0));
        var action=w.Phases[1].Actions[0];action.Strain=-.0002;action.Behavior=BridgeActionBehavior.ShortTerm;action.StrainCaseGuid=m.LoadCases.Values.Last().Guid;
        e.Factors=new Dictionary<Guid,double>{{action.ForceCaseGuid!.Value,1}};
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>Compile(m,w,e)).Message,"BridgeForceAndStrainFactorsDiffer");
    }

    // ----- User decision 9 of 4/11 («falli»): a plan prepared once (review L10) -----

    /// <summary>The variants of the sample that compile differently: ψL, concrete excluded, local state and primary shrinkage, out-of-plane
    /// actions (neglected with warnings), zero coefficients.</summary>
    private static IEnumerable<(string Name,Model Model,BridgeWorkflowDefinition Workflow,BridgeWorkflowEvaluation Evaluation)> Variants()
    {
        foreach(var method in new[]{BridgeWorkflowMethod.Compatible,BridgeWorkflowMethod.LinearHistory})
        {
            var (m,w,e)=Sample();w.Method=method;w.Phases[1].Actions[0].PsiL=1.1;yield return ("psiL "+method,m,w,e);
            (m,w,e)=Sample();w.Method=method;w.Phases[2].Actions[0].ConcreteExcludedBeamGuids=new[]{e.BeamGuid};yield return ("excluded "+method,m,w,e);
            (m,w,e)=Sample();w.Method=method;var action=w.Phases[1].Actions[0];
            action.SectionStates=new[]{new BridgeBeamSectionState {BeamGuid=e.BeamGuid,Behavior=action.Behavior,ModularRatio=18,PhysicalPropertyFingerprint=e.PhysicalPropertyFingerprint}};
            w.Phases[1].Actions=w.Phases[1].Actions.Concat(new[]{new BridgeWorkflowActionDefinition {Key="shrinkage",Behavior=BridgeActionBehavior.Shrinkage,PrimaryOnly=true,
                Strain=-.0002,StrainCaseGuid=action.ForceCaseGuid,PsiL=.55}}).ToArray();
            yield return ("local state and shrinkage "+method,m,w,e);
            (m,w,e)=Sample();w.Method=method;var forces=First(m).ResultBeamForces;forces.V1=10e3;forces.M2=40e6;forces.T=25e6;yield return ("out of plane "+method,m,w,e);
            (m,w,e)=Sample();w.Method=method;e.Factors=e.Factors.ToDictionary(p=>p.Key,p=>0d);e.Results=new Dictionary<Guid,ResultSelection>();yield return ("zero coefficients "+method,m,w,e);
        }
    }

    [TestMethod]public void APreparedPlanCompilesTheSameInputsAndResultsBitForBit()
    {
        foreach(var (name,m,w,e) in Variants())
        {
            var plan=BridgeWorkflowExecutor.Prepare(w,m,new[]{m});Assert.AreEqual(w.Method,plan.Method,name);
            var once=BridgeWorkflowExecutor.BuildInput(w,m,new[]{m},e,out var onceWarnings);var prepared=plan.BuildInput(e,out var preparedWarnings);
            Assert.AreEqual(once.Phases.Length,prepared.Phases.Length,name);
            // Records: every member, doubles by value (NaN equal to NaN), the same names and kinds.
            for(int i=0;i<once.Phases.Length;i++)Assert.AreEqual(once.Phases[i],prepared.Phases[i],name+" phase "+i);
            Assert.AreEqual(once with {Phases=Array.Empty<BridgePhase>()},prepared with {Phases=Array.Empty<BridgePhase>()},name);
            CollectionAssert.AreEqual(onceWarnings.ToArray(),preparedWarnings.ToArray(),name);
            var expected=BridgeWorkflowExecutor.Calculate(w,m,new[]{m},e);var actual=plan.Calculate(e);
            Assert.AreEqual(expected.Stages.Count,actual.Stages.Count,name);
            for(int i=0;i<actual.Stages.Count;i++)
            {
                CollectionAssert.AreEqual(expected.Stages[i].Warnings,actual.Stages[i].Warnings,name);
                Assert.AreEqual(expected.Stages[i].Points.Count,actual.Stages[i].Points.Count,name);
                for(int j=0;j<actual.Stages[i].Points.Count;j++)Assert.AreEqual(expected.Stages[i].Points[j].Stress,actual.Stages[i].Points[j].Stress,name);
            }
            // The strict overload: the same rejection of the out-of-plane actions, or the same phases.
            if(name.StartsWith("out of plane",StringComparison.Ordinal))
                Assert.AreEqual(Assert.ThrowsException<NotSupportedException>(()=>Compile(m,w,e)).Message,Assert.ThrowsException<NotSupportedException>(()=>plan.BuildInput(e)).Message,name);
            else CollectionAssert.AreEqual(Compile(m,w,e).Phases,plan.BuildInput(e).Phases,name);
        }
    }

    [TestMethod]public void APreparedPlanFailsAtPreparationWithTheDiagnosticsOfTheOneShotCalls()
    {
        // Every rejection of Validate and ValidateSources: the same exception, type and message, at Prepare instead of at each evaluation.
        var scenarios=new List<(string Name,Func<(Model,BridgeWorkflowDefinition,BridgeWorkflowEvaluation,IEnumerable<Model>?)> Make)>
        {
            ("stale model",()=>{var (m,w,e)=Sample();w.AnalysisFingerprint="stale";return (m,w,e,new[]{m});}),
            ("other model",()=>{var (m,w,e)=Sample();w.ModelGuid=Guid.NewGuid();return (m,w,e,new[]{m});}),
            ("primary strain without its case",()=>{var (m,w,e)=Sample();w.Phases[1].Actions=w.Phases[1].Actions.Concat(new[]{new BridgeWorkflowActionDefinition {Key="shrinkage",
                Behavior=BridgeActionBehavior.Shrinkage,PrimaryOnly=true,Strain=-.0002}}).ToArray();return (m,w,e,new[]{m});}),
            ("force case not from its source case",()=>{var (m,w,e)=Sample();var q=m.LoadCases.Values.Single(c=>c.Name=="Q");
                w.Phases[2].Actions[0].SourceCaseGuid=w.Phases[1].Actions[0].ForceCaseGuid;w.Phases[1].Actions[0].SourceCaseGuid=q.Guid;return (m,w,e,new[]{m});}),
            ("missing source model",()=>{var (m,w,e)=Sample();return (m,w,e,Array.Empty<Model>());}),
            ("null sources",()=>{var (m,w,e)=Sample();return (m,w,e,null);}),
            ("invalid creep multiplier",()=>{var (m,w,e)=Sample();w.Phases[2].Actions[0].PsiL=0;return (m,w,e,new[]{m});}),
            ("unknown beam of an action",()=>{var (m,w,e)=Sample();w.Phases[0].Actions[0].BeamGuids=new[]{Guid.NewGuid()};return (m,w,e,new[]{m});}),
            ("stale section state",()=>{var (m,w,e)=Sample();var a=w.Phases[1].Actions[0];
                a.SectionStates=new[]{new BridgeBeamSectionState {BeamGuid=e.BeamGuid,Behavior=a.Behavior,ModularRatio=18,PhysicalPropertyFingerprint="stale"}};return (m,w,e,new[]{m});}),
            ("concrete exclusion of a whole phase with Compatible",()=>{var (m,w,e)=Sample();w.Phases[2].State=BridgeWorkflowSectionState.ConcreteExcluded;return (m,w,e,new[]{m});}),
        };
        foreach(var (name,make) in scenarios)
        {
            var (m1,w1,e1,s1)=make();var once=Capture(()=>BridgeWorkflowExecutor.BuildInput(w1,m1,s1!,e1,out _));
            var (m2,w2,_,s2)=make();var prepared=Capture(()=>BridgeWorkflowExecutor.Prepare(w2,m2,s2!));
            Assert.IsNotNull(once,name+": the one-shot call rejects");Assert.IsNotNull(prepared,name+": Prepare rejects");
            Assert.AreEqual(once!.GetType(),prepared!.GetType(),name);Assert.AreEqual(once.Message,prepared.Message,name);
        }
        var (m,w,e)=Sample();
        Assert.ThrowsException<ArgumentNullException>(()=>BridgeWorkflowExecutor.Prepare(null!,m,new[]{m}));
        Assert.ThrowsException<ArgumentNullException>(()=>BridgeWorkflowExecutor.Prepare(w,null!,new[]{m}));
        Assert.ThrowsException<ArgumentNullException>(()=>BridgeWorkflowExecutor.Prepare(w,m,new[]{m}).BuildInput(null!));
        static Exception? Capture(Action action){try{action();return null;}catch(Exception error){return error;}}
    }

    [TestMethod]public void APreparedPlanValidatesOnceKeepsItsWorkflowAndStillChecksEachEvaluation()
    {
        var (m,w,e)=Sample();var plan=BridgeWorkflowExecutor.Prepare(w,m,new[]{m});var expected=plan.BuildInput(e).Phases;
        // A copy of the workflow: a later change to it does not reach the plan (the one-shot call sees it).
        w.Phases[1].Actions[0].PsiL=1.1;
        CollectionAssert.AreEqual(expected,plan.BuildInput(e).Phases);Assert.AreEqual(1.1,Compile(m,w,e).Phases[1].PsiL);
        // Validated once: the model changed after Prepare (a new case changes its analysis fingerprint) is rejected by the one-shot calls,
        // while the plan reads the model of its preparation (the documented contract: prepare again after a change).
        m.AddLoadCase(new LoadCaseBase("later"));
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>Compile(m,w,e)).Message,"StaleBridgeWorkflowModel");
        CollectionAssert.AreEqual(expected,plan.BuildInput(e).Phases);
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>BridgeWorkflowExecutor.Prepare(w,m,new[]{m})).Message,"StaleBridgeWorkflowModel");
        // Each evaluation is still checked: beam, section, station, case bindings, result state.
        var (m2,w2,e2)=Sample();var plan2=BridgeWorkflowExecutor.Prepare(w2,m2,new[]{m2});
        var other=new BridgeWorkflowEvaluation {BeamGuid=Guid.NewGuid(),PhysicalPropertyFingerprint=e2.PhysicalPropertyFingerprint,SectionInput=e2.SectionInput,Station=e2.Station,
            Side=e2.Side,SectionAxes=e2.SectionAxes,Factors=e2.Factors,Results=e2.Results};
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>plan2.BuildInput(other)).Message,"UnknownBridgeEvaluationBeam");
        other.BeamGuid=e2.BeamGuid;other.PhysicalPropertyFingerprint="stale";
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>plan2.BuildInput(other)).Message,"StaleBridgeEvaluationSection");
        e2.Station=.25;e2.SectionAxes.Origin.X=250;
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>plan2.BuildInput(e2)).Message,"MissingBridgeResultStation");
        e2.Station=.5;e2.SectionAxes.Origin.X=500;First(m2).State.InputFingerprint="stale";
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>plan2.BuildInput(e2)).Message,"StaleBridgeResultState");
        First(m2).State.InputFingerprint=w2.AnalysisFingerprint;e2.Results.First().Value.Case="wrong";
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(()=>plan2.BuildInput(e2)).Message,"BridgeResultCaseBindingMismatch");
    }

    /// <summary>A girder of many beams with three cases each, as a bridge model: every beam evaluated at its middle.</summary>
    private static (Model Model,BridgeWorkflowDefinition Workflow,BridgeWorkflowEvaluation[] Evaluations) ManyBeams(int count)
    {
        var nodes=Enumerable.Range(0,count+1).Select(k=>new NodeElement(new Point3d(1000*k,0,0))).ToArray();var elements=new List<BeamElement>();
        for(int k=0;k<count;k++)
        {
            var beam=new BeamElement(nodes[k].Position,nodes[k+1].Position,null);beam.ConnectNodes(nodes[k],nodes[k+1]);
            beam.Assignments.Formulation=GPC.Model.Structure.Assignments.BeamFormulation.StraightTwoNode;elements.Add(beam);
        }
        var model=ModelAssembly.Assemble(elements,Guid.NewGuid());
        var loads=new[]{new LoadCaseBase("G1"),new LoadCaseBase("G2"),new LoadCaseBase("Q")};foreach(var load in loads)model.AddLoadCase(load);
        string fp=model.AnalysisFingerprint();
        model.Datasets.Add("analysis",new AnalysisDataset {Id="analysis",InputFingerprint=fp,NormalizedUnits="N,mm,rad",Semantics=AnalysisSemantics.LinearStatic});
        var evaluations=new List<BridgeWorkflowEvaluation>();var input=CompositeBridgeApiTests.Input() with {Phases=Array.Empty<BridgePhase>()};
        foreach(var beam in model.BeamElements.Values.OrderBy(b=>b.Id))
        {
            var axes=new CoordinateSystem(beam.StartPoint+(beam.EndPoint-beam.StartPoint)*.5,new Vector3d(0,1,0),new Vector3d(0,0,1));
            for(int i=0;i<loads.Length;i++)
            {
                var sample=new StationResultBeamForces(loads[i],new ResultBeamForces(1e3*(i+1),0,2e3*i,0,(1500+500*i+beam.Id)*1e6,0,axes),.5)
                {Side=SectionSide.Left,Body=ActionBody.PositiveSectionFace,State=new ResultState {DatasetId="analysis",InputFingerprint=fp,
                    Semantics=AnalysisSemantics.LinearStatic,IsCombined=false,IsCumulative=true,ConcomitantStateId=loads[i].Name,
                    Components=Enumerable.Repeat(ComponentAvailability.Available,6).ToArray()}};
                beam.Results.Add(new BeamResult(new[]{sample}));
            }
            evaluations.Add(new BridgeWorkflowEvaluation {BeamGuid=beam.Guid,PhysicalPropertyFingerprint=BridgeWorkflowDefinition.PropertyFingerprint(beam),SectionInput=input,
                Station=.5,Side=SectionSide.Left,SectionAxes=axes,LongTermPhi=2,Factors=loads.ToDictionary(l=>l.Guid,l=>1.35),
                Results=loads.ToDictionary(l=>l.Guid,l=>new ResultSelection {Dataset="analysis",Case=l.Name})});
        }
        var workflow=new BridgeWorkflowDefinition {ModelGuid=model.Guid,AnalysisFingerprint=fp,
            Phases=loads.Select((load,i)=>new BridgeWorkflowPhaseDefinition {Id=Guid.NewGuid(),Order=i,Name=load.Name,
                State=i==0?BridgeWorkflowSectionState.Steel:BridgeWorkflowSectionState.Composite,
                Actions=new[]{new BridgeWorkflowActionDefinition {Key=load.Name,ForceCaseGuid=load.Guid,SourceCaseGuid=load.Guid,
                    SourceModelGuid=model.Guid,SourceAnalysisFingerprint=fp,PsiL=i==1?1.1:null,
                    Behavior=i==0?BridgeActionBehavior.Steel:i==1?BridgeActionBehavior.LongTerm:BridgeActionBehavior.ShortTerm}}}).ToArray()};
        return (model,workflow,evaluations.ToArray());
    }

    [TestMethod]public void APreparedPlanOfManyBeamsGivesTheOneShotInputsWithoutTheirValidation()
    {
        // 300 beams: one evaluation per beam, every input identical; the plan does not compute the analysis fingerprints at each evaluation.
        var (m,w,evaluations)=ManyBeams(300);
        var watch=System.Diagnostics.Stopwatch.StartNew();var once=evaluations.Select(e=>BridgeWorkflowExecutor.BuildInput(w,m,new[]{m},e,out _)).ToArray();double oneShot=watch.Elapsed.TotalSeconds;
        watch.Restart();var plan=BridgeWorkflowExecutor.Prepare(w,m,new[]{m});double preparation=watch.Elapsed.TotalSeconds;
        watch.Restart();var prepared=evaluations.Select(e=>plan.BuildInput(e,out _)).ToArray();double planned=watch.Elapsed.TotalSeconds;
        for(int k=0;k<evaluations.Length;k++)CollectionAssert.AreEqual(once[k].Phases,prepared[k].Phases,"beam "+k);
        Console.WriteLine($"D3 plan, {evaluations.Length} evaluations of a girder of {m.BeamElements.Count} beams: one-shot {oneShot:F3} s ({1e3*oneShot/evaluations.Length:F2} ms each); " +
            $"Prepare {preparation:F3} s, then {planned:F3} s ({1e3*planned/evaluations.Length:F3} ms each)");
    }
}
