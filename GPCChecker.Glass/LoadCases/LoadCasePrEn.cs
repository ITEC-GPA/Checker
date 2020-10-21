using System;
using System.Runtime.Serialization;

namespace GPC.Checker.Glass.LoadCases
{
    public class LoadCasePrEn : GPC.Model.LoadCases.LoadCasePrEn, IGlassLoadCase
    {
        private double _loadDuration;
        public double LoadDuration => _loadDuration;


        public LoadCasePrEn(string name, LoadCaseType loadCaseType, LoadCasePrEnType loadCasePrEnType, Guid guid) 
            : base(name, loadCaseType, loadCasePrEnType, guid)
        {

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
