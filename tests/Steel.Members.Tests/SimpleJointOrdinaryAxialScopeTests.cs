using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>
/// Source-based domain regression: SCI AD395 (15 April 2016) excludes ordinary
/// coincident axial/shear actions from P358; AD396 (11 March 2016) validates its
/// one-sided flexible-web prying treatment for tying, not ordinary N+V.
/// Neither note supplies a 5 kN exemption. No accidental UK gammaMu=1.1 is used.
/// See PHASE_2CD_ORDINARY_AXIAL_SCOPE.txt for the source and mechanical boundary.
/// </summary>
[TestClass]
public class SimpleJointOrdinaryAxialScopeTests
{
    private static ConnectionDesignCode Code(bool ntc) => ntc
        ? ConnectionDesignCode.Ntc2018()
        : ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Explicit ordinary ULS regression; no tying policy");

    private static ConnectionBolt Bolt() => new(20, 22, 245, ConnectionBoltGrade.Class8_8, true);
    private static ConnectionRolledISection Column() => new(466, 300, 13.1, 20, 13,
        2 * 300 * 20 + (466 - 2 * 20) * 13.1 + 4 * (1 - Math.PI / 4) * 13 * 13, 265, 410);

    private static FlexibleEndPlateInput EndPlate(ConnectionDesignCode code, double axial,
        ConnectionRolledISection? column = null, ConnectionBolt? bolt = null) => new(
            code, new(200, 10, 275, 410), bolt ?? Bolt(),
            new(412.8, 179.5, 9.5, 16, 10.2, 9450, 275, 410), column ?? Column(),
            4, 140, 90, 357, 5.6, FilletWeldEndCondition.FullSize,
            "Fixture matching consumable qualification",
            new(0, -100000, axial, 0, 0, 0), new(0, 0, 0, 0, 0, 0), true,
            "Fixture frame qualification cannot extend the resistance model to ordinary N+V",
            "Fixture NTC SLE hole-clearance evidence");

    private static DoubleAngleCleatComponentInput Angles(ConnectionDesignCode code, double axial) => new(
        code, new(90, 10, 275, 430), 11, new(547, 210, 9, 15.7, 24, 11700, 275, 430),
        Bolt(), 6, new(axial, -100000, 0, 0, 0, 0), true,
        holeClearanceServiceabilitySource: "Fixture NTC SLE hole-clearance evidence");

    [DataTestMethod]
    [DataRow(false, 5000.0)] [DataRow(false, -5000.0)]
    [DataRow(true, 5000.0)] [DataRow(true, -5000.0)]
    [DataRow(false, 1e-12)] [DataRow(false, -1e-12)]
    [DataRow(true, 1e-12)] [DataRow(true, -1e-12)]
    public void Ad395DoesNotAuthorizeOrdinaryAxialForceAtFlexibleColumnWebEndPlate(bool ntc, double axial)
    {
        var r = FlexibleEndPlate.Verify(EndPlate(Code(ntc), axial));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.IsFalse(r.IsComplete);
        Assert.AreEqual(0, r.Checks.Count);
        Assert.IsNull(r.Utilization);
        Assert.IsNull(r.LoadUtilization);
        Assert.IsNull(r.BoltDistribution);
        Assert.IsNull(r.WeldVerification);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), "CENTRED_DOWNWARD_SHEAR_ONLY");
        Assert.AreEqual(axial, r.Input.Actions.Fz, "The normal action must remain in the immutable input.");
    }

    [DataTestMethod]
    [DataRow(false, 5000.0)] [DataRow(false, -5000.0)]
    [DataRow(true, 5000.0)] [DataRow(true, -5000.0)]
    [DataRow(false, 1e-12)] [DataRow(false, -1e-12)]
    [DataRow(true, 1e-12)] [DataRow(true, -1e-12)]
    public void SbeTyingCannotBePromotedToOrdinaryAxialForceOnDoubleAngles(bool ntc, double axial)
    {
        var r = DoubleAngleCleatComponents.Verify(Angles(Code(ntc), axial));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.ComponentAssessment.Status);
        Assert.IsFalse(r.ComponentAssessment.IsComplete);
        Assert.AreEqual(0, r.ComponentAssessment.Checks.Count);
        Assert.IsNull(r.ComponentAssessment.Utilization);
        Assert.IsNull(r.LoadUtilization);
        Assert.IsNull(r.BeamBolts);
        Assert.IsNull(r.LeftSupportBolts);
        Assert.IsNull(r.RightSupportBolts);
        Assert.IsFalse(r.CanQualifyWholeJoint);
        CollectionAssert.Contains(r.ComponentAssessment.Diagnostics.ToArray(), "DOWNWARD_SHEAR_AT_SUPPORT_FACE_ONLY");
        Assert.AreEqual(axial, r.ComponentAssessment.Input.Actions.Fx);
    }

    [DataTestMethod]
    [DataRow(false, 5000.0)] [DataRow(true, 5000.0)]
    [DataRow(false, 1e-12)] [DataRow(true, 1e-12)]
    public void EqualOpposedTensionDoesNotCertifySharedBoltPryingOrWebLocalContact(bool ntc, double axial)
    {
        var code = Code(ntc); var column = Column(); var bolt = Bolt();
        // Both local positive normals point away from the support. In the common
        // support frame these equal axial pulls cancel, while each local plate and
        // shared through-bolt remains loaded. Cancellation is no qualification.
        var first = EndPlate(code, axial, column, bolt);
        var second = EndPlate(code, axial, column, bolt);
        var r = FlexibleEndPlate.VerifyPair(new(first, second));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.IsFalse(r.IsComplete);
        Assert.AreEqual(0, r.Checks.Count);
        Assert.IsNull(r.Utilization);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), "First:CENTRED_DOWNWARD_SHEAR_ONLY");
        CollectionAssert.Contains(r.Diagnostics.ToArray(), "Second:CENTRED_DOWNWARD_SHEAR_ONLY");
        Assert.AreSame(first, r.Input.First);
        Assert.AreSame(second, r.Input.Second);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ZeroAxialControlsStillExerciseValidOrdinaryShearFixtures(bool ntc)
    {
        var code = Code(ntc);
        var plate = FlexibleEndPlate.Verify(EndPlate(code, 0));
        var angle = DoubleAngleCleatComponents.Verify(Angles(code, 0));
        Assert.AreEqual(ConnectionStatus.Passed, plate.Status, string.Join("; ", plate.Diagnostics));
        Assert.AreEqual(ConnectionStatus.Passed, angle.ComponentAssessment.Status,
            string.Join("; ", angle.ComponentAssessment.Diagnostics));
        Assert.IsTrue(plate.IsComplete);
        Assert.IsTrue(angle.ComponentAssessment.IsComplete);
        Assert.IsFalse(angle.CanQualifyWholeJoint);
    }
}
