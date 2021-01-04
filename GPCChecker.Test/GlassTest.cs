using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Checker.Glasses;
using GPC.Model.Elements.Glasses;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Loads;
using GPC.Checker.Glasses.LoadCases;
using GPC.Model.Combinations;
using GPC.Checker.Glasses.Checkers;
using GPC.Model.Elements;
using System.Collections.Generic;

namespace GlassTests
{
    [TestClass]
    public class GlassTest
    {
        public TestContext TestContext { get; set; }

        private static string _outputFolder;

        #region Test public methods
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Nothing
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _outputFolder = Path.Combine(Directory.GetParent(TestContext.TestDir).ToString(), "GlassTest");
            Directory.CreateDirectory(_outputFolder);
        }

        [TestCleanup]
        public void CleanUp()
        {
            if (Directory.Exists(TestContext.TestDir))
                Directory.Delete(TestContext.TestDir, true);
        }
        #endregion

        #region Private methods

        private GlassMaterialPrEn GetGlassMaterialPrEn()
        {
            return new GlassMaterialPrEn("Glass", 70000, 0.23, 25, GlassMaterialPrEn.GlassType.DrawnSheetGlass, GlassMaterialPrEn.SurfaceTreatment.AsProduced,
                                        GlassMaterialPrEn.PrestressType.HeatStrengthened, GlassMaterialPrEn.ManufactoringProcess.HorizontalToughening, 2700 * 10E-12, 0);
        }

        private InterlayerMaterial GetInterlayerMaterial()
        {
            var it = new InterlayerMaterial(1, 0, InterlayerMaterial.InterlayerType.NormalPVB);
            it.AddShearModule(3, new double[] { 10, 20, 50 }, new double[] { 0.1, 0.2, 0.30 });
            it.AddShearModule(100, new double[] { 10, 20, 50 }, new double[] { 0.15, 0.25, 0.35 });
            return it;
        }

        private Shape GetRectangularShape(Point3d p, Vector3d vector)
        {
            Polygon3d poly = new Polygon3d()
            {
                new Point3d(p.X, p.Y, p.Z),
                new Point3d(p.X + vector.X, p.Y + vector.Y, p.Z), 
                new Point3d(p.X + vector.X, p.Y + vector.Y, p.Z + vector.Z),
                new Point3d(p.X, p.Y, p.Z + vector.Z)
            };

            return new Shape(poly, null, null);
        }

        #endregion


        [TestMethod]
        public void MonolithicGlass1()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 400, 100));
            var restrains = s1.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s1.GetCoordinateSystem()))).ToList();

            // Surface
            MonolithicGlass mg = new MonolithicGlass("Mg1", 10, GetGlassMaterialPrEn());
            GlassSurface gs1 = new GlassSurface(mg, s1, null, restrains, null, 0, Guid.NewGuid());

            // LoadCases
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseType.Wind, Guid.NewGuid());

            // Loads
            GlobalAreaLoad s1GalLc1 = new GlobalAreaLoad(100, 200, 300, s1, lc1, Guid.NewGuid());
            GlobalAreaLoad s1GalLc2 = new GlobalAreaLoad(101, 201, 301, s1, lc2, Guid.NewGuid());
            GlobalPointLoad s1gpl = new GlobalPointLoad(1, 2, 3, 4, 5, 6, new Point3d(100, 200, 50), lc2, Guid.NewGuid());

            gs1.AddLoad(s1GalLc1);
            gs1.AddLoad(s1GalLc2);
            gs1.AddLoad(s1gpl);

            // Combination 
            Combination cmb1 = new CombinationEn("CMB1", CombinationEn.CombinationType.UltimateStructural, Guid.NewGuid());
            cmb1[lc1] = 2;
            cmb1[lc2] = 3;

            Combination cmb2 = new CombinationEn("CMB2", CombinationEn.CombinationType.ServiceabilityCharacteristic, Guid.NewGuid());
            cmb2[lc1] = 4;
            cmb2[lc1] = 3;
            cmb2[lc2] = 5;

            string outputFolder = Path.Combine(_outputFolder, TestContext.TestName);
            Directory.CreateDirectory(outputFolder);

            Model model = new Model(outputFolder);
            model.AddSurface(gs1);

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);


            PrEnGlassChecker check = new PrEnGlassChecker(model, new GlassChecker.CheckParameters()) ;
            check.SetUpFemModels();
            check.ExportToSt7();
        }
        

        [TestMethod]
        public void MonolithicGlass2()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 400, 100));
            Shape s2 = GetRectangularShape(new Point3d(100, 100, 100), new Vector3d(300, 500, 200));

            var restrains1 = s1.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s1.GetCoordinateSystem()))).ToList();
            var restrains2 = s2.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s2.GetCoordinateSystem()))).ToList();

            MonolithicGlass mg1 = new MonolithicGlass("mg1", 10, GetGlassMaterialPrEn());
            MonolithicGlass mg2 = new MonolithicGlass("mg2", 20, GetGlassMaterialPrEn());

            GlassSurface gs1 = new GlassSurface(mg1, s1, null, restrains1, null, 0, Guid.NewGuid());
            GlassSurface gs2 = new GlassSurface(mg2, s2, null, null, null, 1, Guid.NewGuid());

            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseType.Wind, Guid.NewGuid());

            GlobalAreaLoad s1GalLc1 = new GlobalAreaLoad(100, 200, 300, s1, lc1, Guid.NewGuid());
            GlobalAreaLoad s1GalLc2 = new GlobalAreaLoad(101, 201, 301, s1, lc2, Guid.NewGuid());

            GlobalAreaLoad s2GalLc1 = new GlobalAreaLoad(150, 250, 350, s2, lc1, Guid.NewGuid());
            GlobalAreaLoad s2GalLc2 = new GlobalAreaLoad(151, 251, 351, s2, lc2, Guid.NewGuid());

            gs1.AddLoad(s1GalLc1);
            gs1.AddLoad(s1GalLc2);

            gs2.AddLoad(s2GalLc1);
            gs2.AddLoad(s2GalLc2);

            Combination cmb1 = new CombinationEn("CMB1", CombinationEn.CombinationType.UltimateStructural, Guid.NewGuid());
            cmb1[lc1] = 2;
            cmb1[lc2] = 3;

            Combination cmb2 = new CombinationEn("CMB2", CombinationEn.CombinationType.ServiceabilityCharacteristic, Guid.NewGuid());
            cmb2[lc1] = 4;
            cmb2[lc1] = 3;
            cmb2[lc2] = 5;

            string outputFolder = Path.Combine(_outputFolder, TestContext.TestName);
            Directory.CreateDirectory(outputFolder);

            Model model = new Model(outputFolder);
            model.AddSurface(gs1);
            model.AddSurface(gs2);

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            PrEnGlassChecker check = new PrEnGlassChecker(model, new GlassChecker.CheckParameters());
            check.SetUpFemModels();
            check.ExportToSt7();
        }


        [TestMethod]
        public void MonolithicGlass3()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 400, 100));
            Shape s2 = GetRectangularShape(new Point3d(000, 100, 400), new Vector3d(300, 500, 200));
            Shape s3 = GetRectangularShape(new Point3d(000, 300, 800), new Vector3d(300, 500, 200));

            var restrains1 = s1.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s1.GetCoordinateSystem()))).ToList();
            var restrains2 = s2.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s2.GetCoordinateSystem()))).ToList();
            var restrains3 = s3.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s3.GetCoordinateSystem()))).ToList();
            restrains3.RemoveAt(0);

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 10, GetGlassMaterialPrEn());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialPrEn());
            MonolithicGlass mg3 = new MonolithicGlass("Mg3", 30, GetGlassMaterialPrEn());

            GlassSurface gs1 = new GlassSurface(mg1, s1, null, restrains1, null, 0, Guid.NewGuid());
            GlassSurface gs2 = new GlassSurface(mg2, s2, null, restrains2, null, 1, Guid.NewGuid());
            GlassSurface gs3 = new GlassSurface(mg3, s3, null, restrains3, null, 2, Guid.NewGuid());

            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseType.Wind, Guid.NewGuid());
            LoadCase lc3 = new LoadCase("LC3", 10000, 20, LoadCase.LoadCaseType.SelfWeight, Guid.NewGuid());

            GlobalAreaLoad s1GalLc1 = new GlobalAreaLoad(100, 0, 0, s1, lc1, Guid.NewGuid());
            GlobalAreaLoad s2GalLc2 = new GlobalAreaLoad(160, 0, 0, s2, lc2, Guid.NewGuid());
            GlobalAreaLoad s3GalLc3 = new GlobalAreaLoad(170, 0, 0, s3, lc3, Guid.NewGuid());

            GlobalPointLoad s1gpl = new GlobalPointLoad(1, 2, 3, 4, 5, 6, new Point3d(100, 200, 50), lc2, Guid.NewGuid());

            gs1.AddLoad(s1GalLc1);
            gs1.AddLoad(s1gpl);

            gs2.AddLoad(s2GalLc2);

            gs3.AddLoad(s3GalLc3);

            Combination cmb1 = new CombinationEn("CMB1", CombinationEn.CombinationType.UltimateStructural, Guid.NewGuid());
            cmb1[lc1] = 2;
            cmb1[lc2] = 3;

            Combination cmb2 = new CombinationEn("CMB2", CombinationEn.CombinationType.ServiceabilityCharacteristic, Guid.NewGuid());
            cmb2[lc1] = 4;
            cmb2[lc1] = 3;
            cmb2[lc2] = 5;

            string outputFolder = Path.Combine(_outputFolder, TestContext.TestName);
            Directory.CreateDirectory(outputFolder);

            Model model = new Model(outputFolder);
            model.AddSurface(gs1);
            model.AddSurface(gs2);
            model.AddSurface(gs3);

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            GlassChecker.CheckParameters checkParameters = new GlassChecker.CheckParameters();
            checkParameters.SetAnalysisType(GlassChecker.CheckParameters.AnalysisType.LinearStaticAnalisys);
            checkParameters.SetLaminatedAnalysisType(GlassChecker.CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer);

            PrEnGlassChecker check = new PrEnGlassChecker(model, new GlassChecker.CheckParameters()) ;
            check.SetUpFemModels();
            check.ExportToSt7();
        }

        [TestMethod]
        public void MonoAndLaminatedGlass()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 1000));
            Shape s2 = GetRectangularShape(new Point3d(300, 0, 0), new Vector3d(200, 0, 1500));

            var restrains1 = s1.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s1.GetCoordinateSystem()))).ToList();
            var restrains2 = s2.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s2.GetCoordinateSystem()))).ToList();

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 10, GetGlassMaterialPrEn());

            MonolithicGlass mg21 = new MonolithicGlass("Mg21", 5, GetGlassMaterialPrEn());
            MonolithicGlass mg22 = new MonolithicGlass("Mg22", 20, GetGlassMaterialPrEn());


            Interlayer intr = new Interlayer("Int", 0.76, GetInterlayerMaterial(), Guid.NewGuid());


            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[]{ mg21, mg22 }, new Interlayer[] { intr });

            LoadCase lc1 = new LoadCase("LC1", 50, 20, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseType.Wind, Guid.NewGuid());
            GlobalPointLoad s1gpl1 = new GlobalPointLoad(0, 2, 0, 0, 0, 0, new Point3d(80, 0, 500), lc1, Guid.NewGuid());
            GlobalPointLoad s1gpl2 = new GlobalPointLoad(0, 2, 0, 0, 0, 0, new Point3d(80, 0, 700), lc2, Guid.NewGuid());
            GlobalPointLoad s2gpl1 = new GlobalPointLoad(0, 10, 0, 0, 0, 0, new Point3d(380, 0, 600), lc1, Guid.NewGuid());
            GlobalPointLoad s2gpl2 = new GlobalPointLoad(0, 10, 0, 0, 0, 0, new Point3d(380, 0, 1000), lc2, Guid.NewGuid());

            GlassSurface gs1 = new GlassSurface(mg1, s1, new List<Load> { s1gpl1 }, restrains1, null, 0, Guid.NewGuid());
            gs1.AddLoad(s1gpl2);
            GlassSurface gs2 = new GlassSurface(lg1, s2, new List<Load> { s2gpl1 }, restrains2, null, 0, Guid.NewGuid());
            gs2.AddLoad(s2gpl2);


            string outputFolder = Path.Combine(_outputFolder, TestContext.TestName);
            Directory.CreateDirectory(outputFolder);

            Model model = new Model(outputFolder);
            model.AddSurface(gs1);
            model.AddSurface(gs2);

            var cp = new GlassChecker.CheckParameters();
            cp.SetLaminatedAnalysisType(GlassChecker.CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer);

            PrEnGlassChecker check = new PrEnGlassChecker(model, new GlassChecker.CheckParameters());
            check.SetUpFemModels();
            
            check.ExportToSt7();

            check.RunSt7Solver();

        }
    }
}
