using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Models;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Combinations;
using GPC.Model.Glasses;
using GPC.Model.Loads;
using GPC.Checkers.Glasses.Loads;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using GPC.TestUtilities;
using GlassTests;
using GPC.Model.Materials;
using GPC.Model.Restrains;
using GPC.Model.FreedomCases;
using System.Collections.Generic;

namespace GlassTests
{
    [TestClass]
    public class LaminatedGlassTest : GlassTestBase
    {



        [TestMethod]
        [TestCategory("Linear")]
        [TestCategory("MissingAssert")]
        [TestCategory("Layers: 5")]
        public void LaminatedGlass1()
        {
            RunApiServer();

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

            model.PerformChecks();

        }


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

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 20;
            meshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lcCSD = new LoadCase("Climate", 12 * 60 * 60, 10, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.ClimateSummerDeltaH);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            NormalAreaLoad nal1 = new NormalAreaLoad(1, s1, lcCSD);
            NormalAreaLoad nal2 = new NormalAreaLoad(2, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(3, s1, lcLl);
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            LineLoad lll = new LineLoad(model.Options.GetGravityVector() * 1, model.Options.GetGravityVector() * 0, new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

            // Combinazioni
            CombinationEn combo1 = new CombinationEn("Cmb1", StandardEN1990.LimitStates.UltimateStructural);
            combo1.AddLoadCaseCoefficient(lcSw, 1);
            combo1.AddLoadCaseCoefficient(lcLl, 1);

            CombinationEn combo2 = new CombinationEn("Cmb2", StandardEN1990.LimitStates.UltimateStructural);
            combo2.AddLoadCaseCoefficient(lcSw, 1);
            combo2.AddLoadCaseCoefficient(lcWp, 0.6);

            CombinationEn combo3 = new CombinationEn("Cmb3", StandardEN1990.LimitStates.UltimateStructural);
            combo3.AddLoadCaseCoefficient(lcSw, 1);
            combo3.AddLoadCaseCoefficient(lcLl, 0.75);
            combo3.AddLoadCaseCoefficient(lcWp, 0.4);

            CombinationEn combo4 = new CombinationEn("Cmb4", StandardEN1990.LimitStates.UltimateStructural);
            combo4.AddLoadCaseCoefficient(lcSw, 1);
            combo4.AddLoadCaseCoefficient(lcCSD, 0.75);
            combo4.AddLoadCaseCoefficient(lcLl, 0.75);
            combo4.AddLoadCaseCoefficient(lcWp, 0.4);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
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
            model.AddSurface(gs1);

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

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 20;
            meshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lcCs = new LoadCase("Climate", 12 * 60 * 60, 10, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.ClimateSummerDeltaT);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            NormalAreaLoad nal1 = new NormalAreaLoad(1, s1, lcCs);
            NormalAreaLoad nal2 = new NormalAreaLoad(2, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(3, s1, lcLl);
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            LineLoad lll = new LineLoad(model.Options.GetGravityVector() * 1, model.Options.GetGravityVector() * 0, new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

            // Combinazioni
            CombinationEn combo1 = new CombinationEn("Cmb1", StandardEN1990.LimitStates.UltimateStructural);
            combo1.AddLoadCaseCoefficient(lcSw, 1);
            combo1.AddLoadCaseCoefficient(lcLl, 1);

            CombinationEn combo5 = new CombinationEn("Cmb5", StandardEN1990.LimitStates.UltimateStructural);
            combo5.AddLoadCaseCoefficient(lcSw, 1.5);
            combo5.AddLoadCaseCoefficient(lcWp, 0.6);

            CombinationEn combo2 = new CombinationEn("Cmb2", StandardEN1990.LimitStates.UltimateStructural);
            combo2.AddLoadCaseCoefficient(lcSw, 1);
            combo2.AddLoadCaseCoefficient(lcWp, 0.6);

            CombinationEn combo3 = new CombinationEn("Cmb3", StandardEN1990.LimitStates.UltimateStructural);
            combo3.AddLoadCaseCoefficient(lcSw, 1);
            combo3.AddLoadCaseCoefficient(lcLl, 0.75);
            combo3.AddLoadCaseCoefficient(lcWp, 0.4);

            CombinationEn combo4 = new CombinationEn("Cmb4", StandardEN1990.LimitStates.UltimateStructural);
            combo4.AddLoadCaseCoefficient(lcSw, 1);
            combo4.AddLoadCaseCoefficient(lcCs, 0.75);
            combo4.AddLoadCaseCoefficient(lcLl, 0.75);
            combo4.AddLoadCaseCoefficient(lcWp, 0.4);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
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
            model.AddSurface(gs1);


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

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 20;
            meshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lcCs = new LoadCase("Climate", 12 * 60 * 60, 10, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.ClimateSummerDeltaT);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            NormalAreaLoad nal1 = new NormalAreaLoad(1, s1, lcCs);
            NormalAreaLoad nal2 = new NormalAreaLoad(2, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(3, s1, lcLl);

            LineLoad lll = new LineLoad(model.Options.GetGravityVector() * 1, model.Options.GetGravityVector() * 0, new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

            // Combinazioni
            CombinationEn combo1 = new CombinationEn("Cmb1", StandardEN1990.LimitStates.UltimateStructural);
            combo1.AddLoadCaseCoefficient(lcSw, 1);
            combo1.AddLoadCaseCoefficient(lcLl, 1);

            CombinationEn combo5 = new CombinationEn("Cmb5", StandardEN1990.LimitStates.UltimateStructural);
            combo5.AddLoadCaseCoefficient(lcSw, 1.5);
            combo5.AddLoadCaseCoefficient(lcWp, 0.6);

            CombinationEn combo2 = new CombinationEn("Cmb2", StandardEN1990.LimitStates.UltimateStructural);
            combo2.AddLoadCaseCoefficient(lcSw, 1);
            combo2.AddLoadCaseCoefficient(lcWp, 0.6);

            CombinationEn combo3 = new CombinationEn("Cmb3", StandardEN1990.LimitStates.UltimateStructural);
            combo3.AddLoadCaseCoefficient(lcSw, 1);
            combo3.AddLoadCaseCoefficient(lcLl, 0.75);
            combo3.AddLoadCaseCoefficient(lcWp, 0.4);

            CombinationEn combo4 = new CombinationEn("Cmb4", StandardEN1990.LimitStates.UltimateStructural);
            combo4.AddLoadCaseCoefficient(lcSw, 1);
            combo4.AddLoadCaseCoefficient(lcCs, 0.75);
            combo4.AddLoadCaseCoefficient(lcLl, 0.75);
            combo4.AddLoadCaseCoefficient(lcWp, 0.4);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
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
            model.AddSurface(gs1);


            model.PerformChecks();

        }



        [TestMethod]
        [TestCategory("Linear")]
        [TestCategory("MissingAssert")]
        [TestCategory("Layer:2")]
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

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 20;
            meshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lcCSD = new LoadCase("Climate", 12 * 60 * 60, 10, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.ClimateSummerDeltaH);
            LoadCase lcWp = new LoadCase("Wind", 3, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            NormalAreaLoad nal1 = new NormalAreaLoad(1, s1, lcCSD);
            NormalAreaLoad nal2 = new NormalAreaLoad(2, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(3, s1, lcLl);
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            LineLoad lll = new LineLoad(model.Options.GetGravityVector() * 1, model.Options.GetGravityVector() * 0, new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

            // Combinazioni
            CombinationEn combo1 = new CombinationEn("Cmb1", StandardEN1990.LimitStates.UltimateStructural);
            combo1.AddLoadCaseCoefficient(lcSw, 1);
            combo1.AddLoadCaseCoefficient(lcLl, 1);

            CombinationEn combo2 = new CombinationEn("Cmb2", StandardEN1990.LimitStates.UltimateStructural);
            combo2.AddLoadCaseCoefficient(lcSw, 1);
            combo2.AddLoadCaseCoefficient(lcWp, 0.6);

            CombinationEn combo3 = new CombinationEn("Cmb3", StandardEN1990.LimitStates.UltimateStructural);
            combo3.AddLoadCaseCoefficient(lcSw, 1);
            combo3.AddLoadCaseCoefficient(lcLl, 0.75);
            combo3.AddLoadCaseCoefficient(lcWp, 0.4);

            CombinationEn combo4 = new CombinationEn("Cmb4", StandardEN1990.LimitStates.UltimateStructural);
            combo4.AddLoadCaseCoefficient(lcSw, 1);
            combo4.AddLoadCaseCoefficient(lcCSD, 0.75);
            combo4.AddLoadCaseCoefficient(lcLl, 0.75);
            combo4.AddLoadCaseCoefficient(lcWp, 0.4);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
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
            model.AddSurface(gs1);

            model.PerformChecks();

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

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 20;
            meshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", GPC.Utilities.Constants.Constants.SECONDSINAYEAR * 50      , 50,   GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lcCs = new LoadCase("Climate", GPC.Utilities.Constants.Constants.SECONDSINADAY / 2.0 , 10,   GPC.Model.LoadCases.LoadCase.LoadCaseTypes.ClimateSummerDeltaT);
            LoadCase lcWp = new LoadCase("Wind", 3,         40,                 GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", 5 * 60,    30,                 GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            List<LoadCase> loadCases = new List<LoadCase>();
            loadCases.Add(lcSw);
            loadCases.Add(lcCs);
            loadCases.Add(lcWp);
            loadCases.Add(lcLl);


            NormalAreaLoad nal1 = new NormalAreaLoad(1, s1, lcCs);
            NormalAreaLoad nal2 = new NormalAreaLoad(2, s1, lcWp);
            NormalAreaLoad nal3 = new NormalAreaLoad(3, s1, lcLl);
            SelfWeightLoad swl = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            LineLoad lll = new LineLoad(model.Options.GetGravityVector() * 1, model.Options.GetGravityVector() * 0, new Line3d(new Point3d(40, 450, 0), new Point3d(150, 200, 0)), lcLl, CoordinateSystem.Global);

            // Combinazioni
            CombinationEn combo1 = new CombinationEn("Cmb1", StandardEN1990.LimitStates.UltimateStructural);
            combo1.AddLoadCaseCoefficient(lcSw, 1);
            combo1.AddLoadCaseCoefficient(lcLl, 1);

            CombinationEn combo2 = new CombinationEn("Cmb2", StandardEN1990.LimitStates.UltimateStructural);
            combo2.AddLoadCaseCoefficient(lcSw, 1);
            combo2.AddLoadCaseCoefficient(lcWp, 0.6);

            CombinationEn combo3 = new CombinationEn("Cmb3", StandardEN1990.LimitStates.UltimateStructural);
            combo3.AddLoadCaseCoefficient(lcSw, 1);
            combo3.AddLoadCaseCoefficient(lcLl, 0.75);
            combo3.AddLoadCaseCoefficient(lcWp, 0.4);

            CombinationEn combo4 = new CombinationEn("Cmb4", StandardEN1990.LimitStates.UltimateStructural);
            combo4.AddLoadCaseCoefficient(lcSw, 1);
            combo4.AddLoadCaseCoefficient(lcCs, 0.75);
            combo4.AddLoadCaseCoefficient(lcLl, 0.75);
            combo4.AddLoadCaseCoefficient(lcWp, 0.4);

            CombinationEn combo5 = new CombinationEn("Cmb5", StandardEN1990.LimitStates.UltimateStructural);
            combo5.AddLoadCaseCoefficient(lcSw, 1.5);
            combo5.AddLoadCaseCoefficient(lcWp, 0.6);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
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
            model.AddSurface(gs1);


            model.PerformChecks();

            loadCases.ForEach(i => Console.WriteLine($"LC: {i.Name} \t\t Duration: {i.LoadDuration} \t\t Temperature: {i.Temperature} " +
                $"\t\t GPvb: {intrPvb.Material.GetShearModule(i.LoadDuration, i.Temperature).ToString("F3")} \t GSentry: {intrSentry.Material.GetShearModule(i.LoadDuration, i.Temperature).ToString("F3")}"));

        }

    }
}
