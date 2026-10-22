using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Geotechnics.Seepage;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics.Seepage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

/// <summary>Fixed findings of the October 2026 review: P1-P3 in Geotechnics 0.14.0.1, P4 and P5 on 22/10/2026. Each test failed
/// on the reviewed code (master 57f87bf8) and states the physically expected behaviour with an independent expected value.
/// Details: docs/verifiche/paratie-revisione-2026-10.md.</summary>
[TestClass]
public class ReviewFixesTests
{
    // P1. A unilateral support that went slack must re-engage where its force vanished, not follow the wall
    // (before 0.14.0.1 the offset moved also on the zero-force branch).
    [TestMethod]
    [DataRow(EmbeddedWallSupportKind.Strut)]
    [DataRow(EmbeddedWallSupportKind.Anchor)]
    public void UnilateralSupportReengagesAtItsSlackPoint(EmbeddedWallSupportKind kind)
    {
        var r = EmbeddedWallAnalysis.Calculate(ReviewModels.ReversedSupport(kind)); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        var expected = ReviewModels.Expected().GetProperty("unilateral_reload");
        Assert.AreEqual(expected.GetProperty("away_displacement").GetDouble(), r.Stages[1].Points[0].Displacement, 1e-8, "Away: support slack, free cantilever.");
        Assert.AreEqual(0, r.Stages[1].Supports[0].AxialForce, 1e-9);
        // Reviewed code: −2.0348 mm and 15.65 kN (the support re-engages 3.6 mm too early).
        Assert.AreEqual(expected.GetProperty("back_displacement").GetDouble(), r.Stages[2].Points[0].Displacement, 1e-6, "Back: re-engagement at the original contact.");
        Assert.AreEqual(expected.GetProperty("back_force").GetDouble(), r.Stages[2].Supports[0].AxialForce, 1e-3);
    }

    // P2. A Newton line search measured on the max-norm of the residual stalled at a plastic kink although an equilibrium
    // exists (free-earth-support margin 1.17, independent solver converged). Reviewed code: NonConvergent at S3, fraction 0.875.
    // Fixed by an Armijo line search on the potential energy.
    [TestMethod]
    public void AnchoredExcavationWithPassiveMarginConverges()
    {
        var root = ReviewModels.Expected();
        Assert.IsTrue(root.GetProperty("free_earth_support_ratio_anchored_water").GetDouble() > 1.1, "The independent limit equilibrium admits a solution.");
        var r = EmbeddedWallAnalysis.Calculate(ReviewModels.AnchoredWater());
        Assert.IsTrue(r.IsComplete, r.Diagnostic);
        IndependentVerificationTests.CompareStage(r.Stages.Single(s => s.Id == "S3"), ReviewModels.ExpectedStage("anchored_water", "S3"), "anchored_water");
    }

    // P3. The default residual tolerance (1e-5 N + 1e-8 × a scale without bending forces) was below double round-off of
    // EI terms on fine meshes. Reviewed code: NonConvergent at S2 with a 50 mm mesh; the scale now includes bending forces.
    [TestMethod]
    public void FineMeshConvergesWithDefaultTolerances()
    {
        var r = EmbeddedWallAnalysis.Calculate(ReviewModels.CantileverSand(50));
        Assert.IsTrue(r.IsComplete, r.Diagnostic);
        IndependentVerificationTests.CompareStage(r.Stages.Single(s => s.Id == "S2"), ReviewModels.ExpectedStage("cantilever_sand", "S2"), "cantilever_sand");
    }

    // P4. Global stability read only WaterElevation: a prescribed pore-pressure profile (allowed by validation) was ignored and
    // the section computed dry (F = 5.9407 instead of 4.4414). The user's decision (22/10/2026): refuse explicitly for now.
    [TestMethod]
    public void GlobalStabilityRefusesWallFacePorePressureProfiles()
    {
        var model = EmbeddedWallGlobalTests.Model(); var stage = model.Stages[0];
        stage.Left.PorePressures = [new(1000, 0), new(-1500, 0), new(-20000, .181485)];
        stage.Right.PorePressures = [new(-1000, 0), new(-1500, 0), new(-20000, .181485)];
        stage.Left.PorePressureSource = stage.Right.PorePressureSource = "hydrostatic from -1500 mm, prescribed";
        Assert.ThrowsException<NotSupportedException>(() => EmbeddedWallGlobalStability.Prepare(model, "F2", GlobalStabilityIndependentTests.Profile()));
    }

    // P5. The excavation-side lateral boundary was always a fixed head: with the half width of a symmetric excavation the axis
    // acted as a drain. Expected values: same mesh, independent FE (tools/attesi/paratie_revisione/filtrazione/symmetry.py),
    // fixed head 0.22362 and no-flow axis 0.54969 (NTC HYD utilisation 0.65 and 1.59).
    [TestMethod]
    public void SymmetricExcavationAxisCanCarryNoFlow()
    {
        var soil = new SeepageMaterial("s", .01, .01, 0, "given");
        var d = new EmbeddedWallSeepageDomain { CaseId = "symmetric", LeftExtent = 40000, RightExtent = 5000, Bottom = -30000,
            WallTop = 0, WallBottom = -12000, WallThickness = 600, MaximumElementLength = 500,
            LeftGround = 0, RightGround = -6000, LeftHead = 0, RightHead = -6000, LeftLayers = [new(0, -30000, soil)], RightLayers = [new(0, -30000, soil)] };
        var reservoir = SeepageAnalysis.Calculate(EmbeddedWallSeepageMesh.Create(d)); Assert.IsTrue(reservoir.IsComplete, reservoir.Diagnostic);
        Assert.AreEqual(.22362, SeepageSampling.MeanUpwardGradient(reservoir, 300, -6000, -12000), 2e-4 * .22362, "Default: fixed reservoir head.");
        d.RightBoundary = SeepageLateralBoundary.NoFlow;
        var axis = SeepageAnalysis.Calculate(EmbeddedWallSeepageMesh.Create(d)); Assert.IsTrue(axis.IsComplete, axis.Diagnostic);
        Assert.AreEqual(.54969, SeepageSampling.MeanUpwardGradient(axis, 300, -6000, -12000), 2e-4 * .54969, "No-flow symmetry axis.");
        Assert.AreEqual(.091215, SeepageWallCoupling.PressureAt(axis, 300, -12000), 2e-4 * .091215, "Excavation-side pressure at the toe, MPa.");
        Assert.AreEqual(0, axis.Balance, 1e-6);
        d.RightBoundary = (SeepageLateralBoundary)7;
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallSeepageMesh.Create(d));
    }
}
