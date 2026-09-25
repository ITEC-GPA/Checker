using System.IO.Compression;
using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X.Core;

namespace BridgeAudit;

[TestClass, DoNotParallelize]
public class BridgeHistoryIntegrationTests
{
    static JsonObject Input(int method)
    {
        var d = BridgeSection.Defaults(); d["metodo_analisi"] = BridgeSection.CalculationMethods[method];
        d["fibre_anima"] = 40; d["fibre_cls"] = 24; d["fibre_flange"] = 4; d["sottopassi"] = 4;
        return d;
    }
    [DataTestMethod, DataRow(0), DataRow(1), DataRow(2)]
    public void ArchiveRoundtripPreservesMethodAndCalculation(int method)
    {
        var d = Input(method); var r = BridgeSection.Calculate(d);
        var json = r.Json().ToJsonString(); var saved = JsonNode.Parse(json)!["Input"]!.AsObject();
        var again = BridgeSection.Calculate(saved);
        Assert.AreEqual(r.Method, again.Method);
        CollectionAssert.AreEqual(r.Stages.Last().Points.Select(p => p.Stress).ToArray(), again.Stages.Last().Points.Select(p => p.Stress).ToArray());
        Assert.AreEqual(method != 0, JsonNode.Parse(json)!["Stages"]![0]!["History"] is not null);
        if (method == 2) { StringAssert.Contains(json, "PlasticStrain"); Assert.IsFalse(json.Contains("NaN")); }
    }
    [TestMethod] public void NonlinearDoesNotEraseClass4OrCreepPreferences()
    {
        var d = Input(2); var before = d.ToJsonString(); var r = BridgeSection.Calculate(d);
        Assert.AreEqual(before, d.ToJsonString()); Assert.IsTrue(d.B("classe4"));
        Assert.IsTrue(r.Stages.All(s => s.GetHistory()!.State.Panels.Count == 0));
        Assert.AreEqual(0, r.Stages[1].Contributions.Last().Phi);
    }
    [TestMethod] public void UnlimitedHistoryAlsoSupportsGeometryAndPropertiesQueries()
    {
        var d = Input(1); d["classe4"] = false;
        d["fasi"] = new JsonArray(Enumerable.Range(0, 31).Select(i => (JsonNode)BridgeSection.Phase("F" + i, m: 10)).ToArray());
        Assert.IsTrue(BridgeSection.Geometry(d).SteelArea > 0);
        Assert.IsTrue(BridgeSection.GrossPhaseProperties(d, d.Array("fasi")[0]!.AsObject()).Area > 0);
        Assert.AreEqual(31, BridgeSection.Calculate(d).Stages.Count);
    }
    [DataTestMethod, DataRow("fibre_anima", -1), DataRow("fibre_cls", 1.5), DataRow("sottopassi", 0)]
    public void InvalidNumericalOptionsAreRejected(string key, double value)
    {
        var d = Input(2); d[key] = value; Assert.ThrowsException<ArgumentException>(() => BridgeSection.Calculate(d));
    }
    [TestMethod] public void StrictNonlinearViscosityCannotSilentlyIgnorePhi()
    {
        var d = Input(2); d["viscosita_nl"] = BridgeSection.NonlinearCreepModes[1];
        Assert.ThrowsException<NotSupportedException>(() => BridgeSection.Calculate(d));
    }
    [TestMethod] public void UnknownAnalysisMethodCannotSilentlyFallBack()
    {
        var d = Input(2); d["metodo_analisi"] = "Sconosciuto"; Assert.ThrowsException<ArgumentException>(() => BridgeSection.Calculate(d));
    }
    [DataTestMethod, DataRow(1), DataRow(2)]
    public void ReportUsesHistoricalStrainsInsteadOfLegacyEquivalentShrinkage(int method)
    {
        var d = Input(method); var p = BridgeSection.ShrinkagePhase(); p["epsilon_cs"] = -100; d.Array("fasi").Add(p);
        var r = BridgeSection.Calculate(d);
        using var zip = new ZipArchive(new MemoryStream(ReportBridge.Create("Storico", r, ReportBridge.DefaultSections())));
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()); string xml = reader.ReadToEnd();
        StringAssert.Contains(xml, "εmecc"); StringAssert.Contains(xml, "residuo"); StringAssert.Contains(xml, "Ritiro");
        Assert.IsFalse(xml.Contains("Correzione σc")); Assert.IsFalse(xml.Contains("Entro limiti"));
    }
}
