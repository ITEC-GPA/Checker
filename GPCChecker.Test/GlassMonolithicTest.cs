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
    public class GlassMonolithicTest
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
        public void Mono2Glass()

            // Test di creazione di 2 vetri monolitici di grandezza 1500x2000 mm, spessore 8mm, sul piano XZ.
            // Carico uniformemente distribuito lungo y di 1.2KPa.
            // Il vetro 1 è incastrato lungo il bordo, il vetro 2 è appoggiato. il caso 2 è risolto a pagina 232 della CNR.
            // I risultati attesi per il vetro 2 sono: u = 12.88mm e sigma principale = 17.52 MPa per il calcolo lineare

        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1500, 0, 2000));
            Shape s2 = GetRectangularShape(new Point3d(2000, 0, 0), new Vector3d(1500, 0, 2000));

            var restrains1 = s1.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s1.GetCoordinateSystem()))).ToList();
            var restrains2 = s2.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetRotationReleased(s2.GetCoordinateSystem()))).ToList();

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 8, GetGlassMaterialPrEn());

            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 8, GetGlassMaterialPrEn());


            LoadCase lc1 = new LoadCase("LC1", 3, 20, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            GlobalAreaLoad NormalAreaLoad1 = new GlobalAreaLoad(0, 0.0012, 0, s1, lc1, Guid.NewGuid());
            GlobalAreaLoad NormalAreaLoad2 = new GlobalAreaLoad(0, 0.0012, 0, s2, lc1, Guid.NewGuid());


            GlassSurface gs1 = new GlassSurface(mg1, s1, null, restrains1, null, 0, Guid.NewGuid());
            gs1.AddLoad(NormalAreaLoad1);
            GlassSurface gs2 = new GlassSurface(mg2, s2, null, restrains2, null, 0, Guid.NewGuid());
            gs2.AddLoad(NormalAreaLoad2);


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
