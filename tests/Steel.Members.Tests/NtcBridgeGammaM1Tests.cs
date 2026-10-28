using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>NTC 2018 Table 4.2.VII: gammaM1=1.05 for member buckling in general and
/// 1.10 for members of road and railway bridges. gammaM0 and gammaM2 are unchanged.</summary>
[TestClass]
public class NtcBridgeGammaM1Tests
{
    [TestMethod]
    public void BridgeProfileUsesTheTableValueAndRejectsOtherValues()
    {
        Assert.AreEqual(1.05, SteelDesignContext.Ntc2018().GammaM1);
        Assert.AreEqual(1.10, SteelDesignContext.Ntc2018Bridges().GammaM1);
        Assert.AreEqual(1.05, SteelDesignContext.Ntc2018Bridges().SectionCode.GammaM0);
        Assert.AreEqual(1.10, IBeamSplicePlatePolicy.Ntc2018Bridges().GammaM1);
        Assert.AreEqual(1.05, IBeamSplicePlatePolicy.Ntc2018().GammaM1);
        Assert.ThrowsException<ArgumentException>(() => new SteelDesignContext(SectionResistanceCode.Ntc2018(), 1.2));
    }

    [TestMethod]
    public void BridgeCoverBucklingUsesOnePointOneZeroAndKeepsTheSectionFactor()
    {
        // Same covers as IBeamSplicePlateTests.NtcRetainsItsOwnPlateFractureAndStabilityFactors: only gammaM1 changes.
        var r = IBeamSplicePlates.VerifyFlanges(IBeamSplicePlateTests.Input(true, policy: IBeamSplicePlatePolicy.Ntc2018Bridges()));
        IBeamSplicePlateTests.Equal(582089.3054057129 / 1.10, IBeamSplicePlateTests.Check(r, "BottomFlange.Cover.CoverBuckling").Resistance);
        IBeamSplicePlateTests.Equal(594000 / 1.05, IBeamSplicePlateTests.Check(r, "BottomFlange.Cover.GrossCompression").Resistance);
    }

    [TestMethod]
    public void BridgeWebShearBucklingResistanceUsesOnePointOneZero()
    {
        // SCI AD368 web, as in EffectiveShearTests: Vbw = 0.83*86.4*tw^2*eps*fy/(sqrt3*gammaM1).
        var b = CommonSectionCoverageTests.Input(new SectionH(398, 6.4, 141.8, 8.6, 141.8, 8.6, "SCI406x140x39", 10.2), true, fy: 355);
        var i = new SteelVerificationInput(b.Section, new SteelActions(0, 0, 1e5, 0, 0, 0), b.Code);
        var a = SteelShearBucklingAssessment.EvaluateNonRigidHWeb(i, 1.10, 1, "Support stiffeners, welds and force introduction independently checked");
        double expected = .83 * 86.4 * 6.4 * 6.4 * Math.Sqrt(235d / 355) * 355 / (Math.Sqrt(3) * 1.10);
        Assert.AreEqual(expected, a.BucklingResistance!.Value, 1e-7);
        Assert.ThrowsException<ArgumentException>(() => SteelShearBucklingAssessment.EvaluateNonRigidHWeb(i, 1.2, 1, "support"));
    }

    [TestMethod]
    public void BridgeSpliceApplicationRejectsTheBuildingFactorUnderNtc()
    {
        var template = WeldedIBeamSpliceStandardTests.Input(true);
        Assert.AreEqual(1.05, template.Plates.Policy.GammaM1);
        var section = WeldedIBeamSpliceStandardTests.Section(template).Plan.Input.Section;
        var e = Assert.ThrowsException<ArgumentException>(() => BridgeSpliceApplication.CreateBolted(template, section, section,
            BridgeSpliceApplicationTests.Actions(), WeldedIBeamSpliceStandardTests.Welds(), "Declared global analysis", SpliceBoltCategory.A));
        StringAssert.Contains(e.Message, "1.10");
        var bridge = BridgeSpliceApplicationTests.Bridge(template);
        Assert.AreEqual(1.10, bridge.Plates.Policy.GammaM1);
        Assert.AreEqual(2, BridgeSpliceApplication.CreateBolted(bridge, section, section, BridgeSpliceApplicationTests.Actions(),
            WeldedIBeamSpliceStandardTests.Welds(), "Declared global analysis", SpliceBoltCategory.A, BridgeSpliceApplicationTests.Prepare).Cases.Count);
    }
}
