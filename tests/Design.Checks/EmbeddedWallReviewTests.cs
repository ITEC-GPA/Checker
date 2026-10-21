using GPC.Checkers.Concrete.Piles;
using GPC.Checkers.Concrete.Walls;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Steel.Sections;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Design.Concrete;
using GPC.Model.Design.Steel;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesignTests;

/// <summary>October 2026 review of the embedded-wall section checks (docs/verifiche/paratie-revisione-2026-10.md).
/// Expected values: NTC 2018 closed forms, recomputed here and in tools/attesi/paratie_revisione/sezioni/test_cases.py.</summary>
[TestClass]
public sealed class EmbeddedWallReviewTests
{
    internal static EmbeddedWallRcStrip Strip(double width = 1000) => new("RC", width, 400, 40, 6, 20, 5, 16, 30, 450, 200000, .075, 2, "XC2");

    [TestMethod]
    public void StripShearWithoutLinksMatchesNtc2018Formula41()
    {
        // NTC 2018 (4.1.23): VRd = max{0.18 k (100 ρl fck)^⅓/γc + 0.15 σcp; 0.035 k^1.5 fck^½ + 0.15 σcp} bw d,
        // k = 1 + (200/d)^½ ≤ 2, ρl ≤ 0.02, σcp = NEd/Ac ≤ 0.2 fcd. Back bars 6φ20 (d 350), front bars 5φ16 (d 352).
        static double Vrd(double compression, double d, double area)
        {
            double k = Math.Min(2, 1 + Math.Sqrt(200 / d)), rho = Math.Min(.02, area / (1000 * d)), scp = Math.Min(compression / 400000, .2 * .85 * 30 / 1.5);
            return Math.Max(.18 * k * Math.Pow(100 * rho * 30, 1.0 / 3) / 1.5 + .15 * scp, .035 * Math.Pow(k, 1.5) * Math.Sqrt(30) + .15 * scp) * 1000 * d;
        }
        double back = 6 * Math.PI * 100, front = 5 * Math.PI * 64;
        var session = Strip().CreateSession();
        foreach (var (n, m, d, area, independent) in new[] { (-1e5, -60e6, 350.0, back, 1.995658e5), (-1e5, 60e6, 352, front, 1.699232e5),
            (0, -1, 350, back, 1.864408e5), (-1e9, -1e6, 350, back, 3.649408e5) })
        {
            double capacity = session.CheckShear(new WallSectionActions(n, 1e4, m)).Check.Capacity!.Value;
            Assert.AreEqual(Vrd(-n, d, area), capacity, 1e-9 * capacity, $"N={n}, M={m}");
            Assert.AreEqual(independent, capacity, 1e-6 * independent, "Value printed by the review script.");
        }
    }
}

/// <summary>Findings of the October 2026 review: FAIL on the reviewed code (master 57f87bf8), pass once fixed.</summary>
[TestClass]
[TestCategory("ReviewFinding")]
public sealed class EmbeddedWallReviewFindingsTests
{
    static WallAnalysisResult Cantilever(double width, double ei) => EmbeddedWallAnalysis.Calculate(new EmbeddedWallModel
    {
        CaseId = "ULS", Top = 0, Bottom = -6000, TributaryWidth = width, MaximumElementLength = 6000, FixBottomDisplacement = true, FixBottomRotation = true,
        Segments = [new() { Top = 0, Bottom = -6000, FlexuralRigidity = ei, SectionId = "S", Source = "review" }],
        Stages = [new() { Id = "F1", Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 }, PointLoads = [new() { Elevation = 0, HorizontalForce = 1000 }] }]
    });

    // S1. The width passed to the checks is never compared with analysis.TributaryWidth: pile and tube actions are scaled by
    // spacing/width with the caller's number, and the RC strip only compares its own width with that same number.
    [TestMethod]
    public void ChecksRefuseAWidthDifferentFromTheAnalysedStrip()
    {
        var pile = new EmbeddedWallRcPile("S", new CircularRcSection(800, 50, 12, 20, 10, 150, 30, 450, 200000, .075, "review", 64), 1000, true,
            "Single row; uniform strip transfer", true, .9, 0, "XC2", 20, 35, 10, PileDetailingMode.Beam, true, true);
        var piles = Cantilever(1500, pile.FlexuralRigidity(1500)); Assert.IsTrue(piles.IsComplete, piles.Diagnostic);
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallRcPiles.Check(piles, 1000, [pile], EmbeddedWallSectionCheckKind.Ultimate),
            "Analysis on 1500 mm, check on 1000 mm: pile actions ×1.0 instead of ×0.667.");
        var tube = new EmbeddedWallSteelTube("S", new CircularHollowSteelSection(new SteelSection(new SectionCHS(200, 8),
            new SteelMaterialEN1993("S355", 210000, 355, 510)), 355, "Certificate", true), 2000, true, "Single row, equal sharing");
        var tubes = Cantilever(1500, tube.FlexuralRigidity(1500)); Assert.IsTrue(tubes.IsComplete, tubes.Diagnostic);
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallSteelTubes.Check(tubes, 1000, [tube], new StandardNTC2018Steel(), EmbeddedWallSteelCheckKind.StaticUltimate),
            "Tube actions ×2.0 instead of ×1.333.");
        var strip = Cantilever(1000, 2e14); Assert.IsTrue(strip.IsComplete, strip.Diagnostic);
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallConcrete.Check(strip, 2000, [EmbeddedWallReviewTests.Strip(2000)], EmbeddedWallSectionCheckKind.Ultimate),
            "1000 mm actions against a 2000 mm section.");
    }
}
