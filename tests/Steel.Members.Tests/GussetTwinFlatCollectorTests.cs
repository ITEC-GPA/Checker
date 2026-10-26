using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class GussetTwinFlatCollectorTests
{
    private static GussetTwinFlatCollectorInput Input(bool ntc=false,double n=100000,ConnectionPlate? collector=null,
        SteelActions? cut=null,double distance=100,double fusion=16,double weldWidth=200,double weldFy=355,double weldFu=510,
        bool cjp=true,bool aligned=true,string source="Inspection and matching consumables")
    {
        var code=ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"Explicit factors");
        var t=new GussetTwinFlatTensionInput(code,new(200,16,275,430),new(160,10,275,430),
            new(20,21,245,ConnectionBoltGrade.Class8_8,true),new(n,0,0,0,0,0),2,60,80,40,50);
        return new(t,collector??new(200,16,235,360),cut??new SteelActions(n,0,0,0,0,0),distance,fusion,weldWidth,weldFy,weldFu,cjp,aligned,source,"FEM LC1 collector cut");
    }
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void IndependentConstantBarAndCjpEquilibrium(bool ntc)
    {
        var r=GussetTwinFlatCollector.Verify(Input(ntc));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.CollectorAndWeld.Diagnostics));
        Assert.IsTrue(r.IsStaticResistanceComplete);
        Assert.AreEqual(3200*235/(ntc?1.05:1),r.CollectorAndWeld.Checks[0].Resistance,1e-7);
        Assert.AreEqual(.9*3200*360/1.25,r.CollectorAndWeld.Checks[1].Resistance,1e-7);
        Assert.AreEqual(100000,r.CollectorAndWeld.Checks[2].Demand);
    }
    [TestMethod] public void WeakerCollectorCanFailWhileTransferPasses()
    {
        var r=GussetTwinFlatCollector.Verify(Input(n:100000,collector:new(200,16,20,30)));
        Assert.AreEqual(ConnectionStatus.Passed,r.Transfer.Status); Assert.AreEqual(ConnectionStatus.Failed,r.Status);
    }
    [TestMethod] public void FailedTransferSurvivesUnsupportedCollector()
    {
        var r=GussetTwinFlatCollector.Verify(Input(n:1e7,aligned:false));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status); Assert.IsFalse(r.IsStaticResistanceComplete);
    }
    [TestMethod] public void WiderCollectorCannotSilentlyCreditLoadSpread()
    { Assert.AreEqual(ConnectionStatus.NotSupported,GussetTwinFlatCollector.Verify(Input(collector:new(240,16,275,430))).Status); }
    [TestMethod] public void ThicknessTransitionNotImplicitlyQualified()
    { Assert.AreEqual(ConnectionStatus.NotSupported,GussetTwinFlatCollector.Verify(Input(collector:new(200,20,275,430))).Status); }
    [DataTestMethod] [DataRow(79d,16d,200d,true)] [DataRow(80d,16d,200d,true)] [DataRow(100d,15d,200d,true)] [DataRow(100d,16d,199d,true)] [DataRow(100d,16d,200d,false)]
    public void WeldGeometryAndPenetrationMustBePhysical(double x,double depth,double width,bool cjp)
    { Assert.AreEqual(ConnectionStatus.NotSupported,GussetTwinFlatCollector.Verify(Input(distance:x,fusion:depth,weldWidth:width,cjp:cjp)).Status); }
    [TestMethod] public void UnderMatchingWeldRefused()
    { Assert.AreEqual(ConnectionStatus.NotSupported,GussetTwinFlatCollector.Verify(Input(weldFy:200,weldFu:300)).Status); }
    [TestMethod] public void DifferentCutCannotBeASeparateEnvelope()
    { Assert.AreEqual(ConnectionStatus.NotSupported,GussetTwinFlatCollector.Verify(Input(cut:new(100001,0,0,0,0,0))).Status); }
    [TestMethod] public void TinyActualMomentNotDiscarded()
    { Assert.AreEqual(ConnectionStatus.NotSupported,GussetTwinFlatCollector.Verify(Input(cut:new(100000,0,0,0,1e-20,0))).Status); }
    [TestMethod] public void CompressionStillRequiresAStabilityModel()
    { Assert.AreEqual(ConnectionStatus.NotSupported,GussetTwinFlatCollector.Verify(Input(n:-1000)).Status); }
    [TestMethod] public void ExecutionSourceRequired()
    { Assert.AreEqual(ConnectionStatus.NotSupported,GussetTwinFlatCollector.Verify(Input(source:"")).Status); }
    [TestMethod] public void NullCaseAndImmutableResults()
    {
        var r=GussetTwinFlatCollector.Verify(Input(n:0)); Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        Assert.AreEqual(0,r.Utilization); Assert.ThrowsException<NotSupportedException>(()=>((IList<ConnectionCheck>)r.CollectorAndWeld.Checks).Clear());
    }
}
