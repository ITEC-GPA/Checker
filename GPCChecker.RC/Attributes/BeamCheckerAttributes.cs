using System;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.ReinforcedConcrete.Checkers
{
	/// <summary>
	/// This class rapresent the results of one beam (multiple loadcase/combination).
	/// </summary>

	[Serializable]
    public class BeamCheckerAttributes : CheckerAttribute, ISerializable 
    {

        #region Properties

        public BeamResult[] BeamResults => (BeamResult[])_results;

        public double Length => BeamResults.First().Length;

        #endregion


        public BeamCheckerAttributes(IConcreteSection section, BeamResult[] beamResults, string name = "")
            : base(section, beamResults, name)
        {

        }

        public BeamCheckerAttributes(IConcreteSection[] sections, BeamResult[] beamResults, string name = "") 
            : base(sections, beamResults, name)
        {
                        
            if (beamResults.Select(i => i.Length).Distinct().Count() > 1)
            {
                throw new ArgumentException("Different beam result lenght");
            }

            if (sections.Length != beamResults.First().Points.Length)
                throw new ArgumentException("Sections number different than station number");
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
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamCheckerAttributes objCasted) && base.Equals(objCasted);
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
