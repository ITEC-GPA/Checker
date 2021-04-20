using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Checker.Glasses;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Checker.Glasses.LoadCases;
using GPC.Checker.Glasses.Restrain;
using GPC.Checker.Glasses.Models;
using GPC.Checker.Glasses.Glasses;
using GPC.Checker.Glasses.Results;
using GPC.Checker.Glasses.Checkers;
using GPC.Model.FreedomCases;
using GPC.Model.Combinations;
using GPC.Model.FEM;
using GPC.Model.Elements;
using GPC.Model.Restrains;
using GPC.Model.Materials;
using GPC.Model.Loads;
using GPC.Model.Glasses;
using GPC.Model.Results;
using GPC.Model.FEM.Properties;
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
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad, 
                                                                    Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

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
            Combination cmb1 = new CombinationEn("CMB1", CombinationEn.CombinationType.UltimateStructural);
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new CombinationEn("CMB2", CombinationEn.CombinationType.ServiceabilityCharacteristic);
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
            Prototype p1 = new Prototype("p1", mg, null ,null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, 
                                                                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

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
            Combination cmb1 = new CombinationEn("CMB1", CombinationEn.CombinationType.UltimateStructural);
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new CombinationEn("CMB2", CombinationEn.CombinationType.ServiceabilityCharacteristic);
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


        [TestMethod]
        public void MonolithicGlass2()
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
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.NonLinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                                                    Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

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
            Combination cmb1 = new CombinationEn("CMB1", CombinationEn.CombinationType.UltimateStructural);
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new CombinationEn("CMB2", CombinationEn.CombinationType.ServiceabilityCharacteristic);
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            Console.WriteLine($"{cmb1.Name}: {cmb1}");
            Console.WriteLine($"{cmb2.Name}: {cmb2}");

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

            Assert.AreEqual(26.04, worstPlateResult.S11, 1);
            Assert.AreEqual(4.16, worstNodeDisplacement.D3, 0.2);
        }

        [TestMethod]
        public void LaminatedGlass()
        {

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 500));


            MonolithicGlass mg1 = new MonolithicGlass("Mg12", 8, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg12", 8, GetGlassMaterialAstm());

            Interlayer intr = new Interlayer("Int", 0.76, GetInterlayerMaterial(), Guid.NewGuid());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, new Interlayer[] { intr });

            // Prototype
            Prototype p1 = new Prototype("p1", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, 
                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);

            model.AddSurface(gs1);

            model.FemModelSetup();
            model.PerformChecks();

        }


        [TestMethod]
        public void LaminatedGlass2()
        {

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 500));
            s1.Fill[0].Move(new Vector3d(50, 0, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialAstm());
            MonolithicGlass mg3 = new MonolithicGlass("Mg3", 15, GetGlassMaterialAstm());
            MonolithicGlass mg4 = new MonolithicGlass("Mg4", 4, GetGlassMaterialAstm());
            MonolithicGlass mg5 = new MonolithicGlass("Mg5", 10, GetGlassMaterialAstm());

            Interlayer intr1 = new Interlayer("Int1", 0.76, GetInterlayerMaterial());
            Interlayer intr2 = new Interlayer("Int2", 0.76, GetInterlayerMaterial());
            Interlayer intr3 = new Interlayer("Int3", 0.76, GetInterlayerMaterial());
            Interlayer intr4 = new Interlayer("Int4", 0.76, GetInterlayerMaterial());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2, mg3, mg4, mg5 }, new Interlayer[] { intr1, intr2, intr3, intr4 });

            // Prototype
            Prototype p1 = new Prototype("p1", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, 
                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 20;
            meshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lc0 = new LoadCase("Sw", 50* 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseType.SelfWeight);
            LoadCase lc1 = new LoadCase("Climate", 12 * 60 * 60, 10, GPC.Model.LoadCases.LoadCase.LoadCaseType.ClimateSummer);
            LoadCase lc2 = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseType.Wind);
            LoadCase lc3 = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseType.LiveLoad);

            NormalAreaLoad nal1 = new NormalAreaLoad(1, s1, lc1);
            NormalAreaLoad nal2 = new NormalAreaLoad(2, s1, lc2);
            NormalAreaLoad nal3 = new NormalAreaLoad(3, s1, lc3);
            LineLoad lll = new LineLoad(0, 1, 0, 0, 0, 0, new Line3d(new Point3d(40, 0, 50), new Point3d(150, 0, 200)), lc3, CoordinateSystem.Global);

            CombinationEn combo1 = new CombinationEn("Cmb1", CombinationEn.CombinationType.UltimateStructural);
            combo1.AddLoadCaseCoefficient(lc0, 1);
            combo1.AddLoadCaseCoefficient(lc3, 1);

            CombinationEn combo2 = new CombinationEn("Cmb2", CombinationEn.CombinationType.UltimateStructural);
            combo2.AddLoadCaseCoefficient(lc0, 1);
            combo2.AddLoadCaseCoefficient(lc2, 0.6);

            CombinationEn combo3 = new CombinationEn("Cmb3", CombinationEn.CombinationType.UltimateStructural);
            combo3.AddLoadCaseCoefficient(lc0, 1);
            combo3.AddLoadCaseCoefficient(lc3, 0.75);
            combo3.AddLoadCaseCoefficient(lc2, 0.4);

            CombinationEn combo4 = new CombinationEn("Cmb4", CombinationEn.CombinationType.UltimateStructural);
            combo4.AddLoadCaseCoefficient(lc0, 1);
            combo4.AddLoadCaseCoefficient(lc1, 0.75);
            combo4.AddLoadCaseCoefficient(lc3, 0.75);
            combo4.AddLoadCaseCoefficient(lc2, 0.4);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
            gs1.AddLoad(nal1);
            gs1.AddLoad(nal2);
            gs1.AddLoad(nal3);
            gs1.AddLoad(lll);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            // Model
            model.AddSurface(gs1);

            model.AddCombination(combo1);
            model.AddCombination(combo2);
            model.AddCombination(combo3);
            model.AddCombination(combo4);

            model.FemModelSetup();
            model.PerformChecks();

        }

        [TestMethod]
        public void PolygonEquals()
        {
            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, new Polygon3d(), null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            Prototype p2 = new Prototype("p1", mg, new Polygon3d(), null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);
            Assert.IsTrue(p1 == p2);
        }
    }
}
