using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Checkers.Steel.Checkers;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;
using System.Runtime.Serialization;


namespace GPC.Checkers.Steel.Results
{    
    
    /// <summary>
    /// This class contains the result of a check performed on a beam station with a given ILoadCase
    /// </summary>
    [Serializable]
    public abstract class BeamStationResults : Model.ModelObject, ISerializable
    {

        #region Variables

        protected readonly ISteelSection _section;
        protected readonly ResultLocationStation _resultLocationStation;
        protected readonly ILoadCase _case;

        protected readonly Standard _standard;
        protected readonly Checker.Options _options;

        #endregion


        #region Properties

        public ResultBeamForces[] ResultBeamForces => (ResultBeamForces[])_resultLocationStation.ResultTypes;

        public ResultLocationStation Station => _resultLocationStation;

        public ILoadCase LoadCase => _case;

        public ISteelSection Section => _section;

        public Standard Standard => _standard;

        public Checker.Options CheckerOptions => _options;

        #endregion



        internal BeamStationResults(ISteelSection section, ResultLocationStation station, ILoadCase Case, Standard standard, 
                                    Checker.Options checkerOptions, string name = "")
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _resultLocationStation = station ?? throw new ArgumentNullException(nameof(station));
            _case = Case ?? throw new ArgumentNullException(nameof(Case));

            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _options = checkerOptions ?? throw new ArgumentNullException(nameof(checkerOptions));
            _name = name;
        }

        internal BeamStationResults(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            _section = (ISteelSection)info.GetValue("ISteelSection", typeof(ISteelSection));
            _resultLocationStation = (ResultLocationStation)info.GetValue("ResultStation", typeof(ResultLocationStation));
            _case = (ILoadCase)info.GetValue("ILoadCase", typeof(ILoadCase));
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _options = (Checker.Options)info.GetValue("CheckerOptions", typeof(Checker.Options));
        }

        internal abstract double GetMaxWorkingRatio();


        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ISteelSection", _section, typeof(ISteelSection));
            info.AddValue("ResultStation", _resultLocationStation, typeof(ResultLocationStation));
            info.AddValue("ILoadCase", _case, typeof(ILoadCase));
            info.AddValue("ILoadCase", _standard, typeof(Standard));
            info.AddValue("ILoadCase", _options, typeof(Checker.Options));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamStationResults objCasted) && _section.Equals(objCasted._section) 
                                                         && _resultLocationStation.Equals(objCasted._resultLocationStation)
                                                         && _case.Equals(objCasted._case)
                                                         && _standard.Equals(objCasted._case)
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
                hashCode = hashCode * -17 + _resultLocationStation.GetHashCode();
                hashCode = hashCode * -17 + _case.GetHashCode();
                hashCode = hashCode * -17 + _standard.GetHashCode();
                hashCode = hashCode * -17 + _options.GetHashCode();

                return hashCode;
            }
        }
    }
}
