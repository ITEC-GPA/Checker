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
	public class ValidationTestNMethodStressAnalysis : ConcreteTestBase
	{
		[TestMethod]
		public void VCA_N_1()
		{
			double tolerance = 0.05;
			double rebarDiameter = 18;
			double n = 15;

			var section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, ConcreteMaterialEN1992.C25_30, new RebarMaterial("", 200000, 450, 450));
			double psi = n * ConcreteMaterialEN1992.C25_30.E / section.GetRebars()[0].RebarMaterial.E - 1.0;

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, -30 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], -2.843),
				(section.Shape.Fill[2], -11.04),
				(section.Shape.Fill[3], -2.364),
			};

			var rebars = section.GetRebars();
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

			var section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992.C25_30, new RebarMaterial("", 450));
			double psi = n * ConcreteMaterialEN1992.C25_30.E / section.GetRebars()[0].RebarMaterial.E - 1.0;

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 50 * 1000000, -10 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], -8.665),
				(section.Shape.Fill[3], -2.804),
			};

			var rebars = section.GetRebars();
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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			double psi = n * ConcreteMaterialEN1992.C35_45.E / section.GetRebars()[0].RebarMaterial.E - 1.0;

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 50 * 1000000, -50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			double psi = n * ConcreteMaterialEN1992.C45_55.E / section.GetRebars()[0].RebarMaterial.E - 1.0;

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 300 * 1000000, -30 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
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

		[TestMethod]
		public void VCA_N_T_1()
		{
			double tolerance = 0.05;
			double n = 15;

			var section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992.C25_30, new RebarMaterial("", 200000, 450, 450));
			double psi = n * ConcreteMaterialEN1992.C25_30.E / section.GetRebars()[0].RebarMaterial.E - 1.0;
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, -30 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 3.844),
				(section.Shape.Fill[1], -3.202),
				(section.Shape.Fill[2], -9.896),
				(section.Shape.Fill[3], -2.85),
			};

			var rebars = section.GetRebars();
			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 30),
				(rebars[1], -40.45),
				(rebars[2], -120.8),
				(rebars[3], -50.33),
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
		public void VCA_N_T_2()
		{
			double tolerance = 0.05;
			double n = 15;

			var section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992.C25_30, new RebarMaterial("", 450));
			double psi = n * ConcreteMaterialEN1992.C25_30.E / section.GetRebars()[0].RebarMaterial.E - 1.0;
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 50 * 1000000, -10 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 3.916),
				(section.Shape.Fill[1], 1.568),
				(section.Shape.Fill[2], -5.127),
				(section.Shape.Fill[3], -2.778),
			};

			var rebars = section.GetRebars();
			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 42.83),
				(rebars[1], 19.34),
				(rebars[2], -60.99),
				(rebars[3], -37.5),
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
		public void VCA_N_T_3()
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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			double psi = n * ConcreteMaterialEN1992.C25_30.E / section.GetRebars()[0].RebarMaterial.E - 1.0;

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 50 * 1000000, -50 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 5.757),
				(section.Shape.Fill[1], -3.601),
				(section.Shape.Fill[2], -8.565),
				(section.Shape.Fill[3], 0.7935),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 55.52),
				(rebars[1], 8.726),
				(rebars[2], -38.06),
				(rebars[3], -97.63),
				(rebars[4], -50.84),
				(rebars[5], -4.047),
				(rebars[6], 25.73),
				(rebars[7], -67.85),
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
		public void VCA_N_T_4()
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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			double psi = n * ConcreteMaterialEN1992.C45_55.E / section.GetRebars()[0].RebarMaterial.E - 1.0;

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 300 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 8.328),
				(section.Shape.Fill[1], 8.328),
				(section.Shape.Fill[2], -9.541),
				(section.Shape.Fill[3], -9.541),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 98.11),
				(rebars[1], 98.11),
				(rebars[2], 98.11),
				(rebars[3], 98.11),
				(rebars[4], 98.11),
				(rebars[5], -116.3),
				(rebars[6], -116.3),
				(rebars[7], -116.3),
				(rebars[8], -116.3),
				(rebars[9], -116.3),
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
		public void VCA_N_T_5()
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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 20 * 1000000, -20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(n);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(n);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(n);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 1.12),
				(section.Shape.Fill[1], -0.8),
				(section.Shape.Fill[2], -2.72),
				(section.Shape.Fill[3], -0.8),
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

		[TestMethod]
		public void VCA_N_T_6()
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

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C25_30;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult(n);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(n);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 2.4),
				(section.Shape.Fill[1], 2.4),
				(section.Shape.Fill[2], -2.4),
				(section.Shape.Fill[3], -2.4),
			};

			Console.WriteLine($"Tensions associated with force {result[0].Force.N}, {result[0].Force.M1}, {result[0].Force.M2} ");

			for (int i = 0; i < concreteTensions.Length; i++)
				Console.WriteLine($"Vertices {i}: {concreteTensions[i].point}. Tension = {Math.Round(concreteTensions[i].tension, 2)}");

			for (int i = 0; i < section.Shape.Fill.Count; i++)
				if (concreteTensions[i].tension != 0)
					Assert.IsTrue(Math.Abs((concreteTensions[i].tension - expConcreteTensions[i].tension) / concreteTensions[i].tension) < tolerance);

		}
	}
}
