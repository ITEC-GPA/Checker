using GPC.Checkers.Geotechnics.Piles;
using GPC.Checkers.Geotechnics.Seismic;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeotechnicsTests;

[TestClass]
public class MethodRegressionTests
{
    [TestMethod]
    public void LayoutGenerate_RotatesAboutCentroidWithoutChangingSpacing()
    {
        var piles = PileGroupLayout.Generate(new PileLayoutOptions { Columns = 2, Rows = 1, Diameter = 100, SpacingXDiameters = 3, RotationDegrees = 90 });
        Assert.AreEqual(2, piles.Count);
        Assert.AreEqual(0.0, piles[0].X, 1e-10); Assert.AreEqual(-150.0, piles[0].Y, 1e-10);
        Assert.AreEqual(0.0, piles[1].X, 1e-10); Assert.AreEqual(150.0, piles[1].Y, 1e-10);
        Assert.AreEqual("P1", piles[0].Id); Assert.AreEqual("P2", piles[1].Id);
    }
    [TestMethod]
    public void CapOutline_EdgeDistanceIsMeasuredFromPileAxes()
    {
        var piles = new[] { new GroupPile { Id = "1", X = -150, Y = 0 }, new GroupPile { Id = "2", X = 150, Y = 0 } };
        var outline = PileGroupLayout.CapOutline(piles, 100, 1, true);
        Assert.AreEqual(4, outline.Count);
        Assert.AreEqual(-250.0, outline.Min(p => p.X)); Assert.AreEqual(250.0, outline.Max(p => p.X));
        Assert.AreEqual(-100.0, outline.Min(p => p.Y)); Assert.AreEqual(100.0, outline.Max(p => p.Y));
        Assert.AreEqual(-150.0, piles[0].X);
    }
    [TestMethod]
    public void AxisCosine_UsesRadiansAndExcludesHorizontalAxis()
    {
        Assert.AreEqual(.5, MicropileTube.AxisCosine(Math.PI / 3), 1e-14);
        Assert.ThrowsException<ArgumentException>(() => MicropileTube.AxisCosine(Math.PI / 2));
    }
    [DataTestMethod]
    [DataRow(50.0, 1)] [DataRow(50.01, 2)] [DataRow(70.0, 2)] [DataRow(70.01, 3)] [DataRow(90.0, 3)] [DataRow(90.01, 4)]
    public void SectionClass_RespectsInclusiveSlendernessBoundaries(double diameter, int expected)
    { Assert.AreEqual(expected, MicropileTube.SectionClass(new SectionCHS(diameter, 1), 235)); }
    [TestMethod]
    public void NewmarkCalculate_IncludesMotionUntilRestAfterEndOfRecord()
    {
        // Constant 0.2g for one second, yield 0.1g: speed 981 mm/s,
        // 490.5 mm during the record + 490.5 mm while stopping.
        var result = NewmarkSliding.Calculate(new[] { new AccelerogramSample(0, .2), new AccelerogramSample(1, .2) }, .1);
        Assert.AreEqual(981.0, result.Displacement, 1e-8); Assert.AreEqual(981.0, result.PeakVelocity, 1e-8);
        Assert.AreEqual(3, result.Points.Count); Assert.AreEqual(2.0, result.Points.Last().Time, 1e-12); Assert.AreEqual(0.0, result.Points.Last().Velocity);
    }
}
