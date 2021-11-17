using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using GPC.Checkers.Concrete.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.TestUtilities;

namespace ConcreteTests
{
	[TestClass]
	public class SLSConcreteTest : ConcreteTest
	{
		[TestMethod]
		public void RectangularSectionTest1()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(-100 *  1000, 0, 0, 0, 10 * 1000000, 0 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces, standard));
		}

		[TestMethod]
		public void RectangularSectionTest2()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 500),
																		new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(100 * 1000, 0, 0, 0, 0 * 1000000, 10 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();
		}

		[TestMethod]
		public void RectangularSectionTest3()
		{
			double rebarDiameter = 18;
			double height = 800;
			double width = 400;
			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(width, 0),
																		new Point2d(width, height),
																		new Point2d(0, height) }));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,750,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200,750,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(350,750,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(0 * 1000, 0, 0, 0, 10 * 1000000, 0 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();
		}

		[TestMethod]
		public void SquareSectionest1()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 300),
																		new Point2d(0, 300) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(100 * 1000, 0, 0, 0, 10 * 1000000, 0 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();
		}

		[TestMethod]
		public void SquareSectionTest2()
		{
			double rebarDiameter = 20;

			// sezione rettangolare 300x300
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 300),
				new Point2d(0, 300) }));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(-100 * 1000, 0, 0, 0, 20 * 1000000, 10 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = 
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(section.Centroid);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces, standard));
		}
	}
}
