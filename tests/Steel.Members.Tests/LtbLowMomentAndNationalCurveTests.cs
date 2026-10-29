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

/// <summary>EN 1993-1-1:2005 options of the LTB method, both opt-in and refused under NTC 2018: the 6.3.2.2(4) low-moment
/// criterion MEd/Mcr &lt;= lambdaLT0^2 (prismatic bending only) and a national curve assignment in place of Tables 6.4/6.5
/// (alphaLT stays the Table 6.3 value). Expected values are hand calculations (40-digit decimal check) written in the
/// comments, not library outputs. IPE 300 S355 rolled, h/b = 2: Table 6.4 curve a, Table 6.5 curve b.</summary>
[TestClass]
public class LtbLowMomentAndNationalCurveTests
{
    private static SectionH Ipe300() => new(300, 7.1, 150, 10.7, 150, 10.7, "IPE300", 15);
    private static SectionH Heb300() => new(300, 11, 300, 19, 300, 19, "HEB300", 27);
    private static SteelVerificationInput Input(Section shape, bool ntc, double m1, double n = 0)
    {
        var code = ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "EC3 recommended"), 1.0);
        var section = new SteelSectionInput(new SteelSection(shape, new SteelMaterial("S355", 210000, 355, 510), Section.SectionTypes.Rolled), true);
        return new(section, new SteelActions(n, 0, 0, 0, m1, 0), code, new SteelMemberInput(6000, 6000, 6000));
    }
    private static SteelMemberAnalysis Bending(bool ntc, double moment, double mcr) => SteelMemberAnalysis.Bending(Input(Ipe300(), ntc, moment),
        "uniform major-axis moment", "test values", SteelCriticalMomentBasis.UniformMoment, new SteelStabilityValue(SteelStabilityQuantity.Mcr, mcr, SteelStabilityUnit.Nmm, "chosen value"));
    // lambdaLT = sqrt(Wpl fy / Mcr) with Mcr = Wpl fy / lambda^2 (class 1); MEd = ratio Mcr.
    private static SteelMemberAnalysis AtSlenderness(bool ntc, double lambda, double ratio = .01)
    {
        double mcr = Input(Ipe300(), ntc, 1).Section.ReadOnlySection.Wpl1 * 355 / (lambda * lambda);
        return Bending(ntc, ratio * mcr, mcr);
    }
    private static SteelLtbMomentFactor Uniform => SteelLtbMomentFactor.Tabulated(SteelLtbMomentDistribution.Uniform, "uniform moment between the fork supports");
    private static SteelLateralTorsionalMethod Rolled(SteelLtbMomentFactor? kc = null)
        => SteelLateralTorsionalMethod.RolledOrEquivalentWelded(.75, .4, kc ?? Uniform, "EC3 6.3.2.3 recommended values");
    private const string Adopted = "Design basis rev. 2: EN 1993-1-1:2005 6.3.2.2(4) adopted";
    private const string Annex = "National Annex, clause for 6.3.2.2(2)/6.3.2.3(1), rolled I h/b <= 2";
    private static SteelCheckExecution Run(SteelMemberAnalysis a) => SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
    private static double T(SteelCheckExecution r, string name) => r.Trace.Single(t => t.Name == name).Value;
    private static bool Has(SteelCheckExecution r, string prefix) => r.Trace.Any(t => t.Name.StartsWith(prefix, StringComparison.Ordinal));

    // First criterion of 6.3.2.2(4), already in both reductions: lambdaLT <= lambdaLT0 gives chiLT = 1.
    // General: lambda 0.15 and 0.2, Phi(0.2) = 0.5[1 + 0 + 0.04] = 0.52, chi = 1/(0.52 + sqrt(0.2704 - 0.04)) = 1/(0.52 + 0.48) = 1.
    // Rolled: lambda 0.4 = lambdaLT0, Phi = 0.5[1 + 0 + 0.75(0.16)] = 0.56, chi = 1/(0.56 + sqrt(0.3136 - 0.12)) = 1/(0.56 + 0.44) = 1.
    [DataTestMethod]
    [DataRow(.15, false, false)] [DataRow(.2, false, false)] [DataRow(.15, true, false)] [DataRow(.2, true, false)]
    [DataRow(.3, false, true)] [DataRow(.4, false, true)] [DataRow(.3, true, true)] [DataRow(.4, true, true)]
    public void SlendernessUpToLambdaLT0GivesChiLTOne(double lambda, bool ntc, bool rolled)
    {
        var a = AtSlenderness(ntc, lambda); var r = Run(rolled ? a.WithLateralTorsionalMethod(Rolled()) : a);
        Assert.AreEqual(SteelExecutionStatus.Passed, r.Status, r.Message);
        Assert.AreEqual(lambda, T(r, "SlendernessLT"), 1e-14); Assert.AreEqual(1, T(r, "ChiLT"), 1e-14);
        double w = T(r, "Wpl1"), gamma = ntc ? 1.05 : 1;
        Assert.AreEqual(w * 355 / gamma, r.Resistance!.Value, w * 355 * 1e-14);
        Assert.IsFalse(Has(r, "LowMoment.")); // the default never applies the second criterion
    }

    // Second criterion, general method: lambdaLT0 = 0.2, limit 0.2^2 = 0.04. MEd = 1e8 Nmm, Mcr = 2.5e9 Nmm: MEd/Mcr = 0.04 <= 0.04.
    // Mcr = 2.4e9 Nmm: MEd/Mcr = 1/24 = 0.0416667 > 0.04, the LTB check runs exactly as without the option.
    [TestMethod]
    public void LowMomentExemptionAtTheGeneralLimit()
    {
        var general = SteelLateralTorsionalMethod.General.WithLowMomentExemption(Adopted);
        var r = Run(Bending(false, 1e8, 2.5e9).WithLateralTorsionalMethod(general));
        Assert.AreEqual(SteelExecutionStatus.NotRequired, r.Status); Assert.AreEqual("Member.LtbLowMomentExemption", r.DiagnosticCode);
        Assert.IsFalse(r.IsEvaluated); Assert.IsNull(r.Utilization); Assert.IsNull(r.Resistance);
        Assert.AreEqual(1e8, T(r, "LowMoment.MEd")); Assert.AreEqual(2.5e9, T(r, "LowMoment.Mcr"));
        Assert.AreEqual(.04, T(r, "LowMoment.MEdOverMcr"), 1e-17); Assert.AreEqual(.2, T(r, "LowMoment.LambdaLT0"));
        Assert.AreEqual(.04, T(r, "LowMoment.Limit"), 1e-16); Assert.AreEqual(1, T(r, "LowMoment.Exempt"));
        Assert.IsFalse(Has(r, "ChiLT")); StringAssert.Contains(r.Reference, "6.3.2.2(4)"); StringAssert.Contains(r.Reference, "general case");
        StringAssert.Contains(r.Reference, Adopted); StringAssert.Contains(r.Scope, "section");

        var above = Run(Bending(false, 1e8, 2.4e9).WithLateralTorsionalMethod(general)); var plain = Run(Bending(false, 1e8, 2.4e9));
        Assert.AreEqual(SteelExecutionStatus.Passed, above.Status, above.Message);
        Assert.AreEqual(1 / 24d, T(above, "LowMoment.MEdOverMcr"), 1e-16); Assert.AreEqual(0, T(above, "LowMoment.Exempt"));
        Assert.AreEqual(plain.Resistance, above.Resistance); Assert.AreEqual(plain.Utilization, above.Utilization); Assert.AreEqual(plain.Reference, above.Reference);
        CollectionAssert.AreEqual(plain.Trace.Select(t => (t.Name, t.Value)).ToArray(), above.Trace.Where(t => !t.Name.StartsWith("LowMoment.")).Select(t => (t.Name, t.Value)).ToArray());
        // Without the option the same low moment is checked as before.
        var standard = Run(Bending(false, 1e8, 2.5e9));
        Assert.AreEqual(SteelExecutionStatus.Passed, standard.Status); Assert.IsFalse(Has(standard, "LowMoment."));
    }

    // lambdaLT = 1, MEd/Mcr = 0.1: above 0.2^2 = 0.04 (general), below 0.4^2 = 0.16 (rolled, lambdaLT0 = 0.4).
    // General curve a: Phi = 0.5[1 + 0.21(0.8) + 1] = 1.084, chi = 0.66560306; utilization = 0.1/0.66560306 = 0.15023969.
    // Rolled curve b without the option: chi = 0.69966823 (RolledLateralTorsionalBucklingTests), utilization 0.14292488.
    [TestMethod]
    public void LowMomentLimitUsesTheLambdaLT0OfTheMethodInUse()
    {
        var a = AtSlenderness(false, 1, .1);
        var general = Run(a.WithLateralTorsionalMethod(SteelLateralTorsionalMethod.General.WithLowMomentExemption(Adopted)));
        Assert.AreEqual(SteelExecutionStatus.Passed, general.Status, general.Message);
        Assert.AreEqual(.04, T(general, "LowMoment.Limit"), 1e-16); Assert.AreEqual(0, T(general, "LowMoment.Exempt"));
        Assert.AreEqual(.1, T(general, "LowMoment.MEdOverMcr"), 1e-15);
        Assert.AreEqual(.66560305928460710, T(general, "ChiLT"), 1e-14); Assert.AreEqual(.15023969407153929, general.Utilization!.Value, 1e-14);

        var rolled = Run(a.WithLateralTorsionalMethod(Rolled().WithLowMomentExemption(Adopted)));
        Assert.AreEqual(SteelExecutionStatus.NotRequired, rolled.Status, rolled.Message);
        Assert.AreEqual(.4, T(rolled, "LowMoment.LambdaLT0")); Assert.AreEqual(.16, T(rolled, "LowMoment.Limit"), 1e-16);
        Assert.AreEqual(1, T(rolled, "LowMoment.Exempt")); StringAssert.Contains(rolled.Reference, "rolled or equivalent welded method");
        StringAssert.Contains(rolled.Reference, "EC3 6.3.2.3 recommended values");

        var check = Run(a.WithLateralTorsionalMethod(Rolled()));
        Assert.AreEqual(.69966823291156054, T(check, "ChiLT"), 1e-14); Assert.AreEqual(.14292488253163296, check.Utilization!.Value, 1e-14);
    }

    // The Section-and-member scope keeps the section checks: with the member check not required the verdict rests on them.
    [TestMethod]
    public void SectionChecksStillApplyWhenTheLtbCheckIsNotRequired()
    {
        var a = Bending(false, 1e8, 2.5e9).WithLateralTorsionalMethod(SteelLateralTorsionalMethod.General.WithLowMomentExemption(Adopted));
        var result = SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a, SteelCheckScope.SectionAndMember));
        Assert.AreEqual(SteelExecutionStatus.NotRequired, result.Checks.Single(c => c.Decision.Target == SteelCheckTarget.MemberStability).Status);
        Assert.IsTrue(result.Checks.Any(c => c.Decision.Target != SteelCheckTarget.MemberStability && c.IsEvaluated));
        Assert.IsTrue(result.IsComplete); Assert.AreEqual(GPC.Model.Checking.Contracts.EngineeringOutcome.Satisfied, result.Outcome);
        // Member scope alone: the only check is not required, so nothing is evaluated.
        Assert.IsFalse(SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).IsComplete);
    }

    [TestMethod]
    public void NtcRefusesTheEc3Options()
    {
        var a = AtSlenderness(true, 1);
        foreach (var method in new[] { SteelLateralTorsionalMethod.General, Rolled() })
        {
            var exempt = Run(a.WithLateralTorsionalMethod(method.WithLowMomentExemption(Adopted)));
            Assert.AreEqual(SteelExecutionStatus.NotSupported, exempt.Status); Assert.AreEqual("Member.LtbLowMomentExemptionNotInNtc", exempt.DiagnosticCode);
            var curve = Run(a.WithLateralTorsionalMethod(method.WithDeclaredCurve(SteelBucklingCurve.C, Annex)));
            Assert.AreEqual(SteelExecutionStatus.NotSupported, curve.Status); Assert.AreEqual("Member.LtbCurveFixedByNtc", curve.DiagnosticCode);
        }
        var column = BeamColumn(true, SteelLateralTorsionalMethod.General.WithDeclaredCurve(SteelBucklingCurve.C, Annex));
        Assert.AreEqual("Member.LtbCurveFixedByNtc", column.DiagnosticCode);
    }

    // General: Table 6.4 curve a (0.21) replaced; lambda 1: b Phi = 1.136, chi = 0.59702319; a (declared equal to the table) 0.66560306.
    // Rolled, Table 6.5 curve b (0.34) replaced: c Phi = 1.022, chi = 0.63911493 (kc = 1); d with kc = 1/1.33,
    // f = 0.88586466, Phi = 1.103, Base = 0.55988288, chi = 0.55988288/0.88586466 = 0.63201853.
    [DataTestMethod]
    [DataRow(false, "B", .34, .21, .59702319159355289)] [DataRow(false, "A", .21, .21, .66560305928460710)]
    [DataRow(true, "C", .49, .34, .63911493312502756)] [DataRow(true, "D", .76, .34, .63201852546956026)]
    public void DeclaredCurveReplacesTheTableAssignment(bool rolled, string curve, double alpha, double tableAlpha, double chi)
    {
        var declared = Enum.Parse<SteelBucklingCurve>(curve);
        var method = (rolled ? Rolled(curve == "D" ? SteelLtbMomentFactor.Linear(0, "M1 linear to zero") : null) : SteelLateralTorsionalMethod.General).WithDeclaredCurve(declared, Annex);
        var r = Run(AtSlenderness(false, 1).WithLateralTorsionalMethod(method));
        Assert.AreEqual(SteelExecutionStatus.Passed, r.Status, r.Message);
        Assert.AreEqual(alpha, T(r, "AlphaLT")); Assert.AreEqual(chi, T(r, "ChiLT"), 1e-14);
        Assert.AreEqual(1, T(r, "LtbCurveDeclared")); Assert.AreEqual(tableAlpha, T(r, "AlphaLTTableCurve"));
        StringAssert.Contains(r.Reference, "LTB curve " + curve.ToLowerInvariant() + " declared by the National Annex (" + Annex + ")");
        StringAssert.Contains(r.Reference, rolled ? "Table 6.5" : "Table 6.4");
        Assert.AreEqual(rolled ? .75 : 1, T(r, "BetaLT")); Assert.AreEqual(rolled ? .4 : .2, T(r, "LambdaLT0"));
        Assert.IsFalse(Has(Run(AtSlenderness(false, 1)), "LtbCurveDeclared")); // rows only with a declaration
    }

    // HEB 300 S355 rolled (h/b = 1), class 1; lambda1 = 0.5, lambda2 = 1.0, lambdaLT = 1.0 (BeamColumnChiLTUsesTheSelectedMethod).
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

    // Declared curve c in the beam-column chiLT, lambdaLT = 1: general Phi = 0.5[1 + 0.49(0.8) + 1] = 1.196, chi = 0.53993903 (Table 6.4
    // curve a replaced); rolled psi = 0, f = 0.88586466, Phi = 1.022, chi = 0.63911493/0.88586466 = 0.72145889 (Table 6.5 curve b replaced).
    [DataTestMethod] [DataRow(false, .53993902722384103, .21)] [DataRow(true, .72145888733346347, .34)]
    public void BeamColumnChiLTUsesTheDeclaredCurve(bool rolled, double chi, double tableAlpha)
    {
        var method = (rolled ? Rolled(SteelLtbMomentFactor.Linear(0, "M1 linear to zero")) : SteelLateralTorsionalMethod.General).WithDeclaredCurve(SteelBucklingCurve.C, Annex);
        var r = BeamColumn(false, method); var table = BeamColumn(false, rolled ? Rolled(SteelLtbMomentFactor.Linear(0, "M1 linear to zero")) : null);
        Assert.IsTrue(r.IsEvaluated, r.Message);
        Assert.AreEqual(chi, T(r, "ChiLT"), 1e-14); Assert.AreEqual(.49, T(r, "AlphaLT"));
        Assert.AreEqual(1, T(r, "LtbCurveDeclared")); Assert.AreEqual(tableAlpha, T(r, "AlphaLTTableCurve"));
        StringAssert.Contains(r.Reference, "declared by the National Annex");
        Assert.IsTrue(r.Utilization > table.Utilization); // curve c is less favourable than the table's a or b
        Assert.IsFalse(Has(table, "LtbCurveDeclared"));
        // Not susceptible: no chiLT to apply the curve to, the declaration is refused instead of ignored.
        Assert.AreEqual("Member.LtbCurveWithoutLtbReduction", BeamColumn(false, SteelLateralTorsionalMethod.General.WithDeclaredCurve(SteelBucklingCurve.C, Annex), false).DiagnosticCode);
        Assert.IsTrue(BeamColumn(false, null, false).IsEvaluated);
    }

    [TestMethod]
    public void BeamColumnRefusesTheLowMomentCriterion()
    {
        var i = Input(Heb300(), false, 5e7, -2e5);
        var p = new SteelBeamColumnParameters(SteelBucklingCurve.B, SteelBucklingCurve.C, true, 1, 1, 1, "Cm", "non-sway", "excluded", "excluded");
        var a = SteelMemberAnalysis.BeamColumn(i, "state", "test", p, new[] { new SteelStabilityValue(SteelStabilityQuantity.Mcr, 1e9, SteelStabilityUnit.Nmm, "x") });
        Assert.ThrowsException<ArgumentException>(() => a.WithLateralTorsionalMethod(SteelLateralTorsionalMethod.General.WithLowMomentExemption(Adopted)));
        Assert.ThrowsException<ArgumentException>(() => a.WithLateralTorsionalMethod(Rolled().WithLowMomentExemption(Adopted)));
        // The curve declaration belongs to the beam-column chiLT too.
        Assert.AreEqual(SteelBucklingCurve.B, a.WithLateralTorsionalMethod(SteelLateralTorsionalMethod.General.WithDeclaredCurve(SteelBucklingCurve.B, Annex)).LateralTorsionalMethod.DeclaredCurve);
    }

    [TestMethod]
    public void DeclarationsAreValidatedAndTheGeneralMethodStaysUnchanged()
    {
        var general = SteelLateralTorsionalMethod.General;
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => general.WithDeclaredCurve(SteelBucklingCurve.A0, Annex));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => general.WithDeclaredCurve((SteelBucklingCurve)7, Annex));
        Assert.ThrowsException<ArgumentException>(() => general.WithDeclaredCurve(SteelBucklingCurve.B, " "));
        Assert.ThrowsException<ArgumentException>(() => general.WithLowMomentExemption(""));
        var both = general.WithDeclaredCurve(SteelBucklingCurve.B, Annex).WithLowMomentExemption(Adopted);
        Assert.AreNotSame(general, both); Assert.AreEqual(SteelBucklingCurve.B, both.DeclaredCurve); Assert.AreEqual(Annex, both.DeclaredCurveSource);
        Assert.IsTrue(both.LowMomentExemption); Assert.AreEqual(Adopted, both.LowMomentExemptionSource);
        Assert.AreEqual(SteelLateralTorsionalMethodKind.General, both.Kind); Assert.AreEqual(.2, both.LambdaLT0);
        Assert.IsNull(general.DeclaredCurve); Assert.IsNull(general.DeclaredCurveSource); Assert.IsFalse(general.LowMomentExemption);
        var rolled = Rolled(); var exempt = rolled.WithLowMomentExemption(Adopted);
        Assert.IsFalse(rolled.LowMomentExemption); Assert.AreEqual(.75, exempt.Beta); Assert.AreEqual(.4, exempt.LambdaLT0); Assert.AreSame(rolled.MomentFactor, exempt.MomentFactor);
    }
}
