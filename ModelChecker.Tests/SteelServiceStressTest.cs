using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Compatibility;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
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

/// <summary>Serviceability stress limits of the steel section pipeline: typed task Serviceability/StressLimits on
/// Characteristic selections, explicit gammaM,ser and sources, hand-checked H300 stresses.</summary>
[TestClass]
public class SteelServiceStressTest
{
    private static readonly SectionCheckSpecification Characteristic = SectionCheckSpecification.StressLimits(CombinationCategory.Characteristic);
    // H300: h 300, web 12, flanges 150 x 18, no fillets. Gross A = 8568, Ixx = 2(150*18^3/12 + 2700*141^2) + 12*264^3/12.
    private const double A = 8568, Ixx = 2 * (150 * 18.0 * 18 * 18 / 12 + 2700 * 141.0 * 141) + 12 * 264.0 * 264 * 264 / 12;
    // Thin-walled midline model: flanges at +-141, web line 282 long.
    private const double IShear = 2 * 150 * 18 * 141.0 * 141 + 12 * 282.0 * 282 * 282 / 12;
    private static double WebS(double y) => 150 * 18 * 141 + 12 * (141.0 * 141 - y * y) / 2;

    private static Dictionary<string, string> Options(bool ntc = false) => new()
    {
        ["Unperforated"] = "true",
        ["ServiceStress.GammaMSer"] = ntc ? "1.05" : "1.0",
        ["ServiceStress.GammaMSerSource"] = ntc ? "Project specification S-01 par. 4.2" : "EN 1993-2:2006 7.3(1) note, recommended value",
        ["ServiceStress.ApplicabilitySource"] = ntc ? "Railway bridge: project requirement (no NTC text assumed)" : "Road bridge girder, EN 1993-2 7.3"
    };
    private static SteelPipelineMaterialChecker Engine(Dictionary<string, string>? options = null, bool ntc = false)
        => new(ntc ? new StandardNTC2018Steel() : new StandardEN1993p11(), ntc ? "2018" : "2005", parameters: options ?? Options(ntc));
    private static BeamActionInput Input()
    {
        var section = new SteelSection(new SectionH(300, 12, 150, 18, 150, 18, "H300"), SteelMaterialEN1993Data.S355);
        var model = MixedModelFactory.Create(m => {
            var beam = m.BeamElements[250]; beam.BeamProperty = section;
            beam.Assignments.Sections.Clear();
            beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Property = section });
        });
        var sample = Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified);
        var prepared = BeamActionPreparation.Prepare(model, 250, sample, "Steel service stress");
        Assert.AreEqual(DataStatus.Ready, prepared.Status);
        var input = prepared.Input;
        input.Forces.N = input.Forces.V1 = input.Forces.V2 = input.Forces.T = input.Forces.M1 = input.Forces.M2 = 0;
        return input;
    }
    private static CheckResult Run(BeamActionInput input, SteelPipelineMaterialChecker? engine = null, SectionCheckSpecification? check = null)
        => ((ISectionCheckSession)(engine ?? Engine()).CreateSession()).Verify(input, check ?? Characteristic, default);
    private static void Close(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Abs(expected) * 1e-9);

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void MajorBendingAndShearOfAnHAreLimitedAtTheWebJunction(bool ntc)
    {
        // M1 = 100 kNm, V2 = 200 kN: sigma 1e8*150/Ixx = 119.14 MPa at the flanges, tau 2e5 S(0)/(I' 12) = 64.21 MPa at the
        // centroid; with N = 0 the equivalent stress peaks at a web end, y = 132: sigma 104.84, tau 50.78 -> 136.85 MPa.
        var input = Input(); input.Forces.M1 = 1e8; input.Forces.V2 = 2e5;
        var result = Run(input, Engine(ntc: ntc));
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, string.Join(";", result.Diagnostics.Select(d => d.Code + d.Message)));
        var details = (ServiceabilityCheckDetails)result.Details!;
        Assert.AreEqual("Characteristic", details.CombinationCategory); Assert.AreEqual("Steel.ServiceStress", details.MethodId);
        Assert.AreEqual("stress/limit", details.UtilizationDefinition);
        double limit = 355 / (ntc ? 1.05 : 1), sigmaJ = 1e8 * 132 / Ixx, tauJ = 2e5 * WebS(132) / (IShear * 12);
        var normal = details.Metrics.Single(m => m.Key == "NormalStress");
        Close(1e8 * 150 / Ixx, Math.Abs(normal.Demand!.Value)); Close(limit, normal.Capacity!.Value); Assert.AreEqual(119.14, Math.Abs(normal.Demand.Value), 1e-2);
        var shear = details.Metrics.Single(m => m.Key == "ShearStress");
        Close(2e5 * WebS(0) / (IShear * 12), shear.Demand!.Value); Close(limit / Math.Sqrt(3), shear.Capacity!.Value); Assert.AreEqual(64.21, shear.Demand.Value, 1e-2);
        var equivalent = details.Metrics.Single(m => m.Key == "EquivalentStress");
        Close(Math.Sqrt(sigmaJ * sigmaJ + 3 * tauJ * tauJ), equivalent.Demand!.Value); Assert.AreEqual(136.85, equivalent.Demand.Value, 1e-2);
        Close(equivalent.Demand.Value / limit, result.Utilization!.Value);
        var point = details.Points.Single(p => p.Key == "EquivalentStress");
        Assert.AreEqual(132, Math.Abs(point.Y), 1e-9); Assert.AreEqual(6, Math.Abs(point.X), 1e-9); Assert.AreEqual("SectionGeometryCentroidal", point.Frame);
        Assert.IsTrue(details.Trace.Any(t => t.Key == "ServiceStress.GammaMSerSource" && t.Expression == Options(ntc)["ServiceStress.GammaMSerSource"]));
        Assert.IsTrue(details.Trace.Any(t => t.Key == "ServiceStress.ApplicabilitySource" && t.Expression == Options(ntc)["ServiceStress.ApplicabilitySource"]));
        Assert.IsTrue(details.Trace.Any(t => t.Key == "ServiceStress.Reference" && t.Expression!.Contains(ntc ? "no NTC 2018/Circolare 2019 clause verified" : "EN 1993-2:2006 7.3(1)")));
        Assert.AreEqual(details.Trace.Count, details.Trace.Select(t => t.Key).Distinct().Count());
        Assert.IsTrue(details.Trace.All(t => !t.Value.HasValue || double.IsFinite(t.Value.Value)));
    }

    [TestMethod]
    public void OnlyTheCharacteristicStressTaskIsSupportedAndRegistered()
    {
        var engine = Engine(); var session = (ISectionCheckSession)engine.CreateSession();
        Assert.IsTrue(session.Supports(Characteristic));
        foreach (var other in new[] { SectionCheckSpecification.StressLimits(CombinationCategory.Frequent), SectionCheckSpecification.StressLimits(CombinationCategory.QuasiPermanent),
            SectionCheckSpecification.CrackWidth(CombinationCategory.Characteristic), SectionCheckSpecification.ShearAxis2(), SectionCheckSpecification.Torsion() })
            Assert.IsFalse(session.Supports(other), other.Key);
        var refused = Run(Input(), engine, SectionCheckSpecification.StressLimits(CombinationCategory.QuasiPermanent));
        Assert.AreEqual(DataStatus.NotSupported, refused.Data); Assert.AreEqual(EngineeringOutcome.NotEvaluated, refused.Outcome);
        Assert.IsTrue(refused.Diagnostics.Any(d => d.Code == "UnsupportedSectionCheck"));
        Assert.IsFalse(((ISectionCheckSession)new SteelPipelineMaterialChecker(new StandardNTC2018Steel(), "2008", parameters: Options()).CreateSession()).Supports(Characteristic));

        var (_, request) = MultiMaterialWorkflow.Create();
        var config = ConfiguredPlateWorkflowTest.BeamConfiguration(request);
        var definition = config.Engines[1]; var context = config.Contexts[1];
        definition.Kind = SteelPipelineMaterialChecker.EngineId; definition.Parameters = Options();
        var catalog = EngineCatalog.BuiltIn();
        Assert.IsTrue(catalog.Supports(definition, context, CheckMechanism.Serviceability, Characteristic));
        Assert.IsFalse(catalog.Supports(definition, context, CheckMechanism.Serviceability));
        Assert.IsFalse(catalog.Supports(definition, context, CheckMechanism.Serviceability, SectionCheckSpecification.StressLimits(CombinationCategory.Frequent)));
        Assert.IsFalse(catalog.Supports(definition, context, CheckMechanism.UlsBiaxialSection, Characteristic));
        Assert.IsTrue(catalog.Supports(definition, context, CheckMechanism.UlsBiaxialSection));
        StringAssert.Contains(catalog.Capabilities.Single(c => c.Kind == SteelPipelineMaterialChecker.EngineId).Limitations, "Serviceability/StressLimits");
    }

    [TestMethod]
    public void MissingBasisIsReportedAsMissingInputWithoutAnyLimit()
    {
        var input = Input(); input.Forces.M1 = 1e8;
        var result = Run(input, Engine(new() { ["Unperforated"] = "true" }));
        Assert.AreEqual(DataStatus.Insufficient, result.Data); Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome); Assert.IsNull(result.Utilization);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "SteelPipeline.ServiceStress.BasisRequired"));
        Assert.AreEqual(0, result.Details!.Metrics.Count);
        Assert.IsTrue(result.Details.Trace.Any(t => t.Key == "ServiceStress.Status" && t.Expression == "MissingInput"));
    }

    [DataTestMethod]
    [DataRow("ServiceStress.GammaMSer")] [DataRow("ServiceStress.GammaMSerSource")] [DataRow("ServiceStress.ApplicabilitySource")]
    public void PartialBasisIsRejectedByTheSchemaAndTheEngine(string removed)
    {
        var values = Options(); values.Remove(removed);
        Assert.IsTrue(SteelParameterSchemas.Section.Validate(values).Any(i => i.Code == "ServiceStressBasis"));
        Assert.ThrowsException<ArgumentException>(() => Engine(values));
        values = Options(); Assert.AreEqual(0, SteelParameterSchemas.Section.Validate(values).Count);
        values["ServiceStress.GammaMSer"] = "0.95";
        Assert.IsTrue(SteelParameterSchemas.Section.Validate(values).Any(i => i.Key == "ServiceStress.GammaMSer" && i.Code == "InvalidParameterValue"));
        Assert.ThrowsException<ArgumentException>(() => Engine(values));
        Assert.AreNotEqual(Engine().Configuration, Engine(Options(true)).Configuration);
    }

    [DataTestMethod]
    [DataRow("torsion", "SteelPipeline.ServiceStress.Torsion.NotIntegrated", DataStatus.NotSupported)]
    [DataRow("holes", "SteelPipeline.ServiceStress.NetSection.DeclarationRequired", DataStatus.Insufficient)]
    [DataRow("zero", null, DataStatus.Ready)]
    public void UnqualifiedCutsAreNeverEvaluated(string kind, string? code, DataStatus data)
    {
        var input = Input(); input.Forces.M1 = 1e8;
        if (kind == "torsion") input.Forces.T = 1e6;
        if (kind == "zero") input.Forces.M1 = 0;
        var options = Options(); if (kind == "holes") options.Remove("Unperforated");
        var result = Run(input, Engine(options));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome); Assert.AreEqual(data, result.Data); Assert.IsNull(result.Utilization);
        if (code != null) Assert.IsTrue(result.Diagnostics.Any(d => d.Code == code), string.Join(";", result.Diagnostics.Select(d => d.Code)));
        else Assert.IsTrue(result.Details!.Trace.Any(t => t.Key == "ServiceStress.Status" && t.Expression == "NotRequired"));
    }

    private static (GPC.Model.Models.Model Model, VerificationConfiguration Config) Workflow(CombinationCategory category, double gamma = 1.0)
    {
        var (model, _) = MultiMaterialWorkflow.Create();
        // Steel group: beam 20, "Stocky H" = the H300 above; the SLE sample carries the characteristic actions.
        var f = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[20], MultiMaterialWorkflow.State("SLE")).Single().ResultBeamForces;
        f.N = -1e5; f.M1 = 5e7; f.V2 = 1e5; f.V1 = f.T = f.M2 = 0;
        var selection = MultiMaterialWorkflow.State("SLE"); selection.Category = category;
        var check = SectionCheckSpecification.StressLimits(category);
        var options = Options(); options["ServiceStress.GammaMSer"] = gamma.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var config = new VerificationConfiguration {
            Contexts = new[] { new DesignContextDefinition { Id = "ec3", Edition = "2005", Code = new StandardEN1993p11() } },
            Engines = new[] { new EngineDefinition { Id = "section", Kind = SteelPipelineMaterialChecker.EngineId, ContextId = "ec3", Parameters = options } },
            Jobs = new[] { new ConfiguredCheckJob { Name = "Steel SLE stresses", Plan = new BeamCheckPlanRequest {
                Elements = new ElementSelection { Groups = new[] { "Steel" }, Families = new[] { EntityFamily.Beam } },
                Results = new[] { selection }, SectionMechanisms = new CheckMechanism[0], SectionChecks = new[] { check } },
                Routes = new[] { new CheckRouteDefinition { EngineId = "section", Mechanisms = new[] { CheckMechanism.Serviceability }, Checks = new[] { check } } } } }
        };
        return (model, config);
    }

    [TestMethod]
    public void SavedConfigurationRunsTheCharacteristicSelectionAndTheArchiveKeepsTheEvidence()
    {
        // N = -100 kN, M1 = 50 kNm, V2 = 100 kN: bottom web end y = -132, sigma = -11.67 - 52.42 = -64.09, tau 25.39 -> 77.73 MPa.
        var (model, config) = Workflow(CombinationCategory.Characteristic);
        var catalog = EngineCatalog.BuiltIn();
        Assert.IsTrue(catalog.Supports(config.Engines[0], config.Contexts[0], CheckMechanism.Serviceability, Characteristic));
        var request = catalog.Compile(ConfigurationArchive.Copy(config));
        var report = new Service().Verify(model, request);
        var result = report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, string.Join(";", result.Diagnostics.Select(d => d.Code + d.Message)));
        Assert.AreEqual("Serviceability/None/StressLimits/Characteristic", result.Check!.Key);
        Assert.AreEqual("Steel.ServiceStress", result.MethodId); Assert.AreEqual(CombinationCategory.Characteristic, report.Jobs[0].Results.Single().Check!.Category);
        double sigmaJ = -1e5 / A - 5e7 * 132 / Ixx, tauJ = 1e5 * WebS(132) / (IShear * 12), expected = Math.Sqrt(sigmaJ * sigmaJ + 3 * tauJ * tauJ);
        Assert.AreEqual(77.73, expected, 1e-2);
        Close(expected / 355, result.Utilization!.Value);
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var restored = CheckReportArchive.Load(stream);
        var after = (ServiceabilityCheckDetails)restored[0].Results.Single().Details!;
        Assert.AreEqual("Characteristic", after.CombinationCategory); Assert.AreEqual(3, after.Metrics.Count); Assert.AreEqual(3, after.Points.Count);
        Close(expected, after.Metrics.Single(m => m.Key == "EquivalentStress").Demand!.Value);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(restored, model, request));
        // A different gammaM,ser is a different engine configuration: the saved report is no longer current.
        var (_, changed) = Workflow(CombinationCategory.Characteristic, 1.1);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(restored, model, catalog.Compile(changed)));
    }

    [TestMethod]
    public void AFrequentSelectionIsRoutedButRefusedByTheSteelEngine()
    {
        var (model, config) = Workflow(CombinationCategory.Frequent);
        var report = new Service().Verify(model, EngineCatalog.BuiltIn().Compile(config));
        var result = report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome); Assert.AreEqual(DataStatus.NotSupported, result.Data);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "UnsupportedSectionCheck"));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
    }
}
