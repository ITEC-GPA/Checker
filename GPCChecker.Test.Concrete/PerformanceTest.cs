using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using GPC.Utilities.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConcreteTests
{
    [TestClass]
    public class PerformanceTest : ConcreteTestBase
    {
        [TestMethod]
        public void IntegrateSectionStressTest()
        {
            ReinforcedConcreteSection concreteSectionRectangular = GetRectangularSection4Rebars();

            List<ResultBeamForces> forces = new List<ResultBeamForces>();

            for (int i = 0; i < 10; i++)
            {
                forces.Add(new ResultBeamForces(-10 * 1000, 20, 30, 40, 15 * 1000000, 5 * 1000000, GetLocalCoordinateSystem(concreteSectionRectangular)));
            }

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(concreteSectionRectangular, forces.ToArray(), null);
			var sectionSolverModelCode2010Test = new SectionSolverModelCode2010Test(concreteSectionRectangular, new StandardEN1992p11(), concreteSectionRectangular.Centroid);

            GPC.Checkers.Concrete.Results.StressAnalysisResult[] slsResult = sectionSolverModelCode2010Test.GetStressAnalysisResults(forces.ToArray(),
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(concreteSectionRectangular), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic));

            Action ac0 = new Action(() =>
                {
                    sectionSolverModelCode2010Test.IntegrateSectionStressTest(slsResult.Select(i => i.StrainPlane).First());
                });

            double bb0 = MeasureTime.FunctionExecutionTime(20, ac0, true); ;

            Console.WriteLine(bb0);
        }

		[TestMethod]
		public void FailureDomainTest()
		{
			var section = GetRectangularSection4Rebars();
			SectionSolverModelCode2010 solver = new SectionSolverModelCode2010(section, new StandardEN1992p11(), section.Centroid);

            Action ac0 = new Action(() =>
            {
                solver.GetPlasticFailureDomainResult(new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic));
            });

            var bb0 = MeasureTime.FunctionExecutionTime(10, ac0, true); ;

            Console.WriteLine(bb0);
        }

        [TestMethod]
        public void FailureDomainCircularSection2()
        {
            ReinforcedConcreteSection section = GetCircularSection();
            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section);

            SectionCheckerModelCode2010 checker = new SectionCheckerModelCode2010(sectionCheckerAttribute, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(), new StandardModelCode2010());
            checker.SectionSolver.TetaDiscretization = 16;

            Action ac0 = new Action(() =>
            {
                checker.GetPlasticFailureDomainResult();
            });

            double bb0 = MeasureTime.FunctionExecutionTime(2, ac0, true); ;

            Console.WriteLine(bb0);
        }
    }
}
