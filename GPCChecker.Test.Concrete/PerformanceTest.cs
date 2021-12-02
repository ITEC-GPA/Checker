using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using GPC.TestUtilities;
using GPC.Utilities.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
	[TestClass]
	public class PerformanceTest : ConcreteTestBase
	{

		[TestMethod]
		public void IntegrateSectionStressTest()
		{
			RebarSectionCircular rebarPhi20 = new RebarSectionCircular(20, new RebarMaterial("", 450));

			ConcreteSectionRectangular concreteSectionRectangular = new ConcreteSectionRectangular(500, 300,
				new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));

			concreteSectionRectangular.AddRebar(new ReinforcedConcreteRebar(rebarPhi20, new Point3d(50, 50, 0)));

			List<ResultBeamForces> forces = new List<ResultBeamForces>();

			for (int i = 0; i < 10000; i++)
			{
				forces.Add(new ResultBeamForces(10, 20, 30, 40, 50, 60, new CoordinateSystem(concreteSectionRectangular.Centroid, Vector3d.XAxis, Vector3d.YAxis)));
			}


			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(concreteSectionRectangular, forces.ToArray(), null);
			SectionSolverModelCode2010Test sectionSolverModelCode2010Test = new SectionSolverModelCode2010Test(concreteSectionRectangular, new StandardEN1992p11());

			var slsResult = sectionSolverModelCode2010Test.GetStressAnalysisResults(forces.ToArray(), new Point2d());

			Action ac0 = new Action(() =>
				{
					sectionSolverModelCode2010Test.IntegrateSectionStressTest(slsResult.Select(i => i.StrainPlane).First());
				}
			);
						
			var bb0 = MeasureTime.FunctionExecutionTime(20, ac0, true); ;

			Console.WriteLine(bb0);
		}



		[TestMethod]
		public void FailureDomainTest()
		{
			var section = GetRectangularSection4Rebars();

			SectionSolverModelCode2010 solver = new SectionSolverModelCode2010(section, new StandardEN1992p11());

			Action ac0 = new Action(() =>
			{
				solver.GetFailurePlasticDomainResults();
			});

			var bb0 = MeasureTime.FunctionExecutionTime(20, ac0, true); ;

			Console.WriteLine(bb0);
		}


	}
}
