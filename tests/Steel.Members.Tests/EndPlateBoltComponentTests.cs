using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateBoltComponentTests
{
    private static EndPlateMomentComponentResult Parent(bool ntc = false, double shear = 300000, double moment = 300e6, double plateT = 25, bool extended = true,
        double secondInternalY = 134.4)
    {
        var code = ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "SCI C1");
        var policy = ntc ? ColumnWebPolicy.Ntc2018(code) : ColumnWebPolicy.Eurocode3(code, 1);
        var beam = new ConnectionRolledISection(533.1, 209.3, 10.1, 15.6, 12.7, 11700, 275, 410);
        var column = new ConnectionRolledISection(266.7, 258.8, 12.8, 20.5, 12.7, 13600, 265, 410);
        var g = new TStubInternalEndPlateGeometry(250, 258.8, 100, 10.1, 8, 12, 8, 501.9, new[] { 44.4, secondInternalY },
            new(7.5, "SCI P398 C1 graphical alpha"), "SCI C1; column free-end continuations1000mm assumed for test");
        return EndPlateMomentComponents.Verify(new EndPlateMomentComponentInput(policy, column, beam, 265,
            new(250, plateT, 265, 410), new(24, 26, 353, ConnectionBoltGrade.Class8_8, true), g,
            extended ? new(250, 100, 40, 12, 50, "SCI C1") : null!, 1000, 1000, 46.9,
            new(code, beam, 649e6, shear, "Qualified test Mc,Rd for same section/code/shear; SCI C1 baseline649kNm"),
            moment, shear, 0, 0, 0, ColumnWebPanelConfiguration.DoubleSidedSimilarBeamDepths,
            "Equal/opposite beam moments", "Test zero web longitudinal compression", "Qualified column-sway restraint",
            "Same ULS combination", TStubMode1Method.DistributedContact, 39.55, "NTC clearance SLE reference"));
    }
    private static EndPlateBoltHardware Hardware(double footprint = 41.6, double flats = 36, double points = 39.55, string source = "Test hardware: SCI head36/39.55 and washer41.6; matching nut dimensions and execution qualification explicitly assumed for this fixture")
        => new(flats, points, flats, points, footprint, footprint, source);
    private static EndPlateBoltShearRow[] Rows(double tensionShear = 30000, double lowerShear = 60000,
        double lowerY = 470, string? noTension = "Qualified shear-only row near compression; y470mm is TEST geometry, not an SCI published dimension")
        => new[] { new EndPlateBoltShearRow(0, -40, tensionShear, tensionShear),
            new(1, 60, tensionShear, tensionShear), new(2, 150, tensionShear, tensionShear),
            new(null, lowerY, lowerShear, lowerShear, noTension) };
    private static EndPlateBoltComponentResult Verify(EndPlateMomentComponentResult? parent = null,
        EndPlateBoltShearRow[]? rows = null, EndPlateBoltHardware? hardware = null, string distribution = "Explicit balanced same-load-case shears",
        string flangeStrength = "Column flangeFu410 at actual thickness")
        => EndPlateBoltComponents.Verify(new(parent ?? Parent(), hardware ?? Hardware(), rows ?? Rows(), 410, flangeStrength, distribution));
    private static double Trace(EndPlateBoltComponentResult r, string name)
        => r.BoltAssessment.Trace.Single(t => t.Name == name).Value;
    private static void Unsupported(EndPlateBoltComponentResult r, string issue)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.BoltAssessment.Status);
        Assert.AreEqual(0, r.BoltAssessment.Checks.Count); Assert.IsNull(r.BoltAssessment.Utilization);
        Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.BoltAssessment.Diagnostics.Any(d => d.Contains(issue)), string.Join("; ", r.BoltAssessment.Diagnostics));
    }

    [TestMethod]
    public void PrescribedShearClosesBothPlyChecksWithPryingEnvelopeAndRealLongJoint()
    {
        var r = Verify();
        Assert.AreEqual(ConnectionStatus.Passed, r.BoltAssessment.Status, string.Join("; ", r.BoltAssessment.Diagnostics));
        Assert.AreEqual(56, r.BoltAssessment.Checks.Count);
        Assert.AreEqual(300000, Trace(r, "AssignedShear")); Assert.AreEqual(0, Trace(r, "AssignedTorsion"));
        Assert.AreEqual(510, Trace(r, "PhysicalJointLength"));
        Assert.AreEqual(.96875, Trace(r, "BaseBolt.BetaLongJoint"), 1e-12);
        Assert.AreEqual(203328, Trace(r, "FullTensionEnvelopePerBolt"), 1e-8);
        Assert.AreEqual(37.775, Trace(r, "PunchingMeanHeadNutDiameter"), 1e-12);
        Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.PendingChecks.Any(x => x.Contains("weld")));
        Assert.IsTrue(r.PendingChecks.Any(x => x.Contains("net/block")));
    }

    [TestMethod]
    public void IndependentEn622EnvelopeMatchesSciC1CountWithoutCopyingItsRoundedBoltValue()
    {
        var r = Verify();
        double exactUnreduced = (2 + 6 * (2d / 7)) * (.6 * 800 * 353 / 1.25);
        Assert.AreEqual(503478.85714285716, exactUnreduced, 1e-6);
        Assert.AreEqual(exactUnreduced * .96875, Trace(r, "BoltsOnlyShearEnvelope"), 1e-6);
        // SCI P398 p105 uses136kN and0.28 =>500kN; actual long-joint reduction remains applied.
        Assert.AreEqual(500000, Trace(r, "BoltsOnlyShearEnvelope") / .96875, 4000);
        Assert.AreEqual(2d / 7, Trace(r, "TensionBoltResidualShearFactor"), 1e-15);
    }

    [TestMethod]
    public void BothPunchingChecksUseActualHeadNutMeanNotWasherDiameter()
    {
        var r = Verify();
        double plate = .6 * Math.PI * 37.775 * 25 * 410 / 1.25;
        double column = .6 * Math.PI * 37.775 * 20.5 * 410 / 1.25;
        Assert.AreEqual(plate, r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow1.Left.HeadPly.Plate.BoltHeadPunching").Resistance, 1e-6);
        Assert.AreEqual(column, r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow1.Left.NutPly.Plate.BoltHeadPunching").Resistance, 1e-6);
        Assert.AreEqual(203328, r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow1.Left.HeadPly.Plate.BoltHeadPunching").Demand);
    }

    [TestMethod]
    public void FullFtEnvelopeIsIndependentOfRowForceDividedByTwo()
    {
        var r = Verify(parent: Parent(moment: 1));
        Assert.AreEqual(203328, Trace(r, "PhysicalRow1.TensionEnvelopePerBolt"));
        Assert.AreEqual(203328, Trace(r, "PhysicalRow2.TensionEnvelopePerBolt"));
        Assert.AreEqual(0, Trace(r, "PhysicalRow4.TensionEnvelopePerBolt"));
        Assert.IsTrue(r.BoltAssessment.Assumptions.Any(a => a.Contains("prying")));
    }

    [TestMethod]
    public void NtcInteractionIncludesBothBearingAndPunchingMinima()
    {
        var r = Verify(parent: Parent(ntc: true));
        Assert.AreEqual(ConnectionStatus.Passed, r.BoltAssessment.Status);
        Assert.AreEqual(72, r.BoltAssessment.Checks.Count);
        var c = r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow1.Left.Assembly.NtcShearTensionInteraction");
        Assert.AreEqual(30000 / 131316.0 + 1 / 1.4, c.Demand, 1e-12);
        Assert.IsTrue(c.Reference.Contains("4.2.71"));
    }

    [TestMethod]
    public void ExcessShearInOneTensionBoltFailsDespiteGlobalBoltEnvelopeBeingLargeEnough()
    {
        var rows = Rows(40000, 30000); //300kN total, but each tensile bolt exceeds its37.5kN residual.
        var r = Verify(rows: rows);
        Assert.AreEqual(ConnectionStatus.Failed, r.BoltAssessment.Status);
        Assert.IsTrue(Trace(r, "BoltsOnlyShearEnvelope") > 300000);
        Assert.IsFalse(r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow1.Left.Bolt.Bolt.ShearTensionInteraction").Passed);
    }

    [TestMethod]
    public void PunchingBelowFullFtEnvelopeFailsWithoutChangingParentTStubCapacity()
    {
        var parent = Parent(shear: 0, moment: 0, plateT: 8);
        Assert.AreEqual(ConnectionStatus.Passed, parent.ComponentAssessment.Status, string.Join("; ", parent.ComponentAssessment.Diagnostics));
        double capacityBefore = parent.MomentResistance!.Value;
        var r = Verify(parent, Rows(0, 0));
        Assert.AreEqual(ConnectionStatus.Failed, r.BoltAssessment.Status);
        Assert.IsFalse(r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow1.Left.HeadPly.Plate.BoltHeadPunching").Passed);
        Assert.AreEqual(capacityBefore, r.MomentComponents.MomentResistance);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void LowerExtremeRowChecksTheAdjacentPitchForReversedShearOnBothPlies(bool ntc)
    {
        // Independent EN Table3.4 / NTC4.2.67: p62.4 gives alpha=.55.
        // The old end-only selection gave177120N and passed the120kN demand.
        var parent = Parent(ntc: ntc, shear: 240000, moment: 0, plateT: 9);
        Assert.AreEqual(ConnectionStatus.Passed, parent.ComponentAssessment.Status, string.Join("; ", parent.ComponentAssessment.Diagnostics));
        var r = Verify(parent, Rows(tensionShear: 0, lowerShear: 120000, lowerY: 212.4));
        Assert.AreEqual(ConnectionStatus.Failed, r.BoltAssessment.Status);
        foreach (string side in new[] { "Left", "Right" })
        {
            var plate = r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow4." + side + ".HeadPly.Plate.Bearing");
            var column = r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow4." + side + ".NutPly.Plate.Bearing");
            Assert.AreEqual(97416, plate.Resistance, 1e-6);
            Assert.AreEqual(120000, plate.Demand);
            Assert.IsFalse(plate.Passed);
            Assert.AreEqual(221892, column.Resistance, 1e-6);
            Assert.IsTrue(column.Passed);
        }
        var punching = r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow1.Left.HeadPly.Plate.BoltHeadPunching");
        Assert.AreEqual(210194.79100114483, punching.Resistance, 1e-6);
        Assert.IsTrue(punching.Passed, "Punching must not mask this bearing regression.");
        Assert.IsTrue(r.BoltAssessment.Checks.Where(c => !c.Id.Contains("Plate.Bearing")
            && !c.Id.Contains("Assembly.NtcShearTensionInteraction")).All(c => c.Passed));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void UpperExtremeRowAlsoUsesThePitchEnvelopeIndependentlyOfDemand(bool ntc)
    {
        // Flush test variant: firstY60,secondY122.4,p62.4. It represents the
        // opposite extreme of the same two-direction envelope, not an SCI quota.
        // Zero demand isolates capacity selection from the full-Ft interaction.
        var parent = Parent(ntc: ntc, shear: 0, moment: 0, plateT: 9, extended: false, secondInternalY: 106.8);
        Assert.AreEqual(ConnectionStatus.Passed, parent.ComponentAssessment.Status, string.Join("; ", parent.ComponentAssessment.Diagnostics));
        var r = Verify(parent, new[] { new EndPlateBoltShearRow(0, 60, 0, 0), new(1, 122.4, 0, 0) });
        Assert.AreEqual(ConnectionStatus.Passed, r.BoltAssessment.Status, string.Join("; ", r.BoltAssessment.Diagnostics));
        foreach (string side in new[] { "Left", "Right" })
        {
            var plate = r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow1." + side + ".HeadPly.Plate.Bearing");
            var column = r.BoltAssessment.Checks.Single(c => c.Id == "PhysicalRow1." + side + ".NutPly.Plate.Bearing");
            Assert.AreEqual(97416, plate.Resistance, 1e-6);
            Assert.AreEqual(221892, column.Resistance, 1e-6);
            Assert.AreEqual(0, plate.Demand);
        }
    }

    [TestMethod]
    public void SixTensionBoltsOnlyAreValidWhenTheActualShearDistributionFits()
    {
        var r = Verify(Parent(shear: 180000), Rows().Take(3).ToArray());
        Assert.AreEqual(ConnectionStatus.Passed, r.BoltAssessment.Status);
        Assert.AreEqual(190, Trace(r, "PhysicalJointLength"));
        Assert.AreEqual(1, Trace(r, "BaseBolt.BetaLongJoint"));
        Assert.AreEqual(6 * (2d / 7) * 135552, Trace(r, "BoltsOnlyShearEnvelope"), 1e-6);
    }

    [TestMethod]
    public void FlushPlateUsesItsOwnPhysicalTopAndInternalRowIndices()
    {
        var rows = new[] { new EndPlateBoltShearRow(0, 60, 30000, 30000),
            new(1, 150, 30000, 30000), new(null, 470, 60000, 60000, "Qualified shear-only test row") };
        var r = Verify(Parent(shear: 240000, moment: 200e6, extended: false), rows);
        Assert.AreEqual(ConnectionStatus.Passed, r.BoltAssessment.Status, string.Join("; ", r.BoltAssessment.Diagnostics));
        Assert.AreEqual(60, Trace(r, "PhysicalRow1.PlateEndDistance"));
        Assert.AreEqual(410, Trace(r, "PhysicalJointLength"));
        Assert.AreEqual(42, r.BoltAssessment.Checks.Count);
    }

    [TestMethod]
    public void OppositeLocalLeftRightDifferencesAreAllowedOnlyWhenTorsionBalances()
    {
        var rows = Rows();
        rows[0] = new(0, -40, 31000, 29000); rows[1] = new(1, 60, 29000, 31000);
        Assert.AreEqual(ConnectionStatus.Passed, Verify(rows: rows).BoltAssessment.Status);
        rows[1] = new(1, 60, 30000, 30000);
        Unsupported(Verify(rows: rows), "UnmodelledTorsion");
    }

    [TestMethod]
    public void ParentFailureMissingRowsAndUnbalancedShearsCannotBecomeBoltPasses()
    {
        Unsupported(Verify(Parent(moment: 500e6)), "PassedMomentComponentsRequired");
        Unsupported(Verify(rows: Rows().Skip(1).ToArray()), "EveryMomentRow");
        Unsupported(Verify(rows: Rows(30000, 50000)), "DoNotEquilibrate");
        var rows = Rows(); rows[1] = new(0, 60, 30000, 30000);
        Unsupported(Verify(rows: rows), "EveryMomentRow");
    }

    [TestMethod]
    public void ACoordinateCannotBeDetachedFromItsParentMomentRow()
    {
        var rows = Rows(); rows[2] = new(2, 160, 30000, 30000);
        Unsupported(Verify(rows: rows), "PhysicalCoordinate");
    }

    [TestMethod]
    public void ShearOnlyRowsRequireQualifiedNoTensionAndMustFollowTheTensionZone()
    {
        Unsupported(Verify(rows: Rows(noTension: null)), "NoTensionQualification");
        var rows = Rows(); rows[3] = new(null, 140, 60000, 60000, "source");
        Unsupported(Verify(rows: rows), "FollowEveryActiveTensionRow");
    }

    [DataTestMethod]
    [DataRow(520)] [DataRow(580)] [DataRow(590)]
    public void ShearOnlyHardwareCannotIntersectAFlangeWeldOrFreeEnd(double y)
        => Unsupported(Verify(rows: Rows(lowerY: y)), "HardwareOrFullPunchingPerimeter");

    [TestMethod]
    public void ActualHardwareAndContactCannotBeInventedFromTheTStubWidth()
    {
        Unsupported(Verify(hardware: Hardware(footprint: 80)), "HardwareOrFullPunchingPerimeter");
        Unsupported(Verify(hardware: Hardware(footprint: 38, flats: 34, points: 37)), "ContactWidthExceedsActualHardware");
        Unsupported(Verify(hardware: Hardware(source: "")), "ActualHardwareSource");
    }

    [TestMethod]
    public void DistributionAndMaterialSourcesAreMandatory()
    {
        Unsupported(Verify(distribution: ""), "ShearDistributionSource");
        Unsupported(Verify(flangeStrength: ""), "FlangeStrengthSource");
    }

    [TestMethod]
    public void InputRowsAreCopiedAndReadOnly()
    {
        var rows = Rows(); var p = new EndPlateBoltComponentInput(Parent(), Hardware(), rows, 410, "Fu source", "Shear source");
        rows[0] = new(null, 10000, 0, 0, "replacement");
        Assert.AreEqual(ConnectionStatus.Passed, EndPlateBoltComponents.Verify(p).BoltAssessment.Status);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<EndPlateBoltShearRow>)p.Rows).Clear());
    }

    [TestMethod]
    public void InvalidAndOverflowingForceDataAreRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateBoltComponents.Verify(null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new EndPlateBoltShearRow(0, 0, -1, 0));
        Assert.ThrowsException<ArgumentException>(() => Hardware(flats: 40, points: 39));
        Unsupported(Verify(rows: Rows(1e308, 1e308)), "DoNotEquilibrate");
    }
}
