using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Steel.Sections;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesignTests;

[TestClass]
public sealed class EmbeddedWallSteelTubeTests
{
    private static EmbeddedWallSteelTube Tube() => new("T", new CircularHollowSteelSection(
        new SteelSection(new SectionCHS(200, 8), new SteelMaterialEN1993("S355", 210000, 355, 510)), 355, "Certificate", true),
        2000, true, "Single row, equal sharing; no arching or transverse redistribution");
    private static WallAnalysisResult Analysis(bool stable = true)
    {
        var tube = Tube();
        return EmbeddedWallAnalysis.Calculate(new()
        {
            CaseId = "SLU", Top = 0, Bottom = -6000, TributaryWidth = 1500,
            FixBottomDisplacement = stable, FixBottomRotation = stable,
            Segments = [new() { Top = 0, Bottom = -6000, FlexuralRigidity = tube.FlexuralRigidity(1500), SectionId = "T", Source = "Gross tube EI" }],
            Stages = [Stage("A", 200, 5000), Stage("B", -300, 1000)]
        });
        static EmbeddedWallStage Stage(string id, double force, double axial) => new()
        {
            Id = id, Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 },
            PointLoads = [new() { Elevation = 0, HorizontalForce = force, DownwardForce = axial }]
        };
    }
    [TestMethod]
    public void RigidityAndAllThreeActionsUseTheSamePhysicalMemberRepresentation()
    {
        var tube = Tube(); double i = Math.PI / 64 * (Math.Pow(200, 4) - Math.Pow(184, 4));
        Assert.AreEqual(210000 * i * 1500 / 2000, tube.FlexuralRigidity(1500), 1e-3);
        var analysis = Analysis(); Assert.IsTrue(analysis.IsComplete, analysis.Diagnostic);
        double expectedTip = 200 * Math.Pow(6000, 3) / (3 * tube.FlexuralRigidity(1500));
        Assert.AreEqual(expectedTip, analysis.Stages[0].Points.First(p => p.Elevation == 0).Displacement, 1e-7);
        var results = EmbeddedWallSteelTubes.Check(analysis, 1500, [tube], new StandardNTC2018Steel(), EmbeddedWallSteelCheckKind.StaticUltimate);
        Assert.AreEqual(analysis.Stages.Sum(s => s.Points.Count), results.Count);
        foreach (var stage in analysis.Stages)
        foreach (var p in stage.Points)
        {
            var row = results.Single(r => r.StageId == stage.Id && r.Elevation == p.Elevation && r.Side == p.Side);
            Assert.AreEqual(-p.NormalForce * 4 / 3, row.NormalForce!.Value, 1e-8);
            Assert.AreEqual(p.Shear * 4 / 3, row.Shear!.Value, 1e-8); Assert.AreEqual(-p.Moment * 4 / 3, row.Moment!.Value, 1e-6);
            Assert.AreEqual("SLU", row.CaseId); Assert.AreEqual(4.0 / 3, row.StripToMemberFactor!.Value, 1e-15);
            var direct = Ntc2018CircularHollowSections.Check(tube.Section, row.NormalForce.Value, row.Shear.Value, row.Moment.Value, new StandardNTC2018Steel());
            Assert.AreEqual(direct.Checks[2].Capacity, row.Verification!.Checks[2].Capacity);
        }
        var baseA = results.Single(r => r.StageId == "A" && r.Elevation == -6000);
        Assert.AreEqual(5000 * 4.0 / 3, Math.Abs(baseA.NormalForce!.Value), 1e-7);
        Assert.AreEqual(200 * 6000 * 4.0 / 3, Math.Abs(baseA.Moment!.Value), 1e-4);
    }
    [TestMethod]
    public void UnsupportedRepresentationsAndIncompleteHistoriesNeverPass()
    {
        var analysis = Analysis(); var tube = Tube();
        foreach (var definitions in new[] { Array.Empty<EmbeddedWallSteelTube>(), new[] { tube with { SingleRowEqualSharingConfirmed = false } } })
            Assert.IsTrue(EmbeddedWallSteelTubes.Check(analysis, 1500, definitions, new StandardNTC2018Steel(), EmbeddedWallSteelCheckKind.StaticUltimate)
                .All(r => r.Outcome == EngineeringOutcome.NotEvaluated && r.Verification is null));
        Assert.ThrowsException<ArgumentException>(() => (tube with { SingleRowEqualSharingConfirmed = false }).FlexuralRigidity(1500));
        Assert.ThrowsException<ArgumentException>(() => (tube with { Spacing = 100 }).FlexuralRigidity(1500));
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallSteelTubes.Check(analysis, 1500, [tube, tube], null, EmbeddedWallSteelCheckKind.StaticUltimate));
        Assert.ThrowsException<InvalidOperationException>(() => EmbeddedWallSteelTubes.Check(Analysis(false), 1500, [tube], null, EmbeddedWallSteelCheckKind.StaticUltimate));
        Assert.ThrowsException<OperationCanceledException>(() => EmbeddedWallSteelTubes.Check(analysis, 1500, [tube], null, EmbeddedWallSteelCheckKind.StaticUltimate, new CancellationToken(true)));
        Assert.IsTrue(EmbeddedWallSteelTubes.Check(analysis, 1500, [tube], null, EmbeddedWallSteelCheckKind.SeismicUltimate).All(r => r.Outcome == EngineeringOutcome.NotEvaluated));
    }
}
