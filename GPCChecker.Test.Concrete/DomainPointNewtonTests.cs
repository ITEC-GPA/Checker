using System;
using System.Linq;
using System.Threading.Tasks;
using GPC.Checker.SectionSolvers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class DomainPointNewtonTests
    {
        [TestMethod]
        public void SlenderSectionInTensionDoesNotReturnAnUnconvergedNewtonPoint()
        {
            var shape = new DomainPointContractTests.BenchSection { Name = "S1", Width = 1000, Height = 200, Fck = 25,
                Bars = Enumerable.Range(0, 5).SelectMany(i => new[] { new[] { -400 + 200.0 * i, -60.0, 12.0 }, new[] { -400 + 200.0 * i, 60.0, 12.0 } }).ToArray() };
            var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
            var force = DomainPointContractTests.Force(solver.SolverAxes, 221.277, Math.Cos(Math.PI / 6), 0.5);
            var result = new DomainPointStrategyNewton(solver).CalculateDomainPoint(force);
            // On this case the existing surface evaluation has a jump in moment direction near theta=0.00971427.
            // Returning null is legitimate; returning an inaccurate point as a Newton convergence is not.
            if (result != null) AssertConverged(solver, force, result, SectionSolver.FailureAnalysisTypes.ConstantN, "S1 Newton");
            var accepted = solver.CalculateConvergedDomainPoint(force, SectionSolver.FailureAnalysisTypes.ConstantN, System.Threading.CancellationToken.None);
            Assert.IsNotNull(accepted);
            Assert.IsTrue(DomainPointConvergence.Evaluate(solver, force, accepted, SectionSolver.FailureAnalysisTypes.ConstantN).Accepted);
        }

        [TestMethod]
        public void ContinuousDiagramsReturnTheActualConvergedResultantForAllFiveCriteria()
        {
            foreach (string name in new[] { "R1", "C1" })
            foreach (var diagram in DomainPointContractTests.Diagrams.Where(d => d != ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock))
            foreach (var state in DomainPointContractTests.States)
            {
                var section = DomainPointContractTests.BuildSection(DomainPointContractTests.Sections.Single(s => s.Name == name), diagram);
                var solver = DomainPointContractTests.BuildChecker(section, state).SectionSolver;
                foreach (double theta in new[] { 0.2, 2.1 })
                foreach (var field in new[] { SectionSolver.FailureZones.F3A, SectionSolver.FailureZones.F4 })
                {
                    // Forward/inverse consistency: the target is built from an admissible ultimate strain plane.
                    // Independent material/resistance accuracy is covered by the existing reference suites.
                    var plane = solver.BuildPlane(theta, state, field, 0.45);
                    var f = solver.GetExternalForces(solver.CalculateForceResultantForDomain(plane), solver.SectionOption.ForceReferenceCoordinateSystem);
                    foreach (SectionSolver.FailureAnalysisTypes criterion in Enum.GetValues(typeof(SectionSolver.FailureAnalysisTypes)))
                    {
                        double n = f.N, mx = f.Mx, my = f.My;
                        switch (criterion)
                        {
                            case SectionSolver.FailureAnalysisTypes.ConstantEccentricity: n *= 0.8; mx *= 0.8; my *= 0.8; break;
                            case SectionSolver.FailureAnalysisTypes.ConstantN: mx *= 0.8; my *= 0.8; break;
                            case SectionSolver.FailureAnalysisTypes.ConstantNMx: my *= 0.8; break;
                            case SectionSolver.FailureAnalysisTypes.ConstantNMy: mx *= 0.8; break;
                            case SectionSolver.FailureAnalysisTypes.ConstantMxMy: n *= 0.8; break;
                        }
                        var demand = new ResultBeamForces(n, 0, 0, 0, mx, my, solver.SolverAxes);
                        var result = new DomainPointStrategyNewton(solver).CalculateDomainPoint(demand, criterion);
                        string at = name + "/" + diagram + "/" + state + "/" + theta + "/" + field + "/" + criterion;
                        AssertConverged(solver, demand, result, criterion, at);
                    }
                }
            }
        }

        [TestMethod]
        public void IterationStopsOnTheReturnedAxialForceIncludingLargeSections()
        {
            var original = DomainPointContractTests.Sections.Single(s => s.Name == "R1");
            foreach (double k in new[] { 0.25, 1.0, 4.0 })
            {
                var scaled = new DomainPointContractTests.BenchSection { Name = "scaled", Width = original.Width * k, Height = original.Height * k,
                    Fck = original.Fck, Bars = original.Bars.Select(b => new[] { b[0] * k, b[1] * k, b[2] * k }).ToArray() };
                var section = DomainPointContractTests.BuildSection(scaled, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
                var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
                foreach (double n in new[] { 0, -300.0, -900.0 })
                {
                    var demand = DomainPointContractTests.Force(solver.SolverAxes, n * k * k, Math.Cos(0.52), Math.Sin(0.52));
                    var point = new DomainPointStrategyNewton(solver).CalculateDomainPoint(demand);
                    AssertConverged(solver, demand, point, SectionSolver.FailureAnalysisTypes.ConstantN, "scale=" + k + ",N=" + n);
                }
            }
        }

        [TestMethod]
        public void ConcurrentSearchesOfOneSolverAreDeterministic()
        {
            var section = UltimateSections.Asymmetric();
            var solver = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantN).SectionSolver;
            var forces = Enumerable.Range(0, 16).Select(i => UltimateSections.Force(section, -400, Math.Cos(i * 0.4), Math.Sin(i * 0.4))).ToArray();
            var expected = forces.Select(f => new DomainPointStrategyNewton(solver).CalculateDomainPoint(f)).ToArray();
            Parallel.For(0, forces.Length, i => {
                var actual = new DomainPointStrategyNewton(solver).CalculateDomainPoint(forces[i]);
                Assert.IsNotNull(expected[i]); Assert.IsNotNull(actual);
                Assert.AreEqual(expected[i].NRd, actual.NRd);
                Assert.AreEqual(expected[i].MxRd, actual.MxRd);
                Assert.AreEqual(expected[i].MyRd, actual.MyRd);
            });
        }

        [TestMethod]
        public void PureAxialPolesKeepTheDemandSignInBothDomains()
        {
            var section = UltimateSections.Symmetric();
            foreach (var state in DomainPointContractTests.States)
            foreach (int sign in new[] { -1, 1 })
            {
                var solver = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity, state).SectionSolver;
                var demand = UltimateSections.Force(section, sign * 100, 0, 0);
                var point = new DomainPointStrategyNewton(solver).CalculateDomainPoint(demand);
                AssertConverged(solver, demand, point, SectionSolver.FailureAnalysisTypes.ConstantEccentricity, state + "/" + sign);
                Assert.AreEqual(sign, Math.Sign(point.NRd));
            }
        }

        [TestMethod]
        public void ReturnedPointsRemainConsistentNearBothEndsOfFailureFields()
        {
            var section = UltimateSections.Symmetric();
            var solver = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantN).SectionSolver;
            foreach (var field in new[] { SectionSolver.FailureZones.F3A, SectionSolver.FailureZones.F3B, SectionSolver.FailureZones.F4 })
            foreach (double eta in new[] { 1e-6, 1 - 1e-6 })
            {
                var plane = solver.BuildPlane(0.4, SectionSolver.FailureDomainTypes.Plastic, field, eta);
                var f = solver.GetExternalForces(solver.CalculateForceResultantForDomain(plane), solver.SectionOption.ForceReferenceCoordinateSystem);
                var demand = new ResultBeamForces(f.N, 0, 0, 0, f.Mx * 0.8, f.My * 0.8, solver.SolverAxes);
                var point = new DomainPointStrategyNewton(solver).CalculateDomainPoint(demand);
                if (point != null) AssertConverged(solver, demand, point, SectionSolver.FailureAnalysisTypes.ConstantN, field + "/" + eta);
                else
                {
                    var result = solver.CalculateConvergedDomainPoint(demand, SectionSolver.FailureAnalysisTypes.ConstantN, System.Threading.CancellationToken.None);
                    Assert.IsNotNull(result, field + "/" + eta);
                    Assert.IsTrue(DomainPointConvergence.Evaluate(solver, demand, result, SectionSolver.FailureAnalysisTypes.ConstantN).Accepted, field + "/" + eta);
                }
            }
        }

        [TestMethod]
        public void NonFiniteAndZeroSearchDirectionsDoNotProduceAResistancePoint()
        {
            var section = UltimateSections.Symmetric();
            var solver = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity).SectionSolver;
            foreach (double n in new[] { 0.0, double.NaN, double.PositiveInfinity })
            {
                var demand = new ResultBeamForces(n, 0, 0, 0, 0, 0, solver.SolverAxes);
                Assert.IsNull(new DomainPointStrategyNewton(solver).CalculateDomainPoint(demand));
            }
        }

        [TestMethod]
        public void AssignedMomentsFindTheDirectCompressionBranchIndependentlyOfDemandAxialMagnitude()
        {
            foreach (var shape in DomainPointContractTests.Sections)
            foreach (var diagram in DomainPointContractTests.Diagrams.Where(d => d != ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock))
            foreach (var state in DomainPointContractTests.States)
            foreach (int sign in new[] { -1, 1 })
            {
                var section = DomainPointContractTests.BuildSection(shape, diagram);
                var solver = DomainPointContractTests.BuildChecker(section, state).SectionSolver;
                FailureDomain.FailureDomainPoint first = null;
                foreach (double n in new[] { -100.0, -1e7 })
                {
                    // N supplies the sign of the searched branch, not a prescribed axial force in this criterion.
                    var demand = new ResultBeamForces(n, 0, 0, 0, sign * 20e6, sign * 15e6, solver.SolverAxes);
                    var point = new DomainPointStrategyNewton(solver).CalculateDomainPoint(demand, SectionSolver.FailureAnalysisTypes.ConstantMxMy);
                    string at = shape.Name + "/" + diagram + "/" + state + "/" + sign + "/" + n;
                    AssertConverged(solver, demand, point, SectionSolver.FailureAnalysisTypes.ConstantMxMy, at);
                    Assert.IsTrue(point.NRd < 0, at);
                    if (first != null) Assert.AreEqual(first.NRd, point.NRd, Math.Abs(first.NRd) * 1e-10, at + ": magnitude independence");
                    first = point;
                }
            }
        }

        [TestMethod]
        public void SmallPlasticCompressionEccentricitiesConvergeDirectlyAndIndependentlyOfLoadMagnitude()
        {
            var slender = new DomainPointContractTests.BenchSection { Name = "S1", Width = 1000, Height = 200, Fck = 25,
                Bars = Enumerable.Range(0, 5).SelectMany(i => new[] { new[] { -400 + 200.0 * i, -60.0, 12.0 }, new[] { -400 + 200.0 * i, 60.0, 12.0 } }).ToArray() };
            var state = SectionSolver.FailureDomainTypes.Plastic;
            foreach (var shape in DomainPointContractTests.Sections.Concat(new[] { slender }))
            foreach (double angle in Enumerable.Range(0, 12).Select(i => 30.0 * i))
            {
                var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
                var solver = DomainPointContractTests.BuildChecker(section, state).SectionSolver;
                double size = Math.Max(shape.Diameter, Math.Max(shape.Width, shape.Height));
                double theta = angle * Math.PI / 180;
                FailureDomain.FailureDomainPoint first = null;
                foreach (double factor in new[] { 0.01, 1.0, 100.0 })
                {
                    double n = -100000 * factor, moment = Math.Abs(n) * 0.05 * size; // N, Nmm
                    var demand = new ResultBeamForces(n, 0, 0, 0, moment * Math.Cos(theta), moment * Math.Sin(theta), solver.SolverAxes);
                    var point = new DomainPointStrategyNewton(solver).CalculateDomainPoint(demand, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
                    string at = shape.Name + "/" + state + "/" + angle + "/" + factor;
                    AssertConverged(solver, demand, point, SectionSolver.FailureAnalysisTypes.ConstantEccentricity, at);
                    if (first != null)
                    {
                        double momentTolerance = Math.Max(1, Math.Sqrt(first.MxRd * first.MxRd + first.MyRd * first.MyRd)) * 1e-7;
                        Assert.AreEqual(first.NRd, point.NRd, Math.Abs(first.NRd) * 1e-7, at + ": N scale invariance");
                        Assert.AreEqual(first.MxRd, point.MxRd, momentTolerance, at + ": Mx scale invariance");
                        Assert.AreEqual(first.MyRd, point.MyRd, momentTolerance, at + ": My scale invariance");
                    }
                    first = point;
                }
            }
        }

        [TestMethod]
        public void TensionPointWithinNominalToleranceIsRetainedWhenStricterRefinementStalls()
        {
            var shape = DomainPointContractTests.Sections.Single(s => s.Name == "R3");
            var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
            double theta = 330 * Math.PI / 180;
            var demand = new ResultBeamForces(180000, 0, 0, 0, 1e6 * Math.Cos(theta), 1e6 * Math.Sin(theta), solver.SolverAxes);
            var point = new DomainPointStrategyNewton(solver).CalculateDomainPoint(demand, SectionSolver.FailureAnalysisTypes.ConstantN);
            Assert.IsNotNull(point);
            Assert.IsTrue(DomainPointConvergence.Evaluate(solver, demand, point, SectionSolver.FailureAnalysisTypes.ConstantN).Accepted);
            var integrated = solver.GetExternalForces(solver.CalculateForceResultantForDomain(point.StrainPlane), solver.SectionOption.ForceReferenceCoordinateSystem);
            Assert.AreEqual(integrated.N, point.NRd, 1e-6);
            Assert.AreEqual(integrated.Mx, point.MxRd, 1e-3);
            Assert.AreEqual(integrated.My, point.MyRd, 1e-3);
            Assert.IsTrue(Math.Abs(point.NRd - demand.N) <= DomainPointAxialTolerance.Calculate(solver, demand.N));
        }

        private static void AssertConverged(SectionSolver solver, ResultBeamForces demand, FailureDomain.FailureDomainPoint point,
            SectionSolver.FailureAnalysisTypes criterion, string at)
        {
            Assert.IsNotNull(point, at);
            var validation = DomainPointConvergence.Evaluate(solver, demand, point, criterion);
            Assert.IsTrue(validation.Accepted, at + ": " + string.Join(", ", validation.Residuals.Select(r => r.Quantity + "=" + r.Residual + "/" + r.Tolerance)));
            if (criterion == SectionSolver.FailureAnalysisTypes.ConstantN || criterion == SectionSolver.FailureAnalysisTypes.ConstantNMx || criterion == SectionSolver.FailureAnalysisTypes.ConstantNMy)
                Assert.IsTrue(Math.Abs(point.NRd - demand.N) <= DomainPointAxialTolerance.ConvergenceTolerance(solver), at + ": actual stopping tolerance");
            var integrated = solver.GetExternalForces(solver.CalculateForceResultantForDomain(point.StrainPlane), solver.SectionOption.ForceReferenceCoordinateSystem);
            Assert.AreEqual(integrated.N, point.NRd, 1e-6, at + ": N of returned plane");
            Assert.AreEqual(integrated.Mx, point.MxRd, 1e-3, at + ": Mx of returned plane");
            Assert.AreEqual(integrated.My, point.MyRd, 1e-3, at + ": My of returned plane");
        }
    }
}
