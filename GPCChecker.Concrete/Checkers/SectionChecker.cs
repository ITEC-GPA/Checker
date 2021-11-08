using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Checkers
{
    public abstract class SectionChecker : Checker, ISerializable
    {

        public SectionChecker(SectionCheckerAttribute checkerAttribute, SectionCheckerOptions options, Standard standard,
                                int id = ModelObjectId.IDUNASSIGNED, string name = "")
            : base(checkerAttribute, options, standard, id, name)
        {

        }

        public SectionChecker(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

        public override abstract void PerformCheck();

        public override abstract void ULSPerformCheck();

        public override abstract void SLSPerformCheck();


        public abstract class SectionCheckerOptions : Options
        {

            public Point3d AxialForceReferencePoint { get; }

            public SectionCheckerOptions()
            {
                AxialForceReferencePoint = Point3d.Origin;
            }

            public SectionCheckerOptions(Point3d axialForceReferencePoint)
            {
                AxialForceReferencePoint = axialForceReferencePoint;
            }

            public override bool Equals(object obj)
            {
                return obj is SectionCheckerOptions options &&
                       EqualityComparer<Point3d>.Default.Equals(AxialForceReferencePoint, options.AxialForceReferencePoint);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return -17 * EqualityComparer<Point3d>.Default.GetHashCode(AxialForceReferencePoint);
                }
            }

            public static bool operator ==(SectionCheckerOptions left, SectionCheckerOptions right)
            {
                return EqualityComparer<SectionCheckerOptions>.Default.Equals(left, right);
            }

            public static bool operator !=(SectionCheckerOptions left, SectionCheckerOptions right)
            {
                return !(left == right);
            }
        }
    }
}
