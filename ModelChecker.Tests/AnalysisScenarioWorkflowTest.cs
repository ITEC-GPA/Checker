using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Persistence;
using GPC.Model.Sections.Concrete;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Analysis;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Scenarios;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Results.Queries;

namespace ModelChecker.Tests;

[TestClass]
public class AnalysisScenarioWorkflowTest
{
    [TestMethod]
    public void FrozenExecutionIsRepeatableAfterThePreparedGraphChanges()
    {
        var model = MixedModelFactory.Create();
        var prepared = VerificationPreparation.Prepare(model, VerificationScenario.Create(model.Analysis.Id, "Frozen"));
        var snapshot = global::GPC.Model.Checking.Scenarios.VerificationSnapshot.Capture(prepared);
        prepared.Model.NodesElements[40].Position.Z += 1;
        Assert.IsFalse(prepared.IsCurrent);
        var service = new Service(_ => new Verifier());
        var first = service.Verify(snapshot, Request());
        var second = service.Verify(snapshot, Request());
        Assert.AreEqual(5, first.Executed);
        Assert.AreEqual(EngineeringOutcome.Satisfied, first.Outcome);
        CollectionAssert.AreEqual(first.Jobs.Single().Results.Select(r => r.Utilization).ToArray(),
            second.Jobs.Single().Results.Select(r => r.Utilization).ToArray());
    }

    private static ModelCheckRequest Request()
    {
        var request = new ModelCheckRequest();
        request.Jobs.Add(new ModelCheckJob { Name = "scenario", Preparation = new PreparationRequest {
            Selection = new ElementSelection { Families = new[] { EntityFamily.Beam } },
            Results = new[] { new ResultSelection { Dataset = "synthetic-static", Case = "P+" } } } });
        return request;
    }
    private sealed class Verifier : IConfiguredSectionVerifier
    {
        public string Version => "scenario-test";
        public string Configuration => "constant";
        public IReadOnlyCollection<CheckMechanism> Capabilities { get; } = new[] { CheckMechanism.UlsBiaxialSection };
        public CheckResult Verify(BeamCheckInput input, CheckMechanism mechanism, CancellationToken token) => new() {
            Data = DataStatus.Ready, Execution = ExecutionStatus.Completed, Outcome = EngineeringOutcome.Satisfied, Utilization = .5 };
    }
    [TestMethod]
    public void CompatibleScenarioRunsAndSealsItsIdentitiesInStandaloneReports()
    {
        var model = MixedModelFactory.Create(); var scenario = VerificationScenario.Create(model.Analysis.Id, "Design B");
        var section = (ReinforcedConcreteSection)model.Analysis.OpenModel().BeamElements[250].BeamProperty;
        section.Rebars.First().Position.Y += 10; scenario.SetBeamDesign(model.BeamElements[250].Guid, section);
        var prepared = VerificationPreparation.Prepare(model, scenario);
        var report = new Service(_ => new Verifier()).Verify(prepared, Request());
        Assert.AreEqual(5, report.Executed); Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var copy = CheckReportArchive.Load(stream).Single(); var result = copy.Results.First();
        Assert.AreEqual(scenario.Id, result.Provenance.ScenarioId); Assert.AreEqual(model.Analysis.Id, result.Provenance.AnalysisSnapshotId);
        Assert.IsTrue(result.HasUnchangedEvidence); Assert.IsTrue(result.Provenance.IsCurrent(prepared.Model));
        Assert.AreEqual(EngineeringOutcome.Satisfied, copy.CurrentOutcome(prepared.Model, new Verifier()));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, copy.CurrentOutcome(model, new Verifier()));
        result.Provenance = null; Assert.IsFalse(result.HasUnchangedEvidence);
    }
    [TestMethod]
    public void UnknownOrPhysicalChangesDoNotCreateConcreteCheckers()
    {
        foreach (bool physical in new[] { false, true })
        {
            var model = MixedModelFactory.Create(reinforcementRole: ReinforcementAnalysisRole.Unknown);
            if (physical) model.NodesElements[40].Position.Z += 1;
            else ((ReinforcedConcreteSection)model.BeamElements[250].BeamProperty).Rebars.First().Position.Y += 1;
            int factories = 0; var report = new Service(_ => { factories++; return new Verifier(); }).Verify(model, Request());
            Assert.AreEqual(0, factories); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
            Assert.AreEqual(physical ? "RequiresReanalysis" : "UnknownReinforcementInfluence", report.Jobs[0].Results[0].Diagnostics[0].Code);
        }
    }
    [TestMethod]
    public void MultiMaterialEntryAlsoRejectsAnIncompatibleScenarioBeforeNativeExecution()
    {
        var (model, request) = MultiMaterialWorkflow.Create();
        var scenario = VerificationScenario.Create(model.Analysis.Id, "Alternative");
        var prepared = VerificationPreparation.Prepare(model, scenario);
        prepared.Model.NodesElements.Values.First().Position.Z += 1;
        var report = new Service().Verify(prepared, request);
        Assert.AreEqual(0, report.CreatedCheckers); Assert.AreEqual(0, report.Executed);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
    }
    [TestMethod]
    public void LegacyModelRequiresHistoricalEvidenceBeforeAutomaticChecking()
    {
        var legacy = new GPC.Model.Models.Model("legacy"); int factories = 0;
        var report = new Service(_ => { factories++; return new Verifier(); }).Verify(legacy, Request());
        Assert.AreEqual(0, factories); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.AreEqual("UnknownAnalysisProvenance", report.Jobs[0].Results[0].Diagnostics[0].Code);
    }
}
