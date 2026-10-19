using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;

namespace GPC.Checkers.Geotechnics.Seepage;

/// <summary>One-way stationary hydraulic-to-mechanical coupling. No consolidation or suction strengthening.
/// Unit-weight saturation zones remain explicit in the mechanical stage.</summary>
public static class SeepageWallCoupling
{
    public static double PressureAt(SeepageResult result, double x, double z)
    {
        if (result == null || !result.IsComplete) throw new ArgumentException("Le pressioni richiedono una filtrazione convergente.");
        if (!UnsaturatedConductivity.Finite(x) || !UnsaturatedConductivity.Finite(z)) throw new ArgumentException("Coordinate non finite.");
        double? value = null;
        foreach (var e in result.Elements)
        {
            var a = result.Nodes[e.A]; var b = result.Nodes[e.B]; var c = result.Nodes[e.C];
            double cross = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
            double v = ((x - a.X) * (c.Z - a.Z) - (c.X - a.X) * (z - a.Z)) / cross;
            double w = ((b.X - a.X) * (z - a.Z) - (x - a.X) * (b.Z - a.Z)) / cross;
            if (v < -1e-9 || w < -1e-9 || v + w > 1 + 1e-9) continue;
            double pressure = SoilUnits.WaterUnitWeight * (a.Head + v * (b.Head - a.Head) + w * (c.Head - a.Head) - z);
            if (value.HasValue && Math.Abs(value.Value - pressure) > 1e-9) throw new ArgumentException("Quota su bordi coincidenti con pressioni diverse: scegliere il lato idraulico.");
            value = pressure;
        }
        return value ?? throw new ArgumentException("Punto fuori dal dominio idraulico: nessuna estrapolazione della pressione.");
    }

    /// <summary>Exact piecewise-linear pressure along a vertical mesh cut, including zero crossings.
    /// If ignoreSuction is true, negative pressures are explicitly replaced by zero for the wall soil law.</summary>
    public static IReadOnlyList<WallPorePressure> Profile(SeepageResult result, double x, double top, double bottom, bool ignoreSuction)
    {
        if (result == null || !result.IsComplete) throw new ArgumentException("Filtrazione incompleta: pressioni non trasferibili.");
        if (!UnsaturatedConductivity.Finite(top) || !UnsaturatedConductivity.Finite(bottom) || top <= bottom) throw new ArgumentException("Intervallo verticale non valido.");
        var levels = new SortedSet<double> { top, bottom };
        foreach (var t in result.Elements)
        {
            int[] ids = { t.A, t.B, t.C };
            for (int i = 0; i < 3; i++)
            {
                var a = result.Nodes[ids[i]]; var b = result.Nodes[ids[(i + 1) % 3]];
                if (a.X == b.X)
                { if (x == a.X) { if (a.Z > bottom && a.Z < top) levels.Add(a.Z); if (b.Z > bottom && b.Z < top) levels.Add(b.Z); } }
                else if (x >= Math.Min(a.X, b.X) && x <= Math.Max(a.X, b.X))
                { double z = a.Z + (x - a.X) / (b.X - a.X) * (b.Z - a.Z); if (z > bottom && z < top) levels.Add(z); }
            }
        }
        var raw = levels.Reverse().Select(z => new WallPorePressure(z, PressureAt(result, x, z))).ToArray();
        // End points alone cannot detect a cut through a gap; sample the interior of each intersected interval too.
        for (int i = 1; i < raw.Length; i++) PressureAt(result, x, (raw[i - 1].Elevation + raw[i].Elevation) / 2);
        if (!ignoreSuction && raw.Any(p => p.Pressure < -1e-12)) throw new ArgumentException("Suzione nel profilo: esplicitare l'ipotesi di trascurarla prima di trasferire le pressioni.");
        var points = new List<WallPorePressure>();
        for (int i = 0; i < raw.Length; i++)
        {
            if (i > 0 && raw[i - 1].Pressure * raw[i].Pressure < 0)
            {
                double f = raw[i - 1].Pressure / (raw[i - 1].Pressure - raw[i].Pressure);
                points.Add(new(raw[i - 1].Elevation + f * (raw[i].Elevation - raw[i - 1].Elevation), 0));
            }
            points.Add(new(raw[i].Elevation, Math.Max(0, raw[i].Pressure)));
        }
        return points.AsReadOnly();
    }
}
