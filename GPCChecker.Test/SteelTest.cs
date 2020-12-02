using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Model.Sections;
using GPC.Model.Materials;
using GPC.Checker.Steel.EuroCode;

namespace SteelTests
{
    [TestClass]
    public class SteelTest
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Nothing
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // Nothing
        }

        [TestCleanup]
        public void CleanUp()
        {
            // Nothing
        }

        [TestMethod]
        public void CircularSectionTest1()
        {
            Annex annex = new Annex();
            double fy = 355;
            double fu = 510;
            SteelMaterial steel = new SteelMaterial("S355", 200000, 0.3, fy, fu, 7850);
            Section circularSect = new SectionCHS(100, 10, steel);
            //double A = Math.PI * (100 * 100 - 80 * 80) / 4.0 *355 / 1000;

            double N = 1;
            double V1 = 2;
            double V2 = 3;
            double M1 = 4;
            double M2 = 5;
            double T = 6;

            double L = 0;
            double betay = 1;
            double betaz = 1;
            double betaLT = 1;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(circularSect, N, V1, V2, M1, M2, T, L, betay, betaz, betaLT, annex);

            double Nrd = checker.NRd;
            double Mrdy = checker.MRdy;
            double Mrdz = checker.MRdz;
            double Trd = checker.TRd;
            double Vrdy = checker.VRdy;
            double Vrdz = checker.VRdz;
            double WrAxial = checker.WRAxial;
            double WRBending1 = checker.WRBending1;
            double WRBending2 = checker.WRBending2;
            double WRBuckling1 = checker.WRBuckling1;
            double WRBuckling2 = checker.WRBuckling2;
            double WRResistance = checker.WRResistance;
            double WRShear1 = checker.WRShear1;
            double WRShear2 = checker.WRShear2;
            double WRTorsion = checker.WRTorsion;
        }
    }
}
