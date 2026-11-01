using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Checkers.CompositeBridge;
using GPC.Model.Data.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

/// <summary>
/// Analyses from the situations of an earlier analysis (1/11/2026), as the details of a check: an input that differs only in studs, stiffeners,
/// transverse reinforcement, fatigue, limit state or an unused common elevation gets the result of its own analysis, with the same numbers and
/// its own objects; any other input is calculated in full
/// </summary>
[TestClass, DoNotParallelize]
public class RecalculateTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    // the tension stiffening of the bars is a field of the points, outside the serialization
    private static string Results(HBridgeAnalysisResult r) => JsonSerializer.Serialize(new { r.Input, r.Geometry, r.Materials, r.Stages,
        Stiffening = r.Stages.Select(s => s.Points.Select(p => p.GetTensionStiffening())) }, Json);

    private static readonly BridgeStudOptions Studs = new() { Enabled = true, CountPerRow = 2, Diameter = 22, Height = 150, LongitudinalPitch = 200,
        TransversePitch = 120, Fu = 450, GammaV = 1.25, HeadDiameter = 35, HeadThickness = 12, RequiredCover = 20 };

    /// <summary>The check of a section: serviceability, without details</summary>
    private static IEnumerable<object[]> Checks()
    {
        var d = CompositeBridgeApiTests.Input() with { Options = CompositeBridgeApiTests.Input().Options with { LimitState = BridgeLimitState.Rare } };
        var phases = Enumerable.Range(0, 6).Select(i => new BridgePhase { Name = "P" + i, Kind = i == 0 ? BridgePhaseKind.SteelOnly : BridgePhaseKind.Composite,
            MomentKNm = i == 4 ? 1e-6 : 800 + 150 * i, ShearKN = 100 * i, Phi = i % 3 }).ToArray();
        yield return new object[] { "class 4", d with { Phases = phases } };
        yield return new object[] { "class 4, gross start", d with { Phases = phases, Options = d.Options with { AcceleratedIteration = false } } };
        yield return new object[] { "gross section", d with { Phases = phases, Options = d.Options with { Class4 = false } } };
        yield return new object[] { "cracked slab and shrinkage", d with { Phases = [
            new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 1500, ShearKN = 300 },
            new() { Name = "G2", MomentKNm = 2000, Phi = 2, PsiL = 1.1, ShearKN = 200 },
            new() { Name = "R", Kind = BridgePhaseKind.Shrinkage, Phi = 2, PsiL = .55, ShrinkageMicrostrain = -300 },
            new() { Name = "Q-", Kind = BridgePhaseKind.ConcreteExcluded, MomentKNm = -2500, ShearKN = 400 },
            new() { Name = "Q", MomentKNm = 3000, ShearKN = 500 }] } };
    }

    /// <summary>The details of the check: ultimate limit state, studs, stiffeners and transverse reinforcement</summary>
    private static HBridgeInput Details(HBridgeInput check) => check with
    {
        Options = check.Options with { LimitState = BridgeLimitState.Ultimate, CommonLoadY = 150 },
        Studs = Studs,
        Transverse = new() { Enabled = true, TopDiameter = 16, TopPitch = 150, BottomDiameter = 16, BottomPitch = 150, CotTheta = 1, LeftFlowFraction = .5,
            AnchorageLength = 700, GoodBond = true, LeftConcreteEdge = 500, RightConcreteEdge = 500 },
        Intermediate = new() { Enabled = true, LeftPanel = 3000, EqualPanels = true, Width = 150, Thickness = 20, LengthFactor = 1 },
        Support = new() { Enabled = true, Layout = BridgeStiffenerLayout.Symmetric, Width = 200, Thickness = 20, LengthFactor = 1, LeftPanel = 2000,
            RightPanel = 2000, ReactionKN = 3000, FootprintLength = 400, FootprintWidth = 800, Location = BridgeSupportLocation.Internal }
    };

    /// <summary>The recalculated analysis is the full one and has its own objects; reused: whether the situations of the earlier one were kept</summary>
    private static void AssertAsCalculated(HBridgeAnalysisResult? earlier, HBridgeInput input, bool reused, string name)
    {
        long before = HBridgeSection.Recalculations;
        var recalculated = HBridgeSection.Recalculate(earlier, input);
        Assert.AreEqual(reused ? 1 : 0, HBridgeSection.Recalculations - before, name + ": situations reused");
        Assert.AreEqual(Results(HBridgeSection.Calculate(input)), Results(recalculated), name);
        if (earlier is null) return;
        var own = recalculated.Stages.SelectMany(s => s.Contributions.Cast<object>().Append(s.Effective).Append(s.EffectiveSteel)).ToArray();
        var old = earlier.Stages.SelectMany(s => s.Contributions.Cast<object>().Append(s.Effective).Append(s.EffectiveSteel)).ToHashSet(ReferenceEqualityComparer.Instance);
        Assert.IsFalse(own.Any(old.Contains), name + ": objects of the earlier analysis");
    }

    [TestMethod]
    [DynamicData(nameof(Checks), DynamicDataSourceType.Method)]
    public void DetailsOfACheckAreTheFullAnalysis(string name, HBridgeInput check)
    {
        var earlier = HBridgeSection.Calculate(check);
        AssertAsCalculated(earlier, Details(check), true, name);
        // from a recalculated analysis too, and back to the check
        var details = HBridgeSection.Recalculate(earlier, Details(check));
        AssertAsCalculated(details, check, true, name + ", back");
        AssertAsCalculated(details, Details(check) with { Options = Details(check).Options with { LimitState = BridgeLimitState.QuasiPermanent } }, true,
            name + ", quasi permanent");
    }

    [TestMethod]
    public void OtherInputsAreCalculatedInFull()
    {
        var check = (HBridgeInput)Checks().First()[1];
        var earlier = HBridgeSection.Calculate(check);
        var details = Details(check);
        AssertAsCalculated(null, details, false, "no earlier analysis");
        AssertAsCalculated(earlier with { }, details, false, "a copy of the earlier analysis");
        AssertAsCalculated(earlier, details with { Phases = details.Phases.Select((p, i) => i == 2 ? p with { MomentKNm = p.MomentKNm + 1e-9 } : p).ToArray() },
            false, "another moment");
        AssertAsCalculated(earlier, details with { Phases = details.Phases.Take(5).ToArray() }, false, "fewer phases");
        AssertAsCalculated(earlier, details with { Geometry = details.Geometry with { SlabWidth = 3200 } }, false, "another geometry");
        AssertAsCalculated(earlier, details with { Options = details.Options with { Class4 = false } }, false, "other analysis options");
        AssertAsCalculated(earlier, details with { Options = details.Options with { GammaM0 = 1.1 } }, false, "other partial factors");
        AssertAsCalculated(earlier, details with { Materials = details.Materials with { } }, true, "the same Model materials");
        var materials = CompositeBridgeApiTests.Input().Materials;
        AssertAsCalculated(earlier, details with { Materials = materials with { Steel = SteelMaterialEN1993Data.S355 } },
            ReferenceEquals(SteelMaterialEN1993Data.S355, check.Materials.Steel), "other Model materials");
    }

    [TestMethod]
    public void TheCommonElevationCountsOnlyWhereAPhaseRefersToIt()
    {
        var check = (HBridgeInput)Checks().First()[1];
        check = check with { Options = check.Options with { CommonLoadY = 150 },
            Phases = check.Phases.Select((p, i) => i == 3 ? p with { Reference = BridgeLoadReference.CommonElevation, ForceKN = 500 } : p).ToArray() };
        var earlier = HBridgeSection.Calculate(check);
        AssertAsCalculated(earlier, Details(check), true, "the same elevation");
        AssertAsCalculated(earlier, Details(check) with { Options = Details(check).Options with { CommonLoadY = 0 } }, false, "another elevation");
    }

    [TestMethod]
    public void CombinationsOfAStationAreTheSeparateAnalyses()
    {
        var d = (HBridgeInput)Checks().Last()[1];
        var checks = new[] { 3000d, 1e-6, -2500, 3000 }.Select(q => d with { Phases = d.Phases.Select(p => p.Name == "Q" ? p with { MomentKNm = q } : p).ToArray() }).ToArray();
        var earlier = HBridgeSection.Calculate(checks).Select(r => r.Analysis).ToList();
        // a detail that fails (too many studs per row), an input without earlier analysis and one with another geometry
        var inputs = checks.Select(Details).ToList();
        inputs[1] = inputs[1] with { Studs = Studs with { CountPerRow = 25 } };
        inputs.Add(Details(checks[0]));
        earlier.Add(null);
        inputs.Add(Details(checks[0]) with { Geometry = d.Geometry with { SlabWidth = 3200 } });
        earlier.Add(earlier[0]);
        long before = HBridgeSection.Recalculations;
        var batch = HBridgeSection.Recalculate(earlier, inputs);
        Assert.AreEqual(4, HBridgeSection.Recalculations - before, "situations reused");
        for (int i = 0; i < inputs.Count; i++)
        {
            HBridgeAnalysisResult? single = null; Exception? error = null;
            try { single = HBridgeSection.Calculate(inputs[i]); } catch (Exception e) { error = e; }
            if (error is not null)
            {
                Assert.AreEqual(error.GetType(), batch[i].Error?.GetType(), $"[{i}]");
                Assert.AreEqual(error.Message, batch[i].Error!.Message, $"[{i}]");
                continue;
            }
            Assert.IsNull(batch[i].Error, $"[{i}]: {batch[i].Error?.Message}");
            Assert.AreEqual(Results(single!), Results(batch[i].Analysis!), $"[{i}]");
        }
        Assert.IsNotNull(batch[1].Error, "too many studs");
        var stages = batch.Concat(earlier.Select(e => new HBridgeBatchResult(e, null))).Where(b => b.Analysis is not null).Distinct()
            .SelectMany(b => b.Analysis!.Stages).ToArray();
        Assert.AreEqual(stages.Length, stages.Distinct(ReferenceEqualityComparer.Instance).Count(), "shared stages");
        var contributions = stages.SelectMany(s => s.Contributions).ToArray();
        Assert.AreEqual(contributions.Length, contributions.Distinct(ReferenceEqualityComparer.Instance).Count(), "shared contributions");
    }

    [TestMethod]
    public void WithoutReuseEverythingIsCalculatedInFull()
    {
        var check = (HBridgeInput)Checks().First()[1];
        try
        {
            var earlier = HBridgeSection.Calculate(check);
            HBridgeSection.SolutionReuse = false;
            AssertAsCalculated(earlier, Details(check), false, "no reuse");
        }
        finally { HBridgeSection.SolutionReuse = true; }
    }

    [TestMethod]
    public void EveryInputNeedsItsEarlierAnalysis()
    {
        var check = CompositeBridgeApiTests.Input();
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Recalculate(new HBridgeAnalysisResult?[] { null }, new[] { check, check }));
        Assert.ThrowsException<OperationCanceledException>(() => HBridgeSection.Recalculate(HBridgeSection.Calculate(check), Details(check), new CancellationToken(true)));
    }
}
