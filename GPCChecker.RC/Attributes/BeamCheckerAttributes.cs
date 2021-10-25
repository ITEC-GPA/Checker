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

        public BeamResult[] ULSBeamResults => (BeamResult[])_sLSresults;

        public BeamResult[] SLSBeamResults => (BeamResult[])_sLSresults;

        public double Length => SLSBeamResults.First().Length;

        #endregion


        public BeamCheckerAttributes(IConcreteSection section, BeamResult[] slsbeamResults, BeamResult[] ulsbeamResults, string name = "")
            : base(section, slsbeamResults, ulsbeamResults, name)
        {
			if (section is null)
			{
				throw new ArgumentNullException(nameof(section));
			}

			if (slsbeamResults is null)
			{
				throw new ArgumentNullException(nameof(slsbeamResults));
			}

			if (ulsbeamResults is null)
			{
				throw new ArgumentNullException(nameof(ulsbeamResults));
			}

			if (string.IsNullOrEmpty(name))
			{
				throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));
			}
		}

        public BeamCheckerAttributes(IConcreteSection[] sections, BeamResult[] slsbeamResults, BeamResult[] ulsbeamResults, string name = "")
            : base(sections, slsbeamResults, ulsbeamResults, name)
        {
			if (sections is null)
			{
				throw new ArgumentNullException(nameof(sections));
			}

			if (slsbeamResults is null)
			{
				throw new ArgumentNullException(nameof(slsbeamResults));
			}

			if (ulsbeamResults is null)
			{
				throw new ArgumentNullException(nameof(ulsbeamResults));
			}

			if (string.IsNullOrEmpty(name))
			{
				throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));
			}
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
