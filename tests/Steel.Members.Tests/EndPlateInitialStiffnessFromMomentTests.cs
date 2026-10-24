using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.EndPlateInitialCaseTestData;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateInitialStiffnessFromMomentTests
{
    private static void Close(double e, double a) => Assert.AreEqual(e, a, Math.Max(1, Math.Abs(e)) * 1e-10);
    private static void Unsupported(EndPlateInitialStiffnessFromMomentResult r, string issue)
    {
        Assert.IsFalse(r.IsSupported); Assert.IsNull(r.InitialStiffness); Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(issue)), string.Join(";", r.Diagnostics));
    }

    [DataTestMethod]
    [DataRow(false, false, 0.0, 237302576169.69534)] [DataRow(true, false, 0.0, 237302576169.69534)]
    [DataRow(false, false, 1.0, 90338344812.44681)] [DataRow(true, false, 1.0, 90338344812.44681)]
    [DataRow(false, true, 0.0, 152350333980.8783)] [DataRow(true, true, 0.0, 152350333980.8783)]
    [DataRow(false, true, 1.0, 69580519870.4866)] [DataRow(true, true, 1.0, 69580519870.4866)]
    public void SciGeometryWithExplicitDetailMatchesIndependentInitialStiffness(bool ntc, bool cjp, double beta, double expected)
    {
        var parent = Parent(ntc, cjp, beta: beta); var r = Initial(parent);
        Assert.AreEqual(ConnectionStatus.Passed, parent.Status, string.Join(";", parent.Diagnostics));
        Assert.IsTrue(r.IsSupported, string.Join(";", r.Diagnostics)); Close(expected, r.InitialStiffness!.Value);
        Assert.AreSame(parent, r.Parent); Assert.AreSame(parent.MomentComponents, r.MomentComponents);
        var p = r.InitialModel.Input; Assert.AreSame(parent.Input.Column, p.Column); Assert.AreSame(parent.Input.Policy.Code, p.Code);
        Close(247.6, p.CompressionWidth.Width); Assert.AreEqual(beta, p.Beta);
        Assert.AreEqual(cjp ? 2 : 3, p.Rows.Count);
        for (int i = 0; i < p.Rows.Count; i++)
        {
            Assert.AreEqual(parent.MomentComponents.EffectiveRows[i].LeverArm, p.Rows[i].LeverArm);
            Close(33.44, p.Rows[i].ColumnFlangeM); Close(!cjp && i == 0 ? 30.4 : 38.55, p.Rows[i].EndPlateM);
            Assert.AreEqual(25, p.Rows[i].EndPlateThickness); Assert.AreEqual(353, p.Rows[i].BoltStressArea);
            Assert.AreEqual(70.5, p.Rows[i].BoltElongationLength);
        }
        Assert.IsTrue(r.PendingChecks.Any(s => s.Contains("EACH row")));
        Assert.IsTrue(r.PendingChecks.Any(s => s.Contains("not the complete joint")));
    }

    [DataTestMethod] [DataRow(0.0, 0.0)] [DataRow(100000000.0, 300000.0)] [DataRow(150000000.0, -300000.0)]
    public void RealActionsArePreservedWithoutArtificialZeroOrMomentCapacity(double moment, double shear)
    {
        var parent = Parent(moment: moment, shear: shear); var r = Initial(parent);
        Assert.IsTrue(r.IsSupported, string.Join(";", r.Diagnostics)); Close(237302576169.69534, r.InitialStiffness!.Value);
        Assert.AreEqual(moment, r.Trace.Single(v => v.Name == "Actions.M1").Value);
        Assert.AreEqual(shear, r.Trace.Single(v => v.Name == "Actions.V2").Value);
        Assert.AreSame(parent.BeamSection.Input.Actions, r.Parent.BeamSection.CoreInput.Actions);
        Assert.IsFalse(r.InitialModel.Trace.Any(v => v.Name == "JointMomentResistance"));
    }

    [DataTestMethod] [DataRow(200000.0)] [DataRow(209999.0)] [DataRow(210001.0)]
    public void MatchingGeometryAndStrengthsDoNotOverrideActualModelElasticModulus(double modulus)
    {
        var parent = Parent(elasticModulus: modulus);
        Assert.AreEqual(ConnectionStatus.Passed, parent.Status);
        var r = Initial(parent); Unsupported(r, "ModelBeamElasticModulusMustEqual210000MPa");
        Assert.AreEqual(modulus, r.Trace.Single(v => v.Name == "ModelBeamElasticModulus").Value);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
    }

    [TestMethod]
    public void EveryPhysicalRowRemainsEvenWhenPlasticCapacityAllocationExhaustsItsResistance()
    {
        var parent = Parent(beta: 1, moment: 1e6); var r = Initial(parent);
        Assert.AreEqual(ConnectionStatus.Passed, parent.Status, string.Join(";", parent.Diagnostics));
        Assert.IsTrue(parent.MomentComponents.EffectiveRows.Any(v => v.Resistance == 0), "Fixture must exercise an exhausted plastic row.");
        Assert.IsTrue(r.IsSupported, string.Join(";", r.Diagnostics)); Assert.AreEqual(3, r.InitialModel.Input.Rows.Count);
        Assert.IsTrue(r.InitialModel.Trace.Single(v => v.Name == "Row3.Keff").Value > 0);
    }

    [DataTestMethod] [DataRow("missing")] [DataRow("duplicate")] [DataRow("reversed")] [DataRow("extra")]
    public void LayoutMustMapAllParentRowsExactlyOnce(string kind)
    {
        var rows = Details().ToList();
        if (kind == "missing") rows.RemoveAt(2);
        if (kind == "duplicate") rows[1] = rows[0];
        if (kind == "reversed") rows.Reverse();
        if (kind == "extra") rows.Add(new(3, Length(80), Length(80), 70.5, "extra"));
        var r = Initial(Parent(), rows); Assert.IsFalse(r.IsSupported); Assert.IsNull(r.InitialModel);
        Assert.IsTrue(r.Diagnostics.Any(v => v.Contains("EveryPhysicalMomentRow") || v.Contains("ExactlyOnce")));
    }

    [DataTestMethod] [DataRow("hardware")] [DataRow("column")] [DataRow("plate")]
    public void ExternalHardwareAndRowGroupEvidenceRemainRequired(string field)
    {
        var rows = Details(); var old = rows[0];
        rows[0] = new(0, field == "column" ? new(new[] { 166.505 }, "") : old.ColumnLength,
            field == "plate" ? new(new[] { 125.0 }, "") : old.EndPlateLength, old.BoltElongationLength,
            field == "hardware" ? "" : old.HardwareSource);
        var r = Initial(Parent(), rows);
        Unsupported(r, field == "hardware" ? "HardwareElongationSourceRequired" : "RowAndGroupLengthsRequired");
    }

    [DataTestMethod] [DataRow(45.5)] [DataRow(45.0)]
    public void ElongationLengthMustContainActualPlatesAndAdditionalHardware(double length)
        => Unsupported(Initial(Parent(), Details(boltLength: length)), "BoltLengthMustIncludePlatesAndAdditionalHardware");

    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void GroupTotalsCannotExceedKnownSingleRowMechanism(bool column)
    {
        // EN Table6.11: the minimum of individual/group ROW lengths cannot exceed
        // min(cp,nc) of the individual row. The adapter never divides a group total.
        var rows = Details(); var old = rows[0];
        rows[0] = new(0, column ? Length(423.01) : old.ColumnLength,
            column ? old.EndPlateLength : Length(250), old.BoltElongationLength, old.HardwareSource);
        Unsupported(Initial(Parent(), rows), column ? "ColumnLengthExceedsIndividualMechanism" : "PlateLengthExceedsIndividualMechanism");
    }

    [TestMethod]
    public void QualifiedGroupContributionIsNotReplacedByAverageGroupLength()
    {
        var r = Initial(Parent()); Assert.IsTrue(r.IsSupported);
        Assert.AreEqual(95, r.InitialModel.Input.Rows[1].ColumnLength.Minimum);
        Assert.AreEqual(4, r.InitialModel.Input.Rows[1].ColumnLength.RelevantRowLengths.Count);
        Assert.AreNotEqual((166.505 + 95 + 161.505) / 3, r.InitialModel.Input.Rows[1].ColumnLength.Minimum);
    }

    [DataTestMethod] [DataRow(500000000.0)] [DataRow(700000000.0)]
    public void ComponentOrBeamFailureIsRetainedAndCannotExportInitialStiffness(double moment)
    {
        var parent = Parent(moment: moment); var r = Initial(parent);
        Assert.AreEqual(ConnectionStatus.Failed, parent.Status); Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Unsupported(r, "PassedSameCaseBeamAndMomentAssessmentRequired"); Assert.AreSame(parent, r.Parent);
    }

    [TestMethod]
    public void UnsupportedParentAndActionMismatchDoNotAcquireAStiffnessBinding()
    {
        Unsupported(Initial(Parent(actionSource: "")), "PassedSameCaseBeamAndMomentAssessmentRequired");
        var p = Parent(mismatchedJointMoment: 101e6); Assert.IsNull(p.MomentComponents);
        Unsupported(Initial(p), "SameConcomitantActionsRequired");
    }

    [TestMethod]
    public void InputRowsAreOwnedAndPublicResultHasNoSecantOrRotation()
    {
        var parent = Parent(); var rows = Details(); var input = new EndPlateInitialStiffnessFromMomentInput(parent, rows);
        rows[0] = rows[1]; Assert.AreEqual(0, input.Rows[0].MomentRowIndex);
        foreach (string name in new[] { "SecantStiffness", "ModelRotation", "JointMomentResistance" })
            Assert.IsNull(typeof(EndPlateInitialStiffnessFromMomentResult).GetProperty(name));
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateInitialStiffnessFromMoment.Calculate(null!));
    }
}
