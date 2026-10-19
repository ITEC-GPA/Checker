namespace GPC.Design.Actions;

/// <summary>
/// Fixed, compiled compatibility calculation of the CSLP grid. Cell identities in the generated part are retained for audit.
/// Bulk nearest-node searches replace the workbook's identical 10751-row distance/MIN/MAX/MATCH operations.
/// No formulas or executable code are loaded from documents at runtime.
/// </summary>
internal sealed partial class Cslp103Grid
{
    private readonly double longitude, latitude;
    private readonly Dictionary<string, V> cache = new(StringComparer.Ordinal);
    private readonly V[,] nodeX, nodeY;
    internal Cslp103Grid(double longitude, double latitude)
    {
        this.longitude = Math.Round(longitude, 5, MidpointRounding.AwayFromZero);
        this.latitude = Math.Round(latitude, 5, MidpointRounding.AwayFromZero);
        var nodes = Cslp103Data.Nodes;
        nodeX = new V[nodes.Length, 1]; nodeY = new V[nodes.Length, 1];
        for (int i = 0; i < nodes.Length; i++) { nodeX[i, 0] = N(nodes[i].X); nodeY[i, 0] = N(nodes[i].Y); }
    }
    internal double Number(string cell) => G(cell).Number;
    private V G(string cell)
    {
        if (cache.TryGetValue(cell, out var value)) return value;
        return cache[cell] = Formula(cell);
    }
    private int Nearest(double x, double y)
    {
        var nodes = Cslp103Data.Nodes; double min = double.PositiveInfinity; int selected = -1;
        for (int i = 0; i < nodes.Length; i++)
        {
            var dx = nodes[i].X - x; var dy = nodes[i].Y - y; var d = Math.Sqrt(dx * dx + dy * dy);
            if (d < min || (d == min && nodes[i].Id > nodes[selected].Id)) { min = d; selected = i; }
        }
        return selected + 1; // Excel INDEX is one-based.
    }
    private V[,] R(string range)
    {
        var ends = range.Split(':'); var (c1, r1) = Address(ends[0]); var (c2, r2) = Address(ends[1]);
        var values = new V[r2 - r1 + 1, c2 - c1 + 1];
        for (int r = r1; r <= r2; r++) for (int c = c1; c <= c2; c++) values[r-r1,c-c1] = G(Column(c) + r);
        return values;
    }
    private static (int Column, int Row) Address(string value)
    {
        int c = 0, i = 0; while (i < value.Length && char.IsLetter(value[i])) c = c * 26 + value[i++] - 'A' + 1;
        return (c, int.Parse(value[i..], System.Globalization.CultureInfo.InvariantCulture));
    }
    private static string Column(int c) { string s = ""; do { c--; s = (char)('A' + c % 26) + s; c /= 26; } while(c > 0); return s; }
    private readonly record struct V(object Value)
    {
        internal double Number => Value switch { double d => d, bool b => b ? 1 : 0, _ => throw new InvalidOperationException("Valore CSLP non numerico: " + Value) };
        public static V operator +(V a,V b) => N(a.Number+b.Number);
        public static V operator -(V a,V b) => N(a.Number-b.Number);
        public static V operator -(V a) => N(-a.Number);
        public static V operator *(V a,V b) => N(a.Number*b.Number);
        public static V operator /(V a,V b) => N(a.Number/b.Number);
    }
    private static V N(double x) => new(x);
    private static V S(string s) => new(s);
    private static V B(bool b) => new(b);
    private static bool Truth(V v) => v.Value is bool b ? b : v.Number != 0;
    private static V Eq(V a,V b) => B(a.Value.Equals(b.Value));
    private static V Ne(V a,V b) => B(!Truth(Eq(a,b)));
    private static V Lt(V a,V b) => B(a.Number<b.Number);
    private static V Gt(V a,V b) => B(a.Number>b.Number);
    private static V Le(V a,V b) => B(a.Number<=b.Number);
    private static V Ge(V a,V b) => B(a.Number>=b.Number);
    private static V Pow(V a,V b) => N(Math.Pow(a.Number,b.Number));
    private static V Abs(V a) => N(Math.Abs(a.Number));
    private static V Sqrt(V a) => N(Math.Sqrt(a.Number));
    private static V Sin(V a) => N(Math.Sin(a.Number));
    private static V Cos(V a) => N(Math.Cos(a.Number));
    private static V Tan(V a) => N(Math.Tan(a.Number));
    private static V Atan(V a) => N(Math.Atan(a.Number));
    private static V Asinh(V a) => N(Math.Asinh(a.Number));
    private static V And(params V[] args) => B(args.All(Truth));
    private static V Or(params V[] args) => B(args.Any(Truth));
    private static IEnumerable<double> Numbers(object[] args) => args.SelectMany(x => x is V[,] grid ? grid.Cast<V>() : new[] { (V)x }).Where(v => v.Value is double).Select(v => v.Number);
    private static V Min(params object[] args) => N(Numbers(args).DefaultIfEmpty(0).Min());
    private static V Max(params object[] args) => N(Numbers(args).DefaultIfEmpty(0).Max());
    private static V Sum(params object[] args) => N(Numbers(args).Sum());
    private static V Index(V[,] values, V row) => values[checked((int)row.Number)-1,0];
    private static V Lookup(V key,V[,] values,V column,V approximate)
    {
        if (Truth(approximate)) throw new InvalidOperationException("Ricerca approssimata inattesa nel reticolo CSLP.");
        for(int r=0;r<values.GetLength(0);r++) if(Truth(Eq(key, values[r,0]))) return values[r,checked((int)column.Number)-1];
        throw new ArgumentException("Sito esterno o non interpolabile nel reticolo CSLP 1.0.3.");
    }
}
