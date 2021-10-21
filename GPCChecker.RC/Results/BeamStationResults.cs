using System;
using GPC.Model.Results;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using System.Runtime.Serialization;
using GPC.Checkers.ReinforcedConcrete.Checkers;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.ReinforcedConcrete.Results
{

    /// <summary>
    /// This class contains the result of a check performed on a beam station with a given ILoadCase
    /// </summary>
    [Serializable]
    public abstract class BeamStationResults : CheckerStationResult, ISerializable
    {
        #region Variables

        protected readonly BeamChecker.BeamCheckerOptions _options;

        #endregion


        #region Properties

        /// <summary>
        /// The options to perform the check.
        /// </summary>
        public BeamChecker.BeamCheckerOptions CheckerOptions => _options;

        #endregion


        #region Constructor

        internal BeamStationResults(IConcreteSection section, ResultStation station, ResultBeamForces[] forces, ILoadCase[] Case, Standard standard, 
                                    ULSCheckerResults uLSCheckerResults, SLSCheckerResults[] sLSCheckerResults, BeamChecker.BeamCheckerOptions checkerOptions,
                                    string name = "", int id = IDUNASSIGNED)
            : base(section, station, forces, Case, standard, uLSCheckerResults, sLSCheckerResults, name, id)
        {
            _options = checkerOptions ?? throw new ArgumentNullException(nameof(checkerOptions));
        }

        internal BeamStationResults(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _options = (BeamChecker.BeamCheckerOptions)info.GetValue("CheckerOptions", typeof(BeamChecker.BeamCheckerOptions));
        }

        #endregion


        #region Public override method

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("CheckerOptions", _options, typeof(BeamChecker.BeamCheckerOptions));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamStationResults objCasted) && _options.Equals(objCasted._case)
                                                         && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _options.GetHashCode();

                return hashCode;
            }
        }

		#endregion
	}
}
