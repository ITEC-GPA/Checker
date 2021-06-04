using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Cop2011;
using GPCCheckers.Steel.Generic;

namespace GPCCheckers.Steel.Cop2011
{
    public class Cop2011BeamChecker : BeamChecker
    {


        #region Public Constructors

        public Cop2011BeamChecker(ISteelSection sections, ResultBeamForces[] resultBeamForces, ResultStation[] resultStations, Cop2011Checker.Cop2011Options options)
            :base(sections, resultBeamForces, resultStations, options)
        {

        }

        #endregion
    }
}
