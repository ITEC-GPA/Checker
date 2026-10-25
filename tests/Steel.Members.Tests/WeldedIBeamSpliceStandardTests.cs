using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public partial class WeldedIBeamSpliceStandardTests
{
    internal static WeldedIBeamSpliceForceInput Detail(PlanarConnectionActions? actions=null, bool unequalFlanges=false)
    {
        var d=WeldedIBeamSplicePlateTests.Detail(actions??new(2e5,-1e5,0,0,0,50e6));
        SpliceFlangeCoverDetail Balanced(SpliceFlangeCoverDetail f)=>new(new(2*f.EachInner.Width,f.EachInner.Thickness,f.EachInner.Fy,f.EachInner.Fu),f.EachInner,f.InnerGap,f.EndDistance,f.Bolts);
        var s=unequalFlanges?new WeldedIBeamSpliceSection(d.Section.TopFlange,d.Section.Web,new(420,35,355,510),8,8):d.Section;
        return new(s,Balanced(d.Top),Balanced(d.Bottom),new(d.EachWebCover.Width,25,355,510),d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
    }
    internal static WeldedIBeamSpliceDetailInput Input(bool ntc=false,WeldedIBeamSpliceForceInput? detail=null)
        =>WeldedIBeamSpliceDetailTests.Input(ntc,SpliceExposure.ExposedCarbonSteel,detail??Detail());
    [DataTestMethod][DataRow(false,false)][DataRow(false,true)][DataRow(true,false)][DataRow(true,true)]
    public void StandardBalancedDoubleCoversAreApplicableIncludingUnequalParentFlanges(bool ntc,bool unequal)
    {
        var r=WeldedIBeamSpliceStandardModel.Verify(Input(ntc,Detail(unequalFlanges:unequal)));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
    }
    [DataTestMethod][DataRow("thickness")][DataRow("area")][DataRow("material")][DataRow("offset")][DataRow("outofplane")]
    public void EquilibriumAloneDoesNotAdmitGeneralDoubleCoverDetails(string flaw)
    {
        var d=Detail();var f=d.Top;
        var top=new SpliceFlangeCoverDetail(new(flaw=="area"?300:f.Outer.Width,flaw=="thickness"?21:f.Outer.Thickness,
            flaw=="material"?275:f.Outer.Fy,510),f.EachInner,flaw=="offset"?42:f.InnerGap,f.EndDistance,f.Bolts);
        d=new(d.Section,top,d.Bottom,d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,
            flaw=="outofplane"?new(1e5,0,1,0,0,0):d.Actions);
        var r=WeldedIBeamSpliceStandardModel.Verify(Input(detail:d));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsTrue(r.Diagnostics.Count>0);
    }
    [TestMethod]
    public void BalancedFacesDoNotMeanAForcedHalfSplitUnderParentFlangeSelfBending()
    {
        var d=Detail();var f=WeldedIBeamSpliceForces.Calculate(d);
        double outer=f.Trace.Single(t=>t.Name=="Left.Top.OuterForce").Value;
        double inner=f.Trace.Single(t=>t.Name=="Left.Top.BothInnerForce").Value;
        double m=f.Trace.Single(t=>t.Name=="Left.Top.ParentLocalMoment").Value;
        Assert.AreNotEqual(outer,inner);
        WeldedIBeamSpliceTests.Close(m,25*(inner-outer));
    }
}
