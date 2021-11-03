using GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using GPC.Checkers.ReinforcedConcrete.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ConcreteTests
{
	[TestClass]
	public class ConcreteSectionSLSTest
	{
		protected virtual void CalculateAdimensionalForces(IConcreteSection section, ResultBeamForces forces, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY)
		{
			BoundingBox3d bBox = section.Shape.GetBoundingBox();
			double h = bBox.Size.Y;
			double b = bBox.Size.X;

			adimAxialForce = forces.N / (b * h * section.ConcreteMaterial.Fck);
			adimBendingMomentX = forces.M1 / (b * h * h * section.ConcreteMaterial.Fck);
			adimBendingMomentY = forces.M2 / (b * b * h * section.ConcreteMaterial.Fck);
		}

		protected virtual void CalculateAdimensionalForces(IConcreteSection section, double N, double Mx, double My, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY)
		{
			BoundingBox3d bBox = section.Shape.GetBoundingBox();
			double h = bBox.Size.Y;
			double b = bBox.Size.X;

			adimAxialForce = N / (b * h * section.ConcreteMaterial.Fck);
			adimBendingMomentX = Mx / (b * h * h * section.ConcreteMaterial.Fck);
			adimBendingMomentY = My / (b * b * h * section.ConcreteMaterial.Fck);
		}

		protected bool CommonAssert(IConcreteSection section, ResultBeamForces forces, StrainPlane strainPlane, StandardModelCode2010 standard, double tolerance = 1e-5)
		{
			CalculateAdimensionalForces(section, forces, out double adimExternalAxialForce, out double adimExternalendingMomentX, out double adimExternalBendingMomentY);
			ConcreteSectionSolverSLSModelCode2010 solver = new ConcreteSectionSolverSLSModelCode2010(section, forces, standard);

			solver.CalculateForces(strainPlane, out double N, out double Mx, out double My);
			CalculateAdimensionalForces(section, N, Mx, My, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY);
			if (Math.Abs(adimAxialForce - adimExternalAxialForce) > tolerance ||
				Math.Abs(adimBendingMomentX - adimExternalendingMomentX) > tolerance ||
				Math.Abs(adimBendingMomentY - adimExternalBendingMomentY) > tolerance)
				return false;

			return true;
		}

		[TestMethod]
		public void SezioneRettangolareTest1()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape shape = new Shape(new Polygon3d(new Point3d[] {   new Point3d(0, 0, 0),
																	new Point3d(300, 0, 0),
																	new Point3d(300, 500, 0),
																	new Point3d(0, 500, 0), }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(100 *  1000, 0, 0, 0, 10 * 1000000, 0 * 1000000, CoordinateSystem.Global);

			ConcreteSectionSolverSLSModelCode2010 solver = new ConcreteSectionSolverSLSModelCode2010(section, forces, standard);
			StrainPlane strainPlane = solver.CalculateStrainPlane();

			Assert.IsTrue(CommonAssert(section, forces, strainPlane, standard));
		}
	}
}
