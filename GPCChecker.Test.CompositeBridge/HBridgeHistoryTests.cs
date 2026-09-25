using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static CompositeBridgeTests.HistoryBehaviorTests;

namespace CompositeBridgeTests;

[TestClass, DoNotParallelize]
public class HBridgeHistoryTests
{
    static HBridgeInput Input(bool class4 = false)
    {
        var d = CompositeBridgeApiTests.Input();
        return d with { Options = d.Options with { Class4 = class4 } };
    }
    static HBridgeHistoryOptions Mesh(int web = 80) => new() { WebLayers = web, ConcreteLayers = 16, FlangeLayers = 3 };
    [DataTestMethod]
    [DataRow(false, false)] [DataRow(true, false)] [DataRow(false, true)] [DataRow(true, true)]
    public void NetConcreteAndBarsPreserveExactAreaFirstAndSecondMoments(bool upper, bool lower)
    {
        var d = Input(); d = d with { TopRebars = d.TopRebars with { Enabled = upper }, BottomRebars = d.BottomRebars with { Enabled = lower } };
        var section = HBridgeHistoryAnalysis.CreateSection(d, Mesh()); var g = HBridgeSection.Geometry(d);
        var concrete = section.Components.Single(c => c.Id == HBridgeHistoryAnalysis.Concrete).Fibers;
        double ac = g.Width * g.SlabHeight, sc = ac * g.SlabHeight / 2, jc = g.Width * Math.Pow(g.SlabHeight, 3) / 3;
        Near(ac - g.Bars.Sum(b => b.Area), concrete.Sum(f => f.Area));
        Near(sc - g.Bars.Sum(b => b.Area * b.Y), concrete.Sum(f => f.Area * f.Y));
        Near(jc - g.Bars.Sum(b => b.Area * b.Y * b.Y + Math.PI * Math.Pow(b.Diameter, 4) / 64), concrete.Sum(f => f.Area * f.Y * f.Y));
        var rebar = section.Components.Where(c => c.Id == HBridgeHistoryAnalysis.Rebars).SelectMany(c => c.Fibers).ToArray();
        Near(g.Bars.Sum(b => b.Area), rebar.Sum(f => f.Area));
        Near(jc, concrete.Concat(rebar).Sum(f => f.Area * f.Y * f.Y));
    }
    [TestMethod] public void FirstCompositePhaseMatchesIndependentElasticSectionSolution()
    {
        var d = Input(); var phase = new BridgePhase { ForceKN = -200, MomentKNm = 800, Phi = 2, PsiL = 1.1 };
        var s = HBridgeHistoryAnalysis.Calculate(d, new[] { phase }, Mesh()).Stages.Single();
        var props = HBridgeSection.GrossPhaseProperties(d, phase); double ea = d.Materials.Steel.ElasticModulusTension;
        Near(phase.ForceKN * 1000 / (ea * props.Area) + phase.MomentKNm * 1e6 / (ea * props.Ix) * props.Y, s.TotalPlane.AxialStrain, 1e-11);
        Near(phase.MomentKNm * 1e6 / (ea * props.Ix), s.TotalPlane.Curvature, 1e-13);
        Equilibrium(s);
    }
    [TestMethod] public void CastingAfterBendingPreservesTheFullActivationStrainPlane()
    {
        var d = Input(); var p = new[] { new BridgePhase { Kind = BridgePhaseKind.SteelOnly, MomentKNm = 600 }, new BridgePhase() };
        var r = HBridgeHistoryAnalysis.Calculate(d, p, Mesh()).Stages;
        foreach (var f in r[1].Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete || f.ComponentId == HBridgeHistoryAnalysis.Rebars))
        { Near(r[0].TotalPlane.At(f.Fiber.Y), f.ActivationStrain, 1e-12); Near(0, f.Stress, 1e-8); }
        Assert.AreEqual(r[0].TotalPlane, r[1].TotalPlane);
    }
    [TestMethod] public void HAdapterAcceptsHundredsOfShrinkageEventsWithIndependentPhi()
    {
        var d = Input();
        var phases = Enumerable.Range(0, 251).Select(i => new BridgePhase { Name = "R" + i, Kind = BridgePhaseKind.Shrinkage,
            ShrinkageMicrostrain = -1, Phi = i % 3, PsiL = .55 });
        var r = HBridgeHistoryAnalysis.Calculate(d, phases, Mesh(8)).Stages;
        Assert.AreEqual(251, r.Count);
        foreach (var f in r.Last().Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete)) Near(-251e-6, f.ImposedStrain, 1e-12);
        foreach (var f in r.Last().Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Rebars)) Near(0, f.ImposedStrain, 1e-12);
        Equilibrium(r.Last());
    }
    [TestMethod] public void ArrayEntryPointAlsoBypassesLegacyTwentyPhaseLimit()
    {
        var d = Input() with { Phases = Enumerable.Range(0, 101).Select(_ => new BridgePhase { MomentKNm = 1 }).ToArray() };
        Assert.AreEqual(101, HBridgeHistoryAnalysis.Calculate(d, Mesh(4)).Stages.Count);
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Calculate(d)); // old entry point deliberately unchanged
    }
    [TestMethod] public void ShrinkageFromPhiAndFromNProduceIdenticalHistory()
    {
        var d = Input(); var p = new BridgePhase { Kind = BridgePhaseKind.Shrinkage, ShrinkageMicrostrain = -150, Phi = 2, PsiL = .55 };
        double n = CompositeHomogenization.Calculate(d.Materials, p).N;
        var a = HBridgeHistoryAnalysis.Calculate(d, new[] { p }, Mesh()).Stages[0];
        var b = HBridgeHistoryAnalysis.Calculate(d, new[] { p with { HomogenizationSource = BridgeHomogenizationSource.ModularRatio, N = n } }, Mesh()).Stages[0];
        Near(a.TotalPlane.AxialStrain, b.TotalPlane.AxialStrain, 1e-12); Near(a.TotalPlane.Curvature, b.TotalPlane.Curvature, 1e-12);
    }
    [DataTestMethod]
    [DataRow(BridgeLoadReference.GrossCentroid)] [DataRow(BridgeLoadReference.EffectiveCentroid)] [DataRow(BridgeLoadReference.CommonElevation)]
    public void ClassFourHistoryConvergesAndEquilibratesForEachLoadReference(BridgeLoadReference reference)
    {
        var d = Input(true); d.Phases[1] = d.Phases[1] with { ForceKN = -500, Reference = reference };
        var r = HBridgeHistoryAnalysis.Calculate(d, Mesh()).Stages;
        foreach (var s in r) { Equilibrium(s); Assert.IsTrue(s.EffectiveResidual <= 1e-7); }
        Assert.IsTrue(r[0].Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Web).Sum(f => f.EffectiveArea) < d.Geometry.WebHeight * d.Geometry.WebThickness);
        Assert.AreEqual(3, r.Last().Panels.Count);
        Near(r[0].MomentAtOrigin + d.Phases[1].MomentKNm * 1e6 - d.Phases[1].ForceKN * 1000 * r[1].IncrementApplicationY, r[1].MomentAtOrigin);
    }
    [TestMethod] public void ClassFourPreviousStageIsIndependentOfLaterGeometryChanges()
    {
        var d = Input(true);
        var prefix = HBridgeHistoryAnalysis.Calculate(d, new[] { d.Phases[0] }, Mesh()).Stages[0];
        var full = HBridgeHistoryAnalysis.Calculate(d, Mesh()).Stages;
        Assert.AreEqual(prefix.TotalPlane, full[0].TotalPlane);
        for (int i = 0; i < prefix.Fibers.Count; i++) Assert.AreEqual(prefix.Fibers[i], full[0].Fibers[i]);
        Assert.AreNotEqual(full[0].TotalPlane, full.Last().TotalPlane);
    }
    [TestMethod] public void ClassFourFixedFiberDiscretizationConvergesWithRefinement()
    {
        var d = Input(true);
        var coarse = HBridgeHistoryAnalysis.Calculate(d, Mesh(80)).Stages.Last();
        var fine = HBridgeHistoryAnalysis.Calculate(d, Mesh(320)).Stages.Last();
        Assert.IsTrue(Math.Abs(coarse.TotalPlane.Curvature / fine.TotalPlane.Curvature - 1) < .003);
        foreach (var f in fine.Fibers) Assert.IsTrue(f.EffectiveArea >= 0 && f.EffectiveArea <= f.Fiber.Area * (1 + 1e-12));
    }
    [TestMethod] public void NonlinearHUsesModelMaterialsWithCompatibleCastingHistory()
    {
        var d = Input();
        var p = new[] { new BridgePhase { Kind = BridgePhaseKind.SteelOnly, MomentKNm = 300 }, new BridgePhase { MomentKNm = 500 } };
        var r = HBridgeHistoryAnalysis.Calculate(d, p, Mesh() with { MaterialMode = HistoryMaterialMode.Nonlinear, SubstepsPerPhase = 4 }).Stages;
        foreach (var s in r) Equilibrium(s);
        foreach (var f in r[1].Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete))
        { Near(d.Materials.Concrete.GetStress(f.MechanicalStrain), f.Stress, 1e-8); Near(r[0].TotalPlane.At(f.Fiber.Y), f.ActivationStrain, 1e-12); }
    }
    [TestMethod] public void AutomaticClassFourNonlinearCombinationIsExplicitlyRejected()
    {
        Assert.ThrowsException<NotSupportedException>(() => HBridgeHistoryAnalysis.Calculate(Input(true), Mesh() with { MaterialMode = HistoryMaterialMode.Nonlinear }));
    }
    [TestMethod] public void TwoBottomPlatesKeepTheExistingEquivalentRectangleConvention()
    {
        var d = Input(); d = d with { Geometry = d.Geometry with { SecondBottomEnabled = true, SecondBottomWidth = 500, SecondBottomThickness = 20 } };
        var section = HBridgeHistoryAnalysis.CreateSection(d, Mesh()); var bottom = section.Components.Single(c => c.Id == HBridgeHistoryAnalysis.Bottom).Fibers;
        Near(700d * 30 + 500 * 20, bottom.Sum(f => f.Area));
        Near(-1850, bottom.Sum(f => f.Area * f.Y) / bottom.Sum(f => f.Area));
    }
}
