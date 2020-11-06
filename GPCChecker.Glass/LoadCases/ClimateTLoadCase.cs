using System;
using System.Runtime.Serialization;

namespace GPC.Checker.Glasses.LoadCases
{
    public class ClimateTLoadCase : GPC.Model.LoadCases.ClimateHLoadCase, IGlassLoadCase
    {
        private double _loadDuration;
        public double LoadDuration => _loadDuration;


        public ClimateTLoadCase(string name, double loadDuration, double manufactoringHeight, double installationHeight, LoadCaseType loadCaseType, Guid guid) 
            : base(name, manufactoringHeight, installationHeight, loadCaseType, guid)
        {
            this._loadDuration = loadDuration;
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