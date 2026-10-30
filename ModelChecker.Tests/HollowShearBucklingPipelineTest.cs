using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Compatibility;
using GPC.Model.Core;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Automatic shear-buckling screen of rectangular hollow sections in the section engine and in the
/// beam-column member route: the walls parallel to each shear are unstiffened webs, EC3 6.2.6(6) / EN 1993-1-5
/// 5.1(2) with the code object's eta (1.2 for S355), NTC eq. 4.2.27 with eta = 1. S355: eps = 0.81362, limits
/// 72 eps/1.2 = 48.82 and 72 eps = 58.58. hw = clear distance between the inner faces of the walls normal to the shear.
/// Before this screen every row below with shear and no explicit assessment was NotEvaluated.
/// CHS (30/10/2026): GPC wall screen d/t &lt;= 2 (0.0693 E/fy)^(2/3) from EN 1993-1-6 D.1.4, S355 23.774, S235 31.300;
/// beyond it the explicit assessment stays required.</summary>
[TestClass]
public class HollowShearBucklingPipelineTest
{
    private static readonly double Eps = Math.Sqrt(235.0 / 355);

    private static BeamActionInput Input(Section shape, SteelMaterial? material = null)
    {
        var section = new SteelSection(shape, material ?? SteelMaterialEN1993Data.S355);
        var model = GPC.Examples.MixedModelFactory.Create(m => {
            var beam = m.BeamElements[250]; beam.BeamProperty = section;
            beam.Assignments.Sections.Clear();
            beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Property = section });
        });
        var sample = Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified);
        var prepared = BeamActionPreparation.Prepare(model, 250, sample, "Steel pipeline");
        Assert.AreEqual(DataStatus.Ready, prepared.Status);
        var input = prepared.Input;
        input.Forces.N = input.Forces.V1 = input.Forces.V2 = input.Forces.T = input.Forces.M1 = input.Forces.M2 = 0;
        return input;
    }
    private static Dictionary<string, string> Options() => new() { ["Unperforated"] = "true" };
    private static CheckResult Run(BeamActionInput input, bool ntc, Dictionary<string, string>? options = null)
        => new SteelPipelineMaterialChecker(ntc ? new StandardNTC2018Steel() : new StandardEN1993p11(), ntc ? "2018" : "2005",
            parameters: options ?? Options()).CreateSession().Verify(input, CheckMechanism.UlsBiaxialSection, default);
    private static double Value(CheckResult r, string key) => r.Details.Trace.Single(t => t.Key == key).Value!.Value;
    private static string Diagnostics(CheckResult r) => string.Join(";", r.Diagnostics.Select(d => d.Code + " " + d.Message));

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void SectionEngineScreensTheRhsWallsOnBothAxes(bool ntc)
    {
        // RHS 300x200x10 rolled: V2 walls (300 - 20)/10 = 28, V1 walls (200 - 20)/10 = 18, both excluded.
        // M1 = 10 kNm, V1 = 50 kN, V2 = 100 kN: linear N-M-V bound and the shear sum V1/Vpl1 + V2/Vpl2 (EC3 6.2.6(3)(f):
        // A = 9600, Av1 = A b/(b+h) = 3840, Av2 = A h/(b+h) = 5760), Vpl = Av fy/(sqrt3 gM0), gM0 = 1 or 1.05.
        var input = Input(new SectionRHS(300, 200, 10, 10, 10, 10, "RHS300x200x10"));
        input.Forces.M1 = 1e7; input.Forces.V1 = 5e4; input.Forces.V2 = 1e5;
        var r = Run(input, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        double g = ntc ? 1.05 : 1, eta = ntc ? 1 : 1.2, v = 5e4 / (3840 * 355 / (Math.Sqrt(3) * g)) + 1e5 / (5760 * 355 / (Math.Sqrt(3) * g));
        Assert.AreEqual(v, r.Details.Metrics.Single(m => m.Key == "Shear2").Utilization!.Value, v * 1e-12);
        foreach (string prefix in new[] { "NormalResistance", "Shear1", "Shear2" })
        {
            Assert.AreEqual(28, Value(r, prefix + ".ShearAssessment2.WebSlenderness"), 1e-12);
            Assert.AreEqual(18, Value(r, prefix + ".ShearAssessment1.WebSlenderness"), 1e-12);
            Assert.AreEqual(72 * Eps / eta, Value(r, prefix + ".ShearAssessment2.WebSlendernessLimit"), 1e-12);
            Assert.AreEqual(eta, Value(r, prefix + ".ShearAssessment1.Eta"));
        }
        var excluded = r.Details.Trace.First(t => t.Key == "Shear2.ShearAssessment2.Excluded");
        Assert.AreEqual(1, excluded.Value);
        StringAssert.Contains(excluded.Expression, ntc ? "NTC 2018 equation4.2.27 conservative eta=1" : "StandardEN1993p11.NShearBucklingLowGradeOfSteel");
        StringAssert.Contains(r.Details.Trace.First(t => t.Key == "Shear2.ShearAssessment2.WebSlenderness").Expression, "parallel to V2");
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void NegativeWallScreenLeavesTheRowNotEvaluatedOnEachAxis(bool ntc)
    {
        // RHS 300x200x4: V1 walls 192/4 = 48 <= 48.82 / 58.58 (Satisfied); V2 walls 292/4 = 73 > both limits (NotEvaluated).
        var minor = Input(new SectionRHS(300, 200, 4, 4, 4, 4, "RHS300x200x4")); minor.Forces.V1 = 1e4;
        var passed = Run(minor, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, passed.Outcome, Diagnostics(passed));
        // A = 300*200 - 292*192 = 3936, Av1 = 3936*200/500 = 1574.4.
        Assert.AreEqual(1e4 / (1574.4 * 355 / (Math.Sqrt(3) * (ntc ? 1.05 : 1))), passed.Utilization!.Value, 1e-12);
        var major = Input(new SectionRHS(300, 200, 4, 4, 4, 4, "RHS300x200x4")); major.Forces.V2 = 1e4;
        var negative = Run(major, ntc);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, negative.Outcome); Assert.IsNull(negative.Utilization);
        Assert.IsTrue(negative.Diagnostics.Any(d => d.Code == "SteelPipeline.Shear2.ShearBuckling.ResistanceMethodRequired"), Diagnostics(negative));
        Assert.AreEqual(0, Value(negative, "Shear2.ShearAssessment2.Excluded"));
        Assert.AreEqual(73, Value(negative, "Shear2.ShearAssessment2.WebSlenderness"), 1e-12);
        // Square SHS 300x300x4: the walls of width b fail too under V1 (73).
        var square = Input(new SectionRHS(300, 300, 4, 4, 4, 4, "SHS300x4")); square.Forces.V1 = 1e4;
        var squareResult = Run(square, ntc);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, squareResult.Outcome);
        Assert.AreEqual(0, Value(squareResult, "Shear1.ShearAssessment1.Excluded"));
        Assert.AreEqual(73, Value(squareResult, "Shear1.ShearAssessment1.WebSlenderness"), 1e-12);
    }

    [TestMethod]
    public void ExplicitShearAssessmentTakesPrecedenceOverTheWallScreen()
    {
        // Negative screen (73), explicit exclusion: Satisfied with the caller's source only.
        var thin = Input(new SectionRHS(300, 300, 4, 4, 4, 4, "SHS300x4")); thin.Forces.V2 = 1e4;
        var options = Options(); options["SectionFingerprint"] = ModelValues.Fingerprint(new object[] { thin.Property });
        options["ShearBucklingExcluded2"] = "true"; options["ShearBucklingSource2"] = "Independent wall assessment with stiffened supports, rev 1";
        var r = Run(thin, false, options);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.IsTrue(r.Details.Trace.Any(t => t.Key == "Shear2.ShearAssessment2.Excluded" && t.Expression == options["ShearBucklingSource2"]));
        Assert.IsFalse(r.Details.Trace.Any(t => t.Key.EndsWith(".WebSlenderness")));
        // Positive screen (28), explicit negative declaration: NotEvaluated.
        var stocky = Input(new SectionRHS(300, 200, 10, 10, 10, 10, "RHS300x200x10")); stocky.Forces.V2 = 1e4;
        options = Options(); options["SectionFingerprint"] = ModelValues.Fingerprint(new object[] { stocky.Property });
        options["ShearBucklingExcluded2"] = "false"; options["ShearBucklingSource2"] = "Independent assessment: not excluded";
        var declined = Run(stocky, true, options);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, declined.Outcome);
        Assert.IsTrue(declined.Diagnostics.Any(d => d.Code == "SteelPipeline.Shear2.ShearBuckling.ResistanceMethodRequired"));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void CircularAndRotatedHollowSectionsKeepTheExplicitAssessment(bool ntc)
    {
        // CHS beyond the GPC wall screen: 300x8, d/t = 37.5 > 2(0.0693 E/fy)^(2/3) = 23.774 (S355). Nothing is supplied, the row
        // waits for the explicit assessment as before the screen. (Was CHS 200x10, d/t = 20: now screened, see below.)
        var tube = Input(new SectionCHS(300, 8, "CHS300x8")); tube.Forces.V2 = 1e4;
        var waiting = Run(tube, ntc);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, waiting.Outcome); Assert.AreEqual(DataStatus.Insufficient, waiting.Data);
        Assert.IsTrue(waiting.Diagnostics.Any(d => d.Code == "SteelPipeline.Shear2.ShearBuckling.AssessmentRequired"), Diagnostics(waiting));
        Assert.IsFalse(waiting.Details.Trace.Any(t => t.Key.Contains(".ShearAssessment")));
        var options = Options(); options["SectionFingerprint"] = ModelValues.Fingerprint(new object[] { tube.Property });
        options["ShearBucklingExcluded2"] = "true"; options["ShearBucklingSource2"] = "Independent shell assessment EN 1993-1-6";
        Assert.AreEqual(EngineeringOutcome.Satisfied, Run(tube, ntc, options).Outcome);
        // Wide RHS (b > h): axis 1 is vertical, the shear mapping and the screen both require exact alignment.
        var wide = Input(new SectionRHS(200, 300, 10, 10, 10, 10, "RHS200x300x10")); wide.Forces.V2 = 1e4;
        var rotated = Run(wide, ntc);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, rotated.Outcome);
        Assert.IsFalse(rotated.Details.Trace.Any(t => t.Key.Contains(".ShearAssessment")));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void SectionEngineScreensTheChsWallOnBothAxes(bool ntc)
    {
        // CHS 200x10 S355: d/t = 20 <= 23.774 (GPC screen from EN 1993-1-6 D.1.4: long cylinder tau_cr = 0.25 E (2t/d)^1.5
        // = 1660.2 MPa, lambda_tau = sqrt((355/sqrt3)/1660.2) = 0.351 <= 0.40). Was NotEvaluated (AssessmentRequired).
        // V1 = 50 kN, V2 = 100 kN: shear sum on Av = 2A/pi = 3800 for both axes (A = 1900 pi), Vpl = Av fy/(sqrt3 gM0).
        var input = Input(new SectionCHS(200, 10, "CHS200x10"));
        input.Forces.M1 = 1e7; input.Forces.V1 = 5e4; input.Forces.V2 = 1e5;
        var r = Run(input, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        double g = ntc ? 1.05 : 1, v = 1.5e5 / (3800 * 355 / (Math.Sqrt(3) * g));
        Assert.AreEqual(v, r.Details.Metrics.Single(m => m.Key == "Shear2").Utilization!.Value, v * 1e-12);
        foreach (string prefix in new[] { "NormalResistance", "Shear1", "Shear2" })
            foreach (int axis in new[] { 1, 2 })
            {
                Assert.AreEqual(20, Value(r, prefix + ".ShearAssessment" + axis + ".DiameterToThickness"), 1e-12);
                Assert.AreEqual(23.774, Value(r, prefix + ".ShearAssessment" + axis + ".DiameterToThicknessLimit"), 5e-4);
                Assert.AreEqual(.35136, Value(r, prefix + ".ShearAssessment" + axis + ".ShearSlenderness"), 1e-5);
            }
        var excluded = r.Details.Trace.First(t => t.Key == "Shear2.ShearAssessment2.Excluded");
        Assert.AreEqual(1, excluded.Value);
        StringAssert.Contains(excluded.Expression, "GPC automatic CHS wall screen");
        // The explicit declaration still wins: a negative one leaves the stocky tube not evaluated.
        var options = Options(); options["SectionFingerprint"] = ModelValues.Fingerprint(new object[] { input.Property });
        options["ShearBucklingExcluded1"] = "false"; options["ShearBucklingSource1"] = "Independent shell assessment: not excluded";
        options["ShearBucklingExcluded2"] = "true"; options["ShearBucklingSource2"] = "Independent shell assessment";
        var declined = Run(input, ntc, options);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, declined.Outcome);
        Assert.IsTrue(declined.Diagnostics.Any(d => d.Code.EndsWith("ShearBuckling.ResistanceMethodRequired")), Diagnostics(declined));
        Assert.IsFalse(declined.Details.Trace.Any(t => t.Key.EndsWith(".DiameterToThickness")));
        // S235, CHS 300x10: d/t = 30 <= 31.300, screened; the same tube in S355 (limit 23.774) waits for the explicit assessment.
        var s235 = Input(new SectionCHS(300, 10, "CHS300x10"), SteelMaterialEN1993Data.S235); s235.Forces.V2 = 1e4;
        var screened235 = Run(s235, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, screened235.Outcome, Diagnostics(screened235));
        Assert.AreEqual(31.300, Value(screened235, "Shear2.ShearAssessment2.DiameterToThicknessLimit"), 5e-4);
        var s355 = Input(new SectionCHS(300, 10, "CHS300x10")); s355.Forces.V2 = 1e4;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Run(s355, ntc).Outcome);
    }

    private static CheckResult BeamColumn(Func<Section> shape, bool ntc, double v1, double v2, string? source = null)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(sectionFactory: () => new SteelSection(shape(), SteelMaterialEN1993Data.S355));
        foreach (var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]))
        { sample.ResultBeamForces.M1 = 1e6; sample.ResultBeamForces.M2 = -5e5; sample.ResultBeamForces.V1 = v1; sample.ResultBeamForces.V2 = v2; }
        var p = SteelMemberPipelineTest.Options(model, plan);
        foreach (var key in new[] { "InPlaneCurve", "LateralCurve", "LateralTorsionalCurve" }) p.Remove(key);
        p["AnalysisMode"] = "BeamColumn"; p["CurveSelection"] = "Automatic";
        p["TorsionSusceptible"] = "false"; p["Cm1"] = "1"; p["Cm2"] = "1"; p["CmLT"] = "1";
        p["MomentFactorSource"] = "Conservative whole-span factors"; p["StabilityModelSource"] = "Global second order accounted for";
        p["TorsionalExclusionSource"] = "Independent torsional screen"; p["FlexuralTorsionalExclusionSource"] = "Independent FT screen";
        p["LateralTorsionalExclusionSource"] = "Hollow section, independent twist-restraint assessment"; p["McrBasis"] = "UniformMoment";
        foreach (var q in new[] { "Ncr1", "Ncr2" }) { p[q + ".Value"] = "2000000"; p[q + ".Unit"] = "N"; p[q + ".Source"] = "Same-member elastic critical analysis"; }
        if (source != null) p["ShearBucklingExclusionSource"] = source;
        Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count);
        return new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, ntc ? new StandardNTC2018Steel() : new StandardEN1993p11())).Jobs[0].Results.Single();
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnScreensTheRhsWallsLikeTheSectionEngineAndAnExplicitSourceWins(bool ntc)
    {
        // Was NotEvaluated (BeamColumn.ShearBucklingExclusionSource). RHS 200x150x10 rounded (ro 15, ri 10), V1 = 2 kN,
        // V2 = 5 kN (<= .5 Vpl): V2 walls (200 - 20)/10 = 18, V1 walls (150 - 20)/10 = 13, both excluded. The shear does not
        // enter the interaction, so the utilization equals the one with an explicit source.
        static Section Rounded() => new SectionRHSRoundedCorners(200, 150, 10, 15, 10, "RHS200x150x10");
        var r = BeamColumn(Rounded, ntc, 2e3, 5e3);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(1, Value(r, "MemberStability.ShearAssessment2.Excluded"));
        Assert.AreEqual(18, Value(r, "MemberStability.ShearAssessment2.WebSlenderness"), 1e-12);
        Assert.AreEqual(13, Value(r, "MemberStability.ShearAssessment1.WebSlenderness"), 1e-12);
        Assert.AreEqual(72 * Eps / (ntc ? 1 : 1.2), Value(r, "MemberStability.ShearAssessment1.WebSlendernessLimit"), 1e-12);
        var explicitSource = BeamColumn(Rounded, ntc, 2e3, 5e3, "Independent wall assessment, rev 1");
        Assert.AreEqual(EngineeringOutcome.Satisfied, explicitSource.Outcome);
        Assert.AreEqual(r.Utilization, explicitSource.Utilization);
        Assert.IsFalse(explicitSource.Details.Trace.Any(t => t.Key.StartsWith("MemberStability.ShearAssessment")));
        var unsheared = BeamColumn(Rounded, ntc, 0, 0);
        Assert.AreEqual(unsheared.Utilization, r.Utilization);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnNegativeWallScreenLeavesTheMemberNotEvaluated(bool ntc)
    {
        // RHS 400x150x6: V2 walls 388/6 = 64.7 > 48.82 (EC3) and > 58.58 (NTC); no reduced resistance is invented.
        static Section Slender() => new SectionRHS(400, 150, 6, 6, 6, 6, "RHS400x150x6");
        var r = BeamColumn(Slender, ntc, 0, 5e3);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Message != null && d.Message.Contains("Shear buckling is not excluded")
            || d.Code.Contains("ShearBuckling.ResistanceMethodRequired")), Diagnostics(r));
        Assert.AreEqual(0, Value(r, "MemberStability.ShearAssessment2.Excluded"));
        Assert.AreEqual(388.0 / 6, Value(r, "MemberStability.ShearAssessment2.WebSlenderness"), 1e-12);
        // The explicit source still takes precedence over the negative screen.
        var explicitSource = BeamColumn(Slender, ntc, 0, 5e3, "Independent wall assessment with end stiffeners, rev 1");
        Assert.AreEqual(EngineeringOutcome.Satisfied, explicitSource.Outcome, Diagnostics(explicitSource));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnScreensTheChsWallAndKeepsTheSourceBeyondTheLimit(bool ntc)
    {
        // Was NotEvaluated (BeamColumn.ShearBucklingExclusionSource). CHS 200x10 S355, V1 = 2 kN, V2 = 5 kN (<= .5 Vpl):
        // d/t = 20 <= 23.774, both axes excluded by the GPC wall screen. The shear does not enter the interaction: same
        // utilization as with an explicit source and as without shear.
        static Section Stocky() => new SectionCHS(200, 10, "CHS200x10");
        var r = BeamColumn(Stocky, ntc, 2e3, 5e3);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        foreach (int axis in new[] { 1, 2 })
        {
            Assert.AreEqual(1, Value(r, "MemberStability.ShearAssessment" + axis + ".Excluded"));
            Assert.AreEqual(20, Value(r, "MemberStability.ShearAssessment" + axis + ".DiameterToThickness"), 1e-12);
            Assert.AreEqual(23.774, Value(r, "MemberStability.ShearAssessment" + axis + ".DiameterToThicknessLimit"), 5e-4);
        }
        var explicitSource = BeamColumn(Stocky, ntc, 2e3, 5e3, "Independent shell assessment, rev 1");
        Assert.AreEqual(EngineeringOutcome.Satisfied, explicitSource.Outcome);
        Assert.AreEqual(r.Utilization, explicitSource.Utilization);
        Assert.IsFalse(explicitSource.Details.Trace.Any(t => t.Key.StartsWith("MemberStability.ShearAssessment")));
        Assert.AreEqual(BeamColumn(Stocky, ntc, 0, 0).Utilization, r.Utilization);
        // CHS 300x8, d/t = 37.5 > 23.774: as before the screen the member needs ShearBucklingExclusionSource.
        static Section Slender() => new SectionCHS(300, 8, "CHS300x8");
        var waiting = BeamColumn(Slender, ntc, 0, 5e3);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, waiting.Outcome); Assert.IsNull(waiting.Utilization);
        Assert.IsTrue(waiting.Diagnostics.Any(d => (d.Code + " " + d.Message).Contains("BeamColumn.ShearBucklingExclusionSource")), Diagnostics(waiting));
        Assert.IsFalse(waiting.Details.Trace.Any(t => t.Key.StartsWith("MemberStability.ShearAssessment")));
        Assert.AreEqual(EngineeringOutcome.Satisfied, BeamColumn(Slender, ntc, 0, 5e3, "Independent shell assessment, rev 1").Outcome);
    }
}
