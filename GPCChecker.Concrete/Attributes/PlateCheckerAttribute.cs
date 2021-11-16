using System;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Attributes
{
    /// <summary>
    /// This class rapresent the results of one plate (multiple loadcase/combination).
    /// </summary>

    [Serializable]
    public class PlateCheckerAttribute : ModelObjectId, ISerializable
    {

        private readonly SectionCheckerAttribute[] _sectionCheckerAttributes;

        public SectionCheckerAttribute[] SectionCheckerAttribute => _sectionCheckerAttributes;



        public PlateCheckerAttribute(IConcreteSection[] sections, ResultBeamForces[][] slsbeamResults, ResultBeamForces[][] ulsbeamResults, int id = ModelObjectId.IDUNASSIGNED)
            : base(id)
        {

            if (sections is null)
                throw new ArgumentNullException(nameof(sections));

            if (slsbeamResults is null)
                throw new ArgumentNullException(nameof(slsbeamResults));

            if (ulsbeamResults is null)
                throw new ArgumentNullException(nameof(ulsbeamResults));


            if (sections.Length != slsbeamResults.Length)
                throw new ArgumentException();

            if (sections.Length != ulsbeamResults.Length)
                throw new ArgumentException();


            _sectionCheckerAttributes = new SectionCheckerAttribute[sections.Length];

            for (int i = 0; i < sections.Length; i++)
            {
                _sectionCheckerAttributes[i] = new SectionCheckerAttribute(sections[i], slsbeamResults[i], ulsbeamResults[i]);
            }
        }


        public PlateCheckerAttribute(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _sectionCheckerAttributes = (SectionCheckerAttribute[])info.GetValue("SectionCheckerAttribute", typeof(SectionCheckerAttribute[]));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("SectionCheckerAttribute", _sectionCheckerAttributes, typeof(SectionCheckerAttribute[]));
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            return (obj is PlateCheckerAttribute objCasted) && _sectionCheckerAttributes.SequenceEqual(objCasted._sectionCheckerAttributes) && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();

                for (int i = 0; i < _sectionCheckerAttributes.Length; i++)
                    hashCode = hashCode * -17 + _sectionCheckerAttributes[i].GetHashCode();

                return hashCode;
            }
        }
    }
}
