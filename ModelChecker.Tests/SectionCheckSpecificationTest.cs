using GPC.Model.Models;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Shear;
using GPC.Checkers.Concrete.Torsion;
using GPC.Model.Sections.Concrete;
using GPC.Model.Results.Locations;
using GPC.Geometry;
using GPC.Model.Checker;
using GPC.Model.Materials;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Compatibility;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Results.Queries;
using GPC.Model.Structure.Members;

namespace ModelChecker.Tests;

/// <summary>Step 4A/4B: typed discriminators in the plan and serviceability stress limits on the real Concrete engine.</summary>
[TestClass]
public class SectionCheckSpecificationTest
{
    private static ResultSelection Selection(CombinationCategory? category) => new() { Dataset = "synthetic-member", Case = "LC1", ConcomitantState = "LC1", Category = category };

    private static BeamCheckPlanRequest Plan(params SectionCheckSpecification[] checks) => new()
    {
        MemberIds = new[] { "T1" }, Settings = "step4 discriminators", SectionMechanisms = new CheckMechanism[0], SectionChecks = checks,
        Results = new[] { Selection(CombinationCategory.Ultimate), Selection(CombinationCategory.Characteristic), Selection(CombinationCategory.QuasiPermanent) }
    };

    private static GPC.Model.Models.Model Model(double torque = 0)
    {
        var m = ElementScopeCharacterizationTest.Model(torque);
        m.PhysicalMembers.Add("T1", new PhysicalMemberDefinition("T1", new[] { new BeamMemberPart(250, true), new BeamMemberPart(251, false) }, "NodeToNode", source: "fixture"));
        return m;
    }

    private static ConcreteVerificationOptions Options(StandardModelCode2010? standard = null, double? cotTheta = null) => new()
    {
        Standard = standard ?? new StandardNTC2018Concrete(), Criterion = SectionSolver.FailureAnalysisTypes.ConstantEccentricity,
        ServiceabilityAnalysis = SectionSolver.StressAnalysisTypes.Linear, PsiRebar = 2, ShearCotTheta = cotTheta
    };

    private static ModelCheckRequest Request(BeamCheckPlanRequest plan)
    {
        var request = new ModelCheckRequest();
        request.Jobs.Add(new ModelCheckJob { Name = "step4", BeamPlan = plan, Options = Options() });
        return request;
    }

    [TestMethod]
    public void DirectionsAndServiceCategoriesAreDistinctTasks_OnlyForDeclaredSelections()
    {
        var m = Model();
        var plan = BeamCheckPlan.Prepare(m, Plan(SectionCheckSpecification.ShearAxis1(), SectionCheckSpecification.ShearAxis2(),
            SectionCheckSpecification.StressLimits(CombinationCategory.Characteristic), SectionCheckSpecification.StressLimits(CombinationCategory.QuasiPermanent)));
        // 8 exported samples: two shear directions on the ULS selection, one stress task per service selection.
        Assert.AreEqual(32, plan.WorkItems.Count);
        Assert.AreEqual(32, plan.WorkItems.Select(w => w.Id).Distinct().Count());
        Assert.AreEqual(8, plan.WorkItems.Count(w => w.Check!.Key == "Shear/Axis1/Default/Ultimate"));
        Assert.AreEqual(8, plan.WorkItems.Count(w => w.Check!.Key == "Shear/Axis2/Default/Ultimate"));
        Assert.IsTrue(plan.WorkItems.Where(w => w.Check!.Mechanism == CheckMechanism.Shear).All(w => w.Selection.Category == CombinationCategory.Ultimate));
        Assert.IsTrue(plan.WorkItems.Where(w => w.Check!.Category == CombinationCategory.QuasiPermanent).All(w => w.Selection.Category == CombinationCategory.QuasiPermanent));
    }

    [TestMethod]
    public void IncoherentSpecificationsAndMissingCategoriesAreRejected()
    {
        var m = Model();
        Assert.ThrowsException<ArgumentException>(() => BeamCheckPlan.Prepare(m, Plan(new SectionCheckSpecification(CheckMechanism.Shear, CombinationCategory.Ultimate))));
        Assert.ThrowsException<ArgumentException>(() => BeamCheckPlan.Prepare(m, Plan(new SectionCheckSpecification(CheckMechanism.Serviceability, CombinationCategory.Ultimate, criterion: SectionCheckCriterion.StressLimits))));
        Assert.ThrowsException<ArgumentException>(() => BeamCheckPlan.Prepare(m, Plan(SectionCheckSpecification.ShearAxis1(), SectionCheckSpecification.ShearAxis1())));
        var twice = Plan(SectionCheckSpecification.ShearAxis1()); twice.SectionMechanisms = new[] { CheckMechanism.Shear };
        Assert.ThrowsException<ArgumentException>(() => BeamCheckPlan.Prepare(m, twice));
        var frequent = Plan(SectionCheckSpecification.StressLimits(CombinationCategory.Frequent));
        var error = Assert.ThrowsException<ArgumentException>(() => BeamCheckPlan.Prepare(m, frequent));
        StringAssert.Contains(error.Message, "NoResultSelectionForCategory");
    }

    [TestMethod]
    public void LegacyRequestsKeepTheirFingerprints()
    {
        var m = Model();
        var legacy = new BeamCheckPlanRequest { MemberIds = new[] { "T1" }, Settings = "legacy", Results = new[] { Selection(null) } };
        var explicitNull = legacy.Copy(); explicitNull.SectionChecks = null;
        Assert.AreEqual(BeamCheckPlan.FingerprintScope(m, legacy), BeamCheckPlan.FingerprintScope(m, explicitNull));
        var declared = legacy.Copy(); declared.Results[0].Category = CombinationCategory.Ultimate;
        Assert.AreNotEqual(BeamCheckPlan.FingerprintScope(m, legacy), BeamCheckPlan.FingerprintScope(m, declared));
        // Legacy mechanisms keep the identity formula that does not contain any specification.
        var plan = BeamCheckPlan.Prepare(m, legacy);
        Assert.IsTrue(plan.WorkItems.All(w => w.Check == null && w.Mechanism == CheckMechanism.UlsBiaxialSection));
    }

    [TestMethod]
    public void StressLimitsRunOnTheRealEngine_ShearWithoutSectionDataIsInsufficient()
    {
        var m = Model();
        var report = new Service().Verify(m, Request(Plan(SectionCheckSpecification.ShearAxis1(),
            SectionCheckSpecification.StressLimits(CombinationCategory.Characteristic), SectionCheckSpecification.StressLimits(CombinationCategory.QuasiPermanent))));
        var results = report.Jobs[0].Results;
        Assert.AreEqual(24, report.Required);
        Assert.IsTrue(results.Where(r => r.Check!.Mechanism == CheckMechanism.Shear).All(r => r.Data == DataStatus.Insufficient
            && r.Outcome == EngineeringOutcome.NotEvaluated && r.Diagnostics.Any(d => d.Code == "MissingShearData")));
        var stresses = results.Where(r => r.Check!.Mechanism == CheckMechanism.Serviceability).ToArray();
        Assert.AreEqual(16, stresses.Length);
        Assert.IsTrue(stresses.All(r => r.Execution == ExecutionStatus.Completed && r.Outcome == EngineeringOutcome.Satisfied), string.Join(";", stresses.SelectMany(r => r.Diagnostics).Select(d => d.Code + d.Message)));
        var characteristic = stresses.Where(r => r.Check!.Category == CombinationCategory.Characteristic).Select(r => (ServiceabilityCheckDetails)r.Details).ToArray();
        Assert.IsTrue(characteristic.All(d => d.CombinationCategory == "Characteristic" && d.Metrics.Any(x => x.Key == "SteelTension")));
        Assert.IsTrue(stresses.Where(r => r.Check!.Category == CombinationCategory.QuasiPermanent).All(r => r.Details!.Metrics.Single().Key == "ConcreteCompression"));
        // Governing results stay separated by task specification.
        Assert.AreEqual(2, report.Jobs[0].GoverningByConfiguration.Count);
        Assert.ThrowsException<InvalidOperationException>(() => report.Jobs[0].Governing(CheckMechanism.Serviceability));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
    }

    [TestMethod]
    public void StressLimitsMatchDirectCheckerCallAndNtcCoefficients()
    {
        var m = Model(); var beam = m.BeamElements[250];
        var sample = Verification.BeamSample(beam, "synthetic-member", "LC1", 0, SectionSide.Unspecified);
        var prepared = Verification.PrepareBeam(m, 250, sample, "direct");
        var verifier = Options().CreateVerifier();
        var result = Verification.Run(prepared, SectionCheckSpecification.StressLimits(CombinationCategory.Characteristic), verifier);
        Assert.AreEqual(ExecutionStatus.Completed, result.Execution, string.Join(";", result.Diagnostics.Select(d => d.Code + d.Message)));
        Assert.AreEqual("Serviceability/None/StressLimits/Characteristic", result.Check!.Key);

        // Independent native call with the same options.
        var section = prepared.Input!.Section; var standard = new StandardNTC2018Concrete();
        var reference = new CoordinateSystem(section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
        var f = prepared.Input.Forces;
        var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(reference, SectionSolver.FailureAnalysisTypes.ConstantEccentricity,
            SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.Linear, 2, 0, false, 64);
        var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options, standard, false);
        var direct = StressLimitCheck.Evaluate(checker.GetTensionAnalysisResult(new ResultBeamForces(f.N, f.V1, f.V2, f.T, f.M1, f.M2, reference)), ServiceabilityCombination.Characteristic);
        Assert.AreEqual(direct.Ratio!.Value, result.Utilization!.Value, 1e-12);

        // NTC 2018 §4.1.2.2.5: 0.60 fck (characteristic), 0.45 fck (quasi-permanent), 0.80 fyk.
        var concrete = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
        Assert.AreEqual(.6 * Math.Abs(concrete.Fck), direct.ConcreteLimit!.Value, 1e-9);
        Assert.AreEqual(.8 * Math.Abs(section.Rebars.First().RebarMaterial.Fyk), direct.SteelGoverning!.Limit, 1e-9);
        var qp = StressLimitCheck.Evaluate(checker.GetTensionAnalysisResult(new ResultBeamForces(f.N, f.V1, f.V2, f.T, f.M1, f.M2, reference)), ServiceabilityCombination.QuasiPermanent);
        Assert.AreEqual(.45 * Math.Abs(concrete.Fck), qp.ConcreteLimit!.Value, 1e-9);
        Assert.IsNull(qp.SteelRatio);
        var frequent = StressLimitCheck.Evaluate(checker.GetTensionAnalysisResult(new ResultBeamForces(f.N, f.V1, f.V2, f.T, f.M1, f.M2, reference)), ServiceabilityCombination.Frequent);
        Assert.IsNull(frequent.Ratio);
        // The explicit thin-casting factor scales the concrete ratio only.
        var thin = StressLimitCheck.Evaluate(checker.GetTensionAnalysisResult(new ResultBeamForces(f.N, f.V1, f.V2, f.T, f.M1, f.M2, reference)), ServiceabilityCombination.Characteristic, .8);
        Assert.AreEqual(direct.ConcreteRatio!.Value / .8, thin.ConcreteRatio!.Value, 1e-12);
        Assert.AreEqual(direct.SteelRatio!.Value, thin.SteelRatio!.Value, 1e-15);
        Assert.IsFalse(verifier.Supports(SectionCheckSpecification.StressLimits(CombinationCategory.Frequent)));
        Assert.IsFalse(verifier.Supports(new SectionCheckSpecification(CheckMechanism.UlsBiaxialSection, CombinationCategory.UltimateSeismic)));
    }

    // Fixture section RC 300x500, 4Ø16 at 50 mm from the edges. V1 along x: bw = 500, d = 250; V2 along y: bw = 300, d = 450; cv = 42 mm.
    private static ConcreteShearData ShearData(ReinforcedConcreteSection section, bool anchored = true, bool stirrups = true) => new(
        stirrups ? new ConcreteShearReinforcement(8, 200, section.Rebars.First().RebarMaterial) : null,
        new ConcreteShearDirection(500, 250, 402, anchored, stirrups ? 2 : 0, longitudinalCover: 42),
        new ConcreteShearDirection(300, 450, 402, anchored, stirrups ? 2 : 0, longitudinalCover: 42), "fixture drawings", 20);

    public static IEnumerable<object[]> NonAmericanStandards() => new[] { "NTC 2018", "Model Code 2010", "EN 1992-1-1", "UNI EN 1992-1-1",
        "DIN EN 1992-1-1", "DS EN 1992-1-1", "NS EN 1992-1-1", "CNR-DT 204/2006", "CS-TR34", "CNR-DT 200 R1/2013" }.Select(n => new object[] { n });
    private static StandardModelCode2010 Standard(string name) => name switch
    {
        "NTC 2018" => new StandardNTC2018Concrete(), "Model Code 2010" => new StandardModelCode2010(), "EN 1992-1-1" => new StandardEN1992p11(),
        "UNI EN 1992-1-1" => new StandardUNIEN1992p11(), "DIN EN 1992-1-1" => new StandardDINEN1992p11(), "DS EN 1992-1-1" => new StandardDSEN1992p11(),
        "NS EN 1992-1-1" => new StandardNSEN1992p11(), "CNR-DT 204/2006" => new StandardCNR204(), "CS-TR34" => new StandardCSTR34(),
        "CNR-DT 200 R1/2013" => new StandardCNR200(), _ => throw new ArgumentException(name)
    };

    // Torsion profile of the 300x500 fixture with bars at 50 mm: tef = max(Ac/u; 2·50) = 100, Ak = 200·400, uk = 1200; 2Ø16 for torsion.
    private static ConcreteTorsionData TorsionData(bool confirmed = true, double longitudinal = 402) => new(80000, 1200, 100, longitudinal, confirmed, "fixture drawings");

    // Crack data of the fixture: XC3, ordinary bars, c = 42 mm to the bar surface (bars Ø16 at 50 mm from the edges), ribbed bars. One bottom bar
    // is moved by 40 mm in the fixture, so the tensile bars are not in a row: the maximum spacing (200 mm) comes from the drawings.
    private static ConcreteCrackData CrackData(string? exposure = "XC3", bool sensitive = false, double? spacing = 200)
        => new(exposure, sensitive, 42, "fixture drawings", maximumBarSpacing: spacing);

    /// <summary>
    /// Every non-American standard answers every implemented check explicitly: evaluated, not applicable with the reason, or not supported with the reason.
    /// The member carries an 8 kNm torque, so the torsion task runs with the shear of both directions on cot θ = 1.5.
    /// </summary>
    [DataTestMethod, DynamicData(nameof(NonAmericanStandards), DynamicDataSourceType.Method)]
    public void EveryNonAmericanStandardAnswersEveryCheck(string name)
    {
        var m = Model(8e6); var section = m.BeamElements[250].Assignments.Sections[0].Section; section.ShearData = ShearData(section); section.TorsionData = TorsionData();
        section.CrackData = CrackData();
        var request = Request(Plan(new SectionCheckSpecification(CheckMechanism.UlsBiaxialSection, CombinationCategory.Ultimate), SectionCheckSpecification.ShearAxis1(),
            SectionCheckSpecification.ShearAxis2(), SectionCheckSpecification.Torsion(), SectionCheckSpecification.StressLimits(CombinationCategory.Characteristic),
            SectionCheckSpecification.StressLimits(CombinationCategory.QuasiPermanent), SectionCheckSpecification.CrackWidth(CombinationCategory.Characteristic),
            SectionCheckSpecification.CrackWidth(CombinationCategory.QuasiPermanent)));
        request.Jobs[0].Options = Options(Standard(name), 1.5);
        if (name == "Model Code 2010") request.Jobs[0].Options.CrackDesignLimit = .3; // MC2010 has no default wlim
        var results = new Service().Verify(m, request).Jobs[0].Results;
        Assert.AreEqual(64, results.Count);
        string Describe(CheckResult r) => r.Check!.Key + " " + r.Applicability + " " + r.Data + " " + r.Outcome + " " + string.Join(",", r.Diagnostics.Select(d => d.Code + ":" + d.Message));
        foreach (var r in results)
        {
            var check = r.Check!;
            bool notApplicable = name == "CS-TR34" && check.Mechanism != CheckMechanism.UlsBiaxialSection;
            bool notSupported = name == "CNR-DT 204/2006" && (check.Mechanism == CheckMechanism.Shear || check.Mechanism == CheckMechanism.Torsion
                || check.Criterion == SectionCheckCriterion.CrackWidth); // fibres with stirrups; FRC crack model
            bool notRequired = check.Criterion == SectionCheckCriterion.CrackWidth && check.Category == CombinationCategory.Characteristic;
            if (notApplicable)
            {
                Assert.AreEqual(CheckApplicability.NotApplicable, r.Applicability, Describe(r)); StringAssert.Contains(r.ApplicabilityReason, "CS-TR34");
            }
            else if (notSupported)
            {
                Assert.AreEqual(DataStatus.NotSupported, r.Data, Describe(r));
                Assert.IsTrue(r.Diagnostics.Any(d => d.Code == (check.Mechanism == CheckMechanism.Shear ? "ShearMethodNotImplemented"
                    : check.Mechanism == CheckMechanism.Torsion ? "TorsionMethodNotImplemented" : "CrackMethodNotImplemented")), Describe(r));
            }
            else if (notRequired)
            {
                Assert.AreEqual(CheckApplicability.NotApplicable, r.Applicability, Describe(r)); StringAssert.Contains(r.ApplicabilityReason, "not required");
            }
            else
            {
                Assert.AreEqual(ExecutionStatus.Completed, r.Execution, Describe(r)); Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Describe(r));
                Assert.AreEqual(Standard(name).Name, r.Standard!.Code, Describe(r));
                if (check.Mechanism == CheckMechanism.Torsion)
                {
                    var details = (TorsionCheckDetails)r.Details!;
                    Assert.AreEqual(3, details.Metrics.Count, Describe(r)); Assert.IsTrue(r.Utilization > .1, Describe(r));
                    Assert.IsTrue(details.MethodId.StartsWith(SectionTorsionCalculator.MethodId + "."), details.MethodId);
                }
                if (check.Criterion == SectionCheckCriterion.CrackWidth)
                {
                    var metric = r.Details!.Metrics.Single();
                    Assert.AreEqual("CrackWidth", metric.Key, Describe(r)); Assert.IsTrue(metric.Capacity >= .3, Describe(r));
                    Assert.IsTrue(r.Details.MethodId.StartsWith(SectionCrackCheck.MethodId + "."), r.Details.MethodId);
                }
            }
        }
        var torsion = results.Where(r => r.Check!.Mechanism == CheckMechanism.Torsion).ToArray();
        if (name == "CNR-DT 200 R1/2013") Assert.IsTrue(results.Where(r => r.Check!.Mechanism == CheckMechanism.Shear || r.Check!.Mechanism == CheckMechanism.Torsion)
            .All(r => r.Diagnostics.Any(d => d.Code == "NoFrpStrengthening")));
        if (name == "DS EN 1992-1-1") Assert.IsTrue(torsion.All(r => r.Diagnostics.Any(d => d.Code == "TorsionRuleNotApplied" && d.Message!.Contains("6.3.2(6)"))));
        if (name == "DIN EN 1992-1-1" || name == "Model Code 2010")
            Assert.IsTrue(torsion.All(r => r.Details!.Trace.Any(t => t.Key == "interaction" && t.Value == 2)), "quadratic strut interaction of solid sections");
    }

    /// <summary>The ULS domain uses the partial factors of each standard: DS (γc 1.45, γs 1.20) differs from EN; the result equals a direct native call.</summary>
    [TestMethod]
    public void UltimateDomainUsesThePartialFactorsOfEachStandard()
    {
        var m = Model(); var sample = Verification.BeamSample(m.BeamElements[250], "synthetic-member", "LC1", 0, SectionSide.Unspecified);
        var prepared = Verification.PrepareBeam(m, 250, sample, "uls");
        var uls = new SectionCheckSpecification(CheckMechanism.UlsBiaxialSection, CombinationCategory.Ultimate);
        var ratios = NonAmericanStandards().Select(o => (string)o[0]).ToDictionary(n => n, n => Verification.Run(prepared, uls, Options(Standard(n)).CreateVerifier()).Utilization!.Value);
        foreach (var name in ratios.Keys)
        {
            var section = prepared.Input!.Section; var f = prepared.Input.Forces;
            var reference = new CoordinateSystem(section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            var forces = new ResultBeamForces(f.N, f.V1, f.V2, f.T, f.M1, f.M2, reference);
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(reference, SectionSolver.FailureAnalysisTypes.ConstantEccentricity,
                SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 2, 0, false, 64);
            var native = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options, Standard(name), false);
            var point = new GPC.Checkers.Concrete.Analysis.LegacySectionCalculation(native).SolveResistance(new(forces));
            Assert.IsTrue(point.Diagnostics.ResistanceConvergence.Accepted, name);
            Assert.AreEqual(point.Utilization.Value, ratios[name], 1e-12, name);
        }
        Assert.AreNotEqual(ratios["EN 1992-1-1"], ratios["DS EN 1992-1-1"]);
        Assert.IsTrue(ratios["NTC 2018"] > ratios["Model Code 2010"], "αcc = 0.85 (NTC) reduces the capacity with respect to αcc = 1 (MC2010)");
    }

    /// <summary>CNR-DT 204 without shear reinforcement: plain concrete gives the EC2 value, FRC adds the residual strength.</summary>
    [TestMethod]
    public void FibreReinforcedShearUsesTheResidualStrengthOfTheMaterial()
    {
        var m = Model(); var section = m.BeamElements[250].Assignments.Sections[0].Section; section.ShearData = ShearData(section, stirrups: false);
        var sample = Verification.BeamSample(m.BeamElements[250], "synthetic-member", "LC1", .5, SectionSide.Unspecified);
        double Capacity(StandardModelCode2010 standard) => Verification.Run(Verification.PrepareBeam(m, 250, sample, "frc"), SectionCheckSpecification.ShearAxis2(),
            Options(standard).CreateVerifier()).Details!.Metrics.Single().Capacity!.Value;
        double plain = Capacity(new StandardCNR204()), ec2 = Capacity(new StandardEN1992p11());
        Assert.AreEqual(ec2, plain, 1e-9 * ec2);
        // Generate a new analytical fixture for the changed material, recording its inputs before its results.
        m = ElementScopeCharacterizationTest.Model(configureBeforeResults: value => {
            var frcSection = value.BeamElements[250].Assignments.Sections[0].Section;
            frcSection.ConcreteMaterial = new ConcreteMaterialModelCode2010("FRC C25/30", 25, ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear,
                2.0, 1.5, 0, 0.02, ConcreteMaterial.TensionStressStrainDiagrams.Bilinear, ConcreteMaterial.ConcreteTypes.FRC, 0.2, 0.0025, 10e-6, ConcreteMaterial.CementTypes.ClassN);
            frcSection.ShearData = ShearData(frcSection, stirrups: false);
        });
        sample = Verification.BeamSample(m.BeamElements[250], "synthetic-member", "LC1", .5, SectionSide.Unspecified);
        double frc = Capacity(new StandardCNR204());
        Assert.IsTrue(frc > 1.5 * plain, frc + " vs " + plain);
        var warned = Verification.Run(Verification.PrepareBeam(m, 250, sample, "frc"), SectionCheckSpecification.ShearAxis2(), Options(new StandardEN1992p11()).CreateVerifier());
        Assert.IsTrue(warned.Diagnostics.Any(d => d.Code == "FibreContributionNotUsed"));
    }

    [TestMethod]
    public void ShearRunsInBothDirectionsFromTheSectionData_AndMatchesTheCore()
    {
        var m = Model(); var section = m.BeamElements[250].Assignments.Sections[0].Section; section.ShearData = ShearData(section);
        var report = new Service().Verify(m, Request(Plan(SectionCheckSpecification.ShearAxis1(), SectionCheckSpecification.ShearAxis2())));
        var results = report.Jobs[0].Results;
        Assert.AreEqual(16, results.Count);
        Assert.IsTrue(results.All(r => r.Execution == ExecutionStatus.Completed && r.Outcome == EngineeringOutcome.Satisfied),
            string.Join(";", results.SelectMany(r => r.Diagnostics).Select(d => d.Code + " " + d.Message)));
        Assert.IsTrue(results.All(r => ((ShearCheckDetails)r.Details!).Direction == (r.Check!.Direction == SectionCheckDirection.Axis1 ? 1 : 2)));
        Assert.AreEqual(2, report.Jobs[0].GoverningByConfiguration.Count);

        // Same task through the core: NTC 2018, fcd = 0.85·25/1.5, fyd = 450/1.15, Asw = 2·π·8²/4, s = 200 mm.
        var sample = Verification.BeamSample(m.BeamElements[250], "synthetic-member", "LC1", .5, SectionSide.Unspecified);
        var prepared = Verification.PrepareBeam(m, 250, sample, "direct");
        var standard = new StandardNTC2018Concrete(); var f = prepared.Input!.Forces;
        var core = SectionShearCalculator.Calculate(new SectionShearInput(standard, f.N, f.V2, 0, 150000, 300, 450, 0, 25, .85 * 25 / 1.5, 450 / 1.15, 1.5,
            200000, 2 * Math.PI * 16, 200));
        var through = Verification.Run(prepared, SectionCheckSpecification.ShearAxis2(), Options().CreateVerifier());
        var metric = through.Details!.Metrics.Single();
        Assert.AreEqual(core.VRd, metric.Capacity!.Value, 1e-6 * core.VRd); Assert.AreEqual(Math.Abs(f.V2), metric.Demand!.Value, 1e-9);
        Assert.AreEqual(core.CotTheta, ((ShearCheckDetails)through.Details).CotTheta, 1e-12);
    }

    [TestMethod]
    public void ShearDataAreRequiredConfirmedAndInvalidateThePlanWhenChanged()
    {
        var m = Model(); var section = m.BeamElements[250].Assignments.Sections[0].Section;
        // Without shear reinforcement Asl enters the method: an unconfirmed anchorage is insufficient data, not an assumption.
        section.ShearData = new ConcreteShearData(null, new ConcreteShearDirection(500, 250, 402, false, 0), null, "fixture", 20);
        var unconfirmed = Verification.Run(Verification.PrepareBeam(m, 250, Verification.BeamSample(m.BeamElements[250], "synthetic-member", "LC1", .5, SectionSide.Unspecified), "x"),
            SectionCheckSpecification.ShearAxis1(), Options().CreateVerifier());
        Assert.AreEqual(DataStatus.Insufficient, unconfirmed.Data); Assert.IsTrue(unconfirmed.Diagnostics.Any(d => d.Code == "TensionReinforcementAnchorageNotConfirmed"));
        var missingAxis = Verification.Run(Verification.PrepareBeam(m, 250, Verification.BeamSample(m.BeamElements[250], "synthetic-member", "LC1", .5, SectionSide.Unspecified), "x"),
            SectionCheckSpecification.ShearAxis2(), Options().CreateVerifier());
        Assert.IsTrue(missingAxis.Diagnostics.Any(d => d.Code == "MissingShearData"));

        section.ShearData = ShearData(section);
        var plan = BeamCheckPlan.Prepare(m, Plan(SectionCheckSpecification.ShearAxis1()));
        Assert.IsTrue(plan.IsCurrent);
        section.ShearData = new ConcreteShearData(new ConcreteShearReinforcement(10, 200, section.Rebars.First().RebarMaterial),
            section.ShearData.Axis1, section.ShearData.Axis2, "revised drawings", 20);
        Assert.IsFalse(plan.IsCurrent);
    }

    /// <summary>A user-derived class of an implemented standard does not inherit its profile.</summary>
    private sealed class CustomAnnex : StandardEN1992p11 { }

    [TestMethod]
    public void ShearIsNotSupportedForStandardsWithoutAnImplementedProfile()
    {
        var verifier = Options(new CustomAnnex()).CreateVerifier();
        Assert.IsFalse(verifier.Supports(SectionCheckSpecification.ShearAxis1()));
        Assert.IsFalse(Options().CreateVerifier().Supports(new SectionCheckSpecification(CheckMechanism.Shear, CombinationCategory.UltimateSeismic, SectionCheckDirection.Axis1)));
        Assert.IsTrue(Options(new StandardDINEN1992p11()).CreateVerifier().Supports(SectionCheckSpecification.ShearAxis2()));
    }

    [TestMethod]
    public void SectionShearDataSurviveTheArchiveAndSectionsWithoutThemKeepTheirRevision()
    {
        var m = Model(); var section = m.BeamElements[250].Assignments.Sections[0].Section;
        string before = m.AnalysisFingerprint(), verification = m.VerificationFingerprint("x");
        section.ShearData = null;
        Assert.AreEqual(before, m.AnalysisFingerprint()); Assert.AreEqual(verification, m.VerificationFingerprint("x"));
        section.ShearData = ShearData(section);
        Assert.AreNotEqual(verification, m.VerificationFingerprint("x"));
        Assert.AreEqual(before, m.AnalysisFingerprint(), "stirrups do not change the FEM analysis");
        using var stream = new MemoryStream(); ModelArchive.Save(m, stream); stream.Position = 0; var restored = ModelArchive.Load(stream);
        var copy = restored.BeamElements[250].Assignments.Sections[0].Section;
        Assert.AreEqual(section.ShearData, copy.ShearData); Assert.AreEqual(8, copy.ShearData!.Reinforcement.Diameter);
        Assert.AreEqual(m.VerificationFingerprint("x"), restored.VerificationFingerprint("x"));
    }

    /// <summary>NTC 2018 torsion through the verifier equals the core called with the same section data, forces and cot θ.</summary>
    [TestMethod]
    public void TorsionRunsOnTheSectionDataAndMatchesTheCore()
    {
        var m = Model(8e6); var section = m.BeamElements[250].Assignments.Sections[0].Section;
        section.ShearData = ShearData(section); section.TorsionData = TorsionData();
        var sample = Verification.BeamSample(m.BeamElements[250], "synthetic-member", "LC1", .5, SectionSide.Unspecified);
        var prepared = Verification.PrepareBeam(m, 250, sample, "torsion");
        var through = Verification.Run(prepared, SectionCheckSpecification.Torsion(), Options(cotTheta: 1.5).CreateVerifier());
        Assert.AreEqual("Torsion/None/Default/Ultimate", through.Check!.Key);
        Assert.AreEqual(ExecutionStatus.Completed, through.Execution, string.Join(";", through.Diagnostics.Select(d => d.Code + " " + d.Message)));
        var f = prepared.Input!.Forces;
        Assert.AreEqual(8e6, Math.Abs(f.T), 1e-6);

        // Core: fck 25, fcd = 0.85·25/1.5, fyd = 450/1.15, Ø8/200 closed links (2 legs per direction), shear data of the fixture.
        var standard = new StandardNTC2018Concrete(); double fcd = .85 * 25 / 1.5, fyd = 450 / 1.15, asw = 2 * Math.PI * 16;
        var core = SectionTorsionCalculator.Calculate(new SectionTorsionInput(standard, f.T, new TorsionGeometry(80000, 1200, 100), 25, fcd, 1.5, fyd, fyd, Math.PI * 16, 200, 402, 1.5),
            new SectionShearInput(standard, f.N, f.V1, 0, 150000, 500, 250, 0, 25, fcd, fyd, 1.5, 200000, asw, 200),
            new SectionShearInput(standard, f.N, f.V2, 0, 150000, 300, 450, 0, 25, fcd, fyd, 1.5, 200000, asw, 200));
        Assert.AreEqual(core.Ratio!.Value, through.Utilization!.Value, 1e-9);
        var details = (TorsionCheckDetails)through.Details!;
        Assert.AreEqual(core.RequiredLongitudinalArea, details.RequiredLongitudinalArea, 1e-9);
        Assert.AreEqual(core.TRd, details.Metrics.Single(x => x.Key == "T").Capacity!.Value, 1e-6);
        // Hand check of the governing resistance: TRld = 2 Ak (ΣAsl/uk) fyd / cot θ = 2·80000·(402/1200)·391.3/1.5.
        Assert.AreEqual(2 * 80000 * 402.0 / 1200 * fyd / 1.5, core.TRld, 1e-6); Assert.AreEqual(core.TRld, core.TRd);
        Assert.AreEqual(1.5, core.Axis2Shear!.CotTheta, 1e-12);
    }

    [TestMethod]
    public void TorsionDataAreRequiredConfirmedAndInvalidateThePlan()
    {
        TorsionCheckResult(Model(), _ => { }, null, out var none);
        Assert.AreEqual(EngineeringOutcome.Satisfied, none.Outcome); Assert.AreEqual(0, none.Utilization);
        Assert.IsTrue(none.Diagnostics.Any(d => d.Code == "NoTorsionDemand"), "no torque: no data required");

        string Code(Action<ReinforcedConcreteSection> edit, double? cot = 1.5) { TorsionCheckResult(Model(8e6), edit, cot, out var r); return string.Join(",", r.Diagnostics.Select(d => d.Code)) + "|" + r.Data + "|" + r.Outcome; }
        StringAssert.Contains(Code(s => s.ShearData = ShearData(s)), "MissingTorsionData|Insufficient");
        StringAssert.Contains(Code(s => s.TorsionData = TorsionData()), "MissingShearData|Insufficient");
        StringAssert.Contains(Code(s => { s.ShearData = ShearData(s); s.TorsionData = TorsionData(confirmed: false); }), "ClosedLinksNotConfirmed|Insufficient");
        StringAssert.Contains(Code(s => { s.ShearData = ShearData(s); s.TorsionData = TorsionData(longitudinal: 2000); }), "TorsionDataExceedsReinforcement|Insufficient");
        StringAssert.Contains(Code(s => { s.ShearData = ShearData(s); s.TorsionData = TorsionData(); }, null), "CotThetaRequired|Insufficient");
        StringAssert.Contains(Code(s => { s.ShearData = ShearData(s); s.TorsionData = TorsionData(); }, 2.6), "TorsionOutsideMethodRange|Insufficient");
        // A member explicitly without links has no torsional resistance: a failure, not missing data.
        var noLinks = Code(s => { s.ShearData = ShearData(s, stirrups: false); s.TorsionData = TorsionData(); });
        StringAssert.Contains(noLinks, "NoClosedLinks"); StringAssert.EndsWith(noLinks, "|Ready|NotSatisfied");

        var m = Model(8e6); var section = m.BeamElements[250].Assignments.Sections[0].Section; section.ShearData = ShearData(section); section.TorsionData = TorsionData();
        string analysis = m.AnalysisFingerprint();
        var plan = BeamCheckPlan.Prepare(m, Plan(SectionCheckSpecification.Torsion()));
        Assert.IsTrue(plan.IsCurrent); Assert.AreEqual(8, plan.WorkItems.Count);
        section.TorsionData = TorsionData(longitudinal: 603);
        Assert.IsFalse(plan.IsCurrent, "the torsion data enter the verification revision");
        Assert.AreEqual(analysis, m.AnalysisFingerprint(), "torsion data do not change the FEM analysis");
        using var stream = new MemoryStream(); ModelArchive.Save(m, stream); stream.Position = 0;
        var copy = ModelArchive.Load(stream).BeamElements[250].Assignments.Sections[0].Section;
        Assert.AreEqual(section.TorsionData, copy.TorsionData); Assert.AreEqual(603, copy.TorsionData!.LongitudinalArea);
    }

    private static void TorsionCheckResult(GPC.Model.Models.Model m, Action<ReinforcedConcreteSection> edit, double? cot, out CheckResult result)
    {
        edit(m.BeamElements[250].Assignments.Sections[0].Section);
        var sample = Verification.BeamSample(m.BeamElements[250], "synthetic-member", "LC1", .5, SectionSide.Unspecified);
        result = Verification.Run(Verification.PrepareBeam(m, 250, sample, "torsion"), SectionCheckSpecification.Torsion(), Options(cotTheta: cot).CreateVerifier());
    }

    [TestMethod]
    public void TorsionIsNotSupportedForStandardsWithoutAnImplementedProfile()
    {
        Assert.IsFalse(Options(new CustomAnnex()).CreateVerifier().Supports(SectionCheckSpecification.Torsion()));
        Assert.IsFalse(Options().CreateVerifier().Supports(new SectionCheckSpecification(CheckMechanism.Torsion, CombinationCategory.UltimateSeismic)));
        Assert.IsTrue(Options(new StandardCNR204()).CreateVerifier().Supports(SectionCheckSpecification.Torsion()), "known standard: a result with the reason");
        StringAssert.Contains(Options().CreateVerifier().Configuration, "TorsionProfile=Ntc2018");
    }

    /// <summary>NTC 2018 crack width through the verifier equals the core on the linear cracked analysis of the same section and forces.</summary>
    [TestMethod]
    public void CrackWidthRunsOnTheRealEngineAndMatchesTheCore()
    {
        var m = Model(); var section = m.BeamElements[250].Assignments.Sections[0].Section; section.CrackData = CrackData();
        var sample = Verification.BeamSample(m.BeamElements[250], "synthetic-member", "LC1", 0, SectionSide.Unspecified);
        var prepared = Verification.PrepareBeam(m, 250, sample, "crack");
        var through = Verification.Run(prepared, SectionCheckSpecification.CrackWidth(CombinationCategory.QuasiPermanent), Options().CreateVerifier());
        Assert.AreEqual("Serviceability/None/CrackWidth/QuasiPermanent", through.Check!.Key);
        Assert.AreEqual(ExecutionStatus.Completed, through.Execution, string.Join(";", through.Diagnostics.Select(d => d.Code + " " + d.Message)));
        var metric = through.Details!.Metrics.Single();
        Assert.AreEqual(.3, metric.Capacity!.Value, 1e-12, "NTC Tab. 4.1.IV: ordinary environment, little sensitive bars, quasi-permanent: w2 = 0.3 mm");
        Assert.IsTrue(metric.Demand > 0, "the bending moment at the fixed end cracks the section");

        // Core: linear analysis without tensile concrete, φ = 2 as the options.
        var f = prepared.Input!.Forces; var standard = new StandardNTC2018Concrete();
        var reference = new CoordinateSystem(section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
        var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(reference, SectionSolver.FailureAnalysisTypes.ConstantEccentricity,
            SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.Linear, 2, 0, false, 64);
        var stress = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options, standard, false)
            .GetTensionAnalysisResult(new ResultBeamForces(f.N, f.V1, f.V2, f.T, f.M1, f.M2, reference));
        var concrete = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
        var core = SectionCrackCheck.Evaluate(new SectionCrackInput(standard, ServiceabilityCombination.QuasiPermanent, "XC3", false, null, CrackSectionGeometry.From(section),
            stress.StrainPlane, SectionCrackInput.OrdinaryBarStresses(stress, section), true, false, false, section.Rebars.First().RebarMaterial.E, concrete.Ecm, concrete.Fctm,
            false, true, 42, spacingOverride: 200));
        Assert.AreEqual(core.Width!.Value, metric.Demand!.Value, 1e-12); Assert.AreEqual(core.Ratio!.Value, through.Utilization!.Value, 1e-12);
        Assert.AreEqual("TensileZone", core.GoverningRegion);
        // The characteristic combination does not require crack control in NTC 2018: not applicable, with the reason.
        var characteristic = Verification.Run(prepared, SectionCheckSpecification.CrackWidth(CombinationCategory.Characteristic), Options().CreateVerifier());
        Assert.AreEqual(CheckApplicability.NotApplicable, characteristic.Applicability); StringAssert.Contains(characteristic.ApplicabilityReason, "not required");
    }

    [TestMethod]
    public void CrackDataAreRequiredAndDecompressionIsChecked()
    {
        CheckResult Run(Action<ReinforcedConcreteSection> edit, StandardModelCode2010? standard = null, double? designLimit = null)
        {
            var m = Model(); edit(m.BeamElements[250].Assignments.Sections[0].Section);
            var sample = Verification.BeamSample(m.BeamElements[250], "synthetic-member", "LC1", 0, SectionSide.Unspecified);
            var options = Options(standard); options.CrackDesignLimit = designLimit;
            return Verification.Run(Verification.PrepareBeam(m, 250, sample, "crack"), SectionCheckSpecification.CrackWidth(CombinationCategory.QuasiPermanent), options.CreateVerifier());
        }
        string Codes(CheckResult r) => string.Join(",", r.Diagnostics.Select(d => d.Code)) + "|" + r.Data;
        StringAssert.Contains(Codes(Run(_ => { })), "MissingCrackData|Insufficient");
        StringAssert.Contains(Codes(Run(s => s.CrackData = CrackData(null))), "CrackMissingExposure|Insufficient");
        StringAssert.Contains(Codes(Run(s => s.CrackData = CrackData(spacing: null))), "CrackSpacingUndetermined|Insufficient");
        StringAssert.Contains(Codes(Run(s => s.CrackData = CrackData(), new StandardModelCode2010())), "CrackMissingDesignLimit|Insufficient");
        var mc = Run(s => s.CrackData = CrackData(), new StandardModelCode2010(), .2);
        Assert.AreEqual(.2, mc.Details!.Metrics.Single().Capacity!.Value, 1e-12, "design wlim of Model Code 2010");
        // NTC, sensitive reinforcement in an aggressive environment, quasi-permanent: decompression of the uncracked section. Bending puts the
        // bottom fibre in tension: not satisfied, with the stress and the zero limit, no ratio.
        var decompression = Run(s => s.CrackData = CrackData("XD1", true));
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, decompression.Outcome, Codes(decompression));
        var tension = decompression.Details!.Metrics.Single();
        Assert.AreEqual("UncrackedConcreteTension", tension.Key); Assert.AreEqual(0, tension.Capacity); Assert.IsTrue(tension.Demand > 0); Assert.IsNull(decompression.Utilization);
        Assert.AreEqual("decompression", decompression.Details.UtilizationDefinition);

        var model = Model(); var section = model.BeamElements[250].Assignments.Sections[0].Section; section.CrackData = CrackData();
        string analysis = model.AnalysisFingerprint();
        var plan = BeamCheckPlan.Prepare(model, Plan(SectionCheckSpecification.CrackWidth(CombinationCategory.QuasiPermanent)));
        Assert.IsTrue(plan.IsCurrent); Assert.AreEqual(8, plan.WorkItems.Count);
        section.CrackData = CrackData("XD1");
        Assert.IsFalse(plan.IsCurrent, "the crack data enter the verification revision");
        Assert.AreEqual(analysis, model.AnalysisFingerprint());
        using var stream = new MemoryStream(); ModelArchive.Save(model, stream); stream.Position = 0;
        Assert.AreEqual(section.CrackData, ModelArchive.Load(stream).BeamElements[250].Assignments.Sections[0].Section.CrackData);
        var configuration = Options().CreateVerifier().Configuration;
        StringAssert.Contains(configuration, "CrackProfile=Ntc2018"); StringAssert.Contains(configuration, "CrackLoadDuration=LongTerm");
    }

    [TestMethod]
    public void SpecificationSurvivesArchiveRoundTripWithSealedEvidence()
    {
        var m = Model(); var request = Request(Plan(SectionCheckSpecification.StressLimits(CombinationCategory.QuasiPermanent)));
        var report = new Service().Verify(m, request);
        m.CheckReports.AddRange(report.Jobs);
        using var stream = new MemoryStream(); ModelArchive.Save(m, stream); stream.Position = 0; var restored = ModelArchive.Load(stream);
        var saved = restored.CheckReports.Single();
        Assert.IsTrue(saved.Results.All(r => r.HasUnchangedEvidence && r.Check!.Key == "Serviceability/None/StressLimits/QuasiPermanent"));
        Assert.AreEqual(CombinationCategory.QuasiPermanent, saved.BeamScope.SectionChecks!.Single().Category);
        Assert.AreEqual(report.Jobs[0].ScopeFingerprint, BeamCheckPlan.FingerprintScope(restored, saved.BeamScope));
    }
}
