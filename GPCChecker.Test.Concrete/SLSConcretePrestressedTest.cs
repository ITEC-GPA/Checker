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
	public class SLSConcretePrestressedTest : ConcreteTest
	{
		[TestMethod]
		public void SquareSectionPrestressed1()
		{
			double rebarDiameter = 20;
			double rebarDiameterPrestress = 20;

			// sezione rettangolare 300x300
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
				new ReinforcedConcreteRebar(rebarP, new Point3d(150, 150, 0), 0.007045) };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 20 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();
		}

	}
}
