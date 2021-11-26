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
	public class ValidationTestNMethodStressAnalysis : ConcreteTest
	{
		[TestMethod]
		public void VCA_N_1()
		{
			double tolerance = 0.05;
			double n = 15;

			double rebarDiameter = 18;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C25_30;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 200000, 450, 450));

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
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, -30 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(n);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(n);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(n);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], -2.843),
				(section.Shape.Fill[2], -11.04),
				(section.Shape.Fill[3], -2.364),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 53.53),
				(rebars[1], -33.25),
				(rebars[2], -131.6),
				(rebars[3], -44.86),
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
		public void VCA_N_2()
		{
			double tolerance = 0.05;
			double n = 15;

			double rebarDiameter = 18;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C25_30;
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))
			};

			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 50 * 1000000, -10 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(n);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(n);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(n);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], -8.665),
				(section.Shape.Fill[3], -2.804),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 185.6),
				(rebars[1], 127),
				(rebars[2], -88.4),
				(rebars[3], -29.79),
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
		public void VCA_N_3()
		{
			double tolerance = 0.06;
			double rebarDiameter = 26;
			double n = 15;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C35_45;
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
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 50 * 1000000, -50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(n);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(n);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(n);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], -3.642),
				(section.Shape.Fill[2], -10.81),
				(section.Shape.Fill[3], 0.0),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 113.6),
				(rebars[1], 42.02),
				(rebars[2], -29.58),
				(rebars[3], -115.6),
				(rebars[4], -43.98),
				(rebars[5], 27.62),
				(rebars[6], 70.62),
				(rebars[7], -72.58),
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
		public void VCA_N_4()
		{
			double tolerance = 0.05;
			double rebarDiameter = 26;
			double h = 500;
			double n = 15;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(h, 0),
				new Point2d(h, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C45_55;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 200000, 450, 450));

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
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 300 * 1000000, -30 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(n);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(n);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(n);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], -13.65),
				(section.Shape.Fill[3], -9.365),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 276.8),
				(rebars[1], 263.9),
				(rebars[2], 251.1),
				(rebars[3], 238.3),
				(rebars[4], 225.4),
				(rebars[5], -99.82),
				(rebars[6], -112.7),
				(rebars[7], -125.5),
				(rebars[8], -138.4),
				(rebars[9], -151.2),
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
		public void VCA_N_5()
		{
			double tolerance = 0.05;
			double h = 500;
			double n = 15;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(h, 0),
				new Point2d(h, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C45_55;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, new ReinforcedConcreteRebar[] {});
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 20 * 1000000, -20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(n);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(n);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(n);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], -0.5759),
				(section.Shape.Fill[2], -3.31),
				(section.Shape.Fill[3], -0.5759),
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

		}
	}
}
