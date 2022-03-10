using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
	[TestClass]
	public class StressAnalysisRCTest : ConcreteTestBase
	{
		[TestMethod]
		public void RectangularSectionTest1()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			var section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[] 
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)) 
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest2()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 3, ConcreteMaterialEN1992.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest3()
		{
			double rebarDiameter = 18;
			double height = 800;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,750,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200,750,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,750,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000,  GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -0 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000,  GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000,  GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest4()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 3, ConcreteMaterialEN1992.C25_30);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest5()
		{
			double rebarDiameter = 26; 
			double height = 1000;
			double width = 500;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 5, ConcreteMaterialEN1992.C45_55, SteelMaterial.B450C);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-4000 * 1000, 0, 0, 0, 500 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-3000 * 1000, 0, 0, 0, 300 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 220 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest6()
		{
			double rebarDiameter = 26;
			double elasticModulus = 34.077;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 3, GetLinearConcreteMaterial(elasticModulus));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 150 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-885 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -200 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -80 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest7()
		{
			double rebarDiameter = 24;
			double height = 800;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,750,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200,750,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,750,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1500 * 1000, 0, 0, 0, 500 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 300 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1200 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 220 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest8()
		{
			double rebarDiameter = 26;
			double height = 700;
			double width = 300;
			double concreteCover = 50;
				
			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 5, ConcreteMaterialEN1992.C45_55);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-4000 * 1000, 0, 0, 0, 500 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-3500 * 1000, 0, 0, 0, 50 * 1000000, 200 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-3000 * 1000, 0, 0, 0, 300 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1100 * 1000, 0, 0, 0, 100 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-2500 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 150 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 250 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 400 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 150 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1870 * 1000, 0, 0, 0, 250 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest9()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 600),
				new Point2d(0, 600)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{				
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 60, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 60, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 60, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 60, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 60, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 540,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 540,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 540,0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 200 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest10()
		{
			double rebarDiameter = 16;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 600),
				new Point2d(0, 600)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 150, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 200, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 300, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 350, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 400, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 500, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 550, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 20 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 40 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 60 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 80 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest16()
		{
			double rebarDiameter = 6;
			double height = 80;
			double width = 1000;
			double copriferro = 25;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialModelCode2010($"FCM {45}-3.5", 45,
				ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle,
				0.45 * 0.92, 0.3 * 0.76, 0.00003, 0.02, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear));
			RebarSectionCircular rebarSection = new RebarSectionCircular(rebarDiameter, SteelMaterial.B500C);
			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebarSection, new Point3d(100, copriferro, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(300, copriferro, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(500, copriferro, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(700, copriferro, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(900, copriferro, 0)),
			};

			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 2.5 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			//StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			//for (int i = 0; i < slsResult.Length; i++)
			//	Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		[TestCategory("Force reference point not in centroid")]
		public void RectangularSectionTest11()
		{
			double rebarDiameter = 16;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, GetLinearConcreteMaterial(31.476));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			var rebars = section.GetRebars();
			
			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(new Point2d(-150, 0) + section.Centroid,
				Vector2d.XAxis, Vector2d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
{
				(section.Shape.Fill[0], -4.341),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], 0.0),
				(section.Shape.Fill[3], -4.341),
};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], -14.03),
				(rebars[1], 40.12),
				(rebars[2], 40.12),
				(rebars[3], -14.03),
			};

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(CommonAssertsVCA(slsResult[i], section, expConcreteTensions, expRebarTensions, 0.07));
		}

		[TestMethod]
		[TestCategory("Force reference point not in centroid")]
		public void RectangularSectionTest12()
		{
			double rebarDiameter = 16;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, GetLinearConcreteMaterial(31.476));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			var rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(new Point2d(150, 0), Vector2d.XAxis, Vector2d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
{
				(section.Shape.Fill[0], -3.988),
				(section.Shape.Fill[1], -3.988),
				(section.Shape.Fill[2], 0.0),
				(section.Shape.Fill[3], 0.0),
};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], -18.3),
				(rebars[1], -18.3),
				(rebars[2], 37.87),
				(rebars[3], 37.87),
			};

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(CommonAssertsVCA(slsResult[i], section, expConcreteTensions, expRebarTensions, 0.07));
		}

		[TestMethod]
		[TestCategory("Force reference point not in centroid")]
		public void RectangularSectionTest13()
		{
			double rebarDiameter = 16;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, GetLinearConcreteMaterial(31.476));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			var rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(new Point2d(-150, -250) + section.Centroid,
				Vector2d.XAxis, Vector2d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
{
				(section.Shape.Fill[0], -10.15),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], 0.0),
				(section.Shape.Fill[3], 0.0),
};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], -39.33),
				(rebars[1], 26.08),
				(rebars[2], 95.99),
				(rebars[3], 30.58),
			};

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(CommonAssertsVCA(slsResult[i], section, expConcreteTensions, expRebarTensions, 0.07));
		}

		[TestMethod]
		[TestCategory("Force reference point not in centroid")]
		public void RectangularSectionTest14()
		{
			double rebarDiameter = 16;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, GetLinearConcreteMaterial(31.476));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			var rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(new Point2d(50, 50),
				Vector2d.XAxis, Vector2d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
{
				(section.Shape.Fill[0], -6.274),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], 0.0),
				(section.Shape.Fill[3], 0.0),
};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], -27.2),
				(rebars[1], 2.284),
				(rebars[2], 44.4),
				(rebars[3], 14.91),
			};

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(CommonAssertsVCA(slsResult[i], section, expConcreteTensions, expRebarTensions, 0.07));
		}

		[TestMethod]
		public void RectangularSectionTest15()
		{
			double rebarDiameter = 16;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, GetLinearConcreteMaterial(31.476));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			var rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], -9.534),
				(section.Shape.Fill[3], -9.534),
};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 294.3),
				(rebars[1], 294.3),
				(rebars[2], -21.12),
				(rebars[3], -21.12),
			};

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(CommonAssertsVCA(slsResult[i], section, expConcreteTensions, expRebarTensions, 0.07));
		}

		[TestMethod]
		public void SquareSectionTest1()
		{
			double rebarDiameter = 20;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992.C45_55);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -50 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest2()
		{
			double rebarDiameter = 20;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 3, ConcreteMaterialEN1992.C25_30);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[] { new ResultBeamForces(-100 * 1000, 0, 0, 0, 20 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)) };

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest3()
		{
			double rebarDiameter = 20;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 5, ConcreteMaterialEN1992.C25_30);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest4()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, 5, ConcreteMaterialEN1992.C25_30);

			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(+200 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(+100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-900 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1200 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1500 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest5()
		{
			double rebarDiameter = 26;
			double height = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(height, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(+250 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest6()
		{
			double rebarDiameter = 14;
			double height = 400;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSectionBottomSideRebars(height, height, rebarDiameter, concreteCover, 4, ConcreteMaterialEN1992.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 10 * 1000000, 30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

	}
}
