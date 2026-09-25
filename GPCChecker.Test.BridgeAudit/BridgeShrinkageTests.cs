using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X.Core;

namespace BridgeAudit;

[TestClass, TestCategory("BridgeShrinkage"), DoNotParallelize]
public class BridgeShrinkageTests
{
    static void Near(double expected, double actual, string label, double tolerance = 2e-6) => Assert.AreEqual(expected, actual, tolerance * Math.Max(1, Math.Abs(expected)), label);
    static JsonObject Data(double strain = -250, double phi = 2, bool top = true, bool bottom = true)
    {
        var d = BridgeSection.Defaults(); d["classe4"] = false; d["rebars_top"] = top; d["rebars_bottom"] = bottom;
        var p = BridgeSection.ShrinkagePhase(); p["epsilon_cs"] = strain; p["phi"] = phi;
        d["fasi"] = new JsonArray(p); return d;
    }
    [DataTestMethod]
    [DataRow(false, false, 0d)] [DataRow(true, false, 0d)] [DataRow(false, true, 0d)] [DataRow(true, true, 0d)]
    [DataRow(false, false, 2d)] [DataRow(true, false, 2d)] [DataRow(false, true, 2d)] [DataRow(true, true, 2d)]
    public void ShrinkageMatchesIndependentElasticCompatibility(bool top, bool bottom, double phi)
    {
        var d = Data(top: top, bottom: bottom, phi: phi); var r = BridgeSection.Calculate(d); var g = r.Geometry; var c = r.Stages[0].Contributions[0];
        double ea = r.Materials.Ea, es = r.Materials.Es, ec = r.Materials.Ec / (1 + .55 * phi), eps = -250e-6;
        var parts = BridgeSection.SteelPartProperties(g, true);
        double ac = g.Width * g.SlabHeight, yc = g.SlabHeight / 2, ic = g.Width * Math.Pow(g.SlabHeight, 3) / 12;
        double a = parts.Sum(p => p.Area) * ea + ac * ec + g.Bars.Sum(b => b.Area) * (es - ec);
        double s = parts.Sum(p => p.Area * p.Y) * ea + ac * yc * ec + g.Bars.Sum(b => b.Area * b.Y) * (es - ec);
        double i = parts.Sum(p => (p.Name == "Anima" ? p.Ix : 0) + p.Area * p.Y * p.Y) * ea + (ic + ac * yc * yc) * ec + g.Bars.Sum(b => b.Area * b.Y * b.Y) * (es - ec);
        double f = ec * eps * (ac - g.Bars.Sum(b => b.Area)), q = ec * eps * (ac * yc - g.Bars.Sum(b => b.Area * b.Y));
        double e0 = (f * i - q * s) / (a * i - s * s), curvature = (q * a - f * s) / (a * i - s * s);
        foreach (var p in r.Stages[0].Points)
        {
            double strain = e0 + curvature * p.Y;
            double expected = p.Material == "CLS" ? ec * (strain - eps) : (p.Material == "Armatura" ? es : ea) * strain;
            Near(expected, p.Stress, p.Name);
        }
        Near(0, a * e0 + s * curvature - f, "N esterno", 1e-4); Near(0, s * e0 + i * curvature - q, "Momento statico esterno", 1e-2);
        Near(0, c.N, "N riportato"); Near(0, c.MomentAtInterface, "M riportato"); Near(f / 1000, c.EquivalentN, "N equivalente");
        Assert.IsTrue(c.ConcreteStressOffset > 0 && c.EquilibriumResidual < 1e-5);
    }
    [DataTestMethod] [DataRow(0d)] [DataRow(250d)] [DataRow(-250d)]
    public void StrainSignAndZeroArePreserved(double strain)
    {
        var actual = BridgeSection.Calculate(Data(strain)).Stages[0]; var baseline = BridgeSection.Calculate(Data(-250)).Stages[0];
        foreach (var pair in actual.Points.Zip(baseline.Points)) Near(-strain / 250 * pair.Second.Stress, pair.First.Stress, pair.First.Name);
    }
    [TestMethod]
    public void MultipleShrinkageIncrementsSuperposeAtAnyPosition()
    {
        var d = Data(-100); var steel = BridgeSection.Phase("G1", "Solo acciaio", 0, 800);
        var another = BridgeSection.ShrinkagePhase(); another["epsilon_cs"] = -150;
        d.Array("fasi").Insert(0, steel); d.Array("fasi").Add(another);
        var original = BridgeSection.Calculate(d);
        var reordered = (JsonObject)d.DeepClone(); var moving = reordered.Array("fasi")[1]!; reordered.Array("fasi").RemoveAt(1); reordered.Array("fasi").Insert(0, moving);
        var second = BridgeSection.Calculate(reordered);
        foreach (var pair in original.Stages.Last().Points.Zip(second.Stages.Last().Points)) Near(pair.First.Stress, pair.Second.Stress, pair.First.Name);
        var total = Data(-250); total.Array("fasi").Insert(0, steel.DeepClone());
        foreach (var pair in original.Stages.Last().Points.Zip(BridgeSection.Calculate(total).Stages.Last().Points)) Near(pair.First.Stress, pair.Second.Stress, pair.First.Name);
    }
    [TestMethod]
    public void AssignedNAndPhiProduceSameShrinkageField()
    {
        var d = Data(); var a = BridgeSection.Calculate(d); d.Array("fasi")[0]!["modo"] = "Da n"; d.Array("fasi")[0]!["n"] = a.Stages[0].Contributions[0].HomogenizationN;
        foreach (var pair in a.Stages[0].Points.Zip(BridgeSection.Calculate(d).Stages[0].Points)) Near(pair.First.Stress, pair.Second.Stress, pair.First.Name);
    }
    [TestMethod]
    public void Class4ShrinkageConvergesAndShearRemainsAnInputOnly()
    {
        var d = Data(); d["classe4"] = true; var p = BridgeSection.Phase("G", "Composta", 0, 3500); p["V"] = 850; d.Array("fasi").Insert(0, p);
        var a = BridgeSection.Calculate(d); p["V"] = 0; var b = BridgeSection.Calculate(d);
        foreach (var pair in a.Stages.Last().Points.Zip(b.Stages.Last().Points)) Near(pair.First.Stress, pair.Second.Stress, pair.First.Name);
        Assert.IsNotNull(a.Stages.Last().Shear); Assert.AreEqual(850, a.Stages.Last().Contributions.Sum(c => c.V));
        Assert.IsTrue(a.Stages.Last().Contributions.All(c => c.EquilibriumResidual < 1e-5));
    }
    [TestMethod]
    public void SlabWithoutRebarsHasFinitePropertiesAndPhaseNControlsGrossProperties()
    {
        var d = Data(top: false, bottom: false); var phase = d.Array("fasi")[0]!.AsObject();
        var s = BridgeSection.GrossPhaseProperties(d, phase, slabOnly: true); var g = BridgeSection.Geometry(d);
        Near(g.Width * g.SlabHeight, s.Area, "area soletta senza barre"); Near(g.Width * Math.Pow(g.SlabHeight, 3) / 12, s.Ix, "inerzia soletta senza barre");
        var a = BridgeSection.GrossPhaseProperties(d, phase); phase["phi"] = 4; var b = BridgeSection.GrossPhaseProperties(d, phase);
        Assert.IsTrue(b.Area < a.Area && b.Y < a.Y);
    }
}

