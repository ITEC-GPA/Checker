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
	public class FailureDomain : ModelObject
	{
		protected readonly int _axialForceSubdivision;
		protected readonly int _tetaSubdivision;
		protected readonly FailureDomainPoint[][] _domainPoints;
		protected readonly SectionSolver.FailureDomainAnalysisTypes _analysisType;

		public FailureDomainPoint[][] DomainPoints => _domainPoints;

		internal SectionSolver.FailureDomainAnalysisTypes FailureDomainAnalysisTypes => _analysisType;

		public FailureDomain(FailureDomainPoint[][] domainPoints, SectionSolver.FailureDomainAnalysisTypes analysisType)
		{
			_domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
			_axialForceSubdivision = 50;
			_tetaSubdivision = domainPoints.Length;
			_analysisType = analysisType;
		}

		public Mesh GetMesh()
		{
			return GetMesh(RebuildFailureDomain(_axialForceSubdivision, _tetaSubdivision));
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
						pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j].Point]],
						pointIdAssociation[pointVertexAssociation[domainPoint[i + 1][j + 1].Point]]), progressPlateId++
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
						pointIdAssociation[pointVertexAssociation[domainPoint[i][j + 1].Point]],
						pointIdAssociation[pointVertexAssociation[domainPoint[0][j + 1].Point]]), progressPlateId++
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

		protected FailureDomain RebuildFailureDomain(int axialForceSubdivision = 20, int tetaSubdivion = 32, double tolerance = 0.01)
		{
			double deltaN = (_domainPoints[0][0].NRd - _domainPoints[0][_domainPoints[0].Length - 1].NRd) / axialForceSubdivision;
			FailureDomainPoint[][] newDomain = new FailureDomainPoint[tetaSubdivion][];

			for (int dTeta = 0; dTeta < tetaSubdivion; dTeta++)
			{
				int[] startingCount = new int[axialForceSubdivision + 1];
				newDomain[dTeta] = new FailureDomainPoint[axialForceSubdivision + 1];

				for (int dEta = 0; dEta < axialForceSubdivision + 1; dEta++)
				{
					double nRd = _domainPoints[0][0].NRd - dEta * deltaN;
					double mxRd;
					double myRd;

					for (int i = startingCount[dEta]; i < _domainPoints[dTeta].Length; i++)
					{
						if (_domainPoints[dTeta][i].NRd >= nRd - tolerance &&
							_domainPoints[dTeta][i + 1].NRd <= nRd + tolerance)
						{
							if (Math.Abs(_domainPoints[dTeta][i].NRd - _domainPoints[dTeta][i + 1].NRd) < tolerance)
							{
								mxRd = _domainPoints[dTeta][i].MxRd;
								myRd = _domainPoints[dTeta][i].MyRd;
							}
							else if(i + 2 < _domainPoints[dTeta].Length && _analysisType == SectionSolver.FailureDomainAnalysisTypes.Plastic)
							{
								mxRd = Interpolation.GetQuadraticInterpolation(
									_domainPoints[dTeta][i].NRd, _domainPoints[dTeta][i + 1].NRd, _domainPoints[dTeta][i + 2].NRd, 
									_domainPoints[dTeta][i].MxRd, _domainPoints[dTeta][i + 1].MxRd, _domainPoints[dTeta][i + 2].MxRd, nRd);
								myRd = Interpolation.GetQuadraticInterpolation(
									_domainPoints[dTeta][i].NRd, _domainPoints[dTeta][i + 1].NRd, _domainPoints[dTeta][i + 2].NRd,
									_domainPoints[dTeta][i].MyRd, _domainPoints[dTeta][i + 1].MyRd, _domainPoints[dTeta][i + 2].MyRd, nRd);
							}
							else
							{
								mxRd = Interpolation.GetLinearInterpolation(_domainPoints[dTeta][i].NRd,
									_domainPoints[dTeta][i + 1].NRd, _domainPoints[dTeta][i].MxRd, _domainPoints[dTeta][i + 1].MxRd, nRd);
								myRd = Interpolation.GetLinearInterpolation(_domainPoints[dTeta][i].NRd,
									_domainPoints[dTeta][i + 1].NRd, _domainPoints[dTeta][i].MyRd, _domainPoints[dTeta][i + 1].MyRd, nRd);
							}

							newDomain[dTeta][dEta] = new FailureDomainPoint(new ForceTuple(nRd, mxRd, myRd), _domainPoints[dTeta][i].FailureIndex,
								_domainPoints[dTeta][i].StrainPlane);
							startingCount[dEta] = i;
							break;
						}
					}
				}
			}

			return new FailureDomain(newDomain, _analysisType);
		}

		[Serializable]
		public sealed class FailureDomainForce : ResultBeamForces, ISerializable, IEquatable<FailureDomainForce>
		{
			private readonly FailureDomainPoint _failureDomainPoint;

			public FailureDomainPoint FailureDomainPoint => _failureDomainPoint;

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
		}

		[Serializable]
		public sealed class FailureDomainPoint : ISerializable, IEquatable<FailureDomainPoint>
		{
			private readonly ForceTuple _forceTuple;
			private readonly SectionSolver.FailureZones _failureIndex;
			private readonly StrainPlane _strainPlane;

			public double NRd => _forceTuple.N;

			public double MxRd => _forceTuple.Mx;

			public double MyRd => _forceTuple.My;

			public Point3d Point => _forceTuple;

			public ForceTuple ForceTuple => _forceTuple;

			public StrainPlane StrainPlane => _strainPlane;

			/// <inheritdoc cref="SectionSolver.FailureZones"/>
			public SectionSolver.FailureZones FailureIndex => _failureIndex;


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
		}
	}
}