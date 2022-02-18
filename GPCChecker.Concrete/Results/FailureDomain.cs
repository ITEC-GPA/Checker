using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model;
using GPC.Model.Results;
using GPC.Utilities.Maths;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GPC.Checkers.Concrete.Results
{
	[Serializable]
	public class FailureDomain : ModelObject, ISerializable
	{
		#region Variables

		protected readonly int _axialForceSubdivision;
		protected readonly FailureDomainPoint[][] _domainPoints;
		protected readonly SectionSolver.FailureDomainAnalysisTypes _analysisType;

		#endregion

		#region Properties

		public FailureDomainPoint[][] DomainPoints => _domainPoints;

		internal SectionSolver.FailureDomainAnalysisTypes FailureDomainAnalysisTypes => _analysisType;

		#endregion

		#region Constructor

		public FailureDomain(FailureDomainPoint[][] domainPoints, SectionSolver.FailureDomainAnalysisTypes analysisType)
		{
			_domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
			_axialForceSubdivision = 50;
			_analysisType = analysisType;
		}

		protected FailureDomain(SerializationInfo info, StreamingContext context) 
			: base(info, context)
		{
			_axialForceSubdivision = info.GetInt32("AxialForceSubdivision");
			_analysisType = (SectionSolver.FailureDomainAnalysisTypes)info.GetValue("AnalysisType", typeof(SectionSolver.FailureDomainAnalysisTypes));
			_domainPoints = (FailureDomainPoint[][])info.GetValue("FailureDomainPoints", typeof(FailureDomainPoint[][]));
		}

		#endregion

		#region Mesh Method

		public Mesh GetMesh()
		{
			return GetMesh(RefineFailureDomainAlongTeta(RebuildFailureDomainAlongZAxis(this, _axialForceSubdivision)));
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

		protected FailureDomain RebuildFailureDomainAlongZAxis(FailureDomain failureDomain, int axialForceSubdivision = 20, double tolerance = 0.01)
		{
			double deltaN = (failureDomain.DomainPoints[0][0].NRd - failureDomain.DomainPoints[0][failureDomain.DomainPoints[0].Length - 1].NRd) / 
				axialForceSubdivision;
			FailureDomainPoint[][] newDomain = new FailureDomainPoint[failureDomain.DomainPoints.Length][];

			for (int dTeta = 0; dTeta < failureDomain.DomainPoints.Length; dTeta++)
			{
				int[] startingCount = new int[axialForceSubdivision + 1];
				newDomain[dTeta] = new FailureDomainPoint[axialForceSubdivision + 1];

				for (int dEta = 0; dEta < axialForceSubdivision + 1; dEta++)
				{
					double nRd = failureDomain.DomainPoints[0][0].NRd - dEta * deltaN;
					double mxRd;
					double myRd;

					for (int i = startingCount[dEta]; i < failureDomain.DomainPoints[dTeta].Length; i++)
					{
						if (failureDomain.DomainPoints[dTeta][i].NRd >= nRd - tolerance &&
							failureDomain.DomainPoints[dTeta][i + 1].NRd <= nRd + tolerance)
						{
							if (Math.Abs(failureDomain.DomainPoints[dTeta][i].NRd - failureDomain.DomainPoints[dTeta][i + 1].NRd) < tolerance)
							{
								mxRd = failureDomain.DomainPoints[dTeta][i].MxRd;
								myRd = failureDomain.DomainPoints[dTeta][i].MyRd;
							}
							else if(i + 2 < failureDomain.DomainPoints[dTeta].Length && 
								_analysisType == SectionSolver.FailureDomainAnalysisTypes.Plastic &&
								failureDomain.DomainPoints[dTeta][i].NRd < 0.0)
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
							startingCount[dEta] = i;
							break;
						}
					}
				}
			}

			return new FailureDomain(newDomain, _analysisType);
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

		#region FailureDomainForce

		[Serializable]
		public sealed class FailureDomainForce : ResultBeamForces, ISerializable, IEquatable<FailureDomainForce>
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
	}
}