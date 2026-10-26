using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateNmvReportTests
{
    [DataTestMethod]
    [DataRow(false,false,1)][DataRow(false,false,-1)][DataRow(false,true,1)][DataRow(false,true,-1)]
    [DataRow(true,false,1)][DataRow(true,false,-1)][DataRow(true,true,1)][DataRow(true,true,-1)]
    public void ExecutableExamplesExportConcomitantActionsContactAndQualifiedMatrix(bool ntc, bool flush, int sign)
    {
        var g = EndPlateNmvCaseData.Geometry(ntc, flush);
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g, sign * 10000, sign * 1e6, separatePanel: true));
        var report = StandardConnectionCaseReport.Create(r);
        Assert.AreEqual(ConnectionStatus.Passed, report.Status, EndPlateNmvTests.Details(r));
        Assert.IsTrue(report.IsComplete); Assert.IsFalse(report.CanQualifyWholeMember);
        Assert.IsTrue(report.NmvStiffness.HasQualifiedLocalTangent, string.Join(";",report.NmvStiffness.LocalDiagnostics));
        Assert.IsTrue(report.NmvStiffness.CanUseAsGlobalConnectionSpring);
        Assert.AreEqual(sign * 10000, report.Inputs.Single(v => v.Name == "BeamFace.N").Value);
        Assert.AreEqual(sign * 1e6, report.Inputs.Single(v => v.Name == "BeamFace.M1").Value);
        Assert.AreEqual(ntc ? 1.05 : 1, report.Inputs.Single(v => v.Name == "GammaM0").Value);
        Assert.AreEqual("N", report.Results.Single(v => v.Name == "LocalTangent.KNM").Unit);
        Assert.AreEqual(g.Rows.Count, report.Results.Count(v => v.Name.StartsWith("Row") && v.Name.EndsWith(".Tension")));
        Assert.IsTrue(report.Results.Any(v => v.Name == "UpperContact.ClosedLength"));
        Assert.IsTrue(report.Results.Any(v => v.Name == "LowerContact.ClosedLength"));
        Assert.AreEqual(report.Inputs.Count, report.Inputs.Select(v => v.Name).Distinct().Count());
        Assert.AreEqual(report.Results.Count, report.Results.Select(v => v.Name).Distinct().Count());
        Assert.AreEqual(report.Checks.Count, report.Checks.Select(v => v.Id).Distinct().Count());
        Assert.IsTrue(report.Inputs.Concat(report.Results).All(v => double.IsFinite(v.Value) && !string.IsNullOrWhiteSpace(v.Unit)));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<SectionResistanceValue>)report.Results).Clear());
        Export($"nmv-{(ntc ? "ntc" : "ec")}-{(flush ? "flush" : "extended")}-{(sign > 0 ? "tension" : "compression")}", report);
    }

    [TestMethod]
    public void MissingPanelOrMismatchedMatrixDoesNotAlterTheResistanceReport()
    {
        var g = EndPlateNmvCaseData.Geometry(shear: 0);
        var noPanel = StandardConnectionCaseReport.Create(EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g,10000,0)));
        Assert.AreEqual(ConnectionStatus.Passed,noPanel.Status); Assert.IsTrue(noPanel.IsComplete);
        Assert.IsFalse(noPanel.NmvStiffness.CanUseAsGlobalConnectionSpring);
        Assert.IsTrue(noPanel.NmvStiffness.GlobalDiagnostics.Count > 0); Assert.AreEqual(0,noPanel.Diagnostics.Count);
        var k = noPanel.NmvStiffness.LocalTangent;
        var mismatch = StandardConnectionCaseReport.Create(EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g,10000,0,separatePanel:true,
            applied:new EndPlateNmvTangent(k.Axial,0,k.Rotation))));
        Assert.AreEqual(ConnectionStatus.Passed,mismatch.Status); Assert.IsTrue(mismatch.NmvStiffness.RequiresGlobalModelReview);
        Assert.AreEqual(false,mismatch.NmvStiffness.GlobalMatrixMatches);
        Export("nmv-global-matrix-mismatch",mismatch);
    }

    [TestMethod]
    public void UnsupportedGeometryAndFailedColumnKeepTheirStatusWithoutExportingStiffness()
    {
        var unsupported = StandardConnectionCaseReport.Create(EndPlateNmv.Verify(EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry(gap:1))));
        Assert.AreEqual(ConnectionStatus.NotSupported,unsupported.Status); Assert.IsFalse(unsupported.IsComplete);
        Assert.IsNull(unsupported.NmvStiffness.LocalTangent); Assert.IsFalse(unsupported.Results.Any(v=>v.Name.StartsWith("ConditionalComponent.")));
        var failed = StandardConnectionCaseReport.Create(EndPlateNmv.Verify(EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry(),columnN:-4e6)));
        Assert.AreEqual(ConnectionStatus.Failed,failed.Status); Assert.IsTrue(failed.Checks.Any(c=>!c.Passed));
        Assert.IsNull(failed.NmvStiffness.LocalTangent);
        Export("nmv-unsupported-gap",unsupported); Export("nmv-failed-column",failed);
    }

    [TestMethod]
    public void CatalogKeepsTheNewDomainSeparateFromThePureMomentAndComponentRoutes()
    {
        var c = ConnectionCapabilityCatalog.Get("StandardMomentEndPlateNMV");
        Assert.AreEqual(ConnectionCapabilityLevel.StandardStaticVerification,c.Level);
        Assert.IsTrue(c.MissingMechanisms.Any(x=>x.Contains("opposite flange")));
        Assert.IsTrue(c.ExternalRequirements.Any(x=>x.Contains("separately represented column panel")));
        Assert.IsTrue(ConnectionCapabilityCatalog.Get("StandardMomentEndPlate").Domain.Contains("N=0"));
        Assert.AreEqual(ConnectionCapabilityLevel.ComponentsOnly,ConnectionCapabilityCatalog.Get("MomentEndPlate").Level);
    }

    private static void Export(string name, StandardConnectionCaseReport report)
    {
        var options = new JsonSerializerOptions { WriteIndented = true }; options.Converters.Add(new JsonStringEnumConverter());
        string json = JsonSerializer.Serialize(report,options); using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(report.Status.ToString(), doc.RootElement.GetProperty("Status").GetString());
        Assert.AreEqual(report.NmvStiffness.CanUseAsGlobalConnectionSpring,
            doc.RootElement.GetProperty("NmvStiffness").GetProperty("CanUseAsGlobalConnectionSpring").GetBoolean());
        string? folder = Environment.GetEnvironmentVariable("GPC_ENDPLATE_NMV_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(folder)) { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder,name+".json"),json); }
    }
}
