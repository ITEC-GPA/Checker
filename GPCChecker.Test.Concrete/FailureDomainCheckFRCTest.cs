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
	public class FailureDomainCheckFRCTest : ConcreteTestBase
	{

		[TestMethod]
		public void RectangularSectionTest1()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, 
				ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[j], standard, GetLocalCoordinateSystem(section)), $"Force {j} fail");
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
				ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[i], standard, GetLocalCoordinateSystem(section)), $"Force {i} fail");
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
				ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[i], standard, GetLocalCoordinateSystem(section), 
					0.005, new double[] { 1.0 }), $"Force {i} fail");
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
				ConcreteMaterialModelCode2010.C30_37_10);

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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[j], standard, GetLocalCoordinateSystem(section)), $"Force {j} fail");
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

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 
				numberOfSideRebar, ConcreteMaterialModelCode2010.C30_37_10);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-1000 * 1000, 0, 0, 0, -100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-200 * 1000, 0, 0, 0, 40 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-400 * 1000, 0, 0, 0, 40 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-200 * 1000, 0, 0, 0, -50 * 1000000, -100 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(0 * 1000, 0, 0, 0, -200 * 1000000, 0, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-1000 * 1000, 0, 0, 0, 150 * 1000000, 0, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-800 * 1000, 0, 0, 0, 120 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section))
			};

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[i], standard, GetLocalCoordinateSystem(section)), $"Force {i} fail");
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

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 
				numberOfSideRebar, ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[i], standard, GetLocalCoordinateSystem(section)), $"Force {i} fail");
			}
		}

		[TestMethod]
		public void RectangularSectionTest7()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, 
				ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[i], standard, GetLocalCoordinateSystem(section)), $"Force {i} fail");
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

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover,
				numberOfSideRebars, ConcreteMaterialModelCode2010.C30_37_10);

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				//new ResultBeamForces(-2000 * 1000, 0, 0, 0, -300 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-3000 * 1000, 0, 0, 0, -200 * 1000000, -120 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -200 * 1000000, -40 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(0 * 1000, 0, 0, 0, -400 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1000 * 1000, 0, 0, 0, -250 * 1000000, 0, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-1800 * 1000, 0, 0, 0, -120 * 1000000, -120 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[i], standard, GetLocalCoordinateSystem(section)), $"Force {i} fail");
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

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, concreteCover, 
				numberOfSideRebars, ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[i], standard, GetLocalCoordinateSystem(section), 0.01),
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

			ReinforcedConcreteSection section = GetRectangularSection4SideRebars(width, height, rebarDiameter, concreteCover, numberOfSideRebars,
				numberOfSideRebars, ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[i], standard, GetLocalCoordinateSystem(section), 0.01), $"Force {i} fail");
			}

		}

		[TestMethod]
		public void RectangularSectionTest11()
		{
			double rebarDiameter = 6;
			double height = 80;
			double width = 1000;
			double copriferro = 25;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialModelCode2010($"FCM {45}-3.5", 45, 
				ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle,
				0.45 * 0.92, 0.3 * 0.76, 0.00003, 0.02, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, ConcreteMaterial.ConcreteTypes.FRC));
			RebarSectionCircular rebarSection = new RebarSectionCircular(rebarDiameter, SteelMaterial.B500C);
			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);

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
				new ResultBeamForces(0 * 1000, 0, 0, 0, 2 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int i = 0; i < forces.Length; i++)
			{
				Console.WriteLine($"Force {i}");
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[i], standard, GetLocalCoordinateSystem(section), 0.01), $"Force {i} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest1()
		{
			double rebarDiameter = 20;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, width, rebarDiameter, concreteCover, 
				ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[j], standard, GetLocalCoordinateSystem(section)), $"Force {j} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest2()
		{
			double rebarDiameter = 20;
			double width = 300;
			double concreteCover = 50;

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, width, rebarDiameter, concreteCover, ConcreteMaterialModelCode2010.C30_37_10);
			StandardEN1992p11 standard = new StandardEN1992p11();

			ResultBeamForces[] forces = new ResultBeamForces[]
			{
				//new ResultBeamForces(-100 * 1000, 0, 0, 0, 20 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-300 * 1000, 0, 0, 0, 20 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				//new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-800 * 1000, 0, 0, 0, 20 * 1000000, -10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-100 * 1000, 0, 0, 0, -20 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-300 * 1000, 0, 0, 0, -20 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
				new ResultBeamForces(-500 * 1000, 0, 0, 0, -20 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(section)),
			};

			for (int j = 0; j < forces.Length; j++)
			{
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[j], standard, GetLocalCoordinateSystem(section)), $"Force {j} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest3()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, 
				numberOfSideRebars, ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[j], standard, GetLocalCoordinateSystem(section)), $"Force {j} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest4()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover, 
				numberOfSideRebars, ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[j], standard, GetLocalCoordinateSystem(section)), $"Force {j} fail");
			}
		}

		[TestMethod]
		public void SquareSectionTest5()
		{
			double rebarDiameter = 26;
			double height = 500;
			double concreteCover = 50;
			int numberOfSideRebars = 5;

			ReinforcedConcreteSection section = GetRectangularSection2SideRebars(height, height, rebarDiameter, concreteCover,
				numberOfSideRebars, ConcreteMaterialModelCode2010.C30_37_10);
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
				Assert.IsTrue(CommonAssertDomainPointMethodFRC(section, forces[j], standard, GetLocalCoordinateSystem(section)), $"Force {j} fail");
			}
		}

	}
}
