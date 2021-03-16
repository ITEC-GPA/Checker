using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Collections.Generic;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Checker.Glasses.FemModel;
using GPC.Model.FEM.Properties;
using GPC.Model.FEM;
using GPC.Model.Materials;
using GPC.Model.Loads;
using GPC.Checker.Glasses.LoadCases;
using GPC.Model.FreedomCases;
using GPC.Model.Restrains;

namespace GlassTests
{
    [TestClass]
    public class FemModelWrapperTest
    {
        public TestContext TestContext { get; set; }

        private static string _outputFolder;
        private string _testName;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {

        }

        [TestInitialize]
        public void TestInitialize()
        {
            _outputFolder = Path.Combine(Directory.GetParent(TestContext.TestDir).ToString(), TestContext.FullyQualifiedTestClassName.Split(new char[] { '.' })[1]);
            Directory.CreateDirectory(_outputFolder);
            _testName = TestContext.TestName;
        }

        [TestCleanup]
        public void CleanUp()
        {
            if (Directory.Exists(TestContext.TestDir))
                Directory.Delete(TestContext.TestDir, true);
        }



        private Shape CreateSimpleShape(double width, double height)
        {
            Polygon3d p = new Polygon3d()
            {
                new Point3d(0,0,0),
                new Point3d(width, 0, 0),
                new Point3d(width, height, 0),
                new Point3d(0, height, 0)
            };

            return new Shape(p);
        }


        [TestMethod]
        public void Test1()
        {
            FemModelWrapper fmw = new FemModelWrapper();

            Shape s1 = CreateSimpleShape(100, 200);
            var borders = s1.Fill.Explode();


            GlassMaterial gm = new GlassMaterialAstm("", 1, 0.2, 3, 4, 5, 6, 0.008, 0.008, 9);
            MonolithicGlassProperty pp = new MonolithicGlassProperty(1,2, gm);

            Mesh.GenerateMeshOptions.MeshSize = 10;

            PointLoad p1 = new PointLoad(1, 2, 3, 4, 5, 6, new Point3d(35, 35, 0), new LoadCase("LC1", 2, 10, GPC.Model.LoadCases.LoadCase.LoadCaseType.LiveLoad));
            LineLoad l1 = new LineLoad(1, 2, 3, 4, 5, 6, new Line3d(new Point3d(35, 150, 0), new Point3d(75, 100, 0)), new LoadCase("LC2", 2, 10, GPC.Model.LoadCases.LoadCase.LoadCaseType.LiveLoad));

            PointRestrain pr = new PointRestrain(new Point3d(70, 70, 0), new FreedomCase("fc1"), new List<DofRestrain>{ new DofRestrain(LinearSolver.DOF.DX, true), new DofRestrain(LinearSolver.DOF.RZ, true) });

            LineRestrain lr = new LineRestrain(borders[0], new FreedomCase("fc1"), new List<DofRestrain> { new DofRestrain(LinearSolver.DOF.DY, true), new DofRestrain(LinearSolver.DOF.RZ, true) });
            fmw.AddShape(s1, pp, new List<Load>() { p1, l1 }, new List<GeometryRestrain>() { pr, lr });

            fmw.SaveToSt7(Path.Combine(_outputFolder, $"Export.st7"));
        }
    }
}
