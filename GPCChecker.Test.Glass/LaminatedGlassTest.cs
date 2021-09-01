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
                Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement, 
                new Prototype.LaminatedEqThicknessParameters(), null);

            p1.MeshOptions.MeshSize = 20;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            ClimateLoadCase lcCSD = new ClimateLoadCase("Climate", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer, 
                                                                   GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 
                                                                   10, 20, EN16612LoadDurations.CLIMATESUMMER, 40);
            
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            NormalAreaLoad nal1 = new NormalAreaLoad(0.001, s1, lcCSD);
            NormalAreaLoad nal2 = new NormalAreaLoad(0.002, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(0.003, s1, lcLl);
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, ModelAnalysisOptions.Instance.GetGravitySign() * GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            LineLoad lll = new LineLoad(ModelAnalysisOptions.Instance.GetGravityVector() * 1,
                                        ModelAnalysisOptions.Instance.GetGravityVector() * 0, 
                                        new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

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

            gs1.AddRestrains(s1.Fill.Explode().Select(i =>
                                        new LineRestrain(i, new FreedomCase("fc1"), CoordinateSystem.Global, 
                                        new List<DofRestrain> { new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ) }))
                                        .Cast<GeometryRestrain>().ToList());

            gs1.AddRestrain(new PointRestrain(s1.Fill[0], new FreedomCase("fc1"), new List<DofRestrain> {
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ),
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DX),
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DY)}));

            gs1.AddRestrain(new PointRestrain(s1.Fill[1], new FreedomCase("fc1"), new List<DofRestrain> {
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ),
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DY)}));


            model.AddCombination(combo1);
            model.AddCombination(combo2);
            model.AddCombination(combo3);
            model.AddCombination(combo4);

            // Model
            Assert.IsTrue(model.AddSurface(gs1), "Add Surface failed");
            Assert.IsTrue(model.FemModelsSetup(base.GetTestName()), "Fem model setup failed");

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
                Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                              new Prototype.LaminatedEqThicknessParameters(), null);
            p1.MeshOptions.MeshSize = 20;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            ClimateLoadCase lcCSD = new ClimateLoadCase("Climate", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer, GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 10, 20, EN16612LoadDurations.CLIMATESUMMER, 40);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            NormalAreaLoad nal1 = new NormalAreaLoad(0.001, s1, lcCSD);
            NormalAreaLoad nal2 = new NormalAreaLoad(0.002, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(0.003, s1, lcLl);
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, ModelAnalysisOptions.Instance.GetGravitySign() * GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            LineLoad lll = new LineLoad(ModelAnalysisOptions.Instance.GetGravityVector() * 1, 
                                        ModelAnalysisOptions.Instance.GetGravityVector() * 0, 
                                        new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

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
            Assert.IsTrue(model.AddSurface(gs1), "Add Surface failed");
            Assert.IsTrue(model.FemModelsSetup(base.GetTestName()), "Fem model setup failed");


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
                Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                              new Prototype.LaminatedEqThicknessParameters(), null);
            p1.MeshOptions.MeshSize = 20;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            ClimateLoadCase lcCSD = new ClimateLoadCase("Climate", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer, GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 10, 20, EN16612LoadDurations.CLIMATESUMMER, 40);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            NormalAreaLoad nal1 = new NormalAreaLoad(0.001, s1, lcCSD);
            NormalAreaLoad nal2 = new NormalAreaLoad(0.002, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(0.003, s1, lcLl);

            LineLoad lll = new LineLoad(ModelAnalysisOptions.Instance.GetGravityVector() * 1, 
                                        ModelAnalysisOptions.Instance.GetGravityVector() * 0, 
                                        new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

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
            Assert.IsTrue(model.AddSurface(gs1), "Add Surface failed");
            Assert.IsTrue(model.FemModelsSetup(base.GetTestName()), "Fem model setup failed");


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
                Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                              new Prototype.LaminatedEqThicknessParameters(), null);

            p1.MeshOptions.MeshSize = 20;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            ClimateLoadCase lcCSD = new ClimateLoadCase("Climate", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer, GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 10, 20, EN16612LoadDurations.CLIMATESUMMER, 40);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            NormalAreaLoad nal1 = new NormalAreaLoad(0.001, s1, lcCSD);
            NormalAreaLoad nal2 = new NormalAreaLoad(0.002, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(0.003, s1, lcLl);
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, ModelAnalysisOptions.Instance.GetGravitySign() * GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            LineLoad lll = new LineLoad(ModelAnalysisOptions.Instance.GetGravityVector() * 1, 
                                        ModelAnalysisOptions.Instance.GetGravityVector() * 0, 
                                        new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

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
            Assert.IsTrue(model.AddSurface(gs1), "Add Surface failed");
            Assert.IsTrue(model.FemModelsSetup(base.GetTestName()), "Fem model setup failed");

            model.PerformChecks();

            //Assert.IsTrue(model.GlassSurfaces.First().Checker.FemModel.no);


//#if DEBUG
//            model.GlassSurfaces.First().Checker.FemModel.ExportSt7PlateUserDefinedCustomResultFile(base.GetOutputFolder(), base.GetTestName() + "_PlateContour", combo3);
//            model.GlassSurfaces.First().Checker.FemModel.ExportSt7NodeUserDefinedCustomResultFile(base.GetFilePathInOutputFolder(base.GetTestName() + "_NodeContour", "txt"), combo3);
//#endif

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
                Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                              new Prototype.LaminatedEqThicknessParameters(), null);

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

            NormalAreaLoad nal1 = new NormalAreaLoad(0.001, s1, lcCSD);
            NormalAreaLoad nal2 = new NormalAreaLoad(0.002, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(0.003, s1, lcLl);
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, ModelAnalysisOptions.Instance.GetGravitySign() * GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            LineLoad lll = new LineLoad(ModelAnalysisOptions.Instance.GetGravityVector() * 1, 
                                        ModelAnalysisOptions.Instance.GetGravityVector() * 0, 
                                        new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

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

            gs1.AddRestrains(s1.Fill.Explode().Select(i =>
                            new LineRestrain(i, new FreedomCase("fc1"), CoordinateSystem.Global, new List<DofRestrain> { new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ) }))
                            .Cast<GeometryRestrain>().ToList());

            gs1.AddRestrain(new PointRestrain(s1.Fill[0], new FreedomCase("fc1"), new List<DofRestrain> {
                                                                                       new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ),
                                                                                       new DofRestrain(GPC.Model.FEM.Solver.DOF.DX),
                                                                                       new DofRestrain(GPC.Model.FEM.Solver.DOF.DY)}));

            gs1.AddRestrain(new PointRestrain(s1.Fill[1], new FreedomCase("fc1"), new List<DofRestrain> {
                                                                                       new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ),
                                                                                       new DofRestrain(GPC.Model.FEM.Solver.DOF.DY)}));


            p1.AddCombination(combo1);
            model.AddCombination(combo1);
            model.AddCombination(combo5);
            model.AddCombination(combo2);
            model.AddCombination(combo3);
            model.AddCombination(combo4);

            // Model
            Assert.IsTrue(model.AddSurface(gs1), "Add Surface failed");
            Assert.IsTrue(model.FemModelsSetup(base.GetTestName()), "Fem model setup failed");


            model.PerformChecks();

            loadCases.ForEach(i => Console.WriteLine($"LC: {i.Name} \t\t Duration: {(i as IGlassLoadCase).LoadDuration} \t\t Temperature: {(i as IGlassLoadCase).Temperature} " +
                $"\t\t GPvb: {intrPvb.Material.GetShearModule((i as IGlassLoadCase).LoadDuration, (i as IGlassLoadCase).Temperature):F3} \t GSentry: {intrSentry.Material.GetShearModule((i as IGlassLoadCase).LoadDuration, (i as IGlassLoadCase).Temperature):F3}"));

        }

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

            double tw1 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == loadWp.GlassLoadCase).Value;
            double ts11 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == loadWp.GlassLoadCase).Value;
            double ts21 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == loadWp.GlassLoadCase).Value;

            double tw2 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase).Value;
            double ts12 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase).Value;
            double ts22 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase).Value;
            
            double tw3 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == punctualLoadWp2.GlassLoadCase).Value;
            double ts13 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == punctualLoadWp2.GlassLoadCase).Value;
            double ts23 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == punctualLoadWp2.GlassLoadCase).Value;

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
        [TestCategory("MissingAssert")]
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

            NormalAreaLoad loadWp = new NormalAreaLoad(-1/1000, s1, lcPressure, "WholeSurface");
            NormalAreaLoad punctualLoadWp1 = new NormalAreaLoad(-1/1000, loadShapeParallel, lcPressure2, "ConcentratedParallel");
            NormalAreaLoad punctualLoadWp2 = new NormalAreaLoad(-1/1000, loadShapeNotParallel, lcPointLoad, "ConcentratedNotParallel");

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);
            lgw.AddExternalFaceLoad(punctualLoadWp1);
            lgw.AddExternalFaceLoad(punctualLoadWp2);

            lgw.CalculateEquivalentThicknesses();


            double tw1 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == loadWp.GlassLoadCase).Value;
            double ts11 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == loadWp.GlassLoadCase).Value;
            double ts21 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == loadWp.GlassLoadCase).Value;

            double tw2 = lgw.ThicknessesW.FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase).Value;
            double ts12 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase).Value;
            double ts22 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key == punctualLoadWp1.GlassLoadCase).Value;


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
        [TestCategory("NonLinear")]
        [TestCategory("MissingAssert")]
        [TestCategory("Layers: 2")]
        public void LaminatedGlass7()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 500, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 8, GetGlassMaterialAstm());

            Interlayer intr1 = new Interlayer("Int1", 0.76, GetInterlayerMaterialPVBStiff());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, new Interlayer[] { intr1 });

            // Prototype
            Prototype p1 = new Prototype("p1", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.NonLinearStaticAnalysis,
                                Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                              new Prototype.LaminatedEqThicknessParameters(), null);

            p1.MeshOptions.MeshSize = 20;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcWp2 = new LoadCase("Wind2", 3, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            SelfWeightLoad swl = new SelfWeightLoad(lcSw, ModelAnalysisOptions.Instance.GetGravitySign() * GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);
            NormalAreaLoad nal2 = new NormalAreaLoad(-0.002, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(-0.003, s1, lcWp2);

            // Combinazioni
            Combination combo1 = new Combination("Cmb1");
            combo1.AddLoadCaseCoefficient(lcSw, 1);

            Combination combo2 = new Combination("Cmb2");
            combo2.AddLoadCaseCoefficient(lcSw, 1);
            combo2.AddLoadCaseCoefficient(lcWp, 0.6);

            Combination combo3 = new Combination("Cmb3");
            combo3.AddLoadCaseCoefficient(lcSw, 1);
            combo3.AddLoadCaseCoefficient(lcWp2, 0.6);


            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddLoad(swl);
            gs1.AddLoad(nal2);
            gs1.AddLoad(nal3);

            gs1.AddRestrains(s1.Fill.Explode().Select(i =>
                            new LineRestrain(i, new FreedomCase("fc1"), CoordinateSystem.Global,
                            new List<DofRestrain> { new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ) }))
                            .Cast<GeometryRestrain>().ToList());


            gs1.AddRestrain(new PointRestrain(s1.Fill[0], new FreedomCase("fc1"), new List<DofRestrain> {
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ),
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DX),
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DY)}));

            gs1.AddRestrain(new PointRestrain(s1.Fill[1], new FreedomCase("fc1"), new List<DofRestrain> {
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ),
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DY)}));


            model.AddCombination(combo1);
            model.AddCombination(combo2);
            model.AddCombination(combo3);

            // Model
            Assert.IsTrue(model.AddSurface(gs1), "Add Surface failed");
            Assert.IsTrue(model.FemModelsSetup(base.GetTestName()), "Fem model setup failed");

            model.PerformChecks();


        }
    }
}
