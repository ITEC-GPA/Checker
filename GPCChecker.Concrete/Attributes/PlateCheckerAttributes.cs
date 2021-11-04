using System;
using System.Runtime.Serialization;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Checkers
{
	/// <summary>
	/// This class rapresent the results of one plate (multiple loadcase/combination).
	/// </summary>

	[Serializable]
    public class PlateCheckerAttributes : CheckerAttribute, ISerializable 
    {

        #region Properties

        public PlateResult[] ULSPlateResults => (PlateResult[])_uLSresults;

        public PlateResult[] SLSPlateResults => (PlateResult[])_sLSresults;

        #endregion


        public PlateCheckerAttributes(IConcreteSection section, PlateResult[] slsBeamResults, PlateResult[] ulsBeamResults, string name = "")
            : base(section, slsBeamResults, ulsBeamResults, name)
        {

        }

        public PlateCheckerAttributes(IConcreteSection[] sections, PlateResult[] slsBeamResults, PlateResult[] ulsBeamResults, string name = "")
            : base(sections, slsBeamResults, ulsBeamResults, name)
        {

        }

        public PlateCheckerAttributes(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is PlateCheckerAttributes objCasted) && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();

                return hashCode;
            }
        }
    }
}
