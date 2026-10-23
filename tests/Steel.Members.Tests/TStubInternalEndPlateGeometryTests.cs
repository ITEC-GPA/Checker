using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class TStubInternalEndPlateGeometryTests
{
    private static TStubInternalEndPlateGeometry Sci(double[]? rows = null, double clear = 501.9,
        double alpha = 7.5, string alphaSource = "SCI P398 C1 pp90-91 and99, graphical alpha7.5",
        string detail = "SCI P398 C1 h533.1,tf15.6,tw10.1,w100,web weld leg8,flange weld leg12/8")
        => new(250, 258.8, 100, 10.1, 8, 12, 8, clear, rows ?? new[] { 44.4, 134.4 }, new(alpha, alphaSource), detail);
    private static ConnectionDesignCode Code() => ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "SCI P398 C1 UK factors");
    private static ConnectionBolt Bolt() => new(24, 26, 353, ConnectionBoltGrade.Class8_8, true);
    private static double Trace(TStubDerivedGeometry r, string name) => r.Trace.Single(t => t.Name == name).Value;
    private static void Unsupported(TStubDerivedGeometry r, string issue)
    {
        Assert.IsFalse(r.IsSupported); Assert.AreEqual(0, r.IndividualRows.Count); Assert.AreEqual(0, r.AdjacentGroups.Count);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(issue)), string.Join("; ", r.Diagnostics));
        Assert.ThrowsException<InvalidOperationException>(() => r.CreateInput(Code(), Bolt(), 25, 265, new[] { 0.0 }));
    }

    [TestMethod]
    public void SciC1InternalRowsUseActualWeldLegGeometryAndQualifiedGraphicalAlpha()
    {
        var r = TStubInternalEndPlateLengths.Calculate(Sci());
        Assert.IsTrue(r.IsSupported, string.Join("; ", r.Diagnostics));
        Assert.AreEqual(38.55, r.M, 1e-12); Assert.AreEqual(75, r.E); Assert.AreEqual(75, r.Emin);
        Assert.AreEqual(34.8, Trace(r, "M2"), 1e-12);
        Assert.AreEqual(.33949801849405564, Trace(r, "Lambda1"), 1e-12);
        Assert.AreEqual(.30647291941875826, Trace(r, "Lambda2"), 1e-12);
        Assert.AreEqual(7.5, Trace(r, "Alpha"));
        Assert.AreEqual(242.21679359177304, r.IndividualRows[0].Mode1, 1e-10);
        Assert.AreEqual(289.125, r.IndividualRows[0].Mode2, 1e-10);
        Assert.AreEqual(247.95, r.IndividualRows[1].Mode2, 1e-10);
        Assert.AreEqual(379.125, r.AdjacentGroups.Single().EffectiveLengths.Mode1, 1e-10);
        Assert.AreEqual(379.125, r.AdjacentGroups.Single().EffectiveLengths.Mode2, 1e-10);
        Assert.AreEqual(32.4, r.ClearDistanceToRootOrWeld, 1e-12);
    }

    [TestMethod]
    public void SciC1InternalPlateResistancesReproduceIndividualAndGroupBoltModes()
    {
        var g = TStubInternalEndPlateLengths.Calculate(Sci());
        var input = g.CreateInput(Code(), Bolt(), 25, 265, new[] { 300000.0, 300000 }, TStubMode1Method.DistributedContact, 39.55);
        var r = TStubBoltRows.Verify(input);
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(9, r.Checks.Count);
        double Capacity(string id) => r.Checks.Single(c => c.Id == id).Resistance;
        Assert.AreEqual(501961.6666666667, Capacity("Row1.TStub.Mode2"), 1e-6);
        Assert.AreEqual(462649.8962386511, Capacity("Row2.TStub.Mode2"), 1e-6);
        Assert.AreEqual(813809.0337224384, Capacity("Rows1To2.TStub.Mode2"), 1e-6);
        Assert.AreEqual(406656, Capacity("Row1.TStub.Mode3"), 1e-8);
        Assert.AreEqual(813312, Capacity("Rows1To2.TStub.Mode3"), 1e-8);
        // Published SCI P398 C1 pp91,95,100:406kN per row,812kN pair; intermediate rounding.
        Assert.AreEqual(406000, Capacity("Row1.TStub.Mode3"), 1000);
        Assert.AreEqual(812000, Capacity("Rows1To2.TStub.Mode3"), 1500);
        Assert.IsTrue(r.Scope.Contains("No force allocation"));
    }

    [TestMethod]
    public void ThreeInternalRowsDistinguishGroupsWithAndWithoutTheTopFlange()
    {
        var r = TStubInternalEndPlateLengths.Calculate(Sci(new[] { 44.4, 144.4, 234.4 }));
        Assert.IsTrue(r.IsSupported); Assert.AreEqual(3, r.AdjacentGroups.Count);
        Assert.AreEqual(389.125, r.AdjacentGroups.Single(g => g.FirstRow == 0 && g.RowCount == 2).EffectiveLengths.Mode2, 1e-9);
        Assert.AreEqual(337.95, r.AdjacentGroups.Single(g => g.FirstRow == 1 && g.RowCount == 2).EffectiveLengths.Mode2, 1e-9);
        Assert.AreEqual(479.125, r.AdjacentGroups.Single(g => g.RowCount == 3).EffectiveLengths.Mode2, 1e-9);
        Assert.AreEqual(622.216793591773, Trace(r, "InternalRows1To3.CircularLength"), 1e-9);
    }

    [TestMethod]
    public void ExternalRowCannotBeSmuggledIntoInternalGroup()
        => Unsupported(TStubInternalEndPlateLengths.Calculate(Sci(new[] { -40.0, 44.4, 134.4 })), "OutsideClearBeamAndPlateGeometry");

    [TestMethod]
    public void LastTensionRowCannotBeAssumedUnaffectedByNearbyCompressionFlange()
        => Unsupported(TStubInternalEndPlateLengths.Calculate(Sci(clear: 240)), "CompressionFlangeTooClose");

    [TestMethod]
    public void AlphaPointCannotBeExtrapolatedBeyondFigureDomain()
        => Unsupported(TStubInternalEndPlateLengths.Calculate(Sci(new[] { 250.0 }, clear: 1000)), "AlphaPointOutsideFigure611QualifiedDomain");

    [TestMethod]
    public void AlphaBelowTheUnstiffenedAsymptoteIsNotAValidPointForThisGeometry()
        => Unsupported(TStubInternalEndPlateLengths.Calculate(Sci(alpha: 6)), "AlphaBelowUnstiffenedAsymptote");

    [TestMethod]
    public void AlphaAndDetailSourcesAreRequiredEvenWithNoDesignLoad()
    {
        Unsupported(TStubInternalEndPlateLengths.Calculate(Sci(alphaSource: " ")), "AlphaDerivationRequired");
        Unsupported(TStubInternalEndPlateLengths.Calculate(Sci(detail: " ")), "DetailSourceRequired");
    }

    [TestMethod]
    public void AlphaValueIsNotSilentlySubstitutedByDifferentSciApproximation()
    {
        var a = TStubInternalEndPlateLengths.Calculate(Sci(alpha: 7.5));
        var b = TStubInternalEndPlateLengths.Calculate(Sci(alpha: 7.343053807634066,
            alphaSource: "Separate SCI P398 AppendixG approximation, not C1 graph reading"));
        Assert.IsTrue(b.IsSupported); Assert.IsTrue(b.IndividualRows[0].Mode2 < a.IndividualRows[0].Mode2);
        Assert.AreEqual(a.IndividualRows[0].Mode1, b.IndividualRows[0].Mode1);
        Assert.IsTrue(b.AdjacentGroups.Single().EffectiveLengths.Mode2 < a.AdjacentGroups.Single().EffectiveLengths.Mode2);
        var verifyA = TStubBoltRows.Verify(a.CreateInput(Code(), Bolt(), 25, 265,
            new[] { 300000.0, 300000 }, TStubMode1Method.DistributedContact, 39.55));
        var verifyB = TStubBoltRows.Verify(b.CreateInput(Code(), Bolt(), 25, 265,
            new[] { 300000.0, 300000 }, TStubMode1Method.DistributedContact, 39.55));
        Assert.AreEqual(808032.5419200811,
            verifyB.Checks.Single(c => c.Id == "Rows1To2.TStub.Mode2").Resistance, 1e-6);
        Assert.AreEqual(813312,
            verifyA.Checks.Where(c => c.Id.StartsWith("Rows1To2.")).Min(c => c.Resistance), 1e-6);
        Assert.AreEqual(808032.5419200811,
            verifyB.Checks.Where(c => c.Id.StartsWith("Rows1To2.")).Min(c => c.Resistance), 1e-6);
    }

    [TestMethod]
    public void LastRowAndTopFlangeWeldBothConstrainRealHardwareClearance()
    {
        var r = TStubInternalEndPlateLengths.Calculate(Sci());
        // Width66 clears the web weld distance36.95 but not top-flange weld distance32.4.
        Assert.ThrowsException<ArgumentException>(() => r.CreateInput(Code(), Bolt(), 25, 265,
            new[] { 0.0, 0 }, TStubMode1Method.DistributedContact, 66));
    }

    [TestMethod]
    public void OneInternalRowRequiresNoImaginaryGroup()
    {
        var r = TStubInternalEndPlateLengths.Calculate(Sci(new[] { 44.4 }));
        Assert.IsTrue(r.IsSupported); Assert.AreEqual(1, r.IndividualRows.Count); Assert.AreEqual(0, r.AdjacentGroups.Count);
        var v = TStubBoltRows.Verify(r.CreateInput(Code(), Bolt(), 25, 265, new[] { 100000.0 }));
        Assert.AreEqual(ConnectionStatus.Passed, v.Status);
    }

    [TestMethod]
    public void InternalRowOrderAndSpacingCannotBeDegenerate()
    {
        Unsupported(TStubInternalEndPlateLengths.Calculate(Sci(new[] { 44.4, 44.4 })), "MustIncreaseWithFinitePitch");
        Unsupported(TStubInternalEndPlateLengths.Calculate(Sci(new[] { 134.4, 44.4 })), "MustIncreaseWithFinitePitch");
        var r = TStubInternalEndPlateLengths.Calculate(Sci(new[] { 44.4, 60.0 }));
        Assert.ThrowsException<ArgumentException>(() => r.CreateInput(Code(), Bolt(), 25, 265, new[] { 0.0, 0 }));
    }

    [TestMethod]
    public void OpposingColumnFlangeWidthLimitsEminIndependentlyOfYieldLineLengths()
    {
        var p = Sci();
        var narrow = new TStubInternalEndPlateGeometry(250, 200, 100, 10.1, 8, 12, 8, 501.9,
            p.OrderedInternalRows, p.FirstRowAlpha, p.DetailSource);
        var r = TStubInternalEndPlateLengths.Calculate(narrow);
        Assert.IsTrue(r.IsSupported); Assert.AreEqual(50, r.Emin);
        Assert.AreEqual(289.125, r.IndividualRows[0].Mode2, 1e-10);
    }

    [TestMethod]
    public void RowInputSnapshotCannotChangeAfterConstruction()
    {
        var coordinates = new[] { 44.4, 134.4 }; var p = Sci(coordinates); coordinates[0] = 0;
        Assert.IsTrue(TStubInternalEndPlateLengths.Calculate(p).IsSupported);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<double>)p.OrderedInternalRows)[0] = 0);
    }

    [DataTestMethod]
    [DataRow(4.44)] [DataRow(8.01)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void AlphaOutsidePublishedCurveRangeIsRejected(double alpha)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TStubChartAlpha(alpha, "source"));

    [TestMethod]
    public void NullAndNonFiniteInputsAreRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => TStubInternalEndPlateLengths.Calculate(null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Sci(Array.Empty<double>()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Sci(new[] { double.NaN }));
    }
}
