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
