using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;
using GPC.Checkers.Steel.Checkers;
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
        protected readonly ResultBeamForces _forces;
        protected readonly ResultStation _station;
        protected readonly ILoadCase _case; 

        #endregion


        #region Properties

        public ResultBeamForces ResultBeamForces => _forces;

        public ResultStation Station => _station;

        public ILoadCase LoadCase => _case;

        public ISteelSection Section => _section;

        #endregion



        internal BeamStationResults(ISteelSection section, ResultBeamForces forces, ResultStation station, ILoadCase Case, string name = "")
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _forces = forces ?? throw new ArgumentNullException(nameof(forces));
            _station = station ?? throw new ArgumentNullException(nameof(station));
            _case = Case ?? throw new ArgumentNullException(nameof(Case));
            _name = name;
        }

        protected BeamStationResults(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _section = (ISteelSection)info.GetValue("ISteelSection", typeof(ISteelSection));
            _forces = (ResultBeamForces)info.GetValue("ResultBeamForces", typeof(ResultBeamForces));
            _station = (ResultStation)info.GetValue("ResultStation", typeof(ResultStation));
            _case = (ILoadCase)info.GetValue("ILoadCase", typeof(ILoadCase));
        }

        internal abstract double GetMaxWorkingRatio();


        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ISteelSection", _section, typeof(ISteelSection));
            info.AddValue("ResultBeamForces", _forces, typeof(ResultBeamForces));
            info.AddValue("ResultStation", _station, typeof(ResultStation));
            info.AddValue("ILoadCase", _case, typeof(ILoadCase));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamStationResults objCasted) && _section.Equals(objCasted._section) 
                                                         && _forces.Equals(objCasted._forces)
                                                         && _station.Equals(objCasted._station)
                                                         && _case.Equals(objCasted._case)
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
                hashCode = hashCode * -17 + _station.GetHashCode();
                hashCode = hashCode * -17 + _case.GetHashCode();

                return hashCode;
            }
        }
    }
}
