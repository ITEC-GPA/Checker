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

		#endregion

		#region Constructor

		public FailureDomain(FailureDomainPoint[][] domainPoints, SectionSolver.FailureDomainTypes analysisType)
		{
			_domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
			_axialForceSubdivision = 100;
			_analysisType = analysisType;
		}

		protected FailureDomain(SerializationInfo info, StreamingContext context) 
			: base(info, context)
		{
			_axialForceSubdivision = info.GetInt32("AxialForceSubdivision");
			_analysisType = (SectionSolver.FailureDomainTypes)info.GetValue("AnalysisType", typeof(SectionSolver.FailureDomainTypes));
			_domainPoints = (FailureDomainPoint[][])info.GetValue("FailureDomainPoints", typeof(FailureDomainPoint[][]));
		}

		#endregion

		#region Mesh Method

		public Mesh GetMesh()
		{
			return GetMesh(RebuildFailureDomain());
		}

		internal FailureDomain RebuildFailureDomain(int axialForceSubdivision = 50)
		{
			return RefineFailureDomainAlongTeta(RebuildFailureDomainAlongZAxis(this, axialForceSubdivision));
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

					if(pointVertexAssociation.ContainsKey(domainPoint[i][j].Point))
					{
						vertexId = pointIdAssociation[mv];
						commonPoint = true;
					}

					if(!commonPoint)
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

		protected FailureDomain RebuildFailureDomainAlongZAxis(FailureDomain failureDomain, int axialForceSubdivision = 50, double tolerance = 0.1)
		{
			(double maximum, double minimum) limits = GetAxialForceLimits(out FailureDomainPoint maxPoint, out FailureDomainPoint minPoint);

			double deltaN = (limits.maximum - limits.minimum) /axialForceSubdivision;

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
									failureDomain.DomainPoints[dTeta][i].NRd > 0.7 * limits.minimum)
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
					if (DomainPoints[i][j].NRd < min)
					{
						min = DomainPoints[i][j].NRd;
						minimumPoint = DomainPoints[i][j];
					}
					if (DomainPoints[i][j].NRd > max)
					{
						max = DomainPoints[i][j].NRd;
						maximumPoint = DomainPoints[i][j];
					}
				}
			}

			List< FailureDomainPoint > minList = new List<FailureDomainPoint>();
			List< FailureDomainPoint > maxList = new List<FailureDomainPoint>();

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
				StrainPlane strainPlane = new StrainPlane(new Point2d(
					minList.Select(i => i.StrainPlane.ReferencePoint.X).Average(),
					minList.Select(i => i.StrainPlane.ReferencePoint.Y).Average()),
					minList.Select(i => i.StrainPlane.Teta).Average(), 
					minList.Select(i => i.StrainPlane.Chi).Average(),
					minList.Select(i => i.StrainPlane.StrainReferencePoint).Average());
				minimumPoint = new FailureDomainPoint(forceMin, minimumPoint.FailureIndex, strainPlane);
			}
			if (maxList.Count > 0)
			{
				ForceTuple forceMax = new ForceTuple(maxList.Select(i => i.NRd).Average(), maxList.Select(i => i.MxRd).Average(), maxList.Select(i => i.MyRd).Average());
				StrainPlane strainPlane = new StrainPlane(new Point2d(
					maxList.Select(i => i.StrainPlane.ReferencePoint.X).Average(),
					maxList.Select(i => i.StrainPlane.ReferencePoint.Y).Average()),
					maxList.Select(i => i.StrainPlane.Teta).Average(), 
					maxList.Select(i => i.StrainPlane.Chi).Average(),
					maxList.Select(i => i.StrainPlane.StrainReferencePoint).Average());
				maximumPoint = new FailureDomainPoint(forceMax, maximumPoint.FailureIndex, strainPlane);
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
		}

		#endregion

		#region Setter

		public void SetFailureDomainType(SectionSolver.FailureDomainTypes failureDomainType)
		{
			_analysisType= failureDomainType;
		}

		public void SetAxialForceSubdivision(int subdivision)
		{
			_axialForceSubdivision = subdivision;
		}

		#endregion

		#region Nested Class

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
				: base(forces.N, forces.V1, forces.V2, forces.T, forces.M1, forces.M2, forces.CoordinateSystem, forces.Id)
			{
				_failureDomainPoint = failureDomainPoint;
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
		}

		#endregion

		#region FailureDomainPoint

		[Serializable]
		public sealed class FailureDomainPoint : ISerializable, IEquatable<FailureDomainPoint>
		{
			#region Variables

			private readonly ForceTuple _forceTuple;
			private readonly SectionSolver.FailureZones _failureIndex;
			private readonly StrainPlane _strainPlane;

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

			#endregion

			#region Constructor

			internal FailureDomainPoint(ForceTuple forceTuple, SectionSolver.FailureZones failureIndex, StrainPlane strainPlane)
			{
				_forceTuple = forceTuple;
				_failureIndex = failureIndex;
				_strainPlane = strainPlane;
			}

			internal FailureDomainPoint(SerializationInfo info, StreamingContext context)
			{
				_forceTuple = (ForceTuple)info.GetValue("ForceTuple", typeof(ForceTuple));
				_strainPlane = (StrainPlane)info.GetValue("StrainPlane", typeof(StrainPlane));
				_failureIndex = (SectionSolver.FailureZones)info.GetValue("FailureIndex", typeof(SectionSolver.FailureZones));
			}

			#endregion

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

		#region FailureDomainPoint2d

		public sealed class FailureDomainPoint2d : FailureDomainForce, ISerializable, IEquatable<FailureDomainPoint2d>
		{
			#region Variables

			private readonly Point2d _point2d;

			#endregion

			#region Properties

			public Point3d Point2d => _point2d;

			#endregion

			#region Constructor

			internal FailureDomainPoint2d(ResultBeamForces forces, FailureDomainPoint failureDomainPoint, Point2d point2D)
				:base(forces, failureDomainPoint)
			{
				_point2d = point2D;
			}

			internal FailureDomainPoint2d(FailureDomainForce forces, Point2d point2D)
				:this(new ResultBeamForces(forces.N, forces.V1, forces.V2, forces.T, forces.M1, forces.M2, forces.CoordinateSystem, forces.Id),forces.FailureDomainPoint, point2D)
			{

			}

			internal FailureDomainPoint2d(SerializationInfo info, StreamingContext context)
				:base(info, context)
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

			public bool Equals(FailureDomainPoint2d other)
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

			public static bool operator ==(FailureDomainPoint2d left, FailureDomainPoint2d right)
			{
				return EqualityComparer<FailureDomainPoint2d>.Default.Equals(left, right);
			}

			public static bool operator !=(FailureDomainPoint2d left, FailureDomainPoint2d right)
			{
				return !(left == right);
			}

			#endregion
		}

		#endregion

		#endregion
	}
}