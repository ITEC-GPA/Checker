using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Checker.Glasses;
using GPC.Model.Elements.Glasses;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Loads;
using GPC.Checker.Glasses.LoadCases;
using GPC.Model.Combinations;
using GPC.Checker.Glasses.Checkers;

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
            _outputFolder = Path.Combine(Directory.GetParent(TestContext.TestDir).ToString(), "OutputTests");
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
            return new GlassMaterialPrEn(70000, 0.23, 25, GlassMaterialPrEn.GlassType.DrawnSheetGlass, GlassMaterialPrEn.SurfaceTreatment.AsProduced,
                                        GlassMaterialPrEn.PrestressType.HeatStrengthened, GlassMaterialPrEn.ManufactoringProcess.HorizontalToughening, 2700 * 10E-12, 0);
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

            MonolithicGlass mg = new MonolithicGlass(10, GetGlassMaterialPrEn() );

            GlassSurface gs1 = new GlassSurface(mg, s1, 0, Guid.NewGuid());

            LoadCase lc1 = new LoadCase("LC1", 100, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            LoadCase lc2 = new LoadCase("LC2", 5, LoadCase.LoadCaseType.Wind, Guid.NewGuid());

            NormalAreaLoad s1allc1 = new NormalAreaLoad(100, s1, lc1, Guid.NewGuid());
            NormalAreaLoad s1allc2 = new NormalAreaLoad(300, s1, lc2, Guid.NewGuid());

            gs1.AddLoad(s1allc1);
            gs1.AddLoad(s1allc2);

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

            PrEnGlassChecker check = new PrEnGlassChecker(model, GlassChecker.LaminatedAnalysisType.EquivalentThickness);
            check.Run();
        }
        
        [TestMethod]
        public void MonolithicGlass2()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 400, 100));
            Shape s2 = GetRectangularShape(new Point3d(100, 100, 100), new Vector3d(300, 500, 200));

            MonolithicGlass mg = new MonolithicGlass(10, GetGlassMaterialPrEn());

            GlassSurface gs1 = new GlassSurface(mg, s1, 0, Guid.NewGuid());
            GlassSurface gs2 = new GlassSurface(mg, s2, 0, Guid.NewGuid());

            LoadCase lc1 = new LoadCase("LC1", 100, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            LoadCase lc2 = new LoadCase("LC2", 5, LoadCase.LoadCaseType.Wind, Guid.NewGuid());

            NormalAreaLoad s1allc1 = new NormalAreaLoad(100, s1, lc1, Guid.NewGuid());
            NormalAreaLoad s1allc2 = new NormalAreaLoad(300, s1, lc2, Guid.NewGuid());

            NormalAreaLoad s2allc1 = new NormalAreaLoad(150, s2, lc1, Guid.NewGuid());
            NormalAreaLoad s2allc2 = new NormalAreaLoad(350, s2, lc2, Guid.NewGuid());

            gs1.AddLoad(s1allc1);
            gs1.AddLoad(s1allc2);

            gs2.AddLoad(s2allc1);
            gs2.AddLoad(s2allc2);

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

            PrEnGlassChecker check = new PrEnGlassChecker(model, GlassChecker.LaminatedAnalysisType.EquivalentThickness);
            check.Run();
        }
    }
}
