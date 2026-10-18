using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public class StageTests
{
    [TestMethod]
    public void AnchorInstalledOnDeformedWallDoesNotReactRetroactively()
    {
        var m = BeamAndPressureTests.Cantilever();
        m.Supports = [new() { Id = "A1", Elevation = 0, AxialStiffness = 10000 }];
        var initial = m.Stages[0];
        EmbeddedWallStage Stage(string id, double force, bool active) => new()
        {
            Id = id, Left = initial.Left, Right = initial.Right,
            PointLoads = [new() { Elevation = 0, HorizontalForce = force }],
            ActiveSupports = active ? [new() { SupportId = "A1" }] : []
        };
        m.Stages = [Stage("Load", 10000, false), Stage("Install", 10000, true), Stage("Reload", 20000, true), Stage("Remove", 20000, false)];
        var r = EmbeddedWallAnalysis.Calculate(m);
        Assert.IsTrue(r.IsComplete, r.Diagnostic);
        Assert.AreEqual(3.6, r.Stages[1].Points[0].Displacement, 1e-8);
        Assert.AreEqual(0, r.Stages[1].Supports.Single().AxialForce, 1e-5);
        // Added load shared by cantilever stiffness 3EI/L³ and anchor EA/L.
        Assert.AreEqual(3.6 + 10000d / (3 * 2e14 / Math.Pow(6000, 3) + 10000), r.Stages[2].Points[0].Displacement, 1e-8);
        Assert.AreEqual(7.2, r.Stages[3].Points[0].Displacement, 1e-8);
        Assert.AreEqual(3.6, r.Stages[0].Points[0].Displacement, 1e-8, "Later stages must not mutate earlier results.");
        Assert.AreEqual(0, m.Stages[1].ActiveSupports[0].Prestress, "Caller data are not mutated.");
    }

    [TestMethod]
    public void UniformWinklerFieldHasConstantExactTranslation()
    {
        var m = BeamAndPressureTests.Cantilever(0, 10); m.FixBottomDisplacement = m.FixBottomRotation = false;
        var soil = new Soil("Sand", 18e-6, 20e-6, Math.PI / 6, 0, "synthetic benchmark");
        m.LeftSoils = m.RightSoils = [new() { Top = 0, Bottom = -6000, Soil = soil, HorizontalModulus = .001, AtRestCoefficient = .5, StiffnessSource = "given" }];
        m.Stages[0].Left = new() { Elevation = 0, Surcharge = 1 };
        m.Stages[0].Right = new() { Elevation = 0, Surcharge = 1 };
        var r = EmbeddedWallAnalysis.Calculate(m); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        foreach (var p in r.Stages[0].Points)
        { Assert.AreEqual(5, p.Displacement, 1e-7); Assert.AreEqual(0, p.Moment, .05); }
    }

    [TestMethod]
    public void ExcavationConvergesAndIndependentCallsHaveNoSharedHistory()
    {
        var m = Excavation(500); var a = EmbeddedWallAnalysis.Calculate(m); var b = EmbeddedWallAnalysis.Calculate(m);
        Assert.IsTrue(a.IsComplete, a.Diagnostic); Assert.IsTrue(b.IsComplete, b.Diagnostic);
        Assert.AreEqual(a.Stages.Last().Points[0].Displacement, b.Stages.Last().Points[0].Displacement, 1e-12);
        Assert.AreEqual(0, a.Stages[0].Points[0].Displacement, 1e-8);
        Assert.IsTrue(a.Stages[1].Points[0].Displacement > 0);
        var refined = EmbeddedWallAnalysis.Calculate(Excavation(250)); Assert.IsTrue(refined.IsComplete, refined.Diagnostic);
        var fine = EmbeddedWallAnalysis.Calculate(Excavation(125)); Assert.IsTrue(fine.IsComplete, fine.Diagnostic);
        // A posteriori convergence: the finest correction must shrink and be below 0.02 mm.
        // The coarse 500 mm mesh is a probe, not the reference solution of this plastic-front problem.
        double coarseCorrection = Math.Abs(a.Stages[1].Points[0].Displacement - refined.Stages[1].Points[0].Displacement);
        double fineCorrection = Math.Abs(refined.Stages[1].Points[0].Displacement - fine.Stages[1].Points[0].Displacement);
        Assert.IsTrue(fineCorrection < coarseCorrection, $"Corrections do not contract: {coarseCorrection} / {fineCorrection}");
        Assert.AreEqual(refined.Stages[1].Points[0].Displacement, fine.Stages[1].Points[0].Displacement, .02);
        Assert.IsTrue(Math.Abs(a.Stages[1].ForceResidual) < .01);
        Assert.IsTrue(Math.Abs(a.Stages[1].MomentResidual) < 10);
    }

    [TestMethod]
    public void NewStageFailureRetainsOnlyPreviouslyConvergedStages()
    {
        var m = Excavation(500);
        m.Stages = [m.Stages[0], new() { Id = "All soil removed", Left = new() { Elevation = -12000 }, Right = new() { Elevation = -12000 },
            PointLoads = [new() { Elevation = 0, HorizontalForce = 1e9 }] }];
        var r = EmbeddedWallAnalysis.Calculate(m);
        Assert.IsFalse(r.IsComplete); Assert.AreEqual(1, r.Stages.Count); Assert.AreEqual("All soil removed", r.FailedStage);
    }

    internal static EmbeddedWallModel Excavation(double step)
    {
        var soil = new Soil("Sand", 18e-6, 20e-6, Math.PI / 6, 0, "synthetic benchmark");
        return new EmbeddedWallModel
        {
            Top = 0, Bottom = -12000, MaximumElementLength = step,
            Segments = [new() { Top = 0, Bottom = -12000, FlexuralRigidity = 2e14, SectionId = "S", Source = "test" }],
            LeftSoils = [new() { Top = 0, Bottom = -12000, Soil = soil, HorizontalModulus = .001, AtRestCoefficient = .5, StiffnessSource = "given" }],
            RightSoils = [new() { Top = 0, Bottom = -12000, Soil = soil, HorizontalModulus = .001, AtRestCoefficient = .5, StiffnessSource = "given" }],
            Stages = [new() { Id = "Geostatic", Left = new() { Elevation = 0 }, Right = new() { Elevation = 0 } },
                new() { Id = "Excavation", Left = new() { Elevation = 0 }, Right = new() { Elevation = -4000 } }]
        };
    }
}
