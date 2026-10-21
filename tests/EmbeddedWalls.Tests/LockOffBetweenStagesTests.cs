using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

/// <summary>Head anchor and lock-off between stages on the 6 m cantilever (EI 2e14 Nmm², 3EI/L³ = 2777.78 N/mm, H 10 kN at the head,
/// EA/L 10000 N/mm). Expected values are exact rational arithmetic of the paratie review of 21/10/2026
/// (tools/attesi/paratie_revisione/anthea, cases.py and prestress.py on branch claude/verifica-paratie), not engine output.
/// ANTHEA keeps the lock-off of a support listed again with an empty cell by passing the same Prestress (finding A1):
/// these tests fix the engine side of that contract. Each compared value is written as ESITO;test;quantity;unit;expected;obtained;difference.</summary>
[TestClass]
public sealed class LockOffBetweenStagesTests
{
    private const double U1 = 3.6, Tolerance = 1e-9, ForceTolerance = 1e-6;

    private static void Check(string test, string quantity, string unit, double expected, double obtained, double tolerance)
    {
        Console.WriteLine($"ESITO;{test};{quantity};{unit};{expected:R};{obtained:R};{obtained - expected:R}");
        Assert.AreEqual(expected, obtained, tolerance, $"{test}: {quantity}");
    }

    private static EmbeddedWallModel HeadAnchor(double inclination = 0, double spacing = 1000)
    {
        var m = BeamAndPressureTests.Cantilever();
        m.Supports = [new() { Id = "T1", Elevation = 0, AxialStiffness = 10000, Inclination = inclination, Spacing = spacing }];
        return m;
    }

    /// <summary>Stages F1–F3, H 10 kN at the head in each one; T1 active in the listed stages with the given lock-off, N.</summary>
    private static WallAnalysisResult History(params (string Stage, double Prestress)[] activations)
    {
        var m = HeadAnchor(); var initial = m.Stages[0];
        EmbeddedWallStage Stage(string id) => new()
        {
            Id = id, Left = initial.Left, Right = initial.Right, PointLoads = [new() { Elevation = 0, HorizontalForce = 10000 }],
            ActiveSupports = activations.Where(a => a.Stage == id).Select(a => new EmbeddedWallSupportActivation { SupportId = "T1", Prestress = a.Prestress }).ToArray()
        };
        m.Stages = [Stage("F1"), Stage("F2"), Stage("F3")];
        var r = EmbeddedWallAnalysis.Calculate(m);
        Assert.IsTrue(r.IsComplete, r.Diagnostic);
        return r;
    }

    [DataTestMethod]
    [DataRow("alpha 0, s 1 m", 0d, 1000d, 0.782608695652174, 7826.086956521739)]
    [DataRow("alpha 0, s 2.5 m", 0d, 2500d, 1.4754098360655739, 14754.098360655737)]
    [DataRow("alpha 30, s 1 m", 30d, 1000d, 0.972972972972973, 8426.193117902645)]
    public void HeadAnchorSharesTheLoadWithTheCantilever(string name, double degrees, double spacing, double displacement, double force)
    {
        // u = H/(3EI/L³ + EA/L·(b/s)·cos²α); F = EA/L·cos α·u for each element.
        var m = HeadAnchor(degrees * Math.PI / 180, spacing);
        m.Stages[0].ActiveSupports = [new() { SupportId = "T1" }];
        var r = EmbeddedWallAnalysis.Calculate(m); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        Check("head anchor " + name, "head displacement", "mm", displacement, r.Stages[0].Points[0].Displacement, Tolerance);
        Check("head anchor " + name, "anchor force", "N", force, r.Stages[0].Supports.Single().AxialForce, ForceTolerance);
    }

    [TestMethod]
    public void LockOffRepeatedInALaterStageIsKept()
    {
        var r = History(("F2", 50000), ("F3", 50000));
        Check("lock-off kept", "F1 head displacement", "mm", U1, r.Stages[0].Points[0].Displacement, Tolerance);
        Assert.AreEqual(0, r.Stages[0].Supports.Count);
        Check("lock-off kept", "F2 head displacement", "mm", -0.3130434782608696, r.Stages[1].Points[0].Displacement, Tolerance);
        Check("lock-off kept", "F2 anchor force", "N", 10869.565217391305, r.Stages[1].Supports.Single().AxialForce, ForceTolerance);
        Check("lock-off kept", "F3 head displacement", "mm", -0.3130434782608696, r.Stages[2].Points[0].Displacement, Tolerance);
        Check("lock-off kept", "F3 anchor force", "N", 10869.565217391305, r.Stages[2].Supports.Single().AxialForce, ForceTolerance);
        Check("lock-off kept", "F3 installation reference", "mm", U1, r.Stages[2].Supports.Single().ReferenceDisplacement, Tolerance);
    }

    [TestMethod]
    public void ZeroLockOffReleasesThePrestress()
    {
        var r = History(("F2", 50000), ("F3", 0));
        Check("lock-off released", "F3 head displacement", "mm", U1, r.Stages[2].Points[0].Displacement, Tolerance);
        Check("lock-off released", "F3 anchor force", "N", 0, r.Stages[2].Supports.Single().AxialForce, ForceTolerance);
    }

    [TestMethod]
    public void HigherLockOffRetensionsTheAnchor()
    {
        var r = History(("F2", 50000), ("F3", 80000));
        Check("retensioned to 80 kN", "F3 head displacement", "mm", -2.6608695652173915, r.Stages[2].Points[0].Displacement, Tolerance);
        Check("retensioned to 80 kN", "F3 anchor force", "N", 17391.304347826086, r.Stages[2].Supports.Single().AxialForce, ForceTolerance);
    }

    [TestMethod]
    public void RemovedAnchorIsReinstalledWithoutTheOldLockOff()
    {
        var r = History(("F1", 50000), ("F3", 0));
        Check("removed and reinstalled", "F1 head displacement", "mm", -3.130434782608696, r.Stages[0].Points[0].Displacement, Tolerance);
        Check("removed and reinstalled", "F1 anchor force", "N", 18695.652173913043, r.Stages[0].Supports.Single().AxialForce, ForceTolerance);
        Check("removed and reinstalled", "F2 head displacement", "mm", U1, r.Stages[1].Points[0].Displacement, Tolerance);
        Assert.AreEqual(0, r.Stages[1].Supports.Count);
        Check("removed and reinstalled", "F3 head displacement", "mm", U1, r.Stages[2].Points[0].Displacement, Tolerance);
        Check("removed and reinstalled", "F3 anchor force", "N", 0, r.Stages[2].Supports.Single().AxialForce, ForceTolerance);
        Check("removed and reinstalled", "F3 installation reference", "mm", U1, r.Stages[2].Supports.Single().ReferenceDisplacement, Tolerance);
    }
}
