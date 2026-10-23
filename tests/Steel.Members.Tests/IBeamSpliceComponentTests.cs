using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.IBeamSplicePlateTests;

namespace Steel.Members.Tests;

[TestClass]
public class IBeamSpliceComponentTests
{
    private static IBeamSplicePlateInput Low(bool ntc=false)=>Input(ntc,forces:IBeamSpliceForceTests.Input(actions:new(-75000,-75000,0,0,0,-100e6)));
    private static IBeamSpliceBoltSlipInput Slip(IBeamSplicePlateInput p,BoltSlipLimitState state=BoltSlipLimitState.Serviceability,
        IBeamSpliceForceInput? forces=null,IBeamSpliceBoltSet? bolts=null,ConnectionDesignCode? code=null,string? source="Independent P398 SLS",
        double friction=.5)
    {
        code??=p.Policy.Code;
        return new(code.IsNtc?BoltSlipPolicy.Ntc2018(code):BoltSlipPolicy.Eurocode3(code,1.25,1.1),
            forces??IBeamSpliceForceTests.Input(true),bolts??p.Bolts,state,friction,friction,
            BoltTighteningControl.Controlled,"Qualified assemblies","Qualified surfaces",source);
    }
    private static void Blocked(IBeamSpliceComponentResult r,string reason)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentAssessment.Status);Assert.IsNull(r.ComponentAssessment.Utilization);
        Assert.AreEqual(0,r.ComponentAssessment.Checks.Count);
        Assert.IsTrue(r.ComponentAssessment.Diagnostics.Any(d=>d.Contains(reason)),string.Join(";",r.ComponentAssessment.Diagnostics));
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void PassingComponentEnvelopeStillCannotQualifyTheWholeJoint(bool ntc)
    {
        var r=IBeamSpliceComponents.Verify(new(Low(ntc)));
        Assert.AreEqual(ConnectionStatus.Passed,r.ComponentAssessment.Status);Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("WEB_COVER_BLOCK_TEARING_UNDER_N_V_M"));
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("MEMBER_CLASSIFICATION_AND_LOCAL_GLOBAL_RESISTANCE"));
        StringAssert.Contains(r.ComponentAssessment.Scope,"COMPONENT ENVELOPE ONLY");
    }
    [TestMethod]
    public void FullP398LoadIsControlledByTheDeclaredConservativeBearingInteraction()
    {
        var r=IBeamSpliceComponents.Verify(new(Input()));
        Assert.AreEqual(ConnectionStatus.Failed,r.ComponentAssessment.Status);
        Equal(1.1865834167227851,r.ComponentAssessment.Utilization!.Value);
        StringAssert.Contains(r.ComponentAssessment.GoverningCheck.Id,"Bearing.Left.Web.Member");
        Assert.AreEqual(ConnectionStatus.Passed,r.BoltStrength.Status);Assert.AreEqual(ConnectionStatus.Passed,r.WebSections.Status);
    }
    [TestMethod]
    public void SeparateSlsCanGovernWithoutAnyAutomaticScalingFromUls()
    {
        var p=Low();var r=IBeamSpliceComponents.Verify(new(p,BoltSlipLimitState.Serviceability,new[]{Slip(p)}));
        Assert.AreEqual(ConnectionStatus.Passed,r.ComponentAssessment.Status);
        Equal(.730956089233456,r.ComponentAssessment.Utilization!.Value);
        StringAssert.Contains(r.ComponentAssessment.GoverningCheck.Id,"SlipCase1.");
        Equal(-100000,r.SlipCases.Single().Input.Forces.Actions.Fx);Equal(-75000,r.Bearing.Input.Forces.Actions.Fx);
    }
    [DataTestMethod][DataRow(BoltSlipLimitState.Serviceability)][DataRow(BoltSlipLimitState.Ultimate)]
    public void RequiredSlipStateIsNeverOmittedOrSubstituted(BoltSlipLimitState required)
    {
        var p=Low();Blocked(IBeamSpliceComponents.Verify(new(p,required)),"REQUIRED_SLIP");
        var wrong=required==BoltSlipLimitState.Serviceability?BoltSlipLimitState.Ultimate:BoltSlipLimitState.Serviceability;
        Blocked(IBeamSpliceComponents.Verify(new(p,required,new[]{Slip(p,wrong)})),"REQUIRED_SLIP");
    }
    [TestMethod]
    public void UltimateSlipDoesNotImplicitlyCloseCategoryCNetPlasticResistance()
    {
        var p=Low();var r=IBeamSpliceComponents.Verify(new(p,BoltSlipLimitState.Ultimate,new[]{Slip(p,BoltSlipLimitState.Ultimate,forces:p.Forces)}));
        Assert.AreEqual(ConnectionStatus.Passed,r.ComponentAssessment.Status);
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("ULTIMATE_SLIP_CATEGORY_NET_PLASTIC_RESISTANCE"));
    }
    [TestMethod]
    public void EveryRequestedCombinationIsEvaluatedAndTheWeakestIsNotLost()
    {
        var p=Low();var r=IBeamSpliceComponents.Verify(new(p,BoltSlipLimitState.Serviceability,
            new[]{Slip(p),Slip(p,friction:.2)}));
        Assert.AreEqual(2,r.SlipCases.Count);Assert.AreEqual(ConnectionStatus.Failed,r.ComponentAssessment.Status);
        Equal(.730956089233456*.5/.2,r.ComponentAssessment.Utilization!.Value);
        StringAssert.Contains(r.ComponentAssessment.GoverningCheck.Id,"SlipCase2.");
    }
    [TestMethod]
    public void EqualIndependentGeometrySnapshotsAreAccepted()
    {
        var p=Low();var clone=Input();
        var r=IBeamSpliceComponents.Verify(new(p,BoltSlipLimitState.Serviceability,new[]{Slip(p,bolts:clone.Bolts,forces:IBeamSpliceForceTests.Input(true))}));
        Assert.AreEqual(ConnectionStatus.Passed,r.ComponentAssessment.Status);
    }
    [TestMethod]
    public void MixingNationalPoliciesIsRejectedBeforeSlipEvaluation()
    {
        var p=Low();var r=IBeamSpliceComponents.Verify(new(p,slipCases:new[]{Slip(p,code:ConnectionDesignCode.Ntc2018())}));
        Blocked(r,"CODE_MISMATCH");Assert.AreEqual(0,r.SlipCases.Single().Checks.Count);
    }
    [TestMethod]
    public void MixingPartialFactorsWithinEcIsAlsoRejected()
    {
        var p=Low();Blocked(IBeamSpliceComponents.Verify(new(p,slipCases:new[]{Slip(p,
            code:ConnectionDesignCode.Eurocode3(1,1.25,1.25,"Different plate factor"))})),"CODE_MISMATCH");
    }
    [DataTestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void ChangedPhysicalDetailCannotBorrowAnotherDetailsSlipResult(int variant)
    {
        var p=Low();IBeamSpliceForceInput forces=IBeamSpliceForceTests.Input(true);IBeamSpliceBoltSet bolts=p.Bolts;
        if(variant==0)forces=IBeamSpliceForceTests.Input(true,web:new(2,3,80,120,70));
        if(variant==1)forces=IBeamSpliceForceTests.Input(true,iy:294100000);
        if(variant==2)bolts=new(new(20,22,245,ConnectionBoltGrade.Class10_9,true),p.Bolts.WebBolt,37,37);
        if(variant==3)bolts=new(p.Bolts.FlangeBolt,p.Bolts.WebBolt,38,37);
        Blocked(IBeamSpliceComponents.Verify(new(p,slipCases:new[]{Slip(p,forces:forces,bolts:bolts)})),"PHYSICAL_DETAIL_MISMATCH");
    }
    [TestMethod]
    public void AnyUnsupportedComponentSuppressesTheEnvelopeButKeepsTypedEvidence()
    {
        var p=Low();var r=IBeamSpliceComponents.Verify(new(p,slipCases:new[]{Slip(p,source:null)}));
        Blocked(r,"COMBINATION");Assert.AreEqual(ConnectionStatus.Passed,r.BoltStrength.Status);
        Assert.AreEqual(ConnectionStatus.Passed,r.Bearing.Status);Assert.IsTrue(r.Bearing.Checks.Count>0);
    }
    [TestMethod]
    public void InputAndResultCollectionsCannotBeChangedAfterCalculation()
    {
        var p=Low();var cases=new List<IBeamSpliceBoltSlipInput>{Slip(p)};
        var input=new IBeamSpliceComponentInput(p,BoltSlipLimitState.Serviceability,cases);cases.Clear();
        Assert.AreEqual(1,input.SlipCases.Count);var r=IBeamSpliceComponents.Verify(input);
        Assert.ThrowsException<NotSupportedException>(()=>((IList<string>)r.MissingWholeJointChecks).Clear());
        Assert.ThrowsException<NotSupportedException>(()=>((IList<ConnectionVerification<IBeamSpliceBoltSlipInput>>)r.SlipCases).Clear());
    }
    [TestMethod]
    public void EveryAggregatedCheckRetainsSourceUnitAndUniqueIdentity()
    {
        var p=Low();var r=IBeamSpliceComponents.Verify(new(p,slipCases:new[]{Slip(p)}));
        Assert.AreEqual(r.ComponentAssessment.Checks.Count,r.ComponentAssessment.Checks.Select(c=>c.Id).Distinct().Count());
        foreach(var c in r.WebSections.Checks)
        {
            var a=r.ComponentAssessment.Checks.Single(x=>x.Id=="WebSections."+c.Id);
            Equal(c.Resistance,a.Resistance);Equal(c.Demand,a.Demand);Assert.AreEqual(c.Reference,a.Reference);Assert.AreEqual(c.Unit,a.Unit);
        }
    }
    [TestMethod]
    public void InvalidContractIsRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(()=>IBeamSpliceComponents.Verify(null!));
        Assert.ThrowsException<ArgumentNullException>(()=>new IBeamSpliceComponentInput(null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new IBeamSpliceComponentInput(Low(),(BoltSlipLimitState)99));
        Assert.ThrowsException<ArgumentException>(()=>new IBeamSpliceComponentInput(Low(),slipCases:new IBeamSpliceBoltSlipInput[]{null!}));
    }
}
