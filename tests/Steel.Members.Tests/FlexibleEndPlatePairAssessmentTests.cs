using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class FlexibleEndPlatePairAssessmentTests
{
    private static ConnectionRolledISection Section(double h = 466, double b = 300,
        double tw = 13.1, double tf = 20, double radius = 13, double fy = 265)
        => new(h, b, tw, tf, radius, 2 * b * tf + (h - 2 * tf) * tw + (4 - Math.PI) * radius * radius, fy, 410);
    private static FlexibleEndPlatePairInput Pair(double firstForce = 340000, double secondForce = 340000,
        bool firstNotched = false, bool secondNotched = false, double firstThroat = 5.6,
        double secondThroat = 5.6, bool thinSupport = false)
    {
        var code = ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "SCI P358 explicit benchmark factors");
        var bolt = new ConnectionBolt(20, 22, 245, ConnectionBoltGrade.Class8_8, true);
        var support = thinSupport ? Section(256, 200, 6.5, 15, 13) : Section();
        double gauge = thinSupport ? 90 : 140;
        FlexibleEndPlateInput Side(double force, double throat, bool notched)
            => new(code, new ConnectionPlate(gauge + 60, 10, 275, 410), bolt,
                Section(412.8, 179.5, 9.5, 16, 10.2, 275), support,
                4, gauge, 90, 357, throat, FilletWeldEndCondition.FullSize, "Matching consumable",
                new PlanarConnectionActions(0, -force, 0, 0, 0, 0), new PlanarConnectionActions(0, 0, 0, 0, 0, 0),
                true, "Pinned frame-model evidence", beamNotched: notched);
        return new FlexibleEndPlatePairInput(Side(firstForce, firstThroat, firstNotched), Side(secondForce, secondThroat, secondNotched));
    }
    private static void NoAggregateRatio(FlexibleEndPlatePairAssessmentResult r)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.IsFalse(r.IsComplete);
        Assert.AreEqual(0, r.Checks.Count); Assert.IsNull(r.GoverningCheck); Assert.IsNull(r.Utilization);
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void FailedChildSurvivesTheOtherUnsupportedChildInEitherOrder(bool reverse)
    {
        var p = reverse ? Pair(firstNotched: true, secondForce: 450000)
            : Pair(firstForce: 450000, secondNotched: true);
        var r = FlexibleEndPlate.VerifyPairAssessment(p);
        NoAggregateRatio(r); Assert.IsTrue(r.HasFailedComponent);
        Assert.AreSame(p, r.Input); Assert.AreSame(p.First, r.First.Input); Assert.AreSame(p.Second, r.Second.Input);
        var failed = reverse ? r.Second : r.First; var unsupported = reverse ? r.First : r.Second;
        Assert.AreEqual(ConnectionStatus.Failed, failed.Status); Assert.AreEqual(ConnectionStatus.NotSupported, unsupported.Status);
        var check = failed.Checks.Single(c => c.Id == "Beam.WebShear");
        Assert.AreEqual(450000, check.Demand); Assert.AreEqual(393673.4979253112, check.Resistance, 1e-6);
        Assert.AreEqual(1.143079232845324, check.Utilization, 1e-12); Assert.IsFalse(check.Passed);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), (reverse ? "First:" : "Second:") + "NOTCHED_BEAM_NOT_QUALIFIED");
    }

    [TestMethod]
    public void ZeroLoadDetailFailureIsRetainedBesideAnUnsupportedChild()
    {
        var r = FlexibleEndPlate.VerifyPairAssessment(Pair(firstForce: 0, firstThroat: 3, secondNotched: true));
        NoAggregateRatio(r); Assert.IsTrue(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.Failed, r.First.Status); Assert.AreEqual(0, r.First.LoadUtilization!.Value);
        Assert.IsFalse(r.First.Checks.Single(c => c.Id == "Weld.FullStrengthThroat").Passed);
    }

    [TestMethod]
    public void UnsupportedChildDoesNotInventFailureInAPassingChild()
    {
        var r = FlexibleEndPlate.VerifyPairAssessment(Pair(secondNotched: true));
        NoAggregateRatio(r); Assert.IsFalse(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.Passed, r.First.Status); Assert.IsTrue(r.First.Checks.Count > 0);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Second.Status);
    }

    [TestMethod]
    public void BothUnsupportedChildrenHaveNoKnownFailureAndNoAggregateRatio()
    {
        var r = FlexibleEndPlate.VerifyPairAssessment(Pair(firstNotched: true, secondNotched: true));
        NoAggregateRatio(r); Assert.IsFalse(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.First.Status); Assert.AreEqual(ConnectionStatus.NotSupported, r.Second.Status);
    }

    [TestMethod]
    public void SharedSupportFailureIsRecognizedWithTwoIndividuallyPassingChildren()
    {
        var r = FlexibleEndPlate.VerifyPairAssessment(Pair(360000, 360000, thinSupport: true));
        Assert.AreEqual(ConnectionStatus.Passed, r.First.Status); Assert.AreEqual(ConnectionStatus.Passed, r.Second.Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.Status); Assert.IsTrue(r.HasFailedComponent); Assert.IsTrue(r.IsComplete);
        Assert.IsTrue(r.Checks.Any(c => c.Id.StartsWith("Shared.") && !c.Passed)); Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void BothKnownFailuresRemainAvailableWhenEnvelopeIsApplicable()
    {
        var r = FlexibleEndPlate.VerifyPairAssessment(Pair(450000, 450000));
        Assert.AreEqual(ConnectionStatus.Failed, r.First.Status); Assert.AreEqual(ConnectionStatus.Failed, r.Second.Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.Status); Assert.IsTrue(r.HasFailedComponent);
        Assert.IsTrue(r.Checks.Any(c => c.Id.StartsWith("First.") && !c.Passed));
        Assert.IsTrue(r.Checks.Any(c => c.Id.StartsWith("Second.") && !c.Passed));
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void LegacySummaryKeepsItsSignatureStatusChecksAndRatios(int scenario)
    {
        var p = scenario == 1 ? Pair(firstForce: 450000)
            : scenario == 2 ? Pair(firstForce: 450000, secondNotched: true)
            : scenario == 3 ? Pair(360000, 360000, thinSupport: true) : Pair();
        // Explicit legacy return type is part of the compatibility regression.
        ConnectionVerification<FlexibleEndPlatePairInput> legacy = FlexibleEndPlate.VerifyPair(p);
        var detailed = FlexibleEndPlate.VerifyPairAssessment(p);
        Assert.AreEqual(legacy.Status, detailed.Status); Assert.AreEqual(legacy.IsComplete, detailed.IsComplete);
        Assert.AreEqual(legacy.Utilization, detailed.Utilization); Assert.AreEqual(legacy.Scope, detailed.Scope);
        CollectionAssert.AreEqual(legacy.Diagnostics.ToArray(), detailed.Diagnostics.ToArray());
        CollectionAssert.AreEqual(legacy.Checks.Select(c => (c.Id, c.Demand, c.Resistance, c.Unit, c.Reference)).ToArray(),
            detailed.Checks.Select(c => (c.Id, c.Demand, c.Resistance, c.Unit, c.Reference)).ToArray());
        CollectionAssert.AreEqual(legacy.Trace.Select(t => (t.Name, t.Value, t.Unit)).ToArray(),
            detailed.Trace.Select(t => (t.Name, t.Value, t.Unit)).ToArray());
        Assert.IsFalse(detailed.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void PassingDetailedResultRetainsExistingSharedSupportChecks()
    {
        var r = FlexibleEndPlate.VerifyPairAssessment(Pair());
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.IsFalse(r.HasFailedComponent);
        Assert.AreEqual(4, r.Checks.Count(c => c.Id.StartsWith("Shared.Prefix") && c.Id.EndsWith(".GrossShear")));
        Assert.AreEqual(340000, r.Checks.Single(c => c.Id == "Shared.Prefix4.GrossShear").Demand);
        Assert.AreEqual(85000, r.Checks.Single(c => c.Id == "Shared.Row4.SupportBearing").Demand);
    }

    [TestMethod]
    public void ChildAndAggregateCheckCollectionsRemainImmutable()
    {
        var r = FlexibleEndPlate.VerifyPairAssessment(Pair(firstForce: 450000, secondNotched: true));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionCheck>)r.First.Checks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionCheck>)r.Checks).Clear());
        Assert.IsTrue(r.HasFailedComponent); Assert.AreEqual(ConnectionStatus.Failed, r.First.Status);
    }

    [TestMethod]
    public void BothEntryPointsRejectNullInput()
    {
        Assert.ThrowsException<ArgumentNullException>(() => FlexibleEndPlate.VerifyPair(null!));
        Assert.ThrowsException<ArgumentNullException>(() => FlexibleEndPlate.VerifyPairAssessment(null!));
    }
}
