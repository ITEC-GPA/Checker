using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Model.Sections;
using GPC.Model.Materials;
using GPC.Checker.Steel.EC;

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
            SteelMaterial steel = new SteelMaterial("S355", 200000, 0.3, 355, 510, 7850);
            Section circularSect = new SectionCircular(100, 10, steel);
            
            ECBeamCheckerResistance check = new ECBeamCheckerResistance();
            double NtRd = check.Nt_Rd(circularSect.Area, steel.Fyk, circularSect.Area, steel.Fu, 1.0, 1.25);
            double NcRd = check.Nc_Rd(circularSect.Area, steel.Fyk, 1.0);

            double MRdy, MRdz;
            check.M_Rd(circularSect, steel.Fyk, 0, 0, 10000, 1.0, out MRdy, out MRdz);

            Assert.AreEqual(NtRd/1000, (Math.PI * (100 * 100 - 80 * 80) / 4.0) * 355/1000);
            Assert.AreEqual(NcRd/1000, (Math.PI * (100 * 100 - 80 * 80) / 4.0) * 355/1000);

            Console.WriteLine(MRdy / 1000000);
        }
    }
}
