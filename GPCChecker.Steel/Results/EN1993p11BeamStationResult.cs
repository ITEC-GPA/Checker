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
using GPC.Geometry;

namespace GPC.Checkers.Steel.Results
{
    /// <summary>
    /// This class contains the result of a check performed on a beam station with a given ILoadCase
    /// </summary>

    [Serializable]
    public class EN1993p11BeamStationResult : BeamStationResults, ISerializable
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
        protected double _crossSectionInteraction;
        protected double _bucklingInteraction1Axis;
        protected double _bucklingInteraction2Axis;
        protected double _flexuralTorsionalInteraction;

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

        protected double _lengthAxialBuckling1;
        protected double _lengthAxialBuckling2;
        protected double _lengthLateralTorsionalBuckling;
        protected double _lengthCriticalMoment1;
        protected double _lengthCriticalMoment2;

        protected double _epsilon;

        protected EN1993p11Checker.EN1993p11Options.LoadConditions _loadCondition1;
        protected EN1993p11Checker.EN1993p11Options.SupportConditions _supportCondition1;
        protected EN1993p11Checker.EN1993p11Options.LoadConditions _loadCondition2;
        protected EN1993p11Checker.EN1993p11Options.SupportConditions _supportCondition2;

        protected EN1993p11Checker.SectionClass _axialCompressionClass;
        protected EN1993p11Checker.SectionClass _bendingCompressionClass;
        protected EN1993p11Checker.SectionClass _sectionClass;

        protected double _lambdaAxialBuckling1;
        protected double _lambdaAxialBuckling2;

        protected double _chiAxialBuckling1;
        protected double _phiAxialBuckling1;
        protected double _lambdaSignedAxialBuckling1;
        protected double _alphaAxialBuckling1;
        protected double _nCr1;
        protected EN1993p11Checker.EN1993p11Options.AxialBuckingCurves _axialBuckingCurves1;

        protected double _chiAxialBuckling2;
        protected double _phiAxialBuckling2;
        protected double _lambdaSignedAxialBuckling2;
        protected double _alphaAxialBuckling2;
        protected double _nCr2;
        protected EN1993p11Checker.EN1993p11Options.AxialBuckingCurves _axialBuckingCurves2;

        protected double _chiLTBuckling;
        protected double _phiLTBuckling;
        protected double _lambdaSignedLTBuckling;
        protected double _lambda0LTBuckling;
        protected double _alphaLTBuckling;
        protected double _mCr;
        protected EN1993p11Checker.EN1993p11Options.LateralTorsionalBuckingCurves _lateraltorsionalBucklingCurve;

        protected double _NcrTorsional;     
        protected double _NcrFlexuralTorsional; 

        protected double _shearArea1;
        protected double _shearArea2;

        protected double _cmX0;
        protected double _cmY0;

        protected double _mux;
        protected double _muy;

        protected double _wx;
        protected double _wy;

        protected double _cmx;
        protected double _cmy;
        protected double _cmLT;

        protected double _aLT;
        protected double _bLT;
        protected double _cLT;
        protected double _dLT;
        protected double _eLT;
        protected double _cxx;
        protected double _cxy;
        protected double _cyx;
        protected double _cyy;

        protected double _epsilonx;

        protected double _kxx;
        protected double _kxy;
        protected double _kyx;
        protected double _kyy;

        protected double _kwInteraction;
        protected double _kywInteraction; 
        protected double _kAlphaInteraction; 


        #endregion


        #region Properties

        #region Working Ratio

        /// <summary>
        /// Axial tension working ratio. Return 0.001 if the section is compressed
        /// </summary>
        public double AxialTensionWorkingRatio => _axialTensionWorkingRatio;

        /// <summary>
        /// Axial pure compression (with no buckling) working ratio. Return 0.001 if the section is tensed
        /// </summary>
        public double AxialCompressionWorkingRatio => _axialCompressionWorkingRatio;

        /// <summary>
        /// Axial buckling working ratio about 1-principal axis
        /// </summary>
        public double AxialBuckling1WorkingRatio => _axialBuckling1WorkingRatio;

        /// <summary>
        /// Axial buckling working ratio about 2-principal axis
        /// </summary>
        public double AxialBuckling2WorkingRatio => _axialBuckling2WorkingRatio;

        /// <summary>
        /// Shear working ratio about 1-principal axis
        /// </summary>
        public double Shear1WorkingRatio => _shear1WorkingRatio;

        /// <summary>
        /// Shear working ratio about 2-principal axis
        /// </summary>
        public double Shear2WorkingRatio => _shear2WorkingRatio;

        /// <summary>
        /// Bending moment working ratio about 1-principal axis
        /// </summary>
        public double BendingMoment1WorkingRatio => _bendingMoment1WorkingRatio;

        /// <summary>
        /// Bending moment working ratio about 2-principal axis
        /// </summary>
        public double BendingMoment2WorkingRatio => _bendingMoment2WorkingRatio;

        /// <summary>
        /// Torque moment working ratio
        /// </summary>
        public double TorsionalMomentWorkingRatio => _torsionalMomentWorkingRatio;

        /// <summary>
        /// Lateral torsional working ratio
        /// </summary>
        public double LateralTorsionalBucklingWorkingRatio => _lateraTorsionalBucklingWorkingRatio;

        /// <summary>
        /// Cross section interaction working ration §6.2.9.1
        /// </summary>
        public double CrossSectionInteraction => _crossSectionInteraction;

        /// <summary>
        /// Buckling interaction working ration about 1-principal axis §6.3.3
        /// </summary>
        public double BucklingInteraction1Axis => _bucklingInteraction1Axis;

        /// <summary>
        /// Buckling interaction working ration about 2-principal axis §6.3.3
        /// </summary>
        public double BucklingInteraction2Axis => _bucklingInteraction2Axis;

        /// <summary>
        /// Flexural-Torsional Interaction working ratio §Annex A
        /// </summary>
        public double FlexuralTorsionalInteraction => _flexuralTorsionalInteraction;

        #endregion

        #region Capacity

        /// <summary>
        /// Axial tension capacity
        /// </summary>
        public double AxialTensionCapacity => _axialTensionRd;

        /// <summary>
        /// Axial compression capacity
        /// </summary>
        public double AxialCompressionCapacity => _axialCompressionRd;

        /// <summary>
        /// Axial buckling about 1-principal axis capacity
        /// </summary>
        public double AxialBuckling1Capacity => _axialBuckling1Rd;

        /// <summary>
        /// Axial buckling about 2-principal axis capacity
        /// </summary>
        public double AxialBuckling2Capacity => _axialBuckling2Rd;

        /// <summary>
        /// Shear about 1-principal axis capacity
        /// </summary>
        public double Shear1Capacity => _shear1Rd;

        /// <summary>
        /// Shear about 2-principal axis capacity
        /// </summary>
        public double Shear2Capacity => _shear2Rd;

        /// <summary>
        /// Bending moment about 1-principal axis capacity
        /// </summary>
        public double BendingMoment1Capacity => _bendingMoment1Rd;

        /// <summary>
        /// Bending moment about 2-principal axis capacity
        /// </summary>
        public double BendingMoment2Capacity => _bendingMoment2Rd;

        /// <summary>
        /// Torque moment capacity
        /// </summary>
        public double TorsionMomentCapacity => _torsionalMomentRd;

        /// <summary>
        /// Lateral torsional buckling capacity
        /// </summary>
        public double LateralTosionalBucklingCapacity => _lateralTorsionalMomentRd;

        #endregion

        #region Report

        /// <summary>
        /// Length for axial buckling about 1-principal axis check
        /// </summary>
        public double LengthAxialBuckling1 => _lengthAxialBuckling1;

        /// <summary>
        /// Length for axial buckling about 2-principal axis check
        /// </summary>
        public double LengthAxialBuckling2 => _lengthAxialBuckling2;

        /// <summary>
        /// Length for lateral torsional buckling check
        /// </summary>
        public double LengthLaterlaTorsionalBuckling => _lengthLateralTorsionalBuckling;

        /// <summary>
        /// Pure compression section class. <see cref="EN1993p11Checker.SectionClass"/>
        /// </summary>
        public EN1993p11Checker.SectionClass AxialCompressionClass => _axialCompressionClass;

        /// <summary>
        /// Pure bending section class. <see cref="EN1993p11Checker.SectionClass"/>
        /// </summary>
        public EN1993p11Checker.SectionClass BendingCompressionClass => _bendingCompressionClass;

        /// <summary>
        /// The parameter for section classification 
        /// </summary>
        public double Epsilon => _epsilon;

        public double ChiAxialBuckling1 => _chiAxialBuckling1;
        public double PhiAxialBuckling1 => _phiAxialBuckling1;
        public double LambdaSignedAxialBuckling1 => _lambdaSignedAxialBuckling1;
        public double AlphaAxialBuckling1 => _alphaAxialBuckling1;
        public double NCr1 => _nCr1;

        /// <summary>
        /// Buckling curve about 1principal axis for axial buckling check (Chapter 8.7.6)
        /// </summary>
        public EN1993p11Checker.EN1993p11Options.AxialBuckingCurves BuckingCurve1 => _axialBuckingCurves1;

        public double ChiAxialBuckling2 => _chiAxialBuckling2;
        public double PhiAxialBuckling2 => _phiAxialBuckling2;
        public double LambdaSignedAxialBuckling2 => _lambdaSignedAxialBuckling2;
        public double AlphaAxialBuckling2 => _alphaAxialBuckling2;
        public double NCr2 => _nCr2;

        /// <summary>
        /// Buckling curve about 2-principal axis for axial buckling check (Chapter 8.7.6)
        /// </summary>
        public EN1993p11Checker.EN1993p11Options.AxialBuckingCurves BuckingCurve2 => _axialBuckingCurves2;

        public double ChiLTBuckling =>_chiLTBuckling;
        public double PhiLTBuckling => _phiLTBuckling;
        public double LambdaSignedLTBuckling => _lambdaSignedLTBuckling;
        public double Lambda0LTBuckling => _lambda0LTBuckling;
        public double AlphaLTBuckling => _alphaLTBuckling;
        public double MCr => _mCr;
        public EN1993p11Checker.EN1993p11Options.LateralTorsionalBuckingCurves LateraltorsionalBucklingCurve => _lateraltorsionalBucklingCurve;

        /// <summary>
        /// Lambda about 1-principal axis for axial buckling check
        /// </summary>
        public double LambdaAxialBuckling1 => _lambdaAxialBuckling1;

        /// <summary>
        /// Lambda about 2-principal axis for axial buckling check
        /// </summary>
        public double LambdaAxialBuckling2 => _lambdaAxialBuckling2;
               
        /// <summary>
        /// Lambda for lateral torsiona buckling check (Appendix 8.1)
        /// </summary>
        public double LambdaLateralTorsionalBuckling => _lambda0LTBuckling;
                
        /// <summary>
        /// Shear area about 1-principal axis (Chapter 8.2.1)
        /// </summary>
        public double ShearArea1 => _shearArea1;

        /// <summary>
        /// Shear area about 2-principal axis (Chapter 8.2.1)
        /// </summary>
        public double ShearArea2 => _shearArea2;

        public double CmX0 => _cmX0;
        public double CmY0 => _cmY0;
                      
        public double Mux => _mux;
        public double Muy => _muy;
                       
        public double Wx => _wx;
        public double Wy => _wy;
                      
        public double Cmx => _cmx;
        public double Cmy => _cmy;
        public double CmLT => _cmLT;
                       
        public double ALT => _aLT;
        public double BLT => _bLT;
        public double CLT => _cLT;
        public double DLT => _dLT;
        public double ELT => _eLT;
        public double Cxx => _cxx;
        public double Cxy => _cxy;
        public double Cyx => _cyx;
        public double Cyy => _cyy;
                      
        public double EpsilonX => _epsilonx;
        
        public double Kxx => _kxx;
        public double Kxy => _kxy;
        public double Kyx => _kyx;
        public double Kyy => _kyy;
                      
        public double KwInteraction => _kwInteraction;
        public double KywInteraction => _kywInteraction;
        public double KAlphaInteraction  => _kAlphaInteraction;

        public double NcrTorsional => _NcrTorsional;
        public double NcrFlexuralTorsional => _NcrFlexuralTorsional;

        #endregion

        #endregion


        #region Constructor

        protected EN1993p11BeamStationResult(ISteelSection section, ResultLocationStation resultLocationStation, ILoadCase Case,
            EN1993p11Checker.EN1993p11Options checkerOptions, StandardEN1993p11 standard, string name = "") 
            : this(section, resultLocationStation, Case, standard, checkerOptions, name)
        {

        }

        internal EN1993p11BeamStationResult(ISteelSection section, ResultLocationStation resultLocationStation, ILoadCase Case, 
            StandardEN1993p11 standard, EN1993p11Checker.EN1993p11Options checkerOptions, string name = "") 
            : base(section, resultLocationStation, Case, standard, checkerOptions, name)
        {

        }

        #endregion


        #region Public Method

        internal void SetCapacity(double axialTension, double axialCompression, double axialBuck1, double axialBuck2, 
            double shear1, double shear2, double bending1, double bending2, double latTors)
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

        internal void SetWorkingRatio(double axialTensionWR, double axialCompressionWR, double axialBuck1WR, double axialBuck2WR, 
            double shear1WR, double shear2WR, double bending1WR, double bending2WR, double latTorsWR, double crossSectionInteraction, 
            double bucklingInteraction1Axis, double bucklingInteraction2Axis, double flextureTorsionInteraction)
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
            _crossSectionInteraction = crossSectionInteraction < 0 ? throw new ArgumentException($"CrossSectionInteraction cannot be lower than zero") : crossSectionInteraction;
            _bucklingInteraction1Axis = bucklingInteraction1Axis < 0 ? throw new ArgumentException($"BucklingInteraction1Axis cannot be lower than zero") : bucklingInteraction1Axis;
            _bucklingInteraction2Axis = bucklingInteraction2Axis < 0 ? throw new ArgumentException($"BucklingInteraction2Axis cannot be lower than zero") : bucklingInteraction2Axis;
            _flexuralTorsionalInteraction = flextureTorsionInteraction;
        }

        internal void SetClasses(EN1993p11Checker.SectionClass sectionClass, EN1993p11Checker.SectionClass axialSectionClass, EN1993p11Checker.SectionClass bendingSectionClass)
        {
            _axialCompressionClass = axialSectionClass;
            _bendingCompressionClass = bendingSectionClass;
            _sectionClass = sectionClass;
        }

        internal void SetBucklingLenght(double lengthAxialBuckling1, double lengthAxialBuckling2, double lengthLateralTorsionalBuckling, 
            double lengthCriticalMoment1, double lengthCriticalMoment2)
        {
            _lengthAxialBuckling1 = lengthAxialBuckling1; 
            _lengthAxialBuckling2 = lengthAxialBuckling2;
            _lengthLateralTorsionalBuckling = lengthLateralTorsionalBuckling;
            _lengthCriticalMoment1 = lengthCriticalMoment1;
            _lengthCriticalMoment2 = lengthCriticalMoment2;
        }

        internal void SetResultsForReportAxialBuckling1Axis(double chi1, double phi1, double lambdaSigned1, double alpha1, double nCr1, 
            EN1993p11Checker.EN1993p11Options.AxialBuckingCurves axialBuckingCurves1)
        {
            _chiAxialBuckling1 = chi1;
            _phiAxialBuckling1 = phi1;
            _lambdaSignedAxialBuckling1 = lambdaSigned1;
            _alphaAxialBuckling1 = alpha1;
            _nCr1 = nCr1;
            _axialBuckingCurves1 = axialBuckingCurves1;
        }

        internal void SetResultsForReportAxialBuckling2Axis(double chi2, double phi2, double lambdaSigned2, double alpha2, double nCr2, 
            EN1993p11Checker.EN1993p11Options.AxialBuckingCurves axialBuckingCurves2)
        {
            _chiAxialBuckling2 = chi2;
            _phiAxialBuckling2 = phi2;
            _lambdaSignedAxialBuckling2 = lambdaSigned2;
            _alphaAxialBuckling2 = alpha2;
            _nCr2 = nCr2;
            _axialBuckingCurves2 = axialBuckingCurves2;
        }

        internal void SetResultsForReportLateralTorsionalBuckling(double chi, double phi, double lambdaSigned, double lambda0, double alpha, double mCr, 
            EN1993p11Checker.EN1993p11Options.LateralTorsionalBuckingCurves LTBuckingCurves, double NcrT, double NcrFT)
        {
            _chiLTBuckling = chi;
            _phiLTBuckling = phi;
            _lambdaSignedLTBuckling = lambdaSigned;
            _lambda0LTBuckling = lambda0;
            _alphaLTBuckling = alpha;
            _mCr = mCr;
            _lateraltorsionalBucklingCurve = LTBuckingCurves;
            _NcrTorsional = NcrT;
            _NcrFlexuralTorsional = NcrFT;
        }

        internal void SetResultForReportInteractionCoefficient(double cmX0, double cmy0, double mux, double muy, double wx, double wy, double cmx, double cmy, double cmLT,
            double bLT, double cLT, double dLT, double eLT, double cxx, double cxy, double cyx, double cyy, double epsilonx,
            double kxx, double kxy, double kyx,double kyy, double kwInteraction, double kywInteraction, double kAlphaInteraction)
        {                                                   
            _cmX0 = cmX0;                                   
            _cmY0 = cmy0;

            _mux = mux;
            _muy = muy;

            _wx = wx;
            _wy = wy;

            _cmx = cmx;
            _cmy = cmy;
            _cmLT = cmLT;

            _bLT = bLT;
            _cLT = cLT;
            _dLT = dLT;
            _eLT = eLT;
            _cxx = cxx;
            _cxy = cxy;
            _cyx = cyx;
            _cyy = cyy;

            _epsilonx = epsilonx;

            _kxx = kxx;
            _kxy = kxy;
            _kyx = kyx;
            _kyy = kyy;

            _kwInteraction = kwInteraction;
            _kywInteraction = kywInteraction;
            _kAlphaInteraction = kAlphaInteraction;
        }

        internal override double GetMaxWorkingRatio()
        {
            List<double> workingRatioList = new List<double>() { _axialTensionWorkingRatio, _axialCompressionWorkingRatio, _axialBuckling1WorkingRatio, 
                                                                _axialBuckling2WorkingRatio, _shear1WorkingRatio, _shear2WorkingRatio, _bendingMoment1WorkingRatio, 
                                                                _bendingMoment2WorkingRatio, _torsionalMomentWorkingRatio, _lateraTorsionalBucklingWorkingRatio,
                                                                _bucklingInteraction1Axis, _bucklingInteraction2Axis, _flexuralTorsionalInteraction, _crossSectionInteraction};

            return workingRatioList.Max();
        }

        #endregion
                
    }
}
