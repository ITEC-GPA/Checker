using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;

[TestClass]
public class BridgeSpliceUnequalWidthTests
{
    internal static WeldedIBeamSpliceForceInput Detail(double width, PlanarConnectionActions? actions=null)
    {
        var d=WeldedIBeamSpliceStandardTests.Detail(actions);
        SpliceFlangeCoverDetail Change(SpliceFlangeCoverDetail f)=>new(new(width,20,355,510),f.EachInner,f.InnerGap,f.EndDistance,f.Bolts);
        return new(d.Section,Change(d.Top),Change(d.Bottom),d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
    }
    internal static WeldedIBeamSpliceStandardInput Workflow(bool ntc,SpliceBoltCategory category,double width,double scale=1)
    {
        var d=Detail(width,new(2e5*scale,-1e5*scale,0,0,0,50e6*scale));
        var input=WeldedIBeamSpliceStandardTests.Input(ntc,d);
        var slip=new WeldedSpliceSlipCase("slip",WeldedIBeamSpliceSlipTests.Input(ntc,
            category==SpliceBoltCategory.B?BoltSlipLimitState.Serviceability:BoltSlipLimitState.Ultimate,
            category==SpliceBoltCategory.B?Detail(width,new(1e5*scale,-5e4*scale,0,0,0,25e6*scale)):d),category==SpliceBoltCategory.C?"U":null);
        return new(new[]{new WeldedSpliceStandardCase("U",WeldedIBeamSpliceStandardTests.Parents(input))},category,
            category==SpliceBoltCategory.A?null:new[]{slip},SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback,
            SpliceFlangeFaceModel.EqualThicknessIndependentWidths);
    }
    public static IEnumerable<object[]> Cases()
    {
        foreach(bool ntc in new[]{false,true})foreach(var category in new[]{SpliceBoltCategory.B,SpliceBoltCategory.C})
        foreach(double width in new[]{260d,300d,360d,400d})yield return new object[]{ntc,category,width};
    }
    [DataTestMethod,DynamicData(nameof(Cases),DynamicDataSourceType.Method)]
    public void IndependentOuterWidthsAreQualifiedWithAllActualPlateAndInterfaceChecks(bool ntc,SpliceBoltCategory category,double width)
    {
        var p=Workflow(ntc,category,width);var r=WeldedIBeamSpliceStandard.Verify(p);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        Assert.IsTrue(r.CheckSources.Any(x=>x.Component=="DirectBlockTearing"));
        Assert.IsTrue(r.SlipCases.Count>0);Assert.AreEqual(8,r.Cases[0].Components.Plates.Covers.Count);
        var legacy=new WeldedIBeamSpliceStandardInput(p.Cases,category,p.Envelope.SlipCases,p.BlockMethod);
        Assert.AreEqual(ConnectionStatus.NotSupported,WeldedIBeamSpliceStandard.Verify(legacy).Status);
        StringAssert.Contains(BridgeSpliceReport.FromBolted(r).ToText(),"EqualThicknessIndependentWidths");
    }
    [TestMethod]
    public void StiffnessSolutionMatchesIndependentRationalFreeBodiesAndStrainInterpolation()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","splice-unequal-widths-independent.json")));
        foreach(var row in json.RootElement.GetProperty("rows").EnumerateArray())
        {
            double w=row.GetProperty("width").GetDouble(),sign=row.GetProperty("sign").GetDouble(),m=row.GetProperty("moment").GetDouble();
            var d=Detail(w,new(sign*2e5,0,0,0,0,sign*m));
            var r=WeldedIBeamSpliceStandardModel.Verify(WeldedIBeamSpliceStandardTests.Input(detail:d),SpliceFlangeFaceModel.EqualThicknessIndependentWidths);
            Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
            string prefix=(row.GetProperty("top").GetBoolean()?"Top":"Bottom")+".FaceModel.";
            foreach(var value in row.GetProperty("values").EnumerateObject())
                WeldedIBeamSpliceTests.Close(value.Value.GetDouble(),r.Trace.Single(x=>x.Name==prefix+value.Name).Value,1e-10);
        }
    }
    [TestMethod]
    public void PureAxialLoadDoesNotUseAnAreaProportionalForceSplit()
    {
        var d=Detail(360,new(2e5,0,0,0,0,0));
        var r=WeldedIBeamSpliceStandardModel.Verify(WeldedIBeamSpliceStandardTests.Input(detail:d),SpliceFlangeFaceModel.EqualThicknessIndependentWidths);
        double Value(string name)=>r.Trace.Single(x=>x.Name=="Top.FaceModel."+name).Value;
        WeldedIBeamSpliceTests.Close(Value("OuterForce"),Value("BothInnerForce"));
        Assert.AreNotEqual(0,Value("FaceStrainGradient")); // Different A -> different strains, even at equal force.
        Assert.AreNotEqual(2e5*12000/29120d*7200/12800,Value("OuterForce"));
    }
    [TestMethod]
    public void WiderCoverChangesNetCapacityButNotTheStaticallyDeterminedFaceForce()
    {
        var narrow=WeldedIBeamSpliceStandard.Verify(Workflow(false,SpliceBoltCategory.A,260));
        var wide=WeldedIBeamSpliceStandard.Verify(Workflow(false,SpliceBoltCategory.A,360));
        double Force(WeldedIBeamSpliceStandardResult r)=>r.Cases[0].Model.Trace.Single(x=>x.Name=="Top.FaceModel.OuterForce").Value;
        WeldedIBeamSpliceTests.Close(Force(narrow),Force(wide));
        var a=narrow.Cases[0].AdditionalPlates.NetSections.Trace.Single(t=>t.Name=="TopOuter.Anet").Value;
        var b=wide.Cases[0].AdditionalPlates.NetSections.Trace.Single(t=>t.Name=="TopOuter.Anet").Value;
        Assert.AreEqual((360-260)*20,b-a);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ReverseAndOverloadRemainSignedAndFailedAsAppropriate(bool ntc)
    {
        Assert.AreEqual(ConnectionStatus.Passed,WeldedIBeamSpliceStandard.Verify(Workflow(ntc,SpliceBoltCategory.C,360,-1)).Status);
        Assert.AreEqual(ConnectionStatus.Failed,WeldedIBeamSpliceStandard.Verify(Workflow(ntc,SpliceBoltCategory.C,360,50)).Status);
    }
    [DataTestMethod][DataRow("thickness")][DataRow("material")][DataRow("centering")][DataRow("edge")]
    public void WidthExtensionDoesNotAdmitOtherUnqualifiedMechanisms(string flaw)
    {
        var d=Detail(360);var f=d.Top;
        var top=new SpliceFlangeCoverDetail(new(flaw=="edge"?200:360,flaw=="thickness"?21:20,flaw=="material"?275:355,510),
            f.EachInner,flaw=="centering"?44:f.InnerGap,f.EndDistance,f.Bolts);
        d=new(d.Section,top,d.Bottom,d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
        var r=WeldedIBeamSpliceStandardModel.Verify(WeldedIBeamSpliceStandardTests.Input(detail:d),SpliceFlangeFaceModel.EqualThicknessIndependentWidths);
        if(flaw=="edge")
        {
            // Geometry is inside the cover but minimum bolt edge still belongs to the bolt check.
            var bolts=WeldedIBeamSpliceBolts.Verify(WeldedIBeamSpliceStandardTests.Input(detail:d).Plates.Bolts);
            Assert.AreEqual(ConnectionStatus.NotSupported,bolts.ComponentAssessment.Status);
        }
        else Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
    }
    [TestMethod]
    public void EqualWidthModelRemainsBitwiseIdenticalForExistingStandardChecks()
    {
        var a=WeldedIBeamSpliceStandardTests.Workflow();
        var b=new WeldedIBeamSpliceStandardInput(a.Cases,a.Envelope.Category,a.Envelope.SlipCases,a.BlockMethod,SpliceFlangeFaceModel.EqualThicknessIndependentWidths);
        var x=WeldedIBeamSpliceStandard.Verify(a);var y=WeldedIBeamSpliceStandard.Verify(b);
        CollectionAssert.AreEqual(x.CheckSources.Select(c=>c.Utilization).ToArray(),y.CheckSources.Select(c=>c.Utilization).ToArray());
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new WeldedIBeamSpliceStandardInput(a.Cases,a.Envelope.Category,a.Envelope.SlipCases,a.BlockMethod,(SpliceFlangeFaceModel)99));
    }
}

