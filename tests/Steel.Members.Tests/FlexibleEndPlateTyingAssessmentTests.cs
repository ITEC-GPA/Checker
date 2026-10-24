using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class FlexibleEndPlateTyingAssessmentTests
{
    private static ConnectionRolledISection Section(double h, double b, double tw, double tf, double r)
        => new(h, b, tw, tf, r, 2*b*tf+(h-2*tf)*tw+(4-Math.PI)*r*r, 275, 410);
    private static FlexibleEndPlateTyingInput Tying(double force = 175000, double throat = 4.24,
        PlanarConnectionActions? actions = null, string source = "SCI P358 example 2, separate accidental state")
    {
        var beam = Section(412.8,179.5,9.5,16,10.2);
        var geometry = new FlexibleEndPlateTyingGeometry(new(150,10,275,410),
            new(20,22,245,ConnectionBoltGrade.Class8_8,true),beam,4,90,throat,33,
            "SCI P358 example 2 geometry", "Actual M20 head/nut across points 33 mm");
        return new(SciAccidentalTyingPolicy.P3582014(source),geometry,
            actions ?? new(0,0,force,0,0,0,0,geometry.PlateCentreY,geometry.PlateCentreZ));
    }
    private static FlexibleEndPlateTyingWeldInput Weld(FlexibleEndPlateTyingInput p,
        FilletWeldEndCondition ends = FilletWeldEndCondition.FullSize, string detail = "Two continuous full-size equal fillets")
        => new(p,ends,"Matching S275 consumable evidence",detail,"Accidental model record for welds");
    private static FlexibleEndPlateTyingSupportWebInput Support(FlexibleEndPlateTyingInput p, double tw = 13.8,
        bool opposite = false, PlanarConnectionActions? existing = null, double edge = 2500)
        => new(p,Section(320.5,309.2,tw,21.7,15.2),edge,edge,existing ?? new(0,0,0,0,0,0),
            opposite,false,false,false,"Same accidental model, support record",
            "UKC 305x305x137 geometry; explicit sensitivity for tw", "Ideal continuous panel, finite boundaries unverified");
    private static FlexibleEndPlateTyingAssessmentResult Assess(FlexibleEndPlateTyingInput? p = null,
        double supportTw = 13.8, bool opposite = false, FilletWeldEndCondition ends = FilletWeldEndCondition.FullSize)
    { p ??= Tying(); return FlexibleEndPlateTyingAssessment.Verify(Weld(p,ends),Support(p,supportTw,opposite)); }

    [TestMethod]
    public void PublishedComponentChainKeepsEveryMechanismAndBoundaryLimitation()
    {
        var p = Tying(); var r = Assess(p);
        Assert.AreEqual(ConnectionStatus.Passed,r.CoveredComponentsStatus);
        Assert.AreEqual(8,r.Checks.Count); Assert.AreEqual(8,r.Checks.Select(x=>x.Id).Distinct().Count());
        Assert.AreSame(p,r.Welds.Input.Tying); Assert.AreSame(p,r.SupportWeb.Input.Tying);
        Assert.IsTrue(r.IsSameTyingState); Assert.IsFalse(r.HasFailedComponent);
        // Independent P358 calculations, not a recomputation through another component.
        Assert.AreEqual(380519.756529428,r.PlateAndBeam.ComponentTyingResistance!.Value,1e-6);
        Assert.AreEqual(762519.7523855858,r.Welds.WeldTyingResistance!.Value,1e-6);
        Assert.AreEqual(405388.3238919048,r.SupportWeb.LocalTyingResistance!.Value,1e-6);
        Assert.AreEqual(.993580818896465,r.CoveredComponentsUtilization!.Value,1e-12);
        Assert.AreEqual("Tying.Weld.FullStrengthWebYieldThroat",r.GoverningCheck.Id);
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsFalse(r.IsSupportBoundaryCompatibilityVerified);
        CollectionAssert.Contains(r.PendingWholeJointChecks.ToArray(),"SUPPORT_FINITE_BOUNDARIES_AND_YIELD_LINE_COMPATIBILITY");
        CollectionAssert.Contains(r.PendingWholeJointChecks.ToArray(),"ACTUAL_HEAD_NUT_WASHER_BEARING_AND_PUNCHING");
        Assert.IsFalse(r.PendingWholeJointChecks.Contains("WELDS_UNDER_ACCIDENTAL_TYING_AND_REQUIRED_FAILURE_HIERARCHY"));
    }

    [TestMethod]
    public void PassingWeldAndSupportCannotHidePlateFailure()
    {
        var r=Assess(Tying(390000));
        Assert.AreEqual(ConnectionStatus.Failed,r.PlateAndBeam.Status);
        Assert.AreEqual(ConnectionStatus.Passed,r.Welds.Status);
        Assert.AreEqual(ConnectionStatus.Passed,r.SupportWeb.LocalModelStatus);
        Assert.AreEqual(ConnectionStatus.Failed,r.CoveredComponentsStatus); Assert.IsTrue(r.HasFailedComponent);
        Assert.AreEqual("Tying.EndPlate.Mode1",r.GoverningCheck.Id);
    }

    [TestMethod]
    public void PassingPlateAndWeldCannotHideThinnerSupportFailure()
    {
        var r=Assess(Tying(350000),supportTw:12);
        Assert.AreEqual(ConnectionStatus.Passed,r.PlateAndBeam.Status);
        Assert.AreEqual(ConnectionStatus.Passed,r.Welds.Status);
        Assert.AreEqual(ConnectionStatus.Failed,r.SupportWeb.LocalModelStatus);
        Assert.AreEqual(ConnectionStatus.Failed,r.CoveredComponentsStatus);
        Assert.AreEqual(306531.81390692235,r.SupportWeb.LocalTyingResistance!.Value,1e-6);
        Assert.AreEqual("Tying.SupportWeb.InteriorYieldLines",r.GoverningCheck.Id);
    }

    [TestMethod]
    public void WeldThroatHierarchyFailureIsNotReplacedByItsPassingLoadUtilization()
    {
        var r=Assess(Tying(throat:3));
        Assert.AreEqual(ConnectionStatus.Passed,r.PlateAndBeam.Status);
        Assert.AreEqual(ConnectionStatus.Passed,r.SupportWeb.LocalModelStatus);
        Assert.IsTrue(r.Welds.LoadUtilization<1); Assert.AreEqual(ConnectionStatus.Failed,r.Welds.Status);
        Assert.AreEqual(ConnectionStatus.Failed,r.CoveredComponentsStatus); Assert.IsTrue(r.HasFailedComponent);
    }

    [DataTestMethod]
    [DataRow(FilletWeldEndCondition.NominalWithoutFullSizeEvidence)]
    [DataRow(FilletWeldEndCondition.Defective)]
    public void UnsupportedWeldHasNoFavourableAggregate(FilletWeldEndCondition ends)
    {
        var r=Assess(ends:ends);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.CoveredComponentsStatus);
        Assert.AreEqual(0,r.Checks.Count); Assert.IsNull(r.CoveredComponentsUtilization);
        CollectionAssert.Contains(r.PendingWholeJointChecks.ToArray(),"WELDS_UNDER_ACCIDENTAL_TYING_AND_REQUIRED_FAILURE_HIERARCHY");
        Assert.IsTrue(r.Diagnostics.Any(x=>x.Contains("FULL_SIZE_WELD_ENDS_REQUIRED")));
    }

    [TestMethod]
    public void UnsupportedSupportDoesNotHideAnAlreadyKnownPlateFailure()
    {
        var r=Assess(Tying(390000),opposite:true);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.CoveredComponentsStatus);
        Assert.IsTrue(r.HasFailedComponent); Assert.AreEqual(ConnectionStatus.Failed,r.PlateAndBeam.Status);
        Assert.AreEqual(0,r.Checks.Count); Assert.IsNull(r.CoveredComponentsUtilization);
    }

    [DataTestMethod]
    [DataRow(175000.0)] [DataRow(390000.0)] [DataRow(500000.0)]
    public void IndependentlyCreatedStatesCannotBeMergedEvenWhenForcesMatch(double supportForce)
    {
        var r=FlexibleEndPlateTyingAssessment.Verify(Weld(Tying()),Support(Tying(supportForce)));
        Assert.IsFalse(r.IsSameTyingState); Assert.AreEqual(ConnectionStatus.NotSupported,r.CoveredComponentsStatus);
        CollectionAssert.Contains(r.Diagnostics.ToArray(),"SAME_IMMUTABLE_TYING_INPUT_REQUIRED");
        Assert.AreEqual(0,r.Checks.Count); Assert.IsNull(r.CoveredComponentsUtilization);
        // Keep both independent results instead of silently replacing the support action.
        Assert.AreEqual(supportForce,r.SupportWeb.Input.Tying.Actions.Fz);
        Assert.AreEqual(175000,r.Welds.Input.Tying.Actions.Fz);
        Assert.AreEqual(supportForce>380519.756529428,r.HasFailedComponent);
    }

    [TestMethod]
    public void SharingOnlyGeometryAndPolicyDoesNotBindTwoActionStates()
    {
        var p=Tying(); var other=new FlexibleEndPlateTyingInput(p.Policy,p.Geometry,p.Actions);
        var r=FlexibleEndPlateTyingAssessment.Verify(Weld(p),Support(other));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.CoveredComponentsStatus);
        Assert.IsFalse(r.IsSameTyingState);
    }

    [TestMethod]
    public void SourceDescriptionsDoNotReplaceTheSharedImmutableState()
    {
        var p=Tying(); var w=Weld(p); var s=Support(p);
        Assert.AreNotEqual(w.ActionCombinationSource,s.ActionCombinationSource);
        var r=FlexibleEndPlateTyingAssessment.Verify(w,s);
        Assert.IsTrue(r.IsSameTyingState); Assert.AreEqual(ConnectionStatus.Passed,r.CoveredComponentsStatus);
        Assert.AreSame(w,r.Welds.Input); Assert.AreSame(s,r.SupportWeb.Input);
    }

    [TestMethod]
    public void ForceAtBeamCentroidRetainsItsMomentAndCannotEnterTheCentredAssembly()
    {
        var r=Assess(Tying(actions:new(0,0,175000,0,0,0)));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.CoveredComponentsStatus);
        Assert.AreEqual(-1995000,r.PlateAndBeam.ActionsAtPlateCentre.Mx,1e-6);
        Assert.AreEqual(0,r.Checks.Count); Assert.IsNull(r.CoveredComponentsUtilization);
    }

    [TestMethod]
    public void PreexistingSupportActionIsNotRemovedToMakeTheSharedStatePass()
    {
        var p=Tying(); var r=FlexibleEndPlateTyingAssessment.Verify(Weld(p),Support(p,existing:new(0,1e-200,0,0,0,0)));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.CoveredComponentsStatus);
        Assert.AreEqual(1e-200,r.SupportWeb.Input.ExistingSupportActions.Fy);
        Assert.IsTrue(r.Diagnostics.Any(x=>x.Contains("PREEXISTING_SUPPORT_ACTIONS_REQUIRE_INTERACTION_MODEL")));
    }

    [DataTestMethod]
    [DataRow(40.0)] [DataRow(250000.0)]
    public void SupportLengthNeverBecomesAnUnprovedBoundaryCertificate(double edge)
    {
        var p=Tying(); var r=FlexibleEndPlateTyingAssessment.Verify(Weld(p),Support(p,edge:edge));
        Assert.AreEqual(ConnectionStatus.Passed,r.CoveredComponentsStatus);
        Assert.IsFalse(r.IsSupportBoundaryCompatibilityVerified); Assert.IsFalse(r.CanQualifyWholeJoint);
        CollectionAssert.Contains(r.PendingWholeJointChecks.ToArray(),"SUPPORT_FINITE_BOUNDARIES_AND_YIELD_LINE_COMPATIBILITY");
    }

    [TestMethod]
    public void ZeroTyingStillChecksTheRequiredWeldHierarchy()
    {
        var r=Assess(Tying(force:0,throat:3));
        Assert.AreEqual(0,r.Welds.LoadUtilization); Assert.AreEqual(ConnectionStatus.Failed,r.CoveredComponentsStatus);
        Assert.AreEqual("Tying.Weld.FullStrengthWebYieldThroat",r.GoverningCheck.Id);
    }

    [TestMethod]
    public void AggregateListsCannotBeEditedIntoAPassingCertificate()
    {
        var r=Assess(Tying(390000));
        Assert.ThrowsException<NotSupportedException>(()=>((IList<ConnectionCheck>)r.Checks).Clear());
        Assert.ThrowsException<NotSupportedException>(()=>((IList<string>)r.PendingWholeJointChecks).Clear());
        Assert.AreEqual(ConnectionStatus.Failed,r.CoveredComponentsStatus);
    }

    [TestMethod]
    public void NullAdaptersAreRejected()
    {
        var p=Tying();
        Assert.ThrowsException<ArgumentNullException>(()=>FlexibleEndPlateTyingAssessment.Verify(null!,Support(p)));
        Assert.ThrowsException<ArgumentNullException>(()=>FlexibleEndPlateTyingAssessment.Verify(Weld(p),null!));
    }
}
