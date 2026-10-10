using GPC.Examples;
using GPC.Geometry;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Results.Locations;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Compatibility;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Results.Processing;
using GPC.Model.Results.Queries;
using GPC.Model.Structure.Assignments;

namespace ModelChecker.Tests;

[TestClass]
public class QualifiedSteelSectionTest
{
    [DataTestMethod]
    [DataRow(1, 1.0)] [DataRow(1, -1.0)] [DataRow(2, 1.0)] [DataRow(2, -1.0)]
    public void NativeUniaxialBendingMatchesIndependentPlasticSectionProperties(int axis, double sign)
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1];
        var previous = (SteelMaterialChecker)job.Assignments[0].Checker;
        previous.Code.GammaM0 = 1.1;
        var checker = new SteelMaterialChecker(previous.Code, "2005", null, true);
        job.Assignments[0].Checker = checker; job.Plan.SectionMechanisms = new[] { CheckMechanism.UlsBiaxialSection };
        request.Jobs.Clear(); request.Jobs.Add(job);
        var sample = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[20], MultiMaterialWorkflow.State("ULS")).Single();
        // Sharp-corner symmetric H: plastic first moments obtained by summing flange/web rectangles.
        double modulus = axis == 1 ? 2 * 150 * 18 * 141 + 12 * 132 * 132 : 2 * 18 * 150 * 150 / 4 + 264 * 12 * 12 / 4;
        double capacity = modulus * 355 / 1.1;
        if (axis == 1) sample.ResultBeamForces.M1 = sign * .4 * capacity; else sample.ResultBeamForces.M2 = sign * .4 * capacity;
        var report = new Service().Verify(model, request); var result = report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, string.Join(";", result.Diagnostics.Select(d => d.Message ?? d.Code)));
        Assert.AreEqual(.4, result.Utilization!.Value, 1e-12);
        Assert.AreEqual(capacity, result.Details.Metrics.Single(m => m.Key == "M" + axis).Capacity!.Value, capacity * 1e-12);
        var axes = new CoordinateSystem(sample.ResultBeamForces.CoordinateSystem.Origin, new Vector3d(0, 1, 0), new Vector3d(-1, 0, 0));
        sample.ResultBeamForces = ResultOrientation.Beam(sample, axes).ResultBeamForces;
        Assert.AreEqual(result.Utilization, new Service().Verify(model, request).Jobs[0].Results[0].Utilization);
        sample.ResultBeamForces = ResultOrientation.Beam(sample, ResultTransformations.AtPoint(CoordinateSystem.Global, axes.Origin)).ResultBeamForces;
        if (axis == 1) sample.ResultBeamForces.M1 = sign * 1.2 * capacity; else sample.ResultBeamForces.M2 = sign * 1.2 * capacity;
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, new Service().Verify(model, request).Outcome);
    }

    [DataTestMethod]
    [DataRow("axial")] [DataRow("biaxial")] [DataRow("torsion")] [DataRow("positive shear")] [DataRow("negative shear")]
    public void UnqualifiedInteractionsCannotProduceAPass(string kind)
    {
        var (model, request) = MultiMaterialWorkflow.Create(); var job = request.Jobs[1];
        var old = (SteelMaterialChecker)job.Assignments[0].Checker;
        job.Assignments[0].Checker = new SteelMaterialChecker(old.Code, "2005", null, true);
        job.Plan.SectionMechanisms = new[] { CheckMechanism.UlsBiaxialSection }; request.Jobs.Clear(); request.Jobs.Add(job);
        var f = ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[20], MultiMaterialWorkflow.State("ULS")).Single().ResultBeamForces;
        f.M1 = 1e6;
        if (kind == "axial") f.N = -1;
        if (kind == "biaxial") f.M2 = 1;
        if (kind == "torsion") f.T = 1;
        if (kind.EndsWith("shear")) f.V2 = (kind.StartsWith("negative") ? -1 : 1) * .51 * 3384 * 355 / Math.Sqrt(3);
        var result = new Service().Verify(model, request).Jobs[0].Results.Single();
        Assert.AreEqual(DataStatus.NotSupported, result.Data); Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome); Assert.IsNull(result.Utilization);
    }

    [TestMethod]
    public void SectionAssignmentsAreResolvedAtEachCutAndAreNotReplacedByTheDefaultProperty()
    {
        var first = new SteelSection(new GPC.Model.Sections.SectionH(300, 12, 150, 18, 150, 18, "H300"), GPC.Model.Data.Steel.SteelMaterialEN1993Data.S355);
        var second = new SteelSection(new GPC.Model.Sections.SectionH(400, 16, 200, 20, 200, 20, "H400"), GPC.Model.Data.Steel.SteelMaterialEN1993Data.S355);
        var model = MixedModelFactory.Create(m => {
            m.BeamElements[250].BeamProperty = first;
            var assignments = m.BeamElements[250].Assignments.Sections; assignments.Clear();
            assignments.Add(new BeamSectionAssignment { Start = 0, End = .5, Property = first });
            assignments.Add(new BeamSectionAssignment { Start = .5, End = 1, Property = second });
        });
        var checker = new SteelMaterialChecker(new GPC.Model.Standards.StandardEN1993p11(), "2005", null, true);
        var session = checker.CreateSession();
        var results = new List<CheckResult>();
        foreach (double station in new[] { .25, .75 })
        {
            var sample = Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", station, SectionSide.Unspecified);
            var prepared = BeamActionPreparation.Prepare(model, 250, sample, "assigned section");
            Assert.AreEqual(DataStatus.Ready, prepared.Status);
            results.Add(session.Verify(prepared.Input, CheckMechanism.UlsBiaxialSection, default));
        }
        Assert.IsTrue(results.All(r => r.Outcome == EngineeringOutcome.Satisfied));
        Assert.AreEqual(2, session.CreatedCheckers);
        Assert.IsTrue(results[1].Details.Metrics[0].Capacity > results[0].Details.Metrics[0].Capacity);
    }

    [TestMethod]
    public void NewCapabilityIsExplicitAndPersistsWithoutChangingTheLegacyShearSelection()
    {
        var (_, request) = MultiMaterialWorkflow.Create(); var config = ConfiguredPlateWorkflowTest.BeamConfiguration(request);
        var catalog = EngineCatalog.BuiltIn(); var definition = config.Engines[1]; var context = config.Contexts[1];
        Assert.IsFalse(catalog.Supports(definition, context, CheckMechanism.UlsBiaxialSection));
        definition.Kind = "Steel.EN1993.Section";
        Assert.IsTrue(catalog.Supports(definition, context, CheckMechanism.UlsBiaxialSection));
        Assert.IsFalse(catalog.Supports(definition, context, CheckMechanism.Stability));
        var restored = ConfigurationArchive.Copy(config);
        var checker = (SteelMaterialChecker)catalog.Compile(restored).Jobs[1].Assignments[0].Checker;
        Assert.IsTrue(checker.IncludeUniaxialBending); Assert.AreEqual(definition.Kind, checker.Id);
    }
}
