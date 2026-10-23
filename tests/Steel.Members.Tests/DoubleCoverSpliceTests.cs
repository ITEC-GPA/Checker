using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class DoubleCoverSpliceTests
{
    // Dimensions mm, stresses MPa, actions N/Nmm. EC3 factors here are explicitly
    // chosen for analytical benchmarks, not an automatic national-annex selection.
    private static ConnectionDesignCode Ec(double gm0 = 1, double gm2p = 1.25, double gm2b = 1.25)
        => ConnectionDesignCode.Eurocode3(gm0, gm2p, gm2b, "Explicit test factors; EN1993-1-8:2005");
    private static DoubleCoverSpliceInput Input(ConnectionDesignCode? code = null, double n = 100000,
        ConnectionPlate? main = null, ConnectionPlate? cover = null, ConnectionBolt? bolt = null,
        int rows = 2, double pitch = 60, double gauge = 80, double mainEnd = 40, double coverEnd = 50,
        bool exposed = false, string? holeEvidence = null, SteelActions? actions = null)
        => new(code ?? Ec(), main ?? new(200, 16, 275, 430), cover ?? new(160, 10, 275, 430),
            bolt ?? new(20, 21, 245, ConnectionBoltGrade.Class8_8, true), actions ?? new(n, 0, 0, 0, 0, 0),
            rows, pitch, gauge, mainEnd, coverEnd, 10, exposed, holeEvidence);
    private static ConnectionCheck Check(DoubleCoverSpliceResult r, string id) => r.Checks.Single(c => c.Id == id);
    private static double Trace(DoubleCoverSpliceResult r, string id) => r.Trace.Single(c => c.Name == id).Value;
    private static void Equal(double expected, double actual, string label = "")
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-12), label);
    private static void Unsupported(DoubleCoverSpliceResult r, string code)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.IsFalse(r.IsComplete);
        Assert.AreEqual(0, r.Checks.Count, "partial checks must not imply success");
        Assert.IsNull(r.Utilization); Assert.IsNull(r.GoverningCheck);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), code);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void CompleteSpliceMatchesIndependentHandCalculation(bool ntc)
    {
        // Explicit arithmetic from EN1993-1-8 Table3.4, 3.10.2; EN1993-1-1 6.2.3;
        // NTC2018 4.2.8.1.1 and 4.2.4.1.2.1. No production calculator is used as oracle.
        var r = DoubleCoverSplice.Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec()));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.AreEqual(36, r.Checks.Count); // eight bolts x three checks + three plates x four checks
        Equal(100000, Trace(r, "MainAxialForce")); Equal(50000, Trace(r, "ForcePerCover"));
        Equal(4, Trace(r, "BoltCountPerSide")); Equal(8, Trace(r, "TotalBoltCount")); Equal(310, Trace(r, "CoverLength"));
        foreach (int side in new[] { 1, 2 })
        {
            var bolts = r.Checks.Where(c => c.Id.StartsWith($"Side{side}.") && c.Id.EndsWith(".BoltShear"));
            Equal(100000, bolts.Sum(c => c.Demand), "each side independently transfers N, never N/2");
            foreach (var check in bolts) { Equal(25000, check.Demand); Equal(188160, check.Resistance); }
        }
        Equal(2.5 * (40.0 / 63) * 430 * 20 * 16 / 1.25, Check(r, "Side1.Row1.Column1.MainBearing").Resistance);
        Equal(2.5 * (60.0 / 63 - .25) * 430 * 20 * 16 / 1.25, Check(r, "Side1.Row2.Column1.MainBearing").Resistance);
        Equal(12500, Check(r, "Side1.Row1.Column1.EachCoverBearing").Demand);
        Equal(2.5 * (60.0 / 63 - .25) * 430 * 20 * 10 / 1.25, Check(r, "Side1.Row1.Column1.EachCoverBearing").Resistance);
        Equal(2.5 * (50.0 / 63) * 430 * 20 * 10 / 1.25, Check(r, "Side1.Row2.Column1.EachCoverBearing").Resistance);
        double gm0 = ntc ? 1.05 : 1;
        Equal(880000 / gm0, Check(r, "Main.GrossYielding").Resistance);
        Equal(782668.8, Check(r, "Main.NetRupture").Resistance);
        Equal(430 * 944 / 1.25 + 275 * 2192 / (Math.Sqrt(3) * gm0), Check(r, "Main.BlockInside").Resistance);
        Equal(430 * 1584 / 1.25 + 275 * 2192 / (Math.Sqrt(3) * gm0), Check(r, "Main.BlockOutside").Resistance);
        foreach (var id in new[] { "Cover1", "Cover2" })
        {
            Equal(50000, Check(r, id + ".NetRupture").Demand);
            Equal(440000 / gm0, Check(r, id + ".GrossYielding").Resistance);
            Equal(365328, Check(r, id + ".NetRupture").Resistance);
            Equal(430 * 590 / 1.25 + 275 * 1570 / (Math.Sqrt(3) * gm0), Check(r, id + ".BlockOutside").Resistance);
        }
        Assert.AreEqual("Main.BlockInside", r.GoverningCheck!.Id);
        Equal(100000 / (430 * 944 / 1.25 + 275 * 2192 / (Math.Sqrt(3) * gm0)), r.Utilization!.Value);
    }

    [TestMethod]
    public void PublishedSciP358Example3PlateMatchesAreasAndRoundedCapacities()
    {
        // SCI P358 (2014), chapter6.9, Example3 sheet9/9, printed p225 (PDF235).
        // Published component, NOT a published validation of this entire double-cover joint.
        // Companion procedure p179 defines block paths and plate gammaM2=1.1 (UK NA).
        var r = DoubleCoverSplice.Verify(Input(Ec(1, 1.1, 1.25),
            main: new(250, 12, 275, 410), cover: new(250, 12, 275, 410),
            bolt: new(24, 26, 353, ConnectionBoltGrade.Class8_8, true),
            rows: 4, pitch: 80, gauge: 150, mainEnd: 50, coverEnd: 50));
        Assert.IsTrue(r.IsComplete);
        Equal(3000, Trace(r, "Cover1.Ag")); Equal(2376, Trace(r, "Cover1.Anet"));
        Equal(888, Trace(r, "Cover1.AntOutside")); Equal(4776, Trace(r, "Cover1.Anv"));
        Equal(825000, Check(r, "Cover1.GrossYielding").Resistance);
        Assert.AreEqual(797000, Check(r, "Cover1.NetRupture").Resistance, 500, "publication rounded to 1 kN");
        Equal(888 * 410 / 1.1 + 4776 * 275 / Math.Sqrt(3), Check(r, "Cover1.BlockOutside").Resistance);
        Equal(2 * .6 * 800 * 353 / 1.25, Check(r, "Side1.Row1.Column1.BoltShear").Resistance);
    }

    [DataTestMethod]
    [DataRow(false, false, false)] [DataRow(false, false, true)]
    [DataRow(false, true, false)] [DataRow(false, true, true)]
    [DataRow(true, false, false)] [DataRow(true, false, true)]
    [DataRow(true, true, false)] [DataRow(true, true, true)]
    public void BoltGradeAndThreadConditionUseTwoShearPlanes(bool ntc, bool highGrade, bool threaded)
    {
        var r = DoubleCoverSplice.Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec(),
            bolt: new(20, 21, 245, highGrade ? ConnectionBoltGrade.Class10_9 : ConnectionBoltGrade.Class8_8, threaded)));
        double alpha = threaded && highGrade ? .5 : .6;
        double area = threaded ? 245 : 100 * Math.PI;
        Equal(2 * alpha * (highGrade ? 1000 : 800) * area / 1.25, Check(r, "Side1.Row1.Column1.BoltShear").Resistance);
    }

    [TestMethod]
    public void NtcEdgeBearingMustNotSilentlyUseEurocodeTransversePitchTerm()
    {
        var ec = DoubleCoverSplice.Verify(Input(gauge: 51));
        var ntc = DoubleCoverSplice.Verify(Input(ConnectionDesignCode.Ntc2018(), gauge: 51));
        Equal(1.4 * 51 / 21 - 1.7, Trace(ec, "Side1.Row1.Column1.MainBearing.K1"));
        Equal(2.5, Trace(ntc, "Side1.Row1.Column1.MainBearing.K1"));
        Assert.IsTrue(Check(ntc, "Side1.Row1.Column1.MainBearing").Resistance > Check(ec, "Side1.Row1.Column1.MainBearing").Resistance);
    }

    [TestMethod]
    public void BearingCanBeLimitedByFuRatio()
    {
        var r = DoubleCoverSplice.Verify(Input(main: new(200, 16, 460, 900), rows: 1, mainEnd: 80));
        Equal(800.0 / 900, Trace(r, "Side1.Row1.Column1.MainBearing.AlphaB"));
    }

    [DataTestMethod]
    [DataRow(1, 100.0, 1.0)] [DataRow(4, 100.0, 1.0)]
    [DataRow(5, 100.0, .975)] [DataRow(14, 100.0, .75)] [DataRow(16, 100.0, .75)]
    public void LongJointReductionHasCorrectThresholdAndFloor(int rows, double pitch, double beta)
    {
        var r = DoubleCoverSplice.Verify(Input(rows: rows, pitch: pitch));
        Equal(beta, Trace(r, "BetaLongJoint")); Equal(188160 * beta, Check(r, "Side1.Row1.Column1.BoltShear").Resistance);
    }

    [DataTestMethod]
    [DataRow(60.0, true)] [DataRow(110.0, false)]
    public void BothInsideAndOutsideBlockPathsAreChecked(double gauge, bool insideControls)
    {
        var r = DoubleCoverSplice.Verify(Input(gauge: gauge, cover: new(180, 10, 275, 430)));
        double inside = Check(r, "Cover1.BlockInside").Resistance, outside = Check(r, "Cover1.BlockOutside").Resistance;
        Assert.AreEqual(insideControls, inside < outside);
        Equal(430 * (gauge - 21) * 10 / 1.25 + 275 * 1570 / Math.Sqrt(3), inside);
        Equal(430 * (180 - gauge - 21) * 10 / 1.25 + 275 * 1570 / Math.Sqrt(3), outside);
    }

    [DataTestMethod]
    [DataRow(0.0, true)] [DataRow(.4, true)] [DataRow(600000.0, true)] [DataRow(1000000.0, false)]
    public void ZeroTinyAndOverloadDemandsRetainActualRatios(double n, bool pass)
    {
        var r = DoubleCoverSplice.Verify(Input(n: n));
        Assert.AreEqual(pass ? ConnectionStatus.Passed : ConnectionStatus.Failed, r.Status);
        foreach (var c in r.Checks) Equal(c.Demand / c.Resistance, c.Utilization);
        if (n == 0) Equal(0, r.Utilization!.Value);
        else Assert.IsTrue(r.Utilization > 0);
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    public void UnsupportedActionsCannotReturnSuccessfulPartialChecks(int component)
    {
        double[] a = { 100000, 0, 0, 0, 0, 0 }; a[component] = component == 0 ? -1 : 1e-12;
        Unsupported(DoubleCoverSplice.Verify(Input(actions: new(a[0], a[1], a[2], a[3], a[4], a[5]))), "ConcentricTensionOnly");
    }

    [TestMethod]
    public void NtcLargerHoleRequiresExplicitServiceabilityEvidence()
    {
        var bolt = new ConnectionBolt(20, 22, 245, ConnectionBoltGrade.Class8_8, true);
        Unsupported(DoubleCoverSplice.Verify(Input(ConnectionDesignCode.Ntc2018(), bolt: bolt)), "NtcHoleClearanceRequiresServiceabilityEvidence");
        var accepted = DoubleCoverSplice.Verify(Input(ConnectionDesignCode.Ntc2018(), bolt: bolt, holeEvidence: "Project report SLE-07"));
        Assert.IsTrue(accepted.IsComplete);
        Assert.AreEqual("Project report SLE-07", accepted.Input.HoleClearanceServiceabilitySource);
        Assert.IsTrue(accepted.Scope.Contains("No welds"));
    }

    [DataTestMethod]
    [DataRow(20.0, 21.0)] [DataRow(24.0, 25.5)]
    public void NtcNominalHoleBoundaryIsAdmittedWithoutDerogation(double d, double d0)
    {
        Assert.IsTrue(DoubleCoverSplice.Verify(Input(ConnectionDesignCode.Ntc2018(),
            bolt: new(d, d0, 245, ConnectionBoltGrade.Class8_8, true))).IsComplete);
    }

    [DataTestMethod]
    [DataRow(12.0)] [DataRow(14.0)]
    public void M12M14TwoMillimetreClearanceCannotBypassSpecialEurocodeRule(double d)
    {
        Unsupported(DoubleCoverSplice.Verify(Input(bolt: new(d, d + 2, 84, ConnectionBoltGrade.Class8_8, true))), "RoundHoleClearanceOutsideQualifiedRange");
        Assert.IsTrue(DoubleCoverSplice.Verify(Input(bolt: new(d, d + 1, 84, ConnectionBoltGrade.Class8_8, true))).IsComplete);
    }

    [DataTestMethod]
    [DataRow("main-end", "MinimumEndOrEdgeDistance")]
    [DataRow("cover-end", "MinimumEndOrEdgeDistance")]
    [DataRow("edge", "MinimumEndOrEdgeDistance")]
    [DataRow("small-pitch", "MinimumBoltPitch")]
    [DataRow("small-gauge", "MinimumBoltPitch")]
    [DataRow("large-pitch", "MaximumBoltPitch")]
    [DataRow("large-gauge", "MaximumBoltPitch")]
    [DataRow("exposed-end", "MaximumExposedEdgeDistance")]
    [DataRow("wide-cover", "CoverWiderThanMainPlate")]
    [DataRow("grade", "CarbonSteelUpToS460Only")]
    [DataRow("small-bolt", "BoltDiameterOutside12To36mm")]
    [DataRow("large-hole", "RoundHoleClearanceOutsideQualifiedRange")]
    public void GeometryAndMaterialScopeGatesAreExplicit(string fixture, string diagnostic)
    {
        var p = fixture switch
        {
            "main-end" => Input(mainEnd: 25), "cover-end" => Input(coverEnd: 25),
            "edge" => Input(cover: new(130, 10, 275, 430)),
            "small-pitch" => Input(pitch: 46), "small-gauge" => Input(gauge: 50),
            "large-pitch" => Input(pitch: 141), "large-gauge" => Input(gauge: 141),
            "exposed-end" => Input(exposed: true, mainEnd: 81),
            "wide-cover" => Input(cover: new(210, 10, 275, 430)),
            "grade" => Input(main: new(200, 16, 500, 600)),
            "small-bolt" => Input(bolt: new(10, 11, 58, ConnectionBoltGrade.Class8_8, true)),
            "large-hole" => Input(bolt: new(20, 23, 245, ConnectionBoltGrade.Class8_8, true)),
            _ => throw new ArgumentException(fixture)
        };
        Unsupported(DoubleCoverSplice.Verify(p), diagnostic);
    }

    [TestMethod]
    public void ExactMinimumDistancesAndMaximumPitchAreAdmitted()
    {
        var r = DoubleCoverSplice.Verify(Input(main: new(100.8, 16, 275, 430), cover: new(100.8, 10, 275, 430),
            gauge: 50.4, pitch: 140, mainEnd: 25.2, coverEnd: 25.2));
        Assert.IsTrue(r.IsComplete, string.Join(",", r.Diagnostics));
    }

    [DataTestMethod]
    [DataRow(0.0)] [DataRow(-1.0)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidDimensionsCannotEnterCalculator(double x)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ConnectionPlate(x, 10, 275, 430));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ConnectionPlate(200, x, 275, 430));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ConnectionBolt(20, 21, x, ConnectionBoltGrade.Class8_8, true));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(pitch: x));
    }

    [TestMethod]
    public void InvalidMaterialBoltAndCodeInputsAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(() => new ConnectionPlate(200, 10, 500, 400));
        Assert.ThrowsException<ArgumentException>(() => new ConnectionBolt(20, 20, 245, ConnectionBoltGrade.Class8_8, true));
        Assert.ThrowsException<ArgumentException>(() => new ConnectionBolt(20, 21, 400, ConnectionBoltGrade.Class8_8, true));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ConnectionBolt(20, 21, 245, (ConnectionBoltGrade)9, true));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(rows: 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Ec(gm2b: 0));
        Assert.ThrowsException<ArgumentException>(() => ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, " "));
        Assert.ThrowsException<ArgumentNullException>(() => DoubleCoverSplice.Verify(null!));
    }

    [TestMethod]
    public void ResultsHaveStableUniqueIdentifiersReferencesUnitsAndImmutableCollections()
    {
        var p = Input(); var r = DoubleCoverSplice.Verify(p);
        Assert.AreSame(p, r.Input);
        Assert.AreEqual(r.Checks.Count, r.Checks.Select(c => c.Id).Distinct().Count());
        Assert.AreEqual(r.Trace.Count, r.Trace.Select(c => c.Name).Distinct().Count());
        Assert.IsTrue(r.Checks.All(c => !string.IsNullOrWhiteSpace(c.Reference)));
        Assert.IsTrue(r.Trace.All(c => !string.IsNullOrWhiteSpace(c.Unit) && double.IsFinite(c.Value)));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionCheck>)r.Checks).Clear());
        var repeat = DoubleCoverSplice.Verify(p);
        CollectionAssert.AreEqual(r.Checks.Select(c => (c.Id, c.Demand, c.Resistance)).ToArray(),
            repeat.Checks.Select(c => (c.Id, c.Demand, c.Resistance)).ToArray());
    }
}
