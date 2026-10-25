using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class SteelDiagramPreparationTest
{
    [DataTestMethod] [DataRow(false, 1d)] [DataRow(true, 1d)] [DataRow(false, -1d)] [DataRow(true, -1d)]
    public void GenericFemEnvelopeUsesTheInteriorMaximumAndRetainsAllSamples(bool ntc, double sign)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture();
        double[] ordinates = { .1, -.4, 1, -.7, .2 }; int i = 0;
        foreach (var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]))
        {
            var f = sample.ResultBeamForces; f.N = f.M2 = f.V1 = f.T = 0;
            f.M1 = sign * 1e6 * ordinates[i++]; f.V2 = sign * 123;
        }
        var p = Options(model, plan, "GenericEnvelope", ntc); p["McrBasis"] = "UniformMoment";
        p["MomentDiagramCoverageSource"] = "Actual LC1: all extrema, both ends and internal station faces exported; no interpolation between sparse cuts claimed";
        Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count);
        var request = SteelMemberPipelineTest.Request(plan, p, ntc ? new StandardNTC2018Steel() : new StandardEN1993p11());
        var report = new Service().Verify(model, request); var r = report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, string.Join(";", r.Diagnostics.Select(x => x.Message ?? x.Code)));
        Assert.AreEqual(sign * 1e6, r.Details.Trace.Single(x => x.Key == "Input.M1").Value);
        Assert.AreEqual(5, r.MemberInput.Samples.Count);
        StringAssert.Contains(r.Details.Trace.Single(x => x.Key == "Member.External.Mcr").Expression, "conservative bound");
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var archived = CheckReportArchive.Load(stream);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(archived, model, request));
        ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]).ElementAt(2).ResultBeamForces.M1 *= 2;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(archived, model, request));
    }

    [TestMethod]
    public void GenericEnvelopeRequiresCoverageAndNeverClaimsBeneficialMomentFactors()
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(); SetActions(model, plan, "UniformTransverseLoad", 1);
        var p = Options(model, plan, "GenericEnvelope", true);
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(p).Any(x => x.Code == "GenericMomentCoverage"));
        var result = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, new StandardNTC2018Steel())).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void GenericBeamColumnCmUsesActualConcomitantFemStatesWithoutLinearDiagramRequirement(bool ntc)
    {
        var model = SteelBeamColumnFrame.Create(false, false, true);
        foreach (string member in new[] { "C1", "C2" }) foreach (string state in new[] { "ULS-A", "ULS-B" })
        {
            var plan = SteelReferenceFrame.Plan(member, state);
            foreach (var part in model.PhysicalMembers[member].Parts)
                foreach (var cut in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[part.BeamId], plan.Results[0]))
                {
                    double x = cut.ParametricDistance;
                    cut.ResultBeamForces.M1 += 1e6 * Math.Sin(2 * Math.PI * x);
                    cut.ResultBeamForces.V2 -= 2 * Math.PI * 1e6 / 3000 * Math.Cos(2 * Math.PI * x);
                }
        }
        var config = SteelBeamColumnFrame.Configure(model, ntc);
        foreach (var engine in config.Engines.Where(x => x.Kind == SteelMemberPipelineChecker.EngineId && x.Id.StartsWith("C")))
        {
            foreach (string key in new[] { "Cm1", "Cm2", "CmLT" }) engine.Parameters.Remove(key);
            engine.Parameters["MomentFactorPreparation"] = "ConservativeEnvelope";
            engine.Parameters["MomentFactorSource"] = "Explicit conservative generic moment envelope; no favourable diagram coefficient";
            engine.Parameters["MomentDiagramCoverageSource"] = "FEM exports all extrema and discontinuities in each actual ULS state";
            Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(engine.Parameters).Count);
        }
        var report = new Service().Verify(model, EngineCatalog.BuiltIn().Compile(config));
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        foreach (var job in report.Jobs.Take(4))
        {
            var r = job.Results.Single(x => x.Scope == CheckScope.PhysicalMember);
            foreach (string cm in new[] { "Cm1", "Cm2", "CmLT" }) Assert.AreEqual(1d, r.Details.Trace.Single(x => x.Key == "MemberStability." + cm).Value);
            StringAssert.Contains(r.Details.Trace.Single(x => x.Key == "Member.MomentFactorSource").Expression, "generic same-case FEM");
        }
    }
    [DataTestMethod]
    [DataRow("EndMoments", false, 1d)] [DataRow("EndMoments", true, 1d)]
    [DataRow("EndMoments", false, -1d)] [DataRow("EndMoments", true, -1d)]
    [DataRow("UniformTransverseLoad", false, 1d)] [DataRow("UniformTransverseLoad", true, 1d)]
    [DataRow("UniformTransverseLoad", false, -1d)] [DataRow("UniformTransverseLoad", true, -1d)]
    [DataRow("CentralPointLoad", false, 1d)] [DataRow("CentralPointLoad", true, 1d)]
    [DataRow("CentralPointLoad", false, -1d)] [DataRow("CentralPointLoad", true, -1d)]
    public void ActualFemDiagramPreparesCriticalMomentAndArchivesTheSameState(string shape, bool ntc, double sign)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(); SetActions(model, plan, shape, sign);
        var p = Options(model, plan, shape, ntc);
        Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count, string.Join(";", SteelParameterSchemas.Member.Validate(p).Select(i => i.Code)));
        var request = SteelMemberPipelineTest.Request(plan, p, ntc ? new StandardNTC2018Steel() : new StandardEN1993p11());
        var report = new Service().Verify(model, request); var r = report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, string.Join(";", r.Diagnostics.Select(x => x.Message ?? x.Code)));
        var value = r.Details.Trace.Single(x => x.Key == "Member.External.Mcr");
        var section = new SteelSectionInput((ISteelSection)model.BeamElements[250].BeamProperty, true);
        var diagram = shape == "EndMoments" ? SteelForkMomentDiagram.EndMoments(sign * 1e6, 0, "actual")
            : shape == "UniformTransverseLoad" ? SteelForkMomentDiagram.UniformTransverseLoad(sign * 1e6, 0, "actual")
            : SteelForkMomentDiagram.CentralPointLoad(sign * 1e6, 0, "actual");
        var prepared = SteelMemberSchemePreparation.ForkBending(section, new(2000), diagram, "fork");
        double expected = ntc ? prepared.UniformMoment.Value : prepared.ActualDiagram.Value;
        Assert.AreEqual(expected, value.Value!.Value, expected * 1e-10);
        StringAssert.Contains(value.Expression, "SN003b");
        Assert.AreEqual(sign * 1e6, r.Details.Trace.Single(x => x.Key == "Input.M1").Value);
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(CheckReportArchive.Load(stream), model, request));
    }

    [DataTestMethod] [DataRow("moment")] [DataRow("shear")] [DataRow("height")] [DataRow("basis")] [DataRow("external")]
    public void InconsistentFemDataOrCriticalPoliciesDoNotPass(string problem)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(); SetActions(model, plan, "UniformTransverseLoad", 1);
        var samples = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]).ToArray();
        if (problem == "moment") samples[1].ResultBeamForces.M1 *= .9;
        if (problem == "shear") samples[1].ResultBeamForces.V2 *= -1;
        var p = Options(model, plan, "UniformTransverseLoad", true);
        if (problem == "height") p["ForkLoadHeight"] = "100";
        if (problem == "basis") p["McrBasis"] = "ActualMomentDiagram";
        if (problem == "external") { p["Mcr.Value"] = "100"; p["Mcr.Unit"] = "KNm"; p["Mcr.Source"] = "Other analysis"; }
        var r = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, new StandardNTC2018Steel())).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsNull(r.Utilization);
    }

    [DataTestMethod] [DataRow(false, false)] [DataRow(true, false)] [DataRow(false, true)] [DataRow(true, true)]
    public void LinearCmPreparationUsesActualWholeMemberStatesAndReversedMesh(bool ntc, bool reverse)
    {
        var model = SteelBeamColumnFrame.Create(true, reverse, true); var config = SteelBeamColumnFrame.Configure(model, ntc);
        foreach (var engine in config.Engines.Where(x => x.Kind == SteelMemberPipelineChecker.EngineId && x.Id.StartsWith("C")))
        {
            foreach (string key in new[] { "Cm1", "Cm2", "CmLT" }) engine.Parameters.Remove(key);
            engine.Parameters["MomentFactorPreparation"] = "LinearDiagrams";
            engine.Parameters["MomentFactorSource"] = "Explicit nonsway case; same full physical braced span for both bending directions and LTB";
            Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(engine.Parameters).Count);
        }
        var request = EngineCatalog.BuiltIn().Compile(config); var report = new Service().Verify(model, request);
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome, string.Join(";", report.Jobs.SelectMany(x => x.Results).SelectMany(x => x.Diagnostics).Select(x => x.Message ?? x.Code)));
        foreach (var job in report.Jobs.Take(4))
        {
            var r = job.Results.Single(x => x.Scope == CheckScope.PhysicalMember);
            Assert.AreEqual(.6, r.Details.Trace.Single(x => x.Key == "MemberStability.Cm1").Value!.Value, 1e-12);
            Assert.AreEqual(.6, r.Details.Trace.Single(x => x.Key == "MemberStability.Cm2").Value!.Value, 1e-12);
            StringAssert.Contains(r.Details.Trace.Single(x => x.Key == "Member.MomentFactorSource").Expression, ntc ? "C4.2.VI" : "TableB.3");
        }
    }

    private static void SetActions(GPC.Model.Models.Model model, BeamCheckPlanRequest plan, string shape, double sign)
    {
        foreach (var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]))
        {
            double x = sample.ParametricDistance, peak = sign * 1e6;
            var f = sample.ResultBeamForces; f.N = f.M2 = f.V1 = f.T = 0;
            f.M1 = shape == "EndMoments" ? peak * (1 - x) : shape == "UniformTransverseLoad" ? 4 * peak * x * (1 - x) : 2 * peak * Math.Min(x, 1 - x);
            f.V2 = shape == "EndMoments" ? peak / 2000 : shape == "UniformTransverseLoad" ? -4 * peak * (1 - 2 * x) / 2000 : (x < .5 ? -2 : 2) * peak / 2000;
        }
    }
    private static Dictionary<string, string> Options(GPC.Model.Models.Model model, BeamCheckPlanRequest plan, string shape, bool ntc)
    {
        var p = SteelMemberPipelineTest.Options(model, plan);
        foreach (string key in new[] { "InPlaneCurve", "LateralCurve", "LateralTorsionalCurve" }) p.Remove(key);
        p["AnalysisMode"] = "Bending"; p["CriticalValues"] = "ForkDiagram"; p["ForkDiagram"] = shape;
        p["CriticalValuesSource"] = "Whole prismatic member, forks/free warping, no intermediate restraints or offsets";
        p["LoadPatternReference"] = "Actual LC1 analytical FEM samples with declared " + shape + "; positive section face";
        p["McrBasis"] = ntc ? "UniformMoment" : "ActualMomentDiagram";
        if (shape != "EndMoments") p["ForkLoadHeight"] = "0";
        return p;
    }
}
