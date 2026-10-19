using System.Text.Json;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public sealed class DistributedLoadTests
{
    private static JsonElement[] Expected()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "distributed-expected.json")));
        return json.RootElement.GetProperty("cases").EnumerateArray().Select(c => c.Clone()).ToArray();
    }
    private static EmbeddedWallDistributedLoad[] Loads(JsonElement expected) => expected.GetProperty("loads").EnumerateArray()
        .Select((q, i) => new EmbeddedWallDistributedLoad { Id = "Q" + i, Top = -q[0].GetDouble(), Bottom = -q[1].GetDouble(),
            TopLoad = q[2].GetDouble(), BottomLoad = q[3].GetDouble(), Source = "Independent exact Green-function integration" }).ToArray();
    private static EmbeddedWallModel Model(JsonElement expected, double mesh)
    {
        var m = BeamAndPressureTests.Cantilever(0, 0, mesh); m.Stages[0].DistributedLoads = Loads(expected); return m;
    }
    private static void Compare(WallStageResult stage, JsonElement expected)
    {
        foreach (var p in expected.GetProperty("points").EnumerateArray())
        {
            double z = -p.GetProperty("depth").GetDouble();
            // Compare exact nodal displacements; recovered forces are checked at both sides of every mesh station.
            foreach (var row in stage.Points.Where(r => Math.Abs(r.Elevation - z) < 1e-8 && r.Side != "Interior"))
            {
                Assert.AreEqual(p.GetProperty("displacement").GetDouble(), row.Displacement, 5e-8, "u at " + z);
                Assert.AreEqual(p.GetProperty("rotation").GetDouble(), row.Rotation, 5e-11, "rotation at " + z);
                Assert.AreEqual(p.GetProperty("shear").GetDouble(), row.Shear, 2e-5, "V at " + z);
                Assert.AreEqual(p.GetProperty("moment").GetDouble(), row.Moment, .05, "M at " + z);
            }
        }
        var reaction = stage.Reactions.Single();
        Assert.AreEqual(expected.GetProperty("reaction_force").GetDouble(), reaction.Force, 2e-5);
        Assert.AreEqual(expected.GetProperty("reaction_moment").GetDouble(), reaction.Moment, .05);
        Assert.AreEqual(0, stage.ForceResidual, 2e-5); Assert.AreEqual(0, stage.MomentResidual, .1);
    }
    [TestMethod]
    public void TrapezoidsPartialOverlapsAndZeroResultantsMatchIndependentIntegration()
    {
        foreach (var expected in Expected()) foreach (double mesh in new[] { 1500d, 500, 125 })
        {
            var result = EmbeddedWallAnalysis.Calculate(Model(expected, mesh));
            Assert.IsTrue(result.IsComplete, expected.GetProperty("name") + ": " + result.Diagnostic);
            Compare(result.Stages.Single(), expected);
            if (expected.GetProperty("name").GetString() == "partial")
            {
                var rows = result.Stages[0].Points.Where(p => Math.Abs(p.Elevation + 1000) < 1e-8).ToArray();
                Assert.AreEqual(0, rows.Single(p => p.Side == "Above").NetDistributedLoad, 1e-10);
                Assert.AreEqual(1, rows.Single(p => p.Side == "Below").NetDistributedLoad, 1e-10);
            }
        }
    }
    [TestMethod]
    public void ChangingIntervalsRemovalAndIncrementsPreserveTheCompleteStageHistory()
    {
        var cases = Expected(); var m = Model(cases[0], 500);
        m.Stages = cases.Select(c => new EmbeddedWallStage { Id = c.GetProperty("name").GetString()!,
            Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 }, DistributedLoads = Loads(c) })
            .Append(new() { Id = "Removed", Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 } }).ToArray();
        string before = JsonSerializer.Serialize(m);
        foreach (int increments in new[] { 1, 8, 32 })
        {
            var result = EmbeddedWallAnalysis.Calculate(m, new() { InitialSubsteps = increments }); Assert.IsTrue(result.IsComplete, result.Diagnostic);
            for (int i = 0; i < cases.Length; i++) Compare(result.Stages[i], cases[i]);
            Assert.IsTrue(result.Stages.Last().Points.All(p => Math.Abs(p.Displacement) < 1e-8 && Math.Abs(p.Moment) < .02));
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(m));
    }
    [TestMethod]
    public void NonlinearSoilHistoryMatchesTheExistingUniformRepresentation()
    {
        var a = StageTests.Excavation(500); var b = StageTests.Excavation(500);
        foreach (var stage in a.Stages) stage.DistributedLoad = stage.Id == a.Stages[0].Id ? 5 : -3;
        foreach (var stage in b.Stages) stage.DistributedLoads = [new() { Id = "Q", Top = b.Top, Bottom = b.Bottom,
            TopLoad = stage.Id == b.Stages[0].Id ? 5 : -3, BottomLoad = stage.Id == b.Stages[0].Id ? 5 : -3, Source = "Uniform equivalence" }];
        var x = EmbeddedWallAnalysis.Calculate(a); var y = EmbeddedWallAnalysis.Calculate(b);
        Assert.IsTrue(x.IsComplete && y.IsComplete, x.Diagnostic + " / " + y.Diagnostic);
        for (int i = 0; i < x.Stages.Count; i++)
        {
            Assert.AreEqual(x.Stages[i].Points.Count, y.Stages[i].Points.Count);
            for (int j = 0; j < x.Stages[i].Points.Count; j++)
            {
                var p = x.Stages[i].Points[j]; var q = y.Stages[i].Points[j];
                Assert.AreEqual(p.Displacement, q.Displacement, 1e-8); Assert.AreEqual(p.Moment, q.Moment, .05);
                Assert.AreEqual(p.LeftPlastic, q.LeftPlastic); Assert.AreEqual(p.RightPlastic, q.RightPlastic);
            }
        }
    }
    [TestMethod]
    public void InvalidIntervalsAndUnsupportedGlobalTransferAreExplicit()
    {
        foreach (string fault in new[] { "reversed", "outside", "nan", "source", "duplicate", "null" })
        {
            var m = Model(Expected()[0], 500); var q = m.Stages[0].DistributedLoads[0];
            switch (fault) {
                case "reversed": q.Bottom = q.Top; break; case "outside": q.Top = 1; break;
                case "nan": q.TopLoad = double.NaN; break; case "source": q.Source = ""; break;
                case "duplicate": m.Stages[0].DistributedLoads = [q, q]; break; case "null": m.Stages[0].DistributedLoads = null!; break; }
            Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.Calculate(m), fault);
        }
        var valid = Model(Expected()[0], 500);
        var error = Assert.ThrowsException<NotSupportedException>(() => EmbeddedWallGlobalStability.Prepare(valid, valid.Stages[0].Id,
            new(600, 12000, 12000, 1, true, "Test", new SlopeSearch(-10000, -1000, 1000, 10000, 6500, 12000, 3, 20, 0))));
        StringAssert.Contains(error.Message, "tratti verticali");
        Assert.ThrowsException<OperationCanceledException>(() => EmbeddedWallAnalysis.Calculate(valid, cancellation: new CancellationToken(true)));
    }
    [TestMethod]
    public void CallbackMutationDoesNotAlterTheOwnedLoadHistory()
    {
        var expected = Expected()[3]; var m = Model(expected, 500);
        var result = EmbeddedWallAnalysis.Calculate(m, progress: new InlineProgress(() => m.Stages[0].DistributedLoads[0].TopLoad = 10000));
        Assert.IsTrue(result.IsComplete, result.Diagnostic); Compare(result.Stages.Single(), expected);
    }
    [TestMethod]
    public void InteriorDisplacementConvergesAndLineLoadsDoNotAcquireAnotherWidthFactor()
    {
        var expected = Expected()[0];
        double exact = expected.GetProperty("points").EnumerateArray().Single(p => p.GetProperty("depth").GetDouble() == 1500)
            .GetProperty("displacement").GetDouble();
        var errors = new List<double>();
        foreach (double mesh in new[] { 6000d, 3000, 1500 })
        {
            var m = Model(expected, mesh); m.TributaryWidth = 2500;
            var result = EmbeddedWallAnalysis.Calculate(m); Assert.IsTrue(result.IsComplete, result.Diagnostic);
            double u = result.Stages[0].Points.First(p => p.Elevation == -1500).Displacement;
            errors.Add(Math.Abs(u - exact)); Compare(result.Stages[0], expected);
        }
        Assert.IsTrue(errors[0] > 1e-4 && errors[1] < errors[0] / 3 && errors[2] < 1e-8,
            "Cubic interpolation error must contract and vanish at the refined mesh node.");
    }
    [TestMethod]
    public void AFailedVariableLoadStageRollsBackAndCancellationStopsTheHistory()
    {
        var m = StageTests.Excavation(500); int completed = m.Stages.Count;
        m.Stages = m.Stages.Append(new EmbeddedWallStage { Id = "Mechanism", Left = new() { Elevation = m.Bottom }, Right = new() { Elevation = m.Bottom },
            DistributedLoads = [new() { Id = "Q", Top = m.Top, Bottom = m.Bottom, TopLoad = 1, BottomLoad = 3, Source = "Failure probe" }] }).ToArray();
        var result = EmbeddedWallAnalysis.Calculate(m, new() { MinimumIncrement = 1.0 / 32 });
        // An increment can lose convergence before reaching the fully excavated singular state.
        Assert.IsFalse(result.IsComplete); Assert.AreEqual("Mechanism", result.FailedStage);
        Assert.AreEqual(completed, result.Stages.Count);
        var free = Model(Expected()[3], 500); free.FixBottomDisplacement = free.FixBottomRotation = false;
        Assert.AreEqual(WallAnalysisStatus.Unstable, EmbeddedWallAnalysis.Calculate(free).Status);
        using var cancellation = new CancellationTokenSource();
        Assert.ThrowsException<OperationCanceledException>(() => EmbeddedWallAnalysis.Calculate(Model(Expected()[3], 500), cancellation: cancellation.Token,
            progress: new InlineProgress(cancellation.Cancel)));
    }
    private sealed class InlineProgress(Action action) : IProgress<WallAnalysisProgress> { public void Report(WallAnalysisProgress value) => action(); }
}
