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

    /// <summary>
    /// A4 of the embedded-wall review of October 2026, option (a) of the ANTHEA decision sheet of 16/11/2026: a warning when the first stage is not
    /// geostatic, because its at-rest imbalance enters whole at the first load step. Results unchanged (warning only). Sand γ = 18 kN/m³, K0 = 0.5.
    /// - Geostatic stage first (Excavation of these tests): no warning, also with two distinct but equal ground objects.
    /// - Excavation of 4 m on the right as the first stage: K0 γ z on the left against K0 γ (z − 4 m) on the right below the excavation, so the gap grows
    ///   to K0 γ · 4 m = 0.5 · 18 · 4 = 36 kPa at −4 m and stays 36 kPa below.
    /// - Same ground level, surcharge 10 kPa on the left only: K0 q = 5 kPa over the whole wall.
    /// - The warning is also given when the first stage does not converge.
    /// </summary>
    [TestMethod]
    public void FirstStageThatIsNotGeostaticIsDeclared()
    {
        var staged = EmbeddedWallAnalysis.Calculate(Excavation(500));
        Assert.IsTrue(staged.IsComplete, staged.Diagnostic); Assert.AreEqual(0, staged.Warnings.Count, string.Join(" | ", staged.Warnings));
        var model = Excavation(500); model.Stages = [model.Stages[1]];
        var direct = EmbeddedWallAnalysis.Calculate(model);
        Assert.IsTrue(direct.IsComplete, direct.Diagnostic);
        Assert.AreEqual("All stages converged. Small-displacement beam; no axial soil transfer or second-order effects.", direct.Diagnostic, "diagnostic unchanged");
        string warning = direct.Warnings.Single();
        StringAssert.StartsWith(warning, "Prima fase «Excavation» non geostatica");
        StringAssert.Contains(warning, "(scarto massimo 36 kPa a quota -");
        StringAssert.Contains(warning, "Lo squilibrio iniziale entra tutto al primo passo di carico");
        Console.WriteLine("A4 top displacement: staged {0:R} mm, direct {1:R} mm", staged.Stages.Last().Points[0].Displacement, direct.Stages.Last().Points[0].Displacement);
        var surcharge = Excavation(500); surcharge.Stages = [new() { Id = "Sovraccarico", Left = new() { Elevation = 0, Surcharge = .01 }, Right = new() { Elevation = 0 } }];
        var loaded = EmbeddedWallAnalysis.Calculate(surcharge);
        StringAssert.Contains(loaded.Warnings.Single(), "(scarto massimo 5 kPa a quota ");
        var failing = Excavation(500); failing.Stages = [new() { Id = "Excavation", Left = new() { Elevation = 0 }, Right = new() { Elevation = -4000 },
            PointLoads = [new() { Elevation = 0, HorizontalForce = 1e9 }] }];
        var failed = EmbeddedWallAnalysis.Calculate(failing);
        Assert.IsFalse(failed.IsComplete); StringAssert.StartsWith(failed.Warnings.Single(), "Prima fase «Excavation» non geostatica");
        Assert.AreEqual(0, EmbeddedWallAnalysis.Calculate(BeamAndPressureTests.Cantilever()).Warnings.Count, "no soil on either side");
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
