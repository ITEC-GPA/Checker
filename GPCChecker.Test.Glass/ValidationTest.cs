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
            NormalAreaLoad loadWp = new NormalAreaLoad( - 1.2 / 1000, s1, lcWp);
            LineLoad loadLl = new LineLoad(model.Options.GetGravityVector() * 0.8, model.Options.GetGravityVector() * 0, new Line3d(new Point3d(0, 500, 0), new Point3d(1000, 500, 0)), lcLl, CoordinateSystem.Global);
            //LineLoad loadLl = new LineLoad(model.Options.GetGravityVector() * 0.8, model.Options.GetGravityVector() * 0, new Line3d(new Point3d(0, 50, 0), new Point3d(200, 50, 0)), lcLl, CoordinateSystem.Global);

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
                            new LineRestrain(i, new FreedomCase("fc1"), CoordinateSystem.Global, new List<DofRestrain> { new DofRestrain(GPC.Model.FEM.Solver.DOF.DZ) }))
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
            Assert.IsTrue(model.AddSurface(gs1, base.GetTestName()), "Add Surface failed");

            model.PerformChecks();

            //GPC.Utilities.Serialization.Serialization.SerializeToBinaryFile(base.GetFilePathInOutputFolder("model", "obj"), model.GlassSurfaces.FirstOrDefault().Checker.FemModel);
            


            // Assert
            GPC.Model.FEM.Group[] groups = model.GlassSurfaces.FirstOrDefault().Checker.FemModel.GetGroups();

            // Assert - Layer 6 mm
            ResultStress[] worstStressesCmb1 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo1, groups[0].Name));
            ResultStress[] worstStressesCmb2 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo2, groups[0].Name));
            ResultStress[] worstStressesCmb3 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo3, groups[0].Name));
            ResultStress[] worstStressesCmb4 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo4, groups[0].Name));
            ResultStress[] worstStressesCmb5 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo5, groups[0].Name));
            
            AssertStressValue(worstStressesCmb1[0].S11, 3.330, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");
            AssertStressValue(worstStressesCmb2[0].S11, 6.370, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");
            AssertStressValue(worstStressesCmb3[0].S11, 4.760, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");
            AssertStressValue(worstStressesCmb4[0].S11, 15.48, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");
            AssertStressValue(worstStressesCmb5[0].S11, 13.32, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");


            // Assert - Layer 4 mm
            worstStressesCmb1 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo1, groups[2].Name));
            worstStressesCmb2 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo2, groups[2].Name));
            worstStressesCmb3 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo3, groups[2].Name));
            worstStressesCmb4 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo4, groups[2].Name));
            worstStressesCmb5 = GetWorstStressResults(model.GlassSurfaces.FirstOrDefault().Checker.GetCombinationPlateStressResult(combo5, groups[2].Name));


            AssertStressValue(worstStressesCmb1[0].S11, 2.17, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");
            AssertStressValue(worstStressesCmb2[0].S11, 2.29, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");
            AssertStressValue(worstStressesCmb3[0].S11, 1.85, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");
            AssertStressValue(worstStressesCmb4[0].S11, 5.93, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");
            AssertStressValue(worstStressesCmb5[0].S11, 5.51, 1, $"{GetTestName()} st7PlateId: {worstStressesCmb1[0].Name}");

        }



        [TestMethod]
        [TestCategory("V-EQT-EET1")]
        [TestCategory("Layers: 2")]
        public void EQTEET1()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 2000, 0));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 1, GetGlassMaterialEn16612());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.EET, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            p1.MeshOptions.MeshSize = 25;
            p1.MeshOptions.Algorithm = Mesh.GenerateOptions.MeshAlgorithm.PackingOfParallelograms;

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 1, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            List<GPC.Model.LoadCases.LoadCaseBase> loadCases = new List<GPC.Model.LoadCases.LoadCaseBase>
            {
                lcPressure
            };

            NormalAreaLoad loadWp = new NormalAreaLoad(-1, s1, lcPressure);

            // Combinazioni
            Combination combo1 = new Combination("Cmb1");
            combo1.AddLoadCaseCoefficient(lcPressure, 1);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            gs1.AddLoad(loadWp);

            gs1.AddRestrains(s1.Fill.Explode().Select(i => (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());

            // Combo
            model.AddCombination(combo1);

            // Start analysis
            Assert.IsTrue(model.AddSurface(gs1, base.GetTestName()), "Add Surface failed");

            model.PerformChecks();

            // Assert
            GPC.Model.FEM.Group[] groups = model.GlassSurfaces.FirstOrDefault().Checker.FemModel.GetGroups();

            GPC.Model.FEM.Node[] nodes = model.GlassSurfaces.FirstOrDefault().Checker.FemModel.GetNodes();
            GPC.Model.FEM.FiniteElements.FiniteElement[] elements = model.GlassSurfaces.FirstOrDefault().Checker.FemModel.GetElements();


            Dictionary<int, (double D3, double R1, double R2)> displacementMap = new Dictionary<int, (double D3, double R1, double R2)>();


            GPC.Model.FEM.GaussIntegration.GaussPoint[] gaussPointsQuad = GPC.Model.FEM.GaussIntegration.GetPointsRectangular(4);
            GPC.Model.FEM.GaussIntegration.GaussPoint[] gaussPointsTri = GPC.Model.FEM.GaussIntegration.GetPointsRectangular(3);


            double num = 0;
            double den = 0;
            foreach (var element in elements)
            {
                IEnumerable<ResultDisplacement> resultDisplacement = element.Nodes.Select(i => i.Results.FirstOrDefault().Result).Cast<ResultDisplacement>();

                var mean = ResultDisplacement.GetArithmeticMean(resultDisplacement.ToArray());

                num += Math.Abs(mean.D3) * 25 * 25;

                den += (mean.R1 * mean.R1 + mean.R2 * mean.R2) * 25 * 25;
            }

            Console.WriteLine($"Num: {num}");
            Console.WriteLine($"Den: {den}");
            Console.WriteLine($"Num/Den: {num / den}");

            Assert.AreEqual(12.48, num / den, 1.0, (num / den).ToString());



            //foreach (var node in nodes)
            //{
            //    var p = new Point3d(node.Position.X, node.Position.Y, 0);
            //    displacementMap.Add(p.GetHashCode(), (((ResultDisplacement)node.Results.FirstOrDefault().Result).D3,
            //                                                     -((ResultDisplacement)node.Results.FirstOrDefault().Result).R2,
            //                                                     -((ResultDisplacement)node.Results.FirstOrDefault().Result).R1
            //                                                     ));

            //}

            //Func<double, double, double> fDisp = (x, y) =>
            //{
            //    var p = new Point3d(x, y, 0);
            //    return displacementMap[p.GetHashCode()].D3;
            //};

            //Func<double, double, double> fDispDxDySquare = (x, y) =>
            //{
            //    var p = new Point3d(x, y, 0);
            //    return  Math.Pow(displacementMap[p.GetHashCode()].R2, 2) + Math.Pow(displacementMap[p.GetHashCode()].R1, 2);
            //};

            //var num = MathNet.Numerics.Integration.GaussLegendreRule.Integrate(fDisp, 0, 1000, 0, 2000, 1);
            //var den = MathNet.Numerics.Integration.GaussLegendreRule.Integrate(fDispDxDySquare, 0, 1000, 0, 2000, 1);

        }

    }
}
