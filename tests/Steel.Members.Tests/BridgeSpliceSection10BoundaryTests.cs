using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Conservative library domain at the compressed limit; EN1993-1-5:2006 7.1(4)-(5).
/// Section10 is an alternative, not a mandatory referral. Same-face stresses, tension positive.
/// Independent free-body oracle: between the two innermost columns, each web cover
/// takes N*Aw/(2*A), V/2 and M(x)=-V*x/2. No buckling model is inferred from bolt pitch.</summary>
[TestClass]
public class BridgeSpliceSection10BoundaryTests
{
    static WeldedIBeamSpliceDetailInput Detail(bool ntc, double n, double v, double pitch = 56, double first = 28,
        double thickness = 4, int rows = 4)
    {
        var p = BridgeSpliceEffectiveCompressionTests.Input(ntc, n, v, outerWidth: 180);
        var b = p.Plates.Bolts; var d = b.Detail;
        var f = new WeldedIBeamSpliceForceInput(d.Section, d.Top, d.Bottom, new(280, thickness, 355, 510),
            new(3, rows, pitch, pitch, first), 60, 10, d.Actions);
        return new(new(p.Plates.Policy, new(b.Code, f, b.FlangeBolt,
            new ConnectionBolt(12, 13, 84.3, ConnectionBoltGrade.Class8_8, true),
            b.FlangeHardwareDiameter, 24, "Concomitant ULS", "NTC SLE clearance")), p.Exposure);
    }

    [DataTestMethod]
    [DataRow(false, -1e-8)] [DataRow(true, -1e-8)]
    [DataRow(false, 0d)] [DataRow(true, 0d)]
    [DataRow(false, -1e-14)] [DataRow(true, -1e-14)]
    [DataRow(false, 1e-8)] [DataRow(true, 1e-8)]
    public void ZeroTensileEdgeBelongsToTheCompressedLimit(bool ntc, double extraCompression)
    {
        // Use a deliberately failed interaction so it stays observable even when another cut
        // is outside the method's domain: favourable partial checks are intentionally hidden.
        const double b = 280, t = 4, v = 400000, x = -28; // first bolt distance is measured from the butt plane
        const double parentArea = 2 * 450 * 30 + 320 * 16, webArea = 320 * 16;
        double nCover = -6 * Math.Abs(v * x / 2) / b * (1 + extraCompression);
        var p = Detail(ntc, nCover * 2 * parentArea / webArea, v);
        var cuts = WeldedIBeamSplicePlates.Verify(p.Plates).Covers.Single(c => c.Cover == WeldedSpliceCover.WebNegative).Cuts;
        int k = Enumerable.Range(0, cuts.Count).Single(i => cuts[i].X == x && cuts[i].ColumnLimit == 1);
        var cut = cuts[k];
        Assert.AreEqual(nCover, cut.N, 1e-8);
        Assert.AreEqual(v / 2, Math.Abs(cut.V), 1e-8);
        Assert.AreEqual(Math.Abs(v * x / 2), Math.Abs(cut.M), 1e-7);
        double expectedEdge = -extraCompression * 6 * Math.Abs(v * x / 2) / (b * b * t);
        Assert.AreEqual(expectedEdge, cut.N / (b * t) + 6 * Math.Abs(cut.M) / (b * b * t), 1e-10);
        var r = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(p);
        string id = "WebNegative.Cut" + k;
        Assert.AreEqual(extraCompression >= -1e-12 ? 1d : 0d,
            r.Trace.Single(t => t.Name == id + ".CompressedWithoutTensileFibre").Value);
        Assert.AreEqual(extraCompression < -1e-12, r.Checks.Any(c => c.Id == id + ".ShearNormalInteraction"),
            id + "; edge=" + expectedEdge + "; " + string.Join(";", r.Diagnostics) + "; "
            + string.Join(";", r.Checks.Where(c => c.Id.StartsWith(id + ".")).Select(c => c.Id + ":" + c.Demand + "/" + c.Resistance)));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void PureShearDoesNotBecomeCompressionAtZeroNormalStress(bool ntc)
    {
        var p = Detail(ntc, 0, 200000);
        var cuts = WeldedIBeamSplicePlates.Verify(p.Plates).Covers.Single(c => c.Cover == WeldedSpliceCover.WebNegative).Cuts;
        int k = Enumerable.Range(0, cuts.Count).Single(i => cuts[i].X == 0);
        Assert.AreEqual(0d, cuts[k].N, 1e-8); Assert.AreEqual(0d, cuts[k].M, 1e-7);
        var r = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(p);
        Assert.IsTrue(r.Checks.Any(c => c.Id == "WebNegative.Cut" + k + ".ShearNormalInteraction"));
        Assert.IsFalse(r.Diagnostics.Any(d => d.Contains("WHOLE_PLATE_COMPRESSED")));
    }

    [DataTestMethod]
    [DataRow(false, 6d, 2, 70d, 400000d)] [DataRow(true, 6d, 2, 70d, 400000d)]
    [DataRow(false, 4d, 4, 56d, 200000d)] [DataRow(true, 4d, 4, 56d, 200000d)]
    [DataRow(false, 3d, 5, 42d, 120000d)] [DataRow(true, 3d, 5, 42d, 120000d)]
    public void DenserBoltsDoNotQualifyTheCompressedPanelModel(bool ntc, double t, int rows, double pitch, double v)
    {
        var p = Detail(ntc, -10000, v, pitch, pitch / 2, t, rows);
        var r = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(p);
        Assert.IsTrue(pitch <= Math.Min(14 * t, 200));
        Assert.IsFalse(r.IsComplete);
        Assert.AreNotEqual(ConnectionStatus.Passed, r.Status);
        Assert.IsTrue(r.Diagnostics.Contains("WebNegative.HIGH_SHEAR_COMPRESSED_COVER_PANEL_AND_SUBPANELS_NOT_QUALIFIED"));
        Assert.IsFalse(r.Diagnostics.Any(d => d.Contains("SHEAR_PANEL_RESTRAINT_NOT_ESTABLISHED")));
        // The diagnostic concerns the model, not the already satisfied pitch screen.
        Assert.IsTrue(r.Trace.Any(x => x.Name.StartsWith("WebNegative.Cut")
            && x.Name.EndsWith(".ShearRatio") && x.Value > .5));
    }

    [DataTestMethod]
    [DataRow(false, .99999999)] [DataRow(true, .99999999)]
    [DataRow(false, 1.00000001)] [DataRow(true, 1.00000001)]
    public void HalfShearResistanceBoundaryIsSeparateFromNormalStressBoundary(bool ntc, double ratio)
    {
        // Independent EN1993-1-5 A.3 / Table5.1 calculation: long 280x4 panel, eta=1.
        double tauCr = 5.34 * Math.PI * Math.PI * 210000 / (12 * .91) * Math.Pow(4d / 280, 2);
        double lambda = Math.Sqrt(355 / (Math.Sqrt(3) * tauCr));
        double vbw = .83 / lambda * 280 * 4 * 355 / (Math.Sqrt(3) * (ntc ? 1.05 : 1));
        // Each cover receives half the total shear, hence v=Vbw gives eta3=0.5.
        var r = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(Detail(ntc, -10000, vbw * ratio));
        Assert.AreEqual(ratio > 1, r.Diagnostics.Any(d => d.Contains("COMPRESSED_COVER_PANEL_AND_SUBPANELS")));
    }
}
