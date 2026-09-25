using GPC.Checkers.CompositeBridge;
using GPC.Geometry;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass, DoNotParallelize]
public class GenericSectionServicesTests
{
    [TestMethod]
    public void FourPlatesReproduceHollowRectangleProperties()
    {
        // 1000 x 1200 hollow rectangle, uniform wall 20 mm; no H-section adapter.
        var p = CompositeSectionProperties.CombineProperties("Box", new[] {
            CompositeSectionProperties.RectangleProperties("Top", 1000, 20, 1180, 500),
            CompositeSectionProperties.RectangleProperties("Bottom", 1000, 20, 0, 500),
            CompositeSectionProperties.RectangleProperties("Left", 20, 1160, 20, 10),
            CompositeSectionProperties.RectangleProperties("Right", 20, 1160, 20, 990) });
        Assert.AreEqual(1000d * 1200 - 960 * 1160, p.Area, 1e-8);
        Assert.AreEqual(500d, p.X, 1e-8); Assert.AreEqual(600d, p.Y, 1e-8);
        Assert.AreEqual((1000 * Math.Pow(1200, 3) - 960 * Math.Pow(1160, 3)) / 12, p.Ix, .001);
        Assert.AreEqual((1200 * Math.Pow(1000, 3) - 1160 * Math.Pow(960, 3)) / 12, p.Iy, .001);
    }

    [DataTestMethod]
    [DataRow(0d, 0d, 2e9)] [DataRow(2.2d, -400000d, 2e9)] [DataRow(5d, 250000d, -1e9)]
    [DataRow(2.2d, -300000d, 0d)] [DataRow(2.2d, 0d, 0d)]
    public void TwoWebCompositeSectionMatchesIndependentLineWallOracle(double phi, double force, double moment)
    {
        var d = CompositeBridgeApiTests.Input();
        double ea = d.Materials.Steel.ElasticModulusTension, ec = d.Materials.Concrete.ElasticModulusCompression;
        double n = ea / ec * (1 + phi);
        var section = new ReinforcedConcreteSection(2000, 240, d.Materials.Concrete, null!, 200, 50, null!, 200,
            new SectionH(1200, 12, 1000, 20, 1000, 30, "Seed replaced by four box plates"), d.Materials.Steel, 40);
        section.SteelSections.Clear();
        void Add(double width, double height, double x, double bottom, bool web = false)
        {
            var shape = web ? new SectionRectangular(width, height) : new SectionRectangular(height, width);
            section.AddSteelSection(new SteelSectionPosition(new SteelSection(shape, d.Materials.Steel), Point2d.Origin,
                web ? Math.PI / 2 : 0, new Point2d(x, bottom + height / 2), InsertionPointType.Centroid) { IsInsideConcrete = false });
        }
        Add(1000, 20, 1000, -20); Add(1000, 30, 1000, -1200);
        Add(12, 1150, 506, -1170, true); Add(12, 1150, 1494, -1170, true);
        // Native solver integrates horizontal steel plates on the centreline, vertical walls continuously.
        var parts = new[] { (A: 20000d, Y: -10d, I: 0d), (A: 30000d, Y: -1185d, I: 0d),
            (A: 2 * 12d * 1150, Y: -595d, I: 2 * 12 * Math.Pow(1150, 3) / 12),
            (A: 2000 * 240 / n, Y: 120d, I: 2000 * Math.Pow(240, 3) / 12 / n) };
        double area = parts.Sum(p => p.A), cy = parts.Sum(p => p.A * p.Y) / area;
        double inertia = parts.Sum(p => p.I + p.A * Math.Pow(p.Y - cy, 2));
        var field = CompositeLinearStressSolver.Solve(section, force, moment, 1000, 0, phi, BridgeStandard.Ntc2018);
        foreach (double y in new[] { -1185d, -595d, -10d, 0d, 240d })
        {
            double expected = force / area - (moment + force * cy) / inertia * (y - cy);
            Assert.AreEqual(expected, field.Stress(y), 1e-6 + Math.Abs(expected) * 1e-5, "y=" + y);
            Assert.AreEqual(expected / ea, field.Strain(y, ea), 1e-10);
        }
    }

    [DataTestMethod]
    [DataRow(2)] [DataRow(4)] [DataRow(6)]
    public void CumulativeIterationSupportsArbitraryPanelCounts(int count)
    {
        // Synthetic state: prescribed target reductions isolate iteration/state semantics from plate formulae.
        var situations = CumulativePhaseAnalysis.Analyze(new[] { 1d, 2d }, () => Enumerable.Repeat(100d, count).ToArray(),
            (state, load) => state.Select(w => load / w).ToArray(),
            contributions => Enumerable.Repeat(contributions.Count == 1 ? 80d : 70d, count).ToArray(),
            (a, b) => a.Zip(b).Max(p => Math.Abs(p.First - p.Second) / 100),
            (a, b, r) => a.Zip(b).Select(p => p.First * (1 - r) + p.Second * r).ToArray(),
            p => "P" + p).ToArray();
        Assert.AreEqual(2, situations.Length);
        for (int i = 0; i < 2; i++)
        {
            var s = situations[i];
            Assert.AreEqual(count, s.State.Length); Assert.AreEqual(i + 1, s.Contributions.Count);
            Assert.IsTrue(s.Residual < 1e-7);
            Assert.AreEqual(i == 0 ? 80d : 70d, s.State[0], 1e-5);
            for (int p = 0; p <= i; p++) Assert.AreEqual((p + 1) / s.State[0], s.Contributions[p][0], 1e-12);
        }
    }

    [TestMethod]
    public void NonConvergentIterationNeverReturnsAnUsableSituation()
    {
        var states = CumulativePhaseAnalysis.Analyze(new[] { 1 }, () => 1d, (state, _) => state,
            c => 1 - c[0], (a, b) => Math.Abs(a - b), (a, b, _) => b, _ => "Oscillation", maximumIterations: 3);
        var ex = Assert.ThrowsException<InvalidOperationException>(() => states.ToArray());
        StringAssert.Contains(ex.Message, "3 iterazioni");
    }

    [DataTestMethod]
    [DataRow(-100d, -50d)] [DataRow(-100d, 100d)] [DataRow(20d, 100d)]
    public void PlateReductionIsIndependentOfPanelOrientation(double start, double end)
    {
        var a = EffectivePlateReduction.InternalPlate(1600, 10, start, end, 355);
        var b = EffectivePlateReduction.InternalPlate(1600, 10, end, start, 355);
        Assert.AreEqual(a.EffectiveAtStart, b.EffectiveAtEnd, 1e-10);
        Assert.AreEqual(a.EffectiveAtEnd, b.EffectiveAtStart, 1e-10);
        Assert.AreEqual(a.Rho, b.Rho, 1e-12);
    }
}
