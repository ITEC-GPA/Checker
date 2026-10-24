using System.Text.Json;
using GPC.Checkers.Concrete.Piles;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Walls;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Concrete;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesignTests;

[TestClass]
public sealed class EmbeddedWallRcPileTests
{
    static CircularRcSection Section(int divisions = 64) => new(800, 50, 12, 20, 10, 150, 30, 450, 200000, .075, "C30/B450 independent fixture", divisions);
    static EmbeddedWallRcPile Definition(bool shear = true, int divisions = 64) => new("RC", Section(divisions), 1000, true,
        "Single row; uniform strip transfer explicitly assumed for this test", shear, .9, 0, "XC2", 20, 35, 10, PileDetailingMode.Beam, true, true);
    static EmbeddedWallModel Model(double n = 100000, double force = 1000) => new() {
        Name = "RC pile cantilever", CaseId = "ULS independent", Top = 0, Bottom = -6000, TributaryWidth = 1500,
        MaximumElementLength = 6000, FixBottomDisplacement = true, FixBottomRotation = true,
        Segments = [new() { Top = 0, Bottom = -6000, FlexuralRigidity = Definition().FlexuralRigidity(1500), SectionId = "RC", Source = "gross circle" }],
        Stages = [new() { Id = "F1", Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 },
            PointLoads = [new() { Elevation = 0, HorizontalForce = force, DownwardForce = n }] }] };

    [TestMethod]
    public void IndependentFibresAndPolygonRefinementMatchNativeConcrete()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rc-pile-wall.json")));
        foreach (int divisions in new[] { 64, 128 })
        {
            var session = Definition(divisions: divisions).CreateSession();
            foreach (var expected in json.RootElement.GetProperty(divisions == 64 ? "cases" : "discretization128").EnumerateArray())
            foreach (int sign in new[] { -1, 1 })
            {
                double n = expected.GetProperty("N").GetDouble(), m = expected.GetProperty("M").GetDouble();
                var checks = session.CheckUltimate(new WallSectionActions(-n, 1000, sign * 10e6));
                Assert.IsTrue(checks[0].Capacity.HasValue, checks[0].Diagnostic);
                // Native discretized stress integration and exact-N stopping tolerance: <=0.3% versus independent fibres.
                Assert.AreEqual(m, checks[0].Capacity!.Value, .003 * m, $"{divisions}, N={n}, sign={sign}");
                Assert.AreEqual(EngineeringOutcome.Satisfied, checks[0].Outcome);
            }
        }
    }

    [TestMethod]
    public void GeometryOwnershipAndEquivalentStiffnessAreIndependentOfResistanceMesh()
    {
        var definition = Definition(); var section = definition.Section;
        double expectedE = 22000 * Math.Pow(3.8, .3), expectedI = Math.PI * Math.Pow(800, 4) / 64;
        Assert.AreEqual(expectedE, section.ConcreteModulus, 1e-7);
        Assert.AreEqual(expectedE * expectedI * 1.5, definition.FlexuralRigidity(1500), expectedE * expectedI * 1e-12);
        Assert.AreEqual(definition.FlexuralRigidity(1500), Definition(divisions: 128).FlexuralRigidity(1500));
        var first = section.CreateSection(); var second = section.CreateSection();
        Assert.AreNotSame(first, second); Assert.AreNotSame(first.ConcreteMaterial, second.ConcreteMaterial);
        Assert.AreNotSame(first.Rebars.First().RebarMaterial, second.Rebars.First().RebarMaterial);
        Assert.AreEqual(330, section.BarRadius); Assert.AreEqual(12, first.Rebars.Count());
    }

    [TestMethod]
    public void AllCutsRetainTheirOwnActionsAndAnalyticalDeflection()
    {
        var definition = Definition(); var model = Model(); var analysis = EmbeddedWallAnalysis.Calculate(model);
        Assert.IsTrue(analysis.IsComplete, analysis.Diagnostic);
        double displacement = 1000 * Math.Pow(6000, 3) / (3 * definition.FlexuralRigidity(1500));
        Assert.AreEqual(displacement, analysis.Stages[0].Points[0].Displacement, 1e-8);
        var results = EmbeddedWallRcPiles.Check(analysis, 1500, [definition], EmbeddedWallSectionCheckKind.Ultimate);
        Assert.AreEqual(analysis.Stages[0].Points.Count, results.Count);
        var direct = definition.CreateSession();
        foreach (var (r, p) in results.Zip(analysis.Stages[0].Points, (result, point) => (result, point)))
        {
            Assert.AreEqual("ULS independent", r.CaseId); Assert.AreEqual("F1", r.StageId); Assert.AreEqual(p.Side, r.Side);
            Assert.AreEqual(-p.NormalForce * 2 / 3, r.Actions!.N, 1e-7);
            Assert.AreEqual(p.Shear * 2 / 3, r.Actions.V, 1e-7); Assert.AreEqual(-p.Moment * 2 / 3, r.Actions.M, 1e-5);
            var expected = direct.CheckUltimate(r.Actions);
            Assert.AreEqual(expected[0].Capacity!.Value, r.Checks[0].Capacity!.Value, 1);
            Assert.AreEqual(expected[1].Capacity!.Value, r.Checks[1].Capacity!.Value, 1e-6);
            Assert.IsTrue(r.ConstructionChecks.Count > 0);
        }
    }

    [TestMethod]
    public void ServiceabilityCompressionAndBendingUseExistingStressAndCrackEngines()
    {
        var session = Definition().CreateSession();
        foreach (var combination in new[] { ServiceabilityCombination.Characteristic, ServiceabilityCombination.Frequent, ServiceabilityCombination.QuasiPermanent })
        {
            var r = session.CheckServiceability(new WallSectionActions(-100000, 0, 0), combination);
            // NTC frequent has the crack check; the existing profile assigns stress limits only to characteristic/quasi-permanent.
            Assert.AreEqual(combination == ServiceabilityCombination.Frequent ? 1 : 2, r.Checks.Count);
            Assert.IsTrue(r.Checks.All(c => c.Outcome == EngineeringOutcome.Satisfied), string.Join("; ", r.Checks.Select(c => c.Diagnostic)));
            if (combination != ServiceabilityCombination.Characteristic) Assert.AreEqual(0, r.CrackResponse.Width!.Value, 1e-12);
            double area = 32 * 400 * 400 * Math.Sin(Math.PI / 32), bars = 12 * Math.PI * 100;
            double modularRatio = 200000 / (22000 * Math.Pow(3.8, .3));
            double stress = 100000 / (area + (modularRatio - 1) * bars);
            Assert.AreEqual(-stress, r.Response.Concrete.Min(p => p.Stress), 1e-6);
            Assert.AreEqual(-stress * modularRatio, r.Response.Bars.First().Stress, 1e-5);
        }
        var plus = session.CheckServiceability(new WallSectionActions(0, 0, 50e6), ServiceabilityCombination.QuasiPermanent);
        var minus = session.CheckServiceability(new WallSectionActions(0, 0, -50e6), ServiceabilityCombination.QuasiPermanent);
        Assert.IsTrue(plus.CrackResponse?.Width > 0, string.Join("; ", plus.Checks.Select(c => c.Diagnostic)));
        Assert.AreEqual(plus.CrackResponse!.Width!.Value, minus.CrackResponse!.Width!.Value, 1e-5);
        Assert.AreEqual(-plus.Curvature!.Value, minus.Curvature!.Value, 1e-9);
    }

    [TestMethod]
    public void MissingConfirmationsInvalidDataAndIncompleteHistoryCannotPass()
    {
        var checks = Definition(false).CreateSession().CheckUltimate(new WallSectionActions(-10000, 0, 0));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, checks[1].Outcome);
        Assert.ThrowsException<ArgumentException>(() => new CircularRcSection(800, 50, 5, 20, 10, 150, 30, 450, 200000, .075, "given"));
        Assert.ThrowsException<ArgumentException>(() => (Definition() with { SingleRowEqualSharingConfirmed = false }).FlexuralRigidity(1000));
        var model = Model(); model.FixBottomDisplacement = model.FixBottomRotation = false;
        Assert.ThrowsException<InvalidOperationException>(() => EmbeddedWallRcPiles.Check(EmbeddedWallAnalysis.Calculate(model), 1500, [Definition()], EmbeddedWallSectionCheckKind.Ultimate));
        var missing = EmbeddedWallRcPiles.Check(EmbeddedWallAnalysis.Calculate(Model()), 1500, [], EmbeddedWallSectionCheckKind.Ultimate);
        Assert.IsTrue(missing.All(r => r.Outcome == EngineeringOutcome.NotEvaluated));
        Assert.ThrowsException<OperationCanceledException>(() => Definition().CreateSession().CheckUltimate(new WallSectionActions(0,0,0), new CancellationToken(true)));
    }

    [TestMethod]
    public void IndependentSessionsAndOppositeActionsRemainIsolated()
    {
        var first = Definition().CreateSession(); var second = Definition().CreateSession();
        var action = new WallSectionActions(-500000, 30000, 100e6);
        var expected = first.CheckUltimate(action).Select(c => c.Capacity).ToArray();
        Parallel.Invoke(() => first.CheckUltimate(new WallSectionActions(250000, -30000, -50e6)),
            () => second.CheckServiceability(new WallSectionActions(0, 0, -20e6), ServiceabilityCombination.QuasiPermanent));
        CollectionAssert.AreEqual(expected, first.CheckUltimate(action).Select(c => c.Capacity).ToArray());
        CollectionAssert.AreEqual(expected, second.CheckUltimate(action).Select(c => c.Capacity).ToArray());
        var outside = first.CheckUltimate(new WallSectionActions(-1e12, 0, 0));
        Assert.IsTrue(outside.All(c => c.Outcome == EngineeringOutcome.NotEvaluated));
    }
}
