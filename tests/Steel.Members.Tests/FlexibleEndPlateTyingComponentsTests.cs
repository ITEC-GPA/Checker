using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class FlexibleEndPlateTyingComponentsTests
{
    private static ConnectionRolledISection Beam(double h = 412.8, double tw = 9.5, double fy = 275, double fu = 410)
        => new(h, 179.5, tw, 16, 10.2, 2 * 179.5 * 16 + (h - 32) * tw + (4 - Math.PI) * 10.2 * 10.2, fy, fu);
    private static FlexibleEndPlateTyingGeometry Geometry(int rows = 4, double gauge = 90,
        double thickness = 10, double throat = 4.24, double footprint = 33,
        ConnectionRolledISection? beam = null, bool notched = false,
        string geometrySource = "SCI P358 example2 pp44-49 partial-depth geometry, frame at beam centroid depth on support face",
        string hardwareSource = "SCI P358 p47 TableG.66: no washer, M20 across points33mm",
        ConnectionBolt? bolt = null, ConnectionPlate? plate = null)
        => new(plate ?? new ConnectionPlate(gauge + 60, thickness, 275, 410),
            bolt ?? new ConnectionBolt(20, 22, 245, ConnectionBoltGrade.Class8_8, true), beam ?? Beam(),
            rows, gauge, throat, footprint, geometrySource, hardwareSource, notched);
    private static FlexibleEndPlateTyingResult Verify(FlexibleEndPlateTyingGeometry? geometry = null,
        double force = 175000, PlanarConnectionActions? actions = null,
        string caseSource = "Independent accidental robustness tying case F175kN, SCI P358 example2")
    {
        var g = geometry ?? Geometry();
        return FlexibleEndPlateTyingComponents.Verify(new FlexibleEndPlateTyingInput(
            SciAccidentalTyingPolicy.P3582014(caseSource), g, actions ?? new PlanarConnectionActions(0, 0, force,
                0, 0, 0, g.PlateCentreX, g.PlateCentreY, g.PlateCentreZ)));
    }
    private static double T(FlexibleEndPlateTyingResult r, string name) => r.Trace.Single(v => v.Name == name).Value;
    private static ConnectionCheck C(FlexibleEndPlateTyingResult r, string id) => r.Checks.Single(v => v.Id == id);
    private static void Equal(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-10));
    private static void Unsupported(FlexibleEndPlateTyingResult r, string diagnostic)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.IsFalse(r.IsComponentAssessmentComplete); Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsNull(r.Utilization); Assert.IsNull(r.ComponentTyingResistance); Assert.AreEqual(0, r.Checks.Count);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), diagnostic);
        Assert.IsTrue(r.Trace.All(v => double.IsFinite(v.Value)));
    }

    [TestMethod]
    public void SciPublishedPartialDepthExampleMatchesIndependentUnroundedCalculation()
    {
        var r = Verify();
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.Diagnostics));
        Equal(290, T(r, "Geometry.EffectiveLength")); Equal(40, T(r, "Geometry.EffectiveEnd"));
        Equal(70, T(r, "Geometry.EffectivePitch")); Equal(35.45298759643046, T(r, "Geometry.M"));
        Equal(30, T(r, "Geometry.N")); Equal(8.25, T(r, "Geometry.ContactEw"));
        Equal(2702272.727272727, T(r, "Plate.UltimatePlasticMoment"));
        Equal(160363.63636363635, T(r, "Bolt.SingleTensileResistance"));
        Equal(380519.756529428, C(r, "Tying.EndPlate.Mode1").Resistance);
        Equal(670585.4047861958, C(r, "Tying.EndPlate.Mode2").Resistance);
        Equal(1282909.0909090908, C(r, "Tying.EndPlate.Mode3").Resistance);
        Equal(1026863.6363636362, C(r, "Tying.BeamWeb.Tension").Resistance);
        // Published values are rounded intermediate calculations, not exact oracles.
        Assert.AreEqual(381000, C(r, "Tying.EndPlate.Mode1").Resistance, 1000);
        Assert.AreEqual(670000, C(r, "Tying.EndPlate.Mode2").Resistance, 1000);
        Assert.AreEqual(1280000, C(r, "Tying.EndPlate.Mode3").Resistance, 4000);
        Assert.AreEqual(1027000, C(r, "Tying.BeamWeb.Tension").Resistance, 1000);
        Equal(380519.756529428, r.ComponentTyingResistance!.Value);
    }

    [TestMethod]
    public void ExactSixMillimetreLegIsDistinctFromPublishedRoundedFourPointTwoFourThroat()
    {
        var r = Verify(Geometry(throat: 6 / Math.Sqrt(2)));
        Equal(35.45, T(r, "Geometry.M"));
        Equal(380556.8265056462, C(r, "Tying.EndPlate.Mode1").Resistance);
        Assert.AreNotEqual(C(Verify(), "Tying.EndPlate.Mode1").Resistance, C(r, "Tying.EndPlate.Mode1").Resistance);
    }

    [DataTestMethod] [DataRow(0.0, true)] [DataRow(1e-12, true)]
    [DataRow(380519.756529428, true)] [DataRow(380520.756529428, false)]
    public void ZeroSmallAndCapacityBoundaryLoadsPreserveActualForce(double force, bool passes)
    {
        var r = Verify(force: force);
        Assert.AreEqual(passes ? ConnectionStatus.Passed : ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.Checks.All(c => c.Demand == force));
        Equal(force / 380519.756529428, r.Utilization!.Value);
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void RealPlateCentreDiffersFromBeamCentroidAndProducesAnEccentricMoment()
    {
        var g = Geometry(); Equal(11.4, g.PlateCentreY); Equal(5, g.PlateCentreZ);
        var r = Verify(g, actions: new PlanarConnectionActions(0, 0, 175000, 0, 0, 0));
        Unsupported(r, "CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");
        Equal(-1995000, r.ActionsAtPlateCentre.Mx);
        Equal(175000, r.ActionsAtPlateCentre.Fz);
    }

    [TestMethod]
    public void TranslationWithCorrectCoupleRepresentsTheSameCentredAction()
    {
        var g = Geometry(); double f = 175000, x = 8, dy = 9, dz = 123;
        var translated = Verify(g, actions: new PlanarConnectionActions(0, 0, f, -dy * f, x * f, 0,
            x, g.PlateCentreY + dy, g.PlateCentreZ + dz));
        var centred = Verify(g);
        Assert.AreEqual(ConnectionStatus.Passed, translated.Status, string.Join(";", translated.Diagnostics));
        Equal(0, translated.ActionsAtPlateCentre.Mx); Equal(0, translated.ActionsAtPlateCentre.My);
        Equal(0, translated.ActionsAtPlateCentre.Mz);
        Equal(centred.ComponentTyingResistance!.Value, translated.ComponentTyingResistance!.Value);
    }

    [TestMethod]
    public void EccentricApplicationWithoutCoupleIsNotSilentlyCentred()
    {
        var g = Geometry();
        var r = Verify(g, actions: new PlanarConnectionActions(0, 0, 175000, 0, 0, 0,
            8, g.PlateCentreY + 9, 123));
        Unsupported(r, "CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");
        Equal(1575000, r.ActionsAtPlateCentre.Mx); Equal(-1400000, r.ActionsAtPlateCentre.My);
    }

    [TestMethod]
    public void TranslationAlongForceLineDoesNotCreateAnArtificialMoment()
    {
        var g = Geometry();
        var r = Verify(g, actions: new PlanarConnectionActions(0, 0, 175000, 0, 0, 0,
            0, g.PlateCentreY, 1e6));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Equal(0, r.ActionsAtPlateCentre.Mx); Equal(0, r.ActionsAtPlateCentre.My);
    }

    [DataTestMethod] [DataRow(1e-12, 0.0, 175000.0, 0.0, 0.0, 0.0)]
    [DataRow(0.0, -1e-12, 175000.0, 0.0, 0.0, 0.0)]
    [DataRow(0.0, 0.0, -1e-12, 0.0, 0.0, 0.0)]
    [DataRow(0.0, 0.0, 175000.0, 1e-12, 0.0, 0.0)]
    [DataRow(0.0, 0.0, 175000.0, 0.0, 1e-12, 0.0)]
    [DataRow(0.0, 0.0, 175000.0, 0.0, 0.0, 1e-12)]
    public void EveryOutOfFieldActionIsRetainedEvenWhenTiny(double fx, double fy, double fz, double mx, double my, double mz)
    {
        var g = Geometry(); var r = Verify(g, actions: new PlanarConnectionActions(fx, fy, fz, mx, my, mz,
            g.PlateCentreX, g.PlateCentreY, g.PlateCentreZ));
        Unsupported(r, "CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");
        Equal(fx, r.ActionsAtPlateCentre.Fx); Equal(fy, r.ActionsAtPlateCentre.Fy); Equal(fz, r.ActionsAtPlateCentre.Fz);
    }

    [TestMethod]
    public void CombinedActionsAreTransportedWithAllThreeCrossProductComponents()
    {
        var g = Geometry();
        var r = Verify(g, actions: new PlanarConnectionActions(2, 3, 5, 7, 11, 13,
            g.PlateCentreX + 17, g.PlateCentreY + 19, g.PlateCentreZ + 23));
        Unsupported(r, "CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");
        Equal(7 + 19 * 5 - 23 * 3, r.ActionsAtPlateCentre.Mx);
        Equal(11 + 23 * 2 - 17 * 5, r.ActionsAtPlateCentre.My);
        Equal(13 + 17 * 3 - 19 * 2, r.ActionsAtPlateCentre.Mz);
    }

    [DataTestMethod] [DataRow(2, 250.0)] [DataRow(3, 320.0)]
    [DataRow(4, 412.8)] [DataRow(5, 500.0)] [DataRow(6, 570.0)]
    public void EachPermittedRowCountUsesTheWholePhysicalGroup(int rows, double beamHeight)
    {
        var r = Verify(Geometry(rows: rows, beam: Beam(beamHeight)));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.Diagnostics));
        Equal(2 * rows, T(r, "Geometry.BoltCount"));
        Equal(80 + (rows - 1) * 70, T(r, "Geometry.EffectiveLength"));
        Equal(2 * rows * 160363.63636363635, C(r, "Tying.EndPlate.Mode3").Resistance);
        Assert.AreEqual(4, r.Checks.Count, "No uniform effective per-bolt demand is invented.");
    }

    [DataTestMethod] [DataRow(90.0, 10.0)] [DataRow(90.0, 12.0)]
    [DataRow(140.0, 10.0)] [DataRow(140.0, 12.0)]
    public void StandardGaugeAndThicknessCombinationsRemainPartial(double gauge, double thickness)
    {
        var r = Verify(Geometry(gauge: gauge, thickness: thickness), force: 1000);
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.IsFalse(r.CanQualifyWholeJoint);
        CollectionAssert.Contains(r.PendingChecks.ToArray(), "WELDS_UNDER_ACCIDENTAL_TYING_AND_REQUIRED_FAILURE_HIERARCHY");
        CollectionAssert.Contains(r.PendingChecks.ToArray(), "SUPPORT_LOCAL_TYING_RESISTANCE_AND_CONTACT_COMPATIBILITY");
        Assert.IsTrue(r.Assumptions.Any(s => s.Contains("AD396")));
    }

    [TestMethod]
    public void UltimateStrengthAndDedicatedGammaMuAreTracedWithoutNationalCodeInference()
    {
        var r = Verify();
        Equal(1.1, r.Input.Policy.GammaMu); Equal(410, T(r, "Plate.UltimateStrength"));
        Assert.AreEqual("SCI.P358.2014.AccidentalTying", r.Input.Policy.MethodId);
        // It would be1993750 with ordinary fy275/gammaM0=1, not the tying2702272.727.
        Assert.AreNotEqual(1993750, T(r, "Plate.UltimatePlasticMoment"));
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.AreEqual(5, r.PendingChecks.Count);
    }

    [TestMethod]
    public void ThroatCanLimitEffectiveEndLengthAndChangesTheActualHingeDistance()
    {
        var r = Verify(Geometry(throat: 7, beam: Beam(tw: 16)));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        // Independent perturbation of P358 geometry formula, not a published example.
        Equal(38.10050506338833, T(r, "Geometry.EffectiveEnd"));
        Equal(286.2010101267767, T(r, "Geometry.EffectiveLength"));
        Equal(29.080404050710666, T(r, "Geometry.M"));
        Equal(474026.52694430255, C(r, "Tying.EndPlate.Mode1").Resistance);
    }

    [TestMethod]
    public void BeamWebCanGovernEvenWhenPlateAndBoltModesPass()
    {
        var r = Verify(Geometry(thickness: 12, throat: 3, beam: Beam(tw: 4.25)), force: 470000);
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Equal(484340.6406437805, C(r, "Tying.EndPlate.Mode1").Resistance);
        Equal(459386.3636363636, C(r, "Tying.BeamWeb.Tension").Resistance);
        Assert.IsTrue(r.Checks.Where(c => c.Id.StartsWith("Tying.EndPlate")).All(c => c.Passed));
        Assert.AreEqual("Tying.BeamWeb.Tension", r.GoverningCheck.Id);
    }

    [TestMethod]
    public void IncreasingActualContactFootprintChangesOnlyDistributedContactMode()
    {
        var a = Verify(Geometry(footprint: 33)); var b = Verify(Geometry(footprint: 37, hardwareSource: "Actual37mm washer geometry"));
        Assert.IsTrue(C(b, "Tying.EndPlate.Mode1").Resistance > C(a, "Tying.EndPlate.Mode1").Resistance);
        Equal(C(a, "Tying.EndPlate.Mode2").Resistance, C(b, "Tying.EndPlate.Mode2").Resistance);
        Equal(C(a, "Tying.EndPlate.Mode3").Resistance, C(b, "Tying.EndPlate.Mode3").Resistance);
    }

    [TestMethod]
    public void MissingCaseGeometryOrHardwareEvidencePreventsFavourableChecks()
    {
        Unsupported(Verify(caseSource: ""), "ACCIDENTAL_TYING_DESIGN_SITUATION_REQUIRED");
        Unsupported(Verify(Geometry(geometrySource: "")), "ACTUAL_PARTIAL_DEPTH_GEOMETRY_SOURCE_REQUIRED");
        Unsupported(Verify(Geometry(hardwareSource: "")), "ACTUAL_END_PLATE_CONTACT_HARDWARE_SOURCE_REQUIRED");
    }

    [TestMethod]
    public void NonstandardGeometryMaterialNotchAndHardwareAreNotSilentlyAccepted()
    {
        Unsupported(Verify(Geometry(notched: true)), "NOTCHED_BEAM_NOT_QUALIFIED");
        Unsupported(Verify(Geometry(gauge: 100)), "STANDARD_PARTIAL_DEPTH_DETAIL_REQUIRED");
        Unsupported(Verify(Geometry(thickness: 11)), "STANDARD_PARTIAL_DEPTH_DETAIL_REQUIRED");
        Unsupported(Verify(Geometry(beam: Beam(fy: 355, fu: 510))), "INITIAL_S275_MATERIAL_THICKNESS_SCOPE");
        Unsupported(Verify(Geometry(footprint: 20)), "CONTACT_HARDWARE_INTERSECTS_WELD_OR_PLATE_EDGE");
        Unsupported(Verify(Geometry(footprint: 70)), "CONTACT_HARDWARE_INTERSECTS_WELD_OR_PLATE_EDGE");
        Unsupported(Verify(Geometry(bolt: new ConnectionBolt(20, 22, 245, ConnectionBoltGrade.Class10_9, true))), "STANDARD_M20_8_8_ONLY");
    }

    [TestMethod]
    public void ActualPartialDepthLimitsExcludeTooShortOrFlangeIntersectingPlate()
    {
        Unsupported(Verify(Geometry(rows: 2)), "PLATE_HEIGHT_OUTSIDE_STANDARD_SCI_DETAIL");
        Unsupported(Verify(Geometry(rows: 6)), "PLATE_INTERSECTS_BEAM_ROOT_OR_BEAM_TOO_DEEP");
    }

    [TestMethod]
    public void TransportOverflowIsDiagnosticAndDoesNotLeakInfiniteTraceOrPartialChecks()
    {
        var g = Geometry();
        var r = Verify(g, actions: new PlanarConnectionActions(0, 0, double.MaxValue, 0, 0, 0,
            0, g.PlateCentreY + 2, 0));
        Unsupported(r, "Actions.CentredMxOutsideNumericRange"); Assert.IsNull(r.ActionsAtPlateCentre);
    }

    [TestMethod]
    public void NumericAndNullInputValidationIsExplicit()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Geometry(throat: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Geometry(footprint: double.PositiveInfinity));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Geometry(rows: 0));
        Assert.ThrowsException<ArgumentNullException>(() => new FlexibleEndPlateTyingInput(null!, Geometry(), new(0, 0, 0, 0, 0, 0)));
        Assert.ThrowsException<ArgumentNullException>(() => FlexibleEndPlateTyingComponents.Verify(null!));
    }
}
