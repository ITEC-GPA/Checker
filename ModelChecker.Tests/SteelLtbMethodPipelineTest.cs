using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Engine parameter LtbMethod=RolledOrEquivalentWelded: EN 1993-1-1:2005 6.3.2.3 / NTC 2018 4.2.4.1.3.2 with the
/// code object's BetaForLateralTorsionalBucklingMod = 0.75 and LambdaLT0ForLateralTorsionalBucklingMod = 0.4. The fixture H
/// 300x150x12x18 is rolled with h/b = 2: curve b, alphaLT = 0.34. kc only from a validated diagram or a sourced declaration.</summary>
[TestClass]
public class SteelLtbMethodPipelineTest
{
    // Independent substitution of EC3 (6.57)-(6.58) / NTC 4.2.4.1.3.2 with alphaLT 0.34, beta 0.75, lambdaLT0 0.4.
    private static double Rolled(double lambda, double kc, bool ntc)
    {
        if (lambda <= .4) return 1;
        double f = Math.Min(1, 1 - .5 * (1 - kc) * (1 - 2 * (lambda - .8) * (lambda - .8)));
        double phi = .5 * (1 + .34 * (lambda - .4) + .75 * lambda * lambda), basis = 1 / (phi + Math.Sqrt(phi * phi - .75 * lambda * lambda)), cap = 1 / (lambda * lambda);
        return ntc ? Math.Min(Math.Min(basis / f, 1), cap / f) : Math.Min(Math.Min(Math.Min(Math.Min(basis, 1), cap) / f, 1), cap);
    }
    // General case: beta 1, lambdaLT0 0.2, f 1; EC3 Table 6.4 curve a (0.21), NTC Tab. 4.2.IX curve b (0.34) for rolled h/b <= 2.
    private static double General(double lambda, bool ntc)
    {
        double alpha = ntc ? .34 : .21, phi = .5 * (1 + alpha * (lambda - .2) + lambda * lambda);
        return Math.Min(1, 1 / (phi + Math.Sqrt(phi * phi - lambda * lambda)));
    }
    private static double V(CheckResult r, string key) => r.Details.Trace.Single(x => x.Key == key).Value!.Value;
    private static string E(CheckResult r, string key) => r.Details.Trace.Single(x => x.Key == key).Expression;
    private static string Diagnostics(CheckResult r) => string.Join(";", r.Diagnostics.Select(x => x.Code + ":" + x.Message));
    private static StandardEN1993p11 Code(bool ntc) => ntc ? new StandardNTC2018Steel() : new StandardEN1993p11();

    private static void SetActions(GPC.Model.Models.Model model, BeamCheckPlanRequest plan, string shape)
    {
        foreach (var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]))
        {
            double x = sample.ParametricDistance, peak = 1e6;
            var f = sample.ResultBeamForces; f.N = f.M2 = f.V1 = f.T = 0;
            f.M1 = shape == "EndMoments" ? peak * (1 - x) : shape == "UniformTransverseLoad" ? 4 * peak * x * (1 - x) : 2 * peak * Math.Min(x, 1 - x);
            f.V2 = shape == "EndMoments" ? peak / 2000 : shape == "UniformTransverseLoad" ? -4 * peak * (1 - 2 * x) / 2000 : (x < .5 ? -2 : 2) * peak / 2000;
        }
    }
    private static Dictionary<string, string> Bending(GPC.Model.Models.Model model, BeamCheckPlanRequest plan, string? fork, bool ntc)
    {
        var p = SteelMemberPipelineTest.Options(model, plan);
        foreach (string key in new[] { "InPlaneCurve", "LateralCurve", "LateralTorsionalCurve" }) p.Remove(key);
        p["AnalysisMode"] = "Bending"; p["LoadPatternReference"] = "Actual LC1 analytical FEM samples; positive section face";
        p["McrBasis"] = ntc || fork == null ? "UniformMoment" : "ActualMomentDiagram";
        if (fork == null) { p["Mcr.Value"] = "900"; p["Mcr.Unit"] = "KNm"; p["Mcr.Source"] = "Independent uniform-moment critical moment, fork ends, rev 1"; return p; }
        p["CriticalValues"] = "ForkDiagram"; p["ForkDiagram"] = fork;
        p["CriticalValuesSource"] = "Whole prismatic member, forks/free warping, no intermediate restraints or offsets";
        if (fork != "EndMoments") p["ForkLoadHeight"] = "0";
        return p;
    }

    // EndMoments: M1 from 1e6 to 0, psi = 0, kc = 1/1.33; UniformTransverseLoad: simply supported parabola, kc = 0.94;
    // CentralPointLoad: simply supported triangle, kc = 0.86 (EN 1993-1-1:2005 Table 6.6).
    [DataTestMethod]
    [DataRow("EndMoments", false, 1 / 1.33)] [DataRow("EndMoments", true, 1 / 1.33)]
    [DataRow("UniformTransverseLoad", false, .94)] [DataRow("UniformTransverseLoad", true, .94)]
    [DataRow("CentralPointLoad", false, .86)] [DataRow("CentralPointLoad", true, .86)]
    public void ForkDiagramEstablishesKcOfTheRolledMethod(string shape, bool ntc, double kc)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(); SetActions(model, plan, shape);
        var p = Bending(model, plan, shape, ntc); p["LtbMethod"] = "RolledOrEquivalentWelded";
        Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count, string.Join(";", SteelParameterSchemas.Member.Validate(p).Select(i => i.Code)));
        var request = SteelMemberPipelineTest.Request(plan, p, Code(ntc));
        var report = new Service().Verify(model, request); var r = report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        double lambda = Math.Sqrt(V(r, "MemberStability.Wpl1") * 355) / Math.Sqrt(V(r, "MemberStability.Mcr"));
        Assert.IsTrue(lambda > .4 && lambda < 1.5, "the modified curve and f are active: " + lambda);
        Assert.AreEqual(Rolled(lambda, kc, ntc), V(r, "MemberStability.ChiLT"), 1e-12);
        Assert.AreEqual(kc, V(r, "MemberStability.Kc"), 1e-15); Assert.AreEqual(kc, V(r, "Member.LtbKc"), 1e-15);
        Assert.AreEqual(.34, V(r, "MemberStability.AlphaLT")); Assert.AreEqual(.75, V(r, "MemberStability.BetaLT")); Assert.AreEqual(.4, V(r, "MemberStability.LambdaLT0"));
        Assert.AreEqual(0, V(r, "MemberStability.KcNotEstablished"));
        StringAssert.Contains(E(r, "Member.LtbMethod"), "RolledOrEquivalentWelded"); StringAssert.Contains(E(r, "Member.LtbKc"), shape);
        using (var stream = new MemoryStream())
        {
            CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
            Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(CheckReportArchive.Load(stream), model, request));
        }
        // Default: without LtbMethod the same member keeps the general curve and has no kc rows.
        p.Remove("LtbMethod");
        var g = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, Code(ntc))).Jobs[0].Results.Single();
        Assert.AreEqual(General(lambda, ntc), V(g, "MemberStability.ChiLT"), 1e-12);
        Assert.AreEqual(1, V(g, "MemberStability.BetaLT")); Assert.AreEqual(1, V(g, "MemberStability.MomentCorrectionF"));
        Assert.IsFalse(g.Details.Trace.Any(x => x.Key == "Member.LtbKc" || x.Key == "MemberStability.Kc"));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ExternalMcrUsesKcOneUnlessASourcedDiagramIsDeclared(bool ntc)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(); SetActions(model, plan, "EndMoments");
        var p = Bending(model, plan, null, ntc); p["LtbMethod"] = "RolledOrEquivalentWelded";
        CheckResult Run()
        {
            Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count, string.Join(";", SteelParameterSchemas.Member.Validate(p).Select(i => i.Code)));
            var r = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, Code(ntc))).Jobs[0].Results.Single();
            Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r)); return r;
        }
        var r = Run(); double lambda = Math.Sqrt(V(r, "MemberStability.Wpl1") * 355) / Math.Sqrt(900e6);
        Assert.AreEqual(1, V(r, "MemberStability.Kc")); Assert.AreEqual(1, V(r, "MemberStability.KcNotEstablished"));
        Assert.AreEqual(1, V(r, "MemberStability.MomentCorrectionF")); Assert.AreEqual(Rolled(lambda, 1, ntc), V(r, "MemberStability.ChiLT"), 1e-12);
        StringAssert.Contains(E(r, "Member.LtbKc"), "NotEstablished");
        p["LtbMomentDistribution"] = "Linear"; p["LtbPsi"] = "0"; p["LtbMomentSource"] = "Declared linear diagram between the fork supports, LC1";
        r = Run();
        Assert.AreEqual(1 / 1.33, V(r, "MemberStability.Kc"), 1e-15); Assert.AreEqual(0, V(r, "MemberStability.KcPsi"));
        Assert.AreEqual(Rolled(lambda, 1 / 1.33, ntc), V(r, "MemberStability.ChiLT"), 1e-12);
        p["LtbMomentDistribution"] = "SimplySupportedUniformLoad"; p.Remove("LtbPsi");
        Assert.AreEqual(.94, V(Run(), "MemberStability.Kc"));
    }

    [DataTestMethod] [DataRow("route")] [DataRow("prepared")] [DataRow("general")] [DataRow("psi")] [DataRow("national")]
    public void InconsistentLtbDeclarationsAreRefused(string problem)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(); if (problem != "route") SetActions(model, plan, "EndMoments");
        // "route": the fixture's uniform fork compression member, a mode without an LTB reduction to modify.
        var p = problem == "route" ? SteelMemberPipelineTest.Options(model, plan) : Bending(model, plan, problem == "prepared" ? "EndMoments" : null, false);
        p["LtbMethod"] = problem == "general" ? "General" : "RolledOrEquivalentWelded";
        if (problem == "prepared" || problem == "general" || problem == "psi")
        { p["LtbMomentDistribution"] = "Linear"; p["LtbMomentSource"] = "Declared diagram"; if (problem != "psi") p["LtbPsi"] = "0"; }
        var code = new StandardEN1993p11(); if (problem == "national") code.BetaForLateralTorsionalBucklingMod = .7;
        var issues = SteelParameterSchemas.Member.Validate(p).Select(i => i.Code).ToArray();
        string expectedIssue = problem == "route" ? "ParameterOnAnotherMode" : problem == "prepared" ? "LtbMomentConflictsWithPreparation"
            : problem == "general" ? "LtbMomentWithoutRolledMethod" : problem == "psi" ? "LtbPsiRequired" : null!;
        if (expectedIssue != null) CollectionAssert.Contains(issues, expectedIssue); else Assert.AreEqual(0, issues.Length); // a code value is checked at execution
        var r = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, code)).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsNull(r.Utilization);
        string token = problem == "route" ? "LtbMethodOnAnotherRoute" : problem == "prepared" ? "LtbMomentDistributionConflictsWithPreparedDiagram"
            : problem == "general" ? "LtbMomentWithoutRolledMethod" : problem == "psi" ? "MissingSteelMemberParameter: LtbPsi" : "LtbRolledParametersNotQualified";
        StringAssert.Contains(Diagnostics(r), token);
    }

    // Columns of SteelBeamColumnFrame: M1 linear from its end value to zero, psi = 0, kc = 1/1.33 from the validated LinearDiagrams.
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnLinearDiagramsGiveKcToTheRolledChiLT(bool ntc)
    {
        var model = SteelBeamColumnFrame.Create(false, false, true);
        VerificationConfiguration Configure(bool rolled)
        {
            var config = SteelBeamColumnFrame.Configure(model, ntc);
            foreach (var engine in config.Engines.Where(x => x.Kind == SteelMemberPipelineChecker.EngineId && x.Id.StartsWith("C")))
            {
                var p = engine.Parameters;
                foreach (string key in new[] { "Cm1", "Cm2", "CmLT", "LateralTorsionalExclusionSource" }) p.Remove(key);
                p["TorsionSusceptible"] = "true"; p["MomentFactorPreparation"] = "LinearDiagrams";
                p["MomentFactorSource"] = "Explicit nonsway case; same full physical braced span for both bending directions and LTB";
                p["Mcr.Value"] = "400"; p["Mcr.Unit"] = "KNm"; p["Mcr.Source"] = "Independent uniform-moment critical moment of the column, fork ends";
                if (rolled) p["LtbMethod"] = "RolledOrEquivalentWelded";
                Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count, string.Join(";", SteelParameterSchemas.Member.Validate(p).Select(i => i.Code)));
            }
            return config;
        }
        var rolled = new Service().Verify(model, EngineCatalog.BuiltIn().Compile(Configure(true)));
        var general = new Service().Verify(model, EngineCatalog.BuiltIn().Compile(Configure(false)));
        foreach (var (job, baseline) in rolled.Jobs.Take(4).Zip(general.Jobs.Take(4)))
        {
            var r = job.Results.Single(x => x.Scope == CheckScope.PhysicalMember); var g = baseline.Results.Single(x => x.Scope == CheckScope.PhysicalMember);
            Assert.AreNotEqual(EngineeringOutcome.NotEvaluated, r.Outcome, Diagnostics(r));
            double lambda = Math.Sqrt(V(r, "MemberStability.M1Rk")) / Math.Sqrt(400e6);
            Assert.AreEqual(Rolled(lambda, 1 / 1.33, ntc), V(r, "MemberStability.ChiLT"), 1e-12);
            Assert.AreEqual(1 / 1.33, V(r, "Member.LtbKc"), 1e-15); StringAssert.Contains(E(r, "Member.LtbKc"), "LinearDiagrams");
            Assert.AreEqual(General(lambda, ntc), V(g, "MemberStability.ChiLT"), 1e-12);
            Assert.IsFalse(g.Details.Trace.Any(x => x.Key == "Member.LtbKc"));
            Assert.IsTrue(V(r, "MemberStability.Interaction1") < V(g, "MemberStability.Interaction1"));
        }
    }
}
