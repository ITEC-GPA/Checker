using ConcreteTests;
using GPC.Checkers.Concrete.Checkers;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ConcreteTests
{
    [TestClass]
    public class MixedSectionTest : ConcreteTestBase
    {
        [TestMethod]
        public void StructuralSteelDistances01()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);

            SectionSolverModelCode2010Test sectionSolverModelCode2010Test = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());
            var dist = sectionSolverModelCode2010Test.CalculateMaxMinSectionDistances(0.0);

            Assert.AreEqual(-150.0, dist.dmaxStrucSteel, 0.0001);
            Assert.AreEqual(-450.0, dist.dminStrucSteel, 0.0001);

            dist = sectionSolverModelCode2010Test.CalculateMaxMinSectionDistances(0.5 * Math.PI);

            Assert.AreEqual(75.0, dist.dmaxStrucSteel, 0.0001);
            Assert.AreEqual(-75.0, dist.dminStrucSteel, 0.0001);

            dist = sectionSolverModelCode2010Test.CalculateMaxMinSectionDistances(Math.PI / 6.0); // 30°

            Assert.AreEqual(-92.40381057, dist.dmaxStrucSteel, 0.0001);
            Assert.AreEqual(-427.21143170, dist.dminStrucSteel, 0.0001);
        }

        [TestMethod]
        public void StructuralSteelDistances02()
        {
            // Same section used in the StructuralSteelDistances01 test with an additional H-profile eccentricity.
            double eccentricity = 200.0;
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0, eccentricity);

            SectionSolverModelCode2010Test sectionSolverModelCode2010Test = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());
            var dist = sectionSolverModelCode2010Test.CalculateMaxMinSectionDistances(0.0);

            Assert.AreEqual(-150.0, dist.dmaxStrucSteel, 0.0001);
            Assert.AreEqual(-450.0, dist.dminStrucSteel, 0.0001);

            dist = sectionSolverModelCode2010Test.CalculateMaxMinSectionDistances(0.5 * Math.PI);

            Assert.AreEqual(-125.0, dist.dmaxStrucSteel, 0.0001);
            Assert.AreEqual(-275.0, dist.dminStrucSteel, 0.0001);

            dist = sectionSolverModelCode2010Test.CalculateMaxMinSectionDistances(Math.PI / 6.0); // 30°

            Assert.AreEqual(-192.40381057, dist.dmaxStrucSteel, 0.0001);
            Assert.AreEqual(-527.21143170, dist.dminStrucSteel, 0.0001);
        }

        [TestMethod]
        public void FailureDomain1()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete());
            FailureDomainCommonAssertModelCode(section, sectionChecker, new StandardNTC2018Concrete(), 5.0);
            sectionChecker.GetPlasticFailureDomainResult();

            Assert.IsTrue(true);
        }
    }
}
