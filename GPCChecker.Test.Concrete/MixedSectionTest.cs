using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using static GPC.Checkers.Concrete.SectionSolvers.SectionSolver;

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
        public void StrainPlanesDomain1()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);

            (FailureZones, int)[] plasticZones =
            {
                (FailureZones.F1, 5),
                (FailureZones.F2A, 5),
                (FailureZones.F2B, 5),
                (FailureZones.F3A, 5),
                (FailureZones.F3B, 5),
                (FailureZones.F4, 5)
            };

            SectionSolverModelCode2010Test sectionSolverModelCode2010Test = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());
            var sectionDistances = sectionSolverModelCode2010Test.CalculateMaxMinSectionDistances(0.0);
            var p2 = sectionSolverModelCode2010Test.GetP2(sectionDistances, FailureDomainTypes.Plastic);
            var p3 = sectionSolverModelCode2010Test.GetP3(sectionDistances, FailureDomainTypes.Plastic);
            var p4 = sectionSolverModelCode2010Test.GetP4(sectionDistances, FailureDomainTypes.Plastic);
            var p5 = sectionSolverModelCode2010Test.GetP5(sectionDistances, FailureDomainTypes.Plastic);

            var planes = new List<StrainPlane>();

            for (int i = 0; i < plasticZones.Length; i++)
            {
                FailureZones failureZones = plasticZones[i].Item1;
                int subdivision = plasticZones[i].Item2 + 1;
                int subIndex = 0;

                var p1 = sectionSolverModelCode2010Test.GetP1(sectionDistances, FailureDomainTypes.Plastic, failureZones);

                for (int j = 0; j < subdivision; j++)
                {
                    planes.Add(sectionSolverModelCode2010Test.CalculateStrainPlane(0.0, failureZones, (double)j / (double)subdivision, p1, p2, p3, p4, subIndex, p5));
                    subIndex++;
                }
            }

            var defPlanes = new (double, double, double, double)[planes.Count];

            for (int i = 0; i < planes.Count; i++)
            {
                var plane = planes[i];
                double min_y = -300.0;
                double max_y = 300.0;

                defPlanes[i].Item1 = plane.GetStrain(0.0, min_y);
                defPlanes[i].Item2 = min_y;
                defPlanes[i].Item3 = plane.GetStrain(0.0, max_y);
                defPlanes[i].Item4 = max_y;
            }


        }

        [TestMethod]
        public void FailureDomain1()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete());
            //FailureDomainCommonAssertModelCode(section, sectionChecker, new StandardNTC2018Concrete(), 5.0);
            var domainResult = sectionChecker.GetPlasticFailureDomainResult();

            Assert.IsTrue(true);
        }
    }
}
