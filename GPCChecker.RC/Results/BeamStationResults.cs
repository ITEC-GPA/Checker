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

        protected readonly ResultBeamForces _forces;
        protected readonly BeamChecker.BeamCheckerOptions _options;

        #endregion


        #region Properties

        /// <summary>
        /// The <see cref="ResultBeamForces"/> to check
        /// </summary>
        public ResultBeamForces ResultBeamForce => _forces;

        /// <summary>
        /// The options to perform the check.
        /// </summary>
        public BeamChecker.BeamCheckerOptions CheckerOptions => _options;

        #endregion


        #region Constructor

        internal BeamStationResults(IConcreteSection section, ResultBeamForces forces, ResultStation station, ILoadCase Case, Standard standard, 
                                    BeamChecker.BeamCheckerOptions checkerOptions, string name = "", int id = IDUNASSIGNED)
            : base(section, station, Case, standard, name, id)
        {
            _forces = forces ?? throw new ArgumentNullException(nameof(forces));
            _options = checkerOptions ?? throw new ArgumentNullException(nameof(checkerOptions));
        }

        internal BeamStationResults(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _forces = (ResultBeamForces)info.GetValue("ResultBeamForces", typeof(ResultBeamForces));
            _options = (BeamChecker.BeamCheckerOptions)info.GetValue("CheckerOptions", typeof(BeamChecker.BeamCheckerOptions));
        }

        #endregion


        #region Public override method

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ResultBeamForces", _forces, typeof(ResultBeamForces));
            info.AddValue("CheckerOptions", _options, typeof(BeamChecker.BeamCheckerOptions));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamStationResults objCasted) && _forces.Equals(objCasted._forces)
                                                         && _options.Equals(objCasted._case)
                                                         && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _section.GetHashCode();
                hashCode = hashCode * -17 + _forces.GetHashCode();
                hashCode = hashCode * -17 + _location.GetHashCode();
                hashCode = hashCode * -17 + _case.GetHashCode();
                hashCode = hashCode * -17 + _standard.GetHashCode();
                hashCode = hashCode * -17 + _options.GetHashCode();
                hashCode = hashCode * -17 + _options.GetHashCode();

                return hashCode;
            }
        }

		#endregion
	}
}
