using GPC.Checkers.Geotechnics.Anchors;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics.Anchors;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Design.Checks;

[TestClass]
public sealed class EmbeddedWallAnchorTests
{
    private static WallAnalysisResult Analysis() => EmbeddedWallAnalysis.Calculate(new EmbeddedWallModel
    {
        Name = "Anchor transfer", CaseId = "A1M1", Top = 0, Bottom = -4000, TributaryWidth = 1500, MaximumElementLength = 1000,
        FixTopDisplacement = true, FixTopRotation = true,
        Segments = [new() { Top = 0, Bottom = -4000, FlexuralRigidity = 1e15, SectionId = "S", Source = "test" }],
        Supports = [new() { Id = "T", Elevation = 0, Kind = EmbeddedWallSupportKind.Anchor, AxialStiffness = 1000, Inclination = Math.PI / 6, Spacing = 2000 }],
        Stages = [new() { Id = "F1", ActiveSupports = [new() { SupportId = "T", Prestress = 100000 }] },
            new() { Id = "F2", ActiveSupports = [new() { SupportId = "T", Prestress = 200000 }] }, new() { Id = "Removed" }]
    });
    private static EmbeddedWallAnchor Definition(string execution = "IRS execution") => new("T", true, GroundAnchorEvidence.CalculatedProfiles,
        [new("Profile A", 10000, 4000, 200, 6000, GroundAnchorInjection.IRS,
            [new(10000, 14000, GroundAnchorSoilKind.MediumSand, 2, 3, 1.5, "Pressuremeter profile")], execution, "Isolated stable zone")],
        [], "Preliminary site confirmation", "Valid geometry in F1 and F2; removal in F3");

    [TestMethod]
    public void AnchorChecksPreservePhaseAndIndividualForceWithoutRefactoring()
    {
        var analysis = Analysis(); Assert.IsTrue(analysis.IsComplete);
        var result = EmbeddedWallAnchors.Check(analysis, [Definition()], new StandardNTC2018Geotechnics(), GeotechnicalSituation.PersistentTransient, true);
        Assert.AreEqual(2, result.Count); Assert.AreEqual("F1", result[0].StageId); Assert.AreEqual("F2", result[1].StageId);
        Assert.AreEqual("A1M1", result[0].CaseId); Assert.AreEqual(100000, result[0].NormalForce, 1e-8); Assert.AreEqual(200000, result[1].NormalForce, 1e-8);
        // Independent full bulb: pi*200*1.5*4000*.25; xi=1.8, gammaR=1.2. No b/s or cosine on N.
        double rd = Math.PI * 200 * 1.5 * 4000 * .25 / 1.8 / 1.2;
        Assert.AreEqual(rd, result[0].Verification!.DesignResistance!.Value, 1e-6);
        Assert.AreEqual(200000 / rd, result[1].Verification!.Utilization!.Value, 1e-12);
        Assert.IsTrue(result.All(r => r.Outcome == EngineeringOutcome.Satisfied));
    }

    [TestMethod]
    public void WrongBasisUnsupportedScopeAndMissingDefinitionsCannotPass()
    {
        var analysis = Analysis(); var standard = new StandardNTC2018Geotechnics();
        foreach (var result in new[]
        {
            EmbeddedWallAnchors.Check(analysis, [Definition()], standard, GeotechnicalSituation.PersistentTransient, false),
            EmbeddedWallAnchors.Check(analysis, [Definition("")], standard, GeotechnicalSituation.PersistentTransient, true),
            EmbeddedWallAnchors.Check(analysis, [], standard, GeotechnicalSituation.PersistentTransient, true),
            EmbeddedWallAnchors.Check(analysis, [Definition()], standard, GeotechnicalSituation.Seismic, true)
        }) Assert.IsTrue(result.All(r => r.Outcome == EngineeringOutcome.NotEvaluated));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => EmbeddedWallAnchors.Check(analysis, [Definition()], standard,
            GeotechnicalSituation.PersistentTransient, true, cancellation.Token));
    }
}
