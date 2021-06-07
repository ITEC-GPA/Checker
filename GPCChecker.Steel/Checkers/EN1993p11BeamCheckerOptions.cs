using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Checkers
{
    public class EN1993p11BeamCheckerOptions : BeamCheckerOptions
    {


        #region Public Constructors

        public EN1993p11BeamCheckerOptions(ISteelSection sections, ResultBeamForces[] resultBeamForces, ResultStation[] resultStations, EN1993p11Checker.EN1993p11Options options)
            :base(sections, resultBeamForces, resultStations, options)
        {

        }

        #endregion
    }
}
