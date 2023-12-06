using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Results;
using System;
using System.Collections.Generic;
using System.Linq;

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

            double workingRatio = -1;
            SectionSolver.FailureZones _failureIndex = SectionSolver.FailureZones.F1;
            double immersione = -1;
            StrainPlane strainPlane = null;
            ForceTuple forceTuple = new ForceTuple();

            Point3d rayOrigin = null; // Must be inside the mesh volume.

            switch (_solver.SectionOption.FailureAnalysisType)
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
            if (_solver.SectionOption.FailureAnalysisType != SectionSolver.FailureAnalysisTypes.ConstantEccentricity || Point3d.Origin.DistanceTo(rayOrigin) > _failureAnalysisIntersectionTolerance)
            {
                double rayOriginWorkingRatioOrigin = workingRatioSearch(Point3d.Origin, rayOrigin, out _);
                // If the origin point of the ray is outside then enforce the use of ConstantEccentricity.
                if (rayOriginWorkingRatioOrigin >= 1.0)
                    rayOrigin = Point3d.Origin;
            }

            // Now the working ratio search.
            var forcePoint = new Point3d(targetLocalForces.M1, targetLocalForces.M2, targetLocalForces.N);
            workingRatio = workingRatioSearch(rayOrigin, forcePoint, out KeyValuePair<Point3d, MeshBase> intersection);

            // If a solution has been found assigns the deformation plane.
            if (workingRatio != -1 && intersection.Value != null)
            {
                forceTuple = new ForceTuple(intersection.Key.Z, intersection.Key.X, intersection.Key.Y);

                if (intersection.Value is MeshVertex intersectionVertex)
                {
                    var failDomainPoint = _vertexToDomainPoint[intersectionVertex];
                    _failureIndex = failDomainPoint.FailureIndex;
                    immersione = failDomainPoint.Immersione;
                    strainPlane = failDomainPoint.StrainPlane;
                }
                else if (intersection.Value is MeshEdge intersectionEdge)
                {
                    // Calculates linear interpolation weights.
                    var vA = _domainMesh.Vertices[intersectionEdge.A];
                    var vB = _domainMesh.Vertices[intersectionEdge.B];
                    double distB = vB.Point.DistanceTo(forcePoint);
                    double distA = vA.Point.DistanceTo(forcePoint);
                    double weightA = distB / (distA + distB);
                    double weightB = distA / (distA + distB);

                    // Get failure domain points.
                    var failA = _vertexToDomainPoint[vA];
                    var failB = _vertexToDomainPoint[vB];

                    // Make interpolation.
                    if (failA != null && failB != null)
                    {
                        // FailureIndex
                        _failureIndex = (SectionSolver.FailureZones)Math.Min((int)failA.FailureIndex, (int)failB.FailureIndex);

                        // Theta
                        var thetaA = failA.StrainPlane.Teta;
                        var thetaB = failB.StrainPlane.Teta;
                        // Make them close together.
                        if (Math.Abs(thetaA - thetaB) > Math.PI)
                        {
                            if (thetaA < thetaB)
                                thetaA += 2.0 * Math.PI;
                            else
                                thetaB += 2.0 * Math.PI;
                        }
                        double theta = thetaA * weightA + thetaB * weightB;

                        // Immersione
                        var immA = _solver.GetImmersione(failA, _failureIndex);
                        var immB = _solver.GetImmersione(failB, _failureIndex);
                        immersione = immA * weightA + immB * weightB;

                        // StrainPlane
                        strainPlane = _solver.BuildPlane(theta, _solver.SectionOption.FailureDomainType, _failureIndex, immersione);
                    }
                }
                else if (intersection.Value is MeshFace intersectionFace)
                {
                    // Calculates linear interpolation weights.
                    var vA = _domainMesh.Vertices[intersectionFace.A];
                    var vB = _domainMesh.Vertices[intersectionFace.B];
                    var vC = _domainMesh.Vertices[intersectionFace.C];
                    double areaA = new Vector3d((vB.Point - forcePoint) ^ (vC.Point - forcePoint)).Length;
                    double areaB = new Vector3d((vC.Point - forcePoint) ^ (vA.Point - forcePoint)).Length;
                    double areaC = new Vector3d((vA.Point - forcePoint) ^ (vB.Point - forcePoint)).Length;
                    double areaTOT = areaA + areaB + areaC;
                    double weightA = areaA / areaTOT;
                    double weightB = areaB / areaTOT;
                    double weightC = areaC / areaTOT;

                    // Get failure domain points.
                    var failA = _vertexToDomainPoint[vA];
                    var failB = _vertexToDomainPoint[vB];
                    var failC = _vertexToDomainPoint[vC];

                    // Make interpolation.
                    if (failA != null && failB != null && failC != null)
                    {
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
            double workingRatioSearch(Point3d pointOrigin, Point3d pointToSearch, out KeyValuePair<Point3d, MeshBase> meshIntersection)
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
                    switch (_solver.SectionOption.FailureAnalysisType)
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
                Line3d semiRay = new Line3d(pointOrigin, targetPoint);
                var intersOnDomain = _domainMesh.GetIntersectionWihtSemiInfiniteRay(semiRay, true, _failureAnalysisIntersectionTolerance);
                if (intersOnDomain.Count == 0)
                    return -1;

                // Find the key with the smallest distance and get the corresponding pair from the dictionary.
                var closestEntryOnDomain = intersOnDomain.OrderBy(pair => pair.Key.DistanceTo(pointOrigin)).FirstOrDefault();

                if (closestEntryOnDomain.Key is null)
                    return -1;

                meshIntersection = closestEntryOnDomain;

                if (!isRatioZero)
                    return pointOrigin.DistanceTo(pointToSearch) / pointOrigin.DistanceTo(closestEntryOnDomain.Key);
                else
                    return 0.0;
            }
        }
    }
}
