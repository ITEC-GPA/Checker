using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.JointClassificationTestData;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateRotationCapacityTests
{
    private static double V(EndPlateRotationCapacityResult r, string name) => r.Trace.Single(t => t.Name == name).Value;
    private static void Status(EndPlateRotationCapacityStatus expected, EndPlateRotationCapacityResult r)
    {
        Assert.AreEqual(expected, r.Status, string.Join(";", r.Diagnostics));
        Assert.IsNull(r.RotationCapacityRad); Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.Trace.All(t => double.IsFinite(t.Value)));
    }

    [TestMethod]
    public void JrcThinPlatesDoNotQualifyAColumnWebCompressionControlledJoint()
    {
        // JRC96658 example3.3: p173 web compression460.90kN governs (panel548.88kN);
        // p174 Mj224.5kNm; p170 beam Mpl515.59kNm. Both thin plates alone are insufficient.
        var r = Rotation(Resistance(mj: 224.5e6, mb: 515.59e6,
            mode: EndPlateGoverningMechanism.ColumnWebCompression,
            governing: "JRC96658 p173/PDF189 column web compression governs"));
        Assert.AreEqual(17.822959135351955, V(r, "EndPlateThicknessLimit"), 1e-12);
        Assert.AreEqual(17.822959135351955, V(r, "ColumnFlangeThicknessLimit"), 1e-12);
        Assert.IsTrue(15 < V(r, "EndPlateThicknessLimit"));
        Assert.IsTrue(16.5 < V(r, "ColumnFlangeThicknessLimit"));
        Status(EndPlateRotationCapacityStatus.NotDemonstratedBySimplifiedRules, r);
        Assert.AreEqual(0, r.AppliedRules.Count);
    }

    [DataTestMethod]
    [DataRow(12.28036126208316, true)]
    [DataRow(12.28036126208316 - .000001, true)]
    [DataRow(12.28036126208316 + .000001, false)]
    public void IndependentM20S275ThicknessLimitIncludesEquality(double thickness, bool qualifies)
    {
        var r = Rotation(Resistance(column: Column(tf: 30, fy: 275), columnFlangeFy: 275,
            plate: new ConnectionPlate(250, thickness, 275, 430),
            bolt: new ConnectionBolt(20, 22, 245, ConnectionBoltGrade.Class8_8, true)), JointNominalSteelGrade.S275);
        Assert.AreEqual(12.28036126208316, V(r, "EndPlateThicknessLimit"), 1e-12);
        Status(qualifies ? EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis
            : EndPlateRotationCapacityStatus.NotDemonstratedBySimplifiedRules, r);
    }

    [TestMethod]
    public void ThinComponentCanDifferFromTheGoverningBendingComponent()
    {
        var flangeGoverns = Rotation(Resistance(column: Column(tf: 30),
            mode: EndPlateGoverningMechanism.ColumnFlangeBending));
        var plateGoverns = Rotation(Resistance(plate: new ConnectionPlate(250, 30, 235, 360),
            mode: EndPlateGoverningMechanism.EndPlateBending));
        Status(EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis, flangeGoverns);
        Status(EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis, plateGoverns);
    }

    [DataTestMethod] [DataRow(69.0, true)] [DataRow(68.999, true)] [DataRow(69.001, false)]
    public void GoverningPanelClearWebSlendernessBoundaryIncludesEquality(double ratio, bool qualifies)
    {
        // fy235 makes epsilon exactly1; no subtraction uncertainty at the equality case.
        var column = Column(h: 10 * ratio + 40, tw: 10, tf: 20, r: 0);
        var r = Rotation(Resistance(column: column, mode: EndPlateGoverningMechanism.ColumnWebPanelShear));
        Assert.AreEqual(69, V(r, "ColumnWebSlendernessLimit"));
        Status(qualifies ? EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis
            : EndPlateRotationCapacityStatus.NotDemonstratedBySimplifiedRules, r);
    }

    [TestMethod]
    public void S355IndependentWebLimitUsesActualWebFy()
    {
        var r = Rotation(Resistance(column: Column(fy: 355), columnFlangeFy: 355,
            plate: new ConnectionPlate(250, 15, 355, 510),
            mode: EndPlateGoverningMechanism.ColumnWebPanelShear), JointNominalSteelGrade.S355);
        Assert.AreEqual(56.13953942921107, V(r, "ColumnWebSlendernessLimit"), 1e-12);
        Status(EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis, r);
    }

    [TestMethod]
    public void FlangeFyAndWebFyAreNotInterchanged()
    {
        var r = Rotation(Resistance(column: Column(fy: 355), columnFlangeFy: 335,
            plate: new ConnectionPlate(250, 10, 335, 510)), JointNominalSteelGrade.S355);
        Assert.AreEqual(56.13953942921107, V(r, "ColumnWebSlendernessLimit"), 1e-12);
        Assert.AreEqual(.36 * 24 * Math.Sqrt(1000d / 335), V(r, "ColumnFlangeThicknessLimit"), 1e-12);
        // Grade S355 remains eligible although thickness-dependent fy is335MPa.
        Status(EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis, r);
    }

    [DataTestMethod] [DataRow(50000.0, true)] [DataRow(-50000.0, true)]
    [DataRow(50000.001, false)] [DataRow(-50000.001, false)]
    public void FivePercentAxialLimitAppliesToBothSigns(double axial, bool eligible)
    {
        var r = Rotation(Resistance(n: axial));
        Status(eligible ? EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis
            : EndPlateRotationCapacityStatus.OutOfScope, r);
    }

    [DataTestMethod] [DataRow(JointNominalSteelGrade.S235, true)]
    [DataRow(JointNominalSteelGrade.S275, true)] [DataRow(JointNominalSteelGrade.S355, true)]
    [DataRow(JointNominalSteelGrade.S460, false)] [DataRow(JointNominalSteelGrade.Other, false)]
    public void SimplifiedCriteriaUseExplicitNominalGrades(JointNominalSteelGrade grade, bool eligible)
    {
        Status(eligible ? EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis
            : EndPlateRotationCapacityStatus.OutOfScope, Rotation(Resistance(), grade));
    }

    [TestMethod]
    public void AnyOneOutOfFieldMaterialPreventsSimplifiedRotationQualification()
    {
        var e = Resistance();
        var r = EndPlateRotationCapacity.Assess(new EndPlateRotationCapacityInput(e,
            JointNominalSteelGrade.S235, JointNominalSteelGrade.S235, JointNominalSteelGrade.S460, "mixed grades"));
        Status(EndPlateRotationCapacityStatus.OutOfScope, r);
    }

    [DataTestMethod] [DataRow(360e6, true)] [DataRow(360e6 + 1, true)] [DataRow(360e6 - 1, false)]
    public void OnePointTwoConnectedBeamMomentIsAnExemptionNotDuctility(double mj, bool exempt)
    {
        var r = Rotation(Resistance(mj: mj, mode: EndPlateGoverningMechanism.ColumnWebCompression));
        Status(exempt ? EndPlateRotationCapacityStatus.NotRequiredByOverstrength
            : EndPlateRotationCapacityStatus.NotDemonstratedBySimplifiedRules, r);
    }

    [TestMethod]
    public void FullStrengthMinimumIsNotTheOnePointTwoBeamReference()
    {
        // Mj250>=min(Mb300,Mc200), and >=1.2*Mc; but <1.2*Mb360.
        var e = Resistance(mj: 250e6, mode: EndPlateGoverningMechanism.ColumnWebCompression);
        var strength = JointStrengthClassification.Classify(new JointStrengthClassificationInput(e,
            JointColumnPosition.TopOfColumn, "Actual top-of-column configuration"));
        Assert.AreEqual(JointStrengthClass.FullStrength, strength.AppliedClass);
        Status(EndPlateRotationCapacityStatus.NotDemonstratedBySimplifiedRules, Rotation(e));
    }

    [TestMethod]
    public void OverstrengthExemptionDoesNotInheritSimplifiedMaterialAndAxialLimits()
    {
        var r = Rotation(Resistance(mj: 360e6, n: 100000,
            mode: EndPlateGoverningMechanism.BoltShear), JointNominalSteelGrade.S460);
        Status(EndPlateRotationCapacityStatus.NotRequiredByOverstrength, r);
        CollectionAssert.Contains(r.AppliedRules.ToArray(), "EN1993-1-8:2005 6.4.1(3)");
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BoltShearGovernanceCannotBeHiddenByBendingCoGovernance(bool reverse)
    {
        EndPlateGoverningMechanism[] modes = [EndPlateGoverningMechanism.EndPlateBending, EndPlateGoverningMechanism.BoltShear];
        var r = Rotation(Resistance(modes: reverse ? modes.Reverse() : modes));
        Status(EndPlateRotationCapacityStatus.CannotAssumeSufficientForPlasticAnalysis, r);
        CollectionAssert.Contains(r.AppliedRules.ToArray(), "EN1993-1-8:2005 6.4.2(3)");
    }

    [TestMethod]
    public void BoltTensionIsNotMisquotedAsTheBoltShearProhibition()
    {
        var r = Rotation(Resistance(mode: EndPlateGoverningMechanism.BoltTension));
        Status(EndPlateRotationCapacityStatus.NotDemonstratedBySimplifiedRules, r);
        CollectionAssert.DoesNotContain(r.AppliedRules.ToArray(), "EN1993-1-8:2005 6.4.2(3)");
    }

    [TestMethod]
    public void EveryCoGoverningMechanismMustHaveASufficientCriterion()
    {
        Status(EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis, Rotation(Resistance(modes:
            [EndPlateGoverningMechanism.EndPlateBending, EndPlateGoverningMechanism.ColumnFlangeBending,
             EndPlateGoverningMechanism.ColumnWebPanelShear])));
        Status(EndPlateRotationCapacityStatus.NotDemonstratedBySimplifiedRules, Rotation(Resistance(modes:
            [EndPlateGoverningMechanism.EndPlateBending, EndPlateGoverningMechanism.WeldFailure])));
    }

    [TestMethod]
    public void PartialResistanceCannotQualifyRotationEvenUnderApparentOverstrength()
    {
        Status(EndPlateRotationCapacityStatus.MissingEvidence, Rotation(Resistance(mj: 1e9,
            scope: JointResistanceEvidenceScope.ComponentSubset)));
    }

    [TestMethod]
    public void MissingMechanismsOrMaterialEvidenceIsNotAssumedFavourable()
    {
        Status(EndPlateRotationCapacityStatus.MissingEvidence, Rotation(Resistance(modes: [])));
        Status(EndPlateRotationCapacityStatus.MissingEvidence, Rotation(Resistance(governing: "")));
        Status(EndPlateRotationCapacityStatus.MissingEvidence, Rotation(Resistance(), source: ""));
        Status(EndPlateRotationCapacityStatus.MissingEvidence, Rotation(Resistance(resistance: "")));
        Status(EndPlateRotationCapacityStatus.MissingEvidence, Rotation(Resistance(detail: "")));
    }

    [TestMethod]
    public void EvidenceRejectsInvalidNumbersAndSnapshotsAllGovernors()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Resistance(mj: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Resistance(mb: 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Resistance(n: double.PositiveInfinity));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Resistance(mode: (EndPlateGoverningMechanism)999));
        var modes = new List<EndPlateGoverningMechanism> { EndPlateGoverningMechanism.BoltShear };
        var e = Resistance(modes: modes); modes.Clear(); modes.Add(EndPlateGoverningMechanism.EndPlateBending);
        Status(EndPlateRotationCapacityStatus.CannotAssumeSufficientForPlasticAnalysis, Rotation(e));
        Assert.AreEqual(EndPlateGoverningMechanism.BoltShear, e.GoverningMechanisms.Single());
    }
}
