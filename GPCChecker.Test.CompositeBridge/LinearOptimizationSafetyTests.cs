using System.Text.Json;
using System.Text.Json.Serialization;
using CompositeBridgePerformance;
using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using GPC.Geometry;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

/// <summary>Pre-optimization safety net. Mechanical oracles do not call section property/solver helpers.</summary>
[TestClass, DoNotParallelize]
public class LinearOptimizationSafetyTests
{
    // MPa: absolute 1e-8 plus relative 1e-9 covers floating-point integration, not an engineering error allowance.
    private static void Stress(double expected, double actual, string context) =>
        Assert.AreEqual(expected, actual, 1e-8 + 1e-9 * Math.Abs(expected), context);

    private static readonly JsonSerializerOptions Json = new() {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals, ReferenceHandler = ReferenceHandler.IgnoreCycles
    };
    private static string Snapshot(HBridgeAnalysisResult r) => JsonSerializer.Serialize(new { r.Geometry, r.Materials, r.Stages }, Json);

    [DataTestMethod]
    [DataRow(0d, BridgeLoadReference.CommonElevation)]
    [DataRow(2.2d, BridgeLoadReference.CommonElevation)]
    [DataRow(5d, BridgeLoadReference.GrossCentroid)]
    [DataRow(.5d, BridgeLoadReference.EffectiveCentroid)]
    public void CumulativeElasticNMAndShrinkageMatchIndependentNetSection(double phi, BridgeLoadReference reference)
    {
        var input = BridgePerformanceCases.Station(0, false, 1)[0];
        foreach (bool bars in new[] { false, true })
        foreach (var loads in new[] { (N: -800d, M: 1600d), (N: 500d, M: -900d), (N: 0d, M: 1500d), (N: -100d, M: 0d), (N: 0d, M: 0d) })
        {
            var d = input with { TopRebars = input.TopRebars with { Enabled = bars }, BottomRebars = input.BottomRebars with { Enabled = bars },
                Options = input.Options with { CommonLoadY = 320 }, Phases = [
                    new() { Name = "Load", ForceKN = loads.N, MomentKNm = loads.M, Phi = phi, Reference = reference },
                    new() { Name = "Shrinkage", Kind = BridgePhaseKind.Shrinkage, Phi = phi, ShrinkageMicrostrain = -200 }
                ] };
            double ea = d.Materials.Steel.ElasticModulusTension, es = d.Materials.Rebar.ElasticModulusTension;
            double ec = d.Materials.Concrete.ElasticModulusCompression / (1 + phi), ratio = ec / ea;
            // Steel line walls, slab continuum, point reinforcement. The native cumulative solver subtracts
            // the concrete at the bar's point too (SectionSolver.IntegrateRebarLinearStress), not a circular hole.
            var parts = new List<(double A, double Y, double I)> {
                (500 * 25, -12.5, 0), (14 * 1800, -925, 14 * Math.Pow(1800, 3) / 12), (700 * 30, -1840, 0),
                (3000 * 250 * ratio, 125, 3000 * Math.Pow(250, 3) / 12 * ratio)
            };
            double barArea = Math.PI * 16 * 16 / 4;
            if (bars) foreach (double y in new[] { 45d, 205d }) {
                parts.Add((20 * barArea * (es / ea - ratio), y, 0));
            }
            double area = parts.Sum(p => p.A), cy = parts.Sum(p => p.A * p.Y) / area;
            double inertia = parts.Sum(p => p.I + p.A * Math.Pow(p.Y - cy, 2));
            double yN = reference == BridgeLoadReference.CommonElevation ? 320 : cy;
            double n = loads.N * 1000, m = loads.M * 1e6;
            double ac = 3000 * 250 - (bars ? 40 * barArea : 0), shrinkN = ec * ac * -200e-6;
            var stage = HBridgeSection.Calculate(d).Stages.Last();
            foreach (var point in stage.Points) {
                double mechanical = n / area - (m + n * (cy - yN)) * (point.Y - cy) / inertia;
                double shrink = shrinkN / area - shrinkN * (cy - 125) * (point.Y - cy) / inertia;
                double expected = point.Material switch {
                    "CLS" => (mechanical + shrink) * ratio - ec * -200e-6,
                    "Armatura" => (mechanical + shrink) * es / ea,
                    _ => mechanical + shrink
                };
                Stress(expected, point.Stress, $"{phi}, {reference}, bars={bars}, {loads}, {point.Name}");
            }
            foreach (var c in stage.Contributions) Assert.IsTrue(c.EquilibriumResidual <= 1e-5);
        }
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void HundredVariableBranchesAreIndependentOfBatchOrderAndStationConcurrency(bool class4)
    {
        var inputs = BridgePerformanceCases.Station(0, class4);
        var expected = inputs.Select(x => Snapshot(HBridgeSection.Calculate(x))).ToArray();
        var batch = HBridgeSection.Calculate(inputs);
        var reversed = HBridgeSection.Calculate(inputs.Reverse().ToArray());
        var concurrent = new string[inputs.Length];
        Parallel.For(0, inputs.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i => concurrent[i] = Snapshot(HBridgeSection.Calculate(inputs[i])));
        for (int i = 0; i < inputs.Length; i++) {
            Assert.IsNull(batch[i].Error); Assert.IsNull(reversed[inputs.Length - 1 - i].Error);
            Assert.AreEqual(expected[i], Snapshot(batch[i].Analysis!), "batch Q" + i);
            Assert.AreEqual(expected[i], Snapshot(reversed[inputs.Length - 1 - i].Analysis!), "reverse Q" + i);
            Assert.AreEqual(expected[i], concurrent[i], "parallel Q" + i);
        }
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(49)] [DataRow(99)]
    public void EffectiveSectionWarmStartMatchesIndependentGrossStart(int combo)
    {
        var d = BridgePerformanceCases.Station(0, true)[combo];
        var warm = HBridgeSection.Calculate(d);
        var cold = HBridgeSection.Calculate(d with { Options = d.Options with { AcceleratedIteration = false } });
        for (int i = 0; i < warm.Stages.Count; i++) {
            Assert.IsTrue(warm.Stages[i].Residual <= 1e-7); Assert.IsTrue(cold.Stages[i].Residual <= 1e-7);
            for (int j = 0; j < warm.Stages[i].Points.Count; j++) {
                double expected = cold.Stages[i].Points[j].Stress;
                Assert.AreEqual(expected, warm.Stages[i].Points[j].Stress, 1e-5 * Math.Max(1, Math.Abs(expected)), "stage=" + i);
            }
        }
    }

    [DataTestMethod]
    [DataRow(-900000d, -2e8)] [DataRow(500000d, 4e8)] [DataRow(0d, 0d)] [DataRow(1e-3, 1d)]
    public void LinearHistoryConstructionAndModulusChangesMatchIncrementalCentroidOracle(double qn, double qm)
    {
        // Two asymmetric point systems; hand integration is exact for this explicitly defined discrete section.
        double[] y = [-180, -20, 240, 400], a = [160, 90, 250, 180], elastic = [210000, 210000, 32000, 32000];
        var section = new HistorySection([
            new("steel", new HistoryElasticLaw(210000), [new("s0", 0, y[0], a[0]), new("s1", 0, y[1], a[1])], true),
            new("concrete", new HistoryElasticLaw(32000), [new("c0", 0, y[2], a[2]), new("c1", 0, y[3], a[3])])
        ]);
        HistoryPhase[] phases = [
            new() { Name = "G1", DeltaN = -20000, DeltaM = 1e6, ApplicationY = 35 },
            new() { Name = "G2", Activate = ["concrete"], DeltaN = -30000, DeltaM = 2e6, ApplicationY = 35,
                ModulusFactors = new Dictionary<string, double> { ["concrete"] = 1 / 3.2 } },
            new() { Name = "Ritiro", ImposedStrainIncrements = new Dictionary<string, HistoryStrainPlane> { ["concrete"] = new(-200e-6, 0) },
                ModulusFactors = new Dictionary<string, double> { ["concrete"] = 1 / 2.1 } },
            new() { Name = "Q", DeltaN = qn, DeltaM = qm, ApplicationY = 35, ModulusFactors = new Dictionary<string, double> { ["concrete"] = 1 } }
        ];
        var result = BridgeHistoryAnalysis.Calculate(section, phases);
        var sigma = new double[4]; double axial = 0, curvature = 0;
        for (int step = 0; step < phases.Length; step++) {
            int count = step == 0 ? 2 : 4;
            double factor = step == 1 ? 1 / 3.2 : step == 2 ? 1 / 2.1 : 1;
            double eigen = step == 2 ? -200e-6 : 0;
            double E(int i) => elastic[i] * (i < 2 ? 1 : factor);
            double ea = Enumerable.Range(0, count).Sum(i => E(i) * a[i]);
            double cy = Enumerable.Range(0, count).Sum(i => E(i) * a[i] * y[i]) / ea;
            double ei = Enumerable.Range(0, count).Sum(i => E(i) * a[i] * Math.Pow(y[i] - cy, 2));
            double freeN = Enumerable.Range(2, count - 2).Sum(i => E(i) * a[i] * eigen);
            double freeM = -Enumerable.Range(2, count - 2).Sum(i => E(i) * a[i] * eigen * (y[i] - cy));
            double de = (phases[step].DeltaN + freeN) / ea;
            double dk = (phases[step].DeltaM + phases[step].DeltaN * (cy - 35) + freeM) / ei;
            axial += de + dk * cy; curvature += dk;
            for (int i = 0; i < count; i++) sigma[i] += E(i) * (de - dk * (y[i] - cy) - (i < 2 ? 0 : eigen));
            var stage = result.Stages[step]; HistoryBehaviorTests.Equilibrium(stage);
            Assert.AreEqual(axial, stage.TotalPlane.AxialStrain, 1e-12);
            Assert.AreEqual(curvature, stage.TotalPlane.Curvature, 1e-14);
            for (int i = 0; i < 4; i++) Stress(sigma[i], stage.Fibers[i].Stress, $"step {step}, fiber {i}");
        }
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void LinearHistorySharedSectionIsReadOnlyAcrossVariableBranches(bool class4)
    {
        var inputs = BridgePerformanceCases.Station(0, class4, 12);
        var options = new HBridgeHistoryOptions();
        var section = HBridgeHistoryAnalysis.CreateSection(inputs[0], options);
        string Run(int i) => JsonSerializer.Serialize(BridgeHistoryAnalysis.Calculate(section,
            HBridgeHistoryAnalysis.ConvertPhases(inputs[i], inputs[i].Phases, options)), Json);
        var expected = Enumerable.Range(0, inputs.Length).Select(Run).ToArray();
        var actual = new string[inputs.Length];
        Parallel.For(0, inputs.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i => actual[i] = Run(i));
        CollectionAssert.AreEqual(expected, actual);
        for (int i = inputs.Length - 1; i >= 0; i--) Assert.AreEqual(expected[i], Run(i));
    }

    [DataTestMethod]
    [DataRow("cumulative", false)] [DataRow("cumulative", true)]
    [DataRow("history", false)] [DataRow("history", true)]
    public void HundredCombinationsRetainFrozenPreOptimizationResults(string method, bool class4)
    {
        string file = Path.Combine(AppContext.BaseDirectory, "Validation", $"linear-performance-{method}-{class4.ToString().ToLowerInvariant()}.bin");
        using var reader = new BinaryReader(File.OpenRead(file));
        int count = reader.ReadInt32(), index = 0;
        var inputs = BridgePerformanceCases.Station(0, class4);
        var analyses = method == "cumulative" ? HBridgeSection.Calculate(inputs).Select(r => r.Analysis ?? throw r.Error!)
            : inputs.Select(d => HBridgeHistoryResults.Calculate(d));
        foreach (var analysis in analyses)
        foreach (var stage in analysis.Stages) {
            var values = stage.Points.Take(8).Select(p => p.Stress).Concat(new[] {
                stage.Effective.TopWidth, stage.Effective.BottomWidth, stage.Effective.WebTop, stage.Effective.WebBottom });
            foreach (double actual in values) {
                double expected = reader.ReadDouble();
                Assert.IsTrue(double.IsFinite(actual));
                Assert.AreEqual(expected, actual, 1e-7 + 2e-8 * Math.Abs(expected), $"{method}, class4={class4}, index={index++}");
            }
        }
        Assert.AreEqual(count, index); Assert.AreEqual(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    private static IEnumerable<object[]> NativeComparisonCases()
    {
        foreach (int shape in Enumerable.Range(0, 4))
        foreach (bool class4 in new[] { false, true })
        foreach (var reference in Enum.GetValues<BridgeLoadReference>()) yield return [shape, class4, reference];
    }

    [TestMethod]
    [DynamicData(nameof(NativeComparisonCases), DynamicDataSourceType.Method)]
    public void EveryCompositeContributionMatchesNativeSolverOnItsOwnEffectiveGeometry(int shape, bool class4, BridgeLoadReference reference)
    {
        var source = BridgePerformanceCases.Station(0, class4, 2)[1];
        var dimensions = shape switch {
            1 => source.Geometry with { SecondBottomEnabled = true, SecondBottomWidth = 500, SecondBottomThickness = 20 },
            2 => source.Geometry with { SectionType = BridgeSteelSectionType.InclinedWebH, WebOffset = 300 },
            3 => source.Geometry with { SectionType = BridgeSteelSectionType.Box, WebSpacing = 1800, WebOffset = 150,
                TopWidth = 450, BottomWidth = 1900, BottomThickness = 25 },
            _ => source.Geometry
        };
        var input = source with { Geometry = dimensions, Phases = source.Phases.Select(p => p with { Reference = reference }).ToArray() };
        var result = HBridgeSection.Calculate(input); var g = result.Geometry;
        foreach (var stage in result.Stages) {
            var e = stage.Effective;
            ReinforcedConcreteSection section = HBridgeSection.NativeSection(input);
            section.SteelSections.Clear();
            void Plate(double width, double height, double bottom, bool vertical = false) {
                if (width <= 1e-8 || height <= 1e-8) return;
                var rectangle = vertical ? new SectionRectangular(width, height) : new SectionRectangular(height, width);
                section.AddSteelSection(new SteelSectionPosition(new SteelSection(rectangle, input.Materials.Steel), Point2d.Origin,
                    vertical ? Math.PI / 2 : 0, new Point2d(g.Width / 2, bottom + height / 2), InsertionPointType.Centroid) { IsInsideConcrete = false });
            }
            Plate(e.TopWidth, g.TopThickness, -g.TopThickness);
            Plate(g.WebThickness, e.WebTop, -g.TopThickness - e.WebTop, true);
            Plate(g.WebThickness, e.WebBottom, -g.TopThickness - g.WebHeight, true);
            Plate(e.BottomWidth, g.Bottom1Thickness, -g.TopThickness - g.WebHeight - g.Bottom1Thickness);
            if (g.Bottom2Thickness > 0) Plate(e.SecondBottomWidth, g.Bottom2Thickness, -g.Height);
            foreach (var c in stage.Contributions.Where(c => c.HasConcrete)) {
                double n = (c.IsShrinkage ? c.EquivalentN : c.N) * 1000, m = c.IsShrinkage ? 0 : c.Mx * 1e6;
                var field = CompositeLinearStressSolver.Solve(section, n, m, g.Width / 2, c.LoadY, c.EffectivePhi, input.Options.Standard, 1e-12);
                foreach (double y in new[] { -g.Height, -g.TopThickness, 0d, g.SlabHeight })
                    Stress(field.Stress(y), c.SteelStress(y), $"shape={shape}, class4={class4}, {reference}, {stage.Name}/{c.Name}, y={y}");
            }
        }
    }
}
