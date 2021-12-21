using System;
using System.Collections.Generic;
using System.Diagnostics;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
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
	public class FailureDomainRCTest : ConcreteTestBase
	{
		[TestMethod]
		public void RectangularSectionTest1()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = 
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			//var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			//ShowDomainPoints(plasticFailureDomain.Domain);
			//ExportToGmsh(plasticFailureDomain.Domain);
			//ExportToGmsh(elasticFailureDomain.Domain);

			//Mesh mesh = plasticFailureDomain.Domain.GetMesh();
			//ExportToGmsh(mesh);

			//Assert.IsTrue(CommonAssertsModelCode (section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-382.775	3.84247		0			0
				-351.973	11.3133		0			0
				-321.43		18.5065		0			0
				-293.415	24.8945		0			0
				-48.0393	75.0195		0			0
				396.513		159.786		0			0
				495.779		173.896		0			0
				661.413		192.136		0			0
				993.368		208.726		0			0
				1234.54		191.47		0			0
				1539.64		162.899		0			0
				1955.48		104.992		0			0
				2236.37		53.6365		0			0
				2427.3		18.3439		0			0
				2523.84		0			0			0
				2523.84		0			0			0
				2427.3		-18.3439	0			0
				2236.37		-53.6365	0			0
				1955.48		-104.992	0			0
				1539.64		-162.899	0			0
				1234.54		-191.47		0			0
				993.368		-208.726	0			0
				661.413		-192.136	0			0
				495.779		-173.896	0			0
				396.513		-159.786	0			0
				-48.0393	-75.0195	0			0
				-293.415	-24.8945	0			0
				-321.43		-18.5065	0			0
				-351.973	-11.3133	0			0
				-382.775	-3.84247	0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
			*/
		}

		[TestMethod]
		public void RectangularSectionTest2()
		{
			double rebarDiameter = 26;

			// \\studio\Software_Development\FilesForTesting\Libs\GPCChecker\ConcreteSolver\Test_2 
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 450, 0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			//ExportToGmsh(failureDomain.Domain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
				-1210.06	-161.031	0			0
				-1154.63	-147.587	0			0
				-1099.66	-134.642	0			0
				-1049.25	-123.146	0			0
				23.2394		93.24		0			0
				1553.29		391.791		0			0
				1731.92		417.183		0			0
				2029.99		450.008		0			0
				2627.37		479.864		0			0
				2998.8		461.326		0			0
				3485.28		422.426		0			0
				4171.02		330.734		0			0
				4631.74		247.27		0			0
				4930.56		192.712		0			0
				5063.07		167.946		0			0
				5063.07		167.946		0			0
				4713.18		99.7023		0			0
				4178.29		-2.06773	0			0
				3481.51		-132.744	0			0
				2465.78		-290.433	0			0
				1649.33		-395.329	0			0
				947.912		-479.864	0			0
				350.534		-450.008	0			0
				52.4623		-417.183	0			0
				-126.174	-391.791	0			0
				-755.328	-273.418	0			0
				-1049.25	-212.745	0			0
				-1099.66	-201.25		0			0
				-1154.63	-188.305	0			0
				-1210.06	-174.861	0			0
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
			*/
		}

		[TestMethod]
		public void RectangularSectionTest3()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,70,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 70, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 70, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 70, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 70, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 450, 0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			//ExportToGmsh(failureDomain.Domain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
				-1177.11	152.928		0			0
				-1146.31	160.399		0			0
				-1115.77	167.592		0			0
				-1087.75	173.98		0			0
				-842.741	224.032		0			0
				-398.608	308.714		0			0
				-299.343	322.824		0			0
				-133.709	341.065		0			0
				259.844		346.567		0			0
				804.641		272.319		0			0
				1413.36		186.756		0			0
				2132.81		71.857		0			0
				2625.26		-19.253		0			0
				3027.75		-94.3		0			0
				3318.18		-149.085	0			0
				3318.18		-149.085	0			0
				3221.74		-167.409	0			0
				3030.92		-202.679	0			0
				2750.14		-254.013	0			0
				2334.46		-311.889	0			0
				2029.51		-340.43		0			0
				1788.49		-357.655	0			0
				1456.54		-341.065	0			0
				1250.91		-315.626	0			0
				1043.42		-282.035	0			0
				-475.611	2.52598		0			0
				-1087.75	124.191		0			0
				-1115.77	130.579		0			0
				-1146.31	137.772		0			0
				-1177.11	145.243		0			0
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
			*/
		}

		[TestMethod]
		public void RectangularSectionTest4()
		{
			double rebarDiameter = 26;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,70,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 70, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 70, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 70, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 70, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,430,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 430, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 430, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 430, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 430, 0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			//ExportToGmsh(failureDomain.Domain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-4155.61	0			0			0
				-4155.61	0			0			0
				-4155.61	0			0			0
				-4155.61	0			0			0
				-4127.59	6.91482		0			0
				-4072.16	20.3592		0			0
				-4017.19	33.3038		0			0
				-3966.78	44.7995		0			0
				-2704.73	295.308		0			0
				403.698		877.967		0			0
				808.584		944.084		0			0
				1190.26		991.959		0			0
				1916.42		998.635		0			0
				3007.94		843.881		0			0
				4214.5		668.766		0			0
				5620.33		440.858		0			0
				6584.38		262.073		0			0
				7386.54		112.195		0			0
				7980.61		0			0			0
				7980.61		0			0			0
				7386.54		-112.195	0			0
				6584.38		-262.073	0			0
				5620.33		-440.858	0			0
				4214.5		-668.766	0			0
				3007.94		-843.881	0			0
				1916.42		-998.635	0			0
				1190.26		-991.959	0			0
				808.584		-944.084	0			0
				403.698		-877.967	0			0
				-2704.73	-295.308	0			0
				-3966.78	-44.7995	0			0
				-4017.19	-33.3038	0			0
				-4072.16	-20.3592	0			0
				-4127.59	-6.91482	0			0
				-4155.61	0			0			0
				-4155.61	0			0			0
				-4155.61	0			0			0
			*/
		}

		[TestMethod]
		[TestCategory("Rebar with fragile material")]
		public void RectangularSectionTest5()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, 
				ConcreteMaterialEN1992.C35_45, new RebarMaterial("", 200000, 450, 450, 0.002));

			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			//ExportToGmsh(failureDomain.Domain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));
		}

		[TestMethod]
		[TestCategory("Rebar with fragile material")]
		public void RectangularSectionTest6()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover,
				ConcreteMaterialEN1992.C35_45, new RebarMaterial("", 200000, 450, 450, 0.002));

			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			//ExportToGmsh(failureDomain.Domain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));
		}

		[TestMethod]
		[TestCategory("No Rebars")]
		public void RectangularSectionTest7()
		{
			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			Assert.IsNull(plasticFailureDomain);
			Assert.IsNull(elasticFailureDomain);

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-382.775	3.84247		0			0
				-351.973	11.3133		0			0
				-321.43		18.5065		0			0
				-293.415	24.8945		0			0
				-48.0393	75.0195		0			0
				396.513		159.786		0			0
				495.779		173.896		0			0
				661.413		192.136		0			0
				993.368		208.726		0			0
				1234.54		191.47		0			0
				1539.64		162.899		0			0
				1955.48		104.992		0			0
				2236.37		53.6365		0			0
				2427.3		18.3439		0			0
				2523.84		0			0			0
				2523.84		0			0			0
				2427.3		-18.3439	0			0
				2236.37		-53.6365	0			0
				1955.48		-104.992	0			0
				1539.64		-162.899	0			0
				1234.54		-191.47		0			0
				993.368		-208.726	0			0
				661.413		-192.136	0			0
				495.779		-173.896	0			0
				396.513		-159.786	0			0
				-48.0393	-75.0195	0			0
				-293.415	-24.8945	0			0
				-321.43		-18.5065	0			0
				-351.973	-11.3133	0			0
				-382.775	-3.84247	0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
			*/
		}

		[TestMethod]
		[TestCategory("Asymmetric rebars")]
		public void RectangularSectionTest8()
		{
			double rebarDiameter = 16;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 300),
				new Point2d(0, 300)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			ExportToGmsh(section);
			ExportToGmsh(plasticFailureDomain.Domain);
			ExportToGmsh(elasticFailureDomain.Domain);

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
				-387.858	-38.524		0			0
				-377.392	-36.9803	0			0
				-367.221	-35.5046	0			0
				-357.954	-34.1829	0			0
				-337.809	-31.3823	0			0
				-80.3779	-3.54166	0			0
				136.213		18.5814		0			0
				374.925		42.638		0			0
				669.077		70.207		0			0
				821.321		81.3837		0			0
				998.438		85.8304		0			0
				1425.66		65.3478		0			0
				1560.6		50.8911		0			0
				1641.57		42.217		0			0
				1668.56		39.3257		0			0
				1668.56		39.3257		0			0
				1555.4		27.817		0			0
				1379.51		9.65127		0			0
				1149.65		-14.2971	0			0
				308.539		-76.169		0			0
				34.8079		-81.3837	0			0
				-62.0942	-75.7413	0			0
				-172.726	-66.5242	0			0
				-227.918	-60.8195	0			0
				-260.99		-57.0485	0			0
				-337.809	-47.269		0			0
				-357.954	-44.4684	0			0
				-367.221	-43.1467	0			0
				-377.392	-41.671		0			0
				-387.858	-40.1273	0			0
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
			*/
		}

		[TestMethod]
		[TestCategory("Asymmetric rebars and force not in centroid")]
		public void RectangularSectionTest9()
		{
			double rebarDiameter = 16;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 300),
				new Point2d(0, 300)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(new CoordinateSystem(new Point2d(-150, -150), 
				Vector2d.XAxis, Vector2d.YAxis));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			ExportToGmsh(plasticFailureDomain.Domain);
			ExportToGmsh(elasticFailureDomain.Domain);

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
				-387.858	-38.524		0			0
				-377.392	-36.9803	0			0
				-367.221	-35.5046	0			0
				-357.954	-34.1829	0			0
				-337.809	-31.3823	0			0
				-80.3779	-3.54166	0			0
				136.213		18.5814		0			0
				374.925		42.638		0			0
				669.077		70.207		0			0
				821.321		81.3837		0			0
				998.438		85.8304		0			0
				1425.66		65.3478		0			0
				1560.6		50.8911		0			0
				1641.57		42.217		0			0
				1668.56		39.3257		0			0
				1668.56		39.3257		0			0
				1555.4		27.817		0			0
				1379.51		9.65127		0			0
				1149.65		-14.2971	0			0
				308.539		-76.169		0			0
				34.8079		-81.3837	0			0
				-62.0942	-75.7413	0			0
				-172.726	-66.5242	0			0
				-227.918	-60.8195	0			0
				-260.99		-57.0485	0			0
				-337.809	-47.269		0			0
				-357.954	-44.4684	0			0
				-367.221	-43.1467	0			0
				-377.392	-41.671		0			0
				-387.858	-40.1273	0			0
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
				-393.257	-39.3257	0			0
			*/
		}

		[TestMethod]
		[TestCategory("Force not in centroid")]
		public void RectangularSectionTest10()
		{
			double rebarDiameter = 16;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 300),
				new Point2d(0, 300)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(new CoordinateSystem(new Point2d(150, 0),
				Vector2d.XAxis, Vector2d.YAxis));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();

			//ExportToGmsh(plasticFailureDomain.Domain.GetMesh());

			Point3d max = new Point3d(309 * 1000000, 0, 2062 * 1000);	// da vca
			Point3d min = new Point3d(-118 * 1000000, 0, -787 * 1000);

			CommonAssertsDomainBoundingBox(section, plasticFailureDomain.Domain, max, min);
		}

		[TestMethod]
		[TestCategory("Force not in centroid")]
		public void RectangularSectionTest11()
		{
			double rebarDiameter = 16;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 300),
				new Point2d(0, 300)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(new CoordinateSystem(new Point2d(0, 150),
				Vector2d.XAxis, Vector2d.YAxis));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			//var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			//ExportToGmsh(plasticFailureDomain.Domain);
			//ExportToGmsh(elasticFailureDomain.Domain);
		}

		[TestMethod]
		[TestCategory("Force not in centroid")]
		public void RectangularSectionTest12()
		{
			double rebarDiameter = 16;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 300),
				new Point2d(0, 300)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(new CoordinateSystem(new Point2d(150, 0),
				Vector2d.XAxis, Vector2d.YAxis));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			//var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			//ExportToGmsh(plasticFailureDomain.Domain);
			//ExportToGmsh(elasticFailureDomain.Domain);

			Point3d max = new Point3d(238 * 1000000, 0, 1590 * 1000);   // da vca
			Point3d min = new Point3d(-47.2 * 1000000, 0, -315 * 1000);

			CommonAssertsDomainBoundingBox(section, plasticFailureDomain.Domain, max, min);
		}

		[TestMethod]
		public void SquareSectionTest1()
		{
			double rebarDiameter = 18;
			double height = 300;
			double concreteCover = 50;
			int numberOfRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, numberOfRebars, ConcreteMaterialEN1992.C45_55);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			//ExportToGmsh(failureDomain.Domain);
		}

		[TestMethod]
		public void CircularSectionTest1()
		{
			// sezione circolare diametro 500
			double rebarDiameter = 16;
			double sectionDiameter = 500;

			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(434.77591, 326.536678, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(391.421355, 391.421357, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(326.536686, 434.775907, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(173.463322, 434.77591, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(108.578643, 391.421355, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(65.224093, 326.536686, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(65.22409, 173.463322, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(108.578645, 108.578643, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(173.463314, 65.224093, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250.0, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(326.536678, 65.22409, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(391.421357, 108.578645, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(434.775907, 173.463314, 0))
			};


			ConcreteSectionCircular section = new ConcreteSectionCircular(sectionDiameter,
				new ConcreteMaterialEN1992("", 45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			//ExportToGmsh(failureDomain.Domain);

			//// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-1258.81	4.84e-014	0			0
				-1258.81	4.84e-014	0			0
				-1258.81	4.84e-014	0			0
				-1258.81	4.84e-014	0			0
				-1251.8		1.72729		0			0
				-1230.47	6.88051		0			0
				-1200.35	13.9574		0			0
				-1166.33	21.7031		0			0
				-947.661	66.0554		0			0
				14.6662		233.636		0			0
				427.718		287.822		0			0
				1083.72		350.511		0			0
				2479.81		388.665		0			0
				3208.22		357.463		0			0
				4100.23		295.198		0			0
				5147.35		172.755		0			0
				5689.17		88.0575		0			0
				6064.06		29.8863		0			0
				6256.29		-4.84e-014	0			0
				6256.29		-5.21e-014	0			0
				6064.06		-29.8863	0			0
				5689.17		-88.0575	0			0
				5147.35		-172.755	0			0
				4100.23		-295.198	0			0
				3208.22		-357.463	0			0
				2479.81		-388.665	0			0
				1083.72		-350.511	0			0
				427.718		-287.822	0			0
				14.6662		-233.636	0			0
				-947.661	-66.0554	0			0
				-1166.33	-21.7031	0			0
				-1200.35	-13.9574	0			0
				-1230.47	-6.88051	0			0
				-1251.8		-1.72729	0			0
				-1258.81	5.21e-014	0			0
				-1258.81	5.21e-014	0			0
				-1258.81	5.21e-014	0			0
				-1258.81	5.21e-014	0			0
			*/
		}

		[TestMethod]
		public void CHSSectionTest1()
		{
			double rebarDiameter = 26;
			double diameterExternal = 1000;
			double thickness = 100;
			double concreteCover = 50;
			int numberOfRebars = 32;

			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));
			ConcreteSectionCHS section = new ConcreteSectionCHS(diameterExternal, thickness, ConcreteMaterialEN1992.C25_30);
			section.AddRadialRebars(concreteCover, numberOfRebars, rebar);

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			//ExportToGmsh(failureDomain.Domain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-6648.08	-8.94e-014	0			0
				-6648.08	-8.94e-014	0			0
				-6648.08	-8.94e-014	0			0
				-6648.08	-8.94e-014	0			0
				-6631.33	8.24822		0			0
				-6514.99	62.2599		0			0
				-6096.85	249.778		0			0
				-5476.35	520.861		0			0
				-4308.15	1009.75		0			0
				-1593.06	1926.89		0			0
				-739.734	2119.45		0			0
				569.251		2298.39		0			0
				3195.96		2169.78		0			0
				4565.66		1864.64		0			0
				5996.74		1520.36		0			0
				7665.2		1043.25		0			0
				8887.74		612.691		0			0
				9907.84		257.905		0			0
				10647		8.94e-014	0			0
				10647		1.49e-013	0			0
				9907.84		-257.905	0			0
				8887.74		-612.691	0			0
				7665.2		-1043.25	0			0
				5996.74		-1520.36	0			0
				4565.66		-1864.64	0			0
				3195.96		-2169.78	0			0
				569.251		-2298.39	0			0
				-739.734	-2119.45	0			0
				-1593.06	-1926.89	0			0
				-4308.15	-1009.75	0			0
				-5476.35	-520.861	0			0
				-6096.85	-249.778	0			0
				-6514.99	-62.2599	0			0
				-6631.33	-8.24822	0			0
				-6648.08	-1.49e-013	0			0
				-6648.08	-1.49e-013	0			0
				-6648.08	-1.49e-013	0			0
				-6648.08	-1.49e-013	0			0
			*/
		}

		[TestMethod]
		public void SectionTTest1()
		{
			double rebarDiameter = 26;

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
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

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
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 950, 0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetPlasticFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);

			//ExportToGmsh(failureDomain.Domain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
				-2434.32	-81.4583	0			0
				-2056.65	116.101		0			0
				-1470.77	418.62		0			0
				-937.317	692.404		0			0
				-279.167	1025.32		0			0
				675.223		1432.48		0			0
				1052.35		1558.74		0			0
				1983.95		1730.67		0			0
				4075.62		1919.84		0			0
				5387.24		1807.78		0			0
				7076.68		1594.67		0			0
				9348.02		1121.09		0			0
				11014.7		624.156		0			0
				12142.1		286.244		0			0
				12693.4		114.279		0			0
				12693.4		114.279		0			0
				12175.3		-109.057	0			0
				11296		-477.977	0			0
				10092.6		-982.778	0			0
				8455.67		-1510.86	0			0
				7107.4		-1838.84	0			0
				6216.79		-1920		0			0
				3567.49		-1881.07	0			0
				2108.01		-1657.05	0			0
				1265.12		-1442.12	0			0
				-1444.66	-528.688	0			0
				-1829.52	-386.867	0			0
				-2006.56	-317.616	0			0
				-2200.5		-238.705	0			0
				-2394.95	-156.679	0			0
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
			*/
		}


	}
}
