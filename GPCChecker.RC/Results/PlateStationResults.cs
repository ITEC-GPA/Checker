using GPC.Checkers.ReinforcedConcrete.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.ReinforcedConcrete.Results
{
    /// <summary>
    /// This class contains the result of a check performed on a plate station with a given ILoadCase
    /// </summary>
    [Serializable]
    public abstract class PlateStationResults : CheckerStationResult, ISerializable
    {
        #region Variables

        protected readonly PlateChecker.PlateCheckerOptions _options;

        #endregion

        #region Properties

        /// <summary>
        /// The options to perform the check.
        /// </summary>
        public PlateChecker.PlateCheckerOptions CheckerOptions => _options;

        #endregion

        #region Constructor

        internal PlateStationResults(IConcreteSection section, ResultPlateForces[] forces, ResultStation station, ILoadCase[] Case, 
            Standard standard, ULSCheckerResults uLSCheckerResults, SLSCheckerResults[] sLSCheckerResults, 
            PlateChecker.PlateCheckerOptions checkerOptions, string name = "", int id = IDUNASSIGNED)
            : base(section, station, forces, Case, standard, uLSCheckerResults, sLSCheckerResults, name, id)
        {
            _options = checkerOptions ?? throw new ArgumentNullException(nameof(checkerOptions));
        }

        internal PlateStationResults(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _options = (PlateChecker.PlateCheckerOptions)info.GetValue("CheckerOptions", typeof(PlateChecker.PlateCheckerOptions));
        }

        #endregion


        #region Public override method

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("CheckerOptions", _options, typeof(PlateChecker.PlateCheckerOptions));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is PlateStationResults objCasted)  && _options.Equals(objCasted._case)
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
