using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Utilities.Extensions;
using GPC.Utilities.Maths;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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

            var sectionSolverModelCode2010Test = new SectionSolverModelCode2010(section, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(), new StandardNTC2018Concrete(), section.Centroid);
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

            var sectionSolverModelCode2010Test = new SectionSolverModelCode2010(section, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(), new StandardNTC2018Concrete(), section.Centroid);
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

            List<StrainPlane> planes = CalculateStrainPlanes(section: section, gamma_M0: 1.15);

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
        /// Rotation point p5 is not used.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain01_FRC()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialModelCode2010Data.C25_30_17, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);

            List<StrainPlane> planes = CalculateStrainPlanes(section: section, gamma_M0: 1.15);

            var stringForCad = MakePlaneListString(planes, -300.0, 300.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.016749121651",
                "0.013498243303",
                "0.010247364954",
                "0.006996486605",
                "0.003745608256",
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
                "0.010000000000",
                "0.008333333333",
                "0.006666666667",
                "0.005000000000",
                "0.003333333333",
                "0.001666666667",
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

            List<StrainPlane> planes = CalculateStrainPlanes(section: section, gamma_M0: 1.15);

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
        /// Rotation points p5 and p2 are not used.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain02_FRC()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 500.0, concreteHeight: 100.0, concreteMaterial: ConcreteMaterialModelCode2010Data.C25_30_17,
                rebarsSectionTop: rebar, rebarsPitchTop: 200.0, rebarsCoverTop: 50.0,
                rebarsSectionBottom: null, rebarsPitchBottom: 200.0, rebarsCoverBottom: 0.0,
                steelShapeH: new GPC.Model.Sections.SectionH(2000.0, 10.0, 150.0, 15.0, 150.0, 15.0, ""),
                steelMaterial: SteelMaterialEN1993Data.S275);

            List<StrainPlane> planes = CalculateStrainPlanes(section: section, gamma_M0: 1.15);

            var stringForCad = MakePlaneListString(planes, -2000.0, 100.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.016856452726",
                "0.013712905452",
                "0.010569358178",
                "0.007425810904",
                "0.004282263630",
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
                "0.010000000000",
                "0.008333333333",
                "0.006666666667",
                "0.005000000000",
                "0.003333333333",
                "0.001666666667",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000",
                "-0.002076388889",
                "-0.002152777778",
                "-0.002229166667",
                "-0.002305555556",
                "-0.002381944444",
                "-0.002458333333",
                "-0.002392842765",
                "-0.002327352197",
                "-0.002261861629",
                "-0.002196371061",
                "-0.002130880492",
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
                steelMaterial: SteelMaterialEN1993Data.S355);

            section.SteelSections[0].Traslation.Y = 100.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, 0.0, 500.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.068531659389",
                "0.069563318777",
                "0.070594978166",
                "0.071626637555",
                "0.072658296943",
                "0.073689956332",
                "0.073720524017",
                "0.073751091703",
                "0.073781659389",
                "0.073812227074",
                "0.073842794760",
                "0.073873362445",
                "0.073896288210",
                "0.073919213974",
                "0.073942139738",
                "0.073965065502",
                "0.073987991266",
                "0.074010917031",
                "0.062085247769",
                "0.050159578508",
                "0.038233909246",
                "0.026308239985",
                "0.014382570723",
                "0.002456901462",
                "0.002047417885",
                "0.001637934308",
                "0.001228450731",
                "0.000818967154",
                "0.000409483577",
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
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003250000000",
                "-0.003000000000",
                "-0.002750000000",
                "-0.002500000000",
                "-0.002250000000",
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
        public void StrainPlanesDomain03_FRC()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 250.0, concreteHeight: 500.0, concreteMaterial: ConcreteMaterialModelCode2010Data.C25_30_17,
                rebarsSectionTop: rebar, rebarsPitchTop: 150.0, rebarsCoverTop: 50.0,
                rebarsSectionBottom: rebar, rebarsPitchBottom: 150.0, rebarsCoverBottom: 50.0,
                steelShapeH: new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"),
                steelMaterial: SteelMaterialEN1993Data.S355);

            section.SteelSections[0].Traslation.Y = 100.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, 0.0, 500.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.016695379196",
                "0.013390758391",
                "0.010086137587",
                "0.006781516783",
                "0.003476895979",
                "0.000172275174",
                "0.000143562645",
                "0.000114850116",
                "0.000086137587",
                "0.000057425058",
                "0.000028712529",
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
                "0.010000000000",
                "0.008333333333",
                "0.006666666667",
                "0.005000000000",
                "0.003333333333",
                "0.001666666667",
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
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003250000000",
                "-0.003000000000",
                "-0.002750000000",
                "-0.002500000000",
                "-0.002250000000",
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
        /// The outermost steel is the H-section.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain04()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 250.0, concreteHeight: 500.0, concreteMaterial: ConcreteMaterialEN1992Data.C25_30,
                rebarsSectionTop: rebar, rebarsPitchTop: 150.0, rebarsCoverTop: 100.0,
                rebarsSectionBottom: rebar, rebarsPitchBottom: 150.0, rebarsCoverBottom: 100.0,
                steelShapeH: new GPC.Model.Sections.SectionH(400.0, 8.6, 180.0, 13.5, 180.0, 13.5, "IPE400 r=0"),
                steelMaterial: SteelMaterialEN1993Data.S275);

            section.SteelSections[0].Traslation.Y = 50.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section: section, gamma_M0: 1.15);

            var stringForCad = MakePlaneListString(planes, 0.0, 500.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.068750000000",
                "0.070000000000",
                "0.071250000000",
                "0.072500000000",
                "0.073750000000",
                "0.075000000000",
                "0.075037037037",
                "0.075074074074",
                "0.075111111111",
                "0.075148148148",
                "0.075185185185",
                "0.075222222222",
                "0.075250000000",
                "0.075277777778",
                "0.075305555556",
                "0.075333333333",
                "0.075361111111",
                "0.075388888889",
                "0.063099762288",
                "0.050810635687",
                "0.038521509087",
                "0.026232382486",
                "0.013943255885",
                "0.001654129285",
                "0.001378441070",
                "0.001102752856",
                "0.000827064642",
                "0.000551376428",
                "0.000275688214",
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
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003250000000",
                "-0.003000000000",
                "-0.002750000000",
                "-0.002500000000",
                "-0.002250000000",
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
        /// The outermost steel is the H-section.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain04_FRC()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 250.0, concreteHeight: 500.0, concreteMaterial: ConcreteMaterialModelCode2010Data.C25_30_17,
                rebarsSectionTop: rebar, rebarsPitchTop: 150.0, rebarsCoverTop: 100.0,
                rebarsSectionBottom: rebar, rebarsPitchBottom: 150.0, rebarsCoverBottom: 100.0,
                steelShapeH: new GPC.Model.Sections.SectionH(400.0, 8.6, 180.0, 13.5, 180.0, 13.5, "IPE400 r=0"),
                steelMaterial: SteelMaterialEN1993Data.S275);

            section.SteelSections[0].Traslation.Y = 50.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section: section, gamma_M0: 1.15);

            var stringForCad = MakePlaneListString(planes, 0.0, 500.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.016695379196",
                "0.013390758391",
                "0.010086137587",
                "0.006781516783",
                "0.003476895979",
                "0.000172275174",
                "0.000143562645",
                "0.000114850116",
                "0.000086137587",
                "0.000057425058",
                "0.000028712529",
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
                "0.010000000000",
                "0.008333333333",
                "0.006666666667",
                "0.005000000000",
                "0.003333333333",
                "0.001666666667",
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
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003250000000",
                "-0.003000000000",
                "-0.002750000000",
                "-0.002500000000",
                "-0.002250000000",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Rotation point p5 is not used, instead are used p2 and p3.
        /// H-shaped section over concrete.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain05()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);

            section.SteelSections[0].Traslation.Y = 300.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, 0.0, 600.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.068346774194",
                "0.069193548387",
                "0.070040322581",
                "0.070887096774",
                "0.071733870968",
                "0.072580645161",
                "0.072605734767",
                "0.072630824373",
                "0.072655913978",
                "0.072681003584",
                "0.072706093190",
                "0.072731182796",
                "0.073785675586",
                "0.074840168375",
                "0.075894661165",
                "0.076949153955",
                "0.078003646745",
                "0.079058139535",
                "0.066355915066",
                "0.053653690597",
                "0.040951466127",
                "0.028249241658",
                "0.015547017189",
                "0.002844792720",
                "0.002370660600",
                "0.001896528480",
                "0.001422396360",
                "0.000948264240",
                "0.000474132120",
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
                "-0.016009689922",
                "-0.030019379845",
                "-0.044029069767",
                "-0.058038759690",
                "-0.072048449612",
                "-0.086058139535",
                "-0.073355915066",
                "-0.060653690597",
                "-0.047951466127",
                "-0.035249241658",
                "-0.022547017189",
                "-0.009844792720",
                "-0.009370660600",
                "-0.008896528480",
                "-0.008422396360",
                "-0.007948264240",
                "-0.007474132120",
                "-0.007000000000",
                "-0.006166666667",
                "-0.005333333333",
                "-0.004500000000",
                "-0.003666666667",
                "-0.002833333333",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Rotation point p5 is not used, instead are used p2 and p3.
        /// H-shaped section over concrete.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain05_FRC()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialModelCode2010Data.C25_30_17, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);

            section.SteelSections[0].Traslation.Y = 300.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, 0.0, 600.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.016695379196",
                "0.013390758391",
                "0.010086137587",
                "0.006781516783",
                "0.003476895979",
                "0.000172275174",
                "0.000143562645",
                "0.000114850116",
                "0.000086137587",
                "0.000057425058",
                "0.000028712529",
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
                "0.010000000000",
                "0.008333333333",
                "0.006666666667",
                "0.005000000000",
                "0.003333333333",
                "0.001666666667",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000",
                "-0.006166666667",
                "-0.010333333333",
                "-0.014500000000",
                "-0.018666666667",
                "-0.022833333333",
                "-0.027000000000",
                "-0.023695379196",
                "-0.020390758391",
                "-0.017086137587",
                "-0.013781516783",
                "-0.010476895979",
                "-0.007172275174",
                "-0.007143562645",
                "-0.007114850116",
                "-0.007086137587",
                "-0.007057425058",
                "-0.007028712529",
                "-0.007000000000",
                "-0.006166666667",
                "-0.005333333333",
                "-0.004500000000",
                "-0.003666666667",
                "-0.002833333333",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Rotation points p5, p2 and p3 are used.
        /// H-shaped section over concrete.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain06()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(500.0, 100.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, null, 200.0,
                new GPC.Model.Sections.SectionH(2000.0, 10.0, 150.0, 15.0, 150.0, 15.0, ""), SteelMaterialEN1993Data.S275);

            section.SteelSections[0].Traslation.Y = 100.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, 0.0, 2100.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.067729591837",
                "0.067959183673",
                "0.068188775510",
                "0.068418367347",
                "0.068647959184",
                "0.068877551020",
                "0.068884353741",
                "0.068891156463",
                "0.068897959184",
                "0.068904761905",
                "0.068911564626",
                "0.068918367347",
                "0.069421768707",
                "0.069925170068",
                "0.070428571429",
                "0.070931972789",
                "0.071435374150",
                "0.071938775510",
                "0.060791925466",
                "0.049645075421",
                "0.038498225377",
                "0.027351375333",
                "0.016204525288",
                "0.005057675244",
                "0.003236415684",
                "0.002589132547",
                "0.001941849411",
                "0.001294566274",
                "0.000647283137",
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
                "-0.026666666667",
                "-0.051333333333",
                "-0.076000000000",
                "-0.100666666667",
                "-0.125333333333",
                "-0.150000000000",
                "-0.150000000000",
                "-0.150000000000",
                "-0.150000000000",
                "-0.150000000000",
                "-0.150000000000",
                "-0.150000000000",
                "-0.138228313686",
                "-0.125282650949",
                "-0.112336988211",
                "-0.099391325474",
                "-0.086445662737",
                "-0.073500000000",
                "-0.061583333333",
                "-0.049666666667",
                "-0.037750000000",
                "-0.025833333333",
                "-0.013916666667",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Rotation points p5, p2 and p3 are used.
        /// H-shaped section over concrete.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain06_FRC()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(500.0, 100.0, ConcreteMaterialModelCode2010Data.C25_30_17, rebar, 200.0, 50.0, null, 200.0,
                new GPC.Model.Sections.SectionH(2000.0, 10.0, 150.0, 15.0, 150.0, 15.0, ""), SteelMaterialEN1993Data.S275);

            section.SteelSections[0].Traslation.Y = 100.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, 0.0, 2100.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.004519629777",
                "0.003120917122",
                "0.002383756635",
                "0.001646596148",
                "0.000909435661",
                "0.000172275174",
                "0.000143562645",
                "0.000114850116",
                "0.000086137587",
                "0.000057425058",
                "0.000028712529",
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
                "0.010000000000",
                "0.008333333333",
                "0.006666666667",
                "0.005000000000",
                "0.003333333333",
                "0.001666666667",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000",
                "-0.026666666667",
                "-0.051333333333",
                "-0.076000000000",
                "-0.100666666667",
                "-0.125333333333",
                "-0.150000000000",
                "-0.150000000000",
                "-0.135918342432",
                "-0.121175132695",
                "-0.106431922958",
                "-0.091688713222",
                "-0.076945503485",
                "-0.076371252904",
                "-0.075797002324",
                "-0.075222751743",
                "-0.074648501162",
                "-0.074074250581",
                "-0.073500000000",
                "-0.061583333333",
                "-0.049666666667",
                "-0.037750000000",
                "-0.025833333333",
                "-0.013916666667",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Rotation points p5 and p3 are used.
        /// H-shaped section over concrete.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain07()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(500.0, 100.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, null, 200.0,
                new GPC.Model.Sections.SectionH(2000.0, 10.0, 150.0, 15.0, 150.0, 15.0, ""),
                new SteelMaterialEN1993("S275", 210000, 275, 430, 0.03, SteelMaterial.StressStrainCurveType.ElasticHardening, SteelMaterial.SteelTypes.Structural));

            section.SteelSections[0].Traslation.Y = 100.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, 0.0, 2100.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.030000000000",
                "0.030102040816",
                "0.030204081633",
                "0.030306122449",
                "0.030408163265",
                "0.030510204082",
                "0.030612244898",
                "0.030619047619",
                "0.030625850340",
                "0.030632653061",
                "0.030639455782",
                "0.030646258503",
                "0.030653061224",
                "0.030748299320",
                "0.030843537415",
                "0.030938775510",
                "0.031034013605",
                "0.031129251701",
                "0.031224489796",
                "0.026455190772",
                "0.021685891748",
                "0.016916592724",
                "0.012147293700",
                "0.007377994676",
                "0.002608695652",
                "0.001971115841",
                "0.001333536029",
                "0.000695956218",
                "0.000058376406",
                "-0.000579203405",
                "-0.001216783217",
                "-0.001347319347",
                "-0.001477855478",
                "-0.001608391608",
                "-0.001738927739",
                "-0.001869463869",
                "-0.002000000000"
            };

            var max_y_strian_sequence_result = new List<string>()
            {
                "0.030000000000",
                "0.025000000000",
                "0.020000000000",
                "0.015000000000",
                "0.010000000000",
                "0.005000000000",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000",
                "-0.006666666667",
                "-0.011333333333",
                "-0.016000000000",
                "-0.020666666667",
                "-0.025333333333",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.025333333333",
                "-0.020666666667",
                "-0.016000000000",
                "-0.011333333333",
                "-0.006666666667",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Rotation points p5 and p3 are used.
        /// H-shaped section over concrete.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain07_FRC()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(500.0, 100.0, ConcreteMaterialModelCode2010Data.C25_30_17, rebar, 200.0, 50.0, null, 200.0,
                new GPC.Model.Sections.SectionH(2000.0, 10.0, 150.0, 15.0, 150.0, 15.0, ""),
                new SteelMaterialEN1993("S275", 210000, 275, 430, 0.03, SteelMaterial.StressStrainCurveType.ElasticHardening, SteelMaterial.SteelTypes.Structural));

            section.SteelSections[0].Traslation.Y = 100.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, 0.0, 2100.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.016695379196",
                "0.013390758391",
                "0.010086137587",
                "0.006781516783",
                "0.003476895979",
                "0.000172275174",
                "-0.000059234558",
                "-0.000290744289",
                "-0.000522254021",
                "-0.000753763753",
                "-0.000985273485",
                "-0.001216783217",
                "-0.001347319347",
                "-0.001477855478",
                "-0.001608391608",
                "-0.001738927739",
                "-0.001869463869",
                "-0.002000000000"
            };

            var max_y_strian_sequence_result = new List<string>()
            {
                "0.010000000000",
                "0.008333333333",
                "0.006666666667",
                "0.005000000000",
                "0.003333333333",
                "0.001666666667",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000",
                "-0.006666666667",
                "-0.011333333333",
                "-0.016000000000",
                "-0.020666666667",
                "-0.025333333333",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.030000000000",
                "-0.025333333333",
                "-0.020666666667",
                "-0.016000000000",
                "-0.011333333333",
                "-0.006666666667",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Concrete is inside a RHS.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain08()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 300.0, concreteHeight: 300.0, concreteMaterial: ConcreteMaterialEN1992Data.C25_30,
                rebarsSectionTop: rebar, rebarsPitchTop: 100.0, rebarsCoverTop: 50.0,
                rebarsSectionBottom: rebar, rebarsPitchBottom: 100.0, rebarsCoverBottom: 50.0,
                steelShapeH: new GPC.Model.Sections.SectionH(2000.0, 10.0, 150.0, 15.0, 150.0, 15.0, ""),
                steelMaterial: SteelMaterialEN1993Data.S275);

            section.SteelSections.Clear();
            section.SteelSections.Add(new SteelSectionPosition(new SteelSection(
                new SectionRHS(320.0, 320.0, 10.0, 10.0, 10.0, 10.0, "")
                , SteelMaterialEN1993Data.S235), Point2d.Origin, 0.0, new Vector2d(-10.0, -10.0)));

            List<StrainPlane> planes = CalculateStrainPlanes(section: section, gamma_M0: 1.15);

            var stringForCad = MakePlaneListString(planes, -10.0, 310.0,
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
                "0.056412180814",
                "0.045324361629",
                "0.034236542443",
                "0.023148723257",
                "0.012060904072",
                "0.000973084886",
                "0.000797124544",
                "0.000621164202",
                "0.000445203860",
                "0.000269243518",
                "0.000102981398",
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
                "-0.002631720430",
                "-0.003263440860",
                "-0.003895161290",
                "-0.004526881720",
                "-0.005158602151",
                "-0.005790322581",
                "-0.005432650994",
                "-0.005074979407",
                "-0.004717307821",
                "-0.004359636234",
                "-0.004001964647",
                "-0.003644293061",
                "-0.003638616921",
                "-0.003632940781",
                "-0.003627264641",
                "-0.003621588501",
                "-0.003606214139",
                "-0.003527559055",
                "-0.003272965879",
                "-0.003018372703",
                "-0.002763779528",
                "-0.002509186352",
                "-0.002254593176",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Concrete is inside a RHS.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain08_FRC()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 300.0, concreteHeight: 300.0, concreteMaterial: ConcreteMaterialModelCode2010Data.C25_30_17,
                rebarsSectionTop: rebar, rebarsPitchTop: 100.0, rebarsCoverTop: 50.0,
                rebarsSectionBottom: rebar, rebarsPitchBottom: 100.0, rebarsCoverBottom: 50.0,
                steelShapeH: new GPC.Model.Sections.SectionH(2000.0, 10.0, 150.0, 15.0, 150.0, 15.0, ""),
                steelMaterial: SteelMaterialEN1993Data.S275);

            section.SteelSections.Clear();
            section.SteelSections.Add(new SteelSectionPosition(new SteelSection(
                new SectionRHS(320.0, 320.0, 10.0, 10.0, 10.0, 10.0, "")
                , SteelMaterialEN1993Data.S235), Point2d.Origin, 0.0, new Vector2d(-10.0, -10.0)));

            List<StrainPlane> planes = CalculateStrainPlanes(section: section, gamma_M0: 1.15);

            var stringForCad = MakePlaneListString(planes, -10.0, 310.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.016715780724",
                "0.013431561449",
                "0.010147342173",
                "0.006863122898",
                "0.003578903622",
                "0.000294684347",
                "0.000231790761",
                "0.000168897176",
                "0.000110426065",
                "0.000073617376",
                "0.000036808688",
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
                "0.010000000000",
                "0.008333333333",
                "0.006666666667",
                "0.005000000000",
                "0.003333333333",
                "0.001666666667",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000",
                "-0.002376344086",
                "-0.002752688172",
                "-0.003129032258",
                "-0.003505376344",
                "-0.003881720430",
                "-0.004258064516",
                "-0.004152121959",
                "-0.004046179402",
                "-0.003940236844",
                "-0.003834294287",
                "-0.003728351730",
                "-0.003622409172",
                "-0.003620380347",
                "-0.003618351522",
                "-0.003611900223",
                "-0.003583786500",
                "-0.003555672778",
                "-0.003527559055",
                "-0.003272965879",
                "-0.003018372703",
                "-0.002763779528",
                "-0.002509186352",
                "-0.002254593176",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Concrete is between two ractangular plates.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain09()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 300.0, concreteHeight: 300.0, concreteMaterial: ConcreteMaterialEN1992Data.C25_30,
                rebarsSectionTop: rebar, rebarsPitchTop: 100.0, rebarsCoverTop: 50.0,
                rebarsSectionBottom: rebar, rebarsPitchBottom: 100.0, rebarsCoverBottom: 50.0,
                steelShapeH: new GPC.Model.Sections.SectionH(2000.0, 10.0, 150.0, 15.0, 150.0, 15.0, ""),
                steelMaterial: SteelMaterialEN1993Data.S275);

            section.SteelSections.Clear();
            section.SteelSections.Add(new SteelSectionPosition(new SteelSection(
                new SectionRectangular(10.0, 300.0), SteelMaterialEN1993Data.S235), Point2d.Origin, 0.0, new Vector2d(0.0, -10.0)));
            section.SteelSections.Add(new SteelSectionPosition(new SteelSection(
                new SectionRectangular(10.0, 300.0), SteelMaterialEN1993Data.S235), Point2d.Origin, 0.0, new Vector2d(0.0, 300.0)));

            List<StrainPlane> planes = CalculateStrainPlanes(section: section, gamma_M0: 1.15);

            var stringForCad = MakePlaneListString(planes, -10.0, 310.0,
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
                "0.056412180814",
                "0.045324361629",
                "0.034236542443",
                "0.023148723257",
                "0.012060904072",
                "0.000973084886",
                "0.000797124544",
                "0.000621164202",
                "0.000445203860",
                "0.000269243518",
                "0.000102981398",
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
                "-0.002631720430",
                "-0.003263440860",
                "-0.003895161290",
                "-0.004526881720",
                "-0.005158602151",
                "-0.005790322581",
                "-0.005432650994",
                "-0.005074979407",
                "-0.004717307821",
                "-0.004359636234",
                "-0.004001964647",
                "-0.003644293061",
                "-0.003638616921",
                "-0.003632940781",
                "-0.003627264641",
                "-0.003621588501",
                "-0.003606214139",
                "-0.003527559055",
                "-0.003272965879",
                "-0.003018372703",
                "-0.002763779528",
                "-0.002509186352",
                "-0.002254593176",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Concrete is between two ractangular plates.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain09_FRC()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 300.0, concreteHeight: 300.0, concreteMaterial: ConcreteMaterialModelCode2010Data.C25_30_17,
                rebarsSectionTop: rebar, rebarsPitchTop: 100.0, rebarsCoverTop: 50.0,
                rebarsSectionBottom: rebar, rebarsPitchBottom: 100.0, rebarsCoverBottom: 50.0,
                steelShapeH: new GPC.Model.Sections.SectionH(2000.0, 10.0, 150.0, 15.0, 150.0, 15.0, ""),
                steelMaterial: SteelMaterialEN1993Data.S275);

            section.SteelSections.Clear();
            section.SteelSections.Add(new SteelSectionPosition(new SteelSection(
                new SectionRectangular(10.0, 300.0), SteelMaterialEN1993Data.S235), Point2d.Origin, 0.0, new Vector2d(0.0, -10.0)));
            section.SteelSections.Add(new SteelSectionPosition(new SteelSection(
                new SectionRectangular(10.0, 300.0), SteelMaterialEN1993Data.S235), Point2d.Origin, 0.0, new Vector2d(0.0, 300.0)));

            List<StrainPlane> planes = CalculateStrainPlanes(section: section, gamma_M0: 1.15);

            var stringForCad = MakePlaneListString(planes, -10.0, 310.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.010000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.020000000000",
                "0.016715780724",
                "0.013431561449",
                "0.010147342173",
                "0.006863122898",
                "0.003578903622",
                "0.000294684347",
                "0.000231790761",
                "0.000168897176",
                "0.000110426065",
                "0.000073617376",
                "0.000036808688",
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
                "0.010000000000",
                "0.008333333333",
                "0.006666666667",
                "0.005000000000",
                "0.003333333333",
                "0.001666666667",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.001000000000",
                "-0.001333333333",
                "-0.001666666667",
                "-0.002000000000",
                "-0.002376344086",
                "-0.002752688172",
                "-0.003129032258",
                "-0.003505376344",
                "-0.003881720430",
                "-0.004258064516",
                "-0.004152121959",
                "-0.004046179402",
                "-0.003940236844",
                "-0.003834294287",
                "-0.003728351730",
                "-0.003622409172",
                "-0.003620380347",
                "-0.003618351522",
                "-0.003611900223",
                "-0.003583786500",
                "-0.003555672778",
                "-0.003527559055",
                "-0.003272965879",
                "-0.003018372703",
                "-0.002763779528",
                "-0.002509186352",
                "-0.002254593176",
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
        private static List<StrainPlane> CalculateStrainPlanes(ReinforcedConcreteSection section,
            SectionSolver.FailureDomainTypes analysisType = SectionSolver.FailureDomainTypes.Plastic,
            double rotationAngle = 0.0, double gamma_M0 = 1.0)
        {
            (SectionSolver.FailureZones, int)[] plasticZones;

            if (analysisType == SectionSolver.FailureDomainTypes.Plastic)
            {
                plasticZones = new (SectionSolver.FailureZones, int)[]
                {
                    (SectionSolver.FailureZones.F1, 5),
                    (SectionSolver.FailureZones.F2A, 5),
                    (SectionSolver.FailureZones.F2B, 5),
                    (SectionSolver.FailureZones.F3A, 5),
                    (SectionSolver.FailureZones.F3B, 5),
                    (SectionSolver.FailureZones.F4, 5)
                };
            }
            else if (analysisType == SectionSolver.FailureDomainTypes.Elastic)
            {
                plasticZones = new (SectionSolver.FailureZones, int)[]
                {
                    (SectionSolver.FailureZones.F1, 2),
                    (SectionSolver.FailureZones.F2A, 10),
                    (SectionSolver.FailureZones.F3A, 15),
                    (SectionSolver.FailureZones.F4, 5)
                };
            }
            else
                return null;

            var structuralSteelCode = new StandardEN1993p11
            {
                GammaM0 = gamma_M0
            };

            var sectionSolverModelCode2010Test = new SectionSolverModelCode2010(section, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(), new StandardNTC2018Concrete(), section.Centroid,
                false, -1, structuralSteelCode);
            var sectionDistances = sectionSolverModelCode2010Test.CalculateMaxMinSectionDistances(rotationAngle);
            var p2 = sectionSolverModelCode2010Test.GetP2(sectionDistances, analysisType);
            var p3 = sectionSolverModelCode2010Test.GetP3(sectionDistances, analysisType);
            var p4 = sectionSolverModelCode2010Test.GetP4(sectionDistances, analysisType);
            var p5 = sectionSolverModelCode2010Test.GetP5(sectionDistances, analysisType);
            var p6 = sectionSolverModelCode2010Test.GetP6(sectionDistances, analysisType);

            var planes = new List<StrainPlane>();

            for (int i = 0; i < plasticZones.Length; i++)
            {
                SectionSolver.FailureZones failureZones = plasticZones[i].Item1;
                int subdivision = plasticZones[i].Item2 + 1;
                int subIndex = 0;

                var p1 = sectionSolverModelCode2010Test.GetP1(sectionDistances, analysisType, failureZones);

                for (int j = 0; j < subdivision; j++)
                {
                    planes.Add(sectionSolverModelCode2010Test.CalculateStrainPlane(rotationAngle, failureZones,
                        (double)j / (double)subdivision, p1, p2, p3, p4, p5, p6, subIndex));
                    subIndex++;
                }
            }
            var p1_F4 = sectionSolverModelCode2010Test.GetP1(sectionDistances, analysisType, SectionSolver.FailureZones.F4);
            planes.Add(sectionSolverModelCode2010Test.CalculateStrainPlane(rotationAngle,
                SectionSolver.FailureZones.F4, 1.0, p1_F4, p2, p3, p4, p5, p6, 6));
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

        /// <summary>
        /// Utility to build a string to paste into AutoCAD to draw plan lines.
        /// </summary>
        /// <param name="planes"></param>
        /// <param name="min_x"></param>
        /// <param name="max_x"></param>
        /// <param name="min_y_strian_sequence">Sequence of strains, return in string to simplify comparison.</param>
        /// <param name="max_y_strian_sequence">Sequence of strains, return in string to simplify comparison.</param>
        private static string MakePlaneListStringX(IEnumerable<StrainPlane> planes, double min_x, double max_x,
            out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence)
        {
            var stringBuilder = new StringBuilder();
            min_y_strian_sequence = new List<string>();
            max_y_strian_sequence = new List<string>();
            double scale = 10000.0;
            stringBuilder.AppendLine("LINE");

            foreach (var plane in planes)
            {
                var strainMin = plane.GetStrain(min_x, 0.0);
                var strainMax = plane.GetStrain(max_x, 0.0);

                stringBuilder.Append($"{min_x},{strainMin * scale:F8}\n{max_x},{strainMax * scale:F8}\n\n\n");
                min_y_strian_sequence.Add($"{strainMin:F12}");
                max_y_strian_sequence.Add($"{strainMax:F12}");
            }
            return stringBuilder.ToString();
        }

        // Test on plastic domain generation.
        [TestMethod]
        public void FailureDomain01()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var structuralSteel = SteelMaterialEN1993Data.S275;
            structuralSteel.StressStrainCurve = SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic;
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), structuralSteel, 50.0);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete(),
                false, new StandardEN1993p11());
            FailureDomainCommonAssertModelCode(section, sectionChecker, new StandardNTC2018Concrete(), 5.0, false, new StandardEN1993p11());
            //var domainResult = sectionChecker.GetPlasticFailureDomainResult();

            Assert.IsTrue(true);
        }

        // The verifier does not allow to define only a steel section.
        // Then create a minimum reinforced concrete section and at its center of gravity place the steel section.
        [TestMethod]
        public void FailureDomain02()
        {
            // Geometry, material and section.
            double clsSize = 2.0;
            var rebar = new RebarSectionCircular("", 0.5 * clsSize, SteelMaterialEN1992Data.B450C);
            var structuralSteel = SteelMaterialEN1993Data.S275;
            structuralSteel.StressStrainCurve = SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic;
            var section = new ReinforcedConcreteSection(clsSize, clsSize, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 0.5 * clsSize, null, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), structuralSteel, 0.5 * clsSize);

            section.SteelSections[0].Traslation.X = -75.0 + 0.5 * clsSize;
            section.SteelSections[0].Traslation.Y = -150.0 + 0.5 * clsSize;
            section.SteelSections[0].IsInsideConcrete = false;
            var cs = GetLocalCoordinateSystem(section);

            // Code, solver and checker.
            var standard = new StandardNTC2018Concrete();
            var standardSteel = new StandardEN1993p11();
            var sectionChecker = GetSectionCheckerModelCode2010(section, standard, false, standardSteel);
            sectionChecker.SectionCheckerOptionsModelCode2010.FailureAnalysisType = SectionSolver.FailureAnalysisTypes.ConstantN;
            sectionChecker.SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem = cs;

            // ****************************************
            // Common checks.
            FailureDomainCommonAssertModelCode(section, sectionChecker, standard, 5.0, false, standardSteel);

            // ****************************************
            // ***** Elastic
            // Every point in absolute value, belongs to a polygon passing through three points.
            {
                var elasticDomainResult = sectionChecker.GetElasticFailureDomainResult();
                double NMax = 5188.06 * 275.0;
                double MxMax = 533265.7964 * 275.0;
                double MyMax = 80360.79334 * 275.0;
                var domainN_Max = new Point3d(0.0, 0.0, NMax);
                var domainMx_Max = new Point3d(MxMax, 0.0, 0.0);
                var domainMy_Max = new Point3d(0.0, MyMax, 0.0);

                double relativeError = 0.00000001;
                double absoluteError = Math.Sqrt(NMax * NMax + MxMax * MxMax + MyMax * MyMax) * relativeError;

                var domainFacePoint = new Point3d[]
                {
                domainN_Max, domainMx_Max, domainMy_Max
                };
                var domainFace = new Polygon3d(domainFacePoint);

                foreach (var pointList in elasticDomainResult.Domain.DomainPoints)
                    foreach (var point in pointList)
                    {
                        var pointAbs = new Point3d(Math.Abs(point.ForceTuple.Mx), Math.Abs(point.ForceTuple.My), Math.Abs(point.ForceTuple.N));
                        bool isOnEdges = domainFace.IsPointOnEdge(pointAbs, absoluteError) != -1;
                        bool isOnFace = domainFace.IsPointInside(pointAbs, absoluteError);
                        Assert.IsTrue(isOnEdges || isOnFace);
                    }
            }

            // ****************************************
            // ***** Plastic - Part 1
            // Domain point obtained from UNI EN 1993-1-1:2005 - §6.2.9.1 (5)
            var ec3domainPoints = new List<Point3d>()
            {
                new Point3d(165577054.225, 0, 0),
                new Point3d(165577054.225, 0, -71335.825),
                new Point3d(165577054.225, 0, -142671.65),
                new Point3d(165577054.225, 0, -214007.475),
                new Point3d(163661358.350391, 0, -285343.3),
                new Point3d(153432523.453492, 0, -356679.125),
                new Point3d(143203688.556592, 0, -428014.95),
                new Point3d(132974853.659693, 0, -499350.775),
                new Point3d(122746018.762793, 0, -570686.6),
                new Point3d(112517183.865894, 0, -642022.425),
                new Point3d(102288348.968994, 0, -713358.25),
                new Point3d(92059514.072095, 0, -784694.075),
                new Point3d(81830679.1751956, 0, -856029.9),
                new Point3d(71601844.2782961, 0, -927365.725),
                new Point3d(61373009.3813967, 0, -998701.55),
                new Point3d(51144174.4844972, 0, -1070037.375),
                new Point3d(40915339.5875977, 0, -1141373.2),
                new Point3d(30686504.6906983, 0, -1212709.025),
                new Point3d(20457669.7937988, 0, -1284044.85),
                new Point3d(10228834.8968994, 0, -1355380.675), // 13% error...
                // new Point3d(0, 0, -1426716.5) // Not converge.
            };

            var forces = ec3domainPoints.Select(p => new ResultBeamForces(p.Z, 0, 0, 0, p.X, p.Y, cs)).ToArray();

            for (int i = 0; i < forces.Length; i++)
                CommonAssertDomainPointMethod(section, forces[i], standard, sectionChecker.SectionCheckerOptionsModelCode2010,
                    0.005, factor: new double[] { 1.0 }, standardStructuralSteel: standardSteel);

            // ****************************************
            // ***** Plastic - Part 2
            // Check ratio of some values obtained from EC.
            var plasticDomainResult = sectionChecker.GetPlasticFailureDomainResult();

            var maxErrorConstantN_iterativeMethod = new List<double>();
            var maxErrorConstantEccentricity_iterativeMethod = new List<double>();

            foreach (var appliedForce in forces)
            {
                var resDomFail = sectionChecker.CalculatePlasticFailureDomainPoint(appliedForce);
                //var fr = resDomFail.StrainPlane;
                resDomFail.CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes.ConstantN, appliedForce, 1.0, 1.0);
                maxErrorConstantN_iterativeMethod.Add(resDomFail.WorkingRatio);

                resDomFail.CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, appliedForce, 1.0, 1.0);
                maxErrorConstantEccentricity_iterativeMethod.Add(resDomFail.WorkingRatio);
            }
            Assert.AreEqual(0.0, maxErrorConstantN_iterativeMethod.Max(r => Math.Abs(r - 1.0)), 0.07); // 2023-08-21 Max error: 0.0653987358653636.
            Assert.AreEqual(0.0, maxErrorConstantEccentricity_iterativeMethod.Max(r => Math.Abs(r - 1.0)), 0.07); // 2023-08-21 Max error: 0.065398517413020052.

            // ****************************************
            // ***** Plastic - Part 3
            // Check ratio of some values obtained from EC with mesh intersection.
            var maxErrorConstantEccentricity_intersectionMethod = new List<double>();
            var plastiDomainMesh = plasticDomainResult.Domain.GetMesh(plasticDomainResult.Domain, out _);

            foreach (var appliedForce in forces)
            {
                var origin = Point3d.Origin;
                var appliedForcePoint = new Point3d(appliedForce.M1, appliedForce.M2, appliedForce.N);
                var inters = plastiDomainMesh.GetIntersectionWihtSemiInfiniteRay(
                    new Line3d(origin, appliedForcePoint),
                    true,
                    10.0);

                Assert.IsTrue(inters.Count > 0);

                // Find the key with the smallest distance and get the corresponding pair from the dictionary.
                var closestEntry = inters.OrderBy(pair => pair.Key.DistanceTo(origin)).FirstOrDefault();

                Assert.IsNotNull(closestEntry);
                Assert.IsNotNull(closestEntry.Key);

                double workingRatio = origin.DistanceTo(appliedForcePoint) / origin.DistanceTo(closestEntry.Key);
                maxErrorConstantEccentricity_intersectionMethod.Add(workingRatio);
            }
            // 2023-08-21 Max error: 0.059988981343899406.
            // 2023-09-13 Max error: 0.0615187864695554.
            Assert.AreEqual(0.0, maxErrorConstantEccentricity_intersectionMethod.Max(r => Math.Abs(r - 1.0)), 0.062);
        }

        // Now two almost identical sections.
        // The composite section is the same as the RC section with a small rectangular steel section, which is irrelevant to the results.
        // The test wants to check that for a small change in the cross section there is a small change in the domain result.
        // Then compare domain points.
        [TestMethod]
        public void FailureDomain03()
        {
            // Composite section
            double clsSize = 300.0;
            double steelFy = 500.0;
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var structuralSteel = new SteelMaterialEN1993("S500", 210000, steelFy, 600, 0.15, SteelMaterial.StressStrainCurveType.ElasticHardening, SteelMaterial.SteelTypes.Structural); ;
            structuralSteel.StressStrainCurve = SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic;
            var sectionComposite = new ReinforcedConcreteSection(clsSize, clsSize, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), structuralSteel, 50.0);

            double steelSize = 1.0;
            sectionComposite.SteelSections.Clear();
            sectionComposite.SteelSections.Add(
                new SteelSectionPosition(
                    new SteelSection(new SectionRectangular(steelSize, steelSize), structuralSteel),
                    Point2d.Origin, 0.0, new Vector2d(149.5, 149.5)));
            sectionComposite.SteelSections[0].IsInsideConcrete = false;

            // Reinforced concrete section
            var sectionRC = new ReinforcedConcreteSection(clsSize, clsSize, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), structuralSteel, 50.0);

            sectionRC.SteelSections.Clear();

            // Calc domains
            SectionCheckerModelCode2010 sectionCheckerComposite = GetSectionCheckerModelCode2010(sectionComposite, new StandardNTC2018Concrete(), false, new StandardEN1993p11());
            FailureDomainCommonAssertModelCode(sectionComposite, sectionCheckerComposite, new StandardNTC2018Concrete(), 5.0, false, new StandardEN1993p11());
            var plasticDomainResultComposite = sectionCheckerComposite.GetPlasticFailureDomainResult();
            SectionCheckerModelCode2010 sectionCheckerRC = GetSectionCheckerModelCode2010(sectionRC, new StandardNTC2018Concrete(), false, new StandardEN1993p11());
            FailureDomainCommonAssertModelCode(sectionRC, sectionCheckerRC, new StandardNTC2018Concrete(), 5.0, false, new StandardEN1993p11());
            var plasticDomainResultRC = sectionCheckerRC.GetPlasticFailureDomainResult();

            // 2d -> 1d
            double Mscale = 1000000.0;
            double Nscale = 1000.0;
            int domSize0 = plasticDomainResultComposite.Domain.DomainPoints.GetLength(0);
            int subdivision = 10; // Number of subdivisions to make the test faster.
            int domStep0 = domSize0 / subdivision;

            // Stopwatch
            var stopwatch = new Stopwatch();

            ////// ************ Compare 1 - Trick, two domains are almost the same. ************
            ////stopwatch.Start(); // *** timer ***

            //for (int i = 0; i < domSize0; i += domStep0)
            //{
            //    int domSize1 = plasticDomainResultComposite.Domain.DomainPoints[i].GetLength(0);

            //    for (int j = 0; j < domSize1; j++)
            //    {
            //        var currCompositeForce = plasticDomainResultComposite.Domain.DomainPoints[i][j];

            //        var NrdCorrection = Math.Sign(currCompositeForce.NRd) * steelSize * steelSize * steelFy; // due to steel structural section
            //        var currCompositeForcePoint = currCompositeForce.Point;
            //        currCompositeForcePoint.Z -= NrdCorrection;

            //        var currRCForce = plasticDomainResultRC.Domain.DomainPoints[i][j];
            //        var distanceBetweenDomanins = currCompositeForcePoint.DistanceTo(currRCForce.Point);
            //        var distanceFromOrigin = currRCForce.Point.DistanceTo(Point3d.Origin);
            //        double forceTollerance = distanceFromOrigin * forceRelativeTollerance;

            //        Assert.IsTrue(distanceBetweenDomanins < forceTollerance);
            //    }
            //}

            //stopwatch.Stop(); // *** timer ***
            //var elapsedTime = stopwatch.Elapsed;

            // ************ Compare 2 - Intersect method. ************
            stopwatch.Reset();
            stopwatch.Start(); // *** timer ***

            var maxErrorConstantEccentricity_intersectionMethod = new List<double>();
            var origin = Point3d.Origin;
            var plastiDomainMeshRC = plasticDomainResultRC.Domain.GetMesh(plasticDomainResultRC.Domain, out Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint);

            for (int i = 0; i < domSize0; i += domStep0)
            {
                int domSize1 = plasticDomainResultComposite.Domain.DomainPoints[i].GetLength(0);

                for (int j = 0; j < domSize1; j++)
                {
                    var currCompositeForce = plasticDomainResultComposite.Domain.DomainPoints[i][j];

                    var NrdCorrection = Math.Sign(currCompositeForce.NRd) * steelSize * steelSize * steelFy; // due to steel structural section
                    var currCompositeForcePoint = currCompositeForce.Point;
                    currCompositeForcePoint.Z -= NrdCorrection;
                    var resultBeamComposite = new ResultBeamForces(currCompositeForcePoint.Z, 0.0, 0.0, 0.0, currCompositeForcePoint.X, currCompositeForcePoint.Y, GetLocalCoordinateSystem(sectionRC));

                    var failComposite = sectionCheckerRC.SectionSolver.CalculateDomainPoint(resultBeamComposite, plastiDomainMeshRC, vertexToDomainPoint, sectionCheckerRC.SectionCheckerOptions);

                    maxErrorConstantEccentricity_intersectionMethod.Add(failComposite.WorkingRatio);
                }
            }

            stopwatch.Stop(); // *** timer ***

            var maxErrInters = maxErrorConstantEccentricity_intersectionMethod.Max(r => Math.Abs(r - 1.0));
            //Assert.AreEqual(0.0, maxErrInters, 0.003); // 2023-08-03 Max error: 0.0029979178148502594.
            Assert.AreEqual(0.0, maxErrInters, 0.04); // 2023-11-21 Max error: 0.0029979178148502594.
            var elapsedTimeIntersect = stopwatch.Elapsed;

            // ************ Compare 3 - Iterative method, composite points over RC domain. ************
            var sectionRClocalSystem = GetLocalCoordinateSystem(sectionRC);
            stopwatch.Reset();
            stopwatch.Start(); // *** timer ***
            var maxErrorConstantEccentricity_directMethodRC = new List<(double wratio, int iterations, double N, double Mx, double My)>();
            var failForcePointsRC = new List<Point3d>();

            for (int i = 0; i < domSize0; i += domStep0)
            {
                int domSize1 = plasticDomainResultComposite.Domain.DomainPoints[i].GetLength(0);

                for (int j = 0; j < domSize1; j++)
                {
                    var currCompositeForce = plasticDomainResultComposite.Domain.DomainPoints[i][j];

                    var NrdCorrection = Math.Sign(currCompositeForce.NRd) * steelSize * steelSize * steelFy; // due to steel structural section
                    var currCompositeForcePoint = currCompositeForce.Point;
                    currCompositeForcePoint.Z -= NrdCorrection;

                    //if (currCompositeForcePoint.Z > 0)
                    //    continue;
                    {
                        var appliedForce = new ResultBeamForces(currCompositeForcePoint.Z, 0.0, 0.0, 0.0, currCompositeForcePoint.X, currCompositeForcePoint.Y, sectionRClocalSystem);
                        var resDomFail = sectionCheckerRC.CalculatePlasticFailureDomainPoint(appliedForce);

                        if (resDomFail is null)
                        {
                            failForcePointsRC.Add(currCompositeForcePoint);
                        }
                        else
                        {
                            resDomFail.CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, appliedForce, Mscale, Nscale);
                            maxErrorConstantEccentricity_directMethodRC.Add((resDomFail.WorkingRatio, resDomFail.StrainPlane.Id, appliedForce.N, appliedForce.M1, appliedForce.M2));
                        }
                    }
                }
            }
            stopwatch.Stop(); // *** timer ***
            var elapsedTimeDirectOverRC = stopwatch.Elapsed;
            var maxErrRC = maxErrorConstantEccentricity_directMethodRC.Max(r => Math.Abs(r.wratio - 1.0));
            // 2023-09-28: Changed number of not converged from 1 to 40.
            Assert.IsTrue(failForcePointsRC.Count <= 40 / subdivision);
            Assert.IsTrue(maxErrRC < 0.005);

            // ************ Compare 3 - Iterative method, composite points over composite domain. ************
            var sectionCompositelocalSystem = GetLocalCoordinateSystem(sectionComposite);
            stopwatch.Reset();
            stopwatch.Start(); // *** timer ***
            var maxErrorConstantEccentricity_directMethodComposite = new List<(double wratio, int iterations, double N, double Mx, double My)>();
            var failForcePointsComposite = new List<Point3d>();
            domSize0 = plasticDomainResultRC.Domain.DomainPoints.GetLength(0);
            domStep0 = domSize0 / subdivision;

            for (int i = 0; i < domSize0; i += domStep0)
            {
                int domSize1 = plasticDomainResultRC.Domain.DomainPoints[i].GetLength(0);

                for (int j = 0; j < domSize1; j++)
                {
                    var currRCForce = plasticDomainResultRC.Domain.DomainPoints[i][j];

                    var NrdCorrection = Math.Sign(currRCForce.NRd) * steelSize * steelSize * steelFy; // due to steel structural section
                    var currRCForcePoint = currRCForce.Point;
                    currRCForcePoint.Z += NrdCorrection;

                    //if (currRCForcePoint.Z > 0)
                    //    continue;
                    {
                        var appliedForce = new ResultBeamForces(currRCForcePoint.Z, 0.0, 0.0, 0.0, currRCForcePoint.X, currRCForcePoint.Y, sectionCompositelocalSystem);
                        var resDomFail = sectionCheckerComposite.CalculatePlasticFailureDomainPoint(appliedForce);

                        if (resDomFail is null)
                        {
                            failForcePointsComposite.Add(currRCForcePoint);
                        }
                        else
                        {
                            resDomFail.CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, appliedForce, Mscale, Nscale);
                            maxErrorConstantEccentricity_directMethodComposite.Add((resDomFail.WorkingRatio, resDomFail.StrainPlane.Id, appliedForce.N, appliedForce.M1, appliedForce.M2));
                        }
                    }
                }
            }
            stopwatch.Stop(); // *** timer ***
            var elapsedTimeDirectOverComposite = stopwatch.Elapsed;

            var maxErrComp = maxErrorConstantEccentricity_directMethodComposite.Max(r => Math.Abs(r.wratio - 1.0));
            // 2023-09-28: Changed number of not converged from 1 to 40.
            Assert.IsTrue(failForcePointsComposite.Count <= 40 / subdivision);
            Assert.IsTrue(maxErrComp < 0.02);
        }

        /// <summary>
        /// MULTI DIRECTION OF THETA - PLASTIC.
        /// Tests the sequence of deformation planes in a composite section.
        /// Case similar to the situation with only concrete section without steel profiles.
        /// Steel profile is inside concrete area.
        /// The outermost steel is the reinforcing bars.
        /// 
        /// Compare sections with and without structural steel.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain10()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 250.0, concreteHeight: 500.0, concreteMaterial: ConcreteMaterialEN1992Data.C25_30,
                rebarsSectionTop: rebar, rebarsPitchTop: 150.0, rebarsCoverTop: 50.0,
                rebarsSectionBottom: rebar, rebarsPitchBottom: 150.0, rebarsCoverBottom: 50.0,
                steelShapeH: new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"),
                steelMaterial: SteelMaterialEN1993Data.S420);

            section.SteelSections[0].Traslation.Y = 100.0;

            // With steel section.
            CalculateStrainPlanesMultiDirection(section,
                out Dictionary<double, List<StrainPlane>> planesWithSteel,
                out Dictionary<double, List<string>> min_y_strian_sequence,
                out Dictionary<double, List<string>> max_y_strian_sequence,
                SectionSolver.FailureDomainTypes.Plastic);

            // Without steel section.
            section.SteelSections.Clear();

            CalculateStrainPlanesMultiDirection(section,
                out Dictionary<double, List<StrainPlane>> planesWithoutSteel,
                out Dictionary<double, List<string>> min_y_strian_sequence_without,
                out Dictionary<double, List<string>> max_y_strian_sequence_without,
                SectionSolver.FailureDomainTypes.Plastic);

            // Assert
            Assert.IsNotNull(planesWithSteel);
            Assert.IsNotNull(planesWithoutSteel);
            foreach (var strain in min_y_strian_sequence)
                CollectionAssert.AreEqual(strain.Value, min_y_strian_sequence_without[strain.Key]);
            foreach (var strain in max_y_strian_sequence)
                CollectionAssert.AreEqual(strain.Value, max_y_strian_sequence_without[strain.Key]);
        }

        /// <summary>
        /// MULTI DIRECTION OF THETA - ELASTIC.
        /// Tests the sequence of deformation planes in a composite section.
        /// Case similar to the situation with only concrete section without steel profiles.
        /// Steel profile is inside concrete area.
        /// The outermost steel is the reinforcing bars.
        /// 
        /// Compare sections with and without structural steel.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain11()
        {
            // Using S420 then rebars limit strain in tension.
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(
                concreteWidth: 250.0, concreteHeight: 500.0, concreteMaterial: ConcreteMaterialEN1992Data.C25_30,
                rebarsSectionTop: rebar, rebarsPitchTop: 150.0, rebarsCoverTop: 50.0,
                rebarsSectionBottom: rebar, rebarsPitchBottom: 150.0, rebarsCoverBottom: 50.0,
                steelShapeH: new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"),
                steelMaterial: SteelMaterialEN1993Data.S420);

            section.SteelSections[0].Traslation.Y = 100.0;

            // With steel section.
            CalculateStrainPlanesMultiDirection(section,
                out Dictionary<double, List<StrainPlane>> planesWithSteel,
                out Dictionary<double, List<string>> min_y_strian_sequence,
                out Dictionary<double, List<string>> max_y_strian_sequence,
                SectionSolver.FailureDomainTypes.Elastic);

            // Without steel section.
            section.SteelSections.Clear();

            CalculateStrainPlanesMultiDirection(section,
                out Dictionary<double, List<StrainPlane>> planesWithoutSteel,
                out Dictionary<double, List<string>> min_y_strian_sequence_without,
                out Dictionary<double, List<string>> max_y_strian_sequence_without,
                SectionSolver.FailureDomainTypes.Elastic);

            // Assert
            Assert.IsNotNull(planesWithSteel);
            Assert.IsNotNull(planesWithoutSteel);
            foreach (var strain in min_y_strian_sequence)
                CollectionAssert.AreEqual(strain.Value, min_y_strian_sequence_without[strain.Key]);
            foreach (var strain in max_y_strian_sequence)
                CollectionAssert.AreEqual(strain.Value, max_y_strian_sequence_without[strain.Key]);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain12()
        {
            ReinforcedConcreteSection section = GetRectangularSection4Rebars(400, 400, 14, 40, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);

            section.AddSteelSection(
                new SteelSectionPosition(
                    new SteelSection(
                        new SectionC(300.0, 8.5, 75.0, 11.5, 75.0, 11.5, "UPN r=0"),
                        SteelMaterialEN1993Data.S235
                        ),
                    Point2d.Origin,
                    0.0,
                    Point2d.Origin
                    )
                );
            section.SteelSections[0].IsInsideConcrete = false;

            List<StrainPlane> planes = CalculateStrainPlanes(
                section: section,
                analysisType: SectionSolver.FailureDomainTypes.Elastic,
                rotationAngle: 0.0,
                gamma_M0: 1.15);

            var stringForCad = MakePlaneListString(planes, 0.0, 400.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000912267081",
                "0.000851449275",
                "0.000790631470",
                "0.000729813665",
                "0.000668995859",
                "0.000608178054",
                "0.000547360248",
                "0.000486542443",
                "0.000425724638",
                "0.000364906832",
                "0.000304089027",
                "0.000243271222",
                "0.000182453416",
                "0.000121635611",
                "0.000060817805",
                "0.000000000000",
                "-0.000162180814",
                "-0.000324361629",
                "-0.000486542443",
                "-0.000648723257",
                "-0.000810904072",
                "-0.000973084886"
            };

            var max_y_strian_sequence_result = new List<string>()
            {
                "0.000973084886",
                "0.000648723257",
                "0.000324361629",
                "0.000000000000",
                "-0.000147437104",
                "-0.000294874208",
                "-0.000442311312",
                "-0.000589748416",
                "-0.000737185520",
                "-0.000884622624",
                "-0.001032059728",
                "-0.001179496832",
                "-0.001326933936",
                "-0.001474371040",
                "-0.001621808144",
                "-0.001601535542",
                "-0.001581262940",
                "-0.001560990338",
                "-0.001540717736",
                "-0.001520445135",
                "-0.001500172533",
                "-0.001479899931",
                "-0.001459627329",
                "-0.001439354727",
                "-0.001419082126",
                "-0.001398809524",
                "-0.001378536922",
                "-0.001358264320",
                "-0.001337991718",
                "-0.001317719117",
                "-0.001297446515",
                "-0.001243386243",
                "-0.001189325972",
                "-0.001135265700",
                "-0.001081205429",
                "-0.001027145158",
                "-0.000973084886"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain13()
        {
            ReinforcedConcreteSection section = GetRectangularSection4Rebars(400, 400, 14, 40, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);

            section.AddSteelSection(
                new SteelSectionPosition(
                    new SteelSection(
                        new SectionC(300.0, 8.5, 75.0, 11.5, 75.0, 11.5, "UPN r=0"),
                        SteelMaterialEN1993Data.S235
                        ),
                    Point2d.Origin,
                    0.0,
                    Point2d.Origin
                    )
                );
            section.SteelSections[0].IsInsideConcrete = false;

            List<StrainPlane> planes = CalculateStrainPlanes(
                section: section,
                analysisType: SectionSolver.FailureDomainTypes.Elastic,
                rotationAngle: (270.0).ToRadians(),
                gamma_M0: 1.15);

            var stringForCad = MakePlaneListStringX(planes, 0.0, 400.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000973084886",
                "0.000912267081",
                "0.000851449275",
                "0.000790631470",
                "0.000729813665",
                "0.000668995859",
                "0.000608178054",
                "0.000547360248",
                "0.000486542443",
                "0.000425724638",
                "0.000364906832",
                "0.000304089027",
                "0.000243271222",
                "0.000182453416",
                "0.000121635611",
                "0.000060817805",
                "0.000000000000",
                "-0.000333333333",
                "-0.000666666667",
                "-0.000785584886",
                "-0.000848084886",
                "-0.000910584886",
                "-0.000973084886"
            };

            var max_y_strian_sequence_result = new List<string>()
            {
                "0.000973084886",
                "0.000648723257",
                "0.000324361629",
                "0.000000000000",
                "-0.000181818182",
                "-0.000363636364",
                "-0.000545454545",
                "-0.000727272727",
                "-0.000909090909",
                "-0.001090909091",
                "-0.001272727273",
                "-0.001454545455",
                "-0.001636363636",
                "-0.001818181818",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.001785584886",
                "-0.001514751553",
                "-0.001243918219",
                "-0.000973084886"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain14()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var structuralSteel = SteelMaterialEN1993Data.S275;
            structuralSteel.StressStrainCurve = SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic;
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), structuralSteel, 50.0);

            List<StrainPlane> planes = CalculateStrainPlanes(
                section: section,
                analysisType: SectionSolver.FailureDomainTypes.Elastic,
                rotationAngle: 0.0,
                gamma_M0: 1.0);

            var stringForCad = MakePlaneListString(planes, -300.0, 300.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001309523810",
                "0.001227678571",
                "0.001145833333",
                "0.001063988095",
                "0.000982142857",
                "0.000900297619",
                "0.000818452381",
                "0.000736607143",
                "0.000654761905",
                "0.000572916667",
                "0.000491071429",
                "0.000409226190",
                "0.000327380952",
                "0.000245535714",
                "0.000163690476",
                "0.000081845238",
                "0.000000000000",
                "-0.000333333333",
                "-0.000642857143",
                "-0.000809523810",
                "-0.000976190476",
                "-0.001142857143",
                "-0.001309523810"
            };

            var max_y_strian_sequence_result = new List<string>()
            {
                "0.001309523810",
                "0.000873015873",
                "0.000436507937",
                "0.000000000000",
                "-0.000181818182",
                "-0.000363636364",
                "-0.000545454545",
                "-0.000727272727",
                "-0.000909090909",
                "-0.001090909091",
                "-0.001272727273",
                "-0.001454545455",
                "-0.001636363636",
                "-0.001818181818",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.002000000000",
                "-0.001976190476",
                "-0.001809523810",
                "-0.001642857143",
                "-0.001476190476",
                "-0.001309523810"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Section without rebars.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain15()
        {
            ReinforcedConcreteSection section = GetRectangularSection4Rebars(400, 400, 14, 40, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
            var rebarIdList = section.ClearRebars();

            section.AddSteelSection(
                new SteelSectionPosition(
                    new SteelSection(
                        new SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"),
                        SteelMaterialEN1993Data.S235
                        ),
                    Point2d.Origin,
                    0.0,
                    new Vector2d(125.0, 50)
                    )
                );
            section.SteelSections[0].IsInsideConcrete = false;

            List<StrainPlane> planes = CalculateStrainPlanes(
                section: section,
                analysisType: SectionSolver.FailureDomainTypes.Plastic,
                rotationAngle: 0.0,
                gamma_M0: 1.0);

            var stringForCad = MakePlaneListString(planes, 0.0, 400.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.069107142857",
                "0.070714285714",
                "0.072321428571",
                "0.073928571429",
                "0.075535714286",
                "0.077142857143",
                "0.077190476190",
                "0.077238095238",
                "0.077285714286",
                "0.077333333333",
                "0.077380952381",
                "0.077428571429",
                "0.077464285714",
                "0.077500000000",
                "0.077535714286",
                "0.077571428571",
                "0.077607142857",
                "0.077642857143",
                "0.064998866213",
                "0.052354875283",
                "0.039710884354",
                "0.027066893424",
                "0.014422902494",
                "0.001778911565",
                "0.001482426304",
                "0.001185941043",
                "0.000889455782",
                "0.000592970522",
                "0.000296485261",
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
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003250000000",
                "-0.003000000000",
                "-0.002750000000",
                "-0.002500000000",
                "-0.002250000000",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        /// <summary>
        /// Tests the sequence of deformation planes in a composite section.
        /// Tipical Trojena section with a bug.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain16()
        {
            var section = new ReinforcedConcreteSection(
                4000,
                100,
                ConcreteMaterialEN1992Data.C25_30,
                new RebarSectionCircular(10, SteelMaterialEN1992Data.B500C),
                206.32,
                25,
                new RebarSectionCircular(10, SteelMaterialEN1992Data.B500C),
                206.32,
                new SectionH(2500, 15, 1000, 40, 1000, 40, "Tipical Trojena"),
                SteelMaterialEN1993Data.S235,
                25);

            section.SteelSections[0].Traslation.Y -= 100;
            section.SteelSections[0].IsInsideConcrete = false;

            List<StrainPlane> planes = CalculateStrainPlanes(
                section: section,
                analysisType: SectionSolver.FailureDomainTypes.Plastic,
                rotationAngle: 0.0,
                gamma_M0: 1.0);

            var stringForCad = MakePlaneListString(planes, -2600.0, 100.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.067500000000",
                "0.069107142857",
                "0.070714285714",
                "0.072321428571",
                "0.073928571429",
                "0.075535714286",
                "0.077142857143",
                "0.077190476190",
                "0.077238095238",
                "0.077285714286",
                "0.077333333333",
                "0.077380952381",
                "0.077428571429",
                "0.077464285714",
                "0.077500000000",
                "0.077535714286",
                "0.077571428571",
                "0.077607142857",
                "0.077642857143",
                "0.064998866213",
                "0.052354875283",
                "0.039710884354",
                "0.027066893424",
                "0.014422902494",
                "0.001778911565",
                "0.001482426304",
                "0.001185941043",
                "0.000889455782",
                "0.000592970522",
                "0.000296485261",
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
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003500000000",
                "-0.003250000000",
                "-0.003000000000",
                "-0.002750000000",
                "-0.002500000000",
                "-0.002250000000",
                "-0.002000000000"
            };

            Assert.IsNotNull(stringForCad);
            CollectionAssert.AreEqual(min_y_strian_sequence_result, min_y_strian_sequence);
            CollectionAssert.AreEqual(max_y_strian_sequence_result, max_y_strian_sequence);
        }

        private static void CalculateStrainPlanesMultiDirection(ReinforcedConcreteSection section,
            out Dictionary<double, List<StrainPlane>> planes,
            out Dictionary<double, List<string>> min_y_strian_sequence,
            out Dictionary<double, List<string>> max_y_strian_sequence,
            in SectionSolver.FailureDomainTypes domType)
        {
            planes = new Dictionary<double, List<StrainPlane>>();
            min_y_strian_sequence = new Dictionary<double, List<string>>();
            max_y_strian_sequence = new Dictionary<double, List<string>>();
            for (double rotation = 0.0; rotation < 2.0 * Math.PI; rotation += 0.3)
            {
                planes[rotation] = CalculateStrainPlanes(section, domType, rotation);

                var stringForCad = MakePlaneListString(planes[rotation], 0.0, 500.0,
                    out List<string> min_y_strian_sequence_loc, out List<string> max_y_strian_sequence_loc);

                min_y_strian_sequence[rotation] = min_y_strian_sequence_loc;
                max_y_strian_sequence[rotation] = max_y_strian_sequence_loc;
            }
        }

        // Tension in steel section.
        // The verifier does not allow to define only a steel section.
        // Then create a minimum reinforced concrete section and at its center of gravity place the steel section.
        [TestMethod]
        public void TensionCheck01()
        {
            double clsSize = 1.0;
            var rebar = new RebarSectionCircular("", 1.0 * clsSize, SteelMaterialEN1992Data.B450C);
            var structuralSteel = SteelMaterialEN1993Data.S275;
            structuralSteel.StressStrainCurve = SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic;
            var section = new ReinforcedConcreteSection(clsSize, clsSize, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 0.5 * clsSize, null, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), structuralSteel, 0.5 * clsSize);

            section.SteelSections[0].Traslation.X = -75.0 + 0.5 * clsSize;
            section.SteelSections[0].Traslation.Y = -150.0 + 0.5 * clsSize;
            section.SteelSections[0].IsInsideConcrete = false;

            double phi = 0.0;
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(0, 0, 0, 0, 146648095, 0, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(0, 0, 0, 0, 73324047.5, 11049608.63, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(475572.1667, 0, 0, 0, 48882698.33, 7366405.75, GetLocalCoordinateSystem(section))
            };

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, standard, true, new StandardEN1993p11());

            var slsResult = sectionChecker.GetLinearStressAnalysisResult(phi);

            // var concreteTensions = slsResult[0].GetConcreteVerticesTension(phi);
            // var rebarTensions = slsResult[0].GetRebarsTension(phi);
            double error = 0.25;

            // Combination 0
            var steelSectionsTensions = slsResult[0].GetStructuralSteelVerticesTension(phi);
            double maxSteelSectionTension = 275.0; // /gamma_M0 = 1.0;
            var internalMaxSteelTension = steelSectionsTensions.Max(i => i.tension);
            Assert.AreEqual(maxSteelSectionTension, internalMaxSteelTension, error);

            double minSteelSectionTension = -275.0; // /gamma_M0 = 1.0;
            var internalMinSteelTension = steelSectionsTensions.Min(i => i.tension);
            Assert.AreEqual(minSteelSectionTension, internalMinSteelTension, error);

            // Combination 1
            steelSectionsTensions = slsResult[1].GetStructuralSteelVerticesTension(phi);
            internalMaxSteelTension = steelSectionsTensions.Max(i => i.tension);
            Assert.AreEqual(maxSteelSectionTension, internalMaxSteelTension, error);

            internalMinSteelTension = steelSectionsTensions.Min(i => i.tension);
            Assert.AreEqual(minSteelSectionTension, internalMinSteelTension, error);

            // Combination 2
            steelSectionsTensions = slsResult[2].GetStructuralSteelVerticesTension(phi);
            internalMaxSteelTension = steelSectionsTensions.Max(i => i.tension);
            Assert.AreEqual(maxSteelSectionTension, internalMaxSteelTension, error);

            minSteelSectionTension = -91.66666667; // /gamma_M0 = 1.0;
            internalMinSteelTension = steelSectionsTensions.Min(i => i.tension);
            Assert.AreEqual(minSteelSectionTension, internalMinSteelTension, error);
        }

        // Tension in composite section.
        // See excel file "01_Steel_Concrete_Member check.xlsm".
        [TestMethod]
        [TestCategory("Bridge")]
        public void TensionCheck02()
        {
            var rebar = new RebarSectionCircular("", 12.0, SteelMaterialEN1992Data.B450A);
            var section = new ReinforcedConcreteSection(1200.0, 220.0, ConcreteMaterialEN1992Data.C40_50, rebar, 300.0, 110.0, null, 200.0,
                new GPC.Model.Sections.SectionH(600.0, 21.6, 215.0, 32.4, 215.0, 32.4, "IPN600 r=0"), SteelMaterialEN1993Data.S355);

            section.SteelSections[0].Traslation.Y -= 80.0;

            double phi = 0.0;
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(0, 0, 0, 0, 3000000000, 0, GetLocalCoordinateSystem(section)),
            };

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, standard, true, new StandardEN1993p11());

            var slsResult = sectionChecker.GetLinearStressAnalysisResult(phi);

            // Combination 0
            var concreteTensions = slsResult[0].GetConcreteVerticesTension(phi);
            var rebarTensions = slsResult[0].GetRebarsTension(phi);
            var steelSectionsTensions = slsResult[0].GetStructuralSteelVerticesTension(phi);

            double maxSteelSectionTension = 336.95;
            double minConcreteCompression = -26.42;
            double minRebarsCompression = -97.96;

            var internalMaxSteelTension = steelSectionsTensions.Max(i => i.tension);
            var internalMinConcreteCompression = concreteTensions.Min(i => i.tension);
            var internalMinRebarsCompression = rebarTensions.Min(i => i.tension);

            Assert.AreEqual(maxSteelSectionTension, internalMaxSteelTension, 2.0);
            Assert.AreEqual(minConcreteCompression, internalMinConcreteCompression, 0.08);
            Assert.AreEqual(minRebarsCompression, internalMinRebarsCompression, 5.0);
        }

        // Tension in composite section.
        // See excel file "01_Steel_Concrete_Member check.xlsm".
        [TestMethod]
        [TestCategory("Bridge")]
        public void TensionCheck03()
        {
            var rebar = new RebarSectionCircular("", 12.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1200.0, 200.0, ConcreteMaterialEN1992Data.C40_50, rebar, 150, 60.0, null, 150,
                new GPC.Model.Sections.SectionH(600.0, 21.6, 215.0, 32.4, 215.0, 32.4, "IPN600 r=0"), SteelMaterialEN1993Data.S355);
            double phi = 0.0;
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(0, 0, 0, 0, 2000000000, 0, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(0, 0, 0, 0, 2500000000, 0, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(0, 0, 0, 0, 1500000000, 0, GetLocalCoordinateSystem(section)),
            };

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, standard, true, new StandardEN1993p11());

            var slsResult = sectionChecker.GetLinearStressAnalysisResult(phi);

            // Combination 0
            var concreteTensions = slsResult[0].GetConcreteVerticesTension(phi);
            var rebarTensions = slsResult[0].GetRebarsTension(phi);
            var steelSectionsTensions = slsResult[0].GetStructuralSteelVerticesTension(phi);

            double maxSteelSectionTension = 270.88;
            double minConcreteCompression = -20.84;
            double minRebarsCompression = -95.37;

            var internalMaxSteelTension = steelSectionsTensions.Max(i => i.tension);
            var internalMinConcreteCompression = concreteTensions.Min(i => i.tension);
            var internalMinRebarsCompression = rebarTensions.Min(i => i.tension);

            var err1 = Error.TwoValuesRelativeError(maxSteelSectionTension, internalMaxSteelTension);
            var err2 = Error.TwoValuesRelativeError(minConcreteCompression, internalMinConcreteCompression);
            var err3 = Error.TwoValuesRelativeError(minRebarsCompression, internalMinRebarsCompression);

            Assert.IsTrue(err1 < 0.0025);
            Assert.IsTrue(err2 < 0.002);
            Assert.IsTrue(err3 < 0.026);

            // Combination 1
            concreteTensions = slsResult[1].GetConcreteVerticesTension(phi);
            rebarTensions = slsResult[1].GetRebarsTension(phi);
            steelSectionsTensions = slsResult[1].GetStructuralSteelVerticesTension(phi);

            maxSteelSectionTension = 338.60;
            minConcreteCompression = -26.05;
            minRebarsCompression = -119.21;

            internalMaxSteelTension = steelSectionsTensions.Max(i => i.tension);
            internalMinConcreteCompression = concreteTensions.Min(i => i.tension);
            internalMinRebarsCompression = rebarTensions.Min(i => i.tension);

            var err11 = Error.TwoValuesRelativeError(maxSteelSectionTension, internalMaxSteelTension);
            var err22 = Error.TwoValuesRelativeError(minConcreteCompression, internalMinConcreteCompression);
            var err33 = Error.TwoValuesRelativeError(minRebarsCompression, internalMinRebarsCompression);

            Assert.IsTrue(err11 < 0.0025);
            Assert.IsTrue(err22 < 0.002);
            Assert.IsTrue(err33 < 0.026);

            // Combination 1
            concreteTensions = slsResult[2].GetConcreteVerticesTension(phi);
            rebarTensions = slsResult[2].GetRebarsTension(phi);
            steelSectionsTensions = slsResult[2].GetStructuralSteelVerticesTension(phi);

            maxSteelSectionTension = 202.22;
            minConcreteCompression = -15.92;
            minRebarsCompression = -68.977;

            internalMaxSteelTension = steelSectionsTensions.Max(i => i.tension);
            internalMinConcreteCompression = concreteTensions.Min(i => i.tension);
            internalMinRebarsCompression = rebarTensions.Min(i => i.tension);

            var err111 = Error.TwoValuesRelativeError(maxSteelSectionTension, internalMaxSteelTension);
            var err222 = Error.TwoValuesRelativeError(minConcreteCompression, internalMinConcreteCompression);
            var err333 = Error.TwoValuesRelativeError(minRebarsCompression, internalMinRebarsCompression);

            Assert.IsTrue(err111 < 0.0025);
            Assert.IsTrue(err222 < 0.01);
            Assert.IsTrue(err333 < 0.026);
        }


        // Tension in composite section.
        // See excel file "01_Steel_Concrete_Member check.xlsm".
        [TestMethod]
        [TestCategory("Bridge")]
        public void TensionCheck04()
        {
            var rebar = new RebarSectionCircular("", 12.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1200.0, 200.0, ConcreteMaterialEN1992Data.C40_50, rebar, 150, 60.0, null, 150,
                new GPC.Model.Sections.SectionH(600.0, 21.6, 215.0, 32.4, 215.0, 32.4, "IPN600 r=0"), SteelMaterialEN1993Data.S355);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(0, 0, 0, 0, 1500000000, 0, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(0, 0, 0, 0, 2000000000, 0, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(0, 0, 0, 0, 2500000000, 0, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(0, 0, 0, 0, 3000000000, 0, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(0, 0, 0, 0, 3190000000, 0, GetLocalCoordinateSystem(section)),
            };

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, standard, true, new StandardEN1993p11());

            var slsResult = sectionChecker.GetStressAnalysisResult();

            for (int i = 0; i < slsResult.Length; i++)
            {
                var concreteTensions = slsResult[i].GetConcreteVerticesTension();
                var rebarTensions = slsResult[i].GetRebarsTension();
                var steelSectionsTensions = slsResult[i].GetStructuralSteelVerticesTension();

                Console.WriteLine($"CMB {i}");
                Console.WriteLine(concreteTensions.Select(j => j.tension).Min() + ";" + rebarTensions.Select(j => j.tension).Max() + ";" +
                    steelSectionsTensions.Select(j => j.tension).Min() + ";" + steelSectionsTensions.Select(j => j.tension).Max());
            }
        }

        // Tension in composite section.
        [TestMethod]
        [TestCategory("Bridge")]
        public void BendingMomentCheck02()
        {
            var rebar = new RebarSectionCircular("", 1, SteelMaterialACI318Data.Grade60);
            var cnc = new ConcreteMaterialACI318("fc' 4000", 27.579, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var section = new ReinforcedConcreteSection(1200.0, 200.0, cnc, rebar, 150, 60.0, null, 150,
                new GPC.Model.Sections.SectionH(402.6, 7.7, 177.7, 10.9, 177.7, 10.9, "UB 406 x 178 x 54 r=0"), SteelMaterialACI318Data.Grade50);

            StandardACI318p08 standard = new StandardACI318p08();
            StandardAISC360p05 standardAisc = new StandardAISC360p05();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(0, 0, 0, 0, 840000000, 0, GetLocalCoordinateSystem(section), 1),
            };

            SectionCheckerACI318 sectionChecker = GetSectionCheckerACI318(section, forces, null, standard,
                new SectionCheckerACI318.SectionOptionsStandardACI318(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64), false, false, standardAisc);

            var slsResult = sectionChecker.GetStressAnalysisResult();
            var res = slsResult[0].CalculateStrainPlaneResult();

            // Combination 0
            var concreteTensions = slsResult[0].GetConcreteVerticesTension();
            var rebarTensions = slsResult[0].GetRebarsTension();
            var steelSectionsTensions = slsResult[0].GetStructuralSteelVerticesTension();

            Assert.IsTrue(Math.Abs(steelSectionsTensions.Select(i => i.tension).Max() - SteelMaterialACI318Data.Grade50.Fyk) < 0.001);
            Assert.IsTrue(Math.Abs(steelSectionsTensions.Select(i => i.tension).Min() - SteelMaterialACI318Data.Grade50.Fyk) < 0.001);
            Assert.IsTrue(Math.Abs(concreteTensions.Select(i => i.tension).Min() - 0.85 * cnc.Fc) < 0.001);
            Assert.IsTrue(Math.Abs(concreteTensions.Select(i => i.tension).Max()) < 0.001);
        }

        // Tension in composite section.
        [TestMethod]
        [TestCategory("Bridge")]
        public void BendingMomentCheck0()
        {
            var rebar = new RebarSectionCircular("", 1, SteelMaterialACI318Data.Grade60);
            var cnc = new ConcreteMaterialACI318("fc' 4000", 27.579, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);

            var section = new ReinforcedConcreteSection(1200.0, 200.0, cnc, rebar, 150, 60.0, null, 150,
                new GPC.Model.Sections.SectionH(402.6, 7.7, 177.7, 10.9, 177.7, 10.9, "UB 406 x 178 x 54 r=0"), SteelMaterialACI318Data.Grade50);

            StandardACI318p08 standard = new StandardACI318p08();
            StandardAISC360p05 standardAisc = new StandardAISC360p05();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(0, 0, 0, 0, 1000000000, 0, GetLocalCoordinateSystem(section), 1),
                new ResultBeamForces(0, 0, 0, 0, 1000000000, 0, new CoordinateSystem(new Point3d(600,-402.6/2.0, 0), Vector3d.XAxis, Vector3d.YAxis, "centroidSteel"), 1),
                new ResultBeamForces(0, 0, 0, 0, 1000000000, 0, new CoordinateSystem(new Point3d(0, 0, 0), Vector3d.XAxis, Vector3d.YAxis, "generic"), 1),
            };

            SectionCheckerACI318 sectionChecker = GetSectionCheckerACI318(section, null, null, standard,
                new SectionCheckerACI318.SectionOptionsStandardACI318(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64), false, false, standardAisc);

            FailureDomainResult ulsResult = sectionChecker.GetPlasticFailureDomainResult();
            Mesh domainMesh = ulsResult.Domain.GetMesh(ulsResult.Domain, out Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint);

            //ExportToGmsh(domainMesh);

            var domainPoint = sectionChecker.SectionSolver.CalculateDomainPoint(forces[0], domainMesh, vertexToDomainPoint, sectionChecker.SectionCheckerOptions);
            var domainPoint2 = sectionChecker.SectionSolver.CalculateDomainPoint(forces[1], domainMesh, vertexToDomainPoint, sectionChecker.SectionCheckerOptions);
            var domainPoint3 = sectionChecker.SectionSolver.CalculateDomainPoint(forces[2], domainMesh, vertexToDomainPoint, sectionChecker.SectionCheckerOptions);


            Console.WriteLine($"MxRd = {Math.Round(domainPoint.MxRd / 1000000, 2)} kNm");
            Console.WriteLine($"MxRd = {Math.Round(domainPoint2.MxRd / 1000000, 2)} kNm");
            Console.WriteLine($"MxRd = {Math.Round(domainPoint3.MxRd / 1000000, 2)} kNm");

            Console.WriteLine($"Nominal MxRd = {Math.Round(domainPoint3.MxRd / 0.9 / 1000000, 4)} kNm");

            Assert.IsTrue(Math.Abs(domainPoint2.MxRd - domainPoint.MxRd) < 1);
            Assert.IsTrue(Math.Abs(domainPoint3.MxRd - domainPoint.MxRd) < 1);

            var slsResult = sectionChecker.GetStressAnalysisResult(new ResultBeamForces(0, 0, 0, 0, domainPoint.MxRd, 0, GetLocalCoordinateSystem(section), 1));
            var res = slsResult.CalculateStrainPlaneResult();
        }

        [TestMethod]
        public void RectangularSectionWithSteelSetcion01()
        {
            ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
            var standardSteel = new StandardEN1993p11();
            CoordinateSystem cs = GetLocalCoordinateSystem(section);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(cs, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);

            section.AddSteelSection(
                new SteelSectionPosition(
                    new SteelSection(
                        new SectionH(200.0, 5.6, 100.0, 8.5, 100.0, 8.5, "IPE 200 r=0"),
                        SteelMaterialEN1993Data.S275
                        ),
                    Point2d.Origin,
                    0.0,
                    new Vector2d(100.0, 150.0)
                    )
                );
            section.SteelSections[0].IsInsideConcrete = true;

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                new ResultBeamForces(-800 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                new ResultBeamForces(-1200 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
            };

            for (int i = 0; i < forces.Length; i++)
                CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005,
                    standardStructuralSteel: standardSteel);
        }

        [TestMethod]
        public void RectangularSectionWithSteelSetcion02()
        {
            ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
            var standardSteel = new StandardEN1993p11();
            CoordinateSystem cs = GetLocalCoordinateSystem(section);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(cs, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);

            section.AddSteelSection(
                new SteelSectionPosition(
                    new SteelSection(
                        new SectionH(500.0, 10.2, 200.0, 16.0, 200.0, 16.0, "IPE 500 r=0"),
                        SteelMaterialEN1993Data.S275
                        ),
                    Point2d.Origin,
                    0.0,
                    new Vector2d(50.0, 0.0)
                    )
                );
            section.SteelSections[0].IsInsideConcrete = true;

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                //new ResultBeamForces(3000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                //new ResultBeamForces(2000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                new ResultBeamForces(1000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                new ResultBeamForces(-2000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                new ResultBeamForces(-3000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                new ResultBeamForces(-4000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                new ResultBeamForces(-5000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
            };

            for (int i = 0; i < forces.Length; i++)
                CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005,
                    standardStructuralSteel: standardSteel);
        }

        [TestMethod]
        public void RectangularSectionWithSteelSetcion03()
        {
            ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
            var standardSteel = new StandardEN1993p11();
            CoordinateSystem cs = GetLocalCoordinateSystem(section);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(cs, SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);

            var steelMaterialList = new SteelMaterialEN1993[]
            {
                SteelMaterialEN1993Data.S235,
                SteelMaterialEN1993Data.S275,
                SteelMaterialEN1993Data.S355,
                SteelMaterialEN1993Data.S420,
                SteelMaterialEN1993Data.S450
            };

            foreach (var steelMat in steelMaterialList)
            {
                double Nconst = 1000 * steelMat.Fyk / 275.0;
                var forces = new ResultBeamForces[]
                {
                    //new ResultBeamForces(3000 * Nconst, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                    //new ResultBeamForces(2000 * Nconst, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                    new ResultBeamForces(1000 * Nconst, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                    new ResultBeamForces(0 * Nconst, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                    new ResultBeamForces(-1000 * Nconst, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                    new ResultBeamForces(-2000 * Nconst, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                    new ResultBeamForces(-3000 * Nconst, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                    new ResultBeamForces(-4000 * Nconst, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                    new ResultBeamForces(-5000 * Nconst, 0, 0, 0, 50 * 1000000, 0 * 1000000, cs),
                };

                section.SteelSections.Clear();
                section.AddSteelSection(
                    new SteelSectionPosition(
                        new SteelSection(
                            new SectionRHS(524.0, 324.0, 12.0, 12.0, 12.0, 12.0, "RHS r=0"),
                            steelMat
                            ),
                        Point2d.Origin,
                        0.0,
                        new Vector2d(-12.0, -12.0)
                        )
                    );
                section.SteelSections[0].IsInsideConcrete = false;

                for (int i = forces.Length - 1; i >= 0; i--)
                    CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005,
                        standardStructuralSteel: standardSteel);
            }
        }

        [TestMethod]
        public void RectangularSectionWithSteelSetcion04()
        {
            double b = 2000;
            double h = 200;
            double rebarDiameter = 1;
            int numberRebars = 8;
            double rebarsCover = 50;

            double bfw = 500;       // bottom flange width
            double tfw = 500;       // top flange width
            double hh = 1000;          // heigth
            double bft = 40;       // Bottom flange thickness
            double tft = 40;        // Top flange thickness
            double wt = 1;        // Web thickness

            ConcreteMaterialACI318 concreteMaterialACI318 = ConcreteMaterialACI318Data.Fc4000;
            SteelMaterialACI318 steelMaterialACI318 = SteelMaterialACI318Data.Grade50;
            SteelMaterialACI318 rebarMaterial = SteelMaterialACI318Data.Grade50;

            SectionH sectionH = new SectionH(hh, wt, tfw, tft, bfw, bft, "Test");

            var fakeRebar = new RebarSectionCircular("", 1.0, rebarMaterial);
            var rebar = new RebarSectionCircular("", rebarDiameter, rebarMaterial);
            ReinforcedConcreteSection reinforcedConcreteSection = new ReinforcedConcreteSection(b, h, concreteMaterialACI318, rebar, b / numberRebars, rebarsCover, fakeRebar, 1000, sectionH, steelMaterialACI318);

            StandardACI318p14 standardACI318P14 = new StandardACI318p14();
            StandardAISC360p16 standardAISC360P16 = new StandardAISC360p16();

            CoordinateSystem coordinateSystem = GetLocalCoordinateSystem(reinforcedConcreteSection);
            SectionCheckerACI318.SectionOptionsStandardACI318 options = new SectionCheckerACI318.SectionOptionsStandardACI318(coordinateSystem, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(reinforcedConcreteSection);
            SectionCheckerACI318 sectionCheckerACI318 = new SectionCheckerACI318(sectionCheckerAttribute, options, standardACI318P14, false, false, -1, standardAISC360P16);

            FailureDomainResult elasticFailureDomainResult = sectionCheckerACI318.GetElasticFailureDomainResult();
            FailureDomainResult plasticFailureDomainResult = sectionCheckerACI318.GetPlasticFailureDomainResult();

            var elasticDomainMesh = elasticFailureDomainResult.Domain.GetMesh(elasticFailureDomainResult.Domain, out Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> elasticVertexToDomainPoint);
            var plasticDomainMesh = plasticFailureDomainResult.Domain.GetMesh(plasticFailureDomainResult.Domain, out Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> plasticVertexToDomainPoint);

            ShowDomainPoints(elasticFailureDomainResult.Domain);
            ShowDomainPoints(plasticFailureDomainResult.Domain);
            //ExportToGmsh(elasticDomainMesh);
            //ExportToGmsh(plasticDomainMesh);
        }

        [TestMethod]
        public void RectangularSectionWithSteelSetcion05()
        {
            ReinforcedConcreteSection section = GetRectangularSection4Rebars(400, 400, 14, 40, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
            var standardSteel = new StandardEN1993p11();

            section.AddSteelSection(
                new SteelSectionPosition(
                    new SteelSection(
                        new SectionC(200.0, 8.5, 75.0, 11.5, 75.0, 11.5, "UPN200 r=0"),
                        SteelMaterialEN1993Data.S235
                        ),
                    Point2d.Origin,
                    0.0,
                    Point2d.Origin
                    )
                );
            section.SteelSections[0].IsInsideConcrete = false;
            CoordinateSystem cs = GetLocalCoordinateSystem(section);

            var forces = new ResultBeamForces[]
            {
                new ResultBeamForces(-2500 * 1000, 0, 0, 0, -20 * 1000000, 70 * 1000000, cs),
            };

            var sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(cs, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Elastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);

            for (int i = 0; i < forces.Length; i++)
                CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005,
                    standardStructuralSteel: standardSteel);

            var sectionCheckerAttribute = new SectionCheckerAttribute(section);
            var sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, false, -1, new StandardEN1993p11());
            var elasticFailureDomainResult = sectionChecker.GetElasticFailureDomainResult();
            var elasticDomainMesh = elasticFailureDomainResult.Domain.GetMesh(elasticFailureDomainResult.Domain, out Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> elasticVertexToDomainPoint);
            ShowDomainPoints(elasticFailureDomainResult.Domain);
            //ExportToGmsh(elasticDomainMesh);
        }

        /// <summary>
        /// Check whether when the insertion point is changed, the properties of the homogenized section do not change.
        /// </summary>
        [TestMethod]
        public void InsertionPoint01()
        {
            ReinforcedConcreteSection section = GetRectangularSection4Rebars(400, 400, 14, 40, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
            section.ClearRebars();

            section.AddSteelSection(
                new SteelSectionPosition(
                    new SteelSection(
                        new SectionL(200.0, 60.0, 300.0, 40.0, "L 300×200×60"),
                        SteelMaterialEN1993Data.S235
                        ),
                    Point2d.Origin,
                    0.0,
                    new Vector2d(200.0, 200.0),
                    InsertionPointType.MiddleCenter,
                    MiddleCenterType.Midpoint
                    )
                );
            section.SteelSections[0].IsInsideConcrete = true;

            // With InsertionPointType.MiddleCenter
            var homo5 = section.GetHomogeneizedMechanicalProperties(0);
            Assert.AreEqual(282510.69, homo5.areaH, 0.01);
            Assert.AreEqual(17.85, homo5.angleX.ToDegrees(), 0.01);

            // With all other inserion points.
            var positionInfo = new (InsertionPointType insPoint, double xTras, double yTras)[]
            {
                (InsertionPointType.BottomLeft, 100.0, 50.0),
                (InsertionPointType.BottomCenter, 200.0, 50.0),
                (InsertionPointType.BottomRight, 300.0, 50.0),
                (InsertionPointType.MiddleLeft, 100.0, 200.0),
                (InsertionPointType.MiddleCenter, 200.0, 200.0),
                (InsertionPointType.MiddleRight, 300.0, 200.0),
                (InsertionPointType.TopLeft, 100.0, 350.0),
                (InsertionPointType.TopCenter, 200.0, 350.0),
                (InsertionPointType.TopRight, 300.0, 350.0),
                (InsertionPointType.Centroid, 164.4444444444, 146.6666666667),
                (InsertionPointType.ShearCenter, 120.0, 80.0)
            };

            foreach (var position in positionInfo)
            {
                section.SteelSections[0].CardinalPoint = position.insPoint;
                section.SteelSections[0].Traslation.X = position.xTras;
                section.SteelSections[0].Traslation.Y = position.yTras;

                var homo = section.GetHomogeneizedMechanicalProperties();

                Assert.AreEqual(282510.69, homo.areaH, 0.01);
                Assert.AreEqual(17.85, homo.angleX.ToDegrees(), 0.01);

                Assert.AreEqual(homo5.J11H, homo.J11H, 1.0);
                Assert.AreEqual(homo5.J22H, homo.J22H, 1.0);
            }
        }
    }
}
