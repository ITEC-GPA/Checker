using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class DoubleAngleCleatTyingComponentsTests
{
    private static ConnectionRolledISection Beam(double h = 547, double tw = 9, double fy = 275,
        double fu = 430, double tf = 15.7, double radius = 24)
        => new(h, 210, tw, tf, radius,
            2 * 210 * tf + (h - 2 * tf) * tw + (4 - Math.PI) * radius * radius, fy, fu);
    private static DoubleAngleCleatTyingGeometry Geometry(int rows = 6, double contact = 37,
        double beamHardware = 37, double root = 11, ConnectionPlate? leg = null,
        ConnectionRolledISection? beam = null, ConnectionBolt? bolt = null, bool notched = false,
        string geometrySource = "SBE-MS5 example4.4 IPE A550, L90x90x10, drawings pp68-69",
        string materialSource = "SBE-MS5 example4.4: S275 fy275/fu430 for actual products",
        string hardwareSource = "SBE-MS5 p78: actual M20 washer/contact diameter37mm")
        => new(leg ?? new ConnectionPlate(90, 10, 275, 430), root,
            beam ?? new ConnectionRolledISection(547, 210, 9, 15.7, 24, 11700, 275, 430),
            bolt ?? new ConnectionBolt(20, 22, 245, ConnectionBoltGrade.Class8_8, true), rows,
            contact, beamHardware, geometrySource, materialSource, hardwareSource, notched);
    private static DoubleAngleCleatTyingResult Verify(DoubleAngleCleatTyingGeometry? geometry = null,
        double force = 370000, PlanarConnectionActions? actions = null, double gammaM0 = 1,
        string caseSource = "SBE-MS5 example4.4, independent accidental centred tying F370kN",
        string gammaM0Source = "SBE-MS5 example4.4 block-tearing yield term gammaM0=1")
        => DoubleAngleCleatTyingComponents.Verify(new DoubleAngleCleatTyingInput(
            SbeAccidentalTyingPolicy.SbeMs5(caseSource, gammaM0, gammaM0Source), geometry ?? Geometry(),
            actions ?? new PlanarConnectionActions(force, 0, 0, 0, 0, 0)));
    private static double T(DoubleAngleCleatTyingResult r, string name) => r.Trace.Single(v => v.Name == name).Value;
    private static ConnectionCheck C(DoubleAngleCleatTyingResult r, string id) => r.Checks.Single(v => v.Id == id);
    private static void Equal(double expected, double actual)
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-10));
    private static void Unsupported(DoubleAngleCleatTyingResult r, string diagnostic)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus);
        Assert.IsFalse(r.IsComponentAssessmentComplete); Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsNull(r.Utilization); Assert.IsNull(r.ComponentTyingResistance); Assert.IsNull(r.GoverningCheck);
        Assert.AreEqual(0, r.Checks.Count); CollectionAssert.Contains(r.Diagnostics.ToArray(), diagnostic);
        Assert.IsTrue(r.Trace.All(v => double.IsFinite(v.Value)));
    }

    // Independent arithmetic in derive_tying_source_benchmarks.py and
    // derive_double_angle_tying_component_cases.py; no expected values from production.
    [DataTestMethod]
    [DataRow("Tying.Cleats.Mode1", 691252.3723999133)]
    [DataRow("Tying.Cleats.Mode2", 1188813.7788137787)]
    [DataRow("Tying.Cleats.Mode3", 1924363.6363636362)]
    [DataRow("Tying.BeamBolts.DoubleShear", 1282909.0909090908)]
    [DataRow("Tying.Cleats.Bearing", 1421487.6033057852)]
    [DataRow("Tying.BeamWeb.Bearing", 639669.4214876033)]
    [DataRow("Tying.Cleats.BlockCase1", 2060538.3722351268)]
    [DataRow("Tying.Cleats.BlockCase2", 2195178.277026654)]
    [DataRow("Tying.BeamWeb.NetTension", 943576.3636363636)]
    [DataRow("Tying.BeamWeb.BlockCase1", 927242.2675058071)]
    public void SbeExampleMatchesIndependentUnroundedComponentCapacities(string check, double expected)
    {
        var r = Verify();
        Assert.AreEqual(ConnectionStatus.Passed, r.CoveredComponentsStatus, string.Join(";", r.Diagnostics));
        Equal(expected, C(r, check).Resistance); Equal(370000, C(r, check).Demand);
    }

    [DataTestMethod]
    [DataRow("Tying.Cleats.Mode1", 696000.0, 6000.0)]
    [DataRow("Tying.Cleats.Mode2", 1190000.0, 2000.0)]
    [DataRow("Tying.Cleats.Mode3", 1920000.0, 6000.0)]
    [DataRow("Tying.BeamBolts.DoubleShear", 1284000.0, 2000.0)]
    [DataRow("Tying.Cleats.Bearing", 1428000.0, 8000.0)]
    [DataRow("Tying.BeamWeb.Bearing", 642000.0, 3000.0)]
    [DataRow("Tying.Cleats.BlockCase1", 2060000.0, 1000.0)]
    [DataRow("Tying.Cleats.BlockCase2", 2195000.0, 1000.0)]
    [DataRow("Tying.BeamWeb.NetTension", 944000.0, 1000.0)]
    [DataRow("Tying.BeamWeb.BlockCase1", 927000.0, 1000.0)]
    public void PrintedSbeValuesRemainSeparateRoundedComparisons(string check, double printed, double printTolerance)
    {
        // SBE pp78-82 round m31.2 to31 and intermediate capacities; no calibration.
        Assert.AreEqual(printed, C(Verify(), check).Resistance, printTolerance);
    }

    [TestMethod]
    public void GeometryAndBlockLigamentsFollowTheActualPublishedDetail()
    {
        var r = Verify();
        foreach (var entry in new (string, double)[] {
            ("Geometry.Height",430), ("Geometry.BeamCentroidY",-8.5), ("Geometry.SupportGauge",109),
            ("Geometry.BeamBoltX",50), ("Geometry.BeamEndEdge",40), ("Geometry.BeamTopBoltEdge",90),
            ("Geometry.BeamBottomBoltEdge",107), ("Geometry.BeamMinimumVerticalEdge",90),
            ("Geometry.EffectiveEnd",40), ("Geometry.EffectivePitch",70), ("Geometry.EffectiveLength",430),
            ("Geometry.M",31.2), ("Geometry.N",39), ("Geometry.ContactEw",9.25),
            ("Tying.Cleats.BlockCase1.Ant",4800), ("Tying.Cleats.BlockCase1.Anv",1160),
            ("Tying.Cleats.BlockCase2.Ant",5380), ("Tying.Cleats.BlockCase2.Anv",580),
            ("Tying.BeamWeb.BlockCase1.Ant",2160), ("Tying.BeamWeb.BlockCase1.Anv",522),
            ("BeamWeb.NetArea",2682), ("Policy.GammaMu",1.1), ("Policy.GammaM0",1) })
            Equal(entry.Item2, T(r, entry.Item1));
        Equal(4202272.727272727, T(r, "Cleat.UltimatePlasticMoment"));
        Equal(160363.63636363635, T(r, "Bolt.SingleTensileResistance"));
        Equal(106909.0909090909, T(r, "BeamBolt.SingleShearPlaneResistance"));
    }

    [TestMethod]
    public void ComponentMinimumDoesNotQualifyTheSupportingMemberOrWholeJoint()
    {
        var r = Verify();
        Assert.AreEqual(10, r.Checks.Count); Assert.IsTrue(r.IsComponentAssessmentComplete);
        Equal(639669.4214876033, r.ComponentTyingResistance!.Value);
        Equal(.5784237726098191, r.Utilization!.Value);
        Assert.AreEqual("Tying.BeamWeb.Bearing", r.GoverningCheck.Id);
        Assert.IsFalse(r.CanQualifyWholeJoint);
        foreach (var pending in new[] {
            "SUPPORT_FLANGE_OR_WEB_LOCAL_TYING_RESISTANCE_AND_CONTACT_COMPATIBILITY",
            "LOCAL_PUNCHING_BENEATH_HEADS_NUTS_AND_WASHERS",
            "ACTUAL_SUPPORT_BOLT_FORCE_DISTRIBUTION_INCLUDING_PRYING",
            "SUPPORT_MEMBER_ACTIONS_AND_FORCE_TRANSFER", "GLOBAL_ROBUSTNESS_AND_DEFORMATION_REQUIREMENTS" })
            CollectionAssert.Contains(r.PendingChecks.ToArray(), pending);
        Assert.IsTrue(r.Scope.Contains("COMPONENTS ONLY"));
    }

    [DataTestMethod]
    [DataRow(0.0, true)] [DataRow(1e-180, true)]
    [DataRow(639669.4204876033, true)] [DataRow(639669.4224876033, false)]
    [DataRow(1279338.8429752066, false)]
    public void LoadBoundaryKeepsActualDemandAndComponentOnlyStatus(double force, bool passes)
    {
        var r = Verify(force: force);
        Assert.AreEqual(passes ? ConnectionStatus.Passed : ConnectionStatus.Failed, r.CoveredComponentsStatus);
        Assert.IsTrue(r.Checks.All(c => c.Demand == force));
        Equal(force / 639669.4214876033, r.Utilization!.Value);
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsTrue(r.IsComponentAssessmentComplete);
    }

    [TestMethod]
    public void SharedBeamBoltsHaveTwoShearPlanesButNoSupportPryingForceIsInvented()
    {
        var r = Verify(); Equal(370000.0 / 6, T(r, "BeamBolt.TotalForce"));
        Equal(370000.0 / 12, T(r, "BeamBolt.EachShearPlaneForce"));
        Equal(185000, T(r, "EachCleat.TyingForce"));
        Equal(1924363.6363636362, T(r, "SupportBolts.TotalTensileResistance"));
        Assert.IsFalse(r.Trace.Any(v => v.Name.StartsWith("SupportBolts.") && v.Name.Contains("Force")));
        Assert.IsTrue(r.Checks.All(c => c.Demand == 370000));
    }

    [TestMethod]
    public void LongJointLengthFollowsTyingDirectionAndDoesNotUseVerticalRowSpan()
    {
        var r = Verify(); Equal(350, (r.Input.Geometry.Rows - 1) * r.Input.Geometry.Pitch);
        Equal(0, T(r, "BeamBolt.LongJointLengthInTyingDirection")); Equal(1, T(r, "BeamBolt.BetaLongJoint"));
        Equal(1282909.0909090908, C(r, "Tying.BeamBolts.DoubleShear").Resistance);
    }

    [TestMethod]
    public void CleatCaseTwoIsCheckedForUnnotchedBeamButWebCaseTwoIsNotTransferred()
    {
        var r = Verify(); Assert.IsFalse(r.Input.Geometry.BeamNotched);
        Assert.IsTrue(r.Checks.Any(c => c.Id == "Tying.Cleats.BlockCase2"));
        Assert.IsFalse(r.Checks.Any(c => c.Id == "Tying.BeamWeb.BlockCase2"));
    }

    [TestMethod]
    public void BeamCentroidForceRetainsRealEccentricMomentAndIsOutsideCentredScope()
    {
        var g = Geometry(); Equal(-8.5, g.BeamCentroidY);
        var r = Verify(g, actions: new PlanarConnectionActions(370000, 0, 0, 0, 0, 0, 0, g.BeamCentroidY, 0));
        Unsupported(r, "CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");
        Equal(3145000, r.ActionsAtCleatCentre.Mz); Equal(370000, r.ActionsAtCleatCentre.Fx);
        Equal(-8.5, T(r, "Actions.ApplicationY"));
    }

    [TestMethod]
    public void FullSixComponentWrenchIsTransportedBeforeApplicabilityIsDecided()
    {
        var r = Verify(actions: new PlanarConnectionActions(2, 3, 5, 7, 11, 13, 17, 19, 23));
        Unsupported(r, "CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");
        Equal(33, r.ActionsAtCleatCentre.Mx); Equal(-28, r.ActionsAtCleatCentre.My);
        Equal(26, r.ActionsAtCleatCentre.Mz);
        foreach (var entry in new (string, double)[] { ("Fx",2), ("Fy",3), ("Fz",5),
            ("Mx",7), ("My",11), ("Mz",13), ("ApplicationX",17), ("ApplicationY",19), ("ApplicationZ",23) })
            Equal(entry.Item2, T(r, "Actions." + entry.Item1));
    }

    [TestMethod]
    public void CorrectTransportCouplesAtAnOffsetRepresentTheSameCentredTying()
    {
        const double f = 370000;
        var r = Verify(actions: new PlanarConnectionActions(f, 0, 0, 0, -23 * f, 19 * f, 17, 19, 23));
        Assert.AreEqual(ConnectionStatus.Passed, r.CoveredComponentsStatus, string.Join(";", r.Diagnostics));
        Equal(0, r.ActionsAtCleatCentre.Mx); Equal(0, r.ActionsAtCleatCentre.My); Equal(0, r.ActionsAtCleatCentre.Mz);
        Equal(639669.4214876033, r.ComponentTyingResistance!.Value);
        Equal(-8510000, T(r, "Actions.My")); Equal(7030000, T(r, "Actions.Mz"));
    }

    [TestMethod]
    public void TranslationAlongTheTyingLineDoesNotCreateMoment()
    {
        var r = Verify(actions: new PlanarConnectionActions(370000, 0, 0, 0, 0, 0, 1e6));
        Assert.AreEqual(ConnectionStatus.Passed, r.CoveredComponentsStatus);
        Equal(0, r.ActionsAtCleatCentre.My); Equal(0, r.ActionsAtCleatCentre.Mz);
    }

    [DataTestMethod]
    [DataRow(-1e-180, 0.0, 0.0, 0.0, 0.0, 0.0)]
    [DataRow(370000.0, 1e-180, 0.0, 0.0, 0.0, 0.0)]
    [DataRow(370000.0, 0.0, -1e-180, 0.0, 0.0, 0.0)]
    [DataRow(370000.0, 0.0, 0.0, 1e-180, 0.0, 0.0)]
    [DataRow(370000.0, 0.0, 0.0, 0.0, -1e-180, 0.0)]
    [DataRow(370000.0, 0.0, 0.0, 0.0, 0.0, 1e-180)]
    [DataRow(0.0, 0.0, 0.0, 1.0, 0.0, 0.0)]
    public void SmallOutOfScopeActionsAndPureCoupleAreNotCancelled(double fx, double fy, double fz,
        double mx, double my, double mz)
    {
        var r = Verify(actions: new PlanarConnectionActions(fx, fy, fz, mx, my, mz));
        Unsupported(r, "CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");
        Assert.AreEqual(fx, r.ActionsAtCleatCentre.Fx); Assert.AreEqual(fy, r.ActionsAtCleatCentre.Fy);
        Assert.AreEqual(fz, r.ActionsAtCleatCentre.Fz); Assert.AreEqual(mx, r.ActionsAtCleatCentre.Mx);
        Assert.AreEqual(my, r.ActionsAtCleatCentre.My); Assert.AreEqual(mz, r.ActionsAtCleatCentre.Mz);
    }

    [DataTestMethod]
    [DataRow(2, 250.0, 241134.54851159765, 213223.14049586773, 251751.35841489804)]
    [DataRow(3, 320.0, 353664.00448367663, 319834.71074380167, 420624.08568762534)]
    [DataRow(4, 390.0, 466193.4604557555, 426446.28099173546, 589496.8129603525)]
    [DataRow(5, 460.0, 578722.9164278344, 533057.8512396695, 758369.5402330798)]
    [DataRow(6, 530.0, 691252.3723999133, 639669.4214876033, 927242.2675058071)]
    public void EachAdmittedRowCountUsesARealCompatibleBeamAndItsWholeGroup(int rows, double height,
        double mode1, double webBearing, double webBlock)
    {
        var r = Verify(Geometry(rows: rows, beam: Beam(height)), force: 1000);
        Assert.AreEqual(ConnectionStatus.Passed, r.CoveredComponentsStatus, string.Join(";", r.Diagnostics));
        Equal(mode1, C(r, "Tying.Cleats.Mode1").Resistance);
        Equal(webBearing, C(r, "Tying.BeamWeb.Bearing").Resistance);
        Equal(webBlock, C(r, "Tying.BeamWeb.BlockCase1").Resistance);
        Equal(0, r.Input.Geometry.BeamCentroidY); Equal(90, r.Input.Geometry.BeamBottomBoltEdge);
    }

    [TestMethod]
    public void GammaM0ChangesOnlyBlockYieldTermsWithoutChangingTheTyingPolicy()
    {
        var a = Verify(); var b = Verify(gammaM0: 1.05, gammaM0Source: "Explicit external design context gammaM0=1.05");
        Equal(2051768.1467174368, C(b, "Tying.Cleats.BlockCase1").Resistance);
        Equal(2190793.164267809, C(b, "Tying.Cleats.BlockCase2").Resistance);
        Equal(923295.6660228466, C(b, "Tying.BeamWeb.BlockCase1").Resistance);
        foreach (var c in a.Checks)
            if (c.Id.Contains("Block"))
            {
                Equal(T(a, c.Id + ".TensionTerm"), T(b, c.Id + ".TensionTerm"));
                Equal(T(a, c.Id + ".ShearYieldTerm") / 1.05, T(b, c.Id + ".ShearYieldTerm"));
            }
            else Equal(c.Resistance, C(b, c.Id).Resistance);
        Equal(1.1, b.Input.Policy.GammaMu); Assert.AreEqual("CTICM.SCI.SBE_MS5.AccidentalTying", b.Input.Policy.MethodId);
    }

    [TestMethod]
    public void ActualSupportContactChangesOnlyDistributedContactModeOne()
    {
        var a = Verify(); var b = Verify(Geometry(contact: 33));
        Equal(8.25, T(b, "Geometry.ContactEw")); Equal(669617.1861787004, C(b, "Tying.Cleats.Mode1").Resistance);
        foreach (var c in a.Checks.Where(c => c.Id != "Tying.Cleats.Mode1")) Equal(c.Resistance, C(b, c.Id).Resistance);
    }

    [TestMethod]
    public void BeamHardwareEnvelopeIsNotSubstitutedForSupportContactWidth()
    {
        var a = Verify(); var b = Verify(Geometry(beamHardware: 33));
        Equal(37, T(b, "Geometry.SupportContactWidth")); Equal(33, T(b, "Geometry.BeamHardwareEnvelopeDiameter"));
        foreach (var c in a.Checks) Equal(c.Resistance, C(b, c.Id).Resistance);
    }

    [TestMethod]
    public void ActualCleatUltimateStrengthIsIndependentOfBeamStrength()
    {
        var a = Verify(); var b = Verify(Geometry(leg: new ConnectionPlate(90, 10, 275, 410)));
        Equal(659101.0992650336, C(b, "Tying.Cleats.Mode1").Resistance);
        Equal(1183245.2732452732, C(b, "Tying.Cleats.Mode2").Resistance);
        Equal(1355371.900826446, C(b, "Tying.Cleats.Bearing").Resistance);
        Equal(1973265.6449623995, C(b, "Tying.Cleats.BlockCase1").Resistance);
        Equal(2097360.0952084726, C(b, "Tying.Cleats.BlockCase2").Resistance);
        foreach (var c in a.Checks.Where(c => c.Id.StartsWith("Tying.Beam"))) Equal(c.Resistance, C(b, c.Id).Resistance);
        Equal(275, T(b, "Material.CleatFy")); Equal(410, T(b, "Material.CleatFu"));
        Equal(T(a, "Tying.Cleats.BlockCase1.ShearYieldTerm"), T(b, "Tying.Cleats.BlockCase1.ShearYieldTerm"));
    }

    [TestMethod]
    public void ActualWebUltimateStrengthDoesNotAlterTheCleatOrBoltCapacities()
    {
        var a = Verify(); var b = Verify(Geometry(beam: Beam(fu: 410)));
        Equal(609917.3553719008, C(b, "Tying.BeamWeb.Bearing").Resistance);
        Equal(899689.0909090907, C(b, "Tying.BeamWeb.NetTension").Resistance);
        Equal(887969.5402330798, C(b, "Tying.BeamWeb.BlockCase1").Resistance);
        foreach (var c in a.Checks.Where(c => !c.Id.StartsWith("Tying.BeamWeb"))) Equal(c.Resistance, C(b, c.Id).Resistance);
        Equal(T(a, "Tying.BeamWeb.BlockCase1.ShearYieldTerm"), T(b, "Tying.BeamWeb.BlockCase1.ShearYieldTerm"));
    }

    [DataTestMethod] [DataRow(58.0, 37.0)] [DataRow(37.0, 58.0)]
    public void HardwareFitsAtTheActualRootClearanceBoundary(double contact, double beamHardware)
    {
        var r = Verify(Geometry(contact: contact, beamHardware: beamHardware));
        Assert.AreEqual(ConnectionStatus.Passed, r.CoveredComponentsStatus, string.Join(";", r.Diagnostics));
    }

    [DataTestMethod] [DataRow(58.00000001, 37.0)] [DataRow(37.0, 58.00000001)]
    [DataRow(21.99999999, 37.0)] [DataRow(37.0, 21.99999999)]
    public void HardwareOutsideRootOrHoleEnvelopeIsRejected(double contact, double beamHardware)
        => Unsupported(Verify(Geometry(contact: contact, beamHardware: beamHardware)),
            "HARDWARE_INTERSECTS_ROOT_EDGE_OR_OTHER_HARDWARE");

    [DataTestMethod]
    [DataRow("case", "ACCIDENTAL_TYING_DESIGN_SITUATION_REQUIRED")]
    [DataRow("factor", "BLOCK_YIELD_GAMMA_M0_SOURCE_REQUIRED")]
    [DataRow("geometry", "ACTUAL_DOUBLE_ANGLE_GEOMETRY_SOURCE_REQUIRED")]
    [DataRow("material", "ACTUAL_PRODUCT_MATERIAL_SOURCE_REQUIRED")]
    [DataRow("hardware", "ACTUAL_CONTACT_HARDWARE_SOURCE_REQUIRED")]
    public void MissingProvenancePreventsPartialFavourableChecks(string missing, string diagnostic)
    {
        var r = missing switch {
            "case" => Verify(caseSource: " "), "factor" => Verify(gammaM0Source: " "),
            "geometry" => Verify(Geometry(geometrySource: " ")),
            "material" => Verify(Geometry(materialSource: " ")),
            _ => Verify(Geometry(hardwareSource: " ")) };
        Unsupported(r, diagnostic);
    }

    [DataTestMethod] [DataRow(1)] [DataRow(7)]
    public void RowCountsOutsideQualifiedFamilyDoNotUseTheFormulas(int rows)
        => Unsupported(Verify(Geometry(rows: rows)), "STANDARD_IDENTICAL_L90X90X10_DETAIL_ONLY");

    [DataTestMethod] [DataRow(100.0, 10.0, 11.0)] [DataRow(90.0, 12.0, 11.0)] [DataRow(90.0, 10.0, 12.0)]
    public void DifferentAngleGeometryIsNotSilentlyTreatedAsTheSourceProduct(double width, double thickness, double radius)
        => Unsupported(Verify(Geometry(leg: new ConnectionPlate(width, thickness, 275, 430), root: radius)),
            "STANDARD_IDENTICAL_L90X90X10_DETAIL_ONLY");

    [TestMethod]
    public void NotchedBeamDoesNotAcquireUnnotchedWebBlockQualification()
        => Unsupported(Verify(Geometry(notched: true)), "NOTCHED_BEAM_NOT_QUALIFIED");

    [DataTestMethod] [DataRow(355.0, 430.0)] [DataRow(275.0, 450.0)]
    public void StrengthsOutsideExplicitInitialMaterialScopeAreRejected(double fy, double fu)
        => Unsupported(Verify(Geometry(leg: new ConnectionPlate(90, 10, fy, fu))), "INITIAL_S275_MATERIAL_THICKNESS_SCOPE");

    [TestMethod]
    public void BeamThicknessOutsideInitialMaterialScopeIsRejected()
        => Unsupported(Verify(Geometry(beam: Beam(tw: 17))), "INITIAL_S275_MATERIAL_THICKNESS_SCOPE");

    [TestMethod]
    public void ThinWebMustStillMeetPhysicalPitchLimit()
        => Unsupported(Verify(Geometry(beam: Beam(tw: 4))), "BOLT_END_EDGE_OR_PITCH_OUTSIDE_DETAIL_SCOPE");

    [DataTestMethod] [DataRow(21.0, ConnectionBoltGrade.Class8_8, true)]
    [DataRow(22.0, ConnectionBoltGrade.Class10_9, true)] [DataRow(22.0, ConnectionBoltGrade.Class8_8, false)]
    public void DifferentBoltAndHoleAssumptionsAreNotSubstituted(double hole, ConnectionBoltGrade grade, bool threaded)
        => Unsupported(Verify(Geometry(bolt: new ConnectionBolt(20, hole, 245, grade, threaded))), "STANDARD_M20_8_8_ONLY");

    [TestMethod]
    public void BeamAboveInitialHeightLimitIsNotQualified()
        => Unsupported(Verify(Geometry(beam: Beam(650))), "CLEAT_INTERSECTS_BEAM_ROOT_OR_BEAM_TOO_DEEP");

    [TestMethod]
    public void CleatCannotIntersectBeamRoot()
        => Unsupported(Verify(Geometry(beam: Beam(tf: 30, radius: 24))), "CLEAT_INTERSECTS_BEAM_ROOT_OR_BEAM_TOO_DEEP");

    [TestMethod]
    public void ShortCleatCannotBypassSourceDetailHeightRequirement()
        => Unsupported(Verify(Geometry(rows: 2, beam: Beam(300))), "CLEAT_HEIGHT_OUTSIDE_STANDARD_SBE_DETAIL");

    [TestMethod]
    public void NonFiniteTransportDoesNotPublishAnIncompleteFavourableResistance()
    {
        var r = Verify(actions: new PlanarConnectionActions(1e308, 0, 0, 0, 0, 0, 0, 1e308));
        Unsupported(r, "Actions.CentredMz.OUTSIDE_NUMERIC_RANGE"); Assert.IsNull(r.ActionsAtCleatCentre);
    }

    [TestMethod]
    public void OverflowInBlockYieldFactorSuppressesAllPartialChecks()
    {
        var r = Verify(gammaM0: 1e-308);
        Unsupported(r, "Tying.Cleats.BlockCase1.ShearYieldTerm.OUTSIDE_NUMERIC_RANGE");
    }

    [TestMethod]
    public void InvalidScalarAndNullContractsFailAtConstruction()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SbeAccidentalTyingPolicy.SbeMs5("case", 0, "source"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SbeAccidentalTyingPolicy.SbeMs5("case", double.NaN, "source"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Geometry(contact: double.PositiveInfinity));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Geometry(rows: 0));
        Assert.ThrowsException<ArgumentNullException>(() => DoubleAngleCleatTyingComponents.Verify(null!));
        var policy = SbeAccidentalTyingPolicy.SbeMs5("case", 1, "source");
        var actions = new PlanarConnectionActions(370000, 0, 0, 0, 0, 0);
        Assert.ThrowsException<ArgumentNullException>(() => new DoubleAngleCleatTyingInput(null!, Geometry(), actions));
        Assert.ThrowsException<ArgumentNullException>(() => new DoubleAngleCleatTyingInput(policy, null!, actions));
        Assert.ThrowsException<ArgumentNullException>(() => new DoubleAngleCleatTyingInput(policy, Geometry(), null!));
    }

    [TestMethod]
    public void ResultKeepsTheSameImmutableInputAndReadOnlyDiagnosticsAndChecks()
    {
        var geometry = Geometry(); var r = Verify(geometry); Assert.AreSame(geometry, r.Input.Geometry);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionCheck>)r.Checks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)r.PendingChecks).Add("erase-support"));
        var input = r.Input; var second = DoubleAngleCleatTyingComponents.Verify(input);
        Assert.AreSame(input, second.Input); Equal(r.ComponentTyingResistance!.Value, second.ComponentTyingResistance!.Value);
        Equal(370000, input.Actions.Fx); Equal(0, input.Actions.Mz);
    }
}
