using System.Reflection;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Checker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Compatibility;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Models;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using GPC.Model.Structure.Topology;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Independent steel columns checked by the section pipeline: one mid-height cut per case. Rows are ordered by
/// case, then column. Shared by the staleness tests of the concurrent section rows and the large report archive regression.</summary>
internal sealed class PipelineColumns
{
    internal GPC.Model.Models.Model Model = null!;
    internal MultiMaterialCheckRequest Request = null!;
    internal StandardEN1993p11 Code = null!;
    internal SteelSection Section = null!;
    internal List<StationResultBeamForces> Samples = new();
    private string[] _cases = null!;
    private int _jobs;
    private IMaterialChecker? _before;

    /// <param name="jobs">The cases are dealt to this many jobs, in turn.</param>
    /// <param name="before">Optional engine of a Shear task planned before the pipeline task of every cut (same prepared input).</param>
    internal static PipelineColumns Build(int columns, int cases, int jobs = 1, IMaterialChecker? before = null)
    {
        var model = new GPC.Model.Models.Model("Concurrent section rows");
        var section = new SteelSection(new SectionH(300, 12, 150, 18, 150, 18, "Stocky H"), SteelMaterialEN1993Data.S355);
        model.AddProperty(section);
        var group = model.AddGroup("Steel").Name; var all = new List<BeamElement>();
        for (int i = 0; i < columns; i++)
        {
            int id = 10 * (i + 1);
            model.NodesElements.Add(new NodeElement(new Point3d(i * 3000, 0, 0), id: id));
            model.NodesElements.Add(new NodeElement(new Point3d(i * 3000, 0, 1000), id: id + 1));
            var beam = new BeamElement(null, null, section, id: id);
            model.BeamElements.Add(beam); model.ConnectBeam(id, id, id + 1); all.Add(beam);
            beam.Assignments.Formulation = BeamFormulation.StraightTwoNode; beam.Assignments.SectionAxes = CoordinateSystem.Global;
            beam.Assignments.SectionGeometryAxes = CoordinateSystem.Global; beam.Assignments.StationDomain = "NodeToNode";
            beam.Assignments.ActionsAtSectionCentroidConfirmed = true;
        }
        model.AssignGroup(group, all.ToArray());
        var names = Enumerable.Range(0, cases).Select(k => "C" + k).ToArray();
        foreach (string name in names) model.LoadCases.Add(new LoadCaseBase(name));
        model.CaptureAnalysis(ReinforcementAnalysisRole.ExcludedFromAnalysis, "Assigned analytical actions.");
        string revision = model.AnalysisFingerprint();
        model.Datasets.Add("columns", new AnalysisDataset { Id = "columns", Program = "Assigned analytical fixture", ModelRevision = "1",
            InputFingerprint = revision, NormalizedUnits = "N,mm,rad", Semantics = AnalysisSemantics.LinearStatic, IsSynthetic = true });
        int column = 0;
        foreach (var beam in model.BeamElements.Values)
        {
            var point = new BeamReferenceGeometry(beam).PointAt(.5, "NodeToNode");
            // Actions repeat every 100 columns and 4 cases: every row is Satisfied, whatever the size.
            int k = column % 100;
            beam.AddResult(new BeamResult(names.Select((name, c) => new StationResultBeamForces(model.LoadCases[name], new ResultBeamForces(
                -(200000 + 1000.0 * k + 25000.0 * (c % 4)), 0, 0, 0, 20e6 + 1e4 * k + 5e6 * (c % 4), 3e6 + 1e5 * (c % 4),
                ResultTransformations.AtPoint(CoordinateSystem.Global, point)), .5) {
                StationDomain = "NodeToNode", PhysicalDistance = 500, Body = ActionBody.PositiveSectionFace,
                State = new ResultState { DatasetId = "columns", ModelRevision = "1", InputFingerprint = revision, ConcomitantStateId = name,
                    Components = Enumerable.Repeat(ComponentAvailability.Available, 6).ToArray(), IsCumulative = true, IsSynthetic = true,
                    Semantics = AnalysisSemantics.LinearStatic } }).ToArray()));
            column++;
        }
        var fixture = new PipelineColumns { _cases = names, _jobs = jobs, _before = before };
        fixture.Bind(model);
        return fixture;
    }

    /// <summary>The same columns on a copy of the model (same identities and analysis snapshot), with new engines.</summary>
    internal PipelineColumns Copy()
    {
        var copy = new PipelineColumns { _cases = _cases, _jobs = _jobs, _before = _before };
        copy.Bind(ModelArchive.Copy(Model));
        return copy;
    }

    private void Bind(GPC.Model.Models.Model model)
    {
        Model = model; Section = (SteelSection)model.BeamElements.Values.First().BeamProperty;
        Samples = model.BeamElements.Values.SelectMany(b => _cases.Select(c => ResultQueries.Samples<StationResultBeamForces>(b,
            new ResultSelection { Dataset = "columns", Case = c, ConcomitantState = c }).Single())).ToList();
        Code = new StandardEN1993p11();
        var pipeline = new SteelPipelineMaterialChecker(Code, "2005", parameters: new Dictionary<string, string> { ["Unperforated"] = "true" });
        Request = new MultiMaterialCheckRequest();
        for (int j = 0; j < _jobs; j++)
            Request.Jobs.Add(new MultiMaterialCheckJob { Name = "Columns " + j, Plan = new BeamCheckPlanRequest {
                Elements = new ElementSelection { Groups = new[] { "Steel" }, Families = new[] { EntityFamily.Beam } },
                Results = _cases.Where((_, c) => c % _jobs == j).Select(n => new ResultSelection { Dataset = "columns", Case = n, ConcomitantState = n }).ToArray(),
                SectionMechanisms = _before == null ? new[] { CheckMechanism.UlsBiaxialSection } : new[] { CheckMechanism.Shear, CheckMechanism.UlsBiaxialSection } },
                Assignments = _before == null ? new[] { new MaterialCheckerAssignment { Checker = pipeline } } : new[] {
                    new MaterialCheckerAssignment { Checker = _before, Mechanisms = new[] { CheckMechanism.Shear } },
                    new MaterialCheckerAssignment { Checker = pipeline, Mechanisms = new[] { CheckMechanism.UlsBiaxialSection } } } });
    }
}

/// <summary>The engine part of the steel pipeline section rows is computed ahead, block by block; whatever runs in the
/// sequential pass in between (a progress callback, another engine) can change the engine or the inputs a later row of the
/// block reads. Every report must be the one the sequential run gives: same rows and evidence, counters and currency.</summary>
[TestClass]
public class ConcurrentSectionStalenessTest
{
    private sealed class Progress : IProgress<VerificationProgress>
    {
        private readonly Action<VerificationProgress> _action;
        public Progress(Action<VerificationProgress> action) => _action = action;
        public void Report(VerificationProgress value) => _action(value);
    }

    /// <summary>A third-party engine that changes the prepared input it receives (shared by the tasks of its cut).</summary>
    private sealed class MutatingEngine : IMaterialChecker
    {
        private sealed class Session : IMaterialCheckSession
        {
            public int CreatedCheckers => 0;
            public CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token)
            {
                input.Forces.M1 *= 1.5;
                return new CheckResult { Execution = ExecutionStatus.Completed, Data = DataStatus.Ready, Outcome = EngineeringOutcome.Satisfied, Utilization = .1 };
            }
        }
        public string Id => "Fixture.Mutating";
        public string Version => "fixture-1";
        public string Configuration => "mutating";
        public CheckStandardContext Standard => new("Fixture", "1", null, Id, Configuration);
        public bool Accepts(BeamElement element) => true;
        public IMaterialCheckSession CreateSession() => new Session();
    }

    /// <summary>The sequential reference: every task in the sequential pass (internal switch of the service).</summary>
    internal static Service Sequential()
    {
        var service = new Service();
        typeof(Service).GetProperty("ConcurrentSectionRows", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, false);
        return service;
    }

    /// <summary>One line per row; the evidence seal is the value digest of the whole stored row (details, trace, input,
    /// diagnostics, provenance), independent of object identities and of the random GUIDs of geometry objects, which make
    /// the archive bytes of two separately prepared runs differ.</summary>
    private static string[] Rows(ModelCheckReport report) => report.Jobs.SelectMany(j => j.Results).Select(r => string.Join(";",
        r.PlanItemId, r.Execution, r.Data, r.Outcome, r.Utilization?.ToString("R"), r.Details == null ? "no details" : r.Details.Metrics.Count + " metrics, " + r.Details.Trace.Count + " trace",
        string.Join(",", r.Diagnostics.Select(d => d.Code)), r.HasUnchangedEvidence, r.EvidenceFingerprint)).ToArray();

    /// <summary>Runs the request twice, sequential then concurrent, each on its own copy of the same fixture with the same
    /// scripted changes keyed by completed rows, and requires identical reports.</summary>
    private static (ModelCheckReport Sequential, ModelCheckReport Concurrent, PipelineColumns Fixture) Compare(
        Func<PipelineColumns> build, Action<PipelineColumns, int> change)
    {
        var origin = build();
        ModelCheckReport Run(Service service, out PipelineColumns fixture)
        {
            var f = fixture = origin.Copy();
            return service.Verify(f.Model, f.Request, progress: new Progress(p => change(f, p.Completed)));
        }
        var sequential = Run(Sequential(), out var a); var concurrent = Run(new Service(), out var b);
        CollectionAssert.AreEqual(Rows(sequential), Rows(concurrent));
        Assert.AreEqual(sequential.Required, concurrent.Required); Assert.AreEqual(sequential.Executed, concurrent.Executed);
        Assert.AreEqual(sequential.CreatedCheckers, concurrent.CreatedCheckers); Assert.AreEqual(sequential.Outcome, concurrent.Outcome);
        Assert.AreEqual(sequential.Summary.Stale, concurrent.Summary.Stale);
        Assert.AreEqual(Service.CurrentOutcome(sequential, a.Model, a.Request), Service.CurrentOutcome(concurrent, b.Model, b.Request));
        CollectionAssert.AreEqual(sequential.Jobs.SelectMany(j => j.Results).Select(r => r.Provenance?.IsCurrent(a.Model)).ToArray(),
            concurrent.Jobs.SelectMany(j => j.Results).Select(r => r.Provenance?.IsCurrent(b.Model)).ToArray());
        return (sequential, concurrent, b);
    }

    private static void NoNumbersOnEmptyStaleRows(ModelCheckReport report)
    {
        foreach (var row in report.Jobs.SelectMany(j => j.Results).Where(r => r.Data == DataStatus.Stale && r.Execution == ExecutionStatus.NotExecuted))
        { Assert.IsNull(row.Utilization); Assert.IsNull(row.Details); Assert.AreEqual(EngineeringOutcome.NotEvaluated, row.Outcome); }
    }

    [TestMethod]
    public void WithoutChangesTheConcurrentRunIsTheSequentialRun()
    {
        var (sequential, _, fixture) = Compare(() => PipelineColumns.Build(30, 3), (_, _) => { });
        Assert.AreEqual(EngineeringOutcome.Satisfied, sequential.Outcome); Assert.AreEqual(90, sequential.Executed);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(sequential, fixture.Model, fixture.Request));
    }

    /// <summary>The reported case: the engine configuration changes after the third row. The later rows of the block were
    /// already computed with the old configuration; the sequential run computes them with the new one and leaves them
    /// empty and Stale (the engine is no longer the planned one), recording the new configuration.</summary>
    [TestMethod]
    public void EngineChangedInsideABlockLeavesTheLaterRowsEmptyAndStale()
    {
        var (sequential, concurrent, _) = Compare(() => PipelineColumns.Build(30, 3), (f, completed) => { if (completed == 3) f.Code.GammaM0 = 1.05; });
        var rows = concurrent.Jobs.SelectMany(j => j.Results).ToArray();
        Assert.IsTrue(rows.Take(3).All(r => r.Data == DataStatus.Stale && r.Utilization.HasValue && r.Diagnostics.Any(d => d.Code == "MaterialInputsChangedDuringRun")));
        Assert.IsTrue(rows.Skip(3).All(r => r.Data == DataStatus.Stale && r.Execution == ExecutionStatus.NotExecuted && r.Utilization == null));
        Assert.AreEqual(2, concurrent.CreatedCheckers); // the old and the new configuration, as in the sequential run
        NoNumbersOnEmptyStaleRows(sequential); NoNumbersOnEmptyStaleRows(concurrent);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, concurrent.Outcome);
    }

    /// <summary>Changed and restored inside the block: the run ends with the planned engine, so the end-of-run check passes.
    /// The rows reached while the engine differed are Stale in the sequential run and must not come back as current results.</summary>
    [TestMethod]
    public void EngineChangedAndRestoredInsideABlockKeepsTheRowsOfTheChangeStale()
    {
        var (_, concurrent, fixture) = Compare(() => PipelineColumns.Build(30, 3), (f, completed) => {
            if (completed == 3) f.Code.GammaM0 = 1.05; else if (completed == 6) f.Code.GammaM0 = 1.0; });
        var rows = concurrent.Jobs.SelectMany(j => j.Results).ToArray();
        Assert.IsTrue(rows.Skip(3).Take(3).All(r => r.Data == DataStatus.Stale && r.Utilization == null));
        Assert.IsTrue(rows.Take(3).Concat(rows.Skip(6)).All(r => r.Data == DataStatus.Ready && r.Outcome == EngineeringOutcome.Satisfied));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, concurrent.Outcome);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(concurrent, fixture.Model, fixture.Request));
    }

    /// <summary>Blocks of 256 tasks: changes on the last row of a block, on the first rows of the next one.</summary>
    [DataTestMethod]
    [DataRow(255)] [DataRow(256)] [DataRow(257)] [DataRow(300)]
    public void EngineChangedAroundTheBlockBoundary(int completed)
    {
        var (sequential, _, _) = Compare(() => PipelineColumns.Build(110, 3), (f, n) => { if (n == completed) f.Code.GammaM0 = 1.05; });
        Assert.AreEqual(330, sequential.Required);
        NoNumbersOnEmptyStaleRows(sequential);
    }

    [TestMethod]
    public void EngineChangedDuringTheFirstJob()
    {
        var (sequential, _, _) = Compare(() => PipelineColumns.Build(30, 4, jobs: 2), (f, completed) => { if (completed == 10) f.Code.GammaM0 = 1.05; });
        Assert.AreEqual(2, sequential.Jobs.Count);
        Assert.IsTrue(sequential.Jobs[1].Results.All(r => r.Data == DataStatus.Stale && r.Utilization == null));
    }

    /// <summary>Changes of the shared model (a later sample, the section property, a sample changed and restored, an
    /// assignment): the engines read owned copies, the end-of-run check marks the evaluated rows.</summary>
    [DataTestMethod]
    [DataRow("sample")] [DataRow("section")] [DataRow("restored sample")] [DataRow("assignment")]
    public void ModelChangedInsideABlock(string what)
    {
        var (sequential, _, _) = Compare(() => PipelineColumns.Build(30, 3), (f, completed) => {
            var sample = f.Samples[^1];
            if (completed == 3)
                switch (what)
                {
                    case "section": f.Section.SteelMaterial = SteelMaterialEN1993Data.S275; break;
                    case "assignment": f.Model.BeamElements.Values.Last().Assignments.ActionsAtSectionCentroidConfirmed = false; break;
                    default: sample.ResultBeamForces.M1 += 1; break;
                }
            else if (completed == 6 && what == "restored sample") sample.ResultBeamForces.M1 -= 1;
        });
        var rows = sequential.Jobs.SelectMany(j => j.Results).ToArray();
        if (what == "restored sample") Assert.AreEqual(EngineeringOutcome.Satisfied, sequential.Outcome);
        else if (what != "assignment") Assert.IsTrue(rows.All(r => r.Data == DataStatus.Stale && r.Outcome == EngineeringOutcome.NotEvaluated));
    }

    /// <summary>The engine of an earlier task of the same cut changes the prepared input the pipeline task reads next.</summary>
    [TestMethod]
    public void InputChangedByAnEarlierEngineOfTheSameCut()
    {
        var (sequential, _, _) = Compare(() => PipelineColumns.Build(20, 2, before: new MutatingEngine()), (_, _) => { });
        Assert.AreEqual(80, sequential.Required);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, sequential.Outcome);
    }
}
