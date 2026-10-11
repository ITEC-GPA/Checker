using GPC.Checkers.CompositeBridge;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core;
using GPC.Model.Core.Identity;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModelChecker.Tests;

/// <summary>
/// Architecture review R8: the configuration and bridge records enter the fingerprints through the contracts declared by their assemblies
/// (ConfigurationFingerprints, BridgeFingerprints). The expected values were computed with the reflection of the fields used before the
/// contracts (Checker 188caa6a, Model 117ed806): the stored configurations and reports keep their fingerprints.
/// </summary>
[TestClass]
public class FingerprintContractTest
{
    [TestMethod]
    public void DeclaredRecordsKeepTheFingerprintsOfTheReflection()
    {
        Assert.AreEqual("A6416B2F779CACCEB4D8CA4F190010B7DD73641EC3F5119C036E3488FF314486", Fingerprint(new HBridgeInput {
            Phases = new[] { new BridgePhase { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 1200, ShearKN = 340 },
                new BridgePhase { Name = "Q", MomentKNm = 2100, Phi = 1.1, PsiL = 0.55 } },
            Geometry = new HSectionDimensions { SlabWidth = 2500, SlabHeight = 250, WebHeight = 1400, WebThickness = 14, TopWidth = 400, TopThickness = 25,
                BottomWidth = 600, BottomThickness = 35 },
            TopRebars = new BridgeRebarRow { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 50 },
            Support = new BridgeSupportStiffener { Enabled = true, Width = 120, Thickness = 15, ReactionKN = 2200, Location = BridgeSupportLocation.LeftEnd },
            Intermediate = new BridgeIntermediateStiffener { Enabled = true, Width = 100, Thickness = 12, LeftPanel = 2500, RightPanel = 2500 },
            Box = new BridgeBoxOptions { Enabled = true, SpanLength = 40000 }, Fatigue = new BridgeFatigueOptions { Enabled = true, GammaFf = 1 } }));
        Assert.AreEqual("0E5112B7C38D22C89A9348A0849CC990E8CC4E67D8FB14B9761761E49A6D648F",
            Fingerprint(new BridgeStiffener { Width = 90, Thickness = 10, Layout = BridgeStiffenerLayout.LeftOnly }));
        Assert.AreEqual("A237F595B9B186F3A43A3682E33906864070B07974E49CF46933BE17F67C7844", Fingerprint(new EngineDefinition {
            Id = "bridge", Kind = "CompositeBridge.Section", ContextId = "bridge",
            Concrete = new ConcreteVerificationOptions { NationalAnnex = "IT", ShearCotTheta = 2.5 }, BridgeHistory = new BridgeHistoryDefinition { WebLayers = 80 },
            Parameters = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" },
            BridgeCases = new[] { new BridgeCaseDefinition { Cut = new BeamCut { ElementId = 7, Parameter = 0.5, Side = SectionSide.Right },
                State = new ResultSelection { Dataset = "d", Case = "G1" }, ActionReferenceY = 120, Source = "test",
                Input = new BridgeInputDefinition { Phases = new[] { new BridgePhase { Name = "G1", MomentKNm = 900 } },
                    Geometry = new HSectionDimensions { SlabWidth = 2000, WebHeight = 1200 }, Studs = new BridgeStudOptions { Enabled = true, Diameter = 19 },
                    Transverse = new BridgeTransverseReinforcement { Enabled = true, CotTheta = 1.5 } } } } }));
        Assert.AreEqual("336EA13746367751FBE37CB212098C1144CC463400A3026A379FEDA55DA82048", Fingerprint(new VerificationConfiguration {
            Contexts = new[] { new DesignContextDefinition { Id = "rc", Edition = "2018", Code = new StandardNTC2018Concrete() } },
            Engines = new[] { new EngineDefinition { Id = "plate", Kind = "Test.Plate", ContextId = "rc" } },
            PlateJobs = new[] { new ConfiguredPlateJob { Name = "Wall points", AxesKind = ShellInputAxes.Reinforcement,
                Preparation = new PreparationRequest { Selection = new ElementSelection { Families = new[] { EntityFamily.Shell }, Groups = new[] { "Wall" } },
                    Results = new[] { new ResultSelection { Dataset = "synthetic-static", Case = "P+", Category = CombinationCategory.Ultimate } } },
                Checks = new[] { new PlateCheckSpecification { Id = "face-x", MethodId = "TEST-PROTOCOL-ONLY", Mechanism = CheckMechanism.UlsBiaxialSection,
                    Category = CombinationCategory.Ultimate, Direction = SectionCheckDirection.Axis1, ReinforcementRequired = true,
                    Face = new PlateFaceReference { PhysicalName = "ground", NormalFace = ShellNormalFace.Positive } } },
                Routes = new[] { new CheckRouteDefinition { EngineId = "plate" } } } },
            Jobs = new[] { new ConfiguredCheckJob { Name = "beam", Plan = new BeamCheckPlanRequest(),
                Cuts = new[] { new BeamCut { ElementId = 3, Parameter = 1, Domain = BeamStationDomain.OffsetToOffset } },
                Routes = new[] { new CheckRouteDefinition { EngineId = "plate", PlateCheckIds = new[] { "face-x" } } } } } }));
        Assert.AreEqual("0D5F494FA9A0A6AF01146A75DB25DEA4EAD16D5191B45F7BD3BB5537DB12AF05", Fingerprint(new CompositeBridgeCase {
            BeamId = 4, Station = 0.25, StationDomain = "NodeToNode", Side = SectionSide.Left,
            State = new ResultSelection { Dataset = "d", Case = "Q" }, Input = new HBridgeInput { Phases = new[] { new BridgePhase { Name = "Q", ForceKN = 10 } } } }));
    }

    private static string Fingerprint(object value) => ModelValues.Fingerprint(new[] { value });
}
