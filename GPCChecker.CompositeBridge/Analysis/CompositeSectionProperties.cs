
namespace GPC.Checkers.CompositeBridge;

public sealed record BridgeSectionProperties(string Name, double Area, double X, double Y, double Ix, double Iy, double Top, double Bottom, double Width)
{
    public double? WTop => Top > Y ? Ix / (Top - Y) : null;
    public double? WBottom => Y > Bottom ? Ix / (Y - Bottom) : null;
    public double Rx => Math.Sqrt(Ix / Area);
    public double Ry => Math.Sqrt(Iy / Area);
}

/// <summary>Properties of rectangular parts and their aggregates; mm, mm² and mm⁴. Width retains the largest part width.</summary>
public static class CompositeSectionProperties
{
    public static BridgeSectionProperties RectangleProperties(string name, double width, double height, double bottom, double x) =>
        new(name, width * height, x, bottom + height / 2, width * Math.Pow(height, 3) / 12, height * Math.Pow(width, 3) / 12, bottom + height, bottom, width);

    public static BridgeSectionProperties CombineProperties(string name, IEnumerable<BridgeSectionProperties> source)
    {
        var p = source.Where(p => p.Area > 0).ToArray(); double area = p.Sum(p => p.Area);
        double x = p.Sum(p => p.Area * p.X) / area, y = p.Sum(p => p.Area * p.Y) / area;
        return new(name, area, x, y, p.Sum(p => p.Ix + p.Area * Math.Pow(p.Y - y, 2)), p.Sum(p => p.Iy + p.Area * Math.Pow(p.X - x, 2)),
            p.Max(p => p.Top), p.Min(p => p.Bottom), p.Max(p => p.Width));
    }
}
