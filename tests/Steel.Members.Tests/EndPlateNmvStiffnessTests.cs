using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateNmvStiffnessTests
{
    [DataTestMethod][DataRow(false,false)][DataRow(false,true)][DataRow(true,false)][DataRow(true,true)]
    public void LocalTangentDoesNotSilentlyIncludeTheColumnPanel(bool ntc, bool flush)
    {
        var g = EndPlateNmvCaseData.Geometry(ntc, flush, shear: 0);
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g, n: 10000, moment: 1e6));
        var s = EndPlateNmvStiffness.Evaluate(r);
        Assert.IsTrue(s.HasQualifiedLocalTangent, string.Join(";", s.LocalDiagnostics));
        Assert.IsFalse(s.CanUseAsGlobalConnectionSpring); Assert.IsFalse(s.CanQualifyWholeFrame);
        Assert.IsNull(s.GlobalMatrixMatches); Assert.IsFalse(s.RequiresGlobalModelReview);
        Assert.IsTrue(s.GlobalDiagnostics.Contains("SeparateColumnPanelModelAndMatchingBeamFacePortsRequired"));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
    }

    [TestMethod]
    public void PureAxialForceRotatesAnAsymmetricConnectionThroughTheCouplingTerm()
    {
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry(shear: 0), 10000, 0, separatePanel: true));
        var s = EndPlateNmvStiffness.Evaluate(r); var k = s.LocalTangent;
        Assert.IsTrue(s.CanUseAsGlobalConnectionSpring, string.Join(";", s.LocalDiagnostics));
        Assert.IsTrue(Math.Abs(s.ModelRotation!.Value) > 1e-9); Assert.AreNotEqual(0, k.Coupling);
        double determinant = k.Axial * k.Rotation - k.Coupling * k.Coupling;
        Assert.AreEqual(-k.Coupling * 10000 / determinant, s.ModelRotation.Value, 1e-12);
        Assert.AreEqual(k.Rotation * 10000 / determinant, s.AxialOpening!.Value, 1e-10);
        Assert.AreEqual(10000, k.Axial * s.AxialOpening.Value + k.Coupling * s.ModelRotation.Value, 1e-5);
        Assert.AreEqual(0, k.Coupling * s.AxialOpening.Value + k.Rotation * s.ModelRotation.Value, .001);
    }

    [TestMethod]
    public void AppliedFullMatrixMustMatchIncludingItsOffDiagonalTerm()
    {
        var g = EndPlateNmvCaseData.Geometry(shear: 0);
        var baseInput = EndPlateNmvCaseData.Input(g, 10000, 0, separatePanel: true);
        var raw = EndPlateNmvStiffness.Evaluate(EndPlateNmv.Verify(baseInput)); var k = raw.LocalTangent;
        var matching = EndPlateNmvStiffness.Evaluate(EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g, 10000, 0, separatePanel: true, applied: k)));
        Assert.AreEqual(true, matching.GlobalMatrixMatches); Assert.IsFalse(matching.RequiresGlobalModelReview);
        var diagonalOnly = new EndPlateNmvTangent(k.Axial, 0, k.Rotation);
        var wrong = EndPlateNmvStiffness.Evaluate(EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g, 10000, 0, separatePanel: true, applied: diagonalOnly)));
        Assert.AreEqual(false, wrong.GlobalMatrixMatches); Assert.IsTrue(wrong.RequiresGlobalModelReview);
        Assert.AreEqual(ConnectionStatus.Passed, wrong.Resistance.Status);
    }

    [TestMethod]
    public void TwoThirdsElasticUseGateDoesNotTurnAPassingStrengthCaseIntoFailure()
    {
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry(shear: 0, contactWidth: 2), -12000, 0, separatePanel: true));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, EndPlateNmvTests.Details(r));
        Assert.IsTrue(r.ComponentUtilization > 2d / 3);
        var s = EndPlateNmvStiffness.Evaluate(r);
        Assert.IsFalse(s.HasQualifiedLocalTangent); Assert.IsNull(s.LocalTangent);
        Assert.IsTrue(s.LocalDiagnostics.Contains("ComponentDemandOutsideConservativeInitialElasticUseRange"));
    }

    [TestMethod]
    public void UndefinedOriginBranchAndFailedResistanceNeverExportAGlobalSpring()
    {
        var g = EndPlateNmvCaseData.Geometry(shear: 0);
        var zero = EndPlateNmvStiffness.Evaluate(EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g, 0, 0, separatePanel: true)));
        Assert.AreEqual(ConnectionStatus.Passed, zero.Resistance.Status);
        Assert.IsFalse(zero.HasQualifiedLocalTangent); Assert.IsNull(zero.LocalTangent);
        var failed = EndPlateNmvStiffness.Evaluate(EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g, columnN: -4e6, separatePanel: true)));
        Assert.AreEqual(ConnectionStatus.Failed, failed.Resistance.Status); Assert.IsFalse(failed.CanUseAsGlobalConnectionSpring);
    }

    [TestMethod]
    public void APretendedSeparatePanelWithoutItsModelReferenceDoesNotQualifyTheSpring()
    {
        var p = EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry(shear: 0), 10000, 0);
        var r = EndPlateNmv.Verify(new(p.Geometry, p.BeamFaceActions, p.ColumnUpper, p.ColumnLower,
            p.CombinationSource, p.ColumnReferenceSource, true, ""));
        var s = EndPlateNmvStiffness.Evaluate(r);
        Assert.IsTrue(s.HasQualifiedLocalTangent); Assert.IsFalse(s.CanUseAsGlobalConnectionSpring);
    }

    [TestMethod]
    public void AnUnqualifiedAppliedMatrixTriggersReviewInsteadOfBeingSilentlyAccepted()
    {
        var g = EndPlateNmvCaseData.Geometry(shear: 0);
        var p = EndPlateNmvCaseData.Input(g, 10000, 0, applied: new EndPlateNmvTangent(1e6, 0, 1e10));
        var s = EndPlateNmvStiffness.Evaluate(EndPlateNmv.Verify(p));
        Assert.IsFalse(s.CanUseAsGlobalConnectionSpring); Assert.IsNull(s.GlobalMatrixMatches); Assert.IsTrue(s.RequiresGlobalModelReview);
        Assert.ThrowsException<ArgumentException>(() => EndPlateNmvCaseData.Input(g, applied: new EndPlateNmvTangent(1, 2, 1)));
    }
}
