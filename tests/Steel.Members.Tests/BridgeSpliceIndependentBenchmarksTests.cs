using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BridgeSpliceIndependentBenchmarksTests
{
    // SCI P357 example1 section12, printed pp58-59 (PDF66-67): one column of
    // 12 web bolts, pitch75, e55, polar modulus1950. Recomputed from unrounded
    // actions; printed resultants are rounded to 1kN, hence 0.6kN tolerance.
    [DataTestMethod]
    [DataRow(609d,834d,155d,169d)][DataRow(469d,650d,127d,134d)]
    [DataRow(-336d,1320d,-33d,120d)][DataRow(-259d,1011d,-11d,95d)]
    public void SciP357PublishedWebBoltResultants(double n,double v,double m,double resultant)
    {
        var points=Enumerable.Range(0,12).Select(i=>new ConnectionPoint2D(55,(i-5.5)*75)).ToArray();
        var r=ElasticBoltGroup.Calculate(points,new(n*1000,v*1000,0,0,0,-m*1e6));
        Assert.IsTrue(r.IsSupported);
        Assert.AreEqual(resultant*1000,r.MaximumShear!.Value,600);
        WeldedIBeamSpliceTests.Close(1950,points.Sum(p=>p.Y*p.Y)/points.Max(p=>p.Y));
        WeldedIBeamSpliceTests.Close(-m*1e6-55*v*1000,r.ActionsAtCentroid.Mz);
    }
    [DataTestMethod][DataRow(BoltSlipLimitState.Ultimate,158144d)][DataRow(BoltSlipLimitState.Serviceability,179709.0909090909)]
    public void SciP357PublishedM24DoubleInterfaceSlipResistance(BoltSlipLimitState state,double expected)
    {
        var code=ConnectionDesignCode.Eurocode3(1,1.1,1.25,"SCI P357 worked example UK factors, NOT NTC");
        var b=new ConnectionBolt(24,26,353,ConnectionBoltGrade.Class8_8,true);
        var r=BoltSlip.Verify(new(BoltSlipPolicy.Eurocode3(code,1.25,1.1),b,state,1e5,0,2,.5,
            BoltTighteningControl.Controlled,"P357 section12.2","Class A surface P357","Published limit-state example"));
        WeldedIBeamSpliceTests.Close(expected,r.Checks.Single(c=>c.Id=="Slip."+state).Resistance);
    }
    [TestMethod]
    public void SciP357PublishedDoubleShearBoltAndFlangeNetPlasticResistance()
    {
        var code=ConnectionDesignCode.Eurocode3(1,1.1,1.25,"P357 published example");
        var bolt=new ConnectionBolt(24,26,353,ConnectionBoltGrade.Class8_8,true);
        var r=BoltStrength.Verify(new(code,bolt,1e5,0,2,0,0));
        WeldedIBeamSpliceTests.Close(271104,r.Checks.Single(c=>c.Id=="Bolt.Shear").Resistance);
        // Net plastic category C check through the actual cover API, dimensions195x10 with two26 holes.
        // The whole P357 joint has packing, different sections and one-column web groups:
        // those are NOT claimed to be qualified by the standard whole-joint API.
        var grid=new SpliceBoltLayout(3,4,65,100,55);
        var parent=new WeldedIBeamSpliceSection(new(500,40,355,470),new(1000,14,355,470),new(600,60,345,470),8,8);
        var top=new SpliceFlangeCoverDetail(new(490,10,355,470),new(195,10,355,470),30,50,grid);
        var bottom=new SpliceFlangeCoverDetail(new(490,20,345,470),new(195,20,345,470),30,50,new(5,4,65,100,55));
        var d=new WeldedIBeamSpliceForceInput(parent,top,bottom,new(925,10,355,470),new(2,12,65,75,55),50,10,new(1e5,0,0,0,0,0));
        var bi=new WeldedIBeamSpliceBoltInput(code,d,bolt,bolt,40,40,"P357 component dimensions, regular transverse grid and two-column web adaptation");
        var net=WeldedIBeamSpliceSlip.VerifyNetAxialPlastic(new(IBeamSplicePlatePolicy.Eurocode3(code,1.1),bi));
        Assert.IsTrue(net.IsComplete,string.Join(";",net.Diagnostics));
        var cover=net.Checks.Single(c=>c.Id=="TopInnerPositive.NetAxialPlastic");
        WeldedIBeamSpliceTests.Close(507650,cover.Resistance);
        var plates=WeldedIBeamSplicePlates.Verify(new(IBeamSplicePlatePolicy.Eurocode3(code,1.1),bi));
        WeldedIBeamSpliceTests.Close(1345500,plates.Covers.Single(c=>c.Cover==WeldedSpliceCover.BottomInnerPositive)
            .Sections.Checks.First(c=>c.Id.EndsWith("GrossCompression")).Resistance);
    }
    [DataTestMethod][DataRow(false,false)][DataRow(true,false)][DataRow(false,true)][DataRow(true,true)]
    public void CompleteExamplesAgreeWithTheIndependentFractionOracle(bool ntc,bool unequal)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","standard-bridge-splice-independent.json")));
        var expected=doc.RootElement.GetProperty("examples").EnumerateArray().Single(e=>e.GetProperty("unequal").GetBoolean()==unequal);
        var r=WeldedIBeamSpliceStandard.Verify(WeldedIBeamSpliceStandardTests.Workflow(ntc,SpliceBoltCategory.C,unequal:unequal));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        var first=r.Cases.Single(c=>c.Input.Id=="ULS+");
        foreach(var item in expected.GetProperty("covers").EnumerateObject())
        {
            var e=item.Value;var net=first.AdditionalPlates.NetSections;
            Close(e,"Anet",net.Trace.Single(t=>t.Name==item.Name+".Anet").Value);
            Close(e,"Inet",net.Trace.Single(t=>t.Name==item.Name+".Inet").Value);
            Close(e,"NetEquivalent",net.Checks.Where(c=>c.Id.StartsWith(item.Name+".")).Max(c=>c.Demand));
            Close(e,"MaxBoltShear",first.Components.Bolts.Forces.CoverGroups.Where(g=>g.Cover.ToString()==item.Name).SelectMany(g=>g.Distribution.Forces).Max(f=>f.Shear));
        }
        Close(expected,"parentNetEquivalent",first.AdditionalPlates.NetSections.Checks.Single(c=>c.Id=="Parent.NetElasticNVM").Demand);
        Close(expected,"parentNetArea",first.AdditionalPlates.NetSections.Trace.Single(t=>t.Name=="Parent.Anet").Value);
        Close(expected,"parentNetInertia",first.AdditionalPlates.NetSections.Trace.Single(t=>t.Name=="Parent.Inet").Value);
        Close(expected,"topShearFlow",first.Parents.LongitudinalWelds.Trace.Single(t=>t.Name=="Top.ShearFlow").Value);
        Close(expected,"bottomShearFlow",first.Parents.LongitudinalWelds.Trace.Single(t=>t.Name=="Bottom.ShearFlow").Value);
        // Physical demands are the same under EC/NTC; factors enter the resistances only.
        double limit=Math.Min(355/(ntc?1.05:1),.9*510/1.25);
        WeldedIBeamSpliceTests.Close(limit,first.AdditionalPlates.NetSections.Checks[0].Resistance);
    }
    static void Close(JsonElement e,string name,double actual)=>WeldedIBeamSpliceTests.Close(e.GetProperty(name).GetDouble(),actual,1e-10);
}
