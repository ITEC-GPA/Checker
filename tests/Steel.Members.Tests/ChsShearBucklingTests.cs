using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Shear-buckling screen of circular hollow sections, EC3 and NTC alike: EN 1993-1-6:2007 Annex D.1.4.2(3)
/// eq. (D.40), no check when r/t &lt;= 0.16 (E/fy)^0.67, with r = d/2 (conservative against the mid-surface radius):
/// d/t &lt;= 0.32 (E/fy)^0.67. E = 210000: S235 d/t &lt;= 30.368, S355 d/t &lt;= 23.035. (D.40) stays below the bound the
/// D.32 long cylinder and the D.39 plateau give, 2 (0.04 sqrt3 E/fy)^(2/3) (31.300 and 23.774).
/// Hand calculations from those expressions; no stored expectations.</summary>
[TestClass]
public class ChsShearBucklingTests
{
    private static SteelVerificationInput Input(Section shape, bool ntc, double fy = 355, double m1 = 0, double v1 = 0, double v2 = 0)
        => new(new SteelSectionInput(new SteelSection(shape, new SteelMaterial("S", 210000, fy, 510)), true), new SteelActions(0, v1, v2, 0, m1, 0),
            ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "test"), 1.0), new SteelMemberInput(4000, 4000, 4000));
    private static double Value(SteelShearBucklingAssessment a, string name) => a.Trace.Single(v => v.Name == name).Value;

    // (D.40) r/t <= 0.16 (E/fy)^0.67 with r = d/2: d/t <= 2 * 0.16 (E/fy)^0.67.
    private static double HandLimit(double fy) => 2 * .16 * Math.Pow(210000 / fy, .67);
    // Long cylinder 0.25 E (t/r)^1.5 >= fy/(sqrt3 * 0.4^2) with r = d/2: d/t <= 2 (0.25 E sqrt3 0.16/fy)^(2/3).
    private static double PlateauBound(double fy) => 2 * Math.Pow(.25 * 210000 * Math.Sqrt(3) * .4 * .4 / fy, 2.0 / 3);

    [DataTestMethod]
    [DataRow(235d, 30.4)] [DataRow(355d, 23.0)]
    public void LimitIsEquationD40WithTheOuterRadius(double fy, double rounded)
    {
        // S235: 210000/235 = 893.617, ^0.67 = 94.901, d/t = 0.32*94.901 = 30.368; S355: 591.549 -> 71.984 -> 23.035.
        foreach (bool ntc in new[] { false, true })
        {
            var a = SteelShearBucklingAssessment.EvaluateChsWall(Input(new SectionCHS(200, 10, "CHS"), ntc, fy, v2: 1e4), 2, "screen");
            double limit = Value(a, "DiameterToThicknessLimit");
            Assert.AreEqual(HandLimit(fy), limit, 1e-12); Assert.AreEqual(rounded, Math.Round(limit, 1));
            Assert.AreEqual(fy == 235 ? 30.368 : 23.035, limit, 5e-4);
            // About 3% below the D.32/D.39 bound (31.300, 23.774): the clause is on the safe side of the derivation.
            Assert.IsTrue(limit < PlateauBound(fy)); Assert.AreEqual(fy == 235 ? .9702 : .9689, limit / PlateauBound(fy), 5e-5);
            Assert.AreEqual(20, Value(a, "DiameterToThickness"), 1e-12); Assert.IsTrue(a.IsBucklingExcluded);
        }
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ScreenAppliesToBothAxesOfTheAxisymmetricWall(bool ntc)
    {
        // CHS 200x10 S355: d/t = 20 <= 23.035, excluded on V1 and V2.
        var stocky = Input(new SectionCHS(200, 10, "CHS200x10"), ntc, v1: 1e4, v2: 1e4);
        foreach (int axis in new[] { 1, 2 })
        {
            var a = SteelShearBucklingAssessment.EvaluateChsWall(stocky, axis, "screen source");
            Assert.AreEqual(axis, a.Axis); Assert.IsTrue(a.IsBucklingExcluded);
            Assert.AreEqual(200, Value(a, "Diameter")); Assert.AreEqual(10, Value(a, "WallThickness"));
            Assert.AreEqual(355, Value(a, "Fy")); Assert.AreEqual(210000, Value(a, "E"));
            Assert.AreEqual(6, a.Trace.Count);
            StringAssert.Contains(a.Reference, "EN 1993-1-6:2007 Annex D.1.4.2(3) eq. (D.40)");
            StringAssert.Contains(a.Reference, "V" + axis);
            Assert.AreEqual(ntc, a.Reference.Contains("NTC 2018 chapter 12"));
            Assert.AreEqual("screen source", a.Source);
        }
        // CHS 300x8 S355: d/t = 37.5 > 23.035.
        var slender = SteelShearBucklingAssessment.EvaluateChsWall(Input(new SectionCHS(300, 8, "CHS300x8"), ntc, v2: 1e4), 2, "screen source");
        Assert.IsFalse(slender.IsBucklingExcluded);
        Assert.AreEqual(37.5, Value(slender, "DiameterToThickness"), 1e-12);
    }

    [TestMethod]
    public void TheLimitIsSharpAndDependsOnTheGrade()
    {
        // S355 limit 23.0348: d/t = 23.03 passes, 23.04 fails; at the limit r/t = d/2t = 0.16 (E/fy)^0.67 exactly.
        Assert.IsTrue(SteelShearBucklingAssessment.EvaluateChsWall(Input(new SectionCHS(230.3, 10, "a"), false, v2: 1e4), 2, "s").IsBucklingExcluded);
        var beyond = SteelShearBucklingAssessment.EvaluateChsWall(Input(new SectionCHS(230.4, 10, "b"), false, v2: 1e4), 2, "s");
        Assert.IsFalse(beyond.IsBucklingExcluded);
        Assert.AreEqual(.16 * Math.Pow(210000 / 355d, .67), Value(beyond, "DiameterToThicknessLimit") / 2, 1e-12);
        // CHS 300x10, d/t = 30: within S235 (30.368), beyond S355 (23.035).
        Assert.IsTrue(SteelShearBucklingAssessment.EvaluateChsWall(Input(new SectionCHS(300, 10, "c"), true, 235, v2: 1e4), 2, "s").IsBucklingExcluded);
        Assert.IsFalse(SteelShearBucklingAssessment.EvaluateChsWall(Input(new SectionCHS(300, 10, "c"), true, 355, v2: 1e4), 2, "s").IsBucklingExcluded);
    }

    [TestMethod]
    public void OnlyAQualifiedChsIsScreened()
    {
        Assert.ThrowsException<NotSupportedException>(() => SteelShearBucklingAssessment.EvaluateChsWall(Input(new SectionRHS(300, 200, 10, 10, 10, 10, "RHS"), false, v2: 1e4), 2, "RHS"));
        Assert.ThrowsException<NotSupportedException>(() => SteelShearBucklingAssessment.EvaluateChsWall(Input(new SectionH(300, 12, 150, 18, 150, 18, "H"), true, v2: 1e4), 2, "H"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelShearBucklingAssessment.EvaluateChsWall(Input(new SectionCHS(200, 10, "CHS"), false, v2: 1e4), 3, "axis"));
        Assert.ThrowsException<ArgumentException>(() => SteelShearBucklingAssessment.EvaluateChsWall(Input(new SectionCHS(200, 10, "CHS"), false, v2: 1e4), 2, " "));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void SectionShearUsesThePlasticResistanceOnlyWithinTheScreen(bool ntc)
    {
        // CHS 200x10: A = pi/4 (200^2 - 180^2) = 1900 pi = 5969.03, Av = 2A/pi = 3800 (EC3 6.2.6(3)(g), NTC 4.2.4.1.2.4).
        // V2 = 100 kN: V/Vpl, Vpl = Av fy/(sqrt3 gM0), gM0 = 1 or 1.05.
        var i = Input(new SectionCHS(200, 10, "CHS200x10"), ntc, v2: 1e5);
        var plan = SteelMethodSelector.Select(i);
        Assert.AreEqual(SteelExecutionStatus.RequiresPreparation, SteelVerificationPipeline.Execute(plan).Checks.Single(c => c.Decision.Target == SteelCheckTarget.Shear2).Status);
        var check = SteelVerificationPipeline.Execute(plan, new[] { SteelShearBucklingAssessment.EvaluateChsWall(i, 2, "screen") })
            .Checks.Single(c => c.Decision.Target == SteelCheckTarget.Shear2);
        double g = ntc ? 1.05 : 1, expected = 1e5 / (3800 * 355 / (Math.Sqrt(3) * g));
        Assert.AreEqual(SteelExecutionStatus.Passed, check.Status); Assert.AreEqual(expected, check.Utilization!.Value, expected * 1e-12);
        Assert.AreEqual(1, check.ShearAssessments.Count);
        // CHS 300x8 (37.5): a negative screen cannot use Vpl.
        var thin = Input(new SectionCHS(300, 8, "CHS300x8"), ntc, v2: 1e4);
        var negative = SteelVerificationPipeline.Execute(SteelMethodSelector.Select(thin), new[] { SteelShearBucklingAssessment.EvaluateChsWall(thin, 2, "screen") })
            .Checks.Single(c => c.Decision.Target == SteelCheckTarget.Shear2);
        Assert.AreEqual(SteelExecutionStatus.NotSupported, negative.Status); Assert.AreEqual("ShearBuckling.ResistanceMethodRequired", negative.DiagnosticCode);
    }

    // Curves a/b, Cm1=.9, Cm2=.8, CmLT=.7, lambda1=.6, lambda2=1.2 on the gross area, as in RhsShearBucklingTests.
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
    public void BeamColumnChsWaitsForTheScreenWithinTheLimitAndKeepsTheSourceBeyondIt(bool ntc)
    {
        // CHS 200x10 S355, N = 0, M1 = 20 kNm, V1 = 5 kN, V2 = 10 kN (<= .5 Vpl): d/t = 20 <= 23.035.
        var i = Input(new SectionCHS(200, 10, "CHS200x10"), ntc, m1: 2e7, v1: 5e3, v2: 1e4);
        var plan = SteelMethodSelector.SelectMember(BeamColumn(i));
        var decision = plan.Decisions.Single();
        Assert.AreEqual(SteelSelectionStatus.RequiresPreparation, decision.Status, decision.Message);
        StringAssert.Contains(decision.Message, "EvaluateChsWall");
        Assert.AreEqual(SteelExecutionStatus.RequiresPreparation, SteelVerificationPipeline.Execute(plan).Checks.Single().Status);
        var screened = SteelVerificationPipeline.Execute(plan, new[] {
            SteelShearBucklingAssessment.EvaluateChsWall(i, 1, "screen"), SteelShearBucklingAssessment.EvaluateChsWall(i, 2, "screen") }).Checks.Single();
        Assert.IsTrue(screened.IsEvaluated, screened.Message); Assert.AreEqual(2, screened.ShearAssessments.Count);
        var explicitSource = SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(BeamColumn(i, "independent shell assessment"))).Checks.Single();
        Assert.AreEqual(explicitSource.Utilization, screened.Utilization);
        // n = 0, k11 = Cm1 = .9 (elastic hollow): .9 M1 gM1/(Wel1 fy), Wel1 = pi (200^4 - 180^4)/(32*200) = 270098 mm3.
        double wel = Math.PI * (Math.Pow(200, 4) - Math.Pow(180, 4)) / (32 * 200), expected = .9 * 2e7 * (ntc ? 1.05 : 1) / (wel * 355);
        Assert.AreEqual(expected, screened.Utilization!.Value, expected * 1e-9);
        // CHS 300x8 S355, d/t = 37.5 > 23.035: as before the screen, the explicit source stays required.
        var slender = SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(BeamColumn(Input(new SectionCHS(300, 8, "CHS300x8"), ntc, m1: 2e7, v2: 1e4)))).Checks.Single();
        Assert.AreEqual(SteelExecutionStatus.MissingInput, slender.Status);
        CollectionAssert.Contains(slender.Decision.MissingInputs.ToList(), "BeamColumn.ShearBucklingExclusionSource");
    }
}
