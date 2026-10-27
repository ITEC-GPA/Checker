using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class BridgeSpliceClass4LowShearTests
{
    static WeldedIBeamSpliceDetailInput Input(bool ntc=false,double v=20000,double n=-100000,double webThickness=6)
    {
        var p=BridgeSpliceEffectiveCompressionTests.Input(ntc,n,v,outerWidth:180);
        var b=p.Plates.Bolts;var d=b.Detail;
        var f=new WeldedIBeamSpliceForceInput(d.Section,d.Top,d.Bottom,new(280,webThickness,355,510),
            new(3,2,70,70,35),60,d.ButtGap,d.Actions);
        return new(new(p.Plates.Policy,new(b.Code,f,b.FlangeBolt,new ConnectionBolt(12,13,84.3,ConnectionBoltGrade.Class8_8,true),b.FlangeHardwareDiameter,24,
            "Concomitant ULS","NTC SLE clearance")),p.Exposure);
    }
    static double Trace(ConnectionVerification<WeldedIBeamSpliceDetailInput> r,string suffix)
        =>r.Trace.Single(x=>x.Name=="WebNegative."+suffix).Value;
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void CompressedClassFourCoverAdmitsNonzeroLowShear(bool ntc)
    {
        var p=Input(ntc);var r=WeldedIBeamSpliceEffectiveCompression.Verify(p,true);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
        Assert.AreEqual(1,Trace(r,"IsLocallyReduced"));
        Assert.IsTrue(r.Trace.Any(x=>x.Name.EndsWith("LowShear.MaximumDemand")&&x.Value>0));
        var old=WeldedIBeamSpliceEffectiveCompression.Verify(p);
        Assert.AreEqual(ConnectionStatus.NotSupported,old.Status);
    }
    [DataTestMethod][DataRow(false,1d)][DataRow(false,-1d)][DataRow(true,1d)][DataRow(true,-1d)]
    public void CompleteStandardFlowPreservesResistanceAndReport(bool ntc,double sign)
    {
        var p=Input(ntc,sign*20000);
        var r=WeldedIBeamSpliceStandard.Verify(BridgeSpliceEffectiveCompressionTests.Workflow(p,method:SplicePlateStabilityMethod.EffectiveCompressionWithLowShear));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        var report=BridgeSpliceReport.FromBolted(r);Assert.IsTrue(report.IsComplete);
        StringAssert.Contains(report.Scope,"EffectiveCompressionWithLowShear");
    }
    [TestMethod] public void ShearBucklingCannotBeWaivedByUsingEffectiveArea()
    {
        var p=Input(webThickness:4);
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(p,true);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.Diagnostics.Any(x=>x.EndsWith("CLASS4_SHEAR_BUCKLING_NOT_EXCLUDED")));
    }
    [TestMethod] public void IndependentNetAreaAndShearLimitUseTheRemovedOutstands()
    {
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input(),true);
        // Uniform compression of c=105mm outstands. Independent EN1993-1-5
        // Table4.2 calculation, t6, fy355, k=.43; two13mm holes at +/-35.
        double lambda=105d/6/(28.4*Math.Sqrt(235d/355)*Math.Sqrt(.43));
        double rho=Math.Min(1,(lambda-.188)/(lambda*lambda));
        double net=(70+2*105*rho-26)*6;
        Near(net,Trace(r,"EffectiveNetArea"));
        Near(net*355/(1.5*Math.Sqrt(3)),Trace(r,"LowShear.ConservativeShearResistance"));
    }
    [TestMethod] public void AboveHalfShearRemainsIncompleteAndKeepsOnlyKnownFailures()
    {
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input(v:1e7),true);
        Assert.IsTrue(r.Diagnostics.Any(x=>x.EndsWith("CLASS4_SHEAR_EXCEEDS_HALF_RESISTANCE")));
        // Outside the low-shear field no favourable check qualifies the cover; a check that
        // already fails under this very large shear is retained as a known failure.
        Assert.IsFalse(r.IsComplete);Assert.IsTrue(r.Checks.All(x=>!x.Passed));
        Assert.AreEqual(r.Checks.Count>0?ConnectionStatus.Failed:ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(WeldedIBeamSpliceBolts.Verify(Input(v:1e7).Plates.Bolts).ComponentAssessment.Checks.Any(x=>!x.Passed));
    }
    [DataTestMethod][DataRow(-1e-15)][DataRow(1e-15)]
    public void SmallNonzeroShearIsCheckedWithoutAZeroTolerance(double v)
    {
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input(v:v),true);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
        Assert.IsTrue(r.Trace.Any(x=>x.Name.EndsWith("LowShear.MaximumDemand")&&x.Value>0));
    }
    [DataTestMethod][DataRow(.9999,true)][DataRow(1.0001,false)]
    public void HalfResistanceBoundaryUsesActualCutDemand(double factor,bool supported)
    {
        var reference=WeldedIBeamSpliceEffectiveCompression.Verify(Input(),true);
        double v=20000*Trace(reference,"LowShear.AdmittedLimit")/Trace(reference,"LowShear.MaximumDemand")*factor;
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input(v:v),true);
        Assert.AreEqual(!supported,r.Diagnostics.Any(x=>x.EndsWith("CLASS4_SHEAR_EXCEEDS_HALF_RESISTANCE")));
    }
    [TestMethod]
    public void IndependentGammaM1CannotBeBypassedByTheGrossShearGammaM0()
    {
        var p=Input(v:100000);
        p=new(new(IBeamSplicePlatePolicy.Eurocode3(p.Plates.Policy.Code,4),p.Plates.Bolts),p.Exposure);
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(p,true);
        double bound=280d*6*355/(Math.Sqrt(3)*4);
        Near(bound,Trace(r,"LowShear.ConservativeShearResistance"));
        Near(bound,Trace(r,"LowShear.ShearBucklingReferenceResistance"));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.Diagnostics.Any(x=>x.EndsWith("CLASS4_SHEAR_EXCEEDS_HALF_RESISTANCE")));
    }
    static void Near(double a,double b)=>Assert.AreEqual(a,b,Math.Max(1e-8,Math.Abs(a)*1e-10));
}




