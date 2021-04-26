using GPC.Checker.Glasses.Glasses;
using GPC.Checker.Glasses.LoadCases;
using GPC.Checker.Glasses.Models;
using GPC.Checker.Glasses.Restrain;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Combinations;
using GPC.Model.FEM;
using GPC.Model.FreedomCases;
using GPC.Model.Glasses;
using GPC.Model.Loads;
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
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(LinearSolver.DOF.DX, true) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                                                    Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
            gs1.AddRestrains(geometryRestrains1);

            model.AddSurface(gs1);

            // LoadCases
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.001, s1, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);

            // Combination
            Combination cmb1 = new CombinationEn("CMB1", StandardEN1990.LimitStates.UltimateStructural);
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new CombinationEn("CMB2", StandardEN1990.LimitStates.ServiceabilityCharacteristic);
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            model.FemModelSetup();
            model.PerformChecks();

            var stressResults = model.GetPlateCombinationsResult();
            var deflectionResults = model.GetNodeDisplacementCombinationsResult();

            Assert.AreEqual(1, stressResults.Count, 0);
            Assert.AreEqual(1, deflectionResults.Count, 0);
            Assert.IsTrue(stressResults[0].Count > 0);
            Assert.IsTrue(deflectionResults[0].Count > 0);

            ResultPlateStress worstPlateResult = null;
            foreach (var comboResult in stressResults.First())
            {
                comboResult.GetPrincipalStress(out double s11, out double s22);

                if (worstPlateResult is null)
                    worstPlateResult = comboResult;
                else if (s11 > worstPlateResult.S11)
                    worstPlateResult = comboResult;
            }

            ResultNodeDisplacement worstNodeDisplacement = null;
            foreach (var comboResult in deflectionResults.First())
            {
                double disp = comboResult.GetResultingDisplacement();

                if (worstNodeDisplacement is null)
                    worstNodeDisplacement = comboResult;
                else if (Math.Abs(disp) > Math.Abs(worstNodeDisplacement.GetResultingDisplacement()))
                    worstNodeDisplacement = comboResult;
            }

            Console.WriteLine($"STRESS");
            Console.WriteLine($"\t Stress11: {worstPlateResult.S11}, stress22: {worstPlateResult.S22}, stress33: {worstPlateResult.S33}");
            Console.WriteLine($"\t Id: {worstPlateResult.Element.Id} Point: {(worstPlateResult.ResultPoint as ResultStressPoint).Location} Node0 Id: {worstPlateResult.Element.Nodes[0].Position}");

            Console.WriteLine($"DEFLECTION");
            Console.WriteLine($"\t WorstDeflection: {worstNodeDisplacement.GetResultingDisplacement()}");
            Console.WriteLine($"\t Id: {worstNodeDisplacement.Element.Id} Point: {worstNodeDisplacement.Element.Position} D1: {worstNodeDisplacement.D1} D2: {worstNodeDisplacement.D2} D3: {worstNodeDisplacement.D3} ");

            Assert.AreEqual(28.71, worstPlateResult.S11, 1);
            Assert.AreEqual(5.78, worstNodeDisplacement.D3, 0.2);
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
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(LinearSolver.DOF.DX, true) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));
            List<GeometryRestrain> geometryRestrains2 = new List<GeometryRestrain>();
            geometryRestrains2.AddRange(s2.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                                                Prototype.CheckMethods.DominantLoad, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
            GlassSurface gs2 = new GlassSurface(p1, s2, meshOptions);
            gs1.AddRestrains(geometryRestrains1);
            gs2.AddRestrains(geometryRestrains2);

            model.AddSurface(gs1);
            model.AddSurface(gs2);

            // LoadCases
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

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
            Combination cmb1 = new CombinationEn("CMB1", StandardEN1990.LimitStates.UltimateStructural);
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new CombinationEn("CMB2", StandardEN1990.LimitStates.ServiceabilityCharacteristic);
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            model.FemModelSetup();
            model.PerformChecks();

            var stressResults = model.GetPlateCombinationsResult();
            var deflectionResults = model.GetNodeDisplacementCombinationsResult();

            Assert.AreEqual(2, stressResults.Count, 0);
            Assert.AreEqual(2, deflectionResults.Count, 0);
            Assert.IsTrue(stressResults[0].Count > 0);
            Assert.IsTrue(deflectionResults[0].Count > 0);

            ResultPlateStress worstPlateResult1 = null;
            ResultPlateStress worstPlateResult2 = null;
            foreach (var comboResult in stressResults.First())
            {
                comboResult.GetPrincipalStress(out double s11, out double s22);

                if (worstPlateResult1 is null)
                    worstPlateResult1 = comboResult;
                else if (s11 > worstPlateResult1.S11)
                    worstPlateResult1 = comboResult;
            }
            foreach (var comboResult in stressResults.First())
            {
                comboResult.GetPrincipalStress(out double s11, out double s22);

                if (worstPlateResult2 is null)
                    worstPlateResult2 = comboResult;
                else if (s11 > worstPlateResult2.S11)
                    worstPlateResult2 = comboResult;
            }

            ResultNodeDisplacement worstNodeDisplacement1 = null;
            ResultNodeDisplacement worstNodeDisplacement2 = null;
            foreach (var comboResult in deflectionResults.First())
            {
                double disp = comboResult.GetResultingDisplacement();

                if (worstNodeDisplacement1 is null)
                    worstNodeDisplacement1 = comboResult;
                else if (Math.Abs(disp) > Math.Abs(worstNodeDisplacement1.GetResultingDisplacement()))
                    worstNodeDisplacement1 = comboResult;
            }
            foreach (var comboResult in deflectionResults.First())
            {
                double disp = comboResult.GetResultingDisplacement();

                if (worstNodeDisplacement2 is null)
                    worstNodeDisplacement2 = comboResult;
                else if (Math.Abs(disp) > Math.Abs(worstNodeDisplacement2.GetResultingDisplacement()))
                    worstNodeDisplacement2 = comboResult;
            }

            Console.WriteLine($"STRESS");
            Console.WriteLine($"\t Stress11: {worstPlateResult1.S11}, stress22: {worstPlateResult1.S22}, stress33: {worstPlateResult1.S33}");
            Console.WriteLine($"\t Id: {worstPlateResult1.Element.Id} Point: {(worstPlateResult1.ResultPoint as ResultStressPoint).Location} Node0 Id: {worstPlateResult1.Element.Nodes[0].Position}");

            Console.WriteLine($"DEFLECTION");
            Console.WriteLine($"\t WorstDeflection: {worstNodeDisplacement1.GetResultingDisplacement()}");
            Console.WriteLine($"\t Id: {worstNodeDisplacement1.Element.Id} Point: {worstNodeDisplacement1.Element.Position} D1: {worstNodeDisplacement1.D1} D2: {worstNodeDisplacement1.D2} D3: {worstNodeDisplacement1.D3} ");

            Assert.AreEqual(28.71, worstPlateResult1.S11, 1);
            Assert.AreEqual(5.78, worstNodeDisplacement1.D3, 0.2);

            Assert.AreEqual(28.71, worstPlateResult2.S11, 1);
            Assert.AreEqual(5.78, worstNodeDisplacement2.D3, 0.2);
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
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(LinearSolver.DOF.DX, true) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.NonLinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                                                    Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
            gs1.AddRestrains(geometryRestrains1);

            model.AddSurface(gs1);

            // LoadCases
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.001, s1, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);

            // Combination
            Combination cmb1 = new CombinationEn("CMB1", StandardEN1990.LimitStates.UltimateStructural);
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new CombinationEn("CMB2", StandardEN1990.LimitStates.ServiceabilityCharacteristic);
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            Console.WriteLine($"{cmb1.Name}: {cmb1}");
            Console.WriteLine($"{cmb2.Name}: {cmb2}");

            model.FemModelSetup();
            model.PerformChecks();

            var stressResults = model.GetPlateCombinationsResult();
            var deflectionResults = model.GetNodeDisplacementCombinationsResult();

            Assert.AreEqual(1, stressResults.Count, 0);
            Assert.AreEqual(1, deflectionResults.Count, 0);
            Assert.IsTrue(stressResults[0].Count > 0);
            Assert.IsTrue(deflectionResults[0].Count > 0);

            ResultPlateStress worstPlateResult = null;
            foreach (var comboResult in stressResults.First())
            {
                comboResult.GetPrincipalStress(out double s11, out double s22);

                if (worstPlateResult is null)
                    worstPlateResult = comboResult;
                else if (s11 > worstPlateResult.S11)
                    worstPlateResult = comboResult;
            }

            ResultNodeDisplacement worstNodeDisplacement = null;
            foreach (var comboResult in deflectionResults.First())
            {
                double disp = comboResult.GetResultingDisplacement();

                if (worstNodeDisplacement is null)
                    worstNodeDisplacement = comboResult;
                else if (Math.Abs(disp) > Math.Abs(worstNodeDisplacement.GetResultingDisplacement()))
                    worstNodeDisplacement = comboResult;
            }

            Console.WriteLine($"STRESS");
            Console.WriteLine($"\t Stress11: {worstPlateResult.S11}, stress22: {worstPlateResult.S22}, stress33: {worstPlateResult.S33}");
            Console.WriteLine($"\t Id: {worstPlateResult.Element.Id} Point: {(worstPlateResult.ResultPoint as ResultStressPoint).Location} Node0 Id: {worstPlateResult.Element.Nodes[0].Position}");

            Console.WriteLine($"DEFLECTION");
            Console.WriteLine($"\t WorstDeflection: {worstNodeDisplacement.GetResultingDisplacement()}");
            Console.WriteLine($"\t Id: {worstNodeDisplacement.Element.Id} Point: {worstNodeDisplacement.Element.Position} D1: {worstNodeDisplacement.D1} D2: {worstNodeDisplacement.D2} D3: {worstNodeDisplacement.D3} ");

            Assert.AreEqual(26.04, worstPlateResult.S11, 1);
            Assert.AreEqual(4.16, worstNodeDisplacement.D3, 0.2);
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
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(LinearSolver.DOF.DX, true) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                                                    Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
            gs1.AddRestrains(geometryRestrains1);

            model.AddSurface(gs1);

            // LoadCases
            LoadCase lc0 = new LoadCase("SW", 100, 20, LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.001, s1, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);
            SelfWeightLoad swl = new SelfWeightLoad(lc0, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);
            gs1.AddLoad(swl);

            // Combination
            Combination cmb1 = new CombinationEn("CMB1", StandardEN1990.LimitStates.UltimateStructural);
            cmb1[lc0] = 1.5;
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new CombinationEn("CMB2", StandardEN1990.LimitStates.ServiceabilityCharacteristic);
            cmb2[lc0] = 1.2;
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            model.FemModelSetup();
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
            parametricRestrains.AddRange(s1.Fill.Explode().Select(i => new ParametricLineRestrain(i, new FreedomCase("FC1"), new List<DofRestrain>() { new DofRestrain(LinearSolver.DOF.DX, true) })));

            List<GeometryRestrain> geometryRestrains1 = new List<GeometryRestrain>();
            geometryRestrains1.AddRange(s1.Fill.Explode().Select(i => LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("FC1"), CoordinateSystem.Global)));

            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.NonLinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                                                    Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 40;

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1, meshOptions);
            gs1.AddRestrains(geometryRestrains1);

            model.AddSurface(gs1);

            // LoadCases
            LoadCase lc0 = new LoadCase("SW", 100, 20, LoadCase.LoadCaseTypes.SelfWeight);
            LoadCase lc1 = new LoadCase("LC1", 100, 20, LoadCase.LoadCaseTypes.LiveLoad);
            LoadCase lc2 = new LoadCase("LC2", 5, 20, LoadCase.LoadCaseTypes.WindPressure);

            // Loads
            AreaLoad s1GalLc1 = new AreaLoad(0, 0, 0.001, s1, lc1);
            LineLoad s1ll = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 600, 0), new Point3d(800, 600, 0)), lc2);
            SelfWeightLoad swl = new SelfWeightLoad(lc0, model.Options.GetGravityVector(), GPC.Utilities.Constants.Constants.GRAVITYACCELERATION);

            gs1.AddLoad(s1ll);
            gs1.AddLoad(s1GalLc1);
            gs1.AddLoad(swl);

            // Combination
            Combination cmb1 = new CombinationEn("CMB1", StandardEN1990.LimitStates.UltimateStructural);
            cmb1[lc0] = 1.5;
            cmb1[lc1] = 1.5;
            cmb1[lc2] = 2.5;

            Combination cmb2 = new CombinationEn("CMB2", StandardEN1990.LimitStates.ServiceabilityCharacteristic);
            cmb2[lc0] = 1.2;
            cmb2[lc1] = 1.2;
            cmb2[lc2] = 1.5;
            cmb2[lc2] = 0.5;

            model.AddCombination(cmb1);
            model.AddCombination(cmb2);

            model.FemModelSetup();
            model.PerformChecks();


        }

    }

}