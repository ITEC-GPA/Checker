using System.Text.Json;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public sealed class EnvelopeTests
{
    private static JsonElement Expected()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "envelope-expected.json")));
        return json.RootElement.Clone();
    }
    private static EmbeddedWallModel Model(JsonElement history, double mesh)
    {
        var m = BeamAndPressureTests.Cantilever(0, 0, mesh); m.CaseId = history.GetProperty("case").GetString()!;
        m.Segments[0].WeightPerLength = history.GetProperty("weight").GetDouble();
        m.Stages = history.GetProperty("stages").EnumerateArray().Select(s => new EmbeddedWallStage {
            Id = s.GetProperty("stage").GetString()!, Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 },
            PointLoads = s.GetProperty("actions").EnumerateArray().Select(a => new EmbeddedWallPointLoad {
                Elevation = -a[0].GetDouble(), HorizontalForce = a[1].GetDouble(), Moment = a[2].GetDouble(), DownwardForce = a[3].GetDouble() }).ToArray()
        }).ToArray(); return m;
    }
    private static WallAnalysisResult[] Histories() => Expected().GetProperty("histories").EnumerateArray()
        .Select((h, i) => EmbeddedWallAnalysis.Calculate(Model(h, i == 0 ? 500 : 700))).ToArray();
    private static void Compare(WallPointResult actual, JsonElement expected)
    {
        Assert.AreEqual(expected.GetProperty("displacement").GetDouble(), actual.Displacement, 2e-8);
        Assert.AreEqual(expected.GetProperty("rotation").GetDouble(), actual.Rotation, 2e-11);
        Assert.AreEqual(expected.GetProperty("normal").GetDouble(), actual.NormalForce, 2e-7);
        Assert.AreEqual(expected.GetProperty("shear").GetDouble(), actual.Shear, 2e-5);
        Assert.AreEqual(expected.GetProperty("moment").GetDouble(), actual.Moment, .05);
    }
    private static string Key(WallResultQuantity q) => q switch { WallResultQuantity.NormalForce => "normal", _ => q.ToString().ToLowerInvariant() };
    private static double Tolerance(WallResultQuantity q) => q switch { WallResultQuantity.Moment => .05,
        WallResultQuantity.Displacement => 2e-8, WallResultQuantity.Rotation => 2e-11, _ => 2e-5 };

    [TestMethod]
    public void RecoveredFieldsMatchIndependentCurvatureIntegrationAtArbitraryStationsAndBothSides()
    {
        var histories = Histories(); Assert.IsTrue(histories.All(r => r.IsComplete));
        foreach (var station in Expected().GetProperty("stations").EnumerateArray())
        foreach (var expected in station.GetProperty("observations").EnumerateArray())
        {
            var phase = histories.Single(r => r.CaseId == expected.GetProperty("case").GetString()).Stages.Single(s => s.Id == expected.GetProperty("stage").GetString());
            Compare(EmbeddedWallAnalysis.Sample(phase, -station.GetProperty("depth").GetDouble(), Enum.Parse<WallResultSide>(station.GetProperty("side").GetString()!)), expected);
        }
    }
    [TestMethod]
    public void EnvelopesAcrossDifferentMeshesKeepCompleteConcomitantObservationAndProvenance()
    {
        var result = EmbeddedWallAnalysis.Envelope(Histories(), 4); Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(8, result.Series.Count); int compared = 0;
        foreach (var expected in Expected().GetProperty("stations").EnumerateArray())
        foreach (var series in result.Series.Take(5))
        {
            double z = -expected.GetProperty("depth").GetDouble(); string side = expected.GetProperty("side").GetString()!;
            var rows = series.Stations.Where(s => Math.Abs(s.Elevation - z) < 1e-7 && (s.Side == side || s.Side == "Interior")).ToArray();
            foreach (var row in rows)
            foreach (var bound in new[] { ("minimum", row.Minimum), ("maximum", row.Maximum) })
            {
                var oracle = expected.GetProperty("envelopes").GetProperty(Key(series.Quantity)).GetProperty(bound.Item1);
                Assert.AreEqual(oracle.GetProperty(Key(series.Quantity)).GetDouble(), bound.Item2.Value, Tolerance(series.Quantity));
                var concomitant = expected.GetProperty("observations").EnumerateArray().Single(o => o.GetProperty("case").GetString() == bound.Item2.CaseId && o.GetProperty("stage").GetString() == bound.Item2.StageId);
                Compare(bound.Item2.Point, concomitant); compared++;
            }
        }
        Assert.IsTrue(compared >= 100, "Independent common stations were not compared.");
        var n = result.Series.Single(s => s.Quantity == WallResultQuantity.NormalForce).Stations.First(s => Math.Abs(s.Elevation + 3000) < 1e-8).Maximum;
        var m = result.Series.Single(s => s.Quantity == WallResultQuantity.Moment).Stations.First(s => Math.Abs(s.Elevation + 3000) < 1e-8).Maximum;
        Assert.AreEqual("B", n.CaseId); Assert.AreEqual("A", m.CaseId);
        Assert.AreEqual(29000, m.Point.NormalForce, 1e-7); Assert.AreEqual(31500, n.Point.NormalForce, 1e-7);
        Assert.AreNotEqual(n.Point.Moment, m.Point.Moment, "N and M envelopes must not fabricate one common load case.");
        Assert.AreEqual(JsonSerializer.Serialize(result), JsonSerializer.Serialize(EmbeddedWallAnalysis.Envelope(Histories().Reverse(), 4)), "Input order must not change provenance or tie ordering.");
    }
    [TestMethod]
    public void StationaryPointsRetainGlobalExtremaEvenWithOneDisplaySubdivision()
    {
        var a = EmbeddedWallAnalysis.Calculate(BeamAndPressureTests.Cantilever(10000, -4, 1300));
        var result = EmbeddedWallAnalysis.Envelope([a], 1); var moment = result.Series.Single(s => s.Quantity == WallResultQuantity.Moment);
        // M=10000 x-2 x²: max 12.5e6 at x=2500, min -12e6 at the toe. Neither FE node nor display-grid station contains the maximum.
        Assert.AreEqual(-2500, moment.Maximum.Point.Elevation, 2e-5); Assert.AreEqual(12500000, moment.Maximum.Value, .05);
        Assert.AreEqual(-12000000, moment.Minimum.Value, .05);
        Assert.AreEqual(0, moment.Maximum.Point.Shear, 2e-5);
    }
    [TestMethod]
    public void SamplingPreservesOneSidedJumpsAndRejectsExtrapolation()
    {
        var phase = Histories()[1].Stages[0];
        var above = EmbeddedWallAnalysis.Sample(phase, -4000, WallResultSide.Above);
        var below = EmbeddedWallAnalysis.Sample(phase, -4000, WallResultSide.Below);
        Assert.AreEqual(8000000, below.Moment - above.Moment, .05);
        Assert.AreEqual(0, below.Displacement - above.Displacement, 1e-9);
        var forceAbove = EmbeddedWallAnalysis.Sample(phase, -2000, WallResultSide.Above);
        var forceBelow = EmbeddedWallAnalysis.Sample(phase, -2000, WallResultSide.Below);
        Assert.AreEqual(5000, forceBelow.Shear - forceAbove.Shear, 2e-5); Assert.AreEqual(30000, forceBelow.NormalForce - forceAbove.NormalForce, 1e-7);
        foreach (var outside in new[] { (1d, WallResultSide.Below), (0d, WallResultSide.Above), (-6000d, WallResultSide.Below), (-6001d, WallResultSide.Above) })
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => EmbeddedWallAnalysis.Sample(phase, outside.Item1, outside.Item2));
    }
    [TestMethod]
    public void FieldsRetainOriginalRecoveryIncludingPlasticPressuresAndStayIndependent()
    {
        var model = StageTests.Excavation(500); var a = EmbeddedWallAnalysis.Calculate(model); Assert.IsTrue(a.IsComplete);
        var before = JsonSerializer.Serialize(EmbeddedWallAnalysis.Envelope([a]));
        foreach (var stage in a.Stages) foreach (var p in stage.Points)
        {
            var q = EmbeddedWallAnalysis.Sample(stage, p.Elevation, p.Side == "Above" ? WallResultSide.Above : WallResultSide.Below);
            Assert.AreEqual(p.Displacement, q.Displacement, 1e-9); Assert.AreEqual(p.Rotation, q.Rotation, 1e-11);
            Assert.AreEqual(p.NormalForce, q.NormalForce, 1e-7); Assert.AreEqual(p.Shear, q.Shear, 1e-7); Assert.AreEqual(p.Moment, q.Moment, 1e-5);
            Assert.AreEqual(p.LeftPressure, q.LeftPressure, 1e-12); Assert.AreEqual(p.RightPressure, q.RightPressure, 1e-12);
            Assert.AreEqual(p.NetDistributedLoad, q.NetDistributedLoad, 1e-9);
        }
        model.Stages[0].DistributedLoad = 3; _ = EmbeddedWallAnalysis.Calculate(model);
        Assert.AreEqual(before, JsonSerializer.Serialize(EmbeddedWallAnalysis.Envelope([a])), "Subsequent histories cannot mutate recovered fields.");
    }
    [TestMethod]
    public void IncompleteHistoryMakesTheEnvelopeExplicitlyPartial()
    {
        var m = BeamAndPressureTests.Cantilever(); m.CaseId = "Mechanism"; m.FixBottomDisplacement = m.FixBottomRotation = false;
        var failed = EmbeddedWallAnalysis.Calculate(m); Assert.IsFalse(failed.IsComplete);
        var complete = Histories()[0]; var envelope = EmbeddedWallAnalysis.Envelope([complete, failed]);
        Assert.IsFalse(envelope.IsComplete); Assert.IsTrue(envelope.Diagnostic.Contains("parziale"));
        Assert.IsFalse(envelope.Histories.Single(h => h.CaseId == "Mechanism").Complete);
        Assert.IsTrue(envelope.Series.Count > 0); Assert.IsTrue(envelope.Series.All(s => s.Stations.All(p => p.Minimum.CaseId == "A" && p.Maximum.CaseId == "A")));
        var empty = EmbeddedWallAnalysis.Envelope([failed]); Assert.IsFalse(empty.IsComplete); Assert.AreEqual(0, empty.Series.Count);
    }
    [TestMethod]
    public void InvalidCaseSetsDifferentGeometryAndCancellationAreExplicit()
    {
        var a = Histories()[0];
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.Envelope([]));
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.Envelope([a, a]));
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.Envelope([a], 0));
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.Envelope([a], 65));
        var m = BeamAndPressureTests.Cantilever(); m.CaseId = "B"; m.TributaryWidth = 2000;
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.Envelope([a, EmbeddedWallAnalysis.Calculate(m)]));
        m.TributaryWidth = 1000; m.Bottom = m.Segments[0].Bottom = -5000;
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.Envelope([a, EmbeddedWallAnalysis.Calculate(m)]));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => EmbeddedWallAnalysis.Envelope([a], cancellation: cancelled.Token));
    }
}
