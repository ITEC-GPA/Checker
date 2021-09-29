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
using System.Runtime.Serialization;
using GPC.Checkers.ReinforcedConcrete.Checkers;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.ReinforcedConcrete.Results
{    
    
    /// <summary>
    /// This class contains the result of a check performed on a beam station with a given ILoadCase
    /// </summary>
    [Serializable]
    public abstract class BeamStationResults : Model.ModelObject, ISerializable
    {

        #region Variables

        protected readonly IConcreteSection _section;
        protected readonly ResultBeamForces _forces;
        protected readonly ResultStation _station;
        protected readonly ILoadCase _case;

        protected readonly Standard _standard;
        protected readonly Checker.Options _options;

        #endregion


        #region Properties

        /// <summary>
        /// The <see cref="ResultBeamForces"/> to check
        /// </summary>
        public ResultBeamForces ResultBeamForce => _forces;

        /// <summary>
        /// The <see cref="ResultStation"/> to check
        /// </summary>
        public ResultStation Station => _station;

        /// <summary>
        /// The <see cref="ILoadCase"/> to check
        /// </summary>
        public ILoadCase LoadCase => _case;

        /// <summary>
        /// The <see cref="IConcreteSection"/> to check
        /// </summary>
        public IConcreteSection Section => _section;

        /// <summary>
        /// The Standard for the Check
        /// </summary>
        public Standard Standard => _standard;

        /// <summary>
        /// The options to perform the check.
        /// </summary>
        public Checker.Options CheckerOptions => _options;

        #endregion



        internal BeamStationResults(IConcreteSection section, ResultBeamForces forces, ResultStation station, ILoadCase Case, Standard standard, 
                                    Checker.Options checkerOptions, string name = "")
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _forces = forces ?? throw new ArgumentNullException(nameof(forces));
            _station = station ?? throw new ArgumentNullException(nameof(station));
            _case = Case ?? throw new ArgumentNullException(nameof(Case));

            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _options = checkerOptions ?? throw new ArgumentNullException(nameof(checkerOptions));
            _name = name;
        }

        internal BeamStationResults(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _section = (IConcreteSection)info.GetValue("Section", typeof(IConcreteSection));
            _forces = (ResultBeamForces)info.GetValue("ResultBeamForces", typeof(ResultBeamForces));
            _station = (ResultStation)info.GetValue("ResultStation", typeof(ResultStation));
            _case = (ILoadCase)info.GetValue("ILoadCase", typeof(ILoadCase));
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _options = (Checker.Options)info.GetValue("CheckerOptions", typeof(Checker.Options));
        }

        internal abstract double GetMaxWorkingRatio();


        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Section", _section, typeof(ISteelSection));
            info.AddValue("ResultBeamForces", _forces, typeof(ResultBeamForces));
            info.AddValue("ResultStation", _station, typeof(ResultStation));
            info.AddValue("ILoadCase", _case, typeof(ILoadCase));
            info.AddValue("Standard", _standard, typeof(Standard));
            info.AddValue("CheckerOptions", _options, typeof(Checker.Options));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamStationResults objCasted) && _section.Equals(objCasted._section) 
                                                         && _forces.Equals(objCasted._forces)
                                                         && _station.Equals(objCasted._station)
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
                hashCode = hashCode * -17 + _forces.GetHashCode();
                hashCode = hashCode * -17 + _station.GetHashCode();
                hashCode = hashCode * -17 + _case.GetHashCode();
                hashCode = hashCode * -17 + _standard.GetHashCode();
                hashCode = hashCode * -17 + _options.GetHashCode();

                return hashCode;
            }
        }
    }
}
