using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.JointClassificationTestData;

namespace Steel.Members.Tests;

[TestClass]
public class JointStrengthClassificationTests
{
    private static JointStrengthClassificationResult Classify(EndPlateJointResistanceEvidence e,
        JointColumnPosition position = JointColumnPosition.TopOfColumn,
        EndPlatePinnedRotationEvidence? rotation = null, string source = "Fig5.5 adjacent member configuration")
        => JointStrengthClassification.Classify(new JointStrengthClassificationInput(e, position, source, rotation));
    private static EndPlatePinnedRotationEvidence Pinned(EndPlateJointResistanceEvidence e)
        => new(Rotation(e), "Independent global simple-model assessment: actions transferred without adverse moments and load rotations accepted for this case");

    [TestMethod]
    public void IndependentFigureFivePointFiveTopAndContinuousColumnUseOrNotAnd()
    {
        var e = Resistance(mj: 250e6, mb: 300e6, mc: 200e6);
        var top = Classify(e); var within = Classify(e, JointColumnPosition.WithinContinuousEquivalentColumn);
        Assert.AreEqual(200e6, top.FullStrengthThreshold); Assert.AreEqual(50e6, top.PinnedStrengthLimit);
        Assert.AreEqual(300e6, within.FullStrengthThreshold); Assert.AreEqual(75e6, within.PinnedStrengthLimit);
        Assert.AreEqual(JointStrengthClass.FullStrength, top.AppliedClass);
        Assert.AreEqual(JointStrengthClass.PartialStrength, within.AppliedClass);
        Assert.IsFalse(top.CanQualifyWholeJoint); Assert.IsFalse(within.CanQualifyWholeJoint);
    }

    [DataTestMethod]
    [DataRow(200e6, JointStrengthClass.FullStrength)]
    [DataRow(200e6 - 1, JointStrengthClass.PartialStrength)]
    [DataRow(50e6 + 1, JointStrengthClass.PartialStrength)]
    [DataRow(50e6, JointStrengthClass.NominallyPinned)]
    [DataRow(0.0, JointStrengthClass.NominallyPinned)]
    public void StrengthBoundariesHaveTheirNormativeInclusiveSides(double mj, JointStrengthClass expected)
    {
        var e = Resistance(mj: mj); var r = Classify(e, rotation: Pinned(e));
        Assert.AreEqual(expected, r.AppliedClass, string.Join(";", r.Diagnostics));
        Assert.IsTrue(r.IsClassificationComplete);
    }

    [TestMethod]
    public void ContinuousColumnPinnedBoundaryIsSeventyFiveWithRotationEvidence()
    {
        var e = Resistance(mj: 75e6); var r = Classify(e, JointColumnPosition.WithinContinuousEquivalentColumn, Pinned(e));
        Assert.AreEqual(JointStrengthClass.NominallyPinned, r.AppliedClass);
        Assert.IsTrue(r.PinnedRotationEvidenceRequired);
    }

    [TestMethod]
    public void PinnedNumericalRangeWithoutRotationIsNotAPinnedOrPartialClass()
    {
        var r = Classify(Resistance(mj: 50e6));
        Assert.AreEqual(JointStrengthRange.PinnedRange, r.NumericalRange);
        Assert.IsNull(r.AppliedClass); Assert.IsFalse(r.IsClassificationComplete);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), "PinnedRotationAndGlobalModelEvidenceRequired");
    }

    [TestMethod]
    public void RotationProofForAnotherResistanceSnapshotIsNotReused()
    {
        var first = Resistance(mj: 50e6); var sameNumbersDifferentCase = Resistance(mj: 50e6);
        var r = Classify(sameNumbersDifferentCase, rotation: Pinned(first));
        Assert.IsNull(r.AppliedClass);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), "PinnedRotationEvidenceBelongsToAnotherResistanceCase");
    }

    [TestMethod]
    public void ThinPlateWithoutActualDuctileGovernanceDoesNotQualifyPinnedStrength()
    {
        var e = Resistance(mj: 50e6, mode: EndPlateGoverningMechanism.ColumnWebCompression);
        var r = Classify(e, rotation: Pinned(e));
        Assert.AreEqual(JointStrengthRange.PinnedRange, r.NumericalRange);
        Assert.IsNull(r.AppliedClass);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), "PinnedRotationSufficiencyNotDemonstrated");
    }

    [TestMethod]
    public void PlasticSufficiencyAloneDoesNotSupplyTheGlobalPinnedModelAssessment()
    {
        var e = Resistance(mj: 50e6);
        var r = Classify(e, rotation: new EndPlatePinnedRotationEvidence(Rotation(e), ""));
        Assert.IsNull(r.AppliedClass);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), "PinnedGlobalAnalysisCompatibilityEvidenceRequired");
    }

    [TestMethod]
    public void ComponentSubsetCannotBePromotedToCompleteJointStrength()
    {
        var r = Classify(Resistance(mj: 1e9, scope: JointResistanceEvidenceScope.ComponentSubset));
        Assert.IsNull(r.AppliedClass); Assert.IsNull(r.NumericalRange);
        Assert.IsNull(r.FullStrengthThreshold); Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void MissingAdjacentConfigurationOrResistanceEvidencePreventsAppliedClass()
    {
        Assert.IsNull(Classify(Resistance(), source: "").AppliedClass);
        Assert.IsNull(Classify(Resistance(resistance: "")).AppliedClass);
        Assert.IsNull(Classify(Resistance(detail: "")).AppliedClass);
    }

    [TestMethod]
    public void StiffnessAndStrengthClassesAreIndependent()
    {
        var s = JrcStiffness();
        var semi = JointStiffnessClassification.Classify(new JointStiffnessClassificationInput(s,
            new JointFrameMemberStiffness(500e6, 6000), Frame(), "same plane and detail"));
        var full = Classify(Resistance(mj: 250e6));
        Assert.AreEqual(JointStiffnessClass.SemiRigid, semi.AppliedClass);
        Assert.AreEqual(JointStrengthClass.FullStrength, full.AppliedClass);
        var rigid = JointStiffnessClassification.Classify(new JointStiffnessClassificationInput(s,
            new JointFrameMemberStiffness(s.InitialStiffness!.Value / 10, 210000), Frame(), "same plane and detail"));
        var partial = Classify(Resistance(mj: 100e6));
        Assert.AreEqual(JointStiffnessClass.Rigid, rigid.AppliedClass);
        Assert.AreEqual(JointStrengthClass.PartialStrength, partial.AppliedClass);
    }

    [TestMethod]
    public void TwoEquivalentColumnResistancesControlWhenBelowBeamResistance()
    {
        var r = Classify(Resistance(mj: 100e6, mb: 300e6, mc: 60e6), JointColumnPosition.WithinContinuousEquivalentColumn);
        Assert.AreEqual(120e6, r.FullStrengthThreshold);
        Assert.AreEqual(30e6, r.PinnedStrengthLimit);
        Assert.AreEqual(JointStrengthClass.PartialStrength, r.AppliedClass);
    }

    [TestMethod]
    public void LargeFiniteColumnResistanceDoesNotOverflowWhenBeamControls()
    {
        var r = Classify(Resistance(mj: 300e6, mc: double.MaxValue), JointColumnPosition.WithinContinuousEquivalentColumn);
        Assert.AreEqual(300e6, r.FullStrengthThreshold); Assert.AreEqual(JointStrengthClass.FullStrength, r.AppliedClass);
        Assert.IsTrue(r.Trace.All(t => double.IsFinite(t.Value)));
    }
}
