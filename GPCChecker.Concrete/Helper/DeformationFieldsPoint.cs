using System.Collections.Generic;
using GPC.Geometry;

namespace GPC.Checker.Helper
{
    public struct DeformationFieldsPoint
    {
        public double epsilon;
        public Point2d point;
        public double distanceFromBaricentre;

        public DeformationFieldsPoint(double epsilon, Point2d point, double distanceFromBaricentre)
        {
            this.epsilon = epsilon;
            this.point = point;
            this.distanceFromBaricentre = distanceFromBaricentre;
        }

        public override bool Equals(object obj)
        {
            return obj is DeformationFieldsPoint other &&
                   epsilon == other.epsilon &&
                   EqualityComparer<Point2d>.Default.Equals(point, other.point) &&
                   distanceFromBaricentre == other.distanceFromBaricentre;
        }

        public override int GetHashCode()
        {
            int hashCode = 1820279999;
            hashCode = hashCode * -1521134295 + epsilon.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<Point2d>.Default.GetHashCode(point);
            hashCode = hashCode * -1521134295 + distanceFromBaricentre.GetHashCode();
            return hashCode;
        }

        public void Deconstruct(out double epsilon, out Point2d point, out double distanceFromBaricentre)
        {
            epsilon = this.epsilon;
            point = this.point;
            distanceFromBaricentre = this.distanceFromBaricentre;
        }

        public static implicit operator (double epsilon, Point2d point, double distanceFromBaricentre)(DeformationFieldsPoint value)
        {
            return (value.epsilon, value.point, value.distanceFromBaricentre);
        }

        public static implicit operator DeformationFieldsPoint((double epsilon, Point2d point, double distanceFromBaricentre) value)
        {
            return new DeformationFieldsPoint(value.epsilon, value.point, value.distanceFromBaricentre);
        }
    }
}
