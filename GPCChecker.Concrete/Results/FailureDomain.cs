using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model;
using GPC.Model.Results;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GPC.Checkers.Concrete.Results
{
	public class FailureDomain : ModelObject
	{
		protected readonly FailureDomainPoint[][] _domainPoints;

		public FailureDomainPoint[][] DomainPoints => _domainPoints;

		public FailureDomain(FailureDomainPoint[][] domainPoints)
		{
			_domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
		}

		public Mesh GetMesh()
		{
			Mesh mesh = new Mesh();

			for (int i = 0; i < _domainPoints.Length - 1; i++)
			{
				for (int j = 3; j < _domainPoints[i].Length - 2; j++)
				{
					mesh.AddFaceMesh(new Point3d[]
					{
						_domainPoints[i][j].Point,
						_domainPoints[i][j+1].Point,
						_domainPoints[i+1][j+1].Point,
					});

					mesh.AddFaceMesh(new Point3d[]
					{
						_domainPoints[i][j].Point,
						_domainPoints[i+1][j+1].Point,
						_domainPoints[i+1][j].Point,
					});
				}

				for (int j = _domainPoints[i].Length - 2; j < _domainPoints[i].Length - 1; j++)
				{
					mesh.AddFaceMesh(new Point3d[]
					{
						_domainPoints[i][j].Point,
						_domainPoints[i+1][j].Point,
						_domainPoints[i+1][j+1].Point,
					});
				}
			}

			for (int i = _domainPoints.Length - 1; i < _domainPoints.Length; i++)
			{
				for (int j = 3; j < _domainPoints[i].Length - 2; j++)
				{
					mesh.AddFaceMesh(new Point3d[]
					{
						_domainPoints[i][j].Point,
						_domainPoints[i][0].Point,
						_domainPoints[i+1][0].Point,
					});

					mesh.AddFaceMesh(new Point3d[]
					{
						_domainPoints[i][j].Point,
						_domainPoints[i+1][0].Point,
						_domainPoints[i+1][j].Point,
					});
				}

				for (int j = _domainPoints[i].Length - 2; j < _domainPoints[i].Length - 1; j++)
				{
					mesh.AddFaceMesh(new Point3d[]
					{
						_domainPoints[i][j].Point,
						_domainPoints[i+1][j].Point,
						_domainPoints[i+1][0].Point,
					});
				}
			}


			return mesh;
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