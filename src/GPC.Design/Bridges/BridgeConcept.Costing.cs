using GPC.Design.Costs;
namespace GPC.Design.Bridges;
public static partial class BridgeConcept
{
    /// <summary>Pure repricing of measured work: no structural recalculation or mutation.</summary>
    public static CostEstimate Reprice(Result result, BridgeAssumptions assumptions, ResolvedPriceSnapshot snapshot)
    {
        if (result.CostQuantities.Count == 0) throw new ArgumentException("Measured quantities are unavailable; recalculate the concept before repricing.");
        return CostEstimator.Evaluate(result.CostQuantities, snapshot, (decimal)assumptions.Prelims, (decimal)assumptions.Contingency);
    }

    public static ResolvedPriceSnapshot LegacyPrices(BridgeRates rates, string revision, string currency = "EUR") => new(revision, currency,
        Rates.Select(p => new ResolvedCostRate(p.Key, CostUnit(p.Unit[2..]), (decimal)rates.Value(p.Key), revision + "/" + p.Key)));
    private static QuantityUnit CostUnit(string unit) => unit switch
    {
        "t" => QuantityUnit.Tonne, "kg" => QuantityUnit.Kilogram, "m" => QuantityUnit.Metre,
        "m²" => QuantityUnit.SquareMetre, "m³" => QuantityUnit.CubicMetre, "cad" => QuantityUnit.Item,
        _ => throw new ArgumentException("Unknown quantity unit: " + unit)
    };
    private static void AddCostQuantity(List<CostQuantity> costs, int ordinal, string key, double amount, string unit)
        => costs.Add(new("q" + ordinal, key, (decimal)amount, CostUnit(unit)));
    private static void SplitFoundationRebar(List<CostQuantity> costs, string? pileOwner, double pileRebar)
    {
        if (pileOwner is null || pileRebar <= 0) return;
        var combined = costs[^1]; decimal steel = (decimal)pileRebar;
        costs.RemoveAt(costs.Count - 1);
        costs.Add(combined with { Id = combined.Id + ":footings", Amount = combined.Amount - steel });
        costs.Add(combined with { Id = combined.Id + ":piles", Amount = steel, InclusionOwner = pileOwner });
    }
}
