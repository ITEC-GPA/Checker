using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateInPlaneTests
{
    static EndPlateBoltComponentResult Bolts(bool ntc=false,bool flush=false,double shear=300000,double beta=1)
    {
        var parent=EndPlateInitialCaseTestData.Parent(ntc,flush,shear:shear,beta:beta);
        return EndPlateInitialCaseTestData.Connection(parent.MomentComponents).Bolts;
    }

    [DataTestMethod]
    [DataRow(false,false)][DataRow(true,false)][DataRow(false,true)][DataRow(true,true)]
    public void StandardRowsRetainBothPhysicalPlyPaths(bool ntc,bool flush)
    {
        var bolts=Bolts(ntc,flush);var r=EndPlateInPlaneChecks.Verify(bolts);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics)+" "+string.Join(";",r.Checks.Where(x=>!x.Passed).Select(x=>x.Id)));
        Assert.IsTrue(r.IsComplete);Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.AreSame(bolts,r.Bolts);
        Assert.AreEqual(2*2*2*(flush?3:4), r.Checks.Count(x=>x.Id.Contains("Block")));
    }

    [TestMethod]
    public void OuterStripUsesActualPlateEndAndHalfItsHole()
    {
        var r=EndPlateInPlaneChecks.Verify(Bolts());
        // C1-derived geometry: upper bolt=-40, upper end=-90, e2=75, t=25,d0=26.
        // One top outer strip: Anv=(50-13)*25, Ant=(75-13)*25; force=30000.
        double resistance=.5*410*1550/1.1+265*925/(Math.Sqrt(3)*1);
        var c=r.Checks.Single(x=>x.Id=="Plate.Top.Row1.OuterBlock");
        Assert.AreEqual(30000,c.Demand,1e-8);Assert.AreEqual(resistance,c.Resistance,1e-7);
        Assert.AreEqual(925,r.Trace.Single(x=>x.Name=="Plate.Top.Row1.AnvOneSide").Value,1e-8);
    }

    [TestMethod]
    public void EveryCrossedHoleIsDeductedAtTheBottomExit()
    {
        var r=EndPlateInPlaneChecks.Verify(Bolts());
        // Four holes between top row and bottom edge, only half the start hole.
        double av=(580-(-40)-3.5*26)*25;
        Assert.AreEqual(av,r.Trace.Single(x=>x.Name=="Plate.Bottom.Row1.AnvOneSide").Value,1e-8);
        Assert.AreEqual(150000,r.Checks.Single(x=>x.Id=="Plate.Bottom.Row1.OuterBlock").Demand,1e-8);
        Assert.AreEqual(300000,r.Checks.Single(x=>x.Id=="Plate.Bottom.Row1.InnerBlock").Demand,1e-8);
    }

    [TestMethod]
    public void ColumnEndIsNotTheEndPlateEdge()
    {
        var r=EndPlateInPlaneChecks.Verify(Bolts());
        Assert.AreEqual((1000-13)*20.5,r.Trace.Single(x=>x.Name=="ColumnFlange.Top.Row1.AnvOneSide").Value,1e-7);
    }

    [TestMethod]
    public void GrossNetAndMinimumFastenerGroupHaveIndependentOracles()
    {
        var r=EndPlateInPlaneChecks.Verify(Bolts());
        Assert.AreEqual(250*25*265/Math.Sqrt(3),r.Checks.Single(x=>x.Id=="Plate.GrossShear").Resistance,1e-7);
        Assert.AreEqual((250-52)*25*265/Math.Sqrt(3),r.Checks.Single(x=>x.Id=="Plate.NetShearBound").Resistance,1e-7);
        // Joint length510<15d? no: betaLj=1-(510-360)/(200*24).
        double boltShear=.6*800*353/1.25*(1-(510-15*24)/(200d*24));
        Assert.AreEqual(8*boltShear,r.Checks.Single(x=>x.Id=="Plate.BoltGroup").Resistance,1e-7);
    }

    [TestMethod]
    public void ZeroShearRetainsGeometryAndAllChecks()
    {
        var r=EndPlateInPlaneChecks.Verify(Bolts(shear:0));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);Assert.IsTrue(r.Checks.All(x=>x.Demand==0));
    }

    [TestMethod]
    public void DoubleSidedCaseIsNotSilentlyReinterpreted()
    {
        var r=EndPlateInPlaneChecks.Verify(Bolts(beta:0));
        Assert.IsFalse(r.IsComplete);CollectionAssert.Contains(r.Diagnostics.ToArray(),"SingleSidedStandardJointRequired");
    }

    [TestMethod]
    public void NullRejected()=>Assert.ThrowsException<ArgumentNullException>(()=>EndPlateInPlaneChecks.Verify(null!));
}
