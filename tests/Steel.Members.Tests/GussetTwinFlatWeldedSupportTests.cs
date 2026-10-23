using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class GussetTwinFlatWeldedSupportTests
{
    private static ConnectionDesignCode Ec() => ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Explicit analytical factors");
    private static GussetTwinFlatTensionInput Transfer(ConnectionDesignCode? code = null, double n = 100000,
        double fy = 275, double fu = 430, PlanarConnectionActions? actions = null)
        => new(code ?? Ec(), new(200, 16, fy, fu), new(160, 10, fy, fu),
            new(20, 21, 245, ConnectionBoltGrade.Class8_8, true), actions ?? new(n, 0, 0, 0, 0, 0), 2, 60, 80, 40, 50);
    private static GussetTwinFlatWeldedSupportInput Input(GussetTwinFlatTensionInput? transfer = null,
        double distance = 120, double width = 240, double depth = 80, double throat = 6,
        FilletWeldSteelGrade grade = FilletWeldSteelGrade.S275, FilletWeldMaterial? support = null,
        FilletWeldMethod method = FilletWeldMethod.Directional,
        FilletWeldEndCondition ends = FilletWeldEndCondition.FullSize,
        string? consumable = "Explicit matching consumable", string? distribution = "External qualification of rigid support load distribution",
        string? execution = "Double-sided continuous full-size fillets, verified access")
        => new(transfer ?? Transfer(), distance, width, depth, throat, grade,
            support ?? new(FilletWeldSteelGrade.S275, 430, 20), method, ends, consumable!, distribution!, execution!);
    private static GussetTwinFlatWeldedSupportResult Verify(GussetTwinFlatWeldedSupportInput? p = null)
        => GussetTwinFlatWeldedSupport.Verify(p ?? Input());
    private static double T(GussetTwinFlatWeldedSupportResult r, string name) => r.Verification.Trace.Single(t => t.Name == name).Value;
    private static void Equal(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-11));
    private static void Unsupported(GussetTwinFlatWeldedSupportResult r, string issue)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.IsFalse(r.IsComplete); Assert.IsNull(r.Utilization);
        Assert.AreEqual(0, r.Verification.Checks.Count); Assert.IsNull(r.Verification.GoverningCheck);
        Assert.IsTrue(r.Verification.Diagnostics.Any(d => d.Contains(issue)), string.Join(";", r.Verification.Diagnostics));
    }

    [DataTestMethod]
    [DataRow(false, 672762.7422675064)] [DataRow(true, 656190.040254768)]
    public void CompleteDeclaredLoadPathMatchesIndependentBoltPlateAndWeldCalculations(bool ntc, double blockCapacity)
    {
        // Constructed full COMPONENT path, not a published support/joint example.
        // Bolts/plates: existing independent derive_gusset_twin_flat.py.
        // Weld Aw=2*6*200=2400, s=100000/Aw, sigma=|tau|=s/sqrt2,
        // equivalent=s*sqrt2, limit=430/(.85*1.25), no assumed support capacity.
        var r = Verify(Input(transfer: Transfer(ntc ? ConnectionDesignCode.Ntc2018() : Ec())));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(36, r.Verification.Checks.Count);
        Equal(2400, T(r, "SupportWeld.Area")); Equal(100000, T(r, "SupportWeld.RecoveredFz"));
        Equal(0, T(r, "SupportWeld.RecoveredFx")); Equal(0, T(r, "SupportWeld.RecoveredFy"));
        Equal(0, T(r, "SupportWeld.RecoveredMx")); Equal(0, T(r, "SupportWeld.RecoveredMy"));
        Equal(0, T(r, "SupportWeld.RecoveredMz"));
        foreach (var point in r.SupportWeld.Points)
        {
            Equal(100000.0 / 2400, point.StressZ); Equal(0, point.StressX); Equal(0, point.StressY);
            Equal(29.462782549439478, point.Verification.SigmaPerpendicular);
            Equal(29.462782549439478, Math.Abs(point.Verification.TauPerpendicular));
            Equal(0, point.Verification.TauParallel);
            Equal(58.92556509887897, point.Verification.Checks[0].Demand);
            Equal(404.70588235294116, point.Verification.Checks[0].Resistance);
        }
        Equal(.1456009602733928, r.SupportWeld.Utilization!.Value);
        Equal(blockCapacity, r.Verification.GoverningCheck!.Resistance);
        Equal(100000 / blockCapacity, r.Utilization!.Value);
        Assert.AreEqual("Transfer.Gusset.BlockInside", r.Verification.GoverningCheck.Id);
        Equal(-100000, r.ActionsOnSupport.Fx); Equal(120, r.ActionsOnSupport.ApplicationX);
    }

    [DataTestMethod]
    [DataRow(FilletWeldSteelGrade.S275, 275.0, 410.0, 273.0)]
    [DataRow(FilletWeldSteelGrade.S355, 355.0, 470.0, 295.0)]
    public void PublishedSciG37TransverseWeldStrengthIsRecoveredOnTheRealTJunction(
        FilletWeldSteelGrade grade, double fy, double fu, double published)
    {
        // SCI P358:2014 G.36/G.37, printed T-144, PDF442; published rounded MPa.
        // This benchmark is the weld component, not a published complete support.
        var r = Verify(Input(transfer: Transfer(fy: fy, fu: fu), grade: grade, support: new(grade, fu, 20)));
        Assert.IsTrue(r.IsComplete);
        double calculated = 100000 / r.SupportWeld.Utilization!.Value / 2400;
        Assert.AreEqual(published, calculated, .5);
    }

    [DataTestMethod]
    [DataRow(FilletWeldMethod.Directional, 686808.6571148395)]
    [DataRow(FilletWeldMethod.Simplified, 560776.9202858295)]
    public void BothWeldMethodsUseTheirActualStrengthWithoutChangingTransferredLoad(FilletWeldMethod method, double resistance)
    {
        var r = Verify(Input(method: method));
        Equal(resistance, 100000 / r.SupportWeld.Utilization!.Value);
        Equal(-100000, r.ActionsOnSupport.Fx); Equal(100000, T(r, "Transfer.GussetForce"));
    }

    [TestMethod]
    public void WeldRootsAndThroatNormalsRepresentOppositeSidesOfThePhysicalTJoint()
    {
        var r = Verify(); var axes = r.WeldAxes;
        Equal(120, axes.Origin.X); Equal(1, axes.V1.Y); Equal(1, axes.V2.Z); Equal(1, axes.V3.X);
        var positive = r.SupportWeld.Input.Segments[0]; var negative = r.SupportWeld.Input.Segments[1];
        Equal(8, positive.Start.Y); Equal(-8, negative.Start.Y);
        Equal(-100, positive.Start.X); Equal(100, positive.End.X);
        // In the (v=Z,w=X) cross-section the upper hypotenuse has direction(1,1),
        // lower(1,-1). Actual throat normals must be perpendicular to those planes.
        Equal(0, Math.Cos(positive.ThroatNormalAngleRadians) + Math.Sin(positive.ThroatNormalAngleRadians));
        Equal(0, Math.Cos(negative.ThroatNormalAngleRadians) - Math.Sin(negative.ThroatNormalAngleRadians));
        Assert.IsTrue(Math.Sin(positive.ThroatNormalAngleRadians) > 0);
        Assert.IsTrue(r.SupportWeld.Points[0].Verification.TauPerpendicular < 0);
        Assert.IsTrue(r.SupportWeld.Points[2].Verification.TauPerpendicular > 0);
        Equal(0, r.SupportWeld.Input.LapLength);
    }

    [TestMethod]
    public void CollinearSupportDistanceDoesNotCreateAFictitiousEccentricMoment()
    {
        var a = Verify(); var b = Verify(Input(distance: 420));
        Equal(a.Utilization!.Value, b.Utilization!.Value); Equal(0, b.ActionsOnSupport.My); Equal(0, b.ActionsOnSupport.Mz);
        Equal(420, b.ActionsOnSupport.ApplicationX);
    }

    [TestMethod]
    public void EquivalentActionAtOffsetPointRetainsConcentricResult()
    {
        var a = new PlanarConnectionActions(100000, 0, 0, 0, -900000, 800000, 7, 8, 9);
        var r = Verify(Input(transfer: Transfer(actions: a)));
        Assert.IsTrue(r.IsComplete); Equal(Verify().Utilization!.Value, r.Utilization!.Value);
        Equal(0, r.ActionsOnSupport.My); Equal(0, r.ActionsOnSupport.Mz);
    }

    [TestMethod]
    public void UnsupportedSixComponentStateStillHasAnExplicitCorrectSupportDemand()
    {
        var a = new PlanarConnectionActions(100000, 2, 3, 4, 5, 6, 7, 8, 9);
        var r = Verify(Input(transfer: Transfer(actions: a)));
        Unsupported(r, "ConcentricTensionAtGroupCentroidRequired"); Assert.IsNull(r.SupportWeld);
        Equal(-100000, r.ActionsOnSupport.Fx); Equal(-2, r.ActionsOnSupport.Fy); Equal(-3, r.ActionsOnSupport.Fz);
        Equal(-10, r.ActionsOnSupport.Mx); Equal(-900344, r.ActionsOnSupport.My); Equal(800220, r.ActionsOnSupport.Mz);
    }

    [TestMethod]
    public void WeldFailureControlsEvenWhenBoltedTransferPasses()
    {
        var r = Verify(Input(transfer: Transfer(n: 500000), throat: 3));
        Assert.AreEqual(ConnectionStatus.Passed, r.BoltedTransfer.Status); Assert.AreEqual(ConnectionStatus.Failed, r.SupportWeld.Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.Status); Assert.IsTrue(r.Verification.GoverningCheck!.Id.StartsWith("SupportWeld."));
    }

    [TestMethod]
    public void BoltedTransferFailureControlsEvenWhenWeldPasses()
    {
        var r = Verify(Input(transfer: Transfer(n: 700000), throat: 8));
        Assert.AreEqual(ConnectionStatus.Failed, r.BoltedTransfer.Status); Assert.AreEqual(ConnectionStatus.Passed, r.SupportWeld.Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.Status); Assert.IsTrue(r.Verification.GoverningCheck!.Id.StartsWith("Transfer."));
    }

    [DataTestMethod]
    [DataRow(false, 188.0)] [DataRow(true, 200.0)]
    public void NominalWeldEndTreatmentRetainsTheExistingEcNtcDifference(bool ntc, double effectiveLength)
    {
        var r = Verify(Input(transfer: Transfer(ntc ? ConnectionDesignCode.Ntc2018() : Ec()),
            ends: FilletWeldEndCondition.NominalWithoutFullSizeEvidence));
        Assert.IsTrue(r.IsComplete); Equal(effectiveLength, T(r, "SupportWeld.Segment0.EffectiveLength"));
        Equal(2 * 6 * effectiveLength, T(r, "SupportWeld.Area"));
    }

    [DataTestMethod]
    [DataRow(80.0)] [DataRow(88.0)]
    public void SupportMustLieBeyondBraceEndsAndPhysicalFilletFootprint(double distance)
        => Unsupported(Verify(Input(distance: distance)), "SupportFilletIntersectsBraceEnds");

    [DataTestMethod]
    [DataRow(199.0, 80.0, "SupportFaceDoesNotContainWeldLength")]
    [DataRow(240.0, 32.0, "SupportFaceDoesNotContainFilletLegs")]
    public void ActualSupportFaceMustContainBothWelds(double width, double depth, string issue)
        => Unsupported(Verify(Input(width: width, depth: depth)), issue);

    [TestMethod]
    public void GeometricBoundaryFitsButDoesNotCertifySupportStrength()
    {
        var r = Verify(Input(distance: 80 + 6 * Math.Sqrt(2), width: 200, depth: 16 + 12 * Math.Sqrt(2)));
        Assert.IsTrue(r.IsComplete); StringAssert.Contains(r.Verification.Scope, "support plate/member strength");
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void NominalGradeCannotUnderstateActualGussetYieldStrength()
        => Unsupported(Verify(Input(grade: FilletWeldSteelGrade.S235)), "GussetNominalGradeBelowSuppliedYieldStrength");

    [DataTestMethod]
    [DataRow("consumable", "CONSUMABLE_COMPATIBILITY_EVIDENCE_REQUIRED")]
    [DataRow("distribution", "SupportDistributionAssessmentRequired")]
    [DataRow("execution", "WeldExecutionAndAccessEvidenceRequired")]
    public void MissingQualificationInputsBlockTheAggregate(string missing, string issue)
        => Unsupported(Verify(Input(consumable: missing == "consumable" ? null : "matching consumable",
            distribution: missing == "distribution" ? null : "support load distribution",
            execution: missing == "execution" ? null : "access and ends")), issue);

    [TestMethod]
    public void KernelThicknessLimitIsPropagatedWithoutFavourableAggregateChecks()
        => Unsupported(Verify(Input(support: new(FilletWeldSteelGrade.S275, 430, 3))), "PARENT_THICKNESS_BELOW_4_MM_SCOPE");

    [TestMethod]
    public void KernelThroatLimitIsPropagatedWithoutFavourableAggregateChecks()
        => Unsupported(Verify(Input(throat: 2)), "THROAT_BELOW_3_MM_SCOPE");

    [TestMethod]
    public void DefectiveEndsAreNotAccepted()
        => Unsupported(Verify(Input(ends: FilletWeldEndCondition.Defective)), "DEFECTIVE_ENDS");

    [TestMethod]
    public void SupportMaterialCanGovernTheWeldButIsNeverAssignedAResistance()
    {
        var r = Verify(Input(support: new(FilletWeldSteelGrade.S235, 360, 20)));
        Equal(360, r.SupportWeld.GoverningPoint.Verification.Fu); Equal(.8, r.SupportWeld.GoverningPoint.Verification.Beta);
        Assert.IsTrue(r.Verification.Checks.All(k => k.Id.StartsWith("Transfer.") || k.Id.StartsWith("SupportWeld.")));
    }

    [TestMethod]
    public void ExportedWeldFrameIsADefensiveCopy()
    {
        var r = Verify(); r.WeldAxes.Origin.X = 999; r.WeldAxes.V1.X = 77;
        Equal(120, r.WeldAxes.Origin.X); Equal(0, r.WeldAxes.V1.X);
    }

    [DataTestMethod]
    [DataRow(0.0)] [DataRow(-1.0)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidSupportDistanceIsRejected(double value)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(distance: value));

    [TestMethod]
    public void NullInputIsRejected() => Assert.ThrowsException<ArgumentNullException>(() => GussetTwinFlatWeldedSupport.Verify(null!));
}
