using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Model.Checking.Contracts;
using GPC.Model.Compatibility;
using GPC.Model.Results.Locations;

namespace ModelChecker.Tests;

[TestClass]
public class CalculationFactoryTest
{
    internal sealed class Alternative : IConcreteCalculationFactory, ISectionResponseSolver, ISectionResistanceSolver
    {
        public string Id => "TEST-NUMERICAL-PORT";
        public string RuntimeVersion = "test1";
        public string Version => RuntimeVersion;
        public string Configuration => "contract-test-only";
        public int Calls, Sessions;
        public Action<ReinforcedConcreteSection, StandardModelCode2010>? OnCreate;
        public bool Converged = true;
        public bool RejectedEquilibrium;
        public bool OmitEquilibrium;
        public string ReportedEngine = "TEST-NUMERICAL-PORT";
        public string ReportedCriterion = "ConstantN";
        public ConcreteCalculationSession Create(ReinforcedConcreteSection section, StandardModelCode2010 standard, ConcreteCalculationOptions options)
        { Sessions++; OnCreate?.Invoke(section, standard); return new(this, this); }
        public SectionResponse Solve(SectionAnalysisInput input, CancellationToken token) => throw new NotSupportedException();
        public SectionResistanceResponse SolveResistance(SectionAnalysisInput input, CancellationToken token)
        {
            Calls++;
            if (RejectedEquilibrium) return new(new(CalculationStatus.NotConverged, Id, Version, "Outside axial tolerance",
                new AxialEquilibriumEvidence(input.Forces.N, input.Forces.N + 2000, 1000)), "ConstantN");
            var capacity = input.Forces; capacity.M1 /= .9; capacity.M2 /= .9;
            var convergence = OmitEquilibrium ? null : ResistanceConvergence.Evaluate(SectionSolver.FailureAnalysisTypes.ConstantN,
                input, new SectionAnalysisInput(capacity), capacity.CoordinateSystem, 1, 1, 1, .00025);
            return Converged
                ? new(new(CalculationStatus.Completed, ReportedEngine, Version, null, OmitEquilibrium ? null : new AxialEquilibriumEvidence(input.Forces.N, input.Forces.N, 1), convergence), ReportedCriterion, new(0, 0, -.001, 0, 0), capacity.N, capacity.M1, capacity.M2, .9, "test")
                : new(new(CalculationStatus.NotConverged, Id, Version), "ConstantN");
        }
    }
    [TestMethod]
    public void ExternalFactoryReceivesOwnedCopiesOfTheSectionAndStandard()
    {
        var model = MixedModelFactory.Create(); var standard = new StandardNTC2018Concrete();
        var prepared = Verification.PrepareBeam(model, 250,
            Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified), "factory ownership");
        var factory = new Alternative();
        factory.OnCreate = (section, receivedStandard) =>
        {
            Assert.AreNotSame(prepared.Input.Section, section);
            Assert.AreNotSame(standard, receivedStandard);
            section.Rebars.First().Position.Y += 100;
        };
        var verifier = new ConcreteSectionVerifier(factory, standard, SectionSolver.FailureAnalysisTypes.ConstantN,
            false, 32, 0, 0, "2018", null, SectionSolver.StressAnalysisTypes.Linear, 1);
        var result = Verification.Run(prepared, CheckMechanism.UlsBiaxialSection, verifier);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
        Assert.AreEqual(50, prepared.Input.Section.Rebars.First().Position.Y);
        Assert.AreEqual(50, ((ReinforcedConcreteSection)model.BeamElements[250].BeamProperty).Rebars.First().Position.Y);
        Assert.IsTrue(prepared.Input.IsCurrent);
    }

    [TestMethod]
    public void AlternativeCalculationUsesExistingMethodAndKeepsFailureUnevaluated()
    {
        var model = MixedModelFactory.Create(); var factory = new Alternative();
        var verifier = new ConcreteSectionVerifier(factory, new StandardNTC2018Concrete(), SectionSolver.FailureAnalysisTypes.ConstantN,
            false, 32, 0, 0, "2018", null, SectionSolver.StressAnalysisTypes.Linear, 1);
        var prepared = Verification.PrepareBeam(model, 250,
            Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified), "alternative solver");
        var result = Verification.Run(prepared, CheckMechanism.UlsBiaxialSection, verifier);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome); Assert.AreEqual(.9, result.Utilization);
        Assert.AreEqual(typeof(GPC.Checkers.Concrete.Checkers.SectionChecker).Assembly.GetName().Version!.ToString(), result.EngineVersion);
        StringAssert.Contains(result.EngineConfiguration, factory.Id);
        StringAssert.Contains(result.EngineConfiguration, "NumericalEngineVersion=test1");
        factory.Converged = false;
        result = Verification.Run(prepared, CheckMechanism.UlsBiaxialSection, verifier);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome); Assert.IsNull(result.Utilization);
        Assert.AreEqual(1, factory.Sessions); Assert.AreEqual(2, factory.Calls);
        factory.RejectedEquilibrium = true;
        result = Verification.Run(prepared, CheckMechanism.UlsBiaxialSection, verifier);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
        Assert.IsNull(result.Utilization);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "CheckerAxialEquilibriumRejected"));
        StringAssert.Contains(result.Diagnostics.Single(d => d.Code == "NumericalAxialEquilibrium").Message, "residual=2000 N; tolerance=1000 N");
        factory.RejectedEquilibrium = false;
        factory.Converged = true; factory.ReportedEngine = "wrong-engine";
        result = Verification.Run(prepared, CheckMechanism.UlsBiaxialSection, verifier);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "NumericalResistanceContractMismatch"));
        factory.ReportedEngine = factory.Id; factory.ReportedCriterion = "ConstantEccentricity";
        result = Verification.Run(prepared, CheckMechanism.UlsBiaxialSection, verifier);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
    }
}
