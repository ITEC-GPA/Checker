using System;
using System.Runtime.Serialization;

namespace GPC.Checker.Glass.LoadCases
{
    public class ClimateHLoadCase : GPC.Model.LoadCases.ClimateHLoadCase, IGlassLoadCase
    {
        private double _loadDuration;
        public double LoadDuration => _loadDuration;


        public ClimateHLoadCase(string name, double loadDuration, double manufactoringHeight, double installationHeight, LoadCaseType loadCaseType, Guid guid) 
            : base(name, manufactoringHeight, installationHeight, loadCaseType, guid)
        {
            this._loadDuration = loadDuration;
        }

        public ClimateHLoadCase(SerializationInfo info, StreamingContext context)
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