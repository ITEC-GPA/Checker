using GPC.Checker.Glasses.Glasses;
using GPC.Checker.Glasses.LoadCases;
using GPC.Checker.Glasses.Models;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Combinations;
using GPC.Model.Glasses;
using GPC.Model.Loads;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using GPC.TestUtilities;
using GlassTests;
using GPC.Model.Materials;
using GPC.Model.Restrains;
using GPC.Model.FreedomCases;

namespace GlassTests
{
    [TestClass]
    public class LaminatedGlassTest : GlassTestBase
    {



        [TestMethod]
        [TestCategory("Linear")]
        [TestCategory("MissingAssert")]
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

            model.FemModelSetup();
            model.PerformChecks();

        }


        [TestMethod]
        [TestCategory("Linear")]
        [TestCategory("MissingAssert")]
        public void LaminatedGlass2()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 500));
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
            LoadCase lc0 = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseType.SelfWeight);
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
        [TestCategory("NonLinear")]
        [TestCategory("MissingAssert")]
        public void LaminatedGlass3()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 500));
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
            LoadCase lc0 = new LoadCase("Sw", 50 * 24 * 60 * 60, 50, GPC.Model.LoadCases.LoadCase.LoadCaseType.SelfWeight);
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
    }
}
