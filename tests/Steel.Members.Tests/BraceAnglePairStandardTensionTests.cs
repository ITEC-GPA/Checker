using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class BraceAnglePairStandardTensionTests
{
    static BraceAnglePairStandardTensionInput Input(bool ntc=false,double n=200000,double toe=101.25,
        double collectorFy=275,double collectorFu=430,double width=200,double distance=200,
        SpliceWeldPenetration penetration=SpliceWeldPenetration.Full,double cutMoment=0,string? source="Aligned physical collector cut",double edge=100,ConnectionBolt? bolt=null)
    {
        var code=ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"Explicit factors");
        var angle=new SectionL(100,10,150,10,"Sharp unequal L");
        var pair=SectionBuiltUp.DoubleAngle(angle,20,SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack,"2L");
        var snapshot=new SteelSectionInput(new SteelSection(pair,new SteelMaterial("S275",210000,275,430)));
        var adapted=BraceAngleModelAdapter.FromPair(snapshot,code,new(n,0,0,0,0,0),n/2,n/2,"Concomitant LC1",
            bolt??new(16,18,157,ConnectionBoltGrade.Class8_8,true),4,60,toe);
        var transfer=new BraceAnglePairBoltedTransferInput(adapted,new(200,20,275,430),40,40,edge,30,false,false,
            "Drawing","Conventional one-leg tension","LC1","NTC SLE evidence");
        return new(transfer,new(width,20,collectorFy,collectorFu),new(n,0,0,0,cutMoment,0),
            new(penetration,20,200,275,430,"Qualified WPS","Full fusion/inspection"),distance,source!);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ConventionalTensionTerminalIncludesAllPliesAndConstantCollector(bool ntc)
    {
        var r=BraceAnglePairStandardTension.Verify(Input(ntc));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        Assert.IsTrue(r.IsStaticResistanceComplete);Assert.IsFalse(r.SolvesLocalContactOrPrying);
        Assert.AreEqual(24,r.Plates.CollinearBlocks.Checks.Count);
        // Independent EC3.10.3 oracle on each actual single L, not on a fictitious paired net area.
        double beta=.5+.2*(60d/18-2.5)/2.5;
        foreach(var angle in r.Plates.Transfer.AngleSections)
        {
            var c=angle.Checks.Single(c=>c.Id=="Angle.EffectiveNetRupture");
            Assert.AreEqual(100000d,c.Demand);Close(beta*(2400-18*10)*430/1.25,c.Resistance);
        }
        Close(4000*275/(ntc?1.05:1),r.Continuation.Checks.Single(c=>c.Id=="Collector.GrossYield").Resistance);
        Assert.IsTrue(r.Plates.Transfer.FaceDemands.All(d=>d.AtGussetFace.My!=0));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Plates.Transfer.LocalThreeDimensionalStatus);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void FullStandardResultDoesNotEraseLocalMoments(bool ntc)
    {
        var p=Input(ntc);var raw=BraceAnglePairBoltedTransfer.Verify(p.Transfer);var r=BraceAnglePairStandardTension.Verify(p);
        CollectionAssert.AreEqual(raw.FaceDemands.Select(x=>x.AtGussetFace.My).ToArray(),r.Plates.Transfer.FaceDemands.Select(x=>x.AtGussetFace.My).ToArray());
        Assert.IsTrue(Math.Abs(raw.FaceDemands[0].AtGussetFace.My)>1e6);
    }
    [TestMethod] public void WeakCollectorIsActualFailureEvenWhenBoltsPass()
    {
        var r=BraceAnglePairStandardTension.Verify(Input(collectorFy:20,collectorFu:30));
        Assert.IsTrue(r.IsStaticResistanceComplete);Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.AreEqual(ConnectionStatus.Passed,r.Plates.Transfer.InPlaneComponents.Status);
    }
    [TestMethod] public void OverloadRemainsFailureWhenCollectorGeometryIsUnsupported()
    {
        var r=BraceAnglePairStandardTension.Verify(Input(n:2e6,width:210));
        Assert.IsTrue(r.HasFailedComponent);Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsFalse(r.IsStaticResistanceComplete);
    }
    [DataTestMethod][DataRow(200000d)][DataRow(0d)]
    public void InPlaneEccentricityCannotHideBehindSimplifiedAngleRuleEvenAtZeroLoad(double n)
    {
        var r=BraceAnglePairStandardTension.Verify(Input(n:n,toe:75));
        Assert.IsFalse(r.IsStaticResistanceComplete);Assert.IsTrue(r.MissingRequirements.Contains("GUSSET_CENTROID_MUST_ALIGN_WITH_MEMBER_SETTING_OUT_LINE"));
    }
    [TestMethod] public void InfinitesimalConcurrentMomentIsNotDropped()
    {
        var r=BraceAnglePairStandardTension.Verify(Input(cutMoment:1e-15));
        Assert.IsFalse(r.IsStaticResistanceComplete);Assert.IsTrue(r.MissingRequirements.Contains("ACTUAL_COLLECTOR_CUT_MUST_MATCH_PURE_TENSION_CASE"));
    }
    [DataTestMethod][DataRow("width")][DataRow("lap")][DataRow("partial")][DataRow("source")]
    public void PhysicalContinuationAndExecutionAreMandatory(string flaw)
    {
        var p=Input(width:flaw=="width"?210:200,distance:flaw=="lap"?130:200,
            penetration:flaw=="partial"?SpliceWeldPenetration.Partial:SpliceWeldPenetration.Full,source:flaw=="source"?null:"Source");
        Assert.IsFalse(BraceAnglePairStandardTension.Verify(p).IsStaticResistanceComplete);
    }
    [TestMethod] public void ZeroTensionIsSupportedOnlyForValidGeometry()
    {
        var r=BraceAnglePairStandardTension.Verify(Input(n:0));Assert.AreEqual(ConnectionStatus.Passed,r.Status);Assert.AreEqual(0d,r.Utilization);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ActualEccentricGroupQualifiesOnlyWithAlignedCollectorAndSufficientBlocks(bool ntc)
    {
        var r=BraceAnglePairStandardTension.Verify(Input(ntc,toe:75,edge:126.25));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        Assert.IsTrue(r.IsStaticResistanceComplete);Assert.IsNotNull(r.Utilization);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Plates.CollinearBlocks.Status);
        Assert.AreEqual(ConnectionStatus.Passed,r.BlockHierarchy.Status);
        // Independent N*e=200000*(75-48.75); sum xi*Fyi must reproduce the couple.
        var fs=r.Plates.Transfer.InPlaneBoltGroup.Forces;Close(5250000,fs.Sum(f=>f.Position.X*f.Fy));
        Assert.IsTrue(fs.Any(f=>f.Fy>0)&&fs.Any(f=>f.Fy<0));
        var path=r.BlockHierarchy.Checks.Single(c=>c.Id=="Angle0.Axis0.Direction1.Path1.U");
        Close(.6*800*157/1.25,path.Demand);
        Close(2*(40-.5*18)*10*275/(Math.Sqrt(3)*(ntc?1.05:1)),path.Resistance);
    }
    [TestMethod] public void BlockHierarchyIsLoadIndependentAndNeverUsesCancellingForces()
    {
        var a=BraceAnglePairBlockHierarchy.Verify(Input(n:1,toe:75,edge:126.25).Transfer);
        var b=BraceAnglePairBlockHierarchy.Verify(Input(n:200000,toe:75,edge:126.25).Transfer);
        CollectionAssert.AreEqual(a.Checks.Select(c=>c.Demand).ToArray(),b.Checks.Select(c=>c.Demand).ToArray());
        Assert.IsTrue(a.Checks.Any(c=>c.Id.Contains("Axis1")));Assert.IsTrue(a.Checks.Any(c=>c.Id.Contains("Direction-1")));
        Assert.IsTrue(a.Checks.All(c=>c.Demand>0));
    }
    [TestMethod] public void InsufficientHierarchyAtTinyLoadIsNotInventedRupture()
    {
        var r=BraceAnglePairStandardTension.Verify(Input(true,n:1,toe:75,edge:126.25,
            bolt:new(16,18,157,ConnectionBoltGrade.Class10_9,false)));
        Assert.AreEqual(ConnectionStatus.Passed,r.Plates.Transfer.InPlaneComponents.Status);
        Assert.AreEqual(ConnectionStatus.Failed,r.BlockHierarchy.Status);
        Assert.IsFalse(r.IsStaticResistanceComplete);Assert.IsFalse(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
    }
    static void Close(double e,double a)=>Assert.AreEqual(e,a,Math.Max(1,Math.Abs(e))*1e-10);
}
