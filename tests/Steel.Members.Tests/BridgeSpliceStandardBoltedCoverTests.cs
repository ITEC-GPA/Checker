using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class BridgeSpliceStandardBoltedCoverTests
{
    internal static WeldedIBeamSpliceDetailInput Input(bool ntc=false,double sign=1,int columns=1,int rows=12,
        double unusedPitch=75,double first=55,double end=50,double t=10,bool smallBolt=true,PlanarConnectionActions? actions=null)
    {
        var d=BridgeSpliceCompressionInteractionTests.Detail(actions??new(-2e5*sign,2e4*sign,0,0,0,1e8*sign),first,t);
        // The published benchmark concerns the web. The complete workflow fixture
        // uses a separate compliant flange detail: p2=200, centred 140mm inner strips.
        var f=new SpliceFlangeCoverDetail(d.Top.Outer,new(140,25,355,510),60,50,new(3,2,80,200,55));
        d=new(d.Section,f,f,d.EachWebCover,new(columns,rows,unusedPitch,75,first),end,10,d.Actions);
        var old=WeldedIBeamSpliceStandardTests.Input(ntc,d);var b=old.Plates.Bolts;
        var web=smallBolt?new ConnectionBolt(16,18,157,ConnectionBoltGrade.Class8_8,true):b.WebBolt;
        return new(new(old.Plates.Policy,new(b.Code,d,b.FlangeBolt,web,b.FlangeHardwareDiameter,smallBolt?30:b.WebHardwareDiameter,
            b.UltimateCombinationSource,b.HoleClearanceServiceabilitySource)),SpliceExposure.ProtectedCarbonSteel);
    }
    internal static WeldedIBeamSpliceSlipInput Slip(WeldedIBeamSpliceDetailInput p,SpliceBoltCategory category)
    {
        var c=p.Plates.Policy.Code;
        return new(c.IsNtc?BoltSlipPolicy.Ntc2018(c):BoltSlipPolicy.Eurocode3(c,1.25,1.1),p.Plates.Bolts,
            category==SpliceBoltCategory.B?BoltSlipLimitState.Serviceability:BoltSlipLimitState.Ultimate,
            Enum.GetValues<WeldedSpliceCover>().Select(x=>new WeldedSpliceFrictionSurface(x,.5,"Qualified classA surface")),
            BoltTighteningControl.Controlled,"Preloaded assembly","Concomitant case");
    }
    internal static WeldedIBeamSpliceStandardInput Workflow(WeldedIBeamSpliceDetailInput p,SpliceBoltCategory cat=SpliceBoltCategory.A,
        SplicePlateStabilityMethod mode=SplicePlateStabilityMethod.StandardBoltedCovers)
        =>new(new[]{new WeldedSpliceStandardCase("U",WeldedIBeamSpliceStandardTests.Parents(p))},cat,
            cat==SpliceBoltCategory.A?null:new[]{new WeldedSpliceSlipCase("S",Slip(p,cat),cat==SpliceBoltCategory.C?"U":null)},
            SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback,SpliceFlangeFaceModel.EqualThicknessIndependentWidths,mode);
    [DataTestMethod]
    [DataRow(false,SpliceBoltCategory.A)][DataRow(false,SpliceBoltCategory.B)][DataRow(false,SpliceBoltCategory.C)]
    [DataRow(true,SpliceBoltCategory.A)][DataRow(true,SpliceBoltCategory.B)][DataRow(true,SpliceBoltCategory.C)]
    public void RegularThinWebCoverPassesTheFullStandardWorkflow(bool ntc,SpliceBoltCategory category)
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(Input(ntc),category));
        Assert.IsTrue(r.IsStaticResistanceComplete,string.Join(";",r.MissingRequirements));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.CheckSources.Where(c=>c.Failed).Select(c=>c.ComponentCheck?.Id)));
        Assert.IsTrue(r.Cases[0].AdditionalPlates.CompactPlateDomain.Checks.All(c=>c.Passed));
    }
    [TestMethod] public void PreviousDefaultRetainsItsWholeWidthScreen()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(Input(),mode:SplicePlateStabilityMethod.CompactCovers));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.MissingRequirements.Any(x=>x.Contains("CompactPlateDomain")));
        Assert.IsFalse(r.Cases[0].AdditionalPlates.CompactPlateDomain.Checks.Single(x=>x.Id=="WebPositive.NoShearBucklingDomain").Passed);
    }
    [DataTestMethod][DataRow(.001)][DataRow(75d)][DataRow(100000d)]
    public void AbsentColumnPitchHasNoPhysicalEffect(double pitch)
    {
        var a=WeldedIBeamSpliceStandard.Verify(Workflow(Input()));
        var b=WeldedIBeamSpliceStandard.Verify(Workflow(Input(unusedPitch:pitch)));
        Assert.AreEqual(ConnectionStatus.Passed,b.Status,string.Join(";",b.MissingRequirements));
        CollectionAssert.AreEqual(a.CheckSources.Select(x=>x.Utilization).ToArray(),b.CheckSources.Select(x=>x.Utilization).ToArray());
        Assert.IsFalse(b.Cases[0].Components.Detailing.Detailing.Checks.Any(c=>c.Id=="WebPositive.LongitudinalPitch"));
        Near(0,BridgeSpliceReport.FromBolted(b).Cases[0].InputValues.Single(x=>x.Name=="Web.LongitudinalPitch").Value);
    }
    [TestMethod] public void SamePhysicalSingleColumnBindsAcrossDifferentUnusedPitchValues()
    {
        var p=Workflow(Input(),SpliceBoltCategory.C);
        var q=new WeldedIBeamSpliceStandardInput(p.Cases,p.Envelope.Category,
            new[]{new WeldedSpliceSlipCase("S",Slip(Input(unusedPitch:100000),SpliceBoltCategory.C),"U")},p.BlockMethod,p.FaceModel,p.PlateStability);
        Assert.AreEqual(ConnectionStatus.Passed,WeldedIBeamSpliceStandard.Verify(q).Status);
    }
    [TestMethod] public void P357WebForcesAndCompressionMatchIndependentPublishedValues()
    {
        // SCI P357 example1 section12.6-12.7 pp58-61: twelve bolts, p=75,
        // 925x10 web cover, Ncomp305kN, V417kN, max M101kNm.
        // Build the corresponding planar member actions independently by rectangle integrals.
        var s=BridgeSpliceCompressionInteractionTests.Detail().Section;
        double iw=35*Math.Pow(1000,3)/12;
        var p=Input(smallBolt:false,actions:new(-610000*s.Area/(35000),834000,0,0,0,
            2*(101e6-417000*55)*s.StrongAxisSecondMoment/iw));
        var r=WeldedIBeamSplicePlates.Verify(p.Plates);Assert.AreEqual(0,r.Diagnostics.Count,string.Join(";",r.Diagnostics));
        var c=r.Covers.Single(c=>c.Cover==WeldedSpliceCover.WebPositive);
        Near(305000,c.Cuts.Max(x=>-x.N));Near(417000,c.Cuts.Max(x=>Math.Abs(x.V)));Near(101e6,c.Cuts.Max(x=>Math.Abs(x.M)));
        Near(305000d/9250+101e6/(10*925*925/6d),c.LongitudinalCompression.Trace.Single(x=>x.Name=="MaximumCompression").Value);
        Assert.AreEqual(104d,c.LongitudinalCompression.Trace.Single(x=>x.Name=="MaximumCompression").Value,.5); // published MPa rounded
        var f=WeldedIBeamSpliceForces.Calculate(p.Plates.Bolts.Detail);Assert.IsTrue(f.IsBalanced);
        var group=f.CoverGroups.Single(g=>g.Cover==WeldedSpliceCover.WebPositive&&g.Side==SpliceSide.Left);
        double j=Enumerable.Range(0,12).Sum(k=>Math.Pow((k-5.5)*75,2));Near(1950,j/412.5);
        foreach(var force in group.Distribution.Forces)
        { Near(417000d/12,force.Fy);Near(-305000d/12-101e6*force.Position.Y/j,force.Fx); }
        // P357 uses curve a for this example; this implementation deliberately retains
        // the more conservative solid-strip curve c prescribed in P398 p57.
        Assert.IsTrue(c.LongitudinalCompression.Trace.Single(x=>x.Name=="Chi").Value<.98);
    }
    [DataTestMethod][DataRow("outstand")][DataRow("end")][DataRow("pitch")][DataRow("singlebolt")]
    public void MissingPhysicalRestraintOrSlenderLocalStripsCannotPass(string flaw)
    {
        var p=Input(rows:flaw=="outstand"?8:flaw=="singlebolt"?1:12,first:flaw=="pitch"?100:55,end:flaw=="end"?150:50);
        if(flaw=="singlebolt")
        { Assert.IsFalse(WeldedIBeamSpliceForces.Calculate(p.Plates.Bolts.Detail).IsBalanced);return; }
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(p));Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.Cases[0].AdditionalPlates.CompactPlateDomain.Checks.Any(x=>!x.Passed));
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ReversalAndOverloadStayExplicit(bool ntc)
    {
        Assert.AreEqual(ConnectionStatus.Passed,WeldedIBeamSpliceStandard.Verify(Workflow(Input(ntc,-1))).Status);
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(Input(ntc,100)));
        Assert.IsTrue(r.HasFailedComponent);Assert.AreEqual(ConnectionStatus.Failed,r.Status);
    }
    [TestMethod] public void ModelBuilderAndReportKeepTheSelectedMethod()
    {
        var p=Input();var s=WeldedIBeamSpliceStandardTests.Section(p).Plan.Input.Section;
        var built=BridgeSpliceApplication.CreateBolted(p,s,s,new[]{new SpliceActionCase("U",p.Plates.Bolts.Detail.Actions,"FEM")},
            WeldedIBeamSpliceStandardTests.Welds(),"Global model",SpliceBoltCategory.A,SplicePlateStabilityMethod.StandardBoltedCovers,
            BridgeSpliceApplicationTests.Prepare,blockMethod:SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback,
            faceModel:SpliceFlangeFaceModel.EqualThicknessIndependentWidths);
        Assert.AreEqual(SplicePlateStabilityMethod.StandardBoltedCovers,built.PlateStability);
        var r=BridgeSpliceReport.FromBolted(WeldedIBeamSpliceStandard.Verify(built));Assert.IsTrue(r.IsComplete);
        StringAssert.Contains(r.Scope,"StandardBoltedCovers");
        Assert.IsTrue(r.Rows.Any(row=>row.Id.Contains("StandardBoltedCover.")&&row.Role==SpliceReportRole.Applicability));
    }
    // Full mixed-direction hierarchy gives t_min=15.484mm here; use16mm.
    [TestMethod] public void MultipleActualColumnsRemainSupported()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(Input(columns:2,t:16)));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        Near(110,r.Cases[0].AdditionalPlates.CompactPlateDomain.Checks.Single(c=>c.Id=="WebPositive.StandardBoltedCover.MaximumLongitudinalGap").Demand);
    }
    [DataTestMethod][DataRow(70d,true)][DataRow(70.00001,false)]
    public void ActualCentralPitchUsesInclusivePhysicalLimit(double first,bool passes)
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(Input(first:first)));
        var check=r.Cases[0].AdditionalPlates.CompactPlateDomain.Checks.Single(c=>c.Id=="WebPositive.StandardBoltedCover.MaximumLongitudinalGap");
        Near(140,check.Resistance);Near(2*first,check.Demand);Assert.AreEqual(passes,check.Passed);
        Assert.AreEqual(passes?ConnectionStatus.Passed:ConnectionStatus.NotSupported,r.Status);
    }
    [TestMethod] public void ThinCoverWithLargeBoltsDoesNotWaiveTheBlockHierarchy()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(Input(smallBolt:false)));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.MissingRequirements.Any(x=>x.Contains("BlockHierarchy")));
        Assert.IsTrue(r.Cases[0].AdditionalPlates.CompactPlateDomain.Checks.All(x=>x.Passed));
        Assert.IsFalse(r.HasFailedComponent);
        Assert.IsFalse(r.CheckSources.Any(x=>x.Component=="BlockHierarchy"));
    }
    [TestMethod] public void EffectiveModeKeepsCoupledInteractionForUnreducedCovers()
    {
        var p=Input();var original=WeldedIBeamSplicePlates.Verify(p.Plates);
        var effective=WeldedIBeamSpliceEffectiveCompression.Verify(p);
        int found=0;
        foreach(var c in original.Covers) foreach(var check in c.LongitudinalCompression.Checks.Where(x=>x.Id.EndsWith(".CompressionShearBending")))
        {
            var same=effective.Checks.Single(x=>x.Id==c.Cover+"."+check.Id);
            Near(check.Demand,same.Demand);Near(check.Resistance,same.Resistance);found++;
        }
        Assert.IsTrue(found>0);
    }
    [TestMethod] public void InvalidMethodCannotChangeTheDomainSilently()
        =>Assert.ThrowsException<ArgumentOutOfRangeException>(()=>WeldedIBeamSpliceStandardPlates.Verify(Input(),true,
            SpliceFlangeFaceModel.EqualThicknessIndependentWidths,(SplicePlateStabilityMethod)999));
    static void Near(double a,double b)=>Assert.AreEqual(a,b,Math.Max(1e-7,Math.Abs(a)*1e-10));
}
