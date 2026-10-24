using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class TopFlushCjpEndPlateWeldTests
{
    private static EndPlateMomentComponentResult Parent(bool ntc = false, double shear = 300000, double moment = 0,
        double plateFy = 345, double plateFu = 470, double plateT = 25, double plateWidth = 250,
        double lowerLeg = 8, double lowerProjection = 46.9, bool extended = false, double axial = 0,
        double width = 209.3, double flange = 15.6, double upperInnerLeg = 12)
    {
        var code = ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "Analytical top-flush CJP variant");
        var policy = ntc ? ColumnWebPolicy.Ntc2018(code) : ColumnWebPolicy.Eurocode3(code, 1);
        double h = 533.1, tw = 10.1, r = 12.7;
        double area = 2 * width * flange + (h - 2 * flange) * tw + (4 - Math.PI) * r * r;
        var beam = new ConnectionRolledISection(h, width, tw, flange, r, area, 275, 410);
        var column = new ConnectionRolledISection(266.7, 258.8, 12.8, 20.5, 12.7, 13600, 265, 410);
        var geometry = new TStubInternalEndPlateGeometry(plateWidth, 258.8, 100, tw, 8, upperInnerLeg, lowerLeg,
            h - 2 * flange, new[] { 44.4, 134.4 }, new(7.5, "SCI C1 graphical alpha for baseline; changed toe sizes are rejection-only fixtures"),
            "Analytical variant: CJP upper seam, actual declared inner toe; lower plate projection retained");
        // Zero moment is used for analytical seam tests; a same-input beam capacity
        // is retained without using it as a weld-resistance fixture.
        return EndPlateMomentComponents.Verify(new EndPlateMomentComponentInput(policy, column, beam, 265,
            new(plateWidth, plateT, plateFy, plateFu), new(24, 26, 353, ConnectionBoltGrade.Class8_8, true), geometry,
            extended ? new(plateWidth, 100, 40, upperInnerLeg, 50, "Explicit extended counterexample") : null!,
            1000, 1000, lowerProjection, new(code, beam, 500e6, shear, "Conservative externally qualified test capacity for this same action state"),
            moment, shear, axial, 0, 0, ColumnWebPanelConfiguration.DoubleSidedSimilarBeamDepths,
            "Equal/opposite test beams", "No column longitudinal stress in fixture", "Qualified column restraint",
            "One test ULS case", TStubMode1Method.DistributedContact, 39.55, "NTC hole-clearance SLE reference"));
    }
    private static EndPlateCjpFlangeDetail Cjp(double fusion = 15.6, double width = 209.3,
        double innerLeg = 12, double outerLeg = 0, double weldFy = 355, double weldFu = 510,
        bool full = true, string? missing = null)
    {
        string S(string key) => missing == key ? "" : key + ": caller-supplied physical qualification, not a test certificate";
        return new(fusion, width, innerLeg, outerLeg, weldFy, weldFu, full,
            S("penetration"), S("inspection"), S("toe"), S("throughThickness"));
    }
    private static TopFlushCjpEndPlateWeldInput Input(EndPlateMomentComponentResult? parent = null,
        EndPlateCjpFlangeDetail? cjp = null, bool ntc = false, double height = 580, double top = 0,
        string? missing = null, FilletWeldEndCondition webEnds = FilletWeldEndCondition.FullSize,
        FilletWeldEndCondition lowerEnds = FilletWeldEndCondition.FullSize,
        EndPlateCompressionTransfer compression = EndPlateCompressionTransfer.QualifiedDirectBearing,
        double flangeFy = 275, double flangeFu = 410)
    {
        string S(string key) => missing == key ? "" : key + ": explicit fixture evidence";
        return new(parent ?? Parent(ntc), cjp ?? Cjp(), height, top, FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S355,
            flangeFy, flangeFu, S("material"), webEnds, lowerEnds, compression,
            S("fillets"), S("normal"), S("shear"), S("consumable"), S("eccentricity"), S("bearing"));
    }
    private static TopFlushCjpEndPlateWeldResult Verify(TopFlushCjpEndPlateWeldInput? p = null)
        => TopFlushCjpEndPlateWeldComponents.Verify(p ?? Input());
    private static double T(TopFlushCjpEndPlateWeldResult r, string name) => r.WeldAssessment.Trace.Single(t => t.Name == name).Value;
    private static void Equal(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-11));
    private static void Supported(TopFlushCjpEndPlateWeldResult r)
        => Assert.AreNotEqual(ConnectionStatus.NotSupported, r.WeldAssessment.Status,
            string.Join(";", r.WeldAssessment.Diagnostics.Concat(r.MomentComponents.ComponentAssessment.Diagnostics)));
    private static void Unsupported(TopFlushCjpEndPlateWeldResult r, string reason)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.WeldAssessment.Status);
        Assert.AreEqual(0, r.WeldAssessment.Checks.Count); Assert.IsNull(r.WeldAssessment.Utilization);
        Assert.IsTrue(r.WeldAssessment.Diagnostics.Any(d => d.Contains(reason)), string.Join(";", r.WeldAssessment.Diagnostics));
    }

    [DataTestMethod]
    [DataRow(false, 660000.0)] [DataRow(true, 628571.4285714286)]
    public void IndependentCjpSectionBenchmarkUsesActualNormalFlangeAreaAndGammaM0(bool ntc, double resistance)
    {
        var r = Verify(Input(Parent(ntc, plateT: 20, width: 200, flange: 12), Cjp(fusion: 12, width: 200), ntc));
        Supported(r); Equal(2400, T(r, "Cjp.NormalFlangeArea")); Equal(resistance, T(r, "Cjp.EquivalentFlangeResistance"));
        var check = r.WeldAssessment.Checks.Single(c => c.Id == "Cjp.FullStrengthNormalEquivalence");
        Equal(resistance, check.Resistance); Equal(resistance, check.Demand); Assert.AreEqual("N", check.Unit);
        StringAssert.Contains(check.Reference, ntc ? "NTC2018 4.2.8.2.1" : "EN1993-1-8:2005 4.7.1");
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsFalse(r.IsFullyFlushGeometry);
    }

    [DataTestMethod]
    [DataRow(false, 897897.0, 360.3185016619121)] [DataRow(true, 855140.0, 344.4166576313204)]
    public void AnalyticalC1GeometryVariantRetainsFullWebShearInteraction(bool ntc, double cjpResistance, double webEquivalent)
    {
        // Geometry from SCI C1, but TOP-FLUSH CJP and S355 end plate are explicit
        // analytical variants. These numbers are not published SCI joint results.
        var r = Verify(Input(ntc: ntc)); Supported(r);
        Assert.AreEqual(ConnectionStatus.Passed, r.WeldAssessment.Status);
        Equal(3265.08, T(r, "Cjp.NormalFlangeArea")); Equal(cjpResistance, T(r, "Cjp.EquivalentFlangeResistance"));
        Equal(300000, T(r, "WebShearTransferred")); Equal(0, T(r, "FlangeShearTransferred"));
        Equal(476.5, T(r, "Web.EffectiveStraightRun")); Equal(55.6484875015646, T(r, "Web.TauParallel"));
        Equal(webEquivalent, r.WeldAssessment.Checks.Single(c => c.Id == "Web.DirectionalEquivalent").Demand);
        Equal(385.8823529411765, r.WeldAssessment.Checks.Single(c => c.Id == "Web.DirectionalEquivalent").Resistance);
        Equal(.85, T(r, "Web.BetaW")); Equal(410, T(r, "Web.Fu"));
        Assert.AreEqual(3, r.WeldAssessment.Checks.Count);
    }

    [TestMethod]
    public void OriginalC1WeakerPlateIsUnsupportedNotDeclaredAFlangeWeldFailure()
        => Unsupported(Verify(Input(Parent(plateFy: 265, plateFu: 410))), "WeakerPlateRequiresSeparateLocalResistanceQualification");

    [DataTestMethod]
    [DataRow(1.0)] [DataRow(15.59999999)]
    public void IncompleteDepthCannotBePromotedByARealInnerFillet(double depth)
        => Unsupported(Verify(Input(cjp: Cjp(fusion: depth))), "CjpDoesNotFuseWholeFlangeThickness");

    [DataTestMethod]
    [DataRow(100.0)] [DataRow(209.29999999)]
    public void WidthCoverageMustReachTheWholeFlange(double width)
        => Unsupported(Verify(Input(cjp: Cjp(width: width))), "CjpDoesNotCoverWholeFlangeWidth");

    [TestMethod]
    public void ExtraDeclaredWidthOrDepthCannotIncreaseTheEquivalentResistance()
    {
        var r = Verify(Input(cjp: Cjp(fusion: 30, width: 240))); Supported(r);
        Equal(30, T(r, "Cjp.FusionDepthSupplied")); Equal(240, T(r, "Cjp.SeamWidthSupplied"));
        Equal(15.6, T(r, "Cjp.FusionDepthCredited")); Equal(209.3, T(r, "Cjp.SeamWidthCredited"));
        Equal(897897, T(r, "Cjp.EquivalentFlangeResistance"));
    }

    [TestMethod]
    public void ExplicitPartialOrUnqualifiedPenetrationCannotUseTheCjpRule()
        => Unsupported(Verify(Input(cjp: Cjp(full: false))), "PartialOrUnqualifiedPenetrationIsNotCjp");

    [DataTestMethod]
    [DataRow(344.0, 510.0, "AllWeldYieldStrengthBelowConnectedMaterials")]
    [DataRow(355.0, 469.0, "AllWeldUltimateStrengthBelowConnectedMaterials")]
    public void BothAllWeldMechanicalMinimaMustMatchBothConnectedMaterials(double fy, double fu, string issue)
        => Unsupported(Verify(Input(cjp: Cjp(weldFy: fy, weldFu: fu))), issue);

    [TestMethod]
    public void ExactMatchingAllWeldMinimaArePermittedWithoutInventingOverstrength()
    {
        var r = Verify(Input(cjp: Cjp(weldFy: 345, weldFu: 470))); Supported(r);
        Equal(897897, T(r, "Cjp.EquivalentFlangeResistance"));
    }

    [DataTestMethod]
    [DataRow(274.0, 470.0)] [DataRow(345.0, 409.0)]
    public void AWeakerPlateInEitherStrengthRequiresASeparateLocalResistance(double fy, double fu)
        => Unsupported(Verify(Input(Parent(plateFy: fy, plateFu: fu))), "WeakerPlateRequiresSeparateLocalResistanceQualification");

    [TestMethod]
    public void ThinPlateCannotBeTurnedIntoFictitiousMinThicknessResistance()
        => Unsupported(Verify(Input(Parent(plateT: 15))), "PlateThinnerThanFlangeOutsideSimpleCjpEquivalence");

    [TestMethod]
    public void EqualPlateThicknessAndMaterialAreInclusiveWithinTheRestrictedField()
    {
        var r = Verify(Input(Parent(plateT: 15.6, plateFy: 275, plateFu: 410), Cjp(weldFy: 275, weldFu: 410))); Supported(r);
        Equal(897897, T(r, "Cjp.EquivalentFlangeResistance"));
    }

    [DataTestMethod]
    [DataRow(8.0)] [DataRow(12.00000001)]
    public void RealInnerToeMustMatchTheSameParentTStubGeometry(double innerLeg)
        => Unsupported(Verify(Input(cjp: Cjp(innerLeg: innerLeg))), "ActualInnerToeDoesNotMatchParentTStubGeometry");

    [TestMethod]
    public void OuterUpperFilletCannotBePlacedBeyondAFlushPlateEdge()
        => Unsupported(Verify(Input(cjp: Cjp(outerLeg: 3))), "NoOuterTopFilletFitsAtFlushEdge");

    [TestMethod]
    public void InnerFilletUsedOnlyForToeGeometryStillNeedsAMinimumPhysicalThroat()
        => Unsupported(Verify(Input(Parent(upperInnerLeg: 3), Cjp(innerLeg: 3))), "Cjp.InnerFilletGeometry:ThroatBelowThreeMillimetres");

    [TestMethod]
    public void InnerFilletUsedOnlyForGeometryStillNeedsItsActualClearStraightRun()
        => Unsupported(Verify(Input(Parent(upperInnerLeg: 22), Cjp(innerLeg: 22))), "Cjp.InnerFilletGeometry:EffectiveLengthBelowScope");

    [TestMethod]
    public void AbsenceOfAnOuterBoltRowDoesNotEstablishTheTopPlateEdgePosition()
        => Unsupported(Verify(Input(height: 581, top: 1)), "ActualUpperPlateEdgeMustBeFlushWithFlangeExterior");

    [DataTestMethod]
    [DataRow(579.99999999)] [DataRow(600.0)]
    public void ActualPlateHeightMustPreserveBothParentEdgePositions(double height)
        => Unsupported(Verify(Input(height: height)), "PlateHeightDoesNotMatchParentBeamAndEdgePositions");

    [TestMethod]
    public void ExtendedOuterBoltRowCannotBeDisguisedAsTopFlush()
        => Unsupported(Verify(Input(Parent(extended: true))), "TopFlushDetailRequiresNoExternalBoltRow");

    [TestMethod]
    public void FullyFlushBottomRemainsUnsupportedByTheCurrentParentGeometry()
    {
        var r = Verify(Input(Parent(lowerProjection: 0), height: 533.1));
        Unsupported(r, "LowerPairedFilletNeedsRealPlateProjection"); Assert.IsFalse(r.IsFullyFlushGeometry);
    }

    [TestMethod]
    public void PlateWidthMustPhysicallyCoverTheFlange()
        => Unsupported(Verify(Input(Parent(plateWidth: 200))), "EndPlateDoesNotCoverBeamFlangeWidth");

    [DataTestMethod]
    [DataRow("penetration")] [DataRow("inspection")] [DataRow("toe")] [DataRow("throughThickness")]
    public void CjpQualificationSourcesAreRequiredIndependently(string missing)
        => Assert.AreEqual(ConnectionStatus.NotSupported, Verify(Input(cjp: Cjp(missing: missing))).WeldAssessment.Status);

    [DataTestMethod]
    [DataRow("material")] [DataRow("fillets")] [DataRow("normal")] [DataRow("shear")]
    [DataRow("consumable")] [DataRow("eccentricity")] [DataRow("bearing")]
    public void ExistingPhysicalFilletAndBearingQualificationsRemainRequired(string missing)
        => Assert.AreEqual(ConnectionStatus.NotSupported, Verify(Input(missing: missing)).WeldAssessment.Status);

    [TestMethod]
    public void CompressionBearingCannotBeSilentlyReplacedByAnEightMillimetreFullStrengthWeld()
    {
        var r = Verify(Input(compression: EndPlateCompressionTransfer.FullStrengthWelds, missing: "bearing")); Supported(r);
        Assert.AreEqual(ConnectionStatus.Failed, r.WeldAssessment.Status);
        Equal(-268.125, T(r, "CompressionFlange.SigmaPerpendicular"));
        Equal(536.25, r.WeldAssessment.Checks.Single(c => c.Id == "CompressionFlange.DirectionalEquivalent").Demand);
        Assert.IsTrue(r.WeldAssessment.Checks.Single(c => c.Id == "Cjp.FullStrengthNormalEquivalence").Passed);
    }

    [TestMethod]
    public void RealTwelveMillimetreLowerFilletsCanCarryTheFullCompressionEnvelope()
    {
        var r = Verify(Input(Parent(lowerLeg: 12), compression: EndPlateCompressionTransfer.FullStrengthWelds, missing: "bearing")); Supported(r);
        Assert.AreEqual(ConnectionStatus.Passed, r.WeldAssessment.Status);
        Equal(-178.75, T(r, "CompressionFlange.SigmaPerpendicular"));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void NominalEndPoliciesApplyToFilletsButNeverTrimCjpDepthOrWidth(bool ntc)
    {
        var r = Verify(Input(ntc: ntc, webEnds: FilletWeldEndCondition.NominalWithoutFullSizeEvidence)); Supported(r);
        Equal(ntc ? 476.5 : 476.5 - 2 * 8 / Math.Sqrt(2), T(r, "Web.EffectiveStraightRun"));
        Equal(ntc ? 855140 : 897897, T(r, "Cjp.EquivalentFlangeResistance"));
        Equal(15.6, T(r, "Cjp.FusionDepthCredited")); Equal(209.3, T(r, "Cjp.SeamWidthCredited"));
    }

    [TestMethod]
    public void WebNormalShearInteractionStillGovernsWhenCjpEquivalenceIsQualified()
    {
        var r = Verify(Input(Parent(shear: 550000))); Supported(r);
        Assert.AreEqual(ConnectionStatus.Failed, r.WeldAssessment.Status);
        var web = r.WeldAssessment.Checks.Single(c => c.Id == "Web.DirectionalEquivalent");
        Assert.IsTrue(Math.Sqrt(3) * T(r, "Web.TauParallel") < web.Resistance);
        Assert.IsFalse(web.Passed); Equal(550000, T(r, "WebShearTransferred"));
        Equal(0, T(r, "FlangeShearTransferred"));
    }

    [DataTestMethod]
    [DataRow(.99999999, ConnectionStatus.Passed)] [DataRow(1.00000001, ConnectionStatus.Failed)]
    public void IndependentWebInteractionBoundaryIsUnchangedByTheUpperCjp(double factor, ConnectionStatus expected)
    {
        var r = Verify(Input(Parent(shear: 524211.4980928528 * factor))); Supported(r);
        Assert.AreEqual(expected, r.WeldAssessment.Status);
    }

    [TestMethod]
    public void ZeroMomentDoesNotExemptCjpFullStrengthPrerequisitesOrInventFlangeShear()
    {
        var r = Verify(Input(Parent(shear: 0, moment: 0))); Supported(r);
        Equal(0, T(r, "MomentDemand")); Equal(0, T(r, "WebShearTransferred"));
        Equal(897897, T(r, "Cjp.EquivalentFlangeResistance")); Equal(173.59375, T(r, "Web.SigmaPerpendicular"));
        Unsupported(Verify(Input(Parent(shear: 0, moment: 0), Cjp(full: false))), "PartialOrUnqualifiedPenetrationIsNotCjp");
    }

    [TestMethod]
    public void AllParentReferencesAndCapacitiesRemainUnchangedAfterAssessment()
    {
        var parent = Parent(moment: 100e6); var capacity = parent.MomentResistance;
        var input = Input(parent); var r = Verify(input); Supported(r);
        Assert.AreSame(parent, r.MomentComponents); Assert.AreSame(input, r.WeldAssessment.Input);
        Assert.AreEqual(capacity, parent.MomentResistance); Equal(100e6, T(r, "MomentDemand"));
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsTrue(r.PendingChecks.Any(s => s.Contains("rotational")));
    }

    [TestMethod]
    public void NonzeroAxialParentIsRetainedAndUnsupported()
    {
        var r = Verify(Input(Parent(axial: 1000))); Unsupported(r, "PassedMomentComponentsRequiredForWeldEnvelope");
        Equal(1000, T(r, "RetainedAxialDemand"));
    }

    [DataTestMethod]
    [DataRow(true)] [DataRow(false)]
    public void DefectiveRealFilletsAreNotRescuedByCjpQualification(bool web)
        => Unsupported(Verify(Input(webEnds: web ? FilletWeldEndCondition.Defective : FilletWeldEndCondition.FullSize,
            lowerEnds: web ? FilletWeldEndCondition.FullSize : FilletWeldEndCondition.Defective)), "DefectiveEnds");

    [TestMethod]
    public void ArithmeticRoundoffAtInclusiveFullDepthIsNotAManufacturingTolerance()
    {
        var r = Verify(Input(cjp: Cjp(fusion: 15.6 * (1 - 2.220446049250313e-16)))); Supported(r);
        Equal(897897, T(r, "Cjp.EquivalentFlangeResistance"));
    }

    [TestMethod]
    public void InvalidCjpNumbersAreRejectedBeforeAssessment()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Cjp(fusion: double.PositiveInfinity));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Cjp(innerLeg: 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Cjp(outerLeg: -1));
        Assert.ThrowsException<ArgumentException>(() => Cjp(weldFy: 600, weldFu: 500));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(top: double.NaN));
    }

    [TestMethod]
    public void ExtremeFiniteSeamClaimsCannotOverflowOrMultiplyTheCreditedNormalArea()
    {
        var r = Verify(Input(cjp: Cjp(fusion: 1e308, width: 1e308))); Supported(r);
        Equal(3265.08, T(r, "Cjp.NormalFlangeArea")); Equal(897897, T(r, "Cjp.EquivalentFlangeResistance"));
        Assert.IsTrue(r.WeldAssessment.Trace.All(t => double.IsFinite(t.Value)));
        Assert.IsTrue(r.WeldAssessment.Checks.All(c => double.IsFinite(c.Resistance) && double.IsFinite(c.Demand)));
    }

    [TestMethod]
    public void FlangeStrengthCannotExceedItsDeclaredGradeEvenWithAStrongerPlate()
        => Unsupported(Verify(Input(flangeFy: 300)), "YieldStrengthExceedsDeclaredNominalGrade");
}
