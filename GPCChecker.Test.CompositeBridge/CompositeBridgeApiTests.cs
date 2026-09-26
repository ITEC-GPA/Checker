using GPC.Checkers.CompositeBridge;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass, DoNotParallelize]
public class CompositeBridgeApiTests
{
    internal static HBridgeInput Input() => new()
    {
        Materials = new(ConcreteMaterialEN1992Data.C35_45, SteelMaterialEN1993Data.S355, SteelMaterialEN1992Data.B450C),
        Geometry = new() { SlabWidth = 3000, SlabHeight = 250, WebHeight = 1800, WebThickness = 14,
            TopWidth = 500, TopThickness = 25, BottomWidth = 700, BottomThickness = 30 },
        TopRebars = new() { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 45 },
        BottomRebars = new() { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 45 },
        Phases = [new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 1500 },
            new() { Name = "G2", MomentKNm = 2000, Phi = 2, PsiL = 1.1 },
            new() { Name = "Q", MomentKNm = 3000 }]
    };

    [TestMethod]
    public void PublicApiHasNoApplicationOrJsonDependency()
    {
        var references = typeof(HBridgeSection).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.IsFalse(references.Any(n => n!.Contains("ANTHEA") || n.StartsWith("X.") || n.Contains("Json") || n == "PresentationFramework"));
        var result = HBridgeSection.Calculate(Input());
        Assert.AreEqual(3, result.Stages.Count);
        Assert.AreEqual(3, result.Stages.Last().Contributions.Count);
        Assert.IsTrue(result.Stages.All(s => s.Residual < 1e-7 && s.Contributions.All(c => c.EquilibriumResidual < 1e-5)));
    }

    [DataTestMethod]
    [DataRow(false, false)] [DataRow(true, false)] [DataRow(false, true)] [DataRow(true, true)]
    public void OptionalRowsWorkThroughTypedApi(bool top, bool bottom)
    {
        var d = Input();
        d = d with { TopRebars = d.TopRebars with { Enabled = top }, BottomRebars = d.BottomRebars with { Enabled = bottom } };
        var result = HBridgeSection.Calculate(d);
        Assert.AreEqual((top ? 20 : 0) + (bottom ? 20 : 0), result.Geometry.Bars.Length);
        Assert.IsTrue(result.Stages.Last().Points.All(p => double.IsFinite(p.Stress)));
    }

    [DataTestMethod]
    [DataRow(BridgeLoadReference.GrossCentroid)] [DataRow(BridgeLoadReference.EffectiveCentroid)] [DataRow(BridgeLoadReference.CommonElevation)]
    public void AxialLoadReferencesPreserveEquilibrium(BridgeLoadReference reference)
    {
        var d = Input(); d.Phases[1] = d.Phases[1] with { Reference = reference, ForceKN = -900 };
        var result = HBridgeSection.Calculate(d);
        var c = result.Stages.Last().Contributions[1];
        Assert.IsTrue(c.EquilibriumResidual < 1e-5);
        double expected = reference == BridgeLoadReference.GrossCentroid ? HBridgeSection.GrossPhaseCentroid(d, d.Phases[1]) :
            reference == BridgeLoadReference.EffectiveCentroid ? c.Centroid : d.Options.CommonLoadY;
        Assert.AreEqual(expected, c.LoadY, 1e-8);
    }

    [DataTestMethod]
    [DataRow(0d)] [DataRow(1d)] [DataRow(3d)]
    public void PhiAndModularRatioGiveSameStresses(double phi)
    {
        var d = Input(); d.Phases[1] = d.Phases[1] with { Phi = phi };
        var h = CompositeHomogenization.Calculate(d.Materials, d.Phases[1]);
        var a = HBridgeSection.Calculate(d);
        d.Phases[1] = d.Phases[1] with { HomogenizationSource = BridgeHomogenizationSource.ModularRatio, N = h.N };
        var b = HBridgeSection.Calculate(d);
        for (int i = 0; i < a.Stages.Last().Points.Count; i++)
            Assert.AreEqual(a.Stages.Last().Points[i].Stress, b.Stages.Last().Points[i].Stress, 1e-8);
    }

    [TestMethod]
    public void RepeatedPhaseObjectIsTwoIndependentLoadIncrementsAndInputIsSnapshotted()
    {
        var p = new BridgePhase { MomentKNm = 50 };
        var d = Input() with { Phases = [p, p], Options = new() { Class4 = false } };
        var r = HBridgeSection.Calculate(d);
        Assert.AreEqual(2, r.Stages.Last().Contributions.Count);
        Assert.AreEqual(2 * r.Stages[0].Points[0].Stress, r.Stages[1].Points[0].Stress, 1e-8);
        d.Phases[0] = p with { MomentKNm = 500 };
        Assert.AreEqual(50d, r.Input.Phases[0].MomentKNm);
    }

    [TestMethod]
    public void MultipleShrinkagePhasesRemainSeparate()
    {
        var p = new BridgePhase { Kind = BridgePhaseKind.Shrinkage, ShrinkageMicrostrain = -100, Phi = 2, PsiL = .55 };
        var d = Input() with { Phases = [p, p with { ShrinkageMicrostrain = -200 }] };
        var r = HBridgeSection.Calculate(d);
        Assert.AreEqual(2, r.Stages.Last().Contributions.Count(c => c.IsShrinkage));
        Assert.AreEqual(0d, r.Stages.Last().Contributions.Sum(c => c.N));
        Assert.AreEqual(0d, r.Stages.Last().Contributions.Sum(c => c.Mx));
        Assert.AreEqual(2 * r.Stages.Last().Contributions[0].SteelStress(0), r.Stages.Last().Contributions[1].SteelStress(0), 1e-8);
    }

    [TestMethod]
    public void AcceleratedIterationGivesTheSameResultsWithFewerIterations()
    {
        // warm start + Aitken against gross start + fixed relaxation: the same fixed point within the tolerance of convergence
        var d = Input() with { Phases = Enumerable.Range(0, 6).Select(i => new BridgePhase { Name = "P" + i, Kind = i == 0 ? BridgePhaseKind.SteelOnly : BridgePhaseKind.Composite, MomentKNm = 800 + 150 * i, Phi = i % 3 }).ToArray() };
        var fast = HBridgeSection.Calculate(d);
        var plain = HBridgeSection.Calculate(d with { Options = d.Options with { AcceleratedIteration = false } });
        for (int s = 0; s < fast.Stages.Count; s++)
            for (int p = 0; p < fast.Stages[s].Points.Count; p++)
                Assert.AreEqual(plain.Stages[s].Points[p].Stress, fast.Stages[s].Points[p].Stress, 1e-6 * Math.Max(1, Math.Abs(plain.Stages[s].Points[p].Stress)));
        Assert.IsTrue(fast.Stages.Sum(s => s.Iterations) * 2 < plain.Stages.Sum(s => s.Iterations), $"{fast.Stages.Sum(s => s.Iterations)} / {plain.Stages.Sum(s => s.Iterations)}");
        Assert.IsTrue(fast.Stages.All(s => s.Residual < 1e-7));
    }

    [TestMethod]
    public void AitkenRelaxationSolvesAScalarFixedPoint()
    {
        // x = 1 + 0.9 x - 0.05 x²: x* = 10 (sqrt(0.21) - 0.1), F'(x*) = 0.54, so the fixed relaxation 0.55 contracts only by 0.75 per
        // iteration; Aitken (a secant on the residual) converges in a few iterations
        IReadOnlyList<CumulativeSituation<double, double>> Run(bool aitken) => CumulativePhaseAnalysis.Analyze(new[] { 1 }, () => 0d,
            (x, _) => x, c => 1 + .9 * c[0] - .05 * c[0] * c[0], (x, y) => Math.Abs(x - y), (x, y, f) => x + f * (y - x), _ => "x",
            tolerance: 1e-12, coordinates: aitken ? x => new[] { x } : null).ToArray();
        var fast = Run(true).Single(); var plain = Run(false).Single();
        double exact = 10 * (Math.Sqrt(.21) - .1);
        Assert.AreEqual(exact, fast.State, 1e-10); Assert.AreEqual(exact, plain.State, 1e-10);
        Assert.IsTrue(fast.Iterations < plain.Iterations / 2, $"{fast.Iterations} / {plain.Iterations}");
    }

    [TestMethod]
    public void WarmStartBeginsFromThePreviousSituation()
    {
        // two identical increments: the second situation starts from the first converged geometry and needs one or few iterations
        var p = new BridgePhase { Kind = BridgePhaseKind.SteelOnly, MomentKNm = 1500 };
        var d = Input() with { Phases = [p, p with { MomentKNm = 1e-6 }] };
        var r = HBridgeSection.Calculate(d);
        Assert.IsTrue(r.Stages[1].Iterations <= 2, r.Stages[1].Iterations.ToString());
        var cold = HBridgeSection.Calculate(d with { Options = d.Options with { AcceleratedIteration = false } });
        Assert.IsTrue(cold.Stages[1].Iterations > 5, cold.Stages[1].Iterations.ToString());
    }

    [TestMethod]
    public void CancellationIsHonouredByLibrary()
    {
        Assert.ThrowsException<OperationCanceledException>(() => HBridgeSection.Calculate(Input(), new CancellationToken(true)));
    }

    [TestMethod]
    public void InvalidLoadsAndPhaseOrderAreRejectedInChecker()
    {
        var d = Input(); d.Phases[1] = d.Phases[1] with { ForceKN = double.NaN };
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Calculate(d));
        d = Input() with { Phases = [new(), new() { Kind = BridgePhaseKind.SteelOnly }] };
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Calculate(d));
    }
}
