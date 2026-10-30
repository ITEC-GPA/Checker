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

/// <summary>National alphaLT (EN 1993-1-1:2005 6.3.2.2(2) NOTE, an NDP): LtbImperfectionFactorsSource admits the code object's
/// AlphaLTImperfectionFactorForCurveA..D in place of Table 6.3, EC3 only. Same fixture as SteelLtbNationalOptionsPipelineTest:
/// H 300x150x12x18 rolled S355 (h/b = 2: Table 6.4 curve a, Table 6.5 curve b), uniform M1 = 100 kNm, Mcr = 900 kNm. The values
/// 0.25/0.40/0.55/0.80 are a hypothetical national table for the test.</summary>
[TestClass]
public class SteelLtbNationalAlphaPipelineTest
{
    private const string Annex = "National Annex NA.2.16, alphaLT for 6.3.2.2(2)";
    private static double V(CheckResult r, string key) => r.Details.Trace.Single(x => x.Key == key).Value!.Value;
    private static string E(CheckResult r, string key) => r.Details.Trace.Single(x => x.Key == key).Expression;
    private static bool Has(CheckResult r, string prefix) => r.Details.Trace.Any(x => x.Key.StartsWith(prefix, StringComparison.Ordinal));
    private static string Diagnostics(CheckResult r) => string.Join(";", r.Diagnostics.Select(x => x.Code + ":" + x.Message));
    // Independent substitution of EC3 (6.56) and (6.57) with the stated alphaLT (beta 0.75, lambdaLT0 0.4, f = 1 for kc = 1).
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
    private static StandardEN1993p11 National(StandardEN1993p11 code)
    {
        code.AlphaLTImperfectionFactorForCurveA = .25; code.AlphaLTImperfectionFactorForCurveB = .40;
        code.AlphaLTImperfectionFactorForCurveC = .55; code.AlphaLTImperfectionFactorForCurveD = .80;
        return code;
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
    private static CheckResult Run(GPC.Model.Models.Model model, BeamCheckPlanRequest plan, Dictionary<string, string> p, StandardEN1993p11 code)
    {
        var issues = SteelParameterSchemas.Member.Validate(p);
        Assert.AreEqual(0, issues.Count, string.Join(";", issues.Select(i => i.Code + ":" + i.Key)));
        return new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, code)).Jobs[0].Results.Single();
    }

    // General: Table 6.4 curve a, national 0.25 (Table 6.3: 0.21); rolled: Table 6.5 curve b, national 0.40 (0.34); with LtbCurve = C both 0.55.
    [DataTestMethod]
    [DataRow(false, null, .25, .21)] [DataRow(true, null, .40, .34)] [DataRow(false, "C", .55, .49)] [DataRow(true, "C", .55, .49)]
    public void SourceAdmitsTheCodeObjectsNationalAlphaLT(bool rolled, string? curve, double alpha, double recommended)
    {
        var (model, plan, p) = Bending(100e6); p["LtbImperfectionFactorsSource"] = Annex;
        if (rolled) p["LtbMethod"] = "RolledOrEquivalentWelded";
        if (curve != null) { p["LtbCurve"] = curve; p["LtbCurveSource"] = "National Annex curve clause"; }
        var r = Run(model, plan, p, National(new StandardEN1993p11()));
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        double w = V(r, "MemberStability.Wpl1"), lambda = Math.Sqrt(w * 355) / Math.Sqrt(900e6);
        double chi = rolled ? Rolled(lambda, alpha) : General(lambda, alpha);
        Assert.AreEqual(chi, V(r, "MemberStability.ChiLT"), 1e-12); Assert.AreEqual(alpha, V(r, "MemberStability.AlphaLT"));
        Assert.AreEqual(1, V(r, "MemberStability.AlphaLTDeclared")); Assert.AreEqual(recommended, V(r, "MemberStability.AlphaLTRecommended"));
        Assert.AreEqual(100e6 / (chi * w * 355), r.Utilization!.Value, 1e-12);
        StringAssert.Contains(E(r, "Member.LtbImperfectionFactors"), "alphaLT a/b/c/d = 0.25/0.4/0.55/0.8; " + Annex);
        StringAssert.Contains(E(r, "MemberStability.ChiLT"), "6.3.2.2(2) NOTE");
        // Same code object without the source: refused as before.
        p.Remove("LtbImperfectionFactorsSource");
        var refused = Run(model, plan, p, National(new StandardEN1993p11()));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, refused.Outcome); StringAssert.Contains(Diagnostics(refused), "LtbImperfectionFactorsNotQualified");
        // Recommended values with the source: the result of the default code object.
        p["LtbImperfectionFactorsSource"] = Annex;
        var declaredTable = Run(model, plan, p, new StandardEN1993p11()); p.Remove("LtbImperfectionFactorsSource");
        var table = Run(model, plan, p, new StandardEN1993p11());
        Assert.AreEqual(table.Utilization, declaredTable.Utilization); Assert.IsFalse(Has(table, "Member.LtbImperfectionFactors"));
    }

    [TestMethod]
    public void NtcAndImplausibleValuesAreRefused()
    {
        var (model, plan, p) = Bending(100e6); p["LtbImperfectionFactorsSource"] = Annex;
        var ntc = Run(model, plan, p, National(new StandardNTC2018Steel()));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, ntc.Outcome); Assert.IsNull(ntc.Utilization);
        StringAssert.Contains(Diagnostics(ntc), "SteelPipeline.MemberStability.Member.LtbImperfectionFactorsFixedByNtc");
        // Decreasing from b to c, or above 1: invalid declaration.
        var code = National(new StandardEN1993p11()); code.AlphaLTImperfectionFactorForCurveC = .35;
        var decreasing = Run(model, plan, p, code);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, decreasing.Outcome); StringAssert.Contains(Diagnostics(decreasing), "must not decrease");
        code = National(new StandardEN1993p11()); code.AlphaLTImperfectionFactorForCurveD = 1.2;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Run(model, plan, p, code).Outcome);
        // Modes without an LTB reduction reject the key.
        var (_, _, other) = Bending(100e6); other = SteelMemberPipelineTest.Options(model, plan); other["LtbImperfectionFactorsSource"] = Annex;
        CollectionAssert.Contains(SteelParameterSchemas.Member.Validate(other).Select(i => i.Code).ToArray(), "ParameterOnAnotherMode");
    }

    // Columns of SteelBeamColumnFrame, torsion-susceptible with Mcr = 400 kNm: the national 0.25 of the Table 6.4 curve a enters chiLT.
    [TestMethod]
    public void BeamColumnChiLTTakesTheNationalAlphaLT()
    {
        var model = SteelBeamColumnFrame.Create(false, false, true);
        var config = SteelBeamColumnFrame.Configure(model, false);
        foreach (var engine in config.Engines.Where(x => x.Kind == SteelMemberPipelineChecker.EngineId && x.Id.StartsWith("C")))
        {
            var p = engine.Parameters; p.Remove("LateralTorsionalExclusionSource"); p["TorsionSusceptible"] = "true";
            p["Mcr.Value"] = "400"; p["Mcr.Unit"] = "KNm"; p["Mcr.Source"] = "Independent uniform-moment critical moment of the column, fork ends";
            p["LtbImperfectionFactorsSource"] = Annex;
            Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count, string.Join(";", SteelParameterSchemas.Member.Validate(p).Select(i => i.Code)));
        }
        // The frame's EC3 code object (context "code") carries the national table.
        National((StandardEN1993p11)config.Contexts.Single().Code);
        var report = new Service().Verify(model, EngineCatalog.BuiltIn().Compile(config));
        foreach (var job in report.Jobs.Take(4))
        {
            var r = job.Results.Single(x => x.Scope == CheckScope.PhysicalMember);
            Assert.AreNotEqual(EngineeringOutcome.NotEvaluated, r.Outcome, Diagnostics(r));
            double lambda = Math.Sqrt(V(r, "MemberStability.M1Rk")) / Math.Sqrt(400e6);
            Assert.AreEqual(General(lambda, .25), V(r, "MemberStability.ChiLT"), 1e-12); Assert.AreEqual(.25, V(r, "MemberStability.AlphaLT"));
            Assert.AreEqual(.21, V(r, "MemberStability.AlphaLTRecommended")); StringAssert.Contains(E(r, "Member.LtbImperfectionFactors"), Annex);
        }
    }
}
