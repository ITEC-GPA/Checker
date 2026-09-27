using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Results;
using System;
using System.Collections.Generic;

namespace GPC.Checker.SectionSolvers
{
    internal class DomainPointStrategyIntersection : IDomainPointStrategy
    {
        protected readonly SectionSolver _solver;
        protected double _failureAnalysisIntersectionTolerance;

        /// <summary>
        /// List of correspondences to make the search for points faster.
        /// Initially null, it is set at the first calculation.
        /// </summary>
        Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> _vertexToDomainPoint;

        /// <summary>
        /// Domain Mesh.
        /// Initially null, it is set at the first calculation.
        /// </summary>
        Mesh _domainMesh;

        public DomainPointStrategyIntersection(in SectionSolver solver)
        {
            _solver = solver;
            _failureAnalysisIntersectionTolerance = 10;
            _vertexToDomainPoint = null;
            _domainMesh = null;
            CalculateDomainMesh();
        }

        /// <summary>
        /// Domain mesh calculation, will be performed only if not already done.
        /// </summary>
        private void CalculateDomainMesh()
        {
            // Is the calculation done?
            if (_vertexToDomainPoint is null || _domainMesh is null)
            {
                var failureDomain = _solver.GetFailureDomainResult();
                _domainMesh = failureDomain.Domain.GetMesh(failureDomain.Domain, out _vertexToDomainPoint);
            }
        }

        public SectionSolver Solver => _solver;

        /// <summary>
        /// Given a solicitation finds the point on the strength domain and work rate based on the search method of approaching the surface.
        /// The calculation of the strain plane is by interpolation and is much less accurate than the iterative/direct method.
        /// This method is good for always finding an working ratio, which is always in favor of safety.
        /// If the starting mesh does not have too many elements then it is also a very performing method.
        /// </summary>
        public FailureDomain.FailureDomainPoint CalculateDomainPoint(ResultBeamForces targetLocalForces, SectionSolver.FailureAnalysisTypes? failureAnalysisTypeOverride = null)
        {
            CalculateDomainMesh();
            var failureAnalysisType = failureAnalysisTypeOverride is null ? _solver.SectionOption.FailureAnalysisType : failureAnalysisTypeOverride.Value;
            // the domain is in the solver axes: the force is converted into them, with the transport moment when its origin is another one
            // (before, the components were used as they were given)
            targetLocalForces = targetLocalForces.ToCoordinateSystemWithEccentricity(_solver.SolverAxes);

            double workingRatio = -1;
            SectionSolver.FailureZones _failureIndex = SectionSolver.FailureZones.F1;
            double immersione = -1;
            StrainPlane strainPlane = null;
            ForceTuple forceTuple = new ForceTuple();

            Point3d rayOrigin = null; // Must be inside the mesh volume.

            switch (failureAnalysisType)
            {
                case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                    rayOrigin = Point3d.Origin;
                    break;

                case SectionSolver.FailureAnalysisTypes.ConstantN:
                    rayOrigin = new Point3d(0.0, 0.0, targetLocalForces.N);
                    break;

                case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                    rayOrigin = new Point3d(targetLocalForces.M1, targetLocalForces.M2, 0.0);
                    break;

                case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                    rayOrigin = new Point3d(targetLocalForces.M1, 0.0, targetLocalForces.N);
                    break;

                case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                    rayOrigin = new Point3d(0.0, targetLocalForces.M2, targetLocalForces.N);
                    break;
            }

            // For some surface approach methods there may not be an intersection, for these cases we need to do a control
            // specifically to change the actual surface approach method used.
            // The origin of the ray rayOrigin will also determine the ratio and must be internal to the domain.
            if (failureAnalysisType != SectionSolver.FailureAnalysisTypes.ConstantEccentricity || Point3d.Origin.DistanceTo(rayOrigin) > _failureAnalysisIntersectionTolerance)
            {
                double rayOriginWorkingRatioOrigin = workingRatioSearch(Point3d.Origin, rayOrigin, out _, out _);
                // If the origin point of the ray is outside then enforce the use of ConstantEccentricity.
                if (rayOriginWorkingRatioOrigin >= 1.0)
                    rayOrigin = Point3d.Origin;
            }

            // Now the working ratio search.
            var forceP = new Point3d(targetLocalForces.M1, targetLocalForces.M2, targetLocalForces.N);
            // New method for intersection finder.
            {
                workingRatio = workingRatioSearch(rayOrigin, forceP, out Point3d intersectionPoint, out MeshFace intersectionFace);

                // If a solution has been found assigns the deformation plane.
                if (workingRatio != -1 && intersectionFace != null)
                {
                    forceTuple = new ForceTuple(intersectionPoint.Z, intersectionPoint.X, intersectionPoint.Y);
                    workingRatio = rayOrigin.DistanceTo(forceP) / rayOrigin.DistanceTo(intersectionPoint);
                    {
                        // Calculates linear interpolation weights.
                        var vA = _domainMesh.Vertices[intersectionFace.A];
                        var vB = _domainMesh.Vertices[intersectionFace.B];
                        var vC = _domainMesh.Vertices[intersectionFace.C];
                        double areaA = new Vector3d((vB.Point - intersectionPoint) ^ (vC.Point - intersectionPoint)).Length;
                        double areaB = new Vector3d((vC.Point - intersectionPoint) ^ (vA.Point - intersectionPoint)).Length;
                        double areaC = new Vector3d((vA.Point - intersectionPoint) ^ (vB.Point - intersectionPoint)).Length;
                        double areaTOT = areaA + areaB + areaC;
                        double weightA = areaA / areaTOT;
                        double weightB = areaB / areaTOT;
                        double weightC = areaC / areaTOT;

                        // Make interpolation.
                        var failA = _vertexToDomainPoint[vA];
                        var failB = _vertexToDomainPoint[vB];
                        var failC = _vertexToDomainPoint[vC];

                        // FailureIndex
                        _failureIndex = (SectionSolver.FailureZones)Math.Min((int)failA.FailureIndex, Math.Min((int)failB.FailureIndex, (int)failC.FailureIndex));

                        // Theta
                        var thetaA = failA.StrainPlane.Teta;
                        var thetaB = failB.StrainPlane.Teta;
                        var thetaC = failC.StrainPlane.Teta;
                        // Make them close together.
                        if (Math.Abs(thetaA - thetaB) > Math.PI)
                        {
                            if (thetaA < thetaB)
                                thetaA += 2.0 * Math.PI;
                            else
                                thetaB += 2.0 * Math.PI;
                        }
                        if (Math.Abs(thetaA - thetaC) > Math.PI)
                        {
                            thetaC += 2.0 * Math.PI;
                        }
                        double theta = thetaA * weightA + thetaB * weightB + thetaC * weightC;

                        // Immersione
                        var immA = _solver.GetImmersione(failA, _failureIndex);
                        var immB = _solver.GetImmersione(failB, _failureIndex);
                        var immC = _solver.GetImmersione(failC, _failureIndex);
                        immersione = immA * weightA + immB * weightB + immC * weightC;

                        // StrainPlane
                        strainPlane = _solver.BuildPlane(theta, _solver.SectionOption.FailureDomainType, _failureIndex, immersione);
                    }
                }
                else
                    return null;
            }

            return new FailureDomain.FailureDomainPoint(forceTuple, _failureIndex, strainPlane, immersione) { WorkingRatio = workingRatio };


            // Internal utility.
            double workingRatioSearch(Point3d pointOrigin, Point3d pointToSearch, out Point3d intersectionPoint, out MeshFace intersectionFace)
            {
                Point3d targetPoint;
                bool isRatioZero = pointOrigin.DistanceTo(pointToSearch) < _failureAnalysisIntersectionTolerance;

                if (!isRatioZero)
                {
                    targetPoint = pointToSearch;
                }
                else
                {
                    // This is a special case with ratio=0.
                    switch (failureAnalysisType)
                    {
                        case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                            targetPoint = pointOrigin + new Point3d(0.0, SectionSolver.FROM_KNM_TO_NM, 0.0);
                            break;

                        case SectionSolver.FailureAnalysisTypes.ConstantN:
                        case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                            targetPoint = pointOrigin + new Point3d(SectionSolver.FROM_KNM_TO_NM, 0.0, 0.0);
                            break;

                        case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                        case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                        default:
                            targetPoint = pointOrigin + new Point3d(0.0, 0.0, SectionSolver.FROM_KN_TO_N);
                            break;
                    }
                }
                var semiRay = new Ray3d(pointOrigin, targetPoint);
                if (!_domainMesh.PickFace(semiRay, out intersectionFace, out intersectionPoint))
                    return -1;

                if (!isRatioZero)
                    return pointOrigin.DistanceTo(pointToSearch) / pointOrigin.DistanceTo(intersectionPoint);
                else
                    return 0.0;
            }
        }
    }
}
