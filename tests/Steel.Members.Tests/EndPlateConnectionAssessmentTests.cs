using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateConnectionAssessmentTests
{
    // SCI C1 section/row geometry; full-joint verification and execution
    // qualification are not implied by these explicit integration fixtures.
    private static EndPlateMomentComponentResult Parent(bool ntc = false, bool cjp = false,
        double moment = 300e6, double shear = 300000)
    {
        var code = ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "SCI C1");
        var beam = new ConnectionRolledISection(533.1, 209.3, 10.1, 15.6, 12.7, 11700, 275, 410);
        var column = new ConnectionRolledISection(266.7, 258.8, 12.8, 20.5, 12.7, 13600, 265, 410);
        var geometry = new TStubInternalEndPlateGeometry(250, 258.8, 100, 10.1, 8, 12, 8, 501.9,
            new[] { 44.4, 134.4 }, new(7.5, "SCI C1 graphical alpha"), "Explicit fixture continuations1000mm");
        return EndPlateMomentComponents.Verify(new EndPlateMomentComponentInput(
            ntc ? ColumnWebPolicy.Ntc2018(code) : ColumnWebPolicy.Eurocode3(code, 1), column, beam, 265,
            new(250, 25, cjp ? 345 : 265, cjp ? 470 : 410),
            new(24, 26, 353, ConnectionBoltGrade.Class8_8, true), geometry,
            cjp ? null! : new(250, 100, 40, 12, 50, "SCI C1 extended row"), 1000, 1000, 46.9,
            new(code, beam, 649e6, shear, "Explicit same-case beam capacity, baseline SCI C1; local fixture only"),
            moment, shear, 0, 0, 0, ColumnWebPanelConfiguration.DoubleSidedSimilarBeamDepths,
            "Equal opposite beams", "No longitudinal column stress", "Qualified column-sway restraint",
            "Same immutable ULS case", TStubMode1Method.DistributedContact, 39.55, "NTC clearance check evidence"));
    }
    private static EndPlateBoltComponentInput Bolts(EndPlateMomentComponentResult parent,
        bool overloadedRow = false, string hardwareSource = "Actual 36/39.55 head/nut and41.6washer")
    {
        bool cjp = parent.ComponentAssessment.Input.ExtensionGeometry == null;
        double shear = parent.ComponentAssessment.Input.Shear;
        double rowShear = shear == 0 ? 0 : overloadedRow ? 40000 : 30000;
        int tensionRows = cjp ? 2 : 3;
        var rows = new List<EndPlateBoltShearRow>();
        if (!cjp) rows.Add(new(0, -40, rowShear, rowShear));
        rows.Add(new(cjp ? 0 : 1, 60, rowShear, rowShear));
        rows.Add(new(cjp ? 1 : 2, 150, rowShear, rowShear));
        double lower = (shear - 2 * tensionRows * rowShear) / 2;
        rows.Add(new(null, 470, lower, lower, "Qualified shear-only compression-zone row; test geometry"));
        return new(parent, new(36, 39.55, 36, 39.55, 41.6, 41.6, hardwareSource), rows, 410,
            "Actual column-flange Fu410", "Explicit balanced shear for this case");
    }
    private static EndPlateWeldComponentInput Fillets(EndPlateMomentComponentResult parent,
        bool compressionFull = false, string material = "Actual S275 product and thickness")
        => new(parent, FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S275, 275, 410, material,
            new(FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize,
                compressionFull ? EndPlateCompressionTransfer.FullStrengthWelds : EndPlateCompressionTransfer.QualifiedDirectBearing,
                "Real continuous paired welds", "Qualified rolled-root normal transfer", "All V through web welds",
                "Compatible consumable", "Qualified local eccentricities", "Qualified compression contact"));
    private static TopFlushCjpEndPlateWeldInput Cjp(EndPlateMomentComponentResult parent, bool full = true)
        => new(parent, new(15.6, 209.3, 12, 0, 355, 510, full,
                "Actual qualified full penetration", "Preparation and inspection", "Real inner toe", "Through thickness evidence"),
            580, 0, FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S355, 275, 410, "Actual materials",
            FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize,
            EndPlateCompressionTransfer.QualifiedDirectBearing, "Actual fillets", "Normal transfer",
            "All V through web", "Consumable compatibility", "Local eccentricity", "Compression contact");
    private static void Unsupported(EndPlateConnectionAssessmentResult r)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus);
        Assert.AreEqual(0, r.Checks.Count); Assert.IsNull(r.CoveredComponentsUtilization);
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsTrue(r.Diagnostics.Count > 0);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ExtendedPlateRetainsMomentBoltsAndPhysicalWeldChecksForSameCase(bool ntc)
    {
        var parent = Parent(ntc); var r = EndPlateConnectionAssessment.Verify(Bolts(parent), Fillets(parent));
        Assert.AreEqual(ConnectionStatus.Passed, r.CoveredComponentsStatus, string.Join(";", r.Diagnostics));
        Assert.IsTrue(r.IsSameMomentAssessment); Assert.IsFalse(r.HasFailedComponent);
        Assert.AreSame(parent, r.MomentComponents); Assert.AreSame(parent, r.WeldMomentComponents);
        Assert.IsNotNull(r.FilletWelds); Assert.IsNull(r.TopFlushCjpWelds); Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.AreEqual(parent.ComponentAssessment.Checks.Count + (ntc ? 72 : 56) + 4, r.Checks.Count);
        Assert.AreEqual(ntc ? 344.4166576313204 : 360.3185016619121,
            r.Checks.Single(c => c.Id == "Web.DirectionalEquivalent").Demand, 1e-8);
        Assert.IsTrue(r.PendingWholeJointChecks.Any(s => s.Contains("net/block")));
        Assert.IsTrue(r.PendingWholeJointChecks.Any(s => s.Contains("global")));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void TopFlushCjpUsesItsDistinctDetailAndRetainsConditionalEquivalence(bool ntc)
    {
        var p = Parent(ntc, cjp: true, moment: 0); var r = EndPlateConnectionAssessment.Verify(Bolts(p), Cjp(p));
        Assert.AreEqual(ConnectionStatus.Passed, r.CoveredComponentsStatus, string.Join(";", r.Diagnostics));
        Assert.IsNull(r.FilletWelds); Assert.IsNotNull(r.TopFlushCjpWelds);
        Assert.IsFalse(r.TopFlushCjpWelds.IsFullyFlushGeometry); Assert.IsFalse(r.CanQualifyWholeJoint);
        var c = r.Checks.Single(x => x.Id == "Cjp.FullStrengthNormalEquivalence");
        Assert.AreEqual(ntc ? 855140 : 897897, c.Resistance, 1e-8); Assert.AreEqual(c.Resistance, c.Demand);
        Assert.AreEqual(1, r.CoveredComponentsUtilization!.Value, 1e-12);
    }

    [TestMethod]
    public void OneOverloadedBoltCannotBeHiddenByPassingMomentAndWelds()
    {
        var p = Parent(); var r = EndPlateConnectionAssessment.Verify(Bolts(p, overloadedRow: true), Fillets(p));
        Assert.AreEqual(ConnectionStatus.Passed, p.ComponentAssessment.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.FilletWelds.WeldAssessment.Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.CoveredComponentsStatus); Assert.IsTrue(r.HasFailedComponent);
        Assert.IsTrue(r.CoveredComponentsUtilization > 1); Assert.IsFalse(r.GoverningCheck.Passed);
    }

    [TestMethod]
    public void InsufficientCompressionWeldCannotBeHiddenByPassingBolts()
    {
        var p = Parent(); var r = EndPlateConnectionAssessment.Verify(Bolts(p), Fillets(p, compressionFull: true));
        Assert.AreEqual(ConnectionStatus.Passed, r.Bolts.BoltAssessment.Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.CoveredComponentsStatus); Assert.IsTrue(r.HasFailedComponent);
        Assert.AreEqual(536.25, r.Checks.Single(c => c.Id == "CompressionFlange.DirectionalEquivalent").Demand, 1e-10);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void FailedMomentRemainsVisibleWhenItsDependentModelsAreUnsupported(bool cjp)
    {
        var p = Parent(cjp: cjp, moment: 700e6);
        var r = cjp ? EndPlateConnectionAssessment.Verify(Bolts(p), Cjp(p))
            : EndPlateConnectionAssessment.Verify(Bolts(p), Fillets(p));
        Unsupported(r); Assert.IsTrue(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.Failed, r.MomentComponents.ComponentAssessment.Status);
        Assert.IsTrue(r.MomentComponents.ComponentAssessment.Checks.Any(c => !c.Passed));
    }

    [TestMethod]
    public void MissingWeldEvidenceDoesNotEraseAnAlreadyFailedBolt()
    {
        var p = Parent(); var r = EndPlateConnectionAssessment.Verify(Bolts(p, true), Fillets(p, material: ""));
        Unsupported(r); Assert.IsTrue(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.Failed, r.Bolts.BoltAssessment.Status);
    }

    [TestMethod]
    public void MissingHardwareEvidenceDoesNotEraseAnAlreadyFailedWeld()
    {
        var p = Parent(); var r = EndPlateConnectionAssessment.Verify(Bolts(p, hardwareSource: ""), Fillets(p, true));
        Unsupported(r); Assert.IsTrue(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.Failed, r.FilletWelds.WeldAssessment.Status);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void EqualNumbersInIndependentMomentResultsDoNotProveConcomitance(bool cjp)
    {
        var a = Parent(cjp: cjp, moment: 0); var b = Parent(cjp: cjp, moment: 0);
        var r = cjp ? EndPlateConnectionAssessment.Verify(Bolts(a), Cjp(b))
            : EndPlateConnectionAssessment.Verify(Bolts(a), Fillets(b));
        Unsupported(r); Assert.IsFalse(r.IsSameMomentAssessment); Assert.IsFalse(r.HasFailedComponent);
        Assert.AreSame(b, r.WeldMomentComponents);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), "SAME_IMMUTABLE_MOMENT_ASSESSMENT_REQUIRED");
    }

    [TestMethod]
    public void RecomputingTheSameInputStillCreatesAnIndependentAssessment()
    {
        var a = Parent(); var b = EndPlateMomentComponents.Verify(a.ComponentAssessment.Input);
        var r = EndPlateConnectionAssessment.Verify(Bolts(a), Fillets(b));
        Unsupported(r); Assert.IsFalse(r.IsSameMomentAssessment);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void FailureInTheOtherParentSurvivesIdentityMismatch(bool cjp)
    {
        var a = Parent(cjp: cjp, moment: 0); var b = Parent(cjp: cjp, moment: 700e6);
        var r = cjp ? EndPlateConnectionAssessment.Verify(Bolts(a), Cjp(b))
            : EndPlateConnectionAssessment.Verify(Bolts(a), Fillets(b));
        Unsupported(r); Assert.IsTrue(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.Failed, r.WeldMomentComponents.ComponentAssessment.Status);
    }

    [TestMethod]
    public void PartialPenetrationCannotEnterTheCjpAggregate()
    {
        var p = Parent(cjp: true, moment: 0); var r = EndPlateConnectionAssessment.Verify(Bolts(p), Cjp(p, full: false));
        Unsupported(r); Assert.IsFalse(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.Passed, r.Bolts.BoltAssessment.Status);
    }

    [TestMethod]
    public void PassingSameGeometryDoesNotConvertTheExtendedFilletModelToFlush()
    {
        var p = Parent(cjp: true, moment: 0); var r = EndPlateConnectionAssessment.Verify(Bolts(p), Fillets(p));
        Unsupported(r); Assert.IsNull(r.TopFlushCjpWelds);
    }

    [TestMethod]
    public void ZeroDemandRetainsRequiredFullStrengthWeldEnvelope()
    {
        var p = Parent(moment: 0, shear: 0); var r = EndPlateConnectionAssessment.Verify(Bolts(p), Fillets(p, true));
        Assert.AreEqual(ConnectionStatus.Failed, r.CoveredComponentsStatus);
        Assert.IsTrue(r.HasFailedComponent); Assert.IsTrue(r.Checks.Any(c => !c.Passed));
    }

    [TestMethod]
    public void AggregateCollectionsCannotBeEditedIntoAPassingCertificate()
    {
        var p = Parent(); var r = EndPlateConnectionAssessment.Verify(Bolts(p), Fillets(p));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionCheck>)r.Checks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)r.PendingWholeJointChecks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)r.Diagnostics).Add("override"));
    }

    [TestMethod]
    public void NullContractsAreRejectedByBothPhysicalDetailOverloads()
    {
        var p = Parent(); var b = Bolts(p);
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateConnectionAssessment.Verify(null!, Fillets(p)));
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateConnectionAssessment.Verify(b, (EndPlateWeldComponentInput)null!));
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateConnectionAssessment.Verify(null!, Cjp(p)));
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateConnectionAssessment.Verify(b, (TopFlushCjpEndPlateWeldInput)null!));
    }
}
