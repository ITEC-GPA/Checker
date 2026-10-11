using System.Collections.Concurrent;
using GPC.Checkers.CompositeBridge;
using GPC.Geometry;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

/// <summary>
/// Architecture review R7: the composite stress solver builds its native concrete engine without an external gate. Solutions computed in
/// parallel on the same composite section (concrete slab and four steel plates, meshes created at first use) are the sequential ones bit
/// for bit.
/// </summary>
[TestClass]
public class ConcurrencyTests
{
    private static ReinforcedConcreteSection Section()
    {
        var d = CompositeBridgeApiTests.Input();
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
        return section;
    }

    private static readonly (double Phi, double Force, double Moment)[] Cases =
        Enumerable.Range(0, 12).Select(i => (i % 3 * 1.1, -400000d + i * 60000, 2e9 - i * 3e8)).ToArray();

    private static string Solve(ReinforcedConcreteSection section, (double Phi, double Force, double Moment) c)
    {
        var field = CompositeLinearStressSolver.Solve(section, c.Force, c.Moment, 1000, 0, c.Phi, BridgeStandard.Ntc2018);
        return string.Join("|", new[] { -1185d, -595d, -10d, 240d }.Select(y => field.Stress(y).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [TestMethod]
    public void CompositeSolutionsInParallelOnTheSameSectionAreTheSequentialOnes()
    {
        var reference = Section();
        var expected = Cases.Select(c => Solve(reference, c)).ToArray();
        for (int round = 0; round < 4; round++)
        {
            var shared = Section(); var results = new ConcurrentDictionary<int, string>();
            Parallel.For(0, Cases.Length * 2, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                i => results[i] = Solve(shared, Cases[i % Cases.Length]));
            for (int i = 0; i < Cases.Length * 2; i++) Assert.AreEqual(expected[i % Cases.Length], results[i], $"round {round}, case {i % Cases.Length}");
        }
    }
}
