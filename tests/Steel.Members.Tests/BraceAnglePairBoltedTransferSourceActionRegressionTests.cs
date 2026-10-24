using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BraceAnglePairBoltedTransferSourceActionRegressionTests
{
    [DataTestMethod]
    [DataRow(true, 0.00001)]
    [DataRow(true, -0.00001)]
    [DataRow(false, 0.00001)]
    [DataRow(false, -0.00001)]
    public void ARealSuppliedMomentCannotBeDiscardedAsATransportRoundingResidual(bool verticalBacks, double physicalMoment)
    {
        var p = Input(verticalBacks, physicalMoment, 0);
        Assert.IsTrue(p.Angles.SourceActions.M1 != 0 || p.Angles.SourceActions.M2 != 0,
            "The regression supplies a physical moment, not a computed transport residual.");
        var result = BraceAnglePairBoltedTransfer.Verify(p);
        AssertUnsupported(result);
        Assert.AreSame(p.Angles.SourceActions, result.Input.Angles.SourceActions);
        Assert.IsTrue(result.Input.Angles.SourceActions.M1 != 0 || result.Input.Angles.SourceActions.M2 != 0);
    }

    [DataTestMethod]
    [DataRow(0.000001)]
    [DataRow(-0.000001)]
    public void AnExplicitAxialForceMismatchCannotBeReplacedByTheSumOfChordForces(double axialMismatch)
    {
        var p = Input(true, 0, axialMismatch);
        Assert.AreEqual(200000 + axialMismatch, p.Angles.SourceActions.N);
        Assert.AreNotEqual(p.Angles.SourceActions.N,
            p.Angles.Components.Sum(c => c.Input.Tension));
        var result = BraceAnglePairBoltedTransfer.Verify(p);
        AssertUnsupported(result);
        Assert.AreEqual(200000 + axialMismatch, result.Input.Angles.SourceActions.N);
    }

    private static BraceAnglePairBoltedTransferInput Input(bool verticalBacks, double physicalMoment, double axialMismatch)
    {
        var angle = new SectionL(verticalBacks ? 100 : 150, 10,
            verticalBacks ? 150 : 100, 10, "Sharp unequal angle");
        var pair = SectionBuiltUp.DoubleAngle(angle, 20, verticalBacks
            ? SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack
            : SectionBuiltUp.AnglePairOrientation.HorizontalLegsBackToBack, "Symmetric pair");
        var snapshot = new SteelSectionInput(new SteelSection(pair,
            new SteelMaterial("Actual S275", 210000, 275, 430)));
        // Equal axial chord forces produce opposing bending contributions of
        // 3,375,000 Nmm at the pair centroid. FromPair's numerical equilibrium
        // tolerance admits a 1e-5 Nmm difference. The later transfer must still
        // distinguish an explicitly supplied nonzero action from roundoff.
        // Use the geometric bending axis conjugate to the normal separation:
        // about Model Y for vertical backs, about Model X for horizontal backs.
        double mx = verticalBacks ? 0 : physicalMoment;
        double my = verticalBacks ? physicalMoment : 0;
        double c = Math.Cos(snapshot.PrincipalAxisAngle), s = Math.Sin(snapshot.PrincipalAxisAngle);
        var actions = new SteelActions(200000 + axialMismatch, 0, 0, 0,
            c * mx + s * my, -s * mx + c * my);
        var adapted = BraceAngleModelAdapter.FromPair(snapshot,
            ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Explicit source-action regression factors"),
            actions, 100000, 100000, "Explicit equal chord forces; source action deliberately differs within adapter tolerance",
            new ConnectionBolt(16, 18, 157, ConnectionBoltGrade.Class8_8, true), 4, 60, 75);
        Assert.AreSame(actions, adapted.SourceActions);
        return new BraceAnglePairBoltedTransferInput(adapted, new ConnectionPlate(200, 20, 275, 430),
            40, 40, 100, 30, false, false, "Actual one-line geometry and hardware",
            "Conditional in-plane model with exact action scope", "Explicit concomitant source case");
    }

    private static void AssertUnsupported(BraceAnglePairBoltedTransferResult result)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, result.InPlaneComponents.Status,
            "The explicit source actions must not be replaced by a favourable reconstructed case.");
        Assert.AreEqual(0, result.InPlaneComponents.Checks.Count);
        Assert.IsNull(result.InPlaneComponents.Utilization);
        Assert.IsFalse(result.CanQualifyWholeJoint);
    }
}
