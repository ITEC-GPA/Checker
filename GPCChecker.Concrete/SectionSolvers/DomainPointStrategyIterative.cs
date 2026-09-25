using GPC.Checker.Helper;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checker.SectionSolvers
{
    /// <summary>
    /// Iterative method to find a point in the resistant domain.
    /// </summary>
    internal class DomainPointStrategyIterative : IDomainPointStrategy
    {
        protected readonly SectionSolver _solver;

        public SectionSolver Solver => _solver;

        public DomainPointStrategyIterative(in SectionSolver solver)
        {
            _solver = solver;
        }

        public FailureDomain.FailureDomainPoint CalculateDomainPoint(ResultBeamForces force, SectionSolver.FailureAnalysisTypes? failureAnalysisTypeOverride = null)
        {
            var targetLocalForces = new ForceTuple(force.ToCoordinateSystemWithEccentricity(_solver.SectionOption.ForceReferenceCoordinateSystem));
            var failureAnalysisType = failureAnalysisTypeOverride is null ? _solver.SectionOption.FailureAnalysisType : failureAnalysisTypeOverride.Value;
            switch (failureAnalysisType)
            {
                case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantN:
                    if (targetLocalForces.Mx == 0 && targetLocalForces.My == 0)
                        return null;
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                    if (targetLocalForces.My == 0)
                        return null;
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                    if (targetLocalForces.Mx == 0)
                        return null;
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                    break;
                default:
                    break;
            }

            var rebarIsInsideAssociation = _solver.ConcreteSection.GetRebarIsInsideAssociation();

            ForceTuple adimOutputForces = _solver.ConvertToAdimensionalForces(targetLocalForces);

            Vector3d vectorEd = null;
            switch (failureAnalysisType)
            {
                case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                    vectorEd = new Vector3d(targetLocalForces.Mx / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.My / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.N / SectionSolver.FROM_KN_TO_N);
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantN:
                    vectorEd = new Vector3d(targetLocalForces.Mx / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.My / SectionSolver.FROM_KNM_TO_NM, 0.0);
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                    vectorEd = new Vector3d(0, targetLocalForces.My / SectionSolver.FROM_KNM_TO_NM, 0);
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                    vectorEd = new Vector3d(targetLocalForces.Mx / SectionSolver.FROM_KNM_TO_NM, 0, 0);
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                    vectorEd = new Vector3d(0, 0, targetLocalForces.N / SectionSolver.FROM_KN_TO_N);
                    break;
            }

            // Valori di primo tentativo
            var failureIndex = SectionSolver.FailureZones.F3A;
            double eta = 0.5;
            double teta = Math.Atan2(targetLocalForces.My, targetLocalForces.Mx);

            switch (failureAnalysisType)
            {
                case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                    if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-10 && Math.Abs(adimOutputForces.My) < 1e-10)
                    {
                        failureIndex = SectionSolver.FailureZones.F3A;
                        eta = 0.40;
                    }
                    else if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
                    {
                        failureIndex = SectionSolver.FailureZones.F3A;
                        eta = 0.50;
                    }
                    else if (adimOutputForces.N > 0.0)
                    {
                        failureIndex = SectionSolver.FailureZones.F3A;
                        eta = 0.6;
                    }
                    else if (Math.Abs(adimOutputForces.N) < 1e-5)
                    {
                        failureIndex = SectionSolver.FailureZones.F3A;
                        eta = 0.60;
                    }
                    else if (adimOutputForces.N < 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-2 && Math.Abs(adimOutputForces.My) < 1e-2)
                    {
                        failureIndex = SectionSolver.FailureZones.F3B;
                        eta = 0.8;
                    }
                    else if (adimOutputForces.N < 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
                    {
                        failureIndex = SectionSolver.FailureZones.F4;
                        eta = 0.9;
                    }
                    else
                    {
                        failureIndex = SectionSolver.FailureZones.F3B;
                        eta = 0.8;
                    }
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantN:
                case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                    if (adimOutputForces.N > 0.0)
                    {
                        failureIndex = SectionSolver.FailureZones.F3A;
                        eta = 0.90;
                    }
                    else if (adimOutputForces.N < 0.2)
                    {
                        failureIndex = SectionSolver.FailureZones.F3A;
                        eta = 0.95;
                    }
                    else if (adimOutputForces.N < 0.4)
                    {
                        failureIndex = SectionSolver.FailureZones.F4;
                        eta = 0.25;
                    }
                    else if (adimOutputForces.N < 0.6)
                    {
                        failureIndex = SectionSolver.FailureZones.F4;
                        eta = 0.5;
                    }
                    else if (adimOutputForces.N < 1)
                    {
                        failureIndex = SectionSolver.FailureZones.F4;
                        eta = 0.75;
                    }
                    else
                    {
                        failureIndex = SectionSolver.FailureZones.F3A;
                        eta = 0.95;
                    }
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                    if (adimOutputForces.N > 0.0)
                    {
                        failureIndex = SectionSolver.FailureZones.F3A;
                        eta = 0.1;
                    }
                    else
                    {
                        failureIndex = SectionSolver.FailureZones.F4;
                        eta = 0.5;
                    }
                    break;
            }
            if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-10 && Math.Abs(adimOutputForces.My) < 1e-10)
            {
                if (_solver.IntegrationReferencePoint.Y - _solver.ConcreteSection.GetHomogenizedCentroid(out _, out _).Y > 0)
                    teta = Math.PI;
            }

            int id = 1;
            _solver.CalculateRotationPointsPerMaterial(teta, _solver.SectionOption.FailureDomainType, out List<DeformationFieldsPoint> tensionRotationPoints, out List<DeformationFieldsPoint> tensionRotationPointsF1, out List<DeformationFieldsPoint> compressionRotationPoints, out double minDistanceCompression, out double elasticEpsilonTension);
            var strainPlane = _solver.CalculateStrainPlaneMultiPoints(teta, failureIndex, eta, failureIndex == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension, id);

            teta = strainPlane.Teta;

            ForceTuple forces = _solver.GetExternalForces(_solver.CalculateForceResultantForDomain(strainPlane, rebarIsInsideAssociation), _solver.SectionOption.ForceReferenceCoordinateSystem);
            ForceTuple adimIncrement = _solver.ConvertToAdimensionalForces(forces - targetLocalForces);

            (double deltaTeta, double deltaEta, Vector3d distanceToTarget) increment;

            double angle = -1;
            bool exit = false;
            bool pointOutOfDomain = false;

            var closestPoint = new FailureDomain.FailureDomainPoint(forces, failureIndex, strainPlane, eta);

            switch (failureAnalysisType)
            {
                case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                    angle = new Vector3d(forces.Mx / SectionSolver.FROM_KNM_TO_NM, forces.My / SectionSolver.FROM_KNM_TO_NM, forces.N / SectionSolver.FROM_KN_TO_N).AngleTo(new Vector3d(targetLocalForces.Mx / SectionSolver.FROM_KNM_TO_NM,
                        targetLocalForces.My / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.N / SectionSolver.FROM_KN_TO_N));
                    if (Math.Abs(angle) < _solver.FailureAnalysisAngularTolerance)
                        exit = true;
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantN:
                    angle = new Vector3d(forces.Mx / SectionSolver.FROM_KNM_TO_NM, forces.My / SectionSolver.FROM_KNM_TO_NM, 0).AngleTo(new Vector3d(targetLocalForces.Mx / SectionSolver.FROM_KNM_TO_NM,
                        targetLocalForces.My / SectionSolver.FROM_KNM_TO_NM, 0));
                    if (Math.Abs(adimIncrement.N) < _solver.FailureAnalysisDistanceTolerance && Math.Abs(angle) < _solver.FailureAnalysisAngularTolerance)
                        exit = true;
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                    angle = new Vector3d(targetLocalForces.Mx / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.My / SectionSolver.FROM_KNM_TO_NM, forces.N / SectionSolver.FROM_KN_TO_N).AngleTo(new Vector3d(targetLocalForces.Mx / SectionSolver.FROM_KNM_TO_NM,
                        targetLocalForces.My / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.N / SectionSolver.FROM_KN_TO_N));
                    if (Math.Abs(adimIncrement.Mx) < _solver.FailureAnalysisDistanceTolerance && Math.Abs(adimIncrement.My) < _solver.FailureAnalysisDistanceTolerance)
                        exit = true;
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                    angle = new Vector3d(targetLocalForces.Mx / SectionSolver.FROM_KNM_TO_NM, forces.My / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.N / SectionSolver.FROM_KN_TO_N).AngleTo(new Vector3d(targetLocalForces.Mx / SectionSolver.FROM_KNM_TO_NM,
                        targetLocalForces.My / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.N / SectionSolver.FROM_KN_TO_N));
                    if (Math.Abs(adimIncrement.N) < _solver.FailureAnalysisDistanceTolerance && Math.Abs(adimIncrement.Mx) < _solver.FailureAnalysisDistanceTolerance)
                        exit = true;
                    break;
                case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                    angle = new Vector3d(forces.Mx / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.My / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.N / SectionSolver.FROM_KN_TO_N).AngleTo(new Vector3d(targetLocalForces.Mx / SectionSolver.FROM_KNM_TO_NM,
                        targetLocalForces.My / SectionSolver.FROM_KNM_TO_NM, targetLocalForces.N / SectionSolver.FROM_KN_TO_N));
                    if (Math.Abs(adimIncrement.N) < _solver.FailureAnalysisDistanceTolerance && Math.Abs(adimIncrement.My) < _solver.FailureAnalysisDistanceTolerance)
                        exit = true;
                    break;
            }

            if (!exit)
            {
                Line3d externalForcesLine = null;
                switch (failureAnalysisType)
                {
                    case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                        externalForcesLine = new Line3d(new Point3d(0, 0, 0), targetLocalForces);
                        break;
                    case SectionSolver.FailureAnalysisTypes.ConstantN:
                        externalForcesLine = new Line3d(new Point3d(0, 0, targetLocalForces.N), targetLocalForces);
                        break;
                    case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                        externalForcesLine = new Line3d(new Point3d(targetLocalForces.Mx, targetLocalForces.My, 0),
                            new Point3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N - SectionSolver.FROM_KN_TO_N));
                        break;
                    case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                        externalForcesLine = new Line3d(new Point3d(targetLocalForces.Mx, 0, targetLocalForces.N),
                            new Point3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N));
                        break;
                    case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                        externalForcesLine = new Line3d(new Point3d(0, targetLocalForces.My, targetLocalForces.N),
                            new Point3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N));
                        break;
                }

                if (failureAnalysisType == SectionSolver.FailureAnalysisTypes.ConstantMxMy)
                {
                    FailureDomain.FailureDomainPoint pointBuffer = CalculateDomainPoint(force, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
                    FailureDomain.FailureDomainForce failureDomainForce = new FailureDomain.FailureDomainForce(
                        new ResultBeamForces(targetLocalForces.N, 0, 0, 0, targetLocalForces.Mx, targetLocalForces.My, _solver.SectionOption.ForceReferenceCoordinateSystem), pointBuffer);

                    double wr = failureDomainForce.CalculateWorkingRatio(failureAnalysisType, SectionSolver.FROM_KNM_TO_NM, SectionSolver.FROM_KN_TO_N);
                    if (wr > 1)
                        pointOutOfDomain = true;
                }

                do
                {
                    double angleBuffer;

                    if (id < 200 && !pointOutOfDomain)
                    {
                        try
                        {
                            increment = CalculateIncrement(forces, strainPlane, failureIndex, eta, externalForcesLine, angle, _solver.SectionOption.FailureDomainType, rebarIsInsideAssociation);
                        }
                        catch (Exception e)
                        {
                            _solver.Log.Add(e.Message);
                            if (e.InnerException != null)
                                _solver.Log.Add(e.InnerException.Message);
                            return null;
                        }
                        SetIncrement(_solver.SectionOption.FailureDomainType, ref failureIndex, ref teta, ref eta, increment.deltaTeta, increment.deltaEta);

                        id++;
                        _solver.CalculateRotationPointsPerMaterial(teta, _solver.SectionOption.FailureDomainType, out tensionRotationPoints, out tensionRotationPointsF1, out compressionRotationPoints, out minDistanceCompression, out elasticEpsilonTension);
                        strainPlane = _solver.CalculateStrainPlaneMultiPoints(teta, failureIndex, eta, failureIndex == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension, id);
                        forces = _solver.GetExternalForces(_solver.CalculateForceResultantForDomain(strainPlane, rebarIsInsideAssociation), _solver.SectionOption.ForceReferenceCoordinateSystem);

                        ForceTuple incrementForce = new ForceTuple(increment.distanceToTarget.Z, increment.distanceToTarget.X, increment.distanceToTarget.Y);
                        adimIncrement = _solver.ConvertToAdimensionalForces(incrementForce);

                        angleBuffer = Math.PI;

                        switch (failureAnalysisType)
                        {
                            case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                                angleBuffer = new Vector3d(forces.Mx / SectionSolver.FROM_KNM_TO_NM, forces.My / SectionSolver.FROM_KNM_TO_NM, forces.N / SectionSolver.FROM_KN_TO_N).AngleTo(vectorEd);
                                break;
                            case SectionSolver.FailureAnalysisTypes.ConstantN:
                                angleBuffer = new Vector3d(forces.Mx / SectionSolver.FROM_KNM_TO_NM, forces.My / SectionSolver.FROM_KNM_TO_NM, 0).AngleTo(vectorEd);
                                break;
                            case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                            case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                            case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                                angleBuffer = new Vector3d((forces.Mx - targetLocalForces.Mx) / SectionSolver.FROM_KNM_TO_NM, (forces.My - targetLocalForces.My) / SectionSolver.FROM_KNM_TO_NM,
                                    (forces.N - targetLocalForces.N) / SectionSolver.FROM_KN_TO_N).AngleTo(vectorEd);
                                break;
                        }

                        if (angleBuffer < angle)
                        {
                            closestPoint = new FailureDomain.FailureDomainPoint(forces, failureIndex, strainPlane, eta);
                            angle = angleBuffer;
                        }

                        if ((Math.Abs(adimIncrement.N) < _solver.FailureAnalysisDistanceTolerance &&
                            Math.Abs(adimIncrement.Mx) < _solver.FailureAnalysisDistanceTolerance &&
                            Math.Abs(adimIncrement.My) < _solver.FailureAnalysisDistanceTolerance))
                            break;
                    }
                    else
                    {
                        _solver.Log.Add("Fail to calculate point on domain");
                        if (failureIndex == SectionSolver.FailureZones.F2A || failureIndex == SectionSolver.FailureZones.F2B)
                        {
                            if (angle < 100 * _solver.FailureAnalysisAngularTolerance)
                                return closestPoint;
                        }
                        else if (failureIndex == SectionSolver.FailureZones.F3A || failureIndex == SectionSolver.FailureZones.F3B || failureIndex == SectionSolver.FailureZones.F4)
                        {
                            if (angle < 20 * _solver.FailureAnalysisAngularTolerance)
                                return closestPoint;
                        }
                        return null;
                    }

                    switch (failureAnalysisType)
                    {
                        case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                            if (Math.Abs(angleBuffer) < _solver.FailureAnalysisAngularTolerance)
                                exit = true;
                            break;
                        case SectionSolver.FailureAnalysisTypes.ConstantN:
                            if (Math.Abs(adimIncrement.N) < _solver.FailureAnalysisDistanceTolerance && Math.Abs(angleBuffer) < _solver.FailureAnalysisAngularTolerance)
                                exit = true;
                            break;
                        case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                            if (Math.Abs(adimIncrement.Mx) < _solver.FailureAnalysisDistanceTolerance &&
                                Math.Abs(adimIncrement.My) < _solver.FailureAnalysisDistanceTolerance)
                                exit = true;
                            break;
                        case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                            if (Math.Abs(adimIncrement.N) < _solver.FailureAnalysisDistanceTolerance &&
                                Math.Abs(adimIncrement.Mx) < _solver.FailureAnalysisDistanceTolerance &&
                                Math.Abs(angleBuffer) < _solver.FailureAnalysisAngularTolerance)
                                exit = true;
                            break;
                        case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                            if (Math.Abs(adimIncrement.N) < _solver.FailureAnalysisDistanceTolerance &&
                                Math.Abs(adimIncrement.My) < _solver.FailureAnalysisDistanceTolerance &&
                                Math.Abs(angleBuffer) < _solver.FailureAnalysisAngularTolerance)
                                exit = true;
                            break;
                    }

                } while (!exit);
            }

            return new FailureDomain.FailureDomainPoint(forces, failureIndex, strainPlane, eta);
        }

        protected (double deltaTeta, double deltaEta, Vector3d distanceToTarget) CalculateIncrement(ForceTuple iterationPoint,
            StrainPlane inputStrainPlane, SectionSolver.FailureZones inputFailureZone, double inputImmersioneNelCampo, Line3d externalForcesLine,
            double deltaAngle, SectionSolver.FailureDomainTypes failureDomainType, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            var adimIteractionPoint = _solver.ConvertToAdimensionalForces(iterationPoint);

            double dTeta;
            double dEta;

            switch (inputFailureZone)
            {
                case SectionSolver.FailureZones.F1:
                    dTeta = 0.25;
                    dEta = 0.25;
                    break;

                case SectionSolver.FailureZones.F2A:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.01), 0.005);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.01), 0.001);
                    break;

                case SectionSolver.FailureZones.F2B:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.01), 0.005);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.01), 0.0005);
                    break;

                case SectionSolver.FailureZones.F3A:
                    dTeta = Math.Max(0.1 * Math.Min(deltaAngle, 0.01), 0.0001);
                    dEta = Math.Max(0.01 * Math.Min(deltaAngle, 0.01), 0.00001);
                    break;

                case SectionSolver.FailureZones.F3B:
                    dTeta = Math.Max(0.1 * Math.Min(deltaAngle, 0.01), 0.00001);
                    dEta = Math.Max(0.01 * Math.Min(deltaAngle, 0.01), 0.000001);
                    break;

                default:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.01), 0.0001);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.01), 0.0001);
                    break;
            }

            double dNdTeta;
            double dMxdTeta;
            double dMydTeta;
            double dNdImm;
            double dMxdImm;
            double dMydImm;

            double nonLinearErrorTeta;
            double nonLinearErrorEta;
            double dTetaBuffer = dTeta;
            double dEtaBuffer = dEta;

            int etaCounter = 1;
            int tetaCounter = 1;

            // derivate parziali rispetto a teta
            do
            {
                if (tetaCounter < 10)
                {
                    var tetaPlus = inputStrainPlane.Teta + dTetaBuffer;
                    _solver.CalculateRotationPointsPerMaterial(tetaPlus, failureDomainType, out List<DeformationFieldsPoint> tensionRotationPointsPlusdTeta, out List<DeformationFieldsPoint> tensionRotationPointsF1PlusdTeta, out List<DeformationFieldsPoint> compressionRotationPointsPlusdTeta, out double minDistanceCompressionPlusdTeta, out double elasticEpsilonTensionPlusdTeta);
                    StrainPlane strainPlanePlusdTeta = _solver.CalculateStrainPlaneMultiPoints(tetaPlus, inputFailureZone, inputImmersioneNelCampo, inputFailureZone == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1PlusdTeta : tensionRotationPointsPlusdTeta, compressionRotationPointsPlusdTeta, minDistanceCompressionPlusdTeta, elasticEpsilonTensionPlusdTeta);

                    var tetaMinus = inputStrainPlane.Teta - dTetaBuffer;
                    _solver.CalculateRotationPointsPerMaterial(tetaMinus, failureDomainType, out List<DeformationFieldsPoint> tensionRotationPointsMinusdTeta, out List<DeformationFieldsPoint> tensionRotationPointsF1MinusdTeta, out List<DeformationFieldsPoint> compressionRotationPointsMinusdTeta, out double minDistanceCompressionMinusdTeta, out double elasticEpsilonTensionMinusdTeta);
                    StrainPlane strainPlaneMinusdTeta = _solver.CalculateStrainPlaneMultiPoints(tetaMinus, inputFailureZone, inputImmersioneNelCampo, inputFailureZone == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1MinusdTeta : tensionRotationPointsMinusdTeta, compressionRotationPointsMinusdTeta, minDistanceCompressionMinusdTeta, elasticEpsilonTensionMinusdTeta);

                    var forcesPlusTeta = _solver.CalculateForceResultantForDomain(strainPlanePlusdTeta, rebarIsInsideAssociation);
                    var forcesMinusTeta = _solver.CalculateForceResultantForDomain(strainPlaneMinusdTeta, rebarIsInsideAssociation);

                    dNdTeta = (forcesPlusTeta.N - forcesMinusTeta.N) / (2.0 * dTetaBuffer);
                    dMxdTeta = (forcesPlusTeta.Mx - forcesMinusTeta.Mx) / (2.0 * dTetaBuffer);
                    dMydTeta = (forcesPlusTeta.My - forcesMinusTeta.My) / (2.0 * dTetaBuffer);

                    dTetaBuffer += 2.0 * dTeta;

                    var adimForcePlusTeta = _solver.ConvertToAdimensionalForces(new ForceTuple(forcesPlusTeta.N, forcesPlusTeta.Mx, forcesPlusTeta.My));
                    var adimForceMinusTeta = _solver.ConvertToAdimensionalForces(new ForceTuple(forcesMinusTeta.N, forcesMinusTeta.Mx, forcesMinusTeta.My));

                    double nonLinearErrorTetaBuffer = Math.Max(Math.Max(
                        Math.Abs((adimForcePlusTeta.N + adimForceMinusTeta.N) / 2.0 - adimIteractionPoint.N),
                        Math.Abs((adimForcePlusTeta.Mx + adimForceMinusTeta.Mx) / 2.0 - adimIteractionPoint.Mx)),
                        Math.Abs((adimForcePlusTeta.My + adimForceMinusTeta.My) / 2.0 - adimIteractionPoint.My));

                    if (Math.Abs(nonLinearErrorTetaBuffer) < 0.00001)
                        nonLinearErrorTetaBuffer = 0.00001;

                    nonLinearErrorTeta = Math.Sqrt(Math.Max(Math.Abs(adimForcePlusTeta.N - adimForceMinusTeta.N),
                        Math.Max(Math.Abs(adimForcePlusTeta.Mx - adimForceMinusTeta.Mx),
                        Math.Abs(adimForcePlusTeta.My - adimForceMinusTeta.My))) / Math.Sqrt(nonLinearErrorTetaBuffer));

                    tetaCounter++;
                }
                else
                {
                    if (inputFailureZone == SectionSolver.FailureZones.F1)
                        return (+0.5, +0.0, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                    else
                        return (+0.1, +0.0, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                }

            } while ((dNdTeta == 0.0 && (dMxdTeta == 0.0 || dMydTeta == 0.0)) || (dMxdTeta == 0.0 && dMydTeta == 0.0));


            // derivate parziali rispetto a immersione nel campo
            do
            {
                if (etaCounter < 10)
                {
                    var immersioneNelCampoNext = inputImmersioneNelCampo + dEtaBuffer;
                    var inputFailureZoneNext = inputFailureZone;
                    var immersioneNelCampoPrev = inputImmersioneNelCampo - dEtaBuffer;
                    var inputFailureZonePrev = inputFailureZone;

                    // With the next two while loops, we want to handle the transition to the next field (for example,
                    // the transition from F2B to F3A) in order to find the tangent.
                    // Problem emerged with tests on ACI.
                    while (immersioneNelCampoNext >= 1.0 && (int)inputFailureZoneNext < 6)
                    {
                        immersioneNelCampoNext -= 1.0;
                        inputFailureZoneNext++;
                    }
                    while (immersioneNelCampoNext < 0.0 && (int)inputFailureZoneNext > 1)
                    {
                        immersioneNelCampoNext += 1.0;
                        inputFailureZoneNext--;
                    }

                    _solver.CalculateRotationPointsPerMaterial(inputStrainPlane.Teta, failureDomainType, out List<DeformationFieldsPoint> tensionRotationPoints, out List<DeformationFieldsPoint> tensionRotationPointsF1, out List<DeformationFieldsPoint> compressionRotationPoints, out double minDistanceCompression, out double elasticEpsilonTension);

                    StrainPlane strainPlanePlusdImm = _solver.CalculateStrainPlaneMultiPoints(inputStrainPlane.Teta, inputFailureZoneNext, Math.Min(immersioneNelCampoNext, 1.0), inputFailureZoneNext == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension);
                    StrainPlane strainPlaneMinusdImm = _solver.CalculateStrainPlaneMultiPoints(inputStrainPlane.Teta, inputFailureZonePrev, Math.Max(immersioneNelCampoPrev, 0.0), inputFailureZonePrev == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension);

                    var forcesPlusEta = _solver.CalculateForceResultantForDomain(strainPlanePlusdImm, rebarIsInsideAssociation);
                    var forcesMinusEta = _solver.CalculateForceResultantForDomain(strainPlaneMinusdImm, rebarIsInsideAssociation);

                    dNdImm = (forcesPlusEta.N - forcesMinusEta.N) / (2.0 * dEtaBuffer);
                    dMxdImm = (forcesPlusEta.Mx - forcesMinusEta.Mx) / (2.0 * dEtaBuffer);
                    dMydImm = (forcesPlusEta.My - forcesMinusEta.My) / (2.0 * dEtaBuffer);

                    dEtaBuffer += 10.0 * dEta;

                    var adimForcePlusEta = _solver.ConvertToAdimensionalForces(new ForceTuple(forcesPlusEta.N, forcesPlusEta.Mx, forcesPlusEta.My));
                    var adimForceMinusEta = _solver.ConvertToAdimensionalForces(new ForceTuple(forcesMinusEta.N, forcesMinusEta.Mx, forcesMinusEta.My));

                    double nonLinearErrorEtaBuffer = Math.Max(Math.Max(
                        Math.Abs((adimForcePlusEta.N + adimForceMinusEta.N) / 2.0 - adimIteractionPoint.N),
                        Math.Abs((adimForcePlusEta.Mx + adimForceMinusEta.Mx) / 2.0 - adimIteractionPoint.Mx)),
                        Math.Abs((adimForcePlusEta.My + adimForceMinusEta.My) / 2.0 - adimIteractionPoint.My));

                    if (Math.Abs(nonLinearErrorEtaBuffer) < 0.00001)
                        nonLinearErrorEtaBuffer = 0.00001;

                    nonLinearErrorEta = Math.Sqrt(Math.Max(Math.Abs(adimForcePlusEta.N - adimForceMinusEta.N),
                        Math.Max(Math.Abs(adimForcePlusEta.Mx - adimForceMinusEta.Mx),
                        Math.Abs(adimForcePlusEta.My - adimForceMinusEta.My))) / Math.Sqrt(nonLinearErrorEtaBuffer));

                    etaCounter++;
                }
                else
                {
                    if (inputFailureZone == SectionSolver.FailureZones.F1)
                        return (+0.0, +0.5, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                    else if (inputFailureZone == SectionSolver.FailureZones.F2A)
                        return (+0.0, -0.05, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                    else
                        return (+0.0, +0.01, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                }

            } while ((dNdImm == 0.0 && (dMxdImm == 0.0 || dMydImm == 0.0)) || (dMxdImm == 0.0 && dMydImm == 0.0));

            Vector3d v1 = new Vector3d(dMxdTeta, dMydTeta, dNdTeta);
            v1.Unitize();
            Vector3d v2 = new Vector3d(dMxdImm, dMydImm, dNdImm);
            v2.Unitize();

            // vettore uscente dal punto M di test
            Vector3d gradient = v1 ^ v2;
            gradient.Unitize();

            // k dell'equazione del piano tangente alla superficie in M    //      A*x + B*y + C*z + k = 0
            double k = -(gradient.X * iterationPoint.Mx + gradient.Y * iterationPoint.My + gradient.Z * iterationPoint.N);

            // piano tangente 
            Plane planeTg = new Plane(gradient.X, gradient.Y, gradient.Z, k);

            // punto di intersezione tra raggio delle forze sollecitanti e il piano tangente
            bool intersect = planeTg.IntersectWithRay(externalForcesLine, out Point3d intersectionPoint);

            if (!intersect || double.IsNaN(intersectionPoint.X) || double.IsNaN(intersectionPoint.Y) || double.IsNaN(intersectionPoint.Z))
            {
                if (inputFailureZone == SectionSolver.FailureZones.F1)
                    return (+0.5, +0.5, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                else if (inputFailureZone == SectionSolver.FailureZones.F2A)
                    return (+0.01, -0.1, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                else
                    return (+0.01, +0.1, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
            }
            else
            {
                Vector3d displacementVector = new Vector3d(iterationPoint, intersectionPoint);

                Matrix<double> partialDerivatives = Matrix<double>.Build.Dense(2, 2);
                Matrix<double> inputVector = Matrix<double>.Build.Dense(2, 1);

                if ((dNdTeta != 0 || dNdImm != 0) && (dMxdTeta != 0 || dMxdImm != 0) &&
                    dMxdTeta * dNdImm - dNdTeta * dMxdImm != 0)
                {
                    partialDerivatives[0, 0] = dMxdTeta;
                    partialDerivatives[1, 0] = dNdTeta;

                    partialDerivatives[0, 1] = dMxdImm;
                    partialDerivatives[1, 1] = dNdImm;

                    inputVector[0, 0] = displacementVector.X;
                    inputVector[1, 0] = displacementVector.Z;
                }
                else if ((dNdTeta != 0 || dNdImm != 0) && (dMydTeta != 0 || dMydImm != 0) &&
                    dMydTeta * dNdImm - dNdTeta * dMydImm != 0)
                {
                    partialDerivatives[0, 0] = dMydTeta;
                    partialDerivatives[1, 0] = dNdTeta;

                    partialDerivatives[0, 1] = dMydImm;
                    partialDerivatives[1, 1] = dNdImm;

                    inputVector[0, 0] = displacementVector.Y;
                    inputVector[1, 0] = displacementVector.Z;
                }
                else
                {
                    partialDerivatives[0, 0] = dMxdTeta;
                    partialDerivatives[1, 0] = dMydTeta;

                    partialDerivatives[0, 1] = dMxdImm;
                    partialDerivatives[1, 1] = dMydImm;

                    inputVector[0, 0] = displacementVector.X;
                    inputVector[1, 0] = displacementVector.Y;
                }

                Matrix<double> results = partialDerivatives.Inverse() * inputVector;

                double reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.8, 0.4, nonLinearErrorTeta > 1.0 ? 1.0 : nonLinearErrorTeta);
                double reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.8, 0.4, nonLinearErrorEta > 1.0 ? 1.0 : nonLinearErrorEta);

                double deltaTeta = results[0, 0] * reductionFactorTeta;
                double deltaEta = results[1, 0] * reductionFactorEta;

                return (deltaTeta, deltaEta, displacementVector);
            }
        }

        protected void SetIncrement(SectionSolver.FailureDomainTypes analysisType, ref SectionSolver.FailureZones failureZone, ref double teta, ref double eta, double deltaTeta, double deltaEta)
        {
            deltaEta = deltaEta > 0.30 ? 0.30 : deltaEta;
            deltaEta = deltaEta < -0.30 ? -0.30 : deltaEta;

            deltaTeta = deltaTeta > Math.PI / 16.0 ? Math.PI / 16.0 : deltaTeta;
            deltaTeta = deltaTeta < -Math.PI / 16.0 ? -Math.PI / 16.0 : deltaTeta;

            // piano di nuovo tentativo
            teta += deltaTeta;

            if (failureZone == SectionSolver.FailureZones.F3B && eta + deltaEta < 0)
            {
                deltaEta *= 0.5;
            }
            if (failureZone == SectionSolver.FailureZones.F2A && eta + deltaEta < 0)
            {
                eta = 0.99;
                failureZone = SectionSolver.FailureZones.F1;
                return;
            }

            eta += (deltaEta - (int)deltaEta);
            failureZone += (int)deltaEta;

            switch (analysisType)
            {
                case SectionSolver.FailureDomainTypes.Plastic when _solver.ConcreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;
                    }
                    if (_solver.ConcreteSection.ConcreteMaterial.CompressionStressStrainDiagram == ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock)
                    {
                        if (failureZone == SectionSolver.FailureZones.F2A)
                        {
                            _solver.CalculateRotationPointsPerMaterial(teta, analysisType, out List<DeformationFieldsPoint> tensionRotationPoints, out List<DeformationFieldsPoint> tensionRotationPointsF1, out List<DeformationFieldsPoint> compressionRotationPoints, out double minDistanceCompression, out double elasticEpsilonTension);
                            StrainPlane strainPlane = _solver.CalculateStrainPlaneMultiPoints(teta, failureZone, eta, failureZone == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension);

                            double strain = strainPlane.GetStrain(compressionRotationPoints.Last().Point);
                            if (strain < _solver.ConcreteSection.ConcreteMaterial.StrainYCompression)
                            {
                                failureZone++;
                                eta = 0.10;
                            }
                        }
                    }
                    break;

                case SectionSolver.FailureDomainTypes.Plastic when _solver.ConcreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;

                        if (failureZone == SectionSolver.FailureZones.F3A)
                            eta = 0.99;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;
                    }
                    break;

                case SectionSolver.FailureDomainTypes.Elastic when _solver.ConcreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;

                        if (failureZone == SectionSolver.FailureZones.F3B || failureZone == SectionSolver.FailureZones.F2B)
                            failureZone--;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;

                        if (failureZone == SectionSolver.FailureZones.F2B)
                            failureZone++;
                    }
                    break;

                case SectionSolver.FailureDomainTypes.Elastic when _solver.ConcreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;

                        if (failureZone == SectionSolver.FailureZones.F3B || failureZone == SectionSolver.FailureZones.F2B)
                            failureZone--;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;

                        if (failureZone == SectionSolver.FailureZones.F3B)
                            failureZone++;

                        if (failureZone == SectionSolver.FailureZones.F2B)
                            failureZone++;
                    }
                    break;

                default:
                    throw new Exception();
            }

            failureZone = (int)failureZone < 1 ? SectionSolver.FailureZones.F1 : failureZone;
            failureZone = (int)failureZone > 6 ? SectionSolver.FailureZones.F4 : failureZone;
        }
    }
}
