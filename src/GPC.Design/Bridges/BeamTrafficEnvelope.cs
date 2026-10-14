namespace GPC.Design.Bridges;

public readonly record struct TrafficRange(double Minimum, double Maximum);
public sealed record TrafficSection(int Span, double X, TrafficRange Moment, TrafficRange Shear, TrafficRange DeflectionMm);
public sealed record TrafficEnvelopeResult(IReadOnlyList<TrafficRange> Reactions, IReadOnlyList<TrafficSection> Sections);

/// <summary>
/// Prismatic Euler–Bernoulli beam. Unit-load influence functions are piecewise cubics.
/// Integrates only the unfavourable UDL regions and extremizes the COMPLETE two-axle tandem analytically.
/// Each ordinate has its own load arrangement; envelope reactions must never be summed as one load case.
/// </summary>
public sealed class BeamTrafficEnvelope
{
    private readonly double[] spans, offsets;
    private readonly P[][] moments;
    private readonly RoadTraffic traffic;
    public BeamTrafficEnvelope(IReadOnlyList<double> spans, bool continuous, RoadTraffic traffic)
    {
        ArgumentNullException.ThrowIfNull(spans); ArgumentNullException.ThrowIfNull(traffic);
        this.spans = spans.ToArray(); this.traffic = traffic;
        if (spans.Count is < 1 or > 30 || spans.Any(x => !double.IsFinite(x) || x < 2) ||
            !double.IsFinite(traffic.UniformKnPerMetre) || traffic.UniformKnPerMetre < 0 || !double.IsFinite(traffic.AxleKn) || traffic.AxleKn < 0)
            throw new ArgumentException("Dati di trave o traffico non validi.");
        offsets = new double[spans.Count + 1];
        for (int k = 0; k < spans.Count; k++) offsets[k + 1] = offsets[k] + spans[k];
        moments = new P[spans.Count][];
        for (int loaded = 0; loaded < spans.Count; loaded++)
        {
            var support = new P[spans.Count + 1]; moments[loaded] = support;
            if (!continuous || spans.Count == 1) continue;
            int n = spans.Count - 1; var diag = new double[n]; var rhs = new P[n]; double l = spans[loaded];
            // Three-moment equation: -6 times the first moments of the simply-supported point-load diagram / L.
            if (loaded > 0) rhs[loaded - 1] = new(0, -2 * l * l, 3 * l * l, -l * l);
            if (loaded < n) rhs[loaded] = new(0, -l * l, 0, l * l);
            for (int k = 0; k < n; k++)
            {
                diag[k] = 2 * (spans[k] + spans[k + 1]);
                if (k > 0) { double f = spans[k] / diag[k - 1]; diag[k] -= f * spans[k]; rhs[k] -= rhs[k - 1] * f; }
            }
            for (int k = n - 1; k >= 0; k--) support[k + 1] = (rhs[k] - (k < n - 1 ? support[k + 2] * spans[k + 1] : default)) / diag[k];
        }
    }
    public TrafficRange Reaction(int support)
    {
        if (support < 0 || support > spans.Length) throw new ArgumentOutOfRangeException(nameof(support));
        var pieces = new List<Piece>();
        for (int j = 0; j < spans.Length; j++)
        {
            P reaction = default;
            if (support > 0) reaction -= (moments[j][support] - moments[j][support - 1]) / spans[support - 1];
            if (support < spans.Length) reaction += (moments[j][support + 1] - moments[j][support]) / spans[support];
            if (j == support) reaction += new P(1, -1, 0, 0);
            if (j == support - 1) reaction += new P(0, 1, 0, 0);
            pieces.Add(new(offsets[j], offsets[j + 1], reaction));
        }
        return Envelope(pieces);
    }
    /// <param name="span">Zero-based span index.</param><param name="x">Distance from its left support, in metres.</param>
    /// <param name="ei">Flexural rigidity in kN m²; deflection is positive downwards, in mm.</param>
    public TrafficSection At(int span, double x, double ei)
    {
        if (span < 0 || span >= spans.Length || !double.IsFinite(x) || x < 0 || x > spans[span] || !double.IsFinite(ei) || ei <= 0)
            throw new ArgumentException("Sezione o rigidezza non valida.");
        var bending = new List<Piece>(); var shear = new List<Piece>(); var deflection = new List<Piece>();
        double l = spans[span];
        for (int j = 0; j < spans.Length; j++)
        {
            var ml = moments[j][span]; var r = (moments[j][span + 1] - ml) / l;
            if (j == span) r += new P(1, -1, 0, 0);
            var c = (ml * (l * l / 2) + r * (l * l * l / 6)) * (-1 / l);
            if (j == span) c += new P(l * l / 6, -l * l / 2, l * l / 2, -l * l / 6);
            void Add(double from, double to, bool loadBefore)
            {
                if (to <= from) return;
                P m = ml + r * x, v = r, d = ml * (x * x / 2) + r * (x * x * x / 6) + c * x;
                if (j == span && loadBefore)
                {
                    m -= new P(x, -l, 0, 0); v -= new P(1, 0, 0, 0);
                    d -= new P(x * x * x / 6, -x * x * l / 2, x * l * l / 2, -l * l * l / 6);
                }
                double a = offsets[j] + from * spans[j], b = offsets[j] + to * spans[j];
                bending.Add(new(a, b, m.Transform(from, to - from)));
                shear.Add(new(a, b, v.Transform(from, to - from)));
                deflection.Add(new(a, b, (d * (-1000 / ei)).Transform(from, to - from)));
            }
            if (j == span) { Add(0, x / l, true); Add(x / l, 1, false); }
            else Add(0, 1, false);
        }
        return new(span + 1, offsets[span] + x, Envelope(bending), Envelope(shear), Envelope(deflection));
    }
    public TrafficEnvelopeResult Calculate(double ei, CancellationToken cancellation = default)
    {
        var reactions = Enumerable.Range(0, spans.Length + 1).Select(Reaction).ToArray(); var sections = new List<TrafficSection>();
        for (int k = 0; k < spans.Length; k++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int t = 0; t <= 40; t++) sections.Add(At(k, spans[k] * t / 40, ei));
        }
        return new(Array.AsReadOnly(reactions), sections.AsReadOnly());
    }
    private TrafficRange Envelope(List<Piece> pieces)
    {
        double negative = 0, positive = 0;
        foreach (var piece in pieces)
        {
            var cuts = new List<double> { 0 }; cuts.AddRange(piece.Coefficients.Roots()); cuts.Add(1);
            for (int k = 1; k < cuts.Count; k++)
            {
                double a = cuts[k - 1], b = cuts[k];
                double integral = (piece.Coefficients.Integral(b) - piece.Coefficients.Integral(a)) * (piece.End - piece.Start);
                if (piece.Coefficients.Value((a + b) / 2) < 0) negative += integral; else positive += integral;
            }
        }
        double lower = 0, upper = 0, end = offsets[^1] - RoadTraffic.AxleSpacing;
        var boundaries = pieces.SelectMany(p => new[] { p.Start, p.End, p.Start - RoadTraffic.AxleSpacing, p.End - RoadTraffic.AxleSpacing })
            .Where(x => x > 0 && x < end).Append(0).Append(end).Distinct().Order().ToArray();
        for (int k = 1; k < boundaries.Length; k++)
        {
            double a = boundaries[k - 1], b = boundaries[k], mid = (a + b) / 2;
            P Coeff(double shift)
            {
                var p = pieces.First(p => mid + shift >= p.Start && mid + shift <= p.End);
                return p.Coefficients.Transform((a + shift - p.Start) / (p.End - p.Start), (b - a) / (p.End - p.Start));
            }
            var tandem = Coeff(0) + Coeff(RoadTraffic.AxleSpacing);
            foreach (double t in tandem.Critical().Append(0).Append(1))
            { double value = tandem.Value(t); lower = Math.Min(lower, value); upper = Math.Max(upper, value); }
        }
        return new(negative * traffic.UniformKnPerMetre + lower * traffic.AxleKn, positive * traffic.UniformKnPerMetre + upper * traffic.AxleKn);
    }
    private readonly record struct Piece(double Start, double End, P Coefficients);
    // Polynomial in a normalized local coordinate, avoiding ill-conditioning for long bridges.
    private readonly record struct P(double A, double B, double C, double D)
    {
        public static P operator +(P a, P b) => new(a.A + b.A, a.B + b.B, a.C + b.C, a.D + b.D);
        public static P operator -(P a, P b) => new(a.A - b.A, a.B - b.B, a.C - b.C, a.D - b.D);
        public static P operator *(P p, double s) => new(p.A * s, p.B * s, p.C * s, p.D * s);
        public static P operator /(P p, double s) => p * (1 / s);
        public double Value(double t) => ((D * t + C) * t + B) * t + A;
        public double Integral(double t) => (((D * t / 4 + C / 3) * t + B / 2) * t + A) * t;
        public P Transform(double a, double b) => new(Value(a), b * (B + 2 * C * a + 3 * D * a * a), b * b * (C + 3 * D * a), D * b * b * b);
        public IEnumerable<double> Critical()
        {
            double a = 3 * D, b = 2 * C, c = B, scale = Math.Max(Math.Abs(a), Math.Max(Math.Abs(b), Math.Abs(c)));
            if (scale == 0) yield break;
            if (Math.Abs(a) <= scale * 1e-14)
            { if (Math.Abs(b) > scale * 1e-14) { double t = -c / b; if (t > 0 && t < 1) yield return t; } yield break; }
            double disc = b * b - 4 * a * c; if (disc < 0) yield break;
            double q = -.5 * (b + Math.CopySign(Math.Sqrt(disc), b));
            double first = q / a, second = q == 0 ? 0 : c / q;
            if (first > 0 && first < 1) yield return first;
            if (second > 0 && second < 1 && second != first) yield return second;
        }
        public IEnumerable<double> Roots()
        {
            var points = Critical().Append(0).Append(1).Order().ToArray();
            for (int k = 1; k < points.Length; k++)
            {
                double a = points[k - 1], b = points[k], fa = Value(a), fb = Value(b);
                if (a > 0 && fa == 0) yield return a;
                if (Math.Sign(fa) == Math.Sign(fb) || fa == 0 || fb == 0) continue;
                for (int j = 0; j < 50; j++) { double m = (a + b) / 2; if (Math.Sign(Value(m)) == Math.Sign(fa)) a = m; else b = m; }
                yield return (a + b) / 2;
            }
        }
    }
}
