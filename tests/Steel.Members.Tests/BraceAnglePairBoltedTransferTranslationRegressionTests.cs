using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BraceAnglePairBoltedTransferTranslationRegressionTests
{
    [DataTestMethod]
    [DataRow(true, 50.1)]
    [DataRow(true, 1000.1)]
    [DataRow(false, 50.1)]
    [DataRow(false, 1000.1)]
    public void PureNormalTranslationPreservesTheSymmetricPhysicalPlaneProblem(bool verticalBacks, double translation)
    {
        var original = BraceAnglePairBoltedTransfer.Verify(Input(verticalBacks, 0));
        var movedInput = Input(verticalBacks, translation);
        // This is the same exact physical case. Do not derive a tiny supplied moment
        // from the rounded sum of separately transported constituent forces.
        Assert.AreEqual(0d, movedInput.Angles.SourceActions.M1);
        Assert.AreEqual(0d, movedInput.Angles.SourceActions.M2);
        var moved = BraceAnglePairBoltedTransfer.Verify(movedInput);

        Assert.AreEqual(ConnectionStatus.Passed, original.InPlaneComponents.Status);
        Assert.AreEqual(ConnectionStatus.Passed, moved.InPlaneComponents.Status,
            "A rigid translation cannot create a physical out-of-plane resultant: "
            + string.Join("; ", moved.InPlaneComponents.Diagnostics));
        Equal(200000, moved.ActionsOnGussetAtBoltGroup.Fx);
        Equal(5250000, moved.ActionsOnGussetAtBoltGroup.Mz);
        Equal(56471.784990382585, moved.InPlaneBoltGroup.MaximumShear!.Value);
        Equal(original.InPlaneComponents.Utilization!.Value, moved.InPlaneComponents.Utilization!.Value);
        Assert.AreEqual(2, moved.FaceDemands.Count);
        foreach (var face in moved.FaceDemands)
        {
            Equal(100000, face.AtGussetFace.Fx);
            Equal(2375000, Math.Abs(face.AtGussetFace.My));
            Equal(1875000, Math.Abs(face.AtBoltLine.My));
        }
        Assert.AreEqual(ConnectionStatus.NotSupported, moved.LocalThreeDimensionalStatus);
        Assert.IsFalse(moved.CanQualifyWholeJoint);
    }

    private static BraceAnglePairBoltedTransferInput Input(bool verticalBacks, double normalTranslation)
    {
        var code = ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Translation regression: explicit EC factors");
        var angle = new SectionL(verticalBacks ? 100 : 150, 10,
            verticalBacks ? 150 : 100, 10, "Sharp unequal angle");
        var original = SectionBuiltUp.DoubleAngle(angle, 20, verticalBacks
            ? SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack
            : SectionBuiltUp.AnglePairOrientation.HorizontalLegsBackToBack, "Actual symmetric pair");
        var parts = original.Parts.Select(p => new SectionBuiltUp.Part(p.Section,
            p.X + (verticalBacks ? normalTranslation : 0),
            p.Y + (verticalBacks ? 0 : normalTranslation), p.MirrorX, p.MirrorY));
        var pair = new SectionBuiltUp(parts, "Translated symmetric pair");
        var snapshot = new SteelSectionInput(new SteelSection(pair,
            new SteelMaterial("Actual S275", 210000, 275, 430)));
        var adapted = BraceAngleModelAdapter.FromPair(snapshot, code,
            new SteelActions(200000, 0, 0, 0, 0, 0), 100000, 100000,
            "Same explicit equal axial forces at the pair centroid, ULS translation regression",
            new ConnectionBolt(16, 18, 157, ConnectionBoltGrade.Class8_8, true), 4, 60, 75);
        return new BraceAnglePairBoltedTransferInput(adapted, new ConnectionPlate(200, 20, 275, 430),
            40, 40, 100, 30, false, false, "Actual one-line geometry and hardware",
            "Conditional equal-stiffness in-plane model; local couples remain unresolved",
            "Same physical centroidal ULS case before and after translation");
    }

    private static void Equal(double expected, double actual)
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-10));
}
