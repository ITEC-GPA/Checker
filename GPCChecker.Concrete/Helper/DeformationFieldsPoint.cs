using System.Collections.Generic;
using GPC.Geometry;

namespace GPC.Checker.Helper
{
    public class DeformationFieldsPoint
    {
        /// <summary>
        /// Strain in point.
        /// </summary>
        public double Epsilon { get; set; }

        /// <summary>
        /// Strain plane rotation point in initial section coodinate system.
        /// </summary>
        public Point2d Point { get; set; }

        /// <summary>
        /// Distance from axis parallel to neutral axis and that passes for a point of reference.
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// Angle of rotation chi from which this rotation point begins to take effect.
        /// </summary>
        public double Angle { get; set; }

        public DeformationFieldsPoint(double epsilon, Point2d point, double distance, double angle = 0.0)
        {
            Epsilon = epsilon;
            Point = point;
            Distance = distance;
            Angle = angle;
        }

        public override bool Equals(object obj)
        {
            return obj is DeformationFieldsPoint other &&
                   Epsilon == other.Epsilon &&
                   EqualityComparer<Point2d>.Default.Equals(Point, other.Point) &&
                   Distance == other.Distance &&
                   Angle == other.Angle;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 1820279999;
                hashCode = hashCode * -1521134295 + Epsilon.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<Point2d>.Default.GetHashCode(Point);
                hashCode = hashCode * -1521134295 + Distance.GetHashCode();
                hashCode = hashCode * -1521134295 + Angle.GetHashCode();
                return hashCode;
            }
        }
    }
}
