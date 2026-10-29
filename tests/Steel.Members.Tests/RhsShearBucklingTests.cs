using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Shear-buckling exclusion on rectangular hollow sections: the walls parallel to the shear are unstiffened
/// webs, EN 1993-1-1:2005 6.2.6(6) / EN 1993-1-5:2006 5.1(2) hw/t &lt;= 72 eps/eta, NTC 2018 eq. 4.2.27 with eta = 1.
/// hw is the clear distance between the inner faces of the walls normal to the shear (corner radii ignored), t the
/// thinner web. S355: eps = sqrt(235/355) = 0.81362, limits 72 eps/1.2 = 48.82 (EC3, eta of the code object) and
/// 72 eps = 58.58 (NTC). Hand calculations from the clear dimensions; no stored expectations.</summary>
[TestClass]
public class RhsShearBucklingTests
{
    private static readonly double Eps = Math.Sqrt(235.0 / 355);

    private static SteelVerificationInput Input(Section shape, bool ntc, double n = 0, double m1 = 0, double v1 = 0, double v2 = 0)
        => new(new SteelSectionInput(new SteelSection(shape, new SteelMaterial("S", 210000, 355, 510)), true), new SteelActions(n, v1, v2, 0, m1, 0),
            ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "test"), 1.0), new SteelMemberInput(4000, 4000, 4000));
    private static SteelShearBucklingAssessment Walls(SteelVerificationInput i, int axis, bool ntc)
        => ntc ? SteelShearBucklingAssessment.EvaluateNtcRhsWebs(i, axis, "NTC eq. 4.2.27")
            : SteelShearBucklingAssessment.EvaluateEc3RhsWebs(i, axis, 1.2, "EC3 eta 1.2");
    private static double Value(SteelShearBucklingAssessment a, string name) => a.Trace.Single(v => v.Name == name).Value;

    [DataTestMethod]
    // RHS 300x200x10: V2 walls hw = 300 - 2*10 = 280, 280/10 = 28; V1 walls hw = 200 - 2*10 = 180, 18: both excluded.
    [DataRow(false, 300d, 200d, 10d, 10d, 10d, 10d, 280d, 10d, true, 180d, 10d, true)]
    [DataRow(true, 300d, 200d, 10d, 10d, 10d, 10d, 280d, 10d, true, 180d, 10d, true)]
    // Thinner left web: V2 hw = 280, t = min(4, 6) = 4, 70 > 48.82 and > 58.58; V1 hw = 200 - 4 - 6 = 190, t = 10, 19.
    [DataRow(false, 300d, 200d, 10d, 10d, 4d, 6d, 280d, 4d, false, 190d, 10d, true)]
    [DataRow(true, 300d, 200d, 10d, 10d, 4d, 6d, 280d, 4d, false, 190d, 10d, true)]
    // RHS 300x200x4: V2 292/4 = 73 fails both; V1 192/4 = 48 <= 48.82 (EC3) and <= 58.58 (NTC).
    [DataRow(false, 300d, 200d, 4d, 4d, 4d, 4d, 292d, 4d, false, 192d, 4d, true)]
    [DataRow(true, 300d, 200d, 4d, 4d, 4d, 4d, 292d, 4d, false, 192d, 4d, true)]
    // RHS 300x250x4: V1 242/4 = 60.5 > 48.82 and > 58.58: the walls of width b fail.
    [DataRow(false, 300d, 250d, 4d, 4d, 4d, 4d, 292d, 4d, false, 242d, 4d, false)]
    [DataRow(true, 300d, 250d, 4d, 4d, 4d, 4d, 292d, 4d, false, 242d, 4d, false)]
    public void WallsParallelToEachShearActAsWebs(bool ntc, double h, double b, double tTop, double tBottom, double tLeft, double tRight,
        double hw2, double t2, bool excluded2, double hw1, double t1, bool excluded1)
    {
        var i = Input(new SectionRHS(h, b, tTop, tBottom, tLeft, tRight, "RHS"), ntc, v1: 1e4, v2: 1e4);
        double eta = ntc ? 1 : 1.2, limit = 72 * Eps / eta;
        foreach (var (axis, hw, t, excluded) in new[] { (2, hw2, t2, excluded2), (1, hw1, t1, excluded1) })
        {
            var a = Walls(i, axis, ntc);
            Assert.AreEqual(axis, a.Axis); Assert.AreEqual(excluded, a.IsBucklingExcluded, "axis " + axis);
            Assert.AreEqual(hw, Value(a, "ClearWebHeight"), 1e-12); Assert.AreEqual(t, Value(a, "WebThickness"));
            Assert.AreEqual(hw / t, Value(a, "WebSlenderness"), 1e-12); Assert.AreEqual(limit, Value(a, "WebSlendernessLimit"), 1e-12);
            Assert.AreEqual(eta, Value(a, "Eta")); Assert.AreEqual(355, Value(a, "Fy"));
            StringAssert.Contains(a.Reference, ntc ? "NTC 2018 4.2.4.1.2.4 equation 4.2.27" : "EN 1993-1-1:2005 6.2.6(6), EN 1993-1-5:2006 5.1(2)");
            StringAssert.Contains(a.Reference, "parallel to V" + axis);
        }
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void RoundedCornersUseTheClearDistanceBetweenTheInnerFaces(bool ntc)
    {
        // RHS 200x150x10, ro = 15, ri = 10: hw = 200 - 20 = 180 (flat 160 plus the inner radii), 18; b walls 130/10 = 13.
        var i = Input(new SectionRHSRoundedCorners(200, 150, 10, 15, 10, "RHS"), ntc, v1: 1e4, v2: 1e4);
        Assert.AreEqual(18, Value(Walls(i, 2, ntc), "WebSlenderness"), 1e-12);
        Assert.AreEqual(13, Value(Walls(i, 1, ntc), "WebSlenderness"), 1e-12);
        Assert.IsTrue(Walls(i, 1, ntc).IsBucklingExcluded && Walls(i, 2, ntc).IsBucklingExcluded);
    }

    [TestMethod]
    public void OnlyAQualifiedRhsWithHorizontalAxisOneIsScreened()
    {
        var rhs = new SectionRHS(300, 200, 10, 10, 10, 10, "RHS");
        Assert.ThrowsException<NotSupportedException>(() => SteelShearBucklingAssessment.EvaluateEc3RhsWebs(Input(rhs, true, v2: 1e4), 2, 1.2, "EC3 on NTC"));
        Assert.ThrowsException<NotSupportedException>(() => SteelShearBucklingAssessment.EvaluateNtcRhsWebs(Input(rhs, false, v2: 1e4), 2, "NTC on EC3"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelShearBucklingAssessment.EvaluateEc3RhsWebs(Input(rhs, false, v2: 1e4), 3, 1.2, "axis"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelShearBucklingAssessment.EvaluateEc3RhsWebs(Input(rhs, false, v2: 1e4), 2, double.NaN, "eta"));
        // CHS: EN 1993-1-5 does not cover circular tubes; no inference. H: its own web/flange screens.
        Assert.ThrowsException<NotSupportedException>(() => SteelShearBucklingAssessment.EvaluateEc3RhsWebs(Input(new SectionCHS(200, 10, "CHS"), false, v2: 1e4), 2, 1.2, "CHS"));
        Assert.ThrowsException<NotSupportedException>(() => SteelShearBucklingAssessment.EvaluateNtcRhsWebs(Input(new SectionH(300, 12, 150, 18, 150, 18, "H"), true, v2: 1e4), 2, "H"));
        // Wide RHS: axis 1 is vertical (AngleX1 = -pi/2), the alignment is not exact. Model's rounded radius R > 0 is not qualified.
        var wide = Input(new SectionRHS(200, 300, 10, 10, 10, 10, "wide"), false, v2: 1e4);
        Assert.AreEqual(-Math.PI / 2, wide.Section.PrincipalAxisAngle, 1e-12);
        Assert.ThrowsException<NotSupportedException>(() => SteelShearBucklingAssessment.EvaluateEc3RhsWebs(wide, 2, 1.2, "wide"));
        Assert.ThrowsException<NotSupportedException>(() => SteelShearBucklingAssessment.EvaluateEc3RhsWebs(Input(new SectionRHS(300, 200, 10, 10, 10, 10, "R", 15), false, v2: 1e4), 2, 1.2, "R"));
        // A square SHS keeps axis 1 horizontal: both axes are screened alike, 292/4 = 73.
        var square = Input(new SectionRHS(300, 300, 4, 4, 4, 4, "SHS"), false, v1: 1e4, v2: 1e4);
        Assert.AreEqual(0, square.Section.PrincipalAxisAngle);
        Assert.AreEqual(73, Value(SteelShearBucklingAssessment.EvaluateEc3RhsWebs(square, 1, 1.2, "SHS"), "WebSlenderness"), 1e-12);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void SectionShearUsesThePlasticResistanceOnlyWhenTheWallsExcludeBuckling(bool ntc)
    {
        // RHS 300x200x10 rolled: A = 300*200 - 280*180 = 9600; Av2 = A h/(b+h) = 5760, Av1 = A b/(b+h) = 3840 (EC3 6.2.6(3)(f)).
        // V1 = 50 kN, V2 = 100 kN: conservative sum V1/Vpl1 + V2/Vpl2, Vpl = Av fy/(sqrt3 gM0), gM0 = 1 or 1.05.
        var i = Input(new SectionRHS(300, 200, 10, 10, 10, 10, "RHS"), ntc, v1: 5e4, v2: 1e5);
        var plan = SteelMethodSelector.Select(i);
        Assert.AreEqual(SteelExecutionStatus.RequiresPreparation, SteelVerificationPipeline.Execute(plan).Checks.Single(c => c.Decision.Target == SteelCheckTarget.Shear2).Status);
        var result = SteelVerificationPipeline.Execute(plan, new[] { Walls(i, 1, ntc), Walls(i, 2, ntc) });
        Assert.IsTrue(result.IsComplete);
        double g = ntc ? 1.05 : 1, expected = 5e4 / (3840 * 355 / (Math.Sqrt(3) * g)) + 1e5 / (5760 * 355 / (Math.Sqrt(3) * g));
        foreach (var target in new[] { SteelCheckTarget.Shear1, SteelCheckTarget.Shear2 })
        {
            var check = result.Checks.Single(c => c.Decision.Target == target);
            Assert.AreEqual(SteelExecutionStatus.Passed, check.Status); Assert.AreEqual(expected, check.Utilization!.Value, expected * 1e-12);
            Assert.AreEqual(2, check.ShearAssessments.Count);
        }
        // Thin RHS 300x200x4: the V2 walls (73) fail, the V1 walls (48) pass; the failing axis cannot use Vpl.
        var thin = Input(new SectionRHS(300, 200, 4, 4, 4, 4, "thin"), ntc, v2: 1e4);
        var negative = SteelVerificationPipeline.Execute(SteelMethodSelector.Select(thin), new[] { Walls(thin, 2, ntc) }).Checks.Single(c => c.Decision.Target == SteelCheckTarget.Shear2);
        Assert.AreEqual(SteelExecutionStatus.NotSupported, negative.Status); Assert.AreEqual("ShearBuckling.ResistanceMethodRequired", negative.DiagnosticCode);
        var minor = Input(new SectionRHS(300, 200, 4, 4, 4, 4, "thin"), ntc, v1: 1e4);
        var positive = SteelVerificationPipeline.Execute(SteelMethodSelector.Select(minor), new[] { Walls(minor, 1, ntc) }).Checks.Single(c => c.Decision.Target == SteelCheckTarget.Shear1);
        Assert.AreEqual(SteelExecutionStatus.Passed, positive.Status);
        // A = 300*200 - 292*192 = 3936, Av1 = 3936*200/500 = 1574.4.
        Assert.AreEqual(1e4 / (1574.4 * 355 / (Math.Sqrt(3) * g)), positive.Utilization!.Value, 1e-12);
    }

    // Curves a/b, Cm1=.9, Cm2=.8, CmLT=.7, lambda1=.6, lambda2=1.2 on the gross area, as in MemberStabilityWithoutCompressionTests.
    private static SteelMemberAnalysis BeamColumn(SteelVerificationInput i, string? shear = null)
    {
        var values = new List<SteelStabilityValue> {
            new(SteelStabilityQuantity.Ncr1, i.Section.Area * 355 / (.6 * .6), SteelStabilityUnit.N, "lambda1=.6"),
            new(SteelStabilityQuantity.Ncr2, i.Section.Area * 355 / (1.2 * 1.2), SteelStabilityUnit.N, "lambda2=1.2") };
        var p = new SteelBeamColumnParameters(SteelBucklingCurve.A, SteelBucklingCurve.B, false, .9, .8, .7, "declared equivalent moments",
            "non-sway member, global effects accounted", "T assessed", "FT assessed",
            lateralTorsionalExclusionSource: "closed hollow section, twist restraint assessed", shearBucklingExclusionSource: shear);
        return SteelMemberAnalysis.BeamColumn(i, "one design state", "code policy", p, values, null, null);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnRhsWaitsForTheWallScreensAndAnExplicitSourceWins(bool ntc)
    {
        // RHS 400x150x6 S355, N = 0, M1 = 50 kNm, V1 = 5 kN, V2 = 10 kN (both <= .5 Vpl). Class under M1 (Table 5.2, alpha = .5):
        // h walls 388/6 = 64.7 <= 83 eps = 67.5, b walls 138/6 = 23 <= 33 eps: class 2, elastic hollow interaction.
        // Shear: V2 walls 64.7 > 48.82 (EC3) and > 58.58 (NTC) -> negative; V1 walls 23 -> excluded.
        var i = Input(new SectionRHS(400, 150, 6, 6, 6, 6, "RHS400x150x6"), ntc, m1: 5e7, v1: 5e3, v2: 1e4);
        var plan = SteelMethodSelector.SelectMember(BeamColumn(i));
        var decision = plan.Decisions.Single();
        Assert.AreEqual(SteelSelectionStatus.RequiresPreparation, decision.Status, decision.Message);
        StringAssert.Contains(decision.Message, "EvaluateEc3RhsWebs/EvaluateNtcRhsWebs");
        Assert.AreEqual(SteelExecutionStatus.RequiresPreparation, SteelVerificationPipeline.Execute(plan).Checks.Single().Status);
        var negative = SteelVerificationPipeline.Execute(plan, new[] { Walls(i, 1, ntc), Walls(i, 2, ntc) }).Checks.Single();
        Assert.AreEqual(SteelExecutionStatus.NotSupported, negative.Status); Assert.AreEqual("ShearBuckling.ResistanceMethodRequired", negative.DiagnosticCode);
        Assert.AreEqual(388.0 / 6, Value(negative.ShearAssessments.Single(a => a.Axis == 2), "WebSlenderness"), 1e-12);
        // Only one of two active shears assessed: still waiting.
        Assert.AreEqual(SteelExecutionStatus.RequiresPreparation, SteelVerificationPipeline.Execute(plan, new[] { Walls(i, 1, ntc) }).Checks.Single().Status);
        // The explicit source wins: Ready without assessments.
        var external = SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(BeamColumn(i, "independent wall assessment"))).Checks.Single();
        Assert.IsTrue(external.IsEvaluated, external.Message); Assert.AreEqual(0, external.ShearAssessments.Count);
        // Stocky RHS 300x200x10 with the same actions: walls 28 and 18 pass; the interaction equals the explicit-source one.
        var stocky = Input(new SectionRHS(300, 200, 10, 10, 10, 10, "RHS300x200x10"), ntc, m1: 5e7, v1: 5e3, v2: 1e4);
        var screened = SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(BeamColumn(stocky)), new[] { Walls(stocky, 1, ntc), Walls(stocky, 2, ntc) }).Checks.Single();
        Assert.IsTrue(screened.IsEvaluated, screened.Message); Assert.AreEqual(2, screened.ShearAssessments.Count);
        var explicitStocky = SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(BeamColumn(stocky, "independent wall assessment"))).Checks.Single();
        Assert.AreEqual(explicitStocky.Utilization, screened.Utilization);
        // n = 0, k11 = Cm1 = .9, k21 = .8 k11 (Table B.1 / C4.2.IV elastic): .9 M1Ed gM1/(Wel1 fy), Wel1 = (200*300^3 - 180*280^3)/12/150 = 804800.
        double expected = .9 * 5e7 * (ntc ? 1.05 : 1) / (804800 * 355d);
        Assert.AreEqual(expected, screened.Utilization!.Value, expected * 1e-9);
    }
}
