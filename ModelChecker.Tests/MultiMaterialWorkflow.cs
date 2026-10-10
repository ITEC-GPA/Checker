using GPC.Model.Models;
using System;
using System.Linq;
using GPC.Checkers.CompositeBridge;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Checker;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Results.Storage;
using GPC.Model.Results.Locations;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Analysis;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Compatibility;
using GPC.Model.Core.Identity;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Structure.Assignments;
using GPC.Model.Structure.Topology;

namespace GPC.Examples
{
    /// <summary>Analytical assigned actions, not a FEM solution or a complete building verification.</summary>
    public static class MultiMaterialWorkflow
    {
        public static (GPC.Model.Models.Model Model, MultiMaterialCheckRequest Request) Create()
        {
            var model = new GPC.Model.Models.Model("Step 5 - three material groups");
            var bridge = new HBridgeInput {
                Materials = new BridgeMaterialSet(ConcreteMaterialEN1992Data.C35_45, SteelMaterialEN1993Data.S355, SteelMaterialEN1992Data.B450C),
                Geometry = new HSectionDimensions { SlabWidth = 1200, SlabHeight = 180, WebHeight = 400, WebThickness = 20,
                    TopWidth = 200, TopThickness = 20, BottomWidth = 200, BottomThickness = 20 },
                TopRebars = new BridgeRebarRow { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 40 },
                BottomRebars = new BridgeRebarRow { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 40 },
                Options = new BridgeAnalysisOptions { Standard = BridgeStandard.Ntc2018, LimitState = BridgeLimitState.Rare, Class4 = false, CommonLoadY = 0 },
                Phases = new[] { new BridgePhase { Name = "G1", Kind = BridgePhaseKind.SteelOnly, Reference = BridgeLoadReference.CommonElevation, MomentKNm = 20, ShearKN = 5 },
                    new BridgePhase { Name = "Q", Reference = BridgeLoadReference.CommonElevation, MomentKNm = 10, ShearKN = 5 } }
            };
            var sections = new GPC.Model.ElementProperties.BeamProperty[] { MixedModelFactory.Create().BeamElements[250].Assignments.Sections[0].Section,
                new SteelSection(new SectionH(300, 12, 150, 18, 150, 18, "Stocky H"), SteelMaterialEN1993Data.S355), HBridgeSection.NativeSection(bridge) };
            string[] groups = { "Concrete", "Steel", "Composite" };
            for (int i = 0; i < 3; i++)
            {
                int id = 10 * (i + 1); model.NodesElements.Add(new NodeElement(new Point3d(i * 3000, 0, 0), id: id));
                model.NodesElements.Add(new NodeElement(new Point3d(i * 3000, 0, 1000), id: id + 1));
                model.AddProperty(sections[i]); var beam = new BeamElement(null, null, sections[i], id: id);
                model.BeamElements.Add(beam); model.ConnectBeam(id, id, id + 1); model.AssignGroup(model.AddGroup(groups[i]).Name, new[] { beam });
                beam.Assignments.Formulation = BeamFormulation.StraightTwoNode; beam.Assignments.SectionAxes = CoordinateSystem.Global;
                beam.Assignments.SectionGeometryAxes = CoordinateSystem.Global; beam.Assignments.StationDomain = "NodeToNode";
                beam.Assignments.ActionsAtSectionCentroidConfirmed = true;
                if (i == 0) beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Section = (GPC.Model.Sections.Concrete.ReinforcedConcreteSection)sections[i] });
            }
            foreach (string name in new[] { "ULS", "SLE" }) model.LoadCases.Add(new LoadCaseBase(name));
            model.CaptureAnalysis(ReinforcementAnalysisRole.ExcludedFromAnalysis, "Assigned analytical actions independent of design reinforcement.");
            string revision = model.AnalysisFingerprint(); model.Datasets.Add("step5", new AnalysisDataset { Id = "step5", Program = "Assigned analytical fixture",
                ModelRevision = "1", InputFingerprint = revision, NormalizedUnits = "N,mm,rad", Semantics = AnalysisSemantics.LinearStatic, IsSynthetic = true });
            foreach (var beam in model.BeamElements.Values) foreach (string name in new[] { "ULS", "SLE" })
            {
                var point = new BeamReferenceGeometry(beam).PointAt(.5, "NodeToNode");
                var sample = new StationResultBeamForces(model.LoadCases[name], new ResultBeamForces(0, 0, beam.Id == 30 ? 10000 : 1000, 0,
                    beam.Id == 30 ? 30e6 : beam.Id == 10 ? 1e6 : 0, 0, ResultTransformations.AtPoint(CoordinateSystem.Global, point)), .5) {
                    StationDomain = "NodeToNode", PhysicalDistance = 500, Body = ActionBody.PositiveSectionFace,
                    State = new ResultState { DatasetId = "step5", ModelRevision = "1", InputFingerprint = revision, ConcomitantStateId = name,
                        Components = Enumerable.Repeat(ComponentAvailability.Available, 6).ToArray(), IsCumulative = true, IsSynthetic = true, Semantics = AnalysisSemantics.LinearStatic } };
                beam.AddResult(new BeamResult(new[] { sample }));
            }
            var bridgeCases = new[] { "SLE", "ULS" }.Select(name => new CompositeBridgeCase { BeamId = 30, Station = .5, StationDomain = "NodeToNode",
                State = State(name), SectionAxes = CoordinateSystem.Global, ActionReferenceY = 0, HistoryAndReferenceConfirmed = true,
                Source = "Explicit illustrative phase history, terminal N/M1/V2 equal FEM sample", Input = bridge with {
                    Options = bridge.Options with { LimitState = name == "SLE" ? BridgeLimitState.Rare : BridgeLimitState.Ultimate } } }).ToArray();
            var concrete = new ConcreteMaterialChecker(new ConcreteVerificationOptions { Standard = new StandardNTC2018Concrete(), Criterion = SectionSolver.FailureAnalysisTypes.ConstantEccentricity });
            var steel = new SteelMaterialChecker(new StandardEN1993p11(), "2005");
            var composite = new CompositeBridgeMaterialChecker(BridgeStandard.Ntc2018, "2018", bridgeCases);
            var request = new MultiMaterialCheckRequest();
            request.Jobs.Add(Job("RC ULS", "Concrete", "ULS", CheckMechanism.UlsBiaxialSection, concrete));
            request.Jobs.Add(Job("Steel shear", "Steel", "ULS", CheckMechanism.Shear, steel));
            request.Jobs.Add(Job("Bridge SLE", "Composite", "SLE", CheckMechanism.Serviceability, composite));
            request.Jobs.Add(Job("Bridge web shear", "Composite", "ULS", CheckMechanism.Shear, composite));
            return (model, request);
        }
        public static ResultSelection State(string name) => new ResultSelection { Dataset = "step5", Case = name, ConcomitantState = name };
        public static MultiMaterialCheckJob Job(string name, string group, string state, CheckMechanism mechanism, IMaterialChecker checker)
            => new MultiMaterialCheckJob { Name = name, Plan = new BeamCheckPlanRequest { Elements = new ElementSelection {
                Groups = new[] { group }, Families = new[] { EntityFamily.Beam } }, Results = new[] { State(state) }, SectionMechanisms = new[] { mechanism } },
                Assignments = new[] { new MaterialCheckerAssignment { Checker = checker } } };
    }
}
