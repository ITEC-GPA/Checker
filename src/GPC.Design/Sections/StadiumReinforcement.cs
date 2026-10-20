using GPC.Geometry;

namespace GPC.Design.Sections;

/// <summary>Bar counts per semicircle (including both tangent ends) and per straight
/// side (internal bars only). Diameters and coordinates are in mm.</summary>
public sealed record StadiumReinforcement(int CurveCount, double CurveDiameter,
    int StraightCount, double StraightDiameter);

public static partial class ConcreteSectionTemplates
{
    private static IReadOnlyList<Point2d> CreateStadiumBars(ConcreteTemplateInput p, Polygon2d outline, List<DesignBar> bars)
    {
        var layout = p.Stadium!;
        if (layout.CurveCount is < 2 or > 500 || layout.StraightCount is < 0 or > 500
            || !double.IsFinite(layout.CurveDiameter) || layout.CurveDiameter <= 0
            || !double.IsFinite(layout.StraightDiameter) || layout.StraightDiameter <= 0)
            throw new ArgumentException("Capsula: da 2 a 500 barre per semicirconferenza, da 0 a 500 interne per rettilineo; diametri positivi e finiti.");
        bool horizontal = p.Width > p.Height;
        double halfStraight = Math.Abs(p.Width - p.Height) / 2;
        if (halfStraight < 1e-6)
            throw new ArgumentException("Capsula con tratti separati: b e h devono differire. Per b = h usare il wizard circolare.");
        Point2d Orient(Point2d v) => horizontal ? v : new(v.Y, v.X);
        var path = Offset(outline, -(p.Cover + p.LinkDiameter + layout.CurveDiameter / 2)).Select(Orient).ToArray();
        var perimeter = new List<Point2d>();
        void Bar(Point2d position, double diameter)
        {
            var v = Orient(position);
            bars.Add(new(v.X, v.Y, diameter, Math.PI * diameter * diameter / 4));
        }
        Point2d Hit(double center, double angle)
        {
            double dx = Math.Cos(angle), dy = Math.Sin(angle), nearest = double.PositiveInfinity;
            for (int j = 0; j < path.Length; j++)
            {
                var a = path[j]; var b = path[(j + 1) % path.Length];
                double ax = a.X - center, ay = a.Y, ex = b.X - a.X, ey = b.Y - a.Y;
                double cross = dx * ey - dy * ex;
                if (Math.Abs(cross) < 1e-12) continue;
                double t = (ax * ey - ay * ex) / cross, u = (ax * dy - ay * dx) / cross;
                if (t > 0 && u >= -1e-10 && u <= 1 + 1e-10) nearest = Math.Min(nearest, t);
            }
            if (!double.IsFinite(nearest)) throw new ArgumentException("Capsula: copriferro o diametro annullano il tratto curvo utile.");
            return new(center + nearest * dx, nearest * dy);
        }
        void Curve(bool right)
        {
            double center = right ? halfStraight : -halfStraight, start = right ? -Math.PI / 2 : Math.PI / 2;
            var ends = new[] { Hit(center, start), Hit(center, start + Math.PI) };
            double Angle(Point2d v)
            {
                double angle = Math.Atan2(v.Y, v.X - center);
                return !right && angle < 0 ? angle + 2 * Math.PI : angle;
            }
            perimeter.Add(Orient(ends[0]));
            perimeter.AddRange(path.Where(v => right ? v.X > center + 1e-7 : v.X < center - 1e-7)
                .OrderBy(Angle).Select(Orient));
            perimeter.Add(Orient(ends[1]));
            for (int i = 0; i < layout.CurveCount; i++)
                Bar(Hit(center, start + Math.PI * i / (layout.CurveCount - 1)), layout.CurveDiameter);
        }
        void Straight(bool top)
        {
            if (layout.StraightCount == 0) return;
            double y = Math.Min(p.Width, p.Height) / 2 - p.Cover - p.LinkDiameter - layout.StraightDiameter / 2;
            if (y <= 0) throw new ArgumentException("Capsula: copriferro e diametro annullano il tratto rettilineo utile.");
            for (int i = 1; i <= layout.StraightCount; i++)
            {
                double x = -halfStraight + 2 * halfStraight * i / (layout.StraightCount + 1);
                var v = new Point2d(top ? -x : x, top ? y : -y);
                Bar(v, layout.StraightDiameter); perimeter.Add(Orient(v));
            }
        }
        Curve(true); Straight(true); Curve(false); Straight(false);
        return perimeter.AsReadOnly();
    }
}
