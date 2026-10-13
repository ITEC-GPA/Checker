using GPC.Design.Costs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace DesignTests;
[TestClass]
public sealed class CostEstimateTests
{
    [TestMethod]
    public void RepricingPreservesQuantitiesAndUsesCompatibleUnits()
    {
        CostQuantity[] quantities = [new("steel", "rebar", 2, QuantityUnit.Tonne), new("slab", "concrete", 3, QuantityUnit.CubicMetre)];
        var first = new ResolvedPriceSnapshot("A-2026", "EUR", [new("rebar", QuantityUnit.Kilogram, 1.5m, "A/1"), new("concrete", QuantityUnit.CubicMetre, 200m, "A/2")]);
        var second = new ResolvedPriceSnapshot("B-2026", "EUR", [new("rebar", QuantityUnit.Tonne, 1800m, "B/1"), new("concrete", QuantityUnit.CubicMetre, 250m, "B/2")]);
        var a = CostEstimator.Evaluate(quantities, first, 10, 20); var b = CostEstimator.Evaluate(quantities, second);
        Assert.AreEqual(3600m, a.DirectCost); Assert.AreEqual(4752m, a.TotalCost); Assert.AreEqual(4350m, b.DirectCost);
        CollectionAssert.AreEqual(quantities, a.Lines.Select(l => l.Quantity).ToArray());
        Assert.AreEqual(.002m, CostEstimator.ConvertPrice(2, QuantityUnit.Tonne, QuantityUnit.Kilogram));
    }
    [TestMethod]
    public void MissingAndInvalidPricesCannotBecomeFreeWorks()
    {
        CostQuantity[] q = [new("1", "a", 1, QuantityUnit.Metre), new("2", "b", 2, QuantityUnit.Tonne), new("3", "c", 3, QuantityUnit.Item)];
        var snapshot = new ResolvedPriceSnapshot("A", "EUR", [new("b", QuantityUnit.Tonne, null, "b"), new("c", QuantityUnit.Metre, 0, "c")]);
        var r = CostEstimator.Evaluate(q, snapshot);
        Assert.IsFalse(r.Complete); Assert.IsNull(r.TotalCost);
        CollectionAssert.AreEqual(new[]{CostIssue.MissingRate, CostIssue.MissingPrice, CostIssue.IncompatibleUnit}, r.Lines.Select(l => l.Issue).ToArray());
        var zero = CostEstimator.Evaluate([q[0]], new("B", "EUR", [new("a", QuantityUnit.Metre, 0, "explicit free rate")]));
        Assert.IsTrue(zero.Complete); Assert.AreEqual(0m, zero.TotalCost);
    }
    [TestMethod]
    public void InclusionsAreOwnedScopedAndDoNotHideUnpricedParents()
    {
        string[] includes = ["rebar"]; var snapshot = new ResolvedPriceSnapshot("A", "EUR", [new("pile", QuantityUnit.Metre, 100, "pile", includes)]);
        includes[0] = "changed";
        CostQuantity[] q = [new("pile-1", "pile", 10, QuantityUnit.Metre), new("rebar-pile-1", "rebar", 1, QuantityUnit.Tonne, "pile-1"), new("rebar-footing", "rebar", 2, QuantityUnit.Tonne)];
        var result = CostEstimator.Evaluate(q, snapshot);
        Assert.AreEqual("pile-1", result.Lines[1].IncludedBy); Assert.AreEqual(0m, result.Lines[1].Amount);
        Assert.AreEqual(CostIssue.MissingRate, result.Lines[2].Issue); Assert.IsFalse(result.Complete);
        var bad = CostEstimator.Evaluate(q, new("B", "EUR", [new("pile", QuantityUnit.Metre, null, "pile", ["rebar"])]));
        Assert.AreEqual(CostIssue.InvalidInclusion, bad.Lines[1].Issue);
        Assert.ThrowsException<ArgumentException>(() => CostEstimator.Evaluate([q[0] with { InclusionOwner = "rebar-pile-1" }, q[1]], snapshot));
        Assert.ThrowsException<ArgumentException>(() => CostEstimator.Evaluate([q[0] with { InclusionOwner = "missing" }], snapshot));
    }
    [TestMethod]
    public void SnapshotRejectsAmbiguousOrNegativeRates()
    {
        Assert.ThrowsException<ArgumentException>(() => new ResolvedPriceSnapshot("A", "EUR", [new("a", QuantityUnit.Item, -1, "a")]));
        Assert.ThrowsException<ArgumentException>(() => new ResolvedPriceSnapshot("A", "EUR", [new("a", QuantityUnit.Item, 1, "a"), new("a", QuantityUnit.Item, 2, "b")]));
        Assert.ThrowsException<ArgumentException>(() => CostEstimator.ConvertPrice(1, QuantityUnit.Metre, QuantityUnit.CubicMetre));
    }
}
