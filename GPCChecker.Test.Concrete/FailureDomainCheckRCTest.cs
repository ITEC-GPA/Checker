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
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[j], standard), $"Force {j} fail");
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
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[i], standard), $"Force {i} fail");
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
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[i], standard, 0.005, new double[] { 1.0 }), $"Force {i} fail");
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

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			for (int j = 0; j < forces.Length; j++)
			{
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[j], standard), $"Force {j} fail");
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
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[i], standard), $"Force {i} fail");
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
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[i], standard), $"Force {i} fail");
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
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[i], standard, 0.005, new double[] { 1.0 }), $"Force {i} fail");
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

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[i], standard), $"Force {i} fail");
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
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -10 * 1000000, -0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -20 * 1000000, -0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -30 * 1000000, -0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -40 * 1000000, -0 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -10 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -30 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -0 * 1000000, -40 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -10 * 1000000, -10 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, +10 * 1000000, +10 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -20 * 1000000, -20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, +20 * 1000000, +20 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -30 * 1000000, -30 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, +30 * 1000000, +30 * 1000000, new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[i], standard, 0.01), $"Force {i} fail");
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

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			for (int j = 0; j < forces.Length; j++)
			{
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[j], standard), $"Force {j} fail");
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

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			for (int j = 0; j < forces.Length; j++)
			{
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[j], standard), $"Force {j} fail");
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
				//new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
			};

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			for (int j = 0; j < forces.Length; j++)
			{
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[j], standard), $"Force {j} fail");
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

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			for (int j = 0; j < forces.Length; j++)
			{
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[j], standard), $"Force {j} fail");
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

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			for (int j = 0; j < forces.Length; j++)
			{
				Assert.IsTrue(CommonAssertDomainPoint(section, forces[j], standard), $"Force {j} fail");
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

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult result = sectionChecker.GetElasticFailureDomainResult();

			//ExportToGmsh(section);
			//ExportToGmsh(result.Domain);

			FailureDomain.FailureDomainForce[] resultsArray = result.GetFailureDomainForces();
			FailureDomain.FailureDomainPoint failurePoint = result.AddForce(force);

			Console.WriteLine($"Ned = {Math.Round(force.N / 1000, 2)}, " +
				$"MXed = {Math.Round(force.M1 / 1000000, 2)}, " +
				$"MYed = {Math.Round(force.M2 / 1000000, 2)}");

			Console.WriteLine($"NRD = {Math.Round(failurePoint.NRd / 1000, 2)}, " +
				$"MXRD = {Math.Round(failurePoint.MxRd / 1000000, 2)}, " +
				$"MYRD = {Math.Round(failurePoint.MyRd / 1000000, 2)}");

			Vector3d vRd = new Vector3d(failurePoint.Point);
			Vector3d eRd = new Vector3d(new Point3d(force.M1, force.M2, force.N));

			Console.WriteLine($"WR = {Math.Round(eRd.Length / vRd.Length, 3)}");

			FailureDomain2d resultNCost = result.CalculateDomainConstantAxialForce(force.ConvertToForceTuple(section.Centroid));
			FailureDomain.FailureDomainForce pointNCost = resultNCost.GetDomainPointConstantAxialForce(force, Vector2d.Zero);

			Vector3d vRd2 = new Vector3d(pointNCost.FailureDomainPoint.Point);
			Line3d line = new Line3d(Point3d.Origin, pointNCost.FailureDomainPoint.Point);

			Console.WriteLine($"NRD = {Math.Round(pointNCost.FailureDomainPoint.NRd / 1000, 2)}, " +
				$"MXRD = {Math.Round(pointNCost.FailureDomainPoint.MxRd / 1000000, 2)}, " +
				$"MYRD = {Math.Round(pointNCost.FailureDomainPoint.MyRd / 1000000, 2)}");

			Console.WriteLine($"WR = {Math.Round(eRd.Length / vRd2.Length, 3)}");

			ExportToGmsh(resultNCost, new Line3d[] {line}, new Point3d[] { pointNCost.FailureDomainPoint.Point, new Point3d(force.M1, force.M2, force.N) });
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

			ResultBeamForces forces = new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section));

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomainResult = sectionChecker.GetPlasticFailureDomainResult();
			//FailureDomain failureDomain = failureDomainResult.CalculateDomainConstantAxialForce(forces.ConvertToForceTuple(section.Centroid));

			//ExportToGmsh(failureDomain);

			//var failureDomainPoint = failureDomainResult.GetDomainPointConstantAxialForce(forces);

			//Point3d expPoint = new Point3d(420 * 1000000, 0, -1000 * 1000 );

			//Assert.IsTrue(Math.Abs(failureDomainPoint.FailureDomainPoint.Point.X) - )
		}
	}
}
