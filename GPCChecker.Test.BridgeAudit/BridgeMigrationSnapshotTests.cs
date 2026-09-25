using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X.Core;

namespace BridgeAudit;

[TestClass]
public class BridgeMigrationSnapshotTests
{
    public static IEnumerable<object[]> Cases() => Enumerable.Range(0, 8).Select(i => new object[] { i });
    private static JsonObject Input(int id)
    {
        var d = BridgeSection.Defaults(); BridgeSection.EnsureAccessoryDefaults(d);
        switch (id)
        {
            case 1: d["classe4"] = false; d["rebars_top"] = false; d["rebars_bottom"] = false; break;
            case 2: d["plate2"] = true; d["t_web"] = 8; break;
            case 3:
                d.Array("fasi")[1]!["N"] = -1500; d.Array("fasi")[1]!["riferimento_N"] = BridgeSection.CommonLoadReference; d["y_ref"] = -300;
                d.Array("fasi")[2]!["N"] = 500; d.Array("fasi")[2]!["riferimento_N"] = BridgeSection.EffectiveLoadReference; break;
            case 4:
                d.Array("fasi").Insert(1, BridgeSection.ShrinkagePhase()); d.Array("fasi")[1]!["epsilon_cs"] = -250;
                d.Array("fasi").Add(BridgeSection.ShrinkagePhase()); d.Array("fasi")[4]!["epsilon_cs"] = -100; break;
            case 5:
                d["normativa"] = BridgeSection.Standards[1]; d.Array("fasi")[2]!["tipo"] = "Soletta esclusa";
                d["pioli"] = true; d.Array("fasi")[2]!["V"] = -500; break;
            case 6:
                d["fasi"] = new JsonArray(BridgeSection.Phase("Dettagli", "Composta", 500, 2000)); d.Array("fasi")[0]!["V"] = 1600;
                d["irrigidimenti"] = true; d["t_irr"] = 30; d["lati_irr"] = "Bilaterali diversi"; d["b_irr_dx"] = 180; d["t_irr_dx"] = 25;
                d["appoggio"] = true; d["R_app"] = 500; d["pos_app"] = "Estremità sinistra"; d["c_app"] = 250; d["terminale_rigido"] = true; d["e_term"] = 500;
                d["pioli"] = true; d["armatura_trasv"] = true; d["fatica_pioli"] = true; d["q_fat_min"] = -100; d["q_fat_max"] = 400;
                d["flangia_fat_tesa"] = true; d["dsigma_fat"] = 30; break;
            case 7: d["stato"] = "SLE rara"; d["fasi"] = new JsonArray(BridgeSection.Phase("Nulla", "Composta", 0, 0, 2)); break;
        }
        return d;
    }

    [DataTestMethod, DynamicData(nameof(Cases), DynamicDataSourceType.Method)]
    public void MigrationPreservesEveryResult(int id)
    {
        var actual = BridgeSection.Calculate(Input(id)).Json();
        string file = Path.Combine(AppContext.BaseDirectory, "Baselines", $"bridge-{id}.json");
        if (Environment.GetEnvironmentVariable("BRIDGE_CAPTURE_BASELINE") is string capture && capture.Length > 0)
        {
            Directory.CreateDirectory(capture);
            File.WriteAllText(Path.Combine(capture, $"bridge-{id}.json"), actual.ToJsonString(J.Options));
            return;
        }
        Compare(JsonNode.Parse(File.ReadAllText(file)), actual, "result");
    }
    private static void Compare(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected is JsonObject eo && actual is JsonObject ao)
        {
            Assert.AreEqual(eo.Count, ao.Count, path);
            foreach (var pair in eo) { Assert.IsTrue(ao.ContainsKey(pair.Key), path + "." + pair.Key); Compare(pair.Value, ao[pair.Key], path + "." + pair.Key); }
        }
        else if (expected is JsonArray ea && actual is JsonArray aa)
        {
            Assert.AreEqual(ea.Count, aa.Count, path);
            for (int i = 0; i < ea.Count; i++) Compare(ea[i], aa[i], path + "[" + i + "]");
        }
        else if (expected is JsonValue ev && actual is JsonValue av && ev.TryGetValue<double>(out var x) && av.TryGetValue<double>(out var y))
            Assert.AreEqual(x, y, 1e-8 + 1e-9 * Math.Abs(x), path);
        else Assert.AreEqual(expected?.ToJsonString(), actual?.ToJsonString(), path);
    }
}
