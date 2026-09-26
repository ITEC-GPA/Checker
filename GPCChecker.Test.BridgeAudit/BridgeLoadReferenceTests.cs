using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X.Core;

namespace BridgeAudit;

[TestClass, TestCategory("BridgeLoadReference"), DoNotParallelize]
public class BridgeLoadReferenceTests
{
    static JsonObject Data(string kind, string reference, bool effective = false, double force = -350, double moment = 1200)
    {
        var d = BridgeSection.Defaults(); d["classe4"] = effective;
        d["fasi"] = new JsonArray(BridgeSection.Phase("Fase", kind, force, moment, 2, 1.1, reference)); return d;
    }
    static void Near(double a, double b, string label, double tolerance = 1e-7) => Assert.AreEqual(b, a, tolerance * Math.Max(1, Math.Abs(b)), label);
    static double IndependentCentroid(JsonObject d, string kind)
    {
        var g = BridgeSection.Geometry(d); var materials = BridgeSection.Materials(d);
        // the real bottom plates (CompositeBridge 1.1: no more equivalent rectangle)
        var parts = new List<(double A, double Y)>
        {
            (g.TopWidth * g.TopThickness, -g.TopThickness / 2),
            (g.WebThickness * g.WebHeight, -g.TopThickness - g.WebHeight / 2),
            (g.Bottom1Width * g.Bottom1Thickness, -g.TopThickness - g.WebHeight - g.Bottom1Thickness / 2)
        };
        if (g.Bottom2Thickness > 0) parts.Add((g.Bottom2Width * g.Bottom2Thickness, -g.Height + g.Bottom2Thickness / 2));
        double ratio = materials.Rebar.ElasticModulusTension / materials.Steel.ElasticModulusTension;
        double n = materials.Steel.ElasticModulusTension / materials.Concrete.ElasticModulusCompression * (1 + 1.1 * 2);
        if (kind == "Composta") parts.Add((g.Width * g.SlabHeight / n, g.SlabHeight / 2));
        if (kind != "Solo acciaio") parts.AddRange(g.Bars.Select(b => (b.Area * (ratio - (kind == "Composta" ? 1 / n : 0)), b.Y)));
        return parts.Sum(p => p.A * p.Y) / parts.Sum(p => p.A);
    }
    [DataTestMethod]
    [DataRow("Solo acciaio", false, false)] [DataRow("Solo acciaio", true, true)]
    [DataRow("Composta", false, false)] [DataRow("Composta", true, false)] [DataRow("Composta", false, true)] [DataRow("Composta", true, true)]
    [DataRow("Soletta esclusa", false, false)] [DataRow("Soletta esclusa", true, false)] [DataRow("Soletta esclusa", false, true)] [DataRow("Soletta esclusa", true, true)]
    public void GrossPointMatchesIndependentTransformedAreas(string kind, bool topBars, bool bottomBars)
    {
        var d = Data(kind, BridgeSection.GrossLoadReference); d["rebars_top"] = topBars; d["rebars_bottom"] = bottomBars; d["plate2"] = true;
        var c = BridgeSection.Calculate(d).Stages[0].Contributions[0];
        Near(c.LoadY, IndependentCentroid(d, kind), "quota N da aree omogeneizzate"); Near(c.LoadY, c.Centroid, "centratura senza classe 4");
    }
    [DataTestMethod]
    [DataRow("Solo acciaio", false)] [DataRow("Composta", false)] [DataRow("Soletta esclusa", false)]
    [DataRow("Solo acciaio", true)] [DataRow("Composta", true)] [DataRow("Soletta esclusa", true)]
    public void ReferenceMomentTransportPreservesTheSolvedField(string kind, bool effective)
    {
        var d = Data(kind, effective ? BridgeSection.EffectiveLoadReference : BridgeSection.GrossLoadReference, effective);
        var result = BridgeSection.Calculate(d); var c = result.Stages[0].Contributions[0];
        var atZero = (JsonObject)d.DeepClone(); var p = atZero.Array("fasi")[0]!.AsObject();
        p["riferimento_N"] = BridgeSection.CommonLoadReference; atZero["y_ref"] = 0; p["Mx"] = c.Mx - c.N * c.LoadY / 1000;
        var reference = BridgeSection.Calculate(atZero).Stages[0];
        foreach (var pair in result.Stages[0].Points.Zip(reference.Points)) Near(pair.First.Stress, pair.Second.Stress, pair.First.Name, 5e-6);
        Near(c.MomentAtInterface, p.D("Mx"), "momento cumulabile a y=0");
    }
    [DataTestMethod]
    [DataRow("Solo acciaio")] [DataRow("Composta")] [DataRow("Soletta esclusa")]
    public void GrossReferenceRemainsFixedWhenEffectiveCentroidMoves(string kind)
    {
        var d = Data(kind, BridgeSection.GrossLoadReference, true, moment: 0); var result = BridgeSection.Calculate(d); var c = result.Stages[0].Contributions[0];
        Near(c.LoadY, IndependentCentroid(d, kind), "punto fisso lordo"); Assert.IsTrue(Math.Abs(c.Centroid - c.LoadY) > .01, "Il caso deve attivare lo spostamento del baricentro efficace.");
        Assert.IsTrue(c.EquilibriumResidual < 1e-5);
    }
    [DataTestMethod]
    [DataRow("Solo acciaio")] [DataRow("Composta")] [DataRow("Soletta esclusa")]
    public void EffectiveReferenceFollowsEveryPhaseInEachCumulativeSituation(string kind)
    {
        var d = Data(kind, BridgeSection.EffectiveLoadReference, true);
        d.Array("fasi").Add(BridgeSection.Phase("Successiva", "Composta", -80, 450, .5, 1, BridgeSection.EffectiveLoadReference));
        var result = BridgeSection.Calculate(d);
        foreach (var stage in result.Stages) foreach (var c in stage.Contributions)
        { Near(c.LoadY, c.Centroid, "punto al baricentro corrente"); Assert.IsTrue(c.EquilibriumResidual < 1e-5); }
    }
    [TestMethod]
    public void MixedReferencesUseTheirOwnPointsAndPersistInJson()
    {
        var d = BridgeSection.Defaults(); d["y_ref"] = 120;
        var phases = d.Array("fasi").OfType<JsonObject>().ToArray();
        phases[0]["riferimento_N"] = BridgeSection.CommonLoadReference; phases[1]["riferimento_N"] = BridgeSection.GrossLoadReference; phases[2]["riferimento_N"] = BridgeSection.EffectiveLoadReference;
        foreach (var p in phases) p["N"] = -100;
        var r = BridgeSection.Calculate(d); var c = r.Stages.Last().Contributions;
        Near(c[0].LoadY, 120, "quota comune"); Near(c[1].LoadY, IndependentCentroid(d, "Composta"), "quota lorda G2"); Near(c[2].LoadY, c[2].Centroid, "quota efficace Q");
        var roundtrip = BridgeSection.Calculate(JsonNode.Parse(d.ToJsonString())!.AsObject());
        Near(roundtrip.Stages.Last().Contributions.Sum(x => x.MomentAtInterface), c.Sum(x => x.Mx - x.N * x.LoadY / 1000), "somma a riferimento comune");
        Assert.IsTrue(r.Json()["Stages"]![2]!["Contributions"]![2]!.AsObject().ContainsKey("LoadY"));
    }
    [TestMethod]
    public void OldArchiveWithoutReferenceKeepsCommonPoint()
    {
        var d = Data("Composta", BridgeSection.CommonLoadReference); d["y_ref"] = -125; d.Array("fasi")[0]!.AsObject().Remove("riferimento_N");
        var c = BridgeSection.Calculate(d).Stages[0].Contributions[0]; Near(c.LoadY, -125, "compatibilità archivio");
        Assert.AreEqual(BridgeSection.CommonLoadReference, c.LoadReference);
        Assert.IsTrue(BridgeSection.Defaults().Array("fasi").OfType<JsonObject>().All(p => BridgeSection.LoadReference(p) == BridgeSection.GrossLoadReference));
    }
    [TestMethod]
    public void InvalidReferenceIsRejectedButUnusedCommonPointIsIgnored()
    {
        var d = Data("Composta", BridgeSection.GrossLoadReference); d["y_ref"] = "invalido";
        Assert.IsNotNull(BridgeSection.Calculate(d));
        d.Array("fasi")[0]!["riferimento_N"] = BridgeSection.CommonLoadReference;
        Assert.ThrowsException<ArgumentException>(() => BridgeSection.Calculate(d));
        d.Array("fasi")[0]!["riferimento_N"] = "sconosciuto";
        Assert.ThrowsException<ArgumentException>(() => BridgeSection.Calculate(d));
    }
}
