using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X.Core;
namespace BridgeAudit;

[TestClass, DoNotParallelize]
public class BridgeResponseIntegrationTests
{
    [DataTestMethod] [DataRow(0)] [DataRow(1)]
    public void ResponseArchiveRoundtripAndUnits(int mode)
    {
        var d = BridgeSection.Defaults(); var q = BridgeSection.ResponseDefaults();
        q["tipo"] = BridgeSection.ResponseModes[mode]; q["punti"] = 5; q["incremento_k"] = .001; q["incremento_e"] = -200;
        d["curve_sezione"] = q;
        var r = BridgeSection.CalculateResponse(JsonNode.Parse(d.ToJsonString())!.AsObject(), q);
        Assert.IsTrue(r.Completed, r.Message); Assert.AreEqual(6, r.Points.Count);
        Assert.AreEqual(mode == 0 ? 1e-6 : -.0002, r.Points.Last().ControlValue, 1e-12);
        Assert.AreEqual(true, d.B("classe4")); Assert.AreEqual(2, d.Array("fasi")[1]!.D("phi"));
        string csv = BridgeSection.ResponseCsv(r);
        Assert.IsTrue(csv.Contains("epsilon_plastica_accumulata") && csv.Contains("Steel.Web"));
    }
    [TestMethod] public void HistoryStartPreservesCastingReferenceAndDoesNotChangeSelectedMethod()
    {
        var d = BridgeSection.Defaults(); var q = BridgeSection.ResponseDefaults();
        q["origine"] = BridgeSection.ResponseOrigins[1]; q["fase"] = 1; q["punti"] = 5; q["incremento_k"] = .0001;
        string method = d.S("metodo_analisi");
        var r = BridgeSection.CalculateResponse(d, q); Assert.IsTrue(r.Completed, r.Message);
        Assert.IsTrue(r.Points.First().State.Fibers.Any(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete && f.ActivationStrain != 0));
        Assert.AreEqual(method, d.S("metodo_analisi"));
    }
    [DataTestMethod] [DataRow("punti", 0d)] [DataRow("punti", 2.5d)] [DataRow("sottopassi", 0d)] [DataRow("incremento_k", 0d)]
    public void InvalidRequestDoesNotProduceCurve(string key, double value)
    {
        var q = BridgeSection.ResponseDefaults(); q[key] = value;
        Assert.ThrowsException<ArgumentException>(() => BridgeSection.CalculateResponse(BridgeSection.Defaults(), q));
    }
    [TestMethod] public void PrescribedAxialForceAndReferenceAreAppliedInCorrectUnits()
    {
        var q = BridgeSection.ResponseDefaults(); q["n_storico"] = false; q["N"] = -500; q["y"] = -700; q["punti"] = 4; q["incremento_k"] = .0005;
        var r = BridgeSection.CalculateResponse(BridgeSection.Defaults(), q); Assert.IsTrue(r.Completed, r.Message);
        foreach (var p in r.Points) { Assert.AreEqual(-500000d, p.State.N, .001); Assert.AreEqual(p.State.MomentAtOrigin - 700 * p.State.IntegratedN, p.MomentAtReference, .01); }
    }
}
