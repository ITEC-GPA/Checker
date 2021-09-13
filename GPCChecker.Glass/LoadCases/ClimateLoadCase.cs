using GPC.Checkers.Glasses.Glasses;
using GPC.Utilities.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.Glasses.LoadCases
{
    [Serializable]
    [UI(Description = "Climatic", Group = "Load cases", Kind = "Load case")]
    public class ClimateLoadCase : Model.LoadCases.ClimateLoadCase, IGlassLoadCase
    {
        private readonly double _temperature;
        private readonly double _loadDuration;
        private readonly GlassSurface.LoadRestrainCondition _loadRestrainCondition;

        public double LoadDuration => _loadDuration;

        public double Temperature => _temperature;
        public GlassSurface.LoadRestrainCondition LoadRestrainCondition => _loadRestrainCondition;


        public ClimateLoadCase(string name, Seasons season, ClimateTypes climateType, double manufactoring, double installation,
                               double loadDuration, double temperature,
                               GlassSurface.LoadRestrainCondition loadRestrainCondition = GlassSurface.LoadRestrainCondition.FourSidesClimate)
            : this(name, season, climateType, manufactoring, installation, loadDuration, temperature, Guid.NewGuid(), loadRestrainCondition)
        {

        }

        public ClimateLoadCase(string name, Seasons season, ClimateTypes climateType, double manufactoring, double installation, 
                               double loadDuration, double temperature, Guid guid,
                               GlassSurface.LoadRestrainCondition loadRestrainCondition = GlassSurface.LoadRestrainCondition.FourSidesClimate)
            : base(name, season, climateType, manufactoring, installation, guid)
        {
            _loadDuration = loadDuration;
            _temperature = temperature;
            _loadRestrainCondition = loadRestrainCondition;
        }

        public ClimateLoadCase(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _loadDuration = info.GetDouble("LoadDuration");
            _temperature = info.GetDouble("Temperature");
            _loadRestrainCondition = (GlassSurface.LoadRestrainCondition)info.GetValue("LoadRestrainCondition", typeof(GlassSurface.LoadRestrainCondition));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("LoadDuration", _loadDuration);
            info.AddValue("Temperature", _temperature);
            info.AddValue("LoadRestrainCondition", _loadRestrainCondition, typeof(GlassSurface.LoadRestrainCondition));

        }

        public override bool Equals(object obj)
        {

            if (ReferenceEquals(this, obj))
                return true;

            return obj is ClimateLoadCase objCasted && _loadDuration.Equals(objCasted._loadDuration) &&
                                                       _temperature.Equals(objCasted._temperature) &&
                                                       _loadRestrainCondition.Equals(objCasted._loadRestrainCondition) &&
                                                       base.Equals(obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = -17;
                hashCode = hashCode * -23 + base.GetHashCode();
                hashCode = hashCode * -23 + _loadDuration.GetHashCode();
                hashCode = hashCode * -23 + _temperature.GetHashCode();
                hashCode = hashCode * -23 + _loadRestrainCondition.GetHashCode();
                return hashCode;
            }
        }


        public static bool operator ==(ClimateLoadCase obj1, ClimateLoadCase obj2)
        {
            if (obj1 is null)
            {
                return obj2 is null;
            }

            if (ReferenceEquals(obj1, obj2))
                return true;

            return obj1.Equals(obj2);
        }

        public static bool operator !=(ClimateLoadCase obj1, ClimateLoadCase obj2)
        {
            return !(obj1 == obj2);
        }
    }
}
