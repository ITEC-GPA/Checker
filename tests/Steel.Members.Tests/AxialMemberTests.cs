using GPC.Checkers.Steel.Members;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace SteelMemberTests;

[TestClass]
public sealed class AxialMemberTests
{
    private static readonly StandardNTC2018Steel Standard = new();
    private static SteelSection Section(double d = 355.6, double t = 12.5, double fy = 355, double fu = 510,
        Section.FormedTypes formed = GPC.Model.Sections.Section.FormedTypes.HotFinished) => new(
            t == 0 ? new SectionCircular(d) : new SectionCHS(d, t), new SteelMaterialEN1993("Documented structural grade", 210000, fy, fu), formedType: formed);
    private static AxialSteelMember Member(SteelSection? section = null, int grade = 355, double? l1 = 6000, double? l2 = 8000,
        bool axial = true, string material = "Certificate and thickness", string lengths = "Pinned ends in each principal plane",
        double? net = null, string netSource = "") => new(section ?? Section(), grade, material, axial, l1, l2, lengths, net, netSource);
    private static JsonElement Expected(string name)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "axial-ntc2018.json")));
        return file.RootElement.GetProperty("examples").EnumerateArray().Single(e => e.GetProperty("name").GetString() == name).Clone();
    }
    private static void Close(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 2e-12), "Closed-form independent expected, double precision.");

    [TestMethod]
    public void TensionChecksGrossYieldAndExplicitNetFractureWithProfileFactorsOnce()
    {
        var member = Member(Section(50, 0), net: 1000, netSource: "Hole geometry, stagger and shear lag checked separately");
        var result = Ntc2018AxialSteel.Check(member, 400000, Standard); var expected = Expected("solid50_tension_net1000");
        Close(expected.GetProperty("area").GetDouble(), member.Area);
        Close(expected.GetProperty("inertia").GetDouble(), member.Inertia1);
        Close(expected.GetProperty("yield_capacity").GetDouble(), result.Checks[0].Capacity!.Value);
        Close(expected.GetProperty("fracture_capacity").GetDouble(), result.Checks[1].Capacity!.Value);
        Assert.AreEqual(400000, result.Checks[1].Demand); Close(400000.0 / 367200, result.Checks[1].Utilization!.Value);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, result.Outcome);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Checks[0].Outcome);
        Assert.AreEqual(1, Ntc2018AxialSteel.Check(Member(Section(50, 0)), 100000, Standard).Checks.Count);
    }

    [DataTestMethod]
    [DataRow("chs355_6x12_5_hot_s355", 355.6, 12.5, 355.0, 510.0, 355, 6000.0, 8000.0, false, "a")]
    [DataRow("chs193_7x5_hot_s460_reduced440", 193.7, 5.0, 440.0, 550.0, 460, 4000.0, 6000.0, false, "a0")]
    [DataRow("chs193_7x5_cold_s460_reduced440", 193.7, 5.0, 440.0, 550.0, 460, 4000.0, 6000.0, true, "c")]
    [DataRow("solid60_compression_s235", 60.0, 0.0, 235.0, 360.0, 235, 3000.0, 4000.0, false, "c")]
    public void CompressionAndBothBucklingAxesMatchIndependentFormula(string name, double d, double t, double fy, double fu,
        int grade, double l1, double l2, bool cold, string curve)
    {
        var member = Member(Section(d, t, fy, fu, cold ? GPC.Model.Sections.Section.FormedTypes.ColdFormed : GPC.Model.Sections.Section.FormedTypes.HotFinished), grade, l1, l2);
        var result = Ntc2018AxialSteel.Check(member, -100000, Standard); var expected = Expected(name);
        Close(expected.GetProperty("yield_capacity").GetDouble(), result.Checks[0].Capacity!.Value);
        for (int axis = 0; axis < 2; axis++)
        {
            var actual = result.Checks[axis + 1]; var independent = expected.GetProperty("axes")[axis];
            Close(independent.GetProperty("ncr").GetDouble(), actual.CriticalForce!.Value);
            Close(independent.GetProperty("normalized_slenderness").GetDouble(), actual.NormalizedSlenderness!.Value);
            Close(independent.GetProperty("chi").GetDouble(), actual.Reduction!.Value);
            Close(independent.GetProperty("capacity").GetDouble(), actual.Capacity!.Value);
            Assert.AreEqual(curve, actual.Curve);
        }
        Assert.AreEqual(-100000, result.NormalForce); Assert.AreEqual(100000, result.Checks[0].Demand);
    }

    [TestMethod]
    public void MissingInformationAndUnsupportedModelsNeverPass()
    {
        var cases = new[] { Member(axial: false), Member(material: ""), Member(Section(200, 1)), Member(l2: null),
            Member(lengths: ""), Member(Section(200, 8, 500, 600), 500), Member(net: 1000),
            Member(new SteelSection(new SectionCircular(50), new SteelMaterial("tendon", 200000, 1600, 1860, steelType: SteelMaterial.SteelTypes.Tendon)), 1860) };
        foreach (var member in cases)
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018AxialSteel.Check(member, -10000, Standard).Outcome);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018AxialSteel.Check(Member(net: 1000), 10000, Standard).Outcome);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018AxialSteel.Check(Member(), -10000, new StandardEN1993p11()).Outcome);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018AxialSteel.Check(Member(), -10000, new StandardNTC2018Steel { GammaM1 = 1 }).Outcome);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018AxialSteel.Check(new AxialSteelMember(Section(), 355, "source", true, 6000, 8000, "restraints", isBridge: true), -10000, Standard).Outcome);
        Assert.IsNull(Ntc2018AxialSteel.Check(Member(Section(200, 1)), -10000, Standard).Checks.Single().Capacity);
    }

    [TestMethod]
    public void ShortMemberClassBoundariesAndGoverningFailureRemainExplicit()
    {
        foreach (var pair in new[] { (50.0, 1), (70.0, 2), (90.0, 3), (90.01, 4) })
        {
            var r = Ntc2018AxialSteel.Check(Member(Section(pair.Item1, 1, 235, 360), 235, 1, 1), -100, Standard);
            Assert.AreEqual(pair.Item2, r.CompressionClass);
            if (pair.Item2 < 4) Assert.AreEqual(1, r.Checks[1].Reduction);
        }
        var knownFailure = Ntc2018AxialSteel.Check(Member(l2: null), -1e9, Standard);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, knownFailure.Outcome);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, knownFailure.Checks.Last().Outcome);
        var longMember = Ntc2018AxialSteel.Check(Member(Section(60, 0, 235, 360), 235, 5000, 5000), -100, Standard);
        StringAssert.Contains(longMember.Checks[1].Diagnostic, "maggiore di 200");
    }

    [TestMethod]
    public void UnloadedStrutStillRequiresCompressionApplicabilityAndBothEffectiveLengths()
    {
        var incomplete = Ntc2018AxialSteel.Check(Member(l2: null), 0, Standard, true);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, incomplete.Outcome);
        Assert.AreEqual(3, incomplete.Checks.Count);
        Assert.IsNull(incomplete.Checks[2].Capacity);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018AxialSteel.Check(Member(Section(200, 1)), 0, Standard, true).Outcome);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Ntc2018AxialSteel.Check(Member(), 0, Standard, true).Outcome);
    }

    [TestMethod]
    public void SnapshotIsIndependentAndInputsAreValidated()
    {
        var section = Section(); var member = Member(section); double area = member.Area, fy = member.YieldStrength;
        ((SectionCHS)section.SectionShape).Diameter = 600; section.SteelMaterial.Fyk = 235;
        Assert.AreEqual(area, member.Area); Assert.AreEqual(fy, member.YieldStrength);
        var results = Enumerable.Range(0, 10).AsParallel().Select(_ => Ntc2018AxialSteel.Check(member, -100000, Standard).Checks[2].Capacity).ToArray();
        Assert.IsTrue(results.All(value => value == results[0]));
        Assert.ThrowsException<ArgumentException>(() => Member(net: -1));
        Assert.ThrowsException<ArgumentException>(() => Member(l1: 0));
        Assert.ThrowsException<ArgumentException>(() => Member(l2: double.NaN));
        Assert.ThrowsException<ArgumentException>(() => Member(net: 1e20));
        Assert.ThrowsException<ArgumentException>(() => Ntc2018AxialSteel.Check(member, double.NaN, Standard));
    }
}
