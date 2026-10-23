using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.JointClassificationTestData;

namespace Steel.Members.Tests;

// Independent review regressions: three inconsistent-material cases failed
// before the guard; all seven passed after it. No legacy expectations changed.
[TestClass]
public class EndPlateRotationGradeConsistencyTests
{
    private static EndPlateJointResistanceEvidence Contradictory(string component, double moment = 150e6)
        => Resistance(mj: moment,
            column: Column(fy: component == "Web" ? 460 : 235),
            columnFlangeFy: component == "Flange" ? 460 : 235,
            plate: new ConnectionPlate(250, 6, component == "Plate" ? 460 : 235, 560));

    [DataTestMethod]
    [DataRow("Web")]
    [DataRow("Flange")]
    [DataRow("Plate")]
    public void ActualYieldAboveDeclaredNominalGradeCannotQualifySimplifiedRotation(string component)
    {
        // All nominal grades are S235. One actual fy is 460 MPa, and the
        // 6 mm plate satisfies the thickness criterion even when fy=460.
        // Existing code otherwise returns SufficientForPlasticAnalysis.
        var result = Rotation(Contradictory(component), JointNominalSteelGrade.S235);
        Assert.AreEqual(EndPlateRotationCapacityStatus.OutOfScope, result.Status,
            string.Join(";", result.Diagnostics));
        Assert.IsFalse(result.AppliedRules.Contains("EN1993-1-8:2005 6.4.2(2)"));
    }

    [DataTestMethod]
    [DataRow("Web")]
    [DataRow("Flange")]
    [DataRow("Plate")]
    public void OverstrengthExemptionDoesNotAcquireTheSimplifiedMethodGradeRestrictions(string component)
    {
        var result = Rotation(Contradictory(component, 360e6), JointNominalSteelGrade.S235);
        Assert.AreEqual(EndPlateRotationCapacityStatus.NotRequiredByOverstrength, result.Status);
        CollectionAssert.Contains(result.AppliedRules.ToArray(), "EN1993-1-8:2005 6.4.1(3)");
    }

    [TestMethod]
    public void ThicknessReducedYieldRemainsCompatibleWithTheNominalGrade()
    {
        var evidence = Resistance(column: Column(fy: 225), columnFlangeFy: 225,
            plate: new ConnectionPlate(250, 6, 225, 360));
        var result = Rotation(evidence, JointNominalSteelGrade.S235);
        Assert.AreEqual(EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis, result.Status);
    }
}
