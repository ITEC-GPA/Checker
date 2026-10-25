using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class WeldedIBeamSpliceEnvelopeTests
{
    static WeldedSpliceUltimateCase U(string id,double n=2e5,bool ntc=false)=>new(id,WeldedIBeamSpliceDetailTests.Input(ntc,detail:WeldedIBeamSplicePlateTests.Detail(new(n,0,0,0,0,0))));
    static WeldedSpliceSlipCase S(string id,string? u=null,double n=2e5,bool ntc=false,double mu=.4)
        =>new(id,WeldedIBeamSpliceSlipTests.Input(ntc,u==null?BoltSlipLimitState.Serviceability:BoltSlipLimitState.Ultimate,
            WeldedIBeamSplicePlateTests.Detail(new(n,0,0,0,0,0)),mu),u);
    [DataTestMethod][DataRow(false,SpliceBoltCategory.A)][DataRow(true,SpliceBoltCategory.A)]
    [DataRow(false,SpliceBoltCategory.B)][DataRow(true,SpliceBoltCategory.B)][DataRow(false,SpliceBoltCategory.C)][DataRow(true,SpliceBoltCategory.C)]
    public void SamePhysicalDetailAndSeparateConcomitantCasesHaveAComponentEnvelope(bool ntc,SpliceBoltCategory category)
    {
        var slip=category==SpliceBoltCategory.A?Array.Empty<WeldedSpliceSlipCase>():category==SpliceBoltCategory.B?new[]{S("sls",n:1e5,ntc:ntc)}:new[]{S("slip1","u1",ntc:ntc),S("slip2","u2",-1e5,ntc)};
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{U("u1",ntc:ntc),U("u2",-1e5,ntc)},category,slip));
        Assert.AreEqual(ConnectionStatus.Passed,r.ComponentStatus,string.Join(";",r.Diagnostics));
        Assert.IsTrue(r.IsComponentEnvelopeComplete);Assert.IsFalse(r.CanQualifyWholeJoint);Assert.IsNotNull(r.GoverningSource);
        Assert.AreEqual(category==SpliceBoltCategory.C,r.UltimateCases.All(c=>c.NetAxialPlastic!=null));
        if(category==SpliceBoltCategory.B)Assert.IsTrue(r.CheckSources.Where(c=>c.CaseId=="sls").All(c=>c.IsServiceability&&c.UltimateCaseId==null));
        if(category==SpliceBoltCategory.C)Assert.IsTrue(r.CheckSources.Where(c=>c.CaseId=="slip2").All(c=>!c.IsServiceability&&c.UltimateCaseId=="u2"));
    }
    [DataTestMethod][DataRow(SpliceBoltCategory.B)][DataRow(SpliceBoltCategory.C)]
    public void RequiredLimitStateIsNeverSynthesized(SpliceBoltCategory category)
    {
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{U("u")},category));Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentStatus);
        Assert.IsFalse(r.IsComponentEnvelopeComplete);Assert.IsNull(r.GoverningSource);Assert.AreEqual(0,r.CheckSources.Count);
    }
    [TestMethod]
    public void EveryUltimateCaseRequiresItsOwnBoundCategoryCSlipAndNetCheck()
    {
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{U("small"),U("large",3e5)},SpliceBoltCategory.C,new[]{S("slip","small")}));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentStatus);Assert.IsTrue(r.Diagnostics.Contains("large:CATEGORY_C_BOUND_ULTIMATE_SLIP_REQUIRED"));
        Assert.AreEqual(2,r.UltimateCases.Count(c=>c.NetAxialPlastic!=null));
        var first=r.UltimateCases[0].NetAxialPlastic.Checks.Single(c=>c.Id=="Top.Parent.NetAxialPlastic").Demand;
        var second=r.UltimateCases[1].NetAxialPlastic.Checks.Single(c=>c.Id=="Top.Parent.NetAxialPlastic").Demand;
        WeldedIBeamSpliceTests.Close(1.5*first,second);
    }
    [TestMethod]
    public void OptionalUltimateSlipAlsoActivatesCategoryCForEveryUltimateState()
    {
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{U("one"),U("two")},SpliceBoltCategory.A,new[]{S("slip","one")}));
        Assert.IsTrue(r.Input.RequiresCategoryC);Assert.IsTrue(r.UltimateCases.All(u=>u.NetAxialPlastic!=null));
        Assert.IsTrue(r.Diagnostics.Contains("two:CATEGORY_C_BOUND_ULTIMATE_SLIP_REQUIRED"));
    }
    [DataTestMethod][DataRow("actions")][DataRow("pole")][DataRow("case")][DataRow("sls-binding")]
    public void WrongActionOrCaseBindingCannotBorrowAnOtherwisePassingSlip(string flaw)
    {
        var s=S("s","u",flaw=="actions"?1e5:2e5);
        if(flaw=="case")s=new(s.Id,s.Input,"other");
        if(flaw=="sls-binding")s=new(s.Id,S("s").Input,"u");
        if(flaw=="pole")s=new(s.Id,WeldedIBeamSpliceSlipTests.Input(state:BoltSlipLimitState.Ultimate,
            detail:WeldedIBeamSplicePlateTests.Detail(new(2e5,0,0,0,0,0,applicationX:10))),"u");
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{U("u")},SpliceBoltCategory.C,new[]{s}));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentStatus);Assert.IsFalse(r.SlipCases.Single().IsBound);
    }
    [DataTestMethod][DataRow("material")][DataRow("thickness")][DataRow("hole")][DataRow("hardware")][DataRow("weld-toe")][DataRow("code")]
    public void PhysicalBindingChecksAllMaterialsGeometryBoltsAndPolicy(string flaw)
    {
        var u=U("u");var b=u.Detail.Plates.Bolts;var d=b.Detail;
        if(flaw=="material"||flaw=="thickness")d=new(d.Section,new(new(d.Top.Outer.Width,flaw=="thickness"?21:d.Top.Outer.Thickness,
            flaw=="material"?275:d.Top.Outer.Fy,d.Top.Outer.Fu),d.Top.EachInner,d.Top.InnerGap,d.Top.EndDistance,d.Top.Bolts),d.Bottom,d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
        if(flaw=="weld-toe")d=new(new(d.Section.TopFlange,d.Section.Web,d.Section.BottomFlange,9,8),d.Top,d.Bottom,d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
        var other=flaw=="code"?WeldedIBeamSpliceTests.BoltInput(true,d):new WeldedIBeamSpliceBoltInput(b.Code,d,
            flaw=="hole"?new(24,27,353,ConnectionBoltGrade.Class8_8,true):b.FlangeBolt,b.WebBolt,flaw=="hardware"?41:40,40,"ULS","NTC source");
        var policy=flaw=="code"?IBeamSplicePlatePolicy.Ntc2018():IBeamSplicePlatePolicy.Eurocode3(other.Code,1);
        var changed=new WeldedSpliceUltimateCase("changed",new(new(policy,other),u.Detail.Exposure));
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{u,changed},SpliceBoltCategory.A));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentStatus);Assert.IsTrue(r.UltimateCases[1].BindingDiagnostics.Contains("ULTIMATE_PHYSICAL_DETAIL_OR_CODE_MISMATCH"));
    }
    [TestMethod]
    public void OnePhysicalSurfaceCannotAcquireABetterCoefficientInAnotherCombination()
    {
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{U("u")},SpliceBoltCategory.B,new[]{S("s1",mu:.4),S("s2",mu:.5)}));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentStatus);Assert.IsFalse(r.SlipCases[1].IsBound);
    }
    [TestMethod]
    public void KnownFailureRemainsVisibleWithAnUnsupportedMixedStressBlock()
    {
        var input=WeldedIBeamSpliceDetailTests.Input(detail:WeldedIBeamSplicePlateTests.Detail(new(1e9,1e8,0,0,0,1e10)));
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{new WeldedSpliceUltimateCase("failed",input)},SpliceBoltCategory.B));
        Assert.IsTrue(r.HasFailedComponent);Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentStatus);
        Assert.IsTrue(r.UltimateCases[0].Plates.HasFailedComponent);Assert.IsFalse(r.CanQualifyWholeJoint);
    }
    [TestMethod]
    public void AggregateSourceReferencesTheOriginalImmutableCheck()
    {
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{U("u")},SpliceBoltCategory.C,new[]{S("slip","u")}));
        var source=r.CheckSources.First(c=>c.CaseId=="slip");
        Assert.IsTrue(r.SlipCases[0].Assessment.Checks.Any(c=>ReferenceEquals(c,source.Check)));
    }
    [TestMethod]
    public void ReorderingCasesCannotSynthesizeIndependentForceMaxima()
    {
        var cases=new[]{U("low",1e5),U("high",3e5)};
        var a=WeldedIBeamSpliceEnvelope.Verify(new(cases,SpliceBoltCategory.A));
        var b=WeldedIBeamSpliceEnvelope.Verify(new(cases.Reverse(),SpliceBoltCategory.A));
        Assert.AreEqual(a.ComponentStatus,b.ComponentStatus);Assert.AreEqual(a.GoverningSource.Check.Utilization,b.GoverningSource.Check.Utilization);
        foreach(var c in a.UltimateCases)Assert.AreEqual(c.Input.Detail.Plates.Bolts.Detail.Actions.Fx,
            b.UltimateCases.Single(x=>x.Input.Id==c.Input.Id).Input.Detail.Plates.Bolts.Detail.Actions.Fx);
    }
    [TestMethod]
    public void CaseCollectionsAreCopiedAndIdentifiersAreValidated()
    {
        var list=new List<WeldedSpliceUltimateCase>{U("u")};var input=new WeldedIBeamSpliceEnvelopeInput(list,SpliceBoltCategory.A);list.Clear();
        Assert.AreEqual(1,input.UltimateCases.Count);
        Assert.ThrowsException<ArgumentException>(()=>new WeldedIBeamSpliceEnvelopeInput(new[]{U("same"),U("same")},SpliceBoltCategory.A));
        Assert.ThrowsException<ArgumentException>(()=>new WeldedIBeamSpliceEnvelopeInput(Array.Empty<WeldedSpliceUltimateCase>(),SpliceBoltCategory.A));
        Assert.ThrowsException<ArgumentNullException>(()=>WeldedIBeamSpliceEnvelope.Verify(null!));
    }
    [TestMethod]
    public void CompleteFailedComponentsHaveAFailedEnvelopeAndOriginalGoverningSource()
    {
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{U("large",1e8)},SpliceBoltCategory.C,new[]{S("slip","large",1e8)}));
        Assert.IsTrue(r.IsComponentEnvelopeComplete,string.Join(";",r.Diagnostics));
        Assert.IsTrue(r.HasFailedComponent);Assert.AreEqual(ConnectionStatus.Failed,r.ComponentStatus);
        Assert.IsTrue(r.GoverningSource.Check.Utilization>1);
    }
    [TestMethod]
    public void FailedChangedDetailCannotBeHiddenByAnUnboundEnvelope()
    {
        var bad=WeldedIBeamSpliceDetailTests.Input(detail:WeldedIBeamSplicePlateTests.Detail(new(1e8,0,0,0,0,0),outerThickness:21));
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{U("good"),new WeldedSpliceUltimateCase("bad",bad)},SpliceBoltCategory.A));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentStatus);Assert.IsTrue(r.HasFailedComponent);
        Assert.IsTrue(r.UltimateCases[1].HasFailedComponent);Assert.AreEqual(0,r.CheckSources.Count);
    }
    [DataTestMethod][DataRow("gammaM1")][DataRow("exposure")]
    public void SameGeometryCannotMixBucklingFactorsOrEnvironmentalDetails(string change)
    {
        var first=U("u");var plate=first.Detail.Plates;
        var other=new WeldedSpliceUltimateCase("other",new(new(change=="gammaM1"?IBeamSplicePlatePolicy.Eurocode3(plate.Policy.Code,1.1):plate.Policy,plate.Bolts),
            change=="exposure"?SpliceExposure.ProtectedCarbonSteel:first.Detail.Exposure));
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{first,other},SpliceBoltCategory.A));
        Assert.IsTrue(r.UltimateCases[1].BindingDiagnostics.Contains("ULTIMATE_EXPOSURE_OR_BUCKLING_POLICY_MISMATCH"));
    }
    [TestMethod]
    public void ChangingSlipFactorsBetweenCombinationsCannotHideADemand()
    {
        var first=S("s1");var p=first.Input;
        var second=new WeldedSpliceSlipCase("s2",new(BoltSlipPolicy.Eurocode3(p.Policy.Code,1.25,1),p.Bolts,p.LimitState,p.Surfaces,p.TighteningControl,p.AssemblyQualificationSource,p.ActionCombinationSource));
        var r=WeldedIBeamSpliceEnvelope.Verify(new(new[]{U("u")},SpliceBoltCategory.B,new[]{first,second}));
        Assert.IsFalse(r.SlipCases[1].IsBound);Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentStatus);
    }
}
