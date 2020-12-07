using System;
using System.Runtime.Serialization;

namespace GPC.Checker.Glasses.LoadCases
{
    public class LoadCasePrEn : GPC.Model.LoadCases.LoadCasePrEn, IGlassLoadCase
    {
        private double _loadDuration;

        private double _temperature;

        public double LoadDuration => _loadDuration;

        public double Temperature => _temperature;

        public LoadCasePrEn(string name, double loadDuration, double temperature, LoadCaseType loadCaseType, LoadCasePrEnType loadCasePrEnType, Guid guid) 
            : base(name, loadCaseType, loadCasePrEnType, guid)
        {
            this._temperature = temperature;
            this._loadDuration = loadDuration;
        }

        public LoadCasePrEn(SerializationInfo info, StreamingContext context)
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
