using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Checkers.CompositeBridge.Viviani;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests.Viviani;

/// <summary>
/// History of the results of the method of prof. Viviani: the data and the results of the reference cases are frozen in
/// Validation/viviani-storico.json (produced by the extraction of the original program). Any change of <see cref="VivianiMethod"/> that changes a
/// result makes this test fail. To regenerate the file (only for new cases): VIVIANI_CAPTURE=&lt;folder&gt; dotnet test --filter VivianiHistory
/// </summary>
[TestClass]
public class VivianiHistoryTests
{
    internal sealed record HistoryCase(string Name, VivianiInput Input, VivianiResult Result);

    /// <summary>The program gives NaN and ∞ for degenerate data (e.g. the shear flow without slab): they are part of the history</summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals, Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>The reference cases: the branch cases and three girders of road bridges</summary>
    internal static IEnumerable<(string Name, VivianiInput Input)> Cases()
    {
        foreach (var c in VivianiEquivalenceTests.BranchCases())
            yield return c;
        // girder of a road bridge with 35 m span: G1 steel and slab, G2 finishes, Q traffic (ULS factors in the plastic check)
        var bridge = new VivianiInput
        {
            SlabHeight = 25, SlabWidth = 350, RebarCover = 5, RebarArea = 31.4, TopFlangeThickness = 3, TopFlangeWidth = 60, WebHeight = 170, WebThickness = 1.6,
            BottomFlangeThickness = 4, BottomFlangeWidth = 80, ShortTermModularRatio = 6.2, LongTermModularRatio = 17.5, Fcd = 19.8,
            TopFlangeFyd = 338.1, WebFyd = 338.1, BottomFlangeFyd = 338.1, GammaG1 = 1.35, GammaG2 = 1.5, GammaQ = 1.35,
            SteelOnly = new(0, 620, 5300), LongTerm = new(0, 180, 1650), ShortTerm = new(0, 950, 7800)
        };
        yield return ("ponte 35 m, mezzeria", bridge);
        yield return ("ponte 35 m, appoggio intermedio", bridge with
        {
            RebarArea = 62.8, BottomFlangeThickness = 5,
            SteelOnly = new(0, 1400, -6200), LongTerm = new(0, 400, -1900), ShortTerm = new(0, 1900, -5600)
        });
        yield return ("ponte 35 m, tensioni di esercizio", bridge with { FactoredElasticStresses = false, Iterations = 6 });
    }

    private static string FilePath() => Path.Combine(AppContext.BaseDirectory, "Validation", "viviani-storico.json");

    [TestMethod]
    public void ResultsAreTheHistoricalOnes()
    {
        string? capture = Environment.GetEnvironmentVariable("VIVIANI_CAPTURE");
        if (!string.IsNullOrEmpty(capture))
        {
            // the history is produced by the original program, not by the rewrite
            var produced = Cases().Select(c => new HistoryCase(c.Name, c.Input, VivianiMethod.Calculate(c.Input))).ToList();
            foreach (var (name, input) in Cases())
                VivianiEquivalenceTests.AssertSame(VivianiEquivalenceTests.Original(input), produced.Single(p => p.Name == name).Result, name);
            Directory.CreateDirectory(capture);
            File.WriteAllText(Path.Combine(capture, "viviani-storico.json"), JsonSerializer.Serialize(produced, Json));
            return;
        }
        var history = JsonSerializer.Deserialize<List<HistoryCase>>(File.ReadAllText(FilePath()), Json)!;
        Assert.AreEqual(Cases().Count(), history.Count, "casi dello storico");
        foreach (HistoryCase h in history)
        {
            VivianiResult actual = VivianiMethod.Calculate(h.Input);
            Assert.AreEqual(JsonSerializer.Serialize(h.Result, Json), JsonSerializer.Serialize(actual, Json), h.Name);
            // the data of the history are the current reference cases
            Assert.AreEqual(JsonSerializer.Serialize(Cases().Single(c => c.Name == h.Name).Input, Json), JsonSerializer.Serialize(h.Input, Json), h.Name);
        }
    }
}
