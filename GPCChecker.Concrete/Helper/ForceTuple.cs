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

    /// <summary>
    /// This struct represent a set of forces applied on the local reference system of the section.
    /// </summary>
    public struct ForceTuple : IEquatable<ForceTuple>
    {
        private readonly double _N;
        private readonly double _Mx;
        private readonly double _My;


        public double N => _N;

        public double Mx => _Mx;

        public double My => _My;


        internal ForceTuple(double N, double Mx, double My)
        {
            _N = N;
            _Mx = Mx;
            _My = My;
        }

        #region Operators

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
            return new ForceTuple(left.N * right.N, left.Mx * right.Mx, left.My * right.My);
        }

        public static ForceTuple operator *(ForceTuple left, double value)
        {
            return new ForceTuple(left.N * value, left.Mx * value, left.My * value);
        }

        public static ForceTuple operator +(ForceTuple left, ForceTuple right)
        {
            return new ForceTuple(left.N + right.N, left.Mx + right.Mx, left.My + right.My);
        }

        public static ForceTuple operator +(ForceTuple left, double value)
        {
            return new ForceTuple(left.N + value, left.Mx + value, left.My + value);
        }

        public static ForceTuple operator -(ForceTuple left, ForceTuple right)
        {
            return new ForceTuple(left.N - right.N, left.Mx - right.Mx, left.My - right.My);
        }

        public static ForceTuple operator -(ForceTuple left, double value)
        {
            return left + (-value);
        }

        public static bool operator >(ForceTuple left, double value)
        {
            return Math.Abs(left.N) > value || Math.Abs(left.Mx) > value || Math.Abs(left.My) > value;
        }

        public static bool operator >(ForceTuple left, ForceTuple right)
        {
            return left.N > right.N || left.Mx > right.Mx || left.My > right.My;
        }

        public static bool operator <(ForceTuple left, double value)
        {
            return !(left > value);
        }

        public static bool operator <(ForceTuple left, ForceTuple right)
        {
            return !(left > right);
        }

        #endregion

        #region Cast operators

        public static implicit operator Vector3d(ForceTuple value)
        {
            return new Vector3d(value.Mx, value.My, value.N);
        }

        public static implicit operator Point3d(ForceTuple value)
        {
            return new Point3d(value.Mx, value.My, value.N);
        }


        #endregion

        #region Equals - Hashcode

        public override bool Equals(object obj)
        {
            return obj is ForceTuple tuple && Equals(tuple);
        }

        public bool Equals(ForceTuple other)
        {
            return _N == other._N && _Mx == other._Mx && _My == other._My;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = -17;
                hashCode = hashCode * -23 + _N.GetHashCode();
                hashCode = hashCode * -23 + _Mx.GetHashCode();
                hashCode = hashCode * -23 + _My.GetHashCode();
                return hashCode;
            }
        }

        #endregion

    }
}
