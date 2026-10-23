using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class TStubBoltRowsTests
{
    private static ConnectionDesignCode Code() => ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "SCI P398 C1 UK factors");
    private static ConnectionBolt Bolt() => new(24, 26, 353, ConnectionBoltGrade.Class8_8, true);
    private static TStubTensionInput Row(ConnectionDesignCode code, ConnectionBolt bolt, double demand,
        int rows = 1, double l1 = 210, double l2 = 233, double t = 20.5)
        => new(code, bolt, t, 265, 33.4, 75, rows, new(l1, l2,
            "SCI P398 C1 pp86-97; individual or explicitly identified group effective length"), demand,
            TStubMode1Method.DistributedContact, 39.55);
    private static TStubBoltRowsInput Pair(double first = 300000, double second = 300000, double? group = null)
    {
        var code = Code(); var bolt = Bolt();
        return new(new[] { Row(code, bolt, first), Row(code, bolt, second) },
            new[] { new TStubAdjacentRows(0, 2, Row(code, bolt, group ?? first + second, 2, 332, 332)) }, "SCI P398 C1 rows1-2");
    }
    private static void Unsupported(ConnectionVerification<TStubBoltRowsInput> r, string diagnostic)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.AreEqual(0, r.Checks.Count);
        Assert.IsNull(r.Utilization); Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(diagnostic)), string.Join("; ", r.Diagnostics));
    }

    [TestMethod]
    public void AdjacentPairPassesOnlyWhenEachRowAndItsGroupPass()
    {
        var r = TStubBoltRows.Verify(Pair());
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(9, r.Checks.Count);
        Assert.AreEqual(697838.6360612109, r.Checks.Single(c => c.Id == "Rows1To2.TStub.Mode2").Resistance, 1e-6);
        Assert.AreEqual(600000, r.Checks.Single(c => c.Id == "Rows1To2.TStub.Mode2").Demand);
        Assert.AreEqual("Rows1To2.TStub.Mode2", r.GoverningCheck.Id);
    }

    [TestMethod]
    public void GroupFailureCannotBeHiddenByPassingIndividualRows()
    {
        var r = TStubBoltRows.Verify(Pair(380000, 380000));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.Checks.Where(c => c.Id.StartsWith("Row1.") || c.Id.StartsWith("Row2.")).All(c => c.Passed));
        Assert.IsFalse(r.Checks.Single(c => c.Id == "Rows1To2.TStub.Mode2").Passed);
        Assert.AreEqual(760000, r.GoverningCheck.Demand);
    }

    [TestMethod]
    public void IndividualFailureCannotBeHiddenByPassingGroup()
    {
        var r = TStubBoltRows.Verify(Pair(410000, 100000));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.Checks.Where(c => c.Id.StartsWith("Rows1To2.")).All(c => c.Passed));
        Assert.IsFalse(r.Checks.Single(c => c.Id == "Row1.TStub.Mode2").Passed);
    }

    [TestMethod]
    public void GroupForceMustEqualConcomitantIndividualSum()
        => Unsupported(TStubBoltRows.Verify(Pair(380000, 380000, 600000)), "AdjacentGroupTensionMismatch");

    [TestMethod]
    public void AnyNonzeroGroupForceMismatchIsDetectedAtTinyScale()
        => Unsupported(TStubBoltRows.Verify(Pair(1e-120, 2e-120, 2e-120)), "AdjacentGroupTensionMismatch");

    [TestMethod]
    public void ThreeRowsRequireBothAdjacentPairsAndWholeTriple()
    {
        var code = Code(); var bolt = Bolt();
        var rows = new[] { Row(code, bolt, 350000), Row(code, bolt, 350000), Row(code, bolt, 100000) };
        var groups = new[] { new TStubAdjacentRows(0, 2, Row(code, bolt, 700000, 2, 332, 332)),
            new TStubAdjacentRows(1, 2, Row(code, bolt, 450000, 2, 322, 322)),
            new TStubAdjacentRows(0, 3, Row(code, bolt, 800000, 3, 422, 422)) };
        var r = TStubBoltRows.Verify(new(rows, groups, "SCI C1 complete three-row column-flange zone"));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status); Assert.AreEqual(18, r.Checks.Count);
        Assert.IsTrue(r.Checks.Where(c => c.Id.StartsWith("Rows1To3.")).All(c => c.Passed));
        Assert.IsFalse(r.Checks.Single(c => c.Id == "Rows1To2.TStub.Mode2").Passed);
        for (int missing = 0; missing < groups.Length; missing++)
            Unsupported(TStubBoltRows.Verify(new(rows, groups.Where((_, i) => i != missing), "Ordered geometry")), "MissingAdjacentGroup");
    }

    [TestMethod]
    public void ZeroLoadDoesNotWaiveAnUnspecifiedSubgroup()
    {
        var code = Code(); var bolt = Bolt();
        Unsupported(TStubBoltRows.Verify(new(new[] { Row(code, bolt, 0), Row(code, bolt, 0) },
            Array.Empty<TStubAdjacentRows>(), "Rows")), "MissingAdjacentGroup");
    }

    [TestMethod]
    public void DuplicateGroupsAreRejected()
    {
        var p = Pair();
        Unsupported(TStubBoltRows.Verify(new(p.OrderedRows, p.AdjacentGroups.Concat(p.AdjacentGroups), p.RowLayoutSource)), "DuplicateAdjacentGroup");
    }

    [TestMethod]
    public void GroupsOutsideTheRowSetAreRejected()
    {
        var p = Pair();
        var bad = new TStubAdjacentRows(1, 2, p.AdjacentGroups[0].Component);
        Unsupported(TStubBoltRows.Verify(new(p.OrderedRows, new[] { bad }, "Rows")), "AdjacentGroupOutsideRowSet");
    }

    [TestMethod]
    public void AGroupComponentCannotPretendToBeAnIndividualRow()
    {
        var p = Pair();
        Unsupported(TStubBoltRows.Verify(new(new[] { p.AdjacentGroups[0].Component }, Array.Empty<TStubAdjacentRows>(), "Rows")), "RequiresSingleRowComponent");
    }

    [TestMethod]
    public void ComponentBoltCountMustMatchTheSpecifiedGroup()
    {
        var p = Pair(); var first = p.OrderedRows[0];
        var wrong = new TStubAdjacentRows(0, 2, Row(first.Code, first.Bolt, 600000, 1, 332, 332));
        Unsupported(TStubBoltRows.Verify(new(p.OrderedRows, new[] { wrong }, "Rows")), "AdjacentGroupBoltRowCountMismatch");
    }

    [TestMethod]
    public void MixingDifferentFlangeThicknessesIsBlocked()
    {
        var p = Pair(); var first = p.OrderedRows[0];
        var wrong = Row(first.Code, first.Bolt, 300000, t: 25);
        Unsupported(TStubBoltRows.Verify(new(new[] { first, wrong }, p.AdjacentGroups, "Rows")), "InconsistentFlangeSnapshot");
    }

    [TestMethod]
    public void MixingSeparateCodeOrBoltSnapshotsIsBlocked()
    {
        var p = Pair(); var first = p.OrderedRows[0];
        var wrongCode = Row(Code(), first.Bolt, 300000);
        var wrongBolt = Row(first.Code, Bolt(), 300000);
        Unsupported(TStubBoltRows.Verify(new(new[] { first, wrongCode }, p.AdjacentGroups, "Rows")), "InconsistentFlangeSnapshot");
        Unsupported(TStubBoltRows.Verify(new(new[] { first, wrongBolt }, p.AdjacentGroups, "Rows")), "InconsistentFlangeSnapshot");
    }

    [TestMethod]
    public void MissingGroupLengthDerivationRemovesEveryFavourablePartialCheck()
    {
        var p = Pair(); var first = p.OrderedRows[0];
        var unsupported = new TStubTensionInput(first.Code, first.Bolt, 20.5, 265, 33.4, 75, 2,
            new(332, 332, " "), 600000, TStubMode1Method.DistributedContact, 39.55);
        Unsupported(TStubBoltRows.Verify(new(p.OrderedRows, new[] { new TStubAdjacentRows(0, 2, unsupported) }, "Rows")), "EffectiveLengthDerivationRequired");
    }

    [TestMethod]
    public void MissingPhysicalOrderEvidenceCannotPass()
    {
        var p = Pair();
        Unsupported(TStubBoltRows.Verify(new(p.OrderedRows, p.AdjacentGroups, " ")), "OrderedRowLayoutDerivationRequired");
    }

    [TestMethod]
    public void ArraysAreCopiedAndReadOnly()
    {
        var p = Pair(); var rows = p.OrderedRows.ToArray(); var groups = p.AdjacentGroups.ToArray();
        var input = new TStubBoltRowsInput(rows, groups, "Rows");
        rows[0] = null!; groups[0] = null!;
        Assert.AreEqual(ConnectionStatus.Passed, TStubBoltRows.Verify(input).Status);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<TStubTensionInput>)input.OrderedRows)[0] = p.OrderedRows[1]);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<TStubAdjacentRows>)input.AdjacentGroups).Clear());
    }

    [TestMethod]
    public void OneIsolatedRowRequiresNoInventedGroup()
    {
        var r = TStubBoltRows.Verify(new(new[] { Row(Code(), Bolt(), 300000) }, Array.Empty<TStubAdjacentRows>(), "Isolated row"));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(3, r.Checks.Count);
    }

    [TestMethod]
    public void ReversingPhysicalRowOrderPreservesCapacityWhenGroupsFollowTheSameOrder()
    {
        var a = TStubBoltRows.Verify(Pair(200000, 380000));
        var b = TStubBoltRows.Verify(Pair(380000, 200000));
        Assert.AreEqual(a.Status, b.Status); Assert.AreEqual(a.Utilization!.Value, b.Utilization!.Value, 1e-12);
    }

    [TestMethod]
    public void ZeroLoadIsValidOnlyForACompleteMechanismSet()
    {
        var r = TStubBoltRows.Verify(Pair(0, 0));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(0, r.Utilization);
    }

    [TestMethod]
    public void InvalidCollectionsAndIndicesAreRejected()
    {
        var c = Row(Code(), Bolt(), 0);
        Assert.ThrowsException<ArgumentNullException>(() => new TStubBoltRowsInput(null!, Array.Empty<TStubAdjacentRows>(), ""));
        Assert.ThrowsException<ArgumentNullException>(() => new TStubBoltRowsInput(new[] { c }, null!, ""));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TStubBoltRowsInput(Array.Empty<TStubTensionInput>(), Array.Empty<TStubAdjacentRows>(), ""));
        Assert.ThrowsException<ArgumentException>(() => new TStubBoltRowsInput(new TStubTensionInput[] { null! }, Array.Empty<TStubAdjacentRows>(), ""));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TStubAdjacentRows(-1, 2, c));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TStubAdjacentRows(0, 1, c));
        Assert.ThrowsException<ArgumentNullException>(() => TStubBoltRows.Verify(null!));
    }
}
