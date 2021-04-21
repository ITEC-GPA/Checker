using GPC.Checker.Glasses.Models;
using GPC.Geometry;
using GPC.Model.Glasses;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using GPC.Checker.Glasses.LoadCases;
using GPC.Model.Combinations;
using GPC.Model.Materials;
using GPC.Checker.Glasses.Extensions;

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
            Prototype p1 = new Prototype("p1", mg, new Polygon3d(), null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            Prototype p2 = new Prototype("p1", mg, new Polygon3d(), null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis, Prototype.CheckMethods.DominantLoad,
                Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            Assert.IsTrue(p1 == p2);
        }

        #endregion


        #region Extension methods

        [TestMethod]
        public void CombinationGetLongTermLoadCases()
        {

            var interlayerMaterial = GetInterlayerMaterialSentryGlas();
            double GLIMIT = interlayerMaterial.GetShearModule(EN16612LoadDurations.LIVECROWD, 30);


            CombinationEn combination = new CombinationEn("cmb1", CombinationEn.CombinationType.UltimateStructural);

            var lcSw    = new LoadCase("SW", EN16612LoadDurations.SELFWEIGHT, 50, GPC.Model.LoadCases.LoadCase.LoadCaseType.SelfWeight);
            var lcLive  = new LoadCase("LIVE", EN16612LoadDurations.LIVECROWD, 30, GPC.Model.LoadCases.LoadCase.LoadCaseType.LiveLoad);
            var lcWind  = new LoadCase("WIND", EN16612LoadDurations.WIND, 35, GPC.Model.LoadCases.LoadCase.LoadCaseType.Wind);
            var lcClimate = new LoadCase("CLIMATE", EN16612LoadDurations.CLIMATEWINTER, 40, GPC.Model.LoadCases.LoadCase.LoadCaseType.ClimateSummer);


            combination.AddLoadCaseCoefficient(lcSw, 1);
            combination.AddLoadCaseCoefficient(lcLive, 1);
            combination.AddLoadCaseCoefficient(lcWind, 1);
            combination.AddLoadCaseCoefficient(lcClimate, 1);

            var lTCombination = combination.GetLongTermLoadCases(interlayerMaterial, GLIMIT);

            // Assert
            foreach(var loadcase in lTCombination)
            {
                if (interlayerMaterial.GetShearModule(loadcase.LoadDuration, loadcase.Temperature) > GLIMIT)
                {
                    Console.WriteLine($"Glim:{GLIMIT} {loadcase.Name} {interlayerMaterial.GetShearModule(loadcase.LoadDuration, loadcase.Temperature)}");
                    Assert.Fail(interlayerMaterial.GetShearModule(loadcase.LoadDuration, loadcase.Temperature).ToString());
                }
                Console.WriteLine($"Glim:{GLIMIT} {loadcase.Name} {interlayerMaterial.GetShearModule(loadcase.LoadDuration, loadcase.Temperature)}");
            }

        }


        #endregion


    }
}
