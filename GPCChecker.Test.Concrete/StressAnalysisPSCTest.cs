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
	public class StressAnalysisPSCTest : ConcreteTestBase
	{
		[TestMethod]
		public void SquareSectionPrestressed1()
		{
			double rebarDiameter = 20;
			double rebarDiameterPrestress = 20;


			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 300),
				new Point2d(0, 300) }));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));
			RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new RebarMaterial("", 200000, 1620, 1800));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250,0)),
				new ReinforcedConcreteRebar(rebarP, new Point3d(150, 150, 0), 1400) };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[] {
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 40 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -40 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -30 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)) };

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(SLSCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionPrestressed2()
		{
			double rebarDiameter = 26;
			double rebarDiameterPrestress = 26;

			// SquareSectionPrestressed2
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
				new Point2d(0, 0),
				new Point2d(500, 0),
				new Point2d(500, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));
			RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new RebarMaterial("", 200000, 1620, 1800));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 450,0)),
				new ReinforcedConcreteRebar(rebarP, new Point3d(250, 250, 0), 0.007045) };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-400 * 1000, 0, 0, 0, 150 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, 80 * 1000000, 150 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-900 * 1000, 0, 0, 0, 100 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 20 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section))
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
		public void RectangularSectionPrestressed1()
		{
			double rebarDiameter = 20;
			double rebarDiameterPrestress = 26;

			// SquareSectionPrestressed2
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
				new Point2d(0, 0),
				new Point2d(400, 0),
				new Point2d(400, 700),
				new Point2d(0, 700) }));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));
			RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new RebarMaterial("", 200000, 1620, 1800));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 650,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 650,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 650,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 650,0)),
				new ReinforcedConcreteRebar(rebarP, new Point3d(200, 350, 0), 0.007045) };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000,GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 120 * 1000000,GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-400 * 1000, 0, 0, 0, 150 * 1000000, 20 * 1000000,GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, 80 * 1000000, 150 * 1000000,GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-900 * 1000, 0, 0, 0, 100 * 1000000, 100 * 1000000,GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 20 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
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
