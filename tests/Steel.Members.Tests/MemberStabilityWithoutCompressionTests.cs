using System.Globalization;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Member stability without compression (minor bending alone, LTB and biaxial beam-columns with tension
/// neglected as favourable), the automatic H shear-buckling prerequisite of the beam-column and its class under the
/// concomitant N-M. References are hand calculations from the code expressions and closed-form section properties.</summary>
[TestClass]
public class MemberStabilityWithoutCompressionTests
{
    // IPE 300: published Wpl,y = 628.4 cm3, Wpl,z = 125.2 cm3. UKB 406x140x39: h 398, b 141.8, tw 6.4, tf 8.6, r 10.2.
    private static SectionH Ipe300() => new(300, 7.1, 150, 10.7, 150, 10.7, "IPE300", 15);
    private static SectionH Ukb406() => new(398, 6.4, 141.8, 8.6, 141.8, 8.6, "UKB406x140x39", 10.2);

    private static SteelVerificationInput Input(Section shape, bool ntc, double n = 0, double m1 = 0, double m2 = 0, double v1 = 0, double v2 = 0)
        => new(new SteelSectionInput(new SteelSection(shape, new SteelMaterial("S", 210000, 355, 510)), true), new SteelActions(n, v1, v2, 0, m1, m2),
            ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "test"), 1.0), new SteelMemberInput(4000, 4000, 4000));

    private static SteelCheckExecution Run(SteelMemberAnalysis a) => SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
    private static double Value(SteelCheckExecution r, string name) => r.Trace.Single(v => v.Name == name).Value;
    private static double Chi(double lambda, double alpha)
    { double phi = .5 * (1 + alpha * (lambda - .2) + lambda * lambda); return Math.Min(1, 1 / (phi + Math.Sqrt(phi * phi - lambda * lambda))); }

    private static SteelMemberAnalysis Bending(SteelVerificationInput i) => SteelMemberAnalysis.Bending(i, "whole-member maxima", "profile",
        SteelCriticalMomentBasis.UniformMoment, new SteelStabilityValue(SteelStabilityQuantity.Mcr, 2e8, SteelStabilityUnit.Nmm, "independent"));

    // Curves a/b, Cm1=.9, Cm2=.8, CmLT=.7; Ncr from the target slendernesses on the gross area.
    private static SteelMemberAnalysis BeamColumn(SteelVerificationInput i, bool susceptible, double lambda2 = 1.2, double? mcr = null, string? shear = null,
        int? memberClass = null)
    {
        var values = new List<SteelStabilityValue> {
            new(SteelStabilityQuantity.Ncr1, i.Section.Area * 355 / (.6 * .6), SteelStabilityUnit.N, "lambda1=.6"),
            new(SteelStabilityQuantity.Ncr2, i.Section.Area * 355 / (lambda2 * lambda2), SteelStabilityUnit.N, "lambda2") };
        if (mcr.HasValue) values.Add(new(SteelStabilityQuantity.Mcr, mcr.Value, SteelStabilityUnit.Nmm, "independent uniform-moment value"));
        var p = new SteelBeamColumnParameters(SteelBucklingCurve.A, SteelBucklingCurve.B, susceptible, .9, .8, .7, "declared equivalent moments",
            "non-sway member, global effects accounted", "T assessed", "FT assessed",
            lateralTorsionalExclusionSource: susceptible ? null : "continuous twist restraint", shearBucklingExclusionSource: shear);
        return SteelMemberAnalysis.BeamColumn(i, "one design state", "code policy", p, values, memberClass, memberClass.HasValue ? "independent class" : null);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void MinorAxisBendingAloneNeedsNoLateralTorsionalCheck(bool ntc)
    {
        // Was NotSupported (Member.PrismaticActionPattern). M2 alone, with or without tension and V1: no LTB, no flexural buckling.
        foreach (var i in new[] { Input(Ipe300(), ntc, m2: 1e7), Input(Ipe300(), ntc, n: 1e5, m2: 1e7, v1: 1e4) })
        {
            var r = Run(Bending(i));
            Assert.AreEqual(SteelExecutionStatus.NotRequired, r.Status); Assert.AreEqual("Member.MinorAxisBendingOnly", r.DiagnosticCode);
        }
        Assert.AreEqual(SteelExecutionStatus.NotRequired, Run(Bending(Input(Ipe300(), ntc, n: 1e5))).Status); // tension alone
        // Compression with M2 stays a beam-column matter; M1 with M2 is pointed to the beam-column route.
        Assert.AreEqual("Member.PrismaticActionPattern", Run(Bending(Input(Ipe300(), ntc, n: -1e5, m2: 1e7))).DiagnosticCode);
        Assert.AreEqual("Member.BiaxialBendingRequiresBeamColumn", Run(Bending(Input(Ipe300(), ntc, m1: 1e8, m2: 1e7))).DiagnosticCode);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void LateralTorsionalBucklingNeglectsTensionAsFavourable(bool ntc)
    {
        // IPE 300 S355, M1 = 100 kNm with N = +200 kN. The class follows M1 alone (web 35.0 <= 72 eps: class 1, Wpl,y);
        // resistance as in pure bending: lambdaLT = sqrt(Wpl fy/Mcr), curve a (EC3 Table 6.4) / b (NTC Table 4.2.IX), h/b = 2.
        var tension = Run(Bending(Input(Ipe300(), ntc, n: 2e5, m1: 1e8)));
        var pure = Run(Bending(Input(Ipe300(), ntc, m1: 1e8)));
        Assert.IsTrue(tension.IsEvaluated, tension.Message); Assert.AreEqual(1, tension.Decision.SectionClass);
        Assert.AreEqual(pure.Resistance, tension.Resistance); Assert.AreEqual(pure.Utilization, tension.Utilization);
        Assert.AreEqual(2e5, Value(tension, "TensionNeglectedAsFavourable")); StringAssert.Contains(tension.Scope, "tension N>0 neglected");
        double gamma = ntc ? 1.05 : 1, wpl = 628.4e3, chi = Chi(Math.Sqrt(wpl * 355 / 2e8), ntc ? .34 : .21);
        Assert.AreEqual(Math.Min(chi, 1) * wpl * 355 / gamma, tension.Resistance!.Value, tension.Resistance.Value * 3e-3);
    }

    [DataTestMethod]
    [DataRow(false, false, 1.2, 0d)] [DataRow(true, false, 1.2, 0d)] [DataRow(false, true, 1.2, 0d)] [DataRow(true, true, 1.2, 0d)]
    [DataRow(false, true, .3, 0d)] [DataRow(true, true, .3, 1e5)] [DataRow(false, false, 1.2, 1e5)]
    public void BiaxialBendingWithoutCompressionUsesTheInteractionAtZeroAxialForce(bool ntc, bool susceptible, double lambda2, double tension)
    {
        // Was NotSupported (Member.BeamColumnCompressionRequired). IPE 300 S355, M1 = 60 kNm, M2 = 10 kNm, N = 0 or tension
        // (neglected): class 1 in every envelope state (web 35.0 <= 72 eps, flanges 5.28 <= 9 eps). EC3 Annex B Tables B.1-B.2 /
        // Circolare C4.2.IV-V at n = 0, Cm = .9/.8/.7: k11 = .9, k22 = .8, k12 = .6 k22 = .48; k21 = .6 k11 = .54 if restrained,
        // otherwise 1 - .1 lambda2 n2/(CmLT - .25) = 1 for lambda2 >= .4 and min(.6 + lambda2, 1) below (classes 1-2).
        var i = Input(Ipe300(), ntc, n: tension, m1: 6e7, m2: 1e7); const double mcr = 2e8;
        var r = Run(BeamColumn(i, susceptible, lambda2, susceptible ? mcr : null));
        Assert.IsTrue(r.IsEvaluated, r.Message); Assert.AreEqual(1, r.Decision.SectionClass);
        double k21 = susceptible ? (lambda2 < .4 ? .6 + lambda2 : 1) : .54;
        Assert.AreEqual(.9, Value(r, "K11"), 1e-12); Assert.AreEqual(.48, Value(r, "K12"), 1e-12);
        Assert.AreEqual(k21, Value(r, "K21"), 1e-12); Assert.AreEqual(.8, Value(r, "K22"), 1e-12);
        double gamma = ntc ? 1.05 : 1, wpl1 = 628.4e3, wpl2 = 125.2e3;
        double chiLT = susceptible ? Chi(Math.Sqrt(wpl1 * 355 / mcr), ntc ? .34 : .21) : 1, m1 = 6e7 * gamma / (chiLT * wpl1 * 355), m2 = 1e7 * gamma / (wpl2 * 355);
        double expected = Math.Max(.9 * m1 + .48 * m2, k21 * m1 + .8 * m2);
        Assert.AreEqual(expected, r.Utilization!.Value, expected * 3e-3);
        Assert.AreEqual(tension > 0, r.Trace.Any(v => v.Name == "TensionNeglectedAsFavourable" && v.Value == tension));
        StringAssert.Contains(r.Scope, "NEd=0");
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void HollowBeamColumnWithoutCompressionKeepsTheElasticCoefficientsAndItsExplicitShearSource(bool ntc)
    {
        // Table B.1 / C4.2.IV elastic column at n = 0: k11 = Cm1 = .9, k22 = Cm2 = .8, k12 = k22, k21 = .8 k11 = .72.
        var rhs = CommonSectionCoverageTests.Shape("RHS");
        var r = Run(BeamColumn(Input(rhs, ntc, m1: 5e7, m2: 2e7), false));
        Assert.IsTrue(r.IsEvaluated, r.Message); Assert.AreEqual(1, Value(r, "ElasticInteraction"));
        Assert.AreEqual(.9, Value(r, "K11"), 1e-12); Assert.AreEqual(.8, Value(r, "K12"), 1e-12);
        Assert.AreEqual(.72, Value(r, "K21"), 1e-12); Assert.AreEqual(.8, Value(r, "K22"), 1e-12);
        double g = ntc ? 1.05 : 1, m1 = 5e7 * g / Value(r, "M1Rk"), m2 = 2e7 * g / Value(r, "M2Rk");
        Assert.AreEqual(Math.Max(.9 * m1 + .8 * m2, .72 * m1 + .8 * m2), r.Utilization!.Value, 1e-12);
        var tensionOnly = Run(BeamColumn(Input(rhs, ntc, n: 1e5), false));
        Assert.AreEqual(SteelExecutionStatus.NotRequired, tensionOnly.Status); Assert.AreEqual("Member.NoActiveDemand", tensionOnly.DiagnosticCode);
        // Was MissingInput (BeamColumn.ShearBucklingExclusionSource). The RHS 300x200x10 walls parallel to V2 are webs:
        // hw/t = (300 - 2*10)/10 = 28 <= 72 eps/eta = 48.8 (EC3 6.2.6(6), eta 1.2) or 58.6 (NTC eq. 4.2.27, eta 1).
        var shearedInput = Input(rhs, ntc, m1: 5e7, v2: 1e4);
        var shearedPlan = SteelMethodSelector.SelectMember(BeamColumn(shearedInput, false));
        var sheared = SteelVerificationPipeline.Execute(shearedPlan).Checks.Single();
        Assert.AreEqual(SteelExecutionStatus.RequiresPreparation, sheared.Status); Assert.AreEqual("ShearBuckling.AssessmentRequired", sheared.DiagnosticCode);
        var walls = ntc ? SteelShearBucklingAssessment.EvaluateNtcRhsWebs(shearedInput, 2, "NTC eq. 4.2.27")
            : SteelShearBucklingAssessment.EvaluateEc3RhsWebs(shearedInput, 2, 1.2, "EC3 eta");
        var screened = SteelVerificationPipeline.Execute(shearedPlan, new[] { walls }).Checks.Single();
        // V2 = 10 kN <= .5 Vpl leaves the interaction unchanged: .9 m1 with Wel1 = (200*300^3 - 180*280^3)/12/150 = 804800 mm3.
        Assert.IsTrue(screened.IsEvaluated, screened.Message); Assert.AreEqual(.9 * 5e7 * g / (804800 * 355d), screened.Utilization!.Value, 1e-12);
        // CHS 200x10: was MissingInput (BeamColumn.ShearBucklingExclusionSource). d/t = 20 <= 0.32 (E/fy)^0.67 = 23.035,
        // the wall screen (EvaluateChsWall, EN 1993-1-6 eq. D.40): it now waits for that assessment, as the RHS does.
        // Beyond the screen (CHS 300x8, d/t = 37.5) the explicit source stays required, as before.
        var tube = Run(BeamColumn(Input(CommonSectionCoverageTests.Shape("CHS"), ntc, m1: 2e7, v2: 1e4), false));
        Assert.AreEqual(SteelExecutionStatus.RequiresPreparation, tube.Status); Assert.AreEqual("ShearBuckling.AssessmentRequired", tube.DiagnosticCode);
        var slenderTube = Run(BeamColumn(Input(new SectionCHS(300, 8, "CHS300x8"), ntc, m1: 2e7, v2: 1e4), false));
        Assert.AreEqual(SteelExecutionStatus.MissingInput, slenderTube.Status);
        CollectionAssert.Contains(slenderTube.Decision.MissingInputs.ToList(), "BeamColumn.ShearBucklingExclusionSource");
        // Wide RHS 200x300x10: principal axis 1 is vertical, the geometry shear areas would belong to the other axis
        // (A b/(b+h) for V2): refused with shear even with the explicit source, evaluated without shear.
        var wide = new SectionRHS(200, 300, 10, 10, 10, 10, "wide");
        var wideSheared = Run(BeamColumn(Input(wide, ntc, m1: 5e7, v2: 1e4), false, shear: "declared"));
        Assert.AreEqual(SteelExecutionStatus.NotSupported, wideSheared.Status); Assert.AreEqual("Member.BeamColumnShearAreasNotQualified", wideSheared.DiagnosticCode);
        Assert.IsTrue(Run(BeamColumn(Input(wide, ntc, m1: 5e7), false)).IsEvaluated);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnShearBucklingPrerequisiteTakesTheHAssessments(bool ntc)
    {
        // Was MissingInput (BeamColumn.ShearBucklingExclusionSource). IPE 300 S355, V1 = 10 kN, V2 = 50 kN (< .5 Vpl).
        // Web hw/tw = (300 - 2*10.7)/7.1 = 39.24 <= 72 eps/eta = 48.8 (EC3 6.2.6(6), eta 1.2) or 58.6 (NTC eq. 4.2.27, eta 1);
        // flanges c/t = 5.28 <= 14 eps = 11.39 (GPC criterion, not a code clause).
        var i = Input(Ipe300(), ntc, m1: 6e7, m2: 1e7, v1: 1e4, v2: 5e4);
        var plan = SteelMethodSelector.SelectMember(BeamColumn(i, false));
        Assert.AreEqual(SteelSelectionStatus.RequiresPreparation, plan.Decisions.Single().Status);
        var waiting = SteelVerificationPipeline.Execute(plan).Checks.Single();
        Assert.AreEqual(SteelExecutionStatus.RequiresPreparation, waiting.Status); Assert.AreEqual("ShearBuckling.AssessmentRequired", waiting.DiagnosticCode);
        var web = ntc ? SteelShearBucklingAssessment.EvaluateNtcHWeb(i, "NTC eq. 4.2.27") : SteelShearBucklingAssessment.EvaluateEc3HWeb(i, 1.2, "EC3 eta");
        var flanges = SteelShearBucklingAssessment.EvaluateHFlanges(i, "GPC c/t<=14eps");
        Assert.IsTrue(web.IsBucklingExcluded && flanges.IsBucklingExcluded);
        Assert.AreEqual(278.6 / 7.1, web.Trace.Single(v => v.Name == "WebSlenderness").Value, 1e-12);
        var screened = SteelVerificationPipeline.Execute(plan, new[] { web, flanges }).Checks.Single();
        Assert.IsTrue(screened.IsEvaluated, screened.Message); Assert.AreEqual(2, screened.ShearAssessments.Count);
        // An explicit source wins: Ready without assessments, same interaction.
        var explicitSource = Run(BeamColumn(i, false, shear: "independent screen"));
        Assert.IsTrue(explicitSource.IsEvaluated); Assert.AreEqual(explicitSource.Utilization, screened.Utilization);
        // A negative assessment stops the route; no reduced resistance is invented.
        var negative = SteelVerificationPipeline.Execute(plan, new[] { web, SteelShearBucklingAssessment.FromExternal(i, 1, false, "not excluded") }).Checks.Single();
        Assert.AreEqual(SteelExecutionStatus.NotSupported, negative.Status); Assert.AreEqual("ShearBuckling.ResistanceMethodRequired", negative.DiagnosticCode);
        // Welded H 540, web 500 x 5.2: class 3 in bending (96.2 <= 124 eps = 100.9) and hw/tw = 96.2 > 48.8 / 58.6: the web screen is negative.
        var slender = Input(new SectionH(540, 5.2, 250, 20, 250, 20, "welded", 0), ntc, m1: 1e8, v2: 1e4);
        var slenderPlan = SteelMethodSelector.SelectMember(BeamColumn(slender, false));
        Assert.AreEqual(3, slenderPlan.Decisions.Single().SectionClass);
        var slenderWeb = ntc ? SteelShearBucklingAssessment.EvaluateNtcHWeb(slender, "NTC") : SteelShearBucklingAssessment.EvaluateEc3HWeb(slender, 1.2, "EC3");
        Assert.IsFalse(slenderWeb.IsBucklingExcluded);
        Assert.AreEqual("ShearBuckling.ResistanceMethodRequired", SteelVerificationPipeline.Execute(slenderPlan, new[] { slenderWeb }).Checks.Single().DiagnosticCode);
    }

    [DataTestMethod]
    [DataRow(false, -1e4, 1)] [DataRow(true, -1e4, 1)] [DataRow(false, -5e4, 2)] [DataRow(true, -5e4, 2)]
    public void BeamColumnClassFollowsTheConcomitantAxialForceAndMoment(bool ntc, double n, int expectedClass)
    {
        // UKB406x140x39 S355: d = 398 - 2*8.6 - 2*10.2 = 360.4, d/tw = 56.31, eps = sqrt(235/355) = 0.8136.
        // Uniform compression (the class used before): 56.31 > 42 eps = 34.17 -> class 4, Aeff and Weff.
        // Under N and M1 the web plastic compressed fraction is alpha = 0.5 + |N|/(2 d tw fy), d tw fy = 818.8 kN (EC3 Table 5.2):
        //   N = -10 kN: alpha = 0.5061, class 1 limit 396 eps/(13 alpha - 1) = 57.75 >= 56.31 -> class 1;
        //   N = -50 kN: alpha = 0.5305, class 1 limit 54.64 < 56.31 <= class 2 limit 456 eps/(13 alpha - 1) = 62.92 -> class 2.
        // Flanges c/t = ((141.8 - 6.4)/2 - 10.2)/8.6 = 6.69 <= 9 eps = 7.32: class 1.
        double fy = 355, eps = Math.Sqrt(235 / fy), d = 398 - 2 * 8.6 - 2 * 10.2, alpha = .5 - n / (2 * d * 6.4 * fy);
        double limit1 = 396 * eps / (13 * alpha - 1), limit2 = 456 * eps / (13 * alpha - 1);
        Assert.AreEqual(expectedClass, d / 6.4 <= limit1 ? 1 : d / 6.4 <= limit2 ? 2 : 3);
        var i = Input(Ukb406(), ntc, n: n, m1: 1e8);
        var r = Run(BeamColumn(i, false));
        Assert.IsTrue(r.IsEvaluated, r.Message); Assert.AreEqual(expectedClass, r.Decision.SectionClass);
        StringAssert.Contains(r.Decision.ClassificationSource, "alpha=" + alpha.ToString("0.###", CultureInfo.InvariantCulture));
        StringAssert.Contains(r.Decision.ClassificationSource, "envelope state N,M1,M2");
        Assert.IsFalse(r.Trace.Any(v => v.Name == "AreaEffective" || v.Name == "Weff1Min"));
        // Gross properties with root fillets: A = 2 b tf + (h - 2 tf) tw + (4 - pi) r^2; Wpl,y adds the four spandrels,
        // area (1 - pi/4) r^2 with centroid (10 - 3 pi)/(12 - 3 pi) r = 0.2234 r from the flange face.
        double r0 = 10.2, area = 2 * 141.8 * 8.6 + (398 - 17.2) * 6.4 + (4 - Math.PI) * r0 * r0;
        double spandrel = (1 - Math.PI / 4) * r0 * r0, yc = (10 - 3 * Math.PI) / (12 - 3 * Math.PI) * r0;
        double wpl = 2 * 141.8 * 8.6 * (199 - 4.3) + 6.4 * Math.Pow(199 - 8.6, 2) + 4 * spandrel * (199 - 8.6 - yc);
        Assert.AreEqual(area * fy, Value(r, "NRk"), area * fy * 1e-3); Assert.AreEqual(wpl * fy, Value(r, "M1Rk"), wpl * fy * 2e-3);
        // EC3 Table B.1 / Circolare Table C4.2.IV, classes 1-2, restrained: k11 = Cm1 (1 + (lambda1 - .2) n1), k21 = .6 k11;
        // curves a (lambda1 = .6) and b (lambda2 = 1.2).
        double gamma = ntc ? 1.05 : 1, n1 = -n * gamma / (Chi(.6, .21) * area * fy), n2 = -n * gamma / (Chi(1.2, .34) * area * fy);
        double m1 = 1e8 * gamma / (wpl * fy), k11 = .9 * (1 + .4 * n1), expected = Math.Max(n1 + k11 * m1, n2 + .6 * k11 * m1);
        Assert.AreEqual(k11, Value(r, "K11"), k11 * 1e-3); Assert.AreEqual(expected, r.Utilization!.Value, expected * 2e-3);
        // The uniform-compression route keeps class 4 for the same section and force.
        var compression = SteelMemberAnalysis.Compression(new SteelVerificationInput(i.Section, new SteelActions(n, 0, 0, 0, 0, 0), i.Code, i.Member),
            "constant N", "curves", SteelBucklingCurve.A, SteelBucklingCurve.B, SteelBucklingCurve.B, torsionalExclusionSource: "assessed", flexuralTorsionalExclusionSource: "assessed");
        Assert.AreEqual(4, SteelMethodSelector.SelectMember(compression).Decisions.Single().SectionClass);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnWithCompressionKeepsOnlyAlphaBasedClassGains(bool ntc)
    {
        // UKB406 S355, N = -200 kN: alpha = 0.5 + 200/(2*818.8) = 0.622, class 2 limit 456 eps/(13 alpha - 1) = 52.4 < 56.31.
        // With M1 = 100 kNm the web edges carry about -184 and +104 MPa: psi = -0.56, 42 eps/(0.67 + 0.33 psi) = 70.6 -> class 3.
        // With 20 kNm at another cut psi = +0.17 and the limit 47.1 < 56.31 -> class 4: psi depends on M/N along the member,
        // so the uniform-compression class 4 governs (56.31 > 42 eps = 34.17).
        var r = Run(BeamColumn(Input(Ukb406(), ntc, n: -2e5, m1: 1e8), false));
        Assert.IsTrue(r.IsEvaluated, r.Message); Assert.AreEqual(4, r.Decision.SectionClass);
        StringAssert.Contains(r.Decision.ClassificationSource, "the N-M class 3");
        Assert.IsTrue(r.Trace.Any(v => v.Name == "AreaEffective"));
        // Minor-axis bending with compression: the flange alpha depends on M2, the web carries N alone -> uniform class 4.
        Assert.AreEqual(4, Run(BeamColumn(Input(Ukb406(), ntc, n: -1e4, m2: 1e6), false)).Decision.SectionClass);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnClassTakesTheWorstEnvelopeStateAndASuppliedClassWins(bool ntc)
    {
        // UKB406 with N = -10 kN, M1 = 100 kNm, M2 = 1 kNm: the two maxima may come from different cuts. (N,M1,M2) and (N,M1,0)
        // are class 1, but at the M2 cut the web may carry N alone: (N,0,M2) gives alpha = 1, psi = 1 and 56.31 > 42 eps -> class 4.
        var r = Run(BeamColumn(Input(Ukb406(), ntc, n: -1e4, m1: 1e8, m2: 1e6), false));
        Assert.IsTrue(r.IsEvaluated, r.Message); Assert.AreEqual(4, r.Decision.SectionClass);
        StringAssert.Contains(r.Decision.ClassificationSource, "envelope state N,0,M2");
        Assert.IsTrue(r.Trace.Any(v => v.Name == "AreaEffective") && r.Trace.Any(v => v.Name == "Weff1Min"));
        // Without compression no envelope state compresses the web uniformly: class 1 (bending 56.31 <= 72 eps = 58.58).
        Assert.AreEqual(1, Run(BeamColumn(Input(Ukb406(), ntc, m1: 1e8, m2: 1e6), false)).Decision.SectionClass);
        var supplied = Run(BeamColumn(Input(Ukb406(), ntc, n: -1e4, m1: 1e8), false, memberClass: 3));
        Assert.AreEqual(3, supplied.Decision.SectionClass); Assert.AreEqual("independent class", supplied.Decision.ClassificationSource);
    }
}
