using System;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Attributes
{
    /// <summary>
    /// This class rapresent the results of one beam (multiple loadcase/combination).
    /// </summary>

    [Serializable]
    public class BeamCheckerAttributes : CheckerAttribute, ISerializable
    {

        #region Properties

        public BeamResult[] ULSBeamResults => (BeamResult[])_sLSresults;

        public BeamResult[] SLSBeamResults => (BeamResult[])_sLSresults;

        public double Length => SLSBeamResults.First().Length;

        #endregion


        public BeamCheckerAttributes(IConcreteSection section, BeamResult[] slsbeamResults, BeamResult[] ulsbeamResults, string name = "")
            : base(Enumerable.Repeat(section, slsbeamResults.First().ResultLocations.Length).ToArray(), slsbeamResults, ulsbeamResults, ModelObjectId.IDUNASSIGNED, name)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (slsbeamResults is null)
            {
                throw new ArgumentNullException(nameof(slsbeamResults));
            }


        }

        public BeamCheckerAttributes(IConcreteSection[] sections, BeamResult[] slsbeamResults, BeamResult[] ulsbeamResults, string name = "")
            : base(sections, slsbeamResults, ulsbeamResults, ModelObjectId.IDUNASSIGNED, name)
        {

        }

        public BeamCheckerAttributes(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamCheckerAttributes objCasted) && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return -base.GetHashCode();
            }
        }


    }
}
