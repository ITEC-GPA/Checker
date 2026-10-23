using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.IBeamSplicePlateTests;

namespace Steel.Members.Tests;

[TestClass]
public class IBeamSpliceWebLongitudinalIntegrationTests
{
    private const string Prefix = "WebLongitudinal.";
    private const string CheckId = "Web.Longitudinal.InterFastenerBuckling";
    private static IBeamSplicePlateInput Low(bool ntc = false)
        => Input(ntc, forces: IBeamSpliceForceTests.Input(actions: new(-75000, -75000, 0, 0, 0, -100e6)));

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void RequiredLongitudinalComponentUsesTheSameImmutableUlsInput(bool ntc)
    {
        var p = Low(ntc); var r = IBeamSpliceComponents.Verify(new(p));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.WebLongitudinal.Status);
        Assert.AreSame(p, r.WebLongitudinal.Input);
        Assert.AreSame(r.Bearing.Input, r.WebLongitudinal.Input);
        Equal(134.6777569232027 / 2, Check(r.WebLongitudinal, CheckId).Demand);
        Equal(256.0578476773837 / (ntc ? 1.05 : 1), Check(r.WebLongitudinal, CheckId).Resistance);
    }

    [TestMethod]
    public void EveryLongitudinalCheckPreservesNumbersUnitsAndSourceUnderADistinctPrefix()
    {
        var r = IBeamSpliceComponents.Verify(new(Input()));
        foreach (var component in r.WebLongitudinal.Checks)
        {
            var aggregate = r.ComponentAssessment.Checks.Single(c => c.Id == Prefix + component.Id);
            Equal(component.Demand, aggregate.Demand); Equal(component.Resistance, aggregate.Resistance);
            Assert.AreEqual(component.Unit, aggregate.Unit); Assert.AreEqual(component.Reference, aggregate.Reference);
        }
        Assert.AreEqual(2, r.ComponentAssessment.Checks.Count(c => c.Id.StartsWith(Prefix)));
        Assert.AreEqual(r.ComponentAssessment.Checks.Count, r.ComponentAssessment.Checks.Select(c => c.Id).Distinct().Count());
    }

    [TestMethod]
    public void ActualCriticalCutAndItsConcomitantActionsRemainAvailableInAggregateTrace()
    {
        var r = IBeamSpliceComponents.Verify(new(Input()));
        foreach (var component in r.WebLongitudinal.Trace)
        {
            var aggregate = r.ComponentAssessment.Trace.Single(t => t.Name == Prefix + component.Name);
            Equal(component.Value, aggregate.Value); Assert.AreEqual(component.Unit, aggregate.Unit);
        }
        double Value(string name) => r.ComponentAssessment.Trace.Single(t => t.Name == Prefix + "Web.Longitudinal.MaximumCompression." + name).Value;
        Equal(-70, Value("X")); Equal(1, Value("ColumnLimit"));
        Equal(-31912.280701754386, Value("AxialForce")); Equal(-75000, Value("ShearForce"));
        Equal(-24139551.92743764, Value("Moment")); Equal(134.6777569232027, Value("Stress"));
    }

    [TestMethod]
    public void UnsupportedCompressedOutstandBlocksOnlyTheNewModeButSuppressesTheOverallEnvelope()
    {
        var p = Input(forces: IBeamSpliceForceTests.Input(web: new(2, 2, 85, 60, 70),
            actions: new(-20000, 0, 0, 0, 0, 0)), exposed: false);
        var r = IBeamSpliceComponents.Verify(new(p));
        Assert.AreEqual(ConnectionStatus.Passed, r.BoltStrength.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.Bearing.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.Flanges.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.WebSections.Status);
        Unsupported(r.WebLongitudinal, "OUTSTAND_OUTSIDE_CLASS3");
        Assert.AreEqual(ConnectionStatus.NotSupported, r.ComponentAssessment.Status);
        Assert.AreEqual(0, r.ComponentAssessment.Checks.Count); Assert.IsNull(r.ComponentAssessment.Utilization);
        Assert.IsTrue(r.ComponentAssessment.Diagnostics.Single().StartsWith(Prefix.TrimEnd('.') + ":WEB_COMPRESSED_OUTSTAND"));
        Assert.IsTrue(r.WebLongitudinal.Trace.Any(t => t.Name.EndsWith("MaximumCompression.Stress")));
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void FailedLongitudinalModeCannotBeOmittedFromAnOtherwisePassingComponentEnvelope()
    {
        // Deliberately large explicit gammaM1 isolates propagation of THIS
        // failure mode; it is a contract test, not a recommended design factor.
        // Both physical flanges remain in tension for this concomitant N-M pair.
        var p = Input(forces: IBeamSpliceForceTests.Input(actions: new(700000, 0, 0, 0, 0, 100e6)), gammaM1: 100);
        var r = IBeamSpliceComponents.Verify(new(p));
        Assert.AreEqual(ConnectionStatus.Passed, r.BoltStrength.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.Bearing.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.Flanges.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.WebSections.Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.WebLongitudinal.Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.ComponentAssessment.Status);
        Assert.AreEqual(Prefix + CheckId, r.ComponentAssessment.GoverningCheck.Id);
        Equal(r.WebLongitudinal.Utilization!.Value, r.ComponentAssessment.Utilization!.Value);
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void PassingLongitudinalStripsDoNotCloseTransverseShearOrCombinedBlockMechanisms()
    {
        var r = IBeamSpliceComponents.Verify(new(Low()));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("WEB_COVER_LOCAL_BUCKLING"));
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("WEB_COVER_BLOCK_TEARING_UNDER_N_V_M"));
        StringAssert.Contains(string.Join(";", r.ComponentAssessment.Assumptions), "transverse/shear and lateral modes");
        StringAssert.Contains(r.WebLongitudinal.Scope, "No complete N-V-M plate stability");
    }
}
