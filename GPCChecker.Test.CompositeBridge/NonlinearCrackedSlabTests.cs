using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using GPC.Model.Data.Concrete;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static CompositeBridgeTests.HistoryBehaviorTests;

namespace CompositeBridgeTests;

/// <summary>
/// The nonlinear history and the M–κ curves through the cracking of the slab (finding PON-2). Expected values: independent Python calculation
/// (agent-bridge/py/pon2.py; cross-checked with the verifier's ponte-nlfess/indip.py): G1 on the steel alone, the following increments on the cracked
/// section (steel + bars, Es/Ea), concrete without tensile strength; M–κ by an own fibre integration (EN 1992-1-1 §3.1.7 parabola-rectangle with fck,
/// elastic-perfectly plastic S355 and B450C)
/// </summary>
[TestClass, DoNotParallelize]
public class NonlinearCrackedSlabTests
{
    static HBridgeInput Gross() { var d = CompositeBridgeApiTests.Input(); return d with { Options = d.Options with { Class4 = false } }; }

    [TestMethod]
    public void ModelEnvelopeIsCrackedBeyondTheTensileBranchAndRejectsOnlyCrushing()
    {
        var material = ConcreteMaterialEN1992Data.C35_45;
        var law = new HistoryModelEnvelopeLaw(material);
        // EN 1992-1-1 Tab. 3.1, C35/45: Ecm = 22000 (43/10)^0.3 = 34077,15 MPa, fctk,0.05 = 0,7 · 0,3 · 35^(2/3) = 2,2470 MPa, εcr = 6,594e-5
        var uncracked = law.Evaluate(3e-5, law.InitialState(), 1);
        Assert.AreEqual(34077.15 * 3e-5, uncracked.Stress, 1e-4); Assert.AreEqual(34077.15, uncracked.Tangent, .01);
        var cracked = law.Evaluate(1e-4, law.InitialState(), 1);
        Assert.AreEqual(0, cracked.Stress); Assert.AreEqual(0, cracked.Tangent);
        // EN 1992-1-1 §3.1.7, parabola-rectangle with fck: 35 (1 − (1 − 0,001/0,002)²) = 26,25 MPa
        Assert.AreEqual(-26.25, law.Evaluate(-1e-3, law.InitialState(), 1).Stress, 1e-9);
        Assert.ThrowsException<HistoryMaterialRangeException>(() => law.Evaluate(-.0036, law.InitialState(), 1), "beyond εcu2 = 0,0035");
        var noTension = new HistoryModelEnvelopeLaw(material, neglectTension: true);
        Assert.IsTrue(noTension.NeglectsTension && !law.NeglectsTension);
        var r = noTension.Evaluate(3e-5, noTension.InitialState(), 1);
        Assert.AreEqual(0, r.Stress); Assert.AreEqual(0, r.Tangent);
        Assert.AreEqual(-26.25, noTension.Evaluate(-1e-3, noTension.InitialState(), 1).Stress, 1e-9);
    }

    [TestMethod]
    public void NonlinearHistoryCompletesThroughTheCrackingOfTheSlab()
    {
        // finding scenario: default data ×0,6 with negative moments (G1 −900 steel only, G2 −1200, Q −1800 kNm): before, Newton failed in Q
        var d = Gross(); d = d with { Phases = d.Phases.Select(p => p with { MomentKNm = -.6 * p.MomentKNm }).ToArray() };
        var options = new HBridgeHistoryOptions { MaterialMode = HistoryMaterialMode.Nonlinear, InstantaneousConcrete = true };
        var s = HBridgeHistoryAnalysis.Calculate(d, options).Stages.Last();
        Equilibrium(s);
        // κ = M1/(Ea Ia) + M23/(Ea Icr), ε0 = κ1 yGa + κ2 yGcr
        Assert.AreEqual(-4.569638e-7, s.TotalPlane.Curvature, 1e-12);
        Assert.AreEqual(4.384179e-4, s.TotalPlane.AxialStrain, 1e-9);
        double Stress(string component, Func<double, bool> at) => s.Fibers.Single(f => f.ComponentId == component && at(f.Fiber.Y)).Stress;
        Assert.AreEqual(91.9410, Stress(HBridgeHistoryAnalysis.Top, y => Math.Abs(y + 1.3208) < 1e-3), 2e-3);
        Assert.AreEqual(-85.7904, Stress(HBridgeHistoryAnalysis.Bottom, y => Math.Abs(y + 1853.4151) < 1e-3), 2e-3);
        Assert.AreEqual(74.6132, s.Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Rebars && Math.Abs(f.Fiber.Y - 209) < 1e-9).Average(f => f.Stress), 2e-3);
        // the slab is in tension (strain increments 3,04e-4 to 3,87e-4): cracked, no stress
        Assert.IsTrue(s.Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete).All(f => f.Active && f.MechanicalStrain > 0 && f.Stress == 0));
        var presented = HBridgeHistoryResults.Calculate(d, options);
        Assert.IsTrue(presented.Scope.Contains("trazione trascurata"));
        Assert.IsTrue(presented.Stages.Last().Warnings.Any(w => w.Contains("resistenza a trazione trascurata")));
    }

    [TestMethod]
    public void MomentCurvatureFromTheVirginSectionProceedsPastCracking()
    {
        var control = new SectionResponseOptions { Control = SectionResponseControl.MomentCurvature, AxialForce = 0, SubstepsPerTarget = 4 };
        var r = HBridgeSectionResponse.Trace(Gross(), -3e-6, 100, control);
        Assert.IsTrue(r.Completed, r.Message);
        Assert.AreEqual(101, r.Points.Count);
        // elastic cracked branch: M = Ea Icr κ = −1363,61 kNm at κ = −1,5e-4 1/m (before: stop at cracking, κ = −1,33e-4 1/m, M = −2533 kNm)
        Assert.AreEqual(-1.5e-7, r.Points[5].ControlValue, 1e-15);
        Assert.AreEqual(-1363.61e6, r.Points[5].MomentAtReference, .01e6);
        // fibre integration at κ = −3e-3 1/m: −18152,08 kNm
        Assert.AreEqual(-18152.08e6, r.Points.Last().MomentAtReference, 2e6);
    }

    // ---- option HistoryConcreteTension.TensionStiffening (Collins and Mitchell 1991). Expected values: independent Python (agent-bridge/py/pon2ts.py):
    // closed forms of the law, the uncracked composite section (net concrete with Ecm) and an own fibre solution of the cracked states
    const double Ecm = 34077.1462, Fctm = 3.209962;   // EN 1992-1-1 Tab. 3.1, C35/45
    static HBridgeHistoryOptions Stiffening() => new() { MaterialMode = HistoryMaterialMode.Nonlinear, InstantaneousConcrete = true, ConcreteTension = HistoryConcreteTension.TensionStiffening };
    static HBridgeInput Hogging(double factor) { var d = Gross(); return d with { Phases = d.Phases.Select(p => p with { MomentKNm = -factor * p.MomentKNm }).ToArray() }; }

    [TestMethod]
    public void TensionStiffeningLawFollowsCollinsAndMitchell()
    {
        var law = new HistoryTensionStiffeningConcreteLaw(ConcreteMaterialEN1992Data.C35_45, ConcreteMaterialEN1992Data.C35_45.Fctm);
        Assert.AreEqual(Ecm, law.InitialModulus, 1e-3); Assert.AreEqual(Fctm, law.TensileStrength, 1e-6);
        Assert.AreEqual(9.419693e-5, law.CrackingStrain, 1e-11);
        // (ε, σ, tangent): linear Ecm ε up to εcr = fctm/Ecm = 9,4196927e-5; then fctm/(1 + √(500 ε)), tangent −250 fctm/(√(500 ε)(1 + √(500 ε))²)
        foreach (var (e, sigma, tangent) in new[] { (5e-5, 1.703857, Ecm), (9.4196e-5, 3.209931, Ecm), (2e-4, 2.438759, -1464.79842),
            (1e-3, 1.880352, -389.43375), (1e-2, .991933, -34.27044) })
        {
            var r = law.Evaluate(e, law.InitialState(), 1);
            Assert.AreEqual(sigma, r.Stress, 2e-6, "σ at " + e); Assert.AreEqual(tangent, r.Tangent, Math.Abs(tangent) * 1e-6, "tangent at " + e);
        }
        // just after cracking: fctm/(1 + √(500 εcr)) = 2,63756 (the drop of the original formulation)
        Assert.AreEqual(2.63756, law.Evaluate(9.4197e-5, law.InitialState(), 1).Stress, 1e-5);
        Assert.AreEqual(-26.25, law.Evaluate(-1e-3, law.InitialState(), 1).Stress, 1e-9);
        Assert.ThrowsException<HistoryMaterialRangeException>(() => law.Evaluate(-.0036, law.InitialState(), 1));
        Assert.ThrowsException<NotSupportedException>(() => law.Evaluate(1e-4, law.InitialState(), .5));
    }

    [TestMethod]
    public void TensionStiffeningOptionGivesTheUncrackedResponseBeforeCracking()
    {
        // ×0,6 hogging: the slab stays below fctm (2,665 MPa at the top): G1 on the steel, G2 + Q on the uncracked section (net concrete with Ecm),
        // I = 9,076416e10 mm⁴, yG = −246,84 mm. With the default (tension neglected): κ = −4,5696e-7 1/mm, steel 91,94 / −85,79, bars 74,61 MPa
        var s = HBridgeHistoryAnalysis.Calculate(Hogging(.6), Stiffening()).Stages.Last();
        Equilibrium(s);
        Assert.AreEqual(-2.843502e-7, s.TotalPlane.Curvature, 1e-12);
        Assert.AreEqual(1.731741e-4, s.TotalPlane.AxialStrain, 1e-9);
        Assert.AreEqual(36.2877, s.Fibers.Single(f => f.ComponentId == HBridgeHistoryAnalysis.Top && Math.Abs(f.Fiber.Y + 1.3208) < 1e-3).Stress, 2e-3);
        Assert.AreEqual(-74.3074, s.Fibers.Single(f => f.ComponentId == HBridgeHistoryAnalysis.Bottom && Math.Abs(f.Fiber.Y + 1853.4151) < 1e-3).Stress, 2e-3);
        Assert.AreEqual(14.3492, s.Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Rebars && Math.Abs(f.Fiber.Y - 209) < 1e-9).Average(f => f.Stress), 2e-3);
        foreach (var f in s.Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete))
        { Assert.IsTrue(f.MechanicalStrain > 0 && f.MechanicalStrain < 9.419693e-5); Assert.AreEqual(Ecm * f.MechanicalStrain, f.Stress, 1e-6); }
    }

    [TestMethod]
    public void TensionStiffeningOptionConvergesThroughCracking()
    {
        // ×1 hogging (G1 −1500 steel, G2 −2000, Q −3000 kNm): the whole slab beyond εcr, on the tension stiffening branch.
        // With the default (tension neglected): κ = −7,6161e-7 1/mm, steel 153,24 / −142,98, bars 124,36 MPa
        var options = Stiffening();
        var s = HBridgeHistoryAnalysis.Calculate(Hogging(1), options).Stages.Last();
        Equilibrium(s);
        Assert.AreEqual(-5.574612e-7, s.TotalPlane.Curvature, 2e-12);
        Assert.AreEqual(4.152043e-4, s.TotalPlane.AxialStrain, 2e-9);
        Assert.AreEqual(87.0383, s.Fibers.Single(f => f.ComponentId == HBridgeHistoryAnalysis.Top && Math.Abs(f.Fiber.Y + 1.3208) < 1e-3).Stress, 5e-3);
        Assert.AreEqual(-129.7806, s.Fibers.Single(f => f.ComponentId == HBridgeHistoryAnalysis.Bottom && Math.Abs(f.Fiber.Y + 1853.4151) < 1e-3).Stress, 5e-3);
        Assert.AreEqual(52.7236, s.Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Rebars && Math.Abs(f.Fiber.Y - 209) < 1e-9).Average(f => f.Stress), 5e-3);
        foreach (var f in s.Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete))
        { Assert.IsTrue(f.MechanicalStrain > 9.419693e-5); Assert.AreEqual(Fctm / (1 + Math.Sqrt(500 * f.MechanicalStrain)), f.Stress, 1e-6); }
        var presented = HBridgeHistoryResults.Calculate(Hogging(1), options);
        Assert.AreEqual(HBridgeHistoryResults.ScopeFor(HistoryConcreteTension.TensionStiffening), presented.Scope);
        Assert.IsTrue(presented.Scope.Contains("tension stiffening") && !presented.Scope.Contains("trazione trascurata"));
        Assert.IsTrue(presented.Stages.Last().Warnings.Any(w => w.Contains("Collins e Mitchell")));
        Assert.AreEqual(HBridgeHistoryResults.Scope, HBridgeHistoryResults.Calculate(Hogging(1), options with { ConcreteTension = HistoryConcreteTension.Neglected }).Scope);
    }

    [TestMethod]
    public void MomentCurvatureWithTensionStiffeningFromTheVirginSection()
    {
        var control = new SectionResponseOptions { Control = SectionResponseControl.MomentCurvature, AxialForce = 0, SubstepsPerTarget = 4 };
        var r = HBridgeSectionResponse.Trace(Gross(), -3e-6, 100, control, new HBridgeHistoryOptions { MaterialMode = HistoryMaterialMode.Nonlinear, ConcreteTension = HistoryConcreteTension.TensionStiffening });
        Assert.IsTrue(r.Completed, r.Message);
        Assert.AreEqual(101, r.Points.Count);
        // uncracked: M = Ea I κ = −2859,07 kNm at κ = −1,5e-4 1/m (tension neglected: −1363,61 kNm)
        Assert.AreEqual(-2859.071e6, r.Points[5].MomentAtReference, .01e6);
        // own fibre integration at κ = −3e-3 1/m: −19186,74 kNm (tension neglected: −18152,08 kNm)
        Assert.AreEqual(-19186.74e6, r.Points.Last().MomentAtReference, 2e6);
    }

    [TestMethod]
    public void ConcreteTensionOptionOnlyAffectsTheNonlinearMode()
    {
        IHistoryMaterialLaw Concrete(HBridgeHistoryOptions o) => HBridgeHistoryAnalysis.CreateSection(Gross(), o).Components.Single(c => c.Id == HBridgeHistoryAnalysis.Concrete).Material;
        Assert.IsInstanceOfType(Concrete(new() { ConcreteTension = HistoryConcreteTension.TensionStiffening }), typeof(HistoryElasticLaw));
        Assert.IsInstanceOfType(Concrete(Stiffening()), typeof(HistoryTensionStiffeningConcreteLaw));
        Assert.IsTrue(Concrete(new() { MaterialMode = HistoryMaterialMode.Nonlinear }) is HistoryModelEnvelopeLaw { NeglectsTension: true }, "default: tension neglected");
        Assert.ThrowsException<ArgumentException>(() => Concrete(new() { ConcreteTension = (HistoryConcreteTension)7 }));
    }

    private sealed class Bounded : IHistoryMaterialLaw
    {
        public double InitialModulus => 200000; public bool IsLinear => false; public bool SupportsModulusFactors => false;
        public HistoryMaterialState InitialState() => new(0, 0);
        public HistoryMaterialResponse Evaluate(double e, HistoryMaterialState c, double f) =>
            e > .001 ? throw new HistoryMaterialRangeException("Limite di prova superato.") : new(200000 * e, 200000, new(e, 200000 * e));
    }
    [TestMethod]
    public void NewtonFailureReportsTheConstitutiveRejection()
    {
        var section = new HistorySection([Component("steel", 200000) with { Material = new Bounded() }]);
        var ex = Assert.ThrowsException<HistoryConvergenceException>(() => BridgeHistoryAnalysis.Calculate(section, [new HistoryPhase { DeltaN = 60000 }]));
        StringAssert.Contains(ex.Message, "Limite di prova superato");
    }
}
