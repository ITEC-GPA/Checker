using System.Collections.ObjectModel;

namespace GPC.Design.Costs;

/// <summary>Units of measured work and prices; Hour (labour and plant hire in price analyses) converts only to itself.</summary>
public enum QuantityUnit { Item, Metre, SquareMetre, CubicMetre, Kilogram, Tonne, Hour }
public enum CostIssue { None, MissingRate, MissingPrice, IncompatibleUnit, InvalidInclusion }

/// <summary>A measured quantity. InclusionOwner is the id of the specific enclosing work, never a global material exemption.</summary>
public sealed record CostQuantity(string Id, string WorkCode, decimal Amount, QuantityUnit Unit, string? InclusionOwner = null);
public sealed record ResolvedCostRate(string WorkCode, QuantityUnit Unit, decimal? Price, string SourceReference,
    IReadOnlyList<string>? Includes = null);
public sealed record CostLine(CostQuantity Quantity, decimal? UnitPrice, decimal? Amount, CostIssue Issue, string? IncludedBy);
public sealed record CostEstimate(string PriceRevision, string Currency, IReadOnlyList<CostLine> Lines,
    decimal? DirectCost, decimal? TotalCost)
{
    public bool Complete => DirectCost.HasValue && TotalCost.HasValue;
}

/// <summary>Owned immutable resolved rates; no catalogue, document, I/O or UI dependency.</summary>
public sealed class ResolvedPriceSnapshot
{
    public string Revision { get; }
    public string Currency { get; }
    public IReadOnlyDictionary<string, ResolvedCostRate> Rates { get; }
    public ResolvedPriceSnapshot(string revision, string currency, IEnumerable<ResolvedCostRate> rates)
    {
        if (string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Price revision and currency are required.");
        Revision = revision; Currency = currency;
        var owned = new Dictionary<string, ResolvedCostRate>(StringComparer.Ordinal);
        foreach (var rate in rates)
        {
            if (string.IsNullOrWhiteSpace(rate.WorkCode) || !Enum.IsDefined(rate.Unit) || rate.Price is < 0 || string.IsNullOrWhiteSpace(rate.SourceReference))
                throw new ArgumentException("Invalid resolved price.");
            var includes = (rate.Includes ?? []).ToArray();
            if (includes.Any(string.IsNullOrWhiteSpace) || includes.Contains(rate.WorkCode) || includes.Distinct(StringComparer.Ordinal).Count() != includes.Length)
                throw new ArgumentException("Invalid included works.");
            if (!owned.TryAdd(rate.WorkCode, rate with { Includes = Array.AsReadOnly(includes) })) throw new ArgumentException("Duplicate resolved work: " + rate.WorkCode);
        }
        Rates = new ReadOnlyDictionary<string, ResolvedCostRate>(owned);
    }
}

public static class CostEstimator
{
    /// <summary>Conversion restricted to compatible dimensions. Rates are expressed per rate unit.</summary>
    public static decimal ConvertPrice(decimal price, QuantityUnit from, QuantityUnit to)
    {
        if (price < 0 || !Enum.IsDefined(from) || !Enum.IsDefined(to)) throw new ArgumentException("Invalid price or unit.");
        if (from == to) return price;
        return (from, to) switch
        {
            (QuantityUnit.Kilogram, QuantityUnit.Tonne) => checked(price * 1000m),
            (QuantityUnit.Tonne, QuantityUnit.Kilogram) => price / 1000m,
            _ => throw new ArgumentException("Incompatible price units.")
        };
    }
    public static CostEstimate Evaluate(IEnumerable<CostQuantity> quantities, ResolvedPriceSnapshot snapshot,
        decimal additionalPercent = 0, decimal contingencyPercent = 0)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (additionalPercent < 0 || contingencyPercent < 0) throw new ArgumentOutOfRangeException(nameof(additionalPercent));
        var input = quantities.ToArray(); var byId = new Dictionary<string, CostQuantity>(StringComparer.Ordinal);
        foreach (var item in input)
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.WorkCode) || item.Amount < 0 || !Enum.IsDefined(item.Unit) || !byId.TryAdd(item.Id, item))
                throw new ArgumentException("Invalid or duplicate measured work.");
        // Validate graph before pricing, including zero-quantity lines. Cycles must not make every item free.
        foreach (var item in input)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { item.Id }; var current = item;
            while (current.InclusionOwner is string owner)
            {
                if (!byId.TryGetValue(owner, out var parent) || !visited.Add(owner)) throw new ArgumentException("Missing or cyclic inclusion owner.");
                current = parent;
            }
        }
        var lines = input.Select(EvaluateLine).ToArray();
        decimal? direct = lines.Any(l => l.Issue != CostIssue.None) ? null : lines.Sum(l => l.Amount!.Value);
        decimal? total = direct.HasValue ? checked(direct.Value * (1 + additionalPercent / 100) * (1 + contingencyPercent / 100)) : null;
        return new(snapshot.Revision, snapshot.Currency, Array.AsReadOnly(lines), direct, total);

        CostLine EvaluateLine(CostQuantity item)
        {
            if (item.Amount == 0) return new(item, null, 0, CostIssue.None, null);
            if (item.InclusionOwner is string owner)
            {
                var parent = byId[owner];
                if (snapshot.Rates.TryGetValue(parent.WorkCode, out var parentRate) && parentRate.Includes!.Contains(item.WorkCode))
                {
                    // A claimed inclusion cannot hide a missing, zero-quantity or dimensionally incompatible parent.
                    if (parent.Amount <= 0 || !parentRate.Price.HasValue || !Compatible(parentRate.Unit, parent.Unit))
                        return new(item, null, null, CostIssue.InvalidInclusion, null);
                    return new(item, null, 0, CostIssue.None, owner);
                }
            }
            if (!snapshot.Rates.TryGetValue(item.WorkCode, out var rate)) return new(item, null, null, CostIssue.MissingRate, null);
            if (!rate.Price.HasValue) return new(item, null, null, CostIssue.MissingPrice, null);
            if (!Compatible(rate.Unit, item.Unit)) return new(item, null, null, CostIssue.IncompatibleUnit, null);
            decimal price = ConvertPrice(rate.Price.Value, rate.Unit, item.Unit);
            return new(item, price, checked(price * item.Amount), CostIssue.None, null);
        }
    }
    private static bool Compatible(QuantityUnit from, QuantityUnit to) => from == to || from is QuantityUnit.Kilogram or QuantityUnit.Tonne && to is QuantityUnit.Kilogram or QuantityUnit.Tonne;
}
