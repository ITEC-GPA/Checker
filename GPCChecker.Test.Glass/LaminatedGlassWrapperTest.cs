using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.Models;
using GPC.Checkers.Glasses.Wrappers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Glasses;
using GPC.Model.Materials;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Collections.Generic;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Loads;
using GPC.Model.Restrains;
using GPC.Model.FreedomCases;
using System.Threading.Tasks;

namespace GlassTests
{
    [TestClass]
    public class LaminatedGlassWrapperTest : GlassTestBase
    {

        #region Geometry

        [TestMethod]
        public void LaminatedGetDistance()
        {
            // Arrange
            double _tolleranza = 0.0001;
            double[] distancesExpectedGlass = new double[4] { 0, 16, 41.6, 63.6 };
            double[] distancesExpectedInterlayer = new double[3] { 5.5, 26.3, 57.6 };

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 1000));

            MonolithicGlass mg1 = new MonolithicGlass("Mg2", 10, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialEn16612());
            MonolithicGlass mg3 = new MonolithicGlass("Mg3", 30, GetGlassMaterialEn16612());
            MonolithicGlass mg4 = new MonolithicGlass("Mg4", 10, GetGlassMaterialEn16612());

            Interlayer intr1 = new Interlayer("Int1", 1, GetInterlayerMaterial(), Guid.NewGuid());
            Interlayer intr2 = new Interlayer("Int2", 0.6, GetInterlayerMaterial(), Guid.NewGuid());
            Interlayer intr3 = new Interlayer("Int3", 2, GetInterlayerMaterial(), Guid.NewGuid());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2, mg3, mg4 }, new Interlayer[] { intr1, intr2, intr3 });

            Prototype p = new Prototype("", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                              Prototype.CheckMethods.ASTME1300, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                              new Prototype.LaminatedEqThicknessParameters(), null);

            GlassSurface gs = new GlassSurface(p, s1);

            LaminatedGlassWrapper lgw = new LaminatedGlassWrapper(gs, lg1);

            // Act
            double[] distancesGlass = lgw.GetMonolithicBarycenterDistances();
            double[] distancesInterlayer = lgw.GetInterlayerBarycenterDistances();

            // Assert
            for (int i = 0; i < distancesExpectedGlass.Length; i++)
            {
                Assert.IsTrue(Math.Abs(distancesExpectedGlass[i] - distancesGlass[i]) < _tolleranza, $"Glass => Indice: {i}, Calcolata: {distancesGlass[i]}, attesa: {distancesExpectedGlass[i]} ");
            }

            for (int i = 0; i < distancesExpectedInterlayer.Length; i++)
            {
                Assert.IsTrue(Math.Abs(distancesExpectedInterlayer[i] - distancesInterlayer[i]) < _tolleranza, $"Interlayer => Indice: {i}, Calcolata: {distancesInterlayer[i]}, attesa: {distancesExpectedInterlayer[i]} ");
            }
        }

        [TestMethod]
        public void LaminatedUpperLowerVerticesIds()
        {
            // Arrange

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(500, 500, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg2", 10, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialEn16612());

            Interlayer intr1 = new Interlayer("Int1", 1, GetInterlayerMaterial());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, new Interlayer[] { intr1 });

            Prototype p = new Prototype("", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                              Prototype.CheckMethods.ASTME1300, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                              new Prototype.LaminatedEqThicknessParameters(), null);

            p.MeshOptions.MeshSize = 250;

            GlassSurface gs = new GlassSurface(p, s1);

            LaminatedGlassWrapper lgw = new LaminatedGlassWrapper(gs, lg1);

            lgw.GenerateMesh();

            List<int> glassIds = lgw.GetLayerUpperLowerVerticesIds(0).lowerVertices.ToList();
            List<int> interlayerIds = lgw.GetLayerUpperLowerVerticesIds(1).lowerVertices.ToList();

            for (int i = 0; i < glassIds.Count(); i++)
            {
                var vertex1 = lgw.Meshes[0].Vertices.GetElementById(glassIds[i]);
                var vertex2 = lgw.Meshes[1].Vertices.GetElementById(interlayerIds[i]);

                var v1 = vertex1.Point.VectorTo(vertex2.Point);

                Assert.IsTrue(v1 == new Vector3d(0, 0, 5), $"{v1} {vertex1.Point}  {vertex2.Point}");
            }
        }

        #endregion

        #region Equivalent thickness


        [TestMethod]
        [TestCategory("Layers: 2")]
        public void EquivalentThickness1()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 2000;
            double minorSide = 1000;
            double loadWidth = 300;

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                          Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                          new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET,
                                          Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                          majorSide,
                                          minorSide),
                                          null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind1", 3, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcPressure2 = new LoadCase("Wind2", 3, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcPointLoad = new LoadCase("Barrier", 500, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);


            Shape loadShape = GetRectangularShape(new Point3d(minorSide / 2.0 - loadWidth / 2.0, majorSide / 2.0 - loadWidth / 2.0, 0), new Vector3d(loadWidth, loadWidth, 0));
            NormalAreaLoad loadWp = new NormalAreaLoad(-1 / 1000, s1, lcPressure);
            NormalAreaLoad punctualLoadWp1 = new NormalAreaLoad(-1 / 1000, loadShape, lcPressure2);
            NormalAreaLoad punctualLoadWp2 = new NormalAreaLoad(-1 / 1000, loadShape, lcPointLoad);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddRestrains(s1.Fill.Explode().Select(i =>
                                    (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);
            lgw.AddExternalFaceLoad(punctualLoadWp1);
            lgw.AddExternalFaceLoad(punctualLoadWp2);

            lgw.CalculateEquivalentThicknesses();

            double tw1 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == loadWp.GlassLoadCase.Name).Value;
            double ts11 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == loadWp.GlassLoadCase.Name).Value;
            double ts21 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == loadWp.GlassLoadCase.Name).Value;

            double tw2 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase.Name).Value;
            double ts12 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase.Name).Value;
            double ts22 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase.Name).Value;

            double tw3 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == punctualLoadWp2.GlassLoadCase.Name).Value;
            double ts13 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == punctualLoadWp2.GlassLoadCase.Name).Value;
            double ts23 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == punctualLoadWp2.GlassLoadCase.Name).Value;

            double shearModule1 = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);
            double shearModule2 = interlayers[0].Material.GetShearModule(lcPointLoad.LoadDuration, lcPointLoad.Temperature);


            Console.WriteLine($"G interlayer lcPressure: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"G interlayer lcPressure2: {interlayers[0].Material.GetShearModule(lcPressure2.LoadDuration, lcPressure2.Temperature)}");
            Console.WriteLine($"G interlayer lcPointLoad: {interlayers[0].Material.GetShearModule(lcPointLoad.LoadDuration, lcPointLoad.Temperature)}");

            Console.WriteLine($"Tw1: {tw1}");
            Console.WriteLine($"Ts11: {ts11}");
            Console.WriteLine($"Ts21: {ts21}");

            Console.WriteLine($"Tw2: {tw2}");
            Console.WriteLine($"Ts12: {ts12}");
            Console.WriteLine($"Ts22: {ts22}");

            Console.WriteLine($"Tw3: {tw2}");
            Console.WriteLine($"Ts13: {ts12}");
            Console.WriteLine($"Ts23: {ts22}");


            Assert.AreEqual(0.23333, shearModule1, 0.001, shearModule1.ToString()); // valore di G su cui sono tarati gli expected value sotto
            Assert.AreEqual(0.34066, shearModule2, 0.001, shearModule2.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(7.15, tw1, 0.1, tw1.ToString());
            Assert.AreEqual(8.04, ts11, 0.1, ts11.ToString());
            Assert.AreEqual(8.04, ts21, 0.1, ts21.ToString());

            Assert.AreEqual(6.76, tw2, 0.1, tw2.ToString());
            Assert.AreEqual(7.60, ts12, 0.1, ts12.ToString());
            Assert.AreEqual(7.60, ts22, 0.1, ts22.ToString());

            Assert.AreEqual(7.02, tw3, 0.1, tw2.ToString());
            Assert.AreEqual(7.89, ts13, 0.1, ts12.ToString());
            Assert.AreEqual(7.89, ts23, 0.1, ts22.ToString());
        }

        [TestMethod]
        [TestCategory("Layers: 2")]
        public void EquivalentThickness2()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 2000;
            double minorSide = 1000;
            //double loadWidth = 300;

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET,
                                        Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind1", 3, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcPressure2 = new LoadCase("Wind2", 3, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcPointLoad = new LoadCase("Barrier", 500, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);


            //Shape loadShape = GetRectangularShape(new Point3d(minorSide / 2.0 - loadWidth / 2.0, majorSide / 2.0 - loadWidth / 2.0, 0), new Vector3d(loadWidth, loadWidth, 0));

            Shape loadShapeNotParallel = new Shape(new Polygon3d() { new Point3d(500, 900, 0), new Point3d(600, 1000, 0), new Point3d(500, 1100, 0), new Point3d(400, 1000, 0) });

            Shape loadShapeParallel = s1.Scale(50.0 / 1000.0);

            var shap = loadShapeParallel == s1;

            NormalAreaLoad loadWp = new NormalAreaLoad(-1 / 1000, s1, lcPressure, "WholeSurface");
            NormalAreaLoad punctualLoadWp1 = new NormalAreaLoad(-1 / 1000, loadShapeParallel, lcPressure2, "ConcentratedParallel");
            NormalAreaLoad punctualLoadWp2 = new NormalAreaLoad(-1 / 1000, loadShapeNotParallel, lcPointLoad, "ConcentratedNotParallel");

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);
            lgw.AddExternalFaceLoad(punctualLoadWp1);
            lgw.AddExternalFaceLoad(punctualLoadWp2);

            lgw.CalculateEquivalentThicknesses();


            double tw1 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == loadWp.GlassLoadCase.Name).Value;
            double ts11 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == loadWp.GlassLoadCase.Name).Value;
            double ts21 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == loadWp.GlassLoadCase.Name).Value;

            double tw2 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase.Name).Value;
            double ts12 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase.Name).Value;
            double ts22 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase.Name).Value;


            double shearModule1 = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);
            double shearModule2 = interlayers[0].Material.GetShearModule(lcPointLoad.LoadDuration, lcPointLoad.Temperature);


            Console.WriteLine($"G interlayer lcPressure: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"G interlayer lcPointLoad: {interlayers[0].Material.GetShearModule(lcPointLoad.LoadDuration, lcPointLoad.Temperature)}");


            Console.WriteLine($"Tw1: {tw1}");
            Console.WriteLine($"Ts11: {ts11}");
            Console.WriteLine($"Ts21: {ts21}");

            Console.WriteLine($"Tw2: {tw2}");
            Console.WriteLine($"Ts12: {ts12}");
            Console.WriteLine($"Ts22: {ts22}");


            Assert.AreEqual(0.23333, shearModule1, 0.001, shearModule1.ToString()); // valore di G su cui sono tarati gli expected value sotto
            Assert.AreEqual(0.34066, shearModule2, 0.001, shearModule2.ToString()); // valore di G su cui sono tarati gli expected value sotto


            Assert.AreEqual(7.15, tw1, 0.1, tw1.ToString());
            Assert.AreEqual(8.04, ts11, 0.1, ts11.ToString());
            Assert.AreEqual(8.04, ts21, 0.1, ts21.ToString());

            Assert.AreEqual(6.76, tw2, 0.1, tw2.ToString());
            Assert.AreEqual(7.60, ts12, 0.1, ts12.ToString());
            Assert.AreEqual(7.60, ts22, 0.1, ts22.ToString());
        }

        [TestMethod]
        [TestCategory("Layers: 2")]
        [TestCategory("Load overload")]
        public void EquivalentThickness3()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 2000;
            double minorSide = 1000;
            double loadHeight1 = 800;
            double loadHeight2 = 1200;
            double loadHeight3 = 1500;

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET,
                                        Prototype.LaminatedEqThicknessBoundaryConditions.Other,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;


            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());
            model.AddSurface(gs1);

            LaminatedGlassWrapper lgw = new LaminatedGlassWrapper(gs1, lg);


            // Load
            LoadCase lcPressure = new LoadCase("Wind1", 3, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape1 = new Shape(new Polygon3d() { new Point3d(0, loadHeight1, 0),
                                                           new Point3d(minorSide, loadHeight1, 0),
                                                           new Point3d(minorSide, loadHeight2, 0),
                                                           new Point3d(0, loadHeight2, 0) });

            Shape loadShape2 = new Shape(new Polygon3d() { new Point3d(0, loadHeight1, 0),
                                                           new Point3d(minorSide, loadHeight1, 0),
                                                           new Point3d(minorSide, loadHeight3, 0),
                                                           new Point3d(0, loadHeight3, 0) });


            NormalAreaLoad punctualLoadWp1 = new NormalAreaLoad(-1, loadShape1, lcPressure, "l1");
            lgw.AddExternalFaceLoad(punctualLoadWp1);

            NormalAreaLoad punctualLoadWp2 = new NormalAreaLoad(-10 / 1000.0, loadShape2, lcPressure, "Load2");
            lgw.AddExternalFaceLoad(punctualLoadWp2);

            lgw.CalculateEquivalentThicknesses();


            Assert.IsTrue(lgw.ThicknessesW.Count() == 1);


            double tw1 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase.Name).Value;
            double ts11 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase.Name).Value;
            double ts21 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase.Name).Value;

            double shearModule1 = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer lcPressure: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");


            Console.WriteLine($"Tw1: {tw1}");
            Console.WriteLine($"Ts11: {ts11}");
            Console.WriteLine($"Ts21: {ts21}");

            Assert.AreEqual(0.23333, shearModule1, 0.001, shearModule1.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(7.057, tw1, 0.01, tw1.ToString());
            Assert.AreEqual(7.932, ts11, 0.01, ts11.ToString());
            Assert.AreEqual(7.932, ts21, 0.01, ts21.ToString());
        }


        [TestMethod]
        public void EquivalentThicknessEETAmnCoefficient()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1000;
            double minorSide = 2000;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 6, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 6, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET,
                                        Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        Math.Max(majorSide, minorSide), Math.Min(majorSide, minorSide)), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            GlassSurface gs1 = new GlassSurface(p1, s1);
            model.AddSurface(gs1);

            LaminatedGlassWrapperMock lgw = new LaminatedGlassWrapperMock(gs1, lg);

            var amn11 = lgw.GetEETFourSideAmnCoefficientMock(175, 1000, 350, 2000, 2000, 1000, 1, 1);
            var amn1010 = lgw.GetEETFourSideAmnCoefficientMock(175, 1000, 350, 2000, 2000, 1000, 10, 10);

            double expected11 = 1.14743e-21;
            double expected1010 = 2.281e-25;
            Assert.AreEqual(expected11, amn11, expected11 / 1000);
            Assert.AreEqual(expected1010, amn1010, expected1010 / 100);

        }



        [TestMethod]
        public void EquivalentThicknessEETPsiCoefficient1()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 2000;
            double minorSide = 1000;
            double loadHeight = 350/2.0;
            double loadWidth = 350;

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 6, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 6, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET,
                                        Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        Math.Max(majorSide, minorSide), Math.Min(majorSide, minorSide)), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            GlassSurface gs1 = new GlassSurface(p1, s1);
            model.AddSurface(gs1);

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape = GetRectangularShape(new Point3d(0, loadHeight - loadWidth / 2.0, 0), new Vector3d(minorSide, loadWidth, 0));

            NormalAreaLoad load1 = new NormalAreaLoad(-1, loadShape, lcPressure);

            LaminatedGlassWrapperMock lgw = new LaminatedGlassWrapperMock(gs1, lg);

            var parameters = new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET,
                                                                          Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported, 
                                                                          Math.Max(majorSide, minorSide),
                                                                          Math.Min(majorSide, minorSide));

            var task = lgw.GetEETFourSidePsiConcentratedLoadMockAsync(load1, parameters);

            Task.WaitAll(new[] { task });

            double expected = 0.000016454;
            Assert.AreEqual(expected, task.Result.Item2, expected / 1000);

        }


        [TestMethod]
        public void EquivalentThicknessEETPsiCoefficient2()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 2000;
            double minorSide = 1000;
            double loadHeight = 350 / 2.0;
            double loadWidth = 350;

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(majorSide, minorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 6, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 6, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET,
                                        Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        Math.Max(majorSide, minorSide), Math.Min(majorSide, minorSide)), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            GlassSurface gs1 = new GlassSurface(p1, s1);
            model.AddSurface(gs1);

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape = GetRectangularShape(new Point3d(0, loadHeight - loadWidth / 2.0, 0), new Vector3d(majorSide, loadWidth, 0));

            NormalAreaLoad load1 = new NormalAreaLoad(-1, loadShape, lcPressure);

            LaminatedGlassWrapperMock lgw = new LaminatedGlassWrapperMock(gs1, lg);

            var parameters = new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET,
                                                                          Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                                                          Math.Max(majorSide, minorSide),
                                                                          Math.Min(majorSide, minorSide));

            var task = lgw.GetEETFourSidePsiConcentratedLoadMockAsync(load1, parameters);

            Task.WaitAll(new[] { task });

            double expected = 0.000014387;
            Assert.AreEqual(expected, task.Result.Item2, expected / 1000);

        }
       
        #endregion



        public class LaminatedGlassWrapperMock : GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper
        {

            internal LaminatedGlassWrapperMock(GlassSurface glassSurface, LaminatedGlass glass) 
                : base(glassSurface, glass)
            {

            }

            internal LaminatedGlassWrapperMock(GlassSurface glassSurface, LaminatedGlass glass, GlassPanelPositions position) 
                : base(glassSurface, glass, position)
            {

            }

            internal double GetEETFourSideAmnCoefficientMock(double csi, double eta, double u, double v, double a, double b, int m, int n)
            {
                return base.GetEETFourSideAmnCoefficient(csi, eta, u, v, a, b, m, n);
            }


            internal Task<(IGlassLoad, double)> GetEETFourSidePsiConcentratedLoadMockAsync(IGlassLoad load,
                                                   GPC.Checkers.Glasses.Models.Prototype.LaminatedEqThicknessParameters eqThicknessParameters)
            {
                return base.GetEETFourSidePsiConcentratedLoadAsync(load, eqThicknessParameters);
            }
        }
    }
}