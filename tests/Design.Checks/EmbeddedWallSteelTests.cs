using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Steel.Members;
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
public sealed class EmbeddedWallSteelTests
{
    private static AxialSteelMember Member() => new(new SteelSection(new SectionCHS(200, 8), new SteelMaterialEN1993("S355", 210000, 355, 510)),
        355, "Certificate", true, 3000, 4000, "Pinned strut, both axes");
    private static WallAnalysisResult Analysis(bool stable = true)
    {
        var model = new EmbeddedWallModel
        {
            CaseId = "SLU-A1", Top = 0, Bottom = -6000, TributaryWidth = 1500,
            FixTopDisplacement = stable, FixBottomDisplacement = stable, FixBottomRotation = stable,
            Segments = [new() { Top = 0, Bottom = -6000, FlexuralRigidity = 2e14, SectionId = "W", Source = "Given" }],
            Supports = [new() { Id = "A", Kind = EmbeddedWallSupportKind.Anchor, Elevation = 0, Spacing = 2000, Inclination = Math.PI / 6, AxialStiffness = 10000 },
                new() { Id = "S", Kind = EmbeddedWallSupportKind.Strut, Elevation = 0, Spacing = 3000, Inclination = Math.PI / 3, AxialStiffness = 20000, Capacity = 40000 }],
            Stages = [Stage("P1", 20000, 30000), Stage("P2", 10000, 40000),
                new() { Id = "P3", Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 } }]
        };
        return EmbeddedWallAnalysis.Calculate(model);
        static EmbeddedWallStage Stage(string id, double anchor, double strut) => new()
        {
            Id = id, Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 },
            ActiveSupports = [new() { SupportId = "A", Prestress = anchor }, new() { SupportId = "S", Prestress = strut }]
        };
    }

    [TestMethod]
    public void IndividualSupportForceKeepsSignStageAndCaseWithoutWidthOrInclinationScaling()
    {
        var analysis = Analysis(); Assert.IsTrue(analysis.IsComplete, analysis.Diagnostic);
        var member = Member();
        var result = EmbeddedWallSteel.CheckSupports(analysis, [new("A", member), new("S", member)], new StandardNTC2018Steel(), EmbeddedWallSteelCheckKind.StaticUltimate);
        Assert.AreEqual(4, result.Count); Assert.IsTrue(result.All(r => r.CaseId == "SLU-A1" && r.StageId != "P3"));
        Assert.AreEqual(20000, result[0].NormalForce, 1e-6); Assert.AreEqual(-30000, result[1].NormalForce, 1e-6);
        Assert.AreEqual(10000, result[2].NormalForce, 1e-6); Assert.AreEqual(-40000, result[3].NormalForce, 1e-6);
        Assert.AreEqual(10000, result[0].AnalysedAxialStiffness); Assert.AreEqual(20000, result[1].AnalysedAxialStiffness);
        Assert.IsTrue(result[3].AtModelCapacity); Assert.IsFalse(result[1].AtModelCapacity);
        var direct = Ntc2018AxialSteel.Check(member, -30000, new StandardNTC2018Steel());
        Assert.AreEqual(direct.Checks[2].Capacity, result[1].Verification!.Checks[2].Capacity);
        Assert.AreEqual(direct.Checks[2].Utilization!.Value, result[1].Verification!.Checks[2].Utilization!.Value, 1e-12);
    }

    [TestMethod]
    public void MissingMembersUnsupportedCasesCancellationAndFailedHistoriesAreExplicit()
    {
        var analysis = Analysis(); var member = Member();
        var partial = EmbeddedWallSteel.CheckSupports(analysis, [new("A", member)], new StandardNTC2018Steel(), EmbeddedWallSteelCheckKind.StaticUltimate);
        Assert.IsTrue(partial.Where(r => r.SupportId == "S").All(r => r.Outcome == EngineeringOutcome.NotEvaluated && r.Verification is null));
        foreach (var kind in new[] { EmbeddedWallSteelCheckKind.SeismicUltimate, EmbeddedWallSteelCheckKind.Serviceability, EmbeddedWallSteelCheckKind.Analysis })
            Assert.IsTrue(EmbeddedWallSteel.CheckSupports(analysis, [new("A", member)], new StandardNTC2018Steel(), kind).All(r => r.Outcome == EngineeringOutcome.NotEvaluated));
        Assert.ThrowsException<OperationCanceledException>(() => EmbeddedWallSteel.CheckSupports(analysis, [], null, EmbeddedWallSteelCheckKind.StaticUltimate, new CancellationToken(true)));
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallSteel.CheckSupports(analysis, [new("A", member), new("A", member)], null, EmbeddedWallSteelCheckKind.StaticUltimate));
        var failed = Analysis(false); Assert.IsFalse(failed.IsComplete);
        Assert.ThrowsException<InvalidOperationException>(() => EmbeddedWallSteel.CheckSupports(failed, [], null, EmbeddedWallSteelCheckKind.StaticUltimate));
    }
}
