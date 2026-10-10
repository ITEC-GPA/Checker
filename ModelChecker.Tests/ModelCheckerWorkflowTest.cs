using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Elements;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Compatibility;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Results.Queries;

namespace ModelChecker.Tests;

[TestClass]
public class ModelCheckerWorkflowTest
{
    private static ModelCheckRequest Request(bool mixed = false)
    {
        var request = new ModelCheckRequest();
        request.Jobs.Add(new ModelCheckJob { Name = "ULS", Options = new ConcreteVerificationOptions { Standard = new GPC.Model.Standards.StandardNTC2018Concrete(),
            Criterion = GPC.Checkers.Concrete.SectionSolvers.SectionSolver.FailureAnalysisTypes.ConstantEccentricity },
            Preparation = new PreparationRequest { Selection = new ElementSelection { Families = mixed ? new[] { EntityFamily.Beam, EntityFamily.Shell } : new[] { EntityFamily.Beam } },
                Results = new[] { new ResultSelection { Dataset = "synthetic-static", Case = "P+" } }, Settings = "integration fixture" } });
        return request;
    }
    private sealed class TestVerifier : IConfiguredSectionVerifier
    {
        public string Version => "test";
        public string Configuration => "fixed fixture";
        public IReadOnlyCollection<CheckMechanism> Capabilities { get; } = new[] { CheckMechanism.UlsBiaxialSection };
        public Action? DuringCheck { get; set; }
        public CheckResult Verify(BeamCheckInput input, CheckMechanism mechanism, CancellationToken token)
        { DuringCheck?.Invoke(); return new CheckResult { Data = DataStatus.Ready, Execution = ExecutionStatus.Completed, Outcome = EngineeringOutcome.Satisfied, Utilization = .5 }; }
    }
    private sealed class ImmediateProgress : IProgress<VerificationProgress>
    {
        private readonly Action<VerificationProgress> action;
        public ImmediateProgress(Action<VerificationProgress> action) { this.action = action; }
        public void Report(VerificationProgress value) => action(value);
    }

    [TestMethod]
    public void RealChecker_IsReusedForAllStations_AndMatchesFreshInstance()
    {
        var model = MixedModelFactory.Create(); var request = Request();
        var report = new Service().Verify(model, request);
        Assert.AreEqual(5, report.Required); Assert.AreEqual(1, report.CreatedCheckers);
        var midpoint = report.Jobs.Single().Results.Single(r => r.Station == .5);
        Assert.AreEqual(ExecutionStatus.Completed, midpoint.Execution, string.Join(";", midpoint.Diagnostics.Select(d => d.Message)));
        var fresh = request.Jobs[0].Options.CreateVerifier();
        var prepared = Verification.PrepareBeam(model, 250, Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified), "integration fixture");
        var independent = Verification.Run(prepared, CheckMechanism.UlsBiaxialSection, fresh);
        Assert.AreEqual(independent.Utilization!.Value, midpoint.Utilization!.Value, 1e-10);
        Assert.AreEqual(typeof(GPC.Checkers.Concrete.Checkers.SectionChecker).Assembly.GetName().Version!.ToString(), midpoint.EngineVersion);
    }

    [TestMethod]
    public void RealChecker_SourceEditsDoNotMutateTheCachedSessionForAnEquivalentModel()
    {
        var first = MixedModelFactory.Create(); var second = MixedModelFactory.Create();
        var adapter = Request().Jobs[0].Options.CreateVerifier();
        BeamPreparation Prepare(GPC.Model.Models.Model model) => Verification.PrepareBeam(model, 250,
            Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified), "cache fixture");
        var original = Verification.Run(Prepare(first), CheckMechanism.UlsBiaxialSection, adapter);
        first.BeamElements[250].Assignments.Sections[0].Section.Rebars.First().Position.Y += 10;
        var result = Verification.Run(Prepare(second), CheckMechanism.UlsBiaxialSection, adapter);
        Assert.AreEqual(1, adapter.CreatedCheckers); Assert.AreEqual(ExecutionStatus.Completed, result.Execution);
        Assert.AreEqual(original.Utilization!.Value, result.Utilization!.Value, 1e-10);
        var changed = Verification.Run(Prepare(first), CheckMechanism.UlsBiaxialSection, adapter);
        Assert.AreEqual(2, adapter.CreatedCheckers); Assert.AreEqual(ExecutionStatus.Completed, changed.Execution);
        Assert.AreNotEqual(original.Utilization, changed.Utilization);
    }

    [TestMethod]
    public void RealChecker_DifferentJobConfiguration_CreatesSeparateInstances()
    {
        var model = MixedModelFactory.Create(); var request = Request(); var second = Request().Jobs[0];
        second.Name = "Different discretization"; second.Options.AngularDivisions = 32; request.Jobs.Add(second);
        var report = new Service().Verify(model, request);
        Assert.AreEqual(10, report.Required); Assert.AreEqual(2, report.CreatedCheckers);
        Assert.AreNotEqual(report.Jobs[0].Results[0].EngineConfiguration, report.Jobs[1].Results[0].EngineConfiguration);
    }

    [TestMethod]
    public void MixedModel_UnsupportedShellsRemainRequired_NoFalseAggregatePass()
    {
        var model = MixedModelFactory.Create(); var verifier = new TestVerifier();
        var report = new Service(_ => verifier).Verify(model, Request(true));
        Assert.AreEqual(7, report.Required); Assert.AreEqual(5, report.Executed); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        var shells = report.Jobs[0].Results.Where(r => r.Family == EntityFamily.Shell).ToArray();
        Assert.AreEqual(2, shells.Length); Assert.IsTrue(shells.All(r => r.Data == DataStatus.NotSupported && r.ShellPoint != null));
        Assert.IsTrue(shells.All(r => r.Diagnostics.Any(d => d.Code == "ShellCheckerUnavailable")));
    }

    [TestMethod]
    public void GroupReports_PersistAndBecomeStaleWhenSelectionChanges()
    {
        var model = MixedModelFactory.Create(); model.AddGroup("Beam selection"); model.AssignGroup("Beam selection", new Element[] { model.BeamElements[250] });
        var request = Request(); request.Jobs[0].Preparation.Selection.Groups = new[] { "Beam selection" };
        var verifier = new TestVerifier(); var report = new Service(_ => verifier).Verify(model, request);
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.CurrentOutcome(model, _ => verifier));
        model.CheckReports.AddRange(report.Jobs);
        using var stream = new MemoryStream(); ModelArchive.Save(model, stream); stream.Position = 0; var restored = ModelArchive.Load(stream);
        Assert.AreEqual(EngineeringOutcome.Satisfied, restored.CheckReports.Single().CurrentOutcome(restored, verifier));
        model.UnassignGroup("Beam selection", new Element[] { model.BeamElements[250] });
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.CurrentOutcome(model, _ => verifier));
    }

    [TestMethod]
    public void CancellationAfterFirstCheck_PreservesEveryRequiredOutcomeAndProgress()
    {
        var model = MixedModelFactory.Create(); var cancel = new CancellationTokenSource(); var events = new List<VerificationProgress>();
        var report = new Service(_ => new TestVerifier()).Verify(model, Request(), cancel.Token, new ImmediateProgress(p => { events.Add(p); cancel.Cancel(); }));
        Assert.AreEqual(5, report.Required); Assert.AreEqual(1, report.Executed); Assert.AreEqual(4, report.Jobs[0].Results.Count(r => r.Execution == ExecutionStatus.Cancelled));
        Assert.AreEqual(5, events.Last().Completed); Assert.AreEqual(5, events.Last().Total);
    }

    [TestMethod]
    public void MultipleJobs_KeepSettingsMechanismsAndMissingDatasetProvenance()
    {
        var model = MixedModelFactory.Create(); var request = Request();
        request.Jobs.Add(new ModelCheckJob { Name = "Second", Preparation = request.Jobs[0].Preparation.Copy() });
        request.Jobs[1].Preparation.Results[0].Dataset = "missing"; request.Jobs[1].Preparation.Mechanisms = new[] { CheckMechanism.Shear, CheckMechanism.Torsion };
        var report = new Service(_ => new TestVerifier()).Verify(model, request);
        Assert.AreEqual(7, report.Required); Assert.AreEqual(2, report.Jobs[1].Results.Count);
        Assert.IsTrue(report.Jobs[1].Results.All(r => r.Dataset == "missing" && r.Job == "Second" && r.Data == DataStatus.Insufficient));
    }

    [TestMethod]
    public void MutationDuringCheckAndFactoryFailure_DoNotReportSuccess()
    {
        var model = MixedModelFactory.Create(); var verifier = new TestVerifier { DuringCheck = () => model.NodesElements[40].Position.Z += 1 };
        var stale = new Service(_ => verifier).Verify(model, Request());
        Assert.AreEqual(0, stale.Executed); Assert.IsTrue(stale.Jobs[0].Results.All(r => r.Data == DataStatus.Stale));
        var failed = new Service(_ => throw new InvalidOperationException("factory test failure")).Verify(MixedModelFactory.Create(), Request());
        Assert.AreEqual(0, failed.Executed); Assert.IsTrue(failed.Jobs[0].Results.All(r => r.Execution == ExecutionStatus.Error));
    }
}
