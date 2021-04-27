using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.Glasses.LoadCases
{
    public class ClimateTLoadCase : GPC.Model.LoadCases.ClimateHLoadCase, IGlassLoadCase
    {
        private double _temperature;

        private double _loadDuration;

        public double LoadDuration => _loadDuration;

        public double Temperature => _temperature;

        public ClimateTLoadCase(string name, double loadDuration, double temperature, double manufactoringHeight, double installationHeight, LoadCaseTypes loadCaseType, Guid guid) 
            : base(name, manufactoringHeight, installationHeight, loadCaseType, guid)
        {
            this._loadDuration = loadDuration;
            this._temperature = temperature;
        }

        public ClimateTLoadCase(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _loadDuration = info.GetDouble("LoadDuration");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("LoadDuration", _loadDuration);
        }
    }
}
