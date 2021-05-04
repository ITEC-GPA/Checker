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

        public double LoadDuration => _loadDuration;

        public double Temperature => _temperature;

        public ClimateLoadCase(string name, Seasons season, ClimateTypes climateType, double manufactoring, double installation, 
            double loadDuration, double temperature)
            : base(name, season, climateType, manufactoring, installation)
        {
            _loadDuration = loadDuration;
            _temperature = temperature;
        }

        public ClimateLoadCase(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _loadDuration = info.GetDouble("LoadDuration");
            _temperature = info.GetDouble("Temperature");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("LoadDuration", _loadDuration);
            info.AddValue("Temperature", _temperature);
        }
    }
}
