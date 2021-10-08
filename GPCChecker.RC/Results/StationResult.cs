using System;
using GPC.Model.Results;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using System.Runtime.Serialization;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.ReinforcedConcrete.Results
{
	[Serializable]
	public abstract class CheckerStationResult : Model.ModelObject, ISerializable
	{
        #region Variables

        protected readonly IConcreteSection _section;
        protected readonly IResultLocation _location;
        protected readonly ILoadCase _case;

        protected readonly Standard _standard;
        

        #endregion

        #region Properties

        /// <summary>
        /// The <see cref="ResultStation"/> to check
        /// </summary>
        public IResultLocation Station => _location;

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

        #endregion

        #region Constructor

        internal CheckerStationResult(IConcreteSection section, IResultLocation station, ILoadCase Case, Standard standard, string name = "")
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _location = station ?? throw new ArgumentNullException(nameof(station));
            _case = Case ?? throw new ArgumentNullException(nameof(Case));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));            
            _name = name;
        }

        internal CheckerStationResult(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _section = (IConcreteSection)info.GetValue("Section", typeof(IConcreteSection));
            _location = (IResultLocation)info.GetValue("ResultStation", typeof(IResultLocation));
            _case = (ILoadCase)info.GetValue("ILoadCase", typeof(ILoadCase));
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));            
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
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamStationResults objCasted) && _section.Equals(objCasted._section)
                                                         && _location.Equals(objCasted._location)
                                                         && _case.Equals(objCasted._case)
                                                         && _standard.Equals(objCasted._case)                                                         
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

                return hashCode;
            }
        }

        #endregion
    }
}
