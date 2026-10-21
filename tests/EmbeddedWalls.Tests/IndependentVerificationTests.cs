using System.Text.Json;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

/// <summary>October 2026 review: the engine against hand arithmetic and against the independent solver of
/// tools/attesi/paratie_indipendente.py (lumped springs, 50/25 mm, Richardson value). See docs/verifiche/paratie-revisione-2026-10.md.</summary>
[TestClass]
public class IndependentVerificationTests
{
    const double Relative = .005;

    [TestMethod]
    public void RankineLimitsWaterAndSurchargeMatchHandArithmetic()
    {
        // Sand 18/20 kN/m³, φ' 30°, K0 0.5, q 20 kPa, water 3 m below ground: Ka = 1/3, Kp = 3.
        var sand = ReviewModels.AnchoredWater();
        foreach (double depth in new[] { 1000.0, 2500, 3000, 5000, 8000 })
        {
            double total = .02 + 18e-6 * Math.Min(depth, 3000) + 20e-6 * Math.Max(0, depth - 3000), water = 9.81e-6 * Math.Max(0, depth - 3000);
            var p = WallSoilPressure.At(sand.LeftSoils, sand.Stages[0].Left, -depth);
            Assert.AreEqual(total, p.VerticalTotal, 1e-12); Assert.AreEqual(water, p.Water, 1e-12);
            Assert.AreEqual(total - water, p.VerticalEffective, 1e-12); Assert.AreEqual(.5 * (total - water), p.AtRest, 1e-12);
            Assert.AreEqual((total - water) / 3, p.Active, 1e-12); Assert.AreEqual(3 * (total - water), p.Passive, 1e-12);
        }
        // Undrained clay γ 19 kN/m³, cu 40 kPa, q 10 kPa, dry: limits σv ∓ 2cu, at rest K0 σv.
        var clay = ReviewModels.CantileverUndrained();
        foreach (double depth in new[] { 1000.0, 2500, 3000, 5000, 8000 })
        {
            double total = .01 + 19e-6 * depth;
            var p = WallSoilPressure.At(clay.LeftSoils, clay.Stages[1].Left, -depth);
            Assert.AreEqual(total, p.VerticalTotal, 1e-12); Assert.AreEqual(0, p.Water, 1e-15);
            Assert.AreEqual(.6 * total, p.AtRest, 1e-12); Assert.AreEqual(Math.Max(0, total - .08), p.Active, 1e-12); Assert.AreEqual(total + .08, p.Passive, 1e-12);
        }
    }

    [TestMethod]
    public void CantileverInSandMatchesIndependentSolver() => Compare(ReviewModels.CantileverSand(), "cantilever_sand", "S1", "S2");

    [TestMethod]
    public void UndrainedCantileverMatchesIndependentSolver() => Compare(ReviewModels.CantileverUndrained(), "cantilever_undrained", "S1");

    [TestMethod]
    public void AnchoredWallBeforeTheDeepExcavationMatchesIndependentSolver()
    {
        // The final stage S3 is ReviewFindingsTests.AnchoredExcavationWithPassiveMarginConverges.
        var model = ReviewModels.AnchoredWater(); model.Stages = model.Stages.Take(3).ToArray();
        Compare(model, "anchored_water", "S1", "S2");
    }

    [TestMethod]
    public void BilateralSupportReloadsWithCantileverAndSupportInParallel()
    {
        var r = EmbeddedWallAnalysis.Calculate(ReviewModels.ReversedSupport(EmbeddedWallSupportKind.Bilateral)); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        // 3EI/L³ = 2777.78 N/mm and EA/L = 10000 N/mm in parallel: u = ∓10000/12777.78.
        double beam = 3 * 2e14 / Math.Pow(6000, 3), u = 10000 / (beam + 10000);
        Assert.AreEqual(-u, r.Stages[1].Points[0].Displacement, 1e-8); Assert.AreEqual(-10000 * u, r.Stages[1].Supports[0].AxialForce, 1e-5);
        Assert.AreEqual(u, r.Stages[2].Points[0].Displacement, 1e-8); Assert.AreEqual(10000 * u, r.Stages[2].Supports[0].AxialForce, 1e-5);
    }

    internal static void Compare(EmbeddedWallModel model, string caseId, params string[] stages)
    {
        var result = EmbeddedWallAnalysis.Calculate(model); Assert.IsTrue(result.IsComplete, result.Diagnostic);
        foreach (string id in stages) CompareStage(result.Stages.Single(s => s.Id == id), ReviewModels.ExpectedStage(caseId, id), caseId);
    }

    internal static void CompareStage(WallStageResult stage, JsonElement expected, string caseId)
    {
        string where = caseId + "/" + stage.Id;
        double u = expected.GetProperty("top_displacement").GetDouble();
        Assert.AreEqual(u, stage.Points[0].Displacement, Relative * Math.Abs(u) + 1e-3, where + ": top displacement");
        var peak = stage.Points.OrderByDescending(p => Math.Abs(p.Moment)).First();
        double m = expected.GetProperty("max_moment").GetDouble();
        Assert.AreEqual(m, peak.Moment, Relative * Math.Abs(m) + 1e3, where + ": maximum moment");
        Assert.AreEqual(expected.GetProperty("max_moment_elevation").GetDouble(), peak.Elevation, 300, where + ": elevation of the maximum moment");
        foreach (var force in expected.GetProperty("support_forces").EnumerateObject())
            Assert.AreEqual(force.Value.GetDouble(), stage.Supports.Single(s => s.Id == force.Name).AxialForce, Relative * Math.Abs(force.Value.GetDouble()), where + ": " + force.Name);
    }
}
