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

            double N, V1, V2,  M1, M2, T;
            N = V1 = V2 = M1 = M2 = T = 0;

            EuroCodeBeamChecker check = new EuroCodeBeamChecker(circularSect, N, V1, V2, M1, M2, T, annex);
            /*check.NRd;
            check.VRdy;
            check.VRdz;
            check.TRd;
            check.MRdy;
            check.MRdz;

            check.WRAxial;
            check.WRShear1;
            check.WRShear2;
            check.WRTorsion;
            check.WRBending1;
            check.WRBending2;
            check.WRResistance;*/
        }
    }
}
