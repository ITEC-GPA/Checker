using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Cop2011;
using GPC.Checkers.Steel.EuroCode;

namespace GPCCheckers.Steel.Generic
{
    public class EN1993p11BeamChecker : BeamChecker
    {


        #region Public Constructors

        public EN1993p11BeamChecker(ISteelSection sections, ResultBeamForces[] resultBeamForces, ResultStation[] resultStations, EN1993p11Checker.EN1993p11Options options)
            :base(sections, resultBeamForces, resultStations, options)
        {

        }

        #endregion
    }
}
