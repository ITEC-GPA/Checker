using System.Diagnostics;
using GPC.Examples;
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
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using GPC.Model.Structure.Members;
using GPC.Model.Structure.Topology;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>The whole plan is revalidated once per plan and at the end of the run; every task
/// only compares the owned copies handed to its engine. The run stays linear in its tasks and
/// a change of the shared model during the run still invalidates the evaluated rows.</summary>
[TestClass]
public class OrchestratorRevalidationTest
{
    private sealed class Progress : IProgress<VerificationProgress>
    {
        private readonly Action<VerificationProgress> _action;
        public Progress(Action<VerificationProgress> action) => _action = action;
        public void Report(VerificationProgress p) => _action(p);
    }

    /// <summary>N independent steel beams, one mid-span sample per case, low shear only.</summary>
    private static (GPC.Model.Models.Model, MultiMaterialCheckRequest) Build(int beams, int cases, out List<StationResultBeamForces> samples)
    {
        var model = new GPC.Model.Models.Model("Orchestrator scaling");
        var section = new SteelSection(new SectionH(300, 12, 150, 18, 150, 18, "Stocky H"), SteelMaterialEN1993Data.S355);
        model.AddProperty(section);
        var group = model.AddGroup("Steel").Name; var all = new List<BeamElement>();
        for (int i = 0; i < beams; i++)
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
        model.Datasets.Add("scale", new AnalysisDataset { Id = "scale", Program = "Assigned analytical fixture", ModelRevision = "1",
            InputFingerprint = revision, NormalizedUnits = "N,mm,rad", Semantics = AnalysisSemantics.LinearStatic, IsSynthetic = true });
        samples = new List<StationResultBeamForces>();
        foreach (var beam in model.BeamElements.Values)
        {
            var point = new BeamReferenceGeometry(beam).PointAt(.5, "NodeToNode");
            var beamSamples = names.Select(name => new StationResultBeamForces(model.LoadCases[name], new ResultBeamForces(0, 0, 1000, 0, 0, 0,
                ResultTransformations.AtPoint(CoordinateSystem.Global, point)), .5) {
                StationDomain = "NodeToNode", PhysicalDistance = 500, Body = ActionBody.PositiveSectionFace,
                State = new ResultState { DatasetId = "scale", ModelRevision = "1", InputFingerprint = revision, ConcomitantStateId = name,
                    Components = Enumerable.Repeat(ComponentAvailability.Available, 6).ToArray(), IsCumulative = true, IsSynthetic = true,
                    Semantics = AnalysisSemantics.LinearStatic } }).ToArray();
            beam.AddResult(new BeamResult(beamSamples)); samples.AddRange(beamSamples);
        }
        var request = new MultiMaterialCheckRequest();
        request.Jobs.Add(new MultiMaterialCheckJob { Name = "Steel shear", Plan = new BeamCheckPlanRequest {
            Elements = new ElementSelection { Groups = new[] { group }, Families = new[] { EntityFamily.Beam } },
            Results = names.Select(n => new ResultSelection { Dataset = "scale", Case = n, ConcomitantState = n }).ToArray(),
            SectionMechanisms = new[] { CheckMechanism.Shear } },
            Assignments = new[] { new MaterialCheckerAssignment { Checker = new SteelMaterialChecker(new StandardEN1993p11(), "2005") } } });
        return (model, request);
    }

    private static double Time(int beams, int cases)
    {
        var (model, request) = Build(beams, cases, out _);
        var watch = Stopwatch.StartNew(); var report = new Service().Verify(model, request); watch.Stop();
        Assert.AreEqual(beams * cases, report.Executed);
        return watch.Elapsed.TotalMilliseconds;
    }

    [TestMethod]
    public void RunCostGrowsLinearlyWithTheNumberOfTasks()
    {
        Time(10, 4);
        double small = Time(20, 4), large = Time(80, 4);
        // 80 -> 320 tasks: about 4x when linear. Revalidating the whole plan around every
        // task gave about 16x (2.5 s -> 41.6 s measured on 27/10/2026).
        Assert.IsTrue(large / small < 8, $"{small:F0} ms -> {large:F0} ms");
    }

    /// <summary>Protocol double: one member result per call, no calculation.</summary>
    private sealed class MemberFixture : IMaterialChecker, IPhysicalMemberVerifier
    {
        public int Calls;
        public string Id => "Fixture.Member";
        public string Version => "fixture-1";
        public string Configuration => "fixture";
        public CheckStandardContext Standard => new("Fixture", "1", null, Id, Configuration);
        public bool Accepts(BeamElement element) => true;
        public IMaterialCheckSession CreateSession() => throw new NotSupportedException("Members only.");
        public bool Supports(string methodId, CheckMechanism mechanism) => methodId == Id && mechanism == CheckMechanism.Stability;
        public CheckResult Verify(PhysicalMemberCheckInput input, CancellationToken token)
        {
            Calls++;
            return new CheckResult { Execution = ExecutionStatus.Completed, Data = DataStatus.Ready, Outcome = EngineeringOutcome.Satisfied, Utilization = .5 };
        }
    }

    /// <summary>N one-element physical members, exported at both ends and at mid-span; one member check each.</summary>
    private static (GPC.Model.Models.Model, MultiMaterialCheckRequest, MemberFixture) BuildMembers(int members)
    {
        var model = new GPC.Model.Models.Model("Member orchestrator scaling");
        var section = new SteelSection(new SectionH(300, 12, 150, 18, 150, 18, "Stocky H"), SteelMaterialEN1993Data.S355);
        model.AddProperty(section);
        for (int i = 0; i < members; i++)
        {
            int id = 10 * (i + 1);
            model.NodesElements.Add(new NodeElement(new Point3d(i * 3000, 0, 0), id: id));
            model.NodesElements.Add(new NodeElement(new Point3d(i * 3000, 0, 1000), id: id + 1));
            var beam = new BeamElement(null, null, section, id: id);
            model.BeamElements.Add(beam); model.ConnectBeam(id, id, id + 1);
            beam.Assignments.Formulation = BeamFormulation.StraightTwoNode; beam.Assignments.SectionAxes = CoordinateSystem.Global;
            beam.Assignments.SectionGeometryAxes = CoordinateSystem.Global; beam.Assignments.StationDomain = "NodeToNode";
            beam.Assignments.ActionsAtSectionCentroidConfirmed = true;
            model.PhysicalMembers.Add("M" + i, new PhysicalMemberDefinition("M" + i, new[] { new BeamMemberPart(id, true) }, "NodeToNode", source: "Fixture member"));
        }
        model.LoadCases.Add(new LoadCaseBase("C0"));
        model.CaptureAnalysis(ReinforcementAnalysisRole.ExcludedFromAnalysis, "Assigned analytical actions.");
        string revision = model.AnalysisFingerprint();
        model.Datasets.Add("scale", new AnalysisDataset { Id = "scale", Program = "Assigned analytical fixture", ModelRevision = "1",
            InputFingerprint = revision, NormalizedUnits = "N,mm,rad", Semantics = AnalysisSemantics.LinearStatic, IsSynthetic = true });
        foreach (var beam in model.BeamElements.Values)
            beam.AddResult(new BeamResult(new[] { 0d, .5, 1d }.Select(xi => new StationResultBeamForces(model.LoadCases["C0"], new ResultBeamForces(0, 0, 1000, 0, 0, 0,
                ResultTransformations.AtPoint(CoordinateSystem.Global, new BeamReferenceGeometry(beam).PointAt(xi, "NodeToNode"))), xi) {
                StationDomain = "NodeToNode", PhysicalDistance = 1000 * xi, Body = ActionBody.PositiveSectionFace,
                State = new ResultState { DatasetId = "scale", ModelRevision = "1", InputFingerprint = revision, ConcomitantStateId = "C0",
                    Components = Enumerable.Repeat(ComponentAvailability.Available, 6).ToArray(), IsCumulative = true, IsSynthetic = true,
                    Semantics = AnalysisSemantics.LinearStatic } }).ToArray()));
        var ids = model.PhysicalMembers.Keys.ToArray(); var fixture = new MemberFixture();
        var request = new MultiMaterialCheckRequest();
        request.Jobs.Add(new MultiMaterialCheckJob { Name = "Members", Plan = new BeamCheckPlanRequest { MemberIds = ids,
            Results = new[] { new ResultSelection { Dataset = "scale", Case = "C0", ConcomitantState = "C0" } },
            SectionMechanisms = new[] { CheckMechanism.Shear },
            MemberChecks = ids.Select(m => new MemberCheckSpecification { MemberId = m, MethodId = fixture.Id,
                Context = new MemberDesignContext(globalStateConfirmed: true) }).ToArray() },
            Assignments = new[] {
                new MaterialCheckerAssignment { Checker = new SteelMaterialChecker(new StandardEN1993p11(), "2005"), Mechanisms = new[] { CheckMechanism.Shear } },
                new MaterialCheckerAssignment { Checker = fixture, Mechanisms = new[] { CheckMechanism.Stability } } } });
        return (model, request, fixture);
    }

    private static double TimeMembers(int members)
    {
        var (model, request, fixture) = BuildMembers(members);
        var watch = Stopwatch.StartNew(); var report = new Service().Verify(model, request); watch.Stop();
        Assert.AreEqual(members, fixture.Calls); Assert.AreEqual(4 * members, report.Executed);
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        return watch.Elapsed.TotalMilliseconds;
    }

    [TestMethod]
    public void MemberRowsAlsoKeepTheRunLinear()
    {
        TimeMembers(5);
        double small = TimeMembers(20), large = TimeMembers(80);
        // 80 -> 320 tasks: about 4x when linear. MemberCheckExecution.Run revalidates the whole plan twice per
        // member row: about 16x (100 members x 2 states, 1200 tasks: 65.9 s, measured on 28/10/2026).
        Assert.IsTrue(large / small < 8, $"{small:F0} ms -> {large:F0} ms");
    }

    [TestMethod]
    public void SharedModelMutationDuringTheRunInvalidatesEveryEvaluatedRow()
    {
        var (model, request) = Build(3, 2, out var samples); bool mutated = false;
        // After the first task, change a model sample that a later task reads (not an owned copy).
        var report = new Service().Verify(model, request, progress: new Progress(_ => {
            if (!mutated) { samples[^1].ResultBeamForces.V2 += 1; mutated = true; } }));
        Assert.IsTrue(mutated);
        var rows = report.Jobs.SelectMany(j => j.Results).ToArray();
        Assert.AreEqual(6, rows.Length); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.IsTrue(rows.All(r => r.Data == DataStatus.Stale && r.Outcome == EngineeringOutcome.NotEvaluated));
        Assert.IsTrue(rows.All(r => r.Diagnostics.Any(d => d.Code == "MaterialInputsChangedDuringRun")));
    }
}
