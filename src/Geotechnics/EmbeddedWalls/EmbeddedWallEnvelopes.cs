namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

public static partial class EmbeddedWallAnalysis
{
    /// <summary>Sample a committed phase at the requested one-sided limit. No new equilibrium solution and no
    /// interpolation between table rows. Outside-domain and outward endpoint limits are rejected explicitly.</summary>
    public static WallPointResult Sample(WallStageResult stage, double elevation, WallResultSide side)
    {
        if (stage is null) throw new ArgumentNullException(nameof(stage));
        Require(Finite(elevation) && Enum.IsDefined(typeof(WallResultSide), side), "Finite elevation and defined side required.");
        var element = stage.Elements.FirstOrDefault(e => side == WallResultSide.Above
            ? elevation < e.Top && elevation >= e.Bottom : elevation <= e.Top && elevation > e.Bottom);
        if (element is null) throw new ArgumentOutOfRangeException(nameof(elevation), "No recovered field on the requested side of this elevation.");
        string label = elevation == element.Top ? "Below" : elevation == element.Bottom ? "Above" : "Interior";
        return SampleElement(element, elevation, label);
    }

    /// <summary>Pointwise min/max of actual completed phases from independent nonlinear histories, with concomitant
    /// actions and provenance. Same wall bounds and tributary width required; different meshes are supported.
    /// Includes all element boundaries on both sides and every stationary point of each recovered polynomial.
    /// Global outer extrema are retained. The display grid uses additional equal subdivisions; curves between
    /// stations are a drawing approximation, not an exact continuous envelope or a new structural load case.</summary>
    public static WallEnvelopeResult Envelope(IEnumerable<WallAnalysisResult> histories, int subdivisions = 8,
        CancellationToken cancellation = default)
    {
        if (histories is null) throw new ArgumentNullException(nameof(histories));
        cancellation.ThrowIfCancellationRequested();
        var inputs = histories.ToArray();
        Require(subdivisions >= 1 && subdivisions <= 64 && inputs.Length > 0 && inputs.All(r => r is not null), "Histories and 1..64 display subdivisions required.");
        Require(inputs.Select(r => r.CaseId).Distinct(StringComparer.Ordinal).Count() == inputs.Length
            && inputs.All(r => !string.IsNullOrWhiteSpace(r.CaseId)), "Distinct nonempty case identifiers required.");
        var ordered = inputs.OrderBy(r => r.CaseId, StringComparer.Ordinal).ToArray(); var first = ordered[0];
        Require(Finite(first.Top) && Finite(first.Bottom) && first.Top > first.Bottom && Finite(first.TributaryWidth) && first.TributaryWidth > 0
            && ordered.All(r => r.Top == first.Top && r.Bottom == first.Bottom && r.TributaryWidth == first.TributaryWidth),
            "Envelopes require identical wall bounds and declared tributary widths; geometrically different candidates must remain separate.");
        var result = new WallEnvelopeResult { Top = first.Top, Bottom = first.Bottom, TributaryWidth = first.TributaryWidth,
            Subdivisions = subdivisions, IsComplete = ordered.All(r => r.IsComplete && r.Stages.Count > 0),
            Histories = Array.AsReadOnly(ordered.Select(r => new WallEnvelopeHistory { CaseId = r.CaseId, Complete = r.IsComplete,
                FailedStage = r.FailedStage, IncludedStages = Array.AsReadOnly(r.Stages.Select(s => s.Id).ToArray()), Diagnostic = r.Diagnostic }).ToArray()) };
        var stages = ordered.SelectMany(r => r.Stages.OrderBy(s => s.Id, StringComparer.Ordinal)).ToArray();
        foreach (var history in ordered)
        {
            Require(history.Stages.All(s => s.CaseId == history.CaseId && !string.IsNullOrWhiteSpace(s.Id))
                && history.Stages.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() == history.Stages.Count, "Inconsistent phase identity.");
            foreach (var stage in history.Stages)
            {
                Require(stage.Elements.Count > 0 && stage.Elements[0].Top == first.Top && stage.Elements.Last().Bottom == first.Bottom,
                    "A complete recovered field is required for every included phase.");
                double next = first.Top;
                foreach (var e in stage.Elements)
                {
                    Require(e.Top == next && e.Bottom < e.Top && e.Coefficients.All(p => p.All(Finite)), "Invalid recovered field.");
                    next = e.Bottom;
                }
            }
        }
        result.Diagnostic = result.IsComplete ? "Inviluppo delle storie convergenti richieste; estremi con fase, caso e azioni concomitanti. Non è una combinazione di carico."
            : "Inviluppo parziale delle sole fasi concluse: almeno una storia è incompleta. Non rappresenta l'intero percorso richiesto.";
        if (stages.Length == 0) return result;
        var levels = stages.SelectMany(s => s.Elements.Select(e => e.Top).Append(s.Elements.Last().Bottom)).Distinct().OrderByDescending(z => z).ToArray();
        var series = new List<WallEnvelopeSeries>(); int totalPoints = 0;
        foreach (WallResultQuantity quantity in Enum.GetValues(typeof(WallResultQuantity)))
        {
            var rows = new List<WallEnvelopeStation>(); int index = (int)quantity;
            for (int interval = 1; interval < levels.Length; interval++)
            {
                cancellation.ThrowIfCancellationRequested(); double top = levels[interval - 1], bottom = levels[interval];
                // Select by the lower one-sided limit at the interval top, avoiding a midpoint rounded onto a boundary.
                var fields = stages.Select(s => s.Elements.First(e => top <= e.Top && top > e.Bottom)).ToArray();
                var stations = new SortedSet<double>(Enumerable.Range(0, subdivisions + 1).Select(i =>
                    i == 0 ? top : i == subdivisions ? bottom : top - (top - bottom) * i / subdivisions));
                foreach (var e in fields)
                {
                    var p = e.Coefficients[index]; var derivative = Enumerable.Range(1, p.Length - 1).Select(i => i * p[i]).ToArray();
                    if (derivative.Length == 0) continue;
                    foreach (double t in PolynomialRoots(derivative))
                    {
                        double z = e.Top - (e.Top - e.Bottom) * t;
                        if (z < top && z > bottom) stations.Add(z);
                    }
                }
                totalPoints += stations.Count;
                Require(totalPoints <= 1000000, "Envelope output exceeds one million stations; reduce the display sampling or split the requested histories.");
                foreach (double z in stations.Reverse())
                {
                    string side = z == top ? "Below" : z == bottom ? "Above" : "Interior";
                    int min = 0, max = 0; double low = double.PositiveInfinity, high = double.NegativeInfinity;
                    for (int i = 0; i < fields.Length; i++)
                    {
                        double value = Value(fields[i].Coefficients[index], (fields[i].Top - z) / (fields[i].Top - fields[i].Bottom));
                        Require(Finite(value), "Nonfinite recovered observation.");
                        if (value < low) { low = value; min = i; }
                        if (value > high) { high = value; max = i; }
                    }
                    WallEnvelopeObservation Observation(int i, double value) => new() { CaseId = stages[i].CaseId, StageId = stages[i].Id,
                        Value = value, Point = SampleElement(fields[i], z, side) };
                    rows.Add(new WallEnvelopeStation { Elevation = z, Side = side, Minimum = Observation(min, low), Maximum = Observation(max, high) });
                }
            }
            series.Add(new WallEnvelopeSeries { Quantity = quantity, Stations = rows.AsReadOnly(),
                Minimum = rows.Select(r => r.Minimum).OrderBy(r => r.Value).First(), Maximum = rows.Select(r => r.Maximum).OrderByDescending(r => r.Value).First() });
        }
        result.Series = series.AsReadOnly(); return result;
    }

    private static WallPointResult SampleElement(WallRecoveredElement e, double z, string side)
    {
        double t = (e.Top - z) / (e.Top - e.Bottom);
        int nearest = Enumerable.Range(0, 4).OrderBy(i => Math.Abs(t - Gauss[i])).First();
        var v = e.Coefficients.Select(p => Value(p, t)).ToArray();
        Require(v.All(Finite), "Nonfinite recovered field.");
        return new WallPointResult { Elevation = z, Side = side, SectionId = e.SectionId, Displacement = v[0], Rotation = v[1],
            NormalForce = v[2], Shear = v[3], Moment = v[4], LeftPressure = v[5], RightPressure = v[6], NetDistributedLoad = v[7],
            LeftPlastic = e.LeftPlastic[nearest], RightPlastic = e.RightPlastic[nearest] };
    }
}
