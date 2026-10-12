using System.Text.Json;
using GPC.Checkers.Geotechnics.Foundations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShallowFoundation.Tests;

/// <summary>
/// Independent expected values read from the Califfo theoretical manual (26.01.2013): califfo_reference.py writes them
/// in califfo-reference.json without importing the engine. "manual" is the literal reading; where the engine follows a
/// documented convention the manual does not prescribe, "convention" holds that rule's value and "relation" its side
/// of the manual value ("le" = conservative). Never regenerate the file from engine output.
/// </summary>
[TestClass]
public sealed class CaliffoReferenceTests
{
    [TestMethod]
    public void EngineMatchesTheManual()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "califfo-reference.json")));
        var root = json.RootElement; var failures = new List<string>(); int count = 0, conventions = 0, exhausted = 0;
        foreach (var c in root.GetProperty("cases").EnumerateArray())
        {
            count++;
            string id = c.GetProperty("id").GetString()!;
            var x = new ShallowFoundationInput(); Apply(x, root.GetProperty("base")); Apply(x, c.GetProperty("input"));
            ShallowFoundationResult r;
            try { r = ShallowFoundationBearing.Calculate(x); }
            catch (Exception e) { failures.Add($"{id}: {e.GetType().Name}: {e.Message}"); continue; }
            var manual = c.GetProperty("manual");
            bool hasConvention = c.TryGetProperty("convention", out var convention);
            var target = hasConvention ? convention : manual;
            double tolerance = c.TryGetProperty("tolerance", out var t) ? t.GetDouble() : 1e-9;
            void Check(string name, double expected, double actual)
            {
                if (Math.Abs(actual - expected) > tolerance * Math.Abs(expected) || double.IsNaN(actual))
                    failures.Add($"{id} {name}: atteso {expected:R}, motore {actual:R}");
            }
            bool exhaust = target.GetProperty("exhausted").GetBoolean();
            if (exhaust) exhausted++;
            if (r.DomainExhausted != exhaust) failures.Add($"{id}: DomainExhausted {r.DomainExhausted}, atteso {exhaust}");
            Check("qult", target.GetProperty("qult").GetDouble(), r.UltimatePressure);
            Check("Rd", target.GetProperty("rd").GetDouble(), r.Resistance);
            foreach (var (name, term) in new[] { ("Cohesion", r.Cohesion), ("Surcharge", r.Surcharge), ("Weight", r.Weight) })
            {
                if (!target.TryGetProperty(name, out var f)) continue;
                double[] v = f.EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Check(name + ".N", v[0], term.N); Check(name + ".Shape", v[1], term.Shape);
                Check(name + ".Depth", v[2], term.Depth); Check(name + ".Inclination", v[3], term.Inclination);
                bool additive = v.Length == 7;
                if (term.Additive != additive) failures.Add($"{id} {name}.Additive: {term.Additive}, atteso {additive}");
                if (!additive) continue;
                Check(name + ".AdditiveShape", v[4], term.AdditiveShape); Check(name + ".AdditiveDepth", v[5], term.AdditiveDepth);
                Check(name + ".AdditiveInclination", v[6], term.AdditiveInclination);
            }
            if (!hasConvention) continue;
            conventions++;
            double literal = manual.GetProperty("qult").GetDouble();
            bool side = c.GetProperty("relation").GetString() == "le" ? r.UltimatePressure <= literal * (1 + 1e-12) : r.UltimatePressure >= literal * (1 - 1e-12);
            if (!side) failures.Add($"{id}: convenzione dal lato sbagliato del manuale ({c.GetProperty("note").GetString()})");
        }
        Assert.IsTrue(count > 400 && conventions > 0 && exhausted > 0, $"Riferimento incompleto: {count} casi, {conventions} convenzioni, {exhausted} oltre il dominio.");
        if (failures.Count > 0) Assert.Fail($"{failures.Count} differenze su {count} casi:\n" + string.Join("\n", failures.Take(40)));
    }

    static void Apply(ShallowFoundationInput x, JsonElement values)
    {
        foreach (var p in values.EnumerateObject())
        {
            var property = typeof(ShallowFoundationInput).GetProperty(p.Name) ?? throw new InvalidOperationException("Campo sconosciuto: " + p.Name);
            object value = property.PropertyType.IsEnum ? Enum.Parse(property.PropertyType, p.Value.GetString()!)
                : property.PropertyType == typeof(bool) ? p.Value.GetBoolean() : p.Value.GetDouble();
            property.SetValue(x, value);
        }
    }
}
