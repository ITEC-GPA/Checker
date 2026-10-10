using System.Xml.Linq;
using GPC.Examples;
using GPC.Checkers.CompositeBridge;
using GPC.Geometry;
using GPC.Model.Checker;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Results.Processing;
using GPC.Model.Results.Queries;
using GPC.Model.Structure.Members;

namespace ModelChecker.Tests;

[TestClass]
public class MultiMaterialWorkflowTest
{
    private static string Diagnostics(ModelCheckReport report) => string.Join("\n", report.Jobs.SelectMany(j => j.Results).Select(r => r.Job + ": " + r.Data + "/" + r.Execution + " " + string.Join(",", r.Diagnostics.Select(d => d.Code + " " + d.Message))));
    [TestMethod]
    public void ThreeMaterialsUseRealNativeEnginesAndPreserveTheirCheckScopes()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var report = new Service().Verify(model, request);
        Assert.AreEqual(4, report.Required); Assert.AreEqual(4, report.Executed, Diagnostics(report));
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome, Diagnostics(report));
        Assert.AreEqual(3, report.Elements.Count); Assert.AreEqual(3, report.Groups.Count); Assert.AreEqual(3, report.Standards.Count);
        Assert.AreEqual(4, report.CreatedCheckers); Assert.AreEqual(report.Outcome, Service.CurrentOutcome(report, model, request));
        Assert.IsTrue(report.Jobs[2].Results.Single().Details is NativeMethodDetails);
        Assert.AreEqual("CompositeBridge.Section.SleStress", report.Jobs[2].Results.Single().MethodId);
        Assert.AreEqual("CompositeBridge.Section.WebShear", report.Jobs[3].Results.Single().MethodId);
    }
    [TestMethod]
    public void DifferentStandardsAreSeparateJobsAndEquivalentRoutesReuseSessions()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var steelJob = request.Jobs[1]; request.Jobs.Clear(); request.Jobs.Add(steelJob);
        var second = new SteelMaterialChecker(new StandardEN1993p11(), "2005");
        steelJob.Assignments = new[] { steelJob.Assignments[0], new MaterialCheckerAssignment { Checker = second,
            Selection = new ElementSelection { Groups = new[] { "Steel" }, Families = new[] { EntityFamily.Beam } } } };
        request.Jobs.Add(MultiMaterialWorkflow.Job("Same config", "Steel", "ULS", CheckMechanism.Shear, second));
        var third = new SteelMaterialChecker(new StandardEN1993p11 { GammaM0 = 1.1 }, "2005", "Explicit project factors");
        request.Jobs.Add(MultiMaterialWorkflow.Job("Other config", "Steel", "ULS", CheckMechanism.Shear, third));
        var report = new Service().Verify(model, request);
        Assert.AreEqual(3, report.Executed, Diagnostics(report)); Assert.AreEqual(2, report.CreatedCheckers);
        Assert.AreEqual(report.Jobs[0].Results[0].Utilization!.Value * 1.1, report.Jobs[2].Results[0].Utilization!.Value, 1e-12);
        Assert.AreEqual(2, report.Elements.Single().Governing.Count);
    }
    [TestMethod]
    public void OverlappingGroupConflictsAreRejectedBeforeAnyNativeRun()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1];
        job.Assignments = new[] { job.Assignments[0], new MaterialCheckerAssignment { Checker = new SteelMaterialChecker(new StandardEN1993p11 { GammaM0 = 1.2 }, "2005"),
            Selection = new ElementSelection { Groups = new[] { "Steel" }, Families = new[] { EntityFamily.Beam } } } };
        Assert.ThrowsException<ArgumentException>(() => new Service().Verify(model, request));
    }
    [TestMethod]
    public void MissingEngineAndUnsupportedChecksRemainRequired()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); request.Jobs[0].Assignments = Array.Empty<MaterialCheckerAssignment>();
        request.Jobs[1].Plan.SectionMechanisms = new[] { CheckMechanism.Stability, CheckMechanism.Shear };
        var report = new Service().Verify(model, request);
        Assert.AreEqual(5, report.Required); Assert.AreEqual(3, report.Executed, Diagnostics(report));
        Assert.AreEqual(2, report.Summary.Unsupported); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
    }
    [TestMethod]
    public void RequiredMissingStationAndCancellationKeepTheDenominator()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1]; request.Jobs.Clear(); request.Jobs.Add(job);
        job.Plan.CoveragePolicy = BeamCoveragePolicy.RequiredLocations;
        job.Plan.Locations = new[] { .25, .5 }.Select(x => new RequiredBeamLocation { BeamId = 20, Station = x, StationDomain = "NodeToNode" }).ToArray();
        var report = new Service().Verify(model, request); Assert.AreEqual(2, report.Required); Assert.AreEqual(1, report.Executed);
        report = new Service().Verify(model, request, new CancellationToken(true));
        Assert.AreEqual(2, report.Required); Assert.AreEqual(2, report.Summary.Cancelled); Assert.AreEqual(0, report.CreatedCheckers);
    }
    [TestMethod]
    public void PersistedNativeDetailsRoundTripWithoutLiveCheckerObjects()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var report = new Service().Verify(model, request);
        Assert.AreEqual(4, report.Executed, Diagnostics(report));
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var saved = CheckReportArchive.Load(stream);
        Assert.IsTrue(saved.All(r => r.HasUnchangedScope && r.Results.All(v => v.HasUnchangedEvidence)));
        Assert.AreEqual(report.Outcome, Service.CurrentOutcome(saved, model, request));
        model.CheckReports.AddRange(saved); using var archive = new MemoryStream(); ModelArchive.Save(model, archive); archive.Position = 0;
        var copy = ModelArchive.Load(archive);
        Assert.AreEqual(4, copy.CheckReports.Count); Assert.IsTrue(copy.CheckReports.All(r => r.Outcome == EngineeringOutcome.Satisfied));
    }
    [TestMethod]
    public void ExternalConfigurationAndHistoryChangesInvalidateSavedResults()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var report = new Service().Verify(model, request);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, model, request));
        var steel = (SteelMaterialChecker)request.Jobs[1].Assignments[0].Checker; steel.Code.GammaM0 = 1.2;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report, model, request)); steel.Code.GammaM0 = 1;
        var bridge = (CompositeBridgeMaterialChecker)request.Jobs[2].Assignments[0].Checker;
        bridge.Cases[0].Source = "Modified history";
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report, model, request));
    }
    [TestMethod]
    public void BridgeRejectsMissingHistoryMismatchedForcesAndUnsupportedComponents()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[2]; request.Jobs.Clear(); request.Jobs.Add(job);
        var bridge = (CompositeBridgeMaterialChecker)job.Assignments[0].Checker; var binding = bridge.Cases[0];
        binding.HistoryAndReferenceConfirmed = false;
        Assert.AreEqual(0, new Service().Verify(model, request).Executed); binding.HistoryAndReferenceConfirmed = true;
        var original = binding.Input; binding.Input = original with { Phases = new[] { original.Phases[0] } };
        var result = new Service().Verify(model, request).Jobs[0].Results[0];
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "BridgeHistoryDoesNotMatchFemState")); binding.Input = original;
        var sample = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[30], MultiMaterialWorkflow.State("SLE")).Single(); sample.ResultBeamForces.M2 = 1;
        Assert.AreEqual(DataStatus.NotSupported, new Service().Verify(model, request).Jobs[0].Results[0].Data);
    }
    [TestMethod]
    public void RotatedSourceAxesPreserveNativeSteelDemand()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1]; request.Jobs.Clear(); request.Jobs.Add(job);
        var first = new Service().Verify(model, request).Jobs[0].Results[0];
        var sample = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[20], MultiMaterialWorkflow.State("ULS")).Single();
        var rotated = new CoordinateSystem(sample.ResultBeamForces.CoordinateSystem.Origin, new Vector3d(0, 1, 0), new Vector3d(-1, 0, 0), new Vector3d(0, 0, 1));
        var normalized = ResultOrientation.Beam(sample, rotated); sample.ResultBeamForces = normalized.ResultBeamForces;
        var second = new Service().Verify(model, request).Jobs[0].Results[0];
        Assert.AreEqual(first.Utilization, second.Utilization); Assert.AreEqual(first.Input.BeamForces.V2, second.Input.BeamForces.V2);
    }
    [TestMethod]
    public void SteelCapacitiesMatchIndependentArithmeticAndKnownFailureIsRetained()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1]; request.Jobs.Clear(); request.Jobs.Add(job);
        ((SteelMaterialChecker)job.Assignments[0].Checker).Code.GammaM0 = 1.1;
        var report = new Service().Verify(model, request); var metrics = report.Jobs[0].Results[0].Details.Metrics;
        // Stocky H, no fillets: Av1=2*150*18, Av2=12*(300-36)+12*18, fy=355 MPa.
        Assert.AreEqual(5400 * 355 / Math.Sqrt(3) / 1.1, metrics[0].Capacity!.Value, 1e-8);
        Assert.AreEqual(3384 * 355 / Math.Sqrt(3) / 1.1, metrics[1].Capacity!.Value, 1e-8);
        var sample = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[20], MultiMaterialWorkflow.State("ULS")).Single();
        sample.ResultBeamForces.V2 = 2 * metrics[1].Capacity.Value;
        report = new Service().Verify(model, request);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, report.Outcome); Assert.AreEqual(2, report.Jobs[0].Results[0].Utilization!.Value, 1e-12);
    }
    [TestMethod]
    public void BridgeOutputsMatchNativeApiForNtcAndEurocodeProfiles()
    {
        foreach (var code in new[] { BridgeStandard.Ntc2018, BridgeStandard.Eurocode4 })
        {
            var (model, request) = MultiMaterialWorkflow.Create(); var checker = (CompositeBridgeMaterialChecker)request.Jobs[2].Assignments[0].Checker;
            foreach (var binding in checker.Cases) binding.Input = binding.Input with { Options = binding.Input.Options with { Standard = code } };
            var replacement = new CompositeBridgeMaterialChecker(code, code == BridgeStandard.Ntc2018 ? "2018" : "2005", checker.Cases);
            request.Jobs[2].Assignments[0].Checker = replacement; request.Jobs[3].Assignments[0].Checker = replacement;
            var report = new Service().Verify(model, request); Assert.AreEqual(4, report.Executed, Diagnostics(report));
            var stress = HBridgeSection.Calculate(checker.Cases[0].Input).Stages.Last();
            Assert.AreEqual(stress.Points.Count(p => p.Active), report.Jobs[2].Results[0].Details.Metrics.Count);
            foreach (var p in stress.Points.Where(p => p.Active))
                Assert.AreEqual(p.Stress, report.Jobs[2].Results[0].Details.Metrics.Single(m => m.Key == p.Material + ":" + p.Name).Demand);
            var shear = HBridgeSection.Calculate(checker.Cases[1].Input).Stages.Last().Shear!.Checks[0];
            Assert.AreEqual(shear.Ratio, report.Jobs[3].Results[0].Utilization);
        }
    }
    private sealed class Progress : IProgress<VerificationProgress>
    {
        private readonly Action<VerificationProgress> _action;
        public Progress(Action<VerificationProgress> action) { _action = action; }
        public void Report(VerificationProgress p) => _action(p);
    }
    [TestMethod]
    public void CancellationAfterFirstCheckPreservesProgressAndAllOutcomes()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var cts = new CancellationTokenSource(); var progress = new List<VerificationProgress>();
        var report = new Service().Verify(model, request, cts.Token, new Progress(p => { progress.Add(p); cts.Cancel(); }));
        Assert.AreEqual(4, report.Required); Assert.AreEqual(1, report.Executed); Assert.AreEqual(3, report.Summary.Cancelled);
        Assert.AreEqual(4, progress.Last().Completed); Assert.AreEqual(4, progress.Last().Total); Assert.IsNotNull(progress.Last().Target);
    }
    [TestMethod]
    public void ConfigurationMutationDuringProgressInvalidatesEarlierResults()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var options = ((ConcreteMaterialChecker)request.Jobs[0].Assignments[0].Checker).Options;
        var report = new Service().Verify(model, request, progress: new Progress(p => { if (p.Completed == 1) options.AngularDivisions += 1; }));
        Assert.AreEqual(DataStatus.Stale, report.Jobs[0].Results[0].Data); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
    }
    [TestMethod]
    public void NeutralInputDetectsRemovedSamplesAndChangedDataset()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1];
        var prepared = BeamCheckPlan.PrepareActions(model, job.Plan); var input = prepared.WorkItems[0].Actions.Input;
        Assert.IsTrue(input.IsCurrent); model.Datasets["step5"].AnalysisId = "different run"; Assert.IsFalse(input.IsCurrent);
        prepared = BeamCheckPlan.PrepareActions(model, job.Plan); input = prepared.WorkItems[0].Actions.Input;
        model.BeamElements[20].Results.Clear(); Assert.IsFalse(input.IsCurrent); Assert.IsFalse(prepared.IsCurrent);
        input.Sample.State = null; Assert.IsFalse(input.IsCurrent);
    }
    [TestMethod]
    public void PhysicalSteelMemberDoesNotAcquireImplicitBucklingLengths()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1]; request.Jobs.Clear(); request.Jobs.Add(job);
        model.PhysicalMembers["S"] = new PhysicalMemberDefinition("S", new[] { new BeamMemberPart(20, true) }, "NodeToNode");
        job.Plan.MemberIds = new[] { "S" }; job.Plan.MemberChecks = new[] { new MemberCheckSpecification { MemberId = "S", MethodId = "steel-stability" } };
        var report = new Service().Verify(model, request);
        Assert.AreEqual(2, report.Required); Assert.AreEqual(1, report.Executed); Assert.AreEqual(1, report.Summary.Unsupported);
        Assert.IsNull(report.Jobs[0].Results.Last().MemberInput.Context.EffectiveLength1); Assert.AreEqual(1, report.Elements.Count);
    }
    private sealed class BrokenFactory : IMaterialChecker
    {
        public string Id => "Test-only broken factory"; public string Version => "1"; public string Configuration => "fixed";
        public CheckStandardContext Standard => new("fixture", "1", null, Id, Configuration);
        public int Calls;
        public bool Accepts(GPC.Model.Elements.BeamElement beam) => true;
        public IMaterialCheckSession CreateSession() { Calls++; throw new InvalidOperationException("Test failure"); }
    }
    [TestMethod]
    public void FactoryFailureIsCachedAndReportedForEveryRequestedCheck()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1]; request.Jobs.Clear(); request.Jobs.Add(job);
        job.Plan.SectionMechanisms = new[] { CheckMechanism.Shear, CheckMechanism.Stability }; var factory = new BrokenFactory();
        job.Assignments = new[] { new MaterialCheckerAssignment { Checker = factory } };
        var report = new Service().Verify(model, request);
        Assert.AreEqual(1, factory.Calls); Assert.AreEqual(2, report.Summary.Errors); Assert.AreEqual(0, report.Executed);
    }
    [TestMethod]
    public void MissingAndAmbiguousBridgeBindingsCannotChooseAFavourableHistory()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[2]; request.Jobs.Clear(); request.Jobs.Add(job);
        var binding = ((CompositeBridgeMaterialChecker)job.Assignments[0].Checker).Cases[0];
        job.Assignments[0].Checker = new CompositeBridgeMaterialChecker(BridgeStandard.Ntc2018, "2018", new[] { binding, binding });
        var result = new Service().Verify(model, request).Jobs[0].Results[0];
        Assert.AreEqual(DataStatus.Insufficient, result.Data); Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "MissingOrAmbiguousBridgeHistory"));
        job.Assignments[0].Checker = new CompositeBridgeMaterialChecker(BridgeStandard.Ntc2018, "future edition", new[] { binding });
        Assert.AreEqual(DataStatus.NotSupported, new Service().Verify(model, request).Jobs[0].Results[0].Data);
    }
    [TestMethod]
    public void CompositePropertyReinforcementInvalidatesPersistedScope()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var report = new Service().Verify(model, request);
        Assert.AreEqual(4, report.Executed, Diagnostics(report));
        var section = (GPC.Model.Sections.Concrete.ReinforcedConcreteSection)model.BeamElements[30].BeamProperty;
        section.Rebars.First().Position.Y += 1;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report, model, request));
    }
}
