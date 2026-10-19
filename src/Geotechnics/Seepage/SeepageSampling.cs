using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Seepage;

/// <summary>Sampling and integration of a converged P1 field. No extrapolation and no smoothing across material interfaces.</summary>
public static class SeepageSampling
{
    public static double HeadAt(SeepageResult field, double x, double z) => SeepageWallCoupling.PressureAt(field, x, z) / SoilUnits.WaterUnitWeight + z;

    /// <summary>Mean upward gradient along an explicit vertical segment, dimensionless. This alone does not establish HYD applicability.</summary>
    public static double MeanUpwardGradient(SeepageResult field, double x, double top, double bottom)
    {
        if (!(top > bottom)) throw new ArgumentException("Intervallo verticale non valido.");
        // Validate every crossed interval, including gaps; endpoints alone would permit a segment crossing a void.
        Line(field, x, top, x, bottom);
        return (HeadAt(field, x, bottom) - HeadAt(field, x, top)) / (top - bottom);
    }

    /// <summary>Integral of net pressure below minus above a horizontal body, times its represented width (N).
    /// The lower face must be inside the converged domain; topPressure is a uniform assigned pressure on the upper face.
    /// Suction is retained unless ignoreSuction is selected; its zero crossings are inserted before integration.</summary>
    public static double NetUpwardForce(SeepageResult bottomField, double xStart, double xEnd, double bottomZ, double width,
        double topPressure, bool ignoreSuction = false)
    {
        if (!UnsaturatedConductivity.Finite(width) || width <= 0 || !UnsaturatedConductivity.Finite(topPressure) || topPressure < 0)
            throw new ArgumentException("Larghezza positiva e pressione superiore non negativa richieste.");
        if (!(xEnd > xStart)) throw new ArgumentException("Estensione orizzontale positiva richiesta.");
        var line = Line(bottomField, xStart, bottomZ, xEnd, bottomZ); double integral = 0;
        for (int i = 1; i < line.Count; i++)
        {
            double a = line[i - 1].Pressure, b = line[i].Pressure, length = (line[i].Parameter - line[i - 1].Parameter) * (xEnd - xStart);
            if (!ignoreSuction || a >= 0 && b >= 0) integral += (a + b) * length / 2;
            else if (a > 0 || b > 0) { double positive = Math.Max(a, b); integral += positive * positive / Math.Abs(b - a) * length / 2; }
        }
        double force = (integral - topPressure * (xEnd - xStart)) * width;
        if (!UnsaturatedConductivity.Finite(force)) throw new ArithmeticException("Overflow nell'integrale di pressione.");
        return force;
    }

    /// <summary>All element gradients touching a point. Selects maximum upward gradient conservatively; no averaged interface gradient.
    /// Boundary type, representative soil and predominant direction require a separate engineering assessment.</summary>
    public static SeepageGradientSample GradientsAt(SeepageResult field, double x, double z)
    {
        SeepageWallCoupling.PressureAt(field, x, z); var gradients = new List<SeepageElementResult>();
        foreach (var e in field.Elements)
        {
            var a = field.Nodes[e.A]; var b = field.Nodes[e.B]; var c = field.Nodes[e.C];
            double cross = Cross(b.X - a.X, b.Z - a.Z, c.X - a.X, c.Z - a.Z);
            double v = Cross(x - a.X, z - a.Z, c.X - a.X, c.Z - a.Z) / cross;
            double w = Cross(b.X - a.X, b.Z - a.Z, x - a.X, z - a.Z) / cross;
            if (v >= -1e-9 && w >= -1e-9 && v + w <= 1 + 1e-9) gradients.Add(e);
        }
        return new(gradients.Max(e => -e.GradientZ), gradients.Max(e => Math.Abs(e.GradientX)), gradients.Select(e => e.Index).ToArray());
    }

    /// <summary>Exact breakpoints of a line through the piecewise-affine field. Returned pressure remains signed, MPa.</summary>
    public static IReadOnlyList<SeepageLinePoint> Line(SeepageResult field, double x0, double z0, double x1, double z1)
    {
        if (field == null || !field.IsComplete) throw new ArgumentException("Campionamento riservato a campi convergenti.");
        if (new[] { x0, z0, x1, z1 }.Any(v => !UnsaturatedConductivity.Finite(v))) throw new ArgumentException("Coordinate non finite.");
        double dx = x1 - x0, dz = z1 - z0, length2 = dx * dx + dz * dz;
        if (!(length2 > 0) || !UnsaturatedConductivity.Finite(length2)) throw new ArgumentException("Segmento non valido.");
        var parameters = new List<double> { 0, 1 };
        foreach (var e in field.Elements)
        {
            int[] ids = { e.A, e.B, e.C };
            for (int i = 0; i < ids.Length; i++)
            {
                var a = field.Nodes[ids[i]]; var b = field.Nodes[ids[(i + 1) % ids.Length]];
                double ex = b.X - a.X, ez = b.Z - a.Z, denominator = Cross(dx, dz, ex, ez);
                double tolerance = 1e-12 * Math.Sqrt(length2 * (ex * ex + ez * ez));
                if (Math.Abs(denominator) <= tolerance)
                {
                    if (Math.Abs(Cross(a.X - x0, a.Z - z0, dx, dz)) > 1e-12 * length2) continue;
                    Add(((a.X - x0) * dx + (a.Z - z0) * dz) / length2);
                    Add(((b.X - x0) * dx + (b.Z - z0) * dz) / length2);
                }
                else
                {
                    double t = Cross(a.X - x0, a.Z - z0, ex, ez) / denominator;
                    double edge = Cross(a.X - x0, a.Z - z0, dx, dz) / denominator;
                    if (edge >= -1e-10 && edge <= 1 + 1e-10) Add(t);
                }
            }
        }
        void Add(double t) { if (t > 0 && t < 1) parameters.Add(t); }
        parameters.Sort(); var unique = new List<double>();
        foreach (double t in parameters) if (unique.Count == 0 || t - unique[unique.Count - 1] > 1e-12) unique.Add(t);
        var points = unique.Select(t => new SeepageLinePoint(t, x0 + t * dx, z0 + t * dz, SeepageWallCoupling.PressureAt(field, x0 + t * dx, z0 + t * dz))).ToArray();
        for (int i = 1; i < points.Length; i++)
        { double t = (points[i - 1].Parameter + points[i].Parameter) / 2; SeepageWallCoupling.PressureAt(field, x0 + t * dx, z0 + t * dz); }
        return Array.AsReadOnly(points);
    }
    private static double Cross(double ax, double az, double bx, double bz) => ax * bz - bx * az;
}
public sealed class SeepageLinePoint
{
    public double Parameter { get; }
    public double X { get; }
    public double Z { get; }
    public double Pressure { get; }
    internal SeepageLinePoint(double t, double x, double z, double pressure) { Parameter = t; X = x; Z = z; Pressure = pressure; }
}
public sealed class SeepageGradientSample
{
    public double MaximumUpwardGradient { get; }
    public double MaximumAbsoluteHorizontalGradient { get; }
    public IReadOnlyList<int> Elements { get; }
    internal SeepageGradientSample(double up, double horizontal, int[] elements)
    { MaximumUpwardGradient = up; MaximumAbsoluteHorizontalGradient = horizontal; Elements = Array.AsReadOnly(elements); }
}
