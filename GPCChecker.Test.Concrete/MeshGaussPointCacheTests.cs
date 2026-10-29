using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace ConcreteTests
{
    /// <summary>
    /// Cache of the Gauss points of the concrete meshes (30/10/2026): the solvers of equal meshes share one array, with the points a new
    /// computation gives bit for bit; other meshes and other numbers of points are not shared; the stresses do not change
    /// </summary>
    [TestClass, DoNotParallelize]
    public class MeshGaussPointCacheTests : ConcreteTestBase
    {
        private const QuadrangleGaussPoints.GaussPointNumber Quad = QuadrangleGaussPoints.GaussPointNumber.Quad400;
        private const TriangleGaussPoints.GaussPointNumber Tri = TriangleGaussPoints.GaussPointNumber.Tri79;

        private ReinforcedConcreteSection NewSection(double width = 300) =>
            GetRectangularSection4Rebars(width, 500, 20, 50, ConcreteMaterialEN1992Data.C30_37, SteelMaterialEN1992Data.B450C);

        /// <summary>A slab with two layers of bars on a welded H below it, as the sections of the composite bridges</summary>
        private static ReinforcedConcreteSection NewBridgeSection() => ReinforcedConcreteSection.CreateBridgeSection(2500, 250, ConcreteMaterialEN1992Data.C35_45,
            new RebarSectionCircular("", 16, SteelMaterialEN1992Data.B450C), 150, 50, new RebarSectionCircular("", 16, SteelMaterialEN1992Data.B450C), 150,
            new SectionH(2000, 16, 600, 30, 800, 40, "H"), SteelMaterialEN1993Data.S355, 50);

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        private static void AssertSamePoints(GaussIntegration.GlobalCoordinateGaussPoint[][] expected, GaussIntegration.GlobalCoordinateGaussPoint[][] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, "faces");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Length, actual[i].Length, "points of face " + i);
                for (int j = 0; j < expected[i].Length; j++)
                {
                    Assert.AreEqual(Bits(expected[i][j].GpX), Bits(actual[i][j].GpX), $"x [{i}][{j}]");
                    Assert.AreEqual(Bits(expected[i][j].GpY), Bits(actual[i][j].GpY), $"y [{i}][{j}]");
                    Assert.AreEqual(Bits(expected[i][j].GpZ), Bits(actual[i][j].GpZ), $"z [{i}][{j}]");
                    // the product weight × det(J) × factor of the point
                    Assert.AreEqual(Bits(expected[i][j].EvaluateFunction((x, y) => 1.0)), Bits(actual[i][j].EvaluateFunction((x, y) => 1.0)), $"weight [{i}][{j}]");
                }
            }
        }

        [TestMethod]
        public void EqualMeshesShareThePointsOfANewComputation()
        {
            MeshGaussPointCache.Clear();
            Mesh first = NewSection().Mesh, second = NewSection().Mesh;
            Assert.AreNotSame(first, second);
            var cached = MeshGaussPointCache.Get(first, Quad, Tri);
            Assert.AreSame(cached, MeshGaussPointCache.Get(second, Quad, Tri), "an equal mesh finds the points");
            Assert.AreEqual(1, MeshGaussPointCache.Misses);
            Assert.AreEqual(1, MeshGaussPointCache.Hits);
            AssertSamePoints(GaussIntegration.GetGlobalCoordinateGaussPointsLinearShapeFunction(second, Quad, Tri), cached);
        }

        [TestMethod]
        public void OtherMeshesAndNumbersOfPointsAreNotShared()
        {
            MeshGaussPointCache.Clear();
            var mesh = NewSection().Mesh;
            var points = MeshGaussPointCache.Get(mesh, Quad, Tri);
            var wider = MeshGaussPointCache.Get(NewSection(301).Mesh, Quad, Tri);
            var fewer = MeshGaussPointCache.Get(mesh, QuadrangleGaussPoints.GaussPointNumber.Quad25, Tri);
            Assert.AreNotSame(points, wider, "another width");
            Assert.AreNotSame(points, fewer, "other numbers of points");
            Assert.AreEqual(3, MeshGaussPointCache.Misses);
            Assert.AreEqual(0, MeshGaussPointCache.Hits);
            AssertSamePoints(GaussIntegration.GetGlobalCoordinateGaussPointsLinearShapeFunction(mesh, QuadrangleGaussPoints.GaussPointNumber.Quad25, Tri), fewer);
        }

        [TestMethod]
        public void CapacityBoundsTheCache()
        {
            int capacity = MeshGaussPointCache.Capacity;
            try
            {
                MeshGaussPointCache.Clear();
                MeshGaussPointCache.Capacity = 1;
                MeshGaussPointCache.Get(NewSection(300).Mesh, Quad, Tri);
                MeshGaussPointCache.Get(NewSection(301).Mesh, Quad, Tri);
                Assert.AreEqual(1, MeshGaussPointCache.Count);
                MeshGaussPointCache.Get(NewSection(300).Mesh, Quad, Tri);
                Assert.AreEqual(3, MeshGaussPointCache.Misses, "the least recently used mesh left the cache");
                MeshGaussPointCache.Capacity = 0;
                Assert.AreEqual(0, MeshGaussPointCache.Count);
                Assert.AreNotSame(MeshGaussPointCache.Get(NewSection().Mesh, Quad, Tri), MeshGaussPointCache.Get(NewSection().Mesh, Quad, Tri), "capacity 0: no cache");
            }
            finally
            {
                MeshGaussPointCache.Capacity = capacity;
                MeshGaussPointCache.Clear();
            }
        }

        [TestMethod]
        public void StressesOfTheSolversDoNotChange()
        {
            // the linear stress analysis of CompositeBridge: tensile concrete, ψ·φ of the phase, force at the application point
            StressAnalysisResult Solve(ReinforcedConcreteSection section)
            {
                var axes = new CoordinateSystem(new Point2d(1250, -600), new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
                var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                    SectionSolver.FailureDomainTypes.Elastic, SectionSolver.StressAnalysisTypes.Linear, 1.1, 0, true, 16);
                var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options, new StandardEN1992p11(), true, -1, new StandardEN1993p11());
                return checker.SectionSolver.GetLinearStressAnalysisResult(new ResultBeamForces(-2e6, 0, 0, 0, 8e9, 0, axes), 1.1, 0);
            }
            int capacity = MeshGaussPointCache.Capacity;
            try
            {
                MeshGaussPointCache.Capacity = 0;
                var computed = Solve(NewBridgeSection());
                MeshGaussPointCache.Capacity = capacity;
                MeshGaussPointCache.Clear();
                var first = Solve(NewBridgeSection());
                var shared = Solve(NewBridgeSection());
                Assert.AreEqual(1, MeshGaussPointCache.Hits, "the second solver finds the points of the slab");
                foreach (var result in new[] { first, shared })
                {
                    Assert.AreEqual(Bits(computed.StrainPlane.ChiX), Bits(result.StrainPlane.ChiX));
                    Assert.AreEqual(Bits(computed.StrainPlane.ChiY), Bits(result.StrainPlane.ChiY));
                    Assert.AreEqual(Bits(computed.StrainPlane.StrainReferencePoint), Bits(result.StrainPlane.StrainReferencePoint));
                    var expected = computed.GetRebarsTension(1.1).Select(r => Bits(r.tension)).ToArray();
                    CollectionAssert.AreEqual(expected, result.GetRebarsTension(1.1).Select(r => Bits(r.tension)).ToArray(), "bar stresses");
                }
            }
            finally
            {
                MeshGaussPointCache.Capacity = capacity;
                MeshGaussPointCache.Clear();
            }
        }
    }
}
