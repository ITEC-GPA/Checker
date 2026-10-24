using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class FlexibleEndPlateTyingWeldTests
{
    private static FlexibleEndPlateTyingInput Tying(double throat = 4.24, double force = 175000,
        PlanarConnectionActions? actions = null, string policy = "SCI P358 example2 accidental tying case")
    {
        const double h = 412.8, b = 179.5, tw = 9.5, tf = 16, r = 10.2;
        var beam = new ConnectionRolledISection(h, b, tw, tf, r,
            2 * b * tf + (h - 2 * tf) * tw + (4 - Math.PI) * r * r, 275, 410);
        var g = new FlexibleEndPlateTyingGeometry(new(150, 10, 275, 410),
            new(20, 22, 245, ConnectionBoltGrade.Class8_8, true), beam, 4, 90, throat, 33,
            "P358 example2 partial-depth geometry", "M20 head/nut actual across-points33");
        return new(SciAccidentalTyingPolicy.P3582014(policy), g, actions ?? new(0, 0, force, 0, 0, 0,
            g.PlateCentreX, g.PlateCentreY, g.PlateCentreZ));
    }
    private static FlexibleEndPlateTyingWeldInput Input(FlexibleEndPlateTyingInput? tying = null,
        FilletWeldEndCondition ends = FilletWeldEndCondition.FullSize,
        string consumable = "Compatible S275 consumable per actual execution specification",
        string detail = "Two equal90degree continuous full-height full-size welds on beam-web faces",
        string combination = "ACC-T1: Fz175kN at actual plate centre")
        => new(tying ?? Tying(), ends, consumable, detail, combination);
    private static double T(FlexibleEndPlateTyingWeldResult r, string id) => r.Trace.Single(v => v.Name == id).Value;
    private static ConnectionCheck C(FlexibleEndPlateTyingWeldResult r, string id) => r.Checks.Single(c => c.Id == id);
    private static void Equal(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-10));
    private static void Unsupported(FlexibleEndPlateTyingWeldResult r, string diagnostic)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.IsFalse(r.IsComponentAssessmentComplete); Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.AreEqual(0, r.Checks.Count); Assert.IsNull(r.Utilization); Assert.IsNull(r.LoadUtilization);
        Assert.IsNull(r.WeldTyingResistance); CollectionAssert.Contains(r.Diagnostics.ToArray(), diagnostic);
        Assert.IsTrue(r.Trace.All(v => double.IsFinite(v.Value)));
    }

    [TestMethod]
    public void PublishedExampleAndIndependentAppendixC1DerivationHaveDifferentExplicitThroatRequirements()
    {
        // Source: P358 pp48/279; independent Python derivator, no Checker imports.
        var r = FlexibleEndPlateTyingWeld.Verify(Input());
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.Diagnostics));
        Equal(3.8, .4 * r.Input.Tying.Geometry.Beam.WebThickness); // published simplified Check13 only
        Equal(4.212782672121012, C(r, "Tying.Weld.FullStrengthWebYieldThroat").Demand);
        Equal(4.24, C(r, "Tying.Weld.FullStrengthWebYieldThroat").Resistance);
        Equal(50.31867546667445, T(r, "Weld.SigmaPerpendicular"));
        Equal(50.31867546667445, T(r, "Weld.TauPerpendicular")); Equal(0, T(r, "Weld.TauParallel"));
        Equal(100.6373509333489, C(r, "Tying.Weld.DirectionalEquivalent").Demand);
        Equal(438.50267379679144, C(r, "Tying.Weld.DirectionalEquivalent").Resistance);
        Equal(335.45454545454544, C(r, "Tying.Weld.NormalTension").Resistance);
        Equal(762519.7523855858, r.WeldTyingResistance!.Value);
        Equal(1166655.2211499463, T(r, "Weld.NormalResistance"));
        Equal(.22950225151873468, r.LoadUtilization!.Value);
        Equal(.993580818896465, r.Utilization!.Value);
        Assert.AreEqual("Tying.Weld.FullStrengthWebYieldThroat", r.GoverningCheck.Id);
    }

    [TestMethod]
    public void WebYieldHierarchyIsNotRenamedAsUltimateWebOrWholeJointDuctility()
    {
        var r = FlexibleEndPlateTyingWeld.Verify(Input());
        Equal(757625, T(r, "BeamWeb.YieldForce"));
        Equal(1026863.6363636362, T(r, "BeamWeb.UltimateForce"));
        Assert.IsTrue(r.WeldTyingResistance!.Value > T(r, "BeamWeb.YieldForce"));
        Assert.IsTrue(r.WeldTyingResistance.Value < T(r, "BeamWeb.UltimateForce"));
        Equal(380519.756529428, T(r, "Plate.Mode1Resistance"));
        Equal(.49902937640493983, T(r, "Plate.Mode1ToWeldResistanceRatio"));
        Assert.IsFalse(r.CanQualifyWholeJoint);
        StringAssert.Contains(r.Scope, "No ordinary N+V");
        CollectionAssert.Contains(r.PendingChecks.ToArray(), "GLOBAL_ROBUSTNESS_AND_DEFORMATION_REQUIREMENTS");
    }

    [TestMethod]
    public void DirectTyingPassCannotHideFailureOfSourceYieldHierarchy()
    {
        var r = FlexibleEndPlateTyingWeld.Verify(Input(Tying(throat: 3)));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.IsComponentAssessmentComplete);
        Assert.IsFalse(C(r, "Tying.Weld.FullStrengthWebYieldThroat").Passed);
        Assert.IsTrue(C(r, "Tying.Weld.DirectionalEquivalent").Passed);
        Assert.IsTrue(C(r, "Tying.Weld.NormalTension").Passed);
        Assert.IsTrue(r.LoadUtilization!.Value < 1);
    }

    [DataTestMethod] [DataRow(.999999, ConnectionStatus.Failed)] [DataRow(1.000001, ConnectionStatus.Passed)]
    public void YieldThroatBoundaryChangesStatusWithoutChangingTheAppliedCase(double factor, ConnectionStatus expected)
    {
        var r = FlexibleEndPlateTyingWeld.Verify(Input(Tying(throat: 4.212782672121012 * factor)));
        Assert.AreEqual(expected, r.Status); Equal(175000, r.Input.Tying.Actions.Fz);
        Equal(1 / factor, C(r, "Tying.Weld.FullStrengthWebYieldThroat").Utilization);
    }

    [DataTestMethod] [DataRow(.999999, ConnectionStatus.Passed)] [DataRow(1.000001, ConnectionStatus.Failed)]
    public void DirectResistanceBoundaryIsEvaluatedEvenWhenAnotherComponentHasAlreadyFailed(double factor, ConnectionStatus expected)
    {
        var r = FlexibleEndPlateTyingWeld.Verify(Input(Tying(force: 762519.7523855858 * factor)));
        Assert.AreEqual(expected, r.Status); Equal(factor, r.LoadUtilization!.Value);
        Assert.AreEqual(ConnectionStatus.Failed, r.PlateAndBeamAssessment.Status);
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void NullActionStillRequiresThePhysicalYieldStrengthDetail()
    {
        var r = FlexibleEndPlateTyingWeld.Verify(Input(Tying(force: 0)));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Equal(0, r.LoadUtilization!.Value);
        Equal(.993580818896465, r.Utilization!.Value);
    }

    [TestMethod]
    public void ExactSixMillimetreLegIsNotTheRoundedFourPointTwoFourThroat()
    {
        var r = FlexibleEndPlateTyingWeld.Verify(Input(Tying(throat: 6 / Math.Sqrt(2))));
        Equal(6 * 290 * 410 / (.85 * 1.1), r.WeldTyingResistance!.Value);
        Assert.AreNotEqual(762519.7523855858, r.WeldTyingResistance.Value);
    }

    [TestMethod]
    public void LongitudinalTranslationRetainsPhysicalWeldPlaneAndDoesNotInventACouple()
    {
        var p = Tying(); var g = p.Geometry;
        var action = new PlanarConnectionActions(0, 0, 175000, 0, 0, 0, 0, g.PlateCentreY, 1005);
        var r = FlexibleEndPlateTyingWeld.Verify(Input(Tying(actions: action)));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Equal(10, r.ActionsAtWeldCentre.ApplicationZ);
        Equal(g.PlateCentreY, r.ActionsAtWeldCentre.ApplicationY);
        Equal(0, r.ActionsAtWeldCentre.Mx); Equal(0, r.ActionsAtWeldCentre.My); Equal(0, r.ActionsAtWeldCentre.Mz);
        Equal(175000, T(r, "Actions.TransmittedFz")); Equal(-175000, T(r, "Actions.WeldReactionOnBeamFz"));
        Equal(-4.75, T(r, "Geometry.LeftRootX")); Equal(4.75, T(r, "Geometry.RightRootX"));
    }

    [TestMethod]
    public void CompensatedForceAndCoupleAreTransportedBeforeApplyingTheCentredModel()
    {
        var g = Tying().Geometry;
        var a = new PlanarConnectionActions(0, 0, 175000, 0, 8 * 175000, 0, 8, g.PlateCentreY, 5);
        var r = FlexibleEndPlateTyingWeld.Verify(Input(Tying(actions: a)));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Equal(0, r.ActionsAtWeldCentre.My);
        Equal(762519.7523855858, r.WeldTyingResistance!.Value);
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    public void TinyOutOfFieldActionIsNotDroppedToProduceAPlateWeldPass(int term)
    {
        var g = Tying().Geometry; var v = new double[6]; v[2] = 175000; v[term] = 1e-12;
        var a = new PlanarConnectionActions(v[0], v[1], v[2], v[3], v[4], v[5], 0, g.PlateCentreY, 5);
        Unsupported(FlexibleEndPlateTyingWeld.Verify(Input(Tying(actions: a))),
            "PlateAndBeam:CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");
    }

    [TestMethod]
    public void CompressiveForceDoesNotUseTheTyingWeldMethod()
        => Unsupported(FlexibleEndPlateTyingWeld.Verify(Input(Tying(force: -1))),
            "PlateAndBeam:CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");

    [DataTestMethod] [DataRow(FilletWeldEndCondition.NominalWithoutFullSizeEvidence)] [DataRow(FilletWeldEndCondition.Defective)]
    public void IncompleteEndsCannotReuseTheFullHeightFormula(FilletWeldEndCondition condition)
        => Unsupported(FlexibleEndPlateTyingWeld.Verify(Input(ends: condition)), "FULL_SIZE_WELD_ENDS_REQUIRED");

    [DataTestMethod] [DataRow("consumable", "CONSUMABLE_COMPATIBILITY_EVIDENCE_REQUIRED")]
    [DataRow("detail", "SYMMETRIC_CONTINUOUS_WELD_DETAIL_SOURCE_REQUIRED")]
    [DataRow("combination", "ACCIDENTAL_ACTION_COMBINATION_SOURCE_REQUIRED")]
    public void MissingPhysicalOrConcomitantSourceFailsClosed(string field, string diagnostic)
        => Unsupported(FlexibleEndPlateTyingWeld.Verify(Input(consumable: field == "consumable" ? " " : "known",
            detail: field == "detail" ? " " : "known", combination: field == "combination" ? " " : "known")), diagnostic);

    [TestMethod]
    public void AnOrdinaryDesignSituationCannotBeInferredFromAnAbsentAccidentalPolicySource()
        => Unsupported(FlexibleEndPlateTyingWeld.Verify(Input(Tying(policy: " "))),
            "PlateAndBeam:ACCIDENTAL_TYING_DESIGN_SITUATION_REQUIRED");

    [TestMethod]
    public void PassingWeldNeverOverridesTheSeparateFailedEndPlate()
    {
        var p = Input(Tying(force: 400000), combination: "ACC-T2:400kN");
        var r = FlexibleEndPlateTyingWeld.Verify(p);
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.PlateAndBeamAssessment.Status);
        Assert.AreSame(p.Tying, r.PlateAndBeamAssessment.Input);
        Assert.AreEqual("ACC-T2:400kN", r.Input.ActionCombinationSource);
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void SnapshotAndReadOnlyResultsSurviveRepeatedVerification()
    {
        var p = Input(); var a = FlexibleEndPlateTyingWeld.Verify(p); var b = FlexibleEndPlateTyingWeld.Verify(p);
        Assert.AreSame(p, a.Input); Assert.AreSame(p.Tying, a.PlateAndBeamAssessment.Input);
        Equal(4.24, p.Tying.Geometry.WeldThroat); Equal(175000, p.Tying.Actions.Fz);
        Equal(a.Utilization!.Value, b.Utilization!.Value);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionCheck>)a.Checks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)a.PendingChecks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)a.Diagnostics).Add("changed"));
        Assert.IsTrue(a.Trace.All(v => double.IsFinite(v.Value)));
    }

    [TestMethod]
    public void NullInputAndUndefinedEndConditionAreRejectedAtConstructionBoundary()
    {
        Assert.ThrowsException<ArgumentNullException>(() => FlexibleEndPlateTyingWeld.Verify(null!));
        Assert.ThrowsException<ArgumentNullException>(() => new FlexibleEndPlateTyingWeldInput(null!, FilletWeldEndCondition.FullSize, "a", "b", "c"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(ends: (FilletWeldEndCondition)99));
    }
}
