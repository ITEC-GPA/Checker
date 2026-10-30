using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Checkers.CompositeBridge;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

/// <summary>
/// Analyses of several inputs in one call (30/10/2026), as the combinations of a station: the shared leading phases are iterated once and every
/// input gets the result, or the error, of its own analysis, with the same numbers and its own objects
/// </summary>
[TestClass, DoNotParallelize]
public class BatchTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private static string Results(HBridgeAnalysisResult r) => JsonSerializer.Serialize(new { r.Geometry, r.Materials, r.Stages }, Json);

    /// <summary>The combinations of a station: the same permanent phases, then a variable action and the shrinkage</summary>
    private static HBridgeInput[] Combinations(HBridgeInput d, params double[] variable) => variable.Select(q => d with { Phases = [
        new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 1500, ShearKN = 300 },
        new() { Name = "G2", MomentKNm = 2000, Phi = 2, PsiL = 1.1, ShearKN = 200 },
        new() { Name = "Q", MomentKNm = q, ShearKN = q / 5 },
        new() { Name = "R", Kind = BridgePhaseKind.Shrinkage, Phi = 2, PsiL = .55, ShrinkageMicrostrain = -300 }] }).ToArray();

    private static void AssertAsSeparate(IReadOnlyList<HBridgeInput> inputs, string name)
    {
        var batch = HBridgeSection.Calculate(inputs);
        Assert.AreEqual(inputs.Count, batch.Count, name);
        for (int i = 0; i < inputs.Count; i++)
        {
            HBridgeAnalysisResult? single = null; Exception? error = null;
            try { single = HBridgeSection.Calculate(inputs[i]); } catch (Exception e) { error = e; }
            if (error is not null)
            {
                Assert.IsNotNull(batch[i].Error, $"{name} [{i}]: error expected");
                Assert.AreEqual(error.GetType(), batch[i].Error!.GetType(), $"{name} [{i}]");
                Assert.AreEqual(error.Message, batch[i].Error!.Message, $"{name} [{i}]");
                continue;
            }
            Assert.IsNull(batch[i].Error, $"{name} [{i}]: {batch[i].Error?.Message}");
            Assert.AreEqual(Results(single!), Results(batch[i].Analysis!), $"{name} [{i}]");
            Assert.AreSame(inputs[i].Phases[0].Name, batch[i].Analysis!.Input.Phases[0].Name, $"{name} [{i}]: own input");
        }
        // no object of a result belongs to another one
        var stages = batch.Where(b => b.Analysis is not null).SelectMany(b => b.Analysis!.Stages).ToArray();
        Assert.AreEqual(stages.Length, stages.Distinct(ReferenceEqualityComparer.Instance).Count(), name + ": shared stages");
        var contributions = stages.SelectMany(s => s.Contributions).ToArray();
        Assert.AreEqual(contributions.Length, contributions.Distinct(ReferenceEqualityComparer.Instance).Count(), name + ": shared contributions");
    }

    [TestMethod]
    public void CombinationsOfAStationAreTheSeparateAnalyses()
    {
        var d = CompositeBridgeApiTests.Input();
        AssertAsSeparate(Combinations(d, 3000, 1e-6, -2500, 3000, 800), "class 4");
        AssertAsSeparate(Combinations(d with { Options = d.Options with { AcceleratedIteration = false } }, 3000, -2500), "class 4, gross start");
        AssertAsSeparate(Combinations(d with { Options = d.Options with { Class4 = false } }, 3000, -2500), "gross section");
    }

    [TestMethod]
    public void InputsWithOtherDataAreCalculatedApart()
    {
        var d = CompositeBridgeApiTests.Input();
        var wider = d with { Geometry = d.Geometry with { SlabWidth = 3200 } };
        AssertAsSeparate(Combinations(d, 3000).Concat(Combinations(wider, 3000)).Concat(Combinations(d, -2500)).ToArray(), "two geometries");
    }

    [TestMethod]
    public void AnInvalidInputGetsItsOwnError()
    {
        var inputs = Combinations(CompositeBridgeApiTests.Input(), 3000, 2000).ToList();
        inputs.Insert(1, inputs[0] with { Phases = inputs[0].Phases.Select(p => p with { Active = false }).ToArray() });
        inputs.Add(inputs[0] with { Phases = inputs[0].Phases.Reverse().ToArray() });
        AssertAsSeparate(inputs, "errors");
        var batch = HBridgeSection.Calculate(inputs);
        Assert.IsInstanceOfType(batch[1].Error, typeof(ArgumentException));
        Assert.IsNull(batch[0].Error);
        Assert.IsNull(batch[2].Error);
        Assert.IsNotNull(batch[3].Error, "steel-only phase after the casting");
    }

    [TestMethod]
    public void PhasesEqualOnlyBitForBitAreShared()
    {
        // 0 and -0 compare equal as doubles but are different inputs: each input keeps its own numbers
        var inputs = Combinations(CompositeBridgeApiTests.Input(), 0.0, -0.0, 0.0);
        AssertAsSeparate(inputs, "signed zero");
    }

    [TestMethod]
    public void CancellationStopsTheBatch()
    {
        var inputs = Combinations(CompositeBridgeApiTests.Input(), 3000, 2000);
        Assert.ThrowsException<OperationCanceledException>(() => HBridgeSection.Calculate(inputs, new CancellationToken(true)));
    }
}
