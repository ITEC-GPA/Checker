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

		public FailureDomain.FailureDomainPoint[] DomainPoints => _domainPoints;

		public FailureDomain2d(FailureDomain.FailureDomainPoint[] domainPoints)
		{
			_domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
		}

		public virtual FailureDomain.FailureDomainForce GetDomainPointConstantAxialForce(ResultBeamForces forces, Vector2d distanceRefPointToCentroid)
		{
			ForceTuple force = forces.ConvertToForceTuple(distanceRefPointToCentroid);
			Line3d line = new Line3d(new Point3d(0, 0, force.N), new Point3d(force.Mx, force.My, force.N));

			for (int i = 0; i < _domainPoints.Length; i++)
			{
				Line3d edge;

				if(i != _domainPoints.Length - 1)
					edge = new Line3d(_domainPoints[i].Point, _domainPoints[i + 1].Point);
				else
					edge = new Line3d(_domainPoints[i].Point, _domainPoints[0].Point);

				bool intersect = edge.GetIntersectionWithInfiniteLine(line, out Point3d intersection);

				if (intersect)
				{
					if (edge.IsPointOnLine(intersection))
					{
						if(Math.Sign(intersection.Z) == Math.Sign(force.N) && 
							Math.Sign(intersection.X) == Math.Sign(force.Mx) && 
							Math.Sign(intersection.Y) == Math.Sign(force.My))
							return new FailureDomain.FailureDomainForce(forces, new FailureDomain.FailureDomainPoint(new ForceTuple(intersection.Z, 
								intersection.X, intersection.Y), _domainPoints[i].FailureIndex, null));
					}
				}
			}

			return null;
		}

		public Polygon2d GetPolygon()
		{
			Polygon2d polygon = new Polygon2d();

			

			return polygon;
		}

	}
}