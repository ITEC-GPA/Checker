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
using GPC.Checkers.Steel.BeamChecker;

namespace GPC.Checkers.Steel.Results
{
    public abstract class BeamStationCheckerResults
    {

        #region Variables

        protected double _axialTensionWorkingRatio;
        protected double _axialCompressionWorkingRatio;
        protected double _axialBuckling1WorkingRatio;
        protected double _axialBuckling2WorkingRatio;
        protected double _shear1WorkingRatio;
        protected double _shear2WorkingRatio;
        protected double _bendingMoment1WorkingRatio;
        protected double _bendingMoment2WorkingRatio;
        protected double _torsionalMomentWorkingRatio;
        protected double _lateraTorsionalBucklingWorkingRatio;
        protected double _interactioWorkingRatio;

        protected double _axialTensionRd;
        protected double _axialCompressionRd;
        protected double _axialBuckling1Rd;
        protected double _axialBuckling2Rd;
        protected double _shear1Rd;
        protected double _shear2Rd;
        protected double _bendingMoment1Rd;
        protected double _bendingMoment2Rd;
        protected double _torsionalMomentRd;
        protected double _lateralTorsionalMomentRd;

        protected Cop2011BeamChecker.SectionClass _class;
        protected readonly ISteelSection _section;
        protected readonly ResultBeamForces _forces;
        protected readonly ResultStation _station;
        protected readonly ILoadCase _loadCase;

        #endregion


        #region Properties

        public double Axial1WorkingRatio => _axialTensionWorkingRatio;

        public double Axial2WorkingRatio => _axialCompressionWorkingRatio;

        public double AxialBuckling1WorkingRatio => _axialBuckling1WorkingRatio;

        public double AxialBuckling2WorkingRatio => _axialBuckling2WorkingRatio;

        public double Shear1WorkingRatio => _shear1WorkingRatio;

        public double Shear2WorkingRatio => _shear2WorkingRatio;

        public double BendingMoment1WorkingRatio => _bendingMoment1WorkingRatio;

        public double BendingMoment2WorkingRatio => _bendingMoment2WorkingRatio;

        public double TorsionalMomentWorkingRatio => _torsionalMomentWorkingRatio;

        public double LateralTorsionalBucklingWorkingRatio => _lateraTorsionalBucklingWorkingRatio;

        public double InteractionWorkingRatio => _interactioWorkingRatio;

        public double WorkingRatio => GetMaxWorkingRation();

        public double AxialTensionCapacity => _axialTensionRd;

        public double AxialCompressionCapacity => _axialCompressionRd;

        public double AxialBuckling1Capacity => _axialBuckling1Rd;

        public double AxialBuckling2Capacity => _axialBuckling2Rd;

        public double Shear1Capacity => _shear1Rd;

        public double Shear2Capacity => _shear2Rd;

        public double BendingMoment1Capacity => _bendingMoment1Rd;

        public double BendingMoment2Capacity => _bendingMoment2Rd;

        public double TorsionMomentCapacity => _torsionalMomentRd;

        public double LateralTosionalBucklingCapacity => _lateralTorsionalMomentRd;

        public ResultBeamForces ResultBeamForces => _forces;

        public ResultStation Station => _station;

        public ILoadCase LoadCase => _loadCase;

        public ISteelSection Section => _section;

        public Cop2011BeamChecker.SectionClass Class => _class;

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

        internal void SetCapacity(double axialTension, double axialCompression, double axialBuck1, double axialBuck2, double shear1, double shear2, double bending1, double bending2, double tors, double latTors)
        {
            _axialTensionRd = axialTension;
            _axialCompressionRd = axialCompression;
            _axialBuckling1Rd = axialBuck1;
            _axialBuckling2Rd = axialBuck2;
            _shear1Rd = shear1;
            _shear2Rd = shear2;
            _bendingMoment1Rd = bending1;
            _bendingMoment2Rd = bending2;
            _torsionalMomentRd = tors;
            _lateralTorsionalMomentRd = latTors;
        }

        internal void SetWorkingRatio(double axialTensionWR, double axialCompressionWR, double axialBuck1WR, double axialBuck2WR, double shear1WR, double shear2WR, double bending1WR, 
                                        double bending2WR, double torsWR, double latTorsWR, double interactioNWR)
        {
            _axialTensionWorkingRatio = axialTensionWR;
            _axialCompressionWorkingRatio = axialCompressionWR;
            _axialBuckling1WorkingRatio = axialBuck1WR;
            _axialBuckling2WorkingRatio = axialBuck2WR;
            _shear1WorkingRatio = shear1WR; 
            _shear2WorkingRatio = shear2WR;
            _bendingMoment1WorkingRatio = bending1WR;
            _bendingMoment2WorkingRatio = bending2WR;
            _torsionalMomentWorkingRatio = torsWR;
            _lateraTorsionalBucklingWorkingRatio = latTorsWR;
            _interactioWorkingRatio = interactioNWR;
        }

        internal void SetClass(Cop2011BeamChecker.SectionClass sectionClass)
        {
            _class = sectionClass;
        }

        internal double GetMaxWorkingRation()
        {
            List<double> workingRatioList = new List<double>() { _axialTensionWorkingRatio, _axialCompressionWorkingRatio, _axialBuckling1WorkingRatio, 
                                                                _axialBuckling2WorkingRatio, _shear1WorkingRatio, _shear2WorkingRatio, _bendingMoment1WorkingRatio, 
                                                                _bendingMoment2WorkingRatio, _torsionalMomentWorkingRatio, _lateraTorsionalBucklingWorkingRatio, 
                                                                _interactioWorkingRatio};
            return workingRatioList.Max();
        }

        #endregion
    }
}
