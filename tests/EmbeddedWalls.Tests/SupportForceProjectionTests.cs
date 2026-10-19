using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public sealed class SupportForceProjectionTests
{
    [DataTestMethod]
    [DataRow(EmbeddedWallSupportKind.Anchor, 1.0, 1.0)]
    [DataRow(EmbeddedWallSupportKind.Strut, 1.0, -1.0)]
    [DataRow(EmbeddedWallSupportKind.Strut, -1.0, 1.0)]
    [DataRow(EmbeddedWallSupportKind.Bilateral, 1.0, 1.0)]
    public void DownwardAxisProjectsTensionDownAndCompressionUp(EmbeddedWallSupportKind kind, double angleSign, double verticalSign)
    {
        var model = BeamAndPressureTests.Cantilever(0);
        model.FixTopDisplacement = true; model.TributaryWidth = 1500;
        model.Supports = [new() { Id = "S1", Kind = kind, Elevation = 0, AxialStiffness = 10000,
            Inclination = angleSign * Math.PI / 6, Spacing = 2000 }];
        model.Stages[0].ActiveSupports = [new() { SupportId = "S1", Prestress = 20000 }];
        var result = EmbeddedWallAnalysis.Calculate(model);
        Assert.IsTrue(result.IsComplete, result.Diagnostic);
        var support = result.Stages[0].Supports.Single();
        Assert.AreEqual(20000, support.AxialForce, 1e-6);
        Assert.AreEqual(-15000 * Math.Sqrt(3) / 2, support.HorizontalReaction, 1e-6);
        Console.WriteLine($"{kind}; inclination={angleSign}; N below support={result.Stages[0].Points.Last().NormalForce:R} N");
        // 20 kN per physical support * sin(30°) * (1.5 / 2) = 7.5 kN per strip.
        foreach (var point in result.Stages[0].Points)
            Assert.AreEqual(verticalSign * 7500, point.NormalForce, 1e-6);
    }
}
