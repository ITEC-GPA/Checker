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
	[Serializable]
	public class FailureDomain2d : ModelObject, ISerializable
	{
		#region Variables

		protected readonly FailureDomain.FailureDomainPoint[] _domainPoints;
		protected readonly Dictionary<FailureDomain.FailureDomainPoint, Point2d> _domainPoints2dAssociation;
		protected readonly FailureDomainResult2d.DomainTypes _domainType;

		#endregion

		#region Properties

		public FailureDomain.FailureDomainPoint[] DomainPoints => _domainPoints;

		/// <summary>
		/// Dictionary of association between 3d domain points and 2d domain points
		/// </summary>
		public Dictionary<FailureDomain.FailureDomainPoint, Point2d> DomainPoints2dAssociation => _domainPoints2dAssociation;

		public FailureDomainResult2d.DomainTypes DomainType => _domainType;

		#endregion

		#region Constructor

		public FailureDomain2d(FailureDomain.FailureDomainPoint[] domainPoints, FailureDomainResult2d.DomainTypes domainType)
		{
			_domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
			_domainType = domainType;
			_domainPoints2dAssociation = new Dictionary<FailureDomain.FailureDomainPoint, Point2d>();
			CalculateDomainPoints2dAssociation();
		}

		protected FailureDomain2d(SerializationInfo info, StreamingContext context) 
			: base(info, context)
		{
			_domainPoints = (FailureDomain.FailureDomainPoint[])info.GetValue("FailureDomainPoints", typeof(FailureDomain.FailureDomainPoint[]));
			_domainType = (FailureDomainResult2d.DomainTypes)info.GetValue("DomainType", typeof(FailureDomainResult2d.DomainTypes));
			_domainPoints2dAssociation = (Dictionary<FailureDomain.FailureDomainPoint, Point2d>)info.GetValue("DomainPoints2dAssociation", 
				typeof(Dictionary<FailureDomain.FailureDomainPoint, Point2d>));
		}

		#endregion

		#region Internal Methods

		/// <summary>
		/// Return the domain point for input 2d forces <paramref name="point"/>
		/// </summary>
		/// <param name="point"></param>
		/// <returns></returns>
		internal (FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) GetDomainPoint(Point2d point)
		{
			return GetDomainPoint(point.X, point.Y);	
		}

		/// <summary>
		/// Return the domain point for input 2d forces <paramref name="x"/>, <paramref name="y"/>
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		protected (FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) GetDomainPoint(double x, double y)
		{
			double teta = Math.Atan2(x, y);
			int index = -1;

			for (int i = 0; i < _domainPoints.Length; i++)
			{
				Point2d point1;
				Point2d point2;

				if(i != _domainPoints.Length - 1)
				{
					point1 = _domainPoints2dAssociation[_domainPoints[i]];
					point2 = _domainPoints2dAssociation[_domainPoints[i + 1]];
				}
				else
				{
					point1 = _domainPoints2dAssociation[_domainPoints[i]];
					point2 = _domainPoints2dAssociation[_domainPoints[0]];
				}


				double t1 = Math.Atan2(point1.X, point1.Y);
				double t2 = Math.Atan2(point2.X, point2.Y);

				if (Math.Sign(teta - t1) != Math.Sign(teta - t2) && 
					((Math.Sign(x) == Math.Sign(point1.X) || Math.Abs(point1.X) < 1) && 
					((Math.Sign(y) == Math.Sign(point1.Y)) || Math.Abs(point1.Y) < 1)))
				{
					if(Math.Abs(teta) > Math.Abs(t1) && Math.Abs(teta) < Math.Abs(t2) ||
						Math.Abs(teta) < Math.Abs(t1) && Math.Abs(teta) > Math.Abs(t2))
					{
						index = i;
						break;
					}					
				}
			}

			if (index != -1)
			{
				int endIndex = index + 1;
				if (index == _domainPoints.Length - 1)
					endIndex = 0;
				Line2d line = new Line2d(new Point2d(0, 0), new Point2d(x, y));
				Line2d edge = new Line2d(_domainPoints2dAssociation[_domainPoints[index]], _domainPoints2dAssociation[_domainPoints[endIndex]]);

				if (edge.GetIntersectionWithInfiniteLine(line, out Point2d intersection))
				{
					if (_domainType == FailureDomainResult2d.DomainTypes.ConstantN)
						return (new FailureDomain.FailureDomainPoint(new ForceTuple(_domainPoints[index].NRd, intersection.X, intersection.Y),
							_domainPoints[index].FailureIndex, _domainPoints[index].StrainPlane), intersection);
					else
					{
						CoordinateSystem coordinateSystem = GetCoordinateSystem();
						var pointGlobalCoordinate = coordinateSystem.ToGlobal(intersection);
						return (new FailureDomain.FailureDomainPoint(new ForceTuple(intersection.Y, pointGlobalCoordinate.X, pointGlobalCoordinate.Y), 
							_domainPoints[index].FailureIndex, _domainPoints[index].StrainPlane), intersection);
					}
				}
			}

			return (new FailureDomain.FailureDomainPoint(new ForceTuple(), SectionSolver.FailureZones.F1, 
				new StrainPlane(0, 0, new Point2d(0,0), 0)), new Point2d());
		}

		protected void CalculateDomainPoints2dAssociation()
		{
			// caso N costante
			if(_domainType == FailureDomainResult2d.DomainTypes.ConstantN)
			{
				for (int i = 0; i < _domainPoints.Length; i++)
					if (_domainPoints[i] != null)
						_domainPoints2dAssociation.Add(_domainPoints[i], new Point2d(_domainPoints[i].MxRd, _domainPoints[i].MyRd));
			}
			else // caso Mx/My costante
			{
				for (int i = 0; i < _domainPoints.Length; i++)
					if (_domainPoints[i] != null)
					{
						double sign = +1;
						if (i > 0)
						{
							if (Math.Abs(_domainPoints[i].MxRd) < 1 && Math.Abs(_domainPoints[i].MyRd) < 1)
								sign = +1;
							else if (Math.Abs(_domainPoints[i].MxRd) > 1)
								sign *= Math.Sign(_domainPoints[i].MxRd);
							else if (Math.Abs(_domainPoints[i].MyRd) > 1)
								sign *= Math.Sign(_domainPoints[i].MyRd);
						}
						if(!_domainPoints2dAssociation.ContainsKey(_domainPoints[i]))
							_domainPoints2dAssociation.Add(_domainPoints[i],
								new Point2d(sign * Math.Sqrt(Math.Pow(_domainPoints[i].MxRd, 2) + Math.Pow(_domainPoints[i].MyRd, 2)), _domainPoints[i].NRd));
					}
			}
		}

		protected CoordinateSystem GetCoordinateSystem()
		{
			if (_domainType == FailureDomainResult2d.DomainTypes.ConstantN)
			{
				return new CoordinateSystem(new Point3d(0, 0, _domainPoints[0].NRd), Vector3d.XAxis, Vector3d.YAxis);
			}
			else
			{
				CoordinateSystem coordinateSystem = CoordinateSystem.Global;
				coordinateSystem.RotateV3(Math.Atan2(-_domainPoints[(int)(_domainPoints.Length / 4.0)].MyRd, 
					_domainPoints[(int)(_domainPoints.Length / 4.0)].MxRd));

				return coordinateSystem;
			}
		}

		#endregion

		#region Equals, hashcode, operators

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("FailureDomainPoints", _domainPoints);
			info.AddValue("DomainType", _domainType);
			info.AddValue("DomainPoints2dAssociation", _domainPoints2dAssociation);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;

			return obj is FailureDomain2d domain &&
				   base.Equals(obj) &&
				   EqualityComparer<FailureDomain.FailureDomainPoint[]>.Default.Equals(_domainPoints, domain._domainPoints) &&
				   _domainType == domain._domainType;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = 23;
				hashCode = hashCode * -17 + base.GetHashCode();
				hashCode = hashCode * -17 + EqualityComparer<FailureDomain.FailureDomainPoint[]>.Default.GetHashCode(_domainPoints);
				hashCode = hashCode * -17 + _domainType.GetHashCode();
				return hashCode;
			}
		}

		#endregion
	}
}