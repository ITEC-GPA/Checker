using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Checkers
{
    public class Cop2011BeamCheckerAttribute : BeamCheckerAttribute
    {


        #region Public Constructors

        public Cop2011BeamCheckerAttribute(ISteelSection[] sections, ResultBeamForces[] resultBeamForces, ResultStation[] resultStations, Cop2011Checker.Cop2011Options options, string name = "")
            :base(sections, resultBeamForces, resultStations, options, name)
        {

        }

        #endregion
    }
}
