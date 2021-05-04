using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.Glasses.LoadCases
{

    public class ClimateLoadCase : GPC.Model.LoadCases.ClimateLoadCase, IGlassLoadCase
    {
        private readonly double _loadDuration;

        private readonly double _temperature;

        public double LoadDuration => _loadDuration;

        public double Temperature => _temperature;


        public ClimateLoadCase(string name, Seasons season, ClimateTypes climateType, double manufactoring, double installation, double loadDuration, double temperature) 
            : base(name, season, climateType, manufactoring, installation)
        {
            _loadDuration = loadDuration;
            _temperature = temperature;
        }

        public ClimateLoadCase(string name, Seasons season, ClimateTypes climateType, double manufactoring, double installation, double loadDuration, double temperature, Guid guid) 
            : base(name, season, climateType, manufactoring, installation, guid)
        {
            _loadDuration = loadDuration;
            _temperature = temperature;
        }

        public ClimateLoadCase(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            throw new NotImplementedException();
        }
    }
}