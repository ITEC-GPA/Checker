using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.ReinforcedConcrete.Checkers;
using GPC.Model.Standards;
using GPC.Model.Materials;
using GPC.Checkers.ReinforcedConcrete.Results;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.ReinforcedConcrete.Checkers
{
	public class EN1992p11PlateChecker : PlateChecker
	{
        #region Properties

        public StandardEN1992p11 EN1992P11 => (StandardEN1992p11)_standard;

        public ConcreteMaterialEN1992 Material => (ConcreteMaterialEN1992)_plateCheckersAttributes.Sections.FirstOrDefault().ConcreteMaterial;

        public EN1992p11BeamStationResult[] EN1993p11BeamStationResults => _plateStationResults.Cast<EN1992p11BeamStationResult>().ToArray();

        #endregion


        public EN1992p11PlateChecker(PlateCheckerAttributes attributes, EN1992p11Options options, StandardEN1992p11 standardEN1992P11)
            : base(attributes, options, standardEN1992P11)
        {         
        }



        public override void PerformCheck()
        {
            _plateStationResults = PerformCheck(_plateCheckersAttributes.Sections, _plateCheckersAttributes.Results, (EN1992p11Options)_options);
        }

        public async void PerformCheckAsync()
        {
            await Task.Run(() =>
            {
                _plateStationResults = PerformCheck(_plateCheckersAttributes.Sections, _plateCheckersAttributes.Results, (EN1992p11Options)_options);
            });
        }

        /// <param name="steelSection">section of each station</param>
        /// <param name="plateResults">result for each station and loadcase</param>
        /// <returns></returns>
        protected EN1992p11PlateStationResult[] PerformCheck(IConcreteSection[] steelSection, PlateResult[] plateResults, EN1992p11Options options)
		{
            throw new NotImplementedException();
		}



        public class EN1992p11Options : PlateCheckerOptions
		{

		}
    }
}
