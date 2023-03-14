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
                new BoltStresses(new LoadCase("COMB0", LoadCase.LoadCaseTypes.SelfWeight), new ResultBeamForces(8000, 12000, 0, 0, 0, 0, CurrAppPointSystem)),
                new BoltStresses(new LoadCase("COMB1", LoadCase.LoadCaseTypes.SelfWeight), new ResultBeamForces(0, 0, 4000, 0, 0, 0, CurrAppPointSystem))
            };
            var CurrStd = new StandardEN1993p11();
            var CurrOptions = new EN1993BoltOptions();

            var CurrChecker = new EN1993BoltChecker(CurrBoltGrid, CurrBoltStresses, CurrStd, CurrOptions);
            CurrChecker.PerformCheck();

            // Test
            var MasSollV = CurrChecker.BoltResultsEN1993.Max(br => br.BeamForces.GetCombinedShearForce());
            var MinResiV = CurrChecker.BoltResultsEN1993.Min(br => br.ShearResistance);
            var MaxRatioV = CurrChecker.BoltResultsEN1993.Max(br => br.RatioShear);

            var MasSollN = CurrChecker.BoltResultsEN1993.Max(br => br.BeamForces.N);
            var MinResiN = CurrChecker.BoltResultsEN1993.Min(br => br.TensionResistance);
            var MaxRatioN = CurrChecker.BoltResultsEN1993.Max(br => br.RatioTension);

            var MaxCombinedRatio = CurrChecker.BoltResultsEN1993.Max(br => br.RatioCombinedShearTension);

            Assert.AreEqual(3000.0, MasSollV, 1);
            Assert.AreEqual(62800.0, MinResiV, 1);
            Assert.AreEqual(0.0477707006369427, MaxRatioV, 0.00000000001);
            Assert.AreEqual(2000.0, MasSollN, 1);
            Assert.AreEqual(113040.0, MinResiN, 1);
            Assert.AreEqual(0.0176928520877565, MaxRatioN, 0.00000000001);
            Assert.AreEqual(0.06040845213, MaxCombinedRatio, 0.00000000001);
        }
    }
}
