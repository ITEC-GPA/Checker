using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;

namespace GPC.Checkers.Steel.Results
{
    [Serializable]
    public class EN1993BoltResults : BoltResults, ISerializable
    {
        #region Properties

        /// <summary>
        /// Desgin shear resistance per bolt F_v,Rd.
        /// </summary>
        public double ShearResistance { get; internal set; }

        public double RatioShear { get; internal set; }

        /// <summary>
        /// Desgin tension resistance per bolt F_t,Rd.
        /// </summary>
        public double TensionResistance { get; internal set; }

        public double RatioTension { get; internal set; }

        public double RatioCombinedShearTension { get; internal set; }

        #endregion

        #region Constructor

        public EN1993BoltResults(BoltGrid.BoltPosition boltPos, ILoadCase @case, ResultBeamForces beamForces,
            StandardEN1993p11 standard, EN1993BoltChecker.EN1993BoltOptions options)
            : base(boltPos, @case, beamForces, standard, options)
        {
        }

        #endregion

    }
}
