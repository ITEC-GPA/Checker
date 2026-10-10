using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Checking.Contracts;

namespace ModelChecker.Tests;
[TestClass]
public class ConfiguredCalculationTest
{
    private static (GPC.Model.Models.Model model, VerificationConfiguration config, CalculationFactoryTest.Alternative engine, EngineCatalog catalog) Setup()
    {
        var (model, request) = MultiMaterialWorkflow.Create();
        var config = ConfiguredPlateWorkflowTest.BeamConfiguration(request);
        config.Jobs = new[] { config.Jobs[0] }; config.Engines = new[] { config.Engines[0] }; config.Contexts = new[] { config.Contexts[0] };
        var engine = new CalculationFactoryTest.Alternative(); var settings = config.Engines[0].Concrete;
        settings.Criterion = GPC.Checkers.Concrete.SectionSolvers.SectionSolver.FailureAnalysisTypes.ConstantN;
        settings.CalculationEngineId = engine.Id; settings.CalculationEngineVersion = engine.Version; settings.CalculationEngineConfiguration = engine.Configuration;
        var numerical = new ConcreteCalculationCatalog(); numerical.Register(engine);
        return (model, config, engine, EngineCatalog.BuiltIn(numerical));
    }
    [TestMethod]
    public void SavedSelectionRunsRegisteredEngineAndRetainsProvenance()
    {
        var f = Setup(); var copy = ConfigurationArchive.Copy(f.config);
        Assert.AreEqual(ConfigurationArchive.Fingerprint(f.config), ConfigurationArchive.Fingerprint(copy));
        var report = new Service().Verify(f.model, copy, f.catalog);
        Assert.AreEqual(1, f.engine.Calls); Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        StringAssert.Contains(report.Jobs.Single().Results.Single().EngineConfiguration, "NumericalEngineVersion=test1");
        Assert.AreEqual(report.Outcome, Service.CurrentOutcome(report, f.model, copy, f.catalog));
        copy.Engines[0].Concrete.CalculationEngineVersion = "different";
        Assert.ThrowsException<InvalidOperationException>(() => f.catalog.Compile(copy));
    }
    [TestMethod]
    public void UnknownUnpinnedAndChangedEngineAreRejectedBeforeExecution()
    {
        var f = Setup();
        Assert.ThrowsException<NotSupportedException>(() => EngineCatalog.BuiltIn().Compile(f.config));
        var settings = f.config.Engines[0].Concrete; settings.CalculationEngineVersion = null;
        Assert.ThrowsException<ArgumentException>(() => f.catalog.Compile(f.config));
        settings.CalculationEngineVersion = f.engine.Version; settings.CalculationEngineConfiguration = "changed";
        Assert.ThrowsException<InvalidOperationException>(() => f.catalog.Compile(f.config));
        settings.CalculationEngineConfiguration = f.engine.Configuration; f.engine.RuntimeVersion = "new";
        Assert.ThrowsException<InvalidOperationException>(() => f.catalog.Compile(f.config));
        Assert.AreEqual(0, f.engine.Calls);
    }
    [TestMethod]
    public void MissingEquilibriumCannotBeReportedAsSuccessfulConstantNResistance()
    {
        var f = Setup(); f.engine.OmitEquilibrium = true;
        var report = new Service().Verify(f.model, f.config, f.catalog);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.IsTrue(report.Jobs.Single().Results.Single().Diagnostics.Any(d => d.Code == "NumericalResistanceConvergenceContractMismatch"));
    }
    [TestMethod]
    public void EngineChangesAfterCompilationAreRejected()
    {
        var f = Setup(); var request = f.catalog.Compile(f.config); f.engine.RuntimeVersion = "changed-after-compile";
        var report = new Service().Verify(f.model, request);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome); Assert.AreEqual(0, f.engine.Calls);
    }
}
