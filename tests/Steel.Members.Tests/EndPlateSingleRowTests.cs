using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateSingleRowTests
{
    internal static EndPlateNmvGeometry Geometry(bool ntc=false)
    {
        var p=EndPlateNmvCaseData.Geometry(ntc:ntc,flush:true,shear:0,singleInternalRow:true);
        return EndPlateNmvGeometry.PrepareSingleRow(p.Baseline,p.Connection,
            new[]{new EndPlateBoltElongationDetail(0,70.5,"Actual25+20.5+4+4+17 grip")},209.3,15.6,209.3,15.6,"Closed flange contacts");
    }

    [DataTestMethod][DataRow(10000d,1e6)][DataRow(10000d,-1e6)][DataRow(-10000d,1e6)][DataRow(-10000d,-1e6)]
    [DataRow(10000d,0d)][DataRow(-10000d,0d)][DataRow(0d,1e6)][DataRow(0d,-1e6)]
    public void SinglePhysicalRowEquilibratesBothSignsUsingOnlyRealContact(double n,double m)
    {
        var g=Geometry();Assert.IsTrue(g.IsSupported,string.Join(";",g.Diagnostics));
        var r=EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g,n,m));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,EndPlateNmvTests.Details(r));
        Assert.AreEqual(1,r.Equilibrium.Rows.Count);
        Assert.AreEqual(n,r.Equilibrium.Rows.Single().Tension-r.Equilibrium.Contacts.Sum(c=>c.Compression),1e-4);
        Assert.AreEqual(m,r.Equilibrium.Rows.Single().Tension*g.Rows.Single().Z+r.Equilibrium.Contacts.Sum(c=>c.SignedMoment),.01);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void OneActiveTensionSpringCannotBePromotedToAnInvertibleGlobalMatrix(bool ntc)
    {
        var g=Geometry(ntc);double n=10000,m=n*g.Rows.Single().Z;
        var r=EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g,n,m,separatePanel:true));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,EndPlateNmvTests.Details(r));
        Assert.IsTrue(r.Equilibrium.Contacts.All(x=>x.Compression<1e-8));
        Assert.IsFalse(r.Equilibrium.Tangent.IsPositiveDefinite);
        Assert.IsFalse(EndPlateNmvStiffness.Evaluate(r).CanUseAsGlobalConnectionSpring);
    }

    [TestMethod]
    public void SingleRowOutsideTheContactBracketIsRejectedBeforeNumericSearch()
    {
        Assert.ThrowsException<ArgumentException>(()=>EndPlateNmvEquilibrium.Solve(
            new[]{new EndPlateNmvSpringRow("outside",200,1000)},new[]{new EndPlateNmvContactBand("upper",90,110,1000),
                new EndPlateNmvContactBand("lower",-110,-90,1000)},1000,0));
    }
}
