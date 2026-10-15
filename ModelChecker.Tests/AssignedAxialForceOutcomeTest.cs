using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Checker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Compatibility;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Results.Locations;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModelChecker.Tests;

/// <summary>
/// Typed outcomes of the criteria at assigned N of GPCChecker.Concrete (bug SLU-2 of 15/10/2026) in the ULS section check: an axial force
/// outside [NRd,c; NRd,t] is NotSatisfied with the reason AxialForceOutsideDomain, a zero moment at constant N inside the range is Satisfied with
/// utilization 0. Before they were failures (NotEvaluated) with the code NumericalResistanceConvergenceContractMismatch.
/// Section: RC 300 × 500 C25/30 with 4Ø16 B450C at 50 mm from the faces (MixedModelFactory, with the bar moved by the step-3 fixture put back),
/// NTC 2018 (αcc 0.85, γc 1.5, γs 1.15), pure axial force from the analytical cantilever of <see cref="ElementScopeCharacterizationTest.Model"/>
/// with no transverse load. Independent expected values, closed form (uniform strain εc2 = 2 ‰ > εyd = 1.957 ‰, bars at fyd):
/// NRd,c = -(0.85 · 25 / 1.5 · (150000 - 804.25) + 804.25 · 450 / 1.15) = -2428.31 kN, NRd,t = 804.25 · 450 / 1.15 = 314.71 kN; with no moment the
/// point at constant eccentricity is the end of the range, utilization |NEd / NRd,lim|.
/// </summary>
[TestClass]
public class AssignedAxialForceOutcomeTest
{
    private const double NRdc = -2428.31e3, NRdt = 314.71e3;

    private static CheckResult Check(double axial, SectionSolver.FailureAnalysisTypes criterion = SectionSolver.FailureAnalysisTypes.ConstantN)
    {
        var model = ElementScopeCharacterizationTest.Model(0, axial, 0,
            m => ((ReinforcedConcreteSection)m.BeamElements[250].Assignments.Sections[0].Section).Rebars.First().Position.Y -= 40);
        var prepared = Verification.PrepareBeam(model, 250,
            Verification.BeamSample(model.BeamElements[250], "synthetic-member", "LC1", .5, SectionSide.Unspecified), "assigned axial force");
        Assert.AreEqual(axial, prepared.Input.Forces.N, 1e-6);
        Assert.AreEqual(0, prepared.Input.Forces.M1, 1e-6); Assert.AreEqual(0, prepared.Input.Forces.M2, 1e-6);
        var verifier = new ConcreteSectionVerifier(new StandardNTC2018Concrete(), criterion, false, 32, 0, 0, "2018", null,
            SectionSolver.StressAnalysisTypes.NonLinear, 1);
        return Verification.Run(prepared, CheckMechanism.UlsBiaxialSection, verifier);
    }

    [DataTestMethod]
    [DataRow(-3000e3, SectionSolver.FailureAnalysisTypes.ConstantN)]
    [DataRow(400e3, SectionSolver.FailureAnalysisTypes.ConstantN)]
    [DataRow(-3000e3, SectionSolver.FailureAnalysisTypes.ConstantNMx)]
    [DataRow(400e3, SectionSolver.FailureAnalysisTypes.ConstantNMy)]
    public void AxialForceOutsideTheDomainIsNotSatisfied(double axial, SectionSolver.FailureAnalysisTypes criterion)
    {
        var result = Check(axial, criterion);
        string text = Step4Fixture.Describe(result);
        Assert.AreEqual(ExecutionStatus.Completed, result.Execution, text);
        Assert.AreEqual(DataStatus.Ready, result.Data, text);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, result.Outcome, text);
        double expected = Math.Abs(axial / (axial < 0 ? NRdc : NRdt));   // 1.2354 and 1.2710
        Assert.AreEqual(expected, result.Utilization!.Value, .005 * expected, text);
        var reason = result.Diagnostics.Single(d => d.Code == "AxialForceOutsideDomain");
        StringAssert.Contains(reason.Message, "outside [NRd,c; NRd,t]");
        var details = (SectionResistanceDetails)result.Details;
        Assert.AreEqual(SectionResistanceResponse.AxialForceOutsideDomainMode, details.FailureMode);
        Assert.AreEqual(axial < 0 ? NRdc : NRdt, details.NRd, .005 * Math.Abs(axial < 0 ? NRdc : NRdt), "declared point at the end of the range");
        Assert.IsFalse(result.Diagnostics.Any(d => d.Code.Contains("Mismatch")), text);
        // The range is in the message: [NRd,c; NRd,t] within 0.1 % of the closed form.
        var bounds = reason.Message.Substring(reason.Message.IndexOf("= [") + 3).Split(']')[0].Split(';')
            .Select(v => double.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Assert.AreEqual(NRdc, bounds[0], 1e-3 * Math.Abs(NRdc)); Assert.AreEqual(NRdt, bounds[1], 1e-3 * NRdt);
    }

    [TestMethod]
    public void ZeroMomentInsideTheRangeIsSatisfied()
    {
        var result = Check(-1000e3);
        string text = Step4Fixture.Describe(result);
        Assert.AreEqual(ExecutionStatus.Completed, result.Execution, text);
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, text);
        Assert.AreEqual(0.0, result.Utilization);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "ZeroMomentDemand"), text);
        var details = (SectionResistanceDetails)result.Details;
        Assert.AreEqual(SectionResistanceResponse.ZeroMomentDemandMode, details.FailureMode);
        Assert.AreEqual(-1000e3, details.NRd, 1000, "declared point at NEd within the tolerance on N");
        Assert.IsTrue(details.M1Rd > 0, "positive M1");
        // N and Mx constant with no moment: null direction of the search, register F2-17 option A: still not evaluated.
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Check(-1000e3, SectionSolver.FailureAnalysisTypes.ConstantNMx).Outcome);
    }

    /// <summary>An engine that declares the outcome through the public constructor, without the range of the domain.</summary>
    private sealed class Pretender : IConcreteCalculationFactory, ISectionResponseSolver, ISectionResistanceSolver
    {
        public string Id => "TEST-PRETENDER"; public string Version => "1"; public string Configuration => "contract-test-only";
        public ConcreteCalculationSession Create(ReinforcedConcreteSection section, StandardModelCode2010 standard, ConcreteCalculationOptions options) => new(this, this);
        public SectionResponse Solve(SectionAnalysisInput input, CancellationToken token) => throw new NotSupportedException();
        public SectionResistanceResponse SolveResistance(SectionAnalysisInput input, CancellationToken token) =>
            new(new SolverDiagnostics(CalculationStatus.Completed, Id, Version), "ConstantN", new SectionStrain(0, 0, -.002, 0, 0),
                input.Forces.N / 2, 0, 0, 2, SectionResistanceResponse.AxialForceOutsideDomainMode);
    }

    [TestMethod]
    public void AnOutcomeWithoutTheRangeIsNotAccepted()
    {
        var model = ElementScopeCharacterizationTest.Model(0, -3000e3, 0);
        var prepared = Verification.PrepareBeam(model, 250,
            Verification.BeamSample(model.BeamElements[250], "synthetic-member", "LC1", .5, SectionSide.Unspecified), "pretender");
        var verifier = new ConcreteSectionVerifier(new Pretender(), new StandardNTC2018Concrete(), SectionSolver.FailureAnalysisTypes.ConstantN,
            false, 32, 0, 0, "2018", null, SectionSolver.StressAnalysisTypes.Linear, 1);
        var result = Verification.Run(prepared, CheckMechanism.UlsBiaxialSection, verifier);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "NumericalAssignedAxialForceContractMismatch"), Step4Fixture.Describe(result));
    }
}
