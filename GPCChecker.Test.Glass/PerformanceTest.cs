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
using GPC.Model.Materials;
using GPC.Model.Restrains;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GlassTests
{
    [TestClass]
    public class PerformanceTest : GlassTestBase
    {
        [TestMethod]
        public void Monolithic()
        {
            GlassMaterialAstm gm1 = new GlassMaterialAstm("Glass 1", 70000, 0.23, 1, 16, 23.3, 18.3, 0.001,
                GPC.Model.Units.ConvertDensityToDefault(2500, GPC.Model.Units.SI), 0.1);
            MonolithicGlass mg1 = new MonolithicGlass("Monol 4", 4, gm1);
            Polygon3d polygon = new Polygon3d()
            {
                new Point2d(-56.57, -56.57),
                new Point2d(56.57, -56.57),
                new Point2d(56.57, 56.57),
                new Point2d(-56.57, 56.57)
            };
            List<IParametricRestrain> restraints = new List<IParametricRestrain>();
            Prototype prototype = new Prototype("Prototype M1", mg1, polygon, restraints, null, Prototype.Standards.ASTME1300,
                                        Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.ASTME1300,
                                        Prototype.SolverTypes.GPCSolver, Prototype.LaminatedAnalysisTypes.EquivalentThickness,
                                        new Prototype.LaminatedEqThicknessParameters());


            prototype.MeshOptions.MeshSize = 40;
            prototype.MeshOptions.Algorithm = GPC.Geometry.Meshes.Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            ClimateLoadCase lcCSD = new ClimateLoadCase("Climate", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer,
                GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 10, 20, 12 * 60 * 60, 40);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);


            Model model = new Model(Path.GetTempPath());

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            Polygon3d fill = new Polygon3d()
            {
                new Point3d(0, 0, 0),
                new Point3d(1000, 0, 0),
                new Point3d(1000, 0, 2500),
                new Point3d(0, 0, 2500)
            };
            Shape shape = new Shape(fill);
            GlassSurface gs1 = new GlassSurface(prototype, shape);

            NormalAreaLoad nal1 = new NormalAreaLoad(1, gs1.Shape, lcCSD);
            NormalAreaLoad nal2 = new NormalAreaLoad(2, gs1.Shape, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(3, gs1.Shape, lcLl);

            SelfWeightLoad swl = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);
            LineLoad lll = new LineLoad(model.Options.GetGravityVector() * 1, model.Options.GetGravityVector() * 0,
                new Line3d(new Point3d(50, 0, 1000), new Point3d(950, 0, 1000)), lcLl, CoordinateSystem.Global);

            gs1.AddLoad(nal1);
            gs1.AddLoad(nal2);
            gs1.AddLoad(nal3);
            gs1.AddLoad(lll);
            gs1.AddLoad(swl);

            gs1.AddRestrains(gs1.Shape.Fill.Explode()
                .Select(i => (GeometryRestrain)LineRestrain.GetAllFixed(i, new GPC.Model.FreedomCases.FreedomCase("fc1"), CoordinateSystem.Global))
                .ToList());

            Debug.WriteLine(stopWatch.Elapsed, "GlassSurface created");

            stopWatch.Restart();
            model.AddSurface(gs1);
            Debug.WriteLine(stopWatch.Elapsed, "GlassSurface added to model");

            stopWatch.Stop();
            Debug.WriteLine("Finish");
            Assert.IsTrue(stopWatch.ElapsedMilliseconds < 1500, "Too slow");
        }


        [TestMethod]
        public void Monolithic2()
        {
            /// Tempo per ogni iterazione
            /// 2021/05/13: 0.29 secondi
            
            Action action = new Action(() =>
            {
                Model model = new Model(base.GetOutputFolder());

                // Shape
                Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(800, 1600, 0));

                List<IParametricRestrain> parametricRestrains = new List<IParametricRestrain>();
                parametricRestrains.AddRange(s1.Fill
                    .Explode()
                    .Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(Solver.DOF.DX) })));

                List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
                geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

                MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

                // Prototype
                Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, 
                    Prototype.CheckMethods.DominantLoad, Prototype.SolverTypes.GPCSolver, Prototype.LaminatedAnalysisTypes.MultiElement,
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

                model.AddSurface(gs1, base.GetTestName());
            });

            var timeSpan = TimeSpan.FromMilliseconds(GPC.Utilities.Time.MeasureTime.FunctionExecutionTime(2, action, true));

            Debug.WriteLine($"Seconds elapsed for each iteration: {timeSpan.TotalSeconds}");
            Assert.IsTrue(timeSpan.TotalSeconds < 1, $"Seconds elapsed for each iteration: {timeSpan.TotalSeconds}");
        }


        [TestMethod]
        public void Laminated()
        {
            GlassMaterialAstm gm1 = new GlassMaterialAstm("Glass 1", 70000, 0.23, 1, 16, 23.3, 18.3, 0.001,
                GPC.Model.Units.ConvertDensityToDefault(2500, GPC.Model.Units.SI), 0.1);
            MonolithicGlass mg1 = new MonolithicGlass("Monol 4", 4, gm1);
            var im1 = new InterlayerMaterial("ES Stiff PVB", 1, 0, InterlayerMaterial.InterlayerType.NormalPVB);
            im1.AddShearModule(3, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 699, 342, 58, 3.4, 1.7, 1.6, 0, 0 });
            im1.AddShearModule(60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 573, 196, 9.2, 1.8, 1.6, 1.5, 1.9, 0.8 });
            im1.AddShearModule(60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 388, 37, 2, 1.6, 0, 0, 0, 0 });
            im1.AddShearModule(30 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 80, 1.9, 1.5, 1.5, 0, 0, 0, 0 });
            im1.AddShearModule(365 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 19, 1.6, 1.5, 0, 0, 0, 0, 0 });
            im1.AddShearModule(50 * 365 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            Interlayer intr1 = new Interlayer("PVBStiff", 0.76, im1);

            LaminatedGlass lg1 = new LaminatedGlass("44.2", new MonolithicGlass[] { mg1, mg1 }, new Interlayer[] { intr1 });
            Prototype prototype = new Prototype("Prototype 44.2", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                Prototype.CheckMethods.DominantLoad,Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters());

            prototype.MeshOptions.MeshSize = 40;
            prototype.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            ClimateLoadCase lcCSD = new ClimateLoadCase("Climate", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer,
                GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 10, 20, 12 * 60 * 60, 40);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            Model model = new Model(Path.GetTempPath());

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            Polygon3d fill = new Polygon3d()
            {
                new Point3d(0, 0, 0),
                new Point3d(1000, 0, 0),
                new Point3d(1000, 0, 2500),
                new Point3d(0, 0, 2500)
            };
            Shape shape = new Shape(fill);
            GlassSurface gs1 = new GlassSurface(prototype, shape);
            NormalAreaLoad nal1 = new NormalAreaLoad(1, gs1.Shape, lcCSD);
            NormalAreaLoad nal2 = new NormalAreaLoad(2, gs1.Shape, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(3, gs1.Shape, lcLl);
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);
            LineLoad lll = new LineLoad(model.Options.GetGravityVector() * 1, model.Options.GetGravityVector() * 0,
                new Line3d(new Point3d(50, 0, 1000), new Point3d(950, 0, 1000)), lcLl, CoordinateSystem.Global);

            gs1.AddLoad(nal1);
            gs1.AddLoad(nal2);
            gs1.AddLoad(nal3);
            gs1.AddLoad(lll);
            gs1.AddLoad(swl);

            gs1.AddRestrains(gs1.Shape.Fill.Explode()
                .Select(i => (GeometryRestrain)LineRestrain.GetAllFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global))
                .ToList());
            stopWatch.Stop();
            Debug.WriteLine(stopWatch.Elapsed, "GlassSurface created");

            stopWatch.Restart();
            model.AddSurface(gs1);
            stopWatch.Stop();
            Debug.WriteLine(stopWatch.Elapsed, "GlassSurface added to model");
            // Da 26 secondi a 12
            // Con collection nodi ordinata: 7-8 secondi
            
            Debug.WriteLine("Finish");
            //Assert.IsTrue(stopWatch.ElapsedMilliseconds < 5000, "Too slow");
        }
        

        [TestMethod]
        [TestCategory("Layers: 5")]
        public void Laminated2()
        {
            /// Tempo per ogni iterazione
            /// 2021/05/13: > 15 min

            Action action = new Action(() => 
            {
                Model model = new Model(base.GetOutputFolder());

                Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1500, 2500, 0));


                MonolithicGlass mg1 = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());
                MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialAstm());
                MonolithicGlass mg3 = new MonolithicGlass("Mg3", 15, GetGlassMaterialAstm());
                MonolithicGlass mg4 = new MonolithicGlass("Mg4", 4, GetGlassMaterialAstm());
                MonolithicGlass mg5 = new MonolithicGlass("Mg5", 10, GetGlassMaterialAstm());

                Interlayer intr1 = new Interlayer("Int1", 0.76, GetInterlayerMaterialPVBStiff());
                Interlayer intr2 = new Interlayer("Int2", 0.76, GetInterlayerMaterialSentryGlas());
                Interlayer intr3 = new Interlayer("Int3", 0.76, GetInterlayerMaterialSentryGlas());
                Interlayer intr4 = new Interlayer("Int4", 0.76, GetInterlayerMaterialSentryGlas());

                LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2, mg3, mg4, mg5 }, new Interlayer[] { intr1, intr2, intr3, intr4 });

                // Prototype
                Prototype p1 = new Prototype("p1", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.NonLinearStaticAnalysis,
                    Prototype.CheckMethods.DominantLoad, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                                            new Prototype.LaminatedEqThicknessParameters());
                p1.MeshOptions.MeshSize = 50;
                p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

                // Load
                LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
                ClimateLoadCase lcCSD = new ClimateLoadCase("Climate", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer, GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 10, 20, EN16612LoadDurations.CLIMATESUMMER, 40);
                LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
                LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

                NormalAreaLoad nal1 = new NormalAreaLoad(1, s1, lcCSD);
                NormalAreaLoad nal2 = new NormalAreaLoad(2, s1, lcWp);
                NormalAreaLoad nal3 = new NormalAreaLoad(3, s1, lcLl);

                //LineLoad lll = new LineLoad(model.Options.GetGravityVector() * 1, model.Options.GetGravityVector() * 0, new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

                // Combinazioni
                Combination combo1 = new Combination("Cmb1");
                combo1.AddLoadCaseCoefficient(lcSw, 1);
                combo1.AddLoadCaseCoefficient(lcLl, 1);

                Combination combo2 = new Combination("Cmb2");
                combo2.AddLoadCaseCoefficient(lcSw, 1);
                combo2.AddLoadCaseCoefficient(lcWp, 0.6);

                Combination combo3 = new Combination("Cmb3");
                combo3.AddLoadCaseCoefficient(lcSw, 1);
                combo3.AddLoadCaseCoefficient(lcLl, 0.75);
                combo3.AddLoadCaseCoefficient(lcWp, 0.4);

                Combination combo4 = new Combination("Cmb4");
                combo4.AddLoadCaseCoefficient(lcSw, 1);
                combo4.AddLoadCaseCoefficient(lcCSD, 0.75);
                combo4.AddLoadCaseCoefficient(lcLl, 0.75);
                combo4.AddLoadCaseCoefficient(lcWp, 0.4);

                Combination combo5 = new Combination("Cmb5");
                combo5.AddLoadCaseCoefficient(lcSw, 1.5);
                combo5.AddLoadCaseCoefficient(lcWp, 0.6);

                // Surface
                GlassSurface gs1 = new GlassSurface(p1, s1);
                gs1.AddLoad(nal1);
                gs1.AddLoad(nal2);
                gs1.AddLoad(nal3);
                //gs1.AddLoad(lll);

                gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


                p1.AddCombination(combo1);
                model.AddCombination(combo1);
                model.AddCombination(combo5);
                model.AddCombination(combo2);
                model.AddCombination(combo3);
                model.AddCombination(combo4);

                model.AddSurface(gs1, base.GetTestName());

            });

            var timeSpan = TimeSpan.FromMilliseconds(GPC.Utilities.Time.MeasureTime.FunctionExecutionTime(2, action, true));

            Console.WriteLine($"Seconds elapsed for each iteration: {timeSpan.TotalSeconds}");
            Assert.IsTrue(timeSpan.TotalSeconds < 100, $"Seconds elapsed for each iteration: {timeSpan.TotalSeconds}");
        }


        [TestMethod]
        [TestCategory("Layers: 5")]
        public void Laminated3()
        {
            /// Tempo per ogni iterazione
            /// 2021/05/13: 15 secondi 
            /// 2021/05/19: 16 secondi (Geometry 1.0.8.4)
            /// 2021/06/11: 7 secondi


            Action action = new Action(() =>
            {
                Model model = new Model(base.GetOutputFolder());

                Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 500, 0));


                MonolithicGlass mg1 = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());
                MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialAstm());
                MonolithicGlass mg3 = new MonolithicGlass("Mg3", 15, GetGlassMaterialAstm());
                MonolithicGlass mg4 = new MonolithicGlass("Mg4", 4, GetGlassMaterialAstm());
                MonolithicGlass mg5 = new MonolithicGlass("Mg5", 10, GetGlassMaterialAstm());

                Interlayer intr1 = new Interlayer("Int1", 0.76, GetInterlayerMaterialPVBStiff());
                Interlayer intr2 = new Interlayer("Int2", 0.76, GetInterlayerMaterialSentryGlas());
                Interlayer intr3 = new Interlayer("Int3", 0.76, GetInterlayerMaterialSentryGlas());
                Interlayer intr4 = new Interlayer("Int4", 0.76, GetInterlayerMaterialSentryGlas());

                LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2, mg3, mg4, mg5 }, new Interlayer[] { intr1, intr2, intr3, intr4 });

                // Prototype
                Prototype p1 = new Prototype("p1", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.NonLinearStaticAnalysis,
                    Prototype.CheckMethods.DominantLoad, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement, new Prototype.LaminatedEqThicknessParameters());


                p1.MeshOptions.MeshSize = 20;
                p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

                // Load
                LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
                ClimateLoadCase lcCSD = new ClimateLoadCase("Climate", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer, GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 10, 20, EN16612LoadDurations.CLIMATESUMMER, 40);
                LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
                LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

                NormalAreaLoad nal1 = new NormalAreaLoad(1, s1, lcCSD);
                NormalAreaLoad nal2 = new NormalAreaLoad(2, s1, lcWp);
                NormalAreaLoad nal3 = new NormalAreaLoad(3, s1, lcLl);

                LineLoad lll = new LineLoad(model.Options.GetGravityVector() * 1, model.Options.GetGravityVector() * 0, new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

                // Combinazioni
                Combination combo1 = new Combination("Cmb1");
                combo1.AddLoadCaseCoefficient(lcSw, 1);
                combo1.AddLoadCaseCoefficient(lcLl, 1);

                Combination combo2 = new Combination("Cmb2");
                combo2.AddLoadCaseCoefficient(lcSw, 1);
                combo2.AddLoadCaseCoefficient(lcWp, 0.6);

                Combination combo3 = new Combination("Cmb3");
                combo3.AddLoadCaseCoefficient(lcSw, 1);
                combo3.AddLoadCaseCoefficient(lcLl, 0.75);
                combo3.AddLoadCaseCoefficient(lcWp, 0.4);

                Combination combo4 = new Combination("Cmb4");
                combo4.AddLoadCaseCoefficient(lcSw, 1);
                combo4.AddLoadCaseCoefficient(lcCSD, 0.75);
                combo4.AddLoadCaseCoefficient(lcLl, 0.75);
                combo4.AddLoadCaseCoefficient(lcWp, 0.4);

                Combination combo5 = new Combination("Cmb5");
                combo5.AddLoadCaseCoefficient(lcSw, 1.5);
                combo5.AddLoadCaseCoefficient(lcWp, 0.6);

                // Surface
                GlassSurface gs1 = new GlassSurface(p1, s1);
                gs1.AddLoad(nal1);
                gs1.AddLoad(nal2);
                gs1.AddLoad(nal3);
                gs1.AddLoad(lll);

                gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


                p1.AddCombination(combo1);
                model.AddCombination(combo1);
                model.AddCombination(combo5);
                model.AddCombination(combo2);
                model.AddCombination(combo3);
                model.AddCombination(combo4);

                model.AddSurface(gs1, base.GetTestName());

            });

            var timeSpan = TimeSpan.FromMilliseconds(GPC.Utilities.Time.MeasureTime.FunctionExecutionTime(2, action, true));

            Console.WriteLine($"Seconds elapsed for each iteration: {timeSpan.TotalSeconds}");
            Assert.IsTrue(timeSpan.TotalSeconds < 100, $"Seconds elapsed for each iteration: {timeSpan.TotalSeconds}");

        }
    }
}