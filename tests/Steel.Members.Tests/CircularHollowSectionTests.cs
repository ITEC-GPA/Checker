using System.Text.Json;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteelMemberTests;

[TestClass]
public sealed class CircularHollowSectionTests
{
    private static CircularHollowSteelSection Tube(double d = 200, double t = 8, double fy = 355, bool intact = true, string source = "Certificate") => new(
        new SteelSection(new SectionCHS(d, t), new SteelMaterialEN1993("S" + fy, 210000, fy, 510)), (int)fy, source, intact);
    private static void Close(double expected, double actual, double relative = 2e-10) => Assert.AreEqual(expected, actual, Math.Max(1e-7, Math.Abs(expected) * relative),
        "Independent angular Simpson integration vs inverted cap formula; N, mm, Nmm.");

    [TestMethod]
    public void PlasticEquilibriumMatchesIndependentIntegratedDistributionsForBothSigns()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tube-nmv-ntc2018.json")));
        foreach (var item in fixture.RootElement.GetProperty("examples").EnumerateArray())
        foreach (int signN in new[] { -1, 1 })
        foreach (int signM in new[] { -1, 1 })
        {
            double D(string key) => item.GetProperty(key).GetDouble();
            var tube = Tube(D("diameter"), D("thickness"), D("fy"));
            var r = Ntc2018CircularHollowSections.Check(tube, signN * D("normal"), D("shear"), signM * .8 * D("moment_capacity"), new StandardNTC2018Steel());
            Close(D("area"), tube.Area); Close(D("inertia"), tube.Inertia);
            Close(D("shear_capacity"), r.Checks[0].Capacity!.Value); Close(D("normal_capacity"), r.Checks[1].Capacity!.Value);
            Close(D("moment_capacity"), r.Checks[2].Capacity!.Value); Close(D("reduction"), r.ShearReduction!.Value);
            Close(D("neutral_axis"), r.NeutralAxisDistance!.Value); Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome);
            Assert.IsTrue(Math.Abs(r.AxialEquilibriumResidual!.Value) <= D("normal_capacity") * 1e-10);
            Assert.AreEqual(signN * D("normal"), r.NormalForce); Assert.AreEqual(signM * .8 * D("moment_capacity"), r.Moment);
        }
    }

    [TestMethod]
    public void PureActionsLimitsAndHighShearDoNotProduceSpuriousCapacity()
    {
        var tube = Tube(); var basis = new StandardNTC2018Steel();
        var zero = Ntc2018CircularHollowSections.Check(tube, 0, 0, 0, basis);
        double nr = zero.Checks[1].Capacity!.Value, vr = zero.Checks[0].Capacity!.Value, mr = zero.Checks[2].Capacity!.Value;
        Close(4.0 / 3 * (Math.Pow(100, 3) - Math.Pow(92, 3)) * 355 / 1.05, mr);
        var axial = Ntc2018CircularHollowSections.Check(tube, nr, 0, 0, basis);
        Assert.AreEqual(0, axial.Checks[2].Capacity); Assert.AreEqual(EngineeringOutcome.Satisfied, axial.Outcome);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, Ntc2018CircularHollowSections.Check(tube, 1.01 * nr, 0, 0, basis).Outcome);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, Ntc2018CircularHollowSections.Check(tube, nr, 0, 1, basis).Outcome);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, Ntc2018CircularHollowSections.Check(tube, 0, 0, 1.01 * mr, basis).Outcome);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, Ntc2018CircularHollowSections.Check(tube, 0, 1.01 * vr, 0, basis).Outcome);
        var shearLimit = Ntc2018CircularHollowSections.Check(tube, 0, vr, 0, basis);
        Assert.AreEqual(0, shearLimit.Checks[1].Capacity); Assert.AreEqual(0, shearLimit.Checks[2].Capacity);
        Assert.AreEqual(EngineeringOutcome.Satisfied, shearLimit.Outcome);
        double previous = mr;
        foreach (double fraction in new[] { .5, .6, .8, .99 })
        {
            var r = Ntc2018CircularHollowSections.Check(tube, 0, fraction * vr, 0, basis);
            Assert.IsTrue(r.Checks[2].Capacity <= previous); previous = r.Checks[2].Capacity!.Value;
        }
    }

    [TestMethod]
    public void UnsupportedNormClassOrPerforationIsNotEvaluatedEvenAtZeroForce()
    {
        foreach (var tube in new[] { Tube(200, 2.5, 235), Tube(200, 2, 235), Tube(intact: false), Tube(source: "") })
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018CircularHollowSections.Check(tube, 0, 0, 0, new StandardNTC2018Steel()).Outcome);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018CircularHollowSections.Check(Tube(), 0, 0, 0, new StandardEN1993p11()).Outcome);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Ntc2018CircularHollowSections.Check(Tube(), 0, 0, 0, new StandardNTC2018Steel { GammaM0 = 1 }).Outcome);
        Assert.ThrowsException<ArgumentException>(() => Ntc2018CircularHollowSections.Check(Tube(), double.NaN, 0, 0, new StandardNTC2018Steel()));
        Assert.ThrowsException<OperationCanceledException>(() => Ntc2018CircularHollowSections.Check(Tube(), 0, 0, 0, new StandardNTC2018Steel(), new CancellationToken(true)));
    }

    [TestMethod]
    public void OwnedSectionSnapshotAndIndependentConcurrentChecks()
    {
        var section = new SteelSection(new SectionCHS(200, 8), new SteelMaterialEN1993("S355", 210000, 355, 510));
        var snapshot = new CircularHollowSteelSection(section, 355, "Certificate", true);
        ((SectionCHS)section.SectionShape).Diameter = 400; section.SteelMaterial.Fyk = 235;
        Assert.AreEqual(200, snapshot.Diameter); Assert.AreEqual(355, snapshot.YieldStrength);
        var results = Enumerable.Range(0, 20).AsParallel().Select(_ => Ntc2018CircularHollowSections.Check(snapshot, 200000, 100000, 10000000, new StandardNTC2018Steel())).ToArray();
        Assert.IsTrue(results.All(r => r.Checks[2].Capacity == results[0].Checks[2].Capacity));
    }
}
