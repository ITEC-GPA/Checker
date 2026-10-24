using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class AnalyticParabolaIntegrationTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task PublicSyncAsyncAndBatchStressEntriesUseTheSelectedMaterialMethod(bool historical)
        {
            var shape = DomainPointContractTests.Sections.Single(s => s.Name == "R2");
            var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var material = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
#pragma warning disable CS0618 // Intentional historical regression coverage.
            if (historical) material.ParabolaRectangleMethod = ConcreteParabolaRectangleMethod.TabulatedLegacy;
#pragma warning restore CS0618
            const double strain = -.000833;
            double r = strain / -.002;
            double stress = historical ? material.StressStrainTableCompression.GetStress(strain) : -shape.Fck * r * (2 - r);
            stress *= .85 / 1.5;
            double axial = shape.Width * shape.Height * stress;
            foreach (var bar in section.GetRebars()) axial += bar.Area * (200000 * strain - stress);
            var axes = DomainPointContractTests.LocalAxes(section);
            var forces = new[] { DomainPointContractTests.Force(axes, axial / 1000, 0, 0) };
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, forces, null), options,
                new StandardNTC2018Concrete { AlphaCC = .85, GammaC = 1.5, GammaS = 1.15 }, false);
            Assert.AreEqual(!historical, checker.SectionSolver.TryGetParabolaLaw(out _));
            var sync = checker.GetTensionAnalysisResult(forces[0]);
            var asyncSingle = await checker.GetTensionAnalysisResultAsync(forces[0]);
            var syncBatch = checker.GetTensionAnalysisResult();
            var asyncBatch = await checker.GetTensionAnalysisResultAsync();
            Assert.IsNotNull(syncBatch); Assert.IsNotNull(asyncBatch);
            Assert.AreEqual(1, syncBatch.Length); Assert.AreEqual(1, asyncBatch.Length);
            foreach (var result in new[] { sync, asyncSingle, syncBatch[0], asyncBatch[0] })
            {
                Assert.IsNotNull(result?.StrainPlane);
                Assert.AreEqual(strain, result.StrainPlane.GetStrain(Point2d.Origin), 2e-8);
                Assert.AreEqual(stress, result.GetConcreteTension(Point2d.Origin), .001);
            }
        }

        [TestMethod]
        public async Task AsyncDomainsAndTheirForceQueriesUseTheSameAnalyticIntegration()
        {
            var section = DomainPointContractTests.BuildSection(DomainPointContractTests.Sections.Single(s => s.Name == "R3"),
                ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var checker = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic);
            var sync = checker.GetFailureDomainResult();
            var asyncDomain = await checker.GetFailureDomainResultAsync();
            Assert.IsNotNull(asyncDomain);
            var expected = sync.Domain.DomainPoints.SelectMany(p => p).ToArray();
            var actual = asyncDomain.Domain.DomainPoints.SelectMany(p => p).ToArray();
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.AreEqual(expected[i].NRd, actual[i].NRd, 0);
                Assert.AreEqual(expected[i].MxRd, actual[i].MxRd, 0);
                Assert.AreEqual(expected[i].MyRd, actual[i].MyRd, 0);
            }
            var sync2d = checker.GetFailureDomainResult2d();
            var async2d = await checker.GetFailureDomainResult2dAsync();
            Assert.IsNotNull(async2d);
            Assert.AreEqual(sync2d.Domain.DomainPoints.Length, async2d.Domain.DomainPoints.Length);
            for (int i = 0; i < sync2d.Domain.DomainPoints.Length; i++)
            {
                Assert.AreEqual(sync2d.Domain.DomainPoints[i].NRd, async2d.Domain.DomainPoints[i].NRd, 0);
                Assert.AreEqual(sync2d.Domain.DomainPoints[i].MxRd, async2d.Domain.DomainPoints[i].MxRd, 0);
            }
            var force = DomainPointContractTests.Force(checker.SectionSolver.SolverAxes, 0, 1, 0);
            var point = await asyncDomain.CalculateForceAsync(force);
            Assert.IsNotNull(point);
            const double k = .002 / .0035;
            double tension = 3 * Math.PI * 100 * 450 / 1.15;
            double x = tension / (300 * .85 * 25 / 1.5 * (1 - k / 3));
            double centre = x * (.5 - k / 3 + k * k / 12) / (1 - k / 3);
            Assert.AreEqual(tension * (450 - centre), point.MxRd, 2e-5 * tension * 450);
        }

        private static void Close(ForceTuple expected, ForceTuple actual, double scale, double length, string at)
        {
            Assert.AreEqual(expected.N, actual.N, scale * 2e-11, at + " N");
            Assert.AreEqual(expected.Mx, actual.Mx, scale * length * 2e-11, at + " Mx");
            Assert.AreEqual(expected.My, actual.My, scale * length * 2e-11, at + " My");
        }

        [TestMethod]
        public void RectangleMatchesIndependentOneDimensionalAntiderivatives()
        {
            double F(double r) => r <= 1 ? r * r - r * r * r / 3 : r - 1.0 / 3;
            double G(double r) => r <= 1 ? 2 * r * r * r / 3 - Math.Pow(r, 4) / 4 : r * r / 2 - 1.0 / 12;
            var law = new ParabolaRectangleLaw(-.0035, -.002, -17);
            foreach (double scale in new[] { .001, 1, 1000.0 })
            foreach (int divisions in new[] { 1, 5 })
            foreach (bool triangles in new[] { false, true })
            foreach (bool reverse in new[] { false, true })
            {
                double b = 300 * scale, h = 500 * scale, d = .4 * h;
                var origin = new Point2d(1234 * scale, -678 * scale);
                var integration = new StressBlockIntegration(StressBlockIntegrationTests.Grid(b, h, divisions, triangles, reverse, origin), origin);
                foreach (double cut in new[] { -.1, 0, 1e-8, .2, .6, 1, 1.3, 2.0 }.Select(t => t * h))
                {
                    double lo = Math.Max(0, (cut - h) / d), hi = Math.Min(1.75, cut / d);
                    double j0 = hi > lo ? F(hi) - F(lo) : 0, j1 = hi > lo ? G(hi) - G(lo) : 0;
                    double n = law.Stress * b * d * j0, qy = law.Stress * b * d * (cut * j0 - d * j1);
                    var plane = new StrainPlane(0, -law.Peak / d, origin, law.Peak * cut / d);
                    Close(new ForceTuple(n, -qy, n * b / 2), integration.Integrate(plane, law), Math.Abs(law.Stress) * b * h, h, cut.ToString());
                }
            }
        }

        [TestMethod]
        public void ObliqueParabolaHasIndependentTriangleResultantAndCentroid()
        {
            var origin = new Point2d(1e9, -1e9);
            var integration = new StressBlockIntegration(StressBlockIntegrationTests.Grid(100, 100, 1, false, false, origin), origin);
            var law = new ParabolaRectangleLaw(-.0035, -.002, -17);
            foreach (double cut in new[] { 1e-5, 1.0, 33.3, 100.0 })
            {
                // r=1-(x+y)/cut. Integral(2*r-r*r)=A/2; weighted centroid=(4*cut/15,4*cut/15).
                double n = law.Stress * cut * cut / 4, centre = 4 * cut / 15;
                var plane = new StrainPlane(-law.Peak / cut, -law.Peak / cut, origin, law.Peak);
                Close(new ForceTuple(n, -n * centre, n * centre), integration.Integrate(plane, law), Math.Abs(n), 100, cut.ToString());
            }
        }

        [TestMethod]
        public void UniformPlaneAndConcurrentEvaluationRespectMaterialEndpoints()
        {
            var integration = new StressBlockIntegration(StressBlockIntegrationTests.Grid(3, 5, 3, true, true, Point2d.Origin), Point2d.Origin);
            var law = new ParabolaRectangleLaw(-.0035, -.002, -17);
            var strains = new[] { .001, 0, -.000125, -.001, law.Peak, -.003, law.Ultimate, law.Ultimate - 1e-12 };
            Parallel.For(0, strains.Length, i =>
            {
                double e = strains[i], r = e / law.Peak;
                double stress = e >= 0 || e < law.Ultimate ? 0 : e <= law.Peak ? law.Stress : law.Stress * (2 * r - r * r);
                double n = 15 * stress;
                Close(new ForceTuple(n, -2.5 * n, 1.5 * n), integration.Integrate(new StrainPlane(0, 0, Point2d.Origin, e), law), 255, 5, e.ToString());
            });
        }

        [TestMethod]
        public void CustomTablesHighStrengthAndTensionOptionsRetainTheirOriginalLaw()
        {
            var section = DomainPointContractTests.BuildSection(DomainPointContractTests.Sections[0], ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
            Assert.IsTrue(solver.TryGetParabolaLaw(out var law));
            double e = law.Peak / 16;
            Assert.AreEqual(law.Stress * (2.0 / 16 - 1.0 / 256), solver.CalculateSigmaC(e), 1e-12);
            solver.ConsiderTensileConcrete = true;
            Assert.IsFalse(solver.TryGetParabolaLaw(out _));
            solver.ConsiderTensileConcrete = false;
            var table = section.ConcreteMaterial.StressStrainTableCompression;
            table.SetStress(table.Stresses[1] * .99, 1); // In-place mutation must be observed, including after prior analytic calls.
            Assert.IsFalse(solver.TryGetParabolaLaw(out _));
            var material = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
            material.Fck = -70;
            Assert.IsFalse(solver.TryGetParabolaLaw(out _));
        }

        [TestMethod]
        public void SinglyReinforcedRectangleMatchesAnalyticParabolaResistance()
        {
            var section = DomainPointContractTests.BuildSection(DomainPointContractTests.Sections.Single(s => s.Name == "R3"), ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
            const double b = 300, d = 450, fcd = .85 * 25 / 1.5, fyd = 450 / 1.15, k = .002 / .0035;
            double tension = 3 * Math.PI * 20 * 20 / 4 * fyd;
            double x = tension / (b * fcd * (1 - k / 3));
            double centroid = x * (.5 - k / 3 + k * k / 12) / (1 - k / 3);
            double moment = tension * (d - centroid);
            var plane = new StrainPlane(0, -.0035 / x, new Point2d(0, 250), -.0035);
            var forces = solver.CalculateForceResultantForDomain(plane);
            Assert.AreEqual(0, forces.N, 1e-6);
            Assert.AreEqual(moment, forces.Mx, .01);
            Assert.AreEqual(0, forces.My, .01);
            var demand = DomainPointContractTests.Force(solver.SolverAxes, 0, 1, 0);
            var result = solver.CalculateConvergedDomainPoint(demand, SectionSolver.FailureAnalysisTypes.ConstantN, CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.AreEqual(moment, result.MxRd, 2e-5 * moment);
        }

        [TestMethod]
        public void BarSubtractionAndReportedStressesUseTheSameAnalyticLaw()
        {
            var shape = DomainPointContractTests.Sections[0];
            var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
            Assert.IsTrue(solver.TryGetParabolaLaw(out var law));
            double e = law.Peak / 16, sigma = law.Stress * (2.0 / 16 - 1.0 / 256);
            double n = shape.Width * shape.Height * sigma;
            foreach (var bar in section.GetRebars()) n += bar.Area * (solver.CalculateStressRebar(bar, e) - sigma);
            var plane = new StrainPlane(0, 0, Point2d.Origin, e);
            Assert.AreEqual(n, solver.CalculateForceResultantForDomain(plane).N, 1e-6);
            var summary = plane.CalculateStrainPlaneResult(section, DomainPointContractTests.Force(solver.SolverAxes, n / 1000, 0, 0),
                solver, ((SectionSolverModelCode2010)solver).StandardModelCode2010);
            Assert.AreEqual(sigma, summary.SigmaCMin, 1e-12);
            Assert.AreEqual(sigma, summary.SigmaCMax, 1e-12);
            // The public result API has an explicit standard argument; preserve that contract too.
            var otherStandard = new GPC.Model.Standards.StandardNTC2018Concrete { AlphaCC = .75, GammaC = 1.5, GammaS = 1.15 };
            var otherSummary = plane.CalculateStrainPlaneResult(section, DomainPointContractTests.Force(solver.SolverAxes, n / 1000, 0, 0), solver, otherStandard);
            Assert.AreEqual(sigma * .75 / .85, otherSummary.SigmaCMin, 1e-12);
        }

        [TestMethod]
        public void NearCompressionPoleStillReturnsAnAcceptedActualPlane()
        {
            var section = UltimateSections.Symmetric();
            var solver = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantN).SectionSolver;
            foreach (double eta in new[] { .99, .9999, .999999 })
            {
                var source = solver.BuildPlane(.4, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.FailureZones.F4, eta);
                var f = solver.GetExternalForces(solver.CalculateForceResultantForDomain(source), solver.SectionOption.ForceReferenceCoordinateSystem);
                var demand = new GPC.Model.Results.ResultBeamForces(f.N, 0, 0, 0, .8 * f.Mx, .8 * f.My, solver.SolverAxes);
                var result = solver.CalculateConvergedDomainPoint(demand, SectionSolver.FailureAnalysisTypes.ConstantN, CancellationToken.None);
                Assert.IsNotNull(result);
                var check = DomainPointConvergence.Evaluate(solver, demand, result, SectionSolver.FailureAnalysisTypes.ConstantN);
                string details = eta + ": target=" + f.N + "," + f.Mx + "," + f.My + "; result=" + result.NRd + "," + result.MxRd + "," + result.MyRd +
                    "; residuals=" + string.Join(";", check.Residuals.Select(r => r.Quantity + "=" + r.Residual));
                Console.WriteLine(details);
                Assert.IsTrue(check.Accepted, details);
            }
        }

        [TestMethod]
        public void NonlinearStressPlanesSatisfyIndependentAnalyticFibreEquilibrium()
        {
            var shape = DomainPointContractTests.Sections[0];
            var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
            double fcd = .85 * shape.Fck / 1.5;
            double Concrete(double e) => e >= 0 || e < -.0035 ? 0 : e <= -.002 ? -fcd : -fcd * (2 * (e / -.002) - Math.Pow(e / -.002, 2));
            foreach (var load in new[] { (-800.0, 150.0, 0.0), (-1500.0, 60.0, -30.0), (0.0, 100.0, 0.0) })
            {
                var demand = DomainPointContractTests.Force(solver.SolverAxes, load.Item1, load.Item2, load.Item3);
                var target = new ForceTuple(demand);
                var result = solver.GetStressAnalysisResult(demand);
                Assert.IsNotNull(result?.StrainPlane);
                var plane = result.StrainPlane;
                const int nx = 600, ny = 1000;
                double dx = shape.Width / nx, dy = shape.Height / ny, n = 0, mx = 0, my = 0;
                for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    double x = -shape.Width / 2 + (i + .5) * dx, y = -shape.Height / 2 + (j + .5) * dy;
                    double force = Concrete(plane.GetStrain(x, y)) * dx * dy;
                    n += force; mx -= force * y; my += force * x;
                }
                foreach (var bar in section.GetRebars())
                {
                    double e = plane.GetStrain(bar.Position), steel = Math.Max(-450 / 1.15, Math.Min(450 / 1.15, 200000 * e));
                    double force = bar.Area * (steel - Concrete(e));
                    n += force; mx -= force * bar.Position.Y; my += force * bar.Position.X;
                }
                var external = solver.GetExternalForces(new ForceTuple(n, mx, my), solver.SectionOption.ForceReferenceCoordinateSystem);
                Console.WriteLine(load + ": independent residuals [N,Nmm,Nmm] " + (external.N - demand.N) + "," + (external.Mx - target.Mx) + "," + (external.My - target.My));
                Assert.AreEqual(demand.N, external.N, 50, "Independent analytic fibres: N");
                Assert.AreEqual(target.Mx, external.Mx, 25000, "Independent analytic fibres: Mx");
                Assert.AreEqual(target.My, external.My, 25000, "Independent analytic fibres: My");
            }
        }

        [TestMethod]
        public void MainCriteriaReturnActualPlanesIncludingSmallEccentricities()
        {
            foreach (var shape in DomainPointContractTests.Sections)
            foreach (var state in DomainPointContractTests.States)
            {
                var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
                var solver = DomainPointContractTests.BuildChecker(section, state).SectionSolver;
                foreach (int angle in Enumerable.Range(0, 12))
                foreach (double ratio in new[] { .05, .2, .75 })
                {
                    double theta = angle * Math.PI / 6, size = Math.Max(shape.Diameter, Math.Max(shape.Width, shape.Height)) / 1000;
                    var demand = DomainPointContractTests.Force(solver.SolverAxes, -100, 100 * size * ratio * Math.Cos(theta), 100 * size * ratio * Math.Sin(theta));
                    foreach (var criterion in new[] { SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureAnalysisTypes.ConstantEccentricity })
                    {
                        string at = shape.Name + "/" + state + "/" + angle + "/" + ratio + "/" + criterion;
                        var result = solver.CalculateConvergedDomainPoint(demand, criterion, CancellationToken.None);
                        Assert.IsNotNull(result, at);
                        Assert.IsTrue(DomainPointConvergence.Evaluate(solver, demand, result, criterion).Accepted, at);
                        var integrated = solver.GetExternalForces(solver.CalculateForceResultantForDomain(result.StrainPlane), solver.SectionOption.ForceReferenceCoordinateSystem);
                        Close(new ForceTuple(result.NRd, result.MxRd, result.MyRd), integrated, 1e7, 1000, at);
                    }
                }
            }
        }
    }
}
