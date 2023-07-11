using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model;
using GPC.Model.Results;
using GPC.Utilities.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace GPC.Checkers.Concrete.Results
{
    [Serializable]
    public class FailureDomain : ModelObject, ISerializable
    {
        #region Variables

        protected int _axialForceSubdivision;
        protected readonly FailureDomainPoint[][] _domainPoints;
        protected SectionSolver.FailureDomainTypes _analysisType;

        #endregion

        #region Properties

        public FailureDomainPoint[][] DomainPoints => _domainPoints;

        public SectionSolver.FailureDomainTypes FailureDomainAnalysisTypes => _analysisType;

        /// <summary>
        /// In 3d domain force use of linear interpolation instead of quadratic.
        /// </summary>
        public bool ForceLinearInterpolation { get; set; }

        #endregion

        #region Constructor

        public FailureDomain(FailureDomainPoint[][] domainPoints, SectionSolver.FailureDomainTypes analysisType)
        {
            _domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
            _axialForceSubdivision = 50;
            _analysisType = analysisType;
            ForceLinearInterpolation = false;
        }

        protected FailureDomain(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _axialForceSubdivision = info.GetInt32("AxialForceSubdivision");
            _analysisType = (SectionSolver.FailureDomainTypes)info.GetValue("AnalysisType", typeof(SectionSolver.FailureDomainTypes));
            _domainPoints = (FailureDomainPoint[][])info.GetValue("FailureDomainPoints", typeof(FailureDomainPoint[][]));
            ForceLinearInterpolation = info.GetBoolean("ForceLinearInterpolation");
        }

        #endregion

        #region Mesh Method

        public Mesh GetMesh()
        {
            return GetMesh(RebuildFailureDomain());
        }

        internal FailureDomain RebuildFailureDomain(int axialForceSubdivision = 50)
        {
            return RefineFailureDomainAlongTeta(RebuildFailureDomainAlongZAxis(this, axialForceSubdivision, 1.0));
        }

        public FailureDomain RebuildFailureDomain()
        {
            return RebuildFailureDomain(_axialForceSubdivision);
        }

        protected Mesh GetMesh(FailureDomain failureDomain)
        {
            Mesh mesh = new Mesh();

            int progressVertexId = 1;
            int progressEdgeId = 1;
            int progressPlateId = 1;

            Dictionary<Point3d, MeshVertex> pointVertexAssociation = new Dictionary<Point3d, MeshVertex>();
            Dictionary<MeshVertex, int> pointIdAssociation = new Dictionary<MeshVertex, int>();

            FailureDomainPoint[][] domainPoint = failureDomain.DomainPoints;

            for (int i = 0; i < domainPoint.Length; i++)
            {
                for (int j = 0; j < domainPoint[i].Length; j++)
                {
                    bool commonPoint = false;
                    int vertexId = -1;

                    MeshVertex mv = new MeshVertex(domainPoint[i][j].Point);

                    if (pointVertexAssociation.ContainsKey(domainPoint[i][j].Point))
                    {
                        vertexId = pointIdAssociation[mv];
                        commonPoint = true;
                    }

                    if (!commonPoint)
                    {
                        if (!pointIdAssociation.ContainsKey(mv))
                        {
                            vertexId = mesh.Vertices.Build(mv, progressVertexId++);
                        }
                        else
                        {
                            vertexId = pointIdAssociation[mv];
                        }

                        pointIdAssociation.Add(mv, vertexId);
                        pointVertexAssociation.Add(domainPoint[i][j].Point, mv);
                    }

                    if (vertexId == -1)
                    {
                        throw new NotSupportedException("Vertex id not assigned");
                    }

                }
            }

            for (int i = 0; i < domainPoint.Length - 1; i++)
            {
                for (int j = 0; j < domainPoint[i].Length - 2; j++)
                {
                    mesh.Faces.Build(new MeshFace
                    (
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j + 1].Point]]), progressPlateId++
                    );

                    mesh.Faces.Build(new MeshFace
                    (
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j].Point]]), progressPlateId++
                    );

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j + 1].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j + 1].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]]), progressEdgeId++);
                }

                for (int j = domainPoint[i].Length - 2; j < domainPoint[i].Length - 1; j++)
                {
                    mesh.Faces.Build(new MeshFace
                    (
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j].Point]]), progressPlateId++
                    );

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j + 1].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j].Point]]), progressEdgeId++);
                }
            }

            for (int i = domainPoint.Length - 1; i < domainPoint.Length; i++)
            {
                for (int j = 0; j < domainPoint[i].Length - 2; j++)
                {
                    mesh.Faces.Build(new MeshFace
                    (
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j + 1].Point]]), progressPlateId++
                    );

                    mesh.Faces.Build(new MeshFace
                    (
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j].Point]]), progressPlateId++
                    );

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j + 1].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[0][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j + 1].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[0][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[0][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]]), progressEdgeId++);
                }

                for (int j = domainPoint[i].Length - 2; j < domainPoint[i].Length - 1; j++)
                {
                    mesh.Faces.Build(new MeshFace
                    (
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j].Point]]), progressPlateId++
                    );

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i][j].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j + 1].Point]]), progressEdgeId++);

                    mesh.Edges.Build(new MeshEdge(pointIdAssociation[pointVertexAssociation[domainPoint[0][j + 1].Point]],
                        pointIdAssociation[pointVertexAssociation[domainPoint[0][j].Point]]), progressEdgeId++);
                }
            }

            return mesh;
        }

        protected FailureDomain RefineFailureDomainAlongTeta(FailureDomain failureDomain)
        {
            // TODO: implementare 
            return failureDomain;
        }

        protected FailureDomain RebuildFailureDomainAlongZAxis(FailureDomain failureDomain, int axialForceSubdivision = 50, double tolerance = 1)
        {
            (double maximum, double minimum) limits = GetAxialForceLimits(out FailureDomainPoint maxPoint, out FailureDomainPoint minPoint);

            double deltaN = (limits.maximum - limits.minimum) / axialForceSubdivision;

            FailureDomainPoint[][] newDomain = new FailureDomainPoint[failureDomain.DomainPoints.Length][];

            for (int dTeta = 0; dTeta < failureDomain.DomainPoints.Length; dTeta++)
            {
                int[] startingCount = new int[axialForceSubdivision + 2];
                newDomain[dTeta] = new FailureDomainPoint[axialForceSubdivision + 1];

                for (int dEta = 0; dEta < axialForceSubdivision + 1; dEta++)
                {
                    double nRd = limits.maximum - dEta * deltaN;
                    double mxRd;
                    double myRd;

                    for (int i = startingCount[dEta]; i < failureDomain.DomainPoints[dTeta].Length; i++)
                    {
                        if (i == failureDomain.DomainPoints[dTeta].Length - 1)
                        {
                            newDomain[dTeta][dEta] = minPoint;
                            break;
                        }
                        else if (i == 0 && _analysisType == SectionSolver.FailureDomainTypes.Plastic)
                        {
                            newDomain[dTeta][dEta] = maxPoint;
                            startingCount[dEta + 1] = i + 1;
                            break;
                        }
                        else if (i > 0 && failureDomain.DomainPoints[dTeta][0].NRd <= nRd)
                        {
                            mxRd = Interpolation.GetLinearInterpolation(maxPoint.NRd, failureDomain.DomainPoints[dTeta][0].NRd,
                                maxPoint.MxRd, failureDomain.DomainPoints[dTeta][0].MxRd, nRd);
                            myRd = Interpolation.GetLinearInterpolation(maxPoint.NRd, failureDomain.DomainPoints[dTeta][0].NRd,
                                maxPoint.MyRd, failureDomain.DomainPoints[dTeta][0].MyRd, nRd);

                            newDomain[dTeta][dEta] = new FailureDomainPoint(new ForceTuple(nRd, mxRd, myRd), failureDomain.DomainPoints[dTeta][i].FailureIndex,
                                failureDomain.DomainPoints[dTeta][i].StrainPlane);
                            startingCount[dEta + 1] = i - 1;
                            break;
                        }
                        else
                        {
                            if ((failureDomain.DomainPoints[dTeta][i].NRd >= nRd - tolerance &&
                                failureDomain.DomainPoints[dTeta][i + 1].NRd <= nRd + tolerance) ||
                                (failureDomain.DomainPoints[dTeta][i].NRd <= nRd - tolerance &&
                                failureDomain.DomainPoints[dTeta][i + 1].NRd >= nRd + tolerance))
                            {
                                if (Math.Abs(failureDomain.DomainPoints[dTeta][i].NRd - failureDomain.DomainPoints[dTeta][i + 1].NRd) < tolerance)
                                {
                                    mxRd = failureDomain.DomainPoints[dTeta][i].MxRd;
                                    myRd = failureDomain.DomainPoints[dTeta][i].MyRd;
                                }
                                else if (i + 2 < failureDomain.DomainPoints[dTeta].Length &&
                                    _analysisType == SectionSolver.FailureDomainTypes.Plastic &&
                                    failureDomain.DomainPoints[dTeta][i].NRd < 0.0 &&
                                    failureDomain.DomainPoints[dTeta][i].NRd > 0.7 * limits.minimum &&
                                    !ForceLinearInterpolation)
                                {
                                    mxRd = Interpolation.GetQuadraticInterpolation(
                                        failureDomain.DomainPoints[dTeta][i].NRd, failureDomain.DomainPoints[dTeta][i + 1].NRd, failureDomain.DomainPoints[dTeta][i + 2].NRd,
                                        failureDomain.DomainPoints[dTeta][i].MxRd, failureDomain.DomainPoints[dTeta][i + 1].MxRd, failureDomain.DomainPoints[dTeta][i + 2].MxRd, nRd);
                                    myRd = Interpolation.GetQuadraticInterpolation(
                                        failureDomain.DomainPoints[dTeta][i].NRd, failureDomain.DomainPoints[dTeta][i + 1].NRd, failureDomain.DomainPoints[dTeta][i + 2].NRd,
                                        failureDomain.DomainPoints[dTeta][i].MyRd, failureDomain.DomainPoints[dTeta][i + 1].MyRd, failureDomain.DomainPoints[dTeta][i + 2].MyRd, nRd);
                                }
                                else
                                {
                                    mxRd = Interpolation.GetLinearInterpolation(failureDomain.DomainPoints[dTeta][i].NRd,
                                        failureDomain.DomainPoints[dTeta][i + 1].NRd, failureDomain.DomainPoints[dTeta][i].MxRd, failureDomain.DomainPoints[dTeta][i + 1].MxRd, nRd);
                                    myRd = Interpolation.GetLinearInterpolation(failureDomain.DomainPoints[dTeta][i].NRd,
                                        failureDomain.DomainPoints[dTeta][i + 1].NRd, failureDomain.DomainPoints[dTeta][i].MyRd, failureDomain.DomainPoints[dTeta][i + 1].MyRd, nRd);
                                }

                                newDomain[dTeta][dEta] = new FailureDomainPoint(new ForceTuple(nRd, mxRd, myRd), failureDomain.DomainPoints[dTeta][i].FailureIndex,
                                    failureDomain.DomainPoints[dTeta][i].StrainPlane);
                                startingCount[dEta + 1] = i;
                                break;
                            }
                        }
                    }
                }
            }

            return new FailureDomain(newDomain, _analysisType);
        }

        protected (double maximum, double minimum) GetAxialForceLimits(out FailureDomainPoint maximumPoint, out FailureDomainPoint minimumPoint)
        {
            maximumPoint = DomainPoints[0][0];
            minimumPoint = DomainPoints[0][DomainPoints[0].Length - 1];

            double min = minimumPoint.NRd;
            double max = maximumPoint.NRd;

            for (int i = 0; i < DomainPoints.Length; i++)
            {
                for (int j = 0; j < DomainPoints[i].Length; j++)
                {
                    if (DomainPoints[i][j].NRd <= min)
                    {
                        min = DomainPoints[i][j].NRd;
                        minimumPoint = DomainPoints[i][j];
                    }
                    if (DomainPoints[i][j].NRd >= max)
                    {
                        max = DomainPoints[i][j].NRd;
                        maximumPoint = DomainPoints[i][j];
                    }
                }
            }

            List<FailureDomainPoint> minList = new List<FailureDomainPoint>();
            List<FailureDomainPoint> maxList = new List<FailureDomainPoint>();

            for (int i = 0; i < DomainPoints.Length; i++)
            {
                for (int j = 0; j < DomainPoints[i].Length; j++)
                {
                    if (Math.Abs(DomainPoints[i][j].NRd - min) < 1000)
                        minList.Add(DomainPoints[i][j]);

                    if (Math.Abs(DomainPoints[i][j].NRd - max) < 1000)
                        maxList.Add(DomainPoints[i][j]);
                }
            }

            if (minList.Count > 0)
            {
                ForceTuple forceMin = new ForceTuple(minList.Select(i => i.NRd).Average(), minList.Select(i => i.MxRd).Average(), minList.Select(i => i.MyRd).Average());
                minimumPoint = new FailureDomainPoint(forceMin, minList.FirstOrDefault().FailureIndex, minList.FirstOrDefault().StrainPlane);
            }
            if (maxList.Count > 0)
            {
                ForceTuple forceMax = new ForceTuple(maxList.Select(i => i.NRd).Average(), maxList.Select(i => i.MxRd).Average(), maxList.Select(i => i.MyRd).Average());
                maximumPoint = new FailureDomainPoint(forceMax, maxList.FirstOrDefault().FailureIndex, maxList.FirstOrDefault().StrainPlane);
            }

            return (max, min);
        }

        #endregion

        #region Equals, hashcode, operators

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is FailureDomain domain &&
                   base.Equals(obj) &&
                   EqualityComparer<FailureDomainPoint[][]>.Default.Equals(_domainPoints, domain._domainPoints) &&
                   _analysisType == domain._analysisType;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + EqualityComparer<FailureDomainPoint[][]>.Default.GetHashCode(_domainPoints);
                hashCode = hashCode * -17 + _analysisType.GetHashCode();
                return hashCode;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("AxialForceSubdivision", _axialForceSubdivision);
            info.AddValue("AnalysisType", _analysisType);
            info.AddValue("FailureDomainPoints", _domainPoints);
            info.AddValue("ForceLinearInterpolation", ForceLinearInterpolation);
        }

        #endregion

        #region Setter

        public void SetFailureDomainType(SectionSolver.FailureDomainTypes failureDomainType)
        {
            _analysisType = failureDomainType;
        }

        public void SetAxialForceSubdivision(int subdivision)
        {
            _axialForceSubdivision = subdivision;
        }

        #endregion

        #region Nested Class

        #region FailureDomainPoint

        [Serializable]
        public sealed class FailureDomainPoint : ISerializable, IEquatable<FailureDomainPoint>
        {
            #region Variables

            private readonly ForceTuple _forceTuple;
            private readonly SectionSolver.FailureZones _failureIndex;
            private readonly StrainPlane _strainPlane;
            private double _workingRatio;

            #endregion

            #region Properties

            public double NRd => _forceTuple.N;

            public double MxRd => _forceTuple.Mx;

            public double MyRd => _forceTuple.My;

            public Point3d Point => _forceTuple;

            public ForceTuple ForceTuple => _forceTuple;

            public StrainPlane StrainPlane => _strainPlane;

            /// <inheritdoc cref="SectionSolver.FailureZones"/>
            public SectionSolver.FailureZones FailureIndex => _failureIndex;

            public double WorkingRatio
            {
                get => _workingRatio;
                set => _workingRatio = value;
            }

            #endregion

            #region Constructor

            internal FailureDomainPoint(ForceTuple forceTuple, SectionSolver.FailureZones failureIndex, StrainPlane strainPlane)
            {
                _forceTuple = forceTuple;
                _failureIndex = failureIndex;
                _strainPlane = strainPlane;
                _workingRatio = -1;
            }

            internal FailureDomainPoint(SerializationInfo info, StreamingContext context)
            {
                _forceTuple = (ForceTuple)info.GetValue("ForceTuple", typeof(ForceTuple));
                _strainPlane = (StrainPlane)info.GetValue("StrainPlane", typeof(StrainPlane));
                _failureIndex = (SectionSolver.FailureZones)info.GetValue("FailureIndex", typeof(SectionSolver.FailureZones));
                _workingRatio = -1;
            }

            #endregion

            /// <summary>
            /// Calculate the working ratio for the force <paramref name="resultBeamForce"/> 
            /// </summary>
            /// <param name="resultBeamForce"></param>
            /// <param name="SCALE_M">Factor for moments units scale</param>
            /// <param name="SCALE_N">Factor for axial force units scale</param>
            /// <returns>-1 if the procedure is failed, the working ratio otherwise</returns>
            public double CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes failureAnalysisType, ResultBeamForces resultBeamForce, double SCALE_M, double SCALE_N)
            {
                _workingRatio = -1;
                if (SCALE_M <= 0 || SCALE_N <= 0 || resultBeamForce == null)
                    return _workingRatio;

                switch (failureAnalysisType)
                {
                    case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                        _workingRatio = new Vector3d(resultBeamForce.M1 / SCALE_M, resultBeamForce.M2 / SCALE_M, resultBeamForce.N / SCALE_N).Length /
                            ((Vector3d)new Point3d(Point.X / SCALE_M, Point.Y / SCALE_M, Point.Z / SCALE_N)).Length;
                        break;

                    case SectionSolver.FailureAnalysisTypes.ConstantN:
                        _workingRatio = new Vector3d(resultBeamForce.M1 / SCALE_M, resultBeamForce.M2 / SCALE_M, 0).Length /
                            ((Vector3d)new Point3d(Point.X / SCALE_M, Point.Y / SCALE_M, 0)).Length;
                        break;

                    case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                        _workingRatio = new Vector3d(0, 0, resultBeamForce.N / SCALE_N).Length /
                            ((Vector3d)new Point3d(0, 0, Point.Z / SCALE_N)).Length;
                        break;

                    case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                        _workingRatio = new Vector3d(0, resultBeamForce.M2 / SCALE_M, 0).Length /
                            ((Vector3d)new Point3d(0, Point.Y / SCALE_M, 0)).Length;
                        break;

                    case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                        _workingRatio = new Vector3d(resultBeamForce.M1 / SCALE_M, 0, 0).Length /
                            ((Vector3d)new Point3d(Point.X / SCALE_M, 0, 0)).Length;
                        break;
                }
                return _workingRatio;
            }

            public bool CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes failureAnalysisType, ResultBeamForces resultBeamForce, double SCALE_M, double SCALE_N, out double workingRatio)
            {
                CalculateWorkingRatio(failureAnalysisType, resultBeamForce, SCALE_M, SCALE_N);
                workingRatio = _workingRatio;

                if (_workingRatio == -1)
                    return false;
                else
                    return true;
            }

            #region Equals, hashcode, operators

            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("ForceTuple", _forceTuple, typeof(ForceTuple));
                info.AddValue("StrainPlane", _strainPlane, typeof(StrainPlane));
                info.AddValue("FailureIndex", _failureIndex, typeof(SectionSolver.FailureZones));
            }

            public override bool Equals(object obj)
            {
                return Equals((FailureDomainPoint)obj);
            }

            public bool Equals(FailureDomainPoint other)
            {
                return other != null && _forceTuple.Equals(other._forceTuple) &&
                    _failureIndex == other._failureIndex && _strainPlane.Equals(other._strainPlane);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 17;
                    hashCode = hashCode * -23 + _forceTuple.GetHashCode();
                    hashCode = hashCode * -23 + _failureIndex.GetHashCode();
                    hashCode = hashCode * -23 + _strainPlane.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(FailureDomainPoint left, FailureDomainPoint right)
            {
                return EqualityComparer<FailureDomainPoint>.Default.Equals(left, right);
            }

            public static bool operator !=(FailureDomainPoint left, FailureDomainPoint right)
            {
                return !(left == right);
            }

            #endregion
        }

        #endregion

        #region FailureDomainForce

        [Serializable]
        public class FailureDomainForce : ResultBeamForces, ISerializable
        {
            #region Variables

            private readonly FailureDomainPoint _failureDomainPoint;

            #endregion

            #region Properties

            public FailureDomainPoint FailureDomainPoint => _failureDomainPoint;

            #endregion

            #region Constructor

            public FailureDomainForce(ResultBeamForces forces, FailureDomainPoint failureDomainPoint)
                : base(forces.N, forces.V1, forces.V2, forces.T, forces.M1, forces.M2, forces.CoordinateSystem, forces.Id, forces.Name)
            {
                if (forces is null)
                    throw new ArgumentNullException(nameof(forces));

                _failureDomainPoint = failureDomainPoint ?? throw new ArgumentNullException(nameof(failureDomainPoint));
            }

            internal FailureDomainForce(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
                _failureDomainPoint = (FailureDomainPoint)info.GetValue("FailureDomainPoint", typeof(FailureDomainPoint));
            }

            #endregion

            #region Equals, hashcode, operators

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("FailureDomainPoint", _failureDomainPoint, typeof(FailureDomainPoint));
            }

            public override bool Equals(object obj)
            {
                return Equals((FailureDomainForce)obj);
            }

            public bool Equals(FailureDomainForce other)
            {
                return other != null && base.Equals(other) && _failureDomainPoint.Equals(other._failureDomainPoint);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 17;
                    hashCode = hashCode * -29 + _failureDomainPoint.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(FailureDomainForce left, FailureDomainForce right)
            {
                return EqualityComparer<FailureDomainForce>.Default.Equals(left, right);
            }

            public static bool operator !=(FailureDomainForce left, FailureDomainForce right)
            {
                return !(left == right);
            }

            #endregion

            /// <summary>
            /// Calculate the working ratio 
            /// </summary>
            /// <param name="scale_M">Factor for moments units scale</param>
            /// <param name="scale_N">Factor for axial force units scale</param>
            /// <returns></returns>
            public double CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes failureAnalysisType, double scale_M, double scale_N)
            {
                return _failureDomainPoint.CalculateWorkingRatio(failureAnalysisType, this, scale_M, scale_N);
            }

            /// <summary>
            /// Calculate the working ratio 
            /// </summary>
            /// <param name="scale_M">Factor for moments units scale</param>
            /// <param name="scale_N">Factor for axial force units scale</param>
            /// <param name="workingRatio">The working ratio</param>
            /// <returns>True if the procedure is successful, false otherwise</returns>
            public bool CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes failureAnalysisType, double scale_M, double scale_N, out double workingRatio)
            {
                workingRatio = _failureDomainPoint.CalculateWorkingRatio(failureAnalysisType, this, scale_M, scale_N);
                if (workingRatio == -1)
                    return false;
                else
                    return true;
            }
        }

        #endregion

        #region FailureDomainForce2d

        public sealed class FailureDomainForce2d : FailureDomainForce, ISerializable, IEquatable<FailureDomainForce2d>
        {
            #region Variables

            private readonly Point2d _point2d;

            #endregion

            #region Properties

            public Point3d Point2d => _point2d;

            #endregion

            #region Constructor

            internal FailureDomainForce2d(ResultBeamForces forces, FailureDomainPoint failureDomainPoint, Point2d point2D)
                : base(forces, failureDomainPoint)
            {
                _point2d = point2D;
            }

            internal FailureDomainForce2d(FailureDomainForce forces, Point2d point2D)
                : this(new ResultBeamForces(forces.N, forces.V1, forces.V2, forces.T, forces.M1, forces.M2, forces.CoordinateSystem, forces.Id, forces.Name), forces.FailureDomainPoint, point2D)
            {

            }

            internal FailureDomainForce2d(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
                _point2d = (Point2d)info.GetValue("Point2d", typeof(Point2d));
            }

            #endregion

            #region Equals, hashcode, operators

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);
                info.AddValue("Point2d", _point2d, typeof(Point2d));
            }

            public override bool Equals(object obj)
            {
                return Equals((FailureDomainPoint)obj);
            }

            public bool Equals(FailureDomainForce2d other)
            {
                return other != null &&
                    _point2d == other._point2d;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 17;
                    hashCode = hashCode * -23 + _point2d.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(FailureDomainForce2d left, FailureDomainForce2d right)
            {
                return EqualityComparer<FailureDomainForce2d>.Default.Equals(left, right);
            }

            public static bool operator !=(FailureDomainForce2d left, FailureDomainForce2d right)
            {
                return !(left == right);
            }

            #endregion

            #region Method

            public double CalculateWorkingRatio(FailureDomainResult2d.DomainTypes domainType, double SCALE_M, double SCALE_N)
            {
                if (SCALE_M <= 0 || SCALE_N <= 0)
                    return -1;

                switch (domainType)
                {
                    case FailureDomainResult2d.DomainTypes.ConstantMxMy:
						ForceTuple force2d = ConvertForceToForceTuple2d(domainType, new ForceTuple(N, M1, M2));
						return new Vector3d(force2d.Mx / SCALE_M, force2d.N / SCALE_N, 0).Length /
							((Vector3d)new Point3d(Point2d.X / SCALE_M, Point2d.Y / SCALE_N, 0)).Length;

                    case FailureDomainResult2d.DomainTypes.ConstantN:
                        return new Vector3d(M1 / SCALE_M, M2 / SCALE_M, 0).Length /
                            ((Vector3d)new Point3d(Point2d.X / SCALE_M, Point2d.Y / SCALE_M, 0)).Length;

                    default:
                        return -1;
                }
			}

			public Point2d ConvertForceToPoint(FailureDomainResult2d.DomainTypes domainType, ForceTuple forceTuple)
			{
				if (domainType == FailureDomainResult2d.DomainTypes.ConstantN)
					return new Point2d(forceTuple.Mx, forceTuple.My);
				else
					return new Point2d(forceTuple.N, forceTuple.Mx);
			}

			public ForceTuple ConvertForceToForceTuple2d(FailureDomainResult2d.DomainTypes domainType, ForceTuple forceTuple)
			{
				if (domainType == FailureDomainResult2d.DomainTypes.ConstantN)
					return new ForceTuple(forceTuple.Mx, forceTuple.My, 0);
				else
					return new ForceTuple(forceTuple.N, forceTuple.Mx, 0);
            }

            #endregion
        }

        #endregion

        #endregion
    }
}