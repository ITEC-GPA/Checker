using System.Text.Json;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public sealed class DistributedLoadRegressionTests
{
    [TestMethod]
    public void ExistingUniformHistoriesRetainTheirCapturedResults()
    {
        var cases = new List<WallAnalysisResult>();
        foreach (double q in new[] { 0d, 2, -3 })
        foreach (double step in new[] { 500d, 250 })
        {
            var model = BeamAndPressureTests.Cantilever(1500, q, step);
            model.CaseId = "q=" + q + ";h=" + step;
            var result = EmbeddedWallAnalysis.Calculate(model);
            Assert.IsTrue(result.IsComplete, result.Diagnostic); cases.Add(result);
        }
        cases.Add(EmbeddedWallAnalysis.Calculate(StageTests.Excavation(500)));
        string json = JsonSerializer.Serialize(cases, new JsonSerializerOptions { WriteIndented = true });
        string? output = Environment.GetEnvironmentVariable("PARATIE_DISTRIBUTED_CAPTURE");
        if (!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "legacy.json"), json); }
        string? reference = Environment.GetEnvironmentVariable("PARATIE_DISTRIBUTED_REFERENCE");
        if (!string.IsNullOrEmpty(reference)) Assert.AreEqual(File.ReadAllText(Path.Combine(reference, "legacy.json")), json, "Legacy histories changed.");
    }
}
