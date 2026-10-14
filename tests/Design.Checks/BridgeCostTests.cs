using GPC.Design.Bridges;
using GPC.Design.Costs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO.Compression;
using System.Text.Json.Nodes;
namespace DesignTests;
[TestClass]
public sealed class BridgeCostTests
{
    [TestMethod]
    public void EveryOptimizationCandidateUsesOnePriceRevisionAndMissingPricesAreRejected()
    {
        using var stream = new GZipStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures/bridge-f4.json.gz")), CompressionMode.Decompress);
        var input = BridgeConceptTests.Input(JsonNode.Parse(stream)!["cases"]![0]!["input"]!);
        var basePrices = BridgeConcept.LegacyPrices(input.Rates, "A");
        Assert.ThrowsException<ArgumentException>(() => BridgeConcept.Reprice(BridgeConcept.Calculate(input), input.Assumptions,
            new ResolvedPriceSnapshot("unsupported inclusion", "EUR", basePrices.Rates.Values.Select(r => r.WorkCode == "steel" ? r with { Includes = new[]{"erection_special"} } : r))));
        var prices = new ResolvedPriceSnapshot("B", "EUR", basePrices.Rates.Values.Select(r => r with { Price = r.Price * 2 }));
        var options = new BridgeConcept.OptimizationOptions { KeepFamily = true, KeepFoundation = true, KeepPier = true, MinSpans = 2, MaxSpans = 4 };
        var before = BridgeConcept.Optimize(input, options); var after = BridgeConcept.Optimize(input with { Prices = prices }, options);
        Assert.AreEqual(before.Evaluated, after.Evaluated); Assert.AreEqual(before.Admissible, after.Admissible);
        Assert.AreEqual(before.Candidates.Length, after.Candidates.Length);
        foreach (var pair in before.Candidates.Zip(after.Candidates))
        {
            Assert.AreEqual(pair.First.Data.Input, pair.Second.Data.Input);
            Assert.AreEqual(pair.First.Result.TotalCost * 2, pair.Second.Result.TotalCost, 1e-10 * pair.Second.Result.TotalCost);
            Assert.AreEqual("B", pair.Second.Result.PriceRevision);
            Assert.AreEqual(pair.Second.Result.TotalCost, BridgeConcept.Calculate(pair.Second.Data).TotalCost);
            Assert.AreEqual(pair.Second.Result.DirectCost, pair.Second.Result.Quantities.Sum(q => q.Cost), 1e-10 * pair.Second.Result.DirectCost);
        }
        Assert.ThrowsException<ArgumentException>(() => BridgeConcept.Optimize(input with { Prices = new("empty", "EUR", []) }, options));
        Assert.ThrowsException<ArgumentException>(() => BridgeConcept.Calculate(input with { Prices = new("USD", "USD", basePrices.Rates.Values) }));
    }
    [TestMethod]
    public void MeasuredWorkRepricesAllFrozenFamiliesWithoutChangingCarbonOrStructure()
    {
        using var stream = new GZipStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures/bridge-f4.json.gz")), CompressionMode.Decompress);
        foreach (var row in JsonNode.Parse(stream)!["cases"]!.AsArray())
        {
            var input = BridgeConceptTests.Input(row!["input"]!); var result = BridgeConcept.Calculate(input);
            Assert.IsTrue(result.CostQuantities.Count > 0);
            var prices = BridgeConcept.LegacyPrices(input.Rates, "frozen");
            var cost = BridgeConcept.Reprice(result, input.Assumptions, prices);
            Assert.IsTrue(cost.Complete, row["id"]!.ToString());
            Assert.ThrowsException<ArgumentException>(() => BridgeConcept.Reprice(result with { CostQuantities = [] }, input.Assumptions, prices));
            Assert.AreEqual(result.DirectCost, (double)cost.DirectCost!.Value, 1e-10 * result.DirectCost);
            Assert.AreEqual(result.TotalCost, (double)cost.TotalCost!.Value, 1e-10 * result.TotalCost);
            var expensive = new ResolvedPriceSnapshot("double", "EUR", prices.Rates.Values.Select(r => r with { Price = r.Price * 2 }));
            var twice = BridgeConcept.Reprice(result, input.Assumptions, expensive);
            Assert.AreEqual(cost.TotalCost * 2, twice.TotalCost);
            var included = new ResolvedPriceSnapshot("pile includes steel", "EUR", prices.Rates.Values.Select(r => r.WorkCode.StartsWith("pile_") ? r with { Includes = new[]{"rebar"} } : r));
            var inclusive = BridgeConcept.Reprice(result, input.Assumptions, included);
            Assert.IsTrue(inclusive.Complete); Assert.IsTrue(inclusive.TotalCost <= cost.TotalCost);
            decimal expectedSaving = result.CostQuantities.Where(q => q.InclusionOwner is not null).Sum(q => q.Amount * (decimal)input.Rates.Rebar);
            Assert.AreEqual(expectedSaving, cost.DirectCost - inclusive.DirectCost);
            Assert.IsTrue(result.CostQuantities.All(q => q.Amount >= 0));
        }
    }
}
