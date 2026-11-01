using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class GussetTransverseCollectorTests
{
    private static GussetTwinFlatTransverseCollectorInput Input(bool ntc=false,double force=50000,double span=160,double thickness=20,
        string change="",double throat=6)
    {
        var code=ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"Explicit EC factors");
        var transfer=new GussetTwinFlatTensionInput(code,new(200,16,275,430),new(160,10,275,430),
            new(20,21,245,ConnectionBoltGrade.Class8_8,true),new(force,0,0,0,0,0),2,60,80,40,50);
        var support=new GussetTwinFlatWeldedSupportInput(transfer,120,200,span,throat,FilletWeldSteelGrade.S275,
            new(FilletWeldSteelGrade.S275,430,thickness),FilletWeldMethod.Directional,FilletWeldEndCondition.FullSize,
            "Matching consumable","Uniform line load, same width strips; symmetric centre displacement and zero rotation","Continuous double-sided fillets");
        return new(support,new(change=="width"?210:200,change=="thickness"?thickness+1:thickness,change=="grade"?355:275,change=="fu"?510:430),
            change=="span"?span+10:span,210000,
            new(force/2,0,0,0,0,0,120,0,-span/2),
            new(change=="reaction"?-force/2:force/2,change=="other-load"?1:0,0,0,change=="moment"?1:0,0,120,0,change=="position"?-span/2:span/2),
            change=="source"?null!:"Two full-width simple support lines, actual reactions and free rotations",
            "One concomitant design situation",change!="holes");
    }
    private static double Value(GussetTwinFlatTransverseCollectorResult r,string key)=>r.Collector.Trace.Single(t=>t.Name==key).Value;

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void FullTerminalIncludesTheSupportAgainstIndependentBeamIntegrals(bool ntc)
    {
        // Hilti Modular Support Systems Technical Guide Ed1, beam table:
        // central load on simple supports, M=PL/4, V=P/2, delta=PL^3/48EI.
        // Independent rectangle: B=200,t=20,L=160,P=50kN. I=133333.333,
        // M=2e6, sigma=150, tau_max=9.375MPa. Add Timoshenko PL/(4kGA).
        var r=GussetTwinFlatTransverseCollector.Verify(Input(ntc));
        Assert.IsTrue(r.IsStaticResistanceComplete,string.Join(";",r.Collector.Diagnostics));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        Assert.AreEqual(2e6,Value(r,"Collector.MaximumMoment"),1e-7);
        Assert.AreEqual(25000,Value(r,"Collector.MaximumShear"),1e-7);
        Assert.AreEqual(150,Value(r,"Collector.MaximumNormalStress"),1e-10);
        Assert.AreEqual(9.375,Value(r,"Collector.MaximumShearStress"),1e-10);
        double equivalent=Math.Sqrt(22500+3*87.890625);
        Assert.AreEqual(equivalent,r.Collector.Checks.Single().Demand,1e-10);
        Assert.AreEqual(equivalent/(275/(ntc?1.05:1)),r.Utilization!.Value,1e-10);
        Assert.AreEqual(50000d*160*160*160/(48*210000*(200d*20*20*20/12))
            +50000d*160/(4*(5d/6)*(210000/2.6)*4000),Value(r,"Collector.CentreDisplacement"),1e-12);
        Assert.AreEqual(1d/350,Value(r,"Collector.EndRotation"),1e-12);
        Assert.IsFalse(r.Attachment.CanQualifyWholeJoint,"The attachment alone must remain a component result.");
    }

    [TestMethod]
    public void CollectorCanFailWhileBoltsGussetAndWeldPass()
    {
        var r=GussetTwinFlatTransverseCollector.Verify(Input(force:100000));
        Assert.AreEqual(ConnectionStatus.Passed,r.Attachment.Status);
        Assert.AreEqual(ConnectionStatus.Failed,r.Collector.Status);
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.Utilization>1);
    }

    [TestMethod]
    public void AttachmentFailureSurvivesAnUnsupportedCollector()
    {
        var r=GussetTwinFlatTransverseCollector.Verify(Input(force:700000,thickness:80,change:"source",throat:3));
        Assert.AreEqual(ConnectionStatus.Failed,r.Attachment.Status);
        Assert.IsFalse(r.IsStaticResistanceComplete);Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsNull(r.Utilization);
    }

    [DataTestMethod]
    [DataRow("width")][DataRow("thickness")][DataRow("grade")][DataRow("fu")][DataRow("span")]
    [DataRow("reaction")][DataRow("other-load")][DataRow("moment")][DataRow("position")][DataRow("source")][DataRow("holes")]
    public void UnsupportedSupportAndActionModelsNeverBecomeComplete(string change)
    {
        var r=GussetTwinFlatTransverseCollector.Verify(Input(change:change));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsFalse(r.IsStaticResistanceComplete);Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Collector.Diagnostics.Count>0);
    }

    [TestMethod]
    public void LinearModelDomainIsNotAServiceabilityPass()
    {
        var r=GussetTwinFlatTransverseCollector.Verify(Input(force:5000,span:4000));
        Assert.IsFalse(r.IsStaticResistanceComplete);
        CollectionAssert.Contains(r.Collector.Diagnostics.ToArray(),"COLLECTOR_LINEAR_SMALL_DEFORMATION_DOMAIN_EXCEEDED");
    }

    [TestMethod]
    public void WiderSpanIncreasesSupportDemandAndDisplacement()
    {
        var a=GussetTwinFlatTransverseCollector.Verify(Input(span:80));
        var b=GussetTwinFlatTransverseCollector.Verify(Input(span:160));
        Assert.IsTrue(a.IsStaticResistanceComplete);Assert.IsTrue(b.IsStaticResistanceComplete);
        Assert.AreEqual(2*Value(a,"Collector.MaximumMoment"),Value(b,"Collector.MaximumMoment"),1e-8);
        Assert.IsTrue(b.Utilization>a.Utilization);Assert.IsTrue(Value(b,"Collector.CentreDisplacement")>Value(a,"Collector.CentreDisplacement"));
    }

    [TestMethod]
    public void ZeroTensionKeepsZeroActionsWithoutInventingCompression()
    {
        var r=GussetTwinFlatTransverseCollector.Verify(Input(force:0));
        Assert.IsTrue(r.IsStaticResistanceComplete);Assert.AreEqual(0,r.Utilization);
        Assert.AreEqual(0,Value(r,"Collector.MaximumMoment"));
    }
}
