using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class BraceAnglePairTerminalPlateTests
{
    private static BraceAnglePairBoltedTransferInput Input(bool ntc=false,double toe=101.25,double n=200000,
        double edge=100,ConnectionPlate? gusset=null,bool cuts=false)
    {
        var code=ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"Explicit factors");
        var angle=new SectionL(100,10,150,10,"Sharp unequal L");
        var pair=SectionBuiltUp.DoubleAngle(angle,20,SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack,"2L");
        var snapshot=new SteelSectionInput(new SteelSection(pair,new SteelMaterial("S275",210000,275,430)));
        var adapted=BraceAngleModelAdapter.FromPair(snapshot,code,new(n,0,0,0,0,0),n/2,n/2,"Concomitant LC1",new(16,18,157,ConnectionBoltGrade.Class8_8,true),4,60,toe);
        return new(adapted,gusset??new(200,20,275,430),40,40,edge,30,false,cuts,"Drawing","Conditional in-plane model","LC1","NTC SLE evidence");
    }
    private static double T(BraceAnglePairTerminalPlateResult r,string name)=>r.GussetNetSection.Trace.Single(x=>x.Name==name).Value;
    private static void Eq(double e,double a)=>Assert.AreEqual(e,a,Math.Max(1,Math.Abs(e))*1e-9);
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void AllCollinearPrefixesCheckedWithoutClaimingThreeDimensionalQualification(bool ntc)
    {
        var r=BraceAnglePairTerminalPlates.Verify(Input(ntc));
        Assert.AreEqual(ConnectionStatus.Passed,r.CollinearBlocks.Status,string.Join(";",r.CollinearBlocks.Diagnostics));
        Assert.AreEqual(24,r.CollinearBlocks.Checks.Count);
        double gm0=ntc?1.05:1;
        var c=r.CollinearBlocks.Checks.Single(x=>x.Id=="Gusset.NegativeEdge.Prefix4");
        Eq(200000,c.Demand); Eq(.5*1820*430/1.25+3140*275/(Math.Sqrt(3)*gm0),c.Resistance);
        var a=r.CollinearBlocks.Checks.Single(x=>x.Id=="Angle0.NegativeEdge.Prefix1");
        Eq(25000,a.Demand); Eq(.5*297.5*430/1.25+310*275/(Math.Sqrt(3)*gm0),a.Resistance);
        Eq(3640,T(r,"Gusset.NetArea")); Eq(0,T(r,"Gusset.NetCentroidFromBoltLine"));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status); Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.Transfer.FaceDemands.All(x=>x.AtGussetFace.My!=0));
    }
    [TestMethod] public void EccentricNetSectionMatchesIndependentStripIntegration()
    {
        var r=BraceAnglePairTerminalPlates.Verify(Input(toe:75,edge:60));
        Assert.AreEqual(ConnectionStatus.Passed,r.GussetNetSection.Status);
        // Integrate 0.02mm strips of the actual200x20 section minus its18mm hole
        // centred at y=0. No production geometry or net-section kernel is used.
        double area=0,first=0,second=0;
        for(int i=0;i<10000;i++)
        {
            double y=-60+(i+.5)*.02;
            if(Math.Abs(y)<9)continue;
            double a=.02*20;area+=a;first+=a*y;second+=a*(y*y+.02*.02/12);
        }
        double cy=first/area,inertia=second-area*cy*cy;
        Eq(area,T(r,"Gusset.NetArea"));Eq(cy,T(r,"Gusset.NetCentroidFromBoltLine"));Eq(inertia,T(r,"Gusset.NetInertia"));
        Eq(5250000+200000*cy,T(r,"Gusset.MomentAtNetCentroid"));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.CollinearBlocks.Status);
        Assert.IsTrue(r.MissingMechanisms.Contains("NONCOLLINEAR_BLOCK_TEARING"));
    }
    [TestMethod] public void OriginalEccentricBoltForcesRemainUnchanged()
    {
        var p=Input(toe:75);var original=BraceAnglePairBoltedTransfer.Verify(p);var r=BraceAnglePairTerminalPlates.Verify(p);
        CollectionAssert.AreEqual(original.InPlaneBoltGroup.Forces.Select(x=>x.Fy).ToArray(),r.Transfer.InPlaneBoltGroup.Forces.Select(x=>x.Fy).ToArray());
    }
    [TestMethod] public void WeakGussetFailureNotHiddenByUnsupportedMixedDirectionBlocks()
    {
        var r=BraceAnglePairTerminalPlates.Verify(Input(toe:75,gusset:new(200,20,30,40)));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status); Assert.AreEqual(ConnectionStatus.Failed,r.GussetNetSection.Status);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.CollinearBlocks.Status);
    }
    [TestMethod] public void BothAnglesUseHalfForceButGussetUsesTotal()
    {
        var r=BraceAnglePairTerminalPlates.Verify(Input());
        foreach(int k in Enumerable.Range(1,4))
        {
            double g=r.CollinearBlocks.Checks.Single(x=>x.Id==$"Gusset.NegativeEdge.Prefix{k}").Demand;
            foreach(int side in new[]{0,1})Eq(g/2,r.CollinearBlocks.Checks.Single(x=>x.Id==$"Angle{side}.NegativeEdge.Prefix{k}").Demand);
        }
    }
    [TestMethod] public void AdditionalHolesInvalidateBothNewSubmodels()
    {
        var r=BraceAnglePairTerminalPlates.Verify(Input(cuts:true));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.GussetNetSection.Status);Assert.AreEqual(ConnectionStatus.NotSupported,r.CollinearBlocks.Status);
    }
    [TestMethod] public void ZeroAndImmutableResults()
    {
        var r=BraceAnglePairTerminalPlates.Verify(Input(n:0));Assert.AreEqual(ConnectionStatus.Passed,r.GussetNetSection.Status);
        Assert.ThrowsException<NotSupportedException>(()=>((IList<ConnectionCheck>)r.CollinearBlocks.Checks).Clear());
    }
}
