using System;
using System.Runtime.Serialization;

namespace GPC.Checker.Glass.LoadCases
{
    [Serializable]
    public class LoadCase : GPC.Model.LoadCases.LoadCase, IGlassLoadCase
    {
        private double _loadDuration;
        public double LoadDuration => _loadDuration;

        public LoadCase(string name, double loadDuration, LoadCaseType loadCaseType, Guid guid) 
            : base(name, loadCaseType, guid)
        {
            this._loadDuration = loadDuration;
        }

        public LoadCase(SerializationInfo info, StreamingContext context)
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