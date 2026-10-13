namespace GPC.Design.Sections;

/// <summary>Numerical demand projections for the design coordinator. Limits are resolved by the existing checkers.
/// Units must be consistent within a ratio. Signed axial force and concrete stress are tension-positive.</summary>
public static class SectionDesignDemand
{
    public static double Compression(double signedValue)
    {
        if (!double.IsFinite(signedValue)) throw new ArgumentException("A finite signed demand is required.");
        return Math.Max(0, -signedValue);
    }

    /// <summary>Empty action sets give the geometric detailing minimum only, as in the captured coordinator.</summary>
    public static double MaximumCompression(IEnumerable<double> signedAxialForces)
    {
        ArgumentNullException.ThrowIfNull(signedAxialForces);
        return signedAxialForces.Select(Compression).DefaultIfEmpty(0).Max();
    }

    /// <summary>Missing/nonpositive capacity or invalid demand cannot certify a candidate.</summary>
    public static double? Ratio(double demand, double? capacity)
    {
        if (!double.IsFinite(demand) || demand < 0 || capacity is not > 0 || !double.IsFinite(capacity.Value)) return null;
        double ratio = demand / capacity.Value;
        return double.IsFinite(ratio) ? ratio : null;
    }

    public static double RequiredCapacity(double demand, double target)
    {
        if (!double.IsFinite(demand) || demand < 0 || !double.IsFinite(target) || target <= 0 || target > 1)
            throw new ArgumentException("Nonnegative finite demand and target in (0,1] required.");
        double required = demand / target;
        if (!double.IsFinite(required)) throw new ArgumentException("The target capacity is outside the numeric range.");
        return required;
    }
}
