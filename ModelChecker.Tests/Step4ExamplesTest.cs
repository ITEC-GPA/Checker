using GPC.Model.Checker;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using static ModelChecker.Tests.Step4Fixture;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace ModelChecker.Tests;

/// <summary>
/// Step 4G: five reproducible examples on the physical member T1 (two FEM elements, the second reversed) with every concrete check of step 4:
/// ULS biaxial bending, shear in both directions, torsion, serviceability stresses and cracking, member detailing. Missing and unsupported
/// checks stay visible; standards are never forced to the same set of capabilities.
/// </summary>
[TestClass]
public class Step4ExamplesTest
{
    private static IReadOnlyList<CheckResult> Run(GPC.Model.Models.Model m, ConcreteVerificationOptions options, BeamCheckPlanRequest? plan = null)
        => new Service().Verify(m, Request(plan ?? Plan(), ("example", options))).Jobs[0].Results;
    private static IEnumerable<CheckResult> Of(IReadOnlyList<CheckResult> results, string key) => results.Where(r => (r.Check?.Key ?? r.MethodId) == key);
    private static string All(IEnumerable<CheckResult> results) => string.Join("\n", results.Select(Describe));
    private const string Uls = "UlsBiaxialSection/None/Default/Ultimate", V1 = "Shear/Axis1/Default/Ultimate", V2 = "Shear/Axis2/Default/Ultimate",
        T = "Torsion/None/Default/Ultimate", StressChar = "Serviceability/None/StressLimits/Characteristic", StressQp = "Serviceability/None/StressLimits/QuasiPermanent",
        CrackChar = "Serviceability/None/CrackWidth/Characteristic", CrackQp = "Serviceability/None/CrackWidth/QuasiPermanent";

    /// <summary>
    /// Example 1 — NTC 2018 beam: tip load 6 kN (M = 30 kNm at the fixed end), torque 3 kNm, XC3, ordinary bars. Every check is evaluated and
    /// satisfied; the characteristic crack check is not required by NTC (not applicable with the reason).
    /// </summary>
    [TestMethod]
    public void Example1_NtcBeamWithEveryCheck()
    {
        var results = Run(Model(torque: 3e6, shear: 6000), Options());
        Assert.AreEqual(67, results.Count, "8 samples x 8 section checks + 3 member detailing tasks");
        Assert.IsTrue(Of(results, CrackChar).All(r => r.Applicability == CheckApplicability.NotApplicable), All(results));
        var evaluated = results.Where(r => r.Applicability == CheckApplicability.Required).ToArray();
        Assert.IsTrue(evaluated.All(r => r.Execution == ExecutionStatus.Completed && r.Outcome == EngineeringOutcome.Satisfied), All(evaluated.Where(r => r.Outcome != EngineeringOutcome.Satisfied)));
        // The governing results stay separated by task specification and member method.
        var governing = new Service().Verify(Model(torque: 3e6, shear: 6000), Request(Plan(), ("example", Options()))).Jobs[0].GoverningByConfiguration;
        Assert.IsTrue(governing.Count >= 7, governing.Count + " governing configurations");
        // Largest ULS ratio at the fixed end (M = 30 kNm), not at the tip.
        var uls = Of(results, Uls).OrderByDescending(r => r.Utilization).First();
        Assert.AreEqual(250, uls.ElementId); Assert.AreEqual(0, uls.Station);
    }

    /// <summary>
    /// Example 2 — the same beam with EN 1992-1-1: crack control only in the quasi-permanent combination (XC3: 0.3 mm), shear and torsion with the
    /// Eurocode strut, detailing with the recommended values (ρw,min, sl,max, leg spacing). Compared with NTC: same forces, different capacities.
    /// </summary>
    [TestMethod]
    public void Example2_SameBeamWithEurocode2()
    {
        var m = Model(torque: 3e6, shear: 6000);
        var ntc = Run(m, Options()); var en = Run(m, Options(new StandardEN1992p11()));
        Assert.IsTrue(en.Where(r => r.Applicability == CheckApplicability.Required).All(r => r.Outcome == EngineeringOutcome.Satisfied), All(en));
        Assert.IsTrue(Of(en, CrackQp).All(r => r.Details!.Metrics.Single().Capacity == .3));
        Assert.IsTrue(Of(en, CrackChar).All(r => r.ApplicabilityReason!.Contains("QuasiPermanent")));
        // The Eurocode strut (ν fcd = 0.54 fcd) is stronger than the NTC one (0.5 fcd), with αcc = 1 (EN) against 0.85 (NTC).
        double Capacity(IReadOnlyList<CheckResult> results) => Of(results, T).Where(r => r.Station == 0 && r.ElementId == 250).Single().Details!.Trace.First(t => t.Key == "TRcd").Value!.Value;
        Assert.IsTrue(Capacity(en) > Capacity(ntc));
        var detailing = Of(en, ConcreteSectionVerifier.BeamDetailingMethod).First();
        Assert.IsTrue(detailing.Details!.MethodId.EndsWith(".EN1992p11.Beam") && detailing.Details.Metrics.Any(x => x.Key == "LinkLegSpacing:Bottom"));
    }

    /// <summary>
    /// Example 3 — UNI EN 1992-1-1 column: compression 600 kN, small bending. The link spacing (200 mm) exceeds the DM 31/07/2012 limit
    /// min(12 Ømin; b; 250) = 192 mm: member detailing not satisfied, while the section checks pass.
    /// </summary>
    [TestMethod]
    public void Example3_UniColumnWithLinkSpacingBeyondTheAnnex()
    {
        var plan = Plan(memberMethod: ConcreteSectionVerifier.ColumnDetailingMethod);
        var results = Run(Model(torque: 0, axial: -6e5, shear: 2000), Options(new StandardUNIEN1992p11()), plan);
        var detailing = Of(results, ConcreteSectionVerifier.ColumnDetailingMethod).ToArray();
        Assert.AreEqual(3, detailing.Length);
        Assert.IsTrue(detailing.All(r => r.Outcome == EngineeringOutcome.NotSatisfied), All(detailing));
        var spacing = detailing[0].Details!.Metrics.Single(x => x.Key == "LinkSpacing");
        Assert.AreEqual(192, spacing.Capacity!.Value, 1e-12); Assert.AreEqual(200, spacing.Demand!.Value, 1e-12);
        // NEd of the member: the largest compression among the samples of the state.
        Assert.AreEqual(6e5, detailing[0].Details.Trace.Single(t => t.Key == "NEd").Value!.Value, 1e-6);
        Assert.IsTrue(results.Where(r => r.Scope == CheckScope.SectionSample && r.Applicability == CheckApplicability.Required)
            .All(r => r.Outcome == EngineeringOutcome.Satisfied), All(results.Where(r => r.Scope == CheckScope.SectionSample)));
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, new Service().Verify(Model(torque: 0, axial: -6e5, shear: 2000), Request(plan, ("uni", Options(new StandardUNIEN1992p11())))).Outcome);
    }

    /// <summary>
    /// Example 4 — NTC beam with sensitive reinforcement in an aggressive environment (XD1): the quasi-permanent check is decompression of the
    /// uncracked section, not satisfied where bending tensions the bottom fibre; the free end (M = 0) is satisfied. The DS job shows the national
    /// detailing rules not implemented (not supported) and the DK NA combined V-T-N-M rule declared as not applied.
    /// </summary>
    [TestMethod]
    public void Example4_DecompressionAndDanishNationalRules()
    {
        var m = Model(torque: 3e6, shear: 6000, exposure: "XD1", sensitive: true);
        var ntc = Run(m, Options());
        var decompression = Of(ntc, CrackQp).ToArray();
        Assert.IsTrue(decompression.All(r => r.Details?.UtilizationDefinition == "decompression"), All(decompression));
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, decompression.Single(r => r.ElementId == 250 && r.Station == 0).Outcome);
        Assert.AreEqual(EngineeringOutcome.Satisfied, decompression.Single(r => r.ElementId == 251 && r.Station == 0).Outcome, "free end: no bending");
        var ds = Run(Model(torque: 3e6, shear: 6000), Options(new StandardDSEN1992p11()));
        Assert.IsTrue(Of(ds, ConcreteSectionVerifier.BeamDetailingMethod).All(r => r.Data == DataStatus.NotSupported
            && r.Diagnostics.Any(d => d.Code == "DetailingRuleNotImplemented")), All(ds));
        Assert.IsTrue(Of(ds, T).All(r => r.Diagnostics.Any(d => d.Code == "TorsionRuleNotApplied")));
        Assert.IsTrue(Of(ds, CrackQp).All(r => r.Details!.Metrics.Single().Capacity == .4), "DK NA Tabel 7.1 NA: XC3 0.4 mm");
    }

    /// <summary>
    /// Example 5 — what stays visible: DIN EN 1992-1-1 (detailing not supported, quadratic shear-torsion interaction of solid sections), Model Code
    /// 2010 without design wlim (crack check with insufficient data), CS-TR34 (only the ULS domain applies) and a section without crack data.
    /// </summary>
    [TestMethod]
    public void Example5_UnsupportedAndMissingChecksStayVisible()
    {
        var din = Run(Model(torque: 3e6, shear: 6000), Options(new StandardDINEN1992p11()));
        Assert.IsTrue(Of(din, ConcreteSectionVerifier.BeamDetailingMethod).All(r => r.Data == DataStatus.NotSupported), All(din));
        Assert.IsTrue(Of(din, T).All(r => r.Details!.Trace.Any(t => t.Key == "interaction" && t.Value == 2)));
        var mc = Run(Model(torque: 3e6, shear: 6000), Options(new StandardModelCode2010()));
        Assert.IsTrue(Of(mc, CrackQp).All(r => r.Data == DataStatus.Insufficient && r.Diagnostics.Any(d => d.Code == "CrackMissingDesignLimit")), All(mc));
        var floors = Run(Model(torque: 3e6, shear: 6000), Options(new StandardCSTR34()));
        Assert.IsTrue(floors.Where(r => (r.Check?.Key ?? "") != Uls).All(r => r.Applicability == CheckApplicability.NotApplicable), All(floors));
        Assert.IsTrue(Of(floors, Uls).All(r => r.Execution == ExecutionStatus.Completed));
        var m = Model(torque: 3e6, shear: 6000); m.BeamElements[250].Assignments.Sections[0].Section.CrackData = null;
        var noData = Run(m, Options());
        Assert.IsTrue(Of(noData, CrackQp).All(r => r.Data == DataStatus.Insufficient && r.Diagnostics.Any(d => d.Code == "MissingCrackData")));
        // The summary never reports a complete success while checks are missing or unsupported.
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, new Service().Verify(m, Request(Plan(), ("missing", Options()))).Outcome);
    }
}
