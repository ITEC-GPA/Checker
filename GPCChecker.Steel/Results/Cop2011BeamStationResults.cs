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
using GPC.Checkers.Steel.Checkers;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Results
{
    /// <summary>
    /// This class contains the result of a check performed on a beam station with a given ILoadCase
    /// </summary>

    [Serializable]
    public class Cop2011BeamStationResults : BeamStationResults, ISerializable
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
        protected double _pMMWorkingRatio;
        protected double _pMMWorkingRatioBendingSecondOrderEffect;
        protected double _pMMWorkingRatioAxialSecondOrderEffect;
        protected double _pMMWorkingRatioLateralTorsionalBuckling;

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

        protected double _lenghtAxialBuckling1;
        protected double _lenghtAxialBuckling2;
        protected double _lenghtLateralTorsionalBuckling;

        protected double _py;
        protected double _epsilon;

        protected Cop2011Checker.SectionClass _axialCompressionClass;
        protected Cop2011Checker.SectionClass _bendingCompressionClass;

        protected Cop2011Checker.Cop2011Options.BuckingCurves _bucklingCurve1;
        protected Cop2011Checker.Cop2011Options.BuckingCurves _bucklingCurve2;

        protected double _lambdaAxialBuckling1;
        protected double _lambdaAxialBuckling2;
        protected double _pEAxialBuckling1;
        protected double _pEAxialBuckling2;
        protected double _phiCAxialBuckling1;
        protected double _phiCAxialBuckling2;
        protected double _pCAxialBuckling1;
        protected double _pCAxialBuckling2;

        protected double _lambdaLTBuckling;
        protected double _lambda0LTBuckling;
        protected double _pELTBuckling;
        protected double _phiLTBuckling;
        protected double _pBLTBuckling;

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
        
        /// <summary>
        /// Cop2011 Eq. 8.78
        /// </summary>
        public double PMMWorkingRatio => _pMMWorkingRatio;

        /// <summary>
        /// Cop2011 Eq. 8.79
        /// </summary>
        public double PMMWorkingRatioBendingSecondOrderEffect => _pMMWorkingRatioBendingSecondOrderEffect;

        /// <summary>
        /// Cop2011 Eq. 8.80
        /// </summary>
        public double PMMWorkingRatioAxialSecondOrderEffect => _pMMWorkingRatioAxialSecondOrderEffect;

        /// <summary>
        /// Cop2011 Eq. 8.81
        /// </summary>
        public double PMMWorkingRatioLateralTorsionalBuckling => _pMMWorkingRatioLateralTorsionalBuckling;

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

        public double LenghtAxialBuckling1 => _lenghtAxialBuckling1;

        public double LenghtAxialBuckling2 => _lenghtAxialBuckling2;

        public double LenghtLaterlaTorsionalBuckling => _lenghtLateralTorsionalBuckling;

        public Cop2011Checker.SectionClass AxialCompressionClass => _axialCompressionClass;

        public Cop2011Checker.SectionClass BendingCompressionClass => _bendingCompressionClass;

        public double Py => _py;

        public double Epsilon => _epsilon;

        public Cop2011Checker.Cop2011Options.BuckingCurves BuckingCurve1 => _bucklingCurve1;

        public Cop2011Checker.Cop2011Options.BuckingCurves BuckingCurve2 => _bucklingCurve2;

        public double LambdaLateralTorsionalBuckling1 => _lambdaAxialBuckling1;

        public double LambdaLateralTorsionalBuckling2 => _lambdaAxialBuckling2;

        public double PEAxialBuckling1 => _pEAxialBuckling1;

        public double PEAxialBuckling2 => _pEAxialBuckling2;

        public double PhiCAxialBuckling1 => _phiCAxialBuckling1;

        public double PhiCAxialBuckling2 => _phiCAxialBuckling2;

        public double PCAxialBuckling1 => _pCAxialBuckling1;

        public double PCAxialBuckling2 => _pCAxialBuckling2;

        public double LambdaLateralTorsionalBuckling => _lambdaLTBuckling;

        public double Lambda0LateralTorsionalBuckling => _lambda0LTBuckling;

        public double PELateralTorsionalBuckling => _pELTBuckling;

        public double PhiLateralTorsionalBuckling => _phiLTBuckling;

        public double PBLateralTorsionalBuckling => _pBLTBuckling;

        #endregion


        #region Constructor

        protected Cop2011BeamStationResults(ISteelSection section, ResultBeamForces forces, ResultStation station, ILoadCase Case,
                                    Cop2011Checker.Cop2011Options checkerOptions, StandardCopSuos2011 standard, string name = "") 
                                : this(section, forces, station, Case, standard, checkerOptions, name)
        {

        }


        internal Cop2011BeamStationResults(ISteelSection section, ResultBeamForces forces, ResultStation station, ILoadCase Case, 
                                            StandardCopSuos2011 standard, Cop2011Checker.Cop2011Options checkerOptions, string name = "") :
            base(section, forces, station, Case, standard, checkerOptions, name)
        {

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
            _pMMWorkingRatio = interaction878WR < 0 ? throw new ArgumentException($"InteractionWorkingRatio cannot be lower than zero") : interaction878WR;
            _pMMWorkingRatioBendingSecondOrderEffect = interaction879WR < 0 ? throw new ArgumentException($"InteractionWorkingRatio cannot be lower than zero") : interaction879WR;
            _pMMWorkingRatioAxialSecondOrderEffect = interaction880WR < 0 ? throw new ArgumentException($"InteractionWorkingRatio cannot be lower than zero") : interaction880WR;
            _pMMWorkingRatioLateralTorsionalBuckling = interaction881WR < 0 ? throw new ArgumentException($"InteractionWorkingRatio cannot be lower than zero") : interaction881WR;
        }

        internal void SetClasses(Cop2011Checker.SectionClass axialSectionClass, Cop2011Checker.SectionClass bendingSectionClass)
        {
            _axialCompressionClass = axialSectionClass;
            _bendingCompressionClass = bendingSectionClass;
        }

        internal void SetBucklingLenght(double axialBuckling1, double axialBuckling2, double lenghtLateralTorsionalBuckling)
        {
            _lenghtAxialBuckling1 = axialBuckling1; 
            _lenghtAxialBuckling2 = axialBuckling2;
            _lenghtLateralTorsionalBuckling = lenghtLateralTorsionalBuckling;

        }

        internal void SetPy(double py, double epsilon)
        {
            _py = py;
            _epsilon = epsilon;
        }

        internal void SetResultsForReportLTB(double lambdaLTBuckling, double lambda0LTBuckling, double pELTBuckling, double phiLTBuckling, double pBLTBuckling)
        {
            _lambdaLTBuckling = lambdaLTBuckling;
            _lambda0LTBuckling = lambda0LTBuckling;
            _pELTBuckling = pELTBuckling;
            _phiLTBuckling = phiLTBuckling;
            _pBLTBuckling = pBLTBuckling;
        }

        internal void SetResultsForReportAxialBuckling(Cop2011Checker.Cop2011Options.BuckingCurves buckingCurve1, Cop2011Checker.Cop2011Options.BuckingCurves buckingCurve2,
            double lambdaAxialBuckling1, double lambdaAxialBuckling2, double pEAxialBuckling1, double pEAxialBuckling2, double phiCAxialBuckling1, double phiCAxialBuckling2)
        {
            _bucklingCurve1 = buckingCurve1;
            _bucklingCurve2 = buckingCurve2;
            _pEAxialBuckling1 = pEAxialBuckling1;
            _pEAxialBuckling2 = pEAxialBuckling2;
            _phiCAxialBuckling1 = phiCAxialBuckling1;
            _phiCAxialBuckling2 = phiCAxialBuckling2;
            _lambdaAxialBuckling1 = lambdaAxialBuckling1;
            _lambdaAxialBuckling2 = lambdaAxialBuckling2;
        }

        internal override double GetMaxWorkingRatio()
        {
            List<double> workingRatioList = new List<double>() { _axialTensionWorkingRatio, _axialCompressionWorkingRatio, _axialBuckling1WorkingRatio, 
                                                                _axialBuckling2WorkingRatio, _shear1WorkingRatio, _shear2WorkingRatio, _bendingMoment1WorkingRatio, 
                                                                _bendingMoment2WorkingRatio, _torsionalMomentWorkingRatio, _lateraTorsionalBucklingWorkingRatio, 
                                                                _pMMWorkingRatio, _pMMWorkingRatioBendingSecondOrderEffect, _pMMWorkingRatioAxialSecondOrderEffect, _pMMWorkingRatioLateralTorsionalBuckling,};

            return workingRatioList.Max();
        }

        #endregion

        
    }
}
