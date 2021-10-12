using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver;

namespace ConcreteTests
{
	[TestClass]
	public class ConcreteSectionULSTest
	{
		[TestMethod]
		public void StrainPlaneTest()
		{
            double rebarDiameter = 18;

            // sezione rettangolare 300x500
            Shape shape = new Shape(new Polygon3d(new Point3d[] {   new Point3d(0, 0, 0),
                                                                    new Point3d(300, 0, 0),
                                                                    new Point3d(300, 500, 0),
                                                                    new Point3d(0, 500, 0), }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, RebarMaterial.B450C);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
                                                                                new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
                                                                                new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
                                                                                new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};
            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);

            ConcreteSectionSolverEN1992 solver = new ConcreteSectionSolverEN1992(section, new GPC.Model.Standards.StandardEN1992p11());

			ConcreteSectionSolver.StrainPlane[] planes = solver.CalculateAllDesignStrainPlanes(0.0, 2);

        }
	}
}
