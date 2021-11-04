using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Checkers;
using GPC.Model.Standards;
using GPC.Model.Materials;
using GPC.Checkers.Concrete.Results;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Checkers
{
	public class ModelCode2010BeamChecker : BeamChecker
	{
        #region Properties

        public StandardEN1992p11 EN1992P11 => (StandardEN1992p11)_standard;

        public ConcreteMaterialEN1992 Material => (ConcreteMaterialEN1992)_checkerAttributes.Sections.FirstOrDefault().ConcreteMaterial;

        public ModelCode2010BeamStationResult[] EN1993p11BeamStationResults => _checkerStationResult.Cast<ModelCode2010BeamStationResult>().ToArray();

        #endregion


        public ModelCode2010BeamChecker(BeamCheckerAttributes attributes, ModelCode2010Options options, StandardModelCode2010 standard)
            : base(attributes, options, standard)
        {         
        }



        public override void PerformCheck()
        {
            _checkerStationResult = PerformCheck(_checkerAttributes.Sections, BeamCheckersAttribute.SLSBeamResults, (ModelCode2010Options)_options);
        }

        public async void PerformCheckAsync()
        {
            await Task.Run(() =>
            {
                _checkerStationResult = PerformCheck(_checkerAttributes.Sections, BeamCheckersAttribute.SLSBeamResults, (ModelCode2010Options)_options);
            });
        }

        /// <param name="steelSection">section of each station</param>
        /// <param name="beamResult">result for each station and loadcase</param>
        /// <returns></returns>
        protected ModelCode2010BeamStationResult[] PerformCheck(IConcreteSection[] steelSection, BeamResult[] beamResult, ModelCode2010Options options)
		{
            throw new NotImplementedException();
		}

		public override void ULSPerformCheck()
		{
			throw new NotImplementedException();
		}

		public override void SLSPerformCheck()
		{
			throw new NotImplementedException();
		}

		public class ModelCode2010Options : BeamCheckerOptions
		{

		}
    }
}
