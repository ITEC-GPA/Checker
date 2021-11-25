using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Helper;
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

        public sealed class FailureDomainForce
        {

        }

        [Serializable]
        public sealed class FailureDomainPoint : ISerializable, IEquatable<FailureDomainPoint>
        {
            private readonly ForceTuple _forceTuple;
            private readonly SectionSolver.FailureZones _failureIndex;
            private readonly StrainPlane _strainPlane;


            public double NRd => _forceTuple.N;

            public double MxRd => _forceTuple.Mx;

            public double MyRd => _forceTuple.My;


            public Point3d Point => _forceTuple;
            public ForceTuple ForceTuple => _forceTuple;


            /// <inheritdoc cref="SectionSolver.FailureZones"/>
            public SectionSolver.FailureZones FailureIndex => _failureIndex;


            internal FailureDomainPoint(ForceTuple forceTuple, SectionSolver.FailureZones failureIndex, StrainPlane strainPlane)
            {
                _forceTuple = forceTuple;
                _failureIndex = failureIndex;
                _strainPlane = strainPlane ?? throw new ArgumentNullException(nameof(strainPlane));
            }

            internal FailureDomainPoint(SerializationInfo info, StreamingContext context)
            {
                _forceTuple = (ForceTuple)info.GetValue("ForceTuple", typeof(ForceTuple));
                _strainPlane = (StrainPlane)info.GetValue("StrainPlane", typeof(StrainPlane));
                _failureIndex = (SectionSolver.FailureZones)info.GetValue("FailureIndex", typeof(SectionSolver.FailureZones));
            }

            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("ForceTuple", _forceTuple, typeof(ForceTuple));
                info.AddValue("StrainPlane", _strainPlane, typeof(StrainPlane));
                info.AddValue("FailureIndex", _failureIndex, typeof(SectionSolver.FailureZones));
            }

            public override bool Equals(object obj)
            {
                return Equals((FailureDomainPoint)obj);
            }

            public bool Equals(FailureDomainPoint other)
            {
                return other != null &&
                       _forceTuple.Equals(other._forceTuple) &&
                       _failureIndex == other._failureIndex &&
                       _strainPlane.Equals(other._strainPlane);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 17;
                    hashCode = hashCode * -23 + _forceTuple.GetHashCode();
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