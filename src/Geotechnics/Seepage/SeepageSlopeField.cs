using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Seepage;

/// <summary>Immutable stationary field adapter for the existing Bishop solver. Coordinates:
/// field x = originX + xDirection*slopeX; field z = originZ + slopeY (mm).
/// Saturated weight where pressure is positive, natural weight elsewhere. No capillary density model.</summary>
public sealed class SeepageSlopeField : ISlopeHydraulicField
{
    private readonly SeepageResult field;
    private readonly double originX, originZ;
    private readonly int xDirection;
    private readonly bool ignoreSuction;
    public string Source { get; }
    public double WaterUnitWeight => SoilUnits.WaterUnitWeight;
    public SeepageSlopeField(SeepageResult field, string source, double originX = 0, double originZ = 0, int xDirection = 1, bool ignoreSuction = false)
    {
        if (field == null || !field.IsComplete) throw new ArgumentException("Stabilità globale: campo idraulico convergente richiesto.");
        if (string.IsNullOrWhiteSpace(source) || !UnsaturatedConductivity.Finite(originX) || !UnsaturatedConductivity.Finite(originZ) || xDirection is not (-1 or 1))
            throw new ArgumentException("Fonte, origine finita e verso ±1 del campo idraulico richiesti.");
        this.field = field; this.originX = originX; this.originZ = originZ; this.xDirection = xDirection; this.ignoreSuction = ignoreSuction;
        Source = source + "; campo " + field.CaseId + (ignoreSuction ? "; suzione trascurata alla base dei conci" : "; nessuna suzione ammessa alla base dei conci") + "; γsat per u>0, γ naturale altrove";
    }
    public double Pressure(double x, double y)
    {
        double pressure = SeepageWallCoupling.PressureAt(field, originX + xDirection * x, originZ + y);
        if (!ignoreSuction && pressure < -1e-12) throw new ArgumentException("Suzione sulla superficie di scorrimento: dichiarare la scelta di trascurarla; nessun incremento di resistenza implicito.");
        return Math.Max(0, pressure);
    }
    public IReadOnlyList<SlopeMoistureInterval> Column(double x, double bottom, double top)
    {
        if (!(top > bottom)) throw new ArgumentException("Intervallo della colonna idraulica non valido.");
        var line = SeepageSampling.Line(field, originX + xDirection * x, originZ + bottom, originX + xDirection * x, originZ + top);
        var intervals = new List<SlopeMoistureInterval>();
        for (int i = 1; i < line.Count; i++)
        {
            var a = line[i - 1]; var b = line[i]; double low = a.Z - originZ, high = b.Z - originZ;
            if (a.Pressure * b.Pressure < 0)
            {
                double zero = low + (high - low) * a.Pressure / (a.Pressure - b.Pressure);
                Add(low, zero, a.Pressure > 0); Add(zero, high, b.Pressure > 0);
            }
            else Add(low, high, a.Pressure > 0 || b.Pressure > 0);
        }
        void Add(double low, double high, bool wet)
        {
            if (high <= low) return;
            if (intervals.Count > 0 && intervals[intervals.Count - 1].Saturated == wet)
            { var previous = intervals[intervals.Count - 1]; intervals[intervals.Count - 1] = new(previous.Bottom, high, wet); }
            else intervals.Add(new(low, high, wet));
        }
        return intervals.AsReadOnly();
    }
}
