using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Checker;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using static ModelChecker.Tests.Step4Fixture;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Structure.Members;

namespace ModelChecker.Tests;

/// <summary>Step 4G: coverage cases of the new concrete checks (shear, torsion, serviceability stresses and cracking, member detailing).</summary>
[TestClass]
public class Step4CoverageTest
{
    /// <summary>The common node of FEM 250 (I→J) and FEM 251 (reversed) is checked from both elements with the same physical forces and results.</summary>
    [TestMethod]
    public void ReversedElementGivesTheSameResultsAtTheCommonNode()
    {
        var report = new Service().Verify(Model(), Request(Plan(memberMethod: null)));
        var joint = report.Jobs[0].Results.Where(r => r.Scope == CheckScope.SectionSample && r.Station == 1).ToArray();
        foreach (var group in joint.GroupBy(r => r.Check!.Key))
        {
            var rows = group.ToArray();
            Assert.AreEqual(2, rows.Length, group.Key); Assert.AreNotEqual(rows[0].ElementId, rows[1].ElementId, group.Key);
            Assert.AreEqual(rows[0].Outcome, rows[1].Outcome, group.Key + ": " + Describe(rows[0]) + " | " + Describe(rows[1]));
            Assert.AreEqual(rows[0].Utilization.HasValue, rows[1].Utilization.HasValue, group.Key);
            if (rows[0].Utilization.HasValue) Assert.AreEqual(rows[0].Utilization!.Value, rows[1].Utilization!.Value, 1e-9, group.Key);
        }
        Assert.AreEqual(AllSectionChecks.Length, joint.Select(r => r.Check!.Key).Distinct().Count());
    }

    /// <summary>The moved bar makes the section asymmetric: reversing the bending changes stresses and crack width, not the symmetric shear.</summary>
    [TestMethod]
    public void AsymmetricReinforcementChangesSignDependentChecks()
    {
        double Utilization(double shear, SectionCheckSpecification check)
        {
            var report = new Service().Verify(Model(shear: shear), Request(Plan(new[] { check }, null, check.Category)));
            return report.Jobs[0].Results.Where(r => r.Station == 0 && r.ElementId == 250).Single().Utilization!.Value;
        }
        var stress = SectionCheckSpecification.StressLimits(CombinationCategory.Characteristic);
        Assert.AreNotEqual(Utilization(8000, stress), Utilization(-8000, stress), 1e-6, "positive and negative bending of the asymmetric section");
        var shear = SectionCheckSpecification.ShearAxis2();
        Assert.AreEqual(Utilization(8000, shear), Utilization(-8000, shear), 1e-12, "the shear check uses |V|");
    }

    /// <summary>A required location without result stays visible for every new task (denominator kept, insufficient data).</summary>
    [TestMethod]
    public void MissingRequiredStationKeepsTheNewTasks()
    {
        var m = Model(); m.BeamElements[250].Results[0].Results.RemoveAll(r => ((StationResultBeamForces)r).ParametricDistance == .5);
        var plan = Plan(new[] { SectionCheckSpecification.Torsion(), SectionCheckSpecification.CrackWidth(CombinationCategory.QuasiPermanent) }, null,
            CombinationCategory.Ultimate, CombinationCategory.QuasiPermanent);
        plan.CoveragePolicy = BeamCoveragePolicy.RequiredLocations;
        plan.Locations = new[] { 0.0, 1000, 2000 }.Select(d => new RequiredBeamLocation { MemberId = "T1", Distance = d }).ToArray();
        var results = new Service().Verify(m, Request(plan)).Jobs[0].Results;
        var missing = results.Where(r => r.MemberLocation?.Distance == 1000).ToArray();
        Assert.AreEqual(2, missing.Length);
        Assert.IsTrue(missing.All(r => r.Data == DataStatus.Insufficient && r.Diagnostics.Any(d => d.Code == "MissingRequiredLocation")), string.Join(";", missing.Select(Describe)));
    }

    /// <summary>Frequent category: NTC requires crack control (w3 = 0.4 mm), Eurocode 2 only in the quasi-permanent combination.</summary>
    [TestMethod]
    public void FrequentCategoryFollowsTheStandard()
    {
        var plan = Plan(new[] { SectionCheckSpecification.CrackWidth(CombinationCategory.Frequent) }, null, CombinationCategory.Frequent);
        var report = new Service().Verify(Model(), Request(plan, ("NTC", Options()), ("EN", Options(new StandardEN1992p11()))));
        var ntc = report.Jobs[0].Results; var en = report.Jobs[1].Results;
        Assert.IsTrue(ntc.All(r => r.Applicability == CheckApplicability.Required && r.Details?.Metrics.Single().Capacity == .4), string.Join(";", ntc.Select(Describe)));
        Assert.IsTrue(en.All(r => r.Applicability == CheckApplicability.NotApplicable && r.ApplicabilityReason!.Contains("QuasiPermanent")));
    }

    /// <summary>A tension beyond the reinforcement capacity: the nonlinear stress analysis has no solution and the task is not a success.</summary>
    [TestMethod]
    public void NonConvergenceIsNotASuccess()
    {
        var options = Options(); options.ServiceabilityAnalysis = SectionSolver.StressAnalysisTypes.NonLinear;
        var plan = Plan(new[] { SectionCheckSpecification.StressLimits(CombinationCategory.Characteristic) }, null, CombinationCategory.Characteristic);
        var results = new Service().Verify(Model(axial: 2e6), Request(plan, ("NTC", options))).Jobs[0].Results;
        Assert.IsTrue(results.All(r => r.Outcome != EngineeringOutcome.Satisfied), string.Join(";", results.Select(Describe)));
        // 2 MN of tension against 4Ø16 (≈ 360 kN at yield): every sample is an execution error with insufficient data, never a ratio.
        Assert.IsTrue(results.All(r => r.Execution == ExecutionStatus.Error && r.Data == DataStatus.Insufficient && !r.Utilization.HasValue
            && r.Diagnostics.Any(d => d.Code == "CheckerStressAnalysisNotConverged")), string.Join(";", results.Select(Describe)));
    }

    /// <summary>Different standards or parameters never share a native checker; identical options reuse it.</summary>
    [TestMethod]
    public void CacheSeparatesStandardsAndParameters()
    {
        var plan = Plan(new[] { new SectionCheckSpecification(CheckMechanism.UlsBiaxialSection, CombinationCategory.Ultimate),
            SectionCheckSpecification.CrackWidth(CombinationCategory.QuasiPermanent) }, null, CombinationCategory.Ultimate, CombinationCategory.QuasiPermanent);
        var one = new Service().Verify(Model(), Request(plan, ("A", Options())));
        var same = new Service().Verify(Model(), Request(plan, ("A", Options()), ("B", Options())));
        Assert.AreEqual(one.CreatedCheckers, same.CreatedCheckers, "identical configuration: checkers reused");
        var otherParameter = Options(); otherParameter.PsiRebar = 0;
        var different = new Service().Verify(Model(), Request(plan, ("A", Options()), ("B", otherParameter), ("C", Options(new StandardEN1992p11()))));
        Assert.AreEqual(3 * one.CreatedCheckers, different.CreatedCheckers, "creep coefficient and standard are part of the configuration");
        Assert.AreNotEqual(different.Jobs[0].Results[0].EngineConfiguration, different.Jobs[1].Results[0].EngineConfiguration);
    }

    /// <summary>Changing the bars, the links or the crack data invalidates the plan and the current outcome of a stored report.</summary>
    [TestMethod]
    public void ChangedReinforcementInvalidatesPlanAndReport()
    {
        var m = Model(); var request = Request(Plan());
        var report = new Service().Verify(m, request); var job = report.Jobs[0];
        var verifier = request.Jobs[0].Options.CreateVerifier();
        Assert.AreEqual(job.Outcome, job.CurrentOutcome(m, verifier, verifier, request.Jobs[0].BeamPlan));
        var section = m.BeamElements[250].Assignments.Sections[0].Section;
        var plan = BeamCheckPlan.Prepare(m, request.Jobs[0].BeamPlan);
        section.Rebars.First().Position.X += 10;
        Assert.IsFalse(plan.IsCurrent, "moved bar");
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, job.CurrentOutcome(m, verifier, verifier, request.Jobs[0].BeamPlan));
    }

    /// <summary>A report with every new detail type survives the archive with sealed evidence and stays current.</summary>
    [TestMethod]
    public void ReportWithEveryNewDetailSurvivesTheArchive()
    {
        var m = Model(); var request = Request(Plan());
        var report = new Service().Verify(m, request); m.CheckReports.AddRange(report.Jobs);
        using var stream = new MemoryStream(); ModelArchive.Save(m, stream); stream.Position = 0;
        var restored = ModelArchive.Load(stream); var saved = restored.CheckReports.Single();
        Assert.AreEqual(report.Jobs[0].Results.Count, saved.Results.Count);
        Assert.IsTrue(saved.Results.All(r => r.HasUnchangedEvidence));
        Assert.IsTrue(saved.Results.Any(r => r.Details is TorsionCheckDetails t && t.RequiredLongitudinalArea > 0));
        Assert.IsTrue(saved.Results.Any(r => r.Details is ServiceabilityCheckDetails s && s.UtilizationDefinition == "crack-width"));
        Assert.IsTrue(saved.Results.Any(r => r.Details is DetailingCheckDetails && r.Scope == CheckScope.PhysicalMember));
        Assert.IsTrue(saved.Results.Any(r => r.Details is ShearCheckDetails));
        var verifier = request.Jobs[0].Options.CreateVerifier();
        Assert.AreEqual(saved.Outcome, saved.CurrentOutcome(restored, verifier, verifier, request.Jobs[0].BeamPlan));
    }

    /// <summary>An already cancelled run keeps every planned task, section and member, as cancelled.</summary>
    [TestMethod]
    public void CancellationKeepsEveryNewTask()
    {
        var report = new Service().Verify(Model(), Request(Plan()), new CancellationToken(true));
        Assert.AreEqual(8 * 8 + 3, report.Required, "8 samples x 8 specifications, each on its category, + 1 member task per state");
        Assert.AreEqual(report.Required, report.Summary.Cancelled);
    }
}
