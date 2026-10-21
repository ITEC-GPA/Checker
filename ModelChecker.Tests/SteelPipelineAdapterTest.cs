using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Compatibility;
using GPC.Model.Core;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Data.Steel;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class SteelPipelineAdapterTest
{
    private static BeamActionInput Input()
    {
        var section = new SteelSection(new SectionH(300, 12, 150, 18, 150, 18, "H300"), SteelMaterialEN1993Data.S355);
        var model = MixedModelFactory.Create(m => {
            var beam = m.BeamElements[250]; beam.BeamProperty = section;
            beam.Assignments.Sections.Clear();
            beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Property = section });
        });
        var sample = Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified);
        var prepared = BeamActionPreparation.Prepare(model, 250, sample, "Steel pipeline");
        Assert.AreEqual(DataStatus.Ready, prepared.Status);
        var input = prepared.Input;
        input.Forces.N = input.Forces.V1 = input.Forces.V2 = input.Forces.T = input.Forces.M1 = input.Forces.M2 = 0;
        return input;
    }
    private static Dictionary<string, string> Options() => new() { ["Unperforated"] = "true" };
    private static SteelPipelineMaterialChecker Engine(Dictionary<string, string>? options = null, bool ntc = false)
        => new(ntc ? new StandardNTC2018Steel() : new StandardEN1993p11(), ntc ? "2018" : "2005", parameters: options ?? Options());
    private static CheckResult Run(BeamActionInput input, Dictionary<string, string>? options = null, bool ntc = false)
        => Engine(options, ntc).CreateSession().Verify(input, CheckMechanism.UlsBiaxialSection, default);
    private static void Bind(Dictionary<string, string> options, BeamActionInput input)
        => options["SectionFingerprint"] = ModelValues.Fingerprint(new object[] { input.Property });

    [DataTestMethod]
    [DataRow(-.4)] [DataRow(.4)] [DataRow(-1.2)] [DataRow(1.2)]
    public void AxialResistanceRetainsSignedDemandAndIndependentCapacity(double ratio)
    {
        var input = Input(); double capacity = (2 * 150 * 18 + 264 * 12) * 355;
        input.Forces.N = ratio * capacity;
        var result = Run(input);
        Assert.AreEqual(Math.Abs(ratio) <= 1 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied, result.Outcome);
        Assert.AreEqual(Math.Abs(ratio), result.Utilization!.Value, 1e-12);
        var metric = result.Details.Metrics.Single();
        Assert.AreEqual(ratio * capacity, metric.Demand); Assert.AreEqual(capacity, metric.Capacity);
        Assert.AreEqual(4, result.Details.Trace.Count(t => t.Key.EndsWith(".Status")));
    }

    [TestMethod]
    public void DefaultDoesNotInventTheAbsenceOfHoles()
    {
        var input = Input(); input.Forces.N = 1e5;
        var result = Run(input, new());
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
        Assert.AreEqual(DataStatus.Insufficient, result.Data);
    }

    [DataTestMethod]
    [DataRow("zero")] [DataRow("torsion")] [DataRow("two shears")] [DataRow("N-V")]
    public void UncoveredInventoryCannotBeReportedAsACompletePass(string kind)
    {
        var input = Input();
        if (kind == "torsion") { input.Forces.N = 1e4; input.Forces.T = 1; }
        if (kind == "two shears") input.Forces.V1 = input.Forces.V2 = 1e3;
        if (kind == "N-V") { input.Forces.N = -1e4; input.Forces.V2 = 1e3; }
        var result = Run(input);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
        Assert.IsNull(result.Utilization);
        Assert.AreEqual(0d, result.Details.Trace.Single(t => t.Key == "Pipeline.IsComplete").Value);
    }

    [TestMethod]
    public void PartialFailureRemainsVisibleWithoutClaimingCompleteEvaluation()
    {
        var input = Input(); input.Forces.N = -1e4; input.Forces.V2 = 1e8;
        var result = Run(input);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
        Assert.IsFalse(result.Details.Metrics.Single().Passed);
        Assert.AreEqual("NotSatisfied", result.Details.Trace.Single(t => t.Key == "Pipeline.Outcome").Expression);
    }

    [TestMethod]
    public void HWebAssessmentIsTracedAndDifferentMethodRatiosAreNotAggregated()
    {
        var input = Input(); input.Forces.M1 = 1e6; input.Forces.V2 = 1e3;
        var result = Run(input);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        Assert.AreEqual(2, result.Details.Metrics.Count); Assert.IsNull(result.Utilization);
        Assert.IsTrue(result.Details.Trace.Any(t => t.Key.EndsWith(".ClearWebHeight") && t.Value == 264));
        Assert.AreEqual(result.Details.Trace.Count, result.Details.Trace.Select(t => t.Key).Distinct().Count());
    }

    [DataTestMethod]
    [DataRow(true)] [DataRow(false)]
    public void ExternalShearAssessmentIsExplicitBoundAndCannotInventResistance(bool excluded)
    {
        var input = Input(); input.Forces.M2 = 1e6; input.Forces.V1 = 1e3;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Run(input).Outcome);
        var options = Options(); Bind(options, input);
        options["ShearBucklingExcluded1"] = excluded.ToString(); options["ShearBucklingSource1"] = "Independent section assessment A, rev 2";
        var result = Run(input, options);
        Assert.AreEqual(excluded ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotEvaluated, result.Outcome);
        Assert.IsTrue(result.Details.Trace.Any(t => t.Expression == options["ShearBucklingSource1"]));
        options["SectionFingerprint"] = "different-section";
        Assert.AreEqual(DataStatus.Insufficient, Run(input, options).Data);
    }

    [TestMethod]
    public void NtcClassificationCannotLeakFromTheEc3Policy()
    {
        var input = Input(); input.Forces.N = -1e4;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Run(input, ntc: true).Outcome);
        var options = Options(); Bind(options, input);
        options["UniformCompressionClass"] = "1"; options["ClassificationSource"] = "NTC uniform-compression section assessment A";
        var result = Run(input, options, true);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
        Assert.AreEqual(8568 * 355 / 1.05, result.Details.Metrics.Single().Capacity!.Value, 1e-7);
        Assert.IsTrue(result.Details.Trace.Any(t => t.Expression != null && t.Expression.Contains(options["ClassificationSource"])));
    }

    [TestMethod]
    public void CacheKeepsGeometryButNeverCachesActionResultsAndConfigurationIsCopied()
    {
        var input = Input(); input.Forces.N = 1e4; var options = Options();
        var engine = Engine(options); var session = engine.CreateSession();
        options["Unperforated"] = "false";
        double first = session.Verify(input, CheckMechanism.UlsBiaxialSection, default).Utilization!.Value;
        input.Forces.N *= 2;
        Assert.AreEqual(2 * first, session.Verify(input, CheckMechanism.UlsBiaxialSection, default).Utilization!.Value, 1e-12);
        Assert.AreEqual(1, session.CreatedCheckers);
        engine.Code.GammaM0 = 1.1;
        Assert.AreEqual(2.2 * first, session.Verify(input, CheckMechanism.UlsBiaxialSection, default).Utilization!.Value, 1e-12);
        Assert.AreEqual(2, session.CreatedCheckers);
    }

    [TestMethod]
    public void InvalidUsedShearFactorBlocksWhileUnusedOneDoesNot()
    {
        var input = Input(); input.Forces.N = 1e4; var engine = Engine();
        engine.Code.NShearBucklingLowGradeOfSteel = double.NaN;
        Assert.AreEqual(EngineeringOutcome.Satisfied, engine.CreateSession().Verify(input, CheckMechanism.UlsBiaxialSection, default).Outcome);
        input.Forces.N = 0; input.Forces.M1 = 1e6; input.Forces.V2 = 1e3;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, engine.CreateSession().Verify(input, CheckMechanism.UlsBiaxialSection, default).Outcome);
    }

    [DataTestMethod]
    [DataRow("typo", "1")] [DataRow("NetArea", "NaN")] [DataRow("UniformCompressionClass", "5")]
    [DataRow("UniformCompressionClass", "2")] [DataRow("ShearBucklingExcluded1", "true")]
    public void IncompleteOrUnknownParametersAreRejected(string key, string value)
        => Assert.ThrowsException<ArgumentException>(() => Engine(new() { [key] = value }));

    [TestMethod]
    public void CatalogueAndCompleteModelReportRoundTripRetainPipelineEvidence()
    {
        var (model, request) = MultiMaterialWorkflow.Create();
        var config = ConfiguredPlateWorkflowTest.BeamConfiguration(request);
        var definition = config.Engines[1]; var context = config.Contexts[1];
        definition.Kind = SteelPipelineMaterialChecker.EngineId; definition.Parameters = Options();
        var catalog = EngineCatalog.BuiltIn();
        Assert.IsTrue(catalog.Supports(definition, context, CheckMechanism.UlsBiaxialSection));
        Assert.IsFalse(catalog.Supports(definition, context, CheckMechanism.Stability));
        var compiled = catalog.Compile(ConfigurationArchive.Copy(config));
        var job = compiled.Jobs[1]; job.Plan.SectionMechanisms = new[] { CheckMechanism.UlsBiaxialSection };
        job.Assignments[0].Mechanisms = new[] { CheckMechanism.UlsBiaxialSection };
        compiled.Jobs.Clear(); compiled.Jobs.Add(job);
        var forces = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[20], MultiMaterialWorkflow.State("ULS")).Single().ResultBeamForces;
        forces.N = 1e4; forces.V1 = forces.V2 = forces.T = forces.M1 = forces.M2 = 0;
        var report = new Service().Verify(model, compiled);
        var before = report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied, before.Outcome, string.Join(";", before.Diagnostics.Select(d => d.Message ?? d.Code)));
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var after = CheckReportArchive.Load(stream)[0].Results.Single();
        Assert.AreEqual(before.Details.Trace.Count, after.Details.Trace.Count);
        Assert.AreEqual(before.Details.Metrics[0].Reference, after.Details.Metrics[0].Reference);
        Assert.AreEqual(before.Utilization, after.Utilization);
        Assert.AreEqual(SteelPipelineMaterialChecker.EngineId, after.Details.MethodId);
    }

    [TestMethod]
    public void CancellationPropagatesWithoutReturningAnEngineeringResult()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => Engine().CreateSession().Verify(Input(), CheckMechanism.UlsBiaxialSection, source.Token));
    }
}
