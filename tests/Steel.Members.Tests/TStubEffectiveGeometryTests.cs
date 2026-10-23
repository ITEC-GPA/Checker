using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class TStubEffectiveGeometryTests
{
    private static TStubUnstiffenedColumnGeometry Column(double[]? positions = null, double first = 400,
        double last = 400, double plateWidth = 250, string source = "SCI P398 C1 cross-section; assumed verified 400mm column continuation at each end")
        => new(258.8, 12.8, 12.7, plateWidth, 100, positions ?? new[] { 0.0, 100, 190 }, first, last, source);
    private static ConnectionDesignCode Code() => ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "SCI P398 C1 UK factors");
    private static ConnectionBolt Bolt() => new(24, 26, 353, ConnectionBoltGrade.Class8_8, true);
    private static void Unsupported(TStubDerivedGeometry r, string issue)
    {
        Assert.IsFalse(r.IsSupported); Assert.AreEqual(0, r.IndividualRows.Count); Assert.AreEqual(0, r.AdjacentGroups.Count);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(issue)), string.Join("; ", r.Diagnostics));
        Assert.IsTrue(r.Trace.All(v => double.IsFinite(v.Value)));
        Assert.ThrowsException<InvalidOperationException>(() => r.CreateInput(Code(), Bolt(), 20.5, 265, new[] { 0.0 }));
    }

    [TestMethod]
    public void SciC1UnstiffenedColumnLengthsAreDerivedWithoutIntermediateRounding()
    {
        var r = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column());
        Assert.IsTrue(r.IsSupported); Assert.AreEqual(33.44, r.M, 1e-12);
        Assert.AreEqual(79.4, r.E, 1e-12); Assert.AreEqual(75, r.Emin, 1e-12);
        Assert.AreEqual(3, r.IndividualRows.Count); Assert.AreEqual(3, r.AdjacentGroups.Count);
        foreach (var row in r.IndividualRows)
        {
            Assert.AreEqual(210.10971667208535, row.Mode1, 1e-10);
            Assert.AreEqual(233.01, row.Mode2, 1e-10);
        }
        Assert.AreEqual(333.01, r.AdjacentGroups.Single(g => g.FirstRow == 0 && g.RowCount == 2).EffectiveLengths.Mode2, 1e-10);
        Assert.AreEqual(323.01, r.AdjacentGroups.Single(g => g.FirstRow == 1 && g.RowCount == 2).EffectiveLengths.Mode2, 1e-10);
        Assert.AreEqual(423.01, r.AdjacentGroups.Single(g => g.RowCount == 3).EffectiveLengths.Mode2, 1e-10);
        // P398 C1 pp86,92,96 rounds m to33.4 and each row contribution to whole mm,
        // hence prints210/233 and totals332/422. Those are not production targets.
        Assert.AreEqual(116.505, r.Trace.Single(t => t.Name == "RequiredEndContinuation").Value, 1e-10);
    }

    [TestMethod]
    public void DerivedGroupsConstructCoherentInputsAndUseAllSixMechanisms()
    {
        var geometry = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column());
        var input = geometry.CreateInput(Code(), Bolt(), 20.5, 265, new[] { 300000.0, 300000, 300000 },
            TStubMode1Method.DistributedContact, 39.55);
        var r = TStubBoltRows.Verify(input);
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(18, r.Checks.Count);
        Assert.AreEqual(900000, input.AdjacentGroups.Single(g => g.RowCount == 3).Component.TensionForce);
        Assert.IsTrue(input.AdjacentGroups.All(g => g.Component.EffectiveLengths.Source.Contains("Table6.4")));
        Assert.AreEqual(0, input.AdjacentGroups.Single(g => g.RowCount == 3).FirstRow);
    }

    [TestMethod]
    public void TranslationAndReversalPreserveWholeGroupLength()
    {
        var a = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column());
        var b = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(new[] { 1000000.0, 1000100, 1000190 }));
        var c = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(new[] { -190.0, -100, 0 }));
        double Length(TStubDerivedGeometry x) => x.AdjacentGroups.Single(g => g.RowCount == 3).EffectiveLengths.Mode2;
        Assert.AreEqual(Length(a), Length(b), 1e-10); Assert.AreEqual(Length(a), Length(c), 1e-10);
        Assert.AreEqual(333.01, c.AdjacentGroups.Single(g => g.FirstRow == 1 && g.RowCount == 2).EffectiveLengths.Mode2, 1e-10);
    }

    [TestMethod]
    public void OpposingPlateWidthChangesPryingLeverButNotColumnYieldLineLengths()
    {
        var narrow = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(plateWidth: 200));
        var wide = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(plateWidth: 300));
        Assert.AreEqual(50, narrow.Emin); Assert.AreEqual(79.4, wide.Emin, 1e-10);
        Assert.AreEqual(narrow.IndividualRows[0].Mode1, wide.IndividualRows[0].Mode1);
        Assert.AreEqual(narrow.AdjacentGroups[0].EffectiveLengths.Mode2, wide.AdjacentGroups[0].EffectiveLengths.Mode2);
    }

    [TestMethod]
    public void OneRowUsesOnlyAnIndividualMechanism()
    {
        var r = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(new[] { 10.0 }));
        Assert.IsTrue(r.IsSupported); Assert.AreEqual(1, r.IndividualRows.Count); Assert.AreEqual(0, r.AdjacentGroups.Count);
        Assert.IsNull(r.MinimumRowPitch);
        Assert.AreEqual(ConnectionStatus.Passed, TStubBoltRows.Verify(r.CreateInput(Code(), Bolt(), 20.5, 265, new[] { 100000.0 })).Status);
    }

    [TestMethod]
    public void NearbyPhysicalFreeEndIsRejectedInsteadOfAssumingInfiniteFlange()
    {
        Unsupported(TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(first: 100)), "NearbyFreeColumnEnd");
        Unsupported(TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(last: 100)), "NearbyFreeColumnEnd");
    }

    [TestMethod]
    public void EqualityAtEndContinuationLimitIsSupported()
    {
        // Exactly representable dimensions: m36,e80 => 2m+0.625e=122mm.
        var r = TStubEffectiveGeometry.UnstiffenedColumnFlange(new(260, 12, 10, 250,
            100, new[] { 0.0, 100 }, 122, 122, "Analytical equality at free-end threshold"));
        Assert.IsTrue(r.IsSupported, string.Join("; ", r.Diagnostics));
    }

    [TestMethod]
    public void CoordinatesCannotBeCoincidentOrUnsorted()
    {
        Unsupported(TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(new[] { 0.0, 0 })), "RowCoordinatesMustIncrease");
        Unsupported(TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(new[] { 0.0, 100, 90 })), "RowCoordinatesMustIncrease");
    }

    [TestMethod]
    public void NumericOverflowIsBlockedInGeometry()
    {
        var r = TStubEffectiveGeometry.UnstiffenedColumnFlange(new(1.5e308, 1, 0, 1.5e308,
            1e308, new[] { 0.0 }, 1.7e308, 1.7e308, "Extreme inputs"));
        Unsupported(r, "OutsideNumericRange");
    }

    [TestMethod]
    public void ShortPitchCannotBeConvertedToOverlappingBoltRows()
    {
        var g = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(new[] { 0.0, 30 }));
        Assert.IsTrue(g.IsSupported); // effective lengths alone, no selected bolt yet
        Assert.ThrowsException<ArgumentException>(() => g.CreateInput(Code(), Bolt(), 20.5, 265, new[] { 0.0, 0 }));
    }

    [TestMethod]
    public void MissingDetailOrInvalidClearFlangeCannotProduceLengths()
    {
        Unsupported(TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(source: " ")), "DetailSourceRequired");
        Unsupported(TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(plateWidth: 90)), "OutsideClearFlange");
        Unsupported(TStubEffectiveGeometry.UnstiffenedColumnFlange(new(100, 20, 20, 100, 50, new[] { 0.0 }, 400, 400, "Bad root")), "OutsideClearFlange");
    }

    [TestMethod]
    public void SciC1ExternalEndPlateRowIsSeparateAndUsesExAsEmin()
    {
        var g = TStubEffectiveGeometry.ExtendedEndPlateOuterRow(new(250, 100, 40, 12, 50, "SCI P398 C1 p88; external row"));
        Assert.IsTrue(g.IsSupported); Assert.AreEqual(30.4, g.M, 1e-12); Assert.AreEqual(75, g.E); Assert.AreEqual(50, g.Emin);
        Assert.AreEqual(125, g.IndividualRows.Single().Mode1); Assert.AreEqual(125, g.IndividualRows.Single().Mode2);
        Assert.AreEqual(0, g.AdjacentGroups.Count);
        var r = TStubBoltRows.Verify(g.CreateInput(Code(), Bolt(), 25, 265, new[] { 300000.0 },
            TStubMode1Method.DistributedContact, 39.55));
        Assert.AreEqual(377258.6330409357, r.GoverningCheck.Resistance, 1e-6);
        Assert.IsTrue(g.IndividualRows[0].Source.Contains("separate from rows inside"));
    }

    [DataTestMethod]
    [DataRow(300.0, 100.0, 18.0, 10.0, 10.0, 52.5, 52.5)]
    [DataRow(120.0, 100.0, 18.0, 10.0, 10.0, 36.25, 36.25)]
    [DataRow(250.0, 100.0, 40.0, 12.0, 50.0, 125.0, 125.0)]
    [DataRow(220.0, 20.0, 18.0, 10.0, 10.0, 36.25, 36.25)]
    [DataRow(800.0, 400.0, 48.0, 10.0, 200.0, 251.32741228718345, 400.0)]
    [DataRow(2100.0, 100.0, 108.0, 10.0, 1000.0, 414.1592653589793, 875.0)]
    [DataRow(1200.0, 1000.0, 108.0, 10.0, 1000.0, 514.1592653589793, 600.0)]
    public void EveryExternalRowCircularAndNonCircularCandidateCanLimitTheLengths(
        double width, double gauge, double x, double weld, double ex, double l1, double l2)
    {
        // Independent direct evaluations of each Table6.6 candidate; geometry only.
        var r = TStubEffectiveGeometry.ExtendedEndPlateOuterRow(new(width, gauge, x, weld, ex, "Table6.6 analytical candidate table"));
        Assert.IsTrue(r.IsSupported); Assert.AreEqual(l1, r.IndividualRows.Single().Mode1, 1e-9);
        Assert.AreEqual(l2, r.IndividualRows.Single().Mode2, 1e-9);
    }

    [TestMethod]
    public void OuterRowCannotCrossWeldOrLosePlateSideEdge()
    {
        Unsupported(TStubEffectiveGeometry.ExtendedEndPlateOuterRow(new(250, 100, 10, 12, 50, "Bad weld clearance")), "OutsideClearPlateExtension");
        Unsupported(TStubEffectiveGeometry.ExtendedEndPlateOuterRow(new(100, 100, 40, 12, 50, "No side edge")), "OutsideClearPlateExtension");
        var g = TStubEffectiveGeometry.ExtendedEndPlateOuterRow(new(140, 100, 40, 12, 50, "Side edge20 below bolt limit"));
        Assert.ThrowsException<ArgumentException>(() => g.CreateInput(Code(), Bolt(), 25, 265, new[] { 0.0 }));
    }

    [TestMethod]
    public void ActualContactWidthMustFitPastRootNotOnlyPastPlasticHingeLine()
    {
        var g = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(new[] { 0.0 }));
        // 64mm footprint fits m=33.44 but its radius32 exceeds the actual clear root distance30.9.
        Assert.ThrowsException<ArgumentException>(() => g.CreateInput(Code(), Bolt(), 20.5, 265, new[] { 0.0 },
            TStubMode1Method.DistributedContact, 64));
    }

    [TestMethod]
    public void ExternalRowContactFootprintCannotCrossItsSideEdgeEvenWhenExIsLarge()
    {
        var g = TStubEffectiveGeometry.ExtendedEndPlateOuterRow(new(140, 100, 48, 10, 50,
            "Side edge20mm; separate longitudinal ex50mm"));
        var bolt = new ConnectionBolt(12, 13, 84.3, ConnectionBoltGrade.Class8_8, true);
        // Hole fits e=20; radius21 of the actual42mm contact footprint does not.
        Assert.ThrowsException<ArgumentException>(() => g.CreateInput(Code(), bolt, 20, 275,
            new[] { 0.0 }, TStubMode1Method.DistributedContact, 42));
    }

    [TestMethod]
    public void ContactFootprintsCannotOverlapAcrossTheBoltGauge()
    {
        var g = TStubEffectiveGeometry.ExtendedEndPlateOuterRow(new(250, 40, 40, 12, 50, "Gauge40mm"));
        var bolt = new ConnectionBolt(12, 13, 84.3, ConnectionBoltGrade.Class8_8, true);
        Assert.ThrowsException<ArgumentException>(() => g.CreateInput(Code(), bolt, 20, 275,
            new[] { 0.0 }, TStubMode1Method.DistributedContact, 42));
    }

    [TestMethod]
    public void ContactFootprintsCannotOverlapBetweenAdjacentRows()
    {
        var g = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column(new[] { 0.0, 40 }));
        var bolt = new ConnectionBolt(12, 13, 84.3, ConnectionBoltGrade.Class8_8, true);
        Assert.ThrowsException<ArgumentException>(() => g.CreateInput(Code(), bolt, 20.5, 265,
            new[] { 0.0, 0 }, TStubMode1Method.DistributedContact, 42));
    }

    [TestMethod]
    public void ForceCountMustMatchDerivedRows()
    {
        var g = TStubEffectiveGeometry.UnstiffenedColumnFlange(Column());
        Assert.ThrowsException<ArgumentException>(() => g.CreateInput(Code(), Bolt(), 20.5, 265, new[] { 0.0 }));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => g.CreateInput(Code(), Bolt(), 20.5, 265, new[] { 0.0, -1, 0 }));
    }

    [TestMethod]
    public void GeometryInputsCopyRowCoordinates()
    {
        var coordinates = new[] { 0.0, 100, 190 }; var p = Column(coordinates);
        coordinates[1] = 0;
        Assert.IsTrue(TStubEffectiveGeometry.UnstiffenedColumnFlange(p).IsSupported);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<double>)p.OrderedRowCoordinates)[0] = 100);
    }

    [TestMethod]
    public void NullAndNonFiniteInputsAreRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => TStubEffectiveGeometry.UnstiffenedColumnFlange(null!));
        Assert.ThrowsException<ArgumentNullException>(() => TStubEffectiveGeometry.ExtendedEndPlateOuterRow(null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Column(new[] { double.NaN }));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TStubExtendedEndPlateGeometry(250, 100, 40, double.NaN, 50, "source"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Column(Array.Empty<double>()));
    }
}
