using GPC.Checkers.Results;
using GPC.Checkers.Steel.Checkers;
using GPC.Model.ElementProperties;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
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

        protected readonly BeamElement _beamElement;
        protected readonly StationResultBeamForces _resultLocationStation;

        #endregion

        #region Properties

        public ResultBeamForces ResultBeamForces => _resultLocationStation.ResultBeamForces;

        public StationResultBeamForces StationResultBeamForces => _resultLocationStation;

        /// <summary>
        /// The <see cref="ISteelSection"/> to check
        /// </summary>
        public BeamElement BeamElement => _beamElement;

        public BeamProperty Section => _beamElement.BeamProperty;

        /// <summary>
        /// The options to perform the check.
        /// </summary>
        public BeamChecker.BeamOptions CheckerOptions => (BeamChecker.BeamOptions)_options;

        #endregion

        internal BeamStationResults(BeamElement beam, StationResultBeamForces station, ILoadCase Case, Standard standard,
            BeamChecker.BeamOptions checkerOptions, string name = "")
            : base(Case, standard, checkerOptions, name)
        {
            _beamElement = beam ?? throw new ArgumentNullException(nameof(beam));
            _resultLocationStation = station ?? throw new ArgumentNullException(nameof(station));
        }

        internal BeamStationResults(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _beamElement = (BeamElement)info.GetValue("BeamElement", typeof(BeamElement));
            _resultLocationStation = (StationResultBeamForces)info.GetValue("ResultStation", typeof(StationResultBeamForces));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("BeamElement", _beamElement, typeof(BeamElement));
            info.AddValue("ResultStation", _resultLocationStation, typeof(StationResultBeamForces));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamStationResults objCasted)
                && _beamElement.Equals(objCasted._beamElement)
                && _resultLocationStation.Equals(objCasted._resultLocationStation)
                && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _beamElement.GetHashCode();
                hashCode = hashCode * -17 + _resultLocationStation.GetHashCode();

                return hashCode;
            }
        }
    }
}
