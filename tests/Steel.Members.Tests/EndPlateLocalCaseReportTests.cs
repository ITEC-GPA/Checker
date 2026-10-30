using System.Globalization;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.EndPlateInitialCaseTestData;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateLocalCaseReportTests
{
    [DataTestMethod] [DataRow(false, 1d)] [DataRow(true, 1d)] [DataRow(false, -1d)] [DataRow(true, -1d)]
    public void SingleSidedExtendedCaseHasReviewableEvidenceButNoCompleteJointVerdict(bool ntc, double sign)
    {
        var parent = Parent(ntc, shear: sign * 300000, beta: 1);
        var connection = Connection(parent.MomentComponents);
        var report = EndPlateLocalCaseReport.Create("ULS-1", EndPlateLocalCaseAssessment.Assess(parent, connection, Initial(parent)));
        Assert.AreEqual(ConnectionStatus.Passed, report.Assessment.CoveredComponentsStatus, string.Join(";", report.Assessment.Diagnostics));
        Assert.AreEqual(ColumnWebPanelConfiguration.SingleSidedJoint, parent.Input.PanelConfiguration);
        Assert.IsFalse(report.IsWholeJointComplete); Assert.AreEqual(ConnectionCapabilityLevel.ComponentsOnly, report.Capability.Level);
        Assert.IsTrue(report.PendingChecks.Count > 0); Assert.IsFalse(report.HasKnownFailure);
        CollectionAssert.AreEquivalent(new[] { "BeamSection", "BeamMoment", "Bolts", "FilletWelds", "InitialStiffness" }, report.Components.Select(x => x.Id).ToArray());
        Assert.AreSame(connection.Bolts.BoltAssessment.Checks[0], report.Components.Single(x => x.Id == "Bolts").Checks[0]);
        // Equilibrium uses the four real pairs: six tension-zone bolts at30kN and
        // two lower shear-only bolts at60kN. This is a prescribed analytical case,
        // not the published C1 (which has beta0 and a different load level).
        Assert.AreEqual(6 * 30000 + 2 * 60000, connection.Bolts.BoltAssessment.Input.Rows.Sum(r => r.LeftShear + r.RightShear), 1e-8);
        var output = report.ToText();
        StringAssert.Contains(output, "INPUT\tV2\t" + (sign * 300000).ToString(CultureInfo.InvariantCulture) + "\tN");
        StringAssert.Contains(output, "WHOLE_JOINT_COMPLETE\tFalse"); StringAssert.Contains(output, "PENDING\t");
        StringAssert.Contains(output, "Sj.initial"); StringAssert.Contains(output, "COMPONENT\tBeamSection\tPassed");
        if (ntc) StringAssert.Contains(output, "supplementary component model");
        string? dir = Environment.GetEnvironmentVariable("GPC_ENDPLATE_REPORT_AUDIT");
        if (dir != null && sign > 0)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, ntc ? "endplate-ntc-components.txt" : "endplate-ec3-components.txt"), report.ToText(true));
        }
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void KnownBoltFailureRemainsVisibleWhenAnotherRequirementIsMissing(bool missingStiffness)
    {
        var p = Parent(beta: 1); var details = Details();
        if (missingStiffness) details[0] = new(0, details[0].ColumnLength, details[0].EndPlateLength, 70.5, "");
        var c = Connection(p.MomentComponents, overloadedBolt: true, weldSource: missingStiffness ? "Actual" : "");
        var r = EndPlateLocalCaseReport.Create("Failure", EndPlateLocalCaseAssessment.Assess(p, c, Initial(p, details)));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Assessment.CoveredComponentsStatus);
        Assert.IsTrue(r.HasKnownFailure); Assert.IsTrue(r.Components.Single(x => x.Id == "Bolts").Checks.Any(x => !x.Passed));
        StringAssert.Contains(r.ToText(), "COMPONENT\tBolts\tFailed"); StringAssert.Contains(r.ToText(), "KNOWN_FAILURE\tTrue");
        Assert.IsFalse(r.IsWholeJointComplete);
    }

    [TestMethod]
    public void MismatchedCasesRetainTheirSeparateChildrenWithoutExportingCommonStiffness()
    {
        var a = Parent(beta: 1); var b = Parent(beta: 1, moment: 120e6);
        var r = EndPlateLocalCaseReport.Create("Mismatch", EndPlateLocalCaseAssessment.Assess(a, Connection(b.MomentComponents), Initial(a)));
        Assert.IsFalse(r.Assessment.HasCoherentCase); Assert.AreEqual(ConnectionStatus.NotSupported, r.Assessment.CoveredComponentsStatus);
        Assert.IsTrue(r.Components.Any(x => x.Id == "ConnectionMoment")); Assert.IsTrue(r.Components.Any(x => x.Id == "BeamMoment"));
        Assert.IsFalse(r.ToText().Contains("INPUT\tSj.initial\t"));
        StringAssert.Contains(r.ToText(), "DIAGNOSTIC\tSAME_BEAM_MOMENT_CONNECTION_AND_STIFFNESS_PARENT_REQUIRED");
    }

    [TestMethod]
    public void FailedMomentAndMissingDerivedMomentDoNotEraseOriginalEvidence()
    {
        var p = Parent(moment: 700e6, beta: 1);
        var r = EndPlateLocalCaseReport.Create("Overload", EndPlateLocalCaseAssessment.Assess(p, Connection(p.MomentComponents), Initial(p)));
        Assert.IsTrue(r.HasKnownFailure); Assert.IsTrue(r.Components.Any(x => x.Checks.Any(c => !c.Passed)));
        var mismatch = Parent(mismatchedJointMoment: 101e6); var valid = Parent();
        var detached = EndPlateLocalCaseReport.Create("Unbound", EndPlateLocalCaseAssessment.Assess(mismatch, Connection(valid.MomentComponents), Initial(mismatch)));
        Assert.IsFalse(detached.Assessment.HasCoherentCase); Assert.IsFalse(detached.IsWholeJointComplete);
        Assert.IsFalse(detached.Components.Any(x => x.Id == "BeamMoment")); Assert.IsTrue(detached.Components.Any(x => x.Id == "ConnectionMoment"));
    }

    [TestMethod]
    public void ReportIsInvariantAndEscapesCaseLabelsWithoutAddingFalseStatusLines()
    {
        var p = Parent(beta: 1); var r = EndPlateLocalCaseReport.Create("id\nWHOLE_JOINT_COMPLETE\tTrue", EndPlateLocalCaseAssessment.Assess(p, Connection(p.MomentComponents), Initial(p)));
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT"); string italian = r.ToText(true);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US"); Assert.AreEqual(italian, r.ToText(true));
            Assert.AreEqual(1, italian.Split('\n').Count(x => x.StartsWith("WHOLE_JOINT_COMPLETE\t")));
            StringAssert.Contains(italian, "CASE\tid\\nWHOLE_JOINT_COMPLETE\\tTrue");
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [TestMethod]
    public void CatalogDoesNotUpgradeComponentAggregatorsOrTreatMissingTypologiesAsPassing()
    {
        Assert.AreEqual(ConnectionCapabilityCatalog.All.Count, ConnectionCapabilityCatalog.All.Select(x => x.Id).Distinct().Count());
        foreach (string id in new[] { "MomentEndPlate", "GenericSpliceComponents", "PackedPairConnections", "DoubleAngleCleat", "BraceConnections" })
        {
            var capability = ConnectionCapabilityCatalog.Get(id);
            Assert.AreEqual(ConnectionCapabilityLevel.ComponentsOnly, capability.Level); Assert.IsTrue(capability.MissingMechanisms.Count > 0);
        }
        foreach (string id in new[] { "TubularJoints" })
        {
            var capability = ConnectionCapabilityCatalog.Get(id);
            Assert.AreEqual(ConnectionCapabilityLevel.NotImplemented, capability.Level); Assert.AreEqual(0, capability.EntryPoints.Count);
        }
        Assert.ThrowsException<ArgumentException>(() => ConnectionCapabilityCatalog.Get("NoSuchMethod"));
        Assert.ThrowsException<ArgumentNullException>(() => ConnectionCapabilityCatalog.Get(null!));
        var list = (IList<ConnectionCapability>)ConnectionCapabilityCatalog.All;
        Assert.ThrowsException<NotSupportedException>(() => list.Clear());
    }
}
