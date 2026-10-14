using GPC.Design.Bridges;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;
namespace DesignTests;

[TestClass]
public sealed class BridgeTrafficTests
{
    private static BridgeModel[] Models()
    {
        using var stream = new GZipStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures/bridge-f4.json.gz")), CompressionMode.Decompress);
        return JsonNode.Parse(stream)!["cases"]!.AsArray().Select(c => BridgeConceptTests.Input(c!["input"]!)).ToArray();
    }
    [TestMethod]
    public void BeamFamiliesUseSeparateReactionAndSectionEnvelopesWithoutChangingDeckGeometry()
    {
        foreach (var model in Models().Where(m => !BridgeConcept.HasUpperStructure(m.Input.Family)))
        {
            var baseline = BridgeConcept.Calculate(model);
            var updated = BridgeConcept.Calculate(model with { Input = model.Input with { TrafficModel = TrafficModel.Ntc2018Schema1 }, Assumptions = model.Assumptions with { GammaQ = 1.35 } });
            Assert.AreEqual(baseline.Inertia, updated.Inertia); Assert.AreEqual(baseline.Depth, updated.Depth);
            Assert.IsNotNull(updated.Traffic); Assert.AreEqual(1.35, updated.Traffic.GammaQ);
            Assert.AreEqual(41 * updated.Spans.Count, updated.Stations.Count);
            Assert.IsTrue(updated.Stations.All(s => s.MomentMinimum <= s.Moment + 1e-8 && s.ShearMinimum <= s.Shear + 1e-8));
            Assert.IsTrue(updated.Supports.All(s => s.MinimumReaction <= s.Reaction + 1e-8));
            foreach (var q in baseline.Quantities.Where(q => q.Group == "Impalcato"))
                Assert.AreEqual(q.Amount, updated.Quantities.Single(u => u.Group == q.Group && u.Item == q.Item).Amount);
            double negative = updated.Details.Single(d => d.Name == "Momento negativo indicativo").Value;
            Assert.AreEqual(Math.Min(0, updated.Stations.Min(s => s.MomentMinimum!.Value)), negative);
            Assert.IsTrue(updated.Warnings.Any(w => w.Contains("non concomitanti")));
        }
        var bad = Models()[0];
        Assert.ThrowsException<ArgumentException>(() => BridgeConcept.Calculate(bad with { Input = bad.Input with { TrafficModel = (TrafficModel)99 } }));
    }
    [TestMethod]
    public void UpperStructuresUseLocalTandemsAndExposeTheirConceptualBounds()
    {
        foreach (var model in Models().Where(m => BridgeConcept.HasUpperStructure(m.Input.Family)))
        {
            var old = BridgeConcept.Calculate(model);
            var ntc = model with { Input = model.Input with { TrafficModel = TrafficModel.Ntc2018Schema1 }, Assumptions = model.Assumptions with { GammaQ = 1.35 } };
            var r = BridgeConcept.Calculate(ntc);
            Assert.IsNotNull(r.Traffic); Assert.IsTrue(r.Traffic.Scope.Contains("non analisi"));
            Assert.AreEqual(0, r.Stations.Count, "Do not fabricate beam diagrams for the real upper structure.");
            Assert.IsTrue(r.Advanced!.Members.All(m => double.IsFinite(m.Force) && double.IsFinite(m.Mass)));
            Assert.IsTrue(r.Supports.All(p => p.MinimumReaction <= p.Reaction));
            Assert.AreEqual(old.Inertia, r.Inertia); Assert.AreEqual(old.Depth, r.Depth);
            var hanger = r.Advanced.Members.FirstOrDefault(m => m.Kind == "Pendino");
            if (hanger is not null && model.Input.Family == "tied_arch")
            {
                double span = r.Spans[0], panel = span / Math.Max(4, Math.Ceiling(span / model.Input.SuspenderSpacing));
                double permanent = r.Details.Single(d => d.Name == "Permanenti sull'intero impalcato").Value;
                double udl = r.Details.Single(d => d.Name == "Traffico sul piano esterno · distribuito").Value;
                double tandem = r.Details.Single(d => d.Name == "Traffico sul piano esterno · tandem").Value;
                double force = ntc.Assumptions.GammaG * permanent * panel / 2 + 1.35 * (udl * panel + tandem * (1 - .6 / panel));
                Assert.AreEqual(force, hanger.Force, force * 2e-8, "Local tandem was diluted over the bridge length.");
            }
            Console.WriteLine($"{model.Input.Family}: cost {old.TotalCost:F2} -> {r.TotalCost:F2}; CO2 {old.Carbon:F3} -> {r.Carbon:F3}");
        }
    }
    [TestMethod]
    public void TrafficSearchIsReproducibleAndCachedAcrossGeometricallyEquivalentCandidates()
    {
        var model = Models().First(m => !BridgeConcept.HasUpperStructure(m.Input.Family));
        var options = new BridgeConcept.OptimizationOptions { KeepFamily = true, KeepFoundation = true, KeepPier = true, MinSpans = 2, MaxSpans = 6 };
        var watch = Stopwatch.StartNew(); var legacy = BridgeConcept.Optimize(model, options); double legacyMs = watch.Elapsed.TotalMilliseconds;
        var ntc = model with { Input = model.Input with { TrafficModel = TrafficModel.Ntc2018Schema1 }, Assumptions = model.Assumptions with { GammaQ = 1.35 } };
        watch.Restart(); var first = BridgeConcept.Optimize(ntc, options); double firstMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart(); var second = BridgeConcept.Optimize(ntc, options); double secondMs = watch.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Search {legacy.Evaluated}/{first.Evaluated} candidates: legacy {legacyMs:F2} ms, NTC cold {firstMs:F2} ms, warm {secondMs:F2} ms");
        Assert.AreEqual(first.Evaluated, second.Evaluated); Assert.AreEqual(first.Admissible, second.Admissible);
        var broad = options with { KeepFamily = false, KeepFoundation = false, KeepPier = false, KeepContinuity = false, MaxSpans = 12 };
        watch.Restart(); var fullLegacy = BridgeConcept.Optimize(model, broad); double broadLegacy = watch.Elapsed.TotalMilliseconds;
        watch.Restart(); var full = BridgeConcept.Optimize(ntc, broad);
        Console.WriteLine($"Full grid: {fullLegacy.Evaluated}/{full.Evaluated} candidates, {full.Admissible} admitted; legacy {broadLegacy:F2} ms, NTC {watch.Elapsed.TotalMilliseconds:F2} ms");
        Assert.IsTrue(full.Evaluated > 100);
        foreach (var pair in first.Candidates.Zip(second.Candidates))
        {
            Assert.AreEqual(pair.First.Data, pair.Second.Data);
            Assert.AreEqual(pair.First.Result.TotalCost, pair.Second.Result.TotalCost);
            Assert.AreEqual(TrafficModel.Ntc2018Schema1, pair.First.Data.Input.TrafficModel);
            Assert.AreEqual(pair.First.Result.TotalCost, BridgeConcept.Calculate(pair.First.Data).TotalCost);
        }
    }
}
