using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>EN 1993-1-1 options of the LTB method: LtbLowMomentExemption (6.3.2.2(4), MEd/Mcr &lt;= lambdaLT0^2, Bending only)
/// and LtbCurve/LtbCurveSource (national curve in place of Table 6.4/6.5). Both opt-in, both refused under NTC 2018. Fixture
/// H 300x150x12x18 rolled S355 (h/b = 2: Table 6.4 curve a, Table 6.5 curve b), uniform M1, external uniform-moment Mcr = 900 kNm.</summary>
[TestClass]
public class SteelLtbNationalOptionsPipelineTest
{
    private const string Adopted = "Design basis rev. 2: EN 1993-1-1:2005 6.3.2.2(4) adopted for the secondary beams";
    private const string Annex = "National Annex NA.2.17, rolled I h/b <= 2";
    private static double V(CheckResult r, string key) => r.Details.Trace.Single(x => x.Key == key).Value!.Value;
    private static string E(CheckResult r, string key) => r.Details.Trace.Single(x => x.Key == key).Expression;
    private static bool Has(CheckResult r, string prefix) => r.Details.Trace.Any(x => x.Key.StartsWith(prefix, StringComparison.Ordinal));
    private static string Diagnostics(CheckResult r) => string.Join(";", r.Diagnostics.Select(x => x.Code + ":" + x.Message));
    // Independent substitution of EC3 (6.56) and (6.57)-(6.58) with the stated alphaLT (beta 0.75, lambdaLT0 0.4, f = 1 for kc = 1).
    private static double General(double lambda, double alpha)
    {
        double phi = .5 * (1 + alpha * (lambda - .2) + lambda * lambda);
        return Math.Min(1, 1 / (phi + Math.Sqrt(phi * phi - lambda * lambda)));
    }
    private static double Rolled(double lambda, double alpha)
    {
        if (lambda <= .4) return 1;
        double phi = .5 * (1 + alpha * (lambda - .4) + .75 * lambda * lambda);
        return Math.Min(Math.Min(1, 1 / (phi + Math.Sqrt(phi * phi - .75 * lambda * lambda))), 1 / (lambda * lambda));
    }

    private static (GPC.Model.Models.Model Model, BeamCheckPlanRequest Plan, Dictionary<string, string> P) Bending(double moment)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture();
        foreach (var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]))
        { var f = sample.ResultBeamForces; f.N = f.V1 = f.V2 = f.T = f.M2 = 0; f.M1 = moment; }
        var p = SteelMemberPipelineTest.Options(model, plan);
        foreach (string key in new[] { "InPlaneCurve", "LateralCurve", "LateralTorsionalCurve" }) p.Remove(key);
        p["AnalysisMode"] = "Bending"; p["LoadPatternReference"] = "Uniform M1 of LC1, analytical FEM samples"; p["McrBasis"] = "UniformMoment";
        p["Mcr.Value"] = "900"; p["Mcr.Unit"] = "KNm"; p["Mcr.Source"] = "Independent uniform-moment critical moment, fork ends, rev 1";
        return (model, plan, p);
    }
    private static CheckResult Run(GPC.Model.Models.Model model, BeamCheckPlanRequest plan, Dictionary<string, string> p, StandardEN1993p11? code = null)
    {
        var issues = SteelParameterSchemas.Member.Validate(p);
        Assert.AreEqual(0, issues.Count, string.Join(";", issues.Select(i => i.Code + ":" + i.Key)));
        return new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, code)).Jobs[0].Results.Single();
    }

    // MEd = 54 kNm: MEd/Mcr = 54/900 = 0.06, above 0.2^2 = 0.04 (general) and below 0.4^2 = 0.16 (rolled, LambdaLT0ForLateralTorsionalBucklingMod).
    // MEd = 36 kNm: 36/900 = 0.04 <= 0.04, exempt with the general method too.
    [TestMethod]
    public void LowMomentExemptionUsesTheLambdaLT0OfTheSelectedMethod()
    {
        var (model, plan, p) = Bending(54e6); p["LtbLowMomentExemption"] = "true"; p["LtbLowMomentExemptionSource"] = Adopted;
        var general = Run(model, plan, p);
        Assert.AreEqual(EngineeringOutcome.Satisfied, general.Outcome, Diagnostics(general));
        Assert.AreEqual(.06, V(general, "MemberStability.LowMoment.MEdOverMcr"), 1e-15); Assert.AreEqual(.04, V(general, "MemberStability.LowMoment.Limit"), 1e-16);
        Assert.AreEqual(0, V(general, "MemberStability.LowMoment.Exempt"));
        double lambda = Math.Sqrt(V(general, "MemberStability.Wpl1") * 355) / Math.Sqrt(900e6), chi = General(lambda, .21);
        Assert.AreEqual(chi, V(general, "MemberStability.ChiLT"), 1e-12);
        Assert.AreEqual(54e6 / (chi * V(general, "MemberStability.Wpl1") * 355), general.Utilization!.Value, 1e-12);
        Assert.AreEqual(.04, V(general, "Member.LtbLowMomentExemption"), 1e-16); StringAssert.Contains(E(general, "Member.LtbLowMomentExemption"), Adopted);

        p["LtbMethod"] = "RolledOrEquivalentWelded";
        var rolled = Run(model, plan, p);
        Assert.AreEqual("NotRequired", E(rolled, "MemberStability.Status"));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, rolled.Outcome); Assert.IsNull(rolled.Utilization);
        Assert.AreEqual(0, rolled.Diagnostics.Count, Diagnostics(rolled)); Assert.AreEqual(GPC.Model.Core.Diagnostics.DataStatus.Ready, rolled.Data);
        // Exempt: a not applicable row with its reason, resolved but never satisfied (b1102cf1).
        Assert.AreEqual(GPC.Model.Checking.Reports.CheckApplicability.NotApplicable, rolled.Applicability);
        StringAssert.Contains(rolled.ApplicabilityReason, "Member.LtbLowMomentExemption");
        Assert.AreEqual(.4, V(rolled, "MemberStability.LowMoment.LambdaLT0")); Assert.AreEqual(.16, V(rolled, "MemberStability.LowMoment.Limit"), 1e-16);
        Assert.AreEqual(1, V(rolled, "MemberStability.LowMoment.Exempt")); Assert.AreEqual(900e6, V(rolled, "MemberStability.LowMoment.Mcr"));
        StringAssert.Contains(E(rolled, "MemberStability.LowMoment.Exempt"), "6.3.2.2(4)");
        StringAssert.Contains(E(rolled, "MemberStability.LowMoment.Exempt"), "LambdaLT0ForLateralTorsionalBucklingMod=0.4");
        Assert.IsFalse(Has(rolled, "MemberStability.ChiLT"));

        (model, plan, p) = Bending(36e6); p["LtbLowMomentExemption"] = "true"; p["LtbLowMomentExemptionSource"] = Adopted;
        var limit = Run(model, plan, p);
        Assert.AreEqual("NotRequired", E(limit, "MemberStability.Status")); Assert.AreEqual(.04, V(limit, "MemberStability.LowMoment.MEdOverMcr"), 1e-17);
        // Default and an explicit false keep the LTB check of the same member.
        foreach (string? adopted in new[] { null, "false" })
        {
            p.Remove("LtbLowMomentExemptionSource"); if (adopted == null) p.Remove("LtbLowMomentExemption"); else p["LtbLowMomentExemption"] = adopted;
            var standard = Run(model, plan, p);
            Assert.AreEqual(EngineeringOutcome.Satisfied, standard.Outcome, Diagnostics(standard));
            Assert.IsFalse(Has(standard, "MemberStability.LowMoment.") || Has(standard, "Member.LtbLowMomentExemption"));
            Assert.AreEqual(General(lambda, .21), V(standard, "MemberStability.ChiLT"), 1e-12);
        }
    }

    // LtbCurve replaces the table: general a (0.21) -> b (0.34); rolled b (0.34) -> d (0.76), kc = 1 (no diagram established).
    [DataTestMethod] [DataRow(false, "B", .34, .21)] [DataRow(true, "D", .76, .34)]
    public void DeclaredCurveReplacesTheTableAssignment(bool rolled, string curve, double alpha, double tableAlpha)
    {
        var (model, plan, p) = Bending(100e6); p["LtbCurve"] = curve; p["LtbCurveSource"] = Annex;
        if (rolled) p["LtbMethod"] = "RolledOrEquivalentWelded";
        var r = Run(model, plan, p);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        double lambda = Math.Sqrt(V(r, "MemberStability.Wpl1") * 355) / Math.Sqrt(900e6);
        Assert.AreEqual(rolled ? Rolled(lambda, alpha) : General(lambda, alpha), V(r, "MemberStability.ChiLT"), 1e-12);
        Assert.AreEqual(alpha, V(r, "MemberStability.AlphaLT")); Assert.AreEqual(tableAlpha, V(r, "MemberStability.AlphaLTTableCurve"));
        Assert.AreEqual(1, V(r, "MemberStability.LtbCurveDeclared"));
        StringAssert.Contains(E(r, "Member.LtbCurve"), curve + "; " + Annex); StringAssert.Contains(E(r, "Member.LtbCurve"), "2005");
        StringAssert.Contains(E(r, "MemberStability.ChiLT"), "declared by the National Annex");
        p.Remove("LtbCurve"); p.Remove("LtbCurveSource");
        var table = Run(model, plan, p);
        Assert.AreEqual(tableAlpha, V(table, "MemberStability.AlphaLT")); Assert.IsFalse(Has(table, "Member.LtbCurve") || Has(table, "MemberStability.LtbCurveDeclared"));
        Assert.IsTrue(r.Utilization > table.Utilization);
    }

    // Columns of SteelBeamColumnFrame, torsion-susceptible with Mcr = 400 kNm: the declared curve d (0.76) enters chiLT.
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnChiLTTakesTheDeclaredCurveUnderEc3Only(bool ntc)
    {
        var model = SteelBeamColumnFrame.Create(false, false, true);
        var config = SteelBeamColumnFrame.Configure(model, ntc);
        foreach (var engine in config.Engines.Where(x => x.Kind == SteelMemberPipelineChecker.EngineId && x.Id.StartsWith("C")))
        {
            var p = engine.Parameters; p.Remove("LateralTorsionalExclusionSource"); p["TorsionSusceptible"] = "true";
            p["Mcr.Value"] = "400"; p["Mcr.Unit"] = "KNm"; p["Mcr.Source"] = "Independent uniform-moment critical moment of the column, fork ends";
            p["LtbCurve"] = "D"; p["LtbCurveSource"] = Annex;
            Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count, string.Join(";", SteelParameterSchemas.Member.Validate(p).Select(i => i.Code)));
        }
        var report = new Service().Verify(model, EngineCatalog.BuiltIn().Compile(config));
        foreach (var job in report.Jobs.Take(4))
        {
            var r = job.Results.Single(x => x.Scope == CheckScope.PhysicalMember);
            if (ntc)
            {
                Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome);
                StringAssert.Contains(Diagnostics(r), "SteelPipeline.MemberStability.Member.LtbCurveFixedByNtc");
                continue;
            }
            Assert.AreNotEqual(EngineeringOutcome.NotEvaluated, r.Outcome, Diagnostics(r));
            double lambda = Math.Sqrt(V(r, "MemberStability.M1Rk")) / Math.Sqrt(400e6);
            Assert.AreEqual(General(lambda, .76), V(r, "MemberStability.ChiLT"), 1e-12); Assert.AreEqual(.76, V(r, "MemberStability.AlphaLT"));
            Assert.AreEqual(.21, V(r, "MemberStability.AlphaLTTableCurve")); StringAssert.Contains(E(r, "Member.LtbCurve"), Annex);
        }
    }

    [DataTestMethod] [DataRow("exemption")] [DataRow("curve")]
    public void NtcRefusesTheEc3Options(string option)
    {
        var (model, plan, p) = Bending(54e6);
        if (option == "exemption") { p["LtbLowMomentExemption"] = "true"; p["LtbLowMomentExemptionSource"] = Adopted; }
        else { p["LtbCurve"] = "C"; p["LtbCurveSource"] = Annex; }
        var r = Run(model, plan, p, new StandardNTC2018Steel());
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsNull(r.Utilization);
        Assert.AreEqual(GPC.Model.Core.Diagnostics.DataStatus.NotSupported, r.Data);
        StringAssert.Contains(Diagnostics(r), option == "exemption" ? "SteelPipeline.MemberStability.Member.LtbLowMomentExemptionNotInNtc"
            : "SteelPipeline.MemberStability.Member.LtbCurveFixedByNtc");
        StringAssert.Contains(Diagnostics(r), option == "exemption" ? "6.3.2.2(4)" : "Tab. 4.2.IX");
    }

    [DataTestMethod]
    [DataRow("curveSource")] [DataRow("exemptionSource")] [DataRow("sourceWithoutExemption")] [DataRow("route")] [DataRow("beamColumn")]
    [DataRow("alpha")] [DataRow("generalBeta")]
    public void InconsistentDeclarationsAreRefused(string problem)
    {
        CheckResult r; string[] issues;
        if (problem == "beamColumn")
        {
            var model = SteelBeamColumnFrame.Create();
            var config = SteelBeamColumnFrame.Configure(model, false);
            var engine = config.Engines.First(x => x.Kind == SteelMemberPipelineChecker.EngineId && x.Id.StartsWith("C"));
            engine.Parameters["LtbLowMomentExemption"] = "true"; engine.Parameters["LtbLowMomentExemptionSource"] = Adopted;
            issues = SteelParameterSchemas.Member.Validate(engine.Parameters).Select(i => i.Code).ToArray();
            var report = new Service().Verify(model, EngineCatalog.BuiltIn().Compile(config));
            r = report.Jobs.Select(j => j.Results.SingleOrDefault(x => x.Scope == CheckScope.PhysicalMember)).First(x => x != null && Diagnostics(x).Contains("LtbLowMomentExemption"))!;
        }
        else
        {
            var (model, plan, p) = Bending(54e6);
            var code = new StandardEN1993p11();
            if (problem == "curveSource") p["LtbCurve"] = "B";
            if (problem == "exemptionSource") p["LtbLowMomentExemption"] = "true";
            if (problem == "sourceWithoutExemption") { p["LtbLowMomentExemption"] = "false"; p["LtbLowMomentExemptionSource"] = Adopted; }
            if (problem == "route") { p = SteelMemberPipelineTest.Options(model, plan); p["LtbCurve"] = "B"; p["LtbCurveSource"] = Annex; }
            if (problem == "alpha") { code.AlphaLTImperfectionFactorForCurveB = .35; p["LtbCurve"] = "B"; p["LtbCurveSource"] = Annex; }
            if (problem == "generalBeta") code.BetaForLateralTorsionalBuckling = .75;
            issues = SteelParameterSchemas.Member.Validate(p).Select(i => i.Code).ToArray();
            r = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, code)).Jobs[0].Results.Single();
        }
        string? issue = problem == "curveSource" ? "LtbCurvePair" : problem == "exemptionSource" ? "LtbLowMomentExemptionSourceRequired"
            : problem == "sourceWithoutExemption" ? "LtbLowMomentSourceWithoutExemption" : problem == "route" || problem == "beamColumn" ? "ParameterOnAnotherMode" : null;
        if (issue != null) CollectionAssert.Contains(issues, issue); else Assert.AreEqual(0, issues.Length, string.Join(";", issues)); // code values: execution
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsNull(r.Utilization);
        string token = problem == "curveSource" ? "MissingSteelMemberParameter: LtbCurveSource" : problem == "exemptionSource" ? "MissingSteelMemberParameter: LtbLowMomentExemptionSource"
            : problem == "sourceWithoutExemption" ? "LtbLowMomentSourceWithoutExemption" : problem == "route" ? "LtbMethodOnAnotherRoute"
            : problem == "beamColumn" ? "LtbLowMomentExemptionOnAnotherRoute" : problem == "alpha" ? "LtbImperfectionFactorsNotQualified" : "LtbGeneralParametersNotQualified";
        StringAssert.Contains(Diagnostics(r), token);
    }
}
