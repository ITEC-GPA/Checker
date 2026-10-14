using GPC.Design.Costs;
namespace GPC.Design.Bridges;
public static partial class BridgeConcept
{
    /// <summary>Pure repricing of measured work: no structural recalculation or mutation.</summary>
    public static CostEstimate Reprice(Result result, BridgeAssumptions assumptions, ResolvedPriceSnapshot snapshot)
    {
        foreach (var rate in snapshot.Rates.Values)
            if (rate.Includes!.Count > 0 && (rate.WorkCode is not ("pile_1" or "pile_15") || rate.Includes.Any(code => code != "rebar")))
                throw new ArgumentException("Bridge Design: inclusioni supportate solo per rebar nei prezzi pile_1/pile_15; le altre lavorazioni richiedono una scomposizione esplicita.");
        if (result.CostQuantities.Count == 0) throw new ArgumentException("Measured quantities are unavailable; recalculate the concept before repricing.");
        return CostEstimator.Evaluate(result.CostQuantities, snapshot, (decimal)assumptions.Prelims, (decimal)assumptions.Contingency);
    }

    private static Result ApplyPrices(Result result, BridgeAssumptions assumptions, ResolvedPriceSnapshot snapshot)
    {
        if (snapshot.Currency != "EUR") throw new ArgumentException("Bridge concept costs require EUR; currency conversion is explicit and external to the estimate.");
        var estimate = Reprice(result, assumptions, snapshot);
        if (!estimate.Complete) throw new ArgumentException("Computo incompleto: " + string.Join("; ", estimate.Lines.Where(l => l.Issue != CostIssue.None).Select(l => l.Quantity.WorkCode + " (" + l.Issue + ")").Distinct()));
        var quantities = new List<Quantity>();
        for (int k = 0; k < result.Quantities.Count; k++)
        {
            var original = result.Quantities[k]; string id = "q" + (k + 1);
            foreach (var line in estimate.Lines.Where(l => l.Quantity.Id == id || l.Quantity.Id.StartsWith(id + ":", StringComparison.Ordinal)))
            {
                string name = original.Item + (line.Quantity.Id.EndsWith(":piles", StringComparison.Ordinal) ? " · quota pali" : line.Quantity.Id.EndsWith(":footings", StringComparison.Ordinal) ? " · quota plinti/ancoraggi" : "");
                if (line.IncludedBy is not null) name += " · compresa nel prezzo dei pali";
                double amount = (double)line.Quantity.Amount;
                quantities.Add(original with { Item = name, Amount = amount, Rate = (double)(line.UnitPrice ?? 0), Cost = (double)line.Amount!.Value,
                    Carbon = original.Carbon * amount / original.Amount });
            }
        }
        double direct = (double)estimate.DirectCost!.Value, total = (double)estimate.TotalCost!.Value;
        return result with { Quantities = quantities, DirectCost = direct, TotalCost = total,
            CostLow = total * (1 - assumptions.Uncertainty / 100), CostHigh = total * (1 + assumptions.Uncertainty / 100),
            Details = result.Details.Select(d => d.Name == "Costo diretto" ? d with { Value = direct } : d.Name == "Costo totale indicativo" ? d with { Value = total } : d).ToArray(),
            PriceRevision = snapshot.Revision };
    }
    private static double BearingRate(BridgeModel data)
    {
        if (data.Prices is null) return data.Rates.Bearing;
        if (!data.Prices.Rates.TryGetValue("bearing", out var rate) || !rate.Price.HasValue || rate.Unit != QuantityUnit.Item) return 0;
        return (double)rate.Price.Value;
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
