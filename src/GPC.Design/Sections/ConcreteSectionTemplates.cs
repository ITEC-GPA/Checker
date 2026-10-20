using GPC.Geometry;
using GPC.Model.Sections;

namespace GPC.Design.Sections;

public enum ConcreteTemplateKind { HollowRectangle, HollowCircle, I, L, C, Ellipse, Stadium, Trapezoid, RegularPolygon }

/// <summary>Lengths in mm. Cover is measured to the outside of the transverse reinforcement.
/// PerimeterSpacing is an upper bound for centre spacing; radial layouts use the assigned counts.</summary>
public sealed record ConcreteTemplateInput(ConcreteTemplateKind Kind, double Width, double Height,
    double Web, double TopFlange, double BottomWidth, double BottomFlange,
    double InnerWidth, double InnerHeight, double Cover, double BarDiameter, double PerimeterSpacing,
    int RadialCount, int InnerRadialCount, double LinkDiameter, double LinkSpacing, int CurveSides = 96, int PolygonSides = 8)
{
    /// <summary>Optional separate layout. Null preserves the existing global radial layout.</summary>
    public StadiumReinforcement? Stadium { get; init; }
}

public sealed record ConcreteTemplateLink(string Name, IReadOnlyList<Point2d> Points);
public sealed record ConcreteTemplateFiber(double X, double Y, double Area);
public sealed record ConcreteTemplateGeometry(Shape2d Shape, IReadOnlyList<DesignBar> Bars,
    IReadOnlyList<ConcreteTemplateLink> Links, IReadOnlyList<ConcreteTemplateFiber> Fibers,
    double Width, double Height, double Area, string ShearNote)
{
    /// <summary>Closed path of bar centres for column spacing; absent for other templates.
    /// Curved portions follow the discretized offset contour; straight portions join their bars.</summary>
    public IReadOnlyList<Point2d>? ColumnBarPath { get; init; }
}

/// <summary>
/// Constructive layouts, not reinforcement design or certification of anchorage/bend radii.
/// New templates have their own path: established section meshes and their builders are untouched.
/// Shapes are centred on the concrete centroid; bars and transverse paths share that origin.
/// </summary>
public static partial class ConcreteSectionTemplates
{
    public static ConcreteTemplateGeometry Create(ConcreteTemplateInput p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (!Enum.IsDefined(p.Kind)) throw new ArgumentException("Tipologia di sezione sconosciuta.");
        foreach (double value in new[] { p.Width, p.Height, p.BarDiameter, p.PerimeterSpacing, p.LinkSpacing })
            if (!double.IsFinite(value) || value <= 0) throw new ArgumentException("Dimensioni, diametri e passi devono essere positivi e finiti.");
        if (!double.IsFinite(p.Cover) || p.Cover < 0 || !double.IsFinite(p.LinkDiameter) || p.LinkDiameter < 0)
            throw new ArgumentException("Copriferro e diametro staffa devono essere non negativi e finiti.");
        if (p.CurveSides is < 32 or > 720 || p.CurveSides % 4 != 0)
            throw new ArgumentException("Discretizzazione delle curve: multiplo di quattro fra 32 e 720.");
        if (p.RadialCount is < 4 or > 500 || p.InnerRadialCount is < 4 or > 500)
            throw new ArgumentException("Numero di barre per anello compreso fra 4 e 500.");
        if (p.PerimeterSpacing < p.BarDiameter) throw new ArgumentException("Passo delle barre inferiore al diametro.");

        Shape2d shape = Shape(p);
        var outer = shape.Fill2d;
        var holes = shape.Holes2d ?? [];
        // Weighted polygon centroid, also for cavities.
        double area = Math.Abs(outer.GetSignedArea());
        var center = outer.GetCentroid();
        double sx = area * center.X, sy = area * center.Y;
        foreach (var hole in holes)
        {
            double a = Math.Abs(hole.GetSignedArea()); var c = hole.GetCentroid();
            area -= a; sx -= a * c.X; sy -= a * c.Y;
        }
        center = new Point2d(sx / area, sy / area);
        Point2d Shift(Point2d point) => new(point.X - center.X, point.Y - center.Y);
        Polygon2d Move(Polygon2d polygon) => new(polygon.Points.Select(Shift));
        shape = new Shape2d(Move(outer), holes.Select(Move).ToArray());
        outer = shape.Fill2d; holes = shape.Holes2d ?? [];

        double offset = p.Cover + p.LinkDiameter + p.BarDiameter / 2;
        var barPaths = new List<Point2d[]>();
        if (p.Stadium is null)
        {
            barPaths.Add(Offset(outer, -offset));
            barPaths.AddRange(holes.Select(h => Offset(h, offset)));
        }
        var bars = new List<DesignBar>();
        IReadOnlyList<Point2d>? columnBarPath = null;
        bool radial = p.Kind is ConcreteTemplateKind.Ellipse or ConcreteTemplateKind.Stadium or ConcreteTemplateKind.HollowCircle;
        if (p.Stadium is not null)
        {
            if (p.Kind != ConcreteTemplateKind.Stadium)
                throw new ArgumentException("La disposizione per tratti appartiene soltanto alla capsula.");
            columnBarPath = CreateStadiumBars(p, outer, bars);
        }
        else for (int ring = 0; ring < barPaths.Count; ring++)
        {
            IEnumerable<Point2d> points = radial ? Radial(barPaths[ring], ring == 0 ? p.RadialCount : p.InnerRadialCount)
                : Divide(barPaths[ring], p.PerimeterSpacing);
            foreach (var point in points)
                bars.Add(new(point.X, point.Y, p.BarDiameter, Math.PI * p.BarDiameter * p.BarDiameter / 4));
        }
        if (p.Kind == ConcreteTemplateKind.Stadium && columnBarPath is null) columnBarPath = barPaths[0];
        ValidateBars(shape, bars, p.Cover + p.LinkDiameter);

        var links = new List<ConcreteTemplateLink>();
        if (p.LinkDiameter > 0)
        {
            double axis = p.Cover + p.LinkDiameter / 2;
            if (p.Kind is ConcreteTemplateKind.Ellipse or ConcreteTemplateKind.Stadium or ConcreteTemplateKind.HollowCircle
                or ConcreteTemplateKind.Trapezoid or ConcreteTemplateKind.RegularPolygon)
            {
                links.Add(new("Staffa esterna", Offset(outer, -axis)));
                foreach (var hole in holes) links.Add(new("Staffa interna", Offset(hole, axis)));
            }
            else
            {
                // Closed loops in the individual rectangular walls. Never bridge a void with a bounding-box stirrup.
                foreach (var wall in Walls(p))
                {
                    if (wall.X1 - wall.X0 <= 2 * axis || wall.Y1 - wall.Y0 <= 2 * axis)
                        throw new ArgumentException("La parete non contiene la staffa con il copriferro assegnato.");
                    links.Add(new(wall.Name, Rect(wall.X0 + axis, wall.Y0 + axis, wall.X1 - axis, wall.Y1 - axis).Points.Select(Shift).ToArray()));
                }
            }
            foreach (var link in links)
                ValidateLink(shape, link, p.Cover, p.LinkDiameter);
        }

        // Sampling for stress maps only. Equilibrium and stiffness continue using the native GPC mesh.
        var fibers = new List<ConcreteTemplateFiber>();
        double xmin = outer.Points.Min(v => v.X), xmax = outer.Points.Max(v => v.X);
        double ymin = outer.Points.Min(v => v.Y), ymax = outer.Points.Max(v => v.Y);
        const int n = 48; double dx = (xmax - xmin) / n, dy = (ymax - ymin) / n;
        for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
        {
            var point = new Point2d(xmin + (i + .5) * dx, ymin + (j + .5) * dy);
            if (Inside(shape, point)) fibers.Add(new(point.X, point.Y, dx * dy));
        }
        return new(shape, bars.AsReadOnly(), links.AsReadOnly(), fibers.AsReadOnly(), xmax - xmin, ymax - ymin, area,
            "Staffatura geometrica preliminare: verificare piegature, ancoraggi, trattenimento delle barre e ripartizione del taglio nelle pareti. " +
            "Per questa tipologia assegnare esplicitamente bw, d, Asl e i bracci efficaci per direzione; la torsione richiede un modello dedicato.")
        { ColumnBarPath = columnBarPath };
    }

    private static Shape2d Shape(ConcreteTemplateInput p)
    {
        if (p.Kind is ConcreteTemplateKind.HollowRectangle or ConcreteTemplateKind.HollowCircle)
        {
            if (!double.IsFinite(p.InnerWidth) || p.InnerWidth <= 0 || p.InnerWidth >= p.Width ||
                !double.IsFinite(p.InnerHeight) || p.InnerHeight <= 0 || p.InnerHeight >= p.Height)
                throw new ArgumentException("Il foro deve essere strettamente contenuto nella sezione.");
            if (p.Kind == ConcreteTemplateKind.HollowCircle)
            {
                if (p.Height != p.Width || p.InnerHeight != p.InnerWidth) throw new ArgumentException("Il cerchio richiede diametri coerenti.");
                return new(Circle(p.Width / 2, p.CurveSides), [Circle(p.InnerWidth / 2, p.CurveSides)]);
            }
            return new(Rect(-p.Width / 2, -p.Height / 2, p.Width / 2, p.Height / 2),
                [Rect(-p.InnerWidth / 2, -p.InnerHeight / 2, p.InnerWidth / 2, p.InnerHeight / 2)]);
        }
        if (p.Kind == ConcreteTemplateKind.Ellipse) return new SectionEllipse(p.Width, p.Height).Shape;
        if (p.Kind == ConcreteTemplateKind.Stadium) return new SectionStadium(p.Width, p.Height).Shape;
        if (p.Kind == ConcreteTemplateKind.RegularPolygon)
        {
            if (p.PolygonSides is < 3 or > 32) throw new ArgumentException("Numero di lati del poligono compreso fra 3 e 32.");
            return new SectionRegularPolygon(p.PolygonSides, p.Width).Shape;
        }
        if (p.Kind == ConcreteTemplateKind.Trapezoid)
        {
            if (!double.IsFinite(p.BottomWidth) || p.BottomWidth <= 0) throw new ArgumentException("Base inferiore positiva e finita.");
            return new SectionTrapezoid(p.BottomWidth, p.Width, p.Height).Shape;
        }
        foreach (double value in new[] { p.Web, p.TopFlange })
            if (!double.IsFinite(value) || value <= 0) throw new ArgumentException("Spessori di anima e ala positivi.");
        if (p.Web >= p.Width || p.TopFlange >= p.Height) throw new ArgumentException("Anima e ala devono essere contenute nella sezione.");
        if (p.Kind == ConcreteTemplateKind.L) return new SectionL(p.Width, p.TopFlange, p.Height, p.Web, "").Shape;
        if (!double.IsFinite(p.BottomWidth) || p.BottomWidth <= p.Web || !double.IsFinite(p.BottomFlange) ||
            p.BottomFlange <= 0 || p.BottomFlange + p.TopFlange >= p.Height)
            throw new ArgumentException("Ala inferiore o altezza libera dell'anima non valida.");
        return p.Kind == ConcreteTemplateKind.I
            ? new SectionH(p.Height, p.Web, p.Width, p.TopFlange, p.BottomWidth, p.BottomFlange, "").Shape
            : new SectionC(p.Height, p.Web, p.Width, p.TopFlange, p.BottomWidth, p.BottomFlange).Shape;
    }

    private sealed record Wall(string Name, double X0, double Y0, double X1, double Y1);
    private static IEnumerable<Wall> Walls(ConcreteTemplateInput p)
    {
        if (p.Kind == ConcreteTemplateKind.HollowRectangle)
        {
            yield return new("Parete superiore", -p.Width / 2, p.InnerHeight / 2, p.Width / 2, p.Height / 2);
            yield return new("Parete inferiore", -p.Width / 2, -p.Height / 2, p.Width / 2, -p.InnerHeight / 2);
            yield return new("Parete sinistra", -p.Width / 2, -p.Height / 2, -p.InnerWidth / 2, p.Height / 2);
            yield return new("Parete destra", p.InnerWidth / 2, -p.Height / 2, p.Width / 2, p.Height / 2);
            yield break;
        }
        double b = p.Kind == ConcreteTemplateKind.I ? Math.Max(p.Width, p.BottomWidth) : p.Width;
        double wx = p.Kind == ConcreteTemplateKind.I ? (b - p.Web) / 2 : 0;
        yield return new("Anima", wx, 0, wx + p.Web, p.Height);
        if (p.Kind == ConcreteTemplateKind.L)
            yield return new("Ala", 0, 0, p.Width, p.TopFlange);
        else
        {
            double tx = p.Kind == ConcreteTemplateKind.I ? (b - p.Width) / 2 : 0;
            double bx = p.Kind == ConcreteTemplateKind.I ? (b - p.BottomWidth) / 2 : 0;
            yield return new("Ala superiore", tx, p.Height - p.TopFlange, tx + p.Width, p.Height);
            yield return new("Ala inferiore", bx, 0, bx + p.BottomWidth, p.BottomFlange);
        }
    }

    private static Polygon2d Rect(double x0, double y0, double x1, double y1)
        => new(new[] { new Point2d(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1) });
    private static Polygon2d Circle(double r, int n)
        => new(Enumerable.Range(0, n).Select(i => new Point2d(r * Math.Cos(2 * Math.PI * i / n), r * Math.Sin(2 * Math.PI * i / n))));

    private static Point2d[] Offset(Polygon2d source, double distance)
    {
        const double scale = 1e6;
        var points = source.Points.ToArray();
        if (source.GetSignedArea() < 0) Array.Reverse(points);
        var offset = new ClipperOffset();
        offset.AddPath(points.Select(v => new IntPoint(v.X * scale, v.Y * scale)).ToList(), JoinType.jtMiter, EndType.etClosedPolygon);
        var paths = new List<List<IntPoint>>();
        offset.Execute(ref paths, distance * scale);
        if (paths.Count != 1 || paths[0].Count < 3) throw new ArgumentException("Copriferro e armature interrompono o annullano il contorno utile.");
        return paths[0].Select(v => new Point2d(v.X / scale, v.Y / scale)).ToArray();
    }

    private static IEnumerable<Point2d> Divide(Point2d[] path, double maximumSpacing)
    {
        for (int i = 0; i < path.Length; i++)
        {
            var a = path[i]; var b = path[(i + 1) % path.Length];
            int count = (int)Math.Ceiling(a.DistanceTo(b) / maximumSpacing);
            if (count > 1000) throw new ArgumentException("Disposizione troppo fitta.");
            for (int j = 0; j < count; j++) yield return new(a.X + (b.X - a.X) * j / count, a.Y + (b.Y - a.Y) * j / count);
        }
    }

    private static IEnumerable<Point2d> Radial(Point2d[] path, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = 2 * Math.PI * i / count, dx = Math.Cos(angle), dy = Math.Sin(angle), nearest = double.PositiveInfinity;
            for (int j = 0; j < path.Length; j++)
            {
                var a = path[j]; var b = path[(j + 1) % path.Length];
                double ex = b.X - a.X, ey = b.Y - a.Y, cross = dx * ey - dy * ex;
                if (Math.Abs(cross) < 1e-12) continue;
                double t = (a.X * ey - a.Y * ex) / cross, u = (a.X * dy - a.Y * dx) / cross;
                if (t > 0 && u >= -1e-10 && u <= 1 + 1e-10) nearest = Math.Min(nearest, t);
            }
            if (!double.IsFinite(nearest)) throw new ArgumentException("Intersezione radiale con il contorno non determinabile.");
            yield return new(nearest * dx, nearest * dy);
        }
    }

    private static bool Inside(Shape2d shape, Point2d point)
        => shape.Fill2d.IsPointInside(point, 0) && !(shape.Holes2d ?? []).Any(h => h.IsPointInside(point, 0));
    private static double Clearance(Shape2d shape, Point2d point)
        => (Inside(shape, point) ? 1 : -1) * new[] { shape.Fill2d.DistanceTo(point) }
            .Concat((shape.Holes2d ?? []).Select(h => h.DistanceTo(point))).Min();
    public static void ValidateBars(Shape2d shape, IReadOnlyList<DesignBar> bars, double requiredCover)
    {
        if (bars.Count is < 1 or > 2000) throw new ArgumentException("Numero di barre non valido.");
        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i]; var position = new Point2d(bar.X, bar.Y);
            if (Clearance(shape, position) - bar.Diameter / 2 < requiredCover - 2e-5)
                throw new ArgumentException("Una barra non rispetta il copriferro o interseca il foro.");
            for (int j = 0; j < i; j++)
                if (position.DistanceTo(new(bars[j].X, bars[j].Y)) <= (bar.Diameter + bars[j].Diameter) / 2 + 1e-6)
                    throw new ArgumentException("Due barre si sovrappongono: modificare diametro, passo o spessore della parete.");
        }
    }
    private static void ValidateLink(Shape2d shape, ConcreteTemplateLink link, double cover, double diameter)
    {
        for (int j = 0; j < link.Points.Count; j++)
        {
            var a = link.Points[j]; var b = link.Points[(j + 1) % link.Points.Count];
            for (int i = 0; i <= 16; i++)
                if (Clearance(shape, new(a.X + (b.X - a.X) * i / 16, a.Y + (b.Y - a.Y) * i / 16)) < cover + diameter / 2 - 2e-5)
                    throw new ArgumentException("Una staffa attraversa il vuoto o non rispetta il copriferro.");
        }
    }
}
