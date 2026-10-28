using System.Reflection;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;
[TestClass]
public class ConnectionRecommendedEntryTests
{
    [TestMethod]
    public void CatalogMethodsExistAndRecommendationsBelongToTheirDeclaredFamily()
    {
        var types=typeof(ConnectionCapabilityCatalog).Assembly.GetTypes();
        foreach(var item in ConnectionCapabilityCatalog.All)
        {
            if(item.EntryPoints.Count==0) { Assert.IsNull(item.RecommendedEntryPoint,item.Id);continue; }
            Assert.IsTrue(item.EntryPoints.Contains(item.RecommendedEntryPoint),item.Id);
            foreach(string api in item.EntryPoints)
            {
                var parts=api.Split('.');Assert.AreEqual(2,parts.Length,api);
                Assert.IsTrue(types.Any(t=>t.Name==parts[0]&&t.GetMethods(BindingFlags.Public|BindingFlags.Static)
                    .Any(m=>m.Name==parts[1])),api);
            }
        }
    }
    [DataTestMethod]
    [DataRow("WeldedIBeamDoubleCover","BridgeSpliceApplication.CreateBolted")]
    [DataRow("WeldedIBeamButt","BridgeSpliceApplication.VerifyWelded")]
    [DataRow("StandardMomentEndPlateNMV","StandardEndPlate.Verify")]
    [DataRow("AnglePairCoplanarCollector","BraceAnglePairStandardTension.Verify")]
    public void RecommendedApplicationEntrancesRemainExplicit(string id,string api)
        =>Assert.AreEqual(api,ConnectionCapabilityCatalog.Get(id).RecommendedEntryPoint);

    [TestMethod]
    public void NewLowShearModeDoesNotEraseUnimplementedMechanisms()
    {
        var splice=ConnectionCapabilityCatalog.Get("WeldedIBeamDoubleCover");
        StringAssert.Contains(splice.Domain,"EffectiveCompressionWithLowShear");
        Assert.IsTrue(splice.MissingMechanisms.Any(x=>x.Contains("section transitions")));
        Assert.IsTrue(splice.MissingMechanisms.Any(x=>x.Contains("Mixed-direction")));
        Assert.AreEqual(ConnectionCapabilityLevel.ComponentsOnly,ConnectionCapabilityCatalog.Get("DoubleAngleCleat").Level);
    }
}
