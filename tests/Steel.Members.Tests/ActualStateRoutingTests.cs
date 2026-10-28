using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Negligible actions, classification under the actual N-M state, closed-form
/// plastic I/H interaction and V1 flange shear buckling. References are computed here
/// from the code expressions and published IPE 300 properties, not read from the library.</summary>
[TestClass]
public class ActualStateRoutingTests
{
    // IPE 300: h 300, b 150, tw 7.1, tf 10.7, r 15. Published A = 53.81 cm2, Wpl,y = 628.4 cm3, Wpl,z = 125.2 cm3.
    private static SectionH Ipe300() => new(300, 7.1, 150, 10.7, 150, 10.7, "IPE300", 15);

    private static SteelVerificationInput Input(Section shape, double fy, double n = 0, double m1 = 0, double m2 = 0,
        double v1 = 0, double v2 = 0, double t = 0, bool ntc = false, SteelNegligibleActionPolicy? policy = null)
    {
        var code = ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "test"), null);
        if (policy != null) code = code.WithNegligibleActions(policy);
        return new(new SteelSectionInput(new SteelSection(shape, new SteelMaterial("S", 210000, fy, fy + 150)), true),
            new SteelActions(n, v1, v2, t, m1, m2), code);
    }

    private static SteelMethodDecision Decision(SteelSelectionPlan plan, SteelCheckTarget target)
        => plan.Decisions.Single(d => d.Target == target);

    [TestMethod]
    public void TorsionNoiseIsNeglectedAndTracedButRealTorsionStillBlocks()
    {
        var noisy = Input(Ipe300(), 355, m1: 1e8, t: 1e-6);
        var plan = SteelMethodSelector.Select(noisy);
        Assert.AreEqual(SteelSelectionStatus.NotRequired, Decision(plan, SteelCheckTarget.Torsion).Status);
        Assert.AreEqual("Actions.Negligible", Decision(plan, SteelCheckTarget.Torsion).DiagnosticCode);
        Assert.AreEqual(SteelMethod.SectionBending1, Decision(plan, SteelCheckTarget.NormalResistance).Method);
        var neglected = noisy.NeglectedActions.Single();
        Assert.AreEqual("T", neglected.Component); Assert.AreEqual(1e-6, neglected.Value);
        // Thin-walled lower bound fy/(sqrt3 gammaM0) A tmin/3 with tmin = tw = 7.1 mm.
        double reference = 355 / Math.Sqrt(3) * noisy.Section.Area * 7.1 / 3;
        Assert.AreEqual(reference, neglected.ReferenceResistance!.Value, reference * 1e-12);
        Assert.AreEqual(1e-6 * reference, neglected.Limit, reference * 1e-18);
        Assert.AreEqual(0, noisy.EffectiveActions.T); Assert.AreEqual(1e-6, noisy.Actions.T);
        var result = SteelVerificationPipeline.Execute(plan);
        var bending = result.Checks.Single(c => c.Decision.Target == SteelCheckTarget.NormalResistance);
        Assert.IsTrue(bending.IsEvaluated);
        Assert.AreEqual(1e-6, bending.Trace.Single(v => v.Name == "Neglected.T").Value);

        var real = SteelMethodSelector.Select(Input(Ipe300(), 355, m1: 1e8, t: 1e5));
        Assert.AreEqual(SteelSelectionStatus.NotSupported, Decision(real, SteelCheckTarget.NormalResistance).Status);
        var exact = SteelMethodSelector.Select(Input(Ipe300(), 355, m1: 1e8, t: 1e-6, policy: SteelNegligibleActionPolicy.ExactZero));
        Assert.AreEqual(SteelSelectionStatus.NotSupported, Decision(exact, SteelCheckTarget.Torsion).Status);
    }

    [TestMethod]
    public void AbsoluteLimitsApplyToForcesAndMomentsSeparately()
    {
        var policy = new SteelNegligibleActionPolicy(0, absoluteForce: 1, absoluteMoment: 10);
        var input = Input(Ipe300(), 355, m1: 1e8, v1: .9, v2: 2, m2: 9, policy: policy);
        CollectionAssert.AreEquivalent(new[] { "V1", "M2" }, input.NeglectedActions.Select(n => n.Component).ToArray());
        Assert.IsTrue(input.NeglectedActions.All(n => n.ReferenceResistance == null));
        Assert.AreEqual(1, input.NeglectedActions.Single(n => n.Component == "V1").Limit);
        Assert.AreEqual(10, input.NeglectedActions.Single(n => n.Component == "M2").Limit);
        Assert.AreEqual(2, input.EffectiveActions.V2);
        var plan = SteelMethodSelector.Select(input);
        Assert.AreEqual(SteelSelectionStatus.NotRequired, Decision(plan, SteelCheckTarget.Shear1).Status);
        Assert.AreNotEqual(SteelSelectionStatus.NotRequired, Decision(plan, SteelCheckTarget.Shear2).Status);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelNegligibleActionPolicy(1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelNegligibleActionPolicy(absoluteForce: -1));
    }

    [TestMethod]
    public void RelativeLimitUsesTheCorrespondingResistance()
    {
        var input = Input(Ipe300(), 355, n: -1e-3, m1: 1e8);
        var n = input.NeglectedActions.Single();
        Assert.AreEqual("N", n.Component);
        Assert.AreEqual(input.Section.Area * 355, n.ReferenceResistance!.Value, 1e-6);
        Assert.AreEqual(0, input.EffectiveActions.N);
        Assert.AreEqual(0, Input(Ipe300(), 355, n: -10, m1: 1e8).NeglectedActions.Count);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void RolledIpeWebIsClass1InBendingAndClass4InCompression(bool ntc)
    {
        // d = 300 - 2*10.7 - 2*15 = 248.6, d/tw = 35.01; eps = sqrt(235/355) = 0.8136.
        // Uniform: 42 eps = 34.17 -> class 4. Pure bending (alpha 0.5): 72 eps = 58.58 -> class 1.
        // Flange c/tf = ((150 - 7.1)/2 - 15)/10.7 = 5.28 <= 9 eps = 7.32.
        var bending = SteelMethodSelector.Select(Input(Ipe300(), 355, m1: 1e8, ntc: ntc));
        var normal = Decision(bending, SteelCheckTarget.NormalResistance);
        Assert.AreEqual(1, normal.SectionClass); Assert.AreEqual(SteelMethod.SectionBending1, normal.Method);
        StringAssert.Contains(normal.ClassificationSource, "Web c/t=35.014");
        var result = SteelVerificationPipeline.Execute(bending).Checks.Single(c => c.Decision.Target == SteelCheckTarget.NormalResistance);
        double gamma = ntc ? 1.05 : 1;
        Assert.AreEqual(628.4e3 * 355 / gamma, result.Resistance!.Value, 628.4e3 * 355 / gamma * 2e-3);
        Assert.AreEqual(4, Decision(SteelMethodSelector.Select(Input(Ipe300(), 355, n: -1e5, ntc: ntc)), SteelCheckTarget.NormalResistance).SectionClass);
        // N = -200 kN: alpha = 0.5 + 2e5/(2*248.6*7.1*355) = 0.6596; class 1 limit 396 eps/(13 alpha - 1) = 42.53.
        var combined = Decision(SteelMethodSelector.Select(Input(Ipe300(), 355, n: -2e5, m1: 1e8, ntc: ntc)), SteelCheckTarget.NormalResistance);
        Assert.AreEqual(1, combined.SectionClass);
        StringAssert.Contains(combined.ClassificationSource, "alpha=0.66");
    }

    [TestMethod]
    public void SlenderWebUsesTheElasticStressRatioForClass3()
    {
        // Welded H 1250: web 1200 x 12 (d/tw = 100), flanges 300 x 25, S235: class 4 in compression;
        // pure bending psi = -1: class 2 limit 83 < 100 <= class 3 limit 124 -> class 3.
        var h = new SectionH(1250, 12, 300, 25, 300, 25, "welded", 0);
        var bending = Decision(SteelMethodSelector.Select(Input(h, 235, m1: 1e8)), SteelCheckTarget.NormalResistance);
        Assert.AreEqual(3, bending.SectionClass); Assert.AreEqual(SteelMethod.SectionBending1, bending.Method);
        StringAssert.Contains(bending.ClassificationSource, "psi=-1");
        // N/A of 40 MPa with M: web psi rises above -1 and the class 3 limit falls below 100.
        double n = -40.0 * h.Area, m = 60.0 * h.J11 / (1200.0 / 2);
        var compressed = Decision(SteelMethodSelector.Select(Input(h, 235, n: n, m1: m)), SteelCheckTarget.NormalResistance);
        double psi = (40 - 60.0) / (40 + 60.0), limit = 42 / (.67 + .33 * psi);
        Assert.IsTrue(limit < 100); Assert.AreEqual(4, compressed.SectionClass);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void RolledIpeTakesTheClosedFormPlasticInteraction(bool ntc)
    {
        double fy = 355, gamma = ntc ? 1.05 : 1, fd = fy / gamma, area = 5381.2, wply = 628.4e3, wplz = 125.2e3;
        double nEd = -4e5, my = 1.2e8, mz = 1.5e7;
        var input = Input(Ipe300(), fy, n: nEd, m1: my, m2: mz, ntc: ntc);
        Assert.AreEqual(area, input.Section.Area, 1);
        var plan = SteelMethodSelector.Select(input);
        Assert.AreEqual(SteelMethod.IHPlasticInteractionFormulas, Decision(plan, SteelCheckTarget.NormalResistance).Method);
        var check = SteelVerificationPipeline.Execute(plan).Checks.Single(c => c.Decision.Target == SteelCheckTarget.NormalResistance);
        // EN 1993-1-1 (6.36)-(6.38), (6.41).
        double n = 4e5 / (area * fd), a = Math.Min((area - 2 * 150 * 10.7) / area, .5);
        double mny = Math.Min(wply * fd, wply * fd * (1 - n) / (1 - .5 * a));
        double mnz = n <= a ? wplz * fd : wplz * fd * (1 - Math.Pow((n - a) / (1 - a), 2));
        double beta = Math.Max(5 * n, 1), q1 = my / mny, q2 = mz / mnz;
        double lo = 0, hi = 10;
        for (int i = 0; i < 200; i++) { double u = (lo + hi) / 2; if (Math.Pow(q1 / u, 2) + Math.Pow(q2 / u, beta) > 1) lo = u; else hi = u; }
        Assert.AreEqual(hi, check.Utilization!.Value, hi * 3e-3);
        Assert.AreEqual(beta, check.Trace.Single(v => v.Name == "Beta").Value, beta * 1e-3);
        Assert.AreEqual(a, check.Trace.Single(v => v.Name == "a").Value, 1e-3);
    }

    [TestMethod]
    public void FlangeOutstandsWithinClass3ExcludeShearBucklingUnderV1()
    {
        var ipe = Input(Ipe300(), 355, v1: 1e5);
        var assessment = SteelShearBucklingAssessment.EvaluateHFlanges(ipe, "test");
        Assert.IsTrue(assessment.IsBucklingExcluded); Assert.AreEqual(1, assessment.Axis);
        Assert.AreEqual(((150 - 7.1) / 2 - 15) / 10.7, assessment.Trace.Single(v => v.Name == "FlangeOutstandSlenderness").Value, 1e-12);
        var result = SteelVerificationPipeline.Execute(SteelMethodSelector.Select(ipe), new[] { assessment });
        Assert.IsTrue(result.Checks.Single(c => c.Decision.Target == SteelCheckTarget.Shear1).IsEvaluated);
        // Welded flange 400 x 12, S235: c/t = (400 - 10)/2/12 = 16.25 > 14.
        var slender = SteelShearBucklingAssessment.EvaluateHFlanges(Input(new SectionH(500, 10, 400, 12, 400, 12, "w", 0), 235, v1: 1e5), "test");
        Assert.IsFalse(slender.IsBucklingExcluded);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void LateralTorsionalBucklingUsesTheBendingClassOfTheSection(bool ntc)
    {
        // IPE 300 S355: web class 4 in uniform compression, class 1 in bending, so EC3 6.3.2.1(3) and
        // NTC 4.2.4.1.3.2 take Wy = Wpl,y. Curve a (EC3 Table 6.4) / b (NTC Table 4.2.IX), h/b = 2;
        // general case lambdaLT0 = 0.2, beta = 1. Before, the uniform class 4 gave Weff = Wel.
        var code = ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "test"), 1.0);
        var section = new SteelSectionInput(new SteelSection(Ipe300(), new SteelMaterial("S", 210000, 355, 510)), true);
        var input = new SteelVerificationInput(section, new SteelActions(0, 0, 0, 0, 1e8, 0), code, new SteelMemberInput(4000, 4000, 4000));
        double mcr = 2e8;
        var analysis = SteelMemberAnalysis.Bending(input, "uniform moment", "profile", SteelCriticalMomentBasis.UniformMoment,
            new SteelStabilityValue(SteelStabilityQuantity.Mcr, mcr, SteelStabilityUnit.Nmm, "independent"));
        var check = SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(analysis)).Checks.Single();
        Assert.IsTrue(check.IsEvaluated, check.Message);
        Assert.AreEqual(1, check.Decision.SectionClass); StringAssert.Contains(check.Decision.ClassificationSource, "alpha");
        double wpl = check.Trace.Single(v => v.Name == "Wpl1").Value;
        Assert.AreEqual(628.4e3, wpl, 628.4e3 * 3e-3);
        double gamma0 = ntc ? 1.05 : 1, gamma1 = ntc ? 1.05 : 1, alpha = ntc ? .34 : .21;
        double lambda = Math.Sqrt(wpl * 355 / mcr), phi = .5 * (1 + alpha * (lambda - .2) + lambda * lambda);
        double chi = Math.Min(1, 1 / (phi + Math.Sqrt(phi * phi - lambda * lambda)));
        Assert.AreEqual(Math.Min(chi * wpl * 355 / gamma1, wpl * 355 / gamma0), check.Resistance!.Value, 1e-6);
        var compression = SteelMemberAnalysis.Compression(new SteelVerificationInput(section, new SteelActions(-1e5, 0, 0, 0, 0, 0), code, input.Member),
            "constant N", "curves", SteelBucklingCurve.A, SteelBucklingCurve.B, SteelBucklingCurve.B,
            torsionalExclusionSource: "assessed", flexuralTorsionalExclusionSource: "assessed");
        Assert.AreEqual(4, SteelMethodSelector.SelectMember(compression).Decisions.Single().SectionClass);
    }
}
