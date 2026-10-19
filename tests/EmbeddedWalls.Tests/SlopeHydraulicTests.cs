using GPC.Checkers.Geotechnics.Seepage;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.Seepage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public class SlopeHydraulicTests
{
    private static readonly Soil Soil = new("synthetic", 18e-6, 20e-6, Math.PI / 6, .005, "independent analytic cases", .04);
    private static readonly SlopeFactors Factors = new("Characteristic", 1, 1, 1, 1, 1, 1, 0, 0, false);
    private static readonly SlopeBody Body = new("body", [new(-400, -400), new(400, -400), new(400, 0), new(-400, 0)], 25e-6);
    private static SlipCircle Circle() => new(0, 1000, 2000, -Math.Sqrt(3) * 1000, Math.Sqrt(3) * 1000);
    private static SeepageResult Field(Func<SeepageNode, double> head, bool hole = false)
    {
        var m = SeepageTests.Rectangle(10, 10, 4000, 4000);
        if (hole)
        {
            var triangles = m.Triangles.Where(t => {
                double x = (m.Nodes[t.A].X + m.Nodes[t.B].X + m.Nodes[t.C].X) / 3;
                double z = (m.Nodes[t.A].Z + m.Nodes[t.B].Z + m.Nodes[t.C].Z) / 3;
                return !(x > 1600 && x < 2400 && z > 2800 && z < 3200);
            }).ToArray();
            int[] used = triangles.SelectMany(t => new[] { t.A, t.B, t.C }).Distinct().Order().ToArray();
            var map = used.Select((old, i) => (old, i)).ToDictionary(p => p.old, p => p.i);
            m.Nodes = used.Select(i => m.Nodes[i]).ToArray(); m.Triangles = triangles.Select(t => new SeepageTriangle(map[t.A], map[t.B], map[t.C])).ToArray();
        }
        m.Materials = [new("VG", .01, .01, 0, "given", new VanGenuchtenCurve(.001, 2))];
        m.Heads = m.Nodes.Select((p, i) => new SeepageHead(i, head(p))).ToArray();
        var result = SeepageAnalysis.Calculate(m); Assert.IsTrue(result.IsComplete, result.Diagnostic); return result;
    }
    private static SlopeSection Section(ISlopeHydraulicField? field, bool body = false, bool water = false)
    {
        SlopePoint[] surface = [new(-1900, 0), new(1900, 0)]; SlopeLayer[] layers = [new(Soil, -2000)];
        SlopePoint[] heads = water ? [new(-1900, -200), new(1900, -200)] : [];
        SlopeBody[] bodies = body ? [Body] : [];
        return field == null ? new(surface, layers, heads, bodies, null, -500, 500)
            : new(surface, layers, heads, bodies, null, -500, 500, null, 0, SoilUnits.WaterUnitWeight, field);
    }

    [TestMethod]
    public void HydrostaticFemRecoversLegacyWeightsPressuresAndFirstMoments()
    {
        var field = new SeepageSlopeField(Field(_ => 3000), "Hydrostatic", 2000, 3200, ignoreSuction: true);
        foreach (bool body in new[] { false, true })
        {
            var expected = SlopeStability.Slices(Section(null, body, true), Circle(), Factors, 40);
            var actual = SlopeStability.Slices(Section(field, body), Circle(), Factors, 40);
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.AreEqual(expected[i].PorePressure, actual[i].PorePressure, 1e-12, "hydrostatic pressure");
                Assert.AreEqual(expected[i].SoilWeight, actual[i].SoilWeight, 1e-10, "integrated weight");
                Assert.AreEqual(expected[i].BodyWeight, actual[i].BodyWeight, 1e-12, "body weight");
                Assert.AreEqual(expected[i].WeightX, actual[i].WeightX, 1e-8, "weight x");
                Assert.AreEqual(expected[i].WeightY, actual[i].WeightY, 1e-8, "weight y");
                Assert.AreEqual(expected[i].Driving, actual[i].Driving, 1e-10, "driving moment/R");
            }
        }
        var partition = field.Column(100, -1000, 0);
        Assert.AreEqual(2, partition.Count); Assert.AreEqual(-200, partition[0].Top, 1e-9);
        Assert.IsTrue(partition[0].Saturated && !partition[1].Saturated);
    }

    [TestMethod]
    public void ImpermeableBodyHoleIsExcludedBeforeSamplingSoilColumn()
    {
        var field = new SeepageSlopeField(Field(_ => 3000, true), "hole", 2000, 3200, ignoreSuction: true);
        var actual = SlopeStability.Slices(Section(field, true), Circle(), Factors, 40);
        var legacy = SlopeStability.Slices(Section(null, true, true), Circle(), Factors, 40);
        Assert.AreEqual(legacy.Sum(s => s.SoilWeight), actual.Sum(s => s.SoilWeight), 1e-9);
        Assert.AreEqual(800 * 400 * 25e-6, actual.Sum(s => s.BodyWeight), 1e-10);
        Assert.ThrowsException<ArgumentException>(() => field.Column(0, -1000, 0), "A column crossing the body hole must fail when not explicitly excluded.");
    }

    [TestMethod]
    public void AffineNonhydrostaticPressureTransfersWithOriginReflectionAndNoDoubleWater()
    {
        var field = new SeepageSlopeField(Field(p => 6000 - .1 * p.X - .2 * p.Z), "affine", 2000, 3200, -1);
        var section = Section(field); var slices = SlopeStability.Slices(section, Circle(), Factors, 40);
        foreach (var s in slices)
        {
            double x = (s.Left + s.Right) / 2;
            double expected = SoilUnits.WaterUnitWeight * (6000 - .1 * (2000 - x) - 1.2 * (3200 + s.BaseY));
            Assert.AreEqual(expected, s.PorePressure, 1e-12);
        }
        // Circular segment with R=2000 mm and depth h=1000 mm, entirely saturated: gammaSat times area.
        double theta = 2 * Math.Acos(.5), area = 2000 * 2000 * (theta - Math.Sin(theta)) / 2;
        Assert.AreEqual(area * 20e-6, slices.Sum(s => s.SoilWeight), 1e-6);
        var undrained = new SlopeFactors("ND", 1, 1, 1, 1, 1.4, 1, 0, 0, true);
        var nd = SlopeStability.Slices(section, Circle(), undrained, 40);
        Assert.IsTrue(nd.All(s => s.PorePressure == 0 && s.FrictionAngle == 0));
        Assert.AreEqual(.04 / 1.4, nd[0].Cohesion, 1e-15);
        Assert.AreEqual(slices.Sum(s => s.SoilWeight), nd.Sum(s => s.SoilWeight), 1e-12);
    }

    [TestMethod]
    public void MissingCoverageSuctionAndConflictingHydraulicDescriptionsAreExplicitFailures()
    {
        var field = new SeepageSlopeField(Field(_ => 3000), "hydrostatic", 2000, 3200);
        Assert.ThrowsException<ArgumentException>(() => field.Pressure(0, 0), "No implicit suction strengthening.");
        Assert.ThrowsException<ArgumentException>(() => field.Pressure(5000, -500), "No extrapolation.");
        var search = new SlopeSearch(-1800, -600, 600, 1800, 200, 1000, 3, 20, 0);
        Assert.ThrowsException<ArgumentException>(() => SlopeStability.Validate(Section(field, water: true), search, [Factors]));
        var invalid = Section(new IncompleteField());
        Assert.ThrowsException<ArgumentException>(() => SlopeStability.Slices(invalid, Circle(), Factors, 20), "No missing moisture interval silently accepted.");
    }
    [TestMethod]
    public void CompleteBishopSearchMatchesHydrostaticReferenceWithFemField()
    {
        SlopePoint[] surface = [new(0, 0), new(1000, 0), new(2000, 1000), new(4000, 1000)];
        SlopeLayer[] layers = [new(Soil, -1900)]; SlopePoint[] water = [new(0, -500), new(4000, -500)];
        var hydrostatic = new SlopeSection(surface, layers, water, null, null, 1000, 2000);
        var field = new SeepageSlopeField(Field(_ => 1500), "Bishop comparison", originZ: 2000, ignoreSuction: true);
        var fem = new SlopeSection(surface, layers, null, null, null, 1000, 2000, null, 0, SoilUnits.WaterUnitWeight, field);
        var search = new SlopeSearch(100, 900, 2200, 3500, 200, 1800, 3, 20, 0);
        var expected = SlopeStability.Calculate(hydrostatic, search, [Factors]);
        var actual = SlopeStability.Calculate(fem, search, [Factors]);
        var a = actual.Cases.Single(); var e = expected.Cases.Single();
        Assert.IsNotNull(e.Critical); Assert.IsNotNull(a.Critical);
        Assert.AreEqual(e.Critical.Factor, a.Critical.Factor, 1e-9); Assert.AreEqual(e.Critical.Ratio, a.Critical.Ratio, 1e-9);
        Assert.AreEqual(e.Status, a.Status); Assert.AreEqual(e.GeometricallyValid, a.GeometricallyValid);
        Assert.AreEqual(e.Solved, a.Solved); Assert.AreEqual(e.NumericalFailures, a.NumericalFailures);
        Assert.IsTrue(actual.Notes.Any(n => n.Contains("Bishop comparison")));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => SlopeStability.Calculate(fem, search, [Factors], cancellation.Token));
    }
    private sealed class IncompleteField : ISlopeHydraulicField
    {
        public string Source => "Invalid test field";
        public double WaterUnitWeight => SoilUnits.WaterUnitWeight;
        public double Pressure(double x, double y) => 0;
        public IReadOnlyList<SlopeMoistureInterval> Column(double x, double bottom, double top) => [];
    }
}
