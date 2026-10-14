namespace GPC.Design.Sections;

/// <summary>Family of an action row: ultimate, seismic and shear rows are design values, the others serviceability values.</summary>
public enum DesignActionFamily { Ultimate, Seismic, Shear, Characteristic, Frequent, QuasiPermanent }

/// <summary>Signed axial force (tension positive) of a named action row.</summary>
public sealed record DesignAxialAction(string Name, DesignActionFamily Family, double SignedAxialForce);

/// <summary>Governing compression and the row it comes from; no source when no row is compressed.</summary>
public sealed record GoverningCompression(double Value, DesignAxialAction? Source)
{
    /// <summary>The compression comes from a serviceability combination: a service value, not a design value.</summary>
    public bool FromServiceability => Source?.Family is DesignActionFamily.Characteristic or DesignActionFamily.Frequent or DesignActionFamily.QuasiPermanent;
}

/// <summary>Numerical demand projections for the design coordinator. Limits are resolved by the existing checkers.
/// Units must be consistent within a ratio. Signed axial force and concrete stress are tension-positive.</summary>
public static class SectionDesignDemand
{
    /// <summary>
    /// Compression NEd of the detailing rules: the largest compression of all the rows, ultimate, seismic, shear and serviceability
    /// (decision of the ANTHEA user of 14/10/2026: the most severe N of all the combinations). Serviceability rows do not govern when
    /// the ultimate rows are complete; they cover sections without ultimate rows. With equal values the design families win, then the
    /// first row; without compressed rows the value is 0 and there is no source.
    /// </summary>
    public static GoverningCompression DetailingCompression(IEnumerable<DesignAxialAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        DesignAxialAction? source = null; double value = 0;
        foreach (var action in actions)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (!Enum.IsDefined(action.Family)) throw new ArgumentException("Unknown action family.");
            double compression = Compression(action.SignedAxialForce);
            if (compression > value || compression == value && compression > 0 && source is not null && action.Family < source.Family)
                (value, source) = (compression, action);
        }
        return new GoverningCompression(value, source);
    }

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
