using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class WeldedIBeamSpliceSlipTests
{
    internal static WeldedIBeamSpliceSlipInput Input(bool ntc=false,BoltSlipLimitState state=BoltSlipLimitState.Serviceability,
        WeldedIBeamSpliceForceInput? detail=null, double mu=.4, BoltTighteningControl control=BoltTighteningControl.Controlled,
        string? assembly="Actual preloaded assembly",string? source="Actual combination",string? surface="Qualified surface")
    {
        var b=WeldedIBeamSpliceTests.BoltInput(ntc,detail??WeldedIBeamSplicePlateTests.Detail(new(2e5,0,0,0,0,0)));
        return new(ntc?BoltSlipPolicy.Ntc2018(b.Code):BoltSlipPolicy.Eurocode3(b.Code,1.25,1.10),b,state,
            Enum.GetValues<WeldedSpliceCover>().Select(c=>new WeldedSpliceFrictionSurface(c,mu,surface!)),control,assembly!,source!);
    }
    static void Close(double e,double a)=>WeldedIBeamSpliceTests.Close(e,a,1e-10);
    [DataTestMethod]
    [DataRow(false,BoltSlipLimitState.Serviceability,1.1)][DataRow(false,BoltSlipLimitState.Ultimate,1.25)]
    [DataRow(true,BoltSlipLimitState.Serviceability,1.1)][DataRow(true,BoltSlipLimitState.Ultimate,1.25)]
    public void EachActualInterfaceUsesOnePreloadAndTheCorrectIndependentPartialFactor(bool ntc,BoltSlipLimitState state,double gamma)
    {
        var r=WeldedIBeamSpliceSlip.Verify(Input(ntc,state));Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        var slip=r.Checks.Where(c=>c.Id.EndsWith(state==BoltSlipLimitState.Ultimate?"Slip.Ultimate":"Slip.Serviceability")).ToArray();
        // Two flanges * two sides * two planes * six bolts + web two sides * two planes * nine bolts.
        Assert.AreEqual(84,slip.Length);
        foreach(var c in slip)Close(.7*800*353*.4/gamma,c.Resistance);
        Assert.IsTrue(r.Trace.Where(t=>t.Name.EndsWith("FrictionPlanes")).All(t=>t.Value==1));
        Assert.IsTrue(r.Trace.Where(t=>t.Name.EndsWith("PreloadForSlip")).All(t=>t.Value==.7*800*353));
        Close(2e5*12000/29120/2/6,r.Checks.Single(c=>c.Id=="Left.TopOuter.Bolt0.Slip."+state).Demand);
    }
    [DataTestMethod][DataRow(false,ConnectionStatus.NotSupported)][DataRow(true,ConnectionStatus.Passed)]
    public void UncontrolledTighteningRemainsAnNtcOptionWithGammaM7(bool ntc,ConnectionStatus status)
    {
        var r=WeldedIBeamSpliceSlip.Verify(Input(ntc,control:BoltTighteningControl.Uncontrolled));Assert.AreEqual(status,r.Status);
        if(ntc)Close(.7*800*353*.4/(1.1*1.1),r.Checks.First(c=>c.Id.EndsWith("Slip.Serviceability")).Resistance);
    }
    [TestMethod]
    public void StrongOppositeSurfaceCannotRescueOneWeakFace()
    {
        var p=Input();var surfaces=p.Surfaces.Select(s=>new WeldedSpliceFrictionSurface(s.Cover,s.Cover==WeldedSpliceCover.TopOuter?.001:.9,s.QualificationSource));
        var r=WeldedIBeamSpliceSlip.Verify(new(p.Policy,p.Bolts,p.LimitState,surfaces,p.TighteningControl,p.AssemblyQualificationSource,p.ActionCombinationSource));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsTrue(r.Checks.Where(c=>!c.Passed).All(c=>c.Id.Contains("TopOuter")));
        Close(.7*800*353*.001/1.1,r.Checks.First(c=>c.Id.Contains("TopOuter")&&c.Id.EndsWith("Slip.Serviceability")).Resistance);
    }
    [DataTestMethod][DataRow("assembly")][DataRow("surface")][DataRow("combination")][DataRow("missing")][DataRow("code")]
    public void MissingOrInconsistentEvidenceNeverProducesAFavourableSlipResult(string flaw)
    {
        var p=Input(assembly:flaw=="assembly"?null:"Assembly",source:flaw=="combination"?null:"Case",surface:flaw=="surface"?null:"Surface");
        if(flaw=="missing")p=new(p.Policy,p.Bolts,p.LimitState,p.Surfaces.Skip(1),p.TighteningControl,p.AssemblyQualificationSource,p.ActionCombinationSource);
        if(flaw=="code")p=new(BoltSlipPolicy.Ntc2018(ConnectionDesignCode.Ntc2018()),p.Bolts,p.LimitState,p.Surfaces,p.TighteningControl,p.AssemblyQualificationSource,p.ActionCombinationSource);
        var r=WeldedIBeamSpliceSlip.Verify(p);Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.AreEqual(0,r.Checks.Count);
    }
    [TestMethod]
    public void LongJointUsesTheIndividualCoverGroupDiagonal()
    {
        var d=WeldedIBeamSpliceTests.Input();var p=Input(detail:d);
        var top=new SpliceFlangeCoverDetail(d.Top.Outer,d.Top.EachInner,d.Top.InnerGap,d.Top.EndDistance,new(8,2,90,180,80));
        var changed=new WeldedIBeamSpliceForceInput(d.Section,top,d.Bottom,d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
        var r=WeldedIBeamSpliceSlip.Verify(Input(detail:changed));Assert.IsTrue(r.IsComplete);
        double length=Math.Sqrt(630*630+180*180),beta=1-(length-15*24)/(200*24d);
        Close(beta,r.Trace.Single(t=>t.Name=="Left.TopOuter.Bolt0.BetaLongJoint").Value);
        Close((1-(630-360)/4800d),r.Trace.Single(t=>t.Name=="Left.TopInnerPositive.Bolt0.BetaLongJoint").Value);
    }
    [DataTestMethod][DataRow(false,1d)][DataRow(true,1.05)]
    public void CategoryCNetAxialPlasticUsesFyAndAllEightCoversAndThreeParentPlates(bool ntc,double gamma)
    {
        var r=WeldedIBeamSpliceSlip.VerifyNetAxialPlastic(WeldedIBeamSplicePlateTests.Input(ntc));
        Assert.IsTrue(r.IsComplete);Assert.AreEqual(11,r.Checks.Count);
        Close((360-2*26)*20*355/gamma,r.Checks.Single(c=>c.Id=="TopOuter.NetAxialPlastic").Resistance);
        Close((140-26)*20*355/gamma,r.Checks.Single(c=>c.Id=="TopInnerPositive.NetAxialPlastic").Resistance);
        Close((400-2*26)*30*355/gamma,r.Checks.Single(c=>c.Id=="Top.Parent.NetAxialPlastic").Resistance);
        Close((320-3*26)*16*355/gamma,r.Checks.Single(c=>c.Id=="Web.Parent.NetAxialPlastic").Resistance);
        StringAssert.Contains(r.Scope,"NOT N-V-M");
    }
    [TestMethod]
    public void CompressedStateHasNoTensileNetPlasticDemand()
    {
        var r=WeldedIBeamSpliceSlip.VerifyNetAxialPlastic(WeldedIBeamSplicePlateTests.Input(detail:WeldedIBeamSplicePlateTests.Detail(new(-1e6,0,0,0,0,0))));
        Assert.IsTrue(r.Checks.All(c=>c.Demand==0));
    }
    [TestMethod]
    public void DuplicateSurfacesInvalidCoefficientsAndNullAreRejected()
    {
        var p=Input();Assert.ThrowsException<ArgumentNullException>(()=>WeldedIBeamSpliceSlip.Verify(null!));
        Assert.ThrowsException<ArgumentException>(()=>new WeldedIBeamSpliceSlipInput(p.Policy,p.Bolts,p.LimitState,p.Surfaces.Concat(p.Surfaces.Take(1)),p.TighteningControl,"Assembly","Case"));
        foreach(double mu in new[]{0d,-.1,1.01,double.NaN,double.PositiveInfinity})Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new WeldedSpliceFrictionSurface(WeldedSpliceCover.TopOuter,mu,"source"));
    }
}
