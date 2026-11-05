using System.Text.Json;
using System.Text.Json.Serialization;
using CompositeBridgePerformance;
using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass, DoNotParallelize]
public class LinearBatchReuseTests
{
    private static readonly JsonSerializerOptions Json = new() {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals, ReferenceHandler = ReferenceHandler.IgnoreCycles
    };
    private static string Snapshot(HBridgeAnalysisResult r) => JsonSerializer.Serialize(new { r.Geometry, r.Materials, r.Stages,
        History = r.Stages.Select(s => s.GetHistory()?.State), Stiffening = r.Stages.Select(s => s.Points.Select(p => p.GetTensionStiffening())) }, Json);
    private static HBridgeAnalysisResult Single(HBridgeInput input, bool history, HBridgeHistoryOptions? options = null) =>
        history ? HBridgeHistoryResults.Calculate(input, options) : HBridgeSection.Calculate(input);
    private static IReadOnlyList<HBridgeBatchResult> Batch(IReadOnlyList<HBridgeInput> inputs, bool history, int degree,
        HBridgeHistoryOptions? options = null, CancellationToken token = default) => history
        ? HBridgeHistoryResults.CalculateBatch(inputs, options, degree, token) : HBridgeSection.CalculateBatch(inputs, degree, token);

    [DataTestMethod]
    [DataRow(false, false)] [DataRow(false, true)] [DataRow(true, false)] [DataRow(true, true)]
    public void OneHundredBranchesMatchFullIndependentAnalysesAtEveryParallelDegree(bool history, bool class4)
    {
        var inputs = BridgePerformanceCases.Station(0, class4);
        var expected = inputs.Select(d => Snapshot(Single(d, history))).ToArray();
        foreach (int degree in new[] { 1, 2, 8 }) {
            var actual = Batch(inputs, history, degree);
            for (int i = 0; i < inputs.Length; i++) {
                Assert.IsNull(actual[i].Error, actual[i].Error?.ToString());
                Assert.AreEqual(expected[i], Snapshot(actual[i].Analysis!), $"degree={degree}, Q={i}");
            }
            var reverse = Batch(inputs.Reverse().ToArray(), history, degree);
            for (int i = 0; i < inputs.Length; i++) Assert.AreEqual(expected[i], Snapshot(reverse[inputs.Length - 1 - i].Analysis!), $"reverse degree={degree}, Q={i}");
        }
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void MixedStationsPrefixesAndFailedBranchesRetainTheirOwnResult(bool history)
    {
        var a = BridgePerformanceCases.Station(0, true, 3); var b = BridgePerformanceCases.Station(10, true, 3);
        var cases = new List<HBridgeInput> { a[0], b[0], a[1], b[1], a[0], a[2], b[2] };
        cases.Add(a[0] with { Phases = a[0].Phases.Select((p, i) => i == 2 ? p with { ShrinkageMicrostrain = -150, Phi = 1 } : p).ToArray() });
        cases.Add(a[0] with { Phases = a[0].Phases.Select((p, i) => i == 0 ? p with { MomentKNm = 1200 } : p).ToArray() }); // no common prefix
        cases.Add(a[0] with { Phases = a[0].Phases.Append(new BridgePhase { Name = "Q2", MomentKNm = -200, Phi = .5 }).ToArray() });
        cases.Add(a[0] with { Phases = a[0].Phases.Select((p, i) => i == 3 ? p with { Phi = -1 } : p).ToArray() });
        cases.Add(a[0] with { Geometry = a[0].Geometry with { WebThickness = 0 } });
        cases.Add(a[0] with { Phases = a[0].Phases.Select((p, i) => i == 3 ? p with { TorsionKNm = 10 } : p).ToArray() });
        var actual = Batch(cases, history, 4);
        for (int i = 0; i < cases.Count; i++) {
            HBridgeAnalysisResult? expected = null; Exception? failure = null;
            try { expected = Single(cases[i], history); } catch (Exception e) { failure = e; }
            if (failure is not null) {
                Assert.IsNotNull(actual[i].Error); Assert.AreEqual(failure.GetType(), actual[i].Error!.GetType(), "case " + i);
                Assert.IsNull(actual[i].Analysis);
            } else {
                Assert.IsNull(actual[i].Error); Assert.AreEqual(Snapshot(expected!), Snapshot(actual[i].Analysis!), "case " + i);
            }
        }
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void BatchResultCollectionsAreOwnedByEachInput(bool history)
    {
        var input = BridgePerformanceCases.Station(0, true, 1)[0];
        var results = Batch(new[] { input, input, input }, history, 4);
        var first = results[0].Analysis!; var other = results[1].Analysis!;
        string original = Snapshot(other);
        first.Geometry.Bars[0] = first.Geometry.Bars[0] with { Area = -123 };
        first.Stages[0].Points[0].Contributions[0] = -123;
        first.Stages[0].Contributions.Clear(); first.Stages[0].Points.Clear(); first.Stages.Clear();
        Assert.AreEqual(original, Snapshot(other)); Assert.AreEqual(original, Snapshot(results[2].Analysis!));
        Assert.IsTrue(input.TopRebars.Enabled);
    }

    [TestMethod]
    public void LinearHistoryCheckpointPreservesSubstepsActivationAndDifferentTailKinds()
    {
        var inputs = BridgePerformanceCases.Station(0, true, 3);
        inputs[1] = inputs[1] with { Phases = inputs[1].Phases.Select((p, i) => i == 3 ? p with { Kind = BridgePhaseKind.ConcreteExcluded } : p).ToArray() };
        inputs[2] = inputs[2] with { Phases = inputs[2].Phases.Append(new BridgePhase { Name = "R2", Kind = BridgePhaseKind.Shrinkage,
            ShrinkageMicrostrain = -100, Phi = 1 }).ToArray() };
        var options = new HBridgeHistoryOptions { SubstepsPerPhase = 3 };
        var actual = Batch(inputs, true, 3, options);
        for (int i = 0; i < inputs.Length; i++) {
            Assert.IsNull(actual[i].Error, actual[i].Error?.ToString());
            Assert.AreEqual(Snapshot(Single(inputs[i], true, options)), Snapshot(actual[i].Analysis!));
        }
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void EmptyInvalidAndCancelledBatchContracts(bool history)
    {
        Assert.AreEqual(0, Batch(Array.Empty<HBridgeInput>(), history, 4).Count);
        var inputs = BridgePerformanceCases.Station(0, false, 2);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Batch(inputs, history, 0));
        Assert.ThrowsException<OperationCanceledException>(() => Batch(inputs, history, 4, token: new CancellationToken(true)));
    }

    [TestMethod]
    public void NonlinearBatchIsExplicitlyOutsideTheNewContract() =>
        Assert.ThrowsException<NotSupportedException>(() => HBridgeHistoryResults.CalculateBatch(BridgePerformanceCases.Station(0, false, 2),
            new HBridgeHistoryOptions { MaterialMode = HistoryMaterialMode.Nonlinear }));
}
