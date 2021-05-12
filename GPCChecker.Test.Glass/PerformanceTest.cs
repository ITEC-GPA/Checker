using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Models;
using GPC.Checkers.Glasses.Restrain;
using GPC.Geometry;
using GPC.Model.Glasses;
using GPC.Model.Loads;
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
    public class PerformanceTest
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
                Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.ASTME1300, Prototype.LaminatedEqThicknessMethods.ASTME1300, 
                Prototype.SolverTypes.GPCSolver, Prototype.LaminatedAnalysisTypes.EquivalentThickness);
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
                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);
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
            Assert.IsTrue(stopWatch.ElapsedMilliseconds < 5000, "Too slow");
        }
    }
}
