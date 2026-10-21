using System.Collections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Selection;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Status = GPC.Checkers.Steel.Selection.SteelSelectionStatus;

namespace Steel.Members.Tests;

[TestClass]
public class SteelMethodSelectorTests
{
    private static SteelVerificationInput Input(Section? shape = null, double n = 0, double m1 = 0, double m2 = 0,
        double v1 = 0, double v2 = 0, double t = 0, bool ntc = false, bool? unperforated = true,
        double? netArea = null, SteelMemberInput? member = null, double? gammaM1 = null)
        => new(new SteelSectionInput(new SteelSection(shape ?? H(), new SteelMaterial("S235", 210000, 235, 360)),
            unperforated, netArea), new SteelActions(n, v1, v2, t, m1, m2),
            ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "test"), gammaM1), member);

    private static SectionH H(double web = 12, double radius = 0, double bottom = 150)
        => new(300, web, 150, 12, bottom, 12, "H", radius);
    private static SteelMethodDecision Normal(SteelSelectionPlan plan)
        => plan.Decisions.Single(d => d.Target == SteelCheckTarget.NormalResistance);

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void PureTensionUsesTheExistingPrimitiveAndNoMemberContext(bool ntc)
    {
        var input = Input(n: 1e9, ntc: ntc);
        var plan = SteelMethodSelector.Select(input);
        Assert.AreEqual(SteelMethod.SectionTension, Normal(plan).Method);
        Assert.AreEqual(Status.Ready, Normal(plan).Status);
        Assert.AreEqual(4, plan.Decisions.Count);
        Assert.IsTrue(plan.IsReadyForExecution); // Routing is not a resistance verdict, even at 1e9 N.
        Assert.AreSame(input, plan.Input);
        Assert.IsNull(Normal(plan).SectionClass);
        Assert.IsFalse(plan.Decisions.Any(d => d.Target == SteelCheckTarget.MemberStability));
    }

    [TestMethod]
    public void HoleDeclarationAndNetAreaRemainMandatoryForTension()
    {
        var unknown = SteelMethodSelector.Select(Input(n: 1000, unperforated: null));
        Assert.AreEqual(Status.MissingInput, Normal(unknown).Status);
        CollectionAssert.AreEqual(new[] { "Section.Unperforated" }, Normal(unknown).MissingInputs.ToArray());
        var missingArea = SteelMethodSelector.Select(Input(n: 1000, unperforated: false));
        Assert.AreEqual("NetArea.Required", Normal(missingArea).DiagnosticCode);
        var perforated = SteelMethodSelector.Select(Input(n: 1000, unperforated: false, netArea: 5000));
        Assert.IsTrue(perforated.IsReadyForExecution);
    }

    public static IEnumerable<object[]> CompressionFamilies()
    {
        yield return new object[] { H() };
        yield return new object[] { new SectionT(120, 100, 20, 20, "T") };
        yield return new object[] { new SectionC(200, 12, 80, 15, 80, 15, "C") };
        yield return new object[] { new SectionL(80, 10, 80, 10, "L", 0, 0) };
        yield return new object[] { new SectionCHS(150, 8, "CHS") };
        yield return new object[] { new SectionRHS(200, 100, 10, 10, 10, 10, "RHS") };
        yield return new object[] { new SectionRHSRoundedCorners(200, 100, 10, 20, 10, "rounded RHS") };
    }

    [DataTestMethod, DynamicData(nameof(CompressionFamilies), DynamicDataSourceType.Method)]
    public void Ec3CompressionSelectsAllSevenMappedFamilies(Section shape)
    {
        var plan = SteelMethodSelector.Select(Input(shape, n: -1000));
        Assert.AreEqual(Status.Ready, Normal(plan).Status, Normal(plan).Message);
        Assert.AreEqual(SteelMethod.SectionCompression, Normal(plan).Method);
        Assert.IsTrue(Normal(plan).SectionClass is >= 1 and <= 3);
        StringAssert.Contains(Normal(plan).ClassificationSource, "compression");
        Assert.IsTrue(plan.IsReadyForExecution);
    }

    [TestMethod]
    public void ClassFourIsNotMappedToGrossSectionResistance()
    {
        var plan = SteelMethodSelector.Select(Input(H(web: 5), n: -1000));
        Assert.AreEqual(Status.NotSupported, Normal(plan).Status);
        Assert.AreEqual("Class4.EffectivePropertiesRequired", Normal(plan).DiagnosticCode);
        Assert.AreEqual(4, Normal(plan).SectionClass);
        Assert.IsNull(Normal(plan).Method);
        Assert.IsFalse(plan.IsFullyRouted);
    }

    [TestMethod]
    public void NtcDoesNotBorrowEc3ClassificationAndAcceptsExplicitBoundEvidence()
    {
        var input = Input(n: -1000, ntc: true);
        var missing = SteelMethodSelector.Select(input);
        Assert.AreEqual(Status.MissingInput, Normal(missing).Status);
        Assert.AreEqual("NtcClassification.Required", Normal(missing).DiagnosticCode);
        var evidence = new SteelSectionClassification(input, 2, "NTC classification calculation A-42");
        var plan = SteelMethodSelector.Select(input, classification: evidence);
        Assert.AreEqual(SteelMethod.SectionCompression, Normal(plan).Method);
        Assert.AreEqual(2, Normal(plan).SectionClass);
        Assert.AreEqual(evidence.Source, Normal(plan).ClassificationSource);
        Assert.IsTrue(plan.IsReadyForExecution);
    }

    [TestMethod]
    public void NtcChsUsesItsExistingOwnClassification()
    {
        var plan = SteelMethodSelector.Select(Input(new SectionCHS(150, 8), n: -1000, ntc: true));
        Assert.IsTrue(plan.IsReadyForExecution);
        Assert.AreEqual(1, Normal(plan).SectionClass);
        StringAssert.Contains(Normal(plan).ClassificationSource, "Ntc2018DesignPolicy");
    }

    [TestMethod]
    public void ClassificationCannotLeakAcrossSnapshotsCodesOrActions()
    {
        var first = Input(n: -1000, ntc: true);
        var evidence = new SteelSectionClassification(first, 1, "assessment");
        Assert.ThrowsException<ArgumentException>(() => SteelMethodSelector.Select(Input(n: -1000, ntc: true), classification: evidence));
        Assert.ThrowsException<ArgumentException>(() => SteelMethodSelector.Select(Input(n: 1000), classification: evidence));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelSectionClassification(first, 0, "assessment"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelSectionClassification(first, 5, "assessment"));
        Assert.ThrowsException<ArgumentException>(() => new SteelSectionClassification(first, 1, " "));
    }

    [DataTestMethod]
    [DataRow(1, false)] [DataRow(2, false)] [DataRow(1, true)] [DataRow(2, true)]
    public void UniaxialBendingKeepsTheAxisAndNationalCode(int axis, bool ntc)
    {
        var input = Input(m1: axis == 1 ? 1e6 : 0, m2: axis == 2 ? -1e6 : 0, ntc: ntc);
        var plan = SteelMethodSelector.Select(input, classification: ntc ? new SteelSectionClassification(input, 1, "NTC class") : null);
        Assert.IsTrue(plan.IsReadyForExecution);
        Assert.AreEqual(axis == 1 ? SteelMethod.SectionBending1 : SteelMethod.SectionBending2, Normal(plan).Method);
        Assert.AreEqual(ntc ? SectionResistanceStandard.Ntc2018 : SectionResistanceStandard.Eurocode3_2005, plan.Input.Code.SectionCode.Standard);
    }

    [DataTestMethod]
    [DataRow(-1000d, 1e6, 0d)] [DataRow(1000d, 0d, -1e6)] [DataRow(0d, 1e6, -2e6)]
    public void CombinedNormalActionsSelectOneInteractionNotIndependentPasses(double n, double m1, double m2)
    {
        var plan = SteelMethodSelector.Select(Input(n: n, m1: m1, m2: m2));
        Assert.AreEqual(SteelMethod.Ec3PlasticHInteraction, Normal(plan).Method);
        Assert.AreEqual(1, plan.Decisions.Count(d => d.Method.HasValue));
        Assert.IsTrue(plan.IsReadyForExecution);
    }

    [TestMethod]
    public void AsymmetricHUsesPlasticEquilibriumEvenForUniaxialEc3Bending()
    {
        var plan = SteelMethodSelector.Select(Input(H(bottom: 200), m1: 1e6));
        Assert.AreEqual(SteelMethod.Ec3PlasticHInteraction, Normal(plan).Method);
        Assert.IsTrue(plan.IsReadyForExecution);
    }

    [TestMethod]
    public void NtcNormalInteractionUsesOnlyItsOwnElasticMethod()
    {
        var input = Input(n: -1000, m1: 1e6, m2: -2e6, ntc: true);
        var plan = SteelMethodSelector.Select(input, classification: new SteelSectionClassification(input, 3, "NTC actual state"));
        Assert.AreEqual(SteelMethod.NtcElasticNormalInteraction, Normal(plan).Method);
        Assert.IsTrue(plan.IsReadyForExecution);
        var withShear = Input(n: -1000, m1: 1e6, v2: 1, ntc: true);
        Assert.AreEqual("NtcElasticInteraction.ShearExcluded", Normal(SteelMethodSelector.Select(withShear)).DiagnosticCode);
    }

    [TestMethod]
    public void UnqualifiedInteractionsDoNotFallBackToOtherMethods()
    {
        var roundedH = SteelMethodSelector.Select(Input(H(radius: 5), n: -1000, m1: 1e6));
        Assert.AreEqual("NormalInteraction.GeometryNotQualified", Normal(roundedH).DiagnosticCode);
        var tee = SteelMethodSelector.Select(Input(new SectionT(120, 100, 20, 20, "T"), n: -1000, m1: 1e6));
        Assert.AreEqual(Status.NotSupported, Normal(tee).Status);
        var input = Input(n: -1000, m1: 1e6);
        var class3 = SteelMethodSelector.Select(input, classification: new SteelSectionClassification(input, 3, "state class 3"));
        Assert.AreEqual("PlasticH.Class1Or2Required", Normal(class3).DiagnosticCode);
        Assert.IsFalse(roundedH.IsFullyRouted || tee.IsFullyRouted || class3.IsFullyRouted);
    }

    [DataTestMethod]
    [DataRow(1, false)] [DataRow(2, false)] [DataRow(1, true)] [DataRow(2, true)]
    public void ShearRoutesRequireExplicitBucklingAssessment(int axis, bool ntc)
    {
        var plan = SteelMethodSelector.Select(Input(v1: axis == 1 ? 1000 : 0, v2: axis == 2 ? 1000 : 0, ntc: ntc));
        var shear = plan.Decisions.Single(d => d.Target == (axis == 1 ? SteelCheckTarget.Shear1 : SteelCheckTarget.Shear2));
        Assert.AreEqual(Status.RequiresPreparation, shear.Status);
        Assert.AreEqual(axis == 1 ? SteelMethod.SectionShear1 : SteelMethod.SectionShear2, shear.Method);
        CollectionAssert.AreEqual(new[] { SteelPreparationRequirement.ShearBucklingAssessment }, shear.RequiredPreparation.ToArray());
        Assert.IsTrue(plan.IsFullyRouted);
        Assert.IsFalse(plan.IsReadyForExecution);
    }

    [TestMethod]
    public void ExactLowShearBoundaryIsRetainedAndHighShearHasNoFallback()
    {
        // Existing sharp rolled H: Av2=A-2*b*tf+tw*tf=3456 mm2; gammaM0=1.
        double half = .5 * 3456 * 235 / Math.Sqrt(3);
        var boundary = SteelMethodSelector.Select(Input(m1: 1e6, v2: half));
        Assert.AreEqual(Status.RequiresPreparation, Normal(boundary).Status);
        Assert.AreEqual(SteelMethod.SectionBending1, Normal(boundary).Method);
        var high = SteelMethodSelector.Select(Input(m1: 1e6, v2: half * 1.000001));
        Assert.AreEqual("HighShearInteraction.NotIntegrated", Normal(high).DiagnosticCode);
        Assert.IsFalse(high.IsFullyRouted);
    }

    [TestMethod]
    public void SimultaneousShearsAndAxialShearAreNotIndependentCompleteChecks()
    {
        var biaxial = SteelMethodSelector.Select(Input(v1: 1000, v2: 1000));
        Assert.IsFalse(biaxial.IsFullyRouted);
        Assert.IsTrue(biaxial.Decisions.Where(d => d.Target is SteelCheckTarget.Shear1 or SteelCheckTarget.Shear2)
            .All(d => d.DiagnosticCode == "BiaxialShear.NotIntegrated"));
        Assert.AreEqual("AxialShearInteraction.NotIntegrated", Normal(SteelMethodSelector.Select(Input(n: -1000, v2: 1000))).DiagnosticCode);
    }

    [TestMethod]
    public void TorsionRemainsVisibleAndBlocksCoupledNormalAndShearRoutes()
    {
        var plan = SteelMethodSelector.Select(Input(n: -1000, m1: 1e6, v2: 1000, t: 100));
        Assert.IsFalse(plan.IsFullyRouted);
        foreach (var target in new[] { SteelCheckTarget.NormalResistance, SteelCheckTarget.Shear2, SteelCheckTarget.Torsion })
        {
            var decision = plan.Decisions.Single(d => d.Target == target);
            Assert.AreEqual(Status.NotSupported, decision.Status);
            Assert.IsNull(decision.Method);
        }
    }

    [TestMethod]
    public void PrincipalShearMappingIsNotGuessedForAnglesOrRotatedRhs()
    {
        var angle = SteelMethodSelector.Select(Input(new SectionL(80, 10, 80, 10, "L", 0, 0), v1: 1000));
        var rhs = SteelMethodSelector.Select(Input(new SectionRHS(100, 200, 10, 10, 10, 10, "wide RHS"), v1: 1000));
        Assert.AreEqual("ShearAxes.NotQualified", angle.Decisions.Single(d => d.Target == SteelCheckTarget.Shear1).DiagnosticCode);
        Assert.AreEqual("ShearAxes.NotQualified", rhs.Decisions.Single(d => d.Target == SteelCheckTarget.Shear1).DiagnosticCode);
    }

    [TestMethod]
    public void RoundedRhsCompressionSupportDoesNotEnableBendingOrShear()
    {
        var shape = new SectionRHSRoundedCorners(200, 100, 10, 20, 10, "rounded RHS");
        var bending = SteelMethodSelector.Select(Input(shape, m1: 1e6));
        Assert.AreEqual("RoundedRhs.BendingNotQualified", Normal(bending).DiagnosticCode);
        Assert.IsFalse(SteelMethodSelector.Select(Input(shape, v2: 1000)).IsFullyRouted);
        var genericRadius = SteelMethodSelector.Select(Input(new SectionRHS(200, 100, 10, 10, 10, 10, "base RHS", 5), n: -1000));
        Assert.AreEqual("Geometry.NotQualified", Normal(genericRadius).DiagnosticCode);
    }

    [TestMethod]
    public void UnknownAndPerforatedGeometryAreNotSilentlyEnabled()
    {
        var circular = SteelMethodSelector.Select(Input(new SectionCircular(50, "circle"), n: 1000));
        Assert.AreEqual("Geometry.NotQualified", Normal(circular).DiagnosticCode);
        var perforated = SteelMethodSelector.Select(Input(n: -1000, unperforated: false, netArea: 5000));
        Assert.AreEqual("PerforatedSection.NotQualified", Normal(perforated).DiagnosticCode);
        Assert.AreEqual(Status.MissingInput, Normal(SteelMethodSelector.Select(Input(n: -1000, unperforated: null))).Status);
    }

    [TestMethod]
    public void ExternalClassificationCannotBypassInvalidGeometry()
    {
        // Model accepts the degenerate rectangle, but the H mapper has zero outstands.
        var input = Input(H(web: 150), n: -1000, ntc: true);
        var plan = SteelMethodSelector.Select(input, classification: new SteelSectionClassification(input, 1, "claimed class"));
        Assert.AreEqual(Status.InvalidInput, Normal(plan).Status);
        Assert.IsNull(Normal(plan).Method);
    }

    [TestMethod]
    public void MemberScopeIsExplicitAndNeverInfersWholeMemberActionsFromOneCut()
    {
        var input = Input(n: -1000);
        var section = SteelMethodSelector.Select(input);
        Assert.IsTrue(section.IsReadyForExecution);
        var memberOnly = SteelMethodSelector.Select(input, SteelCheckScope.Member);
        Assert.AreEqual(1, memberOnly.Decisions.Count);
        Assert.AreEqual(Status.MissingInput, memberOnly.Decisions[0].Status);
        CollectionAssert.AreEqual(new[] { "Member", "Code.GammaM1" }, memberOnly.Decisions[0].MissingInputs.ToArray());
        var combined = SteelMethodSelector.Select(input, SteelCheckScope.SectionAndMember);
        Assert.AreEqual(5, combined.Decisions.Count);
        Assert.IsFalse(combined.IsFullyRouted);
        foreach (var n in new[] { -1000d, 0, 1000 })
        {
            var withMember = Input(n: n, member: new SteelMemberInput(4000, 4000, 4000, 4000), gammaM1: 1);
            var plan = SteelMethodSelector.Select(withMember, SteelCheckScope.Member);
            Assert.AreEqual("MemberRouting.NotIntegrated", plan.Decisions[0].DiagnosticCode);
            Assert.IsFalse(plan.IsReadyForExecution);
        }
    }

    [TestMethod]
    public void UnloadedSectionHasNoSyntheticSuccessfulCheck()
    {
        var plan = SteelMethodSelector.Select(Input());
        Assert.AreEqual(4, plan.Decisions.Count);
        Assert.IsTrue(plan.Decisions.All(d => d.Status == Status.NotRequired));
        Assert.IsFalse(plan.HasSelectedMethods);
        Assert.IsFalse(plan.IsReadyForExecution);
        Assert.IsTrue(plan.IsFullyRouted);
    }

    [TestMethod]
    public void PlansAreDeterministicAndReadOnly()
    {
        var input = Input(m1: 1e6, v2: 1000);
        var first = SteelMethodSelector.Select(input); var second = SteelMethodSelector.Select(input);
        CollectionAssert.AreEqual(first.Decisions.Select(d => (d.Target, d.Status, d.Method, d.DiagnosticCode)).ToArray(),
            second.Decisions.Select(d => (d.Target, d.Status, d.Method, d.DiagnosticCode)).ToArray());
        Assert.ThrowsException<NotSupportedException>(() => ((IList)first.Decisions).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList)Normal(first).RequiredPreparation).Clear());
    }

    [TestMethod]
    public void MissingRequestAndUnknownScopeAreRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => SteelMethodSelector.Select(null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelMethodSelector.Select(Input(), (SteelCheckScope)99));
    }

    [DataTestMethod]
    [DataRow("T", 1)] [DataRow("T", 2)] [DataRow("C", 1)] [DataRow("C", 2)]
    [DataRow("CHS", 1)] [DataRow("CHS", 2)] [DataRow("RHS", 1)] [DataRow("RHS", 2)]
    public void SimpleBendingUsesClassAndPrincipalModulusOfTheExistingFamilies(string family, int axis)
    {
        Section shape = family switch {
            "T" => new SectionT(120, 100, 20, 20, "T"),
            "C" => new SectionC(200, 12, 80, 15, 80, 15, "C"),
            "CHS" => new SectionCHS(150, 8),
            _ => new SectionRHS(200, 100, 10, 10, 10, 10, "RHS") };
        var input = Input(shape, m1: axis == 1 ? 1e6 : 0, m2: axis == 2 ? -1e6 : 0);
        var plan = SteelMethodSelector.Select(input);
        Assert.IsTrue(plan.IsReadyForExecution, Normal(plan).Message);
        Assert.AreEqual(axis == 1 ? SteelMethod.SectionBending1 : SteelMethod.SectionBending2, Normal(plan).Method);
    }

    [TestMethod]
    public void ExternalStateClassificationIsRetainedWithoutRelaxingMemberClass()
    {
        var input = Input(H(web: 5), m1: 1e6);
        var conservative = SteelMethodSelector.Select(input);
        Assert.AreEqual(4, Normal(conservative).SectionClass);
        var classified = SteelMethodSelector.Select(input, classification: new SteelSectionClassification(input, 3, "external actual bending-state classification"));
        Assert.AreEqual(Status.Ready, Normal(classified).Status);
        Assert.AreEqual(3, Normal(classified).SectionClass);
        var member = SteelMethodSelector.Select(input, SteelCheckScope.SectionAndMember,
            new SteelSectionClassification(input, 3, "external actual bending-state classification"));
        Assert.IsFalse(member.IsReadyForExecution);
        Assert.IsNull(member.Decisions.Single(d => d.Target == SteelCheckTarget.MemberStability).SectionClass);
    }

    [TestMethod]
    public void DerivedKnownModelShapeDoesNotInheritBaseCoverage()
    {
        var shape = new SectionHTaperFlange(300, 10, 150, 15, .1, 10, 5, 10, "taper H");
        var plan = SteelMethodSelector.Select(Input(shape, n: -1000));
        Assert.AreEqual("Geometry.NotQualified", Normal(plan).DiagnosticCode);
        Assert.IsFalse(plan.HasSelectedMethods);
    }

    [TestMethod]
    public void ManufacturingMetadataDoesNotEnableColdFormedOpenSections()
    {
        var section = new SteelSection(new SectionC(200, 12, 80, 15, 80, 15, "C"),
            new SteelMaterial("S235", 210000, 235, 360), Section.SectionTypes.Rolled, Section.FormedTypes.ColdFormed);
        var input = new SteelVerificationInput(new SteelSectionInput(section, true), new SteelActions(-1000, 0, 0, 0, 0, 0), Input().Code);
        Assert.AreEqual("ColdFormedOpenSection.NotQualified", Normal(SteelMethodSelector.Select(input)).DiagnosticCode);
        var hollow = new SteelSection(new SectionRHS(200, 100, 10, 10, 10, 10, "RHS"),
            section.SteelMaterial, Section.SectionTypes.Rolled, Section.FormedTypes.ColdFormed);
        var hollowInput = new SteelVerificationInput(new SteelSectionInput(hollow, true), input.Actions, input.Code);
        Assert.IsTrue(SteelMethodSelector.Select(hollowInput).IsReadyForExecution);
    }

    [DataTestMethod]
    [DataRow(SteelMethod.SectionTension)] [DataRow(SteelMethod.SectionCompression)]
    [DataRow(SteelMethod.SectionBending1)] [DataRow(SteelMethod.SectionBending2)]
    [DataRow(SteelMethod.Ec3PlasticHInteraction)] [DataRow(SteelMethod.NtcElasticNormalInteraction)]
    public void ReadyMethodHasTheInputsRequiredByItsExistingApi(SteelMethod expected)
    {
        bool interaction = expected is SteelMethod.Ec3PlasticHInteraction or SteelMethod.NtcElasticNormalInteraction;
        var input = Input(n: expected == SteelMethod.SectionTension ? 1000 : expected == SteelMethod.SectionCompression || interaction ? -1000 : 0,
            m1: expected == SteelMethod.SectionBending1 || interaction ? 1e6 : 0,
            m2: expected == SteelMethod.SectionBending2 || interaction ? -2e6 : 0,
            ntc: expected == SteelMethod.NtcElasticNormalInteraction);
        var plan = SteelMethodSelector.Select(input, classification: expected == SteelMethod.NtcElasticNormalInteraction
            ? new SteelSectionClassification(input, 1, "test preparation") : null);
        var decision = Normal(plan); var s = input.Section.CreateSectionCopy(); var a = input.Actions;
        Assert.AreEqual(expected, decision.Method);
        Assert.IsTrue(plan.IsReadyForExecution);
        switch (decision.Method)
        {
            case SteelMethod.SectionTension:
                Assert.IsTrue(SectionResistanceVerifier.Tension(input.Code.SectionCode, a.N, s.Area,
                    input.Section.YieldStrength, input.Section.UltimateStrength, input.Section.Unperforated, input.Section.NetArea).IsEvaluated);
                break;
            case SteelMethod.SectionCompression:
                Assert.IsTrue(SectionResistanceVerifier.Compression(input.Code.SectionCode, a.N, s.Area,
                    input.Section.YieldStrength, decision.SectionClass).IsEvaluated);
                break;
            case SteelMethod.SectionBending1:
            case SteelMethod.SectionBending2:
                bool axis1 = decision.Method == SteelMethod.SectionBending1;
                Assert.IsTrue(SectionResistanceVerifier.Bending(input.Code.SectionCode, axis1 ? a.M1 : a.M2,
                    axis1 ? s.Wpl1 : s.Wpl2, axis1 ? s.Wel1 : s.Wel2, input.Section.YieldStrength, decision.SectionClass).IsEvaluated);
                break;
            case SteelMethod.Ec3PlasticHInteraction:
                Assert.IsTrue(double.IsFinite(Ec3PlasticHSection.Evaluate((SectionH)s.SectionShape, decision.SectionClass!.Value,
                    input.Section.YieldStrength, input.Code.SectionCode.GammaM0, a.N, a.M1, a.M2).Utilization));
                break;
            case SteelMethod.NtcElasticNormalInteraction:
                Assert.IsTrue(double.IsFinite(Ntc2018SectionResistance.ElasticNormalInteraction(a.N, a.M1, a.M2,
                    s.Area, s.Wel1, s.Wel2, input.Section.YieldStrength, decision.SectionClass!.Value)));
                break;
            default: Assert.Fail("Unmapped ready method."); break;
        }
    }
}
