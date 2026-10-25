using GPC.Checkers.CompositeBridge;
using GPC.Checkers.Steel.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass]
public class SharedWebPanelKernelTests
{
    [DataTestMethod] [DataRow(0d,false,false)] [DataRow(500d,false,false)] [DataRow(1000d,false,true)]
    [DataRow(4000d,true,true)] [DataRow(4000d,false,true)] [DataRow(4000d,true,false)]
    public void ExistingBridgeContractUsesSameCommonKernelWithoutNumericalChanges(double panel,bool rigid,bool etaArea)
    {
        var b=BridgeShearConnection.Web(1000,5,355,210000,1,1.1,1.2,panel,rigid,etaArea);
        var s=Ec3WebPanelShear.Calculate(1000,5,355,210000,1,1.1,1.2,panel,rigid,etaArea);
        Assert.AreEqual(s.KTau,b.KTau);Assert.AreEqual(s.TauCritical,b.TauCritical);
        Assert.AreEqual(s.Slenderness,b.Slenderness);Assert.AreEqual(s.Chi,b.Chi);
        Assert.AreEqual(s.Area,b.Area);Assert.AreEqual(s.ShearArea,b.ShearArea);
        Assert.AreEqual(s.PlasticResistance,b.PlasticResistance);Assert.AreEqual(s.BucklingResistance,b.BucklingResistance);
        Assert.AreEqual(s.Resistance,b.Resistance);
    }
    [TestMethod]
    public void JrcPublishedCriticalStressRetainedByBridgeFacade()
    {
        var r=BridgeShearConnection.Web(2720,18,345,210000,1,1.1,1.2,8000);
        Assert.AreEqual(5.8024,r.KTau,1e-12);Assert.AreEqual(48.228,r.TauCritical,.01);
    }
}
