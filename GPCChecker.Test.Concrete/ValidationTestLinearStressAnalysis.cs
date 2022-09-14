using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ConcreteTests
{
	[TestClass]
	public class ValidationTestLinearStressAnalysis : ConcreteTestBase
	{
		[TestMethod]
		public void VCA_N_1()
		{
			double tolerance = 0.05;
			double rebarDiameter = 18;
			double n = 15;

			var section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, ConcreteMaterialEN1992Data.C25_30, new SteelMaterial("", 200000, 450, 450));
			double psi = GetPsi(n, section);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

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
				if (concreteTensions[i].tension != 0)
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

			var section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
			double psi = GetPsi(n, section);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 50 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

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

			Shape2d shape = GetRectangularShape(300, 500);
			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C35_45;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

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

			double psi = GetPsi(n, section);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 50 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

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

			Shape2d shape = GetRectangularShape(h, h);
			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C45_55;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new SteelMaterial("", 200000, 450, 450, 0.1, SteelMaterial.SteelTypes.Rebar));

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

			double psi = GetPsi(n, section);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 300 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

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

			Shape2d shape = GetRectangularShape(h, h);
			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C45_55;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());

			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(n);

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

			var section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992Data.C25_30, new SteelMaterial("", 200000, 450, 450));
			double psi = GetPsi(n, section);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), true);
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

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

			var section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
			double psi = GetPsi(n, section);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 50 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), true);
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

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

			Shape2d shape = GetRectangularShape(300, 500);
			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C35_45;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

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

			double psi = GetPsi(n, section);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 50 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), true);
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

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

			Shape2d shape = GetRectangularShape(h, h);
			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C45_55;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new SteelMaterial("", 200000, 450, 450));

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

			double psi = GetPsi(n, section);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 300 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), true);
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

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

			Shape2d shape = GetRectangularShape(h, h);
			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C45_55;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), true);
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(n);

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

			Shape2d shape = GetRectangularShape(h, h);
			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C25_30;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), true);
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(n);

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

		[TestMethod]
		[TestCategory("Asymmetric rebars")]
		public void VCA_7()
		{
			double rebarDiameter = 16;
			double n = 15;
			double h = 500;

			Shape2d shape = GetRectangularShape(h, h);
			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 150, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 350, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 10 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), false);
			double psi = GetPsi(n, section);

			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], -3.475),
				(section.Shape.Fill[3], 0.0),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 145.1),
				(rebars[1], 16.3),
				(rebars[2], 98.63),
				(rebars[3], -30.2),
				(rebars[4], 4.674),
				(rebars[5], -6.952),
				(rebars[6], -18.58),
			};

			CommonAssertsVCA(psi, result[0], expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		[TestCategory("Bridge section")]
		public void VCA_Bridge_1()
		{
			double n = 15;

			ReinforcedConcreteSection section = GetBridgeSection(4600, 1800, 3000, 300, 300, 200, 60,
				10, 14, 13, 14, 10, 14,
				7, 12,
				4, 22, 13, 20, 4, 22,
				ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);

			ResultBeamForces forces = new ResultBeamForces(0 * 1000, 0, 0, 0, 2000 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section));

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, null, null, new StandardNTC2018Concrete(), false);
			double psi = GetPsi(n, section);

			StressAnalysisResult result = sectionChecker.GetLinearStressAnalysisResult(forces, psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], -0.2282),
				(section.Shape.Fill[1], -0.2282),
				(section.Shape.Fill[2], 0),
				(section.Shape.Fill[3], 0),
				(section.Shape.Fill[4], -0.2282),
				(section.Shape.Fill[5], -0.2282),
				(section.Shape.Fill[6], -1.457),
				(section.Shape.Fill[7], -1.457),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
			};

			CommonAssertsVCA(psi, result, expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		[TestCategory("Only rebars")]
		public void VCA_10()
		{
			double n = 15;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, 18, 50,
				ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);

			ResultBeamForces forces = new ResultBeamForces(50 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section));

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, null, null, new StandardNTC2018Concrete(), false);
			double psi = GetPsi(n, section);

			StressAnalysisResult result = sectionChecker.GetLinearStressAnalysisResult(forces, psi);

			(Point2d point, double tension)[] concrete = result.GetConcreteVerticesTension(psi);
			var rebarTensions = result.GetRebarsTension(psi);

			for (int i = 0; i < rebarTensions.Length; i++)
			{
				Assert.IsTrue(Math.Abs(rebarTensions[i].tension - 49.12) < 0.1);
			}
			for (int i = 0; i < concrete.Length; i++)
			{
				Assert.IsTrue(Math.Abs(concrete[i].tension - 0) < 0.1);
			}
		}
	}
}
