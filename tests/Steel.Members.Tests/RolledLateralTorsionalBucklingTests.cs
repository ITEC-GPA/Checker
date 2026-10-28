using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>EN 1993-1-1:2005 6.3.2.3 and NTC 2018 4.2.4.1.3.2 for rolled or equivalent welded I/H, selected explicitly.
/// Expected values are hand calculations (40-digit decimal check) written in the comments, not library outputs.
/// beta = 0.75 and lambdaLT0 = 0.4 are the recommended EC3 values and the NTC values for rolled/welded sections.</summary>
[TestClass]
public class RolledLateralTorsionalBucklingTests
{
    private static SectionH Ipe300() => new(300, 7.1, 150, 10.7, 150, 10.7, "IPE300", 15);
    private static SteelVerificationInput Input(Section shape, bool ntc, Section.SectionTypes type = Section.SectionTypes.Rolled, double n = 0, double m1 = 1e8)
    {
        var code = ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "EC3 recommended"), 1.0);
        var section = new SteelSectionInput(new SteelSection(shape, new SteelMaterial("S355", 210000, 355, 510), type), true);
        return new(section, new SteelActions(n, 0, 0, 0, m1, 0), code, new SteelMemberInput(6000, 6000, 6000));
    }
    private static SteelLtbMomentFactor Uniform => SteelLtbMomentFactor.Tabulated(SteelLtbMomentDistribution.Uniform, "uniform moment between the fork supports");
    private static SteelLateralTorsionalMethod Rolled(SteelLtbMomentFactor? kc = null)
        => SteelLateralTorsionalMethod.RolledOrEquivalentWelded(.75, .4, kc ?? Uniform, "EC3 6.3.2.3 recommended / NTC 4.2.4.1.3.2 rolled values");
    // Mcr chosen so that lambdaLT = sqrt(Wpl fy / Mcr) is the requested value (class 1 throughout).
    private static SteelMemberAnalysis Bending(SteelVerificationInput i, double lambda, int? cl = null, SteelCriticalMomentBasis basis = SteelCriticalMomentBasis.UniformMoment)
        => SteelMemberAnalysis.Bending(i, "uniform major-axis moment", "test values", basis,
            new SteelStabilityValue(SteelStabilityQuantity.Mcr, i.Section.ReadOnlySection.Wpl1 * 355 / (lambda * lambda), SteelStabilityUnit.Nmm, "chosen for lambdaLT"),
            cl, cl == null ? null : "independent class 1: flange c/t and web c/t within the class 1 limits");
    private static SteelCheckExecution Run(SteelMemberAnalysis a) => SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
    private static double T(SteelCheckExecution r, string name) => r.Trace.Single(t => t.Name == name).Value;

    // IPE 300 S355 rolled, h/b = 300/150 = 2 <= 2: curve b, alphaLT = 0.34 (EC3 Table 6.5 = NTC Tab. 4.2.IX); kc = 1, f = 1.
    // lambda 0.5: Phi = 0.5[1 + 0.34(0.1) + 0.75(0.25)] = 0.61075; sqrt(0.61075^2 - 0.1875) = 0.43071518; chi = 1/1.04146518 = 0.96018573
    // lambda 1.0: Phi = 0.5[1 + 0.34(0.6) + 0.75] = 0.977; sqrt(0.954529 - 0.75) = 0.45224; chi = 1/1.42924 = 0.69966823
    // lambda 1.5: Phi = 0.5[1 + 0.34(1.1) + 1.6875] = 1.53075; sqrt(2.34320 - 1.6875) = 0.80975; chi = 1/2.34050 = 0.42725908
    // NTC: same chi (f = 1), gammaM0 = gammaM1 = 1.05.
    [DataTestMethod]
    [DataRow(.5, .96018573008466287, false)] [DataRow(1d, .69966823291156054, false)] [DataRow(1.5, .42725907630387809, false)]
    [DataRow(.5, .96018573008466287, true)] [DataRow(1d, .69966823291156054, true)] [DataRow(1.5, .42725907630387809, true)]
    public void Ipe300RolledCurveBAtThreeSlendernesses(double lambda, double chi, bool ntc)
    {
        var r = Run(Bending(Input(Ipe300(), ntc), lambda).WithLateralTorsionalMethod(Rolled()));
        Assert.IsTrue(r.IsEvaluated, r.Message);
        Assert.AreEqual(.34, T(r, "AlphaLT")); Assert.AreEqual(lambda, T(r, "SlendernessLT"), 1e-14);
        Assert.AreEqual(chi, T(r, "ChiLT"), 1e-14); Assert.AreEqual(chi, T(r, "ChiLTBase"), 1e-14);
        Assert.AreEqual(.75, T(r, "BetaLT")); Assert.AreEqual(.4, T(r, "LambdaLT0"));
        Assert.AreEqual(1, T(r, "MomentCorrectionF")); Assert.AreEqual(1, T(r, "Kc")); Assert.AreEqual(1, T(r, "LtbRolledOrEquivalentWelded"));
        double w = T(r, "Wpl1"), gamma = ntc ? 1.05 : 1;
        Assert.AreEqual(Math.Min(chi * w * 355 / gamma, w * 355 / gamma), r.Resistance!.Value, w * 355 * 1e-14);
        StringAssert.Contains(r.Reference, ntc ? "NTC 2018 4.2.4.1.3.2" : "EN 1993-1-1:2005 6.3.2.3");
    }

    [TestMethod]
    public void KcFollowsTheCorrectionFactorTable()
    {
        // EN 1993-1-1:2005 Table 6.6 / NTC kc table: psi = 1 -> 1.0; linear 1/(1.33 - 0.33 psi).
        Assert.AreEqual(1, Uniform.Kc);
        Assert.AreEqual(1, SteelLtbMomentFactor.Linear(1, "uniform").Kc);
        Assert.AreEqual(.75187969924812030, SteelLtbMomentFactor.Linear(0, "one end moment").Kc, 1e-15);   // 1/1.33
        Assert.AreEqual(.85836909871244635, SteelLtbMomentFactor.Linear(.5, "linear").Kc, 1e-15);          // 1/1.165
        Assert.AreEqual(.60240963855421687, SteelLtbMomentFactor.Linear(-1, "double curvature").Kc, 1e-15); // 1/1.66
        foreach (var (row, kc) in new[] { (SteelLtbMomentDistribution.SimplySupportedUniformLoad, .94), (SteelLtbMomentDistribution.FixedEndsUniformLoad, .90),
            (SteelLtbMomentDistribution.OneEndFixedUniformLoad, .91), (SteelLtbMomentDistribution.SimplySupportedCentralLoad, .86),
            (SteelLtbMomentDistribution.FixedEndsCentralLoad, .77), (SteelLtbMomentDistribution.OneEndFixedCentralLoad, .82) })
            Assert.AreEqual(kc, SteelLtbMomentFactor.Tabulated(row, "declared diagram").Kc);
        var unknown = SteelLtbMomentFactor.NotEstablished("diagram not established");
        Assert.AreEqual(1, unknown.Kc); Assert.IsNull(unknown.Psi);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelLtbMomentFactor.Linear(1.01, "x"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelLtbMomentFactor.Linear(double.NaN, "x"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelLtbMomentFactor.Tabulated(SteelLtbMomentDistribution.Linear, "x"));
        Assert.ThrowsException<ArgumentException>(() => SteelLtbMomentFactor.Linear(0, " "));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void MomentCorrectionAndBoundsOfTheModifiedReduction(bool ntc)
    {
        static RolledLateralTorsionalBuckling.Result R(double lambda, double kc, bool ntc, double beta = .75) => RolledLateralTorsionalBuckling.Reduce(lambda, .34, beta, .4, kc, ntc);
        // psi = 0, kc = 1/1.33, lambda 1.0: f = 1 - 0.5(1 - 0.7518797)(1 - 2(0.2)^2) = 0.88586466; chi = 0.69966823/0.88586466 = 0.78981391
        var r = R(1, 1 / 1.33, ntc);
        Assert.AreEqual(.88586466165413534, r.F, 1e-15); Assert.AreEqual(.78981391085755858, r.Chi, 1e-14);
        // f clipping: kc = 0.86, lambda 1.6: 1 - 0.5(0.14)(1 - 2(0.8)^2) = 1.0196 > 1 -> f = 1; chi = Base = 0.38679400
        r = R(1.6, .86, ntc);
        Assert.AreEqual(1, r.F); Assert.AreEqual(.38679399837040104, r.Chi, 1e-14);
        // chi,mod <= 1: psi = -1, kc = 1/1.66, lambda 0.5: f = 0.83698795, chi/f = 0.96018573/0.83698795 = 1.147 -> 1
        r = R(.5, 1 / 1.66, ntc);
        Assert.AreEqual(.83698795180722892, r.F, 1e-15); Assert.AreEqual(1, r.Chi);
        // 1/lambda^2 cap: lambda 2.0, Phi = 0.5[1 + 0.34(1.6) + 3] = 2.272, Base = 0.26721045 > 1/4 -> chi = 0.25 (f = 1 at lambda 2)
        r = R(2, 1 / 1.66, ntc);
        Assert.AreEqual(.26721044624842639, r.Base, 1e-14); Assert.AreEqual(1, r.F); Assert.AreEqual(.25, r.Chi);
        // Plateau: lambda 0.3 <= lambdaLT0 = 0.4 -> chi = 1 (EC3 6.3.2.2(4)); the formula would exceed 1 as well.
        Assert.AreEqual(1, R(.3, 1 / 1.33, ntc).Chi);
        // The two codes bound differently only when Base/f > 1/lambda^2, which beta >= 0.75 does not reach;
        // arithmetic check with beta = 0.5, lambda 1.3, kc = 1/1.66: Base = 0.61206956 > 1/1.69 = 0.59171598, f = 0.90060241.
        // EC3: min(min(Base, 1, cap)/f, 1, cap) = 0.59171598; NTC: min(Base/f, 1, cap/f) = 0.59171598/0.90060241 = 0.65702242.
        Assert.AreEqual(ntc ? .65702242187963824 : .59171597633136095, R(1.3, 1 / 1.66, ntc, .5).Chi, 1e-14);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void LinearDiagramFactorRunsThroughThePrismaticRoute(bool ntc)
    {
        var r = Run(Bending(Input(Ipe300(), ntc), 1).WithLateralTorsionalMethod(Rolled(SteelLtbMomentFactor.Linear(0, "M1 from Mmax to zero between the restraints"))));
        Assert.IsTrue(r.IsEvaluated, r.Message);
        // Hand values of MomentCorrectionAndBoundsOfTheModifiedReduction: f = 0.88586466, chi = 0.78981391.
        Assert.AreEqual(.78981391085755858, T(r, "ChiLT"), 1e-14); Assert.AreEqual(.69966823291156054, T(r, "ChiLTBase"), 1e-14);
        Assert.AreEqual(.88586466165413534, T(r, "MomentCorrectionF"), 1e-15);
        Assert.AreEqual(1 / 1.33, T(r, "Kc"), 1e-15); Assert.AreEqual(0, T(r, "KcPsi")); Assert.AreEqual(0, T(r, "KcNotEstablished"));
        Assert.AreEqual(.977, T(r, "PhiLT"), 1e-15);
    }

    // Welded 400x200x10x15 (h/b = 2): curve c, alphaLT 0.49. Welded 500x200x10x16 (h/b = 2.5): curve d, 0.76.
    // Rolled IPE 450 (h/b = 450/190 = 2.37): curve c. lambda 1.0, kc = 1:
    // c: Phi = 0.5[1 + 0.49(0.6) + 0.75] = 1.022, chi = 1/(1.022 + sqrt(1.044484 - 0.75)) = 0.63911493
    // d: Phi = 0.5[1 + 0.76(0.6) + 0.75] = 1.103, chi = 1/(1.103 + sqrt(1.216609 - 0.75)) = 0.55988288
    [DataTestMethod]
    [DataRow(400d, 10d, 200d, 15d, 0d, true, .49, .63911493312502756, false)] [DataRow(400d, 10d, 200d, 15d, 0d, true, .49, .63911493312502756, true)]
    [DataRow(500d, 10d, 200d, 16d, 0d, true, .76, .55988287722423752, false)] [DataRow(500d, 10d, 200d, 16d, 0d, true, .76, .55988287722423752, true)]
    [DataRow(450d, 9.4, 190d, 14.6, 21d, false, .49, .63911493312502756, false)] [DataRow(450d, 9.4, 190d, 14.6, 21d, false, .49, .63911493312502756, true)]
    public void WeldedAndDeepRolledSectionsUseCurvesCAndD(double h, double tw, double b, double tf, double r, bool welded, double alpha, double chi, bool ntc)
    {
        var i = Input(new SectionH(h, tw, b, tf, b, tf, "H", r), ntc, welded ? Section.SectionTypes.Welded : Section.SectionTypes.Rolled);
        var c = Run(Bending(i, 1, cl: 1).WithLateralTorsionalMethod(Rolled()));
        Assert.IsTrue(c.IsEvaluated, c.Message);
        Assert.AreEqual(alpha, T(c, "AlphaLT")); Assert.AreEqual(chi, T(c, "ChiLT"), 1e-14);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void DefaultAndExplicitGeneralMethodKeepTheHistoricalResult(bool ntc)
    {
        var a = Bending(Input(Ipe300(), ntc), 1);
        Assert.AreSame(SteelLateralTorsionalMethod.General, a.LateralTorsionalMethod);
        var d = Run(a); var g = Run(a.WithLateralTorsionalMethod(SteelLateralTorsionalMethod.General));
        // General: EC3 Table 6.4 rolled h/b <= 2 curve a (0.21); NTC Tab. 4.2.IX curve b (0.34); beta = 1, lambdaLT0 = 0.2, f = 1.
        // lambda 1.0: a: Phi = 0.5[1 + 0.21(0.8) + 1] = 1.084, chi = 0.66560306; b: Phi = 1.136, chi = 0.59702319.
        Assert.AreEqual(ntc ? .59702319159355289 : .66560305928460710, T(d, "ChiLT"), 1e-14);
        Assert.AreEqual(1, T(d, "BetaLT")); Assert.AreEqual(.2, T(d, "LambdaLT0")); Assert.AreEqual(1, T(d, "MomentCorrectionF"));
        Assert.IsFalse(d.Trace.Any(t => t.Name == "Kc" || t.Name == "LtbRolledOrEquivalentWelded"));
        CollectionAssert.AreEqual(d.Trace.Select(t => (t.Name, t.Value, t.Unit)).ToArray(), g.Trace.Select(t => (t.Name, t.Value, t.Unit)).ToArray());
        Assert.AreEqual(d.Resistance, g.Resistance); Assert.AreEqual(d.Reference, g.Reference);
    }

    [DataTestMethod] [DataRow("C")] [DataRow("T")] [DataRow("RHS")] [DataRow("CHS")]
    public void OtherFamiliesAreRefusedInsteadOfReceivingTheRolledCurves(string family)
    {
        var b = CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape(family), false, m1: 1e6);
        var i = new SteelVerificationInput(b.Section, b.Actions, b.Code, new SteelMemberInput(3000));
        var a = SteelMemberAnalysis.Bending(i, "M1", "test", SteelCriticalMomentBasis.UniformMoment, new(SteelStabilityQuantity.Mcr, 1e8, SteelStabilityUnit.Nmm, "source"),
            3, "independent resistance-only class");
        Assert.IsTrue(Run(a).IsEvaluated); // the general method still applies to this family
        Assert.AreEqual("Member.LtbMethodGeometry", Run(a.WithLateralTorsionalMethod(Rolled())).DiagnosticCode);
    }

    [TestMethod]
    public void InconsistentDeclarationsAreRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelLateralTorsionalMethod.RolledOrEquivalentWelded(.74, .4, Uniform, "national"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelLateralTorsionalMethod.RolledOrEquivalentWelded(.75, .41, Uniform, "national"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelLateralTorsionalMethod.RolledOrEquivalentWelded(.75, .19, Uniform, "national"));
        Assert.ThrowsException<ArgumentNullException>(() => SteelLateralTorsionalMethod.RolledOrEquivalentWelded(.75, .4, null!, "national"));
        Assert.ThrowsException<ArgumentException>(() => SteelLateralTorsionalMethod.RolledOrEquivalentWelded(.75, .4, Uniform, ""));
        var compression = SteelMemberAnalysis.Compression(Input(Ipe300(), false, n: -1e5, m1: 0), "N", "curves", SteelBucklingCurve.A, SteelBucklingCurve.B, SteelBucklingCurve.B,
            torsionalExclusionSource: "assessed", flexuralTorsionalExclusionSource: "assessed");
        Assert.ThrowsException<ArgumentException>(() => compression.WithLateralTorsionalMethod(Rolled()));
        // NTC keeps the uniform-moment Mcr requirement with the rolled method too.
        var actual = Bending(Input(Ipe300(), true), 1, basis: SteelCriticalMomentBasis.ActualMomentDiagram).WithLateralTorsionalMethod(Rolled());
        Assert.AreEqual("Member.NtcUniformMcrRequired", Run(actual).DiagnosticCode);
    }

    // HEB 300 S355 rolled (h/b = 1: curve b), class 1 in uniform compression; lambda1 = 0.5, lambda2 = 1.0, lambdaLT = 1.0.
    // Rolled method, psi = 0: chiLT = 0.78981391 (hand values above); general: EC3 curve a 0.66560306, NTC curve b 0.59702319.
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnChiLTUsesTheSelectedMethod(bool ntc)
    {
        var i = Input(new SectionH(300, 11, 300, 19, 300, 19, "HEB300", 27), ntc, n: -2e5, m1: 5e7);
        var s = i.Section.ReadOnlySection; double npl = s.Area * 355, m1rk = s.Wpl1 * 355;
        var p = new SteelBeamColumnParameters(SteelBucklingCurve.B, SteelBucklingCurve.C, true, 1, 1, 1, "Cm = 1 declared", "non-sway prismatic member",
            "torsional mode excluded by twist restraints", "coupled mode excluded by twist restraints");
        var values = new[] { new SteelStabilityValue(SteelStabilityQuantity.Ncr1, npl / .25, SteelStabilityUnit.N, "lambda1 = 0.5"),
            new SteelStabilityValue(SteelStabilityQuantity.Ncr2, npl, SteelStabilityUnit.N, "lambda2 = 1.0"),
            new SteelStabilityValue(SteelStabilityQuantity.Mcr, m1rk, SteelStabilityUnit.Nmm, "uniform-moment Mcr, lambdaLT = 1.0") };
        var a = SteelMemberAnalysis.BeamColumn(i, "one whole-member state", "test values", p, values, 1, "independent class 1");
        var general = Run(a); var rolled = Run(a.WithLateralTorsionalMethod(Rolled(SteelLtbMomentFactor.Linear(0, "M1 linear to zero"))));
        Assert.IsTrue(general.IsEvaluated, general.Message); Assert.IsTrue(rolled.IsEvaluated, rolled.Message);
        Assert.AreEqual(ntc ? .59702319159355289 : .66560305928460710, T(general, "ChiLT"), 1e-14);
        Assert.IsFalse(general.Trace.Any(t => t.Name == "BetaLT" || t.Name == "Kc"));
        Assert.AreEqual(.78981391085755858, T(rolled, "ChiLT"), 1e-14);
        Assert.AreEqual(.34, T(rolled, "AlphaLT")); Assert.AreEqual(.75, T(rolled, "BetaLT")); Assert.AreEqual(.4, T(rolled, "LambdaLT0"));
        Assert.AreEqual(.88586466165413534, T(rolled, "MomentCorrectionF"), 1e-15);
        Assert.IsTrue(rolled.Utilization < general.Utilization);
        StringAssert.Contains(rolled.Reference, ntc ? "NTC 2018 4.2.4.1.3.2" : "6.3.2.3");
        // Hollow beam-columns have no LTB reduction to modify: the explicit rolled method is refused, not ignored.
        var rhs = Input(new SectionRHS(300, 200, 10, 10, 10, 10, "RHS"), ntc, n: -2e5, m1: 5e7);
        var hp = new SteelBeamColumnParameters(SteelBucklingCurve.A, SteelBucklingCurve.A, false, 1, 1, 1, "Cm = 1", "non-sway", "closed section", "closed section",
            lateralTorsionalExclusionSource: "closed hollow section");
        var hv = new[] { new SteelStabilityValue(SteelStabilityQuantity.Ncr1, 1e8, SteelStabilityUnit.N, "x"), new SteelStabilityValue(SteelStabilityQuantity.Ncr2, 1e8, SteelStabilityUnit.N, "x") };
        Assert.AreEqual("Member.LtbMethodGeometry", Run(SteelMemberAnalysis.BeamColumn(rhs, "state", "test", hp, hv, 1, "class 1").WithLateralTorsionalMethod(Rolled())).DiagnosticCode);
    }
}
