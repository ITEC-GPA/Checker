using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateNmvBoundaryTests
{
    [TestMethod]
    public void SinglePhysicalRowReturnsUnsupportedBeforeEnteringTheTwoRowKernel()
    {
        var g = EndPlateNmvCaseData.Geometry(flush:true,singleInternalRow:true);
        Assert.IsTrue(g.Diagnostics.Contains("AtLeastTwoPhysicalTensionRowsRequiredForNmv"));
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g));
        Assert.IsFalse(r.IsComplete); Assert.IsNull(r.Equilibrium);
    }

    [DataTestMethod][DataRow(false,1)][DataRow(false,-1)][DataRow(true,1)][DataRow(true,-1)]
    public void ReversedShearUsesTheSamePhysicalEnvelopeAndTransportsItsSignedArm(bool ntc,int sign)
    {
        var g = EndPlateNmvCaseData.Geometry(ntc:ntc);
        var p = EndPlateNmvCaseData.Input(g,n:sign*10000,moment:sign*1e6,shear:-20000);
        var r = EndPlateNmv.Verify(p);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,EndPlateNmvTests.Details(r));
        Assert.AreEqual(sign*1e6+20000*266.7/2,
            r.Trace.Single(x=>x.Name=="MomentAtColumnCentroid").Value,1e-8);
        Assert.AreEqual(20000,p.ColumnLower.N-p.ColumnUpper.N,1e-8);
        Assert.AreEqual(g.Connection.Bolts.BoltAssessment.Checks.Count,r.Checks.Count(x=>x.Id.StartsWith("Bolts.")));
    }
}
