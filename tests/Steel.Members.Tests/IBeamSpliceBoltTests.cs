using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class IBeamSpliceBoltTests
{
    private static ConnectionDesignCode Ec()=>ConnectionDesignCode.Eurocode3(1,1.1,1.25,"SCI P398 UK example; factors for this benchmark only");
    private static IBeamSpliceBoltSet Bolts(string? holes="NTC fixture clearance check",double flangeEnvelope=37,double webEnvelope=37,
        ConnectionBolt? flange=null,ConnectionBolt? web=null)
        =>new(flange??new(20,22,245,ConnectionBoltGrade.Class8_8,true),web??new(20,22,245,ConnectionBoltGrade.Class8_8,true),
            flangeEnvelope,webEnvelope,holes);
    private static IBeamSpliceBoltStrengthInput Strength(ConnectionDesignCode? code=null,IBeamSpliceForceInput? forces=null,
        IBeamSpliceBoltSet? bolts=null,string? source="P398 D.1 ULS")
        =>new(code??Ec(),forces??IBeamSpliceForceTests.Input(),bolts??Bolts(),source);
    private static IBeamSpliceBoltSlipInput Slip(ConnectionDesignCode? code=null,IBeamSpliceForceInput? forces=null,
        IBeamSpliceBoltSet? bolts=null,BoltSlipLimitState state=BoltSlipLimitState.Serviceability,
        double muFlange=.5,double muWeb=.5,BoltTighteningControl control=BoltTighteningControl.Controlled,
        string? assembly="Qualified preloaded assemblies",string? surface="Class A surfaces",string? source="P398 D.1 SLS")
    {
        code??=Ec();
        var policy=code.IsNtc?BoltSlipPolicy.Ntc2018(code):BoltSlipPolicy.Eurocode3(code,1.25,1.1);
        return new(policy,forces??IBeamSpliceForceTests.Input(true),bolts??Bolts(),state,muFlange,muWeb,control,assembly,surface,source);
    }
    private static ConnectionCheck Check<T>(ConnectionVerification<T> r,string suffix) where T:class
        =>r.Checks.Single(c=>c.Id==suffix);
    private static void Equal(double expected,double actual)=>Assert.AreEqual(expected,actual,Math.Max(1,Math.Abs(expected))*1e-10);
    private static void Unsupported<T>(ConnectionVerification<T> r,string reason)where T:class
    {
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsFalse(r.IsComplete);Assert.AreEqual(0,r.Checks.Count);
        Assert.IsNull(r.Utilization);Assert.IsNull(r.GoverningCheck);
        Assert.IsTrue(r.Diagnostics.Any(d=>d.Contains(reason)),string.Join(";",r.Diagnostics));
    }
    [DataTestMethod][DataRow("Left.TopFlange",1)][DataRow("Right.TopFlange",1)]
    [DataRow("Left.BottomFlange",1)][DataRow("Right.BottomFlange",1)][DataRow("Left.Web",2)][DataRow("Right.Web",2)]
    public void EveryGroupUsesTheIndependentEcBoltCapacityAndCorrectNumberOfPlanes(string group,int planes)
    {
        var r=IBeamSpliceBolts.VerifyStrength(Strength());Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        // SCI P398 p132: 94.1/188kN; unrounded EN Table3.4 = 0.6*800*245/1.25 per plane.
        Equal(planes*94080,Check(r,group+".Bolt1.Bolt.Shear").Resistance);
        Equal(0,Check(r,group+".Bolt1.Bolt.Tension").Demand);
        Assert.AreEqual(36,r.Checks.Count(c=>c.Id.EndsWith(".Bolt.Shear")));
    }
    [DataTestMethod][DataRow("Left.TopFlange",1)][DataRow("Right.TopFlange",1)]
    [DataRow("Left.BottomFlange",1)][DataRow("Right.BottomFlange",1)][DataRow("Left.Web",2)][DataRow("Right.Web",2)]
    public void EveryGroupMatchesIndependentP398SlsSlipCapacity(string group,int planes)
    {
        var r=IBeamSpliceBolts.VerifySlip(Slip());Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        // P398 p132, 62.4/125kN rounded. Exact 0.7*800*245*0.5/1.10 per interface.
        Equal(planes*62363.6363636363636,Check(r,group+".Bolt1.Slip.Serviceability").Resistance);
        Equal(137200,r.Trace.Single(t=>t.Name==group+".Bolt1.NominalPreload").Value);
        Assert.AreEqual(36,r.Checks.Count(c=>c.Id.EndsWith(".Slip.Serviceability")));
    }
    [TestMethod]
    public void WholeBoltSetHasTheIndependentlyCalculatedGoverningComponent()
    {
        var uls=IBeamSpliceBolts.VerifyStrength(Strength());
        Equal(.7284331833382052,uls.Utilization!.Value);StringAssert.Contains(uls.GoverningCheck.Id,"BottomFlange");
        var sls=IBeamSpliceBolts.VerifySlip(Slip());
        Equal(.730956089233456,sls.Utilization!.Value);StringAssert.Contains(sls.GoverningCheck.Id,"BottomFlange");
        StringAssert.Contains(uls.Scope,"BOLT COMPONENTS ONLY");StringAssert.Contains(sls.Scope,"BOLT COMPONENTS ONLY");
    }
    [TestMethod]
    public void WebDemandIsTheTotalBoltResultantNotAlreadyDividedByTwo()
    {
        var uls=IBeamSpliceBolts.VerifyStrength(Strength());
        Equal(121695.04530109138,uls.Checks.Where(c=>c.Id.Contains(".Web.")&&c.Id.EndsWith(".Bolt.Shear")).Max(c=>c.Demand));
        var sls=IBeamSpliceBolts.VerifySlip(Slip());
        Equal(81014.50291524096,sls.Checks.Where(c=>c.Id.Contains(".Web.")&&c.Id.EndsWith(".Slip.Serviceability")).Max(c=>c.Demand));
    }
    [TestMethod]
    public void TensionFlangeAxialLoadIsInPlaneBoltShear()
    {
        var r=IBeamSpliceBolts.VerifyStrength(Strength());
        Equal(325010.52473425885/6,Check(r,"Left.TopFlange.Bolt1.Bolt.Shear").Demand);
        Assert.IsTrue(r.Checks.Where(c=>c.Id.EndsWith(".Bolt.Tension")).All(c=>c.Demand==0));
    }
    [TestMethod]
    public void NtcAndEcShareDistributionButRetainSeparatePolicies()
    {
        var ec=IBeamSpliceBolts.VerifyStrength(Strength());var ntc=IBeamSpliceBolts.VerifyStrength(Strength(ConnectionDesignCode.Ntc2018()));
        Assert.AreEqual(ConnectionStatus.Passed,ntc.Status);
        for(int i=0;i<ec.Checks.Count;i++){Equal(ec.Checks[i].Demand,ntc.Checks[i].Demand);Equal(ec.Checks[i].Resistance,ntc.Checks[i].Resistance);}
        // The shared bolt-only interaction is explicitly EC complementary guidance;
        // the NTC assembled resistance interaction also needs bearing/punching.
        Assert.IsTrue(ntc.Checks.Where(c=>c.Id.EndsWith(".Bolt.Shear")||c.Id.EndsWith(".Bolt.Tension"))
            .All(c=>c.Reference.Contains("NTC")));
        var changed=IBeamSpliceBolts.VerifyStrength(Strength(ConnectionDesignCode.Eurocode3(1,1.25,1.5,"Explicit sensitivity")));
        Equal(78400,Check(changed,"Left.TopFlange.Bolt1.Bolt.Shear").Resistance);
        Equal(.7284331833382052*1.5/1.25,changed.Utilization!.Value);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void NtcRequiresHoleClearanceEvidenceInBothPaths(bool slip)
    {
        if(slip)Unsupported(IBeamSpliceBolts.VerifySlip(Slip(ConnectionDesignCode.Ntc2018(),bolts:Bolts(null))),"Clearance");
        else Unsupported(IBeamSpliceBolts.VerifyStrength(Strength(ConnectionDesignCode.Ntc2018(),bolts:Bolts(null))),"Clearance");
    }
    [TestMethod]
    public void SlsIsAnIndependentCombinationNotAnAutomaticFractionOfUls()
    {
        var actions=IBeamSpliceForceTests.Input(actions:new(90000,45000,0,0,0,-50000000));
        var r=IBeamSpliceBolts.VerifySlip(Slip(forces:actions));
        var distribution=IBeamSpliceForces.Calculate(actions);
        foreach(var g in distribution.BoltGroups)foreach(var f in g.Distribution.Forces)
            Equal(f.Shear,Check(r,$"{g.Side}.{g.Part}.Bolt{f.Index+1}.Slip.Serviceability").Demand);
        Assert.AreSame(actions,r.Input.Forces);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void LimitStateChangesSlipFactorWithoutChangingTheSuppliedDemands(bool ntc)
    {
        var code=ntc?ConnectionDesignCode.Ntc2018():Ec();
        var sls=IBeamSpliceBolts.VerifySlip(Slip(code));
        var uls=IBeamSpliceBolts.VerifySlip(Slip(code,state:BoltSlipLimitState.Ultimate,source:"Explicit ULS slip case"));
        Equal(54880,Check(uls,"Left.TopFlange.Bolt1.Slip.Ultimate").Resistance);
        Equal(Check(sls,"Left.TopFlange.Bolt1.Slip.Serviceability").Demand,Check(uls,"Left.TopFlange.Bolt1.Slip.Ultimate").Demand);
    }
    [TestMethod]
    public void NtcUncontrolledTighteningDoesNotAlterEcPolicy()
    {
        var ntc=IBeamSpliceBolts.VerifySlip(Slip(ConnectionDesignCode.Ntc2018(),control:BoltTighteningControl.Uncontrolled));
        Equal(62363.63636363636/1.1,Check(ntc,"Left.TopFlange.Bolt1.Slip.Serviceability").Resistance);
        Unsupported(IBeamSpliceBolts.VerifySlip(Slip(control:BoltTighteningControl.Uncontrolled)),"EC_CONTROLLED_TIGHTENING_REQUIRED");
    }
    [TestMethod]
    public void DifferentSurfaceConditionsAreAppliedToTheCorrespondingPhysicalPlanes()
    {
        var r=IBeamSpliceBolts.VerifySlip(Slip(muFlange:.3,muWeb:.4));
        Equal(37418.18181818182,Check(r,"Left.TopFlange.Bolt1.Slip.Serviceability").Resistance);
        Equal(99781.81818181818,Check(r,"Left.Web.Bolt1.Slip.Serviceability").Resistance);
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);
    }
    [TestMethod]
    public void DifferentBoltGradesAndDiametersAreNotInterchangedBetweenWebAndFlanges()
    {
        var set=Bolts(web:new(24,26,353,ConnectionBoltGrade.Class10_9,true),webEnvelope:44);
        var uls=IBeamSpliceBolts.VerifyStrength(Strength(bolts:set));
        Equal(282400,Check(uls,"Left.Web.Bolt1.Bolt.Shear").Resistance);
        Equal(94080,Check(uls,"Left.TopFlange.Bolt1.Bolt.Shear").Resistance);
        var sls=IBeamSpliceBolts.VerifySlip(Slip(bolts:set));
        Equal(224636.36363636364,Check(sls,"Left.Web.Bolt1.Slip.Serviceability").Resistance);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void LongJointReductionIsKeptAtBothLimitStates(bool slip)
    {
        var f=IBeamSpliceForceTests.Input(web:new(2,3,400,120,70),flange:new(5,2,80,120,60));
        double webBeta=1-(Math.Sqrt(400*400+240*240)-300)/4000;
        if(slip)
        {
            var r=IBeamSpliceBolts.VerifySlip(Slip(forces:f));
            Equal(124727.27272727273*webBeta,Check(r,"Left.Web.Bolt1.Slip.Serviceability").Resistance);
            Equal(62363.63636363636*.995,Check(r,"Left.TopFlange.Bolt1.Slip.Serviceability").Resistance);
        }
        else
        {
            var r=IBeamSpliceBolts.VerifyStrength(Strength(forces:f));
            Equal(188160*webBeta,Check(r,"Left.Web.Bolt1.Bolt.Shear").Resistance);
            Equal(94080*.995,Check(r,"Left.TopFlange.Bolt1.Bolt.Shear").Resistance);
        }
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void AllActionsReversedKeepTheSameBoltUtilizations(bool slip)
    {
        var f=IBeamSpliceForceTests.Input(actions:new(150000,150000,0,0,0,200000000));
        if(slip)
        {
            var a=IBeamSpliceBolts.VerifySlip(Slip(forces:IBeamSpliceForceTests.Input()));
            var b=IBeamSpliceBolts.VerifySlip(Slip(forces:f));Equal(a.Utilization!.Value,b.Utilization!.Value);
        }
        else Equal(IBeamSpliceBolts.VerifyStrength(Strength()).Utilization!.Value,IBeamSpliceBolts.VerifyStrength(Strength(forces:f)).Utilization!.Value);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void LoadsBeyondCapacityFailWithoutChangingAnyExpectedResistance(bool slip)
    {
        var f=IBeamSpliceForceTests.Input(actions:new(-300000,-300000,0,0,0,-400000000));
        if(slip)Assert.AreEqual(ConnectionStatus.Failed,IBeamSpliceBolts.VerifySlip(Slip(forces:f)).Status);
        else Assert.AreEqual(ConnectionStatus.Failed,IBeamSpliceBolts.VerifyStrength(Strength(forces:f)).Status);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ZeroActionsDoNotHideUnqualifiedHardwareOrMissingEvidence(bool slip)
    {
        var f=IBeamSpliceForceTests.Input(actions:new(0,0,0,0,0,0));
        if(slip)Unsupported(IBeamSpliceBolts.VerifySlip(Slip(forces:f,assembly:null)),"PRELOADED_ASSEMBLY_QUALIFICATION_REQUIRED");
        else Unsupported(IBeamSpliceBolts.VerifyStrength(Strength(forces:f,bolts:Bolts(flangeEnvelope:100))),"HARDWARE");
    }
    [DataTestMethod][DataRow("assembly")][DataRow("surface")][DataRow("combination")]
    public void SlipEvidenceCannotBeOmitted(string missing)
        =>Unsupported(IBeamSpliceBolts.VerifySlip(Slip(assembly:missing=="assembly"?null:"qualified",
            surface:missing=="surface"?null:"qualified",source:missing=="combination"?null:"SLS")),"REQUIRED");
    [TestMethod]
    public void StrengthRequiresAnExplicitUlsCombination()
        =>Unsupported(IBeamSpliceBolts.VerifyStrength(Strength(source:null)),"ULTIMATE_COMBINATION_SOURCE_REQUIRED");
    [TestMethod]
    public void HardwareCannotPassThroughTheWebRootOrOverlapItsNeighbour()
    {
        Unsupported(IBeamSpliceBolts.VerifyStrength(Strength(forces:IBeamSpliceForceTests.Input(flange:new(3,1,80,120,60)))),"HARDWARE_INTERSECTS_WEB_OR_ROOT");
        Unsupported(IBeamSpliceBolts.VerifyStrength(Strength(forces:IBeamSpliceForceTests.Input(web:new(2,3,30,120,70)))),"Web.HARDWARE_OVERLAP");
        Unsupported(IBeamSpliceBolts.VerifySlip(Slip(forces:IBeamSpliceForceTests.Input(web:new(2,3,85,195,70)))),"Web.HARDWARE_OUTSIDE_AVAILABLE_STEEL");
    }
    [TestMethod]
    public void UnsupportedChildNeverLeavesFavourablePartialChecks()
    {
        var f=IBeamSpliceForceTests.Input(actions:new(-150000,-150000,1e-20,0,0,-200000000));
        Unsupported(IBeamSpliceBolts.VerifyStrength(Strength(forces:f)),"IN_PLANE_N_V_M_ONLY");
        Unsupported(IBeamSpliceBolts.VerifySlip(Slip(forces:f)),"IN_PLANE_N_V_M_ONLY");
        var special=Bolts(web:new(20,25,245,ConnectionBoltGrade.Class8_8,true));
        Unsupported(IBeamSpliceBolts.VerifyStrength(Strength(bolts:special)),"RoundHoleClearanceOutsideQualifiedRange");
    }
    [TestMethod]
    public void NonPhysicalInputIsRejected()
    {
        Assert.ThrowsException<ArgumentException>(()=>Bolts(flangeEnvelope:20));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Slip(muWeb:double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Slip(muFlange:1.1));
        Assert.ThrowsException<ArgumentNullException>(()=>IBeamSpliceBolts.VerifyStrength(null!));
        Assert.ThrowsException<ArgumentNullException>(()=>IBeamSpliceBolts.VerifySlip(null!));
    }
}
