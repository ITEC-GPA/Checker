using System.Diagnostics;
using System.Reflection;
using GPC.Examples;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Checker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Compatibility;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Data.Concrete;
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
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using GPC.Model.Structure.Topology;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Independent RC columns (300x500, 4Ø16, one section shared by all), one mid-height cut per case, checked through
/// the concrete path of ModelChecker.Verify: a ModelCheckJob with a Preparation (rows by column, then case) or with a BeamPlan
/// (tasks by case, then column). The cases are dealt to the jobs in turn.</summary>
internal sealed class ConcreteColumns
{
    internal GPC.Model.Models.Model Model = null!;
    internal ModelCheckRequest Request = null!;
    internal List<StationResultBeamForces> Samples = new();
    internal ReinforcedConcreteSection Section = null!;
    private string[] _cases = null!;
    private int _jobs;
    private bool _plan;
    private CheckMechanism[] _mechanisms = null!;

    /// <param name="shear">Also a Shear row (or task) after the UlsBiaxialSection one of every cut: NotSupported by the adapter, its inputs
    /// are still checked.</param>
    internal static ConcreteColumns Build(int columns, int cases, int jobs = 1, bool plan = false, bool shear = false)
    {
        var model = new GPC.Model.Models.Model("Concrete columns");
        var section = new ReinforcedConcreteSection(new SectionRectangular(500, 300, "RC 300x500"), ConcreteMaterialEN1992Data.C25_30);
        var bar = new RebarSectionCircular(16, SteelMaterialEN1992Data.B450C);
        section.AddRebars(new[] { new ReinforcedConcreteRebar(bar, new Point2d(50, 50)), new ReinforcedConcreteRebar(bar, new Point2d(250, 50)),
            new ReinforcedConcreteRebar(bar, new Point2d(50, 450)), new ReinforcedConcreteRebar(bar, new Point2d(250, 450)) });
        model.AddProperty(section);
        for (int i = 0; i < columns; i++)
        {
            int id = 10 * (i + 1);
            var bottom = new Point3d(i * 3000, 0, 0); var top = new Point3d(i * 3000, 0, 2000);
            model.NodesElements.Add(new NodeElement(bottom, id: id));
            model.NodesElements.Add(new NodeElement(top, id: id + 1));
            var beam = new BeamElement(null, null, section, id: id);
            model.BeamElements.Add(beam); model.ConnectBeam(id, id, id + 1);
            beam.Assignments.Formulation = BeamFormulation.StraightTwoNode;
            beam.Assignments.SectionAxes = Axes.Beam(bottom, top, new Vector3d(1, 0, 0));
            beam.Assignments.ActionsAtSectionCentroidConfirmed = true; beam.Assignments.StationDomain = "NodeToNode";
            beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Section = section });
        }
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
            // Moderate actions, repeating every 50 columns and 4 cases: the ratios differ from row to row and every row is Satisfied.
            int k = column % 50;
            beam.AddResult(new BeamResult(names.Select((name, c) => new StationResultBeamForces(model.LoadCases[name], new ResultBeamForces(
                -(300000 + 2000.0 * k + 50000.0 * (c % 4)), 0, 1000, 0, 20e6 + 2e5 * k + 5e6 * (c % 4), 2e6 + 1e5 * (c % 4),
                ResultTransformations.AtPoint(beam.Assignments.SectionAxes, point)), .5) {
                StationDomain = "NodeToNode", PhysicalDistance = 1000, Body = ActionBody.PositiveSectionFace,
                State = new ResultState { DatasetId = "columns", ModelRevision = "1", InputFingerprint = revision, ConcomitantStateId = name,
                    Components = Enumerable.Repeat(ComponentAvailability.Available, 6).ToArray(), IsCumulative = true, IsSynthetic = true,
                    Semantics = AnalysisSemantics.LinearStatic } }).ToArray()));
            column++;
        }
        var fixture = new ConcreteColumns { _cases = names, _jobs = jobs, _plan = plan,
            _mechanisms = shear ? new[] { CheckMechanism.UlsBiaxialSection, CheckMechanism.Shear } : new[] { CheckMechanism.UlsBiaxialSection } };
        fixture.Bind(model);
        return fixture;
    }

    /// <summary>The same columns on a copy of the model (same identities and analysis snapshot).</summary>
    internal ConcreteColumns Copy()
    {
        var copy = new ConcreteColumns { _cases = _cases, _jobs = _jobs, _plan = _plan, _mechanisms = _mechanisms };
        copy.Bind(ModelArchive.Copy(Model));
        return copy;
    }

    private void Bind(GPC.Model.Models.Model model)
    {
        Model = model; Section = model.BeamElements.Values.First().Assignments.Sections[0].Section;
        Samples = model.BeamElements.Values.SelectMany(b => b.Results.SelectMany(r => r.Results).OfType<StationResultBeamForces>()).ToList();
        Request = new ModelCheckRequest();
        for (int j = 0; j < _jobs; j++)
        {
            var results = _cases.Where((_, c) => c % _jobs == j).Select(n => new ResultSelection { Dataset = "columns", Case = n, ConcomitantState = n }).ToArray();
            var selection = new ElementSelection { Families = new[] { EntityFamily.Beam } };
            Request.Jobs.Add(_plan
                ? new ModelCheckJob { Name = "Columns " + j, Options = Options(), BeamPlan = new BeamCheckPlanRequest { Elements = selection, Results = results, SectionMechanisms = _mechanisms, Settings = "concrete columns" } }
                : new ModelCheckJob { Name = "Columns " + j, Options = Options(), Preparation = new PreparationRequest { Selection = selection, Results = results, Mechanisms = _mechanisms, Settings = "concrete columns" } });
        }
    }

    internal static ConcreteVerificationOptions Options() => new() { Standard = new StandardNTC2018Concrete(),
        Criterion = GPC.Checkers.Concrete.SectionSolvers.SectionSolver.FailureAnalysisTypes.ConstantEccentricity };
}

/// <summary>The concrete rows of a job read the whole model once, not twice per row plus a provenance capture (800 rows on 200 beams:
/// 156 s before, 2 s after): the reads are shared until code that may change the model runs (GPCModel SectionCheckRun), that is a call of
/// a verifier that may read more than its input and a progress report run on the calling thread. Every scripted change of the model
/// during a run gives the report of the reference that reads the model again in every check (ModelChecker.SharedSectionReads = false):
/// same rows, diagnostics, outcomes, utilizations and evidence.</summary>
[TestClass]
public class ConcreteRunScalingTest
{
    private sealed class Progress : IProgress<VerificationProgress>
    {
        private readonly Action<VerificationProgress> _action;
        public Progress(Action<VerificationProgress> action) => _action = action;
        public void Report(VerificationProgress value) => _action(value);
    }

    /// <summary>A verifier of another assembly with scripted changes of the model: during its n-th check, or in the first read of its
    /// Version after the n-th check (the stamping of the evaluated row, after the check of its inputs).</summary>
    private sealed class ScriptedVerifier : IConfiguredSectionVerifier
    {
        private bool _afterCheck;
        internal int Calls;
        internal Action<int>? During, AfterCheck;
        public string Version { get { if (_afterCheck) { _afterCheck = false; AfterCheck?.Invoke(Calls); } return "scripted-1"; } }
        public string Configuration => "scripted";
        public IReadOnlyCollection<CheckMechanism> Capabilities { get; } = new[] { CheckMechanism.UlsBiaxialSection };
        public CheckResult Verify(BeamCheckInput input, CheckMechanism mechanism, CancellationToken token)
        {
            Calls++; During?.Invoke(Calls); _afterCheck = true;
            return new CheckResult { Data = DataStatus.Ready, Execution = ExecutionStatus.Completed, Outcome = EngineeringOutcome.Satisfied, Utilization = .5 };
        }
    }

    /// <summary>The built-in numerical engine registered as an engine of the caller, with a scripted change of the model in its n-th solution.</summary>
    private sealed class RegisteredEngine : IConcreteCalculationFactory, GPC.Checkers.Concrete.Analysis.ISectionResponseSolver, GPC.Checkers.Concrete.Analysis.ISectionResistanceSolver
    {
        private readonly LegacyConcreteCalculationFactory _legacy = new();
        private ConcreteCalculationSession _session = null!;
        internal int Solutions;
        internal Action<int>? During;
        // The identity of the built-in engine, which the adapter requires in the diagnostics of every solution: only the type differs.
        public string Id => _legacy.Id;
        public string Version => _legacy.Version;
        public string Configuration => _legacy.Configuration;
        public ConcreteCalculationSession Create(ReinforcedConcreteSection section, StandardModelCode2010 standard, ConcreteCalculationOptions options)
        { _session = _legacy.Create(section, standard, options); return new ConcreteCalculationSession(this, this); }
        public GPC.Checkers.Concrete.Analysis.SectionResponse Solve(GPC.Checkers.Concrete.Analysis.SectionAnalysisInput input, CancellationToken token)
            => _session.Response.Solve(input, token);
        public GPC.Checkers.Concrete.Analysis.SectionResistanceResponse SolveResistance(GPC.Checkers.Concrete.Analysis.SectionAnalysisInput input, CancellationToken token)
        { Solutions++; During?.Invoke(Solutions); return _session.Resistance.SolveResistance(input, token); }
    }

    private static Service Reference(Service service)
    {
        typeof(Service).GetProperty("SharedSectionReads", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, false);
        return service;
    }

    /// <summary>One line per row; the evidence seal is the value digest of the whole stored row (details, input, diagnostics, provenance).</summary>
    private static string[] Rows(ModelCheckReport report) => report.Jobs.SelectMany(j => j.Results).Select(r => string.Join(";",
        r.Job, r.ElementId, r.Case, r.Execution, r.Data, r.Outcome, r.Utilization?.ToString("R"), string.Join(",", r.Diagnostics.Select(d => d.Code)),
        r.HasUnchangedEvidence, r.EvidenceFingerprint)).ToArray();

    /// <summary>Runs the request on two copies of the same fixture, with the reference reads and with the shared ones, with the same
    /// scripted changes (keyed by the completed rows, or made by the engines of the script), and requires identical reports.</summary>
    private static ModelCheckReport SameAsReference(ConcreteColumns origin, Func<ConcreteColumns, Service> service, Action<ConcreteColumns, int>? change = null)
    {
        ModelCheckReport Run(bool shared)
        {
            var f = origin.Copy(); var checker = service(f);
            if (!shared) Reference(checker);
            return checker.Verify(f.Model, f.Request, progress: change == null ? null : new Progress(p => change(f, p.Completed)));
        }
        var reference = Run(false); var report = Run(true);
        CollectionAssert.AreEqual(Rows(reference), Rows(report));
        Assert.AreEqual(reference.Outcome, report.Outcome); Assert.AreEqual(reference.CreatedCheckers, report.CreatedCheckers);
        return report;
    }

    /// <summary>The digests of the whole model computed during <paramref name="call"/>: analysis inputs, compatibility gate, compatibility
    /// of the known analysis (once per currency check of an input), verification inputs of the settings, design.</summary>
    private static int[] WholeModelReads(Action call)
    {
        var keys = ReportCurrencyTest.Computed(call);
        return new[] { keys.Count(k => k == "analysis"), keys.Count(k => k == "analysis-compatibility"), keys.Count(k => k == "known-analysis"),
            keys.Count(k => k.StartsWith("verification:", StringComparison.Ordinal)), keys.Count(k => k == "design-fingerprint") };
    }

    [TestMethod]
    public void TheWholeModelIsReadAFixedNumberOfTimesPerRunWhateverTheRows()
    {
        foreach (bool plan in new[] { false, true })
        {
            int[] Reads(int cases, bool shared)
            {
                var f = ConcreteColumns.Build(10, cases, plan: plan); ModelCheckReport report = null!;
                var service = shared ? new Service() : Reference(new Service());
                var reads = WholeModelReads(() => report = service.Verify(f.Model, f.Request));
                Assert.AreEqual(10 * cases, report.Required); Assert.AreEqual(10 * cases, report.Executed);
                Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
                Console.WriteLine($"{(plan ? "plan" : "preparation")}, {10 * cases} rows, {(shared ? "shared" : "reference")}: {string.Join(" ", reads)}");
                return reads;
            }
            // Shared: the same reads for 10 and 80 rows (gate, preparation, the rows, the final provenance check).
            CollectionAssert.AreEqual(Reads(1, true), Reads(8, true));
            // Reference (as before): the analysis, compatibility and verification digests twice per row (two currency checks, each in a
            // new read scope) and the design once per row (the capture of the provenance of the row).
            var few = Reads(1, false); var many = Reads(8, false);
            CollectionAssert.AreEqual(new[] { 2 * 70, 2 * 70, 2 * 70, 2 * 70, 70 }, many.Zip(few, (m, f) => m - f).ToArray());
        }
    }

    [TestMethod]
    public void ProgressReportsOnTheCallingThreadAndOtherEnginesAreFollowedByANewRead()
    {
        int KnownAnalysis(int cases, Func<Service> service, IProgress<VerificationProgress>? progress)
        {
            var f = ConcreteColumns.Build(10, cases); ModelCheckReport report = null!;
            var reads = WholeModelReads(() => report = service().Verify(f.Model, f.Request, progress: progress));
            Assert.AreEqual(10 * cases, report.Executed);
            return reads[2];
        }
        // System.Progress posts its handler to its synchronization context: the run reads the model once.
        Assert.AreEqual(KnownAnalysis(1, () => new Service(), new Progress<VerificationProgress>(_ => { })),
            KnownAnalysis(8, () => new Service(), new Progress<VerificationProgress>(_ => { })));
        // A report run on the calling thread may change the model: the next row reads it again (one compatibility read per row).
        Assert.AreEqual(70, KnownAnalysis(8, () => new Service(), new Progress(_ => { })) - KnownAnalysis(1, () => new Service(), new Progress(_ => { })));
        // A verifier of another assembly, or the built-in adapter with a registered numerical engine: the inputs of every row are read
        // again before and after its call, as before.
        Assert.AreEqual(2 * 70, KnownAnalysis(8, () => new Service(_ => new ScriptedVerifier()), null) - KnownAnalysis(1, () => new Service(_ => new ScriptedVerifier()), null));
        Assert.AreEqual(2 * 70, KnownAnalysis(8, () => new Service(j => j.Options.CreateVerifier(new RegisteredEngine())), null)
            - KnownAnalysis(1, () => new Service(j => j.Options.CreateVerifier(new RegisteredEngine())), null));
    }

    [TestMethod]
    public void AChangedSampleMakesExactlyItsRowsStale()
    {
        // 10 columns x 2 cases, a UlsBiaxialSection and a Shear row per cut (rows by column, case, mechanism). After the first row of cut 3
        // (row 6) its sample changes, which the reads of that row hold, and so does the sample of cut 7: rows 7, 14 and 15 are stale.
        var report = SameAsReference(ConcreteColumns.Build(10, 2, shear: true), _ => new Service(), (f, completed) => {
            if (completed == 7) { f.Samples[3].ResultBeamForces.M1 += 1e6; f.Samples[7].ResultBeamForces.M1 += 1e6; } });
        var rows = report.Jobs[0].Results;
        Assert.AreEqual(40, rows.Count);
        for (int i = 0; i < rows.Count; i++)
            if (i == 7 || i == 14 || i == 15)
            {
                Assert.AreEqual(DataStatus.Stale, rows[i].Data, i.ToString()); Assert.AreEqual(ExecutionStatus.NotExecuted, rows[i].Execution);
                Assert.IsNull(rows[i].Utilization); CollectionAssert.AreEqual(new[] { "StalePreparation" }, rows[i].Diagnostics.Select(d => d.Code).ToArray());
            }
            else if (i % 2 == 0) { Assert.AreEqual(EngineeringOutcome.Satisfied, rows[i].Outcome, i.ToString()); Assert.AreEqual(ExecutionStatus.Completed, rows[i].Execution); }
            else { Assert.AreEqual(DataStatus.NotSupported, rows[i].Data, i.ToString()); Assert.AreEqual("UnsupportedMechanism", rows[i].Diagnostics.Single().Code); }
        Assert.AreEqual(19, report.Executed);
    }
    [TestMethod]
    public void AChangedAnalysisInputMakesTheFollowingRowsStaleAndTheRunHistorical()
    {
        // After row 4 a node of the analysis model moves: rows 5-19 are not executed, rows 0-4 keep their ratio, every row is stale.
        foreach (bool plan in new[] { false, true })
        {
            var report = SameAsReference(ConcreteColumns.Build(10, 2, plan: plan), _ => new Service(), (f, completed) => { if (completed == 5) f.Model.NodesElements[11].Position.Z += 1; });
            var rows = report.Jobs[0].Results;
            Assert.IsTrue(rows.All(r => r.Data == DataStatus.Stale && r.Outcome == EngineeringOutcome.NotEvaluated && r.Diagnostics.Any(d => d.Code == "ScenarioChangedDuringVerification")));
            Assert.IsTrue(rows.Take(5).All(r => r.Execution == ExecutionStatus.Completed && r.Utilization > 0));
            Assert.IsTrue(rows.Skip(5).All(r => r.Execution == ExecutionStatus.NotExecuted && r.Utilization == null && r.Diagnostics.Any(d => d.Code == "StalePreparation")));
        }
    }

    [TestMethod]
    public void AChangeUndoneLaterMakesStaleExactlyTheRowsCheckedMeanwhile()
    {
        // A bar of the section shared by all columns moves after row 3 and back after row 8: rows 4-8 are stale, the others evaluated
        // and the run is current. Each check reads the model as it is when it runs.
        foreach (bool plan in new[] { false, true })
        {
            var report = SameAsReference(ConcreteColumns.Build(10, 2, plan: plan), _ => new Service(), (f, completed) => {
                if (completed == 4) f.Section.Rebars.First().Position.Y += 2;
                if (completed == 9) f.Section.Rebars.First().Position.Y -= 2; });
            var rows = report.Jobs[0].Results;
            CollectionAssert.AreEqual(Enumerable.Range(0, 20).Select(i => i >= 4 && i <= 8 ? DataStatus.Stale : DataStatus.Ready).ToArray(), rows.Select(r => r.Data).ToArray());
            Assert.IsTrue(rows.Where(r => r.Data == DataStatus.Stale).All(r => r.Diagnostics.Any(d => d.Code == "StalePreparation")));
            Assert.IsFalse(rows.SelectMany(r => r.Diagnostics).Any(d => d.Code == "ScenarioChangedDuringVerification"));
        }
    }

    [TestMethod]
    public void ChangesMadeByOtherEnginesAndFactoriesAreSeenWhereTheReferenceSeesThem()
    {
        // A verifier of another assembly changes the sample of its own row (the 3rd) and of row 5 during its 3rd check, and moves a node of
        // the analysis model in the stamping that follows its 8th check (row 8), after the currency check of that row.
        var report = SameAsReference(ConcreteColumns.Build(10, 2), f => new Service(_ => new ScriptedVerifier {
            During = n => { if (n == 3) { f.Samples[2].ResultBeamForces.M1 += 1e6; f.Samples[5].ResultBeamForces.M1 += 1e6; } },
            AfterCheck = n => { if (n == 8) f.Model.NodesElements[11].Position.Z += 1; } }));
        var rows = report.Jobs[0].Results;
        Assert.IsTrue(rows.All(r => r.Data == DataStatus.Stale && r.Diagnostics.Last().Code == "ScenarioChangedDuringVerification"));
        Assert.AreEqual("InputsChangedDuringVerification", rows[2].Diagnostics[0].Code); Assert.AreEqual("StalePreparation", rows[5].Diagnostics[0].Code);
        CollectionAssert.AreEqual(new[] { 0, 1, 3, 4, 6, 7, 8 }, Enumerable.Range(0, 20).Where(i => rows[i].Utilization == .5).ToArray());
        Assert.IsTrue(rows.Skip(9).All(r => r.Diagnostics[0].Code == "StalePreparation"));

        // The built-in adapter with a registered numerical engine (code of the caller): its 4th solution (row 3) changes the sample of its
        // own row, which the reads of that row hold; its 7th (row 6) moves a node.
        report = SameAsReference(ConcreteColumns.Build(10, 2), f => new Service(j => j.Options.CreateVerifier(new RegisteredEngine {
            During = n => { if (n == 4) f.Samples[3].ResultBeamForces.M1 += 1e6; if (n == 7) f.Model.NodesElements[11].Position.Z += 1; } })));
        rows = report.Jobs[0].Results;
        Assert.AreEqual("InputsChangedDuringVerification", rows[3].Diagnostics[0].Code); Assert.AreEqual("InputsChangedDuringVerification", rows[6].Diagnostics[0].Code);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 4, 5 }, Enumerable.Range(0, 20).Where(i => rows[i].Execution == ExecutionStatus.Completed).ToArray());
        Assert.IsTrue(rows.Skip(7).All(r => r.Diagnostics[0].Code == "StalePreparation"));
        // The factory of the second job changes a sample that only the second job reads (column 0, case C1).
        report = SameAsReference(ConcreteColumns.Build(10, 2, jobs: 2), f => new Service(job => {
            if (job.Name == "Columns 1") f.Samples[1].ResultBeamForces.M1 += 1e6;
            return job.Options.CreateVerifier(); }));
        Assert.IsTrue(report.Jobs[0].Results.All(r => r.Outcome == EngineeringOutcome.Satisfied));
        Assert.AreEqual("StalePreparation", report.Jobs[1].Results[0].Diagnostics.Single().Code);
        Assert.IsTrue(report.Jobs[1].Results.Skip(1).All(r => r.Outcome == EngineeringOutcome.Satisfied));
    }

    [TestMethod]
    public void AChangedShellSampleIsSeenByItsRowInAMixedJob()
    {
        var origin = MixedModelFactory.Create();
        var request = new ModelCheckRequest();
        request.Jobs.Add(new ModelCheckJob { Name = "Mixed", Options = ConcreteColumns.Options(), Preparation = new PreparationRequest {
            Selection = new ElementSelection { Families = new[] { EntityFamily.Beam, EntityFamily.Shell } },
            Results = new[] { new ResultSelection { Dataset = "synthetic-static", Case = "P+" } }, Settings = "mixed" } });
        ModelCheckReport Run(bool shared)
        {
            var model = ModelArchive.Copy(origin); var service = shared ? new Service() : Reference(new Service());
            // After the five beam rows and the row of shell 1090 the shell 1130 changes: its row is stale, the others are not.
            return service.Verify(model, request, progress: new Progress(p => {
                if (p.Completed == 6) model.AreaElements[1130].Results.SelectMany(r => r.Results).OfType<PointResultPlateForces>().First().Forces.Mxx += 1000; }));
        }
        var reference = Run(false); var report = Run(true);
        CollectionAssert.AreEqual(Rows(reference), Rows(report));
        var shells = report.Jobs[0].Results.Where(r => r.Family == EntityFamily.Shell).ToArray();
        CollectionAssert.AreEqual(new[] { 1090, 1130 }, shells.Select(r => r.ElementId).ToArray());
        CollectionAssert.AreEqual(new[] { DataStatus.NotSupported, DataStatus.Stale }, shells.Select(r => r.Data).ToArray());
        Assert.IsTrue(shells[1].Diagnostics.Any(d => d.Code == "StaleShellPreparation"));
        Assert.AreEqual(5, report.Executed);
    }

    /// <summary>An engine of another assembly for the Shear tasks that moves a bar of the section shared by all columns during its
    /// n-th task and moves it back during its m-th.</summary>
    private sealed class BarMovingEngine : IMaterialChecker
    {
        private readonly ReinforcedConcreteSection _section;
        private readonly int _move, _back;
        private int _calls;
        internal BarMovingEngine(ReinforcedConcreteSection section, int move, int back) { _section = section; _move = move; _back = back; }
        private sealed class Session : IMaterialCheckSession
        {
            private readonly BarMovingEngine _owner;
            internal Session(BarMovingEngine owner) => _owner = owner;
            public int CreatedCheckers => 0;
            public CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token)
            {
                int n = ++_owner._calls;
                if (n == _owner._move) _owner._section.Rebars.First().Position.Y += 2;
                if (n == _owner._back) _owner._section.Rebars.First().Position.Y -= 2;
                return new CheckResult { Execution = ExecutionStatus.Completed, Data = DataStatus.Ready, Outcome = EngineeringOutcome.Satisfied, Utilization = .1 };
            }
        }
        public string Id => "Fixture.BarMoving";
        public string Version => "fixture-1";
        public string Configuration => "moves a bar";
        public CheckStandardContext Standard => new("Fixture", "1", null, Id, Configuration);
        public bool Accepts(BeamElement element) => true;
        public IMaterialCheckSession CreateSession() => new Session(this);
    }

    /// <summary>Material runs (MultiMaterialCheckRequest) on two copies of the same fixture, reference and shared reads, same scripted changes.</summary>
    private static ModelCheckReport SameAsReference(ConcreteColumns origin, Func<ConcreteColumns, MultiMaterialCheckRequest> request, Action<ConcreteColumns, int>? change = null)
    {
        ModelCheckReport Run(bool shared)
        {
            var f = origin.Copy(); var checker = shared ? new Service() : Reference(new Service());
            return checker.Verify(f.Model, request(f), progress: change == null ? null : new Progress(p => change(f, p.Completed)));
        }
        var reference = Run(false); var report = Run(true);
        CollectionAssert.AreEqual(Rows(reference), Rows(report));
        Assert.AreEqual(reference.Outcome, report.Outcome); Assert.AreEqual(reference.CreatedCheckers, report.CreatedCheckers);
        return report;
    }

    /// <summary>The columns as one configured job of the concrete adapter (VerificationConfiguration, built-in EngineCatalog).</summary>
    private static GPC.Model.Checker.Configuration.VerificationConfiguration Configured(ConcreteColumns f) => new() {
        Contexts = new[] { new GPC.Model.Checker.Configuration.DesignContextDefinition { Id = "rc", Edition = "2018", Code = ConcreteColumns.Options().Standard } },
        Engines = new[] { new GPC.Model.Checker.Configuration.EngineDefinition { Id = "rc", Kind = "Concrete.Section", ContextId = "rc", Concrete = ConcreteColumns.Options() } },
        Jobs = new[] { new GPC.Model.Checker.Configuration.ConfiguredCheckJob { Name = "Concrete", Plan = MaterialRequest(f).Jobs[0].Plan,
            Routes = new[] { new GPC.Model.Checker.Configuration.CheckRouteDefinition { EngineId = "rc" } } } } };

    [TestMethod]
    public void TheConcreteTasksOfMaterialAndConfiguredRunsReadTheWholeModelAFixedNumberOfTimes()
    {
        foreach (bool configured in new[] { false, true })
        {
            int[] Reads(int cases, bool shared)
            {
                var f = ConcreteColumns.Build(10, cases); ModelCheckReport report = null!;
                var service = shared ? new Service() : Reference(new Service());
                var reads = WholeModelReads(() => report = configured ? service.Verify(f.Model, Configured(f), GPC.Model.Checker.Configuration.EngineCatalog.BuiltIn())
                    : service.Verify(f.Model, MaterialRequest(f)));
                Assert.AreEqual(10 * cases, report.Executed); Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
                Console.WriteLine($"{(configured ? "configured" : "material")}, {10 * cases} rows, {(shared ? "shared" : "reference")}: {string.Join(" ", reads)}");
                return reads;
            }
            CollectionAssert.AreEqual(Reads(1, true), Reads(8, true));
            // Reference (as before): every task prepares its cut and checks it in new read scopes.
            var few = Reads(1, false); var many = Reads(8, false);
            Assert.IsTrue(many.Zip(few, (m, f) => m - f).All(d => d >= 70), string.Join(" ", many.Zip(few, (m, f) => m - f)));
        }
    }

    [TestMethod]
    public void ChangesDuringAMaterialRunAreSeenWhereTheReferenceSeesThem()
    {
        // A synchronous progress report moves a node after task 4: the following concrete tasks are stale, the run historical.
        var report = SameAsReference(ConcreteColumns.Build(10, 2), MaterialRequest, (f, completed) => { if (completed == 5) f.Model.NodesElements[11].Position.Z += 1; });
        var rows = report.Jobs[0].Results;
        // Every task prepares its cut: after the move the preparation finds that the analysis must be run again (RequiresReanalysis), nothing is executed.
        Assert.IsTrue(rows.Take(5).All(r => r.Execution == ExecutionStatus.Completed) && rows.Skip(5).All(r => r.Execution == ExecutionStatus.NotExecuted && r.Diagnostics.Any(d => d.Code == "RequiresReanalysis")),
            string.Join(" | ", rows.Select(r => r.Execution + ":" + string.Join(",", r.Diagnostics.Select(d => d.Code)))));
        Assert.IsTrue(rows.All(r => r.Data == DataStatus.Stale && r.Diagnostics.Any(d => d.Code == "ScenarioChangedDuringVerification")));

        // A bar of the shared section moved by a progress report after task 3 and back after task 8. Every concrete task prepares its cut
        // from the model as it is: tasks 4-8 check the moved bar, the others the original one, and the run is current.
        report = SameAsReference(ConcreteColumns.Build(10, 2), MaterialRequest, (f, completed) => {
            if (completed == 4) f.Section.Rebars.First().Position.Y += 2;
            if (completed == 9) f.Section.Rebars.First().Position.Y -= 2; });
        rows = report.Jobs[0].Results;
        Assert.IsTrue(rows.All(r => r.Data == DataStatus.Ready && r.Outcome == EngineeringOutcome.Satisfied));
        CollectionAssert.AreEqual(Enumerable.Range(0, 20).Select(i => i >= 4 && i <= 8).ToArray(), rows.Select(r => r.Input!.SectionFingerprint != rows[0].Input!.SectionFingerprint).ToArray());

        // An engine of another assembly checks the Shear task before the concrete task of every cut, and moves the bar during its 3rd
        // task and back during its 6th: the concrete tasks of cuts 2-4 check the moved bar, which the reads of the earlier tasks did not hold.
        report = SameAsReference(ConcreteColumns.Build(10, 2), f => {
            var request = MaterialRequest(f); var job = request.Jobs[0];
            job.Plan.SectionMechanisms = new[] { CheckMechanism.Shear, CheckMechanism.UlsBiaxialSection };
            job.Assignments = new[] { new MaterialCheckerAssignment { Checker = new BarMovingEngine(f.Section, 3, 6), Mechanisms = new[] { CheckMechanism.Shear } },
                new MaterialCheckerAssignment { Checker = job.Assignments[0].Checker, Mechanisms = new[] { CheckMechanism.UlsBiaxialSection } } };
            return request; });
        var concrete = report.Jobs[0].Results.Where(r => r.Mechanism == CheckMechanism.UlsBiaxialSection).ToArray();
        Assert.AreEqual(20, concrete.Length); Assert.IsTrue(concrete.All(r => r.Data == DataStatus.Ready && r.Outcome == EngineeringOutcome.Satisfied));
        CollectionAssert.AreEqual(Enumerable.Range(0, 20).Select(i => i >= 2 && i <= 4).ToArray(), concrete.Select(r => r.Input!.SectionFingerprint != concrete[0].Input!.SectionFingerprint).ToArray());
    }
    /// <summary>The cases of the columns as one multi-material job routed to the concrete adapter.</summary>
    private static MultiMaterialCheckRequest MaterialRequest(ConcreteColumns f)
    {
        var request = new MultiMaterialCheckRequest();
        request.Jobs.Add(new MultiMaterialCheckJob { Name = "Concrete", Plan = new BeamCheckPlanRequest { Elements = new ElementSelection { Families = new[] { EntityFamily.Beam } },
            Results = f.Samples.Select(s => s.Case.Name).Distinct().Select(n => new ResultSelection { Dataset = "columns", Case = n, ConcomitantState = n }).ToArray(), Settings = "concrete columns" },
            Assignments = new[] { new MaterialCheckerAssignment { Checker = new ConcreteMaterialChecker(ConcreteColumns.Options()) } } });
        return request;
    }
    /// <summary>Wall-clock timing of the concrete run, outside the suite: set GPC_MODELCHECKER_TIMING=1.</summary>
    [TestMethod]
    public void TimingOfTheConcreteRun()
    {
        if (Environment.GetEnvironmentVariable("GPC_MODELCHECKER_TIMING") != "1") Assert.Inconclusive("Set GPC_MODELCHECKER_TIMING=1 to time the concrete run.");
        bool reference = Environment.GetEnvironmentVariable("GPC_MODELCHECKER_TIMING_REFERENCE") == "1";
        foreach (var (columns, cases) in new[] { (10, 4), (50, 4), (100, 4), (200, 4) })
            foreach (bool plan in new[] { false, true })
            {
                var f = ConcreteColumns.Build(columns, cases, plan: plan); var service = reference ? Reference(new Service()) : new Service();
                var watch = Stopwatch.StartNew(); var report = service.Verify(f.Model, f.Request); watch.Stop();
                Console.WriteLine($"{(reference ? "reference" : "shared")} {(plan ? "BeamPlan" : "Preparation")}: {columns * cases} rows, {columns} beams: "
                    + $"Verify {watch.Elapsed.TotalSeconds:F2} s, executed {report.Executed}, outcome {report.Outcome}");
                watch.Restart(); var outcome = report.CurrentOutcome(f.Model, _ => ConcreteColumns.Options().CreateVerifier()); watch.Stop();
                Console.WriteLine($"   CurrentOutcome {watch.Elapsed.TotalSeconds:F2} s ({outcome})");
            }
        // The concrete adapter routed by a multi-material plan (VerificationConfiguration and EngineCatalog route concrete beams so).
        foreach (int columns in new[] { 10, 50, 100, 200 })
        {
            var f = ConcreteColumns.Build(columns, 4); var material = MaterialRequest(f);
            var watch = Stopwatch.StartNew(); var report = new Service().Verify(f.Model, material); watch.Stop(); double verify = watch.Elapsed.TotalSeconds;
            Console.WriteLine($"multi-material concrete: {columns * 4} rows, {columns} beams: Verify {watch.Elapsed.TotalSeconds:F2} s, executed {report.Executed}, outcome {report.Outcome}");
            watch.Restart(); var outcome = Service.CurrentOutcome(report, f.Model, material); watch.Stop();
            Console.WriteLine($"   CurrentOutcome {watch.Elapsed.TotalSeconds:F2} s ({outcome})");
            if (verify > 60) break;
        }
        // Parts of one row, each read alone (200 beams).
        var g = ConcreteColumns.Build(200, 4);
        var input = Verification.PrepareBeam(g.Model, g.Model.BeamElements.Keys.First(), g.Samples[0], "concrete columns").Input!;
        var t = Stopwatch.StartNew(); VerificationPreparation.CurrentProvenance(g.Model); double capture = t.Elapsed.TotalMilliseconds;
        t.Restart(); bool current = input.IsCurrent; double isCurrent = t.Elapsed.TotalMilliseconds;
        Console.WriteLine($"one row of 200 beams, read alone: provenance capture {capture:F1} ms, IsCurrent {isCurrent:F1} ms ({current})");
    }
}
