using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class StandardEndPlateApplicationTests
{
    [DataTestMethod]
    [DataRow(false,false,1)][DataRow(false,true,1)][DataRow(true,false,1)][DataRow(true,true,1)]
    [DataRow(false,false,-1)][DataRow(false,true,-1)][DataRow(true,false,-1)][DataRow(true,true,-1)]
    public void OneCallUsesRealSignedFemActionsAndMatchesTheExplicitPipeline(bool ntc,bool flush,int sign)
    {
        var input=StandardEndPlateCaseData.Input(ntc,flush,moment:sign*5e6,shear:sign*20000);
        var r=StandardEndPlate.Verify(input);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
        Assert.IsTrue(r.IsComplete&&r.CanQualifyWholeJoint);Assert.IsFalse(r.CanQualifyWholeMember);
        Assert.AreSame(input.Beam.Actions,r.Strength.Input.BeamFaceActions);
        Assert.AreEqual(0,r.BeamPreparation.Input.Actions.N);Assert.AreEqual(0,r.BeamPreparation.Input.Actions.M1);
        Assert.AreEqual(sign*20000,r.BeamPreparation.Input.Actions.V2);
        Assert.AreEqual(10000,r.Report.Inputs.Single(x=>x.Name=="BeamFace.N").Value);
        Assert.IsTrue(r.Stiffness.CanUseAsGlobalConnectionSpring);
        var g=EndPlateBothFlangesCaseData.Geometry(ntc,flush,shear:sign*20000);
        var reference=EndPlateNmv.Verify(EndPlateBothFlangesCaseData.Input(g,n:10000,moment:sign*5e6,shear:sign*20000,separatePanel:true));
        Assert.AreEqual(reference.Equilibrium.Rotation,r.Strength.Equilibrium.Rotation,1e-12);
        Assert.AreEqual(reference.Checks.Count,r.Checks.Count);
        foreach(var check in r.Checks)
        {
            var other=reference.Checks.Single(x=>x.Id==check.Id);
            Assert.AreEqual(other.Utilization,check.Utilization,1e-10,check.Id);
        }
        string json=JsonSerializer.Serialize(r.Report);
        Assert.IsFalse(json.Contains("NaN")||json.Contains("Infinity"));
        var folder=Environment.GetEnvironmentVariable("GPC_ENDPLATE_APP_REPORT_DIR");
        if(!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,$"application-{ntc}-{flush}-{sign}.json"),json);
        }
    }

    [DataTestMethod][DataRow("unordered")][DataRow("missingAlpha")][DataRow("torsion")][DataRow("noCombination")]
    [DataRow("gap")][DataRow("slender")][DataRow("bearingOnly")][DataRow("badCuts")]
    public void UnsupportedCasesRetainActualActionsAndDiagnosticReport(string problem)
    {
        var p=StandardEndPlateCaseData.Input(problem:problem);
        var r=StandardEndPlate.Verify(p);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status,string.Join(";",r.Diagnostics));
        Assert.IsFalse(r.IsComplete);Assert.IsTrue(r.Diagnostics.Count>0);
        Assert.AreEqual(10000,r.Report.Inputs.Single(x=>x.Name=="BeamFace.N").Value);
        Assert.AreEqual(r.Status,r.Report.Status);Assert.IsFalse(r.Report.IsComplete);
        Assert.IsFalse(r.Stiffness?.CanUseAsGlobalConnectionSpring??false);
        if(problem=="torsion")Assert.AreEqual(1000,r.Report.Inputs.Single(x=>x.Name=="BeamFace.T").Value);
    }

    [TestMethod]
    public void ExcessiveShearRetainsTheKnownFailureWhenPreparationCannotExportCapacity()
    {
        var r=StandardEndPlate.Verify(StandardEndPlateCaseData.Input(shear:5e6));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsNull(r.Strength);
        Assert.IsTrue(r.Checks.Any(x=>!x.Passed));Assert.AreEqual(ConnectionStatus.Failed,r.Report.Status);
        Assert.IsFalse(r.Report.IsComplete);Assert.IsNull(r.Report.NmvStiffness);
    }

    [TestMethod]
    public void OneRowPureTensionKeepsStrengthSeparateFromSingularGlobalMatrix()
    {
        var r=StandardEndPlate.Verify(StandardEndPlateCaseData.Input(flush:true,single:true,n:10000,moment:10000*206.55,shear:0));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
        Assert.AreEqual(1,r.Strength.Equilibrium.Rows.Count);
        Assert.IsFalse(r.Stiffness.HasQualifiedLocalTangent);Assert.IsFalse(r.Stiffness.CanUseAsGlobalConnectionSpring);
    }

    [TestMethod]
    public void LayoutCopiesPhysicalRowsAndNeverSilentlyReordersThem()
    {
        var p=StandardEndPlateCaseData.Input();var l=p.Layout;double[] rows=l.RowY.ToArray();
        var copy=new StandardEndPlateLayout(l.Plate,l.Bolt,l.Gauge,rows,l.TopProjection,l.BottomProjection,l.WebWeldLeg,
            l.UpperWeldLeg,l.LowerWeldLeg,l.AlphaMethod,l.Boundaries,l.Source);
        rows[0]=999;Assert.AreEqual(-40,copy.RowY[0]);
        Assert.ThrowsException<ArgumentException>(()=>new StandardEndPlateLayout(l.Plate,l.Bolt,l.Gauge,Array.Empty<double>(),
            l.TopProjection,l.BottomProjection,l.WebWeldLeg,l.UpperWeldLeg,l.LowerWeldLeg,l.AlphaMethod,l.Boundaries,l.Source));
    }

    [TestMethod]
    public void OverloadedColumnDoesNotLoseFailedChecksInApplicationReport()
    {
        var r=StandardEndPlate.Verify(StandardEndPlateCaseData.Input(columnN:-4e6));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.Checks.Any(x=>x.Id.StartsWith("Column.Cut")&&!x.Passed));
        Assert.AreEqual(ConnectionStatus.Failed,r.Report.Status);Assert.IsFalse(r.Stiffness.CanUseAsGlobalConnectionSpring);
    }

    [DataTestMethod][DataRow("conflictingAlpha")][DataRow("wrongPolicy")][DataRow("wrongProjection")][DataRow("externalRows")]
    public void ConflictingPhysicalDeclarationsAreReportedRatherThanSilentlyAdjusted(string conflict)
    {
        var p=StandardEndPlateCaseData.Input(flush:conflict=="wrongProjection");var l=p.Layout;
        var layout=new StandardEndPlateLayout(l.Plate,l.Bolt,l.Gauge,conflict=="externalRows"?new[]{-40d,-20,60}:l.RowY,
            conflict=="wrongProjection"?10:l.TopProjection,l.BottomProjection,l.WebWeldLeg,l.UpperWeldLeg,l.LowerWeldLeg,
            l.AlphaMethod,l.Boundaries,l.Source,conflict=="conflictingAlpha"?new TStubChartAlpha(8,"Supplied value"):null);
        var policy=conflict=="wrongPolicy"?ColumnWebPolicy.Ntc2018(ConnectionDesignCode.Ntc2018()):p.Policy;
        var r=StandardEndPlate.Verify(new(policy,p.Beam,p.Column,layout,p.Fasteners,p.Welds,p.Contact,
            p.ColumnUpper,p.ColumnLower,p.ColumnReferenceSource));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsFalse(r.IsComplete);
        Assert.IsTrue(r.Diagnostics.Count>0);Assert.AreEqual(r.Status,r.Report.Status);
    }

    [TestMethod]
    public void AppliedGlobalMatrixMismatchRemainsSeparateFromLocalStrength()
    {
        var p=StandardEndPlateCaseData.Input();var first=StandardEndPlate.Verify(p);var k=first.Stiffness.LocalTangent;
        var r=StandardEndPlate.Verify(new(p.Policy,p.Beam,p.Column,p.Layout,p.Fasteners,p.Welds,p.Contact,p.ColumnUpper,p.ColumnLower,
            p.ColumnReferenceSource,true,p.ColumnPanelModelSource,new(k.Axial*2,k.Coupling,k.Rotation*2)));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);Assert.IsTrue(r.Stiffness.RequiresGlobalModelReview);
        Assert.AreEqual(false,r.Stiffness.GlobalMatrixMatches);Assert.AreEqual(0,r.Diagnostics.Count);
    }
}
