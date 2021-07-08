using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Checkers
{
    public class Cop2011BeamCheckerAttribute : BeamCheckerAttributes
    {

        #region Public Constructors

        public Cop2011BeamCheckerAttribute(ISteelSection sections, BeamResult[] resultBeamForces, string name = "")
            :base(sections, resultBeamForces, name)
        {

        }

        #endregion
    }
}
