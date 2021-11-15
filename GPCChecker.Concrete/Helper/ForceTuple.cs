using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Model.Results;

namespace GPC.Checkers.Concrete.Helper
{
    // è una struct perchè rappresenta un value type

    public struct ForceTuple : IEquatable<ForceTuple>
    {
        private readonly double _N;
        private readonly double _M1;
        private readonly double _M2;


        public double N => _N;

        public double M1 => _M1;

        public double M2 => _M2;


        public ForceTuple(double N, double M1, double M2)
        {
            _N = N;
            _M1 = M1;
            _M2 = M2;
        }


        public override bool Equals(object obj)
        {
            return obj is ForceTuple tuple && Equals(tuple);
        }

        public bool Equals(ForceTuple other)
        {
            return _N == other._N && _M1 == other._M1 && _M2 == other._M2;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = -17;
                hashCode = hashCode * -23 + _N.GetHashCode();
                hashCode = hashCode * -23 + _M1.GetHashCode();
                hashCode = hashCode * -23 + _M2.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(ForceTuple left, ForceTuple right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ForceTuple left, ForceTuple right)
        {
            return !(left == right);
        }

        public static ForceTuple operator *(ForceTuple left, ForceTuple right)
        {
            return new ForceTuple(left.N * right.N, left.M1 * right.M1, left.M2 * right.M2);
        }

        public static ForceTuple operator *(ForceTuple left, double value)
        {
            return new ForceTuple(left.N * value, left.M1 * value, left.M2 * value);
        }

        public static ForceTuple operator +(ForceTuple left, ForceTuple right)
        {
            return new ForceTuple(left.N + right.N, left.M1 + right.M1, left.M2 + right.M2);
        }

        public static ForceTuple operator +(ForceTuple left, double value)
        {
            return new ForceTuple(left.N + value, left.M1 + value, left.M2 + value);
        }

        public static ForceTuple operator -(ForceTuple left, ForceTuple right)
        {
            return new ForceTuple(left.N - right.N, left.M1 - right.M1, left.M2 - right.M2);
        }

        public static ForceTuple operator -(ForceTuple left, double value)
        {
            return left + (-value);
        }

        public static bool operator >(ForceTuple left, double value)
        {
            return Math.Abs(left.N) > value || Math.Abs(left.M1) > value || Math.Abs(left.M2) > value;
        }

        public static bool operator >(ForceTuple left, ForceTuple right)
        {
            return left.N > right.N || left.M1 > right.M1 || left.M2 > right.M2;
        }

        public static bool operator <(ForceTuple left, double value)
        {
            return !(left > value);
        }

        public static bool operator <(ForceTuple left, ForceTuple right)
        {
            return !(left > right);
        }

        public static implicit operator Vector3d(ForceTuple value)
        {
            return new Vector3d(value.N, value.M1, value.M2);
        }

        public static implicit operator Point3d(ForceTuple value)
        {
            return new Point3d(value.N, value.M1, value.M2);
        }

        public static implicit operator ForceTuple(ResultBeamForces value)
        {
            return new ForceTuple(value.N, value.M1, value.M2);
        }

        public static explicit operator ResultBeamForces(ForceTuple value)
        {
            return new ForceTuple(value.N, value.M1, value.M2);
        }
    }
}
