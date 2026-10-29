using GPC.Design.Bridges;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO.Compression;
using System.Text.Json.Nodes;
namespace DesignTests;

/// <summary>
/// D8 (ANTHEA census of the combinations, decision of the user of 29/10/2026: «d8 e bridge design devono usare model»): the ultimate envelopes
/// with the rows of NTC 2018 Tab. 5.1.V, set A1, from GPC.Model: γG1 1.35/1.00 on the structural permanent actions, γG2 1.50/0 on surfacing and
/// barriers, γQ 1.35/0 on the traffic. Expected values from the formulas of a simply supported span, M = w L² / 8 and R = w L / 2, written by hand.
/// </summary>
[TestClass]
public sealed class BridgeProfileFactorTests
{
    private static BridgeModel SingleSpan(TrafficModel traffic, PartialFactorSource source)
    {
        using var stream = new GZipStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures/bridge-f4.json.gz")), CompressionMode.Decompress);
        var model = JsonNode.Parse(stream)!["cases"]!.AsArray().Select(c => BridgeConceptTests.Input(c!["input"]!))
            .First(m => !BridgeConcept.HasUpperStructure(m.Input.Family));
        return model with { Input = model.Input with { Spans = 1, Length = 30, Obstacle = "Nessuno", Continuous = false, TrafficModel = traffic, PartialFactors = source } };
    }

    private static double Detail(BridgeConcept.Result r, string name) => r.Details.Single(d => d.Name == name).Value;

    [TestMethod]
    public void UniformTrafficEnvelopesUseTheRowsOfTable51V()
    {
        var r = BridgeConcept.Calculate(SingleSpan(TrafficModel.LegacyUniform, PartialFactorSource.Ntc2018RoadBridges));
        double l = r.Spans.Single(), g = Detail(r, "Permanenti sull'intero impalcato"), g2 = Detail(r, "Permanenti non strutturali G2"), g1 = g - g2;
        double q = Detail(r, "Traffico equivalente sull'intero impalcato");
        var mid = r.Stations.Single(s => Math.Abs(s.X - l / 2) < 1e-9);
        // Maximum: every action unfavourable; minimum: G1 favourable 1.00, G2 and traffic 0.
        Assert.AreEqual((1.35 * g1 + 1.5 * g2 + 1.35 * q) * l * l / 8, mid.Moment, 1e-9 * mid.Moment);
        Assert.AreEqual(1.0 * g1 * l * l / 8, mid.MomentMinimum!.Value, 1e-9 * mid.Moment);
        var start = r.Stations.First();
        Assert.AreEqual((1.35 * g1 + 1.5 * g2 + 1.35 * q) * l / 2, start.Shear, 1e-9 * start.Shear);
        Assert.AreEqual(1.0 * g1 * l / 2, start.ShearMinimum!.Value, 1e-9 * start.Shear);
        Assert.IsTrue(r.Details.Single(d => d.Name == "Momento positivo indicativo").Rule.Contains("GPC.Model"));
    }

    [TestMethod]
    public void Schema1EnvelopesAddTheNonStructuralFactorAndTheFavourableRows()
    {
        var profile = BridgeConcept.Calculate(SingleSpan(TrafficModel.Ntc2018Schema1, PartialFactorSource.Ntc2018RoadBridges));
        var multipliers = SingleSpan(TrafficModel.Ntc2018Schema1, PartialFactorSource.ProjectMultipliers);
        var project = BridgeConcept.Calculate(multipliers with { Assumptions = multipliers.Assumptions with { GammaG = 1.35, GammaQ = 1.35 } });
        double l = profile.Spans.Single(), g = Detail(profile, "Permanenti sull'intero impalcato"), g2 = Detail(profile, "Permanenti non strutturali G2"), g1 = g - g2;
        var a = profile.Stations.Single(s => Math.Abs(s.X - l / 2) < 1e-9); var b = project.Stations.Single(s => Math.Abs(s.X - l / 2) < 1e-9);
        // Same traffic envelope (positive at midspan of a simple span): the maximum differs by (1.50 − 1.35) G2 L²/8; the minimum has the permanent
        // actions favourable, 1.00 G1 instead of 1.35 (G1 + G2), and no traffic (its minimum at midspan is 0).
        Assert.AreEqual(b.Moment + .15 * g2 * l * l / 8, a.Moment, 1e-9 * a.Moment);
        Assert.AreEqual(g1 * l * l / 8, a.MomentMinimum!.Value, 1e-9 * a.Moment);
        Assert.AreEqual(1.35, profile.Traffic!.GammaG); Assert.AreEqual(1.35, profile.Traffic.GammaQ);
        Assert.IsTrue(profile.Warnings.Any(w => w.Contains("γG2 1,50/0")));
        // The service envelope (deflections, supports) does not depend on the partial factors.
        CollectionAssert.AreEqual(project.Supports.Select(s => s.MinimumReaction).ToArray(), profile.Supports.Select(s => s.MinimumReaction).ToArray());
    }

    [TestMethod]
    public void ProjectMultipliersRemainTheDefaultOfTheInput()
    {
        // An input without the choice (archives, older callers) keeps the multipliers of the project and the results of GPC.Design 0.41.
        using var stream = new GZipStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures/bridge-f4.json.gz")), CompressionMode.Decompress);
        var model = BridgeConceptTests.Input(JsonNode.Parse(stream)!["cases"]!.AsArray()[0]!["input"]!);
        Assert.AreEqual(PartialFactorSource.ProjectMultipliers, model.Input.PartialFactors);
        var r = BridgeConcept.Calculate(model);
        Assert.IsFalse(r.Details.Any(d => d.Name == "Permanenti non strutturali G2"));
    }
}
