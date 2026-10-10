using GPC.Model.Models;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Examples;
using GPC.Geometry;
using GPC.Model.Checker;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Loads;
using GPC.Model.Restraints;
using GPC.Model.Results;
using GPC.Model.Results.Storage;
using GPC.Model.Results.Locations;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Analysis;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Compatibility;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core.Identity;
using GPC.Model.Results.Processing;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Structure.Assignments;
using GPC.Model.Structure.Topology;

namespace ModelChecker.Tests;

/// <summary>Step 3 evidence for the existing public path, not an implementation of physical-member design.</summary>
[TestClass]
public class ElementScopeCharacterizationTest
{
    // Analytical assigned fixture: 5 m cantilever, transverse tip force 1000 N.
    // Two FEM elements of 2 m and 3 m; the second has reversed connectivity.
    // The asymmetric reinforcement stays in the same physical section frame.
    /// <param name="torque">Constant torque about the member axis in the analytical results, Nmm (0 = the historical fixture).</param>
    /// <param name="axial">Constant axial force, N (compression negative; 0 = the historical fixture).</param>
    /// <param name="shear">Tip load along global Y, N: V2 and M1 = −shear·(5000 − z) (1000 = the historical fixture).</param>
    internal static GPC.Model.Models.Model Model(double torque = 0, double axial = 0, double shear = 1000,
        Action<GPC.Model.Models.Model>? configureBeforeResults = null)
    {
        var model = new GPC.Model.Models.Model("Step 3: two FEM elements, proposed physical member T1");
        foreach (var entry in new[] { (Id: 10, Z: 0.0), (Id: 20, Z: 2000.0), (Id: 30, Z: 5000.0) })
            model.NodesElements.Add(new NodeElement(new Point3d(0, 0, entry.Z), id: entry.Id));
        var section = MixedModelFactory.Create().BeamElements[250].Assignments.Sections[0].Section;
        section.Rebars.First().Position.Y += 40;
        model.AddProperty(section);
        var group = model.AddGroup("T1 selection");
        foreach (var entry in new[] { (Id: 250, I: 10, J: 20), (Id: 251, I: 30, J: 20) })
        {
            var beam = new BeamElement(null, null, section, id: entry.Id);
            model.BeamElements.Add(beam); model.ConnectBeam(entry.Id, entry.I, entry.J); beam.AddGroup(group);
            beam.Assignments.Formulation = BeamFormulation.StraightTwoNode;
            beam.Assignments.LogicalMember = "T1";
            beam.Assignments.StationDomain = "NodeToNode";
            beam.Assignments.SectionAxes = Axes.Beam(beam.StartPoint, beam.EndPoint, new Vector3d(1, 0, 0));
            beam.Assignments.SectionGeometryAxes = CoordinateSystem.Global;
            beam.Assignments.ActionsAtSectionCentroidConfirmed = true;
            beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Section = section });
        }
        var loadCase = new LoadCaseBase("LC1"); model.LoadCases.Add(loadCase);
        model.NodesElements[30].Loads.Add(new PointLoad(0, 1000, 0, 0, 0, 0, model.NodesElements[30].Position, loadCase));
        model.NodesElements[10].Assignments.Restrains.Add(new RestrainAssignment { Restrain = NodeRestrain.GetAllFixed(model.NodesElements[10], CoordinateSystem.Global) });
        configureBeforeResults?.Invoke(model);
        model.CaptureAnalysis(ReinforcementAnalysisRole.ExcludedFromAnalysis, "Analytical cantilever fixture; forces independent of reinforcement.");
        var fingerprint = model.AnalysisFingerprint();
        model.Datasets.Add("synthetic-member", new AnalysisDataset { Id = "synthetic-member", Program = "Analytical fixture", ModelRevision = "r1",
            InputFingerprint = fingerprint, NormalizedUnits = "N,mm,rad", IsSynthetic = true, Semantics = AnalysisSemantics.LinearStatic });
        foreach (var beam in model.BeamElements.Values)
        {
            var stations = beam.Id == 250 ? new[] { 0.0, .25, .5, .75, 1.0 } : new[] { 0.0, .5, 1.0 };
            var samples = new List<IBeamResultLocation>();
            foreach (var xi in stations)
            {
                var point = new BeamReferenceGeometry(beam).PointAt(xi, "NodeToNode");
                var source = new StationResultBeamForces(loadCase,
                    new ResultBeamForces(axial, 0, shear, torque, -shear * (5000 - point.Z), 0, ResultTransformations.AtPoint(CoordinateSystem.Global, point)), xi)
                {
                    StationDomain = "NodeToNode", PhysicalDistance = xi * beam.Length, Body = ActionBody.PositiveSectionFace,
                    State = new ResultState { DatasetId = "synthetic-member", ModelRevision = "r1", InputFingerprint = fingerprint,
                        Components = Enumerable.Repeat(ComponentAvailability.Available, 6).ToArray(), Semantics = AnalysisSemantics.LinearStatic,
                        ConcomitantStateId = "LC1", IsCumulative = true, IsSynthetic = true,
                        Coverage = "Exported samples only; continuous coverage is not asserted." }
                };
                samples.Add(ResultOrientation.Beam(source, ResultTransformations.AtPoint(beam.Assignments.SectionAxes, point)));
            }
            beam.AddResult(new BeamResult(samples));
        }
        return model;
    }
    private static ModelCheckRequest Request(bool stability = false)
    {
        var request = new ModelCheckRequest();
        request.Jobs.Add(new ModelCheckJob { Name = "Local sections", Options = new ConcreteVerificationOptions {
                Standard = new StandardNTC2018Concrete(), Criterion = SectionSolver.FailureAnalysisTypes.ConstantEccentricity },
            Preparation = new PreparationRequest { Selection = new ElementSelection { Groups = new[] { "T1 selection" }, Families = new[] { EntityFamily.Beam } },
                Results = new[] { new ResultSelection { Dataset = "synthetic-member", Case = "LC1" } }, Settings = "step3 characterization",
                Mechanisms = stability ? new[] { CheckMechanism.UlsBiaxialSection, CheckMechanism.Stability } : new[] { CheckMechanism.UlsBiaxialSection } } });
        return request;
    }

    [TestMethod]
    public void ReversedFemElementRetainsPhysicalForcesAndSeparateElementIdentity()
    {
        var model = Model(); var request = Request(); var report = new Service().Verify(model, request);
        Assert.AreEqual(2000, model.BeamElements[250].Length); Assert.AreEqual(3000, model.BeamElements[251].Length);
        Assert.AreEqual(8, report.Required); Assert.AreEqual(8, report.Executed); Assert.AreEqual(2, report.Elements.Count);
        Assert.AreEqual(1, report.CreatedCheckers);
        var joint = report.Jobs.Single().Results.Where(r => r.Station == 1).ToArray();
        Assert.AreEqual(2, joint.Length); // Same physical point, distinct source element/end; neither is discarded.
        foreach (var row in joint)
        {
            Assert.AreEqual(-3e6, row.Input.BeamForces.M1, 1e-8); Assert.AreEqual(1000, row.Input.BeamForces.V2, 1e-8);
            Assert.AreEqual(2000, row.Input.BeamForces.Axes.Origin.Z, 1e-8);
        }
        Assert.AreEqual(joint[0].Utilization!.Value, joint[1].Utilization!.Value, 1e-12);
        Assert.AreEqual(3e6, Verification.BeamSample(model.BeamElements[251], "synthetic-member", "LC1", 1, SectionSide.Unspecified).ResultBeamForces.M1, 1e-8);
    }

    [TestMethod]
    public void CompletionOfExportedSamplesDoesNotPromiseContinuousMemberCoverage()
    {
        var model = Model(); var beam = model.BeamElements[250];
        beam.Results[0].Results.RemoveAll(r => r is StationResultBeamForces s && s.ParametricDistance == .5);
        var report = new Service().Verify(model, Request());
        Assert.AreEqual(7, report.Required); Assert.AreEqual(7, report.Executed); Assert.IsTrue(report.Summary.IsComplete);
        Assert.IsNull(ResultQueries.BeamStation(beam, new ResultSelection { Dataset = "synthetic-member", Case = "LC1" }, .5, SectionSide.Unspecified));
        Assert.IsTrue(report.Jobs.Single().Results.All(r => r.Coverage.Contains("continuous coverage is not asserted")));
        // A future explicit sampling plan must make this missing physical station a required outcome.
    }

    [TestMethod]
    public void LogicalMemberLabelCurrentlyHasNoPhysicalMemberOrRevisionSemantics()
    {
        var model = Model(); var request = Request(); var report = new Service().Verify(model, request);
        string before = model.VerificationFingerprint(request.Jobs[0].Preparation.Settings);
        model.BeamElements[251].Assignments.LogicalMember = "Different label";
        Assert.AreEqual(before, model.VerificationFingerprint(request.Jobs[0].Preparation.Settings));
        Assert.AreEqual(report.Outcome, report.CurrentOutcome(model, _ => request.Jobs[0].Options.CreateVerifier()));
        Assert.AreEqual(2, report.Elements.Count);
    }

    [TestMethod]
    public void StabilityRequestDoesNotInferEffectiveLengthsFromFemMesh()
    {
        var report = new Service().Verify(Model(), Request(true));
        Assert.AreEqual(16, report.Required); Assert.AreEqual(8, report.Executed); Assert.AreEqual(8, report.Summary.Unsupported);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.IsTrue(report.Jobs.Single().Results.Where(r => r.Mechanism == CheckMechanism.Stability)
            .All(r => r.Diagnostics.Any(d => d.Code == "UnsupportedMechanism")));
        // Existing orchestration counts this per sample. A member planner must instead schedule each method on its actual span/state.
    }
}
