using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateMomentComponentTests
{
    private static ConnectionRolledISection Column(double tf = 20.5)
    {
        if (tf == 20.5) return new(266.7, 258.8, 12.8, 20.5, 12.7, 13600, 265, 410);
        double h = 200.3 + 2 * tf + 2 * 12.7;
        double area = 2 * 258.8 * tf + (h - 2 * tf) * 12.8 + (4 - Math.PI) * 12.7 * 12.7;
        return new(h, 258.8, 12.8, tf, 12.7, area, 265, 410);
    }
    private static EndPlateMomentComponentInput Sci(bool extended = true, bool ntc = false,
        double moment = 300e6, double shear = 200000, double axial = 0, double beta = 0,
        double columnTf = 20.5, double plateT = 25, double columnEnd = 1000, double projection = 46.9,
        double beamMoment = 649e6, double stress = 0, string betaSource = "C1 equal/opposite beams: beta0; qualified source",
        string stressSource = "C1 declared kwc1, zero coexisting web compression in this test",
        string restraintSource = "Test restraint preventing column-sway; external qualification",
        string actionSource = "C1 one concomitant ULS action set", string beamSource = "SCI P398 C1 p103 Mc,Rd649kNm; no shear reduction",
        TStubInternalEndPlateGeometry? geometry = null, TStubExtendedEndPlateGeometry? extension = null,
        ColumnWebPanelConfiguration config = ColumnWebPanelConfiguration.DoubleSidedSimilarBeamDepths,
        string? sleSource = "Independent NTC clearance SLE qualification",
        double contact = 39.55, double? qualifiedShear = null)
    {
        var code = ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "SCI P398 C1 UK factors");
        var policy = ntc ? ColumnWebPolicy.Ntc2018(code) : ColumnWebPolicy.Eurocode3(code, 1);
        var beam = new ConnectionRolledISection(533.1, 209.3, 10.1, 15.6, 12.7, 11700, 275, 410);
        geometry ??= new(250, 258.8, 100, 10.1, 8, 12, 8, 501.9, new[] { 44.4, 134.4 },
            new(7.5, "SCI P398 C1 graphical alpha7.5"), "C1 actual dimensions, 1000mm column-end continuations are a test assumption");
        return new(policy, Column(columnTf), beam, 265, new(250, plateT, 265, 410),
            new(24, 26, 353, ConnectionBoltGrade.Class8_8, true), geometry,
            extended ? extension ?? new(250, 100, 40, 12, 50, "C1 outer row") : null!,
            columnEnd, columnEnd, projection, new(code, beam, beamMoment, qualifiedShear ?? shear, beamSource), moment, shear, axial,
            beta, stress, config, betaSource, stressSource, restraintSource, actionSource,
            TStubMode1Method.DistributedContact, contact, sleSource);
    }
    private static void Unsupported(EndPlateMomentComponentResult r, string issue)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.ComponentAssessment.Status);
        Assert.IsNull(r.MomentResistance); Assert.AreEqual(0, r.EffectiveRows.Count); Assert.AreEqual(0, r.Constraints.Count);
        Assert.IsNull(r.ComponentAssessment.Utilization);
        Assert.IsTrue(r.ComponentAssessment.Diagnostics.Any(d => d.Contains(issue)), string.Join("; ", r.ComponentAssessment.Diagnostics));
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void SciC1AssemblesTheActualGeometryWithoutPublishedIntermediateRounding()
    {
        var r = EndPlateMomentComponents.Verify(Sci());
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status, string.Join("; ", r.ComponentAssessment.Diagnostics));
        Assert.AreEqual(415771287.8777898, r.MomentResistance!.Value, .001);
        // Independent published result416kNm; exact geometry and no intermediate rounding.
        Assert.AreEqual(416e6, r.MomentResistance.Value, .6e6);
        Assert.AreEqual(3, r.EffectiveRows.Count); Assert.AreEqual(21, r.Constraints.Count);
        Assert.AreEqual(377258.6330409356, r.EffectiveRows[0].Resistance, 1e-6);
        Assert.AreEqual(321033.2204445773, r.EffectiveRows[1].Resistance, 1e-6);
        Assert.AreEqual(141567.34651448717, r.EffectiveRows[2].Resistance, 1e-6);
        Assert.AreEqual("ExternalPlate.Row1.TStub", r.EffectiveRows[0].LimitingConstraint);
        Assert.AreEqual("Column.Rows1To2.TStub", r.EffectiveRows[1].LimitingConstraint);
        Assert.AreEqual("ColumnCompression", r.EffectiveRows[2].LimitingConstraint);
        Assert.AreEqual(839859.2, r.EffectiveRows.Sum(row => row.Resistance), 1e-6);
        Assert.AreEqual(565.3, r.EffectiveRows[0].LeverArm, 1e-10);
        Assert.AreEqual(465.3, r.EffectiveRows[1].LeverArm, 1e-10);
        Assert.AreEqual(375.3, r.EffectiveRows[2].LeverArm, 1e-10);
    }

    [TestMethod]
    public void EveryColumnGroupIsCheckedAndPlateGroupsNeverCrossTheBeamFlange()
    {
        var r = EndPlateMomentComponents.Verify(Sci());
        Assert.AreEqual(12, r.Constraints.Count(c => c.Id.StartsWith("Column.")));
        var inner = r.Constraints.Where(c => c.Id.StartsWith("InternalPlate.")).ToArray();
        Assert.AreEqual(6, inner.Length); Assert.IsTrue(inner.All(c => c.FirstRow >= 1));
        Assert.AreEqual(1, r.Constraints.Count(c => c.Id.StartsWith("ExternalPlate.")));
        foreach (var c in r.Constraints)
            Assert.IsTrue(r.EffectiveRows.Skip(c.FirstRow).Take(c.RowCount).Sum(row => row.Resistance) <= c.Resistance * (1 + 1e-12), c.Id);
    }

    [TestMethod]
    public void FlushPathHasOnlyInternalRowsAndItsIndependentCapacity()
    {
        var r = EndPlateMomentComponents.Verify(Sci(extended: false, moment: 200e6));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(2, r.EffectiveRows.Count);
        Assert.AreEqual(295144242.9525269, r.MomentResistance!.Value, .001);
        Assert.AreEqual(398364.51031698566, r.EffectiveRows[0].Resistance, 1e-6);
        Assert.AreEqual(292526.6088516746, r.EffectiveRows[1].Resistance, 1e-6);
        Assert.IsFalse(r.Constraints.Any(c => c.Id.StartsWith("ExternalPlate.")));
    }

    [TestMethod]
    public void NtcUsesNationalFactorsAndExplicitSupplementaryEcMethod()
    {
        var r = EndPlateMomentComponents.Verify(Sci(ntc: true));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(398984921.78837126, r.MomentResistance!.Value, .001);
        Assert.IsTrue(r.ComponentAssessment.Checks[0].Reference.Contains("supplementary"));
        Unsupported(EndPlateMomentComponents.Verify(Sci(ntc: true, sleSource: null)), "ServiceabilityEvidence");
    }

    [TestMethod]
    public void SingleSidedBetaOneLimitsTotalTensionByTheActualColumnPanel()
    {
        var r = EndPlateMomentComponents.Verify(Sci(beta: 1, moment: 200e6,
            config: ColumnWebPanelConfiguration.SingleSidedJoint));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(279420510.4735893, r.MomentResistance!.Value, .001);
        Assert.AreEqual(519438.31328066986, r.EffectiveRows.Sum(row => row.Resistance), 1e-6);
        Assert.AreEqual(0, r.EffectiveRows[2].Resistance, 1e-8);
        Assert.AreEqual("ColumnPanelTransformation", r.EffectiveRows[1].LimitingConstraint);
        Unsupported(EndPlateMomentComponents.Verify(Sci(config: ColumnWebPanelConfiguration.SingleSidedJoint)), "BetaOne");
    }

    [TestMethod]
    public void BeamCompressionCanExhaustAllCapacityAtTheFirstRow()
    {
        var r = EndPlateMomentComponents.Verify(Sci(beamMoment: 100e6, moment: 50e6));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(109236714.97584543, r.MomentResistance!.Value, .001);
        Assert.AreEqual(193236.71497584542, r.EffectiveRows[0].Resistance, 1e-6);
        Assert.IsTrue(r.EffectiveRows.Skip(1).All(row => row.Resistance == 0));
    }

    [TestMethod]
    public void EnOnePointNineBoltThresholdRestrictsLaterRowsWithoutImplicitUkException()
    {
        var r = EndPlateMomentComponents.Verify(Sci(columnTf: 40, plateT: 40));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status, string.Join("; ", r.ComponentAssessment.Diagnostics));
        double first = 2 * .9 * 800 * 353 / 1.25;
        Assert.AreEqual(first, r.EffectiveRows[0].Resistance, 1e-6);
        Assert.AreEqual(first * 465.3 / 565.3, r.EffectiveRows[1].Resistance, 1e-6);
        Assert.AreEqual(first * 375.3 / 565.3, r.EffectiveRows[2].Resistance, 1e-6);
        Assert.AreEqual("EN6.2.7.2(9).Row1", r.EffectiveRows[1].LimitingConstraint);
        Assert.AreEqual("EN6.2.7.2(9).Row1", r.EffectiveRows[2].LimitingConstraint);
    }

    [TestMethod]
    public void ZeroMomentStillDerivesMinimumCapacitiesInsteadOfZeroDemandGoverningCheck()
    {
        var zero = EndPlateMomentComponents.Verify(Sci(moment: 0));
        var loaded = EndPlateMomentComponents.Verify(Sci());
        Assert.AreEqual(ConnectionStatus.Passed, zero.ComponentAssessment.Status);
        Assert.AreEqual(loaded.MomentResistance, zero.MomentResistance);
        CollectionAssert.AreEqual(loaded.EffectiveRows.Select(r => r.Resistance).ToArray(), zero.EffectiveRows.Select(r => r.Resistance).ToArray());
        Assert.IsTrue(zero.ComponentAssessment.Checks.All(c => c.Demand == 0));
    }

    [TestMethod]
    public void DemandAboveMomentResistanceFailsButNeverClaimsWholeJointQualification()
    {
        var r = EndPlateMomentComponents.Verify(Sci(moment: 450e6));
        Assert.AreEqual(ConnectionStatus.Failed, r.ComponentAssessment.Status);
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.AreEqual(5, r.PendingChecks.Count);
        Assert.IsTrue(r.PendingChecks.Any(c => c.Contains("shear/tension")));
        Assert.IsTrue(r.PendingChecks.Any(c => c.Contains("weld")));
        Assert.IsTrue(r.PendingChecks.Any(c => c.Contains("stiffness")));
    }

    [TestMethod]
    public void ExactResistanceBoundaryPassesAndAnActualOverloadFails()
    {
        double resistance = EndPlateMomentComponents.Verify(Sci()).MomentResistance!.Value;
        Assert.AreEqual(ConnectionStatus.Passed, EndPlateMomentComponents.Verify(Sci(moment: resistance)).ComponentAssessment.Status);
        Assert.AreEqual(ConnectionStatus.Failed, EndPlateMomentComponents.Verify(Sci(moment: resistance * (1 + 1e-8))).ComponentAssessment.Status);
    }

    [TestMethod]
    public void RetainedShearIsVisibleAndCannotPretendToBeVerified()
    {
        var r = EndPlateMomentComponents.Verify(Sci(shear: 1e7));
        Assert.AreEqual(1e7, r.ComponentAssessment.Input.Shear);
        Assert.AreEqual(1e7, r.ComponentAssessment.Trace.Single(t => t.Name == "RetainedShearDemand").Value);
        Assert.IsTrue(r.ComponentAssessment.Scope.Contains("shear is not qualified"));
        Assert.IsFalse(r.CanQualifyWholeJoint);
        Unsupported(EndPlateMomentComponents.Verify(Sci(shear: 200000, qualifiedShear: 0)), "SameCodeBeamAndConcomitantShear");
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(-1)] [DataRow(100000)]
    public void AnyNonzeroAxialActionIsRejectedWithoutDroppingIt(double axial)
        => Unsupported(EndPlateMomentComponents.Verify(Sci(axial: axial)), "NonzeroAxial");

    [TestMethod]
    public void CompressionLoadedWidthCannotRunPastThePhysicalColumnEnd()
        => Unsupported(EndPlateMomentComponents.Verify(Sci(columnEnd: 400)), "CompressionSpreadTruncated");

    [TestMethod]
    public void BeamGeometryAndOpposingPlateSnapshotsMustAgree()
    {
        var g = new TStubInternalEndPlateGeometry(250, 258.8, 100, 11, 8, 12, 8, 501.9, new[] { 44.4, 134.4 },
            new(7.5, "source"), "source");
        Unsupported(EndPlateMomentComponents.Verify(Sci(geometry: g)), "DoesNotMatch");
        Unsupported(EndPlateMomentComponents.Verify(Sci(extension: new(260, 100, 40, 12, 50, "source"))), "ExtensionDoesNotMatch");
    }

    [TestMethod]
    public void AllExternalQualificationsRemainMandatoryAtZeroDemand()
    {
        Unsupported(EndPlateMomentComponents.Verify(Sci(moment: 0, betaSource: "")), "Required");
        Unsupported(EndPlateMomentComponents.Verify(Sci(moment: 0, stressSource: "")), "Required");
        Unsupported(EndPlateMomentComponents.Verify(Sci(moment: 0, restraintSource: "")), "Required");
        Unsupported(EndPlateMomentComponents.Verify(Sci(moment: 0, actionSource: "")), "SourceRequired");
        Unsupported(EndPlateMomentComponents.Verify(Sci(moment: 0, beamSource: "")), "BeamMomentResistanceSourceRequired");
    }

    [TestMethod]
    public void UnsupportedPanelStressAndHardwareCannotBecomePartialPasses()
    {
        Unsupported(EndPlateMomentComponents.Verify(Sci(config: ColumnWebPanelConfiguration.DoubleSidedUnequalBeamDepths)), "UnequalBeamDepths");
        Unsupported(EndPlateMomentComponents.Verify(Sci(stress: 266)), "LongitudinalStress");
        Unsupported(EndPlateMomentComponents.Verify(Sci(contact: 66)), "BoltGeometry");
        Unsupported(EndPlateMomentComponents.Verify(Sci(projection: 7)), "CompressionFlangeWeld");
    }

    [TestMethod]
    public void EffectiveRowsAndConstraintsAreImmutable()
    {
        var r = EndPlateMomentComponents.Verify(Sci());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<EndPlateEffectiveRow>)r.EffectiveRows).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<EndPlateRowCapacityConstraint>)r.Constraints).Clear());
    }

    [TestMethod]
    public void NullAndNonFiniteInputsAreRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateMomentComponents.Verify(null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Sci(moment: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Sci(axial: double.PositiveInfinity));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Sci(beta: 2.01));
    }

    [TestMethod]
    public void OverflowAndUnderflowCannotProduceUsableMomentCapacity()
    {
        Unsupported(EndPlateMomentComponents.Verify(Sci(plateT: 1e308)), "OutsideNumericRange");
        Unsupported(EndPlateMomentComponents.Verify(Sci(plateT: 1e-200)), "OutsideNumericRange");
    }
}
