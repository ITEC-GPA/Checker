using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Standards;
using GPC.Model.Structure.Members;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class SteelPipelineWorkflowTest
{
    private static VerificationConfiguration Configuration(BeamCheckPlanRequest plan, Dictionary<string, string> memberOptions, bool sections = false)
    {
        plan = plan.Copy();
        if (sections) plan.SectionMechanisms = new[] { CheckMechanism.UlsBiaxialSection };
        return new VerificationConfiguration {
            Contexts = new[] { new DesignContextDefinition { Id = "ec3", Edition = "2005", Code = new StandardEN1993p11() } },
            Engines = new[] {
                new EngineDefinition { Id = "member", Kind = SteelMemberPipelineChecker.EngineId, ContextId = "ec3", Parameters = memberOptions },
                new EngineDefinition { Id = "section", Kind = SteelPipelineMaterialChecker.EngineId, ContextId = "ec3", Parameters = new() { ["Unperforated"] = "true" } } },
            Jobs = new[] { new ConfiguredCheckJob { Name = "Steel workflow", Plan = plan,
                Routes = new[] {
                    new CheckRouteDefinition { EngineId = "member", Mechanisms = new[] { CheckMechanism.Stability } },
                    new CheckRouteDefinition { EngineId = "section", Mechanisms = new[] { CheckMechanism.UlsBiaxialSection } } } } }
        };
    }
    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void SavedConfigurationRunsWholeWorkflowAndSavedReportRetainsEvidence(bool external)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture();
        var config = Configuration(plan, SteelMemberPipelineTest.Options(model, plan, external), sections: true);
        var catalog = EngineCatalog.BuiltIn();
        Assert.IsTrue(catalog.Supports(config.Engines[0], config.Contexts[0], CheckMechanism.Stability));
        Assert.IsFalse(catalog.Supports(config.Engines[0], config.Contexts[0], CheckMechanism.UlsBiaxialSection));
        var copy = ConfigurationArchive.Copy(config); var request = catalog.Compile(copy);
        var report = new Service().Verify(model, request);
        Assert.AreEqual(6, report.Required); Assert.AreEqual(6, report.Executed);
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome, string.Join(";", report.Jobs[0].Results.SelectMany(r => r.Diagnostics).Select(d => d.Message ?? d.Code)));
        Assert.AreEqual(5, report.Jobs[0].Results.Count(r => r.Scope == CheckScope.SectionSample));
        Assert.AreEqual(1, report.CreatedCheckers); // one section snapshot, independent of five cut actions
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var restored = CheckReportArchive.Load(stream);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(restored, model, request));
        var member = restored[0].Results.Single(r => r.Scope == CheckScope.PhysicalMember);
        Assert.IsTrue(member.HasUnchangedEvidence); Assert.AreEqual(5, member.MemberInput.Samples.Count);
        Assert.AreEqual(SteelMemberPipelineChecker.EngineId, member.MethodId);
        Assert.IsTrue(member.Details.Trace.Any(v => v.Key == "Member.LoadPattern"));
        Assert.IsTrue(member.Details.Trace.All(v => !v.Value.HasValue || double.IsFinite(v.Value.Value)));
        copy.Engines[0].Parameters["ParameterSet"] = "changed engineering assumptions";
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(restored, model, catalog.Compile(copy)));
    }

    [TestMethod]
    public void IncompleteMemberCannotBeHiddenByFivePassingSectionChecks()
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(); var options = SteelMemberPipelineTest.Options(model, plan, true);
        foreach (var suffix in new[] { ".Value", ".Unit", ".Source" }) options.Remove("AlphaUltK" + suffix);
        var request = EngineCatalog.BuiltIn().Compile(Configuration(plan, options, true));
        var report = new Service().Verify(model, request);
        Assert.AreEqual(6, report.Required); Assert.AreEqual(5, report.Executed); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        var member = report.Jobs[0].Results.Single(r => r.Scope == CheckScope.PhysicalMember);
        Assert.AreEqual(DataStatus.Insufficient, member.Data); Assert.IsNull(member.Utilization);
        Assert.IsTrue(member.Details.Trace.Any(t => t.Key == "MemberStability.MissingInputs" && t.Expression.Contains("AlphaUltK")));
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(CheckReportArchive.Load(stream), model, request));
    }

    [DataTestMethod]
    [DataRow("nonconstant")] [DataRow("tiny transverse")] [DataRow("minor moment")]
    public void ReboundInternalDeclarationStillCannotBypassObservedActionLimits(string problem)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture();
        var sample = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]).Last();
        if (problem == "nonconstant") sample.ResultBeamForces.N *= 2;
        if (problem == "tiny transverse") sample.ResultBeamForces.V2 = 1e-12;
        if (problem == "minor moment") sample.ResultBeamForces.M2 = 1;
        var report = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, SteelMemberPipelineTest.Options(model, plan)));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome); Assert.AreEqual(0, report.Executed);
        Assert.IsTrue(report.Jobs[0].Results.Single().Diagnostics.Any(d => d.Code == (problem == "nonconstant" ? "SteelMember.NonconstantActions" : "SteelMember.InternalPatternNotInPlane")));
    }

    [TestMethod]
    public void ExternalWholePatternCanHaveAZeroFirstCutWithoutLosingItsActivePattern()
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture();
        ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]).First().ResultBeamForces.N = 0;
        var report = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, SteelMemberPipelineTest.Options(model, plan, true)));
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void AZeroLoadPatternNeverCreatesAnEvaluatedMember(bool external)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture();
        foreach (var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0])) sample.ResultBeamForces.N = 0;
        var report = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, SteelMemberPipelineTest.Options(model, plan, external)));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome); Assert.AreEqual(0, report.Executed);
    }

    [DataTestMethod]
    [DataRow("boundary")] [DataRow("concomitance")]
    public void MissingGlobalPreparationDataPreventsMemberEngineExecution(string problem)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(); var request = SteelMemberPipelineTest.Request(plan, SteelMemberPipelineTest.Options(model, plan));
        if (problem == "boundary") plan.MemberChecks[0].Start = 100;
        else plan.MemberChecks[0].Context = new MemberDesignContext(globalStateConfirmed: false);
        var report = new Service().Verify(model, request); var result = report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome); Assert.IsNull(result.Details);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == (problem == "boundary" ? "MissingMemberSpanBoundary" : "GlobalMemberStateNotConfirmed")));
    }

    [TestMethod]
    public void ConflictingMemberRoutesAreRejectedBeforeExecution()
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(); var options = SteelMemberPipelineTest.Options(model, plan);
        var request = SteelMemberPipelineTest.Request(plan, options); options["ParameterSet"] = "another curve assessment";
        request.Jobs[0].Assignments = request.Jobs[0].Assignments.Concat(SteelMemberPipelineTest.Request(plan, options).Jobs[0].Assignments).ToArray();
        Assert.ThrowsException<ArgumentException>(() => new Service().Verify(model, request));
    }

    [TestMethod]
    public void ExhaustedAxialCapacityRetainsFailedSectionDecisionWithFiniteArchive()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1];
        job.Assignments[0].Checker = new SteelPipelineMaterialChecker(new StandardEN1993p11(), "2005", parameters: new Dictionary<string, string> { ["Unperforated"] = "true" });
        job.Plan.SectionMechanisms = new[] { CheckMechanism.UlsBiaxialSection }; job.Assignments[0].Mechanisms = null;
        request.Jobs.Clear(); request.Jobs.Add(job);
        var f = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[20], MultiMaterialWorkflow.State("ULS")).Single().ResultBeamForces;
        f.N = -4e6; f.M1 = 1e6; f.M2 = f.V1 = f.V2 = f.T = 0;
        var report = new Service().Verify(model, request); var result = report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, result.Outcome, string.Join(";", result.Diagnostics.Select(d => d.Code)));
        Assert.IsNull(result.Utilization); Assert.IsFalse(result.Details.Metrics.Single().Passed);
        Assert.IsTrue(result.Details.Trace.All(t => !t.Value.HasValue || double.IsFinite(t.Value.Value)));
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, CheckReportArchive.Load(stream)[0].Results.Single().Outcome);
    }
}
