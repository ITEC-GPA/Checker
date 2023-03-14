using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model.Data.Steel;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using static GPC.Checkers.Steel.Checkers.EN1993BoltChecker;

namespace GPCCheckers.Test
{
    [TestClass]
    public class EN1993BoltCheckerTest
    {
        [TestMethod]
        public void Test01_ShearCheck()
        {
            // BoltGrid boltGrid, List<BoltStresses> boltStresses, StandardEN1993p11 standard, EN1993BoltOptions options
            var CurrBoltGrid = new BoltGrid(new double[] { 200 }, new double[] { 150 }, 16, BoltMaterialEN1993Data.Class10_9);
            var CurrBolGriBar = CurrBoltGrid.CalculateBarycenter();
            var CurrAppPointSystem = new CoordinateSystem(CurrBolGriBar, Vector3d.XAxis, Vector3d.YAxis);
            var CurrBoltStresses = new List<BoltStresses>()
            {
                new BoltStresses(new LoadCase("COMB0", LoadCase.LoadCaseTypes.SelfWeight), new ResultBeamForces(0, 12000, 0, 0, 0, 0, CurrAppPointSystem)),
                new BoltStresses(new LoadCase("COMB1", LoadCase.LoadCaseTypes.SelfWeight), new ResultBeamForces(0, 0, 4000, 0, 0, 0, CurrAppPointSystem))
            };
            var CurrStd = new StandardEN1993p11();
            var CurrOptions = new EN1993BoltOptions();

            var CurrChecker = new EN1993BoltChecker(CurrBoltGrid, CurrBoltStresses, CurrStd, CurrOptions);
            CurrChecker.PerformCheck();

            // Test
            var MasSoll = CurrChecker.BoltResultsEN1993.Max(br => br.BeamForces.GetCombinedShearForce());
            var MinResi = CurrChecker.BoltResultsEN1993.Min(br => br.ShearResistance);

            Assert.AreEqual(3000, MasSoll);
            Assert.AreEqual(62800, MinResi);
        }
    }
}
