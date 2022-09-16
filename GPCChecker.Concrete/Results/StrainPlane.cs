using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using GPC.Geometry;
using GPC.Model;

namespace GPC.Checkers.Concrete.Results
{
    /// <summary>
    /// The strain plane - epsilon(x,y) = epsilon0 - chi * ((-sin(teta)*x + cos(teta)*y)
    /// </summary>

    [Serializable]
    public sealed class StrainPlane : ModelObjectId, ISerializable, IEquatable<StrainPlane>
    {
        #region Variables

        private readonly Point2d _referencePoint;
        private readonly double _chiY;
        private readonly double _chiX;
        private readonly double _strainReferencePoint;

		#endregion

		#region Properties

		/// <summary>
		/// The point where is set <see cref="StrainReferencePoint"/>
		/// </summary>
		public Point2d ReferencePoint => _referencePoint;

        /// <summary>
        /// The angle between the strain plane and the plane of section
        /// </summary>
        public double Teta => CalculateTeta();

        /// <summary>
        /// The curvature of the strain plane
        /// </summary>
        public double Chi => CalculateChi();

        /// <summary>
        /// The value of the strain in the <see cref="ReferencePoint"/>
        /// </summary>
        public double StrainReferencePoint => _strainReferencePoint;

        /// <summary>
        /// The curvature of the strain plane along X axis
        /// </summary>
        public double ChiX => _chiX;

        /// <summary>
        /// The curvature of the strain plane along Y axis
        /// </summary>
        public double ChiY => _chiY;

        #endregion

        #region Constructor

        public StrainPlane(Point2d centerOfStrainPlane, double teta, double chi, double epsilonCenterOfStrainPlane, int id = IDUNASSIGNED, string name = "")
            : base(id, name)
        {
            _referencePoint = centerOfStrainPlane;
            _chiX = chi * Math.Sin(teta);
            _chiY = -chi * Math.Cos(teta);
            _strainReferencePoint = epsilonCenterOfStrainPlane;
        }

        public StrainPlane(double chiX, double chiY, Point2d centerOfStrainPlane, double epsilonCenterOfStrainPlane, int id = IDUNASSIGNED, string name = "")
            : base(id, name)
        {
            _referencePoint = centerOfStrainPlane;
            _chiX = chiX;
            _chiY = chiY;
            _strainReferencePoint = epsilonCenterOfStrainPlane;
        }

        private StrainPlane(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _referencePoint = (Point2d)info.GetValue("ReferecePoint", typeof(Point2d));
            _chiX = info.GetDouble("ChiX");
            _chiY = info.GetDouble("ChiY");
            _strainReferencePoint = info.GetDouble("StrainReferencePoint");
        }

		#endregion

		#region Public Methods

		public double GetStrain(Point2d point)
        {
            return _strainReferencePoint + ChiX * (point.X - _referencePoint.X) + ChiY * (point.Y - _referencePoint.Y);
        }

        public double GetStrain(double x, double y)
        {
            return _strainReferencePoint + ChiX * (x - _referencePoint.X) + ChiY * (y - _referencePoint.Y);
        }

        public Line2d GetNeutralAxisRespectReferencePoint()
        {
            if (_chiX == 0 && _chiY == 0)
                return null;
            else if (_chiY == 0 && _chiX != 0)
                return new Line2d(new Point2d(-StrainReferencePoint / _chiX, 100.0), new Point2d(-StrainReferencePoint / _chiX, -100.0));
            else if (_chiX == 0 && _chiY != 0)
                return new Line2d(new Point2d(100.0, -StrainReferencePoint / _chiY), new Point2d(-100.0, -StrainReferencePoint / _chiY));
            else
            {
				Line2d line = new Line2d(new Point2d(0.0, -StrainReferencePoint / _chiY), new Point2d(-StrainReferencePoint / _chiX, 0.0));
				Vector2d vector = line.ToVector();
                vector.Unitize();

                return new Line2d(new Point2d(0.0, -StrainReferencePoint / _chiY), new Point2d(0.0 + vector.X, -StrainReferencePoint / _chiY + vector.Y));
            }
        }


        public Line2d GetNeutralAxis()
        {
            if (_chiX == 0 && _chiY == 0)
                return null;
            else if (_chiY == 0 && _chiX != 0)
                return new Line2d(new Point2d(-StrainReferencePoint / _chiX, 100.0), new Point2d(-StrainReferencePoint / _chiX, -100.0));
            else if (_chiX == 0 && _chiY != 0)
                return new Line2d(new Point2d(100.0, -StrainReferencePoint / _chiY), new Point2d(-100.0, -StrainReferencePoint / _chiY));
            else
                return new Line2d(new Point2d(0.0, -StrainReferencePoint / _chiY + _chiX / _chiY * _referencePoint.X), 
                    new Point2d(-StrainReferencePoint / _chiX + _chiY / _chiX * _referencePoint.Y, 0.0));
        }

        public Line2d GetConstantStrainAxis(double strain)
        {
            if (_chiX == 0 && _chiY == 0)
                return null;
            else if (_chiY == 0 && _chiX != 0)
                return new Line2d(new Point2d((strain - StrainReferencePoint) / _chiX, 100.0), new Point2d((strain - StrainReferencePoint) / _chiX, -100.0));
            else if (_chiX == 0 && _chiY != 0)
                return new Line2d(new Point2d(100.0, (strain - StrainReferencePoint) / _chiY), new Point2d(-100.0, (strain - StrainReferencePoint) / _chiY));
            else
            {
                Line2d line = new Line2d(new Point2d(0.0, -StrainReferencePoint / _chiY), new Point2d(-StrainReferencePoint / _chiX, 0.0));
                Vector2d vector = line.ToVector();
                vector.Unitize();

                return new Line2d(new Point2d(0.0, (strain - StrainReferencePoint) / _chiY), new Point2d(0.0 + vector.X, (strain - StrainReferencePoint) / _chiY + vector.Y));
            }
        }

        #endregion

        #region Private Methods

        private double CalculateTeta()
        {
            if (_chiY != 0)
			{
                double teta = Math.Atan(-_chiX / _chiY);
                if (_chiY > 0)
                    teta -= Math.PI;

                return teta;
            }

            return Math.Atan2(-_chiX, _chiY);
        }

        private double CalculateChi()
        {
            if (Math.Abs(Teta) < GeometryBase.GetDefaultTolerance() ||
                Math.Abs(Math.Abs(Teta) - Math.PI) < GeometryBase.GetDefaultTolerance())
                return -ChiY / Math.Cos(Teta);

            else if (Math.Abs(Math.Abs(Teta) - Math.PI / 2.0) < GeometryBase.GetDefaultTolerance() ||
                Math.Abs(Math.Abs(Teta) - 3.0 * Math.PI / 2.0) < GeometryBase.GetDefaultTolerance())
                return ChiX / Math.Sin(Teta);

            else
                return -ChiY / Math.Cos(Teta);
        }

		#endregion

		#region Equals, hashcode, operators

		public bool Equals(StrainPlane other)
        {
            return !(other is null) &&
                   EqualityComparer<Point2d>.Default.Equals(_referencePoint, other._referencePoint) &&
                   _chiX == other._chiX &&
                   _chiY == other._chiY &&
                   _strainReferencePoint == other._strainReferencePoint;
        }

        public override bool Equals(object obj)
        {
            return Equals((StrainPlane)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = -23;
                hashCode = hashCode * -17 + EqualityComparer<Point2d>.Default.GetHashCode(_referencePoint);
                hashCode = hashCode * -17 + _chiX.GetHashCode();
                hashCode = hashCode * -17 + _chiY.GetHashCode();
                hashCode = hashCode * -17 + _strainReferencePoint.GetHashCode();
                return hashCode;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ReferecePoint", _referencePoint, typeof(Point2d));
            info.AddValue("ChiX", _chiX, typeof(double));
            info.AddValue("ChiY", _chiY, typeof(double));
            info.AddValue("StrainReferencePoint", _strainReferencePoint, typeof(double));
        }

		#endregion
	}
}
