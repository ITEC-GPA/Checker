using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.EndPlateInitialCaseTestData;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateLocalCaseAssessmentTests
{
    private static void Unsupported(EndPlateLocalCaseAssessmentResult r)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus);
        Assert.AreEqual(0, r.Checks.Count); Assert.IsNull(r.GoverningCoveredCheck); Assert.IsNull(r.CoveredComponentsUtilization);
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsFalse(r.CanQualifyWholeMember);
    }

    [DataTestMethod] [DataRow(false, false)] [DataRow(true, false)] [DataRow(false, true)] [DataRow(true, true)]
    public void ActualSectionComponentsAndInitialStiffnessShareOneImmutableCase(bool ntc, bool cjp)
    {
        var parent = Parent(ntc, cjp); var connection = Connection(parent.MomentComponents); var initial = Initial(parent);
        var r = EndPlateLocalCaseAssessment.Assess(parent, connection, initial);
        Assert.AreEqual(ConnectionStatus.Passed, r.CoveredComponentsStatus, string.Join(";", r.Diagnostics));
        Assert.IsTrue(r.HasCoherentCase); Assert.IsFalse(r.HasFailedComponent); Assert.IsFalse(r.HasUnsupportedComponent);
        Assert.AreSame(parent, r.BeamAndMoment); Assert.AreSame(connection, r.Connection); Assert.AreSame(initial, r.InitialStiffnessAssessment);
        Assert.AreSame(parent.BeamSection.Input.Actions, r.Actions);
        Assert.AreEqual(parent.BeamSection.Checks.Count + connection.Checks.Count, r.Checks.Count);
        Assert.IsNotNull(r.CoveredComponentsUtilization); Assert.IsTrue(r.CoveredComponentsUtilization <= 1);
        Assert.AreEqual(initial.InitialStiffness, r.InitialStiffness);
        Assert.IsTrue(r.PendingWholeJointChecks.Any(v => v.Contains("net/block") || v.Contains("net and block")));
        Assert.IsTrue(r.PendingWholeJointChecks.Any(v => v.Contains("not member stability")));
        Assert.IsTrue(r.PendingWholeJointChecks.Any(v => v.Contains("secant")));
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsFalse(r.CanQualifyWholeMember);
    }

    [DataTestMethod] [DataRow(1.0)] [DataRow(-1.0)]
    public void SignedBeamShearIsRetainedWhileConnectionsUseItsMagnitude(double sign)
    {
        var parent = Parent(shear: sign * 300000); var r = EndPlateLocalCaseAssessment.Assess(parent,
            Connection(parent.MomentComponents), Initial(parent));
        Assert.AreEqual(ConnectionStatus.Passed, r.CoveredComponentsStatus);
        Assert.AreEqual(sign * 300000, r.Actions.V2); Assert.AreEqual(300000, r.Connection.MomentComponents.ComponentAssessment.Input.Shear);
        Assert.AreEqual(100e6, r.Actions.M1); Assert.AreEqual(0, r.Actions.N); Assert.AreEqual(0, r.Actions.M2);
        Assert.AreEqual(0, r.Actions.V1); Assert.AreEqual(0, r.Actions.T);
    }

    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void FailedBoltOrWeldRemainsVisibleEvenWithPassingInitialStiffness(bool bolt)
    {
        var parent = Parent(); var initial = Initial(parent);
        var connection = Connection(parent.MomentComponents, overloadedBolt: bolt, compressionFull: !bolt);
        var r = EndPlateLocalCaseAssessment.Assess(parent, connection, initial);
        Assert.IsTrue(initial.IsSupported); Assert.IsTrue(r.HasCoherentCase); Assert.IsTrue(r.HasFailedComponent);
        Assert.IsFalse(r.HasUnsupportedComponent); Assert.AreEqual(ConnectionStatus.Failed, r.CoveredComponentsStatus);
        Assert.IsTrue(r.CoveredComponentsUtilization > 1); Assert.IsFalse(r.GoverningCoveredCheck.Passed);
    }

    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void MissingEvidencePreservesOtherFailureAndProvidesNoIncompleteRatio(bool missingStiffness)
    {
        var parent = Parent(); var rows = Details();
        if (missingStiffness) rows[0] = new(0, rows[0].ColumnLength, rows[0].EndPlateLength, 70.5, "");
        var connection = Connection(parent.MomentComponents, overloadedBolt: true, weldSource: missingStiffness ? "Actual materials" : "");
        var r = EndPlateLocalCaseAssessment.Assess(parent, connection, Initial(parent, rows));
        Unsupported(r); Assert.IsTrue(r.HasFailedComponent); Assert.IsTrue(r.HasUnsupportedComponent); Assert.IsTrue(r.HasCoherentCase);
        Assert.AreEqual(ConnectionStatus.Failed, r.Connection.Bolts.BoltAssessment.Status);
        Assert.IsTrue(r.Connection.Bolts.BoltAssessment.Checks.Any(c => !c.Passed));
    }

    [DataTestMethod] [DataRow(500000000.0)] [DataRow(700000000.0)]
    public void FailedParentSurvivesUnsupportedDependentChildren(double moment)
    {
        var parent = Parent(moment: moment); var r = EndPlateLocalCaseAssessment.Assess(parent,
            Connection(parent.MomentComponents), Initial(parent));
        Unsupported(r); Assert.IsTrue(r.HasFailedComponent); Assert.IsTrue(r.HasUnsupportedComponent);
        Assert.AreEqual(ConnectionStatus.Failed, r.BeamAndMoment.Status);
        Assert.IsTrue(r.BeamAndMoment.MomentComponents.ComponentAssessment.Checks.Any(c => !c.Passed));
        Assert.IsNull(r.InitialStiffness);
    }

    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void EqualNumericResultsAndSourceLabelsDoNotReplaceIdentity(bool mismatchConnection)
    {
        var a = Parent(); var b = Parent();
        var r = EndPlateLocalCaseAssessment.Assess(a, Connection((mismatchConnection ? b : a).MomentComponents),
            Initial(mismatchConnection ? a : b));
        Unsupported(r); Assert.IsFalse(r.HasCoherentCase); Assert.IsFalse(r.HasFailedComponent);
        Assert.IsNull(r.InitialStiffness); Assert.IsTrue(r.Diagnostics.Contains("SAME_BEAM_MOMENT_CONNECTION_AND_STIFFNESS_PARENT_REQUIRED"));
    }

    [TestMethod]
    public void UnsupportedMomentOrBeamBindingRemainsAccessibleWithoutNullDereference()
    {
        var parent = Parent(actionSource: ""); var r = EndPlateLocalCaseAssessment.Assess(parent,
            Connection(parent.MomentComponents), Initial(parent));
        Unsupported(r); Assert.IsTrue(r.HasUnsupportedComponent); Assert.AreSame(parent, r.BeamAndMoment);
        var mismatch = Parent(mismatchedJointMoment: 101e6);
        var valid = Parent(); var detached = EndPlateLocalCaseAssessment.Assess(mismatch,
            Connection(valid.MomentComponents), Initial(mismatch));
        Unsupported(detached); Assert.IsFalse(detached.HasCoherentCase); Assert.IsNull(mismatch.MomentComponents);
    }

    [TestMethod]
    public void InitialStiffnessFailureDoesNotErasePassingStrengthDetails()
    {
        var parent = Parent(elasticModulus: 200000); var connection = Connection(parent.MomentComponents);
        Assert.AreEqual(ConnectionStatus.Passed, connection.CoveredComponentsStatus);
        var r = EndPlateLocalCaseAssessment.Assess(parent, connection, Initial(parent));
        Unsupported(r); Assert.IsFalse(r.HasFailedComponent); Assert.IsTrue(r.HasUnsupportedComponent);
        Assert.AreSame(connection, r.Connection); Assert.IsTrue(r.Connection.Checks.Count > 0);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains("ElasticModulusMustEqual210000")));
    }

    [TestMethod]
    public void ApiRejectsNullInputsAndDoesNotExposeWholeJointMomentOrRotation()
    {
        var p = Parent(); var c = Connection(p.MomentComponents); var i = Initial(p);
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateLocalCaseAssessment.Assess(null!, c, i));
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateLocalCaseAssessment.Assess(p, null!, i));
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateLocalCaseAssessment.Assess(p, c, null!));
        foreach (string name in new[] { "JointMomentResistance", "SecantStiffness", "ModelRotation" })
            Assert.IsNull(typeof(EndPlateLocalCaseAssessmentResult).GetProperty(name));
    }
}
