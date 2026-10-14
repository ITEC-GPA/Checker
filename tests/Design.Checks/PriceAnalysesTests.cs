using GPC.Design.Costs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace DesignTests;

/// <summary>Price analyses with expected values computed by hand (14/10/2026): general expenses on the subtotal, profit on subtotal plus
/// general expenses, overrides per analysis, nesting and refusals.</summary>
[TestClass]
public sealed class PriceAnalysesTests
{
    static readonly ResolvedCostRate[] Items =
    [
        new("cls-c25", QuantityUnit.CubicMetre, 110.00m, "listino/cls-c25"),
        new("operaio", QuantityUnit.Hour, 30.00m, "listino/operaio"),
        new("autopompa", QuantityUnit.Hour, 90.00m, "listino/autopompa"),
        new("casseri", QuantityUnit.SquareMetre, 25.00m, "listino/casseri"),
        new("senza-prezzo", QuantityUnit.Item, null, "listino/senza-prezzo"),
    ];
    static readonly PriceAnalysis CastConcrete = new("cls-opera", QuantityUnit.CubicMetre,
        [new("cls-c25", 1.05m), new("operaio", 0.80m), new("autopompa", 0.10m)]);
    static readonly PriceAnalysis Column = new("pilastro", QuantityUnit.CubicMetre, [new("cls-opera", 1m), new("casseri", 8m)], GeneralExpensesPercent: 13m);

    [TestMethod]
    public void UnitPriceShowsSubtotalGeneralExpensesAndProfit()
    {
        var cast = PriceAnalyses.Resolve(Items, [CastConcrete], 15m, 10m)["cls-opera"];
        // 1,05·110 + 0,80·30 + 0,10·90 = 115,50 + 24,00 + 9,00 = 148,50; SG 15 % = 22,275; utile 10 % di 170,775 = 17,0775
        CollectionAssert.AreEqual(new[] { 115.50m, 24.00m, 9.00m }, cast.Lines.Select(l => l.Amount).ToArray());
        Assert.AreEqual(148.50m, cast.Subtotal); Assert.AreEqual(15m, cast.GeneralExpensesPercent); Assert.AreEqual(22.275m, cast.GeneralExpenses);
        Assert.AreEqual(10m, cast.ProfitPercent); Assert.AreEqual(17.0775m, cast.Profit); Assert.AreEqual(187.8525m, cast.UnitPrice);
        Assert.IsFalse(cast.GeneralExpensesOverridden || cast.ProfitOverridden);
        Assert.AreEqual(QuantityUnit.Hour, cast.Lines[1].Unit);
    }

    [TestMethod]
    public void NestedAnalysisUsesTheExactPriceAndItsOwnOverride()
    {
        var all = PriceAnalyses.Resolve(Items, [Column, CastConcrete], 15m, 10m);
        var column = all["pilastro"];
        // 187,8525 + 8·25 = 387,8525; SG 13 % = 50,420825; utile 10 % di 438,273325 = 43,8273325; prezzo 482,1006575
        Assert.AreEqual(387.8525m, column.Subtotal); Assert.AreEqual(50.420825m, column.GeneralExpenses);
        Assert.AreEqual(43.8273325m, column.Profit); Assert.AreEqual(482.1006575m, column.UnitPrice);
        Assert.IsTrue(column.GeneralExpensesOverridden); Assert.IsFalse(column.ProfitOverridden);
        Assert.AreEqual(187.8525m, column.Lines[0].UnitPrice);
        // The resolved analysis prices measured work like any item
        var snapshot = new ResolvedPriceSnapshot("r1", "EUR", Items.Append(all["cls-opera"].Rate("analisi/cls-opera")));
        var estimate = CostEstimator.Evaluate([new CostQuantity("q1", "cls-opera", 2m, QuantityUnit.CubicMetre)], snapshot);
        Assert.AreEqual(375.705m, estimate.DirectCost);
    }

    [TestMethod]
    public void InvalidAnalysesAreRefused()
    {
        PriceAnalysis A(string code, params (string Code, decimal Q)[] parts) => new(code, QuantityUnit.Item, parts.Select(p => new AnalysisComponent(p.Code, p.Q)).ToArray());
        void Refused(IEnumerable<PriceAnalysis> analyses, decimal g = 15m, decimal u = 10m) =>
            Assert.ThrowsException<ArgumentException>(() => PriceAnalyses.Resolve(Items, analyses, g, u));
        Refused([A("a", ("b", 1)), A("b", ("a", 1))]);                      // circular reference
        Refused([A("a", ("a", 1))]);                                          // self reference
        Refused([A("a", ("manca", 1))]);                                      // missing component
        Refused([A("a", ("senza-prezzo", 1))]);                               // component without price
        Refused([A("operaio", ("casseri", 1))]);                             // code of an item
        Refused([A("a", ("casseri", 1)), A("a", ("casseri", 2))]);           // code used twice
        Refused([A("a", ("casseri", 0))]);                                    // non-positive quantity
        Refused([A("a", ("casseri", 1), ("casseri", 2))]);                   // repeated component
        Refused([new("a", QuantityUnit.Item, [])]);                           // no components
        Refused([A("a", ("casseri", 1))], g: 120m);                          // percentage outside 0–100
        Refused([A("a", ("casseri", 1)) with { ProfitPercent = -1m }]);
        Assert.AreEqual(25m, PriceAnalyses.Resolve(Items, [A("a", ("casseri", 1))], 0m, 0m)["a"].UnitPrice);
    }

    [TestMethod]
    public void HoursConvertOnlyToHours()
    {
        Assert.AreEqual(30m, CostEstimator.ConvertPrice(30m, QuantityUnit.Hour, QuantityUnit.Hour));
        Assert.ThrowsException<ArgumentException>(() => CostEstimator.ConvertPrice(30m, QuantityUnit.Hour, QuantityUnit.Item));
        var snapshot = new ResolvedPriceSnapshot("r1", "EUR", Items);
        var line = CostEstimator.Evaluate([new CostQuantity("q1", "operaio", 2m, QuantityUnit.Item)], snapshot).Lines[0];
        Assert.AreEqual(CostIssue.IncompatibleUnit, line.Issue);
    }
}
