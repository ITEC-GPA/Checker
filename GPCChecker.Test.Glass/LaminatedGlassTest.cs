using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Collections.Generic;
using GlassTests;
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Models;
using GPC.Checkers.Glasses.Loads;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Combinations;
using GPC.Model.Glasses;
using GPC.Model.Materials;
using GPC.Model.Restrains;
using GPC.Model.FreedomCases;
using GPC.TestUtilities;

namespace GlassTests
{
    [TestClass]
    public class LaminatedGlassTest : GlassTestBase
    {


        [TestMethod]
        [TestCategory("Linear")]
        [TestCategory("MissingAssert")]
        [TestCategory("Layers: 5")]
        public void LaminatedGlass2()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 500, 0));
            s1.Fill[0].Move(new Vector3d(50, 0, 0));

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
            Prototype p1 = new Prototype("p1", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);
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
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

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

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddLoad(nal1);
            gs1.AddLoad(nal2);
            gs1.AddLoad(nal3);
            gs1.AddLoad(lll);
            gs1.AddLoad(swl);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            model.AddCombination(combo1);
            model.AddCombination(combo2);
            model.AddCombination(combo3);
            model.AddCombination(combo4);

            // Model
            Assert.IsTrue(model.AddSurface(gs1, base.GetTestName()), "Add Surface failed");

            model.PerformChecks();

        }


        [TestMethod]
        [TestCategory("NonLinear")]
        [TestCategory("MissingAssert")]
        [TestCategory("Layers: 5")]
        public void LaminatedGlass3()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 500, 0));
            s1.Fill[0].Move(new Vector3d(50, 0, 0));

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
                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);
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
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

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
            gs1.AddLoad(swl);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            p1.AddCombination(combo1);
            model.AddCombination(combo1);
            model.AddCombination(combo5);
            model.AddCombination(combo2);
            model.AddCombination(combo3);
            model.AddCombination(combo4);

            // Model
            Assert.IsTrue(model.AddSurface(gs1, base.GetTestName()), "Add Surface failed");


            model.PerformChecks();
        }


        [TestMethod]
        [TestCategory("NonLinear")]
        [TestCategory("MissingAssert")]
        [TestCategory("NoSelfWeight")]
        [TestCategory("Layers: 5")]
        public void LaminatedGlass4()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 500, 0));
            s1.Fill[0].Move(new Vector3d(50, 0, 0));

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
                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);
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

            // Model
            Assert.IsTrue(model.AddSurface(gs1, base.GetTestName()), "Add Surface failed");


            model.PerformChecks();

        }



        [TestMethod]
        [TestCategory("Linear")]
        [TestCategory("MissingAssert")]
        [TestCategory("Layers: 2")]
        public void LaminatedGlass5()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 500, 0));
            s1.Fill[0].Move(new Vector3d(50, 0, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialAstm());

            Interlayer intr1 = new Interlayer("Int1", 0.76, GetInterlayerMaterialPVBStiff());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2}, new Interlayer[] { intr1});

            // Prototype
            Prototype p1 = new Prototype("p1", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);
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
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

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
            gs1.AddLoad(swl);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            model.AddCombination(combo1);
            model.AddCombination(combo2);
            model.AddCombination(combo3);
            model.AddCombination(combo4);

            // Model
            Assert.IsTrue(model.AddSurface(gs1, base.GetTestName()), "Add Surface failed");

            model.PerformChecks();

            //Assert.IsTrue(model.GlassSurfaces.First().Checker.FemModel.no);


#if DEBUG
            model.GlassSurfaces.First().Checker.FemModel.ExportSt7PlateUserDefinedCustomResultFile(base.GetOutputFolder(), base.GetTestName() + "_PlateContour", combo3);
            model.GlassSurfaces.First().Checker.FemModel.ExportSt7NodeUserDefinedCustomResultFile(base.GetFilePathInOutputFolder(base.GetTestName() + "_NodeContour", "txt"), combo3);
#endif

        }


        [TestMethod]
        [TestCategory("NonLinear")]
        [TestCategory("MissingAssert")]
        [TestCategory("Layers: 3")]
        public void LaminatedGlass6()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 500, 0));
            s1.Fill[0].Move(new Vector3d(50, 0, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialAstm());
            MonolithicGlass mg3 = new MonolithicGlass("Mg3", 15, GetGlassMaterialAstm());


            Interlayer intrPvb = new Interlayer("Int1", 0.76, GetInterlayerMaterialPVBStiff());
            Interlayer intrSentry = new Interlayer("Int2", 0.76, GetInterlayerMaterialSentryGlas());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2, mg3}, new Interlayer[] { intrPvb, intrSentry});

            // Prototype
            Prototype p1 = new Prototype("p1", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.NonLinearStaticAnalysis,
                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);
            p1.MeshOptions.MeshSize = 20;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            ClimateLoadCase lcCSD = new ClimateLoadCase("Climate", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer, GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 10, 20, EN16612LoadDurations.CLIMATESUMMER, 40);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            List<GPC.Model.LoadCases.LoadCaseBase> loadCases = new List<GPC.Model.LoadCases.LoadCaseBase>
            {
                lcSw,
                lcCSD,
                lcWp,
                lcLl
            };

            NormalAreaLoad nal1 = new NormalAreaLoad(1, s1, lcCSD);
            NormalAreaLoad nal2 = new NormalAreaLoad(2, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(3, s1, lcLl);
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

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
            gs1.AddLoad(swl);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            p1.AddCombination(combo1);
            model.AddCombination(combo1);
            model.AddCombination(combo5);
            model.AddCombination(combo2);
            model.AddCombination(combo3);
            model.AddCombination(combo4);

            // Model
            Assert.IsTrue(model.AddSurface(gs1, base.GetTestName()), "Add Surface failed");


            model.PerformChecks();

            loadCases.ForEach(i => Console.WriteLine($"LC: {i.Name} \t\t Duration: {(i as IGlassLoadCase).LoadDuration} \t\t Temperature: {(i as IGlassLoadCase).Temperature} " +
                $"\t\t GPvb: {intrPvb.Material.GetShearModule((i as IGlassLoadCase).LoadDuration, (i as IGlassLoadCase).Temperature):F3} \t GSentry: {intrSentry.Material.GetShearModule((i as IGlassLoadCase).LoadDuration, (i as IGlassLoadCase).Temperature):F3}"));

        }




    }
}
