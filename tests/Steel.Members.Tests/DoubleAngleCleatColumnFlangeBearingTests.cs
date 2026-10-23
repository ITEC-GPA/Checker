using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class DoubleAngleCleatColumnFlangeBearingTests
{
    private static ConnectionRolledISection Column(double width = 250, double tw = 10, double tf = 20)
        => new(300, width, tw, tf, 12, 2 * width * tf + (300 - 2 * tf) * tw + 4 * (1 - Math.PI / 4) * 144, 275, 430);
    private static DoubleAngleCleatComponentInput Cleats(bool ntc = false, double vertical = -450000, double axial = 0)
        => new(ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Ordinary EC factors"),
            new(90, 10, 275, 430), 11, new(547, 210, 9, 15.7, 24, 11700, 275, 430),
            new(20, 22, 245, ConnectionBoltGrade.Class8_8, true), 6, new(axial, vertical, 0, 0, 0, 0), true,
            holeClearanceServiceabilitySource: "Separate NTC clearance SLE evidence");
    private static DoubleAngleCleatColumnFlangeBearingInput Input(bool ntc = false, ConnectionRolledISection? column = null,
        DoubleAngleCleatComponentInput? cleats = null, double fu = 430, double top = 100, double bottom = 100,
        string? source = "Independent supporting-column geometry and actual end distances")
        => new(cleats ?? Cleats(ntc), column ?? Column(), 275, fu, top, bottom, source!);
    private static double Value(ConnectionVerification<DoubleAngleCleatColumnFlangeBearingInput> r, string name)
        => r.Trace.Single(t => t.Name == name).Value;
    private static void Equal(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1, Math.Abs(expected)) * 1e-10);
    private static void Unsupported(ConnectionVerification<DoubleAngleCleatColumnFlangeBearingInput> r, string reason)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.AreEqual(0, r.Checks.Count);
        Assert.IsNull(r.Utilization); Assert.IsTrue(r.Diagnostics.Any(s => s.Contains(reason)));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void IndependentTable34ExampleUsesSupportingFlangeAndInteriorPitch(bool ntc)
    {
        var r = DoubleAngleCleatColumnFlangeBearing.Verify(Input(ntc));
        // Gauge=9+2*50=109, edge=(250-109)/2=70.5 -> k1=2.5.
        // Interior alpha=(70/(3*22)-.25)=107/132 governs end alpha=1.
        double resistance = 2.5 * (70d / 66 - .25) * 430 * 20 * 20 / 1.25;
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(12, r.Checks.Count);
        Equal(resistance, Value(r, "Support.BearingResistancePerBolt"));
        Equal(344000, Value(r, "Support.EndBoltResistance"));
        foreach (var check in r.Checks) { Equal(37500, check.Demand); Equal(resistance, check.Resistance); }
        Equal(37500 / resistance, r.Utilization!.Value);
        if (ntc) Assert.IsTrue(r.Checks.All(c => c.Reference.Contains("NTC2018")));
    }

    [TestMethod]
    public void LoadShareIsAlreadyInTheTwoActualSupportGroupsAndIsNotHalvedAgain()
    {
        var p = Input(); var r = DoubleAngleCleatColumnFlangeBearing.Verify(p);
        var original = DoubleAngleCleatComponents.Verify(p.Cleats);
        foreach (var force in original.LeftSupportBolts.Forces)
            Equal(force.Shear, r.Checks.Single(c => c.Id == $"Left.SupportBolt{force.Index + 1}.ColumnFlangeBearing").Demand);
        Equal(450000, r.Checks.Sum(c => c.Demand));
        Equal(-54.5, Value(r, "Left.SupportBolt1.TransversePosition"));
        Equal(54.5, Value(r, "Right.SupportBolt1.TransversePosition"));
    }

    [TestMethod]
    public void ActualColumnEndDistanceIsUsedInsteadOfTheCleatEnd()
    {
        var r = DoubleAngleCleatColumnFlangeBearing.Verify(Input(bottom: 40));
        Equal(40, Value(r, "Support.ConservativeEnd"));
        Equal(2.5 * 40 / 66 * 430 * 20 * 20 / 1.25, r.Checks.First().Resistance);
        var mirrored = DoubleAngleCleatColumnFlangeBearing.Verify(Input(top: 40));
        Equal(r.Utilization!.Value, mirrored.Utilization!.Value);
    }

    [TestMethod]
    public void SupportFlangeUsesItsOwnStrengthAndThicknessRatherThanTheWebOrCleat()
    {
        var a = DoubleAngleCleatColumnFlangeBearing.Verify(Input());
        var b = DoubleAngleCleatColumnFlangeBearing.Verify(Input(column: Column(tf: 25), fu: 400));
        Equal(a.Checks.First().Resistance * 25 / 20 * 400 / 430, b.Checks.First().Resistance);
        Equal(25, Value(b, "Support.FlangeThickness")); Equal(400, Value(b, "Support.FlangeFu"));
    }

    [TestMethod]
    public void ThinSupportNearARealColumnEndCanFailWhileTheOriginalCleatComponentsPass()
    {
        var p = Input(column: Column(tf: 5), bottom: 26.4);
        Assert.AreEqual(ConnectionStatus.Passed, DoubleAngleCleatComponents.Verify(p.Cleats).ComponentAssessment.Status);
        var r = DoubleAngleCleatColumnFlangeBearing.Verify(p);
        Assert.AreEqual(ConnectionStatus.Failed, r.Status); Equal(34400, r.Checks.First().Resistance);
        Equal(37500 / 34400d, r.Utilization!.Value);
    }

    [TestMethod]
    public void SupportHardwareMustClearTheBacksideColumnWebAndRoot()
        => Unsupported(DoubleAngleCleatColumnFlangeBearing.Verify(Input(column: Column(tw: 80))), "HARDWARE_INTERSECTS");

    [TestMethod]
    public void BothWholeLegFootprintsMustLandOnTheActualFlange()
        => Unsupported(DoubleAngleCleatColumnFlangeBearing.Verify(Input(column: Column(width: 180))), "LEGS_EXTEND_BEYOND");

    [TestMethod]
    public void ATooCloseColumnEndCannotReturnAFavorableBearingResult()
        => Unsupported(DoubleAngleCleatColumnFlangeBearing.Verify(Input(bottom: 20)), "MINIMUM_END");

    [DataTestMethod] [DataRow(1e-12, -450000d)] [DataRow(0d, 1e-12)]
    public void OrdinaryAxialForceOrShearReversalStillRequiresAnotherJointModel(double axial, double vertical)
        => Unsupported(DoubleAngleCleatColumnFlangeBearing.Verify(Input(cleats: Cleats(vertical: vertical, axial: axial))), "DOWNWARD_SHEAR");

    [TestMethod]
    public void BearingOnlyDoesNotClaimLocalSupportOrWholeJointQualification()
    {
        var r = DoubleAngleCleatColumnFlangeBearing.Verify(Input());
        StringAssert.Contains(r.Scope, "SUPPORTING COLUMN FLANGE BEARING ONLY");
        StringAssert.Contains(r.Scope, "concurrent support actions");
        StringAssert.Contains(r.Scope, "No support net/block/yield");
        StringAssert.Contains(string.Join(";", r.Assumptions), "not an invented extra bearing factor");
    }

    [TestMethod]
    public void ZeroLoadRemainsFiniteAndOutputsImmutable()
    {
        var r = DoubleAngleCleatColumnFlangeBearing.Verify(Input(cleats: Cleats(vertical: 0)));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Equal(0, r.Utilization!.Value);
        Assert.IsTrue(r.Trace.All(t => double.IsFinite(t.Value)));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionCheck>)r.Checks).Clear());
    }

    [TestMethod]
    public void MissingDetailAndInvalidContractsAreRejected()
    {
        Unsupported(DoubleAngleCleatColumnFlangeBearing.Verify(Input(source: null)), "DETAIL_SOURCE");
        Assert.ThrowsException<ArgumentNullException>(() => DoubleAngleCleatColumnFlangeBearing.Verify(null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(top: 0));
    }
}
