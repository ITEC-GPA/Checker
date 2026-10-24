using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class FlexibleEndPlateTyingSupportWebTests
{
    private static ConnectionRolledISection Section(double h = 320.5, double b = 309.2,
        double tw = 13.8, double tf = 21.7, double r = 15.2, double fy = 275, double fu = 410)
        => new(h, b, tw, tf, r, 2 * b * tf + (h - 2 * tf) * tw + (4 - Math.PI) * r * r, fy, fu);
    private static FlexibleEndPlateTyingInput Tying(double force = 175000, int rows = 4,
        double gauge = 90, PlanarConnectionActions? actions = null)
    {
        double hp = 80 + (rows - 1) * 70, h = rows == 4 ? 412.8 : hp + 80;
        var beam = Section(h, 179.5, 9.5, 16, 10.2);
        var g = new FlexibleEndPlateTyingGeometry(new(gauge + 60, 10, 275, 410),
            new(20, 22, 245, ConnectionBoltGrade.Class8_8, true), beam, rows, gauge, 4.24, 33,
            "P358 partial-depth geometry: example2 for four rows, explicit synthetic sensitivity otherwise", "actual M20 head/nut33");
        return new(SciAccidentalTyingPolicy.P3582014("ACC-T1: separate SCI robustness tying"), g,
            actions ?? new(0, 0, force, 0, 0, 0, g.PlateCentreX, g.PlateCentreY, g.PlateCentreZ));
    }
    private static FlexibleEndPlateTyingSupportWebInput Input(FlexibleEndPlateTyingInput? tying = null,
        ConnectionRolledISection? support = null, double top = 2500, double bottom = 2290,
        PlanarConnectionActions? existing = null, bool opposite = false, bool holes = false,
        bool openings = false, bool stiffeners = false, string combination = "ACC-T1:tying and explicit zero pre-existing support actions",
        string geometry = "UKC305x305x137 actual section and finite support extents",
        string model = "Ideal continuous interior web between the rolled roots; finite boundaries not qualified")
        => new(tying ?? Tying(), support ?? Section(), top, bottom, existing ?? new(0, 0, 0, 0, 0, 0),
            opposite, holes, openings, stiffeners, combination, geometry, model);
    private static double T(FlexibleEndPlateTyingSupportWebResult r, string id) => r.Trace.Single(v => v.Name == id).Value;
    private static void Equal(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-10));
    private static void Unsupported(FlexibleEndPlateTyingSupportWebResult r, string diagnostic)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.LocalModelStatus); Assert.IsFalse(r.IsLocalModelCalculated);
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsFalse(r.IsSupportBoundaryCompatibilityVerified);
        Assert.IsNull(r.LocalUtilization); Assert.IsNull(r.LocalTyingResistance); Assert.AreEqual(0, r.Checks.Count);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), diagnostic);
        Assert.IsTrue(r.Trace.All(v => double.IsFinite(v.Value)));
    }

    [TestMethod]
    public void PublishedP358ExampleMatchesIndependentExactArithmeticAndSourceRounding()
    {
        // P358 pp29,48-49 and derive_tying_weld_support_checks.py, no Checker imports.
        var r = FlexibleEndPlateTyingSupportWeb.Verify(Input());
        Assert.AreEqual(ConnectionStatus.Passed, r.LocalModelStatus, string.Join(";", r.Diagnostics));
        Equal(246.7, T(r, "Support.ClearWeb")); Equal(.36481556546412647, T(r, "Support.Beta1"));
        Equal(.08917713822456426, T(r, "Support.Gamma1")); Equal(.6728820429671666, T(r, "Support.Eta1"));
        Equal(17745.545454545456, T(r, "Support.UnitPlasticMoment"));
        Equal(405388.3238919048, r.LocalTyingResistance!.Value);
        Equal(.43168485544902635, r.LocalUtilization!.Value);
        Assert.AreEqual(405000, r.LocalTyingResistance.Value, 500); // source prints405kN
        Assert.AreEqual("Nmm/mm", r.Trace.Single(v => v.Name == "Support.UnitPlasticMoment").Unit);
        Assert.IsFalse(r.IsSupportBoundaryCompatibilityVerified); Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void IndependentOfficialSds2ReportConfirmsTheLocalWebCalculationWithinPrintingPrecision()
    {
        // Official SDS2 Partial_depth_end_plate-tying-cube-report.pdf pp20-21,
        // v2025.07,13March2025: R405.4kN; ratio0.431673 uses rounded displayed capacity.
        var r = FlexibleEndPlateTyingSupportWeb.Verify(Input());
        Assert.AreEqual(405400, r.LocalTyingResistance!.Value, 50);
        Assert.AreEqual(.431673, r.LocalUtilization!.Value, .00002);
    }

    [TestMethod]
    public void WebThicknessEntersQuadraticallyRatherThanAsAShearArea()
    {
        var baseline = FlexibleEndPlateTyingSupportWeb.Verify(Input());
        var thin = FlexibleEndPlateTyingSupportWeb.Verify(Input(support: Section(tw: 12)));
        Equal(306531.81390692235, thin.LocalTyingResistance!.Value);
        Equal(Math.Pow(12 / 13.8, 2), thin.LocalTyingResistance.Value / baseline.LocalTyingResistance!.Value);
    }

    [DataTestMethod] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)]
    public void EachStandardRowCountSubtractsAllHalfHoleLengthsFromTheYieldLineParameter(int rows)
    {
        var p = Input(Tying(rows: rows)); var r = FlexibleEndPlateTyingSupportWeb.Verify(p);
        Assert.AreEqual(ConnectionStatus.Passed, r.LocalModelStatus, string.Join(";", r.Diagnostics));
        Equal(((rows - 1) * 70 - rows * 11) / 246.7, T(r, "Support.Eta1"));
        Equal(rows, p.Tying.Geometry.Rows);
    }

    [TestMethod]
    public void WiderPhysicalGaugeChangesTheYieldLineMechanismWithoutSubstitutingPlateWidth()
    {
        var r = FlexibleEndPlateTyingSupportWeb.Verify(Input(Tying(gauge: 140)));
        Assert.AreEqual(ConnectionStatus.Passed, r.LocalModelStatus);
        Equal(140 / 246.7, T(r, "Support.Beta1"));
        double b = 140 / 246.7, y = 22 / 246.7, e = 166 / 246.7;
        Equal(8 * 17745.545454545456 / (1 - b) * (e + 1.5 * Math.Sqrt(1 - b) * Math.Sqrt(1 - y)), r.LocalTyingResistance!.Value);
    }

    [DataTestMethod] [DataRow(.999999, ConnectionStatus.Passed)] [DataRow(1.000001, ConnectionStatus.Failed)]
    public void LocalResistanceBoundaryIsIndependentOfAnAlreadyFailedPlate(double factor, ConnectionStatus expected)
    {
        var r = FlexibleEndPlateTyingSupportWeb.Verify(Input(Tying(force: 405388.3238919048 * factor)));
        Assert.AreEqual(expected, r.LocalModelStatus); Equal(factor, r.LocalUtilization!.Value);
        Assert.AreEqual(ConnectionStatus.Failed, r.PlateAndBeamAssessment.Status);
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void AFailingThinSupportCannotBeHiddenByPassingPlateAndBeamChecks()
    {
        var r = FlexibleEndPlateTyingSupportWeb.Verify(Input(Tying(force: 340000), support: Section(tw: 12)));
        Assert.AreEqual(ConnectionStatus.Passed, r.PlateAndBeamAssessment.Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.LocalModelStatus);
        Equal(340000 / 306531.81390692235, r.LocalUtilization!.Value);
    }

    [TestMethod]
    public void PassingLocalSupportDoesNotOverrideFailedPlateOrQualifyFiniteColumn()
    {
        var r = FlexibleEndPlateTyingSupportWeb.Verify(Input(Tying(force: 400000)));
        Assert.AreEqual(ConnectionStatus.Passed, r.LocalModelStatus);
        Assert.AreEqual(ConnectionStatus.Failed, r.PlateAndBeamAssessment.Status);
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsFalse(r.IsSupportBoundaryCompatibilityVerified);
    }

    [TestMethod]
    public void ZeroTyingStillLeavesTheFiniteBoundaryAndGlobalChecksOpen()
    {
        var r = FlexibleEndPlateTyingSupportWeb.Verify(Input(Tying(force: 0)));
        Assert.AreEqual(ConnectionStatus.Passed, r.LocalModelStatus); Equal(0, r.LocalUtilization!.Value);
        CollectionAssert.Contains(r.PendingChecks.ToArray(), "SUPPORT_FINITE_BOUNDARIES_AND_YIELD_LINE_COMPATIBILITY");
        CollectionAssert.Contains(r.PendingChecks.ToArray(), "GLOBAL_ROBUSTNESS_AND_DEFORMATION_REQUIREMENTS");
    }

    [TestMethod]
    public void PhysicalApplicationPointMovesToWebMidplaneWithCorrectForceAndNoArtificialCouple()
    {
        var g = Tying().Geometry;
        var a = new PlanarConnectionActions(0, 0, 175000, 0, 0, 0, 0, g.PlateCentreY, 1005);
        var r = FlexibleEndPlateTyingSupportWeb.Verify(Input(Tying(actions: a)));
        Assert.AreEqual(ConnectionStatus.Passed, r.LocalModelStatus);
        Equal(-6.9, r.ActionsAtSupportWebMidplane.ApplicationZ);
        Equal(g.PlateCentreY, r.ActionsAtSupportWebMidplane.ApplicationY);
        Equal(175000, r.ActionsAtSupportWebMidplane.Fz);
        Equal(0, r.ActionsAtSupportWebMidplane.Mx); Equal(0, r.ActionsAtSupportWebMidplane.My); Equal(0, r.ActionsAtSupportWebMidplane.Mz);
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    public void EveryPreExistingConcomitantActionRemainsExplicitAndRequiresAnInteractionModel(int term)
    {
        var v = new double[6]; v[term] = 1e-12;
        var a = new PlanarConnectionActions(v[0], v[1], v[2], v[3], v[4], v[5], 10, 20, 30);
        var r = FlexibleEndPlateTyingSupportWeb.Verify(Input(existing: a));
        Unsupported(r, "PREEXISTING_SUPPORT_ACTIONS_REQUIRE_INTERACTION_MODEL");
        Assert.AreSame(a, r.Input.ExistingSupportActions);
        Equal(1e-12, T(r, "ExistingSupport." + new[] { "Fx", "Fy", "Fz", "Mx", "My", "Mz" }[term]));
        Equal(10, T(r, "ExistingSupport.ApplicationX")); Equal(20, T(r, "ExistingSupport.ApplicationY"));
        Equal(30, T(r, "ExistingSupport.ApplicationZ"));
    }

    [TestMethod]
    public void ASecondFaceIsRejectedEvenWithoutAnExplicitOppositeForce()
        => Unsupported(FlexibleEndPlateTyingSupportWeb.Verify(Input(opposite: true)), "TWO_FACE_TYING_REQUIRES_SEPARATE_MODEL");

    [DataTestMethod] [DataRow("holes")] [DataRow("opening")] [DataRow("stiffener")]
    public void DeclaredPhysicalInterferenceCannotBeOverriddenByAFreeTextModelSource(string feature)
        => Unsupported(FlexibleEndPlateTyingSupportWeb.Verify(Input(holes: feature == "holes",
            openings: feature == "opening", stiffeners: feature == "stiffener", model: "claimed compatible")),
            "SUPPORT_WEB_INTERFERENCE_REQUIRES_SEPARATE_MODEL");

    [DataTestMethod] [DataRow(39.999999, 2500.0)] [DataRow(2500.0, 39.999999)]
    public void ActualPlateFootprintMustRemainOnTheSupport(double top, double bottom)
        => Unsupported(FlexibleEndPlateTyingSupportWeb.Verify(Input(top: top, bottom: bottom)), "SUPPORT_END_INTERSECTS_PLATE_FOOTPRINT");

    [TestMethod]
    public void FortyMillimetreFootprintLimitIsNotPromotedToAYieldLineBoundaryProof()
    {
        var shortPanel = FlexibleEndPlateTyingSupportWeb.Verify(Input(top: 40, bottom: 40));
        var longPanel = FlexibleEndPlateTyingSupportWeb.Verify(Input(top: 2500, bottom: 2290));
        Assert.AreEqual(ConnectionStatus.Passed, shortPanel.LocalModelStatus);
        Equal(shortPanel.LocalTyingResistance!.Value, longPanel.LocalTyingResistance!.Value);
        Equal(40, T(shortPanel, "Support.TopBoltEdge")); Equal(40, T(shortPanel, "Support.BottomBoltEdge"));
        Assert.IsFalse(shortPanel.IsSupportBoundaryCompatibilityVerified);
        Assert.IsFalse(longPanel.IsSupportBoundaryCompatibilityVerified);
        CollectionAssert.Contains(shortPanel.PendingChecks.ToArray(), "SUPPORT_FINITE_BOUNDARIES_AND_YIELD_LINE_COMPATIBILITY");
    }

    [DataTestMethod] [DataRow(265.0, 410.0)] [DataRow(275.0, 430.0)] [DataRow(355.0, 510.0)]
    public void SupportMaterialCannotBeSilentlyTreatedAsTheQualifiedS275Properties(double fy, double fu)
        => Unsupported(FlexibleEndPlateTyingSupportWeb.Verify(Input(support: Section(fy: fy, fu: fu))), "INITIAL_SUPPORT_S275_FY275_FU410_SCOPE");

    [TestMethod]
    public void ActualPlateIntersectingTheRolledRootIsRejectedBeforeSingularMechanismArithmetic()
        => Unsupported(FlexibleEndPlateTyingSupportWeb.Verify(Input(support: Section(h: 200))), "PLATE_OR_HARDWARE_INTERSECTS_SUPPORT_ROOTS");

    [TestMethod]
    public void BeamCentroidForceCannotBeMistakenForPlateCentredTying()
        => Unsupported(FlexibleEndPlateTyingSupportWeb.Verify(Input(Tying(actions: new(0, 0, 175000, 0, 0, 0)))),
            "PlateAndBeam:CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");

    [DataTestMethod] [DataRow("combination", "ACCIDENTAL_ACTION_COMBINATION_SOURCE_REQUIRED")]
    [DataRow("geometry", "SUPPORT_GEOMETRY_SOURCE_REQUIRED")]
    [DataRow("model", "INTERIOR_WEB_MODEL_SOURCE_REQUIRED")]
    public void MissingConcomitantOrPhysicalEvidenceFailsClosed(string field, string diagnostic)
        => Unsupported(FlexibleEndPlateTyingSupportWeb.Verify(Input(combination: field == "combination" ? " " : "known",
            geometry: field == "geometry" ? " " : "known", model: field == "model" ? " " : "known")), diagnostic);

    [TestMethod]
    public void NumericOverflowCannotBecomeAnInfiniteFavourableResistance()
    {
        var huge = Section(4e153, 3e153, 1e153, 1e153, 0);
        Unsupported(FlexibleEndPlateTyingSupportWeb.Verify(Input(support: huge)), "SUPPORT_RESISTANCE_OUTSIDE_NUMERIC_RANGE");
    }

    [DataTestMethod] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)] [DataRow(0.0)] [DataRow(-1.0)]
    public void InvalidEndDistancesAreRejectedAtBoundary(double value)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(top: value));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(bottom: value));
    }

    [TestMethod]
    public void NoFavourableDefaultIsAvailableForExistingActionsOrPhysicalPresenceFlags()
    {
        var constructor = typeof(FlexibleEndPlateTyingSupportWebInput).GetConstructors().Single();
        Assert.IsTrue(constructor.GetParameters().All(p => !p.HasDefaultValue));
        Assert.ThrowsException<ArgumentNullException>(() => new FlexibleEndPlateTyingSupportWebInput(Tying(), Section(), 40, 40,
            null!, false, false, false, false, "case", "geometry", "model"));
        Assert.ThrowsException<ArgumentNullException>(() => FlexibleEndPlateTyingSupportWeb.Verify(null!));
    }

    [TestMethod]
    public void OneConcomitantCaseAndImmutableSnapshotsArePreservedWithoutAnEnvelope()
    {
        var p = Input(combination: "ACC-T17 actual concomitant case");
        var a = FlexibleEndPlateTyingSupportWeb.Verify(p); var b = FlexibleEndPlateTyingSupportWeb.Verify(p);
        Assert.AreSame(p, a.Input); Assert.AreSame(p.Tying, a.PlateAndBeamAssessment.Input);
        Assert.AreEqual("ACC-T17 actual concomitant case", a.Input.ActionCombinationSource);
        Equal(175000, p.Tying.Actions.Fz); Equal(13.8, p.Support.WebThickness);
        Equal(a.LocalTyingResistance!.Value, b.LocalTyingResistance!.Value);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionCheck>)a.Checks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)a.PendingChecks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)a.Diagnostics).Add("changed"));
    }
}
