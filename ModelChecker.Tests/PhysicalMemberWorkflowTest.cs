using GPC.Model.Models;
using System.Runtime.Serialization;
using System.Xml.Linq;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Checker;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Results.Storage;
using GPC.Model.Results.Locations;
using GPC.Model.Sections.Concrete;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Analysis;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Compatibility;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Structure.Members;

namespace ModelChecker.Tests;

[TestClass]
public class PhysicalMemberWorkflowTest
{
    private static GPC.Model.Models.Model Model()
    {
        var m = ElementScopeCharacterizationTest.Model();
        m.PhysicalMembers.Add("T1", new PhysicalMemberDefinition("T1", new[] { new BeamMemberPart(250, true), new BeamMemberPart(251, false) }, "NodeToNode", source: "Explicit fixture definition"));
        return m;
    }
    private static ModelCheckRequest Request(bool global = true, bool requiredLocations = false)
    {
        var plan = new BeamCheckPlanRequest { MemberIds = new[] { "T1" }, Settings = "step3 implementation",
            Results = new[] { new ResultSelection { Dataset = "synthetic-member", Case = "LC1", ConcomitantState = "LC1" } },
            CoveragePolicy = requiredLocations ? BeamCoveragePolicy.RequiredLocations : BeamCoveragePolicy.ExportedSamples };
        if (requiredLocations) plan.Locations = new[] { 0.0, 500, 1000, 1500, 2000, 3500, 5000 }.Select(d => new RequiredBeamLocation { MemberId = "T1", Distance = d }).ToArray();
        if (global) plan.MemberChecks = new[] { new MemberCheckSpecification { MemberId = "T1", MethodId = "fixture-member-method",
            Context = new MemberDesignContext(globalStateConfirmed: true) } };
        var request = new ModelCheckRequest(); request.Jobs.Add(new ModelCheckJob { Name = "T1 verification", BeamPlan = plan,
            Options = new ConcreteVerificationOptions { Standard = new GPC.Model.Standards.StandardNTC2018Concrete(), Criterion = SectionSolver.FailureAnalysisTypes.ConstantEccentricity } });
        return request;
    }
    // Protocol test double only. Real section calculations below use the installed Concrete engine.
    private sealed class MemberEngine : IPhysicalMemberVerifier
    {
        public string Version => "fixture-1";
        public string Configuration => "protocol-fixture";
        public CheckStandardContext Standard { get; set; } = new("Fixture", "1", null, "protocol test", "protocol-fixture");
        public int Calls { get; private set; }
        public Action<PhysicalMemberCheckInput>? DuringCheck { get; set; }
        public bool Supports(string method, CheckMechanism mechanism) => method == "fixture-member-method" && mechanism == CheckMechanism.Stability;
        public CheckResult Verify(PhysicalMemberCheckInput input, CancellationToken token)
        {
            Calls++; DuringCheck?.Invoke(input);
            return new CheckResult { Execution = ExecutionStatus.Completed, Data = DataStatus.Ready, Outcome = EngineeringOutcome.Satisfied, Utilization = .5 };
        }
    }
    [TestMethod]
    public void NineWorkItemsReuseRealSectionsAndKeepUnavailableGlobalCheckVisible()
    {
        var m = Model(); var request = Request(); var plan = BeamCheckPlan.Prepare(m, request.Jobs[0].BeamPlan);
        Assert.AreEqual(9, plan.WorkItems.Count); Assert.AreEqual(1, plan.WorkItems.Count(w => w.Scope == CheckScope.PhysicalMember));
        var report = new Service().Verify(m, request);
        Assert.AreEqual(9, report.Required); Assert.AreEqual(8, report.Executed); Assert.AreEqual(1, report.CreatedCheckers);
        Assert.AreEqual(2, report.Elements.Count); Assert.AreEqual(1, report.Members.Count); Assert.AreEqual(9, report.Members[0].Results.Count);
        var global = report.Jobs[0].Results.Single(r => r.Scope == CheckScope.PhysicalMember);
        Assert.AreEqual(CheckTargetKind.PhysicalMember, global.Target.Kind); Assert.IsNull(global.Target.BeamId);
        Assert.AreEqual(DataStatus.NotSupported, global.Data); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.AreEqual(5000, global.MemberInput.Length); Assert.IsNull(global.MemberInput.Context.EffectiveLength1);
        Assert.AreEqual(8, global.MemberInput.Samples.Count); Assert.IsFalse(global.CoverageAssessment.ContinuousCoverage);
    }
    [TestMethod]
    public void RequiredPhysicalStationRemainsMissingAndCommonNodeKeepsBothSections()
    {
        var m = Model(); m.BeamElements[250].Results[0].Results.RemoveAll(r => ((StationResultBeamForces)r).ParametricDistance == .5);
        var report = new Service().Verify(m, Request(false, true));
        Assert.AreEqual(8, report.Required); Assert.AreEqual(7, report.Executed); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        var missing = report.Jobs[0].Results.Single(r => r.CoverageAssessment.MissingLocations == 1);
        Assert.AreEqual(1000, missing.MemberLocation.Distance); Assert.AreEqual(DataStatus.Insufficient, missing.Data);
        Assert.IsTrue(missing.Diagnostics.Any(d => d.Code == "MissingRequiredLocation"));
        Assert.AreEqual(2, report.Jobs[0].Results.Count(r => r.MemberLocation.Distance == 2000));
    }
    [TestMethod]
    public void OrderedGeometryMapsReversedStationsAndRejectsInvalidDefinitions()
    {
        var m = Model(); var geometry = new PhysicalMemberGeometry(m, m.PhysicalMembers["T1"]);
        Assert.AreEqual(5000, geometry.Length); Assert.AreEqual(5000, geometry.DeformableLength);
        Assert.AreEqual(2, geometry.Locate(2000).Count);
        Assert.AreEqual(250, geometry.Locate(2000, SectionSide.Left).Single().BeamId);
        var right = geometry.Locate(2000, SectionSide.Right).Single(); Assert.AreEqual(251, right.BeamId); Assert.AreEqual(1, right.Station);
        Assert.AreEqual(SectionSide.Left, right.Side);
        Assert.AreEqual(0, geometry.Locate(5000).Single().Station);
        Assert.AreEqual(3500, geometry.FromElement(251, .5, "NodeToNode", SectionSide.Unspecified).Distance);
        Assert.ThrowsException<ArgumentException>(() => new PhysicalMemberDefinition("T", new[] { new BeamMemberPart(250, true), new BeamMemberPart(250, false) }, "NodeToNode"));
        Assert.ThrowsException<ArgumentException>(() => new PhysicalMemberGeometry(m, new PhysicalMemberDefinition("T", new[] { new BeamMemberPart(250, true), new BeamMemberPart(251, true) }, "NodeToNode")));
        Assert.ThrowsException<NotSupportedException>(() => new PhysicalMemberDefinition("T", new[] { new BeamMemberPart(250, true) }, "Deformable"));
        m.NodesElements[30].Position.X = 10;
        Assert.ThrowsException<NotSupportedException>(() => new PhysicalMemberGeometry(m, m.PhysicalMembers["T1"]));
    }
    [TestMethod]
    public void RigidZonesAndOffsetsDoNotBecomeEffectiveLengths()
    {
        var m = Model(); var b = m.BeamElements[250]; b.Assignments.RigidLengthI = 200; b.Assignments.RigidLengthJ = 100;
        var geometry = new PhysicalMemberGeometry(m, m.PhysicalMembers["T1"]);
        Assert.AreEqual(5000, geometry.Length); Assert.AreEqual(4700, geometry.DeformableLength);
        Assert.AreEqual(1050, geometry.FromElement(250, .5, "Deformable", SectionSide.Unspecified).Distance, 1e-8);
        foreach (var beam in m.BeamElements.Values) { beam.Assignments.OffsetI = beam.Assignments.OffsetJ = new Vector3d(100, 0, 0); beam.Assignments.OffsetAxes = CoordinateSystem.Global; }
        var definition = new PhysicalMemberDefinition("Offset", m.PhysicalMembers["T1"].Parts, "OffsetToOffset");
        Assert.AreEqual(100, new PhysicalMemberGeometry(m, definition).Axes.Origin.X);
        m.BeamElements[251].Assignments.OffsetJ = new Vector3d(120, 0, 0);
        Assert.ThrowsException<NotSupportedException>(() => new PhysicalMemberGeometry(m, definition));
        Assert.IsNull(new MemberDesignContext().EffectiveLength1);
    }
    [TestMethod]
    public void DefinitionChangesInvalidatePlanWithoutInvalidatingFemAnalysis()
    {
        var m = Model(); var request = Request(false); string analysis = m.AnalysisFingerprint();
        var plan = BeamCheckPlan.Prepare(m, request.Jobs[0].BeamPlan); var report = new Service().Verify(m, request);
        Assert.IsTrue(plan.IsCurrent);
        m.PhysicalMembers["T1"] = new PhysicalMemberDefinition("T1", new[] { new BeamMemberPart(250, true) }, "NodeToNode");
        Assert.AreEqual(analysis, m.AnalysisFingerprint()); Assert.IsFalse(plan.IsCurrent);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.CurrentOutcome(m, _ => request.Jobs[0].Options.CreateVerifier()));
    }
    [TestMethod]
    public void GlobalEngineRunsOnceAndContextChangesInvalidateTheCurrentJob()
    {
        var m = Model(); var request = Request(); var engine = new MemberEngine();
        var report = new Service(j => j.Options.CreateVerifier(), _ => engine).Verify(m, request);
        Assert.AreEqual(1, engine.Calls); Assert.AreEqual(9, report.Executed);
        Assert.AreEqual(report.Outcome, report.CurrentOutcome(m, _ => request.Jobs[0].Options.CreateVerifier(), _ => engine, _ => request.Jobs[0].BeamPlan));
        request.Jobs[0].BeamPlan.MemberChecks[0].Context = new MemberDesignContext(effectiveLength1: 9000, lengthSource: "Explicit method input", globalStateConfirmed: true);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.CurrentOutcome(m, _ => request.Jobs[0].Options.CreateVerifier(), _ => engine, _ => request.Jobs[0].BeamPlan));
        Assert.IsNull(report.Jobs[0].Results.Last().MemberInput.Context.EffectiveLength1);
    }
    [TestMethod]
    public void UnknownGlobalStateAndMissingBoundaryPreventEngineExecution()
    {
        var m = Model(); var request = Request(); var engine = new MemberEngine();
        request.Jobs[0].BeamPlan.MemberChecks[0].Context = new MemberDesignContext();
        var report = new Service(j => j.Options.CreateVerifier(), _ => engine).Verify(m, request);
        Assert.AreEqual(0, engine.Calls); Assert.AreEqual(8, report.Executed);
        Assert.IsTrue(report.Jobs[0].Results.Last().Diagnostics.Any(d => d.Code == "GlobalMemberStateNotConfirmed"));
        request.Jobs[0].BeamPlan.MemberChecks[0].Context = new MemberDesignContext(globalStateConfirmed: true);
        request.Jobs[0].BeamPlan.MemberChecks[0].Start = 123;
        report = new Service(j => j.Options.CreateVerifier(), _ => engine).Verify(m, request);
        Assert.AreEqual(0, engine.Calls); Assert.IsTrue(report.Jobs[0].Results.Last().Diagnostics.Any(d => d.Code == "MissingMemberSpanBoundary"));
    }
    [TestMethod]
    public void PartialGroupsDoNotImplicitlySelectAnEntireMember()
    {
        var m = Model(); m.AddGroup("Partial"); m.AssignGroup("Partial", new[] { m.BeamElements[250] }); var request = Request();
        request.Jobs[0].BeamPlan.MemberIds = Array.Empty<string>();
        request.Jobs[0].BeamPlan.Elements = new ElementSelection { Groups = new[] { "Partial" }, Families = new[] { EntityFamily.Beam } };
        Assert.ThrowsException<ArgumentException>(() => new Service().Verify(m, request));
        request.Jobs[0].BeamPlan.MemberChecks = Array.Empty<MemberCheckSpecification>();
        Assert.AreEqual(5, new Service().Verify(m, request).Required);
    }
    [TestMethod]
    public void RequestIsCopiedAndPreparedInputMutationIsDetected()
    {
        var m = Model(); var request = Request(false, true); var plan = BeamCheckPlan.Prepare(m, request.Jobs[0].BeamPlan);
        request.Jobs[0].BeamPlan.Locations[0].Distance = 77; Assert.IsTrue(plan.IsCurrent); Assert.AreEqual(0, plan.Request.Locations[0].Distance);
        plan.WorkItems[0].Section.Input.Forces.M1 += 1; Assert.IsFalse(plan.IsCurrent);
    }
    [TestMethod]
    public void MutationInsideGlobalEngineCannotProduceACompletedPass()
    {
        var engine = new MemberEngine { DuringCheck = input => input.Sections[0].Section.Rebars.First().Position.Y += 1 };
        var report = new Service(j => j.Options.CreateVerifier(), _ => engine).Verify(Model(), Request());
        Assert.AreEqual(0, report.Executed); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.IsTrue(report.Jobs[0].Results.All(r => r.Data == DataStatus.Stale));
    }
    [TestMethod]
    public void Schema3ArchivesRoundTripMembersContextsAndAnalysisEvidence()
    {
        var m = Model(); var request = Request(); var engine = new MemberEngine();
        request.Jobs[0].BeamPlan.MemberChecks[0].Context = new MemberDesignContext(new[] {
            new MemberRestraint(0, CoordinateSystem.Global, new bool?[] { true, true, true, null, null, true }, "Fixture support") },
            effectiveLength1: 9000, lengthSource: "Explicit fixture parameter", globalStateConfirmed: true);
        var report = new Service(j => j.Options.CreateVerifier(), _ => engine).Verify(m, request);
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        Assert.AreEqual("3", XDocument.Load(stream).Root!.Attribute("version")!.Value); stream.Position = 0;
        var restored = CheckReportArchive.Load(stream).Single();
        Assert.IsTrue(restored.Results.All(r => r.HasUnchangedEvidence)); Assert.IsTrue(restored.HasUnchangedScope);
        Assert.AreEqual(report.Outcome, restored.CurrentOutcome(m, request.Jobs[0].Options.CreateVerifier(), engine));
        Assert.AreEqual(9000, restored.Results.Last().MemberInput.Context.EffectiveLength1);
        m.CheckReports.Add(restored); using var full = new MemoryStream(); ModelArchive.Save(m, full); full.Position = 0;
        var copy = ModelArchive.Load(full);
        Assert.AreEqual(5000, new PhysicalMemberGeometry(copy, copy.PhysicalMembers["T1"]).Length);
        Assert.AreEqual(report.Outcome, copy.CheckReports.Single().CurrentOutcome(copy, request.Jobs[0].Options.CreateVerifier(), engine));
        full.Position = 0; var xml = XDocument.Load(full); xml.Root!.SetAttributeValue("version", "1");
        using var invalid = new MemoryStream(); xml.Save(invalid); invalid.Position = 0;
        Assert.ThrowsException<SerializationException>(() => ModelArchive.Load(invalid));
    }
    private sealed class Progress : IProgress<VerificationProgress>
    {
        private readonly Action<VerificationProgress> _action;
        public Progress(Action<VerificationProgress> action) { _action = action; }
        public void Report(VerificationProgress value) => _action(value);
    }
    [TestMethod]
    public void CancellationKeepsEveryPlannedItemAndReportsMemberIdentity()
    {
        var token = new CancellationTokenSource(); var events = new List<VerificationProgress>();
        var report = new Service().Verify(Model(), Request(), token.Token, new Progress(p => { events.Add(p); token.Cancel(); }));
        Assert.AreEqual(9, report.Required); Assert.AreEqual(1, report.Executed); Assert.AreEqual(8, report.Summary.Cancelled);
        Assert.AreEqual(9, events.Last().Completed); Assert.AreEqual("T1", events.Last().Target.MemberId); Assert.IsNull(events.Last().Target.BeamId);
    }
    [TestMethod]
    public void SamePhysicalSectionsGiveSameResultsAfterMeshCoarsening()
    {
        var split = Model(); var coarse = Model();
        var removed = coarse.BeamElements[251]; coarse.UnassignGroup("T1 selection", new[] { removed }); coarse.RemoveElement(removed);
        coarse.ConnectBeam(250, 10, 30); coarse.RemoveNodeChecked(20);
        coarse.PhysicalMembers["T1"] = new PhysicalMemberDefinition("T1", new[] { new BeamMemberPart(250, true) }, "NodeToNode");
        var b = coarse.BeamElements[250]; b.Results.Clear();
        var dataset = coarse.Datasets["synthetic-member"]; coarse.Datasets.Clear();
        coarse.CaptureAnalysis(ReinforcementAnalysisRole.ExcludedFromAnalysis, "New analytical cantilever calculation after mesh coarsening.");
        var fingerprint = coarse.AnalysisFingerprint(); dataset.InputFingerprint = fingerprint;
        coarse.Datasets.Add(dataset.Id, dataset);
        var samples = new[] { 0.0, 500, 1000, 1500, 2000, 3500, 5000 }.Select(d => new StationResultBeamForces(coarse.LoadCases["LC1"],
            new ResultBeamForces(0, 0, 1000, 0, -1000 * (5000 - d), 0, ResultTransformations.AtPoint(CoordinateSystem.Global, new Point3d(0, 0, d))), d / 5000)
            { StationDomain = "NodeToNode", PhysicalDistance = d, Body = ActionBody.PositiveSectionFace,
                State = new ResultState { DatasetId = "synthetic-member", ModelRevision = "r1", InputFingerprint = fingerprint,
                    Components = Enumerable.Repeat(ComponentAvailability.Available, 6).ToArray(), ConcomitantStateId = "LC1",
                    Semantics = AnalysisSemantics.LinearStatic, IsSynthetic = true, IsCumulative = true } }).ToArray();
        b.AddResult(new BeamResult(samples));
        var first = new Service().Verify(split, Request(false, true)); var second = new Service().Verify(coarse, Request(false, true));
        Assert.AreEqual(8, first.Executed); Assert.AreEqual(7, second.Executed); // Shared FEM endpoint becomes one section after merging.
        foreach (var result in first.Jobs[0].Results)
        {
            var samePoint = second.Jobs[0].Results.Single(r => r.MemberLocation.Distance == result.MemberLocation.Distance);
            Assert.AreEqual(result.Utilization!.Value, samePoint.Utilization!.Value, 1e-12);
            Assert.AreEqual(result.Input.BeamForces.M1, samePoint.Input.BeamForces.M1, 1e-8);
        }
        Assert.AreEqual(5000, new PhysicalMemberGeometry(coarse, coarse.PhysicalMembers["T1"]).Length);
    }
    [TestMethod]
    public void DifferentReinforcementAtTheCommonNodeRetainsBothActualSections()
    {
        var m = Model(); var original = m.BeamElements[250].Assignments.Sections[0].Section; var analysis = m.AnalysisFingerprint();
        var second = new ReinforcedConcreteSection(original.SectionShape, original.ConcreteMaterial);
        foreach (var bar in original.Rebars) second.AddRebar(new ReinforcedConcreteRebar(bar.RebarSection, new Point2d(bar.Position.X, bar.Position.Y + 10)));
        m.BeamElements[251].Assignments.Sections[0].Section = second;
        Assert.AreEqual(analysis, m.AnalysisFingerprint());
        var report = new Service().Verify(m, Request(false, true)); Assert.AreEqual(8, report.Executed); Assert.AreEqual(2, report.CreatedCheckers);
        var joint = report.Jobs[0].Results.Where(r => r.MemberLocation.Distance == 2000).ToArray();
        Assert.AreEqual(2, joint.Length); Assert.AreNotEqual(joint[0].Input.SectionFingerprint, joint[1].Input.SectionFingerprint);
        Assert.AreEqual(joint[0].Input.BeamForces.M1, joint[1].Input.BeamForces.M1);
    }
    [TestMethod]
    public void AmbiguousExportedSamplesAreNotSilentlyDeduplicated()
    {
        var m = Model(); var sample = (StationResultBeamForces)m.BeamElements[250].Results[0].Results[0];
        var duplicate = new StationResultBeamForces(sample.Case, new ResultBeamForces(0, 0, 1000, 0, -1e6, 0, sample.ResultBeamForces.CoordinateSystem), sample.ParametricDistance)
            { State = sample.State.Copy(), StationDomain = sample.StationDomain, Body = sample.Body, PhysicalDistance = sample.PhysicalDistance };
        m.BeamElements[250].Results[0].Results.Add(duplicate);
        var report = new Service().Verify(m, Request(false));
        Assert.AreEqual(8, report.Required); Assert.AreEqual(7, report.Executed);
        Assert.IsTrue(report.Jobs[0].Results.Any(r => r.Diagnostics.Any(d => d.Code == "AmbiguousExportedLocationState")));
    }
    [TestMethod]
    public void DatasetMetadataAndConflictingContextsAreValidated()
    {
        var m = Model(); var request = Request(); var plan = BeamCheckPlan.Prepare(m, request.Jobs[0].BeamPlan);
        m.Datasets["synthetic-member"].AnalysisId = "different-run"; Assert.IsFalse(plan.IsCurrent);
        var original = request.Jobs[0].BeamPlan.MemberChecks.Single();
        request.Jobs[0].BeamPlan.MemberChecks = new[] { original, new MemberCheckSpecification { MemberId = "T1", MethodId = original.MethodId,
            Context = new MemberDesignContext(effectiveLength2: 8000, lengthSource: "different context") } };
        Assert.ThrowsException<ArgumentException>(() => BeamCheckPlan.Prepare(m, request.Jobs[0].BeamPlan));
    }
    [TestMethod]
    public void RepeatedSelectorsDeduplicateAndLegacyJobsStillRunAlongsideNewPlans()
    {
        var m = Model(); var request = Request(false); var job = request.Jobs[0];
        job.BeamPlan.Elements = new ElementSelection { Groups = new[] { "T1 selection", "T1 selection" }, Families = new[] { EntityFamily.Beam } };
        job.BeamPlan.Results = new[] { job.BeamPlan.Results[0], job.BeamPlan.Results[0].Copy() };
        request.Jobs.Add(new ModelCheckJob { Name = "Legacy", Options = job.Options,
            Preparation = new PreparationRequest { Selection = new ElementSelection { Families = new[] { EntityFamily.Beam } }, Results = new[] { job.BeamPlan.Results[0] } } });
        var report = new Service().Verify(m, request);
        Assert.AreEqual(16, report.Required); Assert.AreEqual(16, report.Executed); Assert.AreEqual(1, report.CreatedCheckers);
        Assert.AreEqual(2, report.Jobs[0].Results[0].SchemaVersion); Assert.AreEqual(1, report.Jobs[1].Results[0].SchemaVersion);
        Assert.AreEqual(2, report.Elements.Count); Assert.AreEqual(1, report.Members.Single().Governing.Count);
    }
    [TestMethod]
    public void AlreadyCancelledPlanKeepsAllWorkAndBeamPlanRejectsShellCoercion()
    {
        var report = new Service().Verify(Model(), Request(), new CancellationToken(true));
        Assert.AreEqual(9, report.Required); Assert.AreEqual(9, report.Summary.Cancelled);
        var request = Request(false); request.Jobs[0].BeamPlan.Elements = new ElementSelection { Families = new[] { EntityFamily.Shell } };
        Assert.ThrowsException<NotSupportedException>(() => new Service().Verify(Model(), request));
    }
    [TestMethod]
    public void InvalidExportedMemberLocationIsRetainedAsAnIncompleteTask()
    {
        var m = Model(); ((StationResultBeamForces)m.BeamElements[250].Results[0].Results[0]).StationDomain = "Unknown";
        var report = new Service().Verify(m, Request(false));
        Assert.AreEqual(8, report.Required); Assert.AreEqual(7, report.Executed);
        var invalid = report.Jobs[0].Results.Single(r => r.Diagnostics.Any(d => d.Code == "InvalidMemberSampleLocation"));
        Assert.AreEqual(DataStatus.Insufficient, invalid.Data); Assert.IsNull(invalid.MemberLocation);
    }
    [TestMethod]
    public void ChangingTheGlobalStandardInvalidatesTheReportAndSchemaDowngradesAreRejected()
    {
        var m = Model(); var request = Request(); var engine = new MemberEngine();
        var report = new Service(j => j.Options.CreateVerifier(), _ => engine).Verify(m, request);
        engine.Standard = new CheckStandardContext("Fixture", "2", null, "different standard", engine.Configuration);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.CurrentOutcome(m, _ => request.Jobs[0].Options.CreateVerifier(), _ => engine));
        var global = report.Jobs[0].Results.Last(); global.SchemaVersion = 1;
        using var stream = new MemoryStream();
        Assert.ThrowsException<SerializationException>(() => CheckReportArchive.Save(report.Jobs, stream));
        Assert.AreEqual(0, stream.Length);
    }
}
