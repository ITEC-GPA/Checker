using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.IBeamSplicePlateTests;

namespace Steel.Members.Tests;

[TestClass]
public class IBeamSpliceFlangeNetPlasticTests
{
    private static ConnectionVerification<IBeamSplicePlateInput> Verify(IBeamSplicePlateInput? input = null)
        => IBeamSplicePlates.VerifyFlangeNetPlastic(input ?? Input());

    private static IBeamSplicePlateInput Actions(double n, double v, double m, bool ntc = false)
        => Input(ntc, forces: IBeamSpliceForceTests.Input(actions: new(n, v, 0, 0, 0, m)));

    private static IBeamSplicePlateInput Code(IBeamSplicePlateInput p, double gm0 = 1,
        double gm2Plate = 1.1, double gm2Bolt = 1.25, double gm1 = 1)
        => new(IBeamSplicePlatePolicy.Eurocode3(ConnectionDesignCode.Eurocode3(
            gm0, gm2Plate, gm2Bolt, "Explicit test parameters"), gm1), p.Forces, p.Bolts,
            p.FlangeCover, p.WebCover, p.FlangeCoverEnd, p.WebCoverEnd, p.ButtGap,
            p.BeamFlangeFy, p.BeamFlangeFu, p.Exposed, p.UltimateCombinationSource);

    [DataTestMethod]
    [DataRow("TopFlange.Cover", 448800d)]
    [DataRow("BottomFlange.Cover", 448800d)]
    [DataRow("TopFlange.Member", 509555.75)]
    [DataRow("BottomFlange.Member", 509555.75)]
    public void IndependentCategoryCNetPlasticCapacitiesUseP398Geometry(string part, double expected)
    {
        var r = Verify();
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Equal(expected, Check(r, part + ".NetPlasticTension").Resistance);
        Assert.AreEqual("N", Check(r, part + ".NetPlasticTension").Unit);
    }

    [DataTestMethod]
    [DataRow("TopFlange.Cover", 1632d, 2160d)]
    [DataRow("BottomFlange.Cover", 1632d, 2160d)]
    [DataRow("TopFlange.Member", 1852.93, 2411.73)]
    [DataRow("BottomFlange.Member", 1852.93, 2411.73)]
    public void EachTransverseSectionDeductsTwoFullHoles(string part, double net, double gross)
    {
        var r = Verify();
        Equal(net, Trace(r, part + ".NetArea")); Equal(gross, Trace(r, part + ".GrossArea"));
        Equal(2, Trace(r, part + ".HolesAtSection")); Equal(22, Trace(r, part + ".HoleDiameter"));
    }

    [TestMethod]
    public void SignedPhysicalFlangeDemandsAreSharedByMemberAndSingleCover()
    {
        var r = Verify();
        foreach (string part in new[] { "Member", "Cover" })
        {
            Equal(325010.524734258826, Check(r, "TopFlange." + part + ".NetPlasticTension").Demand);
            Equal(0, Check(r, "BottomFlange." + part + ".NetPlasticTension").Demand);
            Equal(-411185.963330750063, Trace(r, "BottomFlange." + part + ".SignedAxialForce"));
        }
        Assert.AreEqual(4, r.Checks.Count);
        Equal(.7241767485166195, r.Utilization!.Value);
        Assert.AreEqual("TopFlange.Cover.NetPlasticTension", r.GoverningCheck.Id);
    }

    [TestMethod]
    public void CategoryCCanFailWhenTheExistingFlangeStrengthChecksPass()
    {
        var p = Actions(1600000, 0, 0);
        var old = IBeamSplicePlates.VerifyFlanges(p); var added = Verify(p);
        Assert.AreEqual(ConnectionStatus.Passed, old.Status, string.Join(";", old.Diagnostics));
        Assert.AreEqual(ConnectionStatus.Failed, added.Status);
        Equal(459602.339181286574, Check(added, "TopFlange.Cover.NetPlasticTension").Demand);
        Equal(547461.818181818118, Check(old, "TopFlange.Cover.NetTension").Resistance);
        Equal(448800, Check(added, "TopFlange.Cover.NetPlasticTension").Resistance);
    }

    [DataTestMethod] [DataRow(.999999, true)] [DataRow(1.000001, false)]
    public void NetPlasticBoundaryControlsTheComponentWithoutRuptureSubstitution(double ratio, bool passes)
    {
        // Choose pure N to put each cover just below/above Anet*fy. No code
        // result is used to derive the force; the adjacent member remains stronger.
        double n = 2 * 448800 * ratio / (1 - 3638d / 8550);
        var r = Verify(Actions(n, 0, 0));
        Assert.AreEqual(passes ? ConnectionStatus.Passed : ConnectionStatus.Failed, r.Status);
        Equal(ratio, Check(r, "TopFlange.Cover.NetPlasticTension").Utilization);
    }

    [TestMethod]
    public void NtcUsesItsOwnGammaM0AndNamesTheSupplementaryEcCriterion()
    {
        var r = Verify(Input(true));
        Equal(427428.57142857142, Check(r, "TopFlange.Cover.NetPlasticTension").Resistance);
        Equal(485291.190476190473, Check(r, "TopFlange.Member.NetPlasticTension").Resistance);
        Equal(325010.524734258826, Check(r, "TopFlange.Cover.NetPlasticTension").Demand);
        Equal(1.05, Trace(r, "GammaM0"));
        Assert.IsTrue(r.Checks.All(c => c.Reference.Contains("supplementary EC") && c.Reference.Contains("NTC2018")));
    }

    [DataTestMethod] [DataRow(.9)] [DataRow(1d)] [DataRow(1.2)]
    public void ExplicitEcGammaM0ScalesThisResistanceOnly(double gamma)
    {
        var r = Verify(Code(Input(), gm0: gamma));
        Equal(448800 / gamma, Check(r, "TopFlange.Cover.NetPlasticTension").Resistance);
        Equal(509555.75 / gamma, Check(r, "TopFlange.Member.NetPlasticTension").Resistance);
        Equal(325010.524734258826, Check(r, "TopFlange.Cover.NetPlasticTension").Demand);
    }

    [TestMethod]
    public void FractureAndBucklingFactorsDoNotLeakIntoNetPlasticTension()
    {
        var baseline = Verify(); var changed = Verify(Code(Input(), gm2Plate: 1.6, gm2Bolt: 1.4, gm1: 1.3));
        foreach (var c in baseline.Checks) Equal(c.Resistance, Check(changed, c.Id).Resistance);
    }

    [TestMethod]
    public void MemberAndCoverUseTheirActualYieldStrengthAndNotTheirUltimateStrength()
    {
        var r = Verify(Input(flange: new(180, 12, 355, 510), beamFlangeFy: 265, beamFlangeFu: 400));
        Equal(579360, Check(r, "TopFlange.Cover.NetPlasticTension").Resistance);
        Equal(491026.45, Check(r, "TopFlange.Member.NetPlasticTension").Resistance);
        var higherFu = Verify(Input(flange: new(180, 12, 355, 650), beamFlangeFy: 265, beamFlangeFu: 650));
        foreach (var c in r.Checks) Equal(c.Resistance, Check(higherFu, c.Id).Resistance);
    }

    [DataTestMethod] [DataRow(0d)] [DataRow(-100000d)] [DataRow(-1600000d)]
    public void PureCompressionOrZeroHasNoTensileDemandButDoesNotQualifyCompression(double n)
    {
        var r = Verify(Actions(n, 0, 0));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.IsTrue(r.Checks.All(c => c.Demand == 0));
        StringAssert.Contains(r.Scope, "compression resistance");
        StringAssert.Contains(string.Join(";", r.Assumptions), "only means this tensile failure mode is inactive");
    }

    [TestMethod]
    public void PureAxialTensionLoadsBothFlangesWithoutTheWebShare()
    {
        var r = Verify(Actions(150000, 0, 0));
        double each = (1 - 3638d / 8550) * 150000 / 2;
        Assert.IsTrue(each < 75000);
        Assert.IsTrue(r.Checks.All(c => Math.Abs(c.Demand - each) < 1e-8));
    }

    [DataTestMethod] [DataRow(-200000000d, "TopFlange")] [DataRow(200000000d, "BottomFlange")]
    public void PureMomentTensionsTheCorrectFlangeEvenWithZeroTotalN(double moment, string tensileFlange)
    {
        var r = Verify(Actions(0, 0, moment));
        double expected = (1 - 55535282.666666664183 / 294000000d) * Math.Abs(moment) / 440.7;
        Equal(expected, Check(r, tensileFlange + ".Cover.NetPlasticTension").Demand);
        string compressed = tensileFlange == "TopFlange" ? "BottomFlange" : "TopFlange";
        Equal(0, Check(r, compressed + ".Cover.NetPlasticTension").Demand);
    }

    [TestMethod]
    public void CombinedActionReversalDoesNotUseTheAbsoluteCompressionAsTension()
    {
        var r = Verify(Actions(150000, 150000, 200e6));
        Equal(0, Check(r, "TopFlange.Cover.NetPlasticTension").Demand);
        Equal(411185.963330750063, Check(r, "BottomFlange.Cover.NetPlasticTension").Demand);
    }

    [TestMethod]
    public void ChangingOnlyJointShearDoesNotMoveTheWebShearToTheFlanges()
    {
        var baseline = Verify(); var changed = Verify(Actions(-150000, 350000, -200e6));
        foreach (var c in baseline.Checks) Equal(c.Demand, Check(changed, c.Id).Demand);
        Assert.AreEqual(350000, changed.Input.Forces.Actions.Fy);
    }

    [TestMethod]
    public void ActionPoleTransportIsAppliedBeforeTheConcomitantFlangeCheck()
    {
        // M0=Mz+x*V-y*N=-200e6 with x20,y30,N=V=-150000.
        var moved = new PlanarConnectionActions(-150000, -150000, 0, 0, 0, -201500000, 20, 30);
        var baseline = Verify(); var r = Verify(Input(forces: IBeamSpliceForceTests.Input(actions: moved)));
        foreach (var c in baseline.Checks) Equal(c.Demand, Check(r, c.Id).Demand);
    }

    [DataTestMethod] [DataRow(2)] [DataRow(3)] [DataRow(4)]
    public void LongitudinalBoltRowsDoNotMultiplyTheHolesInTheNetSection(int rows)
    {
        var r = Verify(Input(forces: IBeamSpliceForceTests.Input(flange: new(rows, 2, 80, 120, 60))));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Equal(1632, Trace(r, "TopFlange.Cover.NetArea"));
        Equal(448800, Check(r, "TopFlange.Cover.NetPlasticTension").Resistance);
    }

    [TestMethod]
    public void ActualRoundHoleDiameterIsDeductedInFull()
    {
        var r = Verify(Input(bolt: new(20, 21, 245, ConnectionBoltGrade.Class8_8, true)));
        Equal(1656, Trace(r, "TopFlange.Cover.NetArea"));
        Equal(455400, Check(r, "TopFlange.Cover.NetPlasticTension").Resistance);
    }

    [DataTestMethod] [DataRow(1e-9)] [DataRow(.5)] [DataRow(2d)]
    public void LinearLoadScalingHasNoAbsoluteFloor(double scale)
    {
        var r = Verify(Actions(-150000 * scale, -150000 * scale, -200e6 * scale));
        Assert.IsTrue(r.IsComplete);
        Assert.AreEqual(325010.524734258826 * scale,
            Check(r, "TopFlange.Cover.NetPlasticTension").Demand, 325010.524734258826 * scale * 1e-10);
        Equal(448800, Check(r, "TopFlange.Cover.NetPlasticTension").Resistance);
    }

    [TestMethod]
    public void ScopePreservesWebAndWholeJointQualificationBoundaries()
    {
        var r = Verify();
        StringAssert.Contains(r.Scope, "CATEGORY C FLANGE NET-PLASTIC COMPONENT ONLY");
        StringAssert.Contains(r.Scope, "No web net-plastic N-V-M assessment");
        StringAssert.Contains(r.Scope, "whole-joint qualification");
        StringAssert.Contains(string.Join(";", r.Assumptions), "assess every concomitant ULS case");
        Assert.IsTrue(r.Checks.All(c => c.Reference.Contains("6.2.3(4)") && c.Reference.Contains("3.4.1(1)c")));
    }

    [TestMethod]
    public void MissingUltimateSourceCannotReturnFavourablePartialChecks()
        => Unsupported(Verify(Input(source: "")), "ULTIMATE_COMBINATION_SOURCE_REQUIRED");

    [TestMethod]
    public void OutOfPlaneActionsAreNotDiscarded()
        => Unsupported(Verify(Input(forces: IBeamSpliceForceTests.Input(actions: new(1, 2, 3, 0, 0, 4)))), "IN_PLANE_N_V_M_ONLY");

    [TestMethod]
    public void UnsupportedFlangeGridCannotBecomeAZeroDemandPass()
        => Unsupported(Verify(Input(forces: IBeamSpliceForceTests.Input(
            actions: new(0, 0, 0, 0, 0, 0), flange: new(3, 1, 80, 120, 60)))), "FLANGE_REQUIRES_TWO_TRANSVERSE_COLUMNS");

    [TestMethod]
    public void NumericOverflowHasNoFavourableUtilization()
        => Unsupported(Verify(Code(Input(), gm0: 1e-308)), "NET_PLASTIC_RESISTANCE_OUTSIDE_NUMERIC_RANGE");

    [TestMethod]
    public void NullInputIsRejected()
        => Assert.ThrowsException<ArgumentNullException>(() => IBeamSplicePlates.VerifyFlangeNetPlastic(null!));
}
