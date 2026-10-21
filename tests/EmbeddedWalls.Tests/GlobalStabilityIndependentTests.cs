using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Geotechnics.Slopes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

/// <summary>October 2026 review: one Bishop circle of the EmbeddedWallGlobalTests model, recomputed with 400 000 midpoint
/// slices by tools/attesi/paratie_revisione/stabilita_globale/circle_check.py (not a port of the C# slicing).</summary>
[TestClass]
public class GlobalStabilityIndependentTests
{
    internal static readonly SlipCircle Circle = new(0, 2000, 10000, -Math.Sqrt(8.4e7), Math.Sqrt(9.6e7));
    internal static EmbeddedWallGlobalProfile Profile() => new(600, 20000, 20000, 1, true,
        "Continuous diaphragm, characteristic input", new SlopeSearch(-16000, -1000, 1000, 16000, 6500, 15000, 3, 20, 0));
    internal static SlopeSurfaceResult Solve(EmbeddedWallGlobalPrepared prepared, double loadFactor = 1)
    {
        var factors = new SlopeFactors("M2", 1, 1, 1.25, 1.25, 1.4, 1.1, 0, 0, false, prepared.Section.Loads.ToDictionary(l => l.Id, _ => loadFactor));
        var result = BishopSolver.Solve(Circle, SlopeStability.Slices(prepared.Section, Circle, factors, 200), 1.1);
        Assert.IsNotNull(result); return result!;
    }

    [TestMethod]
    public void DryCircleMatchesIndependentFineSlices()
    {
        var prepared = EmbeddedWallGlobalStability.Prepare(EmbeddedWallGlobalTests.Model(), "F2", Profile());
        var r = Solve(prepared);
        Assert.AreEqual(5.940702, r.Factor, 1e-3 * 5.940702); Assert.AreEqual(1.1 / 5.940702, r.Ratio, 1e-3 * 1.1 / 5.940702);
        Assert.AreEqual(5.793392, Solve(prepared, 1.3).Factor, 1e-3 * 5.793392, "All actions × 1.3.");
    }

    [TestMethod]
    public void HydrostaticWaterOnSliceBasesMatchesIndependentFineSlices()
    {
        var model = EmbeddedWallGlobalTests.Model(); model.Stages[0].Left.WaterElevation = model.Stages[0].Right.WaterElevation = -1500;
        Assert.AreEqual(4.441361, Solve(EmbeddedWallGlobalStability.Prepare(model, "F2", Profile())).Factor, 1e-3 * 4.441361);
    }
}
