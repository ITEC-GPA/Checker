using System.Security.Cryptography;
using System.Text.Json;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics;

// Capture-only: never overwrite a before file. Used to demonstrate unchanged legacy behavior when adding FEM pressures.
if (args.Length != 1) throw new ArgumentException("Specify a new output JSON path.");
const double m = SoilUnits.Metre, kpa = SoilUnits.KiloPascal, kn3 = SoilUnits.KiloNewtonPerCubicMetre;
SlopePoint P(double x, double z) => new(x * m, z * m);
var soil1 = new Soil("Limo", 19 * kn3, 20 * kn3, 30 * SoilUnits.Degree, 5 * kpa, "capture", 40 * kpa);
var soil2 = new Soil("Argilla", 18 * kn3, 21 * kn3, 25 * SoilUnits.Degree, 12 * kpa, "capture", 60 * kpa);
var surface = new[] { P(-30, 0), P(0, 0), P(12, 6), P(40, 6) };
var layers = new[] { new SlopeLayer(soil1, -3 * m), new SlopeLayer(soil2, -20 * m) };
var valley = new[] { new SlopeLayer(soil2, -20 * m) };
var water = new[] { P(-30, -2), P(40, -1) };
var body = new SlopeBody("Corpo", new[] { P(13, 0), P(15, 0), P(15, 6), P(13, 6) }, 25 * kn3);
var loads = new[] { new SlopeLoad("Q", 16 * m, 30 * m, 6 * m, 10 * kpa, 0, 0, true), new SlopeLoad("F", 16 * m, 16 * m, 6 * m, 20, 5, 2000, false) };
var factors = new[] {
    new SlopeFactors("Static", 1, 1, 1.25, 1.25, 1.4, 1.1, 0, 0, false, new Dictionary<string, double> { ["Q"] = 1.3, ["F"] = 1 }),
    new SlopeFactors("Undrained", 1, 1, 1.25, 1.25, 1.4, 1.1, 0, 0, true),
    new SlopeFactors("Seismic", 1, 1, 1, 1, 1, 1.2, .1, -.05, false) };
var configurations = new[] {
    new SlopeSection(surface, layers, null, null, null, 0, 12*m),
    new SlopeSection(surface, layers, water, null, null, 0, 12*m),
    new SlopeSection(surface, layers, water, new[]{body}, loads, 0, 15*m, valley, 0) };
var search = new SlopeSearch(-15*m, -.5*m, 15.5*m, 30*m, .5*m, 12*m, 5, 30, 1);
var records = configurations.Select((s, index) => new {
    Index = index,
    Slices = factors.Select(f => new { f.Name, Values = SlopeStability.Slices(s, SlopeGeometry.Through(P(-8, 0), P(20, 6), -5*m)!, f, 40) }).ToArray(),
    Search = SlopeStability.Calculate(s, search, factors).Cases.Select(c => new { Name = c.Factors.Name, c.Critical, c.Tried, c.GeometricallyValid, c.Solved, c.NumericalFailures, c.Boundary, c.Status, c.RefinedFactor, c.Ratio, c.IsConclusive }).ToArray()
}).ToArray();
byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(records, new JsonSerializerOptions { WriteIndented = true });
string path = Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) file.Write(bytes);
Console.WriteLine(Convert.ToHexString(SHA256.HashData(bytes)) + " " + path);
