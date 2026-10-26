using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class BridgeSpliceZeroShearTests
{
    static WeldedIBeamSpliceStandardInput Input(bool ntc,double v=0,double n=50000)
    {
        var basis=WeldedIBeamSpliceStandardTests.Detail(new(n,v,0,0,0,0));
        var d=new WeldedIBeamSpliceForceInput(basis.Section,basis.Top,basis.Bottom,new(200,3,355,510),new(3,5,40,40,21),20,4,basis.Actions);
        var b=WeldedIBeamSpliceTests.BoltInput(ntc,d);
        var bolts=new WeldedIBeamSpliceBoltInput(b.Code,d,b.FlangeBolt,new(12,13,84.3,ConnectionBoltGrade.Class8_8,true),
            b.FlangeHardwareDiameter,22,b.UltimateCombinationSource,b.HoleClearanceServiceabilitySource);
        var plate=new WeldedIBeamSplicePlateInput(ntc?IBeamSplicePlatePolicy.Ntc2018():IBeamSplicePlatePolicy.Eurocode3(b.Code,1),bolts);
        var detail=new WeldedIBeamSpliceDetailInput(plate,SpliceExposure.ExposedCarbonSteel);
        return new(new[]{new WeldedSpliceStandardCase("U",WeldedIBeamSpliceStandardTests.Parents(detail))},SpliceBoltCategory.A,null,
            SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void PerforatedThinWebCoverInPureTensionDoesNotRequireShearBucklingResistance(bool ntc)
    {
        var p=Input(ntc);var r=WeldedIBeamSpliceStandard.Verify(p);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        Assert.IsTrue(200d/3>60*Math.Sqrt(235d/355));
        var c=r.Cases[0].AdditionalPlates;
        Assert.AreEqual(0d,c.CompactPlateDomain.Checks.Single(x=>x.Id=="WebPositive.NoShearBucklingDomain").Demand);
        // Manual perforated area:200x3 minus five13mm holes.
        Assert.AreEqual(405d,c.NetSections.Trace.Single(x=>x.Name=="WebPositive.Anet").Value);
        Assert.IsTrue(r.Cases[0].Components.Plates.Covers.Where(x=>x.Cover==WeldedSpliceCover.WebPositive)
            .Single().Cuts.All(x=>x.V==0&&x.M==0&&x.N>=0));
    }
    [DataTestMethod][DataRow(1e-15)][DataRow(-1e-15)][DataRow(1000d)]
    public void EveryNonzeroShearRestoresTheFullApplicabilityGuard(double v)
    {
        var r=WeldedIBeamSpliceStandard.Verify(Input(false,v));
        Assert.IsFalse(r.IsStaticResistanceComplete);
        Assert.IsTrue(r.MissingRequirements.Any(x=>x.Contains("CompactPlateDomain")));
    }
    [TestMethod] public void TensileOverloadStillFailsTheActualNetResistance()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Input(false,n:2e6));
        Assert.IsTrue(r.HasFailedComponent);Assert.AreEqual(ConnectionStatus.Failed,r.Status);
    }
}
