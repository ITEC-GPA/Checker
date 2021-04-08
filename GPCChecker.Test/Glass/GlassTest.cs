using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Checker.Glasses;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Loads;
using GPC.Checker.Glasses.LoadCases;
using GPC.Model.FreedomCases;
using GPC.Model.Combinations;
using GPC.Checker.Glasses.Checkers;
using GPC.Model.FEM;
using GPC.Model.Elements;
using GPC.Model.Restrains;
using GPC.Checker.Glasses.Restrain;
using GPC.Checker.Glasses.Models;
using GPC.Model.Glasses;
using GPC.Model.Results;
using GPC.Checker.Glasses.Glasses;
using GPC.Checker.Glasses.Results;
using GPC.Model.FEM.Properties;
using GPC.Geometry.Meshes;
using GPC.TestUtilities;

namespace GlassTests
{
    [TestClass]
    public class GlassTest : GlassTestBase
    {

        #region Private methods

        private GlassMaterialEn16612 GetGlassMaterialEn16612()
        {
            return new GlassMaterialEn16612("Glass", 70000, 0.23, 25, GlassMaterialEn16612.GlassTypes.FloatGlass, GlassMaterialEn16612.SurfaceTreatments.AsProduced,
                                        GlassMaterialEn16612.PrestressTypes.HeatStrengthened, GlassMaterialEn16612.ManufactoringProcesses.HorizontalToughening, 2700 * 10E-12, 0);
        }

        private GlassMaterialAstm GetGlassMaterialAstm()
        {
            return new GlassMaterialAstm("Glass", 70000, 0.23, 1, 16, 23.3, 18.3, 0.001, 2500, 0.1);
        }

        private InterlayerMaterial GetInterlayerMaterial()
        {
            var it = new InterlayerMaterial("", 1, 0, InterlayerMaterial.InterlayerType.NormalPVB);
            it.AddShearModule(3, new double[] { 10, 20, 50 }, new double[] { 0.1, 0.2, 0.30 });
            it.AddShearModule(100, new double[] { 10, 20, 50 }, new double[] { 0.15, 0.25, 0.35 });
            return it;
        }

        #endregion

        [TestMethod]
        public void MonolithicGlass0()
        {  
            Model model = new Model(base.GetOutputFolder());

            // Shape
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(800, 1600, 0));


            List<IParametricRestrain> parametricRestrains = new List<IParametricRestrain>();
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(LinearSolver.DOF.DX, true) })));


            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));


            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalisys, Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7 );

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
            gs1.AddRestrains(geometryRestrains1);

            model.AddSurface(gs1);

            // LoadCases
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseType.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseType.Wind);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.001, s1, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);


            // Combination 
            Combination cmb1 = new CombinationEn("CMB1", CombinationEn.CombinationType.UltimateStructural, Guid.NewGuid());
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new CombinationEn("CMB2", CombinationEn.CombinationType.ServiceabilityCharacteristic, Guid.NewGuid());
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);


            model.FemModelSetup();
            model.PerformChecks();

            var stressResults = model.GetPlateCombinationsResult();
            var deflectionResults = model.GetNodeDisplacementCombinationsResult();


            Assert.AreEqual(1, stressResults.Count, 0);
            Assert.AreEqual(1, deflectionResults.Count, 0);
            Assert.IsTrue(stressResults[0].Count > 0);
            Assert.IsTrue(deflectionResults[0].Count > 0);


            ResultPlateStress worstPlateResult = null;
            foreach (var comboResult in stressResults.First())
            {
                comboResult.GetPrincipalStress(out double s11, out double s22);

                if (worstPlateResult is null)
                    worstPlateResult = comboResult;
                else if (s11 > worstPlateResult.S11)
                    worstPlateResult = comboResult;
            }

            ResultNodeDisplacement worstNodeDisplacement = null;
            foreach (var comboResult in deflectionResults.First())
            {
                double disp = comboResult.GetResultingDisplacement();

                if (worstNodeDisplacement is null)
                    worstNodeDisplacement = comboResult;
                else if (Math.Abs(disp) > Math.Abs(worstNodeDisplacement.GetResultingDisplacement()))
                    worstNodeDisplacement = comboResult;
            }


            Console.WriteLine($"STRESS");
            Console.WriteLine($"\t Stress11: {worstPlateResult.S11}, stress22: {worstPlateResult.S22}, stress33: {worstPlateResult.S33}");
            Console.WriteLine($"\t Id: {worstPlateResult.Element.Id} Point: {(worstPlateResult.ResultPoint as ResultStressPoint).Location} Node0 Id: {worstPlateResult.Element.Nodes[0].Position}");

            Console.WriteLine($"DEFLECTION");
            Console.WriteLine($"\t WorstDeflection: {worstNodeDisplacement.GetResultingDisplacement()}");
            Console.WriteLine($"\t Id: {worstNodeDisplacement.Element.Id} Point: {worstNodeDisplacement.Element.Position} D1: {worstNodeDisplacement.D1} D2: {worstNodeDisplacement.D2} D3: {worstNodeDisplacement.D3} ");

            Assert.AreEqual(28.71, worstPlateResult.S11, 1);
            Assert.AreEqual(5.78, worstNodeDisplacement.D3, 0.2);
        }


        [TestMethod]
        public void MonolithicGlass1()
        {

            Model model = new Model(base.GetOutputFolder());

            // Shape
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(800, 1600, 0));
            Shape s2 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 1600, 0));


            List<IParametricRestrain> parametricRestrains = new List<IParametricRestrain>();
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(LinearSolver.DOF.DX, true) })));


            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));
            List<GeometryRestrain> geometryRestrains2 = new List<GeometryRestrain>();
            geometryRestrains2.AddRange(s2.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));


            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null ,null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalisys, Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7);

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
            GlassSurface gs2 = new GlassSurface(p1, s2, meshOptions);
            gs1.AddRestrains(geometryRestrains1);
            gs2.AddRestrains(geometryRestrains2);

            model.AddSurface(gs1);
            model.AddSurface(gs2);

            // LoadCases
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseType.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseType.Wind);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.001, s1, lc1);
            AreaLoad s2GalLc1 = new AreaLoad(0, 0, 0.001, s2, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);
            LineLoad s2ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(1000, 600, 0)), lc2);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);
            gs2.AddLoad(s2ll);
            gs2.AddLoad(s2GalLc1);

            // Combination 
            Combination cmb1 = new CombinationEn("CMB1", CombinationEn.CombinationType.UltimateStructural, Guid.NewGuid());
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new CombinationEn("CMB2", CombinationEn.CombinationType.ServiceabilityCharacteristic, Guid.NewGuid());
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            model.FemModelSetup();
            model.PerformChecks();

            var stressResults = model.GetPlateCombinationsResult();
            var deflectionResults = model.GetNodeDisplacementCombinationsResult();


            Assert.AreEqual(2, stressResults.Count, 0);
            Assert.AreEqual(2, deflectionResults.Count, 0);
            Assert.IsTrue(stressResults[0].Count > 0);
            Assert.IsTrue(deflectionResults[0].Count > 0);

            ResultPlateStress worstPlateResult1 = null;
            ResultPlateStress worstPlateResult2 = null;
            foreach (var comboResult in stressResults.First())
            {
                comboResult.GetPrincipalStress(out double s11, out double s22);

                if (worstPlateResult1 is null)
                    worstPlateResult1 = comboResult;
                else if (s11 > worstPlateResult1.S11)
                    worstPlateResult1 = comboResult;
            }
            foreach (var comboResult in stressResults.First())
            {
                comboResult.GetPrincipalStress(out double s11, out double s22);

                if (worstPlateResult2 is null)
                    worstPlateResult2 = comboResult;
                else if (s11 > worstPlateResult2.S11)
                    worstPlateResult2 = comboResult;
            }

            ResultNodeDisplacement worstNodeDisplacement1 = null;
            ResultNodeDisplacement worstNodeDisplacement2 = null;
            foreach (var comboResult in deflectionResults.First())
            {
                double disp = comboResult.GetResultingDisplacement();

                if (worstNodeDisplacement1 is null)
                    worstNodeDisplacement1 = comboResult;
                else if (Math.Abs(disp) > Math.Abs(worstNodeDisplacement1.GetResultingDisplacement()))
                    worstNodeDisplacement1 = comboResult;
            }
            foreach (var comboResult in deflectionResults.First())
            {
                double disp = comboResult.GetResultingDisplacement();

                if (worstNodeDisplacement2 is null)
                    worstNodeDisplacement2 = comboResult;
                else if (Math.Abs(disp) > Math.Abs(worstNodeDisplacement2.GetResultingDisplacement()))
                    worstNodeDisplacement2 = comboResult;
            }


            Console.WriteLine($"STRESS");
            Console.WriteLine($"\t Stress11: {worstPlateResult1.S11}, stress22: {worstPlateResult1.S22}, stress33: {worstPlateResult1.S33}");
            Console.WriteLine($"\t Id: {worstPlateResult1.Element.Id} Point: {(worstPlateResult1.ResultPoint as ResultStressPoint).Location} Node0 Id: {worstPlateResult1.Element.Nodes[0].Position}");

            Console.WriteLine($"DEFLECTION");
            Console.WriteLine($"\t WorstDeflection: {worstNodeDisplacement1.GetResultingDisplacement()}");
            Console.WriteLine($"\t Id: {worstNodeDisplacement1.Element.Id} Point: {worstNodeDisplacement1.Element.Position} D1: {worstNodeDisplacement1.D1} D2: {worstNodeDisplacement1.D2} D3: {worstNodeDisplacement1.D3} ");

            Assert.AreEqual(28.71, worstPlateResult1.S11, 1);
            Assert.AreEqual(5.78, worstNodeDisplacement1.D3, 0.2);

            Assert.AreEqual(28.71, worstPlateResult2.S11, 1);
            Assert.AreEqual(5.78, worstNodeDisplacement2.D3, 0.2);
        }



#if _false
        [TestMethod]
        public void MonolithicGlass2()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 400, 100));
            Shape s2 = GetRectangularShape(new Point3d(100, 100, 100), new Vector3d(300, 500, 200));

            //var restrains1 = s1.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s1.GetCoordinateSystem()))).ToList();
            //var restrains2 = s2.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s2.GetCoordinateSystem()))).ToList();

            MonolithicGlass mg1 = new MonolithicGlass("mg1", 10, GetGlassMaterialPrEn());
            MonolithicGlass mg2 = new MonolithicGlass("mg2", 20, GetGlassMaterialPrEn());

            GlassSurface gs1 = new GlassSurface(s1);
            GlassSurface gs2 = new GlassSurface(s2);

            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseType.Wind, Guid.NewGuid());

            AreaLoad s1GalLc1 = new AreaLoad(100, 200, 300, s1, lc1);
            AreaLoad s1GalLc2 = new AreaLoad(101, 201, 301, s1, lc2);

            AreaLoad s2GalLc1 = new AreaLoad(150, 250, 350, s2, lc1);
            AreaLoad s2GalLc2 = new AreaLoad(151, 251, 351, s2, lc2);

            //gs1.AddLoad(s1GalLc1);
            //gs1.AddLoad(s1GalLc2);

            //gs2.AddLoad(s2GalLc1);
            //gs2.AddLoad(s2GalLc2);

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

            //En16612Checker check = new En16612Checker(model, new Checker.CheckParameters());
            //check.SetUpFemModels();
            //check.ExportToSt7();
        }


        [TestMethod]
        public void MonolithicGlass3()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 400, 100));
            Shape s2 = GetRectangularShape(new Point3d(000, 100, 400), new Vector3d(300, 500, 200));
            Shape s3 = GetRectangularShape(new Point3d(000, 300, 800), new Vector3d(300, 500, 200));

            //var restrains1 = s1.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s1.GetCoordinateSystem()))).ToList();
            //var restrains2 = s2.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s2.GetCoordinateSystem()))).ToList();
            //var restrains3 = s3.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s3.GetCoordinateSystem()))).ToList();
            //restrains3.RemoveAt(0);

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 10, GetGlassMaterialPrEn());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialPrEn());
            MonolithicGlass mg3 = new MonolithicGlass("Mg3", 30, GetGlassMaterialPrEn());

            GlassSurface gs1 = new GlassSurface(s1);
            GlassSurface gs2 = new GlassSurface(s2);
            GlassSurface gs3 = new GlassSurface(s3);

            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseType.Wind, Guid.NewGuid());
            LoadCase lc3 = new LoadCase("LC3", 10000, 20, LoadCase.LoadCaseType.SelfWeight, Guid.NewGuid());

            AreaLoad s1GalLc1 = new AreaLoad(100, 0, 0, s1, lc1);
            AreaLoad s2GalLc2 = new AreaLoad(160, 0, 0, s2, lc2);
            AreaLoad s3GalLc3 = new AreaLoad(170, 0, 0, s3, lc3);

            PointLoad s1gpl = new PointLoad(1, 2, 3, 4, 5, 6, new Point3d(100, 200, 50), lc2);

            //gs1.AddLoad(s1GalLc1);
            //gs1.AddLoad(s1gpl);

            //gs2.AddLoad(s2GalLc2);

            //gs3.AddLoad(s3GalLc3);

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

            //Checker.CheckParameters checkParameters = new Checker.CheckParameters();
            //checkParameters.SetAnalysisType(Checker.CheckParameters.AnalysisType.LinearStaticAnalisys);
            //checkParameters.SetLaminatedAnalysisType(Checker.CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer);

            //En16612Checker check = new En16612GlassChecker(model, new Checker.CheckParameters()) ;
            //check.SetUpFemModels();
            //check.ExportToSt7();
        }

        /// <summary>
        /// Test di creazione di 2 vetri monolitici di grandezza 1500x2000 mm, spessore 8mm, sul piano XZ.
        /// Carico uniformemente distribuito lungo y di 1.2KPa.
        /// Il vetro 1 è incastrato lungo il bordo, il vetro 2 è appoggiato. il caso 2 è risolto a pagina 232 della CNR.
        /// I risultati attesi per il vetro 2 sono: u = 12.88mm e sigma principale = 17.52 MPa per il calcolo lineare
        /// </summary>
        [TestMethod]
        public void MonolithicGlass4()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1500, 0, 2000));
            Shape s2 = GetRectangularShape(new Point3d(2000, 0, 0), new Vector3d(1500, 0, 2000));

            //var restrains1 = s1.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s1.GetCoordinateSystem()))).ToList();
            //var restrains2 = s2.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetRotationReleased(s2.GetCoordinateSystem()))).ToList();

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 8, GetGlassMaterialPrEn());

            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 8, GetGlassMaterialPrEn());


            LoadCase lc1 = new LoadCase("LC1", 3, 20, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            AreaLoad NormalAreaLoad1 = new AreaLoad(0, 0.0012, 0, s1, lc1);
            AreaLoad NormalAreaLoad2 = new AreaLoad(0, 0.0012, 0, s2, lc1);


            //GlassSurface gs1 = new GlassSurface(mg1, s1, null, restrains1, null, 0, Guid.NewGuid());
            //gs1.AddLoad(NormalAreaLoad1);
            //GlassSurface gs2 = new GlassSurface(mg2, s2, null, restrains2, null, 0, Guid.NewGuid());
            //gs2.AddLoad(NormalAreaLoad2);


            string outputFolder = Path.Combine(_outputFolder, TestContext.TestName);
            Directory.CreateDirectory(outputFolder);

            //Model model = new Model(outputFolder);
            //model.AddSurface(gs1);
            //model.AddSurface(gs2);

            //var cp = new Checker.CheckParameters();
            //cp.SetLaminatedAnalysisType(Checker.CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer);

            //En16612Checker check = new En16612GlassChecker(model, new Checker.CheckParameters());
            //check.SetUpFemModels();

            //check.ExportToSt7();

            //check.RunSt7Solver();
        }




        [TestMethod]
        public void MonoAndLaminatedGlass()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 500));
            Shape s2 = GetRectangularShape(new Point3d(300, 0, 0), new Vector3d(200, 0, 400));
            Shape s3 = GetRectangularShape(new Point3d(700, 0, 0), new Vector3d(200, 0, 400));

            //var restrains1 = s1.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s1.GetCoordinateSystem()))).ToList();
            //var restrains2 = s2.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s2.GetCoordinateSystem()))).ToList();
            //var restrains3 = s3.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s3.GetCoordinateSystem()))).ToList();
            //restrains3.RemoveAt(0);

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 10, GetGlassMaterialPrEn());

            MonolithicGlass mg21 = new MonolithicGlass("Mg21", 5, GetGlassMaterialPrEn());
            MonolithicGlass mg22 = new MonolithicGlass("Mg22", 20, GetGlassMaterialPrEn());
            MonolithicGlass mg31 = new MonolithicGlass("Mg32", 15, GetGlassMaterialPrEn());


            Interlayer intr = new Interlayer("Int", 0.76, GetInterlayerMaterial(), Guid.NewGuid());


            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[]{ mg21, mg22 }, new Interlayer[] { intr });
            LaminatedGlass lg2 = new LaminatedGlass("Lg1", new MonolithicGlass[]{ mg31, mg22 }, new Interlayer[] { intr });

            LoadCase lc1 = new LoadCase("LC1", 50, 20, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseType.Wind, Guid.NewGuid());

            PointLoad s1gpl1 = new PointLoad(0, 2, 0, 0, 0, 0, new Point3d(80, 0, 400), lc1);
            PointLoad s1gpl2 = new PointLoad(0, 2, 0, 0, 0, 0, new Point3d(80, 0, 450), lc2);
            PointLoad s2gpl1 = new PointLoad(0, 10, 0, 0, 0, 0, new Point3d(380, 0, 300), lc1);
            PointLoad s2gpl2 = new PointLoad(0, 10, 0, 0, 0, 0, new Point3d(380, 0, 350), lc2);

            //GlassSurface gs1 = new GlassSurface(mg1, s1, new List<Load> { s1gpl1 }, restrains1, null, 0, Guid.NewGuid());
            //gs1.AddLoad(s1gpl2);
            //GlassSurface gs2 = new GlassSurface(lg1, s2, new List<Load> { s2gpl1 }, restrains2, null, 0, Guid.NewGuid());
            //gs2.AddLoad(s2gpl2);
            //GlassSurface gs3 = new GlassSurface(lg2, s3, null, restrains3, null, 0, Guid.NewGuid());


            string outputFolder = Path.Combine(_outputFolder, TestContext.TestName);
            Directory.CreateDirectory(outputFolder);

            //Model model = new Model(outputFolder);
            //model.AddSurface(gs1);
            //model.AddSurface(gs2);
            //model.AddSurface(gs3);

            //var cp = new Checker.CheckParameters();
            //cp.SetLaminatedAnalysisType(Checker.CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer);

            //En16612Checker check = new En16612GlassChecker(model, new Checker.CheckParameters());
            //check.SetUpFemModels();
            
            //check.ExportToSt7();

            //check.RunSt7Solver();

        }


        [TestMethod]
        public void LaminatedGlass()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 500));

            //var restrains1 = s1.Fill.Explode().Select(i => new LineRestrain(i, Restrain.GetAllFixed(s1.GetCoordinateSystem()))).ToList();
            //restrains1.RemoveAt(0);

            MonolithicGlass mg21 = new MonolithicGlass("Mg21", 5, GetGlassMaterialPrEn());
            MonolithicGlass mg22 = new MonolithicGlass("Mg22", 20, GetGlassMaterialPrEn());

            Interlayer intr = new Interlayer("Int", 0.76, GetInterlayerMaterial(), Guid.NewGuid());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg21, mg22 }, new Interlayer[] { intr });

            LoadCase lc1 = new LoadCase("LC1", 50, 20, LoadCase.LoadCaseType.LiveLoad, Guid.NewGuid());
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseType.Wind, Guid.NewGuid());

            PointLoad s1gpl1 = new PointLoad(0, 2, 0, 0, 0, 0, new Point3d(80, 0, 400), lc1);
            PointLoad s1gpl2 = new PointLoad(0, 2, 0, 0, 0, 0, new Point3d(80, 0, 450), lc2);
            PointLoad s2gpl1 = new PointLoad(0, 10, 0, 0, 0, 0, new Point3d(380, 0, 300), lc1);
            PointLoad s2gpl2 = new PointLoad(0, 10, 0, 0, 0, 0, new Point3d(380, 0, 350), lc2);
            

            //GlassSurface gs2 = new GlassSurface(lg1, s1, null, restrains1, null, 0, Guid.NewGuid());
            //gs2.AddLoad(s1gpl1);


            string outputFolder = Path.Combine(_outputFolder, TestContext.TestName);
            Directory.CreateDirectory(outputFolder);

            Model model = new Model(outputFolder);
            //model.AddSurface(gs2);

            //var cp = new Checker.CheckParameters();
            //cp.SetLaminatedAnalysisType(Checker.CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer);

            //En16612Checker check = new En16612GlassChecker(model, new Checker.CheckParameters());
            //check.SetUpFemModels();

            //check.ExportToSt7();

            //check.RunSt7Solver();
        }
    
#endif
    }
}
