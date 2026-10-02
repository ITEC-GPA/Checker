using System.Globalization;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeotechnicsTests;

/// <summary>
/// Reading of the legacy fixtures (ANTHEA harness CheckerMigration.Capture, GeotechnicsCapture) and conversion from the legacy units (m, kN/m, kPa,
/// kN/m³, degrees) to the units of Model (mm, N/mm, MPa, N/mm³, rad). Forces per unit length keep their value: 1 kN/m = 1 N/mm.
/// </summary>
internal static class GeotechnicsFixture
{
    public const double M = SoilUnits.Metre, KPa = SoilUnits.KiloPascal, KN3 = SoilUnits.KiloNewtonPerCubicMetre, Deg = SoilUnits.Degree;
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Fixtures");
    public static string[] Rows(string file) => File.ReadAllLines(Path.Combine(Folder, file)).Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
    public static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    public static double? N(string s) => s.Length == 0 ? null : D(s);

    /// <summary>|expected − actual| ≤ relative·|expected| + absolute.</summary>
    public static void Close(double expected, double actual, string what, double relative = 1e-9, double absolute = 1e-12)
    {
        if (!(Math.Abs(expected - actual) <= relative * Math.Abs(expected) + absolute))
            Assert.Fail($"{what}: expected {expected:R}, actual {actual:R} (difference {expected - actual:R})");
    }

    public static SlopePoint[] Points(string s) => s.Length == 0 ? Array.Empty<SlopePoint>()
        : s.Split('/').Select(p => p.Split(':')).Select(p => new SlopePoint(D(p[0]) * M, D(p[1]) * M)).ToArray();

    public static Soil Soil(string name, double gamma, double gammaSat, double phi, double c, double cu)
        => new(name, gamma * KN3, gammaSat * KN3, phi * Deg, c * KPa, "ANTHEA fixture", undrainedShearStrength: cu > 0 ? cu * KPa : null);

    public static SlopeLayer[] Layers(string s) => s.Length == 0 ? Array.Empty<SlopeLayer>() : s.Split('|').Select(l => l.Split(':'))
        .Select(p => new SlopeLayer(Soil(p[0], D(p[2]), D(p[3]), D(p[4]), D(p[5]), D(p[6])), D(p[1]) * M)).ToArray();

    public static SlopeSection Section(string[] c)
    {
        // SECTION;id;surface;soils;valleySoils;split;water;bodies;loads;requiredLeft;requiredRight
        var bodies = c[7].Length == 0 ? Array.Empty<SlopeBody>() : c[7].Split('|').Select(b => b.Split('~')).Select(b => new SlopeBody(b[0], Points(b[2]), D(b[1]) * KN3)).ToArray();
        var loads = c[8].Length == 0 ? Array.Empty<SlopeLoad>() : c[8].Split('|').Select(l => l.Split(':')).Select(l =>
        {
            bool distributed = bool.Parse(l[7]);
            // Distributed: pressures kPa → MPa over lengths in mm; concentrated: kN/m = N/mm; moments kNm/m → N·mm/mm.
            double force = distributed ? KPa : 1, moment = distributed ? 1 : M;
            return new SlopeLoad(l[0], D(l[1]) * M, D(l[2]) * M, D(l[3]) * M, D(l[4]) * force, D(l[5]) * force, D(l[6]) * moment, distributed);
        }).ToArray();
        return new SlopeSection(Points(c[2]), Layers(c[3]), Points(c[6]), bodies, loads, D(c[9]) * M, D(c[10]) * M, Layers(c[4]), D(c[5]) * M);
    }

    public static SlopeSearch Search(string[] c) => new(D(c[2]) * M, D(c[3]) * M, D(c[4]) * M, D(c[5]) * M, D(c[6]) * M, D(c[7]) * M, int.Parse(c[8]), int.Parse(c[9]), int.Parse(c[10]));

    public static SlopeFactors Factors(string s)
    {
        var p = s.Split(':');
        var loads = p[10].Length == 0 ? new Dictionary<string, double>() : p[10].Split('/').Select(x => x.Split('=')).ToDictionary(x => x[0], x => D(x[1]));
        return new SlopeFactors(p[0], D(p[1]), D(p[2]), D(p[3]), D(p[4]), D(p[5]), D(p[6]), D(p[7]), D(p[8]), bool.Parse(p[9]), loads);
    }

    public static SlipCircle? Circle(string s)
    {
        if (s == "null" || s.Length == 0) return null;
        var p = s.Split(':').Select(D).ToArray();
        return new SlipCircle(p[0] * M, p[1] * M, p[2] * M, p[3] * M, p[4] * M);
    }

    public static void SameCircle(SlipCircle? expected, SlipCircle? actual, string what)
    {
        Assert.AreEqual(expected == null, actual == null, what + ": existence of the circle");
        if (expected == null) return;
        Close(expected.X, actual!.X, what + " X", 1e-9, 1e-6); Close(expected.Y, actual.Y, what + " Y", 1e-9, 1e-6);
        Close(expected.Radius, actual.Radius, what + " R", 1e-9, 1e-6); Close(expected.Left, actual.Left, what + " left", 1e-9, 1e-6);
        Close(expected.Right, actual.Right, what + " right", 1e-9, 1e-6);
    }

    /// <summary>Slice written by the capture: index:left:right:baseY:topY:alpha:soil:soilW:bodyW:wx:wy:vl:hl:u:phi:c:V:D:N':R:Rmob:mα.</summary>
    public static void SameSlice(string expected, SlopeSlice s, string what, bool solved)
    {
        var e = expected.Split(':');
        Assert.AreEqual(int.Parse(e[0]), s.Index, what + " index"); Assert.AreEqual(e[6], s.Soil, what + " soil");
        string w = what + " slice " + e[0];
        // Lengths: 1e-9 relative on coordinates of the order of 1e4 mm; forces per unit length (kN/m = N/mm): 1e-9 relative, 1e-9 N/mm absolute.
        Close(D(e[1]) * M, s.Left, w + " left", 1e-12, 1e-7); Close(D(e[2]) * M, s.Right, w + " right", 1e-12, 1e-7);
        Close(D(e[3]) * M, s.BaseY, w + " base", 1e-10, 1e-6); Close(D(e[4]) * M, s.TopY, w + " top", 1e-10, 1e-6);
        Close(D(e[5]) * Deg, s.Alpha, w + " α", 1e-9, 1e-12);
        Close(D(e[7]), s.SoilWeight, w + " Wsoil", 1e-9, 1e-9); Close(D(e[8]), s.BodyWeight, w + " Wbody", 1e-9, 1e-9);
        Close(D(e[9]) * M, s.WeightX, w + " xG", 1e-9, 1e-6); Close(D(e[10]) * M, s.WeightY, w + " yG", 1e-9, 1e-6);
        Close(D(e[11]), s.VerticalLoad, w + " Vload", 1e-9, 1e-9); Close(D(e[12]), s.HorizontalLoad, w + " Hload", 1e-9, 1e-9);
        Close(D(e[13]) * KPa, s.PorePressure, w + " u", 1e-9, 1e-12); Close(D(e[14]) * Deg, s.FrictionAngle, w + " φ", 1e-12, 1e-15);
        Close(D(e[15]) * KPa, s.Cohesion, w + " c", 1e-12, 1e-15); Close(D(e[16]), s.Vertical, w + " V", 1e-9, 1e-9);
        Close(D(e[17]), s.Driving, w + " D", 1e-9, 1e-9);
        if (!solved) return;
        Close(D(e[18]), s.NormalEffective, w + " N'", 1e-8, 1e-8); Close(D(e[19]), s.Resistance, w + " R", 1e-8, 1e-8);
        Close(D(e[20]), s.Mobilized, w + " Rmob", 1e-8, 1e-8); Close(D(e[21]), s.MAlpha, w + " mα", 1e-9, 1e-12);
    }
}
