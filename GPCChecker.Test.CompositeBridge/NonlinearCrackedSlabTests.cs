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
