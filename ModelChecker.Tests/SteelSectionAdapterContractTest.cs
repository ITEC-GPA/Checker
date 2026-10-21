using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Compatibility;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Data.Steel;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class SteelSectionAdapterContractTest
{
    private static BeamActionInput Input(Section.SectionTypes type = Section.SectionTypes.Rolled, SectionH? shape = null)
    {
        var section = new SteelSection(shape ?? new SectionH(300, 12, 150, 18, 150, 18, "H300"), SteelMaterialEN1993Data.S355, type);
        var model = MixedModelFactory.Create(m => {
            var beam = m.BeamElements[250]; beam.BeamProperty = section;
            beam.Assignments.Sections.Clear();
            beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Property = section });
        });
        var sample = Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified);
        var prepared = BeamActionPreparation.Prepare(model, 250, sample, "Steel adapter contract");
        Assert.AreEqual(DataStatus.Ready, prepared.Status);
        var input = prepared.Input;
        input.Forces.N = input.Forces.V1 = input.Forces.V2 = input.Forces.T = input.Forces.M1 = input.Forces.M2 = 0;
        return input;
    }

    public static IEnumerable<object[]> ShearCases()
    {
        foreach (bool welded in new[] { false, true })
        foreach (int axis in new[] { 1, 2 })
        foreach (double ratio in new[] { -1.2, -1.0, -.5, 0, .5, 1.0, 1.2 })
            yield return new object[] { welded, axis, ratio };
    }

    [DataTestMethod]
    [DynamicData(nameof(ShearCases), DynamicDataSourceType.Method)]
    public void ShearPreservesIndependentRectangleCapacitiesAndExplicitDecisions(bool welded, int axis, double ratio)
    {
        var input = Input(welded ? Section.SectionTypes.Welded : Section.SectionTypes.Rolled);
        var checker = new SteelMaterialChecker(new StandardEN1993p11 { GammaM0 = 1.1 }, "2005");
        // H300 with sharp corners: Av1 = 2btf; Av2,welded = hw*tw;
        // Av2,rolled = A - 2btf + tw*tf. Geometry and arithmetic independent of the verifier.
        double av = axis == 1 ? 5400 : welded ? 3168 : 3384;
        double capacity = av * (355 / Math.Sqrt(3)) / 1.1;
        if (axis == 1) input.Forces.V1 = ratio * capacity; else input.Forces.V2 = ratio * capacity;
        var result = checker.CreateSession().Verify(input, CheckMechanism.Shear, default);
        Assert.AreEqual(ExecutionStatus.Completed, result.Execution);
        Assert.AreEqual(DataStatus.Ready, result.Data);
        Assert.AreEqual(Math.Abs(ratio) <= 1 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied, result.Outcome);
        Assert.AreEqual(Math.Abs(ratio), result.Utilization!.Value, 1e-12);
        var metric = result.Details.Metrics.Single(m => m.Key == "V" + axis);
        Assert.AreEqual(ratio * capacity, metric.Demand);
        Assert.AreEqual(capacity, metric.Capacity!.Value, capacity * 1e-12);
        Assert.AreEqual(Math.Abs(ratio) <= 1, metric.Passed);
        Assert.AreEqual("EN 1993-1-1:2005 6.2.6", metric.Reference);
        Assert.AreEqual(av, result.Details.Trace.Single(t => t.Key == "V" + axis + ".Av").Value);
    }

    [DataTestMethod]
    [DataRow(1, -1.0)] [DataRow(1, 1.0)] [DataRow(2, -1.0)] [DataRow(2, 1.0)]
    public void BendingAtResistanceRetainsSignedDemandAndPlasticModulus(int axis, double sign)
    {
        var input = Input();
        var checker = new SteelMaterialChecker(new StandardEN1993p11 { GammaM0 = 1.1 }, "2005", "", true);
        double w = axis == 1 ? 2 * 150 * 18 * 141 + 12 * 132 * 132 : 2 * 18 * 150 * 150 / 4 + 264 * 12 * 12 / 4;
        double capacity = w * 355 / 1.1;
        if (axis == 1) input.Forces.M1 = sign * capacity; else input.Forces.M2 = sign * capacity;
        var result = checker.CreateSession().Verify(input, CheckMechanism.UlsBiaxialSection, default);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
        Assert.AreEqual(1.0, result.Utilization!.Value, 1e-12);
        Assert.AreEqual(sign * capacity, result.Details.Metrics.Single(m => m.Key == "M" + axis).Demand);
        Assert.AreEqual(w, result.Details.Trace.Single(t => t.Key == "M" + axis + ".Wpl").Value!.Value, w * 1e-12);
        Assert.AreEqual("EN 1993-1-1:2005 6.2.5", result.Details.Metrics[0].Reference);
    }

    [DataTestMethod]
    [DataRow(-.5, true)] [DataRow(.5, true)] [DataRow(-.500001, false)] [DataRow(.500001, false)]
    public void BendingLowShearGateIncludesTheBoundaryForBothSigns(double ratio, bool qualified)
    {
        var input = Input(); input.Forces.M1 = 1e6;
        input.Forces.V2 = ratio * (3384 * (355 / Math.Sqrt(3)) / 1.1);
        var result = new SteelMaterialChecker(new StandardEN1993p11 { GammaM0 = 1.1 }, "2005", null, true)
            .CreateSession().Verify(input, CheckMechanism.UlsBiaxialSection, default);
        if (qualified) Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
        else AssertUnavailable(result, "SteelHighShearBendingNotQualified");
    }

    [DataTestMethod]
    [DataRow("V1", double.NaN)] [DataRow("V2", double.PositiveInfinity)]
    [DataRow("M1", double.NaN)] [DataRow("M2", double.NegativeInfinity)]
    public void NonFiniteDemandDoesNotBecomeAnEngineeringFailure(string axis, double value)
    {
        var input = Input();
        switch (axis) {
            case "V1": input.Forces.V1 = value; break;
            case "V2": input.Forces.V2 = value; break;
            case "M1": input.Forces.M1 = value; break;
            case "M2": input.Forces.M2 = value; break;
        }
        var result = new SteelMaterialChecker(new StandardEN1993p11(), "2005", null, true).CreateSession()
            .Verify(input, axis.StartsWith("M") ? CheckMechanism.UlsBiaxialSection : CheckMechanism.Shear, default);
        Assert.AreEqual(ExecutionStatus.NotExecuted, result.Execution);
        Assert.AreEqual(DataStatus.Insufficient, result.Data);
        AssertNoDecision(result, "SteelSection." + axis + ".DesignAction.NotFinite");
    }

    [DataTestMethod]
    [DataRow(0.0)] [DataRow(double.NaN)]
    public void InvalidMaterialStrengthIsDiagnosedBeforeClassification(double fy)
    {
        var input = Input();
        // Replace the material: its legacy Fyk setter rebuilds properties from the original stress-strain curve.
        ((SteelSection)input.Property).SteelMaterial = new GPC.Model.Materials.SteelMaterial("Invalid strength", 210000, fy, 510);
        Assert.AreEqual(fy, ((SteelSection)input.Property).SteelMaterial.Fyk);
        var result = new SteelMaterialChecker(new StandardEN1993p11(), "2005").CreateSession().Verify(input, CheckMechanism.Shear, default);
        Assert.AreEqual(DataStatus.Insufficient, result.Data);
        AssertNoDecision(result, "SteelSection.V1.Fy." + (double.IsNaN(fy) ? "NotFinite" : "NotPositive"));
    }

    [DataTestMethod]
    [DataRow(0, 0.0)] [DataRow(0, double.NaN)] [DataRow(0, double.PositiveInfinity)]
    [DataRow(2, 0.0)] [DataRow(2, -1.0)] [DataRow(2, double.NaN)] [DataRow(2, double.PositiveInfinity)]
    public void InvalidDeclaredCodeFactorsHaveAnExplicitDiagnostic(int factor, double value)
    {
        var code = new StandardEN1993p11();
        if (factor == 0) code.GammaM0 = value; else code.GammaM2 = value;
        var result = new SteelMaterialChecker(code, "2005").CreateSession().Verify(Input(), CheckMechanism.Shear, default);
        Assert.AreEqual(DataStatus.Insufficient, result.Data);
        AssertNoDecision(result, "InvalidGammaM" + factor);
    }

    [TestMethod]
    public void NumericalOverflowIsAnExecutionErrorWithNoResistanceVerdict()
    {
        var result = new SteelMaterialChecker(new StandardEN1993p11 { GammaM0 = 1e-308 }, "2005")
            .CreateSession().Verify(Input(), CheckMechanism.Shear, default);
        Assert.AreEqual(ExecutionStatus.Error, result.Execution);
        Assert.AreEqual(DataStatus.Ready, result.Data);
        AssertNoDecision(result, "SteelSection.V1.NumericalRange");
    }

    [TestMethod]
    public void MissingInputAndCancellationRetainTheirDistinctMeanings()
    {
        var session = new SteelMaterialChecker(new StandardEN1993p11(), "2005").CreateSession();
        var result = session.Verify(null!, CheckMechanism.Shear, default);
        Assert.AreEqual(DataStatus.Insufficient, result.Data);
        AssertNoDecision(result, "SteelSection.Input.Required");
        Assert.ThrowsException<OperationCanceledException>(() => session.Verify(null!, CheckMechanism.Shear, new CancellationToken(true)));
        Assert.AreEqual(0, session.CreatedCheckers);
    }

    [TestMethod]
    public void ReusedGeometryDoesNotReuseActionsOrResultsAndChangedFactorsInvalidateItsKey()
    {
        var code = new StandardEN1993p11 { GammaM0 = 1.1 };
        var session = new SteelMaterialChecker(code, "2005").CreateSession(); var input = Input();
        var first = session.Verify(input, CheckMechanism.Shear, default);
        double rd = first.Details.Metrics[0].Capacity!.Value;
        input.Forces.V1 = -1.2 * rd;
        var second = session.Verify(input, CheckMechanism.Shear, default);
        Assert.AreEqual(1, session.CreatedCheckers);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, second.Outcome);
        Assert.AreEqual(0.0, first.Utilization);
        code.GammaM0 = 2.2;
        var third = session.Verify(input, CheckMechanism.Shear, default);
        Assert.AreEqual(2, session.CreatedCheckers);
        Assert.AreEqual(2.4, third.Utilization!.Value, 1e-12);
        Assert.AreEqual(rd, first.Details.Metrics[0].Capacity);
    }

    [DataTestMethod]
    [DataRow("edition")] [DataRow("ntc")] [DataRow("stability")]
    [DataRow("asymmetric")] [DataRow("slender")]
    public void AdapterQualificationIsNotExpandedByTheMoreGeneralScalarApi(string kind)
    {
        var shape = kind == "asymmetric" ? new SectionH(300, 12, 150, 18, 140, 18, "asymmetric")
            : kind == "slender" ? new SectionH(1000, 4, 150, 18, 150, 18, "slender") : null;
        var code = kind == "ntc" ? new StandardNTC2018Steel() : new StandardEN1993p11();
        var checker = new SteelMaterialChecker(code, kind == "edition" ? "2022" : "2005", null, true);
        var result = checker.CreateSession().Verify(Input(shape: shape), kind == "stability" ? CheckMechanism.Stability : CheckMechanism.Shear, default);
        string reason = kind switch {
            "edition" => "SteelEditionNotQualified", "ntc" => "SteelStandardVariantNotQualified",
            "stability" => "SteelMethodNotQualified", "asymmetric" => "SteelAsymmetricSectionNotQualified",
            _ => "SteelSlenderSectionNotQualified"
        };
        AssertUnavailable(result, reason);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ReportRoundtripRetainsScalarTraceReferencesAndEvidence(bool bending)
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1];
        var checker = new SteelMaterialChecker(new StandardEN1993p11 { GammaM0 = 1.1 }, "2005", "explicit test factors", bending);
        job.Assignments[0].Checker = checker;
        job.Plan.SectionMechanisms = new[] { bending ? CheckMechanism.UlsBiaxialSection : CheckMechanism.Shear };
        request.Jobs.Clear(); request.Jobs.Add(job);
        var sample = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[20], MultiMaterialWorkflow.State("ULS")).Single();
        if (bending) sample.ResultBeamForces.M1 = -1e6; else sample.ResultBeamForces.V1 = -1.2e6;
        var report = new Service().Verify(model, request);
        var before = report.Jobs[0].Results.Single();
        Assert.AreEqual(bending ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied, before.Outcome);
        Assert.IsTrue(report.Summary.IsComplete);
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var after = CheckReportArchive.Load(stream)[0].Results.Single();
        Assert.IsTrue(after.HasUnchangedEvidence);
        Assert.AreEqual(before.MethodId, after.MethodId); Assert.AreEqual(before.EngineConfiguration, after.EngineConfiguration);
        Assert.AreEqual(before.Outcome, after.Outcome); Assert.AreEqual(before.Utilization, after.Utilization);
        CollectionAssert.AreEqual(before.Details.Trace.Select(t => (t.Key, t.Value, t.Unit, t.Expression)).ToArray(),
            after.Details.Trace.Select(t => (t.Key, t.Value, t.Unit, t.Expression)).ToArray());
        Assert.IsTrue(after.Details.Trace.Any(t => t.Key == "GammaM0"));
        Assert.IsTrue(after.Details.Trace.Any(t => t.Key == (bending ? "CompressionClass" : "Fy")));
        Assert.IsTrue(after.Details.Trace.Any(t => t.Key == (bending ? "M1.Wpl" : "V1.Av")));
        Assert.IsTrue(after.Details.Metrics.All(m => m.Reference != null && m.Passed.HasValue));
    }

    public static IEnumerable<object[]> CapabilityCases()
    {
        foreach (bool bending in new[] { false, true })
        foreach (bool ntc in new[] { false, true })
        foreach (string edition in new[] { "2005", "2022" })
        foreach (var mechanism in new[] { CheckMechanism.Shear, CheckMechanism.UlsBiaxialSection, CheckMechanism.Stability })
            yield return new object[] { bending, ntc, edition, mechanism };
    }

    [DataTestMethod]
    [DynamicData(nameof(CapabilityCases), DynamicDataSourceType.Method)]
    public void CatalogAndRuntimeUseTheSameNormAndMechanismQualification(bool bending, bool ntc, string edition, CheckMechanism mechanism)
    {
        var (_, request) = MultiMaterialWorkflow.Create();
        var config = ConfiguredPlateWorkflowTest.BeamConfiguration(request);
        var definition = config.Engines[1]; var context = config.Contexts[1];
        definition.Kind = bending ? "Steel.EN1993.Section" : "Steel.EN1993.PlasticShear";
        context.Code = ntc ? new StandardNTC2018Steel() : new StandardEN1993p11(); context.Edition = edition;
        bool expected = !ntc && edition == "2005" && (mechanism == CheckMechanism.Shear || bending && mechanism == CheckMechanism.UlsBiaxialSection);
        var catalog = GPC.Model.Checker.Configuration.EngineCatalog.BuiltIn();
        Assert.AreEqual(expected, catalog.Supports(definition, context, mechanism));
        var checker = (SteelMaterialChecker)catalog.Compile(config).Jobs[1].Assignments[0].Checker;
        Assert.AreEqual(definition.Kind, checker.Id); Assert.AreEqual(bending, checker.IncludeUniaxialBending);
        var result = checker.CreateSession().Verify(Input(), mechanism, default);
        Assert.AreEqual(expected ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotEvaluated, result.Outcome);
        Assert.AreEqual(expected ? DataStatus.Ready : DataStatus.NotSupported, result.Data);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ScalarClassificationTraceIdentifiesPartsAndClearDimensions(bool bending)
    {
        var result = new SteelMaterialChecker(new StandardEN1993p11(), "2005", null, bending).CreateSession()
            .Verify(Input(), bending ? CheckMechanism.UlsBiaxialSection : CheckMechanism.Shear, default);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
        var trace = result.Details.Trace;
        Assert.AreEqual(1d, trace.Single(v => v.Key == "CompressionClass").Value);
        Assert.AreEqual(264d, trace.Single(v => v.Key == "Classification.Web.ClearWidth").Value);
        Assert.AreEqual(22d, trace.Single(v => v.Key == "Classification.Web.Ratio").Value);
        Assert.AreEqual(69d, trace.Single(v => v.Key == "Classification.TopFlange.ClearWidth").Value);
        Assert.AreEqual(69d, trace.Single(v => v.Key == "Classification.BottomFlange.ClearWidth").Value);
        Assert.AreEqual(3, trace.Count(v => v.Key.StartsWith("Classification.") && v.Key.EndsWith(".Class")));
    }

    [DataTestMethod]
    [DataRow(33d, 1, true)] [DataRow(38d, 2, true)]
    [DataRow(38.000001, 3, false)] [DataRow(42.000001, 4, false)]
    public void ClassificationMigrationPreservesTheStockinessGateAtTable52Boundaries(double webRatio, int expected, bool qualified)
    {
        // fy235=>epsilon1; web is the governing part of this sharp H.
        var input = Input(shape: new SectionH(webRatio * 12 + 36, 12, 150, 18, 150, 18, "boundary"));
        ((SteelSection)input.Property).SteelMaterial = SteelMaterialEN1993Data.S235;
        var result = new SteelMaterialChecker(new StandardEN1993p11(), "2005").CreateSession().Verify(input, CheckMechanism.Shear, default);
        if (qualified)
        {
            Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
            Assert.AreEqual((double)expected, result.Details.Trace.Single(v => v.Key == "CompressionClass").Value);
        }
        else AssertUnavailable(result, "SteelSlenderSectionNotQualified");
    }

    private static void AssertUnavailable(CheckResult result, string code)
    { Assert.AreEqual(DataStatus.NotSupported, result.Data); AssertNoDecision(result, code); }
    private static void AssertNoDecision(CheckResult result, string code)
    {
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome); Assert.IsNull(result.Utilization);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == code), string.Join(";", result.Diagnostics.Select(d => d.Code)));
        Assert.IsNull(result.Details);
    }
}
