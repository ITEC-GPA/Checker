using System;
using System.Collections.Generic;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using GPC.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class FailureDomainPSCTest : ConcreteTestBase
    {

        [TestMethod]
        public void RectangularSectionPrestressedTest1()
        {
            double rebarDiameter = 20;
            double height = 500;
            double width = 300;
            double concreteCover = 50;
            double rebarDiameterPrestress = 20;

            ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C45_55);

            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new SteelMaterial("", 200000, 1620, 1800, 0.1, SteelMaterial.SteelTypes.Tendon));

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebarP, new Point3d(150, 100, 0), 1400)
            };

            section.AddRebars(rebars);
			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete());

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
            ShowDomainPoints(failureDomain.Domain);
            //ExportToGmsh(failureDomain.Domain);

            //// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

            /* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
				-905.882	73.2787		0			0
				-850.452	86.7231		0			0
				-795.487	99.6677		0			0
				-745.072	111.163		0			0
				-407.98		180.471		0			0
				271.13		308.835		0			0
				449.766		334.227		0			0
				747.837		367.052		0			0
				1388.85		390.363		0			0
				1821.5		361.728		0			0
				2369.18		312.73		0			0
				3116.14		210.941		0			0
				3617.17		120.777		0			0
				3956.3		59.5186		0			0
				4128.07		28.26		0			0
				4128.07		28.26		0			0
				4000.26		-1.42864	0			0
				3705.09		-61.1167	0			0
				3248.02		-149.711	0			0
				2533.25		-243.453	0			0
				2017.76		-284.403	0			0
				1617.29		-304.991	0			0
				981.689		-269.402	0			0
				645.392		-230.843	0			0
				428.53		-199.717	0			0
				-407.98		-47.7433	0			0
				-745.072	21.5644		0			0
				-795.487	33.0601		0			0
				-850.452	46.0048		0			0
				-905.882	59.4491		0			0
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
			*/
        }

        [TestMethod]
        public void RectangularSectionPrestressedTest2()
        {
            double rebarDiameter = 20;
            double rebarDiameterPrestress = 20;
            double height = 500;
            double width = 300;
            double concreteCover = 50;

            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new SteelMaterial("", 200000, 1620, 1800, 0.1, SteelMaterial.SteelTypes.Tendon));

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebarP, new Point3d(150, 100, 0), 0.007045),
                new ReinforcedConcreteRebar(rebarP, new Point3d(150, 400, 0), 0.007045)
            };

            ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C45_55);
            section.AddRebars(rebars);
			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete());

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
            ShowDomainPoints(failureDomain.Domain);
            //ExportToGmsh(failureDomain.Domain);

            //// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

            /* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-1376.33	0			0			0
				-1376.33	0			0			0
				-1376.33	0			0			0
				-1376.33	0			0			0
				-1348.31	6.91482		0			0
				-1292.88	20.3592		0			0
				-1237.91	33.3038		0			0
				-1187.5		44.7995		0			0
				-850.406	114.107		0			0
				-139.496	247.241		0			0
				77.3662		278.367		0			0
				413.663		316.926		0			0
				1049.27		352.515		0			0
				1449.73		331.927		0			0
				1969.1		290.397		0			0
				2726.78		190.217		0			0
				3211.06		97.5407		0			0
				3533.44		33.7706		0			0
				3688.47		0			0			0
				3688.47		0			0			0
				3533.44		-33.7706	0			0
				3211.06		-97.5407	0			0
				2726.78		-190.217	0			0
				1969.1		-290.397	0			0
				1449.73		-331.927	0			0
				1049.27		-352.515	0			0
				413.663		-316.926	0			0
				77.3662		-278.367	0			0
				-139.496	-247.241	0			0
				-850.406	-114.107	0			0
				-1187.5		-44.7995	0			0
				-1237.91	-33.3038	0			0
				-1292.88	-20.3592	0			0
				-1348.31	-6.91482	0			0
				-1376.33	0			0			0
				-1376.33	0			0			0
				-1376.33	0			0			0
				-1376.33	0			0			0
			*/
        }

        [TestMethod]
        public void SectionTPrestressedTest1()
        {
            double rebarDiameter = 26;
            double rebarDiameterPrestress = 20;

            // sezion a T tovescia 
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(500, 0),
                new Point2d(500, 500),
                new Point2d(400, 500),
                new Point2d(400, 1000),
                new Point2d(100, 1000),
                new Point2d(100, 500),
                new Point2d(0, 500)
            }));

            ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1993Data.B450C);
            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new SteelMaterial("", 200000, 1620, 1800, 0.1, SteelMaterial.SteelTypes.Tendon));

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(400, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(100, 450, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(200, 450, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(300, 450, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(400, 450, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(150, 950, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(200, 950, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(300, 950, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(350, 950, 0)),
                new ReinforcedConcreteRebar(rebarP, new Point3d(250, 100, 0), 0.007045),
                new ReinforcedConcreteRebar(rebarP, new Point3d(250, 100, 0), 0.007045)
            };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete());

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
            ShowDomainPoints(failureDomain.Domain);
            //ExportToGmsh(failureDomain.Domain);

            // Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

            /* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3319.17	217.179		0			0
				-2941.5		414.739		0			0
				-2355.62	717.257		0			0
				-1822.17	991.042		0			0
				-1164.02	1323.95		0			0
				-209.629	1731.12		0			0
				167.497		1857.38		0			0
				1099.1		2029.31		0			0
				3190.77		2218.48		0			0
				4502.39		2106.42		0			0
				6191.82		1893.31		0			0
				8512.78		1402.98		0			0
				10248.5		882.735		0			0
				11445		521.508		0			0
				12065.4		326.229		0			0
				12065.4		326.229		0			0
				11577.1		92.8251		0			0
				10727.7		-286.162	0			0
				9554.07		-801.031	0			0
				7896.03		-1321.98	0			0
				6526.62		-1642.82	0			0
				5614.88		-1716.85	0			0
				2884.1		-1650.42	0			0
				1343.13		-1398.91	0			0
				418.76		-1156.47	0			0
				-2329.51	-230.051	0			0
				-2714.37	-88.2293	0			0
				-2891.41	-18.9784	0			0
				-3085.36	59.9323		0			0
				-3279.8		141.959		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
			*/
        }

        [TestMethod]
        public void RectangularSectionPrestressedTest3()
        {
            double rebarDiameter = 20;
            double rebarDiameterPrestress = 22;

            // sezion a T tovescia 
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(300, 0),
                new Point2d(300, 500),
                new Point2d(0, 500),
            }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C25_30);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1993Data.B450C);
            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new SteelMaterial("", 200000, 1620, 1800, 0.035, SteelMaterial.SteelTypes.Tendon));

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebar, new Point3d(40, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(113, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(186, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(260, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(40, 450, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(260, 450, 0)),
                new ReinforcedConcreteRebar(rebarP, new Point3d(150, 100, 0), 1400)
            };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete());

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
            ShowDomainPoints(failureDomain.Domain);

            // Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

            /* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3319.17	217.179		0			0
				-2941.5		414.739		0			0
				-2355.62	717.257		0			0
				-1822.17	991.042		0			0
				-1164.02	1323.95		0			0
				-209.629	1731.12		0			0
				167.497		1857.38		0			0
				1099.1		2029.31		0			0
				3190.77		2218.48		0			0
				4502.39		2106.42		0			0
				6191.82		1893.31		0			0
				8512.78		1402.98		0			0
				10248.5		882.735		0			0
				11445		521.508		0			0
				12065.4		326.229		0			0
				12065.4		326.229		0			0
				11577.1		92.8251		0			0
				10727.7		-286.162	0			0
				9554.07		-801.031	0			0
				7896.03		-1321.98	0			0
				6526.62		-1642.82	0			0
				5614.88		-1716.85	0			0
				2884.1		-1650.42	0			0
				1343.13		-1398.91	0			0
				418.76		-1156.47	0			0
				-2329.51	-230.051	0			0
				-2714.37	-88.2293	0			0
				-2891.41	-18.9784	0			0
				-3085.36	59.9323		0			0
				-3279.8		141.959		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
			*/
        }

        [TestMethod]
        public void RectangularSectionPrestressedTest4()
        {
            double rebarDiameterPrestress = 22;

            // sezion a T tovescia 
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(300, 0),
                new Point2d(300, 500),
                new Point2d(0, 500),
            }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C45_55);
            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new SteelMaterial("", 200000, 1620, 1800, 0.035, SteelMaterial.SteelTypes.Tendon));

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebarP, new Point3d(150, 100, 0), 1400)
            };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
			//ExportToGmsh(section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete());

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
            ShowDomainPoints(failureDomain.Domain);

            // Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

            /* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3319.17	217.179		0			0
				-2941.5		414.739		0			0
				-2355.62	717.257		0			0
				-1822.17	991.042		0			0
				-1164.02	1323.95		0			0
				-209.629	1731.12		0			0
				167.497		1857.38		0			0
				1099.1		2029.31		0			0
				3190.77		2218.48		0			0
				4502.39		2106.42		0			0
				6191.82		1893.31		0			0
				8512.78		1402.98		0			0
				10248.5		882.735		0			0
				11445		521.508		0			0
				12065.4		326.229		0			0
				12065.4		326.229		0			0
				11577.1		92.8251		0			0
				10727.7		-286.162	0			0
				9554.07		-801.031	0			0
				7896.03		-1321.98	0			0
				6526.62		-1642.82	0			0
				5614.88		-1716.85	0			0
				2884.1		-1650.42	0			0
				1343.13		-1398.91	0			0
				418.76		-1156.47	0			0
				-2329.51	-230.051	0			0
				-2714.37	-88.2293	0			0
				-2891.41	-18.9784	0			0
				-3085.36	59.9323		0			0
				-3279.8		141.959		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
			*/
        }

        [TestMethod]
        public void RectangularSectionPrestressedTest5()
        {
            double rebarDiameterPrestress = 22;
            double rebarDiameter = 26;

            // sezion a T tovescia 
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(300, 0),
                new Point2d(300, 500),
                new Point2d(0, 500),
            }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C45_55);
            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new SteelMaterial("", 200000, 1620, 1620, 0.035, SteelMaterial.SteelTypes.Tendon));
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1993Data.B450C);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebar, new Point3d(50, 50, 0), 0),
                new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0), 0),
                new ReinforcedConcreteRebar(rebar, new Point3d(50, 450, 0), 0),
                new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0), 0),
                new ReinforcedConcreteRebar(rebarP, new Point3d(150, 100, 0), 1400),
            };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardNTC2018Concrete());

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
            ShowDomainPoints(failureDomain.Domain);

            // Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

            /* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-1401.12	85.5	
				-1401.12	85.5	
				-1401.12	85.5	
				-1401.12	85.5	
				-1373.1		92.4148	
				-1317.67	105.859	
				-1262.71	118.804	
				-1212.29	130.299	
				-717.748	231.098	
				143.556		395.901	
				322.192		421.293	
				620.263		454.118	
				1217.64		483.973	
				1674.11		448.429	
				2284.87		386.633	
				3107.59		270.144	
				3662.07		169.574	
				4054.66		97.9095	
				4276.12		57	
				4276.12		57	
				4107.86		17.9905	
				3768.47		-51.7735
				3267.18		-150.444
				2483.43		-257.195
				1898.94		-311.154
				1429.49		-344.75	
				785.849		-307.956
				441.517		-268.192
				216.62		-235.861
				-717.748	-60.0982
				-1212.29	40.7005	
				-1262.71	52.1962	
				-1317.67	65.1409	
				-1373.1		78.5852	
				-1401.12	85.5	
				-1401.12	85.5	
				-1401.12	85.5	
				-1401.12	85.5	
			*/
        }
    }
}
