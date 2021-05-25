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
    public class ValidationTest : GlassTestBase
    {

        [TestMethod]
        [TestCategory("V-MG-LS1")]
        [TestCategory("Layers: 2")]
        public void MGLS1()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 2000, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 4, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 6, GetGlassMaterialEn16612());


            Interlayer intrSentry = new Interlayer("Int2", 0.76, GetInterlayerMaterialSentryGlas());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, new Interlayer[] { intrSentry });

            // Prototype
            Prototype p1 = new Prototype("p1", lg1, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.EET, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            p1.MeshOptions.MeshSize = 50;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcSw = new LoadCase("Sw", EN16612LoadDurations.SELFWEIGHT, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lcWp = new LoadCase("Wind", EN16612LoadDurations.WIND, 40, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcLl = new LoadCase("Live", EN16612LoadDurations.LIVECROWD, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            List<GPC.Model.LoadCases.LoadCaseBase> loadCases = new List<GPC.Model.LoadCases.LoadCaseBase>
            {
                lcSw,
                lcWp,
                lcLl
            };

            SelfWeightLoad loadSw = new SelfWeightLoad(lcSw, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);
            NormalAreaLoad loadWp = new NormalAreaLoad(-1.2/1000, s1, lcWp);
            LineLoad loadLl = new LineLoad(model.Options.GetGravityVector() * 0.8, model.Options.GetGravityVector() * 0, new Line3d(new Point3d(0, 500, 0), new Point3d(1000, 500, 0)), lcLl, CoordinateSystem.Global);
            
            // Combinazioni
            Combination combo1 = new Combination("Cmb1");
            combo1.AddLoadCaseCoefficient(lcSw, 1);

            Combination combo2 = new Combination("Cmb2");
            combo2.AddLoadCaseCoefficient(lcWp, 1);

            Combination combo3 = new Combination("Cmb3");
            combo3.AddLoadCaseCoefficient(lcLl, 1);

            Combination combo4 = new Combination("Cmb4");
            combo4.AddLoadCaseCoefficient(lcSw, 1.3);
            combo4.AddLoadCaseCoefficient(lcWp, 1.5);
            combo4.AddLoadCaseCoefficient(lcLl, 0.7);

            Combination combo5 = new Combination("Cmb5");
            combo5.AddLoadCaseCoefficient(lcSw, 1.3);
            combo5.AddLoadCaseCoefficient(lcWp, 0.7);
            combo5.AddLoadCaseCoefficient(lcLl, 1.5);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddLoad(loadSw);
            gs1.AddLoad(loadWp);
            gs1.AddLoad(loadLl);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());

            model.AddCombination(combo1);
            model.AddCombination(combo2);
            model.AddCombination(combo3);
            model.AddCombination(combo4);
            model.AddCombination(combo5);

            // Model
            Assert.IsTrue(model.AddSurface(gs1, base.GetTestName()), "Add Surface failed");


            model.PerformChecks();

        }

    }
}
