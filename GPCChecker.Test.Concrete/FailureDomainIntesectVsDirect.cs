using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Standards;
using GPC.Utilities.Maths;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static GPC.Checkers.Concrete.Checkers.SectionCheckerACI318;

namespace ConcreteTests
{
    [TestClass]
    public class FailureDomainIntesectVsDirect : ConcreteTestBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sectionChecker"></param>
        /// <param name="forces">Forces to check.</param>
        /// <param name="referenceWorkingRatio">Correct reference values for working ratio.</param>
        /// <param name="maxIntersectWRError">Maximum error admitted for the intersection method.</param>
        /// <param name="maxDirectWRError">Maximum error admitted for the iterative/direct method.</param>
        /// <param name="referenceForces">Force at the domain point from which the ratio is determined, correct reference values.</param>
        /// <param name="maxIntersectDomPointError"></param>
        /// <param name="maxDirectDomPointError"></param>
        private static void CompareDirectAndIntersectmethods(SectionChecker sectionChecker, ResultBeamForces[] forces, List<double> referenceWorkingRatio, double maxIntersectWRError, double maxDirectWRError, List<ForceTuple> referenceForces/*, double maxIntersectDomPointError, double maxDirectDomPointError*/)
        {
            var intersectDomPoint = new List<FailureDomain.FailureDomainPoint>();
            var intersectWR = new List<double>();
            var intersectDomForces = new List<ForceTuple>();

            var directDomPoint = new List<FailureDomain.FailureDomainPoint>();
            var directWR = new List<double>();
            var directDomForces = new List<ForceTuple>();

            double Mscale = 1000000.0; // Convert in kN m.
            double Nscale = 1000.0; // Convert in kN.
            // Calculate domain mesh for intersect method.
            //sectionChecker.SectionSolver.SetTetaDiscretization(64);
            var plasticDomainResult = sectionChecker.GetPlasticFailureDomainResult();
            var plastiDomainMesh = plasticDomainResult.Domain.GetMesh(plasticDomainResult.Domain, out Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint);
            // Get point of section.
            var secLines = sectionChecker.SectionSolver.ConcreteSection.SectionShape.Shape.Fill.Explode();
            var secPoints = secLines.Select(l => l.Start).ToArray();
            // Solver
            var solverTestModelCode = sectionChecker.SectionSolver as SectionSolverModelCode2010;
            var solverTestACI = sectionChecker.SectionSolver as SectionSolverACI318;

            foreach (var appliedForce in forces)
            {
                // ***** Intersect method - ratio
                var failIntersect = sectionChecker.SectionSolver.CalculateDomainPoint(appliedForce, plastiDomainMesh, vertexToDomainPoint, sectionChecker.SectionCheckerOptions);
                intersectDomPoint.Add(failIntersect);
                intersectWR.Add(failIntersect.WorkingRatio);
                if (solverTestACI is null)
                    intersectDomForces.Add(solverTestModelCode.CalculateForceResultantForDomain(failIntersect.StrainPlane));
                else
                    intersectDomForces.Add(solverTestACI.CalculateForceResultantForDomain(failIntersect.StrainPlane));

                var epsIntersect = new List<double>();
                foreach (var p in secPoints)
                    epsIntersect.Add(failIntersect.StrainPlane.GetStrain(p));

                // ***** Direct/iterative method - ratio
                FailureDomain.FailureDomainPoint failDirect;
                try
                {
                    failDirect = sectionChecker.CalculatePlasticFailureDomainPoint(appliedForce);
                }
                catch
                {
                    failDirect = null;
                }
                failDirect?.CalculateWorkingRatio(sectionChecker.SectionCheckerOptions.FailureAnalysisType, appliedForce, Mscale, Nscale);
                directDomPoint.Add(failDirect);
                directWR.Add(failDirect?.WorkingRatio ?? double.PositiveInfinity);
                if (failDirect != null)
                {
                    if (solverTestACI is null)
                        directDomForces.Add(solverTestModelCode.CalculateForceResultantForDomain(failDirect.StrainPlane));
                    else
                        directDomForces.Add(solverTestACI.CalculateForceResultantForDomain(failDirect.StrainPlane));
                }
                else
                    directDomForces.Add(new ForceTuple());

                if (failDirect != null)
                {
                    var epsDirect = new List<double>();
                    foreach (var p in secPoints)
                        epsDirect.Add(failDirect.StrainPlane.GetStrain(p));
                }

                // Strain difference
                //for (int i = 0; i < epsIntersect.Count; i++)
                //{
                //    Assert.AreEqual(epsIntersect[i], epsDirect[i], 5e-4);
                //}
            }
            var intersectWRrelativeError = intersectWR.Select((val, index) => Error.CalcRelativeError(val, referenceWorkingRatio[index])).ToArray();
            var directWRrelativeError = directWR.Select((val, index) => Error.CalcRelativeError(val, referenceWorkingRatio[index])).ToArray();

            var maxIntersectWRrelativeError = intersectWRrelativeError.Max(value => Math.Abs(value));
            var maxDirectWRrelativeError = directWRrelativeError.Max(value => Math.Abs(value));

            var intersectDomPointAbsoluteError = new List<double>();
            for (int i = 0; i < intersectDomForces.Count; i++)
            {
                var intersectDomForcePoint = new Point3d(intersectDomForces[i]);
                intersectDomForcePoint.X /= Mscale;
                intersectDomForcePoint.Y /= Mscale;
                intersectDomForcePoint.Z /= Nscale;

                intersectDomPointAbsoluteError.Add(intersectDomForcePoint.DistanceTo(new Point3d(referenceForces[i])));
            }
            var directDomPointAbsoluteError = new List<double>();
            for (int i = 0; i < directDomForces.Count; i++)
            {
                var directDomForcePoint = new Point3d(directDomForces[i]);
                directDomForcePoint.X /= Mscale;
                directDomForcePoint.Y /= Mscale;
                directDomForcePoint.Z /= Nscale;

                directDomPointAbsoluteError.Add(directDomForcePoint.DistanceTo(new Point3d(referenceForces[i])));
            }

            var maxIntersectDomPointAbsoluteError = intersectDomPointAbsoluteError.Max();
            var maxDirectDomPointAbsoluteError = directDomPointAbsoluteError.Max();

            Assert.AreEqual(0.0, maxIntersectWRrelativeError, maxIntersectWRError);
            Assert.AreEqual(0.0, maxDirectWRrelativeError, maxDirectWRError);
            //Assert.AreEqual(0.0, maxIntersectDomPointAbsoluteError, maxIntersectWRError);
            //Assert.AreEqual(0.0, maxDirectDomPointAbsoluteError, maxDirectWRError);
        }

        private void MakeSimpleSection01(SectionSolver.FailureAnalysisTypes ratioMode, out CoordinateSystem cs, out SectionCheckerModelCode2010 sectionChecker, out ResultBeamForces[] forces)
        {
            // Geometry, material and section.
            var section = GetRectangularSection4Rebars(300, 500, 20, 50, ConcreteMaterialEN1992Data.C25_30, SteelMaterialEN1992Data.B450C);
            var standard = new StandardNTC2018Concrete();
            cs = GetLocalCoordinateSystem(section);
            var sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(cs, ratioMode, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);

            // Code, solver and checker.
            bool considerTensileConcrete = false;
            int id = -1;
            StandardEN1993p11 standardStructuralSteel = null;
            var sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            var solver = new SectionSolverModelCode2010(section, sectionOptions, standard, section.Centroid, considerTensileConcrete, id, standardStructuralSteel);
            sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, solver, id);
            sectionChecker.SectionCheckerOptionsModelCode2010.FailureAnalysisType = ratioMode;

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
            var loccs = cs;
            forces = ec3domainPoints.Select(p => new ResultBeamForces(p.Z, 0, 0, 0, p.X, p.Y, loccs)).ToArray();
        }

        private void MakeSimpleSection02(SectionSolver.FailureAnalysisTypes ratioMode, out CoordinateSystem cs, out SectionCheckerACI318 sectionChecker, out ResultBeamForces[] forces)
        {
            // Geometry, material and section.
            var section = GetRectangularSection4Rebars(300, 500, 22, 50,
                new ConcreteMaterialACI318("fc' 30Mpa", 30, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle),
                new SteelMaterialACI318("Grade 60", 200000, 420, 420, 0.10, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar));
            var standard = new StandardACI318p19();
            cs = GetLocalCoordinateSystem(section);
            var sectionOptions = new SectionCheckerACI318.SectionOptionsStandardACI318(cs, ratioMode, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);

            // Code, solver and checker.
            bool considerTensileConcrete = false;
            int id = -1;
            StandardEN1993p11 standardStructuralSteel = null;
            var sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            var solver = new SectionSolverACI318(section, sectionOptions, standard, considerTensileConcrete, section.Centroid, false, id, standardStructuralSteel);
            sectionChecker = new SectionCheckerACI318(sectionCheckerAttribute, sectionOptions, standard, solver, id);
            sectionChecker.SectionCheckerOptionsACI318.FailureAnalysisType = ratioMode;

            // External forces, Mx My N
            var ec3domainPoints = new List<Point3d>()
            {
                new Point3d(80.0, -32.0, 0.0),
                new Point3d(90.0, -32.0, -250.0),
                new Point3d(100.0, -32.00, -500.0),
                new Point3d(110.0, -32.0, -750.0),
                new Point3d(100.0, -32.0, -1000.0),
                new Point3d(90.0, -32.0, -1250.0),
                new Point3d(80.0, 40.0, 0.0),
                new Point3d(90.0, 40.0, -250.0),
                new Point3d(100.0, 40.0, -500.0),
                new Point3d(110.0, 40.0, -750.0),
                new Point3d(100.0, 40.0, -1000.0),
                new Point3d(90.0, 40.0, -1250.0),
            };
            foreach (var e in ec3domainPoints)
            {
                e.X *= 1000000.0;
                e.Y *= 1000000.0;
                e.Z *= 1000.0;
            }

            var loccs = cs;
            forces = ec3domainPoints.Select(p => new ResultBeamForces(p.Z, 0, 0, 0, p.X, p.Y, loccs)).ToArray();
        }

        // SectionSolver.FailureAnalysisTypes.ConstantEccentricity
        [TestMethod]
        public void IntersectVsDirect01()
        {
            MakeSimpleSection01(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, out CoordinateSystem cs, out SectionCheckerModelCode2010 sectionChecker, out ResultBeamForces[] forces);
            sectionChecker.SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem = cs;

            // Workiong ratio from SBeton.
            var referenceWR = new List<double>()
            {
                159.98 / 100.0,
                146.18 / 100.0,
                132.55 / 100.0,
                119.19 / 100.0,
                104.58 / 100.0,
                84.3 / 100.0,
                160.54 / 100.0,
                146.86 / 100.0,
                133.45 / 100.0,
                120.58 / 100.0,
                107.03 / 100.0,
                89.37 / 100.0
            };

            // Domain point force from SBeton.
            var referenceForces = new List<ForceTuple>()
            {
                new ForceTuple( 0, 103.5, 0 ),
                new ForceTuple( -48.8, 113.27, 0 ),
                new ForceTuple( -107.64, 124.92, 0 ),
                new ForceTuple( -179.55, 138.91, 0 ),
                new ForceTuple( -272.87, 156.48, 0.01 ),
                new ForceTuple( -423.1, 182, 0 ),
                new ForceTuple( 0, 103.14, 10.32 ),
                new ForceTuple( -48.57, 112.75, 11.28 ),
                new ForceTuple( -106.91, 124.07, 12.41 ),
                new ForceTuple( -177.49, 137.33, 13.73 ),
                new ForceTuple( -266.59, 152.91, 15.47 ),
                new ForceTuple( -399.11, 171.68, 18.53 )
            };

            CompareDirectAndIntersectmethods(sectionChecker, forces, referenceWR, 0.005, 0.005, referenceForces);
        }

        // SectionSolver.FailureAnalysisTypes.ConstantN
        [TestMethod]
        public void IntersectVsDirect02()
        {
            MakeSimpleSection01(SectionSolver.FailureAnalysisTypes.ConstantN, out CoordinateSystem cs, out SectionCheckerModelCode2010 sectionChecker, out ResultBeamForces[] forces);
            sectionChecker.SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem = cs;

            // Workiong ratio from SBeton.
            var referenceWR = new List<double>()
            {
                159.98 / 100.0,
                140.62 / 100.0,
                125.65 / 100.0,
                113.81 / 100.0,
                103.09 / 100.0,
                89.48 / 100.0,
                160.54 / 100.0,
                141.28 / 100.0,
                126.54 / 100.0,
                115.03 / 100.0,
                105.00 / 100.0,
                92.45 / 100.0
            };

            // Domain point force from SBeton.
            var referenceForces = new List<ForceTuple>()
            {
                new ForceTuple( 0.00, 103.50, 0.00 ),
                new ForceTuple( -71.34, 117.75, 0.00 ),
                new ForceTuple( -142.67, 131.78, 0.00 ),
                new ForceTuple( -214.01, 145.49, -4.82E-006 ),
                new ForceTuple( -285.34, 158.76, 0.00 ),
                new ForceTuple( -356.68, 171.47, 2.83E-006 ),
                new ForceTuple( 0.00, 103.14, 10.32 ),
                new ForceTuple( -71.34, 117.20, 11.72 ),
                new ForceTuple( -142.67, 130.86, 13.09 ),
                new ForceTuple( -214.01, 143.95, 14.40 ),
                new ForceTuple( -285.34, 155.87, 15.77 ),
                new ForceTuple( -356.68, 165.97, 17.91 ),
            };

            CompareDirectAndIntersectmethods(sectionChecker, forces, referenceWR, 0.005, 0.0005, referenceForces);
        }

        // SectionSolver.FailureAnalysisTypes.ConstantNMy
        [TestMethod]
        public void IntersectVsDirect03()
        {
            MakeSimpleSection01(SectionSolver.FailureAnalysisTypes.ConstantNMy, out CoordinateSystem cs, out SectionCheckerModelCode2010 sectionChecker, out ResultBeamForces[] forces);
            sectionChecker.SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem = cs;

            // Workiong ratio from SBeton.
            var referenceWR = new List<double>()
            {
                159.98 / 100.0,
                140.62 / 100.0,
                125.65 / 100.0,
                113.81 / 100.0,
                103.09 / 100.0,
                89.48 / 100.0,
                161.51 / 100.0,
                142.03 / 100.0,
                127.09 / 100.0,
                115.40 / 100.0,
                105.19 / 100.0,
                92.10 / 100.0
            };

            // Domain point force from SBeton.
            var referenceForces = new List<ForceTuple>()
            {
                new ForceTuple( 0.00, 103.50, 0.00 ),
                new ForceTuple( -71.34, 117.75, 0.00 ),
                new ForceTuple( -142.67, 131.78, 0.00 ),
                new ForceTuple( -214.01, 145.49, 0.00 ),
                new ForceTuple( -285.34, 158.76, 0.00 ),
                new ForceTuple( -356.68, 171.47, 0.00 ),
                new ForceTuple( 0.00, 102.52, 16.56 ),
                new ForceTuple( -71.34, 116.58, 16.56 ),
                new ForceTuple( -142.67, 130.28, 16.56 ),
                new ForceTuple( -214.01, 143.48, 16.56 ),
                new ForceTuple( -285.34, 155.58, 16.56 ),
                new ForceTuple( -356.68, 166.59, 16.56 ),
            };

            CompareDirectAndIntersectmethods(sectionChecker, forces, referenceWR, 0.005, 0.0005, referenceForces);
        }

        // SectionSolver.FailureAnalysisTypes.ConstantN
        [TestMethod]
        public void IntersectVsDirect04()
        {
            MakeSimpleSection02(SectionSolver.FailureAnalysisTypes.ConstantN, out CoordinateSystem cs, out SectionCheckerACI318 sectionChecker, out ResultBeamForces[] forces);
            sectionChecker.SectionCheckerOptionsACI318.ForceReferenceCoordinateSystem = cs;

            // Workiong ratio from SBeton.
            var referenceWR = new List<double>()
            {
                69.10 / 100.0,
                57.93 / 100.0,
                59.49 / 100.0,
                63.70 / 100.0,
                60.90 / 100.0,
                57.97 / 100.0,
                72.37 / 100.0,
                64.02 / 100.0,
                65.37 / 100.0,
                69.12 / 100.0,
                65.93 / 100.0,
                63.16 / 100.0
            };

            // Domain point force from SBeton.
            var referenceForces = new List<ForceTuple>()
            {
                new ForceTuple( 0.00, 115.77, -46.31 ),
                new ForceTuple( -250.00, 155.36, -55.24 ),
                new ForceTuple( -500.00, 168.09, -53.79 ),
                new ForceTuple( -750.00, 172.69, -50.24 ),
                new ForceTuple( -1000.00, 164.21, -52.55 ),
                new ForceTuple( -1250.00, 155.26, -55.20 ),
                new ForceTuple( 0.00, 110.55, 55.27 ),
                new ForceTuple( -250.00, 140.58, 62.48 ),
                new ForceTuple( -500.00, 152.98, 61.19 ),
                new ForceTuple( -750.00, 159.15, 57.87 ),
                new ForceTuple( -1000.00, 151.67, 60.67 ),
                new ForceTuple( -1250.00, 142.50, 63.33 )
            };

            CompareDirectAndIntersectmethods(sectionChecker, forces, referenceWR, 0.05, 0.02, referenceForces);
        }

        // SectionSolver.FailureAnalysisTypes.ConstantEccentricity
        [TestMethod]
        public void IntersectVsDirect05()
        {
            MakeSimpleSection02(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, out CoordinateSystem cs, out SectionCheckerACI318 sectionChecker, out ResultBeamForces[] forces);
            sectionChecker.SectionCheckerOptionsACI318.ForceReferenceCoordinateSystem = cs;

            // Workiong ratio from SBeton.
            var referenceWR = new List<double>()
            {
                69.10 / 100.0,
                56.19 / 100.0,
                60.54 / 100.0,
                65.62 / 100.0,
                65.89 / 100.0,
                69.20 / 100.0,
                72.37 / 100.0,
                62.54 / 100.0,
                65.40 / 100.0,
                70.18 / 100.0,
                69.66 / 100.0,
                72.37 / 100.0
            };

            // Domain point force from SBeton.
            var referenceForces = new List<ForceTuple>()
            {
                new ForceTuple( 0.00, 115.77, -46.31 ),
                new ForceTuple( -444.91, 160.17, -56.95 ),
                new ForceTuple( -825.90, 165.18, -52.86 ),
                new ForceTuple( -1142.92, 167.63, -48.76 ),
                new ForceTuple( -1517.78, 151.78, -48.57 ),
                new ForceTuple( -1806.42, 130.06, -46.25 ),
                new ForceTuple( 0.00, 110.55, 55.27 ),
                new ForceTuple( -399.73, 143.89, 63.96 ),
                new ForceTuple( -764.53, 152.90, 61.16 ),
                new ForceTuple( -1068.64, 156.73, 56.99 ),
                new ForceTuple( -1435.52, 143.55, 57.42 ),
                new ForceTuple( -1727.19, 124.35, 55.28 )
            };

            CompareDirectAndIntersectmethods(sectionChecker, forces, referenceWR, 0.04, double.PositiveInfinity, referenceForces);
        }

        // SectionSolver.FailureAnalysisTypes.ConstantNMx
        [TestMethod]
        public void IntersectVsDirect06()
        {
            MakeSimpleSection02(SectionSolver.FailureAnalysisTypes.ConstantNMx, out CoordinateSystem cs, out SectionCheckerACI318 sectionChecker, out ResultBeamForces[] forces);
            sectionChecker.SectionCheckerOptionsACI318.ForceReferenceCoordinateSystem = cs;

            // Workiong ratio from SBeton.
            var referenceWR = new List<double>()
            {
                49.02 / 100.0,
                36.13 / 100.0,
                36.29 / 100.0,
                37.83 / 100.0,
                35.97 / 100.0,
                35.08 / 100.0,
                61.28 / 100.0,
                45.16 / 100.0,
                45.36 / 100.0,
                47.29 / 100.0,
                44.96 / 100.0,
                43.85 / 100.0
            };

            // Domain point force from SBeton.
            var referenceForces = new List<ForceTuple>()
            {
                new ForceTuple( 0.00, 80.00, -65.28 ),
                new ForceTuple( -250.00, 90.00, -88.57 ),
                new ForceTuple( -500.00, 100.00, -88.17 ),
                new ForceTuple( -750.00, 110.00, -84.58 ),
                new ForceTuple( -1000.00, 100.00, -88.96 ),
                new ForceTuple( -1250.00, 90.00, -91.21 ),
                new ForceTuple( 0.00, 80.00, 65.28 ),
                new ForceTuple( -250.00, 90.00, 88.57 ),
                new ForceTuple( -500.00, 100.00, 88.17 ),
                new ForceTuple( -750.00, 110.00, 84.58 ),
                new ForceTuple( -1000.00, 100.00, 88.96 ),
                new ForceTuple( -1250.00, 90.00, 91.21 )
            };

            CompareDirectAndIntersectmethods(sectionChecker, forces, referenceWR, 0.09, double.PositiveInfinity, referenceForces);
        }

        // SectionSolver.FailureAnalysisTypes.ConstantNMy
        [TestMethod]
        public void IntersectVsDirect07()
        {
            MakeSimpleSection02(SectionSolver.FailureAnalysisTypes.ConstantNMy, out CoordinateSystem cs, out SectionCheckerACI318 sectionChecker, out ResultBeamForces[] forces);
            sectionChecker.SectionCheckerOptionsACI318.ForceReferenceCoordinateSystem = cs;

            // Workiong ratio from SBeton.
            var referenceWR = new List<double>()
            {
                66.14 / 100.0,
                53.34 / 100.0,
                47.34 / 100.0,
                53.53 / 100.0,
                51.86 / 100.0,
                48.20 / 100.0,
                67.54 / 100.0,
                54.46 / 100.0,
                50.83 / 100.0,
                57.61 / 100.0,
                54.90 / 100.0,
                50.95 / 100.0
            };

            // Domain point force from SBeton.
            var referenceForces = new List<ForceTuple>()
            {
                new ForceTuple( 0.00, 120.95, -32.00 ),
                new ForceTuple( -250.00, 168.74, -32.00 ),
                new ForceTuple( -500.00, 211.24, -32.00 ),
                new ForceTuple( -750.00, 205.48, -32.00 ),
                new ForceTuple( -1000.00, 192.81, -32.00 ),
                new ForceTuple( -1250.00, 186.74, -32.00 ),
                new ForceTuple( 0.00, 118.45, 40.00 ),
                new ForceTuple( -250.00, 165.25, 40.00 ),
                new ForceTuple( -500.00, 196.72, 40.00 ),
                new ForceTuple( -750.00, 190.95, 40.00 ),
                new ForceTuple( -1000.00, 182.15, 40.00 ),
                new ForceTuple( -1250.00, 176.66, 40.00 )
            };

            CompareDirectAndIntersectmethods(sectionChecker, forces, referenceWR, 0.05, 0.02, referenceForces);
        }

        // SectionSolver.FailureAnalysisTypes.ConstantMxMy
        [TestMethod]
        public void IntersectVsDirect08()
        {
            MakeSimpleSection02(SectionSolver.FailureAnalysisTypes.ConstantMxMy, out CoordinateSystem cs, out SectionCheckerACI318 sectionChecker, out ResultBeamForces[] forces);
            sectionChecker.SectionCheckerOptionsACI318.ForceReferenceCoordinateSystem = cs;

            // Workiong ratio from SBeton.
            var referenceWR = new List<double>()
            {
                0.00 / 100.0,
                11.18 / 100.0,
                22.97 / 100.0,
                35.51 / 100.0,
                45.95 / 100.0,
                55.90 / 100.0,
                0.00 / 100.0,
                11.54 / 100.0,
                23.77 / 100.0,
                36.81 / 100.0,
                47.53 / 100.0,
                57.72 / 100.0
            };

            // Domain point force from SBeton.
            var referenceForces = new List<ForceTuple>()
            {
                new ForceTuple( 203.39, 80.00, -32.00 ),
                new ForceTuple( -2236.19, 90.00, -32.00 ),
                new ForceTuple( -2176.35, 100.00, -32.00 ),
                new ForceTuple( -2112.34, 110.00, -32.00 ),
                new ForceTuple( -2176.35, 100.00, -32.00 ),
                new ForceTuple( -2236.19, 90.00, -32.00 ),
                new ForceTuple( 192.55, 80.00, 40.00 ),
                new ForceTuple( -2165.58, 90.00, 40.00 ),
                new ForceTuple( -2103.88, 100.00, 40.00 ),
                new ForceTuple( -2037.57, 110.00, 40.00 ),
                new ForceTuple( -2103.88, 100.00, 40.00 ),
                new ForceTuple( -2165.58, 90.00, 40.00 )
            };

            CompareDirectAndIntersectmethods(sectionChecker, forces, referenceWR, 0.03, double.PositiveInfinity, referenceForces);
        }
    }
}
