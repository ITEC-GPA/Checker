using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Detailing;
using GPC.Design.Sections;
using GPC.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Design.Checks;

[TestClass]
public sealed class StadiumReinforcementChecks
{
    private static ConcreteTemplateInput Input => new(ConcreteTemplateKind.Stadium,
        5000, 2500, 0, 0, 0, 0, 0, 0, 50, 20, 180, 24, 16, 12, 150)
    { Stadium = new(17, 32, 8, 25) };

    private static MemberDetailingInput Column(ConcreteTemplateGeometry g, IReadOnlyList<DesignBar>? bars = null, bool restrained = true)
        => new(MemberDetailingKind.Column,
            new CrackSectionGeometry(g.Shape.Fill2d.Points, [], (bars ?? g.Bars).Select(b => new CrackBar(b.X, b.Y, b.Diameter, b.Area)), CrackBarLayout.Rows),
            g.Area, 30, 2.9, 450, 450 / 1.15, g.Width, g.Width, g.Width, 5000000,
            true, 12, 150, 0, 20, 50, 25, 10, false, restrained, false);

    [TestMethod]
    public void HorizontalAndVerticalCapsulesHaveSeparateCurvesAndStraightBars()
    {
        foreach (bool rotated in new[] { false, true })
        {
            var p = rotated ? Input with { Width = 2500, Height = 5000 } : Input;
            var g = ConcreteSectionTemplates.Create(p);
            Assert.AreEqual(50, g.Bars.Count); // 2 semicircles × 17 + 2 straight sides × 8
            Assert.AreEqual(34, g.Bars.Count(b => b.Diameter == 32));
            Assert.AreEqual(16, g.Bars.Count(b => b.Diameter == 25));
            Assert.AreEqual(34 * Math.PI * 32 * 32 / 4 + 16 * Math.PI * 25 * 25 / 4, g.Bars.Sum(b => b.Area), 1e-7);
            // Offset paths are quantized to 1e-6 mm by Clipper.
            Assert.AreEqual(0, g.Bars.Average(b => b.X), 2e-6); Assert.AreEqual(0, g.Bars.Average(b => b.Y), 2e-6);
            Assert.AreEqual(2500d * 2500 + Math.PI * 1250 * 1250, g.Area, g.Area * .001);
            Assert.AreEqual(1, g.Links.Count);
            foreach (var b in g.Bars)
            {
                Assert.IsTrue(g.Shape.Fill2d.DistanceTo(new(b.X, b.Y)) - b.Diameter / 2 >= 62 - 2e-5);
                double x = rotated ? b.Y : b.X, y = rotated ? b.X : b.Y;
                if (b.Diameter == 25)
                {
                    Assert.IsTrue(Math.Abs(x) < 1250);
                    Assert.AreEqual(1250 - 50 - 12 - 12.5, Math.Abs(y), 1e-7);
                }
            }
            var r = MemberDetailingCalculator.CalculateColumnOnPerimeter(DetailingProfile.Ntc2018, Column(g), g.ColumnBarPath!);
            var spacing = r.Checks.Single(c => c.Key == "LongitudinalSpacing");
            Assert.IsTrue(spacing.Passed == true);
            Assert.AreEqual(2500d / 9, spacing.Actual!.Value, .1); // small change of ordinate at the two diameters
            Assert.AreEqual(Math.Max(.1 * 5000000 / (450 / 1.15), .003 * g.Area),
                r.Checks.Single(c => c.Key == "MinimumLongitudinal").Limit!.Value, 1e-7);
            Assert.AreEqual(250, r.Checks.Single(c => c.Key == "LinkSpacing").Limit);
        }
    }

    [TestMethod]
    public void SpacingUsesCurvedPathAndIncludesTheClosingStraightSide()
    {
        var p = Input with { Stadium = new(4, 32, 8, 32) };
        var g = ConcreteSectionTemplates.Create(p);
        var r = MemberDetailingCalculator.CalculateColumnOnPerimeter(DetailingProfile.Ntc2018, Column(g), g.ColumnBarPath!);
        var spacing = r.Checks.Single(c => c.Key == "LongitudinalSpacing");
        Assert.IsTrue(spacing.Passed == false);
        // Independent analytic arc, compared with the native polygon discretization.
        Assert.AreEqual(Math.PI * (1250 - 50 - 12 - 16) / 3, spacing.Actual!.Value, 1);
        var reverse = MemberDetailingCalculator.CalculateColumnOnPerimeter(DetailingProfile.Ntc2018, Column(g), g.ColumnBarPath!.Reverse().ToArray());
        Assert.AreEqual(spacing.Actual.Value, reverse.Checks.Single(c => c.Key == "LongitudinalSpacing").Actual!.Value, 1e-8);
        p = Input with { Stadium = new(17, 32, 0, 32) }; g = ConcreteSectionTemplates.Create(p);
        spacing = MemberDetailingCalculator.CalculateColumnOnPerimeter(DetailingProfile.Ntc2018, Column(g), g.ColumnBarPath!)
            .Checks.Single(c => c.Key == "LongitudinalSpacing");
        Assert.AreEqual(2500, spacing.Actual!.Value, 1e-7);
        Assert.IsTrue(spacing.Passed == false);
    }

    [TestMethod]
    public void ManuallyMovedBarLeavesSpacingPendingWithoutSuppressingOtherChecks()
    {
        var g = ConcreteSectionTemplates.Create(Input);
        var bars = g.Bars.ToArray(); var b = bars[0]; bars[0] = new(0, 0, b.Diameter, b.Area);
        var r = MemberDetailingCalculator.CalculateColumnOnPerimeter(DetailingProfile.Ntc2018, Column(g, bars), g.ColumnBarPath!);
        Assert.IsNull(r.Checks.Single(c => c.Key == "LongitudinalSpacing").Passed);
        Assert.IsNotNull(r.Checks.Single(c => c.Key == "MinimumLongitudinal").Passed);
    }

    [TestMethod]
    public void InvalidLayoutsAreRefusedAndLegacyRadialLayoutRemainsAvailable()
    {
        foreach (var p in new[] { Input with { Stadium = new(1, 32, 8, 25) },
            Input with { Stadium = new(17, double.NaN, 8, 25) },
            Input with { Stadium = new(17, 32, -1, 25) }, Input with { Width = 2500 },
            Input with { Stadium = new(500, 32, 8, 25) } })
            Assert.ThrowsException<ArgumentException>(() => ConcreteSectionTemplates.Create(p));
        var g = ConcreteSectionTemplates.Create(Input with { Stadium = null });
        Assert.AreEqual(24, g.Bars.Count);
        for (int i = 0; i < 24; i++)
            Assert.AreEqual(0, g.Bars[i].X * Math.Sin(2 * Math.PI * i / 24) - g.Bars[i].Y * Math.Cos(2 * Math.PI * i / 24), 1e-7);
        Assert.IsNotNull(g.ColumnBarPath);
    }

    [TestMethod]
    public void PerimeterRequiresAUsablePathAndDoesNotConfirmUnrestrainedBars()
    {
        var g = ConcreteSectionTemplates.Create(Input);
        foreach (var path in new IReadOnlyList<Point2d>[] {
            Array.Empty<Point2d>(), new[] { new Point2d(0, 0), new(0, 0), new(0, 0) },
            new[] { new Point2d(0, 0), new(1, double.NaN), new(1, 1) } })
            Assert.ThrowsException<ArgumentException>(() =>
                MemberDetailingCalculator.CalculateColumnOnPerimeter(DetailingProfile.Ntc2018, Column(g), path));
        Assert.ThrowsException<ArgumentNullException>(() =>
            MemberDetailingCalculator.CalculateColumnOnPerimeter(DetailingProfile.Ntc2018, Column(g), null!));
        var original = MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Column(g, restrained: false));
        var perimeter = MemberDetailingCalculator.CalculateColumnOnPerimeter(DetailingProfile.Ntc2018, Column(g, restrained: false), g.ColumnBarPath!);
        Assert.IsNull(perimeter.Checks.Single(c => c.Key == "BarsHeldByLinks").Passed);
        CollectionAssert.AreEqual(original.Checks.Where(c => c.Key != "LongitudinalSpacing")
            .Select(c => (c.Key, c.Actual, c.Limit, c.Passed, c.Reference)).ToArray(),
            perimeter.Checks.Where(c => c.Key != "LongitudinalSpacing")
            .Select(c => (c.Key, c.Actual, c.Limit, c.Passed, c.Reference)).ToArray());
    }
}
