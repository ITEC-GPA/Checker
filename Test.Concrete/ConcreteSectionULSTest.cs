using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver;
using GPC.Checkers.ReinforcedConcrete.Results;

namespace ConcreteTests
{
	[TestClass]
	public class ConcreteSectionULSTest
	{
        [TestMethod]
        public void StrainPlaneTest()
        {
            double rebarDiameter = 8;

            // sezione rettangolare 300x500
            Shape shape = new Shape(new Polygon3d(new Point3d[] {   new Point3d(0, 0, 0),
                                                                    new Point3d(300, 0, 0),
                                                                    new Point3d(300, 500, 0),
                                                                    new Point3d(0, 500, 0), }));

            ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.StressStrainDiagrams.Bilinear));
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, RebarMaterial.B450C);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
                                                                                new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
                                                                                new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
                                                                                new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);

            ConcreteSectionSolverEN1992 solver = new ConcreteSectionSolverEN1992(section, new GPC.Model.Standards.StandardEN1992p11());

            int[] subd = new int[] { 3, 1, 3, 1, 1, 1 };
            FailureDomain failureDomain = solver.CalculateFailureDomain(4, subd);
            Point3d[] points = solver.ExportToGmsh(failureDomain);

            for (int i = 0; i < failureDomain.DomainPoints[0].Length; i++)
                for (int j = 0; j < failureDomain.DomainPoints[1].Length; j++)
                    Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd)}, " +
                                    $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd)}, " +
                                    $"{Math.Round(failureDomain.DomainPoints[i][j].NRd)}");
            

        }
	}
}
