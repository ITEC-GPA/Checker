using GPC.Model.Models;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Checker;
using GPC.Model.Persistence;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Results.Queries;
using GPC.Model.Structure.Members;

namespace ModelChecker.Tests;

/// <summary>Step 4F: 1D detailing of beams and columns as one physical-member task per member and state, on the real Concrete engine.</summary>
[TestClass]
public class MemberDetailingTest
{
    private static GPC.Model.Models.Model Model(bool data = true)
    {
        var m = ElementScopeCharacterizationTest.Model();
        m.PhysicalMembers.Add("T1", new PhysicalMemberDefinition("T1", new[] { new BeamMemberPart(250, true), new BeamMemberPart(251, false) }, "NodeToNode", source: "fixture"));
        var section = m.BeamElements[250].Assignments.Sections[0].Section;
        // Fixture RC 300x500, 4Ø16 at 50 mm from the edges, links Ø8/200 with 2 legs: cover to the links 34 mm, cmin,dur 25 mm, Δcdev 5 mm.
        section.ShearData = new ConcreteShearData(new ConcreteShearReinforcement(8, 200, section.Rebars.First().RebarMaterial),
            new ConcreteShearDirection(500, 250, 402, true, 2), new ConcreteShearDirection(300, 450, 402, true, 2), "fixture drawings", 20);
        if (data) section.DetailingData = new ConcreteDetailingData(34, 25, 5, 300, 300, 300, 20, "fixture drawings", compressionBarsRestrained: true, endZonesConfirmed: true);
        return m;
    }

    private static ModelCheckRequest Request(string method, StandardModelCode2010? standard = null)
    {
        var plan = new BeamCheckPlanRequest { MemberIds = new[] { "T1" }, Settings = "step4 detailing", SectionMechanisms = new CheckMechanism[0],
            Results = new[] { new ResultSelection { Dataset = "synthetic-member", Case = "LC1", ConcomitantState = "LC1", Category = CombinationCategory.Ultimate } },
            MemberChecks = new[] { new MemberCheckSpecification { MemberId = "T1", MethodId = method, Mechanism = CheckMechanism.Detailing,
                Context = new MemberDesignContext(globalStateConfirmed: true) } } };
        var request = new ModelCheckRequest();
        request.Jobs.Add(new ModelCheckJob { Name = "detailing", BeamPlan = plan, Options = new ConcreteVerificationOptions { Standard = standard ?? new StandardNTC2018Concrete(),
            Criterion = SectionSolver.FailureAnalysisTypes.ConstantEccentricity } });
        return request;
    }

    private static CheckResult Run(GPC.Model.Models.Model m, string method, StandardModelCode2010? standard = null)
        => new Service().Verify(m, Request(method, standard)).Jobs[0].Results.Single();
    private static string Describe(CheckResult r) => r.Data + " " + r.Outcome + " " + string.Join(",", r.Diagnostics.Select(d => d.Code + ":" + d.Message))
        + " | " + string.Join(", ", r.Details?.Metrics.Select(x => x.Key + " " + x.Demand + "/" + x.Capacity + " " + x.Passed) ?? new string[0]);

    [TestMethod]
    public void EquivalentCopiesAreDeduplicatedButDifferentSectionsWithTheSameNameRemainDistinct()
    {
        var model = Model();
        var original = model.BeamElements[250].Assignments.Sections[0].Section;
        var different = ModelArchive.CopyValue(original);
        different.ShearData = new ConcreteShearData(new ConcreteShearReinforcement(8, 100, different.Rebars.First().RebarMaterial),
            new ConcreteShearDirection(500, 250, 402, true, 2), new ConcreteShearDirection(300, 450, 402, true, 2), "fixture drawings", 20);
        model.BeamElements[251].Assignments.Sections[0].Section = different;
        Assert.AreEqual(original.Name, different.Name);
        var result = Run(model, ConcreteSectionVerifier.BeamDetailingMethod);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, Describe(result));
        var metrics = result.Details!.Metrics;
        Assert.AreEqual(metrics.Count, metrics.Select(m => m.Key).Distinct().Count());
        var links = metrics.Where(m => m.Key.StartsWith("MinimumLinks:Bottom@")).OrderBy(m => m.Demand).ToArray();
        Assert.AreEqual(2, links.Length, "Every distinct section is checked once, regardless of the number of samples or common names.");
        Assert.AreEqual(2 * links[0].Demand!.Value, links[1].Demand!.Value, 1e-9);
    }

    [TestMethod]
    public void BeamDetailingIsOneMemberTaskAndSatisfiesNtc()
    {
        var m = Model(); var plan = BeamCheckPlan.Prepare(m, Request(ConcreteSectionVerifier.BeamDetailingMethod).Jobs[0].BeamPlan);
        Assert.AreEqual(1, plan.WorkItems.Count, "one task for the member, not one per FEM sample");
        var r = Run(m, ConcreteSectionVerifier.BeamDetailingMethod);
        Assert.AreEqual(CheckScope.PhysicalMember, r.Scope); Assert.AreEqual("T1", r.Target.MemberId);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Describe(r));
        var details = (DetailingCheckDetails)r.Details!;
        Assert.IsTrue(details.MethodId.EndsWith(".Ntc2018.Beam"), details.MethodId);
        // NTC 4.1.6.1.1: Ast/s = 2·π·8²/4·1000/200 = 502.7 mm²/m ≥ 1.5 b = 450 mm²/m.
        var links = details.Metrics.Single(x => x.Key == "MinimumLinks:Bottom");
        Assert.AreEqual(2 * Math.PI * 16 * 1000 / 200, links.Demand!.Value, 1e-9); Assert.AreEqual(450, links.Capacity!.Value, 1e-12);
        Assert.IsTrue(details.Metrics.Any(x => x.Key == "BarCoverMargin" && x.Passed == true));
    }

    [TestMethod]
    public void ColumnDetailingFailsTheLinkSpacingAndEurocodeUsesItsRules()
    {
        var m = Model();
        // NTC columns: link spacing ≤ min(250; 12 Ømin) = 192 mm < 200 mm.
        var ntc = Run(m, ConcreteSectionVerifier.ColumnDetailingMethod);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, ntc.Outcome, Describe(ntc));
        Assert.AreEqual(false, ntc.Details!.Metrics.Single(x => x.Key == "LinkSpacing").Passed);
        // EN 1992-1-1 9.5.3(3): min(20 Ømin; b; 400) = 300 mm ≥ 200 mm; the beam rules give ρw,min and the leg spacing.
        var en = Run(m, ConcreteSectionVerifier.ColumnDetailingMethod, new StandardEN1992p11());
        Assert.AreEqual(EngineeringOutcome.Satisfied, en.Outcome, Describe(en));
        Assert.AreEqual(300, en.Details!.Metrics.Single(x => x.Key == "LinkSpacing").Capacity!.Value, 1e-12);
        var beam = Run(m, ConcreteSectionVerifier.BeamDetailingMethod, new StandardEN1992p11());
        Assert.AreEqual(EngineeringOutcome.Satisfied, beam.Outcome, Describe(beam));
        Assert.AreEqual(.08 * Math.Sqrt(25) / 450 * 300 * 1000, beam.Details!.Metrics.Single(x => x.Key == "MinimumLinks:Bottom").Capacity!.Value, 1e-9);
        Assert.AreEqual((300 - 2 * 34 - 8) / 1.0, beam.Details.Metrics.Single(x => x.Key == "LinkLegSpacing:Bottom").Demand!.Value, 1e-12);
    }

    [TestMethod]
    public void DetailingDataStandardsAndPendingRulesAreExplicit()
    {
        var missing = Run(Model(false), ConcreteSectionVerifier.BeamDetailingMethod);
        Assert.AreEqual(DataStatus.Insufficient, missing.Data); Assert.IsTrue(missing.Diagnostics.Any(d => d.Code == "MissingDetailingData"), Describe(missing));
        // DS beams: the DK NA values of As,min and ρw,min are not implemented: not supported, with the list of the pending rules.
        var ds = Run(Model(), ConcreteSectionVerifier.BeamDetailingMethod, new StandardDSEN1992p11());
        Assert.AreEqual(DataStatus.NotSupported, ds.Data, Describe(ds)); Assert.IsTrue(ds.Diagnostics.Any(d => d.Code == "DetailingRuleNotImplemented"));
        // Without the end-zone confirmation the beam stays insufficient.
        var m = Model(); var section = m.BeamElements[250].Assignments.Sections[0].Section;
        section.DetailingData = new ConcreteDetailingData(34, 25, 5, 300, 300, 300, 20, "fixture drawings", compressionBarsRestrained: true);
        var unconfirmed = Run(m, ConcreteSectionVerifier.BeamDetailingMethod);
        Assert.AreEqual(DataStatus.Insufficient, unconfirmed.Data, Describe(unconfirmed)); StringAssert.Contains(unconfirmed.Diagnostics.Single(d => d.Code == "DetailingPending").Message, "EndSupportAnchorage");
        Assert.AreEqual(DataStatus.NotSupported, Run(Model(), ConcreteSectionVerifier.BeamDetailingMethod, new StandardDINEN1992p11()).Data);
        Assert.AreEqual(CheckApplicability.NotApplicable, Run(Model(), ConcreteSectionVerifier.BeamDetailingMethod, new StandardCSTR34()).Applicability);
        // Data revision and archive.
        string analysis = m.AnalysisFingerprint(), verification = m.VerificationFingerprint("x");
        section.DetailingData = new ConcreteDetailingData(40, 25, 5, 300, 300, 300, 20, "revised drawings", compressionBarsRestrained: true, endZonesConfirmed: true);
        Assert.AreNotEqual(verification, m.VerificationFingerprint("x")); Assert.AreEqual(analysis, m.AnalysisFingerprint());
        using var stream = new MemoryStream(); ModelArchive.Save(m, stream); stream.Position = 0;
        Assert.AreEqual(section.DetailingData, ModelArchive.Load(stream).BeamElements[250].Assignments.Sections[0].Section.DetailingData);
    }

    /// <summary>Detailing data of the fixture section in a lap zone, with the arrangement of the laps when given (GPCModel 5.36.0.0).</summary>
    private static GPC.Model.Models.Model LapModel(double? lappedPercent, int? lapLayers, bool lapZone = true)
    {
        var m = Model();
        m.BeamElements[250].Assignments.Sections[0].Section.DetailingData = new ConcreteDetailingData(34, 25, 5, 300, 300, 300, 20, "fixture drawings", lapZone,
            true, true, lappedPercent, lapLayers);
        return m;
    }
    private static string Metrics(CheckResult r, string? except = null) => string.Join("; ", r.Details!.Metrics.Where(x => x.Key != except).Select(x => x.Key + " "
        + x.Demand?.ToString("R") + "/" + x.Capacity?.ToString("R") + " " + x.Unit + " " + x.Passed + " " + x.Reference));
    private static string Rows(CheckResult r) => r.Data + " " + r.Outcome + " | " + Metrics(r) + " | " + string.Join("; ", r.Diagnostics.Select(d => d.Code + ":" + d.Message));

    [TestMethod]
    public void EurocodeLapSectionsReadTheLapArrangementOfTheDetailingData()
    {
        var en = new StandardEN1992p11();
        // EN 1992-1-1 8.7.2(4), by hand: tension bars lapped in one section ≤ 100% with all of them in one layer, ≤ 50% in several layers.
        // 100% in one layer: 100 ≤ 100, satisfied.
        var one = Run(LapModel(100, 1), ConcreteSectionVerifier.BeamDetailingMethod, en);
        Assert.AreEqual(EngineeringOutcome.Satisfied, one.Outcome, Describe(one)); Assert.AreEqual(DataStatus.Ready, one.Data);
        var metric = one.Details!.Metrics.Single(x => x.Key == "LapPercentage");
        Assert.AreEqual(100, metric.Demand!.Value, 0); Assert.AreEqual(100, metric.Capacity!.Value, 0); Assert.AreEqual("%", metric.Unit); Assert.AreEqual(true, metric.Passed);
        StringAssert.Contains(metric.Reference, "8.7.2(4)");
        // 100% in two layers: 100 > 50, not satisfied.
        var two = Run(LapModel(100, 2), ConcreteSectionVerifier.BeamDetailingMethod, en);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, two.Outcome, Describe(two));
        metric = two.Details!.Metrics.Single(x => x.Key == "LapPercentage");
        Assert.AreEqual(100, metric.Demand!.Value, 0); Assert.AreEqual(50, metric.Capacity!.Value, 0); Assert.AreEqual(false, metric.Passed);
        Assert.IsTrue(two.Details.Metrics.Where(x => x.Key != "LapPercentage").All(x => x.Passed == true), Describe(two));
        // 50% in three layers: 50 ≤ 50, satisfied.
        var three = Run(LapModel(50, 3), ConcreteSectionVerifier.BeamDetailingMethod, en);
        Assert.AreEqual(EngineeringOutcome.Satisfied, three.Outcome, Describe(three));
        metric = three.Details!.Metrics.Single(x => x.Key == "LapPercentage");
        Assert.AreEqual(50, metric.Demand!.Value, 0); Assert.AreEqual(50, metric.Capacity!.Value, 0); Assert.AreEqual(true, metric.Passed);
        // Without the fields: insufficient data, the rule pending with its reason, as before the fields.
        var missing = Run(LapModel(null, null), ConcreteSectionVerifier.BeamDetailingMethod, en);
        Assert.AreEqual(DataStatus.Insufficient, missing.Data, Describe(missing)); Assert.AreEqual(EngineeringOutcome.NotEvaluated, missing.Outcome);
        StringAssert.Contains(missing.Diagnostics.Single(d => d.Code == "DetailingPending").Message, "LapPercentage");
        Assert.IsFalse(missing.Details!.Metrics.Any(x => x.Key == "LapPercentage"));
        // The other checks of the lap section are the same with and without the fields.
        Assert.AreEqual(Metrics(missing), Metrics(one, "LapPercentage"), Rows(one));
        // NTC 2018 and sections outside the lap zone: the fields are not read, the results are those without them.
        foreach (var (standard, zone) in new (StandardModelCode2010?, bool)[] { (null, true), (null, false), (en, false), (new StandardUNIEN1992p11(), false) })
        {
            string expected = Rows(Run(LapModel(null, null, zone), ConcreteSectionVerifier.BeamDetailingMethod, standard));
            foreach (var model in new[] { LapModel(100, 2, zone), LapModel(0, 1, zone) })
                Assert.AreEqual(expected, Rows(Run(model, ConcreteSectionVerifier.BeamDetailingMethod, standard)), (standard?.GetType().Name ?? "NTC") + " " + zone);
        }
    }
}
