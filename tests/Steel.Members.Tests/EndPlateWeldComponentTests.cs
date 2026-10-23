using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateWeldComponentTests
{
    private static EndPlateMomentComponentResult Parent(bool ntc = false, double shear = 300000, double moment = 300e6,
        double tensionLeg = 12, double webLeg = 8, double compressionLeg = 8, double plateT = 25,
        bool extended = true, double plateWidth = 250)
    {
        var code = ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "SCI C1");
        var policy = ntc ? ColumnWebPolicy.Ntc2018(code) : ColumnWebPolicy.Eurocode3(code, 1);
        var beam = new ConnectionRolledISection(533.1, 209.3, 10.1, 15.6, 12.7, 11700, 275, 410);
        var column = new ConnectionRolledISection(266.7, 258.8, 12.8, 20.5, 12.7, 13600, 265, 410);
        var g = new TStubInternalEndPlateGeometry(plateWidth, 258.8, 100, 10.1, webLeg, tensionLeg, compressionLeg, 501.9, new[] { 44.4, 134.4 },
            new(7.5, "SCI P398 C1 graphical alpha"), "SCI C1; column continuations1000mm explicitly assumed in fixture");
        return EndPlateMomentComponents.Verify(new EndPlateMomentComponentInput(policy, column, beam, 265,
            new(plateWidth, plateT, 265, 410), new(24, 26, 353, ConnectionBoltGrade.Class8_8, true), g,
            extended ? new(plateWidth, 100, 40, tensionLeg, 50, "SCI C1") : null!, 1000, 1000, 46.9,
            new(code, beam, 649e6, shear, "Qualified test Mc,Rd for same section/code/shear; SCI C1 baseline649kNm"),
            moment, shear, 0, 0, 0, ColumnWebPanelConfiguration.DoubleSidedSimilarBeamDepths,
            "Equal/opposite beam moments", "Zero web longitudinal compression in fixture", "Qualified column-sway restraint",
            "Same ULS combination", TStubMode1Method.DistributedContact, 39.55, "NTC clearance SLE reference"));
    }
    private static EndPlateWeldDetail Detail(string? missing = null,
        EndPlateCompressionTransfer compression = EndPlateCompressionTransfer.QualifiedDirectBearing,
        FilletWeldEndCondition tensionEnds = FilletWeldEndCondition.FullSize,
        FilletWeldEndCondition webEnds = FilletWeldEndCondition.FullSize,
        FilletWeldEndCondition compressionEnds = FilletWeldEndCondition.FullSize)
    {
        string Source(string name) => missing == name ? "" : name + ": explicit test qualification, not an execution certificate";
        return new(tensionEnds, webEnds, compressionEnds, compression, Source("Detail"), Source("Normal"),
            Source("Shear"), Source("Consumable"), Source("Eccentricity"), Source("Bearing"));
    }
    private static EndPlateWeldComponentResult Verify(EndPlateMomentComponentResult? parent = null,
        EndPlateWeldDetail? detail = null, double flangeFy = 275, double flangeFu = 410,
        FilletWeldSteelGrade beamGrade = FilletWeldSteelGrade.S275, string material = "Actual-thickness S275 material qualification")
        => EndPlateWeldComponents.Verify(new(parent ?? Parent(), beamGrade, FilletWeldSteelGrade.S275,
            flangeFy, flangeFu, material, detail ?? Detail()));
    private static double Trace(EndPlateWeldComponentResult r, string name)
        => r.WeldAssessment.Trace.Single(t => t.Name == name).Value;
    private static void Unsupported(EndPlateWeldComponentResult r, string reason)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.WeldAssessment.Status);
        Assert.AreEqual(0, r.WeldAssessment.Checks.Count); Assert.IsNull(r.WeldAssessment.Utilization);
        Assert.IsTrue(r.WeldAssessment.Diagnostics.Any(d => d.Contains(reason)), string.Join("; ", r.WeldAssessment.Diagnostics));
    }

    [TestMethod]
    public void SciC1LegsBecomeActualThroatsAndRemainLocalComponentEvidence()
    {
        var r = Verify();
        Assert.AreEqual(ConnectionStatus.Passed, r.WeldAssessment.Status, string.Join("; ", r.WeldAssessment.Diagnostics));
        Assert.AreEqual(4, r.WeldAssessment.Checks.Count);
        Assert.AreEqual(8.48528137423857, Trace(r, "TensionFlange.Throat"), 1e-12);
        Assert.AreEqual(5.65685424949238, Trace(r, "Web.Throat"), 1e-12);
        Assert.IsTrue(Trace(r, "TensionFlange.Throat") > 15.6 / 2);
        // SCI p105 rounds its web thickness to10.2; the supplied catalogue snapshot is10.1.
        Assert.IsTrue(Trace(r, "Web.Throat") > 10.2 / 2);
        Assert.AreEqual(86.9, Trace(r, "TensionFlange.NominalStraightRun"), 1e-12);
        Assert.AreEqual(476.5, Trace(r, "Web.EffectiveStraightRun"), 1e-12);
        Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.PendingChecks.Any(s => s.Contains("rolled-root")));
        Assert.IsTrue(r.PendingChecks.Any(s => s.Contains("Rotation")));
    }

    [TestMethod]
    public void IndependentDirectionalBenchmarkCombinesFullNormalEnvelopeAndAllWebShear()
    {
        var r = Verify();
        Assert.AreEqual(178.75, Trace(r, "TensionFlange.SigmaPerpendicular"), 1e-12);
        Assert.AreEqual(173.59375, Trace(r, "Web.SigmaPerpendicular"), 1e-12);
        Assert.AreEqual(173.59375, Trace(r, "Web.TauPerpendicular"), 1e-12);
        Assert.AreEqual(55.6484875015646, Trace(r, "Web.TauParallel"), 1e-12);
        var web = r.WeldAssessment.Checks.Single(c => c.Id == "Web.DirectionalEquivalent");
        Assert.AreEqual(360.3185016619121, web.Demand, 1e-10);
        Assert.AreEqual(385.8823529411765, web.Resistance, 1e-10);
        Assert.AreEqual(300000, Trace(r, "WebShearTransferred"));
        Assert.AreEqual(0, Trace(r, "FlangeShearTransferred"));
        Assert.AreEqual(0, Trace(r, "TensionFlange.TauParallel"));
    }

    [TestMethod]
    public void CombinedNormalAndShearCanFailAlthoughShearAloneWouldPass()
    {
        var parent = Parent(shear: 550000, moment: 0);
        Assert.AreEqual(ConnectionStatus.Passed, parent.ComponentAssessment.Status);
        var r = Verify(parent);
        var web = r.WeldAssessment.Checks.Single(c => c.Id == "Web.DirectionalEquivalent");
        Assert.AreEqual(ConnectionStatus.Failed, r.WeldAssessment.Status);
        Assert.IsFalse(web.Passed);
        Assert.IsTrue(Math.Sqrt(3) * Trace(r, "Web.TauParallel") < web.Resistance);
        Assert.IsTrue(r.WeldAssessment.Checks.Single(c => c.Id == "TensionFlange.DirectionalEquivalent").Passed);
    }

    [TestMethod]
    public void WebOnlyShearInteractionBoundaryMatchesIndependentSolution()
    {
        const double boundary = 524211.4980928528;
        Assert.AreEqual(ConnectionStatus.Passed, Verify(Parent(shear: boundary * (1 - 1e-8), moment: 0)).WeldAssessment.Status);
        Assert.AreEqual(ConnectionStatus.Failed, Verify(Parent(shear: boundary * (1 + 1e-8), moment: 0)).WeldAssessment.Status);
    }

    [TestMethod]
    public void FullYieldEnvelopeDoesNotDisappearAtZeroMoment()
    {
        var r = Verify(Parent(moment: 0));
        Assert.AreEqual(178.75, Trace(r, "TensionFlange.SigmaPerpendicular"), 1e-12);
        Assert.AreEqual(173.59375, Trace(r, "Web.SigmaPerpendicular"), 1e-12);
        Assert.AreEqual(0, Trace(r, "MomentDemand"));
    }

    [TestMethod]
    public void NominalCompressionWeldIsNotSilentlyTreatedAsFullStrengthWithoutBearing()
    {
        var parent = Parent();
        var r = Verify(parent, Detail(compression: EndPlateCompressionTransfer.FullStrengthWelds));
        Assert.AreEqual(ConnectionStatus.Failed, r.WeldAssessment.Status);
        Assert.AreEqual(6, r.WeldAssessment.Checks.Count);
        Assert.AreEqual(-268.125, Trace(r, "CompressionFlange.SigmaPerpendicular"), 1e-12);
        Assert.AreEqual(536.25, r.WeldAssessment.Checks.Single(c => c.Id == "CompressionFlange.DirectionalEquivalent").Demand, 1e-12);
        Assert.AreEqual(0, r.WeldAssessment.Checks.Single(c => c.Id == "CompressionFlange.NormalTension").Demand);
        Assert.AreEqual(parent.MomentResistance, r.MomentComponents.MomentResistance);
    }

    [TestMethod]
    public void CompressionFullStrengthWeldsCanPassWhenTheirParentGeometryUsesAdequateLegs()
    {
        var r = Verify(Parent(compressionLeg: 12), Detail(compression: EndPlateCompressionTransfer.FullStrengthWelds));
        Assert.AreEqual(ConnectionStatus.Passed, r.WeldAssessment.Status, string.Join("; ", r.WeldAssessment.Diagnostics));
        Assert.AreEqual(12, Trace(r, "CompressionFlange.Leg"));
        Assert.AreEqual(-178.75, Trace(r, "CompressionFlange.SigmaPerpendicular"), 1e-12);
        Assert.AreEqual(0, Trace(r, "CompressionFlange.QualifiedDirectBearing"));
    }

    [TestMethod]
    public void QualifiedCompressionBearingDoesNotInventAContactResistanceCheck()
    {
        var r = Verify();
        Assert.AreEqual(1, Trace(r, "CompressionFlange.QualifiedDirectBearing"));
        Assert.AreEqual(0, Trace(r, "CompressionFlange.SigmaPerpendicular"));
        Assert.IsFalse(r.WeldAssessment.Checks.Any(c => c.Id.StartsWith("CompressionFlange")));
        Assert.IsTrue(r.PendingChecks.Any(s => s.Contains("contact pressure")));
        Unsupported(Verify(detail: Detail(missing: "Bearing")), "CompressionBearingQualification");
    }

    [TestMethod]
    public void EcNominalEndsShortenEachRunAndIncreaseBothWebStressComponents()
    {
        var r = Verify(detail: Detail(tensionEnds: FilletWeldEndCondition.NominalWithoutFullSizeEvidence,
            webEnds: FilletWeldEndCondition.NominalWithoutFullSizeEvidence));
        Assert.AreEqual(69.92943725152286, Trace(r, "TensionFlange.EffectiveStraightRun"), 1e-12);
        Assert.AreEqual(465.18629150101524, Trace(r, "Web.EffectiveStraightRun"), 1e-12);
        Assert.AreEqual(222.12927217088006, Trace(r, "TensionFlange.SigmaPerpendicular"), 1e-10);
        Assert.AreEqual(177.81569101723943, Trace(r, "Web.SigmaPerpendicular"), 1e-10);
        Assert.AreEqual(57.00190392312466, Trace(r, "Web.TauParallel"), 1e-10);
        Assert.AreEqual(ConnectionStatus.Failed, r.WeldAssessment.Status);
    }

    [DataTestMethod]
    [DataRow(FilletWeldEndCondition.FullSize)]
    [DataRow(FilletWeldEndCondition.NominalWithoutFullSizeEvidence)]
    public void NtcUsesItsOwnEffectiveLengthAndYieldFactor(FilletWeldEndCondition ends)
    {
        var r = Verify(Parent(ntc: true), Detail(tensionEnds: ends, webEnds: ends));
        Assert.AreEqual(ConnectionStatus.Passed, r.WeldAssessment.Status);
        Assert.AreEqual(86.9, Trace(r, "TensionFlange.EffectiveStraightRun"), 1e-12);
        Assert.AreEqual(476.5, Trace(r, "Web.EffectiveStraightRun"), 1e-12);
        Assert.AreEqual(170.23809523809524, Trace(r, "TensionFlange.SigmaPerpendicular"), 1e-10);
        Assert.AreEqual(344.4166576313204, r.WeldAssessment.Checks.Single(c => c.Id == "Web.DirectionalEquivalent").Demand, 1e-10);
        Assert.IsTrue(r.WeldAssessment.Checks.All(c => c.Reference.Contains("NTC2018")));
    }

    [TestMethod]
    public void ActualFlangeUltimateStrengthDoesNotBorrowTheWebStrength()
    {
        var r = Verify(flangeFu: 350);
        Assert.AreEqual(350, Trace(r, "TensionFlange.Fu"));
        Assert.AreEqual(410, Trace(r, "Web.Fu"));
        Assert.AreEqual(ConnectionStatus.Failed, r.WeldAssessment.Status);
    }

    [TestMethod]
    public void FlangeYieldUsesItsOwnThicknessQualification()
    {
        var r = Verify(flangeFy: 265);
        Assert.AreEqual(172.25, Trace(r, "TensionFlange.SigmaPerpendicular"), 1e-12);
        Assert.AreEqual(173.59375, Trace(r, "Web.SigmaPerpendicular"), 1e-12);
    }

    [DataTestMethod]
    [DataRow("Detail", "ContinuousPaired")]
    [DataRow("Normal", "RolledRootAndTransition")]
    [DataRow("Shear", "UniformWebOnly")]
    [DataRow("Consumable", "ConsumableCompatibility")]
    [DataRow("Eccentricity", "LocalEccentricity")]
    public void DetailQualificationsCannotBeReplacedByAComponentPass(string missing, string expected)
        => Unsupported(Verify(detail: Detail(missing: missing)), expected);

    [TestMethod]
    public void MaterialAndParentStateAreQualified()
    {
        Unsupported(Verify(material: ""), "ActualThicknessMaterialSource");
        Unsupported(Verify(beamGrade: FilletWeldSteelGrade.S235), "YieldStrengthExceeds");
        Unsupported(Verify(Parent(moment: 500e6)), "PassedMomentComponents");
        Unsupported(Verify(Parent(plateT: 3, moment: 0)), "ParentThicknessBelowFour");
    }

    [TestMethod]
    public void DefectiveEndsAreUnsupportedOnEveryBeamPartIncludingNominalCompressionWelds()
    {
        Unsupported(Verify(detail: Detail(tensionEnds: FilletWeldEndCondition.Defective)), "TensionFlange:Defective");
        Unsupported(Verify(detail: Detail(webEnds: FilletWeldEndCondition.Defective)), "Web:Defective");
        Unsupported(Verify(detail: Detail(compressionEnds: FilletWeldEndCondition.Defective)), "CompressionFlange:Defective");
    }

    [TestMethod]
    public void MinimumThroatAndEffectiveRunRemainGeometricalApplicabilityLimits()
    {
        Unsupported(Verify(Parent(webLeg: 4, moment: 0)), "Web:ThroatBelowThree");
        Unsupported(Verify(Parent(tensionLeg: 20, moment: 0), Detail(tensionEnds: FilletWeldEndCondition.NominalWithoutFullSizeEvidence)),
            "TensionFlange:EffectiveLengthBelowScope");
    }

    [TestMethod]
    public void PairedFlangeFilletsRequireAnActualPlateOnBothFacesAcrossTheWholeFlange()
    {
        Unsupported(Verify(Parent(extended: false, moment: 0)), "FlushPlateHasNoOuterTensionFlange");
        Unsupported(Verify(Parent(plateWidth: 200, moment: 0)), "EndPlateMustCoverFullBeamFlangeWidth");
    }

    [TestMethod]
    public void InvalidInputsAreRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateWeldComponents.Verify(null!));
        Assert.ThrowsException<ArgumentException>(() => Verify(flangeFu: 200));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Verify(flangeFy: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Detail(compression: (EndPlateCompressionTransfer)99));
    }
}
