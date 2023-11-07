using System;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
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
	public class StressAnalysisRCTest : ConcreteTestBase
	{
		[TestMethod]
		public void RectangularSectionTest1()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			var section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest2()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			var section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 3, ConcreteMaterialEN1992Data.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest3()
		{
			double rebarDiameter = 18;
			double height = 800;
			double width = 400;

			Shape2d shape = GetRectangularShape(width, height);

			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,750,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200,750,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,750,0))
			};

			var section = new ReinforcedConcreteSection(shape, ConcreteMaterialEN1992Data.C45_55);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000,  GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -0 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000,  GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000,  GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest4()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			var section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 3, ConcreteMaterialEN1992Data.C25_30);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest5()
		{
			double rebarDiameter = 26;
			double height = 1000;
			double width = 500;
			double concreteCover = 50;

			var section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 5, ConcreteMaterialEN1992Data.C45_55, SteelMaterialEN1992Data.B450C);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-4000 * 1000, 0, 0, 0, 500 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-3000 * 1000, 0, 0, 0, 300 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 220 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest6()
		{
			double rebarDiameter = 26;
			double elasticModulus = 34.077;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			var section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 3, GetLinearConcreteMaterial(elasticModulus));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 150 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-885 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -200 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -80 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest7()
		{
			double rebarDiameter = 24;
			double height = 800;
			double width = 400;

			Shape2d shape = GetRectangularShape(width, height);

			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,750,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200,750,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,750,0))
			};

			var section = new ReinforcedConcreteSection(shape, ConcreteMaterialEN1992Data.C45_55);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1500 * 1000, 0, 0, 0, 500 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 300 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1200 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 220 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest8()
		{
			double rebarDiameter = 26;
			double height = 700;
			double width = 300;
			double concreteCover = 50;

			var section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 5, ConcreteMaterialEN1992Data.C45_55);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-4000 * 1000, 0, 0, 0, 500 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-3500 * 1000, 0, 0, 0, 50 * 1000000, 200 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-3000 * 1000, 0, 0, 0, 300 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1100 * 1000, 0, 0, 0, 100 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-2500 * 1000, 0, 0, 0, 250 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, 150 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 250 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 400 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 150 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1870 * 1000, 0, 0, 0, 250 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest9()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = GetRectangularShape(300, 600);

			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 60, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 60, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 60, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 60, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 60, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 540,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 540,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 540,0)),
			};

			var section = new ReinforcedConcreteSection(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear));
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 200 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void RectangularSectionTest10()
		{
			double rebarDiameter = 16;

			// sezione rettangolare 300x500
			Shape2d shape = GetRectangularShape(300, 600);

			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 100, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 150, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 200, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 300, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 350, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 400, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 450, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 500, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 550, 0)),
			};

			var section = new ReinforcedConcreteSection(shape,
				new ConcreteMaterialEN1992("", 25, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle));
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 20 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 40 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 60 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 80 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		[TestCategory("Force reference point not in centroid")]
		public void RectangularSectionTest11()
		{
			double rebarDiameter = 16;

			var section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, GetLinearConcreteMaterial(31.476));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			var rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(new Point2d(-150, 0) + section.Centroid,
				Vector2d.XAxis, Vector2d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
{
				(section.Shape.Fill[0], -4.341),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], 0.0),
				(section.Shape.Fill[3], -4.341),
};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], -14.03),
				(rebars[1], 40.12),
				(rebars[2], 40.12),
				(rebars[3], -14.03),
			};

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(CommonAssertsVCA(slsResult[i], section, expConcreteTensions, expRebarTensions, 0.07));
		}

		[TestMethod]
		[TestCategory("Force reference point not in centroid")]
		public void RectangularSectionTest12()
		{
			double rebarDiameter = 16;

			var section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, GetLinearConcreteMaterial(31.476));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			var rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(new Point2d(150, 0), Vector2d.XAxis, Vector2d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
{
				(section.Shape.Fill[0], -3.988),
				(section.Shape.Fill[1], -3.988),
				(section.Shape.Fill[2], 0.0),
				(section.Shape.Fill[3], 0.0),
};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], -18.3),
				(rebars[1], -18.3),
				(rebars[2], 37.87),
				(rebars[3], 37.87),
			};


			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(CommonAssertsVCA(slsResult[i], section, expConcreteTensions, expRebarTensions, 0.07));


		}

		[TestMethod]
		[TestCategory("Force reference point not in centroid")]
		public void RectangularSectionTest13()
		{
			double rebarDiameter = 16;

			var section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, GetLinearConcreteMaterial(31.476));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ReinforcedConcreteRebar[] rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(new Point2d(-150, -250) + section.Centroid,
				Vector2d.XAxis, Vector2d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
{
				(section.Shape.Fill[0], -10.15),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], 0.0),
				(section.Shape.Fill[3], 0.0),
};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], -39.33),
				(rebars[1], 26.08),
				(rebars[2], 95.99),
				(rebars[3], 30.58),
			};

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(CommonAssertsVCA(slsResult[i], section, expConcreteTensions, expRebarTensions, 0.07));
		}

		[TestMethod]
		[TestCategory("Force reference point not in centroid")]
		public void RectangularSectionTest14()
		{
			double rebarDiameter = 16;

			var section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, GetLinearConcreteMaterial(31.476));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			var rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, new CoordinateSystem(new Point2d(50, 50),
				Vector2d.XAxis, Vector2d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
{
				(section.Shape.Fill[0], -6.274),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], 0.0),
				(section.Shape.Fill[3], 0.0),
};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], -27.2),
				(rebars[1], 2.284),
				(rebars[2], 44.4),
				(rebars[3], 14.91),
			};

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(CommonAssertsVCA(slsResult[i], section, expConcreteTensions, expRebarTensions, 0.07));
		}

		[TestMethod]
		public void RectangularSectionTest15()
		{
			double rebarDiameter = 16;

			var section = GetRectangularSection4Rebars(300, 500, rebarDiameter, 50, GetLinearConcreteMaterial(31.476));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			var rebars = section.GetRebars();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], -9.534),
				(section.Shape.Fill[3], -9.534),
};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 294.3),
				(rebars[1], 294.3),
				(rebars[2], -21.12),
				(rebars[3], -21.12),
			};

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(CommonAssertsVCA(slsResult[i], section, expConcreteTensions, expRebarTensions, 0.07));
		}

		[TestMethod]
		public void RectangularSectionTest16()
		{
			double rebarDiameter = 6;
			double height = 80;
			double width = 1000;
			double copriferro = 25;

			Shape2d shape = GetRectangularShape(width, height);

			RebarSectionCircular rebarSection = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B500C);
			var section = new ReinforcedConcreteSection(shape, new ConcreteMaterialModelCode2010($"FCM {45}-3.5", 45,
                ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle,
                0.45 * 0.92, 0.3 * 0.76, 0.00003, 0.02, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebarSection, new Point3d(100, copriferro, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(300, copriferro, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(500, copriferro, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(700, copriferro, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(900, copriferro, 0)),
			};

			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 2.5 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			//StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			//for (int i = 0; i < slsResult.Length; i++)
			//	Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest1()
		{
			double rebarDiameter = 20;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			var section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C45_55);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -50 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest2()
		{
			double rebarDiameter = 20;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			var section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 3, ConcreteMaterialEN1992Data.C25_30);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[] { new ResultBeamForces(-100 * 1000, 0, 0, 0, 20 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)) };

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest3()
		{
			double rebarDiameter = 20;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			var section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 5, ConcreteMaterialEN1992Data.C25_30);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest4()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;

			var section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, 5, ConcreteMaterialEN1992Data.C25_30);

			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(+200 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(+100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-900 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1200 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1500 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section))
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest5()
		{
			double rebarDiameter = 26;
			double height = 300;
			double concreteCover = 50;

			var section = GetRectangularSection4Rebars(height, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(+250 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest6()
		{
			double rebarDiameter = 14;
			double height = 400;
			double concreteCover = 50;

			var section = GetRectangularSectionBottomSideRebars(height, height, rebarDiameter, concreteCover, 4, ConcreteMaterialEN1992Data.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 10 * 1000000, 30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void SquareSectionTest7()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;

			Shape2d shape = GetRectangularShape(width, height);

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C35_45;
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,350,0)),
			};

			var section = new ReinforcedConcreteSection(shape, concreteMaterial);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -50 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -50 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 20 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -50 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -20 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -50 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic);
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);
			//ExportToGmsh(section);
			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

			for (int i = 0; i < slsResult.Length; i++)
				Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
		}

		[TestMethod]
		public void NeutralAxisTest1()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;
			double concreteCover = 40;
			double phi = 1.36;

			var section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 10 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 0 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 10 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -10 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, standard, true);

			CommonAssertNeutralAxis(sectionChecker, section, phi);
		}

		[TestMethod]
		public void NeutralAxisTest2()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;
			double concreteCover = 40;
			double phi = 1.36;

			var section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -10 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, standard, true);

			CommonAssertNeutralAxis(sectionChecker, section, phi);
		}

		[TestMethod]
		public void NeutralAxisTest3()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;
			double concreteCover = 40;
			double phi = 1.36;

			var section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 10 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 0 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 10 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, -10 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, standard, true);

			CommonAssertNeutralAxis(sectionChecker, section, phi);
		}

		[TestMethod]
		public void NeutralAxisTest4()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;
			double concreteCover = 40;
			double phi = 1.36;

			var section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, -10 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, standard, true);

			CommonAssertNeutralAxis(sectionChecker, section, phi);
		}

		[TestMethod]
		public void NeutralAxisTest5()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;
			double concreteCover = 40;
			double phi = 1.36;

			var section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 10 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 10 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, -10 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, standard, true);

			var slsResult = sectionChecker.GetLinearStressAnalysisResult(phi);

			for (int i = 0; i < slsResult.Length; i++)
			{
				var line1 = slsResult[i].StrainPlane.GetNeutralAxisRespectReferencePoint();
				var line2 = slsResult[i].StrainPlane.GetConstantStrainAxis(-0.001);
				var line3 = slsResult[i].StrainPlane.GetConstantStrainAxis(-0.002);
				var line4 = slsResult[i].StrainPlane.GetConstantStrainAxis(-0.003);
				var line5 = slsResult[i].StrainPlane.GetConstantStrainAxis(-0.02);

				double slope1 = line1.GetSlope();
				double slope2 = line2.GetSlope();
				double slope3 = line3.GetSlope();
				double slope4 = line4.GetSlope();
				double slope5 = line5.GetSlope();

				Console.WriteLine($"{ slope1 }");
				Console.WriteLine($"{ slope2 }");
				Console.WriteLine($"{ slope3 }");
				Console.WriteLine($"{ slope4 }");
				Console.WriteLine($"{ slope5 }");

				Assert.IsTrue(Math.Abs(slope1 - slope2) < 0.01);
				Assert.IsTrue(Math.Abs(slope1 - slope3) < 0.01);
				Assert.IsTrue(Math.Abs(slope1 - slope4) < 0.01);
				Assert.IsTrue(Math.Abs(slope1 - slope5) < 0.01);
			}
		}

		[TestMethod]
		public void SectionLTest1()
		{
			double rebarDiameter = 16;
			double tolerance = 0.06;
			double n = 15;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(500, 0),
				new Point2d(500, 100),
				new Point2d(100, 100),
				new Point2d(100, 500),
				new Point2d(0, 500),
			}));

			RebarSectionCircular rebarSection = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);
			var section = new ReinforcedConcreteSection(shape, ConcreteMaterialEN1992Data.C25_30);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebarSection, new Point3d(50, 450, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(50, 50, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(450, 50, 0)),
			};

			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 20 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			double psi = GetPsi(n, section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0),
				(section.Shape.Fill[2], -3.036),
				(section.Shape.Fill[3], 0),
				(section.Shape.Fill[4], -7.861),
				(section.Shape.Fill[5], -4.189),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], -53.79),
				(rebars[1], 239),
				(rebars[2], 18.6),
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
		public void SectionTTest1()
		{
			double rebarDiameter = 26;
			double tolerance = 0.06;
			double n = 15;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(-800, 0),
				new Point2d(800, 0),
				new Point2d(800, 550),
				new Point2d(200, 550),
				new Point2d(200, 4500),
				new Point2d(-200, 4500),
				new Point2d(-200, 550),
				new Point2d(-800, 550),
			}));

			RebarSectionCircular rebarSection = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);
			var section = new ReinforcedConcreteSection(shape, ConcreteMaterialEN1992Data.C25_30);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebarSection, new Point3d(0, 50, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(0, 4450, 0)),				
			};

			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 500.0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			double psi = GetPsi(n, section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], 0.0),
				(section.Shape.Fill[3], 0.0),
				(section.Shape.Fill[4], -1.375),
				(section.Shape.Fill[5], -1.375),
				(section.Shape.Fill[6], 0.0),
				(section.Shape.Fill[7], 0.0),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 217.6),
				(rebars[1], -17.9),
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
		public void SectionTTest2()
		{
			double rebarDiameter = 100;
			double tolerance = 0.06;
			double n = 15;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(-800, 0),
				new Point2d(800, 0),
				new Point2d(800, 550),
				new Point2d(200, 550),
				new Point2d(200, 4500),
				new Point2d(-200, 4500),
				new Point2d(-200, 550),
				new Point2d(-800, 550),
			}));

			RebarSectionCircular rebarSection = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);
			var section = new ReinforcedConcreteSection(shape, ConcreteMaterialEN1992Data.C25_30);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebarSection, new Point3d(0, 50, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(0, 4450, 0)),
			};

			section.AddRebars(rebars);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-5000 * 1000, 0, 0, 0, 10000 * 1000000.0, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			double psi = GetPsi(n, section);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, forces, null, new StandardNTC2018Concrete());
			StressAnalysisResult[] result = sectionChecker.GetLinearStressAnalysisResult(psi);

			(Point2d point, double tension)[] concreteTensions = result[0].GetConcreteVerticesTension(psi);
			(ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result[0].GetRebarsTension(psi);

			(Point2d point, double tension)[] expConcreteTensions = new (Point2d point, double tension)[]
			{
				(section.Shape.Fill[0], 0.0),
				(section.Shape.Fill[1], 0.0),
				(section.Shape.Fill[2], 0.0),
				(section.Shape.Fill[3], 0.0),
				(section.Shape.Fill[4], -6.686),
				(section.Shape.Fill[5], -6.686),
				(section.Shape.Fill[6], 0.0),
				(section.Shape.Fill[7], 0.0),
			};

			(ReinforcedConcreteRebar rebar, double tension)[] expRebarTensions = new (ReinforcedConcreteRebar rebar, double tension)[]
			{
				(rebars[0], 32.85),
				(rebars[1], -98.8),
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
