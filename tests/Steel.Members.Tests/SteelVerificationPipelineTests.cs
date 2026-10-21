using System.Collections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Checking.Contracts;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Status = GPC.Checkers.Steel.Pipeline.SteelExecutionStatus;

namespace Steel.Members.Tests;

[TestClass]
public class SteelVerificationPipelineTests
{
    private static SectionH H(double web = 12, double radius = 0)
        => new(300, web, 150, 12, 150, 12, "H", radius);
    private static SteelVerificationInput Input(Section? shape = null, double n = 0, double m1 = 0, double m2 = 0,
        double v1 = 0, double v2 = 0, double t = 0, bool ntc = false, bool? unperforated = true,
        double? netArea = null, double gamma0 = 1, double fu = 360, SteelMemberInput? member = null, double fy = 235)
        => new(new SteelSectionInput(new SteelSection(shape ?? H(), new SteelMaterial("test material", 210000, fy, fu)),
            unperforated, netArea), new SteelActions(n, v1, v2, t, m1, m2),
            ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(gamma0, 1.25, "test")), member);
    private static SteelSelectionPlan Plan(SteelVerificationInput input, int? cl = null, SteelCheckScope scope = SteelCheckScope.Section)
        => SteelMethodSelector.Select(input, scope, cl.HasValue ? new SteelSectionClassification(input, cl.Value, "Explicit test classification") : null);
    private static SteelCheckExecution Normal(SteelVerificationResult result)
        => result.Checks.Single(c => c.Decision.Target == SteelCheckTarget.NormalResistance);
    private static SteelCheckExecution Shear(SteelVerificationResult result, int axis)
        => result.Checks.Single(c => c.Decision.Target == (axis == 1 ? SteelCheckTarget.Shear1 : SteelCheckTarget.Shear2));
    private static void Close(double expected, double actual)
        => Assert.AreEqual(expected, actual, Math.Max(1e-10, Math.Abs(expected) * 1e-10));
    private static void NoNumbers(SteelCheckExecution check)
    { Assert.IsNull(check.Resistance); Assert.IsNull(check.Utilization); Assert.IsFalse(check.IsEvaluated); }

    [DataTestMethod]
    [DataRow(false, 1000d)] [DataRow(false, 2000000d)]
    [DataRow(true, 1000d)] [DataRow(true, 2000000d)]
    public void TensionExecutesWithCodeFactorsAndFiniteTrace(bool ntc, double n)
    {
        var input = Input(n: n, ntc: ntc); var plan = Plan(input);
        var result = SteelVerificationPipeline.Execute(plan); var check = Normal(result);
        double resistance = 6912 * 235 / (ntc ? 1.05 : 1);
        Close(resistance, check.Resistance!.Value); Close(n / resistance, check.Utilization!.Value);
        Assert.AreEqual(n <= resistance ? Status.Passed : Status.Failed, check.Status);
        Assert.AreEqual(n <= resistance ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied, result.Outcome);
        Assert.AreEqual("N", check.ResistanceUnit); Assert.IsTrue(result.IsComplete);
        Assert.AreSame(plan, result.Plan); Assert.AreSame(plan.Decisions[0], check.Decision);
        Assert.IsTrue(check.Trace.All(v => double.IsFinite(v.Value)));
        StringAssert.Contains(check.Reference, ntc ? "NTC 2018" : "EN 1993");
        CollectionAssert.AreEqual(plan.Decisions.ToArray(), result.Checks.Select(c => c.Decision).ToArray());
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void NetAreaRemainsPartOfTheExecutedTensionCheck(bool ntc)
    {
        var result = SteelVerificationPipeline.Execute(Plan(Input(n: 600000, ntc: ntc, unperforated: false, netArea: 2000)));
        Close(.9 * 2000 * 360 / 1.25, Normal(result).Resistance!.Value);
        Assert.AreEqual(Status.Failed, Normal(result).Status);
        Close(2000, Normal(result).Trace.Single(v => v.Name == "NetArea").Value);
    }

    public static IEnumerable<object[]> CompressionCases()
    {
        foreach (var family in SteelMethodSelectorTests.CompressionFamilies())
        foreach (bool ntc in new[] { false, true })
            yield return new[] { family[0], (object)ntc };
    }
    [DataTestMethod, DynamicData(nameof(CompressionCases), DynamicDataSourceType.Method)]
    public void CompressionRunsForEveryMappedFamilyAndBothCodes(Section shape, bool ntc)
    {
        var input = Input(shape, n: -10000, ntc: ntc);
        var result = SteelVerificationPipeline.Execute(Plan(input, ntc ? 2 : null));
        Assert.AreEqual(Status.Passed, Normal(result).Status, Normal(result).Message);
        Close(input.Section.Area * 235 / (ntc ? 1.05 : 1), Normal(result).Resistance!.Value);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
        Assert.IsTrue(result.IsComplete);
    }

    [DataTestMethod]
    [DataRow(1, 1, false)] [DataRow(2, 1, false)] [DataRow(1, 3, false)] [DataRow(2, 3, false)]
    [DataRow(1, 1, true)] [DataRow(2, 1, true)] [DataRow(1, 3, true)] [DataRow(2, 3, true)]
    public void BendingUsesSelectedAxisAndPlasticOrElasticModulus(int axis, int cl, bool ntc)
    {
        var input = Input(m1: axis == 1 ? -1e6 : 0, m2: axis == 2 ? 1e6 : 0, ntc: ntc);
        // Independent rectangle integration for this sharp symmetric H.
        double wpl = axis == 1 ? 746928 : 144936;
        double inertia = axis == 1
            ? 2 * (150 * Math.Pow(12, 3) / 12 + 150 * 12 * 144 * 144) + 12 * Math.Pow(276, 3) / 12
            : 2 * 12 * Math.Pow(150, 3) / 12 + 276 * Math.Pow(12, 3) / 12;
        double modulus = cl < 3 ? wpl : inertia / (axis == 1 ? 150 : 75);
        var result = SteelVerificationPipeline.Execute(Plan(input, cl));
        var check = Normal(result);
        Assert.AreEqual(Status.Passed, check.Status);
        Close(modulus * 235 / (ntc ? 1.05 : 1), check.Resistance!.Value);
        Assert.AreEqual("Nmm", check.ResistanceUnit);
        Assert.AreEqual(cl < 3 ? "Wpl" : "WelMin", check.Trace.Single(v => v.Unit == "mm3").Name);
    }

    [DataTestMethod]
    [DataRow(-1000d, 1e6, 2e6)] [DataRow(1000d, -1e6, -2e6)]
    [DataRow(-1000d, 1e10, 0d)] [DataRow(0d, 1e6, -2e6)]
    public void PlasticHDispatchPreservesFixedNInteraction(double n, double m1, double m2)
    {
        var input = Input(n: n, m1: m1, m2: m2);
        var expected = Ec3PlasticHSection.Evaluate(H(), 1, 235, 1, n, m1, m2);
        var result = SteelVerificationPipeline.Execute(Plan(input, 1)); var check = Normal(result);
        Close(expected.Utilization, check.Utilization!.Value);
        Assert.AreEqual(expected.IsSatisfied ? Status.Passed : Status.Failed, check.Status);
        Assert.IsNull(check.Resistance); Assert.IsNull(check.ResistanceUnit);
        StringAssert.Contains(check.Scope, "fixed N");
        Close(expected.Moment1Rd, check.Trace.Single(v => v.Name == "Moment1RdAtFixedN").Value);
    }

    [DataTestMethod]
    [DataRow(1d)] [DataRow(-1d)] [DataRow(1.01)] [DataRow(-1.01)]
    public void ExhaustedAxialCapacityRemainsFailureWithoutInfinityInReport(double factor)
    {
        var input = Input(n: factor * 6912 * 235, m1: 1000);
        var result = SteelVerificationPipeline.Execute(Plan(input, 1)); var check = Normal(result);
        Assert.AreEqual(Status.Failed, check.Status); Assert.IsTrue(check.IsEvaluated);
        Assert.AreEqual("PlasticH.NoRemainingMomentCapacity", check.DiagnosticCode);
        Assert.IsNull(check.Utilization); Assert.IsNull(check.Resistance);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, result.Outcome); Assert.IsTrue(result.IsComplete);
        Assert.IsTrue(check.Trace.All(v => double.IsFinite(v.Value)));
        Close(0, check.Trace.Single(v => v.Name == "Moment1RdAtFixedN").Value);
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void NtcInteractionReportsTheConservativeElasticBound(int cl)
    {
        var input = Input(n: -1000, m1: 1e6, m2: -2e6, ntc: true);
        var s = input.Section.CreateSectionCopy();
        double expected = (1000 / input.Section.Area + 1e6 / s.Wel1 + 2e6 / s.Wel2) * 1.05 / 235;
        var result = SteelVerificationPipeline.Execute(Plan(input, cl)); var check = Normal(result);
        Close(expected, check.Utilization!.Value); Assert.IsNull(check.Resistance);
        StringAssert.Contains(check.Scope, "Conservative elastic"); StringAssert.Contains(check.Reference, "NTC");
        Assert.AreEqual(Status.Passed, check.Status);
    }

    [DataTestMethod]
    [DataRow(1, false)] [DataRow(2, false)] [DataRow(1, true)] [DataRow(2, true)]
    public void ShearWaitsForItsOwnDirectionAndUsesTheNormSpecificArea(int axis, bool ntc)
    {
        // T has different EC3/NTC Av2; axis 1 remains horizontal for this geometry.
        var input = Input(new SectionT(120, 100, 20, 20, "T"), v1: axis == 1 ? -1000 : 0, v2: axis == 2 ? 1000 : 0, ntc: ntc);
        var plan = Plan(input);
        var waiting = SteelVerificationPipeline.Execute(plan);
        Assert.AreEqual(Status.RequiresPreparation, Shear(waiting, axis).Status); NoNumbers(Shear(waiting, axis));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, waiting.Outcome);
        var evidence = SteelShearBucklingAssessment.FromExternal(input, axis, true, "Qualified panel assessment");
        var completed = SteelVerificationPipeline.Execute(plan, new[] { evidence }); var check = Shear(completed, axis);
        var s = input.Section.CreateSectionCopy();
        double area = axis == 1 ? ModelShearAreas.GetArea1(s) : ModelShearAreas.GetArea2(s, input.Code.SectionCode.Standard);
        Close(area * 235 / (Math.Sqrt(3) * (ntc ? 1.05 : 1)), check.Resistance!.Value);
        Assert.AreEqual(Status.Passed, check.Status); Assert.IsTrue(completed.IsComplete);
        Assert.AreSame(evidence, check.ShearAssessments.Single());
        Assert.AreEqual(SteelSelectionStatus.RequiresPreparation, check.Decision.Status); // Plan is unchanged.
    }

    [TestMethod]
    public void Ec3WebPreparationUnlocksBendingAndItsShearTogether()
    {
        var input = Input(m1: 1e6, v2: 1000); var plan = Plan(input);
        var evidence = SteelShearBucklingAssessment.EvaluateEc3HWeb(input, 1.2, "Explicit eta source");
        var result = SteelVerificationPipeline.Execute(plan, new[] { evidence });
        Assert.IsTrue(evidence.IsBucklingExcluded); Assert.AreEqual(2, evidence.Axis);
        Assert.AreEqual(Status.Passed, Normal(result).Status); Assert.AreEqual(Status.Passed, Shear(result, 2).Status);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
        Assert.AreSame(evidence, Normal(result).ShearAssessments.Single());
        Assert.IsFalse(plan.IsReadyForExecution);
    }

    [TestMethod]
    public void PlasticHInteractionReceivesItsActualLowShearRatio()
    {
        var input = Input(n: -1000, m1: 1e6, v2: 1000);
        var result = SteelVerificationPipeline.Execute(Plan(input), new[] { SteelShearBucklingAssessment.EvaluateEc3HWeb(input, 1, "eta=1") });
        Assert.AreEqual(Status.Passed, Normal(result).Status);
        Close(Shear(result, 2).Utilization!.Value, Normal(result).Trace.Single(v => v.Name == "ShearRatio2").Value);
    }

    [TestMethod]
    public void ClearWebHeightIncludesFilletZonesAndSlenderWebBlocksPlasticResistance()
    {
        // hw=276, tw=4: hw/t=69 >60 for eta=1.2. Removing two r=20 zones would incorrectly give 59.
        var input = Input(H(web: 4, radius: 20), m1: 1000, v2: 1000);
        var evidence = SteelShearBucklingAssessment.EvaluateEc3HWeb(input, 1.2, "Explicit eta");
        Assert.IsFalse(evidence.IsBucklingExcluded);
        Close(276, evidence.Trace.Single(v => v.Name == "ClearWebHeight").Value);
        var result = SteelVerificationPipeline.Execute(Plan(input, 3), new[] { evidence });
        foreach (var check in new[] { Normal(result), Shear(result, 2) })
        {
            Assert.AreEqual(Status.NotSupported, check.Status);
            Assert.AreEqual("ShearBuckling.ResistanceMethodRequired", check.DiagnosticCode); NoNumbers(check);
        }
        Assert.IsFalse(result.IsComplete); Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
    }

    [TestMethod]
    public void WrongDirectionEvidenceCannotUnlockNormalOrShearCheck()
    {
        var input = Input(m1: 1000, v1: 1000);
        var result = SteelVerificationPipeline.Execute(Plan(input), new[] { SteelShearBucklingAssessment.EvaluateEc3HWeb(input, 1, "eta") });
        Assert.AreEqual(Status.RequiresPreparation, Normal(result).Status);
        Assert.AreEqual(Status.RequiresPreparation, Shear(result, 1).Status);
        Assert.AreEqual(0, Normal(result).ShearAssessments.Count);
    }

    [TestMethod]
    public void BiaxialShearRequiresBothAssessmentsAndStillDoesNotBecomeACompleteCheck()
    {
        var input = Input(m1: 1000, v1: 1000, v2: 1000); var plan = Plan(input);
        var one = SteelShearBucklingAssessment.FromExternal(input, 1, true, "axis1");
        var two = SteelShearBucklingAssessment.FromExternal(input, 2, true, "axis2");
        Assert.AreEqual(Status.RequiresPreparation, Normal(SteelVerificationPipeline.Execute(plan, new[] { one })).Status);
        var result = SteelVerificationPipeline.Execute(plan, new[] { one, two });
        Assert.AreEqual(Status.Passed, Normal(result).Status);
        Assert.AreEqual(Status.NotSupported, Shear(result, 1).Status); Assert.AreEqual(Status.NotSupported, Shear(result, 2).Status);
        Assert.IsFalse(result.IsComplete); Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
    }

    [TestMethod]
    public void ShearBeyondPlasticCapacityFailsTheNewCombinedMethod()
    {
        var input = Input(m1: 1000, v2: 1e9);
        var result = SteelVerificationPipeline.Execute(Plan(input), new[] { SteelShearBucklingAssessment.EvaluateEc3HWeb(input, 1, "eta") });
        Assert.AreEqual("HighShear.PlasticShearExceeded", Normal(result).DiagnosticCode);
        Assert.AreEqual(Status.Failed, Normal(result).Status); Assert.IsNull(Normal(result).Resistance); Assert.IsNull(Normal(result).Utilization);
        Assert.AreEqual(Status.Failed, Shear(result, 2).Status);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, result.Outcome); Assert.IsTrue(result.IsComplete);
    }

    [DataTestMethod]
    [DataRow(0d)] [DataRow(-1d)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidEtaDoesNotCreateAPassingAssessment(double eta)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelShearBucklingAssessment.EvaluateEc3HWeb(Input(v2: 1000), eta, "eta"));

    [TestMethod]
    public void NativeAssessmentNeverCrossesCodeOrGeometryBoundary()
    {
        Assert.ThrowsException<NotSupportedException>(() => SteelShearBucklingAssessment.EvaluateEc3HWeb(Input(ntc: true, v2: 1000), 1, "eta"));
        Assert.ThrowsException<NotSupportedException>(() => SteelShearBucklingAssessment.EvaluateEc3HWeb(Input(new SectionCHS(150, 8), v2: 1000), 1, "eta"));
    }

    [TestMethod]
    public void OverflowingAssessmentThresholdIsRejected()
        => Assert.ThrowsException<ArithmeticException>(() => SteelShearBucklingAssessment.EvaluateEc3HWeb(Input(v2: 1000), double.Epsilon, "extreme eta"));

    [TestMethod]
    public void ExternalNegativeAssessmentBlocksNtcShearWithoutEc3Fallback()
    {
        var input = Input(v2: 1000, ntc: true);
        var evidence = SteelShearBucklingAssessment.FromExternal(input, 2, false, "NTC panel assessment requires buckling resistance");
        var result = SteelVerificationPipeline.Execute(Plan(input), new[] { evidence });
        Assert.AreEqual(Status.NotSupported, Shear(result, 2).Status); NoNumbers(Shear(result, 2));
        Assert.AreSame(evidence, Shear(result, 2).ShearAssessments.Single());
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
    }

    [TestMethod]
    public void AssessmentsAreBoundToTheExactInputAndRejectDuplicatesOrNullItems()
    {
        var input = Input(v2: 1000); var plan = Plan(input);
        var evidence = SteelShearBucklingAssessment.FromExternal(input, 2, true, "source");
        Assert.ThrowsException<ArgumentException>(() => SteelVerificationPipeline.Execute(Plan(Input(v2: 1000)), new[] { evidence }));
        Assert.ThrowsException<ArgumentException>(() => SteelVerificationPipeline.Execute(plan, new[] { evidence, evidence }));
        Assert.ThrowsException<ArgumentException>(() => SteelVerificationPipeline.Execute(plan, new SteelShearBucklingAssessment[] { null! }));
        Assert.ThrowsException<ArgumentException>(() => SteelVerificationPipeline.Execute(plan, new[] {
            evidence, SteelShearBucklingAssessment.FromExternal(input, 2, false, "negative assessment") }));
    }

    [TestMethod]
    public void EvidenceRequiresARealInputDirectionAndSource()
    {
        var input = Input(v2: 1000);
        Assert.ThrowsException<ArgumentNullException>(() => SteelShearBucklingAssessment.FromExternal(null!, 2, true, "source"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelShearBucklingAssessment.FromExternal(input, 3, true, "source"));
        Assert.ThrowsException<ArgumentException>(() => SteelShearBucklingAssessment.FromExternal(input, 2, true, " "));
        Assert.ThrowsException<ArgumentException>(() => SteelShearBucklingAssessment.EvaluateEc3HWeb(input, 1, " "));
    }

    [TestMethod]
    public void EmptyLoadCaseIsNotASuccessfulVerification()
    {
        var result = SteelVerificationPipeline.Execute(Plan(Input()));
        Assert.IsTrue(result.Checks.All(c => c.Status == Status.NotRequired));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome); Assert.IsFalse(result.IsComplete);
        foreach (var check in result.Checks) NoNumbers(check);
    }

    [DataTestMethod]
    [DataRow(1000d, EngineeringOutcome.NotEvaluated)] [DataRow(1e9, EngineeringOutcome.NotSatisfied)]
    public void SectionExecutionDoesNotHideUnavailableMemberCheck(double n, EngineeringOutcome expected)
    {
        var result = SteelVerificationPipeline.Execute(Plan(Input(n: n), scope: SteelCheckScope.SectionAndMember));
        Assert.IsTrue(Normal(result).IsEvaluated); Assert.AreEqual(5, result.Checks.Count);
        var member = result.Checks.Single(c => c.Decision.Target == SteelCheckTarget.MemberStability);
        Assert.AreEqual(Status.MissingInput, member.Status); NoNumbers(member);
        Assert.AreEqual(expected, result.Outcome); Assert.IsFalse(result.IsComplete);
        CollectionAssert.Contains(member.Decision.MissingInputs.ToArray(), "Member");
    }

    [TestMethod]
    public void MemberScopeDoesNotSecretlyExecuteSectionChecks()
    {
        var input = Input(n: -1000, member: new SteelMemberInput(3000), ntc: true);
        var result = SteelVerificationPipeline.Execute(Plan(input, scope: SteelCheckScope.Member));
        Assert.AreEqual(1, result.Checks.Count); Assert.AreEqual(Status.NotSupported, result.Checks.Single().Status);
        Assert.AreEqual("MemberRouting.NotIntegrated", result.Checks.Single().DiagnosticCode);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
    }

    [TestMethod]
    public void UnresolvedSelectionReasonsSurviveExecutionWithoutNumericalResults()
    {
        var cases = new[] {
            (Input(n: 1000, unperforated: null), (int?)null, Status.MissingInput),
            (Input(n: -1000, ntc: true, unperforated: null), (int?)null, Status.MissingInput),
            (Input(n: -1000), (int?)4, Status.NotSupported),
            (Input(n: 1000, fu: 200), (int?)null, Status.InvalidInput),
            (Input(n: 1000, t: 100), (int?)null, Status.NotSupported)
        };
        foreach (var (input, cl, status) in cases)
        {
            var plan = Plan(input, cl); var result = SteelVerificationPipeline.Execute(plan);
            Assert.AreEqual(status, Normal(result).Status); NoNumbers(Normal(result));
            Assert.AreEqual(plan.Decisions[0].DiagnosticCode, Normal(result).DiagnosticCode);
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
        }
    }

    [TestMethod]
    public void PrimitiveNumericalFailureIsNotConvertedToPass()
    {
        var result = SteelVerificationPipeline.Execute(Plan(Input(n: 1000, gamma0: double.Epsilon)));
        Assert.AreEqual(Status.CalculationError, Normal(result).Status); NoNumbers(Normal(result));
        Assert.AreEqual("NumericalRange", Normal(result).DiagnosticCode);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
    }

    [TestMethod]
    public void NtcInteractionNumericalFailureIsReportedAsCalculationError()
    {
        var result = SteelVerificationPipeline.Execute(Plan(Input(n: 1e308, m1: 1000, ntc: true, fy: 1e-300), 1));
        Assert.AreEqual(Status.CalculationError, Normal(result).Status); NoNumbers(Normal(result));
        Assert.AreEqual("Execution.NumericalRange", Normal(result).DiagnosticCode);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
    }

    [TestMethod]
    public void CancellationBeforeExecutionPropagatesWithoutResult()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => SteelVerificationPipeline.Execute(Plan(Input(n: 1000)), cancellationToken: cts.Token));
        Assert.ThrowsException<ArgumentNullException>(() => SteelVerificationPipeline.Execute(null!));
    }

    [TestMethod]
    public void CancellationDuringPreparationIsObserved()
    {
        using var cts = new CancellationTokenSource(); var input = Input(v2: 1000);
        IEnumerable<SteelShearBucklingAssessment> Evidence()
        {
            cts.Cancel();
            yield return SteelShearBucklingAssessment.FromExternal(input, 2, true, "source");
        }
        Assert.ThrowsException<OperationCanceledException>(() => SteelVerificationPipeline.Execute(Plan(input), Evidence(), cts.Token));
    }

    [TestMethod]
    public void ResultListsAreImmutableAndRepeatedExecutionHasNoSharedMutableState()
    {
        var input = Input(v2: 1000); var plan = Plan(input);
        var evidence = SteelShearBucklingAssessment.EvaluateEc3HWeb(input, 1, "eta");
        var list = new List<SteelShearBucklingAssessment> { evidence };
        var first = SteelVerificationPipeline.Execute(plan, list); list.Clear();
        var second = SteelVerificationPipeline.Execute(plan, new[] { evidence });
        Close(Shear(first, 2).Resistance!.Value, Shear(second, 2).Resistance!.Value);
        Assert.AreNotSame(Shear(first, 2), Shear(second, 2));
        Assert.ThrowsException<NotSupportedException>(() => ((IList)first.Checks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList)Shear(first, 2).Trace).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList)Shear(first, 2).ShearAssessments).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList)evidence.Trace).Clear());
        Assert.AreEqual(1, Shear(first, 2).ShearAssessments.Count);
        Assert.AreEqual(SteelSelectionStatus.RequiresPreparation, plan.Decisions.Single(d => d.Target == SteelCheckTarget.Shear2).Status);
    }
}
