using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model.Data.Steel;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SteelTests
{
    [TestClass]
    public class EN1993BoltCheckerTest
    {
        [TestMethod]
        public void Test01_ShearTensionCheck01()
        {
            var CurrPlateWithBolts = new RectangularPlateWithBolts(300, 250, SteelMaterialEN1993Data.S235, 10,
                new double[] { 200 }, new double[] { 150 }, 16, BoltMaterialEN1993Data.Class10_9, new Point2d(50, 50));
            var CurrBolGriBar = CurrPlateWithBolts.BoltGrid.CalculateBarycenter();
            var CurrAppPointSystem = new CoordinateSystem(CurrBolGriBar, Vector3d.XAxis, Vector3d.YAxis);
            var CurrBoltStresses = new List<BoltStresses>()
            {
                new BoltStresses(new LoadCase("COMB0", LoadCase.LoadCaseTypes.SelfWeight), new ResultBeamForces(8000, 12000, 0, 0, 0, 0, CurrAppPointSystem)),
                new BoltStresses(new LoadCase("COMB1", LoadCase.LoadCaseTypes.SelfWeight), new ResultBeamForces(0, 0, 4000, 0, 0, 0, CurrAppPointSystem))
            };
            var CurrStd = new StandardEN1993p11();
            var CurrOptions = new EN1993BoltChecker.EN1993BoltOptions();

            var CurrChecker = new EN1993BoltChecker(CurrPlateWithBolts, CurrBoltStresses, CurrStd, CurrOptions);
            CurrChecker.PerformCheck();

            // shear
            var MasSollV = CurrChecker.BoltResultsEN1993.Max(br => br.BeamForces.GetCombinedShearForce());
            var MinResiV = CurrChecker.BoltResultsEN1993.Min(br => br.ShearResistance);
            var MaxRatioV = CurrChecker.BoltResultsEN1993.Max(br => br.ShearRatio);

            Assert.AreEqual(3000.0, MasSollV, 1);
            Assert.AreEqual(62800.0, MinResiV, 1);
            Assert.AreEqual(0.0477707006369427, MaxRatioV, 0.00001);

            // bearing
            var MinResiBear = CurrChecker.BoltResultsEN1993.Min(br => br.BearingResistance);
            var MaxRatioBear = CurrChecker.BoltResultsEN1993.Max(br => br.BearingRatio);
            var ListWithMax = CurrChecker.BoltResultsEN1993.FindAll(br => br.BearingRatio == MaxRatioBear);

            Assert.AreEqual(106666.66666667, MinResiBear, 0.00001);
            Assert.AreEqual(0.028125, MaxRatioBear, 0.00001);
            foreach (var iMax in ListWithMax)
            {
                Assert.AreEqual(50.0, iMax.BearingE1, 0.1);
                Assert.AreEqual(double.MaxValue, iMax.BearingP1);
                Assert.AreEqual(50.0, iMax.BearingE2, 0.1);
                Assert.AreEqual(150.0, iMax.BearingP2, 0.1);
                Assert.AreEqual(2.5, iMax.Bearingk1, 0.00001);
                Assert.AreEqual(0.9259259259, iMax.BearingAlphaB, 0.00001);
            }

            // tension
            var MasSollN = CurrChecker.BoltResultsEN1993.Max(br => br.BeamForces.N);
            var MinResiN = CurrChecker.BoltResultsEN1993.Min(br => double.IsNaN(br.TensionResistance) ? double.PositiveInfinity : br.TensionResistance);
            var MaxRatioN = CurrChecker.BoltResultsEN1993.Max(br => br.TensionRatio);

            Assert.AreEqual(2000.0, MasSollN, 1);
            Assert.AreEqual(113040.0, MinResiN, 1);
            Assert.AreEqual(0.0176928520877565, MaxRatioN, 0.00001);

            // combined shear and tension
            var MaxCombinedRatio = CurrChecker.BoltResultsEN1993.Max(br => br.CombinedShearTensionRatio);

            Assert.AreEqual(0.06040845213, MaxCombinedRatio, 0.00001);

            // Punching
            var MinResiPunc = CurrChecker.BoltResultsEN1993.Min(br => double.IsNaN(br.PunchingResistance) ? double.PositiveInfinity : br.PunchingResistance);
            var MaxRatioPunc = CurrChecker.BoltResultsEN1993.Max(br => br.PunchingRatio);
            Assert.AreEqual(130288, MinResiPunc, 10000);
            Assert.AreEqual(0.015350593, MaxRatioPunc, 0.0015);
        }

        [TestMethod]
        public void Test02_ShearTensionCheck02()
        {
            var CurrPlateWithBolts = new RectangularPlateWithBolts(300, 250, SteelMaterialEN1993Data.S235, 10,
                new double[] { 200 }, new double[] { 150 }, 16, BoltMaterialEN1993Data.Class10_9, new Point2d(50, 50));
            var CurrBolGriBar = CurrPlateWithBolts.BoltGrid.CalculateBarycenter();
            var CurrAppPointSystem = new CoordinateSystem(CurrBolGriBar, Vector3d.XAxis, Vector3d.YAxis);
            var CurrBoltStresses = new List<BoltStresses>()
            {
                new BoltStresses(new LoadCase("COMB0", LoadCase.LoadCaseTypes.SelfWeight), new ResultBeamForces(80000, 120000, 0, 0, 0, 0, CurrAppPointSystem)),
                new BoltStresses(new LoadCase("COMB1", LoadCase.LoadCaseTypes.SelfWeight), new ResultBeamForces(0, 0, 40000, 0, 0, 0, CurrAppPointSystem))
            };
            var CurrStd = new StandardEN1993p11();
            var CurrOptions = new EN1993BoltChecker.EN1993BoltOptions()
            {
                ShearConnectionsCategory = EN1993BoltChecker.EN1993BoltOptions.ShearConnectionsCategoryType.C,
                ClassFrictionSurfaces = EN1993BoltChecker.EN1993BoltOptions.ClassFrictionSurfacesType.C
            };

            var CurrChecker = new EN1993BoltChecker(CurrPlateWithBolts, CurrBoltStresses, CurrStd, CurrOptions);
            CurrChecker.PerformCheck();

            // shear --> slip for preloaded connection

            // bearing
            var MinResiBear = CurrChecker.BoltResultsEN1993.Min(br => br.BearingResistance);
            var MaxRatioBear = CurrChecker.BoltResultsEN1993.Max(br => br.BearingRatio);
            var ListWithMax = CurrChecker.BoltResultsEN1993.FindAll(br => br.BearingRatio == MaxRatioBear);

            Assert.AreEqual(106666.66666667, MinResiBear, 0.00001);
            Assert.AreEqual(0.28125, MaxRatioBear, 0.00001);
            foreach (var iMax in ListWithMax)
            {
                Assert.AreEqual(50.0, iMax.BearingE1, 0.1);
                Assert.AreEqual(double.MaxValue, iMax.BearingP1);
                Assert.AreEqual(50.0, iMax.BearingE2, 0.1);
                Assert.AreEqual(150.0, iMax.BearingP2, 0.1);
                Assert.AreEqual(2.5, iMax.Bearingk1, 0.00001);
                Assert.AreEqual(0.9259259259, iMax.BearingAlphaB, 0.00001);
            }

            // slip
            var MinResiSlip = CurrChecker.BoltResultsEN1993.Min(br => br.SlipResistance);
            var MaxRatioSlip = CurrChecker.BoltResultsEN1993.Max(br => br.SlipRatio);

            Assert.AreEqual(22536.0, MinResiSlip, 1);
            Assert.AreEqual(1.3312034078807242, MaxRatioSlip, 0.00001);

            // tension
            var MasSollN = CurrChecker.BoltResultsEN1993.Max(br => br.BeamForces.N);
            var MinResiN = CurrChecker.BoltResultsEN1993.Min(br => double.IsNaN(br.TensionResistance) ? double.PositiveInfinity : br.TensionResistance);
            var MaxRatioN = CurrChecker.BoltResultsEN1993.Max(br => br.TensionRatio);

            Assert.AreEqual(20000.0, MasSollN, 1);
            Assert.AreEqual(113040.0, MinResiN, 1);
            Assert.AreEqual(0.176928520877565, MaxRatioN, 0.00001);

            // combined shear and tension
            //var MaxCombinedRatio = CurrChecker.BoltResultsEN1993.Max(br => br.CombinedShearTensionRatio);

            //Assert.AreEqual(0.6040845213, MaxCombinedRatio, 0.00001);

            // Punching
            var MinResiPunc = CurrChecker.BoltResultsEN1993.Min(br => double.IsNaN(br.PunchingResistance) ? double.PositiveInfinity : br.PunchingResistance);
            var MaxRatioPunc = CurrChecker.BoltResultsEN1993.Max(br => br.PunchingRatio);
            Assert.AreEqual(130288, MinResiPunc, 10000);
            Assert.AreEqual(0.15350593, MaxRatioPunc, 0.015);
        }

        [TestMethod]
        public void Test03_ShearCheck01()
        {
            // Tratto da "Progettare i collegamenti nelle strutture in acciaio" di Giovanni Conticello e Sebastiano Florida.
            // § 1.4. Verifiche sul nodo - TRAVE, a pagina 231.
            // Ricalcolato poi a mano per errori nel libro.
            var CurrPlateWithBolts = new RectangularPlateWithBolts(160, 248, SteelMaterialEN1993Data.S275, 10,
                new double[] { 60 }, new double[] { 80, 80 }, 18, BoltMaterialEN1993Data.Class8_8, new Point2d(50, 44));
            var CurrBolGriBar = CurrPlateWithBolts.BoltGrid.CalculateBarycenter();
            var CurrAppPointSystem = new CoordinateSystem(CurrBolGriBar, Vector3d.XAxis, Vector3d.YAxis);
            var CurrBoltStresses = new List<BoltStresses>()
            {
                new BoltStresses(
                    new LoadCase("COMB0", LoadCase.LoadCaseTypes.SelfWeight),
                    new ResultBeamForces(0, 60000, -250000, -22000000, 0, 0, CurrAppPointSystem))
            };
            var CurrStd = new StandardEN1993p11();
            var CurrOptions = new EN1993BoltChecker.EN1993BoltOptions()
            {
                ShearConnectionsCategory = EN1993BoltChecker.EN1993BoltOptions.ShearConnectionsCategoryType.A,
                NumShearPlane = 2
            };

            var CurrChecker = new EN1993BoltChecker(CurrPlateWithBolts, CurrBoltStresses, CurrStd, CurrOptions);
            CurrChecker.PerformCheck();

            StringBuilder sb = new StringBuilder();
            foreach (var br in CurrChecker.BoltResultsEN1993)
                sb.AppendLine($"{br.BeamForces.V1},{br.BeamForces.V2}");

            Trace.WriteLine(sb);

            // shear
            var MasSollV = CurrChecker.BoltResultsEN1993.Max(br => br.BeamForces.GetCombinedShearForce());
            var MinResiV = CurrChecker.BoltResultsEN1993.Min(br => br.ShearResistance);
            var MaxRatioV = CurrChecker.BoltResultsEN1993.Max(br => br.ShearRatio);

            Assert.AreEqual(45886.75032, MasSollV, 1);
            Assert.AreEqual(73728.0, MinResiV, 1);
            Assert.AreEqual(0.622378883, MaxRatioV, 0.00001);

            // bearing - TRAVE a pagina 249, vedi anoglare.
            var MinResiBear = CurrChecker.BoltResultsEN1993.Min(br => br.BearingResistance);
            var MaxRatioBear = CurrChecker.BoltResultsEN1993.Max(br => br.BearingRatio);
            var ListWithMax = CurrChecker.BoltResultsEN1993.FindAll(br => br.BearingRatio == MaxRatioBear);

            // Geometrie nel disegno "\\studio\Software_Development\01 Theory\08 Bolt\Test03_ShearCheck01.dwg".
            Assert.AreEqual(54.5384, CurrChecker.BoltResultsEN1993[0].BearingE1, 0.0001);
            Assert.AreEqual(47.9938, CurrChecker.BoltResultsEN1993[0].BearingE2, 0.0001);
            Assert.AreEqual(double.MaxValue, CurrChecker.BoltResultsEN1993[0].BearingP1);
            Assert.AreEqual(80.0, CurrChecker.BoltResultsEN1993[0].BearingP2, 0.0001);

            Assert.AreEqual(138.1279, CurrChecker.BoltResultsEN1993[1].BearingE1, 0.0001);
            Assert.AreEqual(55.6967, CurrChecker.BoltResultsEN1993[1].BearingE2, 0.0001);
            Assert.AreEqual(80.0, CurrChecker.BoltResultsEN1993[1].BearingP1, 0.0001);
            Assert.AreEqual(60.0, CurrChecker.BoltResultsEN1993[1].BearingP2, 0.0001);

            Assert.AreEqual(115.0075, CurrChecker.BoltResultsEN1993[2].BearingE1, 0.0001);
            Assert.AreEqual(46.0030, CurrChecker.BoltResultsEN1993[2].BearingE2, 0.0001);
            Assert.AreEqual(60.0, CurrChecker.BoltResultsEN1993[2].BearingP1, 0.0001);
            Assert.AreEqual(80.0, CurrChecker.BoltResultsEN1993[2].BearingP2, 0.0001);

            Assert.AreEqual(54.8146, CurrChecker.BoltResultsEN1993[3].BearingE1, 0.0001);
            Assert.AreEqual(62.2893, CurrChecker.BoltResultsEN1993[3].BearingE2, 0.0001);
            Assert.AreEqual(double.MaxValue, CurrChecker.BoltResultsEN1993[3].BearingP1);
            Assert.AreEqual(60.0, CurrChecker.BoltResultsEN1993[3].BearingP2, 0.0001);

            Assert.AreEqual(125.5545, CurrChecker.BoltResultsEN1993[4].BearingE1, 0.0001);
            Assert.AreEqual(50.6268, CurrChecker.BoltResultsEN1993[4].BearingE2, 0.0001);
            Assert.AreEqual(80.0, CurrChecker.BoltResultsEN1993[4].BearingP1, 0.0001);
            Assert.AreEqual(60.0, CurrChecker.BoltResultsEN1993[4].BearingP2, 0.0001);

            Assert.AreEqual(68.7193, CurrChecker.BoltResultsEN1993[5].BearingE1, 0.0001);
            Assert.AreEqual(60.4730, CurrChecker.BoltResultsEN1993[5].BearingE2, 0.0001);
            Assert.AreEqual(double.MaxValue, CurrChecker.BoltResultsEN1993[5].BearingP1);
            Assert.AreEqual(80.0, CurrChecker.BoltResultsEN1993[5].BearingP2, 0.0001);

            Assert.AreEqual(116100.0, MinResiBear, 0.00001);
            Assert.AreEqual(0.30066302785877264, MaxRatioBear, 0.00001);
            foreach (var iMax in ListWithMax)
            {
                Assert.AreEqual(115.0075, iMax.BearingE1, 0.1);
                Assert.AreEqual(60.0, iMax.BearingP1);
                Assert.AreEqual(46.0030, iMax.BearingE2, 0.1);
                Assert.AreEqual(80.0, iMax.BearingP2, 0.1);
                Assert.AreEqual(2.5, iMax.Bearingk1, 0.00001);
                Assert.AreEqual(0.75, iMax.BearingAlphaB, 0.00001);
            }
        }

        [TestMethod]
        public void Test04_ShearTensionCheck03()
        {
            // Tratto da "Progettare i collegamenti nelle strutture in acciaio" di Giovanni Conticello e Sebastiano Florida.
            // § 1.4. Verifiche sul nodo - PILASTRO, a pagina 234.
            // Ricalcolato poi a mano per errori nel libro.
            var CurrPlateWithBolts = new RectangularPlateWithBolts(80, 248, SteelMaterialEN1993Data.S275, 10,
                new double[] { }, new double[] { 80, 80 }, 18, BoltMaterialEN1993Data.Class8_8, new Point2d(38.9, 44));
            var CurrBolGriBar = CurrPlateWithBolts.BoltGrid.CalculateBarycenter();
            var CurrAppPointSystem = new CoordinateSystem(CurrBolGriBar, Vector3d.XAxis, Vector3d.YAxis);
            var CurrBoltStresses = new List<BoltStresses>()
            {
                new BoltStresses(
                    new LoadCase("COMB0", LoadCase.LoadCaseTypes.SelfWeight),
                    new ResultBeamForces(30000, 25000, -125000, -5250000, 0, 0, CurrAppPointSystem))
            };
            var CurrStd = new StandardEN1993p11();
            var CurrOptions = new EN1993BoltChecker.EN1993BoltOptions()
            {
                ShearConnectionsCategory = EN1993BoltChecker.EN1993BoltOptions.ShearConnectionsCategoryType.A,
                NumShearPlane = 1
            };

            var CurrChecker = new EN1993BoltChecker(CurrPlateWithBolts, CurrBoltStresses, CurrStd, CurrOptions);
            CurrChecker.PerformCheck();

            // shear
            var MasSollV = CurrChecker.BoltResultsEN1993.Max(br => br.BeamForces.GetCombinedShearForce());
            var MinResiV = CurrChecker.BoltResultsEN1993.Min(br => br.ShearResistance);
            var MaxRatioV = CurrChecker.BoltResultsEN1993.Max(br => br.ShearRatio);

            Assert.AreEqual(58558, MasSollV, 1);
            Assert.AreEqual(73728.0, MinResiV, 1);
            Assert.AreEqual(0.79424965323626706, MaxRatioV, 0.00001);

            // tension
            var MasSollN = CurrChecker.BoltResultsEN1993.Max(br => br.BeamForces.N);
            var MinResiN = CurrChecker.BoltResultsEN1993.Min(br => double.IsNaN(br.TensionResistance) ? double.PositiveInfinity : br.TensionResistance);
            var MaxRatioN = CurrChecker.BoltResultsEN1993.Max(br => br.TensionRatio);

            Assert.AreEqual(10000.0, MasSollN, 1);
            Assert.AreEqual(110592.0, MinResiN, 1);
            Assert.AreEqual(0.09, MaxRatioN, 0.001);

            // combined shear and tension
            var MaxCombinedRatio = CurrChecker.BoltResultsEN1993.Max(br => br.CombinedShearTensionRatio);

            Assert.AreEqual(0.858, MaxCombinedRatio, 0.001);
        }
    }
}
