using GPC.Checker.Glasses.Models;
using GPC.Geometry;
using GPC.Model.Glasses;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GlassTests
{
    [TestClass]
    public class GeneralTest : GlassTestBase
    {

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
    }
}
