using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Examples;
using GPC.Geometry;
using GPC.Model.Checker;
using GPC.Model.Elements;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Compatibility;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;

namespace ModelChecker.Tests;

[TestClass]
public class CheckReportIntegrationTest
{
    private static ModelCheckJob Job(string name, StandardModelCode2010 standard) => new()
    {
        Name = name, Options = new ConcreteVerificationOptions { Standard = standard,
            Criterion = SectionSolver.FailureAnalysisTypes.ConstantEccentricity },
        Preparation = new PreparationRequest { Selection = new ElementSelection { Families = new[] { EntityFamily.Beam } },
            Results = new[] { new ResultSelection { Dataset = "synthetic-static", Case = "P+" } }, Settings = "report integration" }
    };

    [TestMethod]
    public void RealDetailsMatchNativeResistanceAndKeepThePreparedForceFrame()
    {
        var model = MixedModelFactory.Create(); var job = Job("ULS", new StandardNTC2018Concrete());
        var preparation = Verification.PrepareBeam(model, 250, Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified), job.Preparation.Settings);
        var verifier = job.Options.CreateVerifier(); var actual = Verification.Run(preparation, CheckMechanism.UlsBiaxialSection, verifier);
        Assert.AreEqual(ExecutionStatus.Completed, actual.Execution, string.Join(";", actual.Diagnostics.Select(d => d.Message)));
        Assert.IsInstanceOfType(actual.Details, typeof(SectionResistanceDetails));
        var detail = (SectionResistanceDetails)actual.Details; var input = preparation.Input;
        var reference = new CoordinateSystem(input.Section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
        var f = input.Forces; var nativeForces = new ResultBeamForces(f.N, f.V1, f.V2, f.T, f.M1, f.M2, reference);
        var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(reference, job.Options.Criterion,
            SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);
        var native = new SectionCheckerModelCode2010(new SectionCheckerAttribute(input.Section), options, job.Options.Standard, false);
        var raw = native.CalculateFailureDomainPoint(nativeForces);
        // This fixture exposes the old finite-but-outside-angle-tolerance candidate.
        Assert.IsFalse(DomainPointConvergence.Evaluate(native.SectionSolver, nativeForces, raw, job.Options.Criterion).Accepted);
        var point = new GPC.Checkers.Concrete.Analysis.LegacySectionCalculation(native).SolveResistance(new(nativeForces));
        Assert.IsTrue(point.Diagnostics.ResistanceConvergence.Accepted);
        Assert.AreEqual(point.N.Value, detail.NRd, 1e-6); Assert.AreEqual(point.M1.Value, detail.M1Rd, 1e-6); Assert.AreEqual(point.M2.Value, detail.M2Rd, 1e-6);
        Assert.AreEqual(point.Strain.Epsilon, detail.StrainAtReference, 1e-12);
        Assert.AreEqual(point.Strain.ChiX, detail.ChiX, 1e-12); Assert.AreEqual(point.Strain.ChiY, detail.ChiY, 1e-12);
        Assert.AreEqual(point.FailureMode, detail.FailureMode);
        Assert.AreEqual(point.Utilization.Value, actual.Utilization!.Value, 1e-12);
        Assert.AreEqual(f.M1, detail.Demand.M1); Assert.AreEqual(f.V2, detail.Demand.V2); Assert.AreEqual(f.T, detail.Demand.T);
        Assert.AreEqual(f.CoordinateSystem.Origin.Z, actual.Input.BeamForces.Axes.Origin.Z);
        Assert.AreEqual(input.Section.Centroid.X, detail.Demand.Axes.Origin.X);
        Assert.AreEqual("2018", actual.Standard.Edition); Assert.IsTrue(actual.Standard.Parameters.Contains("GammaC"));
        Assert.AreEqual(job.Options.Criterion.ToString(), detail.UtilizationDefinition);
        Assert.IsNull(detail.Convergence); // This native API does not supply an iterative convergence record.
        double oldMoment = actual.Input.BeamForces.M1; input.Forces.M1 += 999;
        Assert.AreEqual(oldMoment, actual.Input.BeamForces.M1); Assert.IsTrue(actual.HasUnchangedEvidence);
    }

    [TestMethod]
    public void MultinormReportsPersistAndKeepGroupAndCurrentnessInformation()
    {
        var model = MixedModelFactory.Create(); model.AddGroup("Beam A"); model.AddGroup("Beam B");
        model.AssignGroup("Beam A", new Element[] { model.BeamElements[250] }); model.AssignGroup("Beam B", new Element[] { model.BeamElements[250] });
        var request = new ModelCheckRequest(); request.Jobs.Add(Job("NTC", new StandardNTC2018Concrete())); request.Jobs.Add(Job("EC2", new StandardEN1992p11()));
        var report = new Service().Verify(model, request);
        Assert.AreEqual(10, report.Required); Assert.AreEqual(10, report.Executed); Assert.AreEqual(2, report.CreatedCheckers);
        Assert.AreEqual(1, report.Elements.Count); Assert.AreEqual(2, report.Elements.Single().Governing.Count);
        Assert.AreEqual(2, report.Standards.Count); Assert.AreEqual(2, report.Groups.Count);
        Assert.IsTrue(report.Groups.All(g => g.Results.Count == 10)); Assert.IsTrue(report.Summary.IsComplete);
        Assert.AreSame(report.Jobs[0].Results[0].Standard, report.Jobs[0].Results[1].Standard);
        IConcreteSectionVerifier Engine(string job) => request.Jobs.Single(j => j.Name == job).Options.CreateVerifier();
        Assert.AreEqual(report.Outcome, report.CurrentOutcome(model, Engine));
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var restored = CheckReportArchive.Load(stream);
        Assert.AreEqual(10, restored.Sum(r => r.Required)); Assert.IsTrue(restored.SelectMany(r => r.Results).All(r => r.HasUnchangedEvidence));
        Assert.AreEqual(report.Jobs[0].Outcome, restored[0].CurrentOutcome(model, Engine("NTC")));
        model.CheckReports.AddRange(restored); using var full = new MemoryStream(); ModelArchive.Save(model, full); full.Position = 0;
        var restoredModel = ModelArchive.Load(full);
        Assert.IsTrue(restoredModel.CheckReports.All(r => r.CurrentOutcome(restoredModel, Engine(r.Job)) == r.Outcome));
        restoredModel.BeamElements[250].Assignments.Sections[0].Section.Rebars.First().Position.Y += 2;
        Assert.IsTrue(restoredModel.CheckReports.All(r => r.CurrentOutcome(restoredModel, Engine(r.Job)) == EngineeringOutcome.NotEvaluated));
        var originalStandard = report.Jobs[0].Results[0].Standard; var oldIdentity = originalStandard.Identity;
        request.Jobs[0].Options.Standard.GammaC += .1;
        Assert.AreEqual(oldIdentity, originalStandard.Identity);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, restored[0].CurrentOutcome(model, Engine("NTC")));
    }

    [TestMethod]
    public void SourceResultsAndSavedEvidenceMutationsInvalidateCurrentReports()
    {
        var model = MixedModelFactory.Create(); var request = new ModelCheckRequest(); var job = Job("ULS", new StandardModelCode2010()); request.Jobs.Add(job);
        var report = new Service().Verify(model, request).Jobs.Single(); var engine = job.Options.CreateVerifier();
        Assert.AreNotEqual(EngineeringOutcome.NotEvaluated, report.CurrentOutcome(model, engine));
        var sample = Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified);
        sample.ResultBeamForces.M1 += 100;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.CurrentOutcome(model, engine));
        sample.ResultBeamForces.M1 -= 100;
        report.Results[0].Utilization = .987;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.CurrentOutcome(model, engine));
        Assert.AreEqual(1, report.Summary.Invalid);
    }

    private sealed class DecisionVerifier : IConfiguredSectionVerifier
    {
        public string Version => "fixture";
        public string Configuration => "fixture";
        public IReadOnlyCollection<CheckMechanism> Capabilities => new[] { CheckMechanism.Detailing };
        public bool Invalid { get; set; }
        public CheckResult Verify(BeamCheckInput input, CheckMechanism mechanism, CancellationToken token) => new()
        {
            Execution = ExecutionStatus.Completed, Data = DataStatus.Ready, Outcome = EngineeringOutcome.Satisfied,
            Details = new DetailingCheckDetails("fixture-detailing", new[] { new CheckMetric("spacing", 100, 150, "mm", passed: !Invalid) })
        };
    }
    [TestMethod]
    public void ReplacingOneRequestedStationWithADuplicateDoesNotHideMissingCoverage()
    {
        var model = MixedModelFactory.Create(); var request = new ModelCheckRequest(); var job = Job("ULS", new StandardModelCode2010()); request.Jobs.Add(job);
        var report = new Service().Verify(model, request); var results = report.Jobs.Single().Results;
        results[1] = results[0];
        Assert.IsFalse(report.Jobs.Single().HasUnchangedScope); Assert.IsFalse(report.Summary.IsComplete);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.Outcome);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, report.CurrentOutcome(model, _ => job.Options.CreateVerifier()));
    }
    [TestMethod]
    public void OrchestratorAcceptsExplicitBooleanVerdictsAndRejectsContradictions()
    {
        var request = new ModelCheckRequest(); var job = Job("Detailing", new StandardModelCode2010());
        job.Preparation.Mechanisms = new[] { CheckMechanism.Detailing }; request.Jobs.Add(job);
        var verifier = new DecisionVerifier(); var model = MixedModelFactory.Create();
        var valid = new Service(_ => verifier).Verify(model, request);
        Assert.AreEqual(EngineeringOutcome.Satisfied, valid.Outcome); Assert.AreEqual(5, valid.Executed);
        Assert.IsTrue(valid.Jobs.Single().Results.All(r => r.Input != null && r.Utilization == null));
        verifier.Invalid = true; var invalid = new Service(_ => verifier).Verify(model, request);
        Assert.AreEqual(0, invalid.Executed); Assert.AreEqual(5, invalid.Summary.Errors); Assert.AreEqual(EngineeringOutcome.NotEvaluated, invalid.Outcome);
    }
}
