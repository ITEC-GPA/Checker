using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BraceAnglePairBoltedTransferSymmetryTraceTests
{
    [DataTestMethod]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    public void SymmetryPreservesEveryRawComponentAndResidualWithoutDiscardingFaceCouples(
        bool verticalBacks, bool reflectInPlane, bool reverseParts)
    {
        var angle = new SectionL(verticalBacks ? 100 : 150, 10,
            verticalBacks ? 150 : 100, 10, "Analytical sharp unequal angle");
        var initial = SectionBuiltUp.DoubleAngle(angle, 20, verticalBacks
            ? SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack
            : SectionBuiltUp.AnglePairOrientation.HorizontalLegsBackToBack, "Symmetric pair");
        // Non-binary translations in both directions. The supplied physical
        // actions remain exactly axial; no rounded moment is generated as input.
        var parts = initial.Parts.Select(p => new SectionBuiltUp.Part(p.Section,
            p.X + (verticalBacks ? 50.1 : 1000.1), p.Y + (verticalBacks ? 1000.1 : 50.1),
            !verticalBacks && reflectInPlane ? !p.MirrorX : p.MirrorX,
            verticalBacks && reflectInPlane ? !p.MirrorY : p.MirrorY)).ToArray();
        if (reverseParts) Array.Reverse(parts);
        var snapshot = new SteelSectionInput(new SteelSection(new SectionBuiltUp(parts, "Translated/reflected pair"),
            new SteelMaterial("Actual S275", 210000, 275, 430)));
        var source = new SteelActions(200000, 0, 0, 0, 0, 0);
        var adapted = BraceAngleModelAdapter.FromPair(snapshot,
            ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Explicit analytical factors"),
            source, 100000, 100000, "Explicit equal concomitant tensions",
            new ConnectionBolt(16, 18, 157, ConnectionBoltGrade.Class8_8, true), 4, 60, 75);
        var input = new BraceAnglePairBoltedTransferInput(adapted, new ConnectionPlate(200, 20, 275, 430),
            40, 40, 100, 30, false, false, "Actual one-line geometry/hardware",
            "Conditional plane model with unresolved local couples", "One exact axial action case");
        var result = BraceAnglePairBoltedTransfer.Verify(input);

        Assert.AreEqual(ConnectionStatus.Passed, result.InPlaneComponents.Status,
            string.Join("; ", result.InPlaneComponents.Diagnostics));
        Assert.AreSame(source, result.Input.Angles.SourceActions);
        Assert.AreEqual(200000d, Value("SourceActions.N"));
        foreach (string name in new[] { "V1", "V2", "T", "M1", "M2" })
            Assert.AreEqual(0d, Value("SourceActions." + name));

        // Independent sharp-angle centroid: y=48.75 on the connected150mm leg.
        // The bolt line at75mm gives the retained physical lever26.25mm.
        double sign = reflectInPlane ? -1 : 1;
        Assert.AreEqual(sign * 26.25, Value("GussetAtGroup.InPlaneLever"));
        Assert.AreEqual(sign * 5250000, result.ActionsOnGussetAtBoltGroup.Mz);
        Assert.AreEqual(0d, result.ActionsOnGussetAtBoltGroup.My);
        Assert.IsTrue(Math.Abs(Value("GussetAtGroup.RawResult.My")) > 0,
            "The diagnostic must retain the reproduced transport rounding residual.");
        foreach (string name in new[] { "Fx", "Fy", "Fz", "Mx", "My", "Mz" })
            Assert.AreEqual(Value("GussetAtGroup.RawResult." + name) - Value("GussetAtGroup." + name),
                Value("GussetAtGroup.ArithmeticResidual." + name));

        Assert.AreEqual(2, result.FaceDemands.Count);
        foreach (var face in result.FaceDemands)
        {
            Assert.AreEqual(2375000, Math.Abs(face.AtGussetFace.My), .0001);
            Assert.AreEqual(1875000, Math.Abs(face.AtBoltLine.My), .0001);
        }
        Assert.AreEqual(ConnectionStatus.NotSupported, result.LocalThreeDimensionalStatus);
        Assert.IsFalse(result.CanQualifyWholeJoint);
        double Value(string name) => result.InPlaneComponents.Trace.Single(v => v.Name == name).Value;
    }
}
