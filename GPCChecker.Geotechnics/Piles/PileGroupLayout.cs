namespace GPC.Checkers.Geotechnics.Piles;

public enum PileLayoutKind { Rectangular, Staggered, Triangular, Pentagonal, Hexagonal }
public sealed class PileLayoutOptions
{
    public PileLayoutKind Kind { get; set; }
    public double Diameter { get; set; } = 1;
    public int Columns { get; set; } = 3;
    public int Rows { get; set; } = 3;
    public int SideCount { get; set; } = 3;
    public int Rings { get; set; } = 1;
    public int SideDivisions { get; set; } = 1;
    public bool CenterPile { get; set; } = true;
    public double SpacingXDiameters { get; set; } = 3;
    public double SpacingYDiameters { get; set; } = 3;
    public double RotationDegrees { get; set; }
}
public sealed class PileCapVertex
{
    public double X { get; set; }
    public double Y { get; set; }
    public PileCapVertex() { }
    public PileCapVertex(double x, double y) { X = x; Y = y; }
}
/// <summary>Plan geometry only. The cap outline does not contribute to lateral group reduction factors.</summary>
public static class PileGroupLayout
{
    static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    public static IReadOnlyList<GroupPile> Generate(PileLayoutOptions o)
    {
        if (o == null) throw new ArgumentNullException(nameof(o));
        if (!Enum.IsDefined(typeof(PileLayoutKind), o.Kind) || !Finite(o.Diameter) || o.Diameter <= 0 || !Finite(o.RotationDegrees) ||
            !Finite(o.SpacingXDiameters) || o.SpacingXDiameters < 1 || !Finite(o.SpacingYDiameters) || o.SpacingYDiameters < 1)
            throw new ArgumentException("Diametro positivo, angoli finiti e interassi almeno pari a D richiesti.");
        var points = new List<GroupPile>();
        void Add(double x, double y) { if (points.Count >= 500) throw new ArgumentException("La disposizione supera 500 pali."); points.Add(new GroupPile { X = x, Y = y, Id = "P" + (points.Count + 1) }); }
        double sx = o.SpacingXDiameters * o.Diameter, sy = o.SpacingYDiameters * o.Diameter;
        switch (o.Kind)
        {
            case PileLayoutKind.Rectangular:
            case PileLayoutKind.Staggered:
                if (o.Columns < (o.Kind == PileLayoutKind.Staggered ? 2 : 1) || o.Rows < 1 || (long)o.Columns * o.Rows > 1000) throw new ArgumentException("Numero di pali per fila o numero di file non valido (massimo 500 pali).");
                for (int row = 0; row < o.Rows; row++)
                {
                    bool shifted = o.Kind == PileLayoutKind.Staggered && row % 2 == 1;
                    for (int col = 0; col < o.Columns - (shifted ? 1 : 0); col++) Add((col + (shifted ? .5 : 0)) * sx, row * sy);
                }
                break;
            case PileLayoutKind.Triangular:
                if (o.SideCount < 2 || o.SideCount > 31) throw new ArgumentException("Base triangolare: da 2 a 31 pali per lato.");
                for (int row = 0; row < o.SideCount; row++) for (int col = 0; col < o.SideCount - row; col++) Add((col + row * .5) * sx, row * Math.Sqrt(3) / 2 * sx);
                break;
            default:
                int sides = o.Kind == PileLayoutKind.Pentagonal ? 5 : 6;
                if (o.Rings < 1 || o.Rings > 12 || o.SideDivisions < 1 || o.SideDivisions > 99 || (long)sides * o.SideDivisions * o.Rings * (o.Rings + 1) / 2 + (o.CenterPile ? 1 : 0) > 500)
                    throw new ArgumentException("Numero di anelli/suddivisioni non valido o oltre 500 pali.");
                if (o.CenterPile) Add(0, 0);
                for (int ring = 1; ring <= o.Rings; ring++)
                {
                    int divisions = o.SideDivisions * ring;
                    double radius = divisions * sx / (2 * Math.Sin(Math.PI / sides));
                    for (int side = 0; side < sides; side++)
                    {
                        double a = Math.PI / 2 + side * 2 * Math.PI / sides, b = a + 2 * Math.PI / sides;
                        for (int j = 0; j < divisions; j++) { double f = (double)j / divisions; Add(radius * ((1 - f) * Math.Cos(a) + f * Math.Cos(b)), radius * ((1 - f) * Math.Sin(a) + f * Math.Sin(b))); }
                    }
                }
                break;
        }
        double cx = points.Average(p => p.X), cy = points.Average(p => p.Y), angle = (o.RotationDegrees % 360) * Math.PI / 180;
        foreach (var p in points) { double x = p.X - cx, y = p.Y - cy; p.X = x * Math.Cos(angle) - y * Math.Sin(angle); p.Y = x * Math.Sin(angle) + y * Math.Cos(angle); }
        LateralPileGroup.Validate(new LateralGroupInput { Diameter = o.Diameter, Piles = points });
        return points;
    }
    static double Cross(PileCapVertex a, PileCapVertex b, PileCapVertex c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    /// <summary>Convex hull with mitered outward offset; distance is measured from the outermost pile axes, not pile faces.</summary>
    public static IReadOnlyList<PileCapVertex> CapOutline(IReadOnlyList<GroupPile> piles, double diameter, double edgeDiameters, bool rectangle)
    {
        LateralPileGroup.Validate(new LateralGroupInput { Diameter = diameter, Piles = piles });
        if (!Finite(edgeDiameters) || edgeDiameters < .5 || edgeDiameters > 100) throw new ArgumentException("Distanza asse–bordo del basamento: da 0.5D a 100D.");
        double e = edgeDiameters * diameter;
        var pts = piles.Select(p => new PileCapVertex(p.X, p.Y)).OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (rectangle) return new[] { new PileCapVertex(pts.Min(p => p.X) - e, pts.Min(p => p.Y) - e), new PileCapVertex(pts.Max(p => p.X) + e, pts.Min(p => p.Y) - e), new PileCapVertex(pts.Max(p => p.X) + e, pts.Max(p => p.Y) + e), new PileCapVertex(pts.Min(p => p.X) - e, pts.Max(p => p.Y) + e) };
        if (pts.Count == 1) return Enumerable.Range(0, 48).Select(i => new PileCapVertex(pts[0].X + e * Math.Cos(i * Math.PI / 24), pts[0].Y + e * Math.Sin(i * Math.PI / 24))).ToArray();
        var lower = new List<PileCapVertex>(); var upper = new List<PileCapVertex>();
        foreach (var p in pts) { while (lower.Count > 1 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= diameter * diameter * 1e-10) lower.RemoveAt(lower.Count - 1); lower.Add(p); }
        foreach (var p in pts.AsEnumerable().Reverse()) { while (upper.Count > 1 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= diameter * diameter * 1e-10) upper.RemoveAt(upper.Count - 1); upper.Add(p); }
        var hull = lower.Take(lower.Count - 1).Concat(upper.Take(upper.Count - 1)).ToArray();
        if (hull.Length == 2)
        {
            var a = hull[0]; var b = hull[1]; double len = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2)), ux = (b.X - a.X) / len, uy = (b.Y - a.Y) / len;
            return new[] { new PileCapVertex(a.X - e * ux + e * uy, a.Y - e * uy - e * ux), new PileCapVertex(b.X + e * ux + e * uy, b.Y + e * uy - e * ux), new PileCapVertex(b.X + e * ux - e * uy, b.Y + e * uy + e * ux), new PileCapVertex(a.X - e * ux - e * uy, a.Y - e * uy + e * ux) };
        }
        var output = new List<PileCapVertex>();
        for (int i = 0; i < hull.Length; i++)
        {
            var a = hull[(i + hull.Length - 1) % hull.Length]; var b = hull[i]; var c = hull[(i + 1) % hull.Length];
            double ax = b.X - a.X, ay = b.Y - a.Y, bx = c.X - b.X, by = c.Y - b.Y, al = Math.Sqrt(ax * ax + ay * ay), bl = Math.Sqrt(bx * bx + by * by);
            double n1x = ay / al, n1y = -ax / al, n2x = by / bl, n2y = -bx / bl, denom = 1 + n1x * n2x + n1y * n2y;
            output.Add(new PileCapVertex(b.X + e * (n1x + n2x) / denom, b.Y + e * (n1y + n2y) / denom));
        }
        return output;
    }
}
