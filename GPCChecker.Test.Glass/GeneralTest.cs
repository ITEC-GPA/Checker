using GPC.Checkers.Glasses.Extensions;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Loads;
using GPC.Checkers.Glasses.Models;
using GPC.Geometry;
using GPC.Model.Combinations;
using GPC.Model.Glasses;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GlassTests
{
    [TestClass]
    public class GeneralTest : GlassTestBase
    {

        #region Prototype

        [TestMethod]
        public void PrototypePolygonEquals()
        {
            MonolithicGlass mg = new MonolithicGlass("Mg1", 8, GetGlassMaterialAstm());

            // Prototype
            Prototype p1 = new Prototype("p1", mg, new Polygon3d(), null, null, Prototype.Standards.ASTME1300, 
                                        Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                        Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement, 
                                        new Prototype.LaminatedEqThicknessParameters());

            Prototype p2 = new Prototype("p1", mg, new Polygon3d(), null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                                            Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                            new Prototype.LaminatedEqThicknessParameters());

            Assert.IsTrue(p1 == p2);
        }

        #endregion


        #region Extension methods

        [TestMethod]
        public void CombinationGetLongTermLoadCases()
        {

            var interlayerMaterial = GetInterlayerMaterialSentryGlas();
            double GLIMIT = interlayerMaterial.GetShearModule(EN16612LoadDurations.LIVECROWD, 30);


            Combination combination = new Combination("cmb1");

            var lcSw = new LoadCase("SW", EN16612LoadDurations.SELFWEIGHT, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            var lcLive = new LoadCase("LIVE", EN16612LoadDurations.LIVECROWD, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);
            var lcWind = new LoadCase("WIND", EN16612LoadDurations.WIND, 35, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            var lcClimate = new ClimateLoadCase("CLIMATE", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer, GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 10, 20, EN16612LoadDurations.CLIMATESUMMER, 30);


            combination.AddLoadCaseCoefficient(lcSw, 1);
            combination.AddLoadCaseCoefficient(lcLive, 1);
            combination.AddLoadCaseCoefficient(lcWind, 1);
            combination.AddLoadCaseCoefficient(lcClimate, 1);

            var lTCombination = combination.GetIGlassLoadCase().GetLongTermLoadCases(interlayerMaterial, GLIMIT);

            // Assert
            foreach (var loadcase in lTCombination.Cast<IGlassLoadCase>().ToList())
            {
                if (interlayerMaterial.GetShearModule(loadcase.LoadDuration, loadcase.Temperature) > GLIMIT)
                {
                    Console.WriteLine($"Glim:{GLIMIT} {loadcase.Name} {interlayerMaterial.GetShearModule(loadcase.LoadDuration, loadcase.Temperature)}");
                    Assert.Fail(interlayerMaterial.GetShearModule(loadcase.LoadDuration, loadcase.Temperature).ToString());
                }
                Console.WriteLine($"Glim:{GLIMIT} {loadcase.Name} {interlayerMaterial.GetShearModule(loadcase.LoadDuration, loadcase.Temperature)}");
            }

        }


        [TestMethod]
        public void GetLowerGvalueLoadCase()
        {
            var interlayerMaterial = GetInterlayerMaterialSentryGlas();

            List<IGlassLoadCase> loadCases1 = new List<IGlassLoadCase>();
            List<IGlassLoadCase> loadCases2 = new List<IGlassLoadCase>();

            var lcSw = new LoadCase("SW", EN16612LoadDurations.SELFWEIGHT, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            var lcLive = new LoadCase("LIVE", EN16612LoadDurations.LIVECROWD, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);
            var lcWind = new LoadCase("WIND", EN16612LoadDurations.WIND, 35, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            var lcClimate = new ClimateLoadCase("CLIMATE", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer,
                                                           GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH,
                                                           10, 20,
                                                           EN16612LoadDurations.CLIMATEWINTER, 40);


            loadCases1.Add(lcLive);
            loadCases1.Add(lcSw);
            loadCases1.Add(lcWind);
            loadCases1.Add(lcClimate);


            loadCases2.Add(lcLive);
            loadCases2.Add(lcWind);
            loadCases2.Add(lcClimate);


            var lc1 = loadCases1.GetLowerGvalueLoadCase(interlayerMaterial);
            var lc2 = loadCases2.GetLowerGvalueLoadCase(interlayerMaterial);

            loadCases1.ForEach(i => Console.WriteLine($"{i.Name}: \t {interlayerMaterial.GetShearModule(i.LoadDuration, i.Temperature)}"));
            loadCases2.ForEach(i => Console.WriteLine($"{i.Name}: \t {interlayerMaterial.GetShearModule(i.LoadDuration, i.Temperature)}"));


            Assert.IsTrue(lc1.Equals(lcSw), $"{interlayerMaterial.GetShearModule(lcSw.LoadDuration, lcSw.Temperature)} {interlayerMaterial.GetShearModule(lc1.LoadDuration, lc1.Temperature)}");
            Assert.IsTrue(lc2.Equals(lcClimate), $"{interlayerMaterial.GetShearModule(lcSw.LoadDuration, lcSw.Temperature)} {interlayerMaterial.GetShearModule(lc2.LoadDuration, lc2.Temperature)}");
        }


        [TestMethod]
        public void GetHigherGvalueLoadCase()
        {
            var interlayerMaterial = GetInterlayerMaterialSentryGlas();

            List<IGlassLoadCase> loadCases = new List<IGlassLoadCase>();

            var lcSw = new LoadCase("SW", EN16612LoadDurations.SELFWEIGHT, 50, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight);
            var lcLive = new LoadCase("LIVE", EN16612LoadDurations.LIVECROWD, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);
            var lcWind = new LoadCase("WIND", EN16612LoadDurations.WIND, 35, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            var lcClimate = new ClimateLoadCase("CLIMATE", GPC.Model.LoadCases.ClimateLoadCase.Seasons.Summer, GPC.Model.LoadCases.ClimateLoadCase.ClimateTypes.DeltaH, 10, 20, EN16612LoadDurations.CLIMATESUMMER, 40);

            loadCases.Add(lcLive);
            loadCases.Add(lcSw);
            loadCases.Add(lcWind);
            loadCases.Add(lcClimate);

            var lc = loadCases.GetHigherGvalueLoadCase(interlayerMaterial);

            loadCases.ForEach(i => Console.WriteLine($"{i.Name}: \t {interlayerMaterial.GetShearModule(i.LoadDuration, i.Temperature)}"));



            Assert.IsTrue(lc.Equals(lcWind), $"{interlayerMaterial.GetShearModule(lcSw.LoadDuration, lcSw.Temperature)} {interlayerMaterial.GetShearModule(lc.LoadDuration, lc.Temperature)}");
        }


        #endregion


        [TestMethod]
        public void LoadEqualityComparerTest1()
        {

            HashSet<GPC.Model.Loads.Load> loads1 = new HashSet<GPC.Model.Loads.Load>(new LoadDurationTemperatureAndGeometryEqualityComparer())
            {
                new PointLoad(new Vector3d(0, 0, 0), new Vector3d(1, 2, 3), new Point3d(4, 5, 6),
                                    new LoadCase("lc", 10, 20, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.Earthquake), CoordinateSystem.Global),

                new PointLoad(new Vector3d(0, 0, 0), new Vector3d(1, 2, 3), new Point3d(4, 5, 6),
                                    new LoadCase("lc", 10, 20, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.Earthquake), CoordinateSystem.Global),

                new PointLoad(new Vector3d(0, 0, 0), new Vector3d(1, 2, 3), new Point3d(4, 5, 6),
                                    new LoadCase("lc", 10, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.Earthquake), CoordinateSystem.Global),

                new PointLoad(new Vector3d(0, 0, 0), new Vector3d(1, 2, 3), new Point3d(4, 5, 6),
                                    new LoadCase("lc", 20, 20, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.Earthquake), CoordinateSystem.Global)
            };


            HashSet<GPC.Model.Loads.Load> loads2 = new HashSet<GPC.Model.Loads.Load>(new LoadDurationTemperatureAndGeometryEqualityComparer())
            {
                new PointLoad(new Vector3d(0, 0, 0), new Vector3d(1, 2, 3), new Point3d(40, 5, 6),
                                    new LoadCase("lc", 10, 20, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.Earthquake), CoordinateSystem.Global),

                new PointLoad(new Vector3d(0, 0, 0), new Vector3d(1, 2, 3), new Point3d(4, 5, 6),
                                    new LoadCase("lc", 10, 20, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.Earthquake), CoordinateSystem.Global),

                new PointLoad(new Vector3d(0, 0, 0), new Vector3d(1, 2, 3), new Point3d(4, 5, 6),
                                    new LoadCase("lc", 10, 20, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.Earthquake), CoordinateSystem.Global)
            };


            HashSet<GPC.Model.Loads.Load> loads3 = new HashSet<GPC.Model.Loads.Load>(new LoadDurationTemperatureAndGeometryEqualityComparer())
            {
                new PointLoad(new Vector3d(0, 0, 0), new Vector3d(1, 2, 3), new Point3d(4, 5, 6),
                              new LoadCase("lc", 10, 20, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.Earthquake), CoordinateSystem.Global),

                new PointLoad(new Vector3d(0, 0, 0), new Vector3d(1, 2, 3), new Point3d(4, 5, 6),
                              new LoadCase("lc", 10, 20, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.Earthquake), CoordinateSystem.Global),

                new LineLoad(new Vector3d(0, 0, 0), new Vector3d(1, 2, 3), new Line3d(new Point3d(4, 5, 6), new Point3d(4, 55, 6)),
                             new LoadCase("lc", 10, 20, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.Earthquake), CoordinateSystem.Global)
            };


            Assert.IsTrue(loads1.Count() == 3, loads1.Count().ToString());
            Assert.IsTrue(loads2.Count() == 2, loads2.Count().ToString());
            Assert.IsTrue(loads3.Count() == 2, loads3.Count().ToString());
        }

    }
}
