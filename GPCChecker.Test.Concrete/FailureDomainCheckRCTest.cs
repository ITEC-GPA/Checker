using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using System.Collections.Generic;
using GPC.Model.Standards;
using GPC.Model.Sections;
using GPC.Checkers.Concrete.Attributes;
using GPC.Model.Results;
using GPC.TestUtilities;
using GPC.Checkers.Concrete.Helper;

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
			{
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))), 
					$"Force {j} fail");
			}
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
				//new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-200 * 1000, 0, 0, 0, 20 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-400 * 1000, 0, 0, 0, 20 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, -20 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -40 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, 120 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int i = 0; i < forces.Length; i++)
			{
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))), $"Force {i} fail");
			}
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
			{
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 
					0.005, new double[] {0.5, 1.0, 1.5 }), $"Force {i} fail");
			}
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
			{
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))), 
					$"Force {j} fail");
			}
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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))), 
					$"Force {i} fail");
			}
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
				ConcreteMaterialEN1992.C40_50);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-200 * 1000, 0, 0, 0, -20 * 1000000, 40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-400 * 1000, 0, 0, 0, -20 * 1000000, -120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, 20 * 1000000, 40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 40 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, -120 * 1000000, -120 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest7()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992.C40_50);
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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest8()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, ConcreteMaterialEN1992.C45_55);

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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest9()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, ConcreteMaterialEN1992.C45_55);
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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 0.01), 
					$"Force {i} fail");
			}

		}

		[TestMethod]
		public void RectangularSectionTest10()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection4SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, numberOfSideRebars, ConcreteMaterialEN1992.C45_55);
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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 0.01),
					$"Force {i} fail");
			}

		}

		[TestMethod]
		public void RectangularSectionTest11()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection4SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, numberOfSideRebars, ConcreteMaterialEN1992.C45_55);
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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 0.01),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest12()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection4SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, numberOfSideRebars, ConcreteMaterialEN1992.C45_55);
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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 0.01), 
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest13()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 600;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection4SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars, numberOfSideRebars, ConcreteMaterialEN1992.C45_55);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 0.01),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest14()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 0.01), 
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest15()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

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
			//ExportToGmsh(section);
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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, options, 0.01),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest16()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				//new ResultBeamForces(100 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(100 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(100 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(100 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(100 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(100 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(100 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(100 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(100 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(100 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(100 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(100 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(250 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, 
					new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section)), 0.01, new double[] {1.0}),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest17()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, 50, ConcreteMaterialEN1992.C25_30, SteelMaterial.B450C);
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
			{
				CommonAssertDomainPointMethod(section, forces[i], standard, 
					new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN));
			}
		}

		[TestMethod]
		public void RectangularSectionTest18()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, 50, ConcreteMaterialEN1992.C25_30, SteelMaterial.B450C);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
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
			{
				CommonAssertDomainPointMethod(section, forces[i], standard,
					new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantMxMy));
			}
		}

		[TestMethod]
		public void RectangularSectionTest19()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
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

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(20 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(20 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(50 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest20()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

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
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 0.5, 1.0, 1.5 }),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest21()
		{
			double rebarDiameter = 18;
			double height = 400;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

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

			double n = 10 * 1000;
			double Mx = 10 * 1000000;
			double My = 10 * 1000000;
			int iter = 3;

			List<ResultBeamForces> list = new List<ResultBeamForces>();
			for (int k = 0; k < iter; k++)
			{
				for (int i = -iter; i < iter; i++)
				{
					for (int j = -iter; j < iter; j++)
					{
						if(i != 0 && j != 0)
							list.Add(new ResultBeamForces(n * k, 0, 0, 0, Mx * i, My * j, GetLocalCoordinateSystem(section)));
					}
				}
			}

			ResultBeamForces[] forces = list.ToArray();

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] {0.5, 1.0, 1.5 }),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest22()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C50_60);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

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

			double n = -200 * 1000;
			double Mx = 10 * 1000000;
			double My = 10 * 1000000;
			int iter = 3;

			List<ResultBeamForces> list = new List<ResultBeamForces>();
			for (int k = 0; k < iter; k++)
			{
				for (int i = -iter; i < iter; i++)
				{
					for (int j = -iter; j < iter; j++)
					{
						if (i != 0 && j != 0)
							list.Add(new ResultBeamForces(n * k, 0, 0, 0, Mx * i, My * j, GetLocalCoordinateSystem(section)));
					}
				}
			}

			ResultBeamForces[] forces = list.ToArray();

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 0.5, 1.0, 1.5 }),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest23()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C50_60);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

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

			double n = -200 * 1000;
			double Mx = 10 * 1000000;
			double My = 10 * 1000000;
			int iter = 2;

			List<ResultBeamForces> list = new List<ResultBeamForces>();
			for (int k = 0; k < iter; k++)
			{
				for (int i = -iter; i < iter; i++)
				{
					for (int j = -iter; j < iter; j++)
					{
						if (i != 0 && j != 0)
							list.Add(new ResultBeamForces(n * k, 0, 0, 0, Mx * i, My * j, GetLocalCoordinateSystem(section)));
					}
				}
			}

			ResultBeamForces[] forces = list.ToArray();

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] {0.5, 1.0, 1.5 }),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest24()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

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

			double n = -300 * 1000;
			double Mx = 20 * 1000000;
			double My = 20 * 1000000;
			int iter = 2;

			List<ResultBeamForces> list = new List<ResultBeamForces>();
			for (int k = 0; k < iter; k++)
			{
				for (int i = -iter; i < iter; i++)
				{
					for (int j = -iter; j < iter; j++)
					{
						if (i != 0 && j != 0)
							list.Add(new ResultBeamForces(n * k, 0, 0, 0, Mx * i, My * j, GetLocalCoordinateSystem(section)));
					}
				}
			}

			ResultBeamForces[] forces = list.ToArray();

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 1.0, 1.5, 2.0 }),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest25()
		{
			double rebarDiameter = 20;
			double height = 400;
			double width = 400;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterial.B450C);

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

			List<ResultBeamForces> list = new List<ResultBeamForces>();
			for (int k = 0; k < iter; k++)
			{
				for (int i = -iter; i < iter; i++)
				{
					for (int j = -iter; j < iter; j++)
					{
						if (i != 0 && j != 0)
							list.Add(new ResultBeamForces(n * k, 0, 0, 0, Mx * i, My * j, GetLocalCoordinateSystem(section)));
					}
				}
			}

			ResultBeamForces[] forces = list.ToArray();

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 1.0, 1.5, 2.0 }),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest26()
		{
			ReinforcedConcreteSection section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992.C25_30, SteelMaterial.B450C);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			CoordinateSystem cs = new CoordinateSystem(new Point3d(150, 150, 0), Vector2d.XAxis, Vector2d.YAxis);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(cs, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[i], standard, sectionOptions, 0.005, new double[] { 1.0 }),
					$"Force {i} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest1()
		{
			double rebarDiameter = 20;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, width, rebarDiameter, concreteCover, ConcreteMaterialEN1992.C45_55);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int j = 0; j < forces.Length; j++)
			{
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))),
					$"Force {j} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest2()
		{
			double rebarDiameter = 20;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, width, rebarDiameter, concreteCover, ConcreteMaterialEN1992.C25_30);
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
			{
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))),
					$"Force {j} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest3()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, numberOfSideRebars, ConcreteMaterialEN1992.C25_30);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int j = 0; j < forces.Length; j++)
			{
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))),
					$"Force {j} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest4()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, numberOfSideRebars, ConcreteMaterialEN1992.C25_30);
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
			{
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))), 
					$"Force {j} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest5()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, numberOfSideRebars, ConcreteMaterialEN1992.C45_55);
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

			for (int j = 0; j < forces.Length; j++)
			{
				Assert.IsTrue(CommonAssertDomainPointMethod(section, forces[j], standard, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section))),
					$"Force {j} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest6()
		{
			double rebarDiameter = 26;
			double h = 500;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(h, 0),
				new Point2d(h, h),
				new Point2d(0, h)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 30, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle));
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
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 150, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50, 350, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 150, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 250, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(450, 350, 0)),
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces force = new ResultBeamForces(-512.2 * 1000, 0, 0, 0, 380.4 * 1000000, -86.79 * 1000000, GetLocalCoordinateSystem(section), 1);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, new ResultBeamForces[] { force });
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult result = sectionChecker.GetElasticFailureDomainResult();
			FailureDomain.FailureDomainForce[] forces = result.GetFailureDomainForces();

			FailureDomainResult2d failureDomainResult2d = result.CalculateDomainConstantAxialForce(force);
			FailureDomain.FailureDomainForce[] forces2d = failureDomainResult2d.GetFailureDomainForces();

			ForceTuple expForceConstantMxMy = new ForceTuple(-676 * 1000, 496.21 * 1000000, -113.2 * 1000000);
			ForceTuple expForceConstantN = new ForceTuple(-512.2 * 1000, 487.2 * 1000000, -111 * 1000000);

			double expWR1 = 0.7689;
			double expWR2 = 1.0 / 1.3;

			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForceConstantN, forces2d[0], expWR1);
			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForceConstantMxMy, forces[0], expWR2);

			//ExportToGmsh(failureDomainResult2d.Domain,
			//	new Line3d[] { new Line3d(new Point3d(0, 0, force.N), new Point3d(forces2d[0].FailureDomainPoint.Point)) },
			//	new Point3d[] { forces2d[0].FailureDomainPoint.Point, new Point3d(force.M1, force.M2, force.N) });
		}

		[TestMethod]
		public void SquareSectionTest7()
		{
			double rebarDiameter = 26;
			double h = 500;

			ReinforcedConcreteSection section = GetRectangularSection4SideRebars(h, h, rebarDiameter, 50, 5, 5, ConcreteMaterialEN1992.C30_37);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces force = new ResultBeamForces(-512.2 * 1000, 0, 0, 0, 380.4 * 1000000, -86.79 * 1000000, GetLocalCoordinateSystem(section), 1);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, new ResultBeamForces[] { force });
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult result = sectionChecker.GetElasticFailureDomainResult();
			FailureDomain.FailureDomainForce[] forces = result.GetFailureDomainForces();

			FailureDomainResult2d failureDomainResult2d = result.CalculateDomainConstantAxialForce(force);
			FailureDomain.FailureDomainForce[] forces2d = failureDomainResult2d.GetFailureDomainForces();

			ForceTuple expForceConstantMxMy = new ForceTuple(-676 * 1000, 496.21 * 1000000, -113.2 * 1000000);
			ForceTuple expForceConstantN = new ForceTuple(-512.2 * 1000, 487.2 * 1000000, -111 * 1000000);

			double expWR1 = 0.7689;
			double expWR2 = 1.0 / 1.3;

			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForceConstantN, forces2d[0], expWR1);
			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForceConstantMxMy, forces[0], expWR2);

			//ExportToGmsh(failureDomainResult2d.Domain,
			//	new Line3d[] { new Line3d(new Point3d(0, 0, force.N), new Point3d(forces2d[0].FailureDomainPoint.Point)) },
			//	new Point3d[] { forces2d[0].FailureDomainPoint.Point, new Point3d(force.M1, force.M2, force.N) });
		}

		[TestMethod]
		public void DomainCostantAxialForceTest1()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992.C35_45);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces force = new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section));

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
	new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomainResult = sectionChecker.GetPlasticFailureDomainResult();
			FailureDomainResult2d failureDomainResult2d = failureDomainResult.CalculateDomainConstantAxialForce(force);

			FailureDomain.FailureDomainForce[] forces2d = failureDomainResult2d.GetFailureDomainForces();

			//ExportToGmsh(failureDomainResult2d.Domain,
			//	new Line3d[] { new Line3d(new Point3d(0, 0, force.N), new Point3d(forces2d[0].FailureDomainPoint.Point)) },
			//	new Point3d[] { forces2d[0].FailureDomainPoint.Point, new Point3d(force.M1, force.M2, force.N) });

			Console.WriteLine($"Ned = {Math.Round(force.N / 1000, 2)}, " +
				$"MXed = {Math.Round(force.M1 / 1000000, 2)}, " +
				$"MYed = {Math.Round(force.M2 / 1000000, 2)}");

			Console.WriteLine($"NRD = {Math.Round(forces2d[0].FailureDomainPoint.NRd / 1000, 2)}, " +
				$"MXRD = {Math.Round(forces2d[0].FailureDomainPoint.MxRd / 1000000, 2)}, " +
				$"MYRD = {Math.Round(forces2d[0].FailureDomainPoint.MyRd / 1000000, 2)}");

			//var failureDomainPoint = failureDomainResult.GetDomainPointConstantAxialForce(forces);

			//Point3d expPoint = new Point3d(420 * 1000000, 0, -1000 * 1000 );

			//Assert.IsTrue(Math.Abs(failureDomainPoint.FailureDomainPoint.Point.X) - )
		}

		[TestMethod]
		public void DomainCostantMxMyTest1()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992.C35_45);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces force = new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section), 1);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = 
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomainResult = sectionChecker.GetPlasticFailureDomainResult();
			FailureDomainResult2d failureDomainResult2d = failureDomainResult.CalculateFailureDomainCostantMomentsRatio(force);
			failureDomainResult2d.AddForce(force);
			FailureDomain.FailureDomainForce[] forces2d = failureDomainResult2d.GetFailureDomainForces();

			ForceTuple expForce = new ForceTuple(-3147 * 1000, 314.7 * 1000000, 0);
			double expWR = 0.32;

			//ExportToGmsh(failureDomainResult2d.Domain,
			//	new Line3d[] { new Line3d(new Point3d(0, 0, 0), new Point3d(forces2d[0].FailureDomainPoint.Point)) },
			//	new Point3d[] { forces2d[0].FailureDomainPoint.Point, new Point3d(force.M1, force.M2, force.N) });
			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForce, forces2d[0], expWR);
		}

		[TestMethod]
		public void DomainCostantMxMyTest2()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 500;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992.C35_45);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces force = new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section));

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = 
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomainResult = sectionChecker.GetPlasticFailureDomainResult();
			FailureDomainResult2d failureDomainResult2d = failureDomainResult.CalculateFailureDomainCostantMomentsRatio(force);

			FailureDomain.FailureDomainForce[] forces2d = failureDomainResult2d.GetFailureDomainForces();

			ForceTuple expForce = new ForceTuple(-4000 * 1000, 400 * 1000000, 0);
			double expWR = 0.25;

			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForce, forces2d[0], expWR);

			//ExportToGmsh(failureDomainResult2d.Domain,
			//	new Line3d[] { new Line3d(new Point3d(0, 0, 0), new Point3d(forces2d[0].FailureDomainPoint.Point)) },
			//	new Point3d[] { forces2d[0].FailureDomainPoint.Point, new Point3d(force.M1, force.M2, force.N) });
		}

		[TestMethod]
		public void DomainCostantMxMyTest3()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 500;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, ConcreteMaterialEN1992.C35_45);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces force = new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section));

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = 
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomainResult = sectionChecker.GetPlasticFailureDomainResult();
			FailureDomainResult2d failureDomainResult2d = failureDomainResult.CalculateFailureDomainCostantMomentsRatio(force);

			FailureDomain.FailureDomainForce[] forces2d = failureDomainResult2d.GetFailureDomainForces();

			ForceTuple expForce = new ForceTuple(-4000 * 1000, 0, 400 * 1000000);
			double expWR = 0.25;

			CommonAssertsDomainCheck(section, force.ConvertToForceTuple(GetLocalCoordinateSystem(section)), expForce, forces2d[0], expWR);

			//ExportToGmsh(failureDomainResult2d.Domain,
			//	new Line3d[] { new Line3d(new Point3d(0, 0, 0), new Point3d(forces2d[0].FailureDomainPoint.Point)) },
			//	new Point3d[] { forces2d[0].FailureDomainPoint.Point, new Point3d(force.M1, force.M2, force.N) });
		}

	}
}
