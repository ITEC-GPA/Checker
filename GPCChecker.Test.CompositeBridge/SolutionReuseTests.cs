using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Checkers.CompositeBridge;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

/// <summary>
/// Reuse of the solutions within an analysis (30/10/2026): the same effective state, phase and solver are solved once, with the numbers of a
/// new solution, and every stage keeps its own contributions
/// </summary>
[TestClass, DoNotParallelize]
public class SolutionReuseTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private static string Results(HBridgeAnalysisResult r) => JsonSerializer.Serialize(new { r.Geometry, r.Materials, r.Stages }, Json);

    private static IEnumerable<object[]> Inputs()
    {
        var d = CompositeBridgeApiTests.Input();
        // class 4 with warm start: the situations start from the previous converged state; small increments converge there
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
            new() { Name = "Q", MomentKNm = 3000, ShearKN = 500 }],
            Options = d.Options with { LimitState = BridgeLimitState.Rare } } };
    }

    [TestMethod]
    [DynamicData(nameof(Inputs), DynamicDataSourceType.Method)]
    public void ReusedSolutionsAreTheSolutionsComputedAgain(string name, HBridgeInput input)
    {
        try
        {
            HBridgeSection.SolutionReuse = false;
            string computed = Results(HBridgeSection.Calculate(input));
            HBridgeSection.SolutionReuse = true;
            var reused = HBridgeSection.Calculate(input);
            Assert.AreEqual(computed, Results(reused), name);
            // every stage has its own contribution objects, as before
            var contributions = reused.Stages.SelectMany(s => s.Contributions).ToArray();
            Assert.AreEqual(contributions.Length, contributions.Distinct(ReferenceEqualityComparer.Instance).Count(), name + ": shared contributions");
        }
        finally { HBridgeSection.SolutionReuse = true; }
    }
}
