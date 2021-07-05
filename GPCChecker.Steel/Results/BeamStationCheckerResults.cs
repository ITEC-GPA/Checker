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
    public class BeamStationCheckerResults
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
        protected double _interaction878WorkingRatio;
        protected double _interaction879WorkingRatio;
        protected double _interaction880WorkingRatio;
        protected double _interaction881WorkingRatio;

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

        protected Cop2011BeamChecker.SectionClass _axialCompressionClass;
        protected Cop2011BeamChecker.SectionClass _bendingCompressionClass;

        protected readonly ISteelSection _section;
        protected readonly ResultBeamForces _forces;
        protected readonly ResultStation _station;
        protected readonly ILoadCase _loadCase;

        #endregion


        #region Properties

        public double AxialTensionWorkingRatio => _axialTensionWorkingRatio;

        public double AxialCompressionWorkingRatio => _axialCompressionWorkingRatio;

        public double AxialBuckling1WorkingRatio => _axialBuckling1WorkingRatio;

        public double AxialBuckling2WorkingRatio => _axialBuckling2WorkingRatio;

        public double Shear1WorkingRatio => _shear1WorkingRatio;

        public double Shear2WorkingRatio => _shear2WorkingRatio;

        public double BendingMoment1WorkingRatio => _bendingMoment1WorkingRatio;

        public double BendingMoment2WorkingRatio => _bendingMoment2WorkingRatio;

        public double TorsionalMomentWorkingRatio => _torsionalMomentWorkingRatio;

        public double LateralTorsionalBucklingWorkingRatio => _lateraTorsionalBucklingWorkingRatio;

        public double Interaction878WorkingRatio => _interaction878WorkingRatio;

        public double Interaction879WorkingRatio => _interaction879WorkingRatio;

        public double Interaction880WorkingRatio => _interaction880WorkingRatio;

        public double Interaction881WorkingRatio => _interaction881WorkingRatio;

        public double WorkingRatio => GetMaxWorkingRatio();

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

        public Cop2011BeamChecker.SectionClass AxialCompressionClass => _axialCompressionClass;

        public Cop2011BeamChecker.SectionClass BendingCompressionClass => _bendingCompressionClass;

        #endregion


        #region Constructor

        internal BeamStationCheckerResults(ISteelSection section, ILoadCase loadCase, ResultBeamForces forces, ResultStation station)    
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _forces = forces ?? throw new ArgumentNullException(nameof(forces));
            _station = station ?? throw new ArgumentNullException(nameof(station));
            _loadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        }

        #endregion


        #region Public Method

        internal void SetCapacity(double axialTension, double axialCompression, double axialBuck1, double axialBuck2, double shear1, double shear2, double bending1, double bending2, double latTors)
        {
            _axialTensionRd = axialTension < 0 ? throw new ArgumentException($"AxialTensionRd cannot be lower than zero") : axialTension;
            _axialCompressionRd = axialCompression < 0 ? throw new ArgumentException($"AxialCompressionRd cannot be lower than zero") : axialCompression;
            _axialBuckling1Rd = axialBuck1 < 0 ? throw new ArgumentException($"AxialBuckling1Rd cannot be lower than zero") : axialBuck1;
            _axialBuckling2Rd = axialBuck2 < 0 ? throw new ArgumentException($"AxialBuckling2Rd cannot be lower than zero") : axialBuck2;
            _shear1Rd = shear1 < 0 ? throw new ArgumentException($"Shear1Rd cannot be lower than zero") : shear1;
            _shear2Rd = shear2 < 0 ? throw new ArgumentException($"Shear2Rd cannot be lower than zero") : shear2;
            _bendingMoment1Rd = bending1 < 0 ? throw new ArgumentException($"BendingMoment1Rd cannot be lower than zero") : bending1;
            _bendingMoment2Rd = bending2 < 0 ? throw new ArgumentException($"BendingMoment2Rd cannot be lower than zero") : bending2;
            _lateralTorsionalMomentRd = latTors < 0 ? throw new ArgumentException($"LateralTorsionalBucklingRd cannot be lower than zero") : latTors;
        }

        internal void SetWorkingRatio(double axialTensionWR, double axialCompressionWR, double axialBuck1WR, double axialBuck2WR, double shear1WR, double shear2WR, double bending1WR, 
                                        double bending2WR, double latTorsWR, double interaction878WR, double interaction879WR, double interaction880WR, double interaction881WR)
        {
            _axialTensionWorkingRatio = axialTensionWR < 0 ? throw new ArgumentException($"AxialTensionWorkingRatio cannot be lower than zero") : axialTensionWR;
            _axialCompressionWorkingRatio = axialCompressionWR < 0 ? throw new ArgumentException($"AxialCompressionWorkingRatio cannot be lower than zero") : axialCompressionWR;
            _axialBuckling1WorkingRatio = axialBuck1WR < 0 ? throw new ArgumentException($"AxialBuckling1AxisWorkingRatio cannot be lower than zero") : axialBuck1WR;
            _axialBuckling2WorkingRatio = axialBuck2WR < 0 ? throw new ArgumentException($"AxialBuckling2AxisWorkingRatio cannot be lower than zero") : axialBuck2WR;
            _shear1WorkingRatio = shear1WR < 0 ? throw new ArgumentException($"Shear1WorkingRatio cannot be lower than zero") : shear1WR;
            _shear2WorkingRatio = shear2WR < 0 ? throw new ArgumentException($"Shear2WorkingRatio cannot be lower than zero") : shear2WR;
            _bendingMoment1WorkingRatio = bending1WR < 0 ? throw new ArgumentException($"BendingMoment1AxisWorkingRatio cannot be lower than zero") : bending1WR;
            _bendingMoment2WorkingRatio = bending2WR < 0 ? throw new ArgumentException($"BendingMoment2AxisWorkingRatio cannot be lower than zero") : bending2WR;
            _lateraTorsionalBucklingWorkingRatio = latTorsWR < 0 ? throw new ArgumentException($"LateralTorsionalWorkingRatio cannot be lower than zero") : latTorsWR;
            _interaction878WorkingRatio = interaction878WR < 0 ? throw new ArgumentException($"InteractionWorkingRatio cannot be lower than zero") : interaction878WR;
            _interaction879WorkingRatio = interaction879WR < 0 ? throw new ArgumentException($"InteractionWorkingRatio cannot be lower than zero") : interaction879WR;
            _interaction880WorkingRatio = interaction880WR < 0 ? throw new ArgumentException($"InteractionWorkingRatio cannot be lower than zero") : interaction880WR;
            _interaction881WorkingRatio = interaction881WR < 0 ? throw new ArgumentException($"InteractionWorkingRatio cannot be lower than zero") : interaction881WR;
        }

        internal void SetClasses(Cop2011BeamChecker.SectionClass axialSectionClass, Cop2011BeamChecker.SectionClass bendingSectionClass)
        {
            _axialCompressionClass = axialSectionClass;
            _bendingCompressionClass = bendingSectionClass;
        }

        internal double GetMaxWorkingRatio()
        {
            List<double> workingRatioList = new List<double>() { _axialTensionWorkingRatio, _axialCompressionWorkingRatio, _axialBuckling1WorkingRatio, 
                                                                _axialBuckling2WorkingRatio, _shear1WorkingRatio, _shear2WorkingRatio, _bendingMoment1WorkingRatio, 
                                                                _bendingMoment2WorkingRatio, _torsionalMomentWorkingRatio, _lateraTorsionalBucklingWorkingRatio, 
                                                                _interaction878WorkingRatio, _interaction879WorkingRatio, _interaction880WorkingRatio, _interaction881WorkingRatio,};

            return workingRatioList.Max();
        }

        #endregion
    }
}
