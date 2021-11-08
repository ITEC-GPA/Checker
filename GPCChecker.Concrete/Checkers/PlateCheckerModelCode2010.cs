using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Checkers
{
    public class PlateCheckerModelCode2010 : PlateChecker
    {
        #region Properties

        public StandardEN1992p11 EN1992P11 => (StandardEN1992p11)_standard;

        public ConcreteMaterialEN1992 Material => (ConcreteMaterialEN1992)_checkerAttributes.Sections.FirstOrDefault().ConcreteMaterial;

        public ModelCode2010BeamStationResult[] EN1993p11BeamStationResults => _checkerStationResult.Cast<ModelCode2010BeamStationResult>().ToArray();

        #endregion


        public PlateCheckerModelCode2010(PlateCheckerAttributes attributes, EN1992p11Options options, StandardEN1992p11 standardEN1992P11)
            : base(attributes, options, standardEN1992P11)
        {

        }


        public override void SLSPerformCheck()
        {
            _checkerStationResult = PerformCheck(_checkerAttributes.Sections, PlateCheckerAttribute.SLSPlateResults, (EN1992p11Options)_options);
        }

        public override void ULSPerformCheck()
        {
            _checkerStationResult = PerformCheck(_checkerAttributes.Sections, PlateCheckerAttribute.ULSPlateResults, (EN1992p11Options)_options);
        }

        public override void PerformCheck()
        {
            base.PerformCheck();
        }


        /// <param name="steelSection">section of each station</param>
        /// <param name="plateResults">result for each station and loadcase</param>
        /// <returns></returns>
        protected ModelCode2010PlateStationResult[] PerformCheck(IConcreteSection[] steelSection, PlateResult[] plateResults, EN1992p11Options options)
        {
            throw new NotImplementedException();
        }



        public class EN1992p11Options : PlateCheckerOptions
        {

        }
    }
}
