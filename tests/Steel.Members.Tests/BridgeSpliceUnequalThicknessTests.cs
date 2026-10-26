using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class BridgeSpliceUnequalThicknessTests
{
    private static WeldedIBeamSpliceForceInput Detail(double outer=30,double inner=20,PlanarConnectionActions? actions=null)
    {
        var d=BridgeSpliceUnequalWidthTests.Detail(360,actions);
        SpliceFlangeCoverDetail Face(SpliceFlangeCoverDetail f)=>new(new(f.Outer.Width,outer,355,510),new(f.EachInner.Width,inner,355,510),f.InnerGap,f.EndDistance,f.Bolts);
        return new(d.Section,Face(d.Top),Face(d.Bottom),d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
    }
    private static WeldedIBeamSpliceStandardInput Workflow(bool ntc,SpliceBoltCategory category,double outer,double inner,double scale=1)
    {
        var d=Detail(outer,inner,new(2e5*scale,-1e5*scale,0,0,0,50e6*scale));
        var input=WeldedIBeamSpliceStandardTests.Input(ntc,d);
        var slip=new WeldedSpliceSlipCase("slip",WeldedIBeamSpliceSlipTests.Input(ntc,
            category==SpliceBoltCategory.B?BoltSlipLimitState.Serviceability:BoltSlipLimitState.Ultimate,
            category==SpliceBoltCategory.B?Detail(outer,inner,new(1e5*scale,-5e4*scale,0,0,0,25e6*scale)):d),category==SpliceBoltCategory.C?"U":null);
        return new(new[]{new WeldedSpliceStandardCase("U",WeldedIBeamSpliceStandardTests.Parents(input))},category,
            category==SpliceBoltCategory.A?null:new[]{slip},SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback,
            SpliceFlangeFaceModel.IndependentFaceDimensions);
    }
    public static IEnumerable<object[]> Cases()
    {
        foreach(bool ntc in new[]{false,true})foreach(var c in new[]{SpliceBoltCategory.A,SpliceBoltCategory.B,SpliceBoltCategory.C})
        foreach(var t in new[]{new[]{30d,20d},new[]{20d,25d},new[]{25d,30d}})yield return new object[]{ntc,c,t[0],t[1]};
    }
    [DataTestMethod,DynamicData(nameof(Cases),DynamicDataSourceType.Method)]
    public void ActualUnequalOffsetsAndAllIndividualInterfacesAreChecked(bool ntc,SpliceBoltCategory category,double outer,double inner)
    {
        var p=Workflow(ntc,category,outer,inner);var r=WeldedIBeamSpliceStandard.Verify(p);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        Assert.IsTrue(r.IsStaticResistanceComplete);Assert.AreEqual(8,r.Cases[0].Components.Plates.Covers.Count);
        Assert.AreEqual(category==SpliceBoltCategory.A?0:1,r.SlipCases.Count);
        StringAssert.Contains(BridgeSpliceReport.FromBolted(r).ToText(),"IndependentFaceDimensions");
    }
    [DataTestMethod] [DataRow(30d,20d)] [DataRow(20d,30d)] [DataRow(15d,40d)]
    public void IndependentFreeBodyWithActualArmsDoesNotUseHalfOrAreaSplit(double outer,double inner)
    {
        var d=Detail(outer,inner,new(200000,0,0,0,0,0));
        var r=WeldedIBeamSpliceStandardModel.Verify(WeldedIBeamSpliceStandardTests.Input(detail:d),SpliceFlangeFaceModel.IndependentFaceDimensions);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
        double n=200000*d.Section.TopFlange.Width*d.Section.TopFlange.Thickness/d.Section.Area;
        double a=(d.Section.TopFlange.Thickness+outer)/2,b=(d.Section.TopFlange.Thickness+inner)/2;
        double Value(string name)=>r.Trace.Single(x=>x.Name=="Top.FaceModel."+name).Value;
        WeldedIBeamSpliceTests.Close(n*b/(a+b),Value("OuterForce"));
        WeldedIBeamSpliceTests.Close(n*a/(a+b),Value("BothInnerForce"));
        WeldedIBeamSpliceTests.Close(0,a*Value("OuterForce")-b*Value("BothInnerForce"));
        Assert.AreNotEqual(n/2,Value("OuterForce"));
    }
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ReversalAndOverloadRemainVisible(bool ntc)
    {
        Assert.AreEqual(ConnectionStatus.Passed,WeldedIBeamSpliceStandard.Verify(Workflow(ntc,SpliceBoltCategory.C,30,20,-1)).Status);
        Assert.AreEqual(ConnectionStatus.Failed,WeldedIBeamSpliceStandard.Verify(Workflow(ntc,SpliceBoltCategory.C,30,20,50)).Status);
    }
    [TestMethod] public void OldPoliciesRetainTheirExactThicknessRestrictions()
    {
        var p=WeldedIBeamSpliceStandardTests.Input(detail:Detail());
        foreach(var mode in new[]{SpliceFlangeFaceModel.BalancedEqualFaces,SpliceFlangeFaceModel.EqualThicknessIndependentWidths})
            Assert.AreEqual(ConnectionStatus.NotSupported,WeldedIBeamSpliceStandardModel.Verify(p,mode).Status);
    }
    [TestMethod] public void EqualDimensionsKeepExistingResultsBitwise()
    {
        var p=BridgeSpliceUnequalWidthTests.Workflow(false,SpliceBoltCategory.C,360);
        var q=new WeldedIBeamSpliceStandardInput(p.Cases,p.Envelope.Category,p.Envelope.SlipCases,p.BlockMethod,SpliceFlangeFaceModel.IndependentFaceDimensions);
        var a=WeldedIBeamSpliceStandard.Verify(p);var b=WeldedIBeamSpliceStandard.Verify(q);
        CollectionAssert.AreEqual(a.CheckSources.Select(x=>x.Utilization).ToArray(),b.CheckSources.Select(x=>x.Utilization).ToArray());
    }
    [TestMethod] public void ThinSlenderFaceStillCannotQualifyByEquilibriumAlone()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(false,SpliceBoltCategory.A,2,20));
        Assert.IsFalse(r.IsStaticResistanceComplete);Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
    }
}
