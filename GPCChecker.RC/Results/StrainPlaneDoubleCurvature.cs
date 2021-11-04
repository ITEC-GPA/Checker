using GPC.Geometry;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using GPC.Model;

namespace GPC.Checkers.ReinforcedConcrete.Results
{
	/// <summary>
	/// The strain plane - epsilon(x,y) = epsilon0 - chi * ((-sin(teta)*x + cos(teta)*y)
	/// </summary>
	public class StrainPlaneDoubleCurvature : ModelObjectId, IStrainPlane
	{
		protected readonly Point3d _referencePoint;
		protected readonly double _chiX;
		protected readonly double _chiY;
		protected readonly double _strainReferencePoint;

		/// <summary>
		/// The point where is set <see cref="StrainReferencePoint"/>
		/// </summary>
		public Point3d ReferencePoint => _referencePoint;

		public double ChiX => _chiX;

		public double ChiY => _chiY;

		/// <summary>
		/// The value of the strain in the <see cref="ReferencePoint"/>
		/// </summary>
		public double StrainReferencePoint => _strainReferencePoint;



		public StrainPlaneDoubleCurvature(double chiX, double chiY, Point3d centerOfStrainPlane,  double epsilonCenterOfStrainPlane, int id = IDUNASSIGNED, string name = "")
			:base(id, name)
		{
			_referencePoint = centerOfStrainPlane;
			_chiX = chiX;
			_chiY = chiY;
			_strainReferencePoint = epsilonCenterOfStrainPlane;
		}

		public StrainPlaneDoubleCurvature(SerializationInfo info, StreamingContext context)
			:base(info, context)
		{
			_referencePoint = (Point3d)info.GetValue("ReferecePoint", typeof(Point3d));
			_chiX = info.GetDouble("ChiX");
			_chiY = info.GetDouble("ChiY");
			_strainReferencePoint = info.GetDouble("StrainReferencePoint");			
		}


		public override bool Equals(object obj)
		{
			return obj is StrainPlaneDoubleCurvature plane &&
				   EqualityComparer<Point3d>.Default.Equals(_referencePoint, plane._referencePoint) &&
				   _chiX == plane._chiX &&
				   _chiY == plane._chiY &&
				   _strainReferencePoint == plane._strainReferencePoint;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = -23;
				hashCode = hashCode * -17 + EqualityComparer<Point3d>.Default.GetHashCode(_referencePoint);
				hashCode = hashCode * -17 + _chiX.GetHashCode();
				hashCode = hashCode * -17 + _chiY.GetHashCode();
				hashCode = hashCode * -17 + _strainReferencePoint.GetHashCode();
				return hashCode;
			}
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("ReferecePoint", _referencePoint, typeof(Point3d));
			info.AddValue("ChiX", _chiX, typeof(double));
			info.AddValue("ChiY", _chiY, typeof(double));
			info.AddValue("StrainReferencePoint", _strainReferencePoint, typeof(double));
		}

		public StrainPlane ConvertToStrainPlane()
		{
			double teta = -Math.Atan2(ChiX, ChiY);
			double chi;

			if (Math.Abs(teta) < GeometryBase.GetDefaultTolerance() ||
				Math.Abs(Math.Abs(teta) - Math.PI) < GeometryBase.GetDefaultTolerance())
				chi = -ChiY / Math.Cos(teta);

			else if (Math.Abs(Math.Abs(teta) - Math.PI / 2.0) < GeometryBase.GetDefaultTolerance() ||
				Math.Abs(Math.Abs(teta) - 3.0 * Math.PI / 2.0) < GeometryBase.GetDefaultTolerance())
				chi = ChiX / Math.Sin(teta);

			else
				chi = - ChiY / Math.Cos(teta);

			return new StrainPlane(ReferencePoint, teta, chi, StrainReferencePoint, Id, Name);
		}
	}
}
