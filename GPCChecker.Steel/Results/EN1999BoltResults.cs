using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Text;

namespace GPC.Checkers.Steel.Results
{
    /// <summary>
    /// Results specific to EN1999.
    /// </summary>
    [Serializable]
    public class EN1999BoltResults : ENCommonBoltResults
    {
        #region Properties

        public EN1999BoltChecker.EN1999BoltOptions eN1999BoltOptions => Options as EN1999BoltChecker.EN1999BoltOptions;

        #endregion

        #region Constructor

        public EN1999BoltResults(BoltPosition boltPos, ILoadCase loadCase, ResultBeamForces beamForces,
            StandardEN1999p11 standard, EN1999BoltChecker.EN1999BoltOptions options, string name = "")
            : base(boltPos, loadCase, beamForces, standard, options, name)
        {
        }

        #endregion
    }
}
