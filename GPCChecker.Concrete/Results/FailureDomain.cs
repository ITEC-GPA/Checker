using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;

namespace GPC.Checkers.Concrete.Results
{
    public class FailureDomain : Model.ModelObject
    {
        protected readonly FailureDomainPoint[][] _domainPoints;


        public FailureDomainPoint[][] DomainPoints => _domainPoints;


        public FailureDomain(FailureDomainPoint[][] domainPoints)
        {
            _domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
        }

        [Serializable]
        public sealed class FailureDomainPoint : ISerializable, IEquatable<FailureDomainPoint>
        {
            private readonly Point3d _point;
            private readonly SectionSolverULS.FailureZones _failureIndex;
            private readonly StrainPlane _strainPlane;


            public double NRd => _point.Z;

            public double MxRd => _point.X;

            public double MyRd => _point.Y;

            public Point3d Point => _point;

            /// <inheritdoc cref="SectionSolverULS.FailureZones"/>
            public SectionSolverULS.FailureZones FailureIndex => _failureIndex;


            internal FailureDomainPoint(double nRd, double mxRd, double myRd, SectionSolverULS.FailureZones failureIndex, StrainPlane strainPlane)
            {
                _point = new Point3d(mxRd, myRd, nRd);
                _failureIndex = failureIndex;
                _strainPlane = strainPlane ?? throw new ArgumentNullException(nameof(strainPlane));
            }

            internal FailureDomainPoint(SerializationInfo info, StreamingContext context)
            {
                _point = (Point3d)info.GetValue("Point", typeof(Point3d));
                _strainPlane = (StrainPlane)info.GetValue("StrainPlane", typeof(StrainPlane));
                _failureIndex = (SectionSolverULS.FailureZones)info.GetValue("FailureIndex", typeof(SectionSolverULS.FailureZones));
            }

            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("Point", _point, typeof(Point3d));
                info.AddValue("StrainPlane", _strainPlane, typeof(StrainPlane));
                info.AddValue("FailureIndex", _failureIndex, typeof(SectionSolverULS.FailureZones));
            }

            public override bool Equals(object obj)
            {
                return Equals((FailureDomainPoint)obj);
            }

            public bool Equals(FailureDomainPoint other)
            {
                return other != null &&
                       _point.Equals(other._point) &&
                       _failureIndex == other._failureIndex &&
                       _strainPlane.Equals(other._strainPlane);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 17;
                    hashCode = hashCode * -23 + _point.GetHashCode();
                    hashCode = hashCode * -23 + _failureIndex.GetHashCode();
                    hashCode = hashCode * -23 + _strainPlane.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(FailureDomainPoint left, FailureDomainPoint right)
            {
                return EqualityComparer<FailureDomainPoint>.Default.Equals(left, right);
            }

            public static bool operator !=(FailureDomainPoint left, FailureDomainPoint right)
            {
                return !(left == right);
            }
        }
    }
}