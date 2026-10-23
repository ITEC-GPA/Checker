using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.JointClassificationTestData;

namespace Steel.Members.Tests;

[TestClass]
public class JointStiffnessClassificationTests
{
    private static JointStiffnessClassificationResult Classify(EndPlateRotationalStiffnessResult s,
        double inertia = 500e6, double span = 6000, JointFrameClassificationContext? frame = null,
        string source = "Beam inertia/span belong to this stiffness detail and bending plane")
        => JointStiffnessClassification.Classify(new JointStiffnessClassificationInput(s,
            new JointFrameMemberStiffness(inertia, span), frame ?? Frame(), source));
    private static double V(JointStiffnessClassificationResult r, string name) => r.Trace.Single(t => t.Name == name).Value;

    [TestMethod]
    public void JrcIndependentStiffnessAndFrameLimitsGiveSemiRigid()
    {
        var r = Classify(JrcStiffness());
        Assert.AreEqual(76552582907.97441, r.Input.Stiffness.InitialStiffness!.Value, .01);
        Assert.AreEqual(17.5e9, r.BeamStiffness);
        Assert.AreEqual(8.75e9, r.PinnedLimit);
        Assert.AreEqual(140e9, r.RigidLimit);
        Assert.AreEqual(JointStiffnessClass.SemiRigid, r.AppliedClass);
        Assert.IsTrue(r.IsClassificationComplete); Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [DataTestMethod]
    [DataRow(.5, .8, JointStiffnessClass.NominallyPinned)]
    [DataRow(.500001, .8, JointStiffnessClass.SemiRigid)]
    [DataRow(.499999, .8, JointStiffnessClass.NominallyPinned)]
    [DataRow(8.0, .8, JointStiffnessClass.Rigid)]
    [DataRow(7.999999, .8, JointStiffnessClass.SemiRigid)]
    [DataRow(8.000001, .8, JointStiffnessClass.Rigid)]
    [DataRow(25.0, .79999, JointStiffnessClass.Rigid)]
    [DataRow(24.999999, .79999, JointStiffnessClass.SemiRigid)]
    [DataRow(25.000001, .79999, JointStiffnessClass.Rigid)]
    public void InitialStiffnessBoundariesAreInclusive(double ratio, double reduction, JointStiffnessClass expected)
    {
        var s = JrcStiffness();
        // L=E makes EI/L numerically equal to I, without an arbitrary Sj input.
        var r = Classify(s, s.InitialStiffness!.Value / ratio, 210000, Frame(reduction));
        Assert.AreEqual(expected, r.AppliedClass, string.Join(";", r.Diagnostics));
    }

    [TestMethod]
    public void EightyPercentBracingBoundaryChangesRigidThreshold()
    {
        var s = JrcStiffness(); double ib = s.InitialStiffness!.Value / 10;
        var braced = Classify(s, ib, 210000, Frame(.8));
        var other = Classify(s, ib, 210000, Frame(.799999));
        Assert.AreEqual(JointStiffnessClass.Rigid, braced.AppliedClass);
        Assert.AreEqual(JointStiffnessClass.SemiRigid, other.AppliedClass);
        Assert.AreEqual(8, V(braced, "KbClassificationFactor"));
        Assert.AreEqual(25, V(other, "KbClassificationFactor"));
    }

    [DataTestMethod] [DataRow(.4)] [DataRow(30.0)]
    public void LowStoreyRatioFootnotePrecedesPinnedAndRigidZones(double stiffnessRatio)
    {
        var s = JrcStiffness(); var r = Classify(s, s.InitialStiffness!.Value / stiffnessRatio,
            210000, Frame(0, [Storey(.09), Storey(1)]));
        Assert.AreEqual(stiffnessRatio < 1 ? JointStiffnessClass.NominallyPinned : JointStiffnessClass.Rigid, r.NumericalZone);
        Assert.AreEqual(JointStiffnessClass.SemiRigid, r.AppliedClass);
        Assert.IsTrue(r.StoreyRatioOverridesNumericalZone);
        Assert.AreEqual(.09, r.MinimumStoreyRatio);
    }

    [TestMethod]
    public void OtherFrameRatioExactlyOneTenthIsAdmitted()
    {
        var s = JrcStiffness(); var r = Classify(s, s.InitialStiffness!.Value / 30,
            210000, Frame(0, [Storey(.1)]));
        Assert.AreEqual(JointStiffnessClass.Rigid, r.AppliedClass);
        Assert.IsFalse(r.StoreyRatioOverridesNumericalZone);
    }

    [TestMethod]
    public void BracedBranchDoesNotInheritOtherFrameFootnote()
    {
        var s = JrcStiffness(); var r = Classify(s, s.InitialStiffness!.Value / .4,
            210000, Frame(.8, [Storey(.01)], false));
        Assert.AreEqual(JointStiffnessClass.NominallyPinned, r.AppliedClass);
        Assert.IsFalse(r.StoreyRatioOverridesNumericalZone);
    }

    [TestMethod]
    public void StoreyUsesMeanOfIndividualIOverLAndNeverAveragesBetweenStoreys()
    {
        // Kb=mean(100/10,100/100)=5.5, Kc=100/10=10; ratio .55.
        var mixed = new JointFrameStoreyStiffness([new(100, 10), new(100, 100)], [new(100, 10)], "Complete storey 2");
        var r = Classify(JrcStiffness(), frame: Frame(0, [Storey(.05), mixed]));
        Assert.AreEqual(5.5, V(r, "Storey2.Kb")); Assert.AreEqual(10, V(r, "Storey2.Kc"));
        Assert.AreEqual(.55, V(r, "Storey2.KbOverKc"));
        Assert.AreEqual(.05, r.MinimumStoreyRatio); Assert.IsTrue(r.StoreyRatioOverridesNumericalZone);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void MissingCompleteFrameDataNeverDefaultsToRigid(bool empty)
    {
        var s = JrcStiffness(); var r = Classify(s, 1, 210000,
            Frame(0, empty ? [] : [Storey(.2)], !empty ? false : true));
        Assert.AreEqual(JointStiffnessClass.Rigid, r.NumericalZone);
        Assert.IsNull(r.AppliedClass); Assert.IsFalse(r.IsClassificationComplete);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), "EveryStoreyRequiredForOtherFrames");
    }

    [TestMethod]
    public void SecantStiffnessAndLoadRotationAreNotUsedAsInitialStiffness()
    {
        var low = Classify(JrcStiffness(0)); var high = Classify(JrcStiffness(240e6));
        Assert.IsTrue(high.Input.Stiffness.SecantStiffness < low.Input.Stiffness.SecantStiffness);
        Assert.AreEqual(low.Input.Stiffness.InitialStiffness, high.Input.Stiffness.InitialStiffness);
        Assert.AreEqual(low.AppliedClass, high.AppliedClass);
    }

    [TestMethod]
    public void UnsupportedComponentOrMissingDirectionEvidenceDoesNotProduceAppliedClass()
    {
        Assert.IsNull(Classify(JrcStiffness(supported: false)).AppliedClass);
        Assert.IsNull(Classify(JrcStiffness(), source: "").AppliedClass);
        Assert.IsNull(Classify(JrcStiffness(), frame: Frame(source: "")).AppliedClass);
    }

    [TestMethod]
    public void OverflowLeavesFiniteTraceAndNoClassification()
    {
        var r = Classify(JrcStiffness(), double.MaxValue, 1);
        Assert.IsNull(r.AppliedClass); Assert.IsNull(r.NumericalZone);
        Assert.IsTrue(r.Trace.All(t => double.IsFinite(t.Value)));
    }

    [TestMethod]
    public void GlobalContractsValidateNumbersAndSnapshotCollections()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new JointFrameMemberStiffness(double.NaN, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new JointFrameMemberStiffness(1, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Frame(1.01));
        var beams = new List<JointFrameMemberStiffness> { new(1, 1) };
        var storey = new JointFrameStoreyStiffness(beams, [new(1, 1)], "source"); beams.Clear();
        Assert.AreEqual(1, storey.BeamsAtTop.Count);
        var storeys = new List<JointFrameStoreyStiffness> { storey };
        var frame = Frame(0, storeys); storeys.Clear(); Assert.AreEqual(1, frame.Storeys.Count);
    }
}
