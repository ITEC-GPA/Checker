using System.Text.Json;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesignTests;

[TestClass]
public sealed class EmbeddedWallEmbedmentTests
{
    public TestContext TestContext { get; set; } = null!;
    private static readonly string[] Stages = ["Initial", "Loaded", "Reversed"];
    private static EmbeddedWallModel Template(string id = "SLE", double force = 10000) => new()
    {
        CaseId = id, Top = 0, Bottom = -10000, TributaryWidth = 1000,
        Segments = [new() { Top = 0, Bottom = -10000, FlexuralRigidity = 2e14, SectionId = "S", Source = "Analytical benchmark" }],
        LeftSoils = [Layer()], RightSoils = [Layer()],
        Stages = [Stage("Initial", 0), Stage("Loaded", force), Stage("Reversed", -.5 * force)]
    };
    private static EmbeddedWallSoil Layer() => new()
    {
        Soil = new Soil("Sand", 18e-6, 20e-6, Math.PI / 6, 0, "Analytical benchmark"),
        Top = 0, Bottom = -20000, HorizontalModulus = .001, AtRestCoefficient = .5, StiffnessSource = "Assigned Winkler k = 2 N/mm2 for both faces"
    };
    private static EmbeddedWallStage Stage(string id, double h) => new()
    {
        Id = id, Left = new() { Elevation = 0, Surcharge = .1 }, Right = new() { Elevation = 0, Surcharge = .1 },
        PointLoads = [new() { Elevation = 0, HorizontalForce = h }]
    };
    private static WallAnalysisResult Analyze(double toe, string id = "SLE", double force = 10000, int elements = 48)
    {
        var m = EmbeddedWallAnalysis.WithToe(Template(id, force), toe); m.MaximumElementLength = -toe / elements;
        return EmbeddedWallAnalysis.Calculate(m, new() { InitialSubsteps = 2 });
    }
    private static EmbeddedWallEmbedmentSearch Plan(params string[] ids) => new()
    {
        ReferenceElevation = 0, WallTop = 0, MinimumEmbedment = 3500, MaximumEmbedment = 10000, Increment = 500,
        Source = "Finite constructible grid; assigned head-displacement criterion",
        Histories = (ids.Length == 0 ? new[] { "SLE" } : ids).Select(id => new EmbeddedWallSearchHistory
            { CaseId = id, StageIds = Stages.ToArray(), RequiredCheckIds = ["HeadDisplacement"] }).ToArray()
    };
    private static WallEmbedmentEvaluation<WallAnalysisResult> Evaluate(double toe, double limit = 3.25)
    {
        var result = Analyze(toe);
        return new(toe, [result], [EmbeddedWallEmbedment.HeadDisplacement(result, limit, "Assigned project limit")], result);
    }

    [TestMethod]
    public void FiniteWinklerBeamMatchesIndependentClosedSolutionAndRefines()
    {
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "embedment-winkler-expected.json")));
        foreach (var c in expected.RootElement.GetProperty("cases").EnumerateArray())
        {
            double length = c.GetProperty("length").GetDouble();
            var fine = Analyze(-length, elements: 96); Assert.IsTrue(fine.IsComplete, fine.Diagnostic);
            double head = fine.Stages[1].Points.First(p => p.Elevation == 0).Displacement;
            // 2e-6 mm is below one millionth of the displacement in these 14 elastic cases.
            Assert.AreEqual(c.GetProperty("head_displacement").GetDouble(), head, 2e-6, "Head L=" + length);
            Assert.IsTrue(fine.Stages.All(s => s.Points.All(p => !p.LeftPlastic && !p.RightPlastic)), "Benchmark must remain elastic");
            foreach (var stage in fine.Stages)
            {
                Assert.IsTrue(Math.Abs(stage.ForceResidual) < .01, "Force equilibrium N");
                Assert.IsTrue(Math.Abs(stage.MomentResidual) < 100, "Moment equilibrium Nmm");
            }
            if (length is not (3500 or 6500 or 10000)) continue;
            // Coarse meshes measure truncation convergence; 96 elements separately bound round-off.
            // At 12 elements the recovered actions of the shortest beam are already near the double-precision floor.
            var coarse = Analyze(-length, elements: 6); var medium = Analyze(-length, elements: 24);
            double[] fineError = new double[4], coarseError = new double[4], mediumError = new double[4];
            foreach (var p in c.GetProperty("points").EnumerateArray())
            {
                double depth = p.GetProperty("depth").GetDouble();
                double[] target = [p.GetProperty("u").GetDouble(), p.GetProperty("rotation").GetDouble(), p.GetProperty("moment").GetDouble(), p.GetProperty("shear").GetDouble()];
                foreach (var pair in new[] { (coarse, coarseError), (medium, mediumError), (fine, fineError) })
                {
                    var station = pair.Item1.Stages[1].Points.MinBy(x => Math.Abs(x.Elevation + depth))!;
                    Assert.AreEqual(-depth, station.Elevation, 1e-6);
                    double[] values = [station.Displacement, station.Rotation, station.Moment, station.Shear];
                    for (int i = 0; i < 4; i++) pair.Item2[i] = Math.Max(pair.Item2[i], Math.Abs(values[i] - target[i]));
                }
                var loaded = fine.Stages[1].Points.MinBy(x => Math.Abs(x.Elevation + depth))!;
                var reversed = fine.Stages[2].Points.MinBy(x => Math.Abs(x.Elevation + depth))!;
                Assert.AreEqual(-.5 * loaded.Displacement, reversed.Displacement, 2e-7);
            }
            TestContext.WriteLine($"L={length}; max errors [u,rotation,M,V] mesh6={string.Join(',', coarseError)}; mesh24={string.Join(',', mediumError)}; mesh96={string.Join(',', fineError)}");
            double[] tolerances = [2e-6, 2e-9, 5, .01];
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(fineError[i] < tolerances[i], $"L={length}, quantity={i}, error={fineError[i]:R}");
                Assert.IsTrue(mediumError[i] < coarseError[i] / 4, $"Mesh refinement L={length}, quantity={i}");
            }
        }
    }

    [TestMethod]
    public void ExhaustiveSearchFindsGridMinimumAndKeepsIndependentCompleteHistories()
    {
        var template = Template(); string before = JsonSerializer.Serialize(template);
        var result = EmbeddedWallEmbedment.Run(Plan(), (toe, _) => Evaluate(toe));
        Assert.IsTrue(result.Completed && result.MinimumCertified && !result.Cancelled, result.Diagnostic);
        Assert.AreEqual(14, result.Candidates.Count); Assert.AreEqual(6500, result.Best!.Embedment);
        Assert.AreEqual(6, result.Candidates.Count(c => c.Outcome == EngineeringOutcome.NotSatisfied));
        Assert.AreEqual(14, result.Candidates.Select(c => c.Evaluation!.Histories[0]).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.AreSame(result.Best.Evaluation!.Payload, result.Best.Evaluation.Histories[0]);
        Assert.IsTrue(result.Candidates.All(c => c.Evaluation!.Criteria[0].StageId == "Loaded"));
        Assert.AreEqual(before, JsonSerializer.Serialize(template));
        var second = EmbeddedWallEmbedment.Run(Plan(), (toe, _) => Evaluate(toe, 5));
        Assert.AreEqual(4500, second.Best!.Embedment); Assert.AreEqual(6500, result.Best.Embedment);
    }

    [TestMethod]
    public void TwoIndependentHistoriesMustBothSatisfyTheirOwnCriteria()
    {
        var result = EmbeddedWallEmbedment.Run(Plan("A", "B"), (toe, _) =>
        {
            var a = Analyze(toe, "A"); var b = Analyze(toe, "B", 15000);
            return new WallEmbedmentEvaluation<string>(toe, [a, b],
                [EmbeddedWallEmbedment.HeadDisplacement(a, 3.25, "Project A"), EmbeddedWallEmbedment.HeadDisplacement(b, 4.5, "Project B")], "Both complete");
        });
        Assert.IsTrue(result.MinimumCertified); Assert.AreEqual(7500, result.Best!.Embedment);
        Assert.IsTrue(result.Candidates.Single(c => c.Embedment == 7000).Evaluation!.Criteria.Any(c => c.CaseId == "B" && c.Outcome == EngineeringOutcome.NotSatisfied));
    }

    [TestMethod]
    public void ShallowerMechanismPreventsMinimumCertificationButDeeperMechanismDoesNot()
    {
        foreach (double unknown in new[] { 4000d, 9000d })
        {
            var result = EmbeddedWallEmbedment.Run(Plan(), (toe, _) =>
            {
                if (toe != -unknown) return Evaluate(toe);
                var model = EmbeddedWallAnalysis.WithToe(Template(), toe); model.LeftSoils = []; model.RightSoils = [];
                var mechanism = EmbeddedWallAnalysis.Calculate(model);
                Assert.AreEqual(WallAnalysisStatus.Unstable, mechanism.Status);
                return new(toe, [mechanism], [EmbeddedWallEmbedment.HeadDisplacement(mechanism, 3.25, "Project limit")], mechanism);
            });
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Candidates.Single(c => c.Embedment == unknown).Outcome);
            Assert.AreEqual(6500, result.Best!.Embedment); Assert.AreEqual(unknown > 6500, result.MinimumCertified);
        }
    }

    [TestMethod]
    public void MissingOrInconsistentEvidenceCannotPass()
    {
        var plan = Plan(); plan.MinimumEmbedment = plan.MaximumEmbedment = 6500;
        var correct = Evaluate(-6500);
        var invalid = new WallEmbedmentEvaluation<WallAnalysisResult>[]
        {
            correct with { ToeElevation = -6000 }, correct with { Histories = [] }, correct with { Criteria = [] },
            correct with { Histories = [Analyze(-6000)] }, correct with { Histories = [correct.Payload, correct.Payload] },
            correct with { Criteria = [correct.Criteria[0], correct.Criteria[0]] },
            correct with { Criteria = [correct.Criteria[0] with { Utilization = double.NaN }] },
            correct with { Criteria = [correct.Criteria[0] with { Utilization = 2 }] },
            correct with { Criteria = [correct.Criteria[0] with { StageId = "Unknown" }] },
            correct with { Criteria = [correct.Criteria[0] with { Method = "" }] }
        };
        foreach (var bad in invalid)
        {
            var search = EmbeddedWallEmbedment.Run(plan, (_, _) => bad);
            Assert.IsNull(search.Best); Assert.IsFalse(search.MinimumCertified); Assert.AreEqual(EngineeringOutcome.NotEvaluated, search.Candidates[0].Outcome);
        }
        plan.Histories[0].StageIds = ["Initial", "Reversed", "Loaded"];
        Assert.IsNull(EmbeddedWallEmbedment.Run(plan, (_, _) => correct).Best);
    }

    [TestMethod]
    public void NonMonotoneFeasibilityNeverUsesBisectionOrStopsAtFirstPass()
    {
        var plan = Plan(); plan.Histories[0].RequiredCheckIds = ["HeadDisplacement", "AssignedConstraint"];
        var result = EmbeddedWallEmbedment.Run(plan, (toe, _) =>
        {
            var value = Evaluate(toe, 100); bool passes = -toe is >= 4000 and <= 4500 or >= 7000 and <= 8000;
            return value with { Criteria = [value.Criteria[0], new("SLE", "AssignedConstraint", passes ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied,
                null, null, null, "", "Synthetic nonmonotone acceptance fixture", "Feasible in two disjoint windows")] };
        });
        Assert.AreEqual(14, result.Candidates.Count); Assert.AreEqual(4000, result.Best!.Embedment); Assert.IsTrue(result.MinimumCertified);
        Assert.AreEqual(5, result.Candidates.Count(c => c.Outcome == EngineeringOutcome.Satisfied));
    }

    private sealed class SyncProgress(Action<WallEmbedmentSearchProgress> update) : IProgress<WallEmbedmentSearchProgress>
    { public void Report(WallEmbedmentSearchProgress value) => update(value); }

    [TestMethod]
    public void CancellationKeepsOnlyCompletedCandidatesAndNeverCertifiesMinimum()
    {
        using var cancellation = new CancellationTokenSource();
        var result = EmbeddedWallEmbedment.Run(Plan(), (toe, _) => Evaluate(toe, 100), cancellation.Token,
            new SyncProgress(p => { if (p.Evaluated == 3) cancellation.Cancel(); }));
        Assert.AreEqual(3, result.Candidates.Count); Assert.IsTrue(result.Cancelled); Assert.IsFalse(result.Completed || result.MinimumCertified);
        Assert.IsNotNull(result.Best);
        var before = EmbeddedWallEmbedment.Run<WallAnalysisResult>(Plan(), (_, _) => throw new AssertFailedException("Cancelled evaluator invoked"), cancellation.Token);
        Assert.AreEqual(0, before.Candidates.Count);
    }

    [TestMethod]
    public void PlanIsFrozenBeforeFirstEvaluationAndNonIntegralUpperEndpointIsIncluded()
    {
        var plan = Plan(); var result = EmbeddedWallEmbedment.Run(plan, (toe, _) => Evaluate(toe), progress: new SyncProgress(p =>
        {
            plan.MaximumEmbedment = 20000; plan.Increment = 1; plan.Histories[0].StageIds = ["Changed"];
        }));
        Assert.AreEqual(14, result.Candidates.Count); Assert.AreEqual(6500, result.Best!.Embedment); Assert.IsTrue(result.MinimumCertified);
        plan = Plan(); plan.MaximumEmbedment = 4750;
        CollectionAssert.AreEqual(new[] { 3500d, 4000, 4500, 4750 }, EmbeddedWallEmbedment.Depths(plan).ToArray());
        foreach (Action<EmbeddedWallEmbedmentSearch> invalidate in new Action<EmbeddedWallEmbedmentSearch>[]
        {
            p => p.Increment = 0, p => p.MinimumEmbedment = -1, p => p.MaximumEmbedment = double.NaN,
            p => p.Increment = 1, p => p.Source = "", p => p.Histories = [],
            p => p.Histories = [p.Histories[0], p.Histories[0]], p => p.Histories[0].RequiredCheckIds = [],
            p => p.Histories[0].StageIds = ["A", "A"], p => { p.ReferenceElevation = 1e20; p.WallTop = 1e20; }
        })
        {
            var bad = Plan(); invalidate(bad); Assert.ThrowsException<ArgumentException>(() => EmbeddedWallEmbedment.Depths(bad));
        }
    }

    [TestMethod]
    public void FamilyAggregationRequiresEveryCheckAndKeepsGoverningProvenance()
    {
        var good = new WallEmbedmentCriterion("A", "N-M", EngineeringOutcome.Satisfied, 10, 20, .5, "kNm", "Method A", "Section 1", "F1", -5000);
        var unknown = good with { Outcome = EngineeringOutcome.NotEvaluated, Utilization = null, Diagnostic = "Unsupported section", StageId = "F2" };
        var failed = good with { Outcome = EngineeringOutcome.NotSatisfied, Demand = 30, Utilization = 1.5, StageId = "F3" };
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, EmbeddedWallEmbedment.Aggregate("A", "Steel", []).Outcome);
        var incomplete = EmbeddedWallEmbedment.Aggregate("A", "Steel", [good, unknown]);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, incomplete.Outcome); Assert.AreEqual("F2", incomplete.StageId);
        var rejected = EmbeddedWallEmbedment.Aggregate("A", "Steel", [good, unknown, failed]);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, rejected.Outcome); Assert.AreEqual("F3", rejected.StageId);
        Assert.AreEqual(30, rejected.Demand); Assert.AreEqual(20, rejected.Limit); Assert.AreEqual("kNm", rejected.Unit);
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallEmbedment.Aggregate("A", "Steel", [good with { CaseId = "B" }]));
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallEmbedment.Aggregate("A", "Steel", [good with { Utilization = 2 }]));
    }

    [TestMethod]
    public void ToeCutPreservesInputsAndRejectsExtrapolationDiscardedActionsAndDependentPressures()
    {
        var template = Template(); template.Segments = [new() { Top = 0, Bottom = -4000, FlexuralRigidity = 2e14, SectionId = "A", Source = "A" },
            new() { Top = -4000, Bottom = -10000, FlexuralRigidity = 1e14, SectionId = "B", Source = "B" }];
        string before = JsonSerializer.Serialize(template);
        var cut = EmbeddedWallAnalysis.WithToe(template, -6500);
        Assert.AreEqual(2, cut.Segments.Count); Assert.AreEqual(-6500, cut.Segments[1].Bottom); Assert.AreEqual(-20000, cut.LeftSoils[0].Bottom);
        cut.Stages[0].Left.Surcharge = 5; cut.LeftSoils[0].HorizontalModulus = 10; cut.Segments[0].FlexuralRigidity = 1;
        Assert.AreEqual(before, JsonSerializer.Serialize(template));
        Assert.AreEqual(1, EmbeddedWallAnalysis.WithToe(template, -3500).Segments.Count);
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.WithToe(template, -10001));
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.WithToe(template, 0));
        template.FixBottomRotation = true;
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.WithToe(template, -6500));
        template = Template(); template.Stages[1].PointLoads[0].Elevation = -8000;
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.WithToe(template, -6500));
        template = Template(); template.Supports = [new() { Id = "S", Elevation = -8000, AxialStiffness = 1000 }];
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.WithToe(template, -6500));
        template = Template(); template.Stages[0].Left.PorePressures = [new(0, 0), new(-10000, .05)]; template.Stages[0].Left.PorePressureSource = "Assigned independently of geometry";
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.WithToe(template, -6500));
        Assert.AreEqual(.05, EmbeddedWallAnalysis.WithToe(template, -6500, true).Stages[0].Left.PorePressures[1].Pressure);
    }
}
