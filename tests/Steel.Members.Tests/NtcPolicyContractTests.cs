using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class NtcPolicyContractTests
{
    private static SteelSection Shape() => new(new SectionCHS(200, 8), new SteelMaterialEN1993("S355", 210000, 355, 510),
        formedType: Section.FormedTypes.HotFinished);
    private static AxialSteelMember Member() => new(Shape(), 355, "test certificate", true, 3000, 3000, "fork ends");
    private static CircularHollowSteelSection Tube() => new(Shape(), 355, "test certificate", true);

    [DataTestMethod]
    [DataRow("GammaM0")] [DataRow("GammaM1")] [DataRow("GammaM2")]
    [DataRow("AlphaA0")] [DataRow("AlphaA")] [DataRow("AlphaC")]
    public void ChangedUsedNormativeParameterDoesNotSilentlyChangeQualifiedAxialProfile(string parameter)
    {
        var code = new StandardNTC2018Steel();
        switch (parameter)
        {
            case "GammaM0": code.GammaM0 = 1; break;
            case "GammaM1": code.GammaM1 = 1; break;
            case "GammaM2": code.GammaM2 = 1; break;
            case "AlphaA0": code.AlphaImperfectionFactorForCurveA0 = .12; break;
            case "AlphaA": code.AlphaImperfectionFactorForCurveA = .20; break;
            case "AlphaC": code.AlphaImperfectionFactorForCurveC = .48; break;
        }
        var r = Ntc2018AxialSteel.Check(Member(), -1000, code);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome);
        Assert.IsTrue(r.Checks.All(c => !c.Capacity.HasValue));
        var section = Ntc2018CircularHollowSections.Check(Tube(), 0, 0, 0, code);
        Assert.AreEqual(parameter == "GammaM0" ? EngineeringOutcome.NotEvaluated : EngineeringOutcome.Satisfied, section.Outcome);
    }

    [TestMethod]
    public void Ec3CodeAndDerivedNationalVariantsRemainOutsideNtcPolicy()
    {
        foreach (var code in new StandardEN1993p11[] { new StandardEN1993p11(), new Variant() })
        {
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018AxialSteel.Check(Member(), -1000, code).Outcome);
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018CircularHollowSections.Check(Tube(), 0, 0, 0, code).Outcome);
        }
    }

    [TestMethod]
    public void NtcSectionAndAxialPathsRetainTheSameNominalCircularClass()
    {
        foreach (double ratio in new[] { 49.999, 50, 50.001, 70, 70.001, 90, 90.001 })
        {
            var shape = new SteelSection(new SectionCHS(ratio, 1), new SteelMaterialEN1993("S235", 210000, 235, 360),
                formedType: Section.FormedTypes.HotFinished);
            var axial = new AxialSteelMember(shape, 235, "certificate", true, 100, 100, "forks");
            var tube = new CircularHollowSteelSection(shape, 235, "certificate", true);
            var a = Ntc2018AxialSteel.Check(axial, -.001, new StandardNTC2018Steel());
            var b = Ntc2018CircularHollowSections.Check(tube, -.001, 0, 0, new StandardNTC2018Steel());
            int expected = ratio <= 50 ? 1 : ratio <= 70 ? 2 : ratio <= 90 ? 3 : 4;
            Assert.AreEqual(expected, a.CompressionClass); Assert.AreEqual(expected, b.SectionClass);
            if (expected > 2) Assert.AreEqual(EngineeringOutcome.NotEvaluated, b.Outcome);
            if (expected == 4) Assert.AreEqual(EngineeringOutcome.NotEvaluated, a.Outcome);
        }
    }

    private sealed class Variant : StandardNTC2018Steel { }
}
