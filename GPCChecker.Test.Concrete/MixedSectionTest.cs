using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
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
        /// Rotation point p5 is not used.
        /// </summary>
        [TestMethod]
        public void StrainPlanesDomain01_FRC()
        {
            var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialModelCode2010Data.C25_30_17, rebar, 200.0, 50.0, rebar, 200.0,
                new GPC.Model.Sections.SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);

            List<StrainPlane> planes = CalculateStrainPlanes(section);

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
                "0.017307425058",
                "0.014614850116",
                "0.011922275174",
                "0.009229700232",
                "0.006537125290",
                "0.003844550349",
                "0.003142265109",
                "0.002513812087",
                "0.001885359065",
                "0.001256906044",
                "0.000628453022",
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
                "-0.003402435939",
                "-0.003231039660",
                "-0.003059643381",
                "-0.002888247103",
                "-0.002716850824",
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

            List<StrainPlane> planes = CalculateStrainPlanes(section);

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
                steelMaterial: SteelMaterialEN1993Data.S275);

            section.SteelSections[0].Traslation.Y = 100.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

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
                "0.063251207729",
                "0.051113526570",
                "0.038975845411",
                "0.026838164251",
                "0.014700483092",
                "0.002562801932",
                "0.002135668277",
                "0.001708534622",
                "0.001281400966",
                "0.000854267311",
                "0.000427133655",
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
                steelMaterial: SteelMaterialEN1993Data.S275);

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

            List<StrainPlane> planes = CalculateStrainPlanes(section);

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
                "0.068522727273",
                "0.069545454545",
                "0.070568181818",
                "0.071590909091",
                "0.072613636364",
                "0.073636363636",
                "0.073666666667",
                "0.073696969697",
                "0.073727272727",
                "0.073757575758",
                "0.073787878788",
                "0.073818181818",
                "0.075131818182",
                "0.076445454545",
                "0.077759090909",
                "0.079072727273",
                "0.080386363636",
                "0.081700000000",
                "0.068591304348",
                "0.055482608696",
                "0.042373913043",
                "0.029265217391",
                "0.016156521739",
                "0.003047826087",
                "0.002539855072",
                "0.002031884058",
                "0.001523913043",
                "0.001015942029",
                "0.000507971014",
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
                "-0.016450000000",
                "-0.030900000000",
                "-0.045350000000",
                "-0.059800000000",
                "-0.074250000000",
                "-0.088700000000",
                "-0.075591304348",
                "-0.062482608696",
                "-0.049373913043",
                "-0.036265217391",
                "-0.023156521739",
                "-0.010047826087",
                "-0.009539855072",
                "-0.009031884058",
                "-0.008523913043",
                "-0.008015942029",
                "-0.007507971014",
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
                "0.067774390244",
                "0.068048780488",
                "0.068323170732",
                "0.068597560976",
                "0.068871951220",
                "0.069146341463",
                "0.069154471545",
                "0.069162601626",
                "0.069170731707",
                "0.069178861789",
                "0.069186991870",
                "0.069195121951",
                "0.069735772358",
                "0.070276422764",
                "0.070817073171",
                "0.071357723577",
                "0.071898373984",
                "0.072439024390",
                "0.061248674443",
                "0.050058324496",
                "0.038867974549",
                "0.027677624602",
                "0.016487274655",
                "0.005296924708",
                "0.002650671615",
                "0.002120537292",
                "0.001590402969",
                "0.001060268646",
                "0.000530134323",
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
                "-0.024166666667",
                "-0.046333333333",
                "-0.068500000000",
                "-0.090666666667",
                "-0.112833333333",
                "-0.135000000000",
                "-0.135000000000",
                "-0.135000000000",
                "-0.135000000000",
                "-0.135000000000",
                "-0.135000000000",
                "-0.135000000000",
                "-0.126513432308",
                "-0.115910745847",
                "-0.105308059385",
                "-0.094705372923",
                "-0.084102686462",
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
                "0.007019629777",
                "0.002644726645",
                "0.002026613778",
                "0.001408500910",
                "0.000790388042",
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
                "-0.024166666667",
                "-0.046333333333",
                "-0.068500000000",
                "-0.090666666667",
                "-0.112833333333",
                "-0.135000000000",
                "-0.135000000000",
                "-0.126394532908",
                "-0.114032275552",
                "-0.101670018197",
                "-0.089307760841",
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
                new SteelMaterialEN1993("S275", 210000, 275, 430, 0.03, SteelMaterial.SteelTypes.Structural));

            section.SteelSections[0].Traslation.Y = 100.0;

            List<StrainPlane> planes = CalculateStrainPlanes(section);

            var stringForCad = MakePlaneListString(planes, 0.0, 2100.0,
                out List<string> min_y_strian_sequence, out List<string> max_y_strian_sequence);

            var min_y_strian_sequence_result = new List<string>()
            {
                "0.027000000000",
                "0.027109756098",
                "0.027219512195",
                "0.027329268293",
                "0.027439024390",
                "0.027548780488",
                "0.027658536585",
                "0.027666666667",
                "0.027674796748",
                "0.027682926829",
                "0.027691056911",
                "0.027699186992",
                "0.027707317073",
                "0.027808943089",
                "0.027910569106",
                "0.028012195122",
                "0.028113821138",
                "0.028215447154",
                "0.028317073171",
                "0.024041357370",
                "0.019765641569",
                "0.015489925769",
                "0.011214209968",
                "0.006938494168",
                "0.002662778367",
                "0.002002198756",
                "0.001341619144",
                "0.000681039533",
                "0.000020459922",
                "-0.000640119689",
                "-0.001300699301",
                "-0.001417249417",
                "-0.001533799534",
                "-0.001650349650",
                "-0.001766899767",
                "-0.001883449883",
                "-0.002000000000"
            };

            var max_y_strian_sequence_result = new List<string>()
            {
                "0.027000000000",
                "0.022500000000",
                "0.018000000000",
                "0.013500000000",
                "0.009000000000",
                "0.004500000000",
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
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.022833333333",
                "-0.018666666667",
                "-0.014500000000",
                "-0.010333333333",
                "-0.006166666667",
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
                new SteelMaterialEN1993("S275", 210000, 275, 430, 0.03, SteelMaterial.SteelTypes.Structural));

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
                "-0.000073220572",
                "-0.000318716317",
                "-0.000564212063",
                "-0.000809707809",
                "-0.001055203555",
                "-0.001300699301",
                "-0.001417249417",
                "-0.001533799534",
                "-0.001650349650",
                "-0.001766899767",
                "-0.001883449883",
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
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.027000000000",
                "-0.022833333333",
                "-0.018666666667",
                "-0.014500000000",
                "-0.010333333333",
                "-0.006166666667",
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

            List<StrainPlane> planes = CalculateStrainPlanes(section);

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

            List<StrainPlane> planes = CalculateStrainPlanes(section);

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

            List<StrainPlane> planes = CalculateStrainPlanes(section);

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

            List<StrainPlane> planes = CalculateStrainPlanes(section);

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
        private static List<StrainPlane> CalculateStrainPlanes(ReinforcedConcreteSection section, SectionSolver.FailureDomainTypes analysisType = SectionSolver.FailureDomainTypes.Plastic)
        {
            (SectionSolver.FailureZones, int)[] plasticZones =
            {
                (SectionSolver.FailureZones.F1, 5),
                (SectionSolver.FailureZones.F2A, 5),
                (SectionSolver.FailureZones.F2B, 5),
                (SectionSolver.FailureZones.F3A, 5),
                (SectionSolver.FailureZones.F3B, 5),
                (SectionSolver.FailureZones.F4, 5)
            };

            SectionSolverModelCode2010Test sectionSolverModelCode2010Test = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());
            var sectionDistances = sectionSolverModelCode2010Test.CalculateMaxMinSectionDistances(0.0);
            var p2 = sectionSolverModelCode2010Test.GetP2(sectionDistances, analysisType);
            var p3 = sectionSolverModelCode2010Test.GetP3(sectionDistances, analysisType);
            var p4 = sectionSolverModelCode2010Test.GetP4(sectionDistances, analysisType);
            var p5 = sectionSolverModelCode2010Test.GetP5(sectionDistances, analysisType);

            var planes = new List<StrainPlane>();

            for (int i = 0; i < plasticZones.Length; i++)
            {
                SectionSolver.FailureZones failureZones = plasticZones[i].Item1;
                int subdivision = plasticZones[i].Item2 + 1;
                int subIndex = 0;

                var p1 = sectionSolverModelCode2010Test.GetP1(sectionDistances, analysisType, failureZones);

                for (int j = 0; j < subdivision; j++)
                {
                    planes.Add(sectionSolverModelCode2010Test.CalculateStrainPlane(0.0, failureZones, (double)j / (double)subdivision, p1, p2, p3, p4, p5, subIndex));
                    subIndex++;
                }
            }
            var p1_F4 = sectionSolverModelCode2010Test.GetP1(sectionDistances, analysisType, SectionSolver.FailureZones.F4);
            planes.Add(sectionSolverModelCode2010Test.CalculateStrainPlane(0.0, SectionSolver.FailureZones.F4, 1.0, p1_F4, p2, p3, p4, p5, 6));
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

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete(),
                false, new StandardEN1993p11());
            FailureDomainCommonAssertModelCode(section, sectionChecker, new StandardNTC2018Concrete(), 100.0, true, new StandardEN1993p11());
            var domainResult = sectionChecker.GetPlasticFailureDomainResult();

            Assert.IsTrue(true);
        }
    }
}
