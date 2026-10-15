// Raw domain point on purpose: the bug was in the iterative strategy behind the raw entry points (architecture review R2).
#pragma warning disable CS0618
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.SectionSolvers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ConcreteTests
{
    /// <summary>
    /// The iterative search at constant eccentricity follows the whole line of the demand through the origin; with the elastic (SLV) domain a
    /// pure tension stopped on the other side of the line, at the pole of compression (N = -2422.6 kN for N = +100 kN; found with bug SLU-1 of
    /// 15/10/2026). The point is now refused and searched again by the fallbacks of the solver.
    /// Section 300 × 500 C25/30 4Ø16 NTC 2018 (<see cref="UltimateSections.Symmetric"/>), elastic domain, iterative strategy. Independent expected
    /// value, closed form: the elastic domain limits the bars at the yield strain, so in pure tension NRd,t = As fyd = 804.25 · 450 / 1.15 =
    /// 314.71 kN, with no moment; utilization |NEd| / NRd,t.
    /// </summary>
    [TestClass]
    public class ElasticTensionPoleTests
    {
        private const double NRdt = 314.71e3;

        [TestMethod]
        public void PureTensionAtConstantEccentricityReachesTheTensionPole()
        {
            var section = UltimateSections.Symmetric();
            foreach (double n in new[] { 100.0, 400.0 })
            {
                var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Elastic);
                var force = UltimateSections.Force(section, n, 0, 0);
                var point = checker.CalculateFailureDomainPoint(force, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
                Assert.AreEqual(NRdt, point.NRd, 1e-3 * NRdt, "raw point, N = " + n + " kN");
                Assert.AreEqual(0, point.MxRd / 1e6, 0.1, "raw point, N = " + n + " kN");
                Assert.AreEqual(0, point.MyRd / 1e6, 0.1, "raw point, N = " + n + " kN");
                var response = new LegacySectionCalculation(checker).SolveResistance(new SectionAnalysisInput(force));
                Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status);
                Assert.AreEqual(n * 1e3 / NRdt, response.Utilization.Value, 1e-3 * n * 1e3 / NRdt, "utilization, N = " + n + " kN");
            }
        }
    }
}
