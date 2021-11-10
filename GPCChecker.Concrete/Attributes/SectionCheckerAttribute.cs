using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Attributes
{
    [Serializable]
    public sealed class SectionCheckerAttribute : CheckerAttribute, ISerializable, IEquatable<SectionCheckerAttribute>
    {


        public SectionCheckerAttribute(IConcreteSection section, SectionResult[] slsResults, SectionResult[] ulsResults,
                                        int id = ModelObjectId.IDUNASSIGNED, string name = "")
            : base(new[] { section }, slsResults, ulsResults, id, name)
        {

        }


        public SectionCheckerAttribute(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        public override bool Equals(object obj)
        {
            return Equals((SectionCheckerAttribute)obj);
        }

        public bool Equals(SectionCheckerAttribute other)
        {
            return other != null &&
                   base.Equals(other);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }


        public static bool operator ==(SectionCheckerAttribute left, SectionCheckerAttribute right)
        {
            return EqualityComparer<SectionCheckerAttribute>.Default.Equals(left, right);
        }

        public static bool operator !=(SectionCheckerAttribute left, SectionCheckerAttribute right)
        {
            return !(left == right);
        }
    }
}
