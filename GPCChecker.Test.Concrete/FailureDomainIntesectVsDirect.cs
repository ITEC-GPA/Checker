using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Results;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static GPC.Checkers.Concrete.Results.FailureDomain;

namespace ConcreteTests
{
    [TestClass]
    public class FailureDomainIntesectVsDirect : ConcreteTestBase
    {
        private static void CompareDirectAndIntersectmethods(SectionChecker sectionChecker, ResultBeamForces[] forces, double ratioDifference)
        {
            var intersectDomPoint = new List<FailureDomainPoint>();
            var intersectWR = new List<double>();
            var directDomPoint = new List<FailureDomainPoint>();
            var directWR = new List<double>();
            var wrDifference = new List<double>();
            double Mscale = 1000000.0;
            double Nscale = 1000.0;
            // Calculate domain mesh for intersect method.
            var plasticDomainResult = sectionChecker.GetPlasticFailureDomainResult();
            var plastiDomainMesh = plasticDomainResult.Domain.GetMesh(plasticDomainResult.Domain, out Dictionary<MeshVertex, FailureDomainPoint> vertexToDomainPoint);

            foreach (var appliedForce in forces)
            {
                // ***** Intersect method
                var failIntersect = new FailureDomainPoint(plastiDomainMesh, appliedForce, vertexToDomainPoint, sectionChecker, SectionSolver.FailureDomainTypes.Plastic, 10);
                intersectDomPoint.Add(failIntersect);
                intersectWR.Add(failIntersect.WorkingRatio);

                // ***** Direct/iterative method
                var failDirect = sectionChecker.CalculatePlasticFailureDomainPoint(appliedForce);
                failDirect.CalculateWorkingRatio(sectionChecker.SectionCheckerOptions.FailureAnalysisType, appliedForce, Mscale, Nscale);
                directDomPoint.Add(failDirect);
                directWR.Add(failDirect.WorkingRatio);

                // Ratio difference
                wrDifference.Add(failIntersect.WorkingRatio - failDirect.WorkingRatio);
            }
            Assert.AreEqual(0.0, wrDifference.Max(r => Math.Abs(r)), ratioDifference);
        }

        [TestMethod]
        public void IntesectVsDirect01()
        {
            // Geometry, material and section.
            var section = GetRectangularSection4Rebars(300, 500, 18, 50, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
            var standard = new StandardNTC2018Concrete();
            var cs = GetLocalCoordinateSystem(section);
            var sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(cs, SectionSolver.FailureAnalysisTypes.ConstantN);

            // Code, solver and checker.
            var sectionChecker = GetSectionCheckerModelCode2010(section, standard, false);
            sectionChecker.SectionCheckerOptionsModelCode2010.FailureAnalysisType = SectionSolver.FailureAnalysisTypes.ConstantN;
            sectionChecker.SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem = cs;

            // External forces
            var ec3domainPoints = new List<Point3d>()
            {
                new Point3d(165577054.225, 0, 0),
                new Point3d(165577054.225, 0, -71335.825),
                new Point3d(165577054.225, 0, -142671.65),
                new Point3d(165577054.225, 0, -214007.475),
                new Point3d(163661358.350391, 0, -285343.3),
                new Point3d(153432523.453492, 0, -356679.125),
                new Point3d(165577054.225, 16557705.4225, 0),
                new Point3d(165577054.225, 16557705.4225, -71335.825),
                new Point3d(165577054.225, 16557705.4225, -142671.65),
                new Point3d(165577054.225, 16557705.4225, -214007.475),
                new Point3d(163661358.350391, 16557705.4225, -285343.3),
                new Point3d(153432523.453492, 16557705.4225, -356679.125),
            };
            var forces = ec3domainPoints.Select(p => new ResultBeamForces(p.Z, 0, 0, 0, p.X, p.Y, cs)).ToArray();

            CompareDirectAndIntersectmethods(sectionChecker, forces, 0.01);
        }
    }
}
