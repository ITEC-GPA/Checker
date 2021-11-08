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


#if DEBUG

    /// <summary>
    /// The purpose of this class is to perform a check of a single beam between all the ILoadCases
    /// </summary>

    [Serializable]
    public abstract class PlateChecker : Checker, ISerializable
    {
        #region Properties

        public PlateCheckerAttributes PlateCheckerAttribute => (PlateCheckerAttributes)_checkerAttributes;

        public PlateStationResults[] PlateStationResult => (PlateStationResults[])_checkerStationResult;

        public PlateCheckerOptions PlateCheckerOption => (PlateCheckerOptions)_options;

        public ILoadCase[] LoadCases => GetLoadCases();

        public string PlateName => PlateCheckerAttribute.Name;


        #endregion


        #region Constructor

        public PlateChecker(PlateCheckerAttributes plateCheckerAttributes, PlateCheckerOptions options, Standard standard, string name = "")
            : this(plateCheckerAttributes, options, standard, Model.ModelObjectId.IDUNASSIGNED, name)
        {
            _errorLog = new List<string>();
        }

        public PlateChecker(PlateCheckerAttributes plateCheckerAttributes, PlateCheckerOptions options, Standard standard, int id, string name = "")
            : base(plateCheckerAttributes, options, standard, id, name)
        {

        }

        protected PlateChecker(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }


        #endregion


        #region Public abstract method

        public override void PerformCheck()
        {
            throw new Exception();
        }

        #endregion


        /// <returns>The unique ILoadCases array</returns>
        protected ILoadCase[] GetLoadCases()
        {
            List<ILoadCase> list = new List<ILoadCase>();

            list.AddRange(PlateCheckerAttribute.ULSPlateResults.Select(i => i.Case).Distinct());
            list.AddRange(PlateCheckerAttribute.SLSPlateResults.Select(i => i.Case).Distinct());

            return list.ToArray();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        public override bool Equals(object obj)
        {
            // TODO: implementare 
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            // TODO: implementare 
            throw new NotImplementedException();
        }

        public abstract class PlateCheckerOptions : Options
        {
            public PlateCheckerOptions()
            {

            }

        }
    } 
#endif
}
