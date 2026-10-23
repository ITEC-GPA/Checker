using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class FlexibleEndPlateTests
{
    private static ConnectionDesignCode Ec(double gm0=1, double gm2p=1.1, double gm2c=1.25)
        => ConnectionDesignCode.Eurocode3(gm0,gm2p,gm2c,"SCI P358 UK factors, explicit benchmark policy");
    private static ConnectionRolledISection Section(double h=466,double b=300,double tw=13.1,double tf=20,double r=13,double fy=265,double fu=410)
        => new(h,b,tw,tf,r,2*b*tf+(h-2*tf)*tw+4*(1-Math.PI/4)*r*r,fy,fu);
    private static ConnectionRolledISection Beam() => new(412.8,179.5,9.5,16,10.2,9450,275,410);
    private static FlexibleEndPlateInput Input(ConnectionDesignCode? code=null,double v=340000,int rows=4,double gauge=140,
        ConnectionRolledISection? beam=null,ConnectionRolledISection? support=null,ConnectionPlate? plate=null,ConnectionBolt? bolt=null,
        double top=90,double bottom=357,double throat=5.6,FilletWeldEndCondition ends=FilletWeldEndCondition.FullSize,
        string? consumable="Fixture matching consumable qualification",string? pinned="Fixture frame-model qualification",
        string? holes="Fixture NTC SLE evidence",bool restrained=true,bool notched=false,
        PlanarConnectionActions? actions=null,PlanarConnectionActions? existing=null)
        => new(code??Ec(),plate??new(gauge+60,rows==6?12:10,275,410),bolt??new(20,22,245,ConnectionBoltGrade.Class8_8,true),
            beam??Beam(),support??Section(),rows,gauge,top,bottom,throat,ends,consumable!,actions??new(0,-v,0,0,0,0),
            existing??new(0,0,0,0,0,0),restrained,pinned!,holes,notched);
    private static ConnectionCheck Check(FlexibleEndPlateResult r,string id)=>r.Checks.Single(c=>c.Id==id);
    private static double Trace(FlexibleEndPlateResult r,string name)=>r.Trace.Single(c=>c.Name==name).Value;
    private static void Equal(double expected,double actual,string message="")
        => Assert.AreEqual(expected,actual,Math.Max(1e-8,Math.Abs(expected)*1e-10),message);
    private static void Unsupported(FlexibleEndPlateResult r,string diagnostic)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status); Assert.IsFalse(r.IsComplete);
        Assert.AreEqual(0,r.Checks.Count); Assert.IsNull(r.Utilization); Assert.IsNull(r.LoadUtilization);
        CollectionAssert.Contains(r.Diagnostics.ToArray(),diagnostic);
    }
    private static JsonDocument Fixtures()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures","flexible-end-plate-independent.json")));
    private static JsonElement Case(JsonDocument doc,string id)=>doc.RootElement.GetProperty("cases").EnumerateArray()
        .Single(c=>c.GetProperty("id").GetString()==id);
    private static void Published(JsonElement c,string quantity,double actual)
    {
        var q=c.GetProperty("publishedExpected").EnumerateArray().Single(q=>q.GetProperty("quantity").GetString()==quantity);
        Assert.AreEqual(q.GetProperty("value").GetDouble(),actual,q.GetProperty("absoluteTolerance").GetDouble(),quantity);
    }

    [DataTestMethod] [DataRow(4)] [DataRow(6)]
    public void OrdinaryComponentsMatchPublishedSciExampleWithoutChangingItsExpectations(int rows)
    {
        using var doc=Fixtures(); var c=Case(doc,rows==4?"SCI-P358-Ex1-406-components":"SCI-P358-Ex1-533-components");
        var r=FlexibleEndPlate.Verify(Input(rows:rows,v:rows==4?340000:550000,throat:6/Math.Sqrt(2),
            beam:rows==4?Beam():Section(540,210,10.1,16,12,275)));
        // Published example is notched: these are component comparisons, not qualification of that whole joint.
        Assert.IsTrue(r.IsComplete); Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsFalse(Check(r,"Weld.FullStrengthThroat").Passed,"Our declared policy omits the UK-only 0.8 weld reduction.");
        if(rows==4) Published(c,"weldThroat",r.Input.WeldThroat);
        Published(c,"beamWebShearArea",Trace(r,"Beam.EffectiveShearArea"));
        Published(c,"beamWebShearResistance",Check(r,"Beam.WebShear").Resistance);
        if(rows==4) { Published(c,"plateBearingK1",Trace(r,"PlateBearing.K1")); Published(c,"plateBearingAlpha",Trace(r,"PlateBearing.AlphaB")); }
        Published(c,"plateBearingPerBolt",Trace(r,"PlateBearing.PerBolt"));
        // SCI printed nominal 902kN for six rows. Keep our explicit long-joint reduction separate.
        Published(c,"boltGroupShearWithTensionAllowance",Check(r,"BoltGroup.ShearWithParasiticTension").Resistance/Trace(r,"Bolt.BetaLongJoint"));
        Published(c,"plateGrossShearResistance",Check(r,"Plate.GrossShear").Resistance);
        Published(c,"plateNetAreaPerSide",Trace(r,"Plate.NetAreaPerSide"));
        Published(c,"plateNetShearResistance",Check(r,"Plate.NetShear").Resistance);
        Published(c,"blockNetTensionAreaPerSide",Trace(r,"Plate.AntPerSide"));
        Published(c,"blockNetShearAreaPerSide",Trace(r,"Plate.AnvPerSide"));
        Published(c,"plateBlockTearingResistance",Check(r,"Plate.BlockTearing").Resistance);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void CompleteOrdinaryJointHasIndependentResistanceAndWeldArithmetic(bool ntc)
    {
        var r=FlexibleEndPlate.Verify(Input(code:ntc?ConnectionDesignCode.Ntc2018():Ec()));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status); Assert.IsTrue(r.ConformsToSciPinnedDetail);
        // Independent calculations from P358 checks4/8/9/10 and EN1993-1-8 Table4.1, in N and mm.
        double gm0=ntc?1.05:1,gm2=ntc?1.25:1.1;
        Equal(602112,Check(r,"BoltGroup.ShearWithParasiticTension").Resistance);
        Equal(2479.5,Trace(r,"Beam.EffectiveShearArea"));
        Equal(2479.5*275/Math.Sqrt(3)/gm0,Check(r,"Beam.WebShear").Resistance);
        Equal(2*2900*275/(1.27*Math.Sqrt(3)*gm0),Check(r,"Plate.GrossShear").Resistance);
        Equal(4040*410/(Math.Sqrt(3)*gm2),Check(r,"Plate.NetShear").Resistance);
        Equal(2*(190*410/gm2+1730*275/Math.Sqrt(3)/gm0),Check(r,"Plate.BlockTearing").Resistance);
        Equal(4847,Trace(r,"Support.GrossAreaPerSide")); Equal(3694.2,Trace(r,"Support.NetAreaPerSide"));
        Equal(170000,Check(r,"Support.GrossShear").Demand);
        Equal(4847*265/Math.Sqrt(3)/gm0,Check(r,"Support.GrossShear").Resistance);
        Equal(3694.2*410/Math.Sqrt(3)/gm2,Check(r,"Support.NetShear").Resistance);
        Equal(9.5*275*.85*1.25/(Math.Sqrt(2)*410),Check(r,"Weld.FullStrengthThroat").Demand);
        Equal(Math.Sqrt(3)*340000/(2*290*5.6)/(410/(.85*1.25)),r.WeldVerification.Utilization!.Value);
        Equal(340000/(2479.5*275/Math.Sqrt(3)/gm0),r.LoadUtilization!.Value);
    }

    [TestMethod]
    public void SharedColumnDemandMatchesSciPublishedComponentAndChecksEveryPrefix()
    {
        using var doc=Fixtures();var c=Case(doc,"SCI-P358-Ex1-shared-support");
        var code=Ec();var support=Section();var bolt=new ConnectionBolt(20,22,245,ConnectionBoltGrade.Class8_8,true);
        var first=Input(code:code,support:support,bolt:bolt,rows:6,beam:Section(540,210,10.1,16,12,275),v:550000,bottom:217);
        var second=Input(code:code,support:support,bolt:bolt,v:340000,bottom:357);
        var r=FlexibleEndPlate.VerifyPair(new(first,second)); Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        double T(string name)=>r.Trace.Single(t=>t.Name==name).Value;
        ConnectionCheck C(string name)=>r.Checks.Single(t=>t.Id==name);
        // The source has a beam support. Identical local geometric terms are compared; our qualified whole model is column web only.
        Published(c,"supportShearDemandPerPlane",C("Shared.Prefix4.GrossShear").Demand);
        Published(c,"supportGrossAreaPerPlane",T("Shared.Prefix4.GrossAreaPerSide"));
        Published(c,"supportNetAreaPerPlane",T("Shared.Prefix4.NetAreaPerSide"));
        Published(c,"supportGrossShearResistancePerPlane",C("Shared.Prefix4.GrossShear").Resistance);
        Published(c,"supportNetShearResistancePerPlane",C("Shared.Prefix4.NetShear").Resistance);
        Published(c,"supportBearingDemandPerBolt",C("Shared.Row4.SupportBearing").Demand);
        Published(c,"supportBearingResistancePerBolt",C("Shared.Row4.SupportBearing").Resistance);
        Equal(445000,C("Shared.Prefix6.GrossShear").Demand);
        Equal(550000.0/12,C("Shared.Row5.SupportBearing").Demand);
        Equal(6,r.Checks.Count(x=>x.Id.StartsWith("Shared.Prefix")&&x.Id.EndsWith(".GrossShear")));
        Equal(891878.4,C("First.BoltGroup.ShearWithParasiticTension").Resistance);
        Equal(602112,C("Second.BoltGroup.ShearWithParasiticTension").Resistance);
    }

    [DataTestMethod] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)]
    public void EachQualifiedRowCountChecksTheWholeDeclaredLoadPath(int rows)
    {
        var beam=rows==2?Section(220,110,6,10,8,275):rows==3?Section(300,150,7,12,8,275):
            rows==4?Beam():rows==5?Section(480,200,9.5,16,10,275):Section(540,210,10.1,16,12,275);
        double throat=rows==2?3.2:rows==3?4:5.6;
        var r=FlexibleEndPlate.Verify(Input(rows:rows,beam:beam,throat:throat,v:1000));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status); Equal(rows*2,r.BoltDistribution.Forces.Count);
        Equal(-1000,r.BoltDistribution.Forces.Sum(f=>f.Fy));
        Equal(0,r.BoltDistribution.Forces.Sum(f=>f.Position.X*f.Fy-f.Position.Y*f.Fx));
        foreach(var id in new[]{"BoltGroup.ShearWithParasiticTension","Plate.GrossShear","Plate.NetShear","Plate.BlockTearing",
            "Beam.WebShear","Support.GrossShear","Support.NetShear","Weld.FullStrengthThroat"}) Assert.IsTrue(Check(r,id).Passed,id);
    }

    [DataTestMethod] [DataRow(90,false)] [DataRow(140,true)]
    public void ShortPlateHasReducedBlockTearingAndAnAdditionalInPlaneBendingCheck(int gauge,bool shortPlate)
    {
        var r=FlexibleEndPlate.Verify(Input(rows:2,gauge:gauge,beam:Section(220,110,6,10,8,275),throat:3.2,v:1000));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        Equal(shortPlate?.5:1,Trace(r,"Plate.BlockTensionFactor"));
        Equal(2*((shortPlate?.5:1)*190*410/1.1+770*275/Math.Sqrt(3)),Check(r,"Plate.BlockTearing").Resistance);
        Assert.AreEqual(shortPlate,r.Checks.Any(c=>c.Id=="Plate.InPlaneBending"));
        if(shortPlate) Equal(2*10*150.0*150*275/(3*(140-6)),Check(r,"Plate.InPlaneBending").Resistance);
    }

    [DataTestMethod] [DataRow(1e-8)] [DataRow(.4)] [DataRow(1.0)] [DataRow(200000.0)]
    public void SmallActionsAreNotTruncatedAndWeldAndBoltFramesBalance(double v)
    {
        var r=FlexibleEndPlate.Verify(Input(v:v));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        Assert.AreEqual(-v,r.BoltDistribution.Forces.Sum(f=>f.Fy),v*1e-12);
        Assert.IsTrue(r.BoltDistribution.Forces.All(f=>f.Shear>0));
        Assert.AreEqual(-v,Trace(r,"Weld.RecoveredFy"),v*1e-12);
        Equal(0,Trace(r,"Weld.RecoveredMx")); Equal(0,Trace(r,"Weld.RecoveredMz"));
        Equal(3248,Trace(r,"Weld.Area"));
        Assert.AreEqual(v/(2479.5*275/Math.Sqrt(3)),r.LoadUtilization!.Value,v*1e-12);
    }

    [TestMethod]
    public void ZeroLoadDoesNotRemoveDetailAndFullStrengthWeldRequirements()
    {
        var good=FlexibleEndPlate.Verify(Input(v:0));Equal(0,good.LoadUtilization!.Value);Assert.AreEqual(ConnectionStatus.Passed,good.Status);
        var bad=FlexibleEndPlate.Verify(Input(v:0,throat:3));Assert.AreEqual(ConnectionStatus.Failed,bad.Status);
        Equal(0,bad.LoadUtilization!.Value);Assert.IsFalse(Check(bad,"Weld.FullStrengthThroat").Passed);
    }

    [DataTestMethod] [DataRow(.999999,ConnectionStatus.Passed)] [DataRow(1.000001,ConnectionStatus.Failed)]
    public void BeamWebCapacityBoundaryChangesStatus(double factor,ConnectionStatus expected)
    {
        var r=FlexibleEndPlate.Verify(Input(v:2479.5*275/Math.Sqrt(3)*factor));
        Assert.AreEqual(expected,r.Status);Equal(factor,r.LoadUtilization!.Value);
    }

    [TestMethod]
    public void MaterialFactorsAffectTheirOwnMechanismsOnly()
    {
        var a=FlexibleEndPlate.Verify(Input());var b=FlexibleEndPlate.Verify(Input(code:Ec(gm0:1.05,gm2p:1.25)));
        Equal(Check(a,"Plate.GrossShear").Resistance/1.05,Check(b,"Plate.GrossShear").Resistance);
        Equal(Check(a,"Plate.NetShear").Resistance*1.1/1.25,Check(b,"Plate.NetShear").Resistance);
        Equal(Check(a,"BoltGroup.ShearWithParasiticTension").Resistance,Check(b,"BoltGroup.ShearWithParasiticTension").Resistance);
        Equal(Trace(a,"PlateBearing.PerBolt"),Trace(b,"PlateBearing.PerBolt"));
        Equal(a.WeldVerification.Utilization!.Value,b.WeldVerification.Utilization!.Value);
    }

    [TestMethod]
    public void ThinSupportFailureCannotBeHiddenByPassingEachSeparateConnection()
    {
        var code=Ec();var bolt=new ConnectionBolt(20,22,245,ConnectionBoltGrade.Class8_8,true);
        var support=Section(256,200,6.5,15,13); // Clear web200: assembly clears and web slenderness is within scope.
        var a=Input(code:code,bolt:bolt,support:support,gauge:90,v:360000);
        var b=Input(code:code,bolt:bolt,support:support,gauge:90,v:360000);
        Assert.AreEqual(ConnectionStatus.Passed,FlexibleEndPlate.Verify(a).Status);
        Assert.AreEqual(ConnectionStatus.Passed,FlexibleEndPlate.Verify(b).Status);
        var pair=FlexibleEndPlate.VerifyPair(new(a,b)); Assert.AreEqual(ConnectionStatus.Failed,pair.Status);
        Assert.IsTrue(pair.Checks.Any(c=>c.Id.StartsWith("Shared.")&&!c.Passed));
    }

    [DataTestMethod]
    [DataRow("Fx")] [DataRow("Fz")] [DataRow("Mx")] [DataRow("My")] [DataRow("Mz")]
    [DataRow("X")] [DataRow("Y")] [DataRow("Z")] [DataRow("up")]
    public void UnsupportedActionOrApplicationPointDoesNotProducePartialPass(string term)
    {
        const double tiny=1e-12;
        var a=new PlanarConnectionActions(term=="Fx"?tiny:0,term=="up"?tiny:-1000,term=="Fz"?tiny:0,
            term=="Mx"?tiny:0,term=="My"?tiny:0,term=="Mz"?tiny:0,term=="X"?tiny:0,term=="Y"?tiny:0,term=="Z"?tiny:0);
        Unsupported(FlexibleEndPlate.Verify(Input(actions:a)),"CENTRED_DOWNWARD_SHEAR_ONLY");
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    public void EveryExistingSupportActionRequiresAnInteractionModel(int term)
    {
        var v=new double[6];v[term]=1e-12;
        Unsupported(FlexibleEndPlate.Verify(Input(existing:new(v[0],v[1],v[2],v[3],v[4],v[5]))),"PREEXISTING_SUPPORT_ACTIONS_REQUIRE_INTERACTION_MODEL");
    }

    [TestMethod] public void MissingQualificationFailsClosed()=>Unsupported(FlexibleEndPlate.Verify(Input(pinned:null)),"PINNED_FRAME_MODEL_QUALIFICATION_REQUIRED");
    [TestMethod] public void MissingConsumableFailsClosed()=>Unsupported(FlexibleEndPlate.Verify(Input(consumable:null)),"CONSUMABLE_EVIDENCE_REQUIRED");
    [TestMethod] public void NtcOversizeNormalHoleNeedsServiceabilityEvidence()=>Unsupported(FlexibleEndPlate.Verify(Input(code:ConnectionDesignCode.Ntc2018(),holes:null)),"NTC_HOLE_CLEARANCE_EVIDENCE_REQUIRED");
    [TestMethod] public void NotchedBeamNeedsItsOwnModel()=>Unsupported(FlexibleEndPlate.Verify(Input(notched:true)),"NOTCHED_BEAM_NOT_QUALIFIED");
    [TestMethod] public void UnrestrainedBeamNeedsItsOwnModel()=>Unsupported(FlexibleEndPlate.Verify(Input(restrained:false)),"LATERAL_RESTRAINT_REQUIRED");
    [TestMethod] public void PlateIntersectingBeamRootIsUnsupported()=>Unsupported(FlexibleEndPlate.Verify(Input(beam:Section(340,180,9.5,16,10,275))),"PLATE_INTERSECTS_BEAM_ROOT_OR_BEAM_TOO_DEEP");
    [TestMethod] public void SlenderSupportCannotBePassedFromLocalAreasOnly()=>Unsupported(FlexibleEndPlate.Verify(Input(support:Section(600,300,8))),"SUPPORT_WEB_SLENDERNESS_OUTSIDE_SCOPE");
    [TestMethod] public void ShortSupportEndIsNotAnInfinitePanel()=>Unsupported(FlexibleEndPlate.Verify(Input(bottom:69)),"COLUMN_END_TOO_CLOSE_FOR_SUPPORT_MODEL");
    [TestMethod] public void NominalWeldEndsAreNotFullStrengthDetail()=>Unsupported(FlexibleEndPlate.Verify(Input(ends:FilletWeldEndCondition.NominalWithoutFullSizeEvidence)),"FULL_SIZE_WELD_ENDS_REQUIRED");
    [TestMethod] public void InvalidWeldThroatIsUnsupported()=>Unsupported(FlexibleEndPlate.Verify(Input(throat:8)),"WELD_THROAT_OUTSIDE_FILLET_SCOPE");
    [TestMethod] public void UnsupportedBoltGradeIsNotSilentlyConverted()=>Unsupported(FlexibleEndPlate.Verify(Input(bolt:new(20,22,245,ConnectionBoltGrade.Class10_9,true))),"STANDARD_M20_8_8_ONLY");
    [DataTestMethod] [DataRow(1)] [DataRow(7)]
    public void UnqualifiedRowCountIsUnsupported(int rows)=>Unsupported(FlexibleEndPlate.Verify(Input(rows:rows)),"NONSTANDARD_DETAIL_NOT_QUALIFIED");

    [TestMethod]
    public void UnsupportedChildInvalidatesWholePairWithoutReturningPassingChecks()
    {
        var code=Ec();var bolt=new ConnectionBolt(20,22,245,ConnectionBoltGrade.Class8_8,true);var support=Section();
        var r=FlexibleEndPlate.VerifyPair(new(Input(code:code,bolt:bolt,support:support),Input(code:code,bolt:bolt,support:support,notched:true)));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.AreEqual(0,r.Checks.Count);Assert.IsNull(r.Utilization);
        CollectionAssert.Contains(r.Diagnostics.ToArray(),"Second:NOTCHED_BEAM_NOT_QUALIFIED");
    }

    [TestMethod]
    public void PairRejectsInconsistentPhysicalFramesOrUnsharedSnapshots()
    {
        var code=Ec();var bolt=new ConnectionBolt(20,22,245,ConnectionBoltGrade.Class8_8,true);var support=Section();
        var a=Input(code:code,bolt:bolt,support:support);
        Assert.ThrowsException<ArgumentException>(()=>new FlexibleEndPlatePairInput(a,a));
        Assert.ThrowsException<ArgumentException>(()=>new FlexibleEndPlatePairInput(a,Input(code:code,bolt:bolt,support:support,top:91)));
        Assert.ThrowsException<ArgumentException>(()=>new FlexibleEndPlatePairInput(a,Input(code:code,bolt:bolt,support:support,bottom:356)));
        Assert.ThrowsException<ArgumentException>(()=>new FlexibleEndPlatePairInput(a,Input(code:code,bolt:bolt,support:support,gauge:90)));
        Assert.ThrowsException<ArgumentException>(()=>new FlexibleEndPlatePairInput(a,Input(bolt:bolt,support:support)));
        Assert.ThrowsException<ArgumentException>(()=>new FlexibleEndPlatePairInput(a,Input(code:code,bolt:bolt)));
    }

    [DataTestMethod] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)] [DataRow(0)] [DataRow(-1)]
    public void InvalidGeometryIsRejectedAtInputBoundary(double value)
        =>Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(top:value));

    [TestMethod]
    public void VerificationIsRepeatableAndDoesNotMutateCallerInputs()
    {
        var p=Input();var a=FlexibleEndPlate.Verify(p);var b=FlexibleEndPlate.Verify(p);
        Equal(340000,-p.Actions.Fy);Equal(200,p.Plate.Width);Equal(140,p.Gauge);
        Equal(a.Utilization!.Value,b.Utilization!.Value);
        CollectionAssert.AreEqual(a.Checks.Select(c=>c.Resistance).ToArray(),b.Checks.Select(c=>c.Resistance).ToArray());
    }
}
