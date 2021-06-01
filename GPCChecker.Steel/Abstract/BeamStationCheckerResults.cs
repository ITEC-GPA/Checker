using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Results
{
    public abstract class BeamStationCheckerResults
    {

        #region Variables

        private readonly double _axialWorkingRatio;
        private readonly double _shearWorkingRatio;
        private readonly double _bendingMoment1WorkingRatio;
        private readonly double _bendingMoment2WorkingRatio;
        private readonly double _torsionalMomentWorkingRatio;
        private readonly double _interactioWorkingRatio;

        private readonly ISteelSection _section;
        private readonly ResultBeamForces _forces;
        private readonly ResultStation _station;
        private readonly ILoadCase _loadCase;

        #endregion


        #region Properties

        public double AxialWorkingRatio => _axialWorkingRatio;

        public double ShearWorkingRatio => _shearWorkingRatio;

        public double BendingMoment1WorkingRatio => _bendingMoment1WorkingRatio;

        public double BendingMoment2WorkingRatio => _bendingMoment2WorkingRatio;

        public double TorsionalMomentWorkingRatio => _torsionalMomentWorkingRatio;

        public double InteractionWorkingRatio => _interactioWorkingRatio;

        public double WorkingRatio { get; set; }

        public ResultBeamForces ResultBeamForces => _forces;

        public ResultStation Station => _station;

        public ILoadCase LoadCase => _loadCase;

        public ISteelSection Section => _section;

        #endregion


        #region Constructor

        public BeamStationCheckerResults(ISteelSection section, ResultBeamForces forces)    // load case e stazione
        {
            _section = section;
            _forces = forces;                     
        }

        #endregion


        #region Public Method



        #endregion
    }
}
