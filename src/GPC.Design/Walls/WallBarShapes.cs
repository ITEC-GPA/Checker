namespace GPC.Design.Walls;

/// <summary>Centre line of a bent bar from the corner points of its straight legs (m) and the centre-line bend radius at each inner
/// corner (m, 0 = sharp bend of known angle). Straight legs run between tangent points; arcs are developed exactly and sampled for
/// drawing only. A radius that does not fit in the adjacent legs is reduced.</summary>
internal static class WallBarShapes
{
    internal sealed record Shape(IReadOnlyList<WallBarPoint> Path, IReadOnlyList<WallBarLeg> Legs, double Length);

    internal static Shape Build(IReadOnlyList<WallBarPoint> corners, IReadOnlyList<double> radii)
    {
        if (corners.Count < 2 || radii.Count != corners.Count - 2) throw new ArgumentException("A bar shape needs n corners and n-2 radii.");
        var path = new List<WallBarPoint> { corners[0] }; var legs = new List<WallBarLeg>();
        var start = corners[0]; double kink = 0; char name = 'A';
        void Straight(WallBarPoint to)
        {
            double length = Distance(start, to);
            legs.Add(new WallBarLeg((name++).ToString(), length) { Kind = WallBarLegKind.Straight, Angle = kink * 180 / Math.PI, From = start, To = to });
            path.Add(to); start = to; kink = 0;
        }
        for (int i = 1; i < corners.Count - 1; i++)
        {
            var p = corners[i]; var u = Unit(corners[i - 1], p); var w = Unit(p, corners[i + 1]);
            double phi = Math.Acos(PortableMath.Clamp(u.X * w.X + u.Y * w.Y, -1, 1)), cross = u.X * w.Y - u.Y * w.X, r = radii[i - 1];
            if (r <= 0 || phi < 1e-9) { Straight(p); kink = phi; continue; }
            double tangent = Math.Tan(phi / 2), next = Distance(p, corners[i + 1]) * (i + 2 == corners.Count ? 1 : .5);
            double d = Math.Min(r * tangent, Math.Min(Distance(start, p), next)); r = d / tangent;
            var t1 = new WallBarPoint(p.X - u.X * d, p.Y - u.Y * d); var t2 = new WallBarPoint(p.X + w.X * d, p.Y + w.Y * d);
            Straight(t1);
            double side = cross > 0 ? 1 : -1;
            var centre = new WallBarPoint(t1.X - side * u.Y * r, t1.Y + side * u.X * r);
            double from = Math.Atan2(t1.Y - centre.Y, t1.X - centre.X); int steps = Math.Max(2, (int)Math.Ceiling(phi / (Math.PI / 24) - 1e-9));
            for (int j = 1; j < steps; j++) { double angle = from + side * phi * j / steps; path.Add(new(centre.X + r * Math.Cos(angle), centre.Y + r * Math.Sin(angle))); }
            path.Add(t2);
            legs.Add(new WallBarLeg("Arc" + Math.Round(phi * 180 / Math.PI).ToString(System.Globalization.CultureInfo.InvariantCulture), r * phi)
                { Kind = WallBarLegKind.Arc, Angle = phi * 180 / Math.PI });
            start = t2;
        }
        Straight(corners[corners.Count - 1]);
        return new(path.AsReadOnly(), legs.AsReadOnly(), legs.Sum(l => l.Length));
    }

    /// <summary>Minimum mandrel diameter (mm) of EN 1992-1-1 Table 8.1N and NTC 2018: 4Ø up to Ø16, 7Ø above.</summary>
    internal static double MinimumMandrel(double diameter) => (diameter <= 16 ? 4 : 7) * diameter;

    /// <summary>Centre of a longitudinal bar of diameter <paramref name="bar"/> (mm) held in the bend of a bar: the bend centre
    /// <paramref name="centre"/> moved towards the corner (unit diagonal <paramref name="dx"/>, <paramref name="dy"/>) until the bar
    /// touches the inside of the bend (mandrel in mm).</summary>
    internal static WallBarPoint Nested(WallBarPoint centre, double dx, double dy, double mandrel, double bar)
    {
        double offset = Math.Max(0, mandrel / 2 - bar / 2) / 1000 / Math.Sqrt(2);
        return new(centre.X + dx * offset, centre.Y + dy * offset);
    }

    internal static double Distance(WallBarPoint a, WallBarPoint b) => PortableMath.Hypot(a.X - b.X, a.Y - b.Y);
    static WallBarPoint Unit(WallBarPoint a, WallBarPoint b)
    {
        double length = Distance(a, b);
        if (!(length > 0)) throw new ArgumentException("Coincident corners in a bar shape.");
        return new((b.X - a.X) / length, (b.Y - a.Y) / length);
    }
}
