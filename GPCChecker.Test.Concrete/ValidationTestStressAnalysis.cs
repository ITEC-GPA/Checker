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

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 0.0),
				(section.ConcreteShape.Fill[1], -2.703),
				(section.ConcreteShape.Fill[2], -12.34),
				(section.ConcreteShape.Fill[3], -2.343),
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
			ReinforcedConcreteRebar[] rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 50 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 0.0),
				(section.ConcreteShape.Fill[1], 0.0),
				(section.ConcreteShape.Fill[2], -12.54),
				(section.ConcreteShape.Fill[3], -3.547),
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

			Shape2d shape = GetRectangularShape(300, 500);
			ConcreteMaterialEN1992 concreteMaterial = GetLinearConcreteMaterial(elasticModulus);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new SteelMaterial("", 200000, 450, 450, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar));

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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shape, concreteMaterial);
			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 50 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 0.0),
				(section.ConcreteShape.Fill[1], -3.926),
				(section.ConcreteShape.Fill[2], -16.01),
				(section.ConcreteShape.Fill[3], 0.0),
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

			Shape2d shape = GetRectangularShape(h, h);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

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

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shape, GetLinearConcreteMaterial(elasticModulus));
			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 300 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 0.0),
				(section.ConcreteShape.Fill[1], 0.0),
				(section.ConcreteShape.Fill[2], -22.97),
				(section.ConcreteShape.Fill[3], -15.51),
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
			ReinforcedConcreteRebar[] rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 40 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), true);
			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 1.88),
				(section.ConcreteShape.Fill[1], 1.88),
				(section.ConcreteShape.Fill[2], -1.88),
				(section.ConcreteShape.Fill[3], -1.88),
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

			Shape2d shape = GetRectangularShape(h, h);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

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

			var section = new ReinforcedConcreteSection(shape, GetLinearConcreteMaterial(elasticModulus));
			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 200 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), true);
			StressAnalysisResult[] result = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 0.0),
				(section.ConcreteShape.Fill[1], 0.0),
				(section.ConcreteShape.Fill[2], -14.21),
				(section.ConcreteShape.Fill[3], -14.21),
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

		[TestMethod]
		[TestCategory("Bridge with 4 Rebars")]
		public void VCA_7()
		{
			double rebarDiameter = 26;
			double n = 15;

			ReinforcedConcreteSection section = GetBridgeShapeWithHole(4600, 1800, 3000, 300, 300, 200, ConcreteMaterialEN1992Data.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(950, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(950, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3650, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3650, 1700, 0)),
			};
			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 2000 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			double psi = GetPsi(n, section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), true);
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], -0.6866),
				(section.ConcreteShape.Fill[1], -0.6866),
				(section.ConcreteShape.Fill[2], +1.794),
				(section.ConcreteShape.Fill[3], +1.794),
				(section.ConcreteShape.Fill[4], -0.6866),
				(section.ConcreteShape.Fill[5], -0.6866),
				(section.ConcreteShape.Fill[6], -1.183),
				(section.ConcreteShape.Fill[7], -1.183),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 24.43),
				(rebars[1], -15.26),
				(rebars[2], 24.43),
				(rebars[3], -15.26),
			};

			CommonAssertsVCA(psi, result[0], expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		[TestCategory("Bridge with 4 Rebars")]
		public void VCA_7_2()
		{
			double rebarDiameter = 26;
			double n = 15;

			ReinforcedConcreteSection section = GetBridgeShapeWithHole(4600, 1800, 3000, 300, 300, 200, ConcreteMaterialEN1992Data.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(950, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(950, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3650, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3650, 1700, 0)),
			};
			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 500 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			double psi = GetPsi(n, section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 0),
				(section.ConcreteShape.Fill[1], 0),
				(section.ConcreteShape.Fill[2], 0),
				(section.ConcreteShape.Fill[3], 0),
				(section.ConcreteShape.Fill[4], 0),
				(section.ConcreteShape.Fill[5], 0),
				(section.ConcreteShape.Fill[6], -1.24),
				(section.ConcreteShape.Fill[7], -1.24),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 282.4),
				(rebars[1], -0.87),
				(rebars[2], 282.4),
				(rebars[3], -0.87),
			};

			CommonAssertsVCA(psi, result[0], expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		[TestCategory("Bridge No Rebars")]
		public void VCA_8()
		{
			ReinforcedConcreteSection section = GetBridgeShapeWithHole(4600, 1800, 3000, 300, 300, 200, ConcreteMaterialEN1992Data.C25_30);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 2000 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete(), true);
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(1.36);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], -0.6954),
				(section.ConcreteShape.Fill[1], -0.6954),
				(section.ConcreteShape.Fill[2], +1.83),
				(section.ConcreteShape.Fill[3], +1.83),
				(section.ConcreteShape.Fill[4], -0.6954),
				(section.ConcreteShape.Fill[5], -0.6954),
				(section.ConcreteShape.Fill[6], -1.2),
				(section.ConcreteShape.Fill[7], -1.2),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
			};

			CommonAssertsVCA(1.36, result[0], expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		[TestCategory("Bridge")]
		public void VCA_9()
		{
			double rebarDiameter = 26;
			double n = 15;

			ReinforcedConcreteSection section = GetBridgeShapeWithHole(4600, 1800, 3000, 300, 300, 200, ConcreteMaterialEN1992Data.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(1000, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1200, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1400, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1600, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3000, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3200, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3400, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3600, 100, 0)),

				new ReinforcedConcreteRebar(rebar, new Point3d(1000, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1200, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1400, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1600, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3000, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3200, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3400, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3600, 1700, 0)),
			};
			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 2000 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			double psi = GetPsi(n, section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 0),
				(section.ConcreteShape.Fill[1], 0),
				(section.ConcreteShape.Fill[2], 0),
				(section.ConcreteShape.Fill[3], 0),
				(section.ConcreteShape.Fill[4], 0),
				(section.ConcreteShape.Fill[5], 0),
				(section.ConcreteShape.Fill[6], -2.525),
				(section.ConcreteShape.Fill[7], -2.525),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 288.5),
				(rebars[1], 288.5),
				(rebars[2], 288.5),
				(rebars[3], 288.5),
				(rebars[4], 288.5),
				(rebars[5], 288.5),
				(rebars[6], 288.5),
				(rebars[7], 288.5),

				(rebars[8], -18.86),
				(rebars[9], -18.86),
				(rebars[10], -18.86),
				(rebars[11], -18.86),
				(rebars[12], -18.86),
				(rebars[13], -18.86),
				(rebars[14], -18.86),
				(rebars[15], -18.86),
			};

			CommonAssertsVCA(psi, result[0], expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		[TestCategory("Bridge")]
		public void VCA_9_2()
		{
			double rebarDiameter = 26;
			double n = 15;

			ReinforcedConcreteSection section = GetBridgeShapeWithHole(4600, 1800, 3000, 300, 300, 200, ConcreteMaterialEN1992Data.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(1000, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1200, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1400, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1600, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3000, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3200, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3400, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3600, 100, 0)),

				new ReinforcedConcreteRebar(rebar, new Point3d(1000, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1200, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1400, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1600, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3000, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3200, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3400, 1700, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3600, 1700, 0)),

				new ReinforcedConcreteRebar(rebar, new Point3d(1000, 400, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1000, 800, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1000, 1200, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(1000, 1600, 0)),

				new ReinforcedConcreteRebar(rebar, new Point3d(3600, 400, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3600, 800, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3600, 1200, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(3600, 1600, 0)),
			};

			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 2000 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			double psi = GetPsi(n, section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 0),
				(section.ConcreteShape.Fill[1], 0),
				(section.ConcreteShape.Fill[2], 0),
				(section.ConcreteShape.Fill[3], 0),
				(section.ConcreteShape.Fill[4], 0),
				(section.ConcreteShape.Fill[5], 0),
				(section.ConcreteShape.Fill[6], -2.41),
				(section.ConcreteShape.Fill[7], -2.41),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 231),
				(rebars[1], 231),
				(rebars[2], 231),
				(rebars[3], 231),
				(rebars[4], 231),
				(rebars[5], 231),
				(rebars[6], 231),
				(rebars[7], 231),

				(rebars[8], -20.44),
				(rebars[9], -20.44),
				(rebars[10], -20.44),
				(rebars[11], -20.44),
				(rebars[12], -20.44),
				(rebars[13], -20.44),
				(rebars[14], -20.44),
				(rebars[15], -20.44),

				(rebars[16], 183.9),
				(rebars[17], 121),
				(rebars[18], 58.15),
				(rebars[19], -4.72),

				(rebars[20], 183.9),
				(rebars[21], 121),
				(rebars[22], 58.15),
				(rebars[23], -4.72),
			};

			CommonAssertsVCA(psi, result[0], expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		[TestCategory("Bridge")]
		public void VCA_10()
		{
			double rebarDiameter26 = 26;
			double rebarDiameter20 = 20;
			double rebarDiameter12 = 12;
			double n = 15;

			ReinforcedConcreteSection section = GetBridgeShapeWithoutHole(4600, 1800, 3000, 300, ConcreteMaterialEN1992Data.C25_30);
			RebarSectionCircular rebar26 = new RebarSectionCircular(rebarDiameter26, SteelMaterialEN1992Data.B450C);
			RebarSectionCircular rebar20 = new RebarSectionCircular(rebarDiameter20, SteelMaterialEN1992Data.B450C);
			RebarSectionCircular rebar12 = new RebarSectionCircular(rebarDiameter12, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar26, new Point3d(1000, 100, 0)),
				new ReinforcedConcreteRebar(rebar26, new Point3d(1200, 100, 0)),
				new ReinforcedConcreteRebar(rebar26, new Point3d(1400, 100, 0)),
				new ReinforcedConcreteRebar(rebar26, new Point3d(1600, 100, 0)),
				new ReinforcedConcreteRebar(rebar26, new Point3d(3000, 100, 0)),
				new ReinforcedConcreteRebar(rebar26, new Point3d(3200, 100, 0)),
				new ReinforcedConcreteRebar(rebar26, new Point3d(3400, 100, 0)),
				new ReinforcedConcreteRebar(rebar26, new Point3d(3600, 100, 0)),

				new ReinforcedConcreteRebar(rebar20, new Point3d(1000, 1700, 0)),
				new ReinforcedConcreteRebar(rebar20, new Point3d(1200, 1700, 0)),
				new ReinforcedConcreteRebar(rebar20, new Point3d(1400, 1700, 0)),
				new ReinforcedConcreteRebar(rebar20, new Point3d(1600, 1700, 0)),
				new ReinforcedConcreteRebar(rebar20, new Point3d(3000, 1700, 0)),
				new ReinforcedConcreteRebar(rebar20, new Point3d(3200, 1700, 0)),
				new ReinforcedConcreteRebar(rebar20, new Point3d(3400, 1700, 0)),
				new ReinforcedConcreteRebar(rebar20, new Point3d(3600, 1700, 0)),

				new ReinforcedConcreteRebar(rebar12, new Point3d(1000, 400, 0)),
				new ReinforcedConcreteRebar(rebar12, new Point3d(1000, 800, 0)),
				new ReinforcedConcreteRebar(rebar12, new Point3d(1000, 1200, 0)),
				new ReinforcedConcreteRebar(rebar12, new Point3d(1000, 1600, 0)),

				new ReinforcedConcreteRebar(rebar12, new Point3d(3600, 400, 0)),
				new ReinforcedConcreteRebar(rebar12, new Point3d(3600, 800, 0)),
				new ReinforcedConcreteRebar(rebar12, new Point3d(3600, 1200, 0)),
				new ReinforcedConcreteRebar(rebar12, new Point3d(3600, 1600, 0)),
			};

			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 2000 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			double psi = GetPsi(n, section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 0),
				(section.ConcreteShape.Fill[1], 0),
				(section.ConcreteShape.Fill[2], 0),
				(section.ConcreteShape.Fill[3], 0),
				(section.ConcreteShape.Fill[4], 0),
				(section.ConcreteShape.Fill[5], 0),
				(section.ConcreteShape.Fill[6], -2.538),
				(section.ConcreteShape.Fill[7], -2.538),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 273.8),
				(rebars[1], 273.8),
				(rebars[2], 273.8),
				(rebars[3], 273.8),
				(rebars[4], 273.8),
				(rebars[5], 273.8),
				(rebars[6], 273.8),
				(rebars[7], 273.8),

				(rebars[8], -19.73),
				(rebars[9], -19.73),
				(rebars[10], -19.73),
				(rebars[11], -19.73),
				(rebars[12], -19.73),
				(rebars[13], -19.73),
				(rebars[14], -19.73),
				(rebars[15], -19.73),

				(rebars[16], 218.7),
				(rebars[17], 145.4),
				(rebars[18], 71.99),
				(rebars[19], -1.385),

				(rebars[20], 218.7),
				(rebars[21], 145.4),
				(rebars[22], 71.99),
				(rebars[23], -1.385),
			};

			CommonAssertsVCA(psi, result[0], expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		[TestCategory("Bridge")]
		public void VCA_11()
		{
			double n = 15;

			ReinforcedConcreteSection section = GetBridgeSection(4600, 1800, 3000, 300, 300, 200, 60,
				10, 14, 13, 14, 10, 14,
				7, 14,
				4, 14, 13, 14, 4, 14,
				ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 2000 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			double psi = GetPsi(n, section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], 0),
				(section.ConcreteShape.Fill[1], 0),
				(section.ConcreteShape.Fill[2], 0),
				(section.ConcreteShape.Fill[3], 0),
				(section.ConcreteShape.Fill[4], 0),
				(section.ConcreteShape.Fill[5], 0),
				(section.ConcreteShape.Fill[6], -1.954),
				(section.ConcreteShape.Fill[7], -1.954),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				//(rebars[0], 231),
				//(rebars[1], 231),
				//(rebars[2], 231),
				//(rebars[3], 231),
				//(rebars[4], 231),
				//(rebars[5], 231),
				//(rebars[6], 231),
				//(rebars[7], 231),

				//(rebars[8], -20.44),
				//(rebars[9], -20.44),
				//(rebars[10], -20.44),
				//(rebars[11], -20.44),
				//(rebars[12], -20.44),
				//(rebars[13], -20.44),
				//(rebars[14], -20.44),
				//(rebars[15], -20.44),

				//(rebars[16], 183.9),
				//(rebars[17], 121),
				//(rebars[18], 58.15),
				//(rebars[19], -4.72),

				//(rebars[20], 183.9),
				//(rebars[21], 121),
				//(rebars[22], 58.15),
				//(rebars[23], -4.72),
			};

			CommonAssertsVCA(psi, result[0], expConcreteTensions, expRebarTensions);
		}

		[TestMethod]
		[TestCategory("Bridge")]
		public void VCA_12()
		{
			double n = 15;
			double d = 20;

			ReinforcedConcreteSection section = GetBridgeSection(4600, 1800, 3000, 300, 300, 200, 60,
				10, d, 13, d, 10, d,
				7, d,
				4, d, 13, d, 4, d,
				ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 2000 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			double psi = GetPsi(n, section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.ConcreteShape.Fill[0], -0.2128),
				(section.ConcreteShape.Fill[1], -0.2128),
				(section.ConcreteShape.Fill[2], 0),
				(section.ConcreteShape.Fill[3], 0),
				(section.ConcreteShape.Fill[4], -0.2128),
				(section.ConcreteShape.Fill[5], -0.2128),
				(section.ConcreteShape.Fill[6], -1.384),
				(section.ConcreteShape.Fill[7], -1.384),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				//(rebars[0], 231),
				//(rebars[1], 231),
				//(rebars[2], 231),
				//(rebars[3], 231),
				//(rebars[4], 231),
				//(rebars[5], 231),
				//(rebars[6], 231),
				//(rebars[7], 231),

				//(rebars[8], -20.44),
				//(rebars[9], -20.44),
				//(rebars[10], -20.44),
				//(rebars[11], -20.44),
				//(rebars[12], -20.44),
				//(rebars[13], -20.44),
				//(rebars[14], -20.44),
				//(rebars[15], -20.44),

				//(rebars[16], 183.9),
				//(rebars[17], 121),
				//(rebars[18], 58.15),
				//(rebars[19], -4.72),

				//(rebars[20], 183.9),
				//(rebars[21], 121),
				//(rebars[22], 58.15),
				//(rebars[23], -4.72),
			};

			CommonAssertsVCA(psi, result[0], expConcreteTensions, expRebarTensions);
		}
	}
}
