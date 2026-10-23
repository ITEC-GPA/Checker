using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class IBeamSplicePlateTests
{
    internal static IBeamSplicePlatePolicy Policy(bool ntc=false,double gammaM1=1)
        =>ntc?IBeamSplicePlatePolicy.Ntc2018():IBeamSplicePlatePolicy.Eurocode3(
            ConnectionDesignCode.Eurocode3(1,1.1,1.25,"SCI P398 D.1 UK example only"),gammaM1);
    internal static IBeamSplicePlateInput Input(bool ntc=false,IBeamSpliceForceInput? forces=null,
        ConnectionPlate? flange=null,ConnectionPlate? web=null,double flangeEnd=60,double webEnd=50,
        double gap=0,double beamFlangeFy=275,double beamFlangeFu=410,bool exposed=true,
        string? source="P398 D.1 ULS; butt-gap zero is explicit test assumption",string? holes="Separate NTC SLE clearance evidence",
        double gammaM1=1,ConnectionBolt? bolt=null)
    {
        bolt??=new(20,22,245,ConnectionBoltGrade.Class8_8,true);
        return new(Policy(ntc,gammaM1),forces??IBeamSpliceForceTests.Input(),new(bolt,bolt,37,37,holes),
            flange??new(180,12,275,410),web??new(340,10,275,410),flangeEnd,webEnd,gap,
            beamFlangeFy,beamFlangeFu,exposed,source);
    }
    internal static ConnectionCheck Check(ConnectionVerification<IBeamSplicePlateInput> r,string id)=>r.Checks.Single(c=>c.Id==id);
    internal static double Trace(ConnectionVerification<IBeamSplicePlateInput> r,string name)=>r.Trace.Single(t=>t.Name==name).Value;
    internal static void Equal(double expected,double actual)=>Assert.AreEqual(expected,actual,Math.Max(1,Math.Abs(expected))*1e-10);
    internal static void Unsupported(ConnectionVerification<IBeamSplicePlateInput> r,string reason)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsFalse(r.IsComplete);
        Assert.AreEqual(0,r.Checks.Count);Assert.IsNull(r.Utilization);Assert.IsNull(r.GoverningCheck);
        Assert.IsTrue(r.Diagnostics.Any(d=>d.Contains(reason)),string.Join(";",r.Diagnostics));
    }

    [DataTestMethod]
    [DataRow("TopFlange.Cover.GrossTension",594000d)]
    [DataRow("TopFlange.Cover.NetTension",547461.8181818181)]
    [DataRow("TopFlange.Cover.BlockOutside",798698.0795111388)]
    [DataRow("TopFlange.Cover.BlockInside",1067061.7158747753)]
    [DataRow("BottomFlange.Cover.GrossCompression",594000d)]
    [DataRow("BottomFlange.Cover.CoverBuckling",582089.3054057129)]
    public void P398FlangeCapacitiesMatchIndependentUnroundedCalculations(string id,double resistance)
    {
        var r=IBeamSplicePlates.VerifyFlanges(Input());Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        Equal(resistance,Check(r,id).Resistance);
    }
    [DataTestMethod]
    [DataRow("GrossArea",2160d)][DataRow("NetArea",1632d)][DataRow("BlockShearNetArea",3960d)]
    [DataRow("BlockTensionInsideNetArea",1176d)][DataRow("BlockTensionOutsideNetArea",456d)]
    [DataRow("MaximumUnrestrainedPitch",120d)][DataRow("EffectiveBucklingLength",72d)]
    [DataRow("EulerForce",10363084.621143825)][DataRow("Slenderness",.23941353560722312)]
    [DataRow("Chi",.9799483255988432)]
    public void P398GeometricAndStabilityQuantitiesHaveIndependentValues(string name,double value)
        =>Equal(value,Trace(IBeamSplicePlates.VerifyFlanges(Input()),"BottomFlange.Cover."+name));

    [TestMethod]
    public void PublishedBlockSubstitutionUsesFourRowsButActualDrawingUsesThree()
    {
        var r=IBeamSplicePlates.VerifyFlanges(Input());
        // P398 printed p136: n1=3 in the text/drawing but 4 in the numerical line.
        // 2*12*((3-1)*80+60-(3-.5)*22)=3960, not the printed 5352mm2.
        Equal(3960,Trace(r,"TopFlange.Cover.BlockShearNetArea"));
        Equal(798698.0795111388,Check(r,"TopFlange.Cover.BlockOutside").Resistance);
        Assert.IsTrue(Math.Abs(Check(r,"TopFlange.Cover.BlockOutside").Resistance-1020000)>200000);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void CompressionAndTensionUseThePhysicalFlangeSignOnBothSides(bool reverse)
    {
        var a=reverse?new PlanarConnectionActions(150000,150000,0,0,0,200e6):new(-150000,-150000,0,0,0,-200e6);
        var r=IBeamSplicePlates.VerifyFlanges(Input(forces:IBeamSpliceForceTests.Input(actions:a)));
        Equal(reverse?0:325010.52473425885,Check(r,"TopFlange.Cover.GrossTension").Demand);
        Equal(reverse?325010.52473425885:0,Check(r,"TopFlange.Cover.GrossCompression").Demand);
        Equal(reverse?411185.96333075:0,Check(r,"BottomFlange.Cover.GrossTension").Demand);
        Equal(reverse?0:411185.96333075,Check(r,"BottomFlange.Cover.GrossCompression").Demand);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void MemberFlangeUsesItsOwnThicknessAndStrengthRatherThanWebMaterial(bool ntc)
    {
        var r=IBeamSplicePlates.VerifyFlanges(Input(ntc,beamFlangeFy:265,beamFlangeFu:400));
        double gm0=ntc?1.05:1,gm2=ntc?1.25:1.1;
        Equal(2411.73,Trace(r,"TopFlange.Member.GrossArea"));Equal(1852.93,Trace(r,"TopFlange.Member.NetArea"));
        Equal(639108.45/gm0,Check(r,"TopFlange.Member.GrossTension").Resistance);
        Equal(667054.8/gm2,Check(r,"TopFlange.Member.NetTension").Resistance);
        Equal(594000/gm0,Check(r,"TopFlange.Cover.GrossTension").Resistance);
    }
    [TestMethod]
    public void NtcRetainsItsOwnPlateFractureAndStabilityFactors()
    {
        var r=IBeamSplicePlates.VerifyFlanges(Input(true));
        Equal(594000/1.05,Check(r,"BottomFlange.Cover.GrossCompression").Resistance);
        Equal(481766.4,Check(r,"TopFlange.Cover.NetTension").Resistance);
        Equal(582089.3054057129/1.05,Check(r,"BottomFlange.Cover.CoverBuckling").Resistance);
        Assert.IsTrue(Check(r,"TopFlange.Cover.BlockOutside").Reference.Contains("NTC"));
    }
    [TestMethod]
    public void EcStabilityFactorIsExplicitAndDoesNotChangeCrossSectionResistance()
    {
        var r=IBeamSplicePlates.VerifyFlanges(Input(gammaM1:1.2));
        Equal(582089.3054057129/1.2,Check(r,"BottomFlange.Cover.CoverBuckling").Resistance);
        Equal(594000,Check(r,"BottomFlange.Cover.GrossCompression").Resistance);
        Assert.ThrowsException<ArgumentException>(()=>IBeamSplicePlatePolicy.Eurocode3(ConnectionDesignCode.Ntc2018(),1));
    }
    [TestMethod]
    public void ThickCoverMeetsTheNineEpsilonExemptionWithoutArtificialGammaM1Penalty()
    {
        var r=IBeamSplicePlates.VerifyFlanges(Input(flange:new(180,20,275,410),gammaM1:1.2));
        Equal(1,Trace(r,"BottomFlange.Cover.BucklingExempt"));Equal(1,Trace(r,"BottomFlange.Cover.Chi"));
        Equal(990000,Check(r,"BottomFlange.Cover.CoverBuckling").Resistance);
    }
    [TestMethod]
    public void CentralBoltSpacingRatherThanWithinGroupPitchControlsBuckling()
    {
        var normal=IBeamSplicePlates.VerifyFlanges(Input());
        var wider=IBeamSplicePlates.VerifyFlanges(Input(forces:IBeamSpliceForceTests.Input(flange:new(3,2,80,120,80))));
        Equal(160,Trace(wider,"BottomFlange.Cover.MaximumUnrestrainedPitch"));
        Assert.IsTrue(Check(wider,"BottomFlange.Cover.CoverBuckling").Resistance<Check(normal,"BottomFlange.Cover.CoverBuckling").Resistance);
    }
    [TestMethod]
    public void ButtGapReducesMemberEndAndBlockCapacityButDoesNotChangeCoverGeometry()
    {
        var zero=IBeamSplicePlates.VerifyFlanges(Input());var gap=IBeamSplicePlates.VerifyFlanges(Input(gap:20));
        Equal(50,Trace(gap,"Flange.MemberEndDistance"));Equal(560,Trace(gap,"Flange.CoverLength"));
        Equal(2*12.7*10,Trace(zero,"TopFlange.Member.BlockShearNetArea")-Trace(gap,"TopFlange.Member.BlockShearNetArea"));
        Equal(Check(zero,"TopFlange.Cover.BlockOutside").Resistance,Check(gap,"TopFlange.Cover.BlockOutside").Resistance);
    }
    [DataTestMethod]
    [DataRow("Left.TopFlange.Cover.Bolt1.BearingX",151584.79338842974)]
    [DataRow("Right.BottomFlange.Cover.Bolt6.BearingX",151584.79338842974)]
    [DataRow("Left.TopFlange.Member.Bolt1.BearingX",189345.45454545456)]
    [DataRow("Left.Web.Member.Bolt1.BearingX",139400d)]
    [DataRow("Left.Web.Member.Bolt1.BearingY",139400d)]
    [DataRow("Right.Web.Cover.Bolt6.BearingX",124242.42424242424)]
    [DataRow("Right.Web.Cover.Bolt6.BearingY",124242.42424242424)]
    public void BearingCapacitiesMatchP398GeometryWithIndependentTable34Calculations(string id,double expected)
    {
        var r=IBeamSplicePlates.VerifyBearing(Input());Assert.IsTrue(r.IsComplete);
        Equal(expected,Check(r,id).Resistance);
    }
    [TestMethod]
    public void LinearBearingIsConservativeAndDoesNotSilentlyReproduceThePublishedResultantCheck()
    {
        var r=IBeamSplicePlates.VerifyBearing(Input());
        // Independent left web group: J=68437.5, M=-54.6541038548753e6,
        // N=-63824.561403509, V=-150000. Evaluate every point without the production distributor.
        double max=0;
        foreach(double x in new[]{42.5,-42.5})foreach(double y in new[]{-120d,0d,120d})
        {
            double fx=-63824.561403509/6-(-54654103.8548753)*y/68437.5;
            double fy=-150000d/6+(-54654103.8548753)*x/68437.5;
            max=Math.Max(max,(Math.Abs(fx)+Math.Abs(fy))/139400);
        }
        Equal(max,r.Utilization!.Value);Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsTrue(max>1);Assert.IsTrue(121695.04530109138/139400<1);
        StringAssert.Contains(r.GoverningCheck.Id,"Left.Web.Member");
    }
    [DataTestMethod][DataRow("Left")][DataRow("Right")]
    public void EachWebCoverReceivesHalfOfEveryActualBoltComponent(string side)
    {
        var r=IBeamSplicePlates.VerifyBearing(Input());
        for(int i=1;i<=6;i++)foreach(string axis in new[]{"X","Y"})
            Equal(Check(r,$"{side}.Web.Member.Bolt{i}.Bearing{axis}").Demand/2,
                Check(r,$"{side}.Web.Cover.Bolt{i}.Bearing{axis}").Demand);
        Assert.AreEqual(24,r.Checks.Count(c=>c.Id.EndsWith("BearingInteraction")));
    }
    [TestMethod]
    public void SmallerConcomitantActionsPassTheSameBearingDetail()
    {
        var r=IBeamSplicePlates.VerifyBearing(Input(forces:IBeamSpliceForceTests.Input(actions:new(-75000,-75000,0,0,0,-100e6))));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        Equal(IBeamSplicePlates.VerifyBearing(Input()).Utilization!.Value/2,r.Utilization!.Value);
    }
    [TestMethod]
    public void ForceReversalPreservesBearingAndTheWholeBeamWebIsNotCountedTwice()
    {
        var a=IBeamSplicePlates.VerifyBearing(Input());
        var b=IBeamSplicePlates.VerifyBearing(Input(forces:IBeamSpliceForceTests.Input(actions:new(150000,150000,0,0,0,200e6))));
        for(int i=0;i<a.Checks.Count;i++){Equal(a.Checks[i].Demand,b.Checks[i].Demand);Equal(a.Checks[i].Resistance,b.Checks[i].Resistance);}
    }
    [TestMethod]
    public void SandwichedWebThicknessIsNotTheOuterThicknessInTable33()
    {
        var r=IBeamSplicePlates.VerifyBearing(Input());Assert.IsTrue(r.IsComplete);
        // p2=120 and central p1=140 exceed 14*8.5=119, but equal/below14*10=140.
        StringAssert.Contains(string.Join(";",r.Assumptions),"OUTER");
        Unsupported(IBeamSplicePlates.VerifyBearing(Input(web:new(340,9.9,275,410))),"MAXIMUM_BOLT_PITCH");
    }
    [TestMethod]
    public void LongFlangeGroupsReduceBearingAsWellAsBoltShear()
    {
        var r=IBeamSplicePlates.VerifyBearing(Input(forces:IBeamSpliceForceTests.Input(flange:new(6,2,80,120,60))));
        Equal(.975,Trace(r,"Left.TopFlange.Cover.BetaLongJoint"));
        // k1=2.8*30/22-1.7=2.11818, below 2.5: cover edge distance governs.
        Equal(151584.79338842974*.975,Check(r,"Left.TopFlange.Cover.Bolt1.BearingX").Resistance);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ZeroActionsRemainFiniteAndDoNotQualifyWholeJoint(bool bearing)
    {
        var input=Input(forces:IBeamSpliceForceTests.Input(actions:new(0,0,0,0,0,0)));
        var r=bearing?IBeamSplicePlates.VerifyBearing(input):IBeamSplicePlates.VerifyFlanges(input);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);Equal(0,r.Utilization!.Value);
        Assert.IsTrue(r.Trace.All(v=>double.IsFinite(v.Value)));StringAssert.Contains(r.Scope,"COMPONENTS ONLY");
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void MissingCombinationOrNtcClearanceNeverLeavesFavourablePartialChecks(bool bearing)
    {
        var noSource=Input(source:null);var noHoles=Input(true,holes:null);
        Unsupported(bearing?IBeamSplicePlates.VerifyBearing(noSource):IBeamSplicePlates.VerifyFlanges(noSource),"COMBINATION");
        Unsupported(bearing?IBeamSplicePlates.VerifyBearing(noHoles):IBeamSplicePlates.VerifyFlanges(noHoles),"Clearance");
    }
    public static IEnumerable<object[]> OutOfScope()
    {
        yield return new object[]{Input(gap:80),"MINIMUM_END"};
        yield return new object[]{Input(flangeEnd:20),"MINIMUM_END"};
        yield return new object[]{Input(webEnd:100),"EXPOSED_EDGE"};
        yield return new object[]{Input(flange:new(200,12,275,410)),"COVER_OUTSIDE"};
        yield return new object[]{Input(web:new(420,10,275,410)),"COVER_OUTSIDE"};
        yield return new object[]{Input(flange:new(160,12,275,410)),"MINIMUM_END"};
        yield return new object[]{Input(beamFlangeFy:500,beamFlangeFu:600),"S460"};
        yield return new object[]{Input(flange:new(180,12,500,600)),"S460"};
        yield return new object[]{Input(forces:IBeamSpliceForceTests.Input(actions:new(0,0,1,0,0,0))),"IN_PLANE"};
        yield return new object[]{Input(forces:IBeamSpliceForceTests.Input(flange:new(1,2,80,120,60))),"TWO_LONGITUDINAL"};
        yield return new object[]{Input(forces:IBeamSpliceForceTests.Input(web:new(1,3,85,120,70))),"WEB_REQUIRES"};
        yield return new object[]{Input(forces:IBeamSpliceForceTests.Input(web:new(2,3,50,120,70))),"MINIMUM_BOLT_PITCH"};
        yield return new object[]{Input(bolt:new(20,24,245,ConnectionBoltGrade.Class8_8,true)),"Hole"};
    }
    [DataTestMethod][DynamicData(nameof(OutOfScope),DynamicDataSourceType.Method)]
    public void InvalidDetailFailsClosedForBothComponents(IBeamSplicePlateInput input,string issue)
    { Unsupported(IBeamSplicePlates.VerifyBearing(input),issue);Unsupported(IBeamSplicePlates.VerifyFlanges(input),issue); }
    [TestMethod]
    public void OverloadIsFailedNotUnsupportedAndKeepsCheckTrace()
    {
        var r=IBeamSplicePlates.VerifyFlanges(Input(forces:IBeamSpliceForceTests.Input(actions:new(-600000,-600000,0,0,0,-800e6))));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.IsComplete);Assert.IsTrue(r.Utilization>1);Assert.AreEqual(0,r.Diagnostics.Count);
    }
    [TestMethod]
    public void InvalidScalarsAreRejectedAtTheInputBoundary()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(gap:-1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(webEnd:double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(beamFlangeFy:double.PositiveInfinity));
        Assert.ThrowsException<ArgumentException>(()=>Input(beamFlangeFu:200));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Policy(gammaM1:0));
        Assert.ThrowsException<ArgumentNullException>(()=>IBeamSplicePlates.VerifyBearing(null!));
        Assert.ThrowsException<ArgumentNullException>(()=>IBeamSplicePlates.VerifyFlanges(null!));
    }
}
