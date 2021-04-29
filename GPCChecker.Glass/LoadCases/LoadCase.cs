using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.Glasses.LoadCases
{
    [Serializable]
    public class LoadCase : Model.LoadCases.LoadCase, IGlassLoadCase
    {
        private readonly double _loadDuration;

        private readonly double _temperature;

        public double LoadDuration => _loadDuration;

        public double Temperature => _temperature;

        public LoadCase(string name, double loadDuration, double temperature, LoadCaseTypes loadCaseType)
            : this(name, loadDuration, temperature, loadCaseType, Guid.NewGuid())
        {
        }

        public LoadCase(string name, double loadDuration, double temperature, LoadCaseTypes loadCaseType, Guid guid)
            : base(name, loadCaseType, guid)
        {
            _loadDuration = loadDuration;
            _temperature = temperature;
        }

        public LoadCase(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _loadDuration = info.GetDouble("LoadDuration");
            _temperature = info.GetDouble("Temperature");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("LoadDuration", _loadDuration);
            info.AddValue("LoadDuration", _temperature);
        }
    }
}