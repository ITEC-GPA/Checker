using GPC.Checkers.Concrete.Checkers;
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

        public Mesh GetMesh(out Dictionary<MeshVertex, FailureDomainPoint> vertexToDomainPoint)
        {
            return GetMesh(RebuildFailureDomain(), out vertexToDomainPoint);
        }

        internal FailureDomain RebuildFailureDomain(int axialForceSubdivision = 50)
        {
            return RefineFailureDomainAlongTeta(RebuildFailureDomainAlongZAxis(this, axialForceSubdivision, 1.0));
        }

        public FailureDomain RebuildFailureDomain()
        {
            return RebuildFailureDomain(_axialForceSubdivision);
        }

        public Mesh GetMesh(FailureDomain failureDomain, out Dictionary<MeshVertex, FailureDomainPoint> vertexToDomainPoint)
        {
            Mesh mesh = new Mesh();

            int progressVertexId = 1;
            int progressEdgeId = 1;
            int progressPlateId = 1;

            var pointVertexAssociation = new Dictionary<Point3d, MeshVertex>();
            var pointIdAssociation = new Dictionary<MeshVertex, int>();
            vertexToDomainPoint = new Dictionary<MeshVertex, FailureDomainPoint>();

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
                        vertexToDomainPoint.Add(mv, domainPoint[i][j]);
                    }

                    if (vertexId == -1)
                    {
                        throw new NotSupportedException("Vertex id not assigned");
                    }

                }
            }

            for (int i = 0; i < domainPoint.Length; i++) // meridians
            {
                int i1 = i;
                int i2 = i + 1;
                if (i2 == domainPoint.Length)
                    i2 = 0;

                for (int j = 0; j < domainPoint[i].Length - 1; j++) // parallels
                {
                    if (j == 0) // pole
                    {
                        int vA = -1, vB = -1, vC = -1;
                        if (pointVertexAssociation.TryGetValue(domainPoint[i1][j].Point, out MeshVertex meshVertexA))
                            vA = pointIdAssociation[meshVertexA];
                        if (pointVertexAssociation.TryGetValue(domainPoint[i1][j + 1].Point, out MeshVertex meshVertexB))
                            vB = pointIdAssociation[meshVertexB];
                        if (pointVertexAssociation.TryGetValue(domainPoint[i2][j + 1].Point, out MeshVertex meshVertexC))
                            vC = pointIdAssociation[meshVertexC];

                        if (vA != -1 && vB != -1 && vC != -1)
                            mesh.Faces.Build(new MeshFace(vA, vB, vC), progressPlateId++);

                        // only left side
                        if (vA != -1 && vB != -1)
                            mesh.Edges.Build(new MeshEdge(vA, vB), progressEdgeId++);
                    }
                    else if (j == domainPoint[i].Length - 2) // pole
                    {
                        int vA = -1, vB = -1, vC = -1;
                        if (pointVertexAssociation.TryGetValue(domainPoint[i1][j].Point, out MeshVertex meshVertexA))
                            vA = pointIdAssociation[meshVertexA];
                        if (pointVertexAssociation.TryGetValue(domainPoint[i1][j + 1].Point, out MeshVertex meshVertexB))
                            vB = pointIdAssociation[meshVertexB];
                        if (pointVertexAssociation.TryGetValue(domainPoint[i2][j].Point, out MeshVertex meshVertexC))
                            vC = pointIdAssociation[meshVertexC];

                        if (vA != -1 && vB != -1 && vC != -1)
                            mesh.Faces.Build(new MeshFace(vA, vB, vC), progressPlateId++);

                        // upper and left side
                        if (vC != -1 && vA != -1)
                            mesh.Edges.Build(new MeshEdge(vC, vA), progressEdgeId++);
                        if (vA != -1 && vB != -1)
                            mesh.Edges.Build(new MeshEdge(vA, vB), progressEdgeId++);
                    }
                    else
                    {
                        int vA = -1, vB = -1, vC = -1, vD = -1;
                        if (pointVertexAssociation.TryGetValue(domainPoint[i1][j].Point, out MeshVertex meshVertexA))
                            vA = pointIdAssociation[meshVertexA];
                        if (pointVertexAssociation.TryGetValue(domainPoint[i1][j + 1].Point, out MeshVertex meshVertexB))
                            vB = pointIdAssociation[meshVertexB];
                        if (pointVertexAssociation.TryGetValue(domainPoint[i2][j].Point, out MeshVertex meshVertexC))
                            vC = pointIdAssociation[meshVertexC];
                        if (pointVertexAssociation.TryGetValue(domainPoint[i2][j + 1].Point, out MeshVertex meshVertexD))
                            vD = pointIdAssociation[meshVertexD];

                        if (vA != -1 && vB != -1 && vD != -1)
                            mesh.Faces.Build(new MeshFace(vA, vB, vD), progressPlateId++);
                        if (vA != -1 && vD != -1 && vC != -1)
                            mesh.Faces.Build(new MeshFace(vA, vD, vC), progressPlateId++);

                        // upper, middle and left side
                        if (vC != -1 && vA != -1)
                            mesh.Edges.Build(new MeshEdge(vC, vA), progressEdgeId++);
                        if (vA != -1 && vD != -1)
                            mesh.Edges.Build(new MeshEdge(vA, vD), progressEdgeId++);
                        if (vA != -1 && vB != -1)
                            mesh.Edges.Build(new MeshEdge(vA, vB), progressEdgeId++);
                    }
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
            (double maximum, double minimum) = GetAxialForceLimits(out FailureDomainPoint maxPoint, out FailureDomainPoint minPoint);

            double deltaN = (maximum - minimum - 100) / (axialForceSubdivision - 1);

            FailureDomainPoint[][] newDomain = new FailureDomainPoint[failureDomain.DomainPoints.Length][];

            double[] axialForces = new double[axialForceSubdivision + 1];
            for (int dEta = 0; dEta < axialForceSubdivision; dEta++)
            {
                axialForces[dEta] = maximum - dEta * deltaN;
            }

            axialForces[axialForceSubdivision] = minimum;


            for (int dTeta = 0; dTeta < failureDomain.DomainPoints.Length; dTeta++)
            {
                int[] startingCount = new int[axialForceSubdivision + 2];
                newDomain[dTeta] = new FailureDomainPoint[axialForceSubdivision + 1];

                for (int dEta = 0; dEta < axialForceSubdivision + 1; dEta++)
                {
                    double nRd = axialForces[dEta];
                    double mxRd;
                    double myRd;

                    for (int i = startingCount[dEta]; i < failureDomain.DomainPoints[dTeta].Length; i++)
                    {
                        if (i == failureDomain.DomainPoints[dTeta].Length - 1 || dEta == axialForceSubdivision ||
                            (failureDomain.DomainPoints[dTeta][i].NRd - minimum) < tolerance)
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
                                failureDomain.DomainPoints[dTeta][i].StrainPlane, failureDomain.DomainPoints[dTeta][i].Immersione);
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
                                    failureDomain.DomainPoints[dTeta][i].NRd > 0.7 * minimum &&
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
                                    failureDomain.DomainPoints[dTeta][i].StrainPlane, failureDomain.DomainPoints[dTeta][i].Immersione);
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
                    if (Math.Abs(DomainPoints[i][j].NRd - min) < 100)
                        minList.Add(DomainPoints[i][j]);

                    if (Math.Abs(DomainPoints[i][j].NRd - max) < 1000)
                        maxList.Add(DomainPoints[i][j]);
                }
            }

            if (minList.Count > 0)
            {
                ForceTuple forceMin = new ForceTuple(minList.Select(i => i.NRd).Average(), minList.Select(i => i.MxRd).Average(), minList.Select(i => i.MyRd).Average());
                minimumPoint = new FailureDomainPoint(forceMin, minList.FirstOrDefault().FailureIndex, minList.FirstOrDefault().StrainPlane, minList.FirstOrDefault().Immersione);
            }
            if (maxList.Count > 0)
            {
                ForceTuple forceMax = new ForceTuple(maxList.Select(i => i.NRd).Average(), maxList.Select(i => i.MxRd).Average(), maxList.Select(i => i.MyRd).Average());
                maximumPoint = new FailureDomainPoint(forceMax, maxList.FirstOrDefault().FailureIndex, maxList.FirstOrDefault().StrainPlane, maxList.FirstOrDefault().Immersione);
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
            private readonly double _immersione;

            #endregion

            #region Properties

            public double NRd => _forceTuple.N;

            public double MxRd => _forceTuple.Mx;

            public double MyRd => _forceTuple.My;

            public Point3d Point => _forceTuple;

            /// <summary>
            /// Point in the resistance domain.
            /// </summary>
            public ForceTuple ForceTuple => _forceTuple;

            /// <summary>
            /// Deformation plane generating the resistance point in the domain.
            /// </summary>
            public StrainPlane StrainPlane => _strainPlane;

            /// <summary>
            /// Failure field of the domain resistance point.
            /// </summary>
            public SectionSolver.FailureZones FailureIndex => _failureIndex;

            /// <summary>
            /// Parameter of immersion in the failure field.
            /// </summary>
            public double Immersione => _immersione;

            public double WorkingRatio
            {
                get => _workingRatio;
                set => _workingRatio = value;
            }

            #endregion

            #region Constructor

            internal FailureDomainPoint(ForceTuple forceTuple, SectionSolver.FailureZones failureIndex, StrainPlane strainPlane, double immersione)
            {
                _forceTuple = forceTuple;
                _failureIndex = failureIndex;
                _strainPlane = strainPlane;
                _immersione = immersione;
                _workingRatio = -1;
            }

            internal FailureDomainPoint(SerializationInfo info, StreamingContext context)
            {
                _forceTuple = (ForceTuple)info.GetValue("ForceTuple", typeof(ForceTuple));
                _strainPlane = (StrainPlane)info.GetValue("StrainPlane", typeof(StrainPlane));
                _failureIndex = (SectionSolver.FailureZones)info.GetValue("FailureIndex", typeof(SectionSolver.FailureZones));
                _immersione = info.GetDouble("Immersione");
                _workingRatio = -1;
            }

            /// <summary>
            /// Given a solicitation finds the point on the strength domain and work rate based on the search method of approaching the surface.
            /// The calculation of the strain plane is by interpolation and is much less accurate than the iterative/direct method.
            /// This method is good for always finding an working ratio, which is always in favor of safety.
            /// If the starting mesh does not have too many elements then it is also a very performing method.
            /// </summary>
            /// <param name="domainMesh">Complete domain in mesh form.</param>
            /// <param name="failureAnalysisType">Method of approaching the surface.</param>
            /// <param name="resultBeamForce"></param>
            public FailureDomainPoint(Mesh domainMesh, ResultBeamForces resultBeamForce,
                in Dictionary<MeshVertex, FailureDomainPoint> vertexToDomainPoint, SectionChecker sectionChecker,
                SectionSolver.FailureDomainTypes failureDomainType, double lenghtTolerance = GeometryBase.Tolerance)
            {
                _workingRatio = -1;

                Point3d rayOrigin = null; // Must be inside the mesh volume.
                var sectionSolver = sectionChecker.SectionSolver;
                var failureAnalysisType = sectionChecker.SectionCheckerOptions.FailureAnalysisType;

                switch (failureAnalysisType)
                {
                    case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                        rayOrigin = Point3d.Origin;
                        break;

                    case SectionSolver.FailureAnalysisTypes.ConstantN:
                        rayOrigin = new Point3d(0.0, 0.0, resultBeamForce.N);
                        break;

                    case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                        rayOrigin = new Point3d(resultBeamForce.M1, resultBeamForce.M2, 0.0);
                        break;

                    case SectionSolver.FailureAnalysisTypes.ConstantNMx:
                        rayOrigin = new Point3d(resultBeamForce.M1, 0.0, resultBeamForce.N);
                        break;

                    case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                        rayOrigin = new Point3d(0.0, resultBeamForce.M2, resultBeamForce.N);
                        break;
                }

                // For some surface approach methods there may not be an intersection, for these cases we need to do a control
                // specifically to change the actual surface approach method used.
                // The origin of the ray rayOrigin will also determine the ratio and must be internal to the domain.
                if (failureAnalysisType != SectionSolver.FailureAnalysisTypes.ConstantEccentricity || Point3d.Origin.DistanceTo(rayOrigin) > lenghtTolerance)
                {
                    double rayOriginWorkingRatioOrigin = workingRatioSearch(Point3d.Origin, rayOrigin, out _);
                    // If the origin point of the ray is outside then enforce the use of ConstantEccentricity.
                    if (rayOriginWorkingRatioOrigin >= 1.0)
                        rayOrigin = Point3d.Origin;
                }

                // Now the working ratio search.
                var forcePoint = new Point3d(resultBeamForce.M1, resultBeamForce.M2, resultBeamForce.N);
                _workingRatio = workingRatioSearch(rayOrigin, forcePoint, out KeyValuePair<Point3d, MeshBase> intersection);

                // If a solution has been found assigns the deformation plane.
                if (_workingRatio != -1 && intersection.Value != null)
                {
                    _forceTuple = new ForceTuple(intersection.Key.Z, intersection.Key.X, intersection.Key.Y);

                    if (intersection.Value is MeshVertex intersectionVertex)
                    {
                        var failDomainPoint = vertexToDomainPoint[intersectionVertex];
                        _failureIndex = failDomainPoint.FailureIndex;
                        _immersione = failDomainPoint.Immersione;
                        _strainPlane = failDomainPoint.StrainPlane;
                        return;
                    }
                    else if (intersection.Value is MeshEdge intersectionEdge)
                    {
                        // Calculates linear interpolation weights.
                        var vA = domainMesh.Vertices[intersectionEdge.A];
                        var vB = domainMesh.Vertices[intersectionEdge.B];
                        double distB = vB.Point.DistanceTo(forcePoint);
                        double distA = vA.Point.DistanceTo(forcePoint);
                        double weightA = distB / (distA + distB);
                        double weightB = distA / (distA + distB);

                        // Get failure domain points.
                        var failA = vertexToDomainPoint[vA];
                        var failB = vertexToDomainPoint[vB];

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
                            var immA = GetImmersione(failA);
                            var immB = GetImmersione(failB);
                            _immersione = immA * weightA + immB * weightB;

                            // StrainPlane
                            _strainPlane = BuildPlane(theta);
                        }
                    }
                    else if (intersection.Value is MeshFace intersectionFace)
                    {
                        // Calculates linear interpolation weights.
                        var vA = domainMesh.Vertices[intersectionFace.A];
                        var vB = domainMesh.Vertices[intersectionFace.B];
                        var vC = domainMesh.Vertices[intersectionFace.C];
                        double areaA = new Vector3d((vB.Point - forcePoint) ^ (vC.Point - forcePoint)).Length;
                        double areaB = new Vector3d((vC.Point - forcePoint) ^ (vA.Point - forcePoint)).Length;
                        double areaC = new Vector3d((vA.Point - forcePoint) ^ (vB.Point - forcePoint)).Length;
                        double areaTOT = areaA + areaB + areaC;
                        double weightA = areaA / areaTOT;
                        double weightB = areaB / areaTOT;
                        double weightC = areaC / areaTOT;

                        // Get failure domain points.
                        var failA = vertexToDomainPoint[vA];
                        var failB = vertexToDomainPoint[vB];
                        var failC = vertexToDomainPoint[vC];

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
                            var immA = GetImmersione(failA);
                            var immB = GetImmersione(failB);
                            var immC = GetImmersione(failC);
                            _immersione = immA * weightA + immB * weightB + immC * weightC;

                            // StrainPlane
                            _strainPlane = BuildPlane(theta);
                        }
                    }
                }

                // Internal utility.
                double workingRatioSearch(Point3d pointOrigin, Point3d pointToSearch, out KeyValuePair<Point3d, MeshBase> meshIntersection)
                {
                    Point3d targetPoint;
                    bool isRatioZero = pointOrigin.DistanceTo(pointToSearch) < lenghtTolerance;

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
                                targetPoint = pointOrigin + new Point3d(0.0, 1000000.0, 0.0);
                                break;

                            case SectionSolver.FailureAnalysisTypes.ConstantN:
                            case SectionSolver.FailureAnalysisTypes.ConstantNMy:
                                targetPoint = pointOrigin + new Point3d(1000000.0, 0.0, 0.0);
                                break;

                            case SectionSolver.FailureAnalysisTypes.ConstantEccentricity:
                            case SectionSolver.FailureAnalysisTypes.ConstantMxMy:
                            default:
                                targetPoint = pointOrigin + new Point3d(0.0, 0.0, 1000.0);
                                break;
                        }
                    }
                    Line3d semiRay = new Line3d(pointOrigin, targetPoint);
                    var intersOnDomain = domainMesh.GetIntersectionWihtSemiInfiniteRay(semiRay, true, lenghtTolerance);
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

                // Internal utility.
                double GetImmersione(FailureDomainPoint fail)
                {
                    return fail.Immersione != 0.0 || fail.FailureIndex <= _failureIndex ? fail.Immersione : 1.0;
                }

                // Internal utility. Build StrainPlane.
                StrainPlane BuildPlane(double theta)
                {
                    var distances = sectionSolver.CalculateMaxMinSectionDistances(theta);
                    var p1 = sectionSolver.GetP1(distances, failureDomainType, _failureIndex);
                    var p2 = sectionSolver.GetP2(distances, failureDomainType);
                    var p3 = sectionSolver.GetP3(distances, failureDomainType);
                    var p4 = sectionSolver.GetP4(distances, failureDomainType);
                    var p5 = sectionSolver.GetP5(distances, failureDomainType);
                    var p6 = sectionSolver.GetP6(distances, failureDomainType);
                    return sectionSolver.CalculateStrainPlane(theta, _failureIndex, _immersione, p1, p2, p3, p4, p5, p6);
                }
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
                info.AddValue("Immersione", _immersione);
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