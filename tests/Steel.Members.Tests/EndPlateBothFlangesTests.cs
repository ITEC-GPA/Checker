using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateBothFlangesTests
{
    [DataTestMethod][DataRow(false,false)][DataRow(false,true)][DataRow(true,false)][DataRow(true,true)]
    public void TheNewPhysicalLowerRowParticipatesInResistanceAndGeometryDerivedStiffness(bool ntc,bool flush)
    {
        var g=EndPlateBothFlangesCaseData.Geometry(ntc,flush);
        Assert.IsTrue(g.IsSupported,string.Join(";",g.Diagnostics));
        Assert.AreEqual(flush?4:5,g.Rows.Count);
        Assert.AreEqual(-206.55,g.Rows.Last().Z,1e-10);
        foreach(var sign in new[]{-1,1})
        {
            var r=EndPlateNmv.Verify(EndPlateBothFlangesCaseData.Input(g,n:10000,moment:sign*5e6,separatePanel:true));
            Assert.AreEqual(ConnectionStatus.Passed,r.Status,EndPlateNmvTests.Details(r));
            Assert.IsTrue(r.Equilibrium.IsConverged);
            if(sign<0)Assert.IsTrue(r.Equilibrium.Rows.Last().Tension>0);
            var k=EndPlateNmvStiffness.Evaluate(r);
            Assert.IsTrue(k.HasQualifiedLocalTangent,string.Join(";",k.LocalDiagnostics));
        }
    }

    [TestMethod]
    public void SymmetricInternalRowsHaveSymmetricSpringsAndSignedEquilibrium()
    {
        var g=EndPlateBothFlangesCaseData.Geometry(flush:true,shear:0);
        for(int i=0;i<g.Rows.Count;i++)
        {
            Assert.AreEqual(-g.Rows[3-i].Z,g.Rows[i].Z,1e-9);
            Assert.AreEqual(g.Rows[3-i].Stiffness,g.Rows[i].Stiffness,1e-6);
        }
        var a=EndPlateNmv.Verify(EndPlateBothFlangesCaseData.Input(g,20000,5e6));
        var b=EndPlateNmv.Verify(EndPlateBothFlangesCaseData.Input(g,20000,-5e6));
        Assert.AreEqual(ConnectionStatus.Passed,a.Status);Assert.AreEqual(ConnectionStatus.Passed,b.Status);
        Assert.AreEqual(a.Equilibrium.AxialOpening,b.Equilibrium.AxialOpening,1e-9);
        Assert.AreEqual(a.Equilibrium.Rotation,-b.Equilibrium.Rotation,1e-11);
        Assert.AreEqual(a.PositiveMomentResistance!.Value,a.NegativeMomentResistance!.Value,1e-4);
    }

    [TestMethod]
    public void NarrowFlangesCannotConstrainTheSupplementaryYieldLinePatterns()
    {
        var p=EndPlateBothFlangesCaseData.Geometry().Baseline.Input;
        var b=p.Beam;
        var narrow=new ConnectionRolledISection(b.Height,120,b.WebThickness,b.FlangeThickness,b.RootRadius,b.Area-2*(b.Width-120)*b.FlangeThickness,b.Fy,b.Fu);
        var input=new EndPlateMomentComponentInput(p.Policy,p.Column,narrow,p.ColumnFlangeFy,p.EndPlate,p.Bolt,
            p.InternalGeometry,p.ExtensionGeometry,p.ColumnFirstEndContinuation,p.ColumnLastEndContinuation,
            p.CompressionEndPlateProjection,new(p.Policy.Code,narrow,1e8,p.Shear,"Independent beam capacity"),
            0,p.Shear,0,p.Beta,0,p.PanelConfiguration,p.TransformationSource,p.ColumnStressSource,
            p.ColumnSwayRestraintSource,p.ActionCombinationSource,p.TStubMethod,p.ContactWidth,p.HoleClearanceServiceabilitySource);
        var r=EndPlateMomentComponents.Verify(input);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.ComponentAssessment.Status);
        CollectionAssert.Contains(r.ComponentAssessment.Diagnostics.ToArray(),"BeamFlangeTooNarrowForSciBothFlangePatterns");
    }

    [TestMethod]
    public void LegacyNearFlangeFixtureRemainsUnsupportedWithoutTheNewBoundaryEvidence()
    {
        var g=EndPlateNmvCaseData.Geometry(nearLowerFlangeRow:true);
        Assert.IsFalse(g.IsSupported);Assert.IsTrue(g.Diagnostics.Any(x=>x.Contains("CompressionFlangeTooClose")));
    }
}
