using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class SteelMemberAnalysisTests
{
    private static SteelVerificationInput Input(double n = -1e5, double m = 1e7, bool ntc = false, SteelMemberInput? member = null, double v = 0)
        => new(new SteelSectionInput(new SteelSection(new SectionH(300, 12, 150, 18, 150, 18, "H"), new SteelMaterial("S355", 210000, 355, 510)), true),
            new SteelActions(n, 0, v, 0, m, 0), ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "test"), 1),
            member ?? new SteelMemberInput(3000));
    private static SteelMemberAnalysis Uniform(SteelVerificationInput input) => SteelMemberAnalysis.UniformFork(input,
        "Uniform H, fork/free warping, constant first-order N/M1; no transverse actions", "explicit EC3 test curves",
        Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.C);
    private static SteelCheckExecution Run(SteelMemberAnalysis analysis)
        => SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(analysis)).Checks.Single();
    private static SteelStabilityValue Factor(SteelStabilityQuantity q, double value) => new(q, value, SteelStabilityUnit.One, "external analysis rev 2");

    [DataTestMethod]
    [DataRow(-1e5, 0d)] [DataRow(0d, 1e7)] [DataRow(-1e5, -1e7)] [DataRow(-5e6, 1e7)]
    public void ExplicitUniformRouteReusesQualifiedNativeMethod(double n, double m)
    {
        var input = Input(n, m); var result = Run(Uniform(input));
        var native = Ec3MonosymmetricMember.EvaluateUniformFork(input.Section.CreateSectionCopy(), 3000, n, m,
            Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.C, 1, 1, "explicit EC3 test curves");
        Assert.IsTrue(result.IsEvaluated, result.Message);
        Assert.AreEqual(native.Utilization, result.Utilization!.Value, 1e-12);
        // Euler value independently reconstructed from flange/web rectangles, N/mm/MPa.
        double inertia = 2 * (150 * Math.Pow(18, 3) / 12 + 150 * 18 * 141 * 141) + 12 * Math.Pow(264, 3) / 12;
        Assert.AreEqual(Math.PI * Math.PI * 210000 * inertia / (3000 * 3000), result.Trace.Single(v => v.Name == "Ncr1").Value, 1e-6);
        Assert.AreEqual(n == 0, result.Trace.Any(v => v.Name == "Mcr"));
    }

    [DataTestMethod]
    [DataRow(true, 0d)] [DataRow(false, 1d)]
    public void NormAndActionLimitsAreNotBypassed(bool ntc, double shear)
    { var result = Run(Uniform(Input(ntc: ntc, v: shear))); Assert.AreEqual(SteelExecutionStatus.NotSupported, result.Status); }

    [TestMethod]
    public void ExplicitMetadataConflictingWithForkModelIsRejected()
    {
        foreach (var member in new[] { new SteelMemberInput(3000, bucklingLength1: 1500),
            new SteelMemberInput(3000, startRestraints: new SteelEndRestraints(warping: SteelRestraintState.Fixed)),
            new SteelMemberInput(3000, imperfections: new SteelMemberImperfections(true, "already included")) })
            Assert.AreEqual("Member.UniformForkConflict", Run(Uniform(Input(member: member))).DiagnosticCode);
    }

    [DataTestMethod]
    [DataRow(SteelStabilityQuantity.Ncr1, SteelStabilityUnit.KN, 1000d)]
    [DataRow(SteelStabilityQuantity.Mcr, SteelStabilityUnit.KNm, 1000000d)]
    [DataRow(SteelStabilityQuantity.AlphaCrOp, SteelStabilityUnit.One, 1d)]
    public void ExternalUnitsAreCanonicalAndOriginalProvenanceIsRetained(SteelStabilityQuantity q, SteelStabilityUnit unit, double scale)
    { var v = new SteelStabilityValue(q, 2, unit, "analysis ref"); Assert.AreEqual(2 * scale, v.Value); Assert.AreEqual(2d, v.OriginalValue); Assert.AreEqual("analysis ref", v.Source); }

    [TestMethod]
    public void CriticalValuesAloneDoNotEstablishAResistance()
    {
        var analysis = SteelMemberAnalysis.FromExternal(Input(), "complete load pattern", "EC3", Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.C,
            new[] { new SteelStabilityValue(SteelStabilityQuantity.Mcr, 100, SteelStabilityUnit.KNm, "eigenvalue analysis") }, 2, "nominal member class");
        var result = Run(analysis); Assert.AreEqual(SteelExecutionStatus.MissingInput, result.Status); Assert.IsNull(result.Utilization);
        CollectionAssert.Contains(result.Decision.MissingInputs.ToArray(), "AlphaUltK");
        CollectionAssert.Contains(result.Decision.MissingInputs.ToArray(), "AlphaCrOp");
    }

    [TestMethod]
    public void ExternalFactorsUseTheExistingGeneralMethodAndOwnSnapshot()
    {
        var values = new[] { Factor(SteelStabilityQuantity.AlphaUltK, 2), Factor(SteelStabilityQuantity.AlphaCrOp, 4) };
        var input = Input(); var analysis = SteelMemberAnalysis.FromExternal(input, "whole-member pattern A", "EC3 Annex assessed",
            Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.C, values, 2, "uniform compression class");
        values[0] = Factor(SteelStabilityQuantity.AlphaUltK, 100);
        var result = Run(analysis);
        var expected = Ec3GeneralMethod.EvaluateInPlane(2, 4, Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.C, 1, "test", "test");
        Assert.AreEqual(expected.Utilization, result.Utilization!.Value, 1e-12);
        Assert.AreSame(input, SteelMethodSelector.SelectMember(analysis).Input);
    }

    [DataTestMethod]
    [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)] [DataRow(0d)] [DataRow(-1d)]
    public void ExternalNonpositiveOrNonfiniteValueIsRejected(double value)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Factor(SteelStabilityQuantity.AlphaUltK, value));

    [TestMethod]
    public void WrongUnitsDuplicatesAndUnsupportedClassAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(() => new SteelStabilityValue(SteelStabilityQuantity.Mcr, 1, SteelStabilityUnit.KN, "test"));
        var factor = Factor(SteelStabilityQuantity.AlphaUltK, 2);
        Assert.ThrowsException<ArgumentException>(() => SteelMemberAnalysis.FromExternal(Input(), "pattern", "test", Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.C, new[] { factor, factor }));
        var analysis = SteelMemberAnalysis.FromExternal(Input(), "pattern", "test", Ec3GeneralMethodCurve.B, Ec3GeneralMethodCurve.C,
            new[] { factor, Factor(SteelStabilityQuantity.AlphaCrOp, 4) }, 4, "class 4");
        Assert.AreEqual(SteelExecutionStatus.NotSupported, Run(analysis).Status);
    }
}
