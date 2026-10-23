using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class DoubleAngleCleatComponentTests
{
    private static ConnectionDesignCode Ec()=>ConnectionDesignCode.Eurocode3(1,1.25,1.25,"SBE-MS5 recommended EC ordinary ULS factors");
    private static ConnectionRolledISection Beam()=>new(547,210,9,15.7,24,11700,275,430);
    private static ConnectionRolledISection Section(double h,double b,double tw,double tf,double r)
        =>new(h,b,tw,tf,r,2*b*tf+(h-2*tf)*tw+4*(1-Math.PI/4)*r*r,275,430);
    private static DoubleAngleCleatComponentInput Input(ConnectionDesignCode? code=null,double v=450000,int rows=6,
        ConnectionRolledISection? beam=null,ConnectionPlate? leg=null,double root=11,ConnectionBolt? bolt=null,
        PlanarConnectionActions? actions=null,bool restrained=true,bool notched=false,string? holes="Fixture NTC SLE clearance evidence")
        =>new(code??Ec(),leg??new(90,10,275,430),root,beam??Beam(),bolt??new(20,22,245,ConnectionBoltGrade.Class8_8,true),rows,
            actions??new(0,-v,0,0,0,0),restrained,notched,holes);
    private static ConnectionCheck Check(DoubleAngleCleatComponentResult r,string id)=>r.ComponentAssessment.Checks.Single(x=>x.Id==id);
    private static double Trace(DoubleAngleCleatComponentResult r,string id)=>r.ComponentAssessment.Trace.Single(x=>x.Name==id).Value;
    private static void Equal(double a,double b)=>Assert.AreEqual(a,b,Math.Max(1e-8,Math.Abs(a)*1e-9));
    private static void Unsupported(DoubleAngleCleatComponentResult r,string diagnostic)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentAssessment.Status);Assert.IsFalse(r.ComponentAssessment.IsComplete);
        Assert.AreEqual(0,r.ComponentAssessment.Checks.Count);Assert.IsNull(r.LoadUtilization);
        CollectionAssert.Contains(r.ComponentAssessment.Diagnostics.ToArray(),diagnostic);
    }
    private static JsonDocument Fixtures()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures","double-angle-independent.json")));
    private static double Quantity(DoubleAngleCleatComponentResult r,string key)=>key switch
    {
        "oneBoltSingleShear_N"=>Trace(r,"Bolt.SingleShearResistance")/Trace(r,"Bolt.BetaLongJoint"),
        "beamSideBoltGroupShearResistance_N"=>Trace(r,"BeamBolts.EquivalentGroupResistance")/Trace(r,"Bolt.BetaLongJoint"),
        "cleatBearingVerticalPerBolt_N"=>Trace(r,"CleatBearing.Vertical"),
        "cleatBearingHorizontalPerBolt_N"=>Trace(r,"CleatBearing.Horizontal"),
        "beamSideCleatBearingGroupResistance_N"=>Trace(r,"CleatBearing.EquivalentGroupResistance"),
        "beamWebBearingVerticalPerBolt_N"=>Trace(r,"BeamBearing.Vertical"),
        "beamWebBearingHorizontalPerBolt_N"=>Trace(r,"BeamBearing.Horizontal"),
        "beamWebBearingGroupResistance_N"=>Trace(r,"BeamBearing.EquivalentGroupResistance"),
        "supportSideBoltGroupResistance_N"=>Trace(r,"SupportBolts.EquivalentGroupResistance")/Trace(r,"Bolt.BetaLongJoint"),
        "eachLegPairGrossShearResistance_N"=>2*Check(r,"Left.BeamLeg.GrossShear").Resistance,
        "eachLegPairNetShearResistance_N"=>2*Check(r,"Left.BeamLeg.NetShear").Resistance,
        "eachLegPairBlockTearingResistance_N"=>2*Check(r,"Left.BeamLeg.BlockTearing").Resistance,
        "beamWebGrossShearResistance_N"=>Check(r,"Beam.GrossShear").Resistance,
        "beamWebNetShearResistance_N"=>Check(r,"Beam.NetShear").Resistance,
        "beamWebBlockTearingResistance_N"=>Check(r,"Beam.BlockTearing").Resistance,
        "governingPublishedComponentResistance_N"=>450000/r.LoadUtilization!.Value,
        "beta"=>Trace(r,"BeamBolts.BetaEccentricity"),
        "beamBoltGroupSecondMoment_mm2"=>r.BeamBolts.Forces.Sum(f=>f.Position.Y*f.Position.Y),
        "cleatAvNetEach_mm2"=>Trace(r,"Cleat.AvNetEach"),
        "cleatAntEach_mm2"=>Trace(r,"Cleat.AntEach"),
        "cleatAnvEach_mm2"=>Trace(r,"Cleat.AnvEach"),
        "beamAv_mm2"=>Trace(r,"Beam.Av"),
        "beamAnt_mm2"=>Trace(r,"Beam.Ant"),
        "beamAnv_mm2"=>Trace(r,"Beam.Anv"),
        _=>throw new ArgumentException(key)
    };

    [DataTestMethod]
    [DataRow("oneBoltSingleShear_N")] [DataRow("beamSideBoltGroupShearResistance_N")]
    [DataRow("cleatBearingVerticalPerBolt_N")] [DataRow("cleatBearingHorizontalPerBolt_N")]
    [DataRow("beamSideCleatBearingGroupResistance_N")] [DataRow("beamWebBearingVerticalPerBolt_N")]
    [DataRow("beamWebBearingHorizontalPerBolt_N")] [DataRow("beamWebBearingGroupResistance_N")]
    [DataRow("supportSideBoltGroupResistance_N")] [DataRow("eachLegPairGrossShearResistance_N")]
    [DataRow("eachLegPairNetShearResistance_N")] [DataRow("eachLegPairBlockTearingResistance_N")]
    [DataRow("beamWebGrossShearResistance_N")] [DataRow("beamWebNetShearResistance_N")]
    [DataRow("beamWebBlockTearingResistance_N")] [DataRow("governingPublishedComponentResistance_N")]
    public void EachPublishedOrdinaryComponentMatchesItsIndependentSource(string quantity)
    {
        using var doc=Fixtures();var c=doc.RootElement.GetProperty("ordinaryUlsPublishedCase");
        var r=DoubleAngleCleatComponents.Verify(Input()); Assert.AreEqual(ConnectionStatus.Passed,r.ComponentAssessment.Status);
        double expected=c.GetProperty("publishedRoundedExpected").GetProperty(quantity).GetDouble();
        // Published source uses rounded bolt/bearing intermediates; separate exact tests follow.
        double tolerance=quantity.Contains("Group")||quantity.Contains("Bearing")?expected*.01:1500;
        Assert.AreEqual(expected,Quantity(r,quantity),tolerance,quantity);
    }

    [DataTestMethod]
    [DataRow("beta")] [DataRow("beamBoltGroupSecondMoment_mm2")] [DataRow("oneBoltSingleShear_N")]
    [DataRow("beamSideBoltGroupShearResistance_N")] [DataRow("beamSideCleatBearingGroupResistance_N")]
    [DataRow("beamWebBearingGroupResistance_N")] [DataRow("supportSideBoltGroupResistance_N")]
    [DataRow("eachLegPairGrossShearResistance_N")] [DataRow("eachLegPairNetShearResistance_N")]
    [DataRow("eachLegPairBlockTearingResistance_N")] [DataRow("cleatAvNetEach_mm2")]
    [DataRow("cleatAntEach_mm2")] [DataRow("cleatAnvEach_mm2")] [DataRow("beamAv_mm2")]
    [DataRow("beamAnt_mm2")] [DataRow("beamAnv_mm2")] [DataRow("beamWebBlockTearingResistance_N")]
    public void IndependentUnroundedArithmeticUsesStrictTolerance(string quantity)
    {
        using var doc=Fixtures();double expected=doc.RootElement.GetProperty("ordinaryUlsPublishedCase")
            .GetProperty("independentUnroundedCalculations").GetProperty(quantity).GetDouble();
        Equal(expected,Quantity(DoubleAngleCleatComponents.Verify(Input()),quantity));
    }

    [TestMethod]
    public void ComponentSuccessNeverClaimsThatTheAbsentSupportingMemberWasVerified()
    {
        var r=DoubleAngleCleatComponents.Verify(Input());Assert.AreEqual(ConnectionStatus.Passed,r.ComponentAssessment.Status);
        Assert.IsFalse(r.CanQualifyWholeJoint);StringAssert.Contains(r.ComponentAssessment.Scope,"SUPPORTING MEMBER IS NOT ASSESSED");
        CollectionAssert.Contains(r.MissingWholeJointChecks.ToArray(),"SUPPORT_MEMBER_BEARING");
        CollectionAssert.Contains(r.MissingWholeJointChecks.ToArray(),"SUPPORT_LOCAL_RESISTANCE_AND_CONCURRENT_ACTIONS");
        Equal(450000/500724.4712819393,r.LoadUtilization!.Value);
        Assert.IsFalse(r.ComponentAssessment.Checks.Any(c=>c.Id.Contains("SupportMember")));
    }

    [TestMethod]
    public void BeamBoltDoubleShearIsNotAppliedTwiceAndEachAngleReceivesHalfTheLoad()
    {
        var r=DoubleAngleCleatComponents.Verify(Input());
        Equal(.9875,Trace(r,"Bolt.BetaLongJoint"));Equal(185808,Check(r,"BeamBolt1.DoubleShear").Resistance);
        Equal(891878.4,Trace(r,"SupportBolts.EquivalentGroupResistance"));
        Equal(450000*50,r.BeamBolts.ActionsAtCentroid.Mz);
        Equal(-450000,r.BeamBolts.Forces.Sum(f=>f.Fy));Equal(0,r.BeamBolts.Forces.Sum(f=>f.Fx));
        Equal(22500000,r.BeamBolts.Forces.Sum(f=>f.Position.X*f.Fy-f.Position.Y*f.Fx));
        Equal(-225000,r.LeftSupportBolts.Forces.Sum(f=>f.Fy));Equal(-225000,r.RightSupportBolts.Forces.Sum(f=>f.Fy));
        Equal(0,r.LeftSupportBolts.ActionsAtCentroid.Mz);Equal(0,r.RightSupportBolts.ActionsAtCentroid.Mz);
        Equal(109,Trace(r,"Geometry.SupportGauge"));Equal(107,Trace(r,"Geometry.BeamBottomBoltEdge"));
        foreach(var f in r.BeamBolts.Forces)
        {
            Equal(f.Fx/2,Trace(r,$"BeamBolt{f.Index+1}.LeftHorizontalForce"));
            Equal(f.Fy/2,Trace(r,$"BeamBolt{f.Index+1}.RightVerticalForce"));
        }
        foreach(var side in new[]{"Left","Right"})foreach(var part in new[]{"BeamLeg","SupportLeg"})
            Equal(225000,Check(r,$"{side}.{part}.BlockTearing").Demand);
    }

    [TestMethod]
    public void NtcUsesItsOwnPlateFactorsWhilePreservingConcomitantBoltAndBearingDemands()
    {
        var ec=DoubleAngleCleatComponents.Verify(Input());var ntc=DoubleAngleCleatComponents.Verify(Input(code:ConnectionDesignCode.Ntc2018()));
        Assert.AreEqual(ConnectionStatus.Passed,ntc.ComponentAssessment.Status);
        Equal(Check(ec,"Beam.GrossShear").Resistance/1.05,Check(ntc,"Beam.GrossShear").Resistance);
        Equal(.5*261*430/1.25+2871*275/Math.Sqrt(3)/1.05,Check(ntc,"Beam.BlockTearing").Resistance);
        Equal(Check(ec,"BeamBolt1.DoubleShear").Resistance,Check(ntc,"BeamBolt1.DoubleShear").Resistance);
        Equal(Trace(ec,"CleatBearing.Vertical"),Trace(ntc,"CleatBearing.Vertical"));
    }

    [DataTestMethod] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)]
    public void EveryQualifiedRowCountBalancesTheTwoPhysicalBoltPlanes(int rows)
    {
        var beam=rows==2?Section(220,110,6,10,8):rows==3?Section(300,150,7,12,8):
            rows==4?Section(413,180,9.5,16,10):rows==5?Section(480,200,9.5,16,10):Beam();
        var r=DoubleAngleCleatComponents.Verify(Input(rows:rows,beam:beam,v:1000));
        Assert.AreEqual(ConnectionStatus.Passed,r.ComponentAssessment.Status);Equal(rows,r.BeamBolts.Forces.Count);
        Equal(-1000,r.BeamBolts.Forces.Sum(x=>x.Fy));Equal(50000,r.BeamBolts.ActionsAtCentroid.Mz);
        Equal(-1000,r.LeftSupportBolts.Forces.Sum(x=>x.Fy)+r.RightSupportBolts.Forces.Sum(x=>x.Fy));
        Assert.IsTrue(r.ComponentAssessment.Checks.Any(x=>x.Id=="Beam.BlockTearing"));
    }

    [DataTestMethod] [DataRow(0.0)] [DataRow(1e-8)] [DataRow(.4)] [DataRow(1000.0)]
    public void ZeroAndTinyLoadsRetainEquilibriumAndNoAbsoluteForceFloor(double v)
    {
        var r=DoubleAngleCleatComponents.Verify(Input(v:v));Assert.AreEqual(ConnectionStatus.Passed,r.ComponentAssessment.Status);
        Assert.AreEqual(-v,r.BeamBolts.Forces.Sum(x=>x.Fy),Math.Max(double.Epsilon,v*1e-12));
        Assert.AreEqual(v/500724.4712819393,r.LoadUtilization!.Value,Math.Max(double.Epsilon,v*1e-12));
    }

    [DataTestMethod] [DataRow(.999999,ConnectionStatus.Passed)] [DataRow(1.000001,ConnectionStatus.Failed)]
    public void UnnotchedBeamBlockTearingControlsThePublishedExample(double factor,ConnectionStatus expected)
    {
        var r=DoubleAngleCleatComponents.Verify(Input(v:factor*500724.4712819393));
        Assert.AreEqual(expected,r.ComponentAssessment.Status);Equal(factor,r.LoadUtilization!.Value);
    }

    [DataTestMethod] [DataRow("Fx")] [DataRow("Fz")] [DataRow("Mx")] [DataRow("My")] [DataRow("Mz")]
    [DataRow("X")] [DataRow("Y")] [DataRow("Z")] [DataRow("up")]
    public void OrdinaryTensionTyingMomentsAndReversalAreNotSubstitutedForVerticalShear(string term)
    {
        const double s=1e-12;var a=new PlanarConnectionActions(term=="Fx"?s:0,term=="up"?s:-1000,term=="Fz"?s:0,
            term=="Mx"?s:0,term=="My"?s:0,term=="Mz"?s:0,term=="X"?s:0,term=="Y"?s:0,term=="Z"?s:0);
        Unsupported(DoubleAngleCleatComponents.Verify(Input(actions:a)),"DOWNWARD_SHEAR_AT_SUPPORT_FACE_ONLY");
    }
    [TestMethod] public void NotchIsUnsupported()=>Unsupported(DoubleAngleCleatComponents.Verify(Input(notched:true)),"NOTCHED_BEAM_NOT_QUALIFIED");
    [TestMethod] public void UnrestrainedBeamIsUnsupported()=>Unsupported(DoubleAngleCleatComponents.Verify(Input(restrained:false)),"LATERAL_RESTRAINT_REQUIRED");
    [TestMethod] public void NtcNeedsHoleEvidence()=>Unsupported(DoubleAngleCleatComponents.Verify(Input(code:ConnectionDesignCode.Ntc2018(),holes:null)),"NTC_HOLE_CLEARANCE_EVIDENCE_REQUIRED");
    [TestMethod] public void RootIntersectionIsUnsupported()=>Unsupported(DoubleAngleCleatComponents.Verify(Input(beam:Section(460,210,9,16,24))),"CLEAT_INTERSECTS_BEAM_ROOT_OR_BEAM_TOO_DEEP");
    [TestMethod] public void SlenderWebNeedsSeparateModel()=>Unsupported(DoubleAngleCleatComponents.Verify(Input(beam:Section(547,210,6,16,24))),"BEAM_SHEAR_BUCKLING_REQUIRES_SEPARATE_MODEL");
    [TestMethod] public void ThickAngleIsNotSilentlyTreatedAsTheSameFlexibleDetail()=>Unsupported(DoubleAngleCleatComponents.Verify(Input(leg:new(90,12,275,430))),"STANDARD_EQUAL_L90X90X10_DETAIL_ONLY");
    [TestMethod] public void DifferentBoltIsUnsupported()=>Unsupported(DoubleAngleCleatComponents.Verify(Input(bolt:new(20,22,245,ConnectionBoltGrade.Class10_9,true))),"STANDARD_M20_8_8_ONLY");
    [DataTestMethod] [DataRow(1)] [DataRow(7)] public void UnsupportedRowsAreNotClamped(int rows)=>Unsupported(DoubleAngleCleatComponents.Verify(Input(rows:rows)),"STANDARD_EQUAL_L90X90X10_DETAIL_ONLY");
    [DataTestMethod] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)] [DataRow(-1.0)]
    public void InvalidRootRejected(double radius)=>Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(root:radius));
    [TestMethod] public void NullInputRejected()=>Assert.ThrowsException<ArgumentNullException>(()=>DoubleAngleCleatComponents.Verify(null!));
}
