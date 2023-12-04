using GPC.Checkers.Results;
using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Results.ResultLocations;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Results
{
    /// <summary>
    /// This class contains the result of a check performed on a beam station with a given ILoadCase
    /// </summary>
    [Serializable]
    public abstract class BeamStationResults : CommonResults, ISerializable
    {
        #region Variables

        protected readonly ISteelSection _section;
        protected readonly StationResultBeamForces _resultLocationStation;

        #endregion

        #region Properties

        public ResultBeamForces ResultBeamForces => _resultLocationStation.ResultBeamForces;

        public StationResultBeamForces Station => _resultLocationStation;

        /// <summary>
        /// The <see cref="ISteelSection"/> to check
        /// </summary>
        public ISteelSection Section => _section;

        /// <summary>
        /// The options to perform the check.
        /// </summary>
        public BeamChecker.BeamOptions CheckerOptions => (BeamChecker.BeamOptions)_options;

        #endregion

        internal BeamStationResults(ISteelSection section, StationResultBeamForces station, ILoadCase Case, Standard standard,
            BeamChecker.BeamOptions checkerOptions, string name = "")
            : base(Case, standard, checkerOptions, name)
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _resultLocationStation = station ?? throw new ArgumentNullException(nameof(station));
        }

        internal BeamStationResults(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _section = (ISteelSection)info.GetValue("ISteelSection", typeof(ISteelSection));
            _resultLocationStation = (StationResultBeamForces)info.GetValue("ResultStation", typeof(StationResultBeamForces));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ISteelSection", _section, typeof(ISteelSection));
            info.AddValue("ResultStation", _resultLocationStation, typeof(StationResultBeamForces));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamStationResults objCasted)
                && _section.Equals(objCasted._section)
                && _resultLocationStation.Equals(objCasted._resultLocationStation)
                && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _section.GetHashCode();
                hashCode = hashCode * -17 + _resultLocationStation.GetHashCode();

                return hashCode;
            }
        }
    }
}
