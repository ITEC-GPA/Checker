using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;
using GPC.Model.Sections;
using GPC.Model.Combinations;
using System.Runtime.Serialization;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.Attributes;

namespace GPC.Checkers.Concrete.Checkers
{
    /// <summary>
    /// The purpose of this class is to perform a check of a single beam between all the ILoadCases
    /// </summary>
    
    [Serializable]
    public abstract class BeamChecker : Checker, ISerializable
    {
        #region Properties

        public BeamCheckerAttributes BeamCheckersAttribute => (BeamCheckerAttributes)_checkerAttributes;

        public BeamCheckerOptions BeamCheckerOption => (BeamCheckerOptions)_options;

        public BeamStationResults[] BeamStationCheckerResults => (BeamStationResults[])_checkerStationResult; 

        public double BeamLength => BeamCheckersAttribute.Length;

        public ILoadCase[] LoadCases => GetLoadCases();

        public string BeamName => BeamCheckersAttribute.Name;

        #endregion


        #region Constructor

        public BeamChecker(BeamCheckerAttributes beamCheckerAttributes, BeamCheckerOptions options, Standard standard, string name = "")
            : this(beamCheckerAttributes, options, standard, Model.ModelObjectId.IDUNASSIGNED, name)
        {

        }

        public BeamChecker(BeamCheckerAttributes beamCheckerAttributes, BeamCheckerOptions options, Standard standard, int id, string name = "") 
            : base(beamCheckerAttributes, options, standard, id, name)
        {

        }

        protected BeamChecker(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            
        }

        #endregion


        /// <returns>The unique ILoadCases array</returns>
        protected ILoadCase[] GetLoadCases()
        {
            List<ILoadCase> list = new List<ILoadCase>();

            list.AddRange(BeamCheckersAttribute.ULSBeamResults.Select(i => i.Case).Distinct());
            list.AddRange(BeamCheckersAttribute.SLSBeamResults.Select(i => i.Case).Distinct());

            return list.ToArray();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamChecker objCasted) && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return -391 * base.GetHashCode();
            }
        }

        public abstract class BeamCheckerOptions : Options
        {
           

            public BeamCheckerOptions()
            {
               
            }


        }
    }
}
