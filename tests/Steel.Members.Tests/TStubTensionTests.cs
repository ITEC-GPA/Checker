using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class TStubTensionTests
{
    private static ConnectionDesignCode Code(bool ntc = false) => ntc ? ConnectionDesignCode.Ntc2018()
        : ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "SCI P398 2013 UK parameter set; only this benchmark");
    private static ConnectionBolt Bolt(double area = 245, ConnectionBoltGrade grade = ConnectionBoltGrade.Class8_8)
        => new(20, 21, area, grade, true);
    private static double Trace<T>(ConnectionVerification<T> r, string name) where T : class => r.Trace.Single(v => v.Name == name).Value;
    private static double Capacity<T>(ConnectionVerification<T> r, string id) where T : class => r.Checks.Single(c => c.Id == id).Resistance;
    private static TStubTensionInput Hand(double t = 12, double force = 100000, bool ntc = false,
        double l1 = 200, double l2 = 200, double e = 60, int rows = 1,
        TStubMode1Method method = TStubMode1Method.PointForce, double width = 0,
        TStubPryingModel model = TStubPryingModel.Develops, TStubBoltElongation? elongation = null,
        string source = "EN1993-1-8 Table6.2 hand calculation; explicitly prescribed effective lengths")
        => new(Code(ntc), Bolt(), t, 275, 40, e, rows, new(l1, l2, source), force, method, width, model, elongation);
    private static void Unsupported<T>(ConnectionVerification<T> r, string diagnostic) where T : class
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.IsFalse(r.IsComplete); Assert.AreEqual(0, r.Checks.Count); Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(diagnostic)), string.Join("; ", r.Diagnostics));
        Assert.IsTrue(r.Trace.All(t => double.IsFinite(t.Value)));
    }

    [DataTestMethod]
    [DataRow(20.5, 33.4, 75.0, 210.0, 233.0, 1, 898005.3358285215, 398563.621091151, 406656.0, 898000.0, 398000.0, 406000.0)]
    [DataRow(25.0, 30.4, 50.0, 125.0, 125.0, 1, 900245.3503506528, 377258.6330409357, 406656.0, 901000.0, 377000.0, 406000.0)]
    [DataRow(20.5, 33.4, 75.0, 332.0, 332.0, 2, 1419703.6737860436, 697838.6360612109, 813312.0, 1420000.0, 697000.0, 812000.0)]
    [DataRow(20.5, 33.4, 75.0, 422.0, 422.0, 3, 1804563.1034268384, 990445.0133067199, 1219968.0, 1797000.0, 988000.0, 1218000.0)]
    public void SciP398C1SingleAndGroupMechanismsMatchPublishedAndIndependentUnroundedCalculations(
        double t, double m, double e, double l1, double l2, int rows, double exact1, double exact2,
        double exact3, double printed1, double printed2, double printed3)
    {
        // SCI P398 C.1 pp86-89,92-93,96-97. Source rounds intermediate Mpl, n and bolt resistances.
        // Printed values are retained separately; independent Decimal reproduction is derive_tstub.py.
        var r = TStubTension.Verify(new(Code(), new(24, 26, 353, ConnectionBoltGrade.Class8_8, true),
            t, 265, m, e, rows, new(l1, l2, "SCI P398 C.1 pp86-97; prescribed row/group lengths"),
            100000, TStubMode1Method.DistributedContact, 39.55));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.AreEqual(exact1, Capacity(r, "TStub.Mode1"), 1e-6);
        Assert.AreEqual(exact2, Capacity(r, "TStub.Mode2"), 1e-6);
        Assert.AreEqual(exact3, Capacity(r, "TStub.Mode3"), 1e-6);
        // 0.6% covers the guide's visible intermediate rounding; exact references above use tight tolerance.
        Assert.AreEqual(printed1, Capacity(r, "TStub.Mode1"), .006 * printed1);
        Assert.AreEqual(printed2, Capacity(r, "TStub.Mode2"), .006 * printed2);
        Assert.AreEqual(printed3, Capacity(r, "TStub.Mode3"), .006 * printed3);
        Assert.AreEqual("TStub.Mode2", r.GoverningCheck.Id);
    }

    [DataTestMethod]
    [DataRow(6.0, 49500.0, 167800.0, "TStub.Mode1")]
    [DataRow(12.0, 198000.0, 200800.0, "TStub.Mode1")]
    [DataRow(20.0, 550000.0, 279022.2222222222, "TStub.Mode2")]
    [DataRow(30.0, 1237500.0, 431800.0, "TStub.Mode3")]
    public void AllThreeModesGovernIndependentHandCases(double t, double mode1, double mode2, string governing)
    {
        // m40, n50, leff200, fy275, two M20 8.8: sum Ft=282240 N.
        var r = TStubTension.Verify(Hand(t: t, force: 0));
        Assert.AreEqual(mode1, Capacity(r, "TStub.Mode1"), 1e-7);
        Assert.AreEqual(mode2, Capacity(r, "TStub.Mode2"), 1e-7);
        Assert.AreEqual(282240, Capacity(r, "TStub.Mode3"), 1e-7);
        var nonzero = TStubTension.Verify(Hand(t: t));
        Assert.AreEqual(governing, nonzero.GoverningCheck.Id);
    }

    [DataTestMethod]
    [DataRow(0.0, true)] [DataRow(197999.0, true)] [DataRow(198000.0, true)] [DataRow(198001.0, false)]
    public void CapacityEqualityAndExceedanceDoNotChangeMechanisms(double demand, bool passed)
    {
        var r = TStubTension.Verify(Hand(force: demand));
        Assert.AreEqual(passed ? ConnectionStatus.Passed : ConnectionStatus.Failed, r.Status);
        Assert.AreEqual(198000, Capacity(r, "TStub.Mode1"), 1e-8);
    }

    [TestMethod]
    public void NtcFactorsAndReferencesRemainDistinctFromTheSupplementaryComponentModel()
    {
        var ec = TStubTension.Verify(Hand()); var ntc = TStubTension.Verify(Hand(ntc: true));
        Assert.AreEqual(198000, Capacity(ec, "TStub.Mode1"), 1e-7);
        Assert.AreEqual(188571.42857142858, Capacity(ntc, "TStub.Mode1"), 1e-7);
        Assert.AreEqual(198704.7619047619, Capacity(ntc, "TStub.Mode2"), 1e-7);
        Assert.AreEqual(Capacity(ec, "TStub.Mode3"), Capacity(ntc, "TStub.Mode3"));
        Assert.IsTrue(ntc.Checks.All(c => c.Reference.Contains("supplementary component model") && c.Reference.Contains("EN1993-1-8")));
    }

    [TestMethod]
    public void ModeTwoUsesItsOwnEffectiveLengthAndPlateFactorDoesNotReplaceConnectionFactor()
    {
        var r = TStubTension.Verify(Hand(l1: 100, l2: 300));
        Assert.AreEqual(99000, Capacity(r, "TStub.Mode1"), 1e-8);
        Assert.AreEqual(222800, Capacity(r, "TStub.Mode2"), 1e-8);
        Assert.AreEqual(282240, Capacity(r, "TStub.Mode3"), 1e-8);
        Assert.AreEqual(1.25, Trace(r, "GammaM2Connection"));
    }

    [DataTestMethod]
    [DataRow(30.0, 30.0, 177531.42857142858)]
    [DataRow(49.0, 49.0, 199884.9438202247)]
    [DataRow(50.0, 50.0, 200800.0)] [DataRow(60.0, 50.0, 200800.0)]
    public void EdgeLeverArmIsCappedAtOnePointTwoFiveM(double e, double n, double mode2)
    {
        var r = TStubTension.Verify(Hand(e: e));
        Assert.AreEqual(n, Trace(r, "N")); Assert.AreEqual(mode2, Capacity(r, "TStub.Mode2"), 1e-7);
    }

    [TestMethod]
    public void DistributedContactIncreasesModeOneOnly()
    {
        var p = TStubTension.Verify(Hand());
        var d = TStubTension.Verify(Hand(method: TStubMode1Method.DistributedContact, width: 32));
        Assert.AreEqual(231804.8780487805, Capacity(d, "TStub.Mode1"), 1e-7);
        Assert.IsTrue(Capacity(d, "TStub.Mode1") > Capacity(p, "TStub.Mode1"));
        Assert.AreEqual(Capacity(p, "TStub.Mode2"), Capacity(d, "TStub.Mode2"));
        Assert.AreEqual(Capacity(p, "TStub.Mode3"), Capacity(d, "TStub.Mode3"));
        Assert.AreEqual("TStub.Mode2", d.GoverningCheck.Id);
    }

    [DataTestMethod]
    [DataRow(1, 86.24)] [DataRow(2, 172.48)] [DataRow(3, 258.72)]
    public void CorrectedPryingLimitCountsRowsNotTwiceAsManyBolts(int rows, double criticalLength)
    {
        var r = TStubTension.Verify(Hand(t: 20, rows: rows));
        Assert.AreEqual(criticalLength, Trace(r, "PryingLengthLimit"), 1e-9);
        Assert.AreEqual(rows, Trace(r, "BoltRows")); Assert.AreEqual(2 * rows, Trace(r, "BoltCount"));
    }

    [DataTestMethod]
    [DataRow(86.239, true)] [DataRow(86.24, true)] [DataRow(86.241, false)] [DataRow(150.0, false)]
    public void ActualElongationControlsPryingIncludingEquality(double length, bool develops)
    {
        var r = TStubTension.Verify(Hand(t: 20, model: TStubPryingModel.FromBoltElongation,
            elongation: new(length - 10, 10, 10, "Grip incl. washers plus half head/nut; drawing T1")));
        Assert.AreEqual(develops ? 1 : 0, Trace(r, "PryingDevelops"));
        Assert.AreEqual(length, Trace(r, "BoltElongationLength"), 1e-10);
        Assert.AreEqual(develops ? 3 : 2, r.Checks.Count);
        Assert.AreEqual(develops ? 279022.2222222222 : 275000, r.GoverningCheck.Resistance, 1e-7);
        Assert.AreEqual(develops ? "TStub.Mode2" : "TStub.NoPryingPlate", r.GoverningCheck.Id);
    }

    [TestMethod]
    public void NoPryingUsesHalfThePointLoadPlateMechanismAndDoesNotInventBoltInteraction()
    {
        var r = TStubTension.Verify(Hand(model: TStubPryingModel.FromBoltElongation,
            elongation: new(400, 10, 10, "Long-grip symmetric T-stub test")));
        Assert.AreEqual(99000, Capacity(r, "TStub.NoPryingPlate"), 1e-8);
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsFalse(r.Checks.Any(c => c.Id == "TStub.Mode2" || c.Id == "TStub.Mode1"));
    }

    [TestMethod]
    public void DistributedContactDoesNotIncreaseNoPryingResistance()
    {
        var elongation = new TStubBoltElongation(400, 10, 10, "Known grip");
        var r = TStubTension.Verify(Hand(method: TStubMode1Method.DistributedContact, width: 32,
            model: TStubPryingModel.FromBoltElongation, elongation: elongation));
        Assert.AreEqual(99000, Capacity(r, "TStub.NoPryingPlate"), 1e-8);
    }

    [TestMethod]
    public void BeamJointPryingAssumptionIsExplicitAndNotOverriddenByOptionalGrip()
    {
        var r = TStubTension.Verify(Hand(elongation: new(400, 10, 10, "Known grip")));
        Assert.AreEqual(1, Trace(r, "PryingDevelops"));
        Assert.IsTrue(r.Assumptions.Any(a => a.Contains("Table6.2 note1")));
        Assert.IsTrue(r.Scope.Contains("not a whole joint"));
        Assert.IsTrue(r.Assumptions.Any(a => a.Contains("does not provide individual bolt forces")));
    }

    [TestMethod]
    public void StrongerBoltsChangeModesTwoAndThreeButNotPlateOnlyMode()
    {
        var input = Hand();
        var strong = new TStubTensionInput(input.Code, Bolt(grade: ConnectionBoltGrade.Class10_9),
            12, 275, 40, 60, 1, input.EffectiveLengths, 100000);
        var r = TStubTension.Verify(strong);
        Assert.AreEqual(198000, Capacity(r, "TStub.Mode1"));
        Assert.AreEqual(240000, Capacity(r, "TStub.Mode2"));
        Assert.AreEqual(352800, Capacity(r, "TStub.Mode3"));
    }

    [TestMethod]
    public void MissingEffectiveLengthDerivationCannotPassEvenAtZeroLoad()
        => Unsupported(TStubTension.Verify(Hand(force: 0, source: " ")), "EffectiveLengthDerivationRequired");
    [TestMethod]
    public void NoPryingCannotBeSelectedWithoutActualBoltGeometry()
        => Unsupported(TStubTension.Verify(Hand(model: TStubPryingModel.FromBoltElongation)), "ActualBoltElongationRequired");
    [TestMethod]
    public void MissingBoltLengthEvidenceIsBlocked()
        => Unsupported(TStubTension.Verify(Hand(elongation: new(20, 10, 10, " "))), "BoltElongationDerivationRequired");
    [TestMethod]
    public void GripCannotBeShorterThanFlange()
        => Unsupported(TStubTension.Verify(Hand(elongation: new(10, 10, 10, "Bad drawing"))), "GripShorterThanTStubFlange");
    [TestMethod]
    public void MinimumEdgeDistanceIsNotHiddenByZeroDemand()
        => Unsupported(TStubTension.Verify(Hand(force: 0, e: 20)), "MinimumEdgeDistance");
    [TestMethod]
    public void ContactDimensionCannotSilentlyBeIgnoredInPointForceModel()
        => Unsupported(TStubTension.Verify(Hand(width: 32)), "ContactWidthRequiresDistributedContactMethod");

    [DataTestMethod]
    [DataRow(0.0)] [DataRow(20.0)] [DataRow(80.0)] [DataRow(100.0)]
    public void DistributedFootprintMustCoverHoleAndFitFlange(double width)
        => Unsupported(TStubTension.Verify(Hand(method: TStubMode1Method.DistributedContact, width: width)), "FootprintOutsideQualifiedGeometry");

    [TestMethod]
    public void NtcNormalHoleClearanceNeedsItsSleEvidence()
    {
        var b = new ConnectionBolt(24, 26, 353, ConnectionBoltGrade.Class8_8, true);
        var code = Code(true); var lengths = new TStubEffectiveLengths(210, 233, "SCI C1 geometry");
        var p = new TStubTensionInput(code, b, 20.5, 265, 33.4, 75, 1, lengths, 10000);
        Unsupported(TStubTension.Verify(p), "NtcHoleClearanceRequiresServiceabilityEvidence");
        p = new(code, b, 20.5, 265, 33.4, 75, 1, lengths, 10000, holeClearanceServiceabilitySource: "Qualified SLE hole check S1");
        Assert.AreEqual(ConnectionStatus.Passed, TStubTension.Verify(p).Status);
    }

    [TestMethod]
    public void PlasticHingeCannotIntersectBoltHoleInQualifiedGeometry()
    {
        var p = new TStubTensionInput(Code(), Bolt(), 12, 275, 10, 60, 1, new(200, 200, "Bad geometry"), 0);
        Unsupported(TStubTension.Verify(p), "BoltHoleIntersectsPlasticHingeLine");
    }

    [TestMethod]
    public void SteelAboveS460IsOutsideScope()
    {
        var p = new TStubTensionInput(Code(), Bolt(), 12, 500, 40, 60, 1, new(200, 200, "Geometry"), 0);
        Unsupported(TStubTension.Verify(p), "CarbonSteelUpToS460Only");
    }

    [TestMethod]
    public void NumericOverflowCannotBecomeFavourableResistance()
        => Unsupported(TStubTension.Verify(Hand(l1: 1e308, l2: 1e308)), "OutsideNumericRange");

    [TestMethod]
    public void LargeButFiniteArithmeticRemainsValidAndBoltFailureStillLimitsResistance()
    {
        var r = TStubTension.Verify(Hand(l1: 1e300, l2: 1e300));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.AreEqual("TStub.Mode3", r.GoverningCheck.Id);
        Assert.IsTrue(r.Trace.All(t => double.IsFinite(t.Value)));
        Assert.IsTrue(r.Checks.All(c => double.IsFinite(c.Resistance)));
    }

    [DataTestMethod]
    [DataRow(-1.0)] [DataRow(0.0)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidEffectiveLengthsAreRejected(double length)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TStubEffectiveLengths(length, 200, "source"));

    [TestMethod]
    public void InvertedModeLengthsAreRejected()
        => Assert.ThrowsException<ArgumentException>(() => new TStubEffectiveLengths(201, 200, "source"));
    [TestMethod]
    public void NegativeTensionIsNotSilentlyTreatedAsZero()
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Hand(force: -1));

    [DataTestMethod]
    [DataRow(0)] [DataRow(21)]
    public void InvalidRowCountsAreRejected(int rows)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Hand(rows: rows));

    [TestMethod]
    public void InvalidEnumsAreRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Hand(method: (TStubMode1Method)42));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Hand(model: (TStubPryingModel)42));
    }

    [TestMethod]
    public void NullInputIsRejected()
        => Assert.ThrowsException<ArgumentNullException>(() => TStubTension.Verify(null!));
}
