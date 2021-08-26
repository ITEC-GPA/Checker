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
using GPC.Model.Results;
using GPC.Model.FreedomCases;
using GPC.Utilities.Fem;
using System.Threading.Tasks;

namespace GlassTests
{
    [TestClass]
    public class ValidationTest : GlassTestBase
    {

        [TestMethod]
        [TestCategory("V-LG-LS1")]
        [TestCategory("Layers: 2")]
        public void LGLS1()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 2000, 0));
            //Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(100, 200, 0));

            MonolithicGlass mg4mm = new MonolithicGlass("Mg1", 4, GetGlassMaterialEn16612());
            MonolithicGlass mg6mm = new MonolithicGlass("Mg2", 6, GetGlassMaterialEn16612());


            Interlayer intrSentry = new Interlayer("Int2", 0.76, GetInterlayerMaterialSentryGlas());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg6mm, mg4mm }, new Interlayer[] { intrSentry });

            // Prototype
            Prototype p1 = new Prototype("p1", lg1, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement, 
                                        new Prototype.LaminatedEqThicknessParameters(), null);

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

            SelfWeightLoad loadSw = new SelfWeightLoad(lcSw, ModelAnalysisOptions.Instance.GetGravitySign() * GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);
            NormalAreaLoad loadWp = new NormalAreaLoad( - 1.2 / 1000, s1, lcWp);
            
            LineLoad loadLl = new LineLoad(ModelAnalysisOptions.Instance.GetGravityVector() * 0.8 * ModelAnalysisOptions.Instance.GetGravitySign(), 
                                          ModelAnalysisOptions.Instance.GetGravityVector() * 0 * ModelAnalysisOptions.Instance.GetGravitySign(),
                              new Line3d(new Point3d(0, 500, 0), new Point3d(1000, 500, 0)), lcLl, CoordinateSystem.Global);
            
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

            gs1.AddRestrains(s1.Fill.Explode().Select(i => 
                            new LineRestrain(i, new FreedomCase("fc1"), CoordinateSystem.Global, 
                            new List<DofRestrain> { new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ) }))
                            .Cast<GeometryRestrain>().ToList());


            gs1.AddRestrain(new PointRestrain(s1.Fill[0], new FreedomCase("fc1"), new List<DofRestrain> { 
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ), 
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DX), 
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DY)} ));

            gs1.AddRestrain(new PointRestrain(s1.Fill[1], new FreedomCase("fc1"), new List<DofRestrain> {
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ),
                                                                                  new DofRestrain(GPC.Model.FEM.Solver.DOF.DY)}));


            // Combo
            model.AddCombination(combo1);
            model.AddCombination(combo2);
            model.AddCombination(combo3);
            model.AddCombination(combo4);
            model.AddCombination(combo5);

            // Start analysis
            Assert.IsTrue(model.AddSurface(gs1), "Add Surface failed");
            Assert.IsTrue(model.FemModelsSetup(base.GetTestName()), "Fem model setup failed");


            model.PerformChecks();

            // Assert
            GPC.Model.FEM.Group[] groups = model.GlassSurfaces.FirstOrDefault().Checker.FemModel.GetGroups();

            // Assert - Layer 6 mm
            ResultStress[] worstStressesCmb1 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo1, groups[0].Name));
            ResultStress[] worstStressesCmb2 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo2, groups[0].Name));
            ResultStress[] worstStressesCmb3 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo3, groups[0].Name));
            ResultStress[] worstStressesCmb4 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo4, groups[0].Name));
            ResultStress[] worstStressesCmb5 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo5, groups[0].Name));
            
            AssertStressValue(worstStressesCmb1[0].S11, 3.330, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");
            AssertStressValue(worstStressesCmb2[0].S11, 6.370, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb2[0].Name}");
            AssertStressValue(worstStressesCmb3[0].S11, 4.760, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb3[0].Name}");
            AssertStressValue(worstStressesCmb4[0].S11, 15.48, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb4[0].Name}");
            AssertStressValue(worstStressesCmb5[0].S11, 13.32, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb5[0].Name}");


            // Assert - Layer 4 mm
            worstStressesCmb1 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo1, groups[2].Name));
            worstStressesCmb2 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo2, groups[2].Name));
            worstStressesCmb3 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo3, groups[2].Name));
            worstStressesCmb4 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo4, groups[2].Name));
            worstStressesCmb5 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo5, groups[2].Name));

            AssertStressValue(worstStressesCmb1[0].S11, 2.17, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");
            AssertStressValue(worstStressesCmb2[0].S11, 2.29, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb2[0].Name}"); // fallisce perchè lo stress non è mediato fra elementi, il 2.29 è preso da straus che fa la media fra elementi
            AssertStressValue(worstStressesCmb3[0].S11, 1.85, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb3[0].Name}");
            AssertStressValue(worstStressesCmb4[0].S11, 5.93, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb4[0].Name}");
            AssertStressValue(worstStressesCmb5[0].S11, 5.51, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb5[0].Name}");

        }


        #region EET


        [TestMethod]
        [TestCategory("V-EQT-EET1")]
        [TestCategory("Layers: 2")]
        [TestCategory("Uniform Pressure load")]
        public void EQTEET1()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 2000;
            double minorSide = 1000;
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
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            NormalAreaLoad loadWp = new NormalAreaLoad(-1, s1, lcPressure);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(7.16, tw, 0.1, tw.ToString());
            Assert.AreEqual(8.04, ts1, 0.1, ts1.ToString());
            Assert.AreEqual(8.04, ts2, 0.1, ts2.ToString());


        }




        [TestMethod]
        [TestCategory("V-EQT-EET2")]
        [TestCategory("Layers: 2")]
        [TestCategory("Uniform Pressure load")]
        public void EQTEET2()
        {

            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 800;
            double minorSide = 320;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET, Prototype.LaminatedEqThicknessBoundaryConditions.RectangularThreeSidesSimplySupported,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            NormalAreaLoad loadWp = new NormalAreaLoad(-1, s1, lcPressure);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            // vincolo su tre lati
            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());
            gs1.RemoveRestrain(gs1.GetRestrains().Where(i => ((Line3d)i.GetGeometry()).GetLength() > minorSide).FirstOrDefault());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();


            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(6.84, tw, 0.1, tw.ToString());
            Assert.AreEqual(7.70, ts1, 0.1, ts1.ToString());
            Assert.AreEqual(7.70, ts2, 0.1, ts2.ToString());



        }



        [TestMethod]
        [TestCategory("V-EQT-EET3")]
        [TestCategory("Layers: 2")]
        [TestCategory("Uniform Pressure load")]
        public void EQTEET3()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1500;
            double minorSide = 700;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET, Prototype.LaminatedEqThicknessBoundaryConditions.RectangularOneSideClamped,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            NormalAreaLoad loadWp = new NormalAreaLoad(-1, s1, lcPressure);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            // vincolo su un lati
            gs1.AddRestrain((GeometryRestrain)LineRestrain.GetAllFixed(
                             s1.Fill.Explode().Where(i => i.GetLength() < majorSide).FirstOrDefault(),
                             new FreedomCase("fc1"), CoordinateSystem.Global));



            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto


            Assert.AreEqual(9.39, tw, 0.1, tw.ToString());
            Assert.AreEqual(9.96, ts1, 0.1, ts1.ToString());
            Assert.AreEqual(9.96, ts2, 0.1, ts2.ToString());

        }



        [TestMethod]
        [TestCategory("V-EQT-EET4")]
        [TestCategory("Layers: 2")]
        [TestCategory("Punctual load")]
        public void EQTEET4()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1000;
            double minorSide = 800;
            double loadWidth = 300;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET, Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape = GetRectangularShape(new Point3d(minorSide / 2.0 - loadWidth / 2.0, majorSide / 2.0 - loadWidth / 2.0, 0), new Vector3d(loadWidth, loadWidth, 0));

            NormalAreaLoad loadWp = new NormalAreaLoad(-1, loadShape, lcPressure);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(6.76, tw, 0.1, tw.ToString());
            Assert.AreEqual(7.61, ts1, 0.1, ts1.ToString());
            Assert.AreEqual(7.61, ts2, 0.1, ts2.ToString());



        }




        [TestMethod]
        [TestCategory("V-EQT-EET5")]
        [TestCategory("Layers: 2")]
        [TestCategory("Punctual load")]
        public void EQTEET5()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1000;
            double minorSide = 800;
            double loadWidth = 50;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET, Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape = GetRectangularShape(new Point3d(minorSide / 2.0 - loadWidth / 2.0, majorSide / 2.0 - loadWidth / 2.0, 0), new Vector3d(loadWidth, loadWidth, 0));

            NormalAreaLoad loadWp = new NormalAreaLoad(-1, loadShape, lcPressure);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(6.74, tw, 0.1, tw.ToString());
            Assert.AreEqual(7.58, ts1, 0.1, ts1.ToString());
            Assert.AreEqual(7.58, ts2, 0.1, ts2.ToString());

        }



        [TestMethod]
        [TestCategory("V-EQT-EET6")]
        [TestCategory("Layers: 2")]
        [TestCategory("Punctual load")]
        [TestCategory("NormalAreaLoad")]
        public void EQTEET6_1()
        {
            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1000;
            double minorSide = 800;
            double loadWidth = 10;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET, Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape = GetRectangularShape(new Point3d(minorSide / 2.0 - loadWidth / 2.0, majorSide / 2.0 - loadWidth / 2.0, 0), new Vector3d(loadWidth, loadWidth, 0));

            NormalAreaLoad loadWp = new NormalAreaLoad(-1, loadShape, lcPressure);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(6.74, tw, 0.1, tw.ToString());
            Assert.AreEqual(7.58, ts1, 0.1, ts1.ToString());
            Assert.AreEqual(7.58, ts2, 0.1, ts2.ToString());

        }



        [TestMethod]
        [TestCategory("V-EQT-EET6")]
        [TestCategory("Layers: 2")]
        [TestCategory("Punctual load")]
        [TestCategory("PointLoad")]
        public void EQTEET6_2()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1000;
            double minorSide = 800;
            double loadWidth = 10;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET, Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape = GetRectangularShape(new Point3d(minorSide / 2.0 - loadWidth / 2.0, majorSide / 2.0 - loadWidth / 2.0, 0), new Vector3d(loadWidth, loadWidth, 0));

            PointLoad loadWp = new PointLoad(0, 0, -1, 0, 0, 0, new Point3d(minorSide / 2.0, majorSide / 2.0, 0), lcPressure);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.Checker = new GPC.Checkers.Glasses.Checkers.EN16612Checker(gs1, new ModelOptions()); // settato il checker in modo che non sia nullo

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(6.74, tw, 0.1, tw.ToString());
            Assert.AreEqual(7.58, ts1, 0.1, ts1.ToString());
            Assert.AreEqual(7.58, ts2, 0.1, ts2.ToString());

        }


        [TestMethod]
        [TestCategory("V-EQT-EET7")]
        [TestCategory("Layers: 2")]
        [TestCategory("Linear load")]
        [TestCategory("NormalAreaLoad")]
        public void EQTEET7_1()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1000;
            double minorSide = 800;
            double loadHeight = 300;
            double loadWidth = 10;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET, Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape = GetRectangularShape(new Point3d(0, loadHeight - loadWidth / 2.0, 0), new Vector3d(minorSide, loadWidth, 0));

            NormalAreaLoad loadWp = new NormalAreaLoad(-1, loadShape, lcPressure);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(6.80, tw, 0.1, tw.ToString());
            Assert.AreEqual(7.66, ts1, 0.1, ts1.ToString());
            Assert.AreEqual(7.66, ts2, 0.1, ts2.ToString());


        }


        [TestMethod]
        [TestCategory("V-EQT-EET7")]
        [TestCategory("Layers: 2")]
        [TestCategory("Linear load")]
        [TestCategory("LineLoad")]
        public void EQTEET7_2()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1000;
            double minorSide = 800;
            double loadHeight = 300;
            double loadWidth = 10;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET, Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape = GetRectangularShape(new Point3d(0, loadHeight - loadWidth / 2.0, 0), new Vector3d(minorSide, loadWidth, 0));

            LineLoad loadWp = new LineLoad(new Vector3d(0, 0, -1), new Vector3d(0, 0, 0), new Line3d(new Point3d(0, loadHeight, 0), new Point3d(minorSide, loadHeight, 0)), lcPressure, s1.GetCoordinateSystem());


            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.Checker = new GPC.Checkers.Glasses.Checkers.EN16612Checker(gs1, new ModelOptions()); // settato il checker in modo che non sia nullo

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(6.80, tw, 0.1, tw.ToString());
            Assert.AreEqual(7.66, ts1, 0.1, ts1.ToString());
            Assert.AreEqual(7.66, ts2, 0.1, ts2.ToString());

        }



        [TestMethod]
        [TestCategory("V-EQT-EET7")]
        [TestCategory("Layers: 2")]
        [TestCategory("Linear load")]
        [TestCategory("LineLoad")]
        [TestCategory("Numerical")]
        public void EQTEET7_3()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1000;
            double minorSide = 800;
            double loadHeight = 300;
            double loadWidth = 10;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(
                                            Prototype.LaminatedEqThicknessMethods.EET,
                                            Prototype.LaminatedEqThicknessBoundaryConditions.Other,
                                            majorSide,
                                            minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            //Shape loadShape = GetRectangularShape(new Point3d(0, loadHeight - loadWidth / 2.0, 0), new Vector3d(minorSide, loadWidth, 0));

            LineLoad loadWp = new LineLoad(new Vector3d(0, 0, -1), new Vector3d(0, 0, 0),
                              new Line3d(new Point3d(0, loadHeight, 0),
                              new Point3d(minorSide, loadHeight, 0)), lcPressure, s1.GetCoordinateSystem());


            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.Checker = new GPC.Checkers.Glasses.Checkers.EN16612Checker(gs1, new ModelOptions()); // settato il checker in modo che non sia nullo

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(6.80, tw, 0.2, tw.ToString());
            Assert.AreEqual(7.66, ts1, 0.2, ts1.ToString());
            Assert.AreEqual(7.66, ts2, 0.2, ts2.ToString());


        }



        [TestMethod]
        [TestCategory("V-EQT-EET7")]
        [TestCategory("Layers: 2")]
        [TestCategory("Linear load")]
        [TestCategory("AreaLoad")]
        public void EQTEET7_4()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1000;
            double minorSide = 800;
            double loadHeight = 300;
            double loadWidth = 10;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET, Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape = GetRectangularShape(new Point3d(0, loadHeight - loadWidth / 2.0, 0), new Vector3d(minorSide, loadWidth, 0));

            AreaLoad loadWp = new AreaLoad(-1, 1, 1, loadShape, lcPressure);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(6.80, tw, 0.2, tw.ToString());
            Assert.AreEqual(7.66, ts1, 0.2, ts1.ToString());
            Assert.AreEqual(7.66, ts2, 0.2, ts2.ToString());


        }


        [TestMethod]
        [TestCategory("V-EQT-EET8")]
        [TestCategory("Layers: 2")]
        [TestCategory("Linear load")]
        public void EQTEET8()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 1000;
            double minorSide = 800;
            double loadHeight = 500;
            double loadWidth = 300;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 5, GetGlassMaterialEn16612());

            Interlayer[] interlayers = new Interlayer[] { new Interlayer("int1", 0.76, GetInterlayerMaterial()) };

            LaminatedGlass lg = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2 }, interlayers);

            // Prototype
            Prototype p1 = new Prototype("p1", lg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(Prototype.LaminatedEqThicknessMethods.EET, Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported,
                                        majorSide, minorSide), null);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            Shape loadShape = GetRectangularShape(new Point3d(0, loadHeight - loadWidth / 2.0, 0), new Vector3d(minorSide, loadWidth, 0));

            NormalAreaLoad loadWp = new NormalAreaLoad(-1, loadShape, lcPressure);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());


            GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper lgw = new GPC.Checkers.Glasses.Wrappers.LaminatedGlassWrapper(gs1, lg);
            lgw.AddExternalFaceLoad(loadWp);

            lgw.CalculateEquivalentThicknesses();

            double tw = lgw.ThicknessesW.FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts1 = lgw.ThicknessesStress[0].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;
            double ts2 = lgw.ThicknessesStress[1].FirstOrDefault(i => i.Key.EqualsParameters(loadWp.GlassLoadCase.LoadDuration, loadWp.GlassLoadCase.Temperature, loadWp.GetGeometry(), loadWp.LoadRestrainCondition)).Value;

            double shearModule = interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature);

            Console.WriteLine($"G interlayer: {interlayers[0].Material.GetShearModule(lcPressure.LoadDuration, lcPressure.Temperature)}");
            Console.WriteLine($"Tw: {tw}");
            Console.WriteLine($"Ts1: {ts1}");
            Console.WriteLine($"Ts2: {ts2}");

            Assert.AreEqual(0.23333, shearModule, 0.001, shearModule.ToString()); // valore di G su cui sono tarati gli expected value sotto

            Assert.AreEqual(6.76, tw, 0.1, tw.ToString());
            Assert.AreEqual(7.61, ts1, 0.1, ts1.ToString());
            Assert.AreEqual(7.61, ts2, 0.1, ts2.ToString());

        } 
        #endregion


        #region BAM

        [TestMethod]
        [TestCategory("V-BAM-DGU1")]
        [TestCategory("Uniform pressure")]
        public void BAMDGU1()
        {

            Model model = new Model(base.GetOutputFolder());

            double majorSide = 2500;
            double minorSide = 1000;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 10, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 10, GetGlassMaterialEn16612());
            AirChamber airChamber = new AirChamber("Ac", 15);

            DoubleInsulatingGlass dgu = new DoubleInsulatingGlass("Dgu", mg1, mg2, airChamber);

            // Prototype
            Prototype p1 = new Prototype("p1", dgu, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(), null);

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            NormalAreaLoad loadWp = new NormalAreaLoad(3, s1, lcPressure, "wp1", GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);


            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            var loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { loadWp }, p1.Standard, false);


            Assert.AreEqual(-1.5, loadSharing[0].Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.001);
            Assert.AreEqual(1.5, loadSharing[1].Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.001);
        }


        #endregion
    }
}
