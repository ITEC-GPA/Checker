using System;
using System.Collections.Generic;
using System.Diagnostics;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
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
	public class ValidationTestStressAnalysis : ConcreteTestBase
	{
		[TestMethod]
		public void VCA_1()
		{
			double elasticModulus = 31.476;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, 18, 50, GetLinearConcreteMaterial(elasticModulus));
			ReinforcedConcreteRebar[] rebars = section.GetRebars();

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], -2.703),
				(section.Shape.Fill[2], -12.34),
				(section.Shape.Fill[3], -2.343),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 29.87),
				(rebars[1], -13.1),
				(rebars[2], -63.23),
				(rebars[3], -20.27),
			};

			CommonAssertsVCA(result[0], section, expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		public void VCA_2()
		{
			double elasticModulus = 31.476;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, 18, 50, GetLinearConcreteMaterial(elasticModulus));
			var rebars = section.GetRebars();

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 50 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], -12.54),
				(section.Shape.Fill[3], -3.547),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 164.9),
				(rebars[1], 125.9),
				(rebars[2], -49.78),
				(rebars[3], -10.83),
			};

			CommonAssertsVCA(result[0], section, expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		public void VCA_3()
		{
			double elasticModulus = 34.077;
			double rebarDiameter = 26;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ConcreteMaterialEN1992 concreteMaterial = GetLinearConcreteMaterial(elasticModulus);
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,250,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,250,0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 50 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], -3.926),
				(section.Shape.Fill[2], -16.01),
				(section.Shape.Fill[3], 0.0),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 88.76),
				(rebars[1], 40.89),
				(rebars[2], -6.98),
				(rebars[3], -65.04),
				(rebars[4], -17.17),
				(rebars[5], 30.7),
				(rebars[6], 59.73),
				(rebars[7], -36.01),
			};

			CommonAssertsVCA(result[0], section, expConcreteTensions, expRebarTensions, 0.06);
		}

		[TestMethod]
		public void VCA_4()
		{
			double elasticModulus = 36.283;
			double rebarDiameter = 26;
			double h = 500;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(h, 0),
				new Point2d(h, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = GetLinearConcreteMaterial(elasticModulus);
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 450, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 300 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], -22.97),
				(section.Shape.Fill[3], -15.51),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 259.2),
				(rebars[1], 251.0),
				(rebars[2], 242.8),
				(rebars[3], 234.5),
				(rebars[4], 226.3),
				(rebars[5], -50.8),
				(rebars[6], -59.02),
				(rebars[7], -67.28),
				(rebars[8], -75.47),
				(rebars[9], -83.7),
			};

			CommonAssertsVCA(result[0], section, expConcreteTensions, expRebarTensions, 0.06);
		}

		[TestMethod]
		public void VCA_5()
		{
			double elasticModulus = 36.283;
			double rebarDiameter = 8;
			double h = 500;
			
			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(h, h, rebarDiameter, 50, 5, GetLinearConcreteMaterialTensile(elasticModulus));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			var rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 40 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 1.88),
				(section.Shape.Fill[1], 1.88),
				(section.Shape.Fill[2], -1.88),
				(section.Shape.Fill[3], -1.88),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 8.273),
				(rebars[1], 8.273),
				(rebars[2], 8.273),
				(rebars[3], 8.273),
				(rebars[4], 8.273),
				(rebars[5], -8.273),
				(rebars[6], -8.273),
				(rebars[7], -8.273),
				(rebars[8], -8.273),
				(rebars[9], -8.273),
			};

			CommonAssertsVCA(result[0], section, expConcreteTensions, expRebarTensions);

		}

		[TestMethod]
		[TestCategory("Rebar out of section")]
		public void VCA_6()
		{
			double elasticModulus = 36.283;
			double rebarDiameter = 16;
			double h = 500;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(h, 0),
				new Point2d(h, h),
				new Point2d(0, h)
			}));

			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, RebarMaterial.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50, -50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, -50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, -50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, -50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, -50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 550, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 550, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 550, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 550, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 550, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(new ShapeEx(shape, GetLinearConcreteMaterial(elasticModulus)));
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 200 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, false);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], -14.21),
				(section.Shape.Fill[3], -14.21),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 184.0),
				(rebars[1], 184.0),
				(rebars[2], 184.0),
				(rebars[3], 184.0),
				(rebars[4], 184.0),
				(rebars[5], -102.0),
				(rebars[6], -102.0),
				(rebars[7], -102.0),
				(rebars[8], -102.0),
				(rebars[9], -102.0),
			};

			CommonAssertsVCA(result[0], section, expConcreteTensions, expRebarTensions);
		}
				
	}
}
