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
	public class FailureDomain2d : ModelObject
	{
		protected readonly FailureDomain.FailureDomainPoint[] _domainPoints;
		protected readonly Dictionary<FailureDomain.FailureDomainPoint, Point2d> _domainPoints2dAssociation;
		protected readonly FailureDomainResult2d.DomainTypes _domainType;

		internal FailureDomain.FailureDomainPoint[] DomainPoints => _domainPoints;
		
		internal Dictionary<FailureDomain.FailureDomainPoint, Point2d> DomainPoints2dAssociation => _domainPoints2dAssociation;
				

		internal FailureDomain2d(FailureDomain.FailureDomainPoint[] domainPoints, FailureDomainResult2d.DomainTypes domainType)
		{
			_domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
			_domainType = domainType;
			_domainPoints2dAssociation = new Dictionary<FailureDomain.FailureDomainPoint, Point2d>();
			CalculateDomainPoints2dAssociation();
		}


		internal (FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) GetDomainPoint(Point2d point)
		{
			return GetDomainPoint(point.X, point.Y);	
		}

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
				Line2d line = new Line2d(new Point2d(0, 0), new Point2d(x, y));
				Line2d edge = new Line2d(_domainPoints2dAssociation[_domainPoints[index]], _domainPoints2dAssociation[_domainPoints[index + 1]]);

				if (edge.GetIntersectionWithInfiniteLine(line, out Point2d intersection))
				{
					if (_domainType == FailureDomainResult2d.DomainTypes.CostantN)
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
			if(_domainType == FailureDomainResult2d.DomainTypes.CostantN)
			{
				for (int i = 0; i < _domainPoints.Length; i++)
					_domainPoints2dAssociation.Add(_domainPoints[i], new Point2d(_domainPoints[i].MxRd, _domainPoints[i].MyRd));
			}
			else // caso Mx/My costante
			{
				for (int i = 0; i < _domainPoints.Length; i++)
					_domainPoints2dAssociation.Add(_domainPoints[i],
						new Point2d(Math.Sqrt(Math.Pow(_domainPoints[i].MxRd, 2) + Math.Pow(_domainPoints[i].MyRd, 2)), _domainPoints[i].NRd));				
			}
		}

		protected CoordinateSystem GetCoordinateSystem()
		{
			if (_domainType == FailureDomainResult2d.DomainTypes.CostantN)
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
	}
}