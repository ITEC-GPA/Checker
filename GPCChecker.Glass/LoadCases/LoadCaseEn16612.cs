using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.Glasses.LoadCases
{
    public class LoadCaseEn16612 : Model.LoadCases.LoadCaseEn16612, IGlassLoadCase
    {
        private readonly double _loadDuration;

        private readonly double _temperature;

        public double LoadDuration => _loadDuration;

        public double Temperature => _temperature;

        public LoadCaseEn16612(string name, double loadDuration, double temperature, LoadCaseTypes loadCaseType, LoadCaseEn16612Types loadCasePrEnType, Guid guid)
            : base(name, loadCaseType, loadCasePrEnType, guid)
        {
            _temperature = temperature;
            _loadDuration = loadDuration;
        }

        public LoadCaseEn16612(SerializationInfo info, StreamingContext context)
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