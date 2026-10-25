using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Materials;

namespace Steel.Members.Tests;

[TestClass]
public class WeldedIBeamSpliceTests
{
    internal static WeldedIBeamSpliceForceInput Input(PlanarConnectionActions? actions = null,
        SpliceFlangeCoverDetail? top = null, SpliceFlangeCoverDetail? bottom = null,
        ConnectionPlate? webCover = null, SpliceBoltLayout? web = null, double gap = 10)
        => new(new(new(400,24,355,510),new(800,16,355,510),new(500,30,355,510),10,12),
            top ?? new(new(360,16,355,510),new(160,18,355,510),50,60,new(3,2,90,180,80)),
            bottom ?? new(new(460,20,355,510),new(200,16,355,510),50,60,new(4,2,95,200,80)),
            webCover ?? new(720,12,355,510),web ?? new(3,5,100,140,80),60,gap,
            actions ?? new(240000,-360000,0,0,0,480e6));
    internal static void Close(double expected, double actual, double tolerance=2e-11)
        => Assert.AreEqual(expected,actual,Math.Max(1e-12,Math.Abs(expected)*tolerance));
    static double T(WeldedIBeamSpliceForceResult r,string name)=>r.Trace.Single(v=>v.Name==name).Value;
    internal static WeldedIBeamSpliceBoltInput BoltInput(bool ntc=false,WeldedIBeamSpliceForceInput? detail=null,
        string? source="Independent ULS",double? diameter=null,ConnectionBoltGrade grade=ConnectionBoltGrade.Class8_8)
    {
        double d=diameter??24;
        return new(ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"EN recommended factors"),
            detail??Input(),new(d,d+2,d==24?353:84.3,grade,true),new(d,d+2,d==24?353:84.3,grade,true),40,40,source!,"Explicit NTC SLE clearance evidence");
    }

    [TestMethod]
    public void UnsymmetricSectionAndAllSixteenGroupsMatchIndependentRationalOracle()
    {
        var r=WeldedIBeamSpliceForces.Calculate(Input());Assert.IsTrue(r.IsBalanced,string.Join(";",r.Diagnostics));
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","welded-ibeam-splice-independent.json")));
        foreach(var item in json.RootElement.GetProperty("trace").EnumerateObject()) Close(item.Value.GetDouble(),T(r,item.Name));
        foreach(var item in json.RootElement.GetProperty("groups").EnumerateArray())
        {
            var g=r.CoverGroups.Single(g=>g.Side.ToString()==item.GetProperty("side").GetString()&&g.Cover.ToString()==item.GetProperty("cover").GetString());
            var expected=item.GetProperty("forces").EnumerateArray().ToArray();Assert.AreEqual(expected.Length,g.Distribution.Forces.Count);
            for(int i=0;i<expected.Length;i++)
            {
                var values=expected[i].EnumerateArray().Select(v=>v.GetDouble()).ToArray();var f=g.Distribution.Forces[i];
                Close(values[0],f.Position.X);Close(values[1],f.Position.Y);Close(values[2],f.Fx);Close(values[3],f.Fy);
            }
        }
        Assert.AreEqual(16,r.CoverGroups.Count);Assert.IsFalse(r.CanQualifyWholeJoint);
    }
    [DataTestMethod][DataRow(240000d,0d,0d)][DataRow(0d,-360000d,0d)][DataRow(0d,0d,480e6)]
    [DataRow(-240000d,360000d,-480e6)][DataRow(0d,0d,0d)]
    public void ReconstructedGlobalActionsBalanceInAllSixDirections(double n,double v,double m)
    {
        var r=WeldedIBeamSpliceForces.Calculate(Input(new(n,v,0,0,0,m)));Assert.IsTrue(r.IsBalanced,string.Join(";",r.Diagnostics));
        foreach(var t in r.Trace.Where(t=>t.Name.Contains(".Residual."))) Assert.IsTrue(Math.Abs(t.Value)<1e-6,t.Name);
    }
    [TestMethod]
    public void FlangeSelfBendingAndUnsymmetricWebAxialForceAreNotDiscarded()
    {
        var r=WeldedIBeamSpliceForces.Calculate(Input(new(0,0,0,0,0,480e6)));
        Assert.IsTrue(Math.Abs(T(r,"Web.AxialForce"))>1000);
        Assert.IsTrue(Math.Abs(T(r,"Web.LocalMoment"))>1e6);
        Assert.IsTrue(T(r,"Left.Top.ParentLocalMoment")>0);
        Assert.AreNotEqual(T(r,"Left.Top.ParentAxial")/2,T(r,"Left.Top.OuterForce"));
    }
    [TestMethod]
    public void InnerStripBoltLineEccentricityCreatesTransverseForcesWithoutFictitiousNetShear()
    {
        var r=WeldedIBeamSpliceForces.Calculate(Input());
        var g=r.CoverGroups.Single(g=>g.Side==SpliceSide.Left&&g.Cover==WeldedSpliceCover.TopInnerPositive);
        Assert.IsTrue(g.Distribution.Forces.Any(f=>Math.Abs(f.Fy)>100));
        Close(0,g.Distribution.Forces.Sum(f=>f.Fy));
        Close(105,g.Distribution.InputActions.ApplicationY);
    }
    [TestMethod]
    public void RealCoverOffsetsNotAreasDetermineTheFlangeEquilibriumSplit()
    {
        var p=Input(new(240000,0,0,0,0,0));var r=WeldedIBeamSpliceForces.Calculate(p);
        double n=T(r,"Left.Top.ParentAxial");Close(n*21/41,T(r,"Left.Top.OuterForce"));
        Assert.AreNotEqual(n*5760/(5760+5760),T(r,"Left.Top.OuterForce"));
    }
    [TestMethod]
    public void ChangingOnlyTheActionPolePreservesThePhysicalResult()
    {
        var origin=WeldedIBeamSpliceForces.Calculate(Input());
        var shifted=WeldedIBeamSpliceForces.Calculate(Input(new(240000,-360000,0,0,0,480e6-120*(-360000)+35*240000,120,35)));
        Assert.IsTrue(shifted.IsBalanced);for(int i=0;i<origin.CoverGroups.Count;i++)
            for(int j=0;j<origin.CoverGroups[i].Distribution.Forces.Count;j++)
            { Close(origin.CoverGroups[i].Distribution.Forces[j].Fx,shifted.CoverGroups[i].Distribution.Forces[j].Fx);
                Close(origin.CoverGroups[i].Distribution.Forces[j].Fy,shifted.CoverGroups[i].Distribution.Forces[j].Fy); }
    }
    [DataTestMethod][DataRow(1e-12)][DataRow(1e-6)][DataRow(-1d)][DataRow(1e6)]
    public void ForceScalingAndReversalHaveNoAbsoluteLoadFloor(double factor)
    {
        var reference=WeldedIBeamSpliceForces.Calculate(Input());
        var r=WeldedIBeamSpliceForces.Calculate(Input(new(240000*factor,-360000*factor,0,0,0,480e6*factor)));
        Assert.IsTrue(r.IsBalanced,string.Join(";",r.Diagnostics));
        for(int i=0;i<r.CoverGroups.Count;i++)for(int j=0;j<r.CoverGroups[i].Distribution.Forces.Count;j++)
        { Close(reference.CoverGroups[i].Distribution.Forces[j].Fx*factor,r.CoverGroups[i].Distribution.Forces[j].Fx);
            Close(reference.CoverGroups[i].Distribution.Forces[j].Fy*factor,r.CoverGroups[i].Distribution.Forces[j].Fy); }
    }
    [TestMethod]
    public void FourTransverseBoltLinesAreSplitAcrossTheActualInnerPlates()
    {
        var top=new SpliceFlangeCoverDetail(new(390,16,355,510),new(160,18,355,510),50,60,new(3,4,90,110,80));
        var r=WeldedIBeamSpliceForces.Calculate(Input(top:top));Assert.IsTrue(r.IsBalanced,string.Join(";",r.Diagnostics));
        Assert.AreEqual(12,r.CoverGroups.Single(g=>g.Side==SpliceSide.Left&&g.Cover==WeldedSpliceCover.TopOuter).Distribution.Forces.Count);
        Assert.AreEqual(6,r.CoverGroups.Single(g=>g.Side==SpliceSide.Left&&g.Cover==WeldedSpliceCover.TopInnerNegative).Distribution.Forces.Count);
    }
    [DataTestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void EvenInfinitesimalOutOfPlaneActionsAreExplicitlyUnsupported(int component)
    {
        double e=1e-20;
        var r=WeldedIBeamSpliceForces.Calculate(Input(new(1,2,component==0?e:0,component==1?e:0,component==2?e:0,3,applicationZ:component==3?e:0)));
        Assert.IsFalse(r.IsBalanced);Assert.AreEqual(0,r.CoverGroups.Count);Assert.IsNull(r.ActionsAtJoint);
    }
    [DataTestMethod][DataRow("gap")][DataRow("odd")][DataRow("web")][DataRow("outside")][DataRow("bolt")]
    public void ImpossibleDetailsNeverExposeBalancedCoverForces(string variant)
    {
        var top=Input().Top;
        if(variant=="odd")top=new(top.Outer,top.EachInner,50,60,new(3,3,90,90,80));
        if(variant=="outside")top=new(new(450,16,355,510),top.EachInner,50,60,top.Bolts);
        if(variant=="bolt")top=new(top.Outer,top.EachInner,150,60,top.Bolts);
        var r=WeldedIBeamSpliceForces.Calculate(Input(top:top,webCover:variant=="web"?new(790,12,355,510):null,gap:variant=="gap"?160:10));
        Assert.IsFalse(r.IsBalanced);Assert.AreEqual(0,r.CoverGroups.Count);
    }
    [TestMethod]
    public void InvalidValuesAndMutableResultsAreRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(()=>WeldedIBeamSpliceForces.Calculate(null!));
        Assert.ThrowsException<ArgumentException>(()=>new WeldedIBeamSpliceSection(new(10,20,355,510),new(100,16,355,510),new(100,20,355,510),0,0));
        var r=WeldedIBeamSpliceForces.Calculate(Input());
        Assert.ThrowsException<NotSupportedException>(()=>((IList<WeldedSpliceCoverForceGroup>)r.CoverGroups).Clear());
    }
    [DataTestMethod][DataRow(false,ConnectionBoltGrade.Class8_8,135552d)][DataRow(true,ConnectionBoltGrade.Class8_8,135552d)]
    [DataRow(false,ConnectionBoltGrade.Class10_9,141200d)]
    public void EveryBoltPlaneUsesItsOwnForceAndAnIndependentResistance(bool ntc,ConnectionBoltGrade grade,double resistance)
    {
        var r=WeldedIBeamSpliceBolts.Verify(BoltInput(ntc,grade:grade));
        Assert.IsTrue(r.ComponentAssessment.IsComplete,string.Join(";",r.ComponentAssessment.Diagnostics));
        Assert.IsFalse(r.CanQualifyWholeJoint);
        var shear=r.ComponentAssessment.Checks.Where(c=>c.Id.EndsWith(".ShearPlane")).ToArray();
        Assert.AreEqual(r.Forces.CoverGroups.Sum(g=>g.Distribution.Forces.Count),shear.Length);
        foreach(var c in shear.Where(c=>!c.Id.Contains(".Web")))Close(resistance,c.Resistance);
        var g=r.Forces.CoverGroups[0];Close(g.Distribution.Forces[0].Shear,shear[0].Demand);
    }
    [TestMethod]
    public void ParentPlyReceivesTheVectorSumOfTheTwoShearPlanes()
    {
        var r=WeldedIBeamSpliceBolts.Verify(BoltInput());
        foreach(var part in new[]{IBeamSplicePart.TopFlange,IBeamSplicePart.BottomFlange,IBeamSplicePart.Web})
        {
            double sum=r.ComponentAssessment.Trace.Where(t=>t.Name.StartsWith("Left."+part+".Parent.Bolt")&&t.Name.EndsWith(".Fx")).Sum(t=>t.Value);
            Close(T(r.Forces,part==IBeamSplicePart.Web?"Web.AxialForce":part==IBeamSplicePart.TopFlange?"Left.Top.ParentAxial":"Left.Bottom.ParentAxial"),sum);
        }
        double vy=r.ComponentAssessment.Trace.Where(t=>t.Name.StartsWith("Left.Web.Parent.Bolt")&&t.Name.EndsWith(".Fy")).Sum(t=>t.Value);
        Close(-360000,vy);
    }
    [DataTestMethod][DataRow(0d)][DataRow(.2)][DataRow(10d)]
    public void BoltUtilizationsScaleAndOverloadsRemainFailed(double scale)
    {
        var r=WeldedIBeamSpliceBolts.Verify(BoltInput(detail:Input(new(240000*scale,-360000*scale,0,0,0,480e6*scale))));
        var baseline=WeldedIBeamSpliceBolts.Verify(BoltInput());Assert.IsTrue(r.ComponentAssessment.IsComplete);
        Close(baseline.ComponentAssessment.Utilization!.Value*scale,r.ComponentAssessment.Utilization!.Value);
        if(scale==0)Assert.AreEqual(ConnectionStatus.Passed,r.ComponentAssessment.Status);
        if(scale==10)Assert.AreEqual(ConnectionStatus.Failed,r.ComponentAssessment.Status);
    }
    [TestMethod]
    public void MissingCombinationAndHardwareClearanceDoNotProduceFavourableResults()
    {
        var missing=WeldedIBeamSpliceBolts.Verify(BoltInput(source:null));Assert.AreEqual(ConnectionStatus.NotSupported,missing.ComponentAssessment.Status);
        var q=BoltInput();var tight=new WeldedIBeamSpliceBoltInput(q.Code,q.Detail,q.FlangeBolt,q.WebBolt,250,250,"ULS");
        var r=WeldedIBeamSpliceBolts.Verify(tight);Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentAssessment.Status);
        Assert.AreEqual(0,r.ComponentAssessment.Checks.Count);
    }
    [TestMethod]
    public void WebLongJointReductionUsesABoundForTheActualChangingForceDirection()
    {
        var r=WeldedIBeamSpliceBolts.Verify(BoltInput());
        double diagonal=Math.Sqrt(200*200+560*560);
        double expected=135552*(1-(diagonal-15*24)/(200*24));
        foreach(var c in r.ComponentAssessment.Checks.Where(c=>c.Id.Contains(".Web")&&c.Id.EndsWith(".ShearPlane")))
            Close(expected,c.Resistance);
    }
    [TestMethod]
    public void AdjacentHardwareEnvelopesCannotOverlapEvenWhenEdgeClearancePasses()
    {
        var q=BoltInput();var r=WeldedIBeamSpliceBolts.Verify(new(q.Code,q.Detail,q.FlangeBolt,q.WebBolt,110,110,"ULS"));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentAssessment.Status);
        Assert.IsTrue(r.ComponentAssessment.Diagnostics.Any(s=>s.Contains("HARDWARE_OVERLAP")));
    }
    [TestMethod]
    public void ModelAdapterRetainsUnequalFlangesAndMatchesIndependentPlateIntegrals()
    {
        var model=new SteelSection(new SectionH(854,16,400,24,500,30,"Welded I",0),
            new SteelMaterial("S355",210000,355,510),Section.SectionTypes.Welded,Section.FormedTypes.HotFinished);
        var input=new SteelSectionInput(model,true);
        var s=WeldedIBeamSpliceSection.FromModel(input,10,12);var expected=Input().Section;
        Close(expected.Area,s.Area);Close(expected.StrongAxisSecondMoment,s.StrongAxisSecondMoment);
        Close(model.Area,s.Area);Close(model.J11,s.StrongAxisSecondMoment);Close(400,s.TopFlange.Width);Close(500,s.BottomFlange.Width);
        Assert.AreEqual(355d,s.TopFlange.Fy);Assert.AreEqual(510d,s.BottomFlange.Fu);
    }
    [TestMethod]
    public void ModelAdapterDoesNotSilentlyTurnARolledOrRoundedSectionIntoThreePlates()
    {
        foreach(var type in new[]{Section.SectionTypes.Rolled,Section.SectionTypes.Welded})
        {
            var m=new SteelSection(new SectionH(854,16,400,24,500,30,"Invalid",type==Section.SectionTypes.Welded?10:0),
                new SteelMaterial("S355",210000,355,510),type,Section.FormedTypes.HotFinished);
            Assert.ThrowsException<NotSupportedException>(()=>WeldedIBeamSpliceSection.FromModel(new(m,true),10,12));
        }
    }
    [DataTestMethod]
    [DataRow("Left.TopOuter.Bolt0.Cover.BearingX",354018.46153846156)]
    [DataRow("Left.TopOuter.Bolt0.Cover.BearingY",391680d)]
    [DataRow("Left.TopInnerNegative.Bolt0.Cover.BearingX",398270.76923076925)]
    [DataRow("Left.TopInnerNegative.Bolt0.Cover.BearingY",367200d)]
    public void ActualOuterAndInnerPlyBearingMatchesIndependentFractions(string trace,double expected)
    {
        // Outer: alpha_x=90/(3*26)-1/4=47/52; k=2.5; t=16.
        // Inner: same alpha_x, t=18. Y: one bolt line, alpha=65/(3*26)=5/6.
        var r=WeldedIBeamSpliceBolts.Verify(BoltInput());
        Close(expected,r.ComponentAssessment.Trace.Single(t=>t.Name==trace).Value);
    }
    [TestMethod]
    public void InnerStripTransverseBearingIsNotLostWhenTheTotalFlangeShearIsZero()
    {
        var r=WeldedIBeamSpliceBolts.Verify(BoltInput());
        double expected=32010.061784473237/398270.76923076925+8002.515446118309/367200;
        Close(expected,r.ComponentAssessment.Checks.Single(c=>c.Id=="Left.TopInnerNegative.Bolt0.Cover.BearingInteraction").Demand);
    }
}
