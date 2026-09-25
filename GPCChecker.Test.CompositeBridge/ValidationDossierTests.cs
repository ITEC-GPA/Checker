using System.Text.Json;
using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

/// <summary>Reproducible numerical evidence for the validation document. Expected values use
/// closed-form rectangle integration, not production property/stress routines.</summary>
[TestClass, DoNotParallelize]
public class ValidationDossierTests
{
    public sealed record Row(string Id, string Quantity, string Unit, double Expected, double Actual, double Tolerance);
    static void Save(string name, object value)
    {
        var path = Environment.GetEnvironmentVariable("BRIDGE_VALIDATION_OUTPUT");
        if (string.IsNullOrEmpty(path)) return;
        Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, name + ".json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
    static Row Check(string id, string label, string unit, double expected, double actual, double tolerance)
    { Assert.AreEqual(expected, actual, tolerance, id + ": " + label); return new(id, label, unit, expected, actual, tolerance); }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void CumulativeAndLinearHistoryMatchIndependentFivePhaseSectionSolution(bool historical)
    {
        var input = CompositeBridgeApiTests.Input() with { TopRebars = new() { Enabled = false }, BottomRebars = new() { Enabled = false },
            Options = new() { Class4 = false }, Phases = [
                new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, ForceKN = -100, MomentKNm = 1500 },
                new() { Name = "G2", ForceKN = -200, MomentKNm = 2000, Phi = 2, PsiL = 1.1 },
                new() { Name = "R1", Kind = BridgePhaseKind.Shrinkage, ShrinkageMicrostrain = -100, Phi = 1, PsiL = .55 },
                new() { Name = "Q", ForceKN = 50, MomentKNm = -500 },
                new() { Name = "R2", Kind = BridgePhaseKind.Shrinkage, ShrinkageMicrostrain = -200, Phi = 2, PsiL = .55 }] };
        input = input with { Phases = input.Phases.Select(p => p with { Reference = BridgeLoadReference.CommonElevation }).ToArray() };
        double es = input.Materials.Steel.ElasticModulusTension, ec = input.Materials.Concrete.ElasticModulusCompression;
        // Independent rectangles (b, h, centre). Deliberately do not use Geometry(), Rectangle() or property helpers.
        var rectangles = new[] { (B: 500d, H: 25d, Y: -12.5), (B: 14d, H: 1800d, Y: -925d), (B: 700d, H: 30d, Y: -1840d) };
        var expected = new List<(bool Concrete, double E0, double K, double Ec, double Eigen)>();
        foreach (var phase in input.Phases)
        {
            bool concrete = phase.Kind != BridgePhaseKind.SteelOnly;
            double modulus = ec / (1 + phase.Phi * phase.PsiL);
            // The native cumulative solver integrates flange centrelines. The history engine
            // integrates full rectangles. Compare each to its declared mechanical model.
            var parts = rectangles.Select((r, i) => (EA: es * r.B * r.H, r.Y,
                EJ: !historical && concrete && i != 1 ? 0 : es * r.B * Math.Pow(r.H, 3) / 12)).ToList();
            if (concrete) parts.Add((modulus * 3000 * 250, 125, modulus * 3000 * Math.Pow(250, 3) / 12));
            double a = parts.Sum(p => p.EA), b = -parts.Sum(p => p.EA * p.Y), d = parts.Sum(p => p.EJ + p.EA * p.Y * p.Y);
            double eigen = phase.Kind == BridgePhaseKind.Shrinkage ? phase.ShrinkageMicrostrain * 1e-6 : 0;
            double n = phase.ForceKN * 1000 + modulus * 3000 * 250 * eigen, m = phase.MomentKNm * 1e6 - modulus * 3000 * 250 * 125 * eigen;
            expected.Add((concrete, (d * n - b * m) / (a * d - b * b), (a * m - b * n) / (a * d - b * b), modulus, eigen));
        }
        var rows = new List<Row>();
        if (historical)
        {
            var actual = HBridgeHistoryAnalysis.Calculate(input);
            for (int i = 0; i < actual.Stages.Count; i++)
            {
                var s = actual.Stages[i];
                foreach (var f in s.Fibers.Where(f => f.Active))
                {
                    bool c = f.ComponentId == HBridgeHistoryAnalysis.Concrete;
                    double stress = expected.Take(i + 1).Where(p => !c || p.Concrete).Sum(p => (c ? p.Ec : es) * (p.E0 - p.K * f.Fiber.Y - (c ? p.Eigen : 0)));
                    Assert.AreEqual(stress, f.Stress, 2e-7, "Phase " + i + " fiber " + f.Fiber.Id);
                }
                rows.Add(Check("HL" + (i + 1), "epsilon0 " + s.Name, "-", expected.Take(i + 1).Sum(p => p.E0), s.TotalPlane.AxialStrain, 2e-12));
                rows.Add(Check("HL" + (i + 1), "kappa " + s.Name, "1/mm", expected.Take(i + 1).Sum(p => p.K), s.TotalPlane.Curvature, 2e-14));
            }
        }
        else
        {
            var actual = HBridgeSection.Calculate(input);
            for (int i = 0; i < actual.Stages.Count; i++)
            {
                var s = actual.Stages[i];
                foreach (var p in s.Points.Where(p => p.Active))
                {
                    bool c = p.Material == "CLS";
                    double stress = expected.Take(i + 1).Where(e => !c || e.Concrete).Sum(e => (c ? e.Ec : es) * (e.E0 - e.K * p.Y - (c ? e.Eigen : 0)));
                    rows.Add(Check("CU" + (i + 1), p.Name, "MPa", stress, p.Stress, 2e-6));
                }
            }
        }
        Save(historical ? "dossier-storico-lineare" : "dossier-cumulativo", new { Es = es, Ec = ec, Rows = rows });
    }
    [TestMethod] public void PublishedPlateAndShearBenchmarksAreRecorded()
    {
        var p = EffectivePlateReduction.InternalPlate(492, 8, -100, -40.6, 235);
        var s = BridgeShearConnection.Web(2720, 18, 345, 210000, 1, 1.1, 1.2, 8000);
        Save("dossier-published", new[] { Check("JRC17", "k_sigma", "-", 5.632, p.KSigma, .001),
            Check("JRC17", "lambda", "-", .912, p.Lambda, .001), Check("JRC17", "rho", "-", .871, p.Rho, .002),
            Check("JRC6", "k_tau", "-", 5.802, s.KTau, .001), Check("JRC6", "tau_cr", "MPa", 48.22, s.TauCritical, .02) });
    }
    [TestMethod] public void AnalyticalNonlinearAndCurveBenchmarksAreRecorded()
    {
        var section = new HistorySection([new("steel", new HistoryBilinearSteelLaw(200000, 200, 10000),
            [new("lower", 0, -100, 100), new("upper", 0, 100, 100)], true)]);
        var h = BridgeHistoryAnalysis.Calculate(section, [new HistoryPhase { DeltaN = 60000 }, new HistoryPhase { DeltaN = -60000 }]);
        var rectangle = SectionResponseTests.Rectangle();
        var mc = BridgeSectionResponseAnalysis.Calculate(rectangle, [1e-6], new() { SubstepsPerTarget = 1 }).Points.Last();
        var ne = BridgeSectionResponseAnalysis.Calculate(rectangle, [.001], new() { Control = SectionResponseControl.AxialForceStrain, SubstepsPerTarget = 1 }).Points.Last();
        var plastic = BridgeSectionResponseAnalysis.Calculate(SectionResponseTests.Rectangle(new HistoryBilinearSteelLaw(200000, 250)), [.005],
            new() { Control = SectionResponseControl.AxialForceStrain, SubstepsPerTarget = 1 }).Points.Last();
        Save("dossier-analytical", new[] { Check("NL1", "epsilon carico", "-", .011, h.Stages[0].TotalPlane.AxialStrain, 1e-12),
            Check("NL2", "epsilon residua scarico", "-", .0095, h.Stages[1].TotalPlane.AxialStrain, 1e-12),
            Check("MC1", "M a kappa 1e-6", "Nmm", 200000d * 100 * Math.Pow(200, 3) / 12 * 1e-6, mc.MomentAtReference, 1e-5),
            Check("NE1", "N a epsilon 0.001", "N", 4e6, ne.State.N, 1e-6), Check("NE2", "N a epsilon 0.005", "N", 5e6, plastic.State.N, 1e-6) });
    }
}
