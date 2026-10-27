using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class BridgeSpliceCompressionInteractionTests
{
    internal static WeldedIBeamSpliceForceInput Detail(PlanarConnectionActions? actions = null, double first = 55, double webCoverThickness = 10)
    {
        var f = new SpliceFlangeCoverDetail(new(440,25,355,510),new(170,25,355,510),60,50,new(3,2,80,230,55));
        return new(new(new(500,40,355,510),new(1000,35,355,510),new(500,40,355,510),8,8),f,f,
            new(925,webCoverThickness,355,510),new(1,12,75,75,first),50,10,actions ?? new(-2e5,2e4,0,0,0,1e8));
    }
    static SpliceCoverPlateAssessment Assess(bool ntc,double shearRatio,double axialRatio,double first=55,double sign=1)
    {
        var d=Detail(first:first); double g0=ntc?1.05:1;
        double a=9250,an=(925-12*26)*10d;
        double v=shearRatio*Math.Min(a*355/(1.27*Math.Sqrt(3)*g0),an*510/(Math.Sqrt(3)*1.25));
        double n=-axialRatio*a*355/g0;
        var actions=new PlanarConnectionActions(2*n*d.Section.Area/(1000*35),2*sign*v,0,0,0,0);
        var result = WeldedIBeamSplicePlates.Verify(WeldedIBeamSplicePlateTests.Input(ntc,Detail(actions,first)));
        Assert.AreEqual(0,result.Diagnostics.Count,string.Join(";",result.Diagnostics));
        return result.Covers.Single(c=>c.Cover==WeldedSpliceCover.WebPositive);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void SeparateComponentChecksCanPassWhileCoupledInteractionFails(bool ntc)
    {
        double g0=ntc?1.05:1,a=9250,w=10*925*925/6d;
        double v=.9*Math.Min(a*355/(1.27*Math.Sqrt(3)*g0),(925-12*26)*10*510/(Math.Sqrt(3)*1.25));
        double rho=.64,m=v*55/(w*355/g0),n=.99-m/(1-rho);
        var c=Assess(ntc,.9,n);
        Assert.AreEqual(ConnectionStatus.Passed,c.Sections.Status);
        Assert.IsTrue(c.LongitudinalCompression.Checks.Where(x=>x.Id.StartsWith("MaximumCompression.")).All(x=>x.Passed));
        Assert.IsTrue(c.LongitudinalCompression.IsComplete);
        Assert.AreEqual(ConnectionStatus.Failed,c.LongitudinalCompression.Status);
        Assert.IsTrue(c.LongitudinalCompression.Checks.Any(x=>x.Id.EndsWith(".CompressionShearBending")&&!x.Passed));
    }
    [DataTestMethod]
    [DataRow(false,.5,1d)][DataRow(false,.50001,1d)][DataRow(false,.9,1d)][DataRow(false,.9,-1d)]
    [DataRow(true,.5,1d)][DataRow(true,.50001,1d)][DataRow(true,.9,1d)][DataRow(true,.9,-1d)]
    public void EveryCutMatchesIndependentP398Interaction(bool ntc,double shearRatio,double sign)
    {
        var c=Assess(ntc,shearRatio,.2,sign:sign); double a=9250,g0=ntc?1.05:1,g1=ntc?1.05:1;
        // Hand arithmetic: normal filled holes, Lcr=.6*110mm, i=t/sqrt12, curve c.
        double lambda=66*Math.Sqrt(12*355d/210000)/(Math.PI*10);
        double phi=.5*(1+.49*(lambda-.2)+lambda*lambda),chi=Math.Min(1,1/(phi+Math.Sqrt(phi*phi-lambda*lambda)));
        double nb=a*Math.Min(355/g0,chi*355/g1),vp=Math.Min(a*355/(1.27*Math.Sqrt(3)*g0),(925-12*26)*10*510/(Math.Sqrt(3)*1.25));
        foreach(var check in c.LongitudinalCompression.Checks.Where(x=>x.Id.EndsWith(".CompressionShearBending")))
        {
            int k=int.Parse(check.Id.Split('.')[0][3..]);var cut=c.Cuts[k];
            double v=Math.Abs(cut.V)/vp,rho=v<=.5?0:Math.Pow(2*v-1,2);
            Near(Math.Max(0,1-Math.Abs(cut.N)/nb)*(1-rho)*(10*925*925/6d)*355/g0,check.Resistance);
            Near(Math.Abs(cut.M),check.Demand);
        }
        Near(nb,c.LongitudinalCompression.Trace.Single(x=>x.Name=="CompressionResistanceForInteraction").Value);
    }
    [TestMethod] public void ZeroAndTensileAxialForceDoNotActivateCompressionInteraction()
    {
        foreach(double n in new[]{0d,-.2})
            Assert.IsFalse(Assess(false,.7,n).LongitudinalCompression.Checks.Any(x=>x.Id.EndsWith(".CompressionShearBending")));
    }
    [TestMethod] public void HighShearRemovesAllMomentCapacityWithoutNan()
    {
        var c=Assess(false,1.2,.2);
        Assert.IsTrue(c.LongitudinalCompression.Checks.Any(x=>x.Id.EndsWith(".CompressionShearBending")&&x.Demand>0&&x.Resistance==0));
        Assert.IsTrue(c.LongitudinalCompression.Trace.All(x=>double.IsFinite(x.Value)));
    }
    static void Near(double a,double b)=>Assert.AreEqual(a,b,Math.Max(1e-7,Math.Abs(a)*1e-10));
}
