using System;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Attributes
{
    [Serializable]
    public class SectionCheckerAttribute : ModelObjectId, ISerializable
    {
        
        protected readonly IConcreteSection _section;
        protected readonly ResultBeamForces[] _uLSresults;
        protected readonly ResultBeamForces[] _sLSresults;


        public IConcreteSection Section => _section;

        public ResultBeamForces[] ULSResults => _uLSresults;

        public ResultBeamForces[] SLSResults => _sLSresults;


        public SectionCheckerAttribute(IConcreteSection section, ResultBeamForces[] slsResults, ResultBeamForces[] ulsResults, int id = ModelObjectId.IDUNASSIGNED)
            : base(id)
        {
            _uLSresults = ulsResults;
            _sLSresults = slsResults;
            _section = section ?? throw new ArgumentNullException(nameof(section));
        }

        protected SectionCheckerAttribute(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _section = (IConcreteSection)info.GetValue("Sections", typeof(IConcreteSection));
            _sLSresults = (ResultBeamForces[])info.GetValue("SLSResult", typeof(ResultBeamForces[]));
            _uLSresults = (ResultBeamForces[])info.GetValue("ULSResult", typeof(ResultBeamForces[]));
        }


        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Sections", _section, typeof(IConcreteSection));
            info.AddValue("SLSResult", _sLSresults, typeof(ResultBeamForces[]));
            info.AddValue("ULSResult", _uLSresults, typeof(ResultBeamForces[]));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is SectionCheckerAttribute objCasted) && _section.Equals(objCasted.Section)
                                                       && _sLSresults.SequenceEqual(objCasted.SLSResults)
                                                       && _uLSresults.SequenceEqual(objCasted.ULSResults)
                                                       && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();

                hashCode = hashCode * -17 + _section.GetHashCode();

                for (int i = 0; i < _sLSresults.Length; i++)
                    hashCode = hashCode * -17 + _sLSresults[i].GetHashCode();

                for (int i = 0; i < _uLSresults.Length; i++)
                    hashCode = hashCode * -17 + _uLSresults[i].GetHashCode();

                return hashCode;
            }
        }
    }
}
