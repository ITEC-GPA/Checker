using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Models;
using GPC.Checkers.Glasses.Restrain;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Combinations;
using GPC.Model.FEM;
using GPC.Model.FreedomCases;
using GPC.Model.Glasses;
using GPC.Checkers.Glasses.Loads;
using GPC.Model.Restrains;
using GPC.Model.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GlassTests
{
    [TestClass]
    public class MonolithicGlassTest : GlassTestBase
    {
        [TestMethod]
        [TestCategory("Linear")]
        public void MonolithicGlass0()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            // Shape
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(800, 1600, 0));

            List<IParametricRestrain> parametricRestrains = new List<IParametricRestrain>();
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(Solver.DOF.DX) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                                                    Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                                                    new Prototype.LaminatedEqThicknessParameters());
            p1.MeshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddRestrains(geometryRestrains1);


            // LoadCases
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.010, s1, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);

            // Combination
            Combination cmb1 = new Combination("CMB1");
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new Combination("CMB2");
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            model.AddSurface(gs1, base.GetTestName());
            model.PerformChecks();

            var worstDisplacementsCmb1 = GetWorstDisplacementResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationNodeDisplacementResult(cmb1));
            var worstDisplacementsCmb2 = GetWorstDisplacementResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationNodeDisplacementResult(cmb2));

            var worstStressesCmb1 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(cmb1));
            var worstStressesCmb2 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(cmb2));


            Assert.AreEqual(23.17, worstDisplacementsCmb1[2].D3, 0.2);
            Assert.AreEqual(18.54, worstDisplacementsCmb2[2].D3, 0.2);

            Assert.AreEqual(107, worstStressesCmb1[0].S11, 1);
            Assert.AreEqual(85, worstStressesCmb2[0].S11, 1);

        }

        [TestMethod]
        [TestCategory("Linear")]
        public void MonolithicGlass1()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            // Shape
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(800, 1600, 0));
            Shape s2 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 1600, 0));

            List<IParametricRestrain> parametricRestrains = new List<IParametricRestrain>();
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(Solver.DOF.DX) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));
            List<GeometryRestrain> geometryRestrains2 = new List<GeometryRestrain>();
            geometryRestrains2.AddRange(s2.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                                                Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, 
                                                                Prototype.LaminatedAnalysisTypes.MultiElement, new Prototype.LaminatedEqThicknessParameters());
            p1.MeshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            GlassSurface gs2 = new GlassSurface(p1, s2);
            gs1.AddRestrains(geometryRestrains1);
            gs2.AddRestrains(geometryRestrains2);

            // LoadCases
            LoadCase lc1 = new LoadCase("LL", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("Wp", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

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
            Combination cmb1 = new Combination("CMB1");
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new Combination("CMB2");
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            model.AddSurface(gs1, base.GetTestName());
            model.AddSurface(gs2, base.GetTestName());

            model.PerformChecks();


//#if DEBUG
//            model.GlassSurfaces.First().Checker.FemModel.ExportSt7PlateUserDefinedCustomResultFile(base.GetOutputFolder(), base.GetTestName() + "_PlateContour", cmb1);
//            model.GlassSurfaces.First().Checker.FemModel.ExportSt7NodeUserDefinedCustomResultFile(base.GetFilePathInOutputFolder(base.GetTestName() + "_NodeContour", "txt"), cmb1);
//#endif

            var gs1WorstDisplacementsCmb1 = GetWorstDisplacementResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationNodeDisplacementResult(cmb1));
            var gs1WorstStressesCmb1 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(cmb1));

            var gs2WorstDisplacementsCmb1 = GetWorstDisplacementResults(model.GlassSurfaces.LastOrDefault().Checker.GetCombinationNodeDisplacementResult(cmb1));
            var gs2WorstStressesCmb1 = GetWorstStressResults(model.GlassSurfaces.LastOrDefault().Checker.GetCombinationPlateStressResult(cmb1));

            Assert.AreEqual(5.78, gs1WorstDisplacementsCmb1[2].D3, 0.2);
            Assert.AreEqual(28.46, gs1WorstStressesCmb1[0].S11, 1);


            Assert.AreEqual(10.84, gs2WorstDisplacementsCmb1[2].D3, 0.2);
            Assert.AreEqual(35.27, gs2WorstStressesCmb1[0].S11, 0.2);
        }

        [TestMethod]
        [TestCategory("NonLinear")]
        public void MonolithicGlass2()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            // Shape
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(800, 1600, 0));

            List<IParametricRestrain> parametricRestrains = new List<IParametricRestrain>();
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(Solver.DOF.DX) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.NonLinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                                Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                                new Prototype.LaminatedEqThicknessParameters());

            p1.MeshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddRestrains(geometryRestrains1);


            // LoadCases
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.001, s1, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);

            // Combination
            Combination cmb1 = new Combination("CMB1");
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new Combination("CMB2");
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            Console.WriteLine($"{cmb1.Name}: {cmb1}");
            Console.WriteLine($"{cmb2.Name}: {cmb2}");

            model.AddSurface(gs1, base.GetTestName());
            model.PerformChecks();


            var gs1WorstDisplacementsCmb1 = GetWorstDisplacementResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationNodeDisplacementResult(cmb1));
            var gs1WorstStressesCmb1 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(cmb1));

            var gs2WorstDisplacementsCmb1 = GetWorstDisplacementResults(model.GlassSurfaces.LastOrDefault().Checker.GetCombinationNodeDisplacementResult(cmb1));
            var gs2WorstStressesCmb1 = GetWorstStressResults(model.GlassSurfaces.LastOrDefault().Checker.GetCombinationPlateStressResult(cmb1));

            Assert.AreEqual(4.16, gs1WorstDisplacementsCmb1[2].D3, 0.2);
            Assert.AreEqual(26.04, gs1WorstStressesCmb1[0].S11, 1);

        }


        [TestMethod]
        [TestCategory("Linear")]
        [TestCategory("MissingAssert")]
        public void MonolithicGlass3()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            // Shape
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(800, 1600, 0));

            List<IParametricRestrain> parametricRestrains = new List<IParametricRestrain>();
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(Solver.DOF.DX) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                                                    Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                                                    new Prototype.LaminatedEqThicknessParameters());
            p1.MeshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddRestrains(geometryRestrains1);


            // LoadCases
            LoadCase lc0 = new LoadCase("SW", 100, 20, LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.001, s1, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);
            SelfWeightLoad swl = new SelfWeightLoad(lc0, model.Options.GetGravitySign() * GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);
            gs1.AddLoad(swl);

            // Combination
            Combination cmb1 = new Combination("CMB1");
            cmb1[lc0] = 1.5;
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new Combination("CMB2");
            cmb2[lc0] = 1.2;
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            model.AddSurface(gs1, base.GetTestName());
            model.PerformChecks();


        }


        [TestMethod]
        [TestCategory("NonLinear")]
        [TestCategory("MissingAssert")]
        public void MonolithicGlass4()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            // Shape
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(800, 1600, 0));

            List<IParametricRestrain> parametricRestrains = new List<IParametricRestrain>();
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(Solver.DOF.DX) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.NonLinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                                                    Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                                                    new Prototype.LaminatedEqThicknessParameters());
            p1.MeshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddRestrains(geometryRestrains1);


            // LoadCases
            LoadCase lc0 = new LoadCase("SW", 100, 20, LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.001, s1, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);
            SelfWeightLoad swl = new SelfWeightLoad(lc0, model.Options.GetGravitySign() * GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);
            gs1.AddLoad(swl);

            // Combination
            Combination cmb1 = new Combination("CMB1");
            cmb1[lc0] = 1.5;
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new Combination("CMB2");
            cmb2[lc0] = 1.2;
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            model.AddSurface(gs1, base.GetTestName());
            model.PerformChecks();


        }



        [TestMethod]
        [TestCategory("NonLinear")]
        [TestCategory("MissingAssert")]
        public void MonolithicGlass5()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            // Shape
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(800, 2000, 0));

            List<IParametricRestrain> parametricRestrains = new List<IParametricRestrain>();
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(Solver.DOF.DX) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.NonLinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                                                    Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                                                    new Prototype.LaminatedEqThicknessParameters());
            p1.MeshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddRestrains(geometryRestrains1);


            // LoadCases
            LoadCase lc0 = new LoadCase("SW", 100, 20, LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.001, s1, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);
            SelfWeightLoad swl = new SelfWeightLoad(lc0, model.Options.GetGravitySign() * GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);
            gs1.AddLoad(swl);

            // Combination
            Combination cmb1 = new Combination("CMB1");
            cmb1[lc0] = 1.5;
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new Combination("CMB2");
            cmb2[lc0] = 1.2;
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            model.AddSurface(gs1, base.GetTestName());
            model.PerformChecks();


        }
    }

}