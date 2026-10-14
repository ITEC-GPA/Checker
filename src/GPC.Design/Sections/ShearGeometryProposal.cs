namespace GPC.Design.Sections;

public enum ShearSectionShape { Rectangle, Circle, Tee }
public sealed record ShearGeometryDirection(double Bw, double Depth, double SteelArea);

/// <summary>Geometric suggestions in mm and mm²; no anchorage or detailing certification.
/// Extra face indices identify layers explicitly assigned by the editor.</summary>
public static class ShearGeometryProposal
{
    public static ShearGeometryDirection Derive(ShearSectionShape shape, bool alongX,
        IReadOnlyList<(double X, double Y)> outline, IReadOnlyList<DesignBar> reinforcement,
        double width, double height, double radius, double innerWidth, double innerHeight,
        double flangeThickness, double webWidth, IReadOnlyList<int>? extraNegative = null, IReadOnlyList<int>? extraPositive = null)
    {
        ArgumentNullException.ThrowIfNull(outline); ArgumentNullException.ThrowIfNull(reinforcement);
        if (!Enum.IsDefined(shape)) throw new ArgumentException("Unknown section shape.");
        var bars = reinforcement.ToArray();
        if (outline.Count < 3 || outline.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)) || bars.Length == 0 ||
            bars.Any(b => b is null || !double.IsFinite(b.X) || !double.IsFinite(b.Y) || !double.IsFinite(b.Area) || b.Area <= 0 || !double.IsFinite(b.Diameter) || b.Diameter <= 0))
            throw new ArgumentException("Finite section outline and positive reinforcement required.");
        double C(DesignBar b) => alongX ? b.X : b.Y;
        DesignBar[] negative, positive;
        if (shape == ShearSectionShape.Circle)
        {
            negative = bars.Where(b => C(b) < -1e-8).ToArray(); positive = bars.Where(b => C(b) > 1e-8).ToArray();
            if (negative.Length == 0 || positive.Length == 0) throw new ArgumentException("Circolare: armatura sui due semicerchi non determinabile.");
            double an = negative.Sum(b => b.Area), ap = positive.Sum(b => b.Area);
            return Valid(new(width - innerWidth, radius + Math.Min(-negative.Sum(b => b.Area * C(b)) / an, positive.Sum(b => b.Area * C(b)) / ap), Math.Min(an, ap)));
        }
        double min = outline.Min(p => alongX ? p.X : p.Y), max = outline.Max(p => alongX ? p.X : p.Y);
        double rim = bars.Max(b => b.Diameter), low = bars.Min(C), high = bars.Max(C);
        negative = bars.Where(b => C(b) <= low + rim && C(b) < (min + max) / 2).ToArray();
        positive = bars.Where(b => C(b) >= high - rim && C(b) > (min + max) / 2).ToArray();
        DesignBar[] WithExtra(DesignBar[] face, IReadOnlyList<int>? extra)
        {
            if (extra is null) return face;
            if (extra.Any(i => i < 0 || i >= bars.Length)) throw new ArgumentException("Invalid explicit face bar index.");
            return face.Concat(extra.Select(i => bars[i])).Distinct().ToArray();
        }
        negative = WithExtra(negative, extraNegative); positive = WithExtra(positive, extraPositive);
        if (negative.Length == 0 || positive.Length == 0) throw new ArgumentException("Armature sui due lembi non determinabili: usare parametri manuali.");
        double negArea = negative.Sum(b => b.Area), posArea = positive.Sum(b => b.Area);
        double depth = Math.Min(max - negative.Sum(b => b.Area * C(b)) / negArea, positive.Sum(b => b.Area * C(b)) / posArea - min);
        double bw = shape == ShearSectionShape.Rectangle ? alongX ? height - innerHeight : width - innerWidth : alongX ? flangeThickness : webWidth;
        return Valid(new(bw, depth, Math.Min(negArea, posArea)));
    }
    static ShearGeometryDirection Valid(ShearGeometryDirection value)
    {
        if (new[] { value.Bw, value.Depth, value.SteelArea }.Any(v => !double.IsFinite(v) || v <= 0))
            throw new ArgumentException("Positive finite shear width, effective depth and reinforcement required.");
        return value;
    }
}
