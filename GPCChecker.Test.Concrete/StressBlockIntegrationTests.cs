using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class StressBlockIntegrationTests
    {
        internal static Mesh Grid(double width, double height, int n, bool triangles, bool reverse, Point2d origin)
        {
            var mesh = new Mesh();
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
                mesh.Vertices.Add(new MeshVertex(new Point3d(origin.X + width * i / n, origin.Y + height * j / n, 0)), j * (n + 1) + i);
            int face = 0;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, d = a + n + 1, c = d + 1;
                if (triangles)
                {
                    mesh.Faces.Add(reverse ? new MeshFace(c, b, a) : new MeshFace(a, b, c), face++);
                    mesh.Faces.Add(reverse ? new MeshFace(d, c, a) : new MeshFace(a, c, d), face++);
                }
                else mesh.Faces.Add(reverse ? new MeshFace(d, c, b, a) : new MeshFace(a, b, c, d), face++);
            }
            return mesh;
        }

        private static void Close(ForceTuple expected, ForceTuple actual, double forceScale, double lengthScale, string at)
        {
            Assert.AreEqual(expected.N, actual.N, forceScale * 2e-11, at + " N");
            Assert.AreEqual(expected.Mx, actual.Mx, forceScale * lengthScale * 2e-11, at + " Mx");
            Assert.AreEqual(expected.My, actual.My, forceScale * lengthScale * 2e-11, at + " My");
        }

        [TestMethod]
        public void RectangleCutsMatchClosedFormAcrossMeshesOrientationsAndScales()
        {
            foreach (double scale in new[] { 0.001, 1, 1000.0 })
            foreach (int n in new[] { 1, 5 })
            foreach (bool reverse in new[] { false, true })
            foreach (bool triangles in new[] { false, true })
            {
                double b = 300 * scale, h = 500 * scale;
                var reference = new Point2d(1234 * scale, -678 * scale);
                var integrator = new StressBlockIntegration(Grid(b, h, n, triangles, reverse, reference), reference);
                var law = new StressBlockIntegration.Law(-1, 0, -17);
                foreach (double fraction in new[] { -0.1, 0, 1e-8, .2, .500001, .99999999, 1, 1.1 })
                {
                    double cut = fraction * h, active = Math.Max(0, Math.Min(h, cut));
                    var plane = new StrainPlane(0, .001 / h, reference, -.001 * fraction);
                    double axial = law.Stress * b * active;
                    Close(new ForceTuple(axial, -axial * active / 2, axial * b / 2), integrator.Integrate(plane, law),
                        17 * b * h, Math.Max(b, h), scale + "/" + n + "/" + fraction);
                }
            }
        }

        [TestMethod]
        public void ObliqueCutsAndUltimateStrainBandMatchIndependentAreasAndCentroids()
        {
            var origin = new Point2d(1e9, -1e9);
            var integrator = new StressBlockIntegration(Grid(100, 100, 1, false, false, origin), origin);
            var law = new StressBlockIntegration.Law(-1, 0, -20);
            foreach (double cut in new[] { 1e-5, 1.0, 33.3, 100.0 })
            {
                // x+y <= cut is a right triangle, area=cut^2/2, centroid=(cut/3,cut/3).
                var plane = new StrainPlane(.001, .001, origin, -.001 * cut);
                double axial = -20 * cut * cut / 2;
                Close(new ForceTuple(axial, -axial * cut / 3, axial * cut / 3), integrator.Integrate(plane, law),
                    Math.Abs(axial), 100, "oblique " + cut);
            }
            var band = new StressBlockIntegration.Law(-.08, -.02, -20);
            var bandPlane = new StrainPlane(.001, 0, origin, -.1);
            Close(new ForceTuple(-120000, 6000000, -6000000), integrator.Integrate(bandPlane, band), 200000, 100, "band");
        }

        [TestMethod]
        public void UniformStrainRespectsBothMaterialEndpointsAndNoCurvatureDivision()
        {
            var integrator = new StressBlockIntegration(Grid(3, 5, 1, false, false, Point2d.Origin), Point2d.Origin);
            var law = new StressBlockIntegration.Law(-.0035, -.0007, -17);
            foreach (double strain in new[] { .001, 0, law.Threshold, law.Threshold - 1e-12, -.002, law.Ultimate, law.Ultimate - 1e-12 })
            {
                double n = (strain < law.Threshold && strain >= law.Ultimate) ? -17 * 15 : 0;
                Close(new ForceTuple(n, -n * 2.5, n * 1.5), integrator.Integrate(new StrainPlane(0, 0, Point2d.Origin, strain), law), 255, 5, strain.ToString());
            }
        }

        [TestMethod]
        public void MeshHolesAndConcaveCellsPreserveAreaAndStaticMoments()
        {
            var frame = Grid(300, 300, 3, false, false, Point2d.Origin);
            frame.Faces.Remove(4); // square hole [100,200] x [100,200]
            var law = new StressBlockIntegration.Law(-1, 0, -17);
            var half = new StressBlockIntegration(frame, Point2d.Origin).Integrate(new StrainPlane(0, .001, Point2d.Origin, -.15), law);
            // Lower half of the outer square minus the lower half of the hole.
            double area = 300 * 150 - 100 * 50, qx = 300 * 150 * 75 - 100 * 50 * 125;
            Close(new ForceTuple(-17 * area, 17 * qx, -17 * area * 150), half, 17 * 90000, 300, "hole");
            var concave = new Mesh();
            var vertices = new[] { new Point3d(0, 0, 0), new Point3d(4, 0, 0), new Point3d(1, 1, 0), new Point3d(0, 4, 0) };
            for (int i = 0; i < vertices.Length; i++) concave.Vertices.Add(new MeshVertex(vertices[i]), i);
            concave.Faces.Add(new MeshFace(0, 1, 2, 3), 0);
            var whole = new StressBlockIntegration(concave, Point2d.Origin).Integrate(new StrainPlane(0, 0, Point2d.Origin, -.5), law);
            Close(new ForceTuple(-68, 68, -68), whole, 68, 4, "concave");
        }

        [TestMethod]
        public void GeometricIntegrationIsIndependentOfConcurrentEvaluationOrder()
        {
            var integration = new StressBlockIntegration(Grid(300, 500, 8, true, false, Point2d.Origin), Point2d.Origin);
            var law = new StressBlockIntegration.Law(-1, 0, -17);
            var planes = Enumerable.Range(0, 100).Select(i => new StrainPlane(.00001, .000002, Point2d.Origin, -.00005 * i)).ToArray();
            var expected = planes.Select(p => integration.Integrate(p, law)).ToArray();
            Parallel.For(0, planes.Length, i => Assert.AreEqual(expected[i], integration.Integrate(planes[i], law)));
        }

        [TestMethod]
        public void SinglyReinforcedRectangleMatchesClosedFormUltimateResistance()
        {
            var shape = DomainPointContractTests.Sections.Single(s => s.Name == "R3");
            var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock);
            var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
            const double b = 300, d = 450, fcd = .85 * 25 / 1.5, fyd = 450 / 1.15;
            double tension = 3 * Math.PI * 20 * 20 / 4 * fyd, depth = tension / (b * fcd);
            double neutralDepth = depth / .8, moment = tension * (d - depth / 2);
            var plane = new StrainPlane(0, -.0035 / neutralDepth, new Point2d(0, 250), -.0035);
            var direct = solver.CalculateForceResultantForDomain(plane);
            Assert.AreEqual(0, direct.N, 1e-6, "Exact block equilibrium");
            Assert.AreEqual(moment, direct.Mx, .01, "As*fyd*(d-a/2), Nmm");
            Assert.AreEqual(0, direct.My, .01);
            var demand = DomainPointContractTests.Force(solver.SolverAxes, 0, 1, 0);
            var result = solver.CalculateConvergedDomainPoint(demand, SectionSolver.FailureAnalysisTypes.ConstantN, CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.AreEqual(moment, result.MxRd, moment * 2e-5, "Inverse domain search");
        }

        [TestMethod]
        public void ElasticCompressionAtBlockActivationHasZeroConcreteArea()
        {
            foreach (var shape in DomainPointContractTests.Sections)
            {
                var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock);
                var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Elastic).SectionSolver;
                foreach (int angle in Enumerable.Range(0, 12))
                foreach (double eta in new[] { .1, .4, .9 })
                {
                    var plane = solver.BuildPlane(angle * Math.PI / 6, SectionSolver.FailureDomainTypes.Elastic, SectionSolver.FailureZones.F3A, eta);
                    Assert.IsTrue(section.Mesh.Vertices.Min(p => plane.GetStrain(p.Point.X, p.Point.Y)) >= section.ConcreteMaterial.StrainYCompression - 1e-15);
                    Assert.AreEqual(0, solver.IntegrateSectionStress(plane).N, 1e-5, shape.Name + "/" + angle + "/" + eta);
                }
            }
        }

        [TestMethod]
        public void SmallPlasticEccentricityFindsAnActualPlaneWithoutFallback()
        {
            foreach (var shape in DomainPointContractTests.Sections)
            {
                var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock);
                var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
                double size = Math.Max(shape.Diameter, Math.Max(shape.Width, shape.Height));
                foreach (int angle in Enumerable.Range(0, 12))
                foreach (double scale in new[] { .01, 1, 100.0 })
                {
                    double theta = angle * Math.PI / 6, n = -100000 * scale, m = Math.Abs(n) * .05 * size;
                    var demand = new ResultBeamForces(n, 0, 0, 0, m * Math.Cos(theta), m * Math.Sin(theta), solver.SolverAxes);
                    int logs = solver.GetLog().Count;
                    var result = solver.CalculateConvergedDomainPoint(demand, SectionSolver.FailureAnalysisTypes.ConstantEccentricity, CancellationToken.None);
                    string at = shape.Name + "/" + angle + "/" + scale;
                    Assert.IsNotNull(result, at); Assert.AreEqual(logs, solver.GetLog().Count, at + " fallback");
                    Assert.IsTrue(DomainPointConvergence.Evaluate(solver, demand, result, SectionSolver.FailureAnalysisTypes.ConstantEccentricity).Accepted, at);
                    var forces = solver.GetExternalForces(solver.CalculateForceResultantForDomain(result.StrainPlane), solver.SectionOption.ForceReferenceCoordinateSystem);
                    Close(new ForceTuple(result.NRd, result.MxRd, result.MyRd), forces, 1e7, 1000, at);
                }
            }
        }

        [TestMethod]
        public void StressBlockSearchReintegratesTheReturnedPlaneForBothMainCriteria()
        {
            foreach (var shape in DomainPointContractTests.Sections)
            foreach (var state in DomainPointContractTests.States)
            {
                var section = DomainPointContractTests.BuildSection(shape, ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock);
                var solver = DomainPointContractTests.BuildChecker(section, state).SectionSolver;
                Assert.IsTrue(solver.TryGetStressBlockLaw(out _));
                foreach (double theta in new[] { .2, 1.1, 2.1 })
                foreach (var zone in new[] { SectionSolver.FailureZones.F3A, SectionSolver.FailureZones.F4 })
                {
                    var plane = solver.BuildPlane(theta, state, zone, .45);
                    var source = solver.GetExternalForces(solver.CalculateForceResultantForDomain(plane), solver.SectionOption.ForceReferenceCoordinateSystem);
                    foreach (var criterion in new[] { SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureAnalysisTypes.ConstantEccentricity })
                    {
                        var demand = new ResultBeamForces(criterion == SectionSolver.FailureAnalysisTypes.ConstantN ? source.N : .8 * source.N,
                            0, 0, 0, .8 * source.Mx, .8 * source.My, solver.SolverAxes);
                        string at = shape.Name + "/" + state + "/" + zone + "/" + theta + "/" + criterion;
                        var result = solver.CalculateConvergedDomainPoint(demand, criterion, CancellationToken.None);
                        Assert.IsNotNull(result, at);
                        Assert.IsTrue(DomainPointConvergence.Evaluate(solver, demand, result, criterion).Accepted, at);
                        var integrated = solver.GetExternalForces(solver.CalculateForceResultantForDomain(result.StrainPlane), solver.SectionOption.ForceReferenceCoordinateSystem);
                        Close(new ForceTuple(result.NRd, result.MxRd, result.MyRd), integrated, 1e7, 1000, at + " plane");
                    }
                }
            }
        }
    }
}
