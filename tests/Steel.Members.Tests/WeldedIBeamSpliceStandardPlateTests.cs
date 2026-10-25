using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;


public partial class WeldedIBeamSpliceStandardTests
{
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void CompactStandardCoversHaveNetSectionsAndASeparateBlockHierarchy(bool ntc)
    {
        var r=WeldedIBeamSpliceStandardPlates.Verify(Input(ntc));
        Assert.AreEqual(ConnectionStatus.Passed,r.NetSections.Status,string.Join(";",r.NetSections.Diagnostics));
        Assert.AreEqual(ConnectionStatus.Passed,r.CompactPlateDomain.Status);
        Assert.AreEqual(ConnectionStatus.Passed,r.BlockHierarchy.Status,
            string.Join(";",r.BlockHierarchy.Checks.Where(c=>!c.Passed).Take(8).Select(c=>c.Id+"="+c.Utilization)));
        Assert.IsTrue(r.IsQualified);
    }
    [TestMethod]
    public void HolePositionsReduceSecondMomentNotJustArea()
    {
        var r=WeldedIBeamSpliceStandardPlates.Verify(Input());
        // Outer flange width280, t20, two26mm holes centred at +/-90.
        double a=(280-2*26)*20;
        double i=20*Math.Pow(280,3)/12-2*20*(Math.Pow(26,3)/12+26*90*90);
        WeldedIBeamSpliceTests.Close(a,r.NetSections.Trace.Single(t=>t.Name=="TopOuter.Anet").Value);
        WeldedIBeamSpliceTests.Close(i,r.NetSections.Trace.Single(t=>t.Name=="TopOuter.Inet").Value);
        Assert.IsTrue(i<20*Math.Pow(280,3)/12*a/(280*20));
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void NetSectionsUseSelectedFactorsWithoutImportingTheSciUkAnnex(bool ntc)
    {
        var r=WeldedIBeamSpliceStandardPlates.Verify(Input(ntc));
        double limit=Math.Min(355/(ntc?1.05:1),.9*510/1.25);
        foreach(var c in r.NetSections.Checks)WeldedIBeamSpliceTests.Close(limit,c.Resistance);
    }
    [TestMethod]
    public void BlockHierarchyIsActionIndependentAndIncludesBothDirectionsAndAxes()
    {
        var a=WeldedIBeamSpliceStandardPlates.Verify(Input());
        var b=WeldedIBeamSpliceStandardPlates.Verify(Input(detail:Detail(new(-4e5,2e5,0,0,0,-80e6))));
        CollectionAssert.AreEqual(a.BlockHierarchy.Checks.Select(c=>c.Demand).ToArray(),b.BlockHierarchy.Checks.Select(c=>c.Demand).ToArray());
        CollectionAssert.AreEqual(a.BlockHierarchy.Checks.Select(c=>c.Resistance).ToArray(),b.BlockHierarchy.Checks.Select(c=>c.Resistance).ToArray());
        foreach(var direction in new[]{"Axis0.Direction-1","Axis0.Direction1","Axis1.Direction-1","Axis1.Direction1"})
            Assert.IsTrue(a.BlockHierarchy.Checks.Any(c=>c.Id.Contains(direction)));
        // Single hole U shear-out: two47mm ligaments, t20, no tensile contribution.
        var path=a.BlockHierarchy.Checks.Single(c=>c.Id=="TopOuter.Axis0.Direction1.Path1.U");
        WeldedIBeamSpliceTests.Close(2*47*20*355/Math.Sqrt(3),path.Resistance);
        WeldedIBeamSpliceTests.Close(.6*800*353/1.25,path.Demand);
    }
    [TestMethod]
    public void AThinWebCanPassTheActualLoadYetFailTheSufficientHierarchy()
    {
        var d=Detail(new(100,50,0,0,0,1000));
        d=new(d.Section,d.Top,d.Bottom,new(280,8,355,510),d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
        var r=WeldedIBeamSpliceStandardPlates.Verify(Input(detail:d));
        Assert.AreEqual(ConnectionStatus.Passed,r.NetSections.Status);
        Assert.AreEqual(ConnectionStatus.Failed,r.BlockHierarchy.Status);
        Assert.IsFalse(r.IsQualified);
    }
    [TestMethod]
    public void FullParentNetAreaIncludesAllThreePerforatedPlates()
    {
        var r=WeldedIBeamSpliceStandardPlates.Verify(Input());
        double expected=2*(400-2*26)*30+(320-3*26)*16;
        WeldedIBeamSpliceTests.Close(expected,r.NetSections.Trace.Single(t=>t.Name=="Parent.Anet").Value);
        WeldedIBeamSpliceTests.Close(0,r.NetSections.Trace.Single(t=>t.Name=="Parent.NetCentroidOffsetY").Value);
    }
    [DataTestMethod][DataRow(1d)][DataRow(-1d)]
    public void OverloadIsAnActualNetSectionFailureAndIsNotHiddenBySign(double sign)
    {
        var r=WeldedIBeamSpliceStandardPlates.Verify(Input(detail:Detail(new(sign*3e7,sign*2e6,0,0,0,sign*2e9))));
        Assert.AreEqual(ConnectionStatus.Failed,r.NetSections.Status);Assert.IsTrue(r.NetSections.Checks.Any(c=>!c.Passed));
    }
}
