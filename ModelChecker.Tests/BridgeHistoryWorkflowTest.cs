using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Processing;
using GPC.Model.Results.Queries;

namespace ModelChecker.Tests;

[TestClass]
public class BridgeHistoryWorkflowTest
{
    private static (GPC.Model.Models.Model Model, MultiMaterialCheckRequest Request, CompositeBridgeMaterialChecker Checker) Setup(BridgeHistoryDefinition? options = null)
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[2];
        var old = (CompositeBridgeMaterialChecker)job.Assignments[0].Checker;
        var checker = new CompositeBridgeMaterialChecker(old.Code, old.Edition, old.Cases, null, options ?? new());
        job.Assignments[0].Checker = checker; request.Jobs.Clear(); request.Jobs.Add(job);
        return (model, request, checker);
    }
    [DataTestMethod]
    [DataRow(BridgeStandard.Ntc2018)] [DataRow(BridgeStandard.Eurocode4)]
    public void LinearHistoryPreservesNativeStressResultsAndAllPhaseConvergenceEvidence(BridgeStandard code)
    {
        var (model, request, checker) = Setup();
        foreach (var c in checker.Cases) c.Input = c.Input with { Options = c.Input.Options with { Standard = code } };
        checker = new CompositeBridgeMaterialChecker(code, code == BridgeStandard.Ntc2018 ? "2018" : "2005", checker.Cases, null, checker.History);
        request.Jobs[0].Assignments[0].Checker = checker;
        var result = new Service().Verify(model, request).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, string.Join(";", result.Diagnostics.Select(d => d.Message ?? d.Code)));
        var native = HBridgeHistoryResults.Calculate(checker.Cases[0].Input, checker.History.CreateOptions());
        var points = native.Stages.Last().Points.Where(p => p.Active).ToArray();
        Assert.AreEqual(points.Length, result.Details.Metrics.Count);
        for (int i = 0; i < points.Length; i++) Assert.AreEqual(points[i].Stress, result.Details.Metrics[i].Demand!.Value, 1e-10);
        Assert.IsTrue(result.Details.Convergence.Converged);
        Assert.AreEqual(native.Stages.Count, result.Details.Trace.Count(t => t.Key.StartsWith("ForceResidual@")));
        Assert.AreEqual(result.Details.Metrics.Count, result.Details.Metrics.Select(m => m.Key).Distinct().Count());
        Assert.IsTrue(result.Details.Convergence.Residual <= 1);
    }

    [TestMethod]
    public void HistoryRetainsConstructionOrderDespiteEqualFinalActionsAndSurvivesTheArchive()
    {
        var (model, request, checker) = Setup();
        var first = new Service().Verify(model, request);
        var initialStress = first.Jobs[0].Results[0].Details.Metrics[0].Demand;
        var binding = checker.Cases[0];
        binding.Input = binding.Input with { Phases = binding.Input.Phases.Select((p, i) => i == 0 ? p with { Kind = BridgePhaseKind.Composite } : p).ToArray() };
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(first, model, request));
        var second = new Service().Verify(model, request);
        Assert.AreEqual(EngineeringOutcome.Satisfied, second.Outcome);
        Assert.AreNotEqual(initialStress, second.Jobs[0].Results[0].Details.Metrics[0].Demand);
        var sample = global::GPC.Model.Results.Queries.ResultQueries.Samples<GPC.Model.Results.Locations.StationResultBeamForces>(model.BeamElements[30], MultiMaterialWorkflow.State("SLE")).Single();
        var axes = new GPC.Geometry.CoordinateSystem(sample.ResultBeamForces.CoordinateSystem.Origin, new GPC.Geometry.Vector3d(0, 1, 0), new GPC.Geometry.Vector3d(-1, 0, 0));
        sample.ResultBeamForces = ResultOrientation.Beam(sample, axes).ResultBeamForces;
        var rotated = new Service().Verify(model, request);
        Assert.AreEqual(second.Jobs[0].Results[0].Details.Metrics[0].Demand, rotated.Jobs[0].Results[0].Details.Metrics[0].Demand);
        using var stream = new MemoryStream(); GPC.Model.Persistence.CheckReportArchive.Save(rotated.Jobs, stream); stream.Position = 0;
        var restored = GPC.Model.Persistence.CheckReportArchive.Load(stream);
        Assert.AreEqual(rotated.Jobs[0].Results[0].Details.Trace.Count, restored[0].Results[0].Details.Trace.Count);
        Assert.IsTrue(restored[0].Results[0].Details.Convergence.Converged);
        Assert.AreEqual(rotated.Outcome, Service.CurrentOutcome(restored, model, request));
    }

    [TestMethod]
    public void HistorySettingsAreCopiedPersistedAndAChangeInvalidatesTheReport()
    {
        var (model, initial) = MultiMaterialWorkflow.Create(); var config = ConfiguredPlateWorkflowTest.BeamConfiguration(initial);
        var definition = config.Engines[2]; definition.Kind = "CompositeBridge.HistoryLinear";
        definition.BridgeHistory = new() { WebLayers = 80, SubstepsPerPhase = 2, ForceTolerance = .001 };
        config.Jobs = new[] { config.Jobs[2] };
        var catalog = EngineCatalog.BuiltIn(); var restored = ConfigurationArchive.Copy(config);
        Assert.IsTrue(catalog.Supports(restored.Engines[2], restored.Contexts[2], CheckMechanism.Serviceability));
        Assert.IsFalse(catalog.Supports(restored.Engines[2], restored.Contexts[2], CheckMechanism.Shear));
        var compiled = catalog.Compile(restored); var checker = (CompositeBridgeMaterialChecker)compiled.Jobs[0].Assignments[0].Checker;
        Assert.AreEqual(80, checker.History.WebLayers); Assert.AreEqual(.001, checker.History.ForceTolerance);
        string before = checker.Configuration;
        definition.BridgeHistory.WebLayers = 100; checker.History.WebLayers = 120;
        Assert.AreEqual(before, checker.Configuration, "Compiled settings and exported copies do not share mutable values.");
        var service = new Service(); var report = service.Verify(model, config, catalog);
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        config.Engines[2].BridgeHistory.SubstepsPerPhase = 3;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report, model, config, catalog));
        restored.Engines[2].Kind = "CompositeBridge.Section";
        Assert.ThrowsException<ArgumentException>(() => catalog.Compile(restored));
    }

    [TestMethod]
    public void MissingConvergenceHasNoUtilizationAndReportsTheFailingPhase()
    {
        var (model, request, _) = Setup(new() { MaximumNewtonIterations = 1, RelativeTolerance = 0, ForceTolerance = 1e-30, MomentTolerance = 1e-30 });
        var result = new Service().Verify(model, request).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome); Assert.IsNull(result.Utilization);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "BridgeHistoryNotConverged"));
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "BridgeHistoryFailureLocation"));
    }

    [DataTestMethod]
    [DataRow(CheckMechanism.Shear)] [DataRow(CheckMechanism.UlsBiaxialSection)] [DataRow(CheckMechanism.Stability)]
    public void HistoryDoesNotInventUnsupportedResistances(CheckMechanism mechanism)
    {
        var (model, request, _) = Setup(); request.Jobs[0].Plan.SectionMechanisms = new[] { mechanism };
        var result = new Service().Verify(model, request).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome); Assert.AreEqual(DataStatus.NotSupported, result.Data);
    }

    [TestMethod]
    public void InvalidNumericalSettingsAreRejectedBeforeExecution()
    {
        Assert.ThrowsException<ArgumentException>(() => Setup(new() { ForceTolerance = double.NaN }));
        Assert.ThrowsException<ArgumentException>(() => Setup(new() { WebLayers = 0 }));
        Assert.ThrowsException<ArgumentException>(() => Setup(new() { RelativeTolerance = -1 }));
    }
}
