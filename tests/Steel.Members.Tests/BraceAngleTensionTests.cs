using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BraceAngleTensionTests
{
    private static ConnectionDesignCode Ec() => ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Explicit analytical factors");
    private static BraceAngleTensionInput Input(ConnectionDesignCode? code = null, BraceAngleSection? section = null,
        BraceAngleConnectedLeg leg = BraceAngleConnectedLeg.First, int bolts = 3, double pitch = 60,
        double edge = 35, double tension = 100000, double? equivalentArea = null, string? source = null,
        ConnectionBolt? bolt = null)
        => new(code ?? Ec(), section ?? new(100, 75, 8, 1336, 275, 430), leg,
            bolt ?? new(16, 18, 157, ConnectionBoltGrade.Class8_8, true), tension, bolts, pitch, edge, equivalentArea, source);
    private static ConnectionVerification<BraceAngleTensionInput> Verify(BraceAngleTensionInput? p = null)
        => BraceAngleTension.Verify(p ?? Input());
    private static double T(ConnectionVerification<BraceAngleTensionInput> r, string name)
        => r.Trace.Single(v => v.Name == name).Value;
    private static double Rd(ConnectionVerification<BraceAngleTensionInput> r, string id)
        => r.Checks.Single(v => v.Id == id).Resistance;
    private static void Equal(double expected, double actual)
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-12));
    private static void Unsupported(ConnectionVerification<BraceAngleTensionInput> r, string issue)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.AreEqual(0, r.Checks.Count);
        Assert.IsNull(r.Utilization); Assert.IsNull(r.GoverningCheck);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), issue);
    }

    [DataTestMethod]
    [DataRow(false, 1, 143104.0)] [DataRow(false, 2, 205024.0)] [DataRow(false, 3, 232360.53333333333)]
    [DataRow(true, 1, 143104.0)] [DataRow(true, 2, 205024.0)] [DataRow(true, 3, 232360.53333333333)]
    public void LongLegIndependentCalculationUsesEachNormativeExpression(bool ntc, int bolts, double capacity)
    {
        // EN1993-1-8:2005 p33, equations3.11-3.13 and Table3.8.
        // Constructed sharp angle L100x75x8: A=(100+75-8)*8=1336, An=1192.
        // e2=35, d0=18; p1=60 gives beta2=.5 and beta3=17/30.
        var r = Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec(), bolts: bolts));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(2, r.Checks.Count);
        Equal(1192, T(r, "ActualNetArea")); Equal(0, T(r, "ConnectedToShortLeg"));
        Equal(capacity, Rd(r, "Angle.EffectiveNetRupture"));
        Equal(367400 / (ntc ? 1.05 : 1), Rd(r, "Angle.GrossYielding"));
        Assert.AreEqual("Angle.EffectiveNetRupture", r.GoverningCheck!.Id);
    }

    [DataTestMethod]
    [DataRow(false, 2, 170624.0)] [DataRow(false, 3, 193373.86666666667)]
    [DataRow(true, 2, 170624.0)] [DataRow(true, 3, 193373.86666666667)]
    public void ShortLegUsesEquivalentEqualLegAreaButActualGrossYielding(bool ntc, int bolts, double capacity)
    {
        var r = Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec(), leg: BraceAngleConnectedLeg.Second,
            bolts: bolts, equivalentArea: 1136, source: "Analytical sharp L75x75x8: (75+75-8)*8"));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Equal(1, T(r, "ConnectedToShortLeg")); Equal(992, T(r, "ModelNetArea")); Equal(1192, T(r, "ActualNetArea"));
        Equal(capacity, Rd(r, "Angle.EffectiveNetRupture"));
        Equal(367400 / (ntc ? 1.05 : 1), Rd(r, "Angle.GrossYielding"));
    }

    [DataTestMethod]
    [DataRow(2, 39.6, .4)] [DataRow(2, 45.0, .4)] [DataRow(2, 67.5, .55)] [DataRow(2, 90.0, .7)] [DataRow(2, 180.0, .7)]
    [DataRow(3, 39.6, .5)] [DataRow(3, 45.0, .5)] [DataRow(3, 67.5, .6)] [DataRow(3, 90.0, .7)] [DataRow(3, 180.0, .7)]
    [DataRow(8, 39.6, .5)] [DataRow(8, 67.5, .6)] [DataRow(8, 180.0, .7)]
    public void Table38EndpointsInterpolationAndClampingAreExplicit(int bolts, double pitch, double beta)
    {
        var r = Verify(Input(bolts: bolts, pitch: pitch));
        Assert.IsTrue(r.IsComplete); Equal(beta, T(r, "Beta"));
        Equal(beta * 1192 * 430 / 1.25, Rd(r, "Angle.EffectiveNetRupture"));
    }

    [DataTestMethod]
    [DataRow(BraceAngleConnectedLeg.First)] [DataRow(BraceAngleConnectedLeg.Second)]
    public void EqualAnglesDoNotNeedEquivalentSectionRegardlessOfPhysicalLeg(BraceAngleConnectedLeg leg)
    {
        var r = Verify(Input(section: new(100, 100, 8, 1536, 275, 430), leg: leg));
        Assert.IsTrue(r.IsComplete); Equal(0, T(r, "ConnectedToShortLeg")); Equal(1392, T(r, "ModelNetArea"));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void SwappingSectionLabelsAndConnectedLegKeepsThePhysicalResult(bool shortLeg)
    {
        var a = Verify(Input(leg: shortLeg ? BraceAngleConnectedLeg.Second : BraceAngleConnectedLeg.First,
            equivalentArea: 1136, source: "Equivalent sharp short-leg section"));
        var b = Verify(Input(section: new(75, 100, 8, 1336, 275, 430),
            leg: shortLeg ? BraceAngleConnectedLeg.First : BraceAngleConnectedLeg.Second,
            equivalentArea: 1136, source: "Equivalent sharp short-leg section"));
        Assert.IsTrue(a.IsComplete); Assert.IsTrue(b.IsComplete);
        Equal(Rd(a, "Angle.EffectiveNetRupture"), Rd(b, "Angle.EffectiveNetRupture"));
        Equal(T(a, "ConnectedLeg"), T(b, "ConnectedLeg"));
    }

    [TestMethod]
    public void OneBoltShortLegUsesToeDistanceWithoutInventingEquivalentArea()
    {
        var r = Verify(Input(leg: BraceAngleConnectedLeg.Second, bolts: 1));
        Assert.IsTrue(r.IsComplete); Equal(143104, Rd(r, "Angle.EffectiveNetRupture"));
        StringAssert.Contains(r.Checks.Single(v => v.Id == "Angle.EffectiveNetRupture").Reference, "3.11");
    }

    [TestMethod]
    public void ActualCatalogueAreaIsNotReplacedBySharpCornerArea()
    {
        var r = Verify(Input(section: new(100, 75, 8, 1362, 275, 430)));
        Equal(1362, T(r, "GrossArea")); Equal(1218, T(r, "ActualNetArea"));
        Equal(1362 * 275, Rd(r, "Angle.GrossYielding"));
    }

    [TestMethod]
    public void OneBoltHasNoExtraPointNineFactorAndDoesNotDependOnInactivePitch()
    {
        var r = Verify(Input(bolts: 1, pitch: 1));
        Assert.IsTrue(r.IsComplete); Equal(416, T(r, "EffectiveNetArea")); Equal(143104, Rd(r, "Angle.EffectiveNetRupture"));
    }

    [TestMethod]
    public void StrengthIsNotMultipliedByNumberOfBoltsOrByAnAssumedSecondAngle()
    {
        var two = Verify(Input(bolts: 2, pitch: 90)); var eight = Verify(Input(bolts: 8, pitch: 90));
        Equal(Rd(two, "Angle.EffectiveNetRupture"), Rd(eight, "Angle.EffectiveNetRupture"));
        Assert.IsTrue(eight.Checks.All(v => v.Demand == 100000));
        StringAssert.Contains(eight.Scope, "One angle");
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void MissingAreaOrProvenanceForShortLegFailsClosed(bool withArea)
        => Unsupported(Verify(Input(leg: BraceAngleConnectedLeg.Second,
            equivalentArea: withArea ? 1136 : null)), "ShortLegEquivalentSectionRequired");

    [DataTestMethod]
    [DataRow(2000.0)] [DataRow(100.0)]
    public void InvalidEquivalentAreaFailsClosed(double area)
        => Unsupported(Verify(Input(leg: BraceAngleConnectedLeg.Second, equivalentArea: area, source: "Invalid test section")),
            area > 1336 ? "EquivalentSectionAreaExceedsPhysicalBounds" : "NonpositiveEquivalentNetArea");

    [DataTestMethod]
    [DataRow(20.0, "MinimumToeEdgeDistance")] [DataRow(90.0, "BoltHoleIntersectsHeel")]
    public void HoleMustBeInsideTheConnectedLeg(double edge, string issue)
        => Unsupported(Verify(Input(edge: edge)), issue);

    [TestMethod]
    public void CloseBoltPitchAndUnqualifiedSteelFailClosed()
    {
        Unsupported(Verify(Input(pitch: 39)), "MinimumBoltPitch");
        Unsupported(Verify(Input(section: new(100, 75, 8, 1336, 500, 600))), "CarbonSteelUpToS460Only");
    }

    [DataTestMethod]
    [DataRow(0.0, ConnectionStatus.Passed)] [DataRow(300000.0, ConnectionStatus.Failed)]
    public void ZeroAndOverloadAreCompleteSectionAssessments(double force, ConnectionStatus status)
    {
        var r = Verify(Input(tension: force)); Assert.AreEqual(status, r.Status); Assert.IsTrue(r.IsComplete);
    }

    [TestMethod]
    public void NtcGrossFactorIsDistinctAndEcSupplementIsVisible()
    {
        var r = Verify(Input(code: ConnectionDesignCode.Ntc2018()));
        StringAssert.Contains(r.Checks.Single(v => v.Id == "Angle.GrossYielding").Reference, "NTC2018");
        StringAssert.Contains(r.Checks.Single(v => v.Id == "Angle.EffectiveNetRupture").Reference, "EN1993-1-8");
        Assert.IsTrue(r.Assumptions.Any(v => v.Contains("supplementary EN1993-1-8")));
    }

    [TestMethod]
    public void ComponentDoesNotCertifyOtherChecksOrRemoveGussetEccentricity()
    {
        var r = Verify(); StringAssert.Contains(r.Scope, "No bolt/bearing/block-tearing");
        Assert.IsTrue(r.Assumptions.Any(v => v.Contains("does not remove eccentric actions")));
        Assert.AreEqual(2, r.Checks.Count);
    }

    [TestMethod]
    public void InvalidInputCannotCreateASilentDefaultLegOrCompressionCase()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(leg: (BraceAngleConnectedLeg)42));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(tension: -1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(bolts: 0));
        Assert.ThrowsException<ArgumentException>(() => new BraceAngleSection(100, 75, 80, 1336, 275, 430));
    }
}
