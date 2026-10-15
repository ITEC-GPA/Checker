using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

/// <summary>
/// SLE stresses of the bars of the cracked slab with tension stiffening, EN 1994-2 §7.2.1(1) and EN 1994-1-1 §7.4.3(3): σs = σs,0 + Δσs,
/// Δσs = 0,4 fctm/(αst ρs), ρs = As/Act, αst = A I/(Aa Ia). Expected values: independent Python calculation from the standard (agent-bridge/py/pon1.py,
/// cross-checked with the verifier's ponte-ts-verifica/indip.py): default section, G1 «Solo acciaio» −1500, G2 and Q «Soletta esclusa» −2000 and −10000 kNm.
/// </summary>
[TestClass, DoNotParallelize]
public class TensionStiffeningTests
{
    // EN 1992-1-1 Tab. 3.1: fctm = 0,30 fck^(2/3) for C35/45
    const double Fctm = 3.20996;
    // 2 rows x 20 Ø16 (Ø16/150 on 3000 mm) over Act = 3000 x 250 mm
    const double Rho = 8042.4772 / 750000;

    static HBridgeInput Hogging(BridgeLimitState state, bool class4)
    {
        var d = CompositeBridgeApiTests.Input();
        return d with
        {
            Options = d.Options with { LimitState = state, Class4 = class4 },
            Phases = [new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = -1500 },
                new() { Name = "G2", Kind = BridgePhaseKind.ConcreteExcluded, MomentKNm = -2000 },
                new() { Name = "Q", Kind = BridgePhaseKind.ConcreteExcluded, MomentKNm = -10000 }]
        };
    }
    static BridgeStressPoint Bar(BridgeStage s, string name) => s.Points.Single(p => p.Name == name);

    [DataTestMethod]
    // class 4 off (gross steel): Aa = 58700, Ia = 3,375737e10, A = 66359,502, I = 4,328908e10 → αst = 1,44969, Δσs = 82,5956;
    // σs,0 = 297,3968 (y = 205) and 255,1559 (y = 45)
    [DataRow(false, 1.44969, 82.5956, 297.3968, 255.1559)]
    // class 4 (finding PON-1): the effective steel of the converged situation (web 1320,13 + 285,44, bottom flange 696,62):
    // Aa = 55876,589, Ia = 3,326205e10, A = 63536,091, I = 4,242049e10 → αst = 1,45016, Δσs = 82,5684; σs,0 = 297,0792 and 253,9734
    [DataRow(true, 1.45016, 82.5684, 297.0792, 253.9734)]
    public void CumulativeRareAddsTensionStiffeningToTheTensileBars(bool class4, double alpha, double increment, double top, double bottom)
    {
        var s = HBridgeSection.Calculate(Hogging(BridgeLimitState.Rare, class4)).Stages.Last();
        foreach (var (name, atCrack) in new[] { ("Armatura · y=205", top), ("Armatura · y=45", bottom) })
        {
            var p = Bar(s, name); var t = p.GetTensionStiffening();
            Assert.IsNotNull(t, name);
            Assert.AreEqual(Fctm, t!.Fctm, 1e-5);
            Assert.AreEqual(Rho, t.ReinforcementRatio, 1e-9);
            Assert.AreEqual(alpha, t.Alpha, 1e-4);
            Assert.AreEqual(increment, t.Increment, 5e-3);
            Assert.AreEqual(atCrack, t.StressAtCrack, 5e-3);
            Assert.AreEqual(atCrack, p.Contributions.Sum(), 5e-3, "the contributions give σs,0");
            Assert.AreEqual(atCrack + increment, p.Stress, 1e-2);
            Assert.AreEqual((atCrack + increment) / 360, p.Utilization!.Value, 5e-5, "0,8 fyk = 360 MPa");
        }
        // finding: 297,1 + 82,6 = 379,6 MPa > 360 MPa, η = 1,055 (class 4)
        if (class4) { Assert.AreEqual(379.6477, Bar(s, "Armatura · y=205").Stress, 1e-2); Assert.AreEqual(1.0546, Bar(s, "Armatura · y=205").Utilization!.Value, 1e-4); }
        // the structural steel is unchanged
        Assert.IsTrue(s.Points.Where(p => p.Material != "Armatura").All(p => p.GetTensionStiffening() is null && Math.Abs(p.Stress - p.Contributions.Sum()) < 1e-9));
        Assert.IsTrue(s.Warnings.Any(w => w.Contains("tension stiffening") && w.Contains("§7.4.3(3)")));
    }

    [TestMethod]
    public void QuasiPermanentAppliesTheSameIncrement()
    {
        var p = Bar(HBridgeSection.Calculate(Hogging(BridgeLimitState.QuasiPermanent, false)).Stages.Last(), "Armatura · y=205");
        Assert.AreEqual(297.3968 + 82.5956, p.Stress, 1e-2);
    }

    [TestMethod]
    public void NoTensionStiffeningAtUltimateOnCompressedBarsOrWithoutCrackedContributions()
    {
        static void None(HBridgeAnalysisResult r)
        {
            foreach (var s in r.Stages)
                foreach (var p in s.Points)
                {
                    Assert.IsNull(p.GetTensionStiffening(), s.Name + " " + p.Name);
                    Assert.AreEqual(p.Contributions.Sum(), p.Stress, 1e-9, s.Name + " " + p.Name);
                }
        }
        // SLU: no tension stiffening
        None(HBridgeSection.Calculate(Hogging(BridgeLimitState.Ultimate, false)));
        // sagging on the cracked section: the bars are compressed
        var d = Hogging(BridgeLimitState.Rare, false);
        None(HBridgeSection.Calculate(d with { Phases = d.Phases.Select(p => p with { MomentKNm = -p.MomentKNm }).ToArray() }));
        // only «Composta»: uncracked model (the steel-only situation has no bar stress)
        None(HBridgeSection.Calculate(d with { Phases = d.Phases.Select(p => p.Kind == BridgePhaseKind.ConcreteExcluded ? p with { Kind = BridgePhaseKind.Composite } : p).ToArray() }));
        // the first situation (steel only) has no cracked contribution
        Assert.IsNull(Bar(HBridgeSection.Calculate(d).Stages[0], "Armatura · y=205").GetTensionStiffening());
    }

    [TestMethod]
    public void LinearHistoryAddsTheSameTensionStiffening()
    {
        var d = Hogging(BridgeLimitState.Rare, false);
        var s = HBridgeHistoryResults.Calculate(d, new HBridgeHistoryOptions { MaterialMode = HistoryMaterialMode.Linear }).Stages.Last();
        var p = Bar(s, "Armatura · y=205"); var t = p.GetTensionStiffening()!;
        Assert.AreEqual(1.44969, t.Alpha, 1e-4);
        Assert.AreEqual(82.5956, t.Increment, 5e-3);
        Assert.AreEqual(297.3968, t.StressAtCrack, 1e-2);
        Assert.AreEqual(379.9924, p.Stress, 2e-2);
        Assert.AreEqual(1.05553, p.Utilization!.Value, 1e-4);
        // the extreme integration point of the bars, y = 205 + Ø/4: σs,0 = 298,4528
        var extreme = s.Points.Where(x => x.Material == "Armatura" && x.Name.Contains("estremo")).OrderByDescending(x => x.Y).First();
        Assert.AreEqual(209, extreme.Y, 1e-9);
        Assert.AreEqual(298.4528 + 82.5956, extreme.Stress, 2e-2);
        Assert.IsTrue(s.Warnings.Any(w => w.Contains("tension stiffening")));
        // the first situation (steel only) has no cracked contribution
        Assert.IsTrue(HBridgeHistoryResults.Calculate(d, new HBridgeHistoryOptions()).Stages[0].Points.All(x => x.GetTensionStiffening() is null));
    }
}
