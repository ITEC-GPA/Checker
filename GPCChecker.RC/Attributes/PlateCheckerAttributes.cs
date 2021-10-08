using System;
using System.Runtime.Serialization;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.ReinforcedConcrete.Checkers
{
	/// <summary>
	/// This class rapresent the results of one plate (multiple loadcase/combination).
	/// </summary>

	[Serializable]
    public class PlateCheckerAttributes : CheckerAttribute, ISerializable 
    {

        #region Properties

        public PlateResult[] PlateResult => (PlateResult[])_results;

        #endregion


        public PlateCheckerAttributes(IConcreteSection section, PlateResult[] beamResults, string name = "")
            : base(section, beamResults, name)
        {

        }

        public PlateCheckerAttributes(IConcreteSection[] sections, PlateResult[] plateResults, string name = "")
            : base(sections, plateResults, name)
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
