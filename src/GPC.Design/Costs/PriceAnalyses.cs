using System.Collections.ObjectModel;

namespace GPC.Design.Costs;

/// <summary>Component of a price analysis: a priced item or another analysis, with its quantity per unit of the analysis, expressed in the
/// unit of the component (for example 0,35 h of labour per m³).</summary>
public sealed record AnalysisComponent(string Code, decimal Quantity);

/// <summary>Price analysis: unit price built from components, then general expenses and contractor's profit. Null percentages take the
/// defaults of the price list; a value given here overrides them for this analysis only.</summary>
public sealed record PriceAnalysis(string Code, QuantityUnit Unit, IReadOnlyList<AnalysisComponent> Components,
    decimal? GeneralExpensesPercent = null, decimal? ProfitPercent = null);

/// <summary>One component of a resolved analysis: quantity, unit price of the component in its unit and amount.</summary>
public sealed record AnalysisLine(string Code, decimal Quantity, QuantityUnit Unit, decimal UnitPrice, decimal Amount);

/// <summary>
/// Resolved analysis with every term of the total: subtotal of the components, general expenses on the subtotal, profit on subtotal
/// plus general expenses, unit price. No rounding: presentation rounds, nested analyses use the exact values.
/// </summary>
public sealed record AnalysisBreakdown(string Code, QuantityUnit Unit, IReadOnlyList<AnalysisLine> Lines, decimal Subtotal,
    decimal GeneralExpensesPercent, decimal GeneralExpenses, decimal ProfitPercent, decimal Profit, decimal UnitPrice,
    bool GeneralExpensesOverridden, bool ProfitOverridden)
{
    /// <summary>Resolved rate for the cost estimate, with the analysis as its source.</summary>
    public ResolvedCostRate Rate(string sourceReference) => new(Code, Unit, UnitPrice, sourceReference);
}

/// <summary>Price analyses of a price list (no catalogue, document, I/O or UI dependency).</summary>
public static class PriceAnalyses
{
    /// <summary>
    /// Resolves every analysis. A component is an item of <paramref name="items"/> (with a price) or another analysis; a missing or
    /// unpriced component, a code used twice, a circular reference, a non-positive quantity or a percentage outside 0–100 is refused.
    /// Unit price = subtotal + general expenses + profit, with general expenses = subtotal · g/100 and profit = (subtotal + general
    /// expenses) · u/100.
    /// </summary>
    public static IReadOnlyDictionary<string, AnalysisBreakdown> Resolve(IEnumerable<ResolvedCostRate> items, IEnumerable<PriceAnalysis> analyses,
        decimal generalExpensesPercent, decimal profitPercent)
    {
        Guard.NotNull(items); Guard.NotNull(analyses);
        Percent(generalExpensesPercent, "general expenses"); Percent(profitPercent, "profit");
        var priced = new Dictionary<string, ResolvedCostRate>(StringComparer.Ordinal);
        foreach (var item in items)
            if (item is null || string.IsNullOrWhiteSpace(item.WorkCode) || !priced.TryAdd(item.WorkCode, item)) throw new ArgumentException("Invalid or duplicate priced item: " + item?.WorkCode);
        var byCode = new Dictionary<string, PriceAnalysis>(StringComparer.Ordinal);
        foreach (var analysis in analyses)
        {
            if (analysis is null || string.IsNullOrWhiteSpace(analysis.Code) || !PortableEnum.IsDefined(analysis.Unit) || analysis.Components is null || analysis.Components.Count == 0)
                throw new ArgumentException("Analysis without code, unit or components: " + analysis?.Code);
            if (priced.ContainsKey(analysis.Code) || !byCode.TryAdd(analysis.Code, analysis)) throw new ArgumentException("Code used twice: " + analysis.Code);
            if (analysis.GeneralExpensesPercent is decimal g) Percent(g, "general expenses of " + analysis.Code);
            if (analysis.ProfitPercent is decimal u) Percent(u, "profit of " + analysis.Code);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var component in analysis.Components)
                if (component is null || string.IsNullOrWhiteSpace(component.Code) || component.Quantity <= 0 || !seen.Add(component.Code))
                    throw new ArgumentException("Invalid or repeated component in " + analysis.Code + ": " + component?.Code);
        }
        var resolved = new Dictionary<string, AnalysisBreakdown>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        foreach (var code in byCode.Keys) Visit(code);
        return new ReadOnlyDictionary<string, AnalysisBreakdown>(resolved);

        AnalysisBreakdown Visit(string code)
        {
            if (resolved.TryGetValue(code, out var done)) return done;
            if (!visiting.Add(code)) throw new ArgumentException("Circular reference between analyses: " + code);
            var analysis = byCode[code]; var lines = new List<AnalysisLine>(); decimal subtotal = 0;
            foreach (var component in analysis.Components)
            {
                QuantityUnit unit; decimal price;
                if (byCode.ContainsKey(component.Code)) { var nested = Visit(component.Code); unit = nested.Unit; price = nested.UnitPrice; }
                else if (priced.TryGetValue(component.Code, out var item))
                {
                    if (!PortableEnum.IsDefined(item.Unit) || item.Price is not decimal value || value < 0) throw new ArgumentException("Component without price: " + component.Code + " in " + code);
                    unit = item.Unit; price = value;
                }
                else throw new ArgumentException("Missing component: " + component.Code + " in " + code);
                decimal amount = checked(component.Quantity * price);
                lines.Add(new(component.Code, component.Quantity, unit, price, amount)); subtotal = checked(subtotal + amount);
            }
            decimal g = analysis.GeneralExpensesPercent ?? generalExpensesPercent, u = analysis.ProfitPercent ?? profitPercent;
            decimal expenses = checked(subtotal * g / 100), profit = checked((subtotal + expenses) * u / 100);
            var breakdown = new AnalysisBreakdown(code, analysis.Unit, lines.AsReadOnly(), subtotal, g, expenses, u, profit, checked(subtotal + expenses + profit),
                analysis.GeneralExpensesPercent.HasValue, analysis.ProfitPercent.HasValue);
            visiting.Remove(code); resolved[code] = breakdown; return breakdown;
        }
    }

    private static void Percent(decimal value, string name)
    {
        if (value < 0 || value > 100) throw new ArgumentException("Percentage of " + name + " outside 0–100.");
    }
}
