using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Results;
using GPC.Model;
using GPC.Model.Combinations;
using GPC.Model.LoadCases;
using GPC.Model.Sections;
using GPC.Model.Standards;

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


        public BeamChecker(BeamCheckerAttributes beamCheckerAttributes, BeamCheckerOptions options, Standard standard, int id = ModelObjectId.IDUNASSIGNED, string name = "")
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
            if (obj == null)
                return false;

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
