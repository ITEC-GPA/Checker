using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;

[TestClass]
public class BridgeSpliceDirectBlockTests
{
    internal static WeldedIBeamSpliceStandardInput Direct(WeldedIBeamSpliceStandardInput p)
        => new(p.Cases, p.Envelope.Category, p.Envelope.SlipCases, SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback);

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ActualFirstBoltAndEccentricBlockResistanceHaveIndependentValues(bool ntc)
    {
        var p = WeldedIBeamSpliceStandardTests.Input(ntc, WeldedIBeamSpliceStandardTests.Detail(new(2e5,0,0,0,0,0)));
        var r = WeldedIBeamSpliceStandardPlates.Verify(p,true);
        var c = r.DirectBlockTearing.Checks.Single(x=>x.Id=="TopOuter.Axis0.Direction1.Path2.Lmin");
        // Ntop=N*12000/29120; one face carries half, distributed to six bolts.
        double demand=2e5*12000/29120d/12;
        // First path: end60, transverse edge50, d0=26, t=20.
        double ant=(50-13)*20, anv=(60-13)*20;
        double resistance=.5*510*ant/1.25+355*anv/(Math.Sqrt(3)*(ntc?1.05:1));
        WeldedIBeamSpliceTests.Close(demand,c.Demand);
        WeldedIBeamSpliceTests.Close(resistance,c.Resistance);
        Assert.AreEqual(0,r.BlockHierarchy.Checks.Count);
        Assert.IsTrue(r.DirectBlockTearing.IsComplete);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void CollinearForceReversalKeepsCapacityAndDoesNotCancelTwoLapHalves(bool ntc)
    {
        var p=WeldedIBeamSpliceStandardTests.Input(ntc,WeldedIBeamSpliceStandardTests.Detail(new(2e5,0,0,0,0,0)));
        var n=WeldedIBeamSpliceStandardTests.Input(ntc,WeldedIBeamSpliceStandardTests.Detail(new(-2e5,0,0,0,0,0)));
        var a=WeldedIBeamSpliceStandardPlates.Verify(p,true).DirectBlockTearing;
        var b=WeldedIBeamSpliceStandardPlates.Verify(n,true).DirectBlockTearing;
        CollectionAssert.AreEqual(a.Checks.Select(c=>c.Demand).ToArray(),b.Checks.Select(c=>c.Demand).ToArray());
        double maximum=a.Checks.Where(c=>c.Id.StartsWith("TopOuter.")).Max(c=>c.Demand);
        WeldedIBeamSpliceTests.Close(2e5*12000/29120d,maximum);
        Assert.IsTrue(maximum>0); // Sum(Fx) over both halves would incorrectly vanish.
    }
    [TestMethod]
    public void MixedWebActionsRetainExplicitHierarchyButFlangesUseActualForces()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Direct(WeldedIBeamSpliceStandardTests.Workflow()));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        foreach(var c in r.Cases)
        {
            Assert.IsTrue(c.AdditionalPlates.BlockHierarchy.Checks.All(x=>x.Id.StartsWith("Web")));
            Assert.IsTrue(c.AdditionalPlates.DirectBlockTearing.Checks.All(x=>!x.Id.StartsWith("Web")));
        }
        Assert.IsTrue(r.CheckSources.Any(x=>x.Component=="DirectBlockTearing"));
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ProtectedThinFlangeCoversAreAdmittedByDemandWithoutChangingLegacyHierarchy(bool ntc)
    {
        var d=WeldedIBeamSpliceStandardTests.Detail();
        SpliceFlangeCoverDetail Thin(SpliceFlangeCoverDetail f)=>new(new(f.Outer.Width,14,355,510),new(f.EachInner.Width,14,355,510),f.InnerGap,f.EndDistance,f.Bolts);
        d=new(d.Section,Thin(d.Top),Thin(d.Bottom),d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
        var parents=WeldedIBeamSpliceStandardTests.Parents(WeldedIBeamSpliceDetailTests.Input(ntc,SpliceExposure.ProtectedCarbonSteel,d));
        var old=new WeldedIBeamSpliceStandardInput(new[]{new WeldedSpliceStandardCase("U",parents)},SpliceBoltCategory.A);
        var legacy=WeldedIBeamSpliceStandard.Verify(old);
        Assert.IsTrue(legacy.MissingRequirements.Any(x=>x.Contains("BlockHierarchy")));
        var direct=WeldedIBeamSpliceStandard.Verify(Direct(old));
        Assert.AreEqual(ConnectionStatus.Passed,direct.Status,string.Join(";",direct.MissingRequirements.Concat(direct.CheckSources.Where(x=>x.Failed).Select(x=>x.Component+":"+x.ComponentCheck?.Id))));
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void AxialThinWebCanUseActualForcesButMixedWebCannotBorrowThatQualification(bool ntc)
    {
        var d=BridgeSpliceScenarioTests.Detail("web-12");
        var a=new PlanarConnectionActions(2e5,0,0,0,0,0);
        d=new(d.Section,d.Top,d.Bottom,d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,a);
        var p=new WeldedIBeamSpliceStandardInput(new[]{new WeldedSpliceStandardCase("U",WeldedIBeamSpliceStandardTests.Parents(WeldedIBeamSpliceStandardTests.Input(ntc,d)))},SpliceBoltCategory.A);
        Assert.AreEqual(ConnectionStatus.Passed,WeldedIBeamSpliceStandard.Verify(Direct(p)).Status);
        var mixed=WeldedIBeamSpliceStandard.Verify(Direct(BridgeSpliceScenarioTests.Workflow(ntc,SpliceBoltCategory.B,"web-12")));
        Assert.AreEqual(ConnectionStatus.NotSupported,mixed.Status);
        Assert.IsTrue(mixed.MissingRequirements.Any(x=>x.Contains("BlockHierarchy")));
    }
    [TestMethod]
    public void OverloadIsAnActualFailedCheckAndUnknownMethodIsRejected()
    {
        var p=WeldedIBeamSpliceStandardTests.Workflow(category:SpliceBoltCategory.A,scale:100);
        var r=WeldedIBeamSpliceStandard.Verify(Direct(p));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsTrue(r.CheckSources.Any(x=>x.Component=="DirectBlockTearing"&&x.Failed));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new WeldedIBeamSpliceStandardInput(p.Cases,p.Envelope.Category,null,(SpliceBlockVerificationMethod)99));
    }
}

