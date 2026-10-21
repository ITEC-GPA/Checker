using GPC.Checkers.Steel.Members;
using GPC.Model.Checker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Data.Steel;
using GPC.Model.Results.Queries;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using GPC.Model.Structure.Members;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class SteelMemberPipelineTest
{
    internal static (GPC.Model.Models.Model Model, BeamCheckPlanRequest Plan) Fixture(bool split = false)
    {
        var model = ElementScopeCharacterizationTest.Model(axial: -1e5, shear: 0, configureBeforeResults: m => {
            foreach (var beam in m.BeamElements.Values)
            {
                var section = new SteelSection(new SectionH(300, 12, 150, 18, 150, 18, "H"), SteelMaterialEN1993Data.S355);
                beam.BeamProperty = section; beam.Assignments.Sections.Clear();
                beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Property = section });
            }
        });
        model.PhysicalMembers.Add("S1", new PhysicalMemberDefinition("S1", split ? new[] { new BeamMemberPart(250, true), new BeamMemberPart(251, false) }
            : new[] { new BeamMemberPart(250, true) }, "NodeToNode", source: "Explicit test member"));
        var plan = new BeamCheckPlanRequest { MemberIds = new[] { "S1" }, SectionMechanisms = Array.Empty<CheckMechanism>(),
            Results = new[] { new ResultSelection { Dataset = "synthetic-member", Case = "LC1", ConcomitantState = "LC1" } },
            MemberChecks = new[] { new MemberCheckSpecification { MemberId = "S1", MethodId = SteelMemberPipelineChecker.EngineId,
                Context = new MemberDesignContext(globalStateConfirmed: true, assumptions: "Explicit uniform fork test declaration") } } };
        return (model, plan);
    }
    internal static Dictionary<string, string> Options(GPC.Model.Models.Model model, BeamCheckPlanRequest plan, bool external = false)
    {
        var work = BeamCheckPlan.PrepareActions(model, plan).WorkItems.Single();
        Assert.AreEqual(DataStatus.Ready, work.Data, string.Join(";", work.Diagnostics.Select(d => d.Code)));
        var result = new Dictionary<string, string> { ["AnalysisMode"] = external ? "ExternalInPlane" : "UniformFork",
            ["MemberFingerprint"] = ModelValues.Fingerprint(new object[] { work.Member.Snapshot }), ["Unperforated"] = "true",
            ["LoadPatternReference"] = "Whole-member LC1, uniform sharp H, fork/free warping, first-order N/M1, no transverse loads or intermediate restraints",
            ["ParameterSet"] = "Explicit EC3 curves", ["LateralCurve"] = "B", ["LateralTorsionalCurve"] = "C" };
        if (external)
        {
            result["MemberClass"] = "1"; result["ClassificationSource"] = "Independent nominal member classification";
            result["AlphaUltK.Value"] = "2"; result["AlphaUltK.Unit"] = "One"; result["AlphaUltK.Source"] = "In-plane analysis including second order and imperfections, rev 1";
            result["AlphaCrOp.Value"] = "4"; result["AlphaCrOp.Unit"] = "One"; result["AlphaCrOp.Source"] = "Out-of-plane bifurcation, same LC1 proportional pattern, rev 1";
            result["Mcr.Value"] = "100"; result["Mcr.Unit"] = "KNm"; result["Mcr.Source"] = "Independent critical-moment analysis, rev 1";
        }
        else result["InPlaneCurve"] = "B";
        return result;
    }
    internal static MultiMaterialCheckRequest Request(BeamCheckPlanRequest plan, Dictionary<string, string> options, StandardEN1993p11? code = null)
    {
        var request = new MultiMaterialCheckRequest();
        request.Jobs.Add(new MultiMaterialCheckJob { Name = "Steel member", Plan = plan,
            Assignments = new[] { new MaterialCheckerAssignment { Checker = new SteelMemberPipelineChecker(code ?? new StandardEN1993p11(), code is StandardNTC2018Steel ? "2018" : "2005", parameters: options),
                Mechanisms = new[] { CheckMechanism.Stability } } } });
        return request;
    }

    [TestMethod]
    public void MaterialNeutralMemberPreparationOwnsSteelInputsAndTracksMutations()
    {
        var (model, request) = Fixture(); var plan = BeamCheckPlan.PrepareActions(model, request); var input = plan.WorkItems.Single().Member;
        Assert.IsTrue(plan.IsCurrent); Assert.AreEqual(0, input.Sections.Count); Assert.AreEqual(5, input.ActionSections.Count);
        Assert.IsTrue(input.ActionSections.All(c => c.Property is SteelSection));
        Assert.AreEqual(input.ActionSections.Count, input.Snapshot.Samples.Count);
        input.ActionSections[0].Forces.N *= 2;
        Assert.IsFalse(plan.IsCurrent); // Prepared values are guarded independently of source results.
    }

    [TestMethod]
    public void PhysicalMemberUsesOneResultAndNativeUniformResistance()
    {
        var (model, plan) = Fixture(); var options = Options(model, plan); var request = Request(plan, options);
        var report = new Service().Verify(model, request); var result = report.Jobs.Single().Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, string.Join(";", result.Diagnostics.Select(d => d.Message ?? d.Code)));
        Assert.AreEqual(CheckScope.PhysicalMember, result.Scope); Assert.AreEqual("S1", result.Target.MemberId);
        Assert.AreEqual(1, report.Required); Assert.AreEqual(1, report.Executed);
        var section = (ISteelSection)model.BeamElements[250].BeamProperty;
        var native = GPC.Checkers.Steel.ModelAdapters.Ec3MonosymmetricMember.EvaluateUniformFork(section, 2000, -1e5, 0,
            Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.C, 1, 1, "test");
        Assert.AreEqual(native.Utilization, result.Utilization!.Value, 1e-12);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, model, request));
        ((SteelMemberPipelineChecker)request.Jobs[0].Assignments[0].Checker).Code.GammaM1 = 1.1;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report, model, request));
    }

    [TestMethod]
    public void ExternalRouteRetainsConvertedAndOriginalUnitsAndSource()
    {
        var (model, plan) = Fixture(split: true); var options = Options(model, plan, external: true);
        var result = new Service().Verify(model, Request(plan, options)).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, string.Join(";", result.Diagnostics.Select(d => d.Message ?? d.Code)));
        var canonical = result.Details.Trace.Single(v => v.Key == "Member.External.Mcr");
        Assert.AreEqual(1e8, canonical.Value); Assert.AreEqual("Nmm", canonical.Unit); Assert.AreEqual(options["Mcr.Source"], canonical.Expression);
        var original = result.Details.Trace.Single(v => v.Key == "Member.External.Mcr.Original");
        Assert.AreEqual(100d, original.Value); Assert.AreEqual("KNm", original.Unit);
    }

    [DataTestMethod]
    [DataRow("missing factor")] [DataRow("missing source")] [DataRow("wrong units")] [DataRow("wrong fingerprint")] [DataRow("class 4")]
    public void IncompleteExternalDataNeverProduceACompletePass(string problem)
    {
        var (model, plan) = Fixture(); var options = Options(model, plan, true);
        if (problem == "missing factor") foreach (var key in new[] { "AlphaUltK.Value", "AlphaUltK.Unit", "AlphaUltK.Source" }) options.Remove(key);
        if (problem == "missing source") options.Remove("AlphaCrOp.Source");
        if (problem == "wrong units") options["Mcr.Unit"] = "KN";
        if (problem == "wrong fingerprint") options["MemberFingerprint"] = "foreign-analysis";
        if (problem == "class 4") options["MemberClass"] = "4";
        var report = new Service().Verify(model, Request(plan, options));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.AreEqual(0, report.Executed);
    }

    [TestMethod]
    public void SplitMemberCannotUseInternalSingleElementQualification()
    {
        var (model, plan) = Fixture(true); var report = new Service().Verify(model, Request(plan, Options(model, plan)));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.IsTrue(report.Jobs[0].Results.Single().Diagnostics.Any(d => d.Code == "SteelMember.InternalSchemeNotQualified"));
    }

    [TestMethod]
    public void NtcMemberDoesNotSilentlyUseAnEc3Method()
    {
        var (model, plan) = Fixture(); var report = new Service().Verify(model, Request(plan, Options(model, plan), new StandardNTC2018Steel()));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome); Assert.AreEqual(0, report.Executed);
    }

    [TestMethod]
    public void ChangedSampleInvalidatesSourceBoundAnalysisBeforeRecalculation()
    {
        var (model, plan) = Fixture(); var request = Request(plan, Options(model, plan));
        var sample = ResultQueries.Samples<GPC.Model.Results.Locations.StationResultBeamForces>(model.BeamElements[250], plan.Results[0]).First();
        sample.ResultBeamForces.N *= 2;
        var result = new Service().Verify(model, request).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "SteelMember.AnalysisSnapshotMismatch"));
    }
}
