using System;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Attributes
{
    public abstract class CheckerAttribute : ModelObjectId
    {
        #region Variables
        
        protected readonly IConcreteSection[] _sections;
        protected readonly IElementResult[] _uLSresults;
        protected readonly IElementResult[] _sLSresults;

        #endregion

        #region Properties

        public IConcreteSection[] Sections => _sections;

        public IElementResult[] ULSResults => _uLSresults;

        public IElementResult[] SLSResults => _sLSresults;

        #endregion

        public CheckerAttribute(IConcreteSection section, IElementResult[] slsResults, IElementResult[] ulsResults, int id = ModelObjectId.IDUNASSIGNED, string name = "")
            : base(id, name)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            _sLSresults = slsResults ?? throw new ArgumentException("Input resultBeamForces can not be null");
            _uLSresults = ulsResults ?? throw new ArgumentException("Input resultBeamForces can not be null");

            for (int i = 0; i < slsResults.Length; i++)
                for (int c = 0; c < slsResults[i].Results.Length; c++)
                    if (!(slsResults[i].Results[c] is ResultBeamForces))
                        throw new ArgumentException("Input BeamResult.Results must be ResultBeamForces");

            for (int i = 0; i < ulsResults.Length; i++)
                for (int c = 0; c < ulsResults[i].Results.Length; c++)
                    if (!(ulsResults[i].Results[c] is ResultBeamForces))
                        throw new ArgumentException("Input BeamResult.Results must be ResultBeamForces");


            if (slsResults.Select(i => i.Points.Length).Distinct().Count() > 1)
            {
                throw new ArgumentException("Different station number");
            }

            _sections = Enumerable.Repeat(section, slsResults.First().Points.Length).ToArray();
        }

        public CheckerAttribute(IConcreteSection[] sections, IElementResult[] slsElementResults, IElementResult[] ulsElementResults, string name = "")
            : base(name)
        {
            _sLSresults = slsElementResults ?? throw new ArgumentException("Input resultBeamForces can not be null");
            _uLSresults = ulsElementResults ?? throw new ArgumentException("Input resultBeamForces can not be null");

            _sections = sections ?? throw new ArgumentException("Input sections can not be null");

            for (int i = 0; i < slsElementResults.Length; i++)
                for (int c = 0; c < slsElementResults[i].Results.Length; c++)
                    if (!(slsElementResults[i].Results[c] is ResultBeamForces))
                        throw new ArgumentException("Input BeamResult.Results must be ResultBeamForces");

            for (int i = 0; i < ulsElementResults.Length; i++)
                for (int c = 0; c < ulsElementResults[i].Results.Length; c++)
                    if (!(ulsElementResults[i].Results[c] is ResultBeamForces))
                        throw new ArgumentException("Input BeamResult.Results must be ResultBeamForces");

            if (sections.Length != slsElementResults.First().Points.Length)
                throw new ArgumentException("Sections number different than station number");
            if (sections.Length != ulsElementResults.First().Points.Length)
                throw new ArgumentException("Sections number different than station number");
        }

        public CheckerAttribute(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _sections = (IConcreteSection[])info.GetValue("Sections", typeof(IConcreteSection[]));
            _sLSresults = (IElementResult[])info.GetValue("SLSResult", typeof(IElementResult[]));
            _uLSresults = (IElementResult[])info.GetValue("ULSResult", typeof(IElementResult[]));
        }


        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Sections", _sections, typeof(IConcreteSection[]));
            info.AddValue("SLSResult", _sLSresults, typeof(IElementResult[]));
            info.AddValue("ULSResult", _uLSresults, typeof(IElementResult[]));
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
