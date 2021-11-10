using System;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Attributes
{
    [Serializable]
    public abstract class CheckerAttribute : ModelObjectId, ISerializable
    {
        #region Variables
        
        protected readonly IConcreteSection[] _sections;
        protected readonly ElementResult[] _uLSresults;
        protected readonly ElementResult[] _sLSresults;

        #endregion

        #region Properties

        public IConcreteSection[] Sections => _sections;

        public ElementResult[] ULSResults => _uLSresults;

        public ElementResult[] SLSResults => _sLSresults;

        #endregion

        public CheckerAttribute(IConcreteSection[] section, ElementResult[] slsResults, ElementResult[] ulsResults, 
                                int id = ModelObjectId.IDUNASSIGNED, string name = "")
            : base(id, name)
        {

            _sLSresults = slsResults ?? throw new ArgumentException("Input results can not be null");
            _uLSresults = ulsResults ?? throw new ArgumentException("Input results can not be null");

            _sections = section ?? throw new ArgumentNullException(nameof(section));
        }

        public CheckerAttribute(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _sections = (IConcreteSection[])info.GetValue("Sections", typeof(IConcreteSection[]));
            _sLSresults = (ElementResult[])info.GetValue("SLSResult", typeof(ElementResult[]));
            _uLSresults = (ElementResult[])info.GetValue("ULSResult", typeof(ElementResult[]));
        }


        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Sections", _sections, typeof(IConcreteSection[]));
            info.AddValue("SLSResult", _sLSresults, typeof(ElementResult[]));
            info.AddValue("ULSResult", _uLSresults, typeof(ElementResult[]));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is CheckerAttribute objCasted) && _sections.SequenceEqual(objCasted.Sections)
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

                for (int i = 0; i < _sections.Length; i++)
                    hashCode = hashCode * -17 + _sections[i].GetHashCode();

                for (int i = 0; i < _sLSresults.Length; i++)
                    hashCode = hashCode * -17 + _sLSresults[i].GetHashCode();

                for (int i = 0; i < _uLSresults.Length; i++)
                    hashCode = hashCode * -17 + _uLSresults[i].GetHashCode();

                return hashCode;
            }
        }
    }
}
