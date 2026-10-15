using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.SectionSolvers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace ConcreteTests
{
    /// <summary>
    /// Bug SLU-2 of 15/10/2026 (finding sezione-slu F2): with the criteria at assigned N (ConstantN, ConstantNMx, ConstantNMy) an axial force
    /// outside [NRd,c; NRd,t] gave NotConverged («punto resistente non convergente» in ANTHEA, summary «DA COMPLETARE»), and with ConstantN a zero
    /// moment demand gave NotConverged also inside the range (null direction of the search). Now: completed typed outcomes
    /// <see cref="SectionResistanceResponse.AxialForceOutsideDomainMode"/> (utilization above 1, point at constant eccentricity) and
    /// <see cref="SectionResistanceResponse.ZeroMomentDemandMode"/> (utilization 0). The other cases of register F2-17 (null direction and residuals
    /// beyond the tolerance of ConstantNMx and ConstantNMy with N inside the range) stay NotConverged (option A of the user).
    /// Section 300 × 500 C25/30 4Ø16 NTC 2018 (<see cref="UltimateSections.Symmetric"/>). Expected values, independent of the library:
    /// NRd,c = -(fcd (Ac - As) + As fyd) = -2428.31 kN and NRd,t = As fyd = 314.71 kN (closed form of the verifier, scratchpad/bug/sezione-slu-ncost/
    /// ref.py, and agent-conc-slu/ref_slu.py), |NEd / NRd,lim| of the same closed form, MRd,x(N = -1000 kN) = 191.93 kNm and the point at constant
    /// eccentricity of (-3000; 50) of the fibre model of agent-conc-slu/ref_slu.py (η = 1.3451).
    /// </summary>
    [TestClass]
    public class AssignedAxialForceOutcomeTests
    {
        private const double NRdc = -2428.31e3, NRdt = 314.71e3;

        private static SectionResistanceResponse Solve(SectionSolver.FailureAnalysisTypes criterion, double nKn, double mxKnm, double myKnm)
        {
            var section = UltimateSections.Symmetric();
            var checker = UltimateSections.Checker(section, criterion);
            return new LegacySectionCalculation(checker).SolveResistance(new SectionAnalysisInput(UltimateSections.Force(section, nKn, mxKnm, myKnm)));
        }

        private static void AssertRange(SectionResistanceResponse response, string id)
        {
            Assert.AreEqual(NRdc, response.AxialCompressionResistance.Value, 1e-3 * Math.Abs(NRdc), id + ": NRd,c");
            Assert.AreEqual(NRdt, response.AxialTensionResistance.Value, 1e-3 * NRdt, id + ": NRd,t");
        }

        [TestMethod]
        public void ZeroMomentDemandInsideTheRangeHasZeroUtilization()
        {
            foreach (double n in new[] { -1000.0, -2000, 200 })
            {
                string id = "N = " + n + " kN";
                var response = Solve(SectionSolver.FailureAnalysisTypes.ConstantN, n, 0, 0);
                Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status, id);
                Assert.IsTrue(response.IsZeroMomentDemand, id);
                Assert.IsFalse(response.IsAxialForceOutsideDomain || response.IsZeroDemand, id);
                Assert.AreEqual(SectionResistanceResponse.ZeroMomentDemandMode, response.FailureMode, id);
                Assert.AreEqual(SectionResistanceResponse.ZeroMomentDemandMessage, response.Diagnostics.Message, id);
                Assert.AreEqual("ConstantN", response.Criterion, id);
                Assert.AreEqual(0.0, response.Utilization.Value, id);
                AssertRange(response, id);
                // The declared point is at the assigned N, for a positive M1 of the force reference axes.
                Assert.IsTrue(response.Diagnostics.AxialEquilibrium.Accepted, id);
                Assert.AreEqual(n * 1000, response.N.Value, response.Diagnostics.AxialEquilibrium.Tolerance, id);
                Assert.IsTrue(response.M1.Value > 0 && Math.Abs(response.M2.Value) < 1e-3 * response.M1.Value, id);
                Assert.IsTrue(response.Diagnostics.ResistanceConvergence.Accepted, id);
                Assert.IsNotNull(response.Strain, id);
            }
            // MRd,x at N = -1000 kN: 191.93 kNm (verifier's ref.py and agent-conc-slu/ref_slu.py).
            Assert.AreEqual(191.93e6, Solve(SectionSolver.FailureAnalysisTypes.ConstantN, -1000, 0, 0).M1.Value, 0.01 * 191.93e6, "MRd,x(-1000 kN)");
        }

        [TestMethod]
        public void AxialForceOutsideTheRangeIsNotVerifiedForAnyMoment()
        {
            foreach (var criterion in new[] { SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureAnalysisTypes.ConstantNMx,
                SectionSolver.FailureAnalysisTypes.ConstantNMy })
            {
                // (N, Mx, My) kN, kNm and the independent utilization (NaN: only above 1)
                foreach (var (n, mx, my, expected, tolerance) in new[]
                {
                    (-3000.0, 0.0, 0.0, 3000 / 2428.31, 0.005), (400, 0, 0, 400 / 314.71, 0.005), (-5000, 0, 0, 5000 / 2428.31, 0.005),
                    (-3000, 50, 0, 1.3451, 0.01), (-2600, 5, 0, double.NaN, 0), (-3000, 50, 20, double.NaN, 0), (400, 10, 0, double.NaN, 0),
                })
                {
                    string id = criterion + " (" + n + "; " + mx + "; " + my + ")";
                    var response = Solve(criterion, n, mx, my);
                    Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status, id + ": " + response.Diagnostics.Message);
                    Assert.IsTrue(response.IsAxialForceOutsideDomain, id);
                    Assert.AreEqual(SectionResistanceResponse.AxialForceOutsideDomainMode, response.FailureMode, id);
                    Assert.AreEqual(SectionResistanceResponse.AxialForceOutsideDomainMessage, response.Diagnostics.Message, id);
                    Assert.AreEqual(criterion.ToString(), response.Criterion, id);
                    AssertRange(response, id);
                    Assert.IsTrue(response.Utilization.Value > 1, id + ": " + response.Utilization);
                    if (!double.IsNaN(expected))
                        Assert.AreEqual(expected, response.Utilization.Value, tolerance * expected, id);
                    // The declared point is the point at constant eccentricity of the demand: the same as the criterion ConstantEccentricity.
                    Assert.IsNull(response.Diagnostics.AxialEquilibrium, id);
                    Assert.AreEqual(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, response.Diagnostics.ResistanceConvergence.Criterion, id);
                    Assert.IsTrue(response.Diagnostics.ResistanceConvergence.Accepted, id);
                    var eccentricity = Solve(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, n, mx, my);
                    Assert.AreEqual(eccentricity.Utilization.Value, response.Utilization.Value, 1e-9, id + ": constant eccentricity");
                    Assert.AreEqual(eccentricity.N.Value, response.N.Value, 1e-6, id + ": NRd");
                }
            }
        }

        [TestMethod]
        public void InsideTheRangeTheSearchIsUnchanged()
        {
            // Constant N with a moment: η = 50 / MRd,x(-1000 kN) = 50 / 191.93 = 0.2605 (independent MRd, see the class).
            var withMoment = Solve(SectionSolver.FailureAnalysisTypes.ConstantN, -1000, 50, 0);
            Assert.AreEqual(CalculationStatus.Completed, withMoment.Diagnostics.Status);
            Assert.IsFalse(withMoment.IsZeroMomentDemand || withMoment.IsAxialForceOutsideDomain);
            Assert.IsNull(withMoment.AxialCompressionResistance);
            Assert.AreEqual(50 / 191.93, withMoment.Utilization.Value, 0.01 * 50 / 191.93);
            // Register F2-17, option A: null direction of the search with N and Mx constant and N inside the range stays NotConverged.
            foreach (var (n, mx, my) in new[] { (-1000.0, 0.0, 0.0), (-1000, 50, 0) })
            {
                var response = Solve(SectionSolver.FailureAnalysisTypes.ConstantNMx, n, mx, my);
                Assert.AreEqual(CalculationStatus.NotConverged, response.Diagnostics.Status, "ConstantNMx (" + n + "; " + mx + "; " + my + ")");
                Assert.AreEqual("The returned point does not satisfy the search constraints.", response.Diagnostics.Message);
                Assert.IsNull(response.Utilization);
            }
            // Zero N-M1-M2 demand keeps its own outcome.
            Assert.IsTrue(Solve(SectionSolver.FailureAnalysisTypes.ConstantN, 0, 0, 0).IsZeroDemand);
        }

        [TestMethod]
        public void TypedOutcomesRequireTheirUtilization()
        {
            var diagnostics = new SolverDiagnostics(CalculationStatus.Completed, "test", "1");
            var strain = new SectionStrain(0, 0, 0, 0, 0);
            Assert.ThrowsException<ArgumentException>(() => SectionResistanceResponse.ForAssignedAxialForce(diagnostics, "ConstantN",
                SectionResistanceResponse.AxialForceOutsideDomainMode, strain, -1, 0, 0, 1.0, NRdc, NRdt));
            Assert.ThrowsException<ArgumentException>(() => SectionResistanceResponse.ForAssignedAxialForce(diagnostics, "ConstantN",
                SectionResistanceResponse.ZeroMomentDemandMode, strain, -1, 0, 0, 0.5, NRdc, NRdt));
            Assert.ThrowsException<ArgumentException>(() => SectionResistanceResponse.ForAssignedAxialForce(new SolverDiagnostics(CalculationStatus.NotConverged,
                "test", "1"), "ConstantN", SectionResistanceResponse.ZeroMomentDemandMode, strain, -1, 0, 0, 0, NRdc, NRdt));
            var plain = new SectionResistanceResponse(diagnostics, "ConstantN", strain, -1, 0, 0, 0.5, "F3A");
            Assert.IsFalse(plain.IsAxialForceOutsideDomain || plain.IsZeroMomentDemand);
            Assert.IsNull(plain.AxialTensionResistance);
        }
    }
}
