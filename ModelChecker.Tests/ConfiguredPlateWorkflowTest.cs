using System.Runtime.Serialization;
using System.Xml.Linq;
using GPC.Examples;
using GPC.Geometry;
using GPC.Model.Checker;
using GPC.Model.Checking;
using GPC.Model.Structure;
using GPC.Model.Checker.Configuration;
using GPC.Model.ElementProperties;
using GPC.Model.Elements;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Analysis;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Results.Queries;
using GPC.Model.Structure.Members;

namespace ModelChecker.Tests;

[TestClass]
public class ConfiguredPlateWorkflowTest
{
    internal static VerificationConfiguration BeamConfiguration(MultiMaterialCheckRequest request)
    {
        var rc = (ConcreteMaterialChecker)request.Jobs[0].Assignments[0].Checker;
        var steel = (SteelMaterialChecker)request.Jobs[1].Assignments[0].Checker;
        var bridge = (CompositeBridgeMaterialChecker)request.Jobs[2].Assignments[0].Checker;
        return new VerificationConfiguration {
            Contexts = new[] {
                new DesignContextDefinition { Id = "rc", Edition = "2018", Code = rc.Options.Standard },
                new DesignContextDefinition { Id = "steel", Edition = steel.Edition, Code = steel.Code },
                new DesignContextDefinition { Id = "bridge", Edition = bridge.Edition, BridgeCode = bridge.Code } },
            Engines = new[] {
                new EngineDefinition { Id = "rc", Kind = "Concrete.Section", ContextId = "rc", Concrete = rc.Options },
                new EngineDefinition { Id = "steel", Kind = "Steel.EN1993.PlasticShear", ContextId = "steel" },
                new EngineDefinition { Id = "bridge", Kind = "CompositeBridge.Section", ContextId = "bridge", BridgeCases = bridge.Cases.Select(BridgeCaseDefinition.FromCase).ToArray() } },
            Jobs = request.Jobs.Select((j, i) => new ConfiguredCheckJob { Name = j.Name, Plan = j.Plan.Copy(),
                Routes = new[] { new CheckRouteDefinition { EngineId = i == 0 ? "rc" : i == 1 ? "steel" : "bridge" } } }).ToArray()
        };
    }
    private static VerificationConfiguration PlateConfiguration(string kind = "Test.Plate") => new VerificationConfiguration {
        Contexts = new[] { new DesignContextDefinition { Id = "rc", Edition = "2018", Code = new StandardNTC2018Concrete() } },
        Engines = new[] { new EngineDefinition { Id = "plate", Kind = kind, ContextId = "rc" } },
        PlateJobs = new[] { new ConfiguredPlateJob { Name = "Wall points", AxesKind = ShellInputAxes.Reinforcement,
            Preparation = new PreparationRequest { Selection = new ElementSelection { Families = new[] { EntityFamily.Shell }, Groups = new[] { "Wall" } },
                Results = new[] { new ResultSelection { Dataset = "synthetic-static", Case = "P+", Category = CombinationCategory.Ultimate } } },
            Checks = new[] { new PlateCheckSpecification { Id = "face-x", MethodId = "TEST-PROTOCOL-ONLY", Mechanism = CheckMechanism.UlsBiaxialSection,
                Category = CombinationCategory.Ultimate, Direction = SectionCheckDirection.Axis1, ReinforcementRequired = true,
                Face = new PlateFaceReference { PhysicalName = "ground", NormalFace = ShellNormalFace.Positive } } },
            Routes = new[] { new CheckRouteDefinition { EngineId = "plate" } } } }
    };
    // This adapter tests orchestration only. It is deliberately not a structural resistance formula.
    private sealed class ProtocolPlate : IPlateChecker, IPlateCheckSession
    {
        public string Id => "Test.Plate";
        public string Version => "test-1";
        public string RuntimeConfiguration = "protocol-only";
        public string Configuration => RuntimeConfiguration;
        public CheckStandardContext Standard => new("TEST", "1", null, Id, Configuration);
        public int CreatedCheckers => 1;
        public int Calls, Sessions;
        public Action<ShellCheckInput>? BeforeReturn;
        public bool Accepts(AreaElement element) => true;
        public bool Supports(PlateCheckSpecification check) => check.MethodId == "TEST-PROTOCOL-ONLY";
        public IPlateCheckSession CreateSession() { Sessions++; return this; }
        public CheckResult Verify(ShellCheckInput input, PlateCheckSpecification check, CancellationToken token)
        {
            Calls++; Assert.IsTrue(input.IsCurrent); Assert.IsTrue(input.Thickness.Physical > 0);
            BeforeReturn?.Invoke(input);
            return new CheckResult { Execution = ExecutionStatus.Completed, Data = DataStatus.Ready,
                Outcome = EngineeringOutcome.Satisfied, Utilization = .5 };
        }
    }
    private static EngineCatalog Catalog(ProtocolPlate engine)
    {
        var catalog = EngineCatalog.BuiltIn();
        catalog.RegisterPlate(new PlateEngineCapability("Test.Plate", 1, "Test", "Protocol only", (_, _) => engine)); return catalog;
    }
    private static string Diagnostics(ModelCheckReport report) => string.Join(";", report.Jobs.SelectMany(j => j.Results)
        .Select(r => r.Data + "/" + r.Execution + ":" + string.Join(",", r.Diagnostics.Select(d => d.Code + " " + d.Message))));

    [TestMethod]
    public void ThreeNativeConfigurationsRoundTripAndRunWithoutPersistingEngineObjects()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var config = BeamConfiguration(request);
        var copy = ConfigurationArchive.Copy(config);
        Assert.AreEqual(ConfigurationArchive.Fingerprint(config), ConfigurationArchive.Fingerprint(copy));
        var report = new Service().Verify(model, copy, EngineCatalog.BuiltIn());
        Assert.AreEqual(4, report.Executed, Diagnostics(report)); Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        Assert.AreEqual(report.Outcome, Service.CurrentOutcome(report, model, copy, EngineCatalog.BuiltIn()));
        copy.Contexts[1].Revision++;
        Assert.ThrowsException<SerializationException>(() => ConfigurationArchive.Copy(copy));
    }
    [TestMethod]
    public void PerMechanismRoutesAllowDistinctEnginesOnTheSameElementAndRejectConflicts()
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1]; request.Jobs.Clear(); request.Jobs.Add(job);
        job.Plan.SectionMechanisms = new[] { CheckMechanism.Shear, CheckMechanism.Stability };
        job.Assignments[0].Mechanisms = new[] { CheckMechanism.Shear };
        var other = new MaterialCheckerAssignment { Checker = new SteelMaterialChecker(new StandardEN1993p11 { GammaM0 = 1.1 }, "2005"),
            Mechanisms = new[] { CheckMechanism.Stability } };
        job.Assignments = new[] { job.Assignments[0], other };
        var report = new Service().Verify(model, request);
        Assert.AreEqual(2, report.Required); Assert.AreEqual(1, report.Executed); Assert.AreEqual(1, report.Summary.Unsupported);
        other.Mechanisms = new[] { CheckMechanism.Shear };
        Assert.ThrowsException<ArgumentException>(() => new Service().Verify(model, request));
    }
    [TestMethod]
    public void PlatesHaveIndependentThicknessesAndPersistedImmutableEvidence()
    {
        var model = MixedModelFactory.Create(m => { foreach (var p in m.AreaElements.Values) ((ConcretePlateProperty)p.PlateProperty).BendingThickness = 240; });
        var config = PlateConfiguration(); var engine = new ProtocolPlate(); var catalog = Catalog(engine);
        var copy = ConfigurationArchive.Copy(config); var plans = Service.PreparePlates(model, catalog.CompilePlates(copy));
        Assert.AreEqual(2, plans.Single().WorkItems.Count); Assert.AreEqual(0, engine.Sessions);
        var thickness = plans[0].WorkItems[0].Input.Thickness;
        Assert.AreEqual(300, thickness.Physical); Assert.AreEqual(300, thickness.Membrane); Assert.AreEqual(240, thickness.Bending);
        var report = new Service().Verify(model, copy, catalog);
        Assert.AreEqual(2, report.Executed, Diagnostics(report)); Assert.AreEqual(1, report.CreatedCheckers);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, model, copy, catalog));
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var saved = CheckReportArchive.Load(stream);
        Assert.IsTrue(saved.All(j => j.HasUnchangedScope && j.Results.All(r => r.HasUnchangedEvidence)));
        Assert.AreEqual(240, saved[0].Results[0].ShellInput.Thickness.Bending);
        var spec = saved[0].Results[0].ShellCheck;
        Assert.AreEqual("face-x", spec.Id); Assert.AreEqual(SectionCheckDirection.Axis1, spec.Direction);
        Assert.AreEqual(CombinationCategory.Ultimate, spec.ResultCategory); Assert.AreEqual(1, spec.FaceNormalSign);
        Assert.IsTrue(spec.ReinforcementRequired);
        saved[0].Results[0].ShellCheck = new ShellCheckSnapshot("changed", spec.MethodId, spec.Mechanism, spec.RequestedCategory,
            spec.ResultCategory, spec.Direction, spec.PhysicalFace, spec.FaceNormalSign, spec.ReinforcementRequired);
        Assert.IsFalse(saved[0].Results[0].HasUnchangedEvidence);
        saved[0].Results[0].ShellCheck = spec;
        Assert.AreEqual(EntityFamily.Shell, saved[0].Results[0].Target.Family);
        model.CheckReports.AddRange(saved); using var modelFile = new MemoryStream(); ModelArchive.Save(model, modelFile); modelFile.Position = 0;
        Assert.AreEqual(EngineeringOutcome.Satisfied, ModelArchive.Load(modelFile).CheckReports.Single().Outcome);
        model.AreaElements[1090].Assignments.PhysicalThickness = 301;
        Assert.IsFalse(plans[0].IsCurrent); Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report, model, copy, catalog));
        Assert.AreEqual(300, saved[0].Results[0].ShellInput.Thickness.Physical);
    }
    [TestMethod]
    public void SectionOwnedPlateReinforcementFlowsThroughPreparationAndEvidence()
    {
        var model=MixedModelFactory.Create(m=> { foreach(var shell in m.AreaElements.Values) PlateSections.UpgradeLegacy(shell); });
        var configuration=PlateConfiguration(); var engine=new ProtocolPlate(); var catalog=Catalog(engine);
        engine.BeforeReturn=input=> {
            Assert.IsInstanceOfType(input.Property,typeof(ReinforcedConcretePlateSection));
            Assert.AreEqual(0,input.Element.Assignments.Layers.Count);
            Assert.AreEqual(2,input.Reinforcement.Count);
            Assert.AreEqual("ground",input.Reinforcement[0].PhysicalFace);
            Assert.AreNotSame(((ReinforcedConcretePlateSection)input.Element.PlateProperty).RebarLayers[0],input.Reinforcement[0]);
        };
        var report=new Service().Verify(model,configuration,catalog);
        Assert.AreEqual(2,report.Executed,Diagnostics(report));
        Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(report,model,configuration,catalog));
        model.CheckReports.AddRange(report.Jobs); var copy=ModelArchive.Copy(model);
        Assert.IsTrue(copy.CheckReports.All(r=>r.Results.All(v=>v.HasUnchangedEvidence)));
        ((ReinforcedConcretePlateSection)model.AreaElements[1090].PlateProperty).RebarLayers[0].Pitch=200;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Service.CurrentOutcome(report,model,configuration,catalog));
    }
    [TestMethod]
    public void PreparedPlateSectionMutationCannotProduceACurrentSuccessfulReport()
    {
        var model=MixedModelFactory.Create(m=> { foreach(var shell in m.AreaElements.Values) PlateSections.UpgradeLegacy(shell); });
        var engine=new ProtocolPlate(); var configuration=PlateConfiguration();
        engine.BeforeReturn=input=>((ReinforcedConcretePlateSection)input.Property).RebarLayers[0].Pitch+=1;
        var report=new Service().Verify(model,configuration,Catalog(engine));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,report.Outcome);
        Assert.IsTrue(report.Jobs.SelectMany(j=>j.Results).Any(r=>r.Data==DataStatus.Stale));
        Assert.AreEqual(150,((ReinforcedConcretePlateSection)model.AreaElements[1090].PlateProperty).RebarLayers[0].Pitch);
    }
    [TestMethod]
    public void SurfaceZoneSelectionPersistsAndReportsItsPhysicalIdentity()
    {
        var model = MixedModelFactory.Create();
        model.PhysicalSurfaces.Add("wall", new PhysicalSurfaceDefinition("wall", new[] { 1090, 1130 }, new[] {
            new SurfaceZoneDefinition("left", new[] { 1090 }), new SurfaceZoneDefinition("right", new[] { 1130 }) }));
        var configuration = PlateConfiguration();
        configuration.PlateJobs[0].Preparation.Selection = new ElementSelection {
            Families = new[] { EntityFamily.Shell }, Surfaces = new[] { new SurfaceSelection { SurfaceId = "wall", ZoneId = "left" } } };
        var copy = ConfigurationArchive.Copy(configuration);
        Assert.AreEqual(ConfigurationArchive.Fingerprint(configuration), ConfigurationArchive.Fingerprint(copy));
        configuration.PlateJobs[0].Preparation.Selection.Surfaces[0].ZoneId = "right";
        Assert.AreEqual("left", copy.PlateJobs[0].Preparation.Selection.Surfaces[0].ZoneId);
        var engine = new ProtocolPlate(); var catalog = Catalog(engine);
        var report = new Service().Verify(model, copy, catalog);
        Assert.AreEqual(1, report.Required); Assert.AreEqual(1, report.Executed, Diagnostics(report));
        var result = report.Jobs.Single().Results.Single();
        Assert.AreEqual(1090, result.ElementId);
        Assert.AreEqual("wall", result.ShellInput.PhysicalSurfaceId);
        Assert.AreEqual("left", result.ShellInput.SurfaceZoneId);
        model.CheckReports.AddRange(report.Jobs);
        var restored = ModelArchive.Copy(model);
        Assert.AreEqual("left", restored.CheckReports.Single().Results.Single().ShellInput.SurfaceZoneId);
        Assert.IsTrue(restored.CheckReports.Single().Results.Single().HasUnchangedEvidence);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, restored, copy, catalog));
        // Reassigning zones leaves the mesh unchanged, but the saved verification scope is no longer current.
        restored.PhysicalSurfaces["wall"] = new PhysicalSurfaceDefinition("wall", new[] { 1090, 1130 }, new[] {
            new SurfaceZoneDefinition("left", new[] { 1130 }), new SurfaceZoneDefinition("right", new[] { 1090 }) });
        Assert.AreEqual(AnalysisCompatibility.Compatible, AnalysisCompatibilityValidator.Validate(restored).Status);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report, restored, copy, catalog));
    }
    [TestMethod]
    public void InvalidPhysicalMembershipIsReportedWithoutCreatingPlateCheckers()
    {
        var model = MixedModelFactory.Create();
        model.PhysicalSurfaces.Add("first", new PhysicalSurfaceDefinition("first", new[] { 1090, 1130 }));
        model.PhysicalSurfaces.Add("second", new PhysicalSurfaceDefinition("second", new[] { 1090 }));
        var engine = new ProtocolPlate();
        var report = new Service().Verify(model, PlateConfiguration(), Catalog(engine));
        Assert.AreEqual(2, report.Required); Assert.AreEqual(0, report.Executed, Diagnostics(report));
        Assert.AreEqual(0, engine.Sessions); Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.IsTrue(report.Jobs.SelectMany(j => j.Results).All(r => r.Diagnostics.Any(d => d.Code == "PlateInMultiplePhysicalSurfaces")), Diagnostics(report));
    }
    [TestMethod]
    public void PhysicalSurfaceSelectionDoesNotEnableUnqualifiedPlateResistanceMethods()
    {
        var model = MixedModelFactory.Create();
        model.PhysicalSurfaces.Add("wall", new PhysicalSurfaceDefinition("wall", new[] { 1090, 1130 }));
        var config = PlateConfiguration("Concrete.Plate");
        config.PlateJobs[0].Preparation.Selection = new ElementSelection { Families = new[] { EntityFamily.Shell },
            Surfaces = new[] { new SurfaceSelection { SurfaceId = "wall" } } };
        var report = new Service().Verify(model, config, EngineCatalog.BuiltIn());
        Assert.AreEqual(2, report.Required); Assert.AreEqual(0, report.Executed);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.IsTrue(report.Jobs.Single().Results.All(r => r.ShellInput.PhysicalSurfaceId == "wall" && r.ShellInput.SurfaceZoneId == null
            && r.Diagnostics.Any(d => d.Code == "PlateCheckerUnavailable")), Diagnostics(report));
    }
    [TestMethod]
    public void BuiltInPlateMethodsAreExplicitlyUnavailableAndKeepRequestedCoverage()
    {
        var model = MixedModelFactory.Create(); var config = PlateConfiguration("Concrete.Plate");
        var report = new Service().Verify(model, config, EngineCatalog.BuiltIn());
        Assert.AreEqual(2, report.Required); Assert.AreEqual(0, report.Executed); Assert.AreEqual(0, report.CreatedCheckers);
        Assert.IsTrue(report.Jobs[0].Results.All(r => r.Diagnostics.Any(d => d.Code == "PlateCheckerUnavailable")), Diagnostics(report));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
    }
    [TestMethod]
    public void ReversedPlateNormalsRequireExplicitFaceCorrespondenceAndRotateActions()
    {
        var model = MixedModelFactory.Create(); var config = PlateConfiguration(); var job = config.PlateJobs[0];
        var axes = model.AreaElements[1090].CoordinateSystem;
        job.AxesKind = ShellInputAxes.Explicit; job.Axes = new CoordinateSystem(axes.Origin, axes.V1, new Vector3d(-axes.V2.X, -axes.V2.Y, -axes.V2.Z));
        var engine = new ProtocolPlate(); var catalog = Catalog(engine);
        var invalid = new Service().Verify(model, config, catalog);
        Assert.AreEqual(0, invalid.Executed); Assert.AreEqual(0, engine.Sessions);
        Assert.IsTrue(invalid.Jobs[0].Results.All(r => r.Diagnostics.Any(d => d.Code == "UnsupportedShellTransformation")), Diagnostics(invalid));
        job.Axes = axes;
        job.Checks[0].Face.NormalFace = ShellNormalFace.Negative;
        var wrongFace = new Service().Verify(model, config, catalog);
        Assert.IsTrue(wrongFace.Jobs[0].Results.All(r => r.Diagnostics.Any(d => d.Code == "PlateFaceNormalMismatch")));
        job.Checks[0].Face.NormalFace = ShellNormalFace.Positive;
        job.Axes = new CoordinateSystem(axes.Origin, axes.V2, new Vector3d(-axes.V1.X, -axes.V1.Y, -axes.V1.Z));
        var valid = new Service().Verify(model, config, catalog);
        Assert.AreEqual(2, valid.Executed, Diagnostics(valid));
        Assert.AreEqual(-10, valid.Jobs[0].Results[0].ShellInput.Values[2], 1e-9);
    }
    [TestMethod]
    public void MaterialNeutralPlatesDoNotRequireReinforcementAndRejectMissingPhysicalThickness()
    {
        var model = MixedModelFactory.Create(m => { foreach (var p in m.AreaElements.Values) { p.Assignments.Layers.Clear(); p.Assignments.LayerAxes = null; } });
        var config = PlateConfiguration(); var job = config.PlateJobs[0]; job.AxesKind = ShellInputAxes.Element;
        job.Checks[0].ReinforcementRequired = false; job.Checks[0].Face = null;
        var engine = new ProtocolPlate(); var report = new Service().Verify(model, config, Catalog(engine));
        Assert.AreEqual(2, report.Executed, Diagnostics(report));
        var missing = MixedModelFactory.Create(m => { foreach (var p in m.AreaElements.Values) p.Assignments.PhysicalThickness = null; });
        report = new Service().Verify(missing, config, Catalog(new ProtocolPlate()));
        Assert.AreEqual(0, report.Executed); Assert.IsTrue(report.Jobs[0].Results.All(r => r.Diagnostics.Any(d => d.Code == "MissingPhysicalThickness")), Diagnostics(report));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ShellThickness(0, 1, 1));
    }
    [TestMethod]
    public void PlateCancellationMissingCasesAndEngineMutationNeverCertifySuccess()
    {
        var model = MixedModelFactory.Create(); var config = PlateConfiguration(); var engine = new ProtocolPlate(); var catalog = Catalog(engine);
        var cancelled = new Service().Verify(model, config, catalog, new CancellationToken(true));
        Assert.AreEqual(2, cancelled.Required); Assert.AreEqual(2, cancelled.Summary.Cancelled); Assert.AreEqual(0, engine.Sessions);
        config.PlateJobs[0].Preparation.Results[0].Case = "absent";
        var missing = new Service().Verify(model, config, catalog); Assert.AreEqual(2, missing.Required); Assert.AreEqual(0, missing.Executed);
        config.PlateJobs[0].Preparation.Results[0].Case = "P+";
        engine.BeforeReturn = input => input.LocalForces.Fxx = 123456;
        var changed = new Service().Verify(model, config, catalog);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, changed.Outcome); Assert.AreEqual(2, changed.Summary.Stale);
        Assert.AreEqual(150, changed.Jobs[0].Results[0].ShellInput.Values[0], 1e-9);
    }
    [TestMethod]
    public void TheSameExportedStateCanHaveTwoExplicitDesignCategoriesWithoutLosingTasks()
    {
        var model = MixedModelFactory.Create(); var config = PlateConfiguration(); var job = config.PlateJobs[0];
        var selection = job.Preparation.Results[0].Copy(); selection.Category = CombinationCategory.Characteristic;
        job.Preparation.Results = new[] { job.Preparation.Results[0], selection };
        var check = job.Checks[0].Copy(); check.Id = "other-category"; check.Category = CombinationCategory.Characteristic;
        job.Checks = new[] { job.Checks[0], check };
        var engine = new ProtocolPlate(); var report = new Service().Verify(model, config, Catalog(engine));
        Assert.AreEqual(4, report.Required); Assert.AreEqual(4, report.Executed, Diagnostics(report));
    }
    [TestMethod]
    public void UnknownConfigurationVersionsAndDowngradedPlateReportSchemasAreRejected()
    {
        var config = PlateConfiguration(); config.Schema = 100;
        Assert.ThrowsException<SerializationException>(() => ConfigurationArchive.Copy(config)); config.Schema = 1;
        config.Engines[0].Kind = "Unknown.ClassName";
        Assert.ThrowsException<NotSupportedException>(() => EngineCatalog.BuiltIn().CompilePlates(config));
        config = PlateConfiguration(); var report = new Service().Verify(MixedModelFactory.Create(), config, Catalog(new ProtocolPlate()));
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var xml = XDocument.Load(stream); Assert.AreEqual("5", xml.Root!.Attribute("version")!.Value); xml.Root.Attribute("version")!.Value = "3";
        using var changed = new MemoryStream(); xml.Save(changed); changed.Position = 0;
        Assert.ThrowsException<SerializationException>(() => CheckReportArchive.Load(changed));
        report.Jobs[0].Results[0].SchemaVersion = 2;
        Assert.ThrowsException<SerializationException>(() => CheckReportArchive.Save(report.Jobs, new MemoryStream()));
    }
    [TestMethod]
    public void MixedBeamAndPlateJobsKeepTheirScopeAndReadableSpecifications()
    {
        var model = MixedModelFactory.Create(); var config = PlateConfiguration(); var engine = new ProtocolPlate();
        config.Engines = config.Engines.Concat(new[] { new EngineDefinition { Id = "beam", Kind = "Concrete.Section", ContextId = "rc",
            Concrete = new ConcreteVerificationOptions { Criterion = GPC.Checkers.Concrete.SectionSolvers.SectionSolver.FailureAnalysisTypes.ConstantEccentricity } } }).ToArray();
        config.Jobs = new[] { new ConfiguredCheckJob { Name = "beam", Plan = new BeamCheckPlanRequest {
            Elements = new ElementSelection { Families = new[] { EntityFamily.Beam } },
            Results = new[] { new ResultSelection { Dataset = "synthetic-static", Case = "P+", Category = CombinationCategory.Ultimate } } },
            Routes = new[] { new CheckRouteDefinition { EngineId = "beam" } } } };
        var catalog = Catalog(engine); var report = new Service().Verify(model, config, catalog);
        Assert.AreEqual(7, report.Required); Assert.AreEqual(7, report.Executed, Diagnostics(report));
        Assert.AreEqual(5, report.Jobs[0].Results.Count); Assert.AreEqual(2, report.Jobs[1].Results.Count);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, model, config, catalog));
        config.Engines[0].Kind = "missing-plugin";
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report, model, config, catalog));
    }
    [TestMethod]
    public void PlateRoutingCanSelectDifferentEnginesPerCheckAndRejectOverlap()
    {
        var model = MixedModelFactory.Create(); var config = PlateConfiguration(); var first = new ProtocolPlate(); var second = new ProtocolPlate();
        var job = config.PlateJobs[0]; var other = job.Checks[0].Copy(); other.Id = "face-y"; other.Direction = SectionCheckDirection.Axis2;
        job.Checks = new[] { job.Checks[0], other };
        config.Engines = new[] { config.Engines[0], new EngineDefinition { Id = "second", Kind = "Test.Second", ContextId = "rc" } };
        job.Routes = new[] { new CheckRouteDefinition { EngineId = "plate", PlateCheckIds = new[] { "face-x" } },
            new CheckRouteDefinition { EngineId = "second", PlateCheckIds = new[] { "face-y" } } };
        // Different configurations identify different runtime sessions even when the implementation type is shared.
        second.RuntimeConfiguration = "second-protocol";
        var catalog = Catalog(first); catalog.RegisterPlate(new PlateEngineCapability("Test.Second", 1, "Test", "Protocol only", (_, _) => second));
        var report = new Service().Verify(model, config, catalog);
        Assert.AreEqual(4, report.Executed, Diagnostics(report)); Assert.AreEqual(2, first.Calls); Assert.AreEqual(2, second.Calls);
        CollectionAssert.AreEquivalent(new[] { "face-x", "face-y" }, report.Jobs[0].Results.Select(r => r.ShellCheck.Id).Distinct().ToArray());
        job.Routes[1].PlateCheckIds = new[] { "face-x", "face-y" };
        Assert.ThrowsException<ArgumentException>(() => new Service().Verify(model, config, catalog));
    }
    [TestMethod]
    public void CapabilityQueriesRejectMismatchedContextsBeforeCreatingAnEngine()
    {
        var definition = new EngineDefinition { Id = "c", Kind = "Concrete.Section", ContextId = "expected" };
        var context = new DesignContextDefinition { Id = "other", Edition = "2018", Code = new StandardNTC2018Concrete() };
        Assert.ThrowsException<ArgumentException>(() => EngineCatalog.BuiltIn().Supports(definition, context, CheckMechanism.UlsBiaxialSection));
        context.Id = "expected"; context.Revision = 2;
        Assert.ThrowsException<ArgumentException>(() => EngineCatalog.BuiltIn().Supports(definition, context, CheckMechanism.UlsBiaxialSection));
    }

}
