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
	public class EN1992p11BeamChecker : BeamChecker
	{
        #region Properties

        public StandardEN1992p11 EN1992P11 => (StandardEN1992p11)_standard;

        public ConcreteMaterialEN1992 Material => (ConcreteMaterialEN1992)_checkerAttributes.Sections.FirstOrDefault().ConcreteMaterial;

        public EN1992p11BeamStationResult[] EN1993p11BeamStationResults => _checkerStationResult.Cast<EN1992p11BeamStationResult>().ToArray();

        #endregion


        public EN1992p11BeamChecker(BeamCheckerAttributes attributes, EN1992p11Options options, StandardEN1992p11 standardEN1992P11)
            : base(attributes, options, standardEN1992P11)
        {         
        }



        public override void PerformCheck()
        {
            _checkerStationResult = PerformCheck(_checkerAttributes.Sections, BeamCheckersAttribute.SLSBeamResults, (EN1992p11Options)_options);
        }

        public async void PerformCheckAsync()
        {
            await Task.Run(() =>
            {
                _checkerStationResult = PerformCheck(_checkerAttributes.Sections, BeamCheckersAttribute.SLSBeamResults, (EN1992p11Options)_options);
            });
        }

        /// <param name="steelSection">section of each station</param>
        /// <param name="beamResult">result for each station and loadcase</param>
        /// <returns></returns>
        protected EN1992p11BeamStationResult[] PerformCheck(IConcreteSection[] steelSection, BeamResult[] beamResult, EN1992p11Options options)
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

		public class EN1992p11Options : BeamCheckerOptions
		{

		}
    }
}
