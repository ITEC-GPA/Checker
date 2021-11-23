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
	public class SLSConcreteTest : ConcreteTest
	{
		[TestMethod]
		public void RectangularSectionTest1()
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

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[] 
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, CoordinateSystem.Global),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, CoordinateSystem.Global),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, CoordinateSystem.Global),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, CoordinateSystem.Global) 
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest2()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{   new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
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
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000,  new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -0 * 1000000, -50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000,  new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000,  new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest4()
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

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, CoordinateSystem.Global),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, CoordinateSystem.Global),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, CoordinateSystem.Global),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, CoordinateSystem.Global)
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest5()
		{
			double rebarDiameter = 26;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(500, 0),
				new Point2d(500, 1000),
				new Point2d(0, 1000)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,950,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150,950,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,950,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,950,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450,950,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-4000 * 1000, 0, 0, 0, 500 * 1000000, 80 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-3000 * 1000, 0, 0, 0, 300 * 1000000, 120 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 220 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest6()
		{
			double rebarDiameter = 26;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{   
				new Point2d(0, 0),
				new Point2d(200, 0),
				new Point2d(200, 600),
				new Point2d(0, 600)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,550,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100,550,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150,550,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 500 * 1000000, 80 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-2500 * 1000, 0, 0, 0, 300 * 1000000, -120 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1500 * 1000, 0, 0, 0, -250 * 1000000, -20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -200 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -80 * 1000000, -20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
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
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1500 * 1000, 0, 0, 0, 500 * 1000000, 80 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 300 * 1000000, 120 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1200 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 220 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest8()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 700),
				new Point2d(0, 700)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,0,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 0, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 0, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 0, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 0, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,700,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100,700,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150,700,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200,700,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,700,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-4000 * 1000, 0, 0, 0, 500 * 1000000, 80 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-3000 * 1000, 0, 0, 0, 300 * 1000000, 120 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-2500 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-4500 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 220 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 250 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1870 * 1000, 0, 0, 0, 250 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionest1()
		{
			double rebarDiameter = 20;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 300),
				new Point2d(0, 300)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest2()
		{
			double rebarDiameter = 20;

			// sezione rettangolare 300x300
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
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[] { new ResultBeamForces(-100 * 1000, 0, 0, 0, 20 * 1000000, 10 * 1000000, CoordinateSystem.Global) };

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest3()
		{
			double rebarDiameter = 20;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(500, 0),
				new Point2d(500, 500),
				new Point2d(0, 500)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 450,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, CoordinateSystem.Global),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, CoordinateSystem.Global),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, CoordinateSystem.Global),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, CoordinateSystem.Global)
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest4()
		{
			double rebarDiameter = 26;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(500, 0),
				new Point2d(500, 500),
				new Point2d(0, 500)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 450,0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(+200 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(+100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-900 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1200 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1500 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}
	}
}
