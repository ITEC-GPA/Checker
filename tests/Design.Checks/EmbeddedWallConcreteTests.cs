using GPC.Checkers.Concrete.Walls;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesignTests;

[TestClass]
public sealed class EmbeddedWallConcreteTests
{
    private static EmbeddedWallRcStrip Section() => new("RC", 1000, 400, 40, 6, 20, 5, 16, 30, 450, 200000, .075, 2, "XC2");
    private static WallAnalysisResult Analysis(bool restrained = true) => EmbeddedWallAnalysis.Calculate(new EmbeddedWallModel
    {
        Name = "Analytical cantilever", CaseId = "ULS-test", Top = 0, Bottom = -6000, MaximumElementLength = 6000,
        FixBottomDisplacement = restrained, FixBottomRotation = restrained,
        Segments = [new() { Top = 0, Bottom = -6000, FlexuralRigidity = 2e14, SectionId = "RC", Source = "test" }],
        Stages = [new() { Id = "P1", Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 },
            PointLoads = [new() { Elevation = 0, HorizontalForce = 10000, DownwardForce = 100000 }] }]
    });

    [TestMethod]
    public void ConcreteReceivesOneConcomitantCutWithPhysicalFaceAndCompressionSigns()
    {
        var analysis = Analysis(); var results = EmbeddedWallConcrete.Check(analysis, 1000, [Section()], EmbeddedWallSectionCheckKind.Ultimate);
        var toe = results.Last();
        Assert.AreEqual("ULS-test", toe.CaseId); Assert.AreEqual("P1", toe.StageId); Assert.AreEqual("RC", toe.SectionId);
        Assert.AreEqual(-100000, toe.Actions.N, 1e-8);
        Assert.AreEqual(10000, toe.Actions.V, 1e-4);
        // H pushes right: the back (left) face is in tension, represented by Concrete's top face (M < 0).
        Assert.AreEqual(-60e6, toe.Actions.M, .01);
        var direct = Section().CreateSession(); var expected = direct.CheckMoment(new WallSectionActions(-100000, 10000, -60e6));
        Assert.IsNotNull(expected.Capacity, expected.Diagnostic);
        Assert.AreEqual(expected.Capacity!.Value, toe.Checks[0].Capacity!.Value, 1);
        Assert.AreEqual(direct.CheckShear(new WallSectionActions(-100000, 10000, -60e6)).Check.Capacity!.Value, toe.Checks[1].Capacity!.Value, .001);
        Assert.AreEqual(analysis.Stages.Single().Points.Count, results.Count);
    }

    [TestMethod]
    public void FailedHistoryAndImplicitWidthConversionAreRejected()
    {
        Assert.ThrowsException<InvalidOperationException>(() => EmbeddedWallConcrete.Check(Analysis(false), 1000, [Section()], EmbeddedWallSectionCheckKind.Ultimate));
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallConcrete.Check(Analysis(), 2000, [Section()], EmbeddedWallSectionCheckKind.Ultimate));
        var missing = EmbeddedWallConcrete.Check(Analysis(), 1000, [], EmbeddedWallSectionCheckKind.Ultimate);
        Assert.IsTrue(missing.All(r => r.Checks.Count == 0 && r.Diagnostic.Contains("non eseguita")));
    }
}
