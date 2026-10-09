namespace GPC.Checkers.Geotechnics.Piles;

public enum LateralGroupMethod { Davisson, Aashto, Fhwa, Rollins, ReeseVanImpe, Caltrans }
public sealed class GroupPile
{
    public string Id { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}
/// <summary>Coordinates, diameter and spacing must use the same length unit. Angle in degrees, counterclockwise from +X.</summary>
public sealed class LateralGroupInput
{
    public IReadOnlyList<GroupPile> Piles { get; set; } = Array.Empty<GroupPile>();
    public double Diameter { get; set; }
    public double LoadAngleDegrees { get; set; }
    public bool AllowExtrapolation { get; set; }
    public bool AcceptProjectedRows { get; set; }
    public double? RepresentativeParallelSpacing { get; set; }
    public double? RepresentativeTransverseSpacing { get; set; }
}
public sealed class GroupPileFactor
{
    public string Id { get; internal set; } = "";
    public int Row { get; internal set; }
    public double ParallelCoordinate { get; internal set; }
    public double TransverseCoordinate { get; internal set; }
    public double Beta { get; internal set; }
    public double Alpha { get; internal set; } = 1;
    public double Factor { get; internal set; }
}
public sealed class LateralGroupResult
{
    public LateralGroupMethod Method { get; internal set; }
    public string Quantity => Method == LateralGroupMethod.Davisson ? "Riduzione kh/nh · Rg" : "p-multiplier medio";
    public string Source { get; internal set; } = "";
    public double? Factor { get; internal set; }
    public string Error { get; internal set; } = "";
    public bool Available => Factor.HasValue && Error.Length == 0;
    public bool RegularGrid { get; internal set; }
    public double? ParallelSpacing { get; internal set; }
    public double? TransverseSpacing { get; internal set; }
    public IReadOnlyList<double> ProjectedRowGaps { get; internal set; } = Array.Empty<double>();
    public IReadOnlyList<double> ProjectedTransverseGaps { get; internal set; } = Array.Empty<double>();
    public IReadOnlyList<GroupPileFactor> Piles { get; internal set; } = Array.Empty<GroupPileFactor>();
    public List<string> Warnings { get; } = new List<string>();
}

/// <summary>Reduction factors only: no conversion to lateral capacity. Row 1 is the greatest projection along the applied load.</summary>
public static class LateralPileGroup
{
    public static string Source(LateralGroupMethod method) => method switch
    {
        LateralGroupMethod.Davisson => "Davisson (1970), Highway Research Record 333, pp. 104–112; tabella Rg ripresa nel testo di specifica.",
        LateralGroupMethod.Aashto => "AASHTO LRFD 2014, tabella 3D–5D riprodotta da FHWA GEC 12 (2016), volume I.",
        LateralGroupMethod.Fhwa => "FHWA-NHI-18-024 (2018), GEC 10, tabella 10-41 (interassi 3D–6D).",
        LateralGroupMethod.Rollins => "Rollins et al. (2006); FEMA P-751 (2012), relazioni logaritmiche per fila.",
        LateralGroupMethod.ReeseVanImpe => "Reese / Van Impe: interazioni palo-palo; equazioni riprodotte in Caltrans 2025 §10.7.2.4, (1)–(5), attribuite a Reese et al. (2006).",
        LateralGroupMethod.Caltrans => "California Amendments, settembre 2025, AASHTO LRFD 8th ed., §10.7.2.4, (1)–(6), tabella 10.7.2.4-2. Non prescrizione NTC.",
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };
    static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    public static void Validate(LateralGroupInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (!Finite(input.Diameter) || input.Diameter <= 0 || !Finite(input.LoadAngleDegrees)) throw new ArgumentException("Diametro positivo e angolo finito richiesti.");
        if (input.Piles == null || input.Piles.Count < 1 || input.Piles.Count > 500) throw new ArgumentException("Inserire da 1 a 500 pali.");
        if (input.Piles.Any(p => p == null || string.IsNullOrWhiteSpace(p.Id) || !Finite(p.X) || !Finite(p.Y)) || input.Piles.Select(p => p.Id).Distinct().Count() != input.Piles.Count)
            throw new ArgumentException("Coordinate finite e identificativi dei pali univoci richiesti.");
        foreach (var s in new[] { input.RepresentativeParallelSpacing, input.RepresentativeTransverseSpacing })
            if (s.HasValue && (!Finite(s.Value) || s.Value <= 0)) throw new ArgumentException("Gli interassi rappresentativi devono essere positivi e finiti.");
        for (int i = 0; i < input.Piles.Count; i++) for (int j = 0; j < i; j++)
        {
            double dx = (input.Piles[i].X - input.Piles[j].X) / input.Diameter, dy = (input.Piles[i].Y - input.Piles[j].Y) / input.Diameter;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (!Finite(distance) || distance < 1 - 1e-9) throw new ArgumentException("Pali sovrapposti, coincidenti o coordinate fuori scala: " + input.Piles[i].Id + " / " + input.Piles[j].Id);
        }
    }
    static List<double> Levels(IEnumerable<double> coordinates, double tolerance)
    {
        var levels = new List<double>();
        foreach (var c in coordinates.OrderBy(x => x)) if (levels.Count == 0 || c - levels[levels.Count - 1] > tolerance) levels.Add(c);
        return levels;
    }
    static double[] Gaps(List<double> levels) => levels.Skip(1).Select((v, i) => v - levels[i]).ToArray();
    static bool Uniform(double[] gaps, double tolerance) => gaps.Length < 2 || gaps.Max() - gaps.Min() <= tolerance;
    static double Interpolate(double x, double[] xs, double[] ys)
    {
        if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
        int i = 0; while (i < xs.Length - 2 && x > xs[i + 1]) i++;
        return ys[i] + (ys[i + 1] - ys[i]) * (x - xs[i]) / (xs[i + 1] - xs[i]);
    }
    static double Bound(double x) => Math.Max(0, Math.Min(1, x));
    public static LateralGroupResult Calculate(LateralGroupInput input, LateralGroupMethod method)
    {
        Validate(input);
        var result = new LateralGroupResult { Method = method, Source = Source(method) };
        double angle = (input.LoadAngleDegrees % 360) * Math.PI / 180, c = Math.Cos(angle), s = Math.Sin(angle), d = input.Diameter, tol = d * 1e-6;
        // Translation before projection avoids loss of precision for large site coordinates.
        var origin = input.Piles[0];
        var factors = input.Piles.Select(p => new GroupPileFactor { Id = p.Id, ParallelCoordinate = (p.X - origin.X) * c + (p.Y - origin.Y) * s, TransverseCoordinate = -(p.X - origin.X) * s + (p.Y - origin.Y) * c }).ToArray();
        if (factors.Any(p => !Finite(p.ParallelCoordinate) || !Finite(p.TransverseCoordinate))) throw new ArgumentException("Coordinate proiettate fuori scala.");
        var q = Levels(factors.Select(p => p.ParallelCoordinate), tol); var t = Levels(factors.Select(p => p.TransverseCoordinate), tol);
        var qg = Gaps(q); var tg = Gaps(t);
        foreach (var p in factors) p.Row = q.Count - q.FindIndex(v => Math.Abs(v - p.ParallelCoordinate) <= tol);
        result.Piles = factors; result.ProjectedRowGaps = qg; result.ProjectedTransverseGaps = tg;
        result.RegularGrid = q.Count * t.Count == factors.Length && Uniform(qg, tol) && Uniform(tg, tol);
        result.ParallelSpacing = input.RepresentativeParallelSpacing ?? (qg.Length > 0 && result.RegularGrid ? qg.Average() : (double?)null);
        result.TransverseSpacing = input.RepresentativeTransverseSpacing ?? (tg.Length > 0 && result.RegularGrid ? tg.Average() : (double?)null);
        if (factors.Length == 1) { factors[0].Beta = factors[0].Factor = 1; result.Factor = 1; return result; }
        bool pair = method == LateralGroupMethod.ReeseVanImpe || method == LateralGroupMethod.Caltrans;
        if (input.RepresentativeParallelSpacing.HasValue || input.RepresentativeTransverseSpacing.HasValue)
            result.Warnings.Add("Interassi rappresentativi assegnati dall'utente: applicazione approssimata, verificare la pertinenza al modello.");
        LateralGroupResult Fail(string message) { result.Error = message; return result; }
        if (!result.RegularGrid && method != LateralGroupMethod.ReeseVanImpe)
        {
            if (!input.AcceptProjectedRows) return Fail("Geometria irregolare o carico obliquo: abilitare esplicitamente l'estensione per file proiettate e assegnare gli interassi rappresentativi, oppure usare Reese / Van Impe.");
            result.Warnings.Add("File proiettate: estensione geometrica del metodo originale per direzione obliqua o geometria irregolare. La fila 1 è quella più avanzata nel verso H.");
            if (method != LateralGroupMethod.Caltrans && (!result.ParallelSpacing.HasValue || !result.TransverseSpacing.HasValue))
                return Fail("Per l'estensione geometrica assegnare entrambi gli interassi rappresentativi.");
        }
        if (method == LateralGroupMethod.Caltrans)
            result.ParallelSpacing = input.RepresentativeParallelSpacing ?? (qg.Length > 0 ? qg.Average() : (double?)null);
        double? lambda = result.ParallelSpacing / d;
        // Geometry projections can land a few ulps below a tabulated boundary after rigid rotation.
        foreach (double knot in new[] { 2d, 3d, 3.3, 4d, 5d, 5.65, 6d, 7d, 8d })
            if (lambda.HasValue && Math.Abs(lambda.Value - knot) < 1e-8) lambda = knot;
        if (method == LateralGroupMethod.Davisson)
        {
            if (result.TransverseSpacing / d < 2.5 - 1e-8) return Fail("S⊥ < 2.5D: fuori dalla condizione di Davisson per trascurare l'interazione trasversale; nessun coefficiente trasversale disponibile.");
            result.Warnings.Add("Nessuna ulteriore influenza trasversale secondo Davisson per S⊥ ≥ 2.5D (o unica colonna).");
            if (!lambda.HasValue && q.Count > 1) return Fail("Interasse parallelo non definito.");
            if (lambda < 3 && !input.AllowExtrapolation) return Fail("S∥ < 3D: interasse inferiore al campo minimo tabellato da Davisson.");
            if (lambda < 3) result.Warnings.Add("Davisson: estrapolazione esplicita sotto 3D; risultato limitato a [0,1].");
            result.Factor = lambda.HasValue ? Bound(0.15 * lambda.Value - 0.20) : 1;
            foreach (var p in factors) p.Beta = p.Factor = result.Factor.Value;
            result.Warnings.Add("Rg riduce kh/nh; non è un'efficienza di capacità Hgruppo/(N Hsingolo).");
            return result;
        }
        if (!pair)
        {
            if (!lambda.HasValue) return Fail("Metodo per file: interasse parallelo non definito (unica fila). Usare un metodo palo-palo.");
            double min = method == LateralGroupMethod.Rollins ? 3.3 : 3;
            bool outside = lambda < min || (method == LateralGroupMethod.Rollins && lambda > 5.65);
            if (outside && !input.AllowExtrapolation) return Fail("Interasse fuori dal campo del metodo: abilitare esplicitamente le estrapolazioni.");
            if (outside) result.Warnings.Add("Estrapolazione esplicita oltre il campo tabellato/sperimentale; coefficienti limitati a [0,1].");
            if (method == LateralGroupMethod.Aashto && lambda > 5) result.Warnings.Add("AASHTO: oltre 5D si mantengono i coefficienti di 5D, senza estrapolare verso 1.");
            result.Warnings.Add("Metodo per file: non include una legge di interazione trasversale; verificare l'applicabilità alla disposizione dei pali e al terreno.");
            if (method == LateralGroupMethod.Rollins) result.Warnings.Add("Rollins: calibrazione principalmente in argilla rigida, S/D circa 3.3–5.65.");
            foreach (var p in factors)
            {
                int row = Math.Min(p.Row, 3) - 1; double x = lambda.Value;
                double value = method == LateralGroupMethod.Aashto ? Interpolate(x, new[] { 3d, 5d }, new[] { new[] { .8, 1d }, new[] { .4, .85 }, new[] { .3, .7 } }[row])
                    : method == LateralGroupMethod.Fhwa ? Interpolate(x, new[] { 3d, 4d, 5d, 6d }, new[] { new[] { .7, .85, 1d, 1d }, new[] { .5, .65, .85, 1d }, new[] { .35, .5, .7, 1d } }[row])
                    : row == 0 ? .26 * Math.Log(x) + .5 : row == 1 ? .52 * Math.Log(x) : .60 * Math.Log(x) - .25;
                p.Beta = p.Factor = Bound(value);
            }
        }
        else
        {
            if (method == LateralGroupMethod.Caltrans && lambda < 2 && !input.AllowExtrapolation) return Fail("Caltrans: interasse medio fra file inferiore a 2D, fuori tabella α.");
            if (method == LateralGroupMethod.Caltrans && lambda < 2) result.Warnings.Add("Caltrans: estrapolazione esplicita di α sotto 2D, limitata a [0,1].");
            foreach (var p in factors)
            {
                p.Beta = 1;
                foreach (var other in factors)
                {
                    if (ReferenceEquals(other, p)) continue;
                    double dq = (p.ParallelCoordinate - other.ParallelCoordinate) / d, dt = (p.TransverseCoordinate - other.TransverseCoordinate) / d;
                    double r = Math.Sqrt(dq * dq + dt * dt), ratio = r;
                    double a = ratio >= 3.75 ? 1 : Bound(.64 * Math.Pow(ratio, .34));
                    bool leading = dq > 0;
                    double b = leading ? (ratio >= 4 ? 1 : Bound(.70 * Math.Pow(ratio, .26))) : (ratio >= 7 ? 1 : Bound(.48 * Math.Pow(ratio, .38)));
                    p.Beta *= Math.Sqrt(b * b * (dq / r) * (dq / r) + a * a * (dt / r) * (dt / r));
                }
                if (method == LateralGroupMethod.Caltrans && lambda.HasValue)
                    p.Alpha = Bound(Interpolate(lambda.Value, new[] { 2d, 3d, 5d, 7d, 8d }, new[] { new[] { 1d, .9, 1d, 1d, 1d }, new[] { 1d, 1d, 1d, 1d, 1d }, new[] { 1d, .8, .8, .9, 1d }, new[] { 1d, .8, .9, 1d, 1d } }[Math.Min(p.Row, 4) - 1]));
                p.Factor = p.Alpha * p.Beta;
            }
            if (method == LateralGroupMethod.Caltrans) result.Warnings.Add("Caltrans 2025: α usa la media degli interassi fra file nella direzione H. Per unica fila α=1; oltre 8D α=1. Non prescrizione NTC.");
        }
        result.Factor = factors.Average(p => p.Factor);
        result.Warnings.Add("La media dei p-multiplier è un indicatore di riduzione per pali identici, non una verifica della capacità laterale del gruppo.");
        return result;
    }
}
