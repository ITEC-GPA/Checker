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
	public class StrainPlane : ModelObjectId
	{
		protected readonly Point3d _referencePoint;
		protected readonly double _teta;
		protected readonly double _chi;
		protected readonly double _strainReferencePoint;

		/// <summary>
		/// The point where is set <see cref="StrainReferencePoint"/>
		/// </summary>
		public Point3d ReferencePoint => _referencePoint;

		/// <summary>
		/// The angle between the strain plane and the plane of section
		/// </summary>
		public double Teta => _teta;

		/// <summary>
		/// The curvature of the strain plane
		/// </summary>
		public double Chi => _chi;

		/// <summary>
		/// The value of the strain in the <see cref="ReferencePoint"/>
		/// </summary>
		public double StrainReferencePoint => _strainReferencePoint;

		public double ChiX => Chi * Math.Sin(Teta);

		public double ChiY => - Chi * Math.Cos(Teta);


		public StrainPlane(Point3d centerOfStrainPlane, double teta, double chi, double epsilonCenterOfStrainPlane, int id = IDUNASSIGNED, string name = "")
			:base(id, name)
		{
			_referencePoint = centerOfStrainPlane;
			_teta = teta;
			_chi = chi;
			_strainReferencePoint = epsilonCenterOfStrainPlane;
		}

		public StrainPlane(double chiX, double chiY, Point3d centerOfStrainPlane, double epsilonCenterOfStrainPlane, int id = IDUNASSIGNED, string name = "")
			: base(id, name)
		{
			_referencePoint = centerOfStrainPlane;
			_teta = - Math.Atan2(chiX, chiY);

			if (Math.Abs(_teta) < GeometryBase.GetDefaultTolerance() || 
				Math.Abs(Math.Abs(_teta) - Math.PI) < GeometryBase.GetDefaultTolerance())
				_chi = - chiY / Math.Cos(_teta);

			else if (Math.Abs(Math.Abs(_teta) - Math.PI / 2.0) < GeometryBase.GetDefaultTolerance() ||
				Math.Abs(Math.Abs(_teta) - 3.0 * Math.PI / 2.0) < GeometryBase.GetDefaultTolerance())
				_chi = chiX / Math.Sin(_teta);

			else
				_chi = - chiY / Math.Cos(_teta);

			_strainReferencePoint = epsilonCenterOfStrainPlane;
		}

		public StrainPlane(SerializationInfo info, StreamingContext context)
			:base(info, context)
		{
			_referencePoint = (Point3d)info.GetValue("ReferecePoint", typeof(Point3d));
			_teta = info.GetDouble("Teta");
			_chi = info.GetDouble("Chi");
			_strainReferencePoint = info.GetDouble("StrainReferencePoint");			
		}


		public override bool Equals(object obj)
		{
			return obj is StrainPlane plane &&
				   EqualityComparer<Point3d>.Default.Equals(_referencePoint, plane._referencePoint) &&
				   _teta == plane._teta &&
				   _chi == plane._chi &&
				   _strainReferencePoint == plane._strainReferencePoint;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = -23;
				hashCode = hashCode * -17 + EqualityComparer<Point3d>.Default.GetHashCode(_referencePoint);
				hashCode = hashCode * -17 + _teta.GetHashCode();
				hashCode = hashCode * -17 + _chi.GetHashCode();
				hashCode = hashCode * -17 + _strainReferencePoint.GetHashCode();
				return hashCode;
			}
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("ReferecePoint", _referencePoint, typeof(Point3d));
			info.AddValue("Teta", _teta, typeof(double));
			info.AddValue("Chi", _chi, typeof(double));
			info.AddValue("StrainReferencePoint", _strainReferencePoint, typeof(double));
		}



		///// <summary>
		///// The angle between the strain plane and the plane of section
		///// </summary>
		//public double Teta => _teta;

		///// <summary>
		///// The immersion in the <see cref="FailureIndices"/>
		///// eta = (chi - chi1 ) / (chi2 - chi1)
		///// where: 
		///// chi = curvature of the section 
		///// chi1 = curvature of the section in the previous <see cref="FailureIndices"/> boundary 
		///// chi2 = curvature of the section in the following <see cref="FailureIndices"/> boundary 
		///// </summary>
		//public double Eta => _eta;
	}
}
