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

        private double _axial1WorkingRatio;
        private double _axial2WorkingRatio;
        private double _shear1WorkingRatio;
        private double _shear2WorkingRatio;
        private double _bendingMoment1WorkingRatio;
        private double _bendingMoment2WorkingRatio;
        private double _torsionalMomentWorkingRatio;
        private double _lateraTorsionalBucklingWorkingRatio;
        private double _interactioWorkingRatio;
        private double _maxWorkingRatio;

        private double _axialTensionRd;
        private double _axialCompression1Rd;
        private double _axialCompression2Rd;
        private double _shear1Rd;
        private double _shear2Rd;
        private double _bendingMoment1Rd;
        private double _bendingMoment2Rd;
        private double _torsionalMomentRd;
        private double _lateralTorsionalMomentRd;

        private readonly ISteelSection _section;
        private readonly ResultBeamForces _forces;
        private readonly ResultStation _station;
        private readonly ILoadCase _loadCase;

        #endregion


        #region Properties

        protected double Axial1WorkingRatio => _axial1WorkingRatio;

        protected double Axial2WorkingRatio => _axial2WorkingRatio;

        protected double Shear1WorkingRatio => _shear1WorkingRatio;

        protected double Shear2WorkingRatio => _shear2WorkingRatio;

        protected double BendingMoment1WorkingRatio => _bendingMoment1WorkingRatio;

        protected double BendingMoment2WorkingRatio => _bendingMoment2WorkingRatio;

        protected double TorsionalMomentWorkingRatio => _torsionalMomentWorkingRatio;

        protected double LateralTorsionalBucklingWorkingRatio => _lateraTorsionalBucklingWorkingRatio;

        protected double InteractionWorkingRatio => _interactioWorkingRatio;

        protected double WorkingRatio { get; set; }

        protected ResultBeamForces ResultBeamForces => _forces;

        protected ResultStation Station => _station;

        protected ILoadCase LoadCase => _loadCase;

        protected ISteelSection Section => _section;
        public double AxialTensionCapacity { get => _axialTensionRd; set => _axialTensionRd = value; }

        public double AxialCompression1Capacity { get => _axialCompression1Rd; set => _axialCompression1Rd = value; }

        public double AxialCompression2Capacity { get => _axialCompression2Rd; set => _axialCompression2Rd = value; }

        public double Shear1Capacity { get => _shear1Rd; set => _shear1Rd = value; }

        public double Shear2Capacity { get => _shear2Rd; set => _shear2Rd = value; }

        public double BendingMoment1Capacity { get => _bendingMoment1Rd; set => _bendingMoment1Rd = value; }

        public double BendingMoment2Capacity { get => _bendingMoment2Rd; set => _bendingMoment2Rd = value; }

        public double TorsionMomentCapacity { get => _torsionalMomentRd; set => _torsionalMomentRd = value; }

        public double LateralTosionalBucklingCapacity { get => _lateralTorsionalMomentRd; set => _lateralTorsionalMomentRd = value; }


        #endregion


        #region Constructor

        internal BeamStationCheckerResults(ISteelSection section, ILoadCase loadCase, ResultBeamForces forces, ResultStation station)    
        {
            _section = section;
            _forces = forces;
            _station = station;
            _loadCase = loadCase;
        }

        #endregion


        #region Public Method

        internal void SetCapacity(double axialTension, double axialBuck1, double axialBuck2, double shear1, double shear2, double bending1, double bending2, double tors, double latTors)
        {
            _axialTensionRd = axialTension;
            _axialCompression1Rd = axialBuck1;
            _axialCompression2Rd = axialBuck2;
            _shear1Rd = shear1;
            _shear2Rd = shear2;
            _bendingMoment1Rd = bending1;
            _bendingMoment2Rd = bending2;
            _torsionalMomentRd = tors;
            _lateralTorsionalMomentRd = latTors;
        }

        internal void SetWorkingRatio(double axialTensionWR, double axialBuck1WR, double axialBuck2WR, double shear1WR, double shear2WR, double bending1WR, 
                                        double bending2WR, double torsWR, double latTorsWR, double interactioNWR, double maxWR)
        {
            if (axialTensionWR != 0)
            {
                _axial1WorkingRatio = axialTensionWR;
                _axial2WorkingRatio = axialTensionWR;
            }
            else
            {
                _axial1WorkingRatio = axialBuck1WR;
                _axial2WorkingRatio = axialBuck2WR;
            }            
            _shear1WorkingRatio = shear1WR; 
            _shear2WorkingRatio = shear2WR;
            _bendingMoment1WorkingRatio = bending1WR;
            _bendingMoment2WorkingRatio = bending2WR;
            _torsionalMomentWorkingRatio = torsWR;
            _lateraTorsionalBucklingWorkingRatio = latTorsWR;
            _interactioWorkingRatio = interactioNWR;
            _maxWorkingRatio = maxWR;
        }

        #endregion
    }
}
