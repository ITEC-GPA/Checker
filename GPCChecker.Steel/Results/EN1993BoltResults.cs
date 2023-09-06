using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Results
{
    /// <summary>
    /// Results specific to EN1993.
    /// </summary>
    [Serializable]
    public class EN1993BoltResults : ENCommonBoltResults
    {
        #region Properties

        public EN1993BoltChecker.EN1993BoltOptions eN1993BoltOptions => Options as EN1993BoltChecker.EN1993BoltOptions;

        #endregion

        #region Constructor

        public EN1993BoltResults(BoltPosition boltPos, ILoadCase @case, ResultBeamForces beamForces,
            StandardEN1993p11 standard, EN1993BoltChecker.EN1993BoltOptions options)
            : base(boltPos, @case, beamForces, standard, options)
        {
        }

        #endregion
    }
}
