using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Checkers
{
    public class EN1993p11BeamCheckerAttributes : BeamCheckerAttributes
    {


        #region Public Constructors

        public EN1993p11BeamCheckerAttributes(ISteelSection sections, BeamResult[] resultBeamForces, string name = "")
            :base(sections, resultBeamForces, name)
        {

        }

        #endregion
    }
}
