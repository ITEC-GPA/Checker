using ConcreteTests;
using GPC.Checkers.Concrete.Checkers;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GPCChecker.Test.Concrete
{
    [TestClass]
    public class MixedSectionTest : ConcreteTestBase
    {
        [TestMethod]
        public void FailureDomain1()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete());
            FailureDomainCommonAssertModelCode(section, sectionChecker, new StandardNTC2018Concrete(), 5.0, true);
            sectionChecker.GetPlasticFailureDomainResult();

            Assert.IsTrue(true);
        }
    }
}
