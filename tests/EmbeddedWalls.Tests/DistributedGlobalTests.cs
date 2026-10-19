using System.Text.Json;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public sealed class DistributedGlobalTests
{
    private static EmbeddedWallModel Clean(int direction)
    {
        var m = EmbeddedWallGlobalTests.Model(); var s = m.Stages[0];
        s.DistributedLoad = 0; s.PointLoads = []; s.Left.Surcharge = s.Right.Surcharge = 0;
        if (direction < 0) (s.Left, s.Right) = (s.Right, s.Left);
        return m;
    }
    private static EmbeddedWallGlobalProfile Profile(int direction) => new(600, 20000, 20000, direction, true,
        "Complete continuous body; admissible circles pass below the entire wall", new SlopeSearch(-16000, -1000, 1000, 16000, 6500, 15000, 3, 20, 0));
    private static SlipCircle Circle() => new(0, 2000, 10000, -Math.Sqrt(100000000 - 16000000), Math.Sqrt(100000000 - 4000000));
    private static SlopeFactors Factors(EmbeddedWallGlobalPrepared p, double factor) => new("Test", 1, 1, 1, 1, 1, 1, 0, 0, false,
        p.Section.Loads.ToDictionary(l => l.Id, _ => factor));

    [TestMethod]
    public void EquivalentActionsPreserveIndependentIntegralsAndSliceDrivingMoments()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "global-distributed-expected.json")));
        foreach (var expected in file.RootElement.GetProperty("cases").EnumerateArray()) foreach (int direction in new[] { 1, -1 })
        {
            var m = Clean(direction); m.TributaryWidth = expected.GetProperty("width").GetDouble();
            var baseline = EmbeddedWallGlobalStability.Prepare(m, "F2", Profile(direction));
            m.Stages[0].DistributedLoads = [new() { Id = "Q", Top = m.Top + expected.GetProperty("top").GetDouble(),
                Bottom = m.Top + expected.GetProperty("bottom").GetDouble(), TopLoad = expected.GetProperty("q_top").GetDouble(),
                BottomLoad = expected.GetProperty("q_bottom").GetDouble(), Source = "Independent rational integral" }];
            var prepared = EmbeddedWallGlobalStability.Prepare(m, "F2", Profile(direction)); var load = prepared.Section.Loads.Single();
            Assert.AreEqual("StructureInterval-Q", load.Id);
            Assert.AreEqual(direction * expected.GetProperty("horizontal").GetDouble(), load.Horizontal, 1e-12);
            Assert.AreEqual(direction * expected.GetProperty("first_moment_y").GetDouble(), load.Horizontal * load.Y - load.Moment, 1e-9);
            var circle = Circle(); Assert.IsTrue(SlopeGeometry.Admissible(prepared.Section, circle));
            var before = SlopeStability.Slices(baseline.Section, circle, Factors(baseline, 1.3), 40);
            var after = SlopeStability.Slices(prepared.Section, circle, Factors(prepared, 1.3), 40);
            Assert.AreEqual(direction * 1.3 * expected.GetProperty("driving_at_centre_y_2000_radius_10000").GetDouble(),
                after.Sum(s => s.Driving) - before.Sum(s => s.Driving), 1e-10);
            Assert.AreEqual(1.3 * load.Horizontal, after.Sum(s => s.HorizontalLoad), 1e-12);
            Assert.AreEqual(before.Sum(s => s.Vertical), after.Sum(s => s.Vertical), 1e-12);
        }
    }
    [TestMethod]
    public void UniformEquivalenceZeroLoadsAndPartialFactorsRemainExplicit()
    {
        var m = Clean(1); m.Stages[0].DistributedLoad = 2;
        var uniform = EmbeddedWallGlobalStability.Prepare(m, "F2", Profile(1));
        m.Stages[0].DistributedLoad = 0;
        m.Stages[0].DistributedLoads = [new() { Id = "Q", Top = m.Top, Bottom = m.Bottom, TopLoad = 2, BottomLoad = 2, Source = "Uniform equivalence" }];
        var interval = EmbeddedWallGlobalStability.Prepare(m, "F2", Profile(1));
        Assert.AreEqual(JsonSerializer.Serialize(SlopeStability.Slices(uniform.Section, Circle(), Factors(uniform, 1.3), 40)),
            JsonSerializer.Serialize(SlopeStability.Slices(interval.Section, Circle(), Factors(interval, 1.3), 40)));
        var combination = new StandardNTC2018Geotechnics().Combinations(GeotechnicalCheck.GlobalStability, GeotechnicalSituation.PersistentTransient).Single();
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallGlobalStability.CreateCase(interval, combination, [], false));
        var selected = EmbeddedWallGlobalStability.CreateCase(interval, combination,
            [new("StructureInterval-Q", GeotechnicalCharacteristicActionCategory.Variable, true, .7, "Explicit characteristic load")], false);
        Assert.AreEqual(.91, selected.Factors.Loads["StructureInterval-Q"], 1e-12);
        var a = EmbeddedWallGlobalStability.Calculate(uniform, [Factors(uniform, 1.3)]);
        var b = EmbeddedWallGlobalStability.Calculate(interval, [Factors(interval, 1.3)]);
        Assert.IsTrue(a.Cases[0].Solved > 0); Assert.AreEqual(a.Cases[0].Critical!.Factor, b.Cases[0].Critical!.Factor, 1e-12);
        m.Stages[0].DistributedLoads[0].TopLoad = m.Stages[0].DistributedLoads[0].BottomLoad = 0;
        Assert.AreEqual(0, EmbeddedWallGlobalStability.Prepare(m, "F2", Profile(1)).Section.Loads.Count);
    }
}
