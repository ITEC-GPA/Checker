using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public class BeamAndPressureTests
{
    internal static EmbeddedWallModel Cantilever(double force = 10000, double q = 0, double step = 500) => new()
    {
        Top = 0, Bottom = -6000, MaximumElementLength = step, FixBottomDisplacement = true, FixBottomRotation = true,
        Segments = [new() { Top = 0, Bottom = -6000, FlexuralRigidity = 2e14, SectionId = "S", Source = "analytical test" }],
        Stages = [new() { Id = "Load", Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 }, DistributedLoad = q,
            PointLoads = [new() { Elevation = 0, HorizontalForce = force }] }]
    };

    [TestMethod]
    public void TipForceMatchesAnalyticalDeflectionAndEquilibrium()
    {
        var model = Cantilever(); var result = EmbeddedWallAnalysis.Calculate(model);
        Assert.IsTrue(result.IsComplete, result.Diagnostic); var stage = result.Stages.Single();
        // H L³ / (3 EI) = 3.6 mm. Exact Hermite representation of cubic displacement.
        Assert.AreEqual(3.6, stage.Points[0].Displacement, 1e-8);
        Assert.AreEqual(-.0009, stage.Points[0].Rotation, 1e-10);
        Assert.AreEqual(6e7, stage.Points.Last().Moment, 1e-2);
        Assert.AreEqual(0, stage.ForceResidual, 1e-4); Assert.AreEqual(0, stage.MomentResidual, .1);
    }

    [TestMethod]
    public void DistributedLoadHasConsistentForcesAndRecoveredMoment()
    {
        var result = EmbeddedWallAnalysis.Calculate(Cantilever(0, 2)); var stage = result.Stages.Single();
        Assert.IsTrue(result.IsComplete, result.Diagnostic);
        // q L^4/(8 EI) = 1.62 mm; q L²/2 = 36e6 Nmm.
        Assert.AreEqual(1.62, stage.Points[0].Displacement, 1e-8);
        Assert.AreEqual(36e6, stage.Points.Last().Moment, .01);
        Assert.AreEqual(0, stage.Points[0].Shear, 1e-4);
        Assert.AreEqual(12000, stage.Points.Last().Shear, 1e-4);
        Assert.AreEqual(0, stage.ForceResidual, 1e-4);
    }

    [TestMethod]
    public void UnrestrainedBeamIsAnExplicitMechanism()
    {
        var m = Cantilever(); m.FixBottomDisplacement = m.FixBottomRotation = false;
        var r = EmbeddedWallAnalysis.Calculate(m);
        Assert.AreEqual(WallAnalysisStatus.Unstable, r.Status); Assert.AreEqual(0, r.Stages.Count);
    }

    [TestMethod]
    public void SaturatedPressureSeparatesEffectiveStressAndWater()
    {
        var soil = new Soil("Sand", 18e-6, 20e-6, Math.PI / 6, 0, "independent test");
        var layers = new[] { new EmbeddedWallSoil { Soil = soil, Top = 0, Bottom = -10000, HorizontalModulus = .001, AtRestCoefficient = .5, StiffnessSource = "given" } };
        var p = WallSoilPressure.At(layers, new() { Elevation = 0, WaterElevation = -2000, Surcharge = .01 }, -6000);
        Assert.AreEqual(.126, p.VerticalTotal, 1e-12);
        Assert.AreEqual(.03924, p.Water, 1e-12);
        Assert.AreEqual(.08676, p.VerticalEffective, 1e-12);
        Assert.AreEqual(.02892, p.Active, 1e-12);
        Assert.AreEqual(.26028, p.Passive, 1e-12);
    }

    [TestMethod]
    public void CancellationDoesNotPublishPartialResults()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => EmbeddedWallAnalysis.Calculate(Cantilever(), cancellation: cts.Token));
    }
}
