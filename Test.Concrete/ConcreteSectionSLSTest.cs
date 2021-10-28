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
			ResultBeamForces forces = new ResultBeamForces(0, 0, 0, 0, 10 * 1000000, 10 * 1000000, CoordinateSystem.Global);

			ConcreteSectionSolverSLSModelCode2010 solver = new ConcreteSectionSolverSLSModelCode2010(section, forces, standard);
			StrainPlane plane = solver.CalculateStrainPlane();

		}
	}
}
