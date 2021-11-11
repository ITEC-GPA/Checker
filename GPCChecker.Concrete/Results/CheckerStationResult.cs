using System;
using System.Runtime.Serialization;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Results
{
    [Serializable]
    public abstract class CheckerStationResult : Model.ModelObjectId, ISerializable
    {
        #region Variables

        protected readonly IConcreteSection _section;
        protected readonly ResultLocation _location;
        protected readonly ILoadCase[] _case;
        protected readonly ResultType[] _forces;
        protected readonly ULSCheckerResultsType _uLSCheckerResult;
        protected readonly SLSCheckerResultsType[] _sLSCheckerResults;

        protected readonly Standard _standard;

        #endregion

        #region Properties

        /// <summary>
        /// The <see cref="ResultStation"/> to check
        /// </summary>
        public ResultLocation Station => _location;

        /// <summary>
        /// The <see cref="ILoadCase"/> to check
        /// </summary>
        public ILoadCase[] LoadCase => _case;

        /// <summary>
        /// Forces
        /// </summary>
        public ResultType[] Forces => _forces;

        /// <summary>
        /// The <see cref="IConcreteSection"/> to check
        /// </summary>
        public IConcreteSection Section => _section;

        /// <summary>
        /// The Standard for the Check
        /// </summary>
        public Standard Standard => _standard;

        /// <summary>
        /// Results for ultimate limit state analysis
        /// </summary>
        public ULSCheckerResultsType ULSCheckerResults => _uLSCheckerResult;

        /// <summary>
        /// Results for serviceability limit state analysis
        /// </summary>
        public SLSCheckerResultsType[] SLSCheckerResults => _sLSCheckerResults;

        #endregion

        #region Constructor

        internal CheckerStationResult(IConcreteSection section, ResultLocationStation station, ResultType[] forces, ILoadCase[] Case, Standard standard,
            ULSCheckerResultsType uLSCheckerResults, SLSCheckerResultsType[] sLSCheckerResults, string name = "", int id = IDUNASSIGNED)
            : base(id, name)
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _location = station ?? throw new ArgumentNullException(nameof(station));
            _forces = forces ?? throw new ArgumentNullException(nameof(forces));
            _case = Case ?? throw new ArgumentNullException(nameof(Case));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _uLSCheckerResult = uLSCheckerResults ?? throw new ArgumentNullException(nameof(uLSCheckerResults));
            _sLSCheckerResults = sLSCheckerResults ?? throw new ArgumentNullException(nameof(sLSCheckerResults));
        }

        internal CheckerStationResult(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _section = (IConcreteSection)info.GetValue("Section", typeof(IConcreteSection));
            _location = (IResultLocation)info.GetValue("ResultStation", typeof(IResultLocation));
            _case = (ILoadCase[])info.GetValue("ILoadCase", typeof(ILoadCase[]));
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _forces = (ResultType[])info.GetValue("Forces", typeof(ResultType[]));
            _uLSCheckerResult = (ULSCheckerResultsType)info.GetValue("ULSCheckerResults", typeof(ULSCheckerResultsType));
            _sLSCheckerResults = (SLSCheckerResultsType[])info.GetValue("SLSCheckerResults", typeof(ULSCheckerResultsType));

        }

        #endregion

        #region Public abstract method

        internal abstract double GetMaxWorkingRatio();

        #endregion

        #region Public override method

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Section", _section, typeof(IConcreteSection));
            info.AddValue("ResultStation", _location, typeof(IResultLocation));
            info.AddValue("ILoadCase", _case, typeof(ILoadCase));
            info.AddValue("Standard", _standard, typeof(Standard));
            info.AddValue("Forces", _forces, typeof(ResultType[]));
            info.AddValue("ULSCheckerResults", _uLSCheckerResult, typeof(ULSCheckerResultsType));
            info.AddValue("SLSCheckerResults", _sLSCheckerResults, typeof(ULSCheckerResultsType));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamStationResults objCasted) && _section.Equals(objCasted._section)
                                                         && _location.Equals(objCasted._location)
                                                         && _case.Equals(objCasted._case)
                                                         && _standard.Equals(objCasted._case)
                                                         && _forces.Equals(objCasted._forces)
                                                         && _uLSCheckerResult.Equals(objCasted._uLSCheckerResult)
                                                         && _sLSCheckerResults.Equals(objCasted._sLSCheckerResults)
                                                         && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _section.GetHashCode();
                hashCode = hashCode * -17 + _location.GetHashCode();
                hashCode = hashCode * -17 + _case.GetHashCode();
                hashCode = hashCode * -17 + _standard.GetHashCode();
                hashCode = hashCode * -17 + _uLSCheckerResult.GetHashCode();
                hashCode = hashCode * -17 + _sLSCheckerResults.GetHashCode();
                hashCode = hashCode * -17 + _forces.GetHashCode();

                return hashCode;
            }
        }

        #endregion
    }
}
