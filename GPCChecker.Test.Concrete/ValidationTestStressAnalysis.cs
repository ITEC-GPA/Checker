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
using System.Diagnostics;
using GPC.Checkers.Concrete.Helper;

namespace ConcreteTests
{
	[TestClass]
	public class ValidationTestStressAnalysis : ConcreteTest
	{
		[TestMethod]
		public void VCA_1()
		{
			double tolerance = 0.05;
			double elasticModulusFactor = 0.85 / 1.5;

			double rebarDiameter = 18;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 0.0,
				new StressStrainTable(new double[] { 0, -31.476 / elasticModulusFactor, -62.9152 / elasticModulusFactor }, new double[] { 0, -0.001, -0.002 }),
				new StressStrainTable(new double[] { 0, 0 }, new double[] { 0, 0.001 }));
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
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
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, -30 * 1000000, CoordinateSystem.Global),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension();
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension();

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

			Console.WriteLine($"Tensions associated with force {result[0].Force.N}, {result[0].Force.M1}, {result[0].Force.M2} ");

			for (int i = 0; i < rebarTensions.Length; i++)
				Console.WriteLine($"Rebar {i}: {rebarTensions[i].rebar.Position.X}, {rebarTensions[i].rebar.Position.Y}. " +
					$"Tension = {Math.Round(rebarTensions[i].tension, 2)}");

			for (int i = 0; i < concreteTensions.Length; i++)
				Console.WriteLine($"Vertices {i}: {concreteTensions[i].point}. Tension = {Math.Round(concreteTensions[i].tension, 2)}");

			for (int i = 0; i < section.Shape.Fill.Count; i++)
				if(concreteTensions[i].tension != 0)
					Assert.IsTrue(Math.Abs((concreteTensions[i].tension - expConcreteTensions[i].tension) / concreteTensions[i].tension) < tolerance);

			for (int i = 0; i < rebarTensions.Length; i++)
				if (rebarTensions[i].tension != 0)
					Assert.IsTrue(Math.Abs((rebarTensions[i].tension - expRebarTensions[i].tension) / rebarTensions[i].tension) < tolerance);
		}

		[TestMethod]
		public void VCA_2()
		{
			double tolerance = 0.05;
			double elasticModulusFactor = 0.85 / 1.5;

			double rebarDiameter = 18;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 0.0,
				new StressStrainTable(new double[] { 0, -31.476 / elasticModulusFactor, -62.9152 / elasticModulusFactor }, new double[] { 0, -0.001, -0.002 }),
				new StressStrainTable(new double[] { 0, 0 }, new double[] { 0, 0.001 }));
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
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
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 50 * 1000000, -10 * 1000000, CoordinateSystem.Global),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension();
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension();

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

			Console.WriteLine($"Tensions associated with force {result[0].Force.N}, {result[0].Force.M1}, {result[0].Force.M2} ");

			for (int i = 0; i < rebarTensions.Length; i++)
				Console.WriteLine($"Rebar {i}: {rebarTensions[i].rebar.Position.X}, {rebarTensions[i].rebar.Position.Y}. " +
					$"Tension = {Math.Round(rebarTensions[i].tension, 2)}");

			for (int i = 0; i < concreteTensions.Length; i++)
				Console.WriteLine($"Vertices {i}: {concreteTensions[i].point}. Tension = {Math.Round(concreteTensions[i].tension, 2)}");

			for (int i = 0; i < section.Shape.Fill.Count; i++)
				if (concreteTensions[i].tension != 0)
					Assert.IsTrue(Math.Abs((concreteTensions[i].tension - expConcreteTensions[i].tension) / concreteTensions[i].tension) < tolerance);

			for (int i = 0; i < rebarTensions.Length; i++)
				if (rebarTensions[i].tension != 0)
					Assert.IsTrue(Math.Abs((rebarTensions[i].tension - expRebarTensions[i].tension) / rebarTensions[i].tension) < tolerance);
		}

		[TestMethod]
		public void VCA_3()
		{
			double tolerance = 0.06;
			double elasticModulusFactor = 0.85 / 1.5;
			double elasticModulus = 34.077;
			double rebarDiameter = 26;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 0.0,
				new StressStrainTable(new double[] { 0, -elasticModulus / elasticModulusFactor, -elasticModulus * 2.0 / elasticModulusFactor }, 
				new double[] { 0, -0.001, -0.002 }),
				new StressStrainTable(new double[] { 0, 0 }, new double[] { 0, 0.001 }));
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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 50 * 1000000, -50 * 1000000, CoordinateSystem.Global),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension();
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension();

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

			Console.WriteLine($"Tensions associated with force {result[0].Force.N}, {result[0].Force.M1}, {result[0].Force.M2} ");

			for (int i = 0; i < rebarTensions.Length; i++)
				Console.WriteLine($"Rebar {i}: {rebarTensions[i].rebar.Position.X}, {rebarTensions[i].rebar.Position.Y}. " +
					$"Tension = {Math.Round(rebarTensions[i].tension, 2)}");

			for (int i = 0; i < concreteTensions.Length; i++)
				Console.WriteLine($"Vertices {i}: {concreteTensions[i].point}. Tension = {Math.Round(concreteTensions[i].tension, 2)}");

			for (int i = 0; i < section.Shape.Fill.Count; i++)
				if (concreteTensions[i].tension != 0)
					Assert.IsTrue(Math.Abs((concreteTensions[i].tension - expConcreteTensions[i].tension) / concreteTensions[i].tension) < tolerance);

			for (int i = 0; i < rebarTensions.Length; i++)
				if (rebarTensions[i].tension != 0)
					Assert.IsTrue(Math.Abs((rebarTensions[i].tension - expRebarTensions[i].tension) / rebarTensions[i].tension) < tolerance);
		}

		[TestMethod]
		public void VCA_4()
		{
			double tolerance = 0.06;
			double elasticModulusFactor = 0.85 / 1.5;
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

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 0.0,
				new StressStrainTable(new double[] { 0, - elasticModulus / elasticModulusFactor, - 2.0 * elasticModulus / elasticModulusFactor }, 
				new double[] { 0, -0.001, -0.002 }),
				new StressStrainTable(new double[] { 0, 0 }, new double[] { 0, 0.001 }));
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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);			
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 300 * 1000000, -30 * 1000000, CoordinateSystem.Global),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension();
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension();

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

			Console.WriteLine($"Tensions associated with force {result[0].Force.N}, {result[0].Force.M1}, {result[0].Force.M2} ");

			for (int i = 0; i < rebarTensions.Length; i++)
				Console.WriteLine($"Rebar {i}: {rebarTensions[i].rebar.Position.X}, {rebarTensions[i].rebar.Position.Y}. " +
					$"Tension = {Math.Round(rebarTensions[i].tension, 2)}");

			for (int i = 0; i < concreteTensions.Length; i++)
				Console.WriteLine($"Vertices {i}: {concreteTensions[i].point}. Tension = {Math.Round(concreteTensions[i].tension, 2)}");

			for (int i = 0; i < section.Shape.Fill.Count; i++)
				if (concreteTensions[i].tension != 0)
					Assert.IsTrue(Math.Abs((concreteTensions[i].tension - expConcreteTensions[i].tension) / concreteTensions[i].tension) < tolerance);

			for (int i = 0; i < rebarTensions.Length; i++)
				if (rebarTensions[i].tension != 0)
					Assert.IsTrue(Math.Abs((rebarTensions[i].tension - expRebarTensions[i].tension) / rebarTensions[i].tension) < tolerance);
		}

	}
}
