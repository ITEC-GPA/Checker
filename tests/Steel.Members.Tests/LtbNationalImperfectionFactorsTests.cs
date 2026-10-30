using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>National alphaLT of the LTB curves (EN 1993-1-1:2005 6.3.2.2(2) NOTE: "the imperfection factor alphaLT ... may be
/// obtained from the National Annex"; 6.3.2.2(2) is in the NDP list of the foreword; Table 6.3 gives recommended values).
/// EC3 only, opt-in, refused under NTC 2018 (Tab. 4.2.IX(a)). The values a/b/c/d = 0.25/0.40/0.55/0.80 are a hypothetical
/// national table for the test, not taken from any annex. IPE 300 S355 rolled, h/b = 2: Table 6.4 curve a, Table 6.5 curve b.
/// Expected values are hand substitutions written in the comments.</summary>
[TestClass]
public class LtbNationalImperfectionFactorsTests
{
    private const string Annex = "National Annex NA.2.17, alphaLT for 6.3.2.2(2)";
    private static SectionH Ipe300() => new(300, 7.1, 150, 10.7, 150, 10.7, "IPE300", 15);
    private static SectionH Heb300() => new(300, 11, 300, 19, 300, 19, "HEB300", 27);
    private static SteelVerificationInput Input(Section shape, bool ntc, double m1, double n = 0)
    {
        var code = ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "EC3 recommended"), 1.0);
        var section = new SteelSectionInput(new SteelSection(shape, new SteelMaterial("S355", 210000, 355, 510), Section.SectionTypes.Rolled), true);
        return new(section, new SteelActions(n, 0, 0, 0, m1, 0), code, new SteelMemberInput(6000, 6000, 6000));
    }
    // lambdaLT = sqrt(Wpl fy/Mcr) = 1 with Mcr = Wpl fy (class 1); MEd = 0.01 Mcr.
    private static SteelMemberAnalysis AtLambdaOne(bool ntc)
    {
        var i = Input(Ipe300(), ntc, 1); double mcr = i.Section.ReadOnlySection.Wpl1 * 355;
        return SteelMemberAnalysis.Bending(Input(Ipe300(), ntc, .01 * mcr), "uniform major-axis moment", "test values",
            SteelCriticalMomentBasis.UniformMoment, new SteelStabilityValue(SteelStabilityQuantity.Mcr, mcr, SteelStabilityUnit.Nmm, "chosen value"));
    }
    private static SteelLateralTorsionalMethod National(SteelLateralTorsionalMethod m) => m.WithDeclaredImperfectionFactors(.25, .40, .55, .80, Annex);
    private static SteelLateralTorsionalMethod Rolled()
        => SteelLateralTorsionalMethod.RolledOrEquivalentWelded(.75, .4, SteelLtbMomentFactor.Tabulated(SteelLtbMomentDistribution.Uniform, "uniform"), "EC3 6.3.2.3 recommended values");
    private static SteelCheckExecution Run(SteelMemberAnalysis a) => SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
    private static double T(SteelCheckExecution r, string name) => r.Trace.Single(t => t.Name == name).Value;
    private static bool Has(SteelCheckExecution r, string name) => r.Trace.Any(t => t.Name == name);

    // lambdaLT = 1. General (6.56), curve a with the national 0.25: Phi = 0.5[1 + 0.25(0.8) + 1] = 1.1,
    // chi = 1/(1.1 + sqrt(1.21 - 1)) = 0.64174243 (Table 6.3 0.21: 0.66560306). Declared curve c with 0.55: Phi = 1.22, chi = 0.52114379.
    // Rolled (6.57, beta 0.75, lambdaLT0 0.4, f = 1), Table 6.5 curve b with 0.40: Phi = 0.5[1 + 0.4(0.6) + 0.75] = 0.995,
    // chi = 1/(0.995 + sqrt(0.990025 - 0.75)) = 0.67343538; declared curve c with 0.55: Phi = 1.04, chi = 0.61887040.
    [DataTestMethod]
    [DataRow(false, null, .25, .21, .64174243050441593)] [DataRow(false, "C", .55, .49, .521143791613754)]
    [DataRow(true, null, .40, .34, .67343538211952314)] [DataRow(true, "C", .55, .49, .6188703973926416)]
    public void NationalAlphaLTOfTheCurveInUseReplacesTableSixThree(bool rolled, string? curve, double alpha, double recommended, double chi)
    {
        var method = National(rolled ? Rolled() : SteelLateralTorsionalMethod.General);
        if (curve != null) method = method.WithDeclaredCurve(Enum.Parse<SteelBucklingCurve>(curve), "National Annex curve clause");
        var r = Run(AtLambdaOne(false).WithLateralTorsionalMethod(method));
        Assert.AreEqual(SteelExecutionStatus.Passed, r.Status, r.Message);
        Assert.AreEqual(alpha, T(r, "AlphaLT")); Assert.AreEqual(chi, T(r, "ChiLT"), 1e-14);
        Assert.AreEqual(.01 / chi, r.Utilization!.Value, 1e-14);
        Assert.AreEqual(1, T(r, "AlphaLTDeclared")); Assert.AreEqual(recommended, T(r, "AlphaLTRecommended"));
        StringAssert.Contains(r.Reference, "alphaLT = " + alpha.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " of curve "
            + (curve ?? (rolled ? "b" : "a")).ToLowerInvariant() + " declared by the National Annex (" + Annex + ")");
        StringAssert.Contains(r.Reference, "6.3.2.2(2) NOTE");
        Assert.AreEqual(curve != null, Has(r, "LtbCurveDeclared"));
        Assert.IsFalse(r.Reference.Contains("alphaLT from Table 6.3"));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void RecommendedValuesDeclaredGiveTheUndeclaredResult(bool rolled)
    {
        var plain = Run(AtLambdaOne(false).WithLateralTorsionalMethod(rolled ? Rolled() : SteelLateralTorsionalMethod.General));
        var declared = Run(AtLambdaOne(false).WithLateralTorsionalMethod((rolled ? Rolled() : SteelLateralTorsionalMethod.General)
            .WithDeclaredImperfectionFactors(.21, .34, .49, .76, Annex)));
        Assert.AreEqual(plain.Utilization, declared.Utilization); Assert.AreEqual(T(plain, "ChiLT"), T(declared, "ChiLT"));
        Assert.IsFalse(Has(plain, "AlphaLTDeclared")); Assert.IsFalse(plain.Reference.Contains("National Annex"));
    }

    // HEB 300 S355 rolled (h/b = 1: Table 6.4 curve a), lambda1 = 0.5, lambda2 = 1.0, lambdaLT = 1.0; chiLT with the national 0.25 = 0.64174243.
    private static SteelCheckExecution BeamColumn(bool ntc, SteelLateralTorsionalMethod? method, bool susceptible = true)
    {
        var i = Input(Heb300(), ntc, 5e7, -2e5);
        var s = i.Section.ReadOnlySection; double npl = s.Area * 355, m1rk = s.Wpl1 * 355;
        var p = new SteelBeamColumnParameters(SteelBucklingCurve.B, SteelBucklingCurve.C, susceptible, 1, 1, 1, "Cm = 1 declared", "non-sway prismatic member",
            "torsional mode excluded by twist restraints", "coupled mode excluded by twist restraints",
            lateralTorsionalExclusionSource: susceptible ? null : "continuous twist restraint");
        var values = new List<SteelStabilityValue> { new(SteelStabilityQuantity.Ncr1, npl / .25, SteelStabilityUnit.N, "lambda1 = 0.5"),
            new(SteelStabilityQuantity.Ncr2, npl, SteelStabilityUnit.N, "lambda2 = 1.0") };
        if (susceptible) values.Add(new(SteelStabilityQuantity.Mcr, m1rk, SteelStabilityUnit.Nmm, "uniform-moment Mcr, lambdaLT = 1.0"));
        var a = SteelMemberAnalysis.BeamColumn(i, "one whole-member state", "test values", p, values, 1, "independent class 1");
        return Run(method == null ? a : a.WithLateralTorsionalMethod(method));
    }

    [TestMethod]
    public void BeamColumnChiLTUsesTheNationalAlphaLT()
    {
        var r = BeamColumn(false, National(SteelLateralTorsionalMethod.General)); var table = BeamColumn(false, null);
        Assert.IsTrue(r.IsEvaluated, r.Message);
        Assert.AreEqual(.64174243050441593, T(r, "ChiLT"), 1e-14); Assert.AreEqual(.25, T(r, "AlphaLT"));
        Assert.AreEqual(.66560305928460710, T(table, "ChiLT"), 1e-14); Assert.AreEqual(.21, T(table, "AlphaLT"));
        Assert.IsTrue(r.Utilization > table.Utilization); // 0.25 > 0.21 lowers chiLT
        StringAssert.Contains(r.Reference, "declared by the National Annex");
        // Not susceptible: no chiLT for the declaration, refused instead of ignored.
        Assert.AreEqual("Member.LtbImperfectionFactorsWithoutLtbReduction", BeamColumn(false, National(SteelLateralTorsionalMethod.General), false).DiagnosticCode);
    }

    [TestMethod]
    public void NtcRefusesNationalAlphaLT()
    {
        foreach (var method in new[] { SteelLateralTorsionalMethod.General, Rolled() })
        {
            var r = Run(AtLambdaOne(true).WithLateralTorsionalMethod(National(method)));
            Assert.AreEqual(SteelExecutionStatus.NotSupported, r.Status); Assert.AreEqual("Member.LtbImperfectionFactorsFixedByNtc", r.DiagnosticCode);
            StringAssert.Contains(r.Message, "Tab. 4.2.IX(a)");
        }
        Assert.AreEqual("Member.LtbImperfectionFactorsFixedByNtc", BeamColumn(true, National(SteelLateralTorsionalMethod.General)).DiagnosticCode);
    }

    [TestMethod]
    public void DeclaredAlphaLTAreValidated()
    {
        var general = SteelLateralTorsionalMethod.General;
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => general.WithDeclaredImperfectionFactors(0, .34, .49, .76, Annex));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => general.WithDeclaredImperfectionFactors(-.21, .34, .49, .76, Annex));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => general.WithDeclaredImperfectionFactors(.21, .34, .49, 1.01, Annex));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => general.WithDeclaredImperfectionFactors(.21, .49, .34, .76, Annex)); // b > c
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => general.WithDeclaredImperfectionFactors(double.NaN, .34, .49, .76, Annex));
        Assert.ThrowsException<ArgumentException>(() => general.WithDeclaredImperfectionFactors(.21, .34, .49, .76, " "));
        var national = National(general);
        Assert.AreNotSame(general, national); Assert.AreEqual(Annex, national.DeclaredImperfectionFactorsSource);
        Assert.AreEqual(.25, national.DeclaredImperfectionFactor(SteelBucklingCurve.A)); Assert.AreEqual(.40, national.DeclaredImperfectionFactor(SteelBucklingCurve.B));
        Assert.AreEqual(.55, national.DeclaredImperfectionFactor(SteelBucklingCurve.C)); Assert.AreEqual(.80, national.DeclaredImperfectionFactor(SteelBucklingCurve.D));
        Assert.IsNull(national.DeclaredImperfectionFactor(SteelBucklingCurve.A0)); // a0 is no LTB curve
        Assert.IsNull(general.DeclaredImperfectionFactorsSource); Assert.IsNull(general.DeclaredImperfectionFactor(SteelBucklingCurve.B));
        // Equal neighbours and the bound 1 are admitted.
        Assert.AreEqual(1, general.WithDeclaredImperfectionFactors(.34, .34, .76, 1, Annex).DeclaredImperfectionFactor(SteelBucklingCurve.D));
    }
}
