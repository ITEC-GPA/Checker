using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BraceAnglePairBoltedTransferFailureEvidenceTests
{
    [TestMethod]
    public void FailedAngleSectionsRemainConsultableWhenLaterNtcBoltEvidenceIsMissing()
    {
        var shape = SectionBuiltUp.DoubleAngle(new SectionL(100, 10, 150, 10, "Sharp angle"), 20,
            SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack, "Actual pair");
        var snapshot = new SteelSectionInput(new SteelSection(shape, new SteelMaterial("Actual S275", 210000, 275, 430)));
        var adapted = BraceAngleModelAdapter.FromPair(snapshot, ConnectionDesignCode.Ntc2018(),
            new SteelActions(1000000, 0, 0, 0, 0, 0), 500000, 500000, "Explicit equal forces in the same failed ULS case",
            new ConnectionBolt(16, 18, 157, ConnectionBoltGrade.Class8_8, true), 4, 60, 75);
        var input = new BraceAnglePairBoltedTransferInput(adapted, new ConnectionPlate(200, 20, 275, 430),
            40, 40, 100, 30, false, false, "Actual one-line geometry and hardware",
            "Conditional in-plane model", "ULS failure-evidence regression", null);
        var result = BraceAnglePairBoltedTransfer.Verify(input);

        // Missing NTC bolt evidence keeps the assessment incomplete; only the known failures stay visible.
        Assert.AreEqual(ConnectionStatus.Failed, result.InPlaneComponents.Status);
        Assert.IsFalse(result.InPlaneComponents.IsComplete);
        Assert.IsTrue(result.InPlaneComponents.Checks.Count > 0 && result.InPlaneComponents.Checks.All(c => !c.Passed));
        Assert.IsNull(result.InPlaneComponents.Utilization);
        Assert.AreEqual(2, result.AngleSections.Count);
        foreach (var section in result.AngleSections)
        {
            Assert.AreEqual(ConnectionStatus.Failed, section.Status);
            var net = section.Checks.Single(c => c.Id == "Angle.EffectiveNetRupture");
            Assert.AreEqual(500000d, net.Demand);
            Assert.AreEqual(432752d, net.Resistance, 1e-6);
            Assert.IsFalse(net.Passed);
            Assert.IsTrue(section.Utilization > 1);
        }
        Assert.AreEqual(ConnectionStatus.NotSupported, result.Status);
        Assert.IsFalse(result.CanQualifyWholeJoint);
    }
}
