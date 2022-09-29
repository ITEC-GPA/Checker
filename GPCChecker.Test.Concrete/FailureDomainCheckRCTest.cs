using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
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
using System;

namespace ConcreteTests
{
	[TestClass]
	public class FailureDomainCheckRCTest : ConcreteTestBase
	{
		[TestMethod]
		public void RectangularSectionTest1()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover,
				new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-150 * 1000, 0, 0, 0, -38 * 1000000, -8 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 20 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-400 * 1000, 0, 0, 0, 20 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -50 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -40 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, 120 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int j = 0; j < forces.Length; j++)
				CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest2()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover,
				new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 20 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-400 * 1000, 0, 0, 0, 20 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, -20 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -40 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, 120 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int j = 0; j < forces.Length; j++)
				CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest3()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover,
				new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 20 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-400 * 1000, 0, 0, 0, 20 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, -20 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -40 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, 120 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)),
					0.005, new double[] { 0.5, 1.0, 1.5 });
		}

		[TestMethod]
		public void RectangularSectionTest4()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover,
				new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));

			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(+200 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(+100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-900 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1200 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1500 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int j = 0; j < forces.Length; j++)
				CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest5()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;
			int numberOfSideRebar = 3;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebar,
				new ConcreteMaterialEN1992("", 45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 40 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-400 * 1000, 0, 0, 0, 40 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -50 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -200 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 150 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, 120 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest6()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;
			int numberOfSideRebar = 3;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebar,
				ConcreteMaterialEN1992Data.C40_50);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-75 * 1000, 0, 0, 0, -15 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -20 * 1000000, 40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-400 * 1000, 0, 0, 0, -20 * 1000000, -120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 20 * 1000000, 40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 40 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, -120 * 1000000, -120 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest7()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C40_50);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -20 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-400 * 1000, 0, 0, 0, -20 * 1000000, -120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, -20 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -40 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, -120 * 1000000, -120 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest8()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, ConcreteMaterialEN1992Data.C45_55);

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-2000 * 1000, 0, 0, 0, -300 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-3000 * 1000, 0, 0, 0, -200 * 1000000, -120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -200 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -400 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -250 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -120 * 1000000, -120 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest9()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, ConcreteMaterialEN1992Data.C45_55);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 0.01);
		}

		[TestMethod]
		public void RectangularSectionTest10()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection4SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, numberOfSideRebars, ConcreteMaterialEN1992Data.C45_55);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 0.01);
		}

		[TestMethod]
		public void RectangularSectionTest11()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection4SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, numberOfSideRebars, ConcreteMaterialEN1992Data.C45_55);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(100 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest12()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection4SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, numberOfSideRebars, ConcreteMaterialEN1992Data.C45_55);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(10 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(10 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest13()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection4SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, numberOfSideRebars, ConcreteMaterialEN1992Data.C45_55);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest14()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			Shape2d shape = GetRectangularShape(width, height);
			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			//ExportToGmsh(section);
			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(100 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void RectangularSectionTest15()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			ShapeEx shapeEx = new ShapeEx(GetRectangularShape(width, height), ConcreteMaterialEN1992Data.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 options =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, options, 0.01);
		}

		[TestMethod]
		public void RectangularSectionTest16()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			Shape2d shape = GetRectangularShape(width, height);
			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 350, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 350, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(250 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard,
					new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 0.01, new double[] { 1.0 });
		}

		[TestMethod]
		public void RectangularSectionTest17()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, 50, ConcreteMaterialModelCode2010Data.C25_30, SteelMaterialEN1992Data.B450C);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, -80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -50 * 1000000, -80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -20 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -80 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -100 * 1000000, -50 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard,
					new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN));
		}

		[TestMethod]
		public void RectangularSectionTest18()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, 50, ConcreteMaterialEN1992Data.C45_55, SteelMaterialEN1992Data.B450C);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1050 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -80 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 20 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -20 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard,
					new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN));
		}

		[TestMethod]
		public void RectangularSectionTest19()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, 0, rebarDiameter, 4, 50);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-20 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-20 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-20 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-20 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-20 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-20 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-20 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-20 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-20 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-50 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-50 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-50 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-50 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-50 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-50 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-50 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-50 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions);
		}

		[TestMethod]
		public void RectangularSectionTest20()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, 2, rebarDiameter, 4, 50);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 5 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 15 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 25 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 35 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 45 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 55 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 60 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 65 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 70 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 75 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 85 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 90 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 95 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, 50 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 0.5, 1.0, 1.5 });
		}

		[TestMethod]
		public void RectangularSectionTest21()
		{
			double rebarDiameter = 18;
			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(400, 400, rebarDiameter, 2, rebarDiameter, 4, 50);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = GetForces(section, 20 * 1000, 20 * 1000000, 20 * 1000000, 3);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 0.5, 1.0, 1.5 });
		}

		[TestMethod]
		public void RectangularSectionTest22()
		{
			double rebarDiameter = 18;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(400, 400, rebarDiameter, 2, rebarDiameter, 4, 50);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = GetForces(section, -200 * 1000, 20 * 1000000, 20 * 1000000, 3);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions, 0.005, new double[] { 0.5, 1.0, 1.5 });
		}

		[TestMethod]
		public void RectangularSectionTest23()
		{
			double rebarDiameter = 20;
			ShapeEx shapeEx = new ShapeEx(GetRectangularShape(400, 400), ConcreteMaterialEN1992Data.C50_60);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = GetForces(section, -200 * 1000, 20 * 1000000, 20 * 1000000, 3);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 0.5, 1.0, 1.5 });
		}

		[TestMethod]
		public void RectangularSectionTest24()
		{
			double rebarDiameter = 20;

			Shape2d shape = GetRectangularShape(400, 400);
			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialModelCode2010Data.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantNMy);

			ResultBeamForces[] forces = GetForces(section, -200 * 1000, 20 * 1000000, 5 * 1000000, 3);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 1.0, 1.5, 2.0 });
		}

		[TestMethod]
		public void RectangularSectionTest25()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;

			Shape2d shape = GetRectangularShape(width, height);
			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantNMx);

			double n = -300 * 1000;
			double Mx = 20 * 1000000;
			double My = 20 * 1000000;
			int iter = 2;

			ResultBeamForces[] forces = GetForces(section, n, Mx, My, iter);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 1.0 });
		}

		[TestMethod]
		public void RectangularSectionTest26()
		{
			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			CoordinateSystem cs = new CoordinateSystem(new Point3d(150, 150, 0), Vector2d.XAxis, Vector2d.YAxis);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(cs, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1200 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 1.0 });
		}

		[TestMethod]
		public void RectangularSectionTest27()
		{
			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(400, 400, 18, 2, 18, 4, 50);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);
			ResultBeamForces[] forces = GetForces(section, -200 * 1000, 30 * 1000000, 10 * 1000000, 3);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions, 0.005, new double[] { 0.5, 1.0, 1.5 });
		}

		[TestMethod]
		public void RectangularSectionTest28()
		{
			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(400, 400, 18, 2, 18, 4, 50);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = GetForces(section, -200 * 1000, 20 * 1000000, 20 * 1000000, 3);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions, 0.005, new double[] { 0.5, 1.0, 1.5 });
		}

		[TestMethod]
		public void RectangularSectionTest29()
		{
			RebarSectionCircular rebar = new RebarSectionCircular(20, SteelMaterialEN1992Data.B450C);
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,350,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(new ShapeEx(GetRectangularShape(400, 400), ConcreteMaterialEN1992Data.C25_30));
			section.AddRebars(rebars);

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantNMy);

			ResultBeamForces[] forces = GetForces(section, -200 * 1000, 20 * 1000000, 0 * 1000000, 3);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions);
		}

		[TestMethod]
		public void RectangularSectionTest30()
		{
			RebarSectionCircular rebar = new RebarSectionCircular(20, SteelMaterialEN1992Data.B450C);
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(new ShapeEx(GetRectangularShape(400, 500), ConcreteMaterialEN1992Data.C25_30));
			section.AddRebars(rebars);

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			ResultBeamForces[] forces = GetRandomResultBeamForces(section);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions);
		}


		[TestMethod]
		public void RectangularSectionTest31()
		{
			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock);
			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, 18, 50, concreteMaterial);

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 options =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			ResultBeamForces force = new ResultBeamForces(0, 0, 0, 0, 10000000, 0, GetLocalCoordinateSystem(section));
			CommonAssertDomainPointMethod(section, force, new StandardNTC2018Concrete(), options);

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());
			FailureDomain.FailureDomainPoint result = solver.CalculatePlasticDomainPointTest(force.ConvertToForceTuple(options.ForceReferenceCoordinateSystem),
				options.ForceReferenceCoordinateSystem, options.FailureAnalysisType);

			double expMxRd = 84.6 * 1000000;  // da VCA
			Assert.IsTrue(Math.Abs(result.MxRd - expMxRd) / expMxRd * 100 < 1);
		}

		[TestMethod]
		public void RectangularSectionTest32()
		{
			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 45, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock);
			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, 18, 50, concreteMaterial);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 options =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			ResultBeamForces force = new ResultBeamForces(0, 0, 0, 0, 10000000, 0, GetLocalCoordinateSystem(section));
			CommonAssertDomainPointMethod(section, force, new StandardNTC2018Concrete(), options);

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());
			FailureDomain.FailureDomainPoint result = solver.CalculatePlasticDomainPointTest(force.ConvertToForceTuple(options.ForceReferenceCoordinateSystem),
				options.ForceReferenceCoordinateSystem, options.FailureAnalysisType);

			double expMxRd = 88.0 * 1000000;  // da VCA
			Assert.IsTrue(Math.Abs(result.MxRd - expMxRd) / expMxRd * 100 < 1);
		}

		[TestMethod]
		public void RectangularSectionTest33()
		{
			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 45, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle);

			RebarSectionCircular rebar = new RebarSectionCircular(18, SteelMaterialEN1992Data.B450C);
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(new ShapeEx(GetRectangularShape(300, 500), concreteMaterial));
			section.AddRebars(rebars);

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 options =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			ResultBeamForces force = new ResultBeamForces(0 * 1000, 0, 0, 0, 100000000, 0, GetLocalCoordinateSystem(section));

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());
			FailureDomain.FailureDomainPoint result = solver.CalculatePlasticDomainPointTest(force.ConvertToForceTuple(options.ForceReferenceCoordinateSystem),
				options.ForceReferenceCoordinateSystem, options.FailureAnalysisType);

			double expMxRd = 208.0 * 1000000;  // da VCA
			Assert.IsTrue(Math.Abs(result.MxRd - expMxRd) / expMxRd * 100 < 2);
		}

		[TestMethod]
		public void RectangularSectionTest34()
		{
			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 45, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock);

			RebarSectionCircular rebar = new RebarSectionCircular(26, SteelMaterialEN1992Data.B450C);
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(new ShapeEx(GetRectangularShape(300, 500), concreteMaterial));
			section.AddRebars(rebars);

			SectionCheckerModelCode2010.SectionOptionsModelCode2010 options =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			ResultBeamForces force = new ResultBeamForces(0 * 1000, 0, 0, 0, 100000000, 0, GetLocalCoordinateSystem(section));

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());
			FailureDomain.FailureDomainPoint result = solver.CalculatePlasticDomainPointTest(force.ConvertToForceTuple(options.ForceReferenceCoordinateSystem),
				options.ForceReferenceCoordinateSystem, options.FailureAnalysisType);

			double expMxRd = 419.7 * 1000000;  // da VCA
			Assert.IsTrue(Math.Abs(result.MxRd - expMxRd) / expMxRd * 100 < 1);
		}

		[TestMethod]
		public void RectangularSectionTest35()
		{
			double rebarDiameter10 = 26;
			double height = 500;
			double width = 300;

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 45, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
			Shape2d shape = GetRectangularShape(width, height);

			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebarSection16 = new RebarSectionCircular(rebarDiameter10, SteelMaterialEN1992Data.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebarSection16, new Point3d(0,50,0)),
				new ReinforcedConcreteRebar(rebarSection16, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebarSection16, new Point3d(100,50,0)),
				new ReinforcedConcreteRebar(rebarSection16, new Point3d(150,50,0)),
				new ReinforcedConcreteRebar(rebarSection16, new Point3d(200,50,0)),
				new ReinforcedConcreteRebar(rebarSection16, new Point3d(250,50,0)),
				new ReinforcedConcreteRebar(rebarSection16, new Point3d(300,50,0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			ResultBeamForces force = new ResultBeamForces(0 * 1000, 0, 0, 0, 100000000, 0, GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 options =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());
			FailureDomain.FailureDomainPoint result = solver.CalculatePlasticDomainPointTest(force.ConvertToForceTuple(options.ForceReferenceCoordinateSystem),
				options.ForceReferenceCoordinateSystem, options.FailureAnalysisType);

			double expMxRd1 = 512 * 1000000;  // da VCA
			double expMxRd2 = 516 * 1000000;  // da Excel
			Assert.IsTrue(Math.Abs(result.MxRd - expMxRd1) / expMxRd1 * 100 < 1);
			Assert.IsTrue(Math.Abs(result.MxRd - expMxRd2) / expMxRd2 * 100 < 1);
		}

		[TestMethod]
		public void RectangularSectionTest36()
		{
			double rebarDiameter = 25.49;
			double height = 500;
			double width = 300;
			double copriferro = 70;

			ConcreteMaterialACI318 concreteMaterial = new ConcreteMaterialACI318("", 28, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
			Shape2d shape = GetRectangularShape(width, height);

			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebarSection = new RebarSectionCircular(rebarDiameter, new SteelMaterialACI318("", 200000, 420, 420));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebarSection, new Point3d(50, copriferro, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(150, copriferro, 0)),
				new ReinforcedConcreteRebar(rebarSection, new Point3d(250, copriferro, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			ResultBeamForces force = new ResultBeamForces(0 * 1000, 0, 0, 0, 100000000, 0, GetLocalCoordinateSystem(section));
			SectionCheckerACI318.SectionOptionsStandardACI318 options = new SectionCheckerACI318.SectionOptionsStandardACI318(GetLocalCoordinateSystem(section));

			SectionSolverACI318Test solver = new SectionSolverACI318Test(section, new StandardACI318p08(), true);
			FailureDomain.FailureDomainPoint result = solver.CalculatePlasticDomainPointTest(force.ConvertToForceTuple(options.ForceReferenceCoordinateSystem),
				options.ForceReferenceCoordinateSystem, options.FailureAnalysisType);

			double expMxRd1 = 247.4 * 1000000 * 0.9;  // valore nominale da design_of_reinforced_concrete_9th_edition
			Assert.IsTrue(Math.Abs(result.MxRd - expMxRd1) / expMxRd1 * 100 < 2);
		}

		[TestMethod]
		[TestCategory("Bridge")]
		public void Bridge_2()
		{
			ReinforcedConcreteSection section = GetBridgeSection(4600, 1800, 3000, 300, 300, 200, 60,
				10, 14, 13, 14, 10, 14,
				7, 12,
				4, 22, 13, 20, 4, 22,
				ConcreteMaterialEN1992Data.C35_45, SteelMaterialEN1992Data.B450C);

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 9126.0 * 1000000, 7178.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 7500.0 * 1000000, -10000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 8000.0 * 1000000, -10000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 8500.0 * 1000000, -10000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 9000.0 * 1000000, -10000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 9500.0 * 1000000, -10000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 10000.0 * 1000000, -10000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 10500.0 * 1000000, -10000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 11000.0 * 1000000, -10000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 11500.0 * 1000000, -10000.0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 0.75, 1.0, 1.25 }, SectionSolver.FailureDomainTypes.Elastic);
		}

		[TestMethod]
		[TestCategory("Bridge")]
		public void Bridge_3()
		{
			ReinforcedConcreteSection section = GetBridgeSection(4600, 1800, 3000, 300, 300, 200, 60,
				10, 14, 13, 14, 10, 14,
				7, 12,
				4, 22, 13, 20, 4, 22,
				ConcreteMaterialEN1992Data.C35_45, SteelMaterialEN1992Data.B450C);

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 7500.0 * 1000000, -9000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 8000.0 * 1000000, -9000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 8500.0 * 1000000, -9000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 9000.0 * 1000000, -9000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 9500.0 * 1000000, -9000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 10000.0 * 1000000, -9000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 10500.0 * 1000000, -9000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 11000.0 * 1000000, -9000.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 11500.0 * 1000000, -9000.0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 0.75, 1.0, 1.25 }, SectionSolver.FailureDomainTypes.Elastic);
		}

		[TestMethod]
		[TestCategory("Bridge")]
		public void Bridge_4()
		{
			ReinforcedConcreteSection section = GetBridgeSection(4600, 1800, 3000, 300, 300, 200, 60,
				10, 14, 13, 14, 10, 14,
				7, 12,
				4, 22, 13, 20, 4, 22,
				ConcreteMaterialEN1992Data.C35_45, SteelMaterialEN1992Data.B450C);

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 7500.0 * 1000000, -9500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 8000.0 * 1000000, -9500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 8500.0 * 1000000, -9500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 9000.0 * 1000000, -9500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 9500.0 * 1000000, -9500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 10000.0 * 1000000, -9500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 10500.0 * 1000000, -9500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 11000.0 * 1000000, -9500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 11500.0 * 1000000, -9500.0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 0.75, 1.0, 1.25 }, SectionSolver.FailureDomainTypes.Elastic);
		}

		[TestMethod]
		[TestCategory("Bridge")]
		public void Bridge_5()
		{
			ReinforcedConcreteSection section = GetBridgeSection(4600, 1800, 3000, 300, 300, 200, 60,
				10, 14, 13, 14, 10, 14,
				7, 12,
				4, 22, 13, 20, 4, 22,
				ConcreteMaterialEN1992Data.C35_45, SteelMaterialEN1992Data.B450C);

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 7500.0 * 1000000, -10500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 8000.0 * 1000000, -10500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 8500.0 * 1000000, -10500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 9000.0 * 1000000, -10500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 9500.0 * 1000000, -10500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 10000.0 * 1000000, -10500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 10500.0 * 1000000, -10500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 11000.0 * 1000000, -10500.0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-12883.0 * 1000, 0, 0, 0, 11500.0 * 1000000, -10500.0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 0.75, 1.0, 1.25 }, SectionSolver.FailureDomainTypes.Elastic);
		}

		[TestMethod]
		public void SquareSectionTest1()
		{
			double rebarDiameter = 20;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, width, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C45_55);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int j = 0; j < forces.Length; j++)
				CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));

		}

		[TestMethod]
		public void SquareSectionTest2()
		{
			double rebarDiameter = 20;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, width, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C25_30);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 20 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-300 * 1000, 0, 0, 0, 20 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, 20 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, -20 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-300 * 1000, 0, 0, 0, -20 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -20 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int j = 0; j < forces.Length; j++)
				CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void SquareSectionTest3()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, numberOfSideRebars, ConcreteMaterialEN1992Data.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int j = 0; j < forces.Length; j++)
				CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void SquareSectionTest4()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, numberOfSideRebars, ConcreteMaterialEN1992Data.C25_30);
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

			for (int j = 0; j < forces.Length; j++)
				CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void SquareSectionTest5()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, numberOfSideRebars, ConcreteMaterialEN1992Data.C45_55);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 20 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 20 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -20 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -20 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)));
		}

		[TestMethod]
		public void SquareSectionTest6()
		{
			double rebarDiameter = 26;
			double h = 500;

			Shape2d shape = GetRectangularShape(h, h);
			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C30_37);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new SteelMaterial("", 200000, 450, 450, 0.075, SteelMaterial.SteelTypes.Rebar));

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
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 150, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 350, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 150, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 350, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			ResultBeamForces force = new ResultBeamForces(-512.2 * 1000, 0, 0, 0, 380.4 * 1000000, -86.79 * 1000000, GetLocalCoordinateSystem(section), 1);

			SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section,
				null, new ResultBeamForces[] { force }, new StandardNTC2018Concrete());

			FailureDomain.FailureDomainPoint forceConstEccentr = sectionChecker.CalculateElasticFailureDomainPoint(force);

			FailureDomainResult result = sectionChecker.GetElasticFailureDomainResult();
			FailureDomain.FailureDomainForce[] forces = result.GetFailureDomainForces();

			FailureDomainResult2d failureDomainResult2d = result.CalculateDomainConstantAxialForce(force);
			FailureDomain.FailureDomainForce2d[] forces2d = failureDomainResult2d.GetFailureDomainForces();

			ForceTuple expForceConstantMxMy = new ForceTuple(-676 * 1000, 496.21 * 1000000, -113.2 * 1000000);
			ForceTuple expForceConstantN = new ForceTuple(-512.2 * 1000, 487.2 * 1000000, -111 * 1000000);

			double expWR1 = 0.7689;
			double expWR2 = 1.0 / 1.3;

			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForceConstantN, forces2d[0], expWR1, 3.5);
			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForceConstantMxMy, forces[0], expWR2);
		}

		[TestMethod]
		public void CircularHole1()
		{
			// \\studio\Software_Development\FilesForTesting\Libs\GPCChecker\ConcreteSolver\VCA_file\CHS_D500 

			double rebarDiameter = 16;
			double externalDiameter = 500;
			double thickness = 100;
			double concreteCover = 50;
			int numberOfRebars = 16;

			ReinforcedConcreteSection section = GetCHS(externalDiameter, thickness, concreteCover, numberOfRebars, rebarDiameter,
				ConcreteMaterialEN1992Data.C45_55);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			ResultBeamForces[] forces = GetRandomResultBeamForces(section);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions);
		}

		[TestMethod]
		public void CircularHole2()
		{
			double rebarDiameter = 12;
			double externalDiameter = 500;
			double thickness = 100;
			double concreteCover = 50;
			int numberOfRebars = 16;

			ReinforcedConcreteSection section = GetCHS(externalDiameter, thickness, concreteCover, numberOfRebars, rebarDiameter,
				ConcreteMaterialEN1992Data.C40_50);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			ResultBeamForces[] forces = GetRandomResultBeamForces(section);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions);
		}

		[TestMethod]
		public void CircularHole3()
		{
			double rebarDiameter = 8;
			double externalDiameter = 500;
			double thickness = 100;
			double concreteCover = 50;
			int numberOfRebars = 16;

			ReinforcedConcreteSection section = GetCHS(externalDiameter, thickness, concreteCover, numberOfRebars, rebarDiameter,
				ConcreteMaterialEN1992Data.C55_67);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			//ResultBeamForces[] forces = new ResultBeamForces[] { 
			//    new ResultBeamForces(-450.0 * 1000, 0, 0, 0, 75 * 1000000,0, GetLocalCoordinateSystem(section)) };// GetRandomResultBeamForces(section);
			ResultBeamForces[] forces = GetRandomResultBeamForces(section);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions);
		}

		[TestMethod]
		public void CircularHole4()
		{
			double rebarDiameter = 16;
			double externalDiameter = 500;
			double thickness = 100;
			double concreteCover = 50;
			int numberOfRebars = 16;

			ReinforcedConcreteSection section = GetCHS(externalDiameter, thickness, concreteCover,
				numberOfRebars, rebarDiameter, ConcreteMaterialEN1992Data.C30_37);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			ResultBeamForces[] forces = GetRandomResultBeamForces(section);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions);
		}

		[TestMethod]
		public void CircularHole5()
		{
			double rebarDiameter = 8;
			double externalDiameter = 500;
			double thickness = 100;
			double concreteCover = 50;
			int numberOfRebars = 16;

			ReinforcedConcreteSection section = GetCHS(externalDiameter, thickness, concreteCover,
				numberOfRebars, rebarDiameter, ConcreteMaterialEN1992Data.C45_55);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));

			ResultBeamForces[] forces = GetRandomResultBeamForces(section);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions);
		}

		[TestMethod]
		public void CircularHole6()
		{
			double rebarDiameter = 8;
			double externalDiameter = 500;
			double thickness = 100;
			double concreteCover = 50;
			int numberOfRebars = 12;

			ReinforcedConcreteSection section = GetCHS(externalDiameter, thickness, concreteCover, numberOfRebars, rebarDiameter,
				ConcreteMaterialEN1992Data.C28_35, SteelMaterialEN1992Data.B500C, 32, new Point2d(250, 250));
			ResultBeamForces force = new ResultBeamForces(-800 * 1000, 0, 0, 0, 0, -100 * 1e6, GetLocalCoordinateSystem(section), 1);

			SectionCheckerModelCode2010 sectionCheckerModelCode2010 = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section),
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section),
				SectionSolver.FailureAnalysisTypes.ConstantN),
				new StandardNTC2018Concrete(), false);

			var domain = sectionCheckerModelCode2010.GetPlasticFailureDomainResult();
			var forceOnDomain = domain.AddForce(force);

			Console.WriteLine($"Area: {section.Area}");
			Console.WriteLine($"AreaRebars: {section.AreaRebars}");
			Console.WriteLine($"Input N: {force.N / 1000} Mx: {force.M1 / 1000000} My: {force.M2 / 1000000}");
			Console.WriteLine($"N: {forceOnDomain.ForceTuple.N / 1000} Mx: {forceOnDomain.ForceTuple.Mx / 1000000} My: {forceOnDomain.ForceTuple.My / 1000000}");

			Assert.AreEqual(forceOnDomain.ForceTuple.N / 1000, -800, 1);
			Assert.AreEqual(forceOnDomain.ForceTuple.My / 1000000, -150, 1);
		}

		[TestMethod]
		public void CircularHole7()
		{
			double rebarDiameter = 12;
			double externalDiameter = 500;
			double thickness = 100;
			double concreteCover = 50;
			int numberOfRebars = 16;

			ReinforcedConcreteSection section = GetCHS(externalDiameter, thickness, concreteCover,
				numberOfRebars, rebarDiameter, ConcreteMaterialEN1992Data.C30_37);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = GetRandomResultBeamForces(section);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions);
		}

		[TestMethod]
		public void CircularHole8()
		{
			double rebarDiameter = 8;
			double externalDiameter = 500;
			double thickness = 100;
			double concreteCover = 50;
			int numberOfRebars = 16;

			ReinforcedConcreteSection section = GetCHS(externalDiameter, thickness, concreteCover,
				numberOfRebars, rebarDiameter, ConcreteMaterialEN1992Data.C45_55);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);

			ResultBeamForces[] forces = GetRandomResultBeamForces(section);

			for (int i = 0; i < forces.Length; i++)
				CommonAssertDomainPointMethod(section, forces[i], new StandardEN1992p11(), sectionOptions);
		}

		[TestMethod]
		public void DomainCostantAxialForceTest1()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C35_45);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces force = new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section));

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomainResult = sectionChecker.GetPlasticFailureDomainResult();
			FailureDomainResult2d failureDomainResult2d = sectionChecker.GetPlasticFailureDomainResult2d();

			FailureDomain.FailureDomainForce2d[] forces2d = failureDomainResult2d.GetFailureDomainForces();

			//ExportToGmsh(failureDomainResult2d.Domain,
			//	new Line3d[] { new Line3d(new Point3d(0, 0, force.N), new Point3d(forces2d[0].FailureDomainPoint.Point)) },
			//	new Point3d[] { forces2d[0].FailureDomainPoint.Point, new Point3d(force.M1, force.M2, force.N) });

			//Console.WriteLine($"Ned = {Math.Round(force.N / 1000, 2)}, " +
			//	$"MXed = {Math.Round(force.M1 / 1000000, 2)}, " +
			//	$"MYed = {Math.Round(force.M2 / 1000000, 2)}");

			//Console.WriteLine($"NRD = {Math.Round(forces2d[0].FailureDomainPoint.NRd / 1000, 2)}, " +
			//	$"MXRD = {Math.Round(forces2d[0].FailureDomainPoint.MxRd / 1000000, 2)}, " +
			//	$"MYRD = {Math.Round(forces2d[0].FailureDomainPoint.MyRd / 1000000, 2)}");

			//var failureDomainPoint = failureDomainResult.GetDomainPointConstantAxialForce(forces);

			//Point3d expPointConcribe= new Point3d(420 * 1000000, 0, -1000 * 1000 );

			//Assert.IsTrue(Math.Abs(failureDomainPoint.FailureDomainPoint.Point.X) - )
		}

		[TestMethod]
		public void DomainCostantMxMyTest1()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C35_45);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces force = new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section), 1);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomainResult = sectionChecker.GetPlasticFailureDomainResult();
			FailureDomainResult2d failureDomainResult2d = failureDomainResult.CalculateDomainConstantMomentsRatio(force);
			failureDomainResult2d.AddForce(force);
			FailureDomain.FailureDomainForce2d[] forces2d = failureDomainResult2d.GetFailureDomainForces();

			ForceTuple expForce = new ForceTuple(-3147 * 1000, 314.7 * 1000000, 0);
			double expWR = 0.32;

			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForce, forces2d[0], expWR);
		}

		[TestMethod]
		public void DomainCostantMxMyTest2()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 500;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992Data.C35_45);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces force = new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section));

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomainResult = sectionChecker.GetPlasticFailureDomainResult();
			FailureDomainResult2d failureDomainResult2d = failureDomainResult.CalculateDomainConstantMomentsRatio(force);

			FailureDomain.FailureDomainForce2d[] forces2d = failureDomainResult2d.GetFailureDomainForces();

			ForceTuple expForce = new ForceTuple(-4000 * 1000, 400 * 1000000, 0);
			double expWR = 0.25;

			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForce, forces2d[0], expWR);
		}
	}
}
