using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.Checkers;
using System.Collections.Generic;
using GPC.Model.Standards;
using GPC.Model.Sections;
using GPC.Checkers.Concrete.Attributes;
using GPC.Model.Results;
using GPC.TestUtilities;

namespace ConcreteTests
{
	[TestClass]
	public class ULSCheckConcreteTest : ConcreteTest
	{

		[TestMethod]
		public void ULSCheckRectangularSectionTest1()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces forces1 = new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 0, CoordinateSystem.Global);
			ResultBeamForces forces2 = new ResultBeamForces(-2000 * 1000, 0, 0, 0, 100 * 1000000, 0, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, new ResultBeamForces[] {forces1, forces2});
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			ExportToGmsh(failureDomain.Domain);
		}

		[TestMethod]
		public void ULSCheckRectangularSectionTest2()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces forces1 = new ResultBeamForces(-400 * 1000, 0, 0, 0, 100 * 1000000, 0, CoordinateSystem.Global);
			ResultBeamForces forces2 = new ResultBeamForces(-800 * 1000, 0, 0, 0, 200 * 1000000, 0, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, new ResultBeamForces[] { forces1, forces2 });
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			ExportToGmsh(failureDomain.Domain);
		}

		[TestMethod]
		public void ULSCheckRectangularSectionTest3()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces forces = new ResultBeamForces(500 * 1000, 0, 0, 0, 0 * 1000000, 0, CoordinateSystem.Global);
			ResultBeamForces forces1 = new ResultBeamForces(100 * 1000, 0, 0, 0, 10 * 1000000, 0, CoordinateSystem.Global);
			ResultBeamForces forces2 = new ResultBeamForces(200 * 1000, 0, 0, 0, 20 * 1000000, 0, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, new ResultBeamForces[] { forces, forces1, forces2 });
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			var failureDomain = sectionChecker.GetFailureDomainResult();
			ShowDomainPoints(failureDomain.Domain);
			ExportToGmsh(failureDomain.Domain);
			
		}

	}
}
