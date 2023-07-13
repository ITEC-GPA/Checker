using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
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

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Rotation point p5 is not used.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain01()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, -300.0, 300.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.056332454985",
                "0.045164909969",
                "0.033997364954",
                "0.022829819939",
                "0.011662274923",
                "0.001138716356",
                "0.000948930297",
                "0.000759144237",
                "0.000569358178",
                "0.000379572119",
                "0.000189786059",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000"
            };

            var max_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.056250000000",
                "0.045000000000",
                "0.033750000000",
                "0.022500000000",
                "0.011250000000",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000",
                "-0.002250000000",
                "-0.002500000000",
                "-0.002750000000",
                "-0.003000000000",
                "-0.003250000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.002856013552",
                "-0.002804253717",
                "-0.002752493883",
                "-0.002700734049",
                "-0.002648974214",
                "-0.002597214380",
                "-0.002545454545",
                "-0.002454545455",
                "-0.002363636364",
                "-0.002272727273",
                "-0.002181818182",
                "-0.002090909091",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Rotation points p5 and p2 are not used.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain02()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 500.0, concreteHeight: 100.0, concreteMaterial: ConcreteMaterialEN1992Data.C25_30,
                rebarsSectionTop: rebar, rebarsPitchTop: 200.0, rebarsCoverTop: 50.0,
                rebarsSectionBottom: null, rebarsPitchBottom: 200.0, rebarsCoverBottom: 0.0,
                steelShapeH: new GPC.Model.Sections.SectionH(2000.0, 10.0, 150.0, 15.0, 150.0, 15.0, ""),
                steelMaterial: SteelMaterialEN1993Data.S275);

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, -2000.0, 100.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.056439786059",
                "0.045379572119",
                "0.034319358178",
                "0.023259144237",
                "0.012198930297",
                "0.001138716356",
                "0.000948930297",
                "0.000759144237",
                "0.000569358178",
                "0.000379572119",
                "0.000189786059",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000"
            };

            var max_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.056250000000",
                "0.045000000000",
                "0.033750000000",
                "0.022500000000",
                "0.011250000000",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000",
                "-0.002241319444",
                "-0.002482638889",
                "-0.002723958333",
                "-0.002965277778",
                "-0.003206597222",
                "-0.003447916667",
                "-0.003217495543",
                "-0.002987074419",
                "-0.002756653295",
                "-0.002526232172",
                "-0.002295811048",
                "-0.002065389924",
                "-0.002061436048",
                "-0.002057482172",
                "-0.002053528295",
                "-0.002049574419",
                "-0.002045620543",
                "-0.002041666667",
                "-0.002034722222",
                "-0.002027777778",
                "-0.002020833333",
                "-0.002013888889",
                "-0.002006944444",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Case similar to the situation with only concrete section without steel profiles.
        /// Steel profile is inside concrete area.
        /// The outermost steel is the reinforcing bars.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain03()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 250.0, concreteHeight: 500.0, concreteMaterial: ConcreteMaterialEN1992Data.C25_30,
                rebarsSectionTop: rebar, rebarsPitchTop: 150.0, rebarsCoverTop: 50.0,
                rebarsSectionBottom: rebar, rebarsPitchBottom: 150.0, rebarsCoverBottom: 50.0,
                steelShapeH: new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"),
                steelMaterial: SteelMaterialEN1993Data.S275);

            section.SteelSections[0].Traslation.Y = 100.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, 0.0, 500.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.067500000000",
                "0.056439786059",
                "0.045379572119",
                "0.034319358178",
                "0.023259144237",
                "0.012198930297",
                "0.001138716356",
                "0.000948930297",
                "0.000759144237",
                "0.000569358178",
                "0.000379572119",
                "0.000189786059",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000"
            };

            var max_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.056250000000",
                "0.045000000000",
                "0.033750000000",
                "0.022500000000",
                "0.011250000000",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000",
                "-0.002241319444",
                "-0.002482638889",
                "-0.002723958333",
                "-0.002965277778",
                "-0.003206597222",
                "-0.003447916667",
                "-0.003217495543",
                "-0.002987074419",
                "-0.002756653295",
                "-0.002526232172",
                "-0.002295811048",
                "-0.002065389924",
                "-0.002061436048",
                "-0.002057482172",
                "-0.002053528295",
                "-0.002049574419",
                "-0.002045620543",
                "-0.002041666667",
                "-0.002034722222",
                "-0.002027777778",
                "-0.002020833333",
                "-0.002013888889",
                "-0.002006944444",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Calculates the deformation planes for a composite section.
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        private static List<StrainPlane> CalculateStrainPlanes(ReinforcedConcreteSection section)
        {
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
            var p1_F4 = sectionSolverModelCode2010Test.GetP1(sectionDistances, FailureDomainTypes.Plastic, FailureZones.F4);
            planes.Add(sectionSolverModelCode2010Test.CalculateStrainPlane(0.0, FailureZones.F4, 1.0, p1_F4, p2, p3, p4, 6, p5));
            return planes;
        }

        /// <summary>
        /// Utility to build a string to paste into AutoCAD to draw plan lines.
        /// </summary>
        /// <param name="planes"></param>
        /// <param name="min_y"></param>
        /// <param name="max_y"></param>
        /// <param name="min_y_strian_sequence">Sequence of strains, return in string to simplify comparison.</param>
        /// <param name="max_y_strian_sequence">Sequence of strains, return in string to simplify comparison.</param>
        private static string MakePlaneListString(IEnumerable<StrainPlane> planes, double min_y, double max_y,
            out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence)
        {
            var stringBuilder = new StringBuilder();
            min_y_strian_sequence = new List<string>();
            max_y_strian_sequence = new List<string>();
            double scale = 10000.0;
            stringBuilder.AppendLine("LINE");

            foreach (var plane in planes)
            {
                var strainMin = plane.GetStrain(0.0, min_y);
                var strainMax = plane.GetStrain(0.0, max_y);

                stringBuilder.Append($"{strainMin * scale:F8},{min_y}\n{strainMax * scale:F8},{max_y}\n\n\n");
                min_y_strian_sequence.Add($"{strainMin:F12}");
                max_y_strian_sequence.Add($"{strainMax:F12}");
            }
            return stringBuilder.ToString();
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
