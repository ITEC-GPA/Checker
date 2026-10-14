namespace GPC.Design.Bridges;

public enum TrafficModel { LegacyUniform, Ntc2018Schema1 }
public sealed record TrafficLane(int Number, int Carriageway, double Width, double AxleKn, double UniformKnPerSquareMetre);
public sealed record RoadTraffic(IReadOnlyList<TrafficLane> Lanes, double RemainingWidth, double UniformKnPerMetre, double AxleKn)
{
    public const double AxleSpacing = 1.2;
    public const double NtcUnfavourableGamma = 1.35;
    public const string Reference = "NTC 2018 §5.1.3.3, Tabelle 5.1.I e 5.1.II; parte rimanente EN 1991-2 Tabella 4.2";
    /// <summary>Global longitudinal action. Numbering is common to carriageways divided by a fixed barrier.</summary>
    public static RoadTraffic Ntc2018(params double[] carriageways)
    {
        if (carriageways.Length == 0 || carriageways.Any(w => !double.IsFinite(w) || w < 3 || w > 100))
            throw new ArgumentException("Ogni carreggiata deve avere una larghezza fra 3 e 100 m.");
        var lanes = new List<TrafficLane>(); double remaining = 0;
        for (int c = 0; c < carriageways.Length; c++)
        {
            double w = carriageways[c]; int n = w < 5.4 ? 1 : w < 6 ? 2 : (int)Math.Floor(w / 3);
            double width = w >= 5.4 && w < 6 ? w / 2 : 3;
            remaining += w - n * width;
            for (int k = 0; k < n; k++)
            {
                int number = lanes.Count + 1;
                lanes.Add(new(number, c + 1, width, number switch { 1 => 300, 2 => 200, 3 => 100, _ => 0 }, number == 1 ? 9 : 2.5));
            }
        }
        // On unequal carriageways lane 1 occupies the widest conventional lane for the global effect.
        lanes = lanes.OrderByDescending(l => l.Width).Select((lane, k) => lane with { Number = k + 1,
            AxleKn = k switch { 0 => 300, 1 => 200, 2 => 100, _ => 0 }, UniformKnPerSquareMetre = k == 0 ? 9 : 2.5 }).ToList();
        return new(lanes.AsReadOnly(), remaining, lanes.Sum(l => l.Width * l.UniformKnPerSquareMetre) + remaining * 2.5, lanes.Sum(l => l.AxleKn));
    }
}
