using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Results.ResultLocations;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Results
{
    /// <summary>
    /// This class contains the result of a check performed on a beam station with a given ILoadCase
    /// </summary>

    [Serializable]
    public class AISCBeamStationResults : BeamStationResults, ISerializable
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
        protected double _beta;

        protected AISCChecker.SectionClass _axialCompressionClass;
        protected AISCChecker.SectionClass _bendingCompressionClass;

        protected AISCChecker.AISCOptions.BuckingCurves _bucklingCurve1;
        protected AISCChecker.AISCOptions.BuckingCurves _bucklingCurve2;

        protected double _lambdaAxialBuckling1;
        protected double _lambdaAxialBuckling2;
        protected double _lambda0AxialBuckling;
        protected double _pEAxialBuckling1;
        protected double _pEAxialBuckling2;
        protected double _phiCAxialBuckling1;
        protected double _phiCAxialBuckling2;
        protected double _pCAxialBuckling1;
        protected double _pCAxialBuckling2;
        protected double _nAxialBuckling1;
        protected double _nAxialBuckling2;

        protected double _lambdaLTBuckling;
        protected double _lambda0LTBuckling;
        protected double _pELTBuckling;
        protected double _phiLTBuckling;
        protected double _pBLTBuckling;

        protected double _shearArea1;
        protected double _shearArea2;

        #endregion

        #region Properties

        /// <summary>
        /// Axial tension working ratio. Return 0.001 if the section is compressed
        /// </summary>
        public double AxialTensionWorkingRatio { get => _axialTensionWorkingRatio; set => _axialTensionWorkingRatio = value; }

        /// <summary>
        /// Axial pure compression (with no buckling) working ratio. Return 0.001 if the section is tensed
        /// </summary>
        public double AxialCompressionWorkingRatio { get => _axialCompressionWorkingRatio; set => _axialCompressionWorkingRatio = value; }

        /// <summary>
        /// Axial buckling working ratio about 1-principal axis
        /// </summary>
        public double AxialBuckling1WorkingRatio { get => _axialBuckling1WorkingRatio; set => _axialBuckling1WorkingRatio = value; }

        /// <summary>
        /// Axial buckling working ratio about 2-principal axis
        /// </summary>
        public double AxialBuckling2WorkingRatio { get => _axialBuckling2WorkingRatio; set => _axialBuckling2WorkingRatio = value; }

        /// <summary>
        /// Shear working ratio about 1-principal axis
        /// </summary>
        public double Shear1WorkingRatio { get => _shear1WorkingRatio; set => _shear1WorkingRatio = value; }

        /// <summary>
        /// Shear working ratio about 2-principal axis
        /// </summary>
        public double Shear2WorkingRatio { get => _shear2WorkingRatio; set => _shear2WorkingRatio = value; }

        /// <summary>
        /// Bending moment working ratio about 1-principal axis
        /// </summary>
        public double BendingMoment1WorkingRatio { get => _bendingMoment1WorkingRatio; set => _bendingMoment1WorkingRatio = value; }

        /// <summary>
        /// Bending moment working ratio about 2-principal axis
        /// </summary>
        public double BendingMoment2WorkingRatio { get => _bendingMoment2WorkingRatio; set => _bendingMoment2WorkingRatio = value; }

        /// <summary>
        /// Torque moment working ratio
        /// </summary>
        public double TorsionalMomentWorkingRatio { get => _torsionalMomentWorkingRatio; set => _torsionalMomentWorkingRatio = value; }

        /// <summary>
        /// Lateral torsional working ratio
        /// </summary>
        public double LateralTorsionalBucklingWorkingRatio { get => _lateraTorsionalBucklingWorkingRatio; set => _lateraTorsionalBucklingWorkingRatio = value; }

        /// <summary>
        /// Axial tension capacity
        /// </summary>
        public double AxialTensionCapacity { get => _axialTensionRd; set => _axialTensionRd = value; }

        /// <summary>
        /// Axial compression capacity
        /// </summary>
        public double AxialCompressionCapacity { get => _axialCompressionRd; set => _axialCompressionRd = value; }

        /// <summary>
        /// Axial buckling about 1-principal axis capacity
        /// </summary>
        public double AxialBuckling1Capacity { get => _axialBuckling1Rd; set => _axialBuckling1Rd = value; }

        /// <summary>
        /// Axial buckling about 2-principal axis capacity
        /// </summary>
        public double AxialBuckling2Capacity { get => _axialBuckling2Rd; set => _axialBuckling2Rd = value; }

        /// <summary>
        /// Shear about 1-principal axis capacity
        /// </summary>
        public double Shear1Capacity { get => _shear1Rd; set => _shear1Rd = value; }

        /// <summary>
        /// Shear about 2-principal axis capacity
        /// </summary>
        public double Shear2Capacity { get => _shear2Rd; set => _shear2Rd = value; }

        /// <summary>
        /// Bending moment about 1-principal axis capacity
        /// </summary>
        public double BendingMoment1Capacity { get => _bendingMoment1Rd; set => _bendingMoment1Rd = value; }

        /// <summary>
        /// Bending moment about 2-principal axis capacity
        /// </summary>
        public double BendingMoment2Capacity { get => _bendingMoment2Rd; set => _bendingMoment2Rd = value; }

        /// <summary>
        /// Torque moment capacity
        /// </summary>
        public double TorsionMomentCapacity { get => _torsionalMomentRd; set => _torsionalMomentRd = value; }

        /// <summary>
        /// Lateral torsional buckling capacity
        /// </summary>
        public double LateralTosionalBucklingCapacity { get => _lateralTorsionalMomentRd; set => _lateralTorsionalMomentRd = value; }

        /// <summary>
        /// Length for axial buckling about 1-principal axis check
        /// </summary>
        public double LengthAxialBuckling1 { get => _lenghtAxialBuckling1; set => _lenghtAxialBuckling1 = value; }

        /// <summary>
        /// Length for axial buckling about 2-principal axis check
        /// </summary>
        public double LengthAxialBuckling2 { get => _lenghtAxialBuckling2; set => _lenghtAxialBuckling2 = value; }

        /// <summary>
        /// Length for lateral torsional buckling check
        /// </summary>
        public double LengthLaterlaTorsionalBuckling { get => _lenghtLateralTorsionalBuckling; set => _lenghtLateralTorsionalBuckling = value; }

        /// <summary>
        /// Pure compression section class. <see cref="AISCChecker.SectionClass"/>
        /// </summary>
        public AISCChecker.SectionClass AxialCompressionClass { get => _axialCompressionClass; set => _axialCompressionClass = value; }

        /// <summary>
        /// Pure bending section class. <see cref="AISCChecker.SectionClass"/>
        /// </summary>
        public AISCChecker.SectionClass BendingCompressionClass { get => _bendingCompressionClass; set => _bendingCompressionClass = value; }

        /// <summary>
        /// The parameter for section classification (Chapter 7.2)
        /// </summary>
        public double Epsilon { get => _epsilon; set => _epsilon = value; }

        /// <summary>
        /// The reduced design strength coefficient for effective stress method for slender cross-sections in §7.7
        /// </summary>
        public double Beta { get => _beta; set => _beta = value; }

        /// <summary>
        /// Buckling curve about 1principal axis for axial buckling check (Chapter 8.7.6)
        /// </summary>
        public AISCChecker.AISCOptions.BuckingCurves BuckingCurve1 => _bucklingCurve1;

        /// <summary>
        /// Buckling curve about 2-principal axis for axial buckling check (Chapter 8.7.6)
        /// </summary>
        public AISCChecker.AISCOptions.BuckingCurves BuckingCurve2 => _bucklingCurve2;

        /// <summary>
        /// Lambda about 1-principal axis for axial buckling check
        /// </summary>
        public double LambdaAxialBuckling1 { get => _lambdaAxialBuckling1; set => _lambdaAxialBuckling1 = value; }

        /// <summary>
        /// Lambda about 2-principal axis for axial buckling check
        /// </summary>
        public double LambdaAxialBuckling2 { get => _lambdaAxialBuckling2; set => _lambdaAxialBuckling2 = value; }

        /// <summary>
        /// Lambda0 about 1-principal axis for axial buckling check (Appendix 8.4)
        /// </summary>
        public double Lambda0AxialBuckling { get => _lambda0AxialBuckling; set => _lambda0AxialBuckling = value; }

        /// <summary>
        /// Perry factor about 1-principal axis for axial buckling check (Appendix 8.4)
        /// </summary>
        public double NAxialBuckling1 { get => _nAxialBuckling1; set => _nAxialBuckling1 = value; }

        /// <summary>
        /// Perry factor about 2-principal axis for axial buckling check (Appendix 8.4)
        /// </summary>
        public double NAxialBuckling2 { get => _nAxialBuckling2; set => _nAxialBuckling2 = value; }

        /// <summary>
        /// PE about 1-principal axis for axial buckling check (Appendix 8.4)
        /// </summary>
        public double PEAxialBuckling1 { get => _pEAxialBuckling1; set => _pEAxialBuckling1 = value; }

        /// <summary>
        /// PE about 2-principal axis for axial buckling check (Appendix 8.4)
        /// </summary>
        public double PEAxialBuckling2 { get => _pEAxialBuckling2; set => _pEAxialBuckling2 = value; }

        /// <summary>
        /// PhiC about 1-principal axis for axial buckling check (Appendix 8.4)
        /// </summary>
        public double PhiCAxialBuckling1 { get => _phiCAxialBuckling1; set => _phiCAxialBuckling1 = value; }

        /// <summary>
        /// PhiC about 2-principal axis for axial buckling check (Appendix 8.4)
        /// </summary>
        public double PhiCAxialBuckling2 { get => _phiCAxialBuckling2; set => _phiCAxialBuckling2 = value; }

        /// <summary>
        /// Design strength about 1-principal axis for axial buckling check (Appendix 8.4)
        /// </summary>
        public double PCAxialBuckling1 { get => _pCAxialBuckling1; set => _pCAxialBuckling1 = value; }

        /// <summary>
        /// Design strength about 2-principal axis for axial buckling check (Appendix 8.4)
        /// </summary>
        public double PCAxialBuckling2 { get => _pCAxialBuckling2; set => _pCAxialBuckling2 = value; }

        /// <summary>
        /// Lambda for lateral torsiona buckling check (Appendix 8.1)
        /// </summary>
        public double LambdaLateralTorsionalBuckling { get => _lambdaLTBuckling; set => _lambdaLTBuckling = value; }

        /// <summary>
        /// Lambda0 for lateral torsiona buckling check (Appendix 8.1)
        /// </summary>
        public double Lambda0LateralTorsionalBuckling { get => _lambda0LTBuckling; set => _lambda0LTBuckling = value; }

        /// <summary>
        /// PE for lateral torsiona buckling check (Appendix 8.1)
        /// </summary>
        public double PELateralTorsionalBuckling { get => _pELTBuckling; set => _pELTBuckling = value; }

        /// <summary>
        /// Phi for lateral torsiona buckling check (Appendix 8.1)
        /// </summary>
        public double PhiLateralTorsionalBuckling { get => _phiLTBuckling; set => _phiLTBuckling = value; }

        /// <summary>
        /// Design strength for lateral torsiona buckling check (Appendix 8.1)
        /// </summary>
        public double PBLateralTorsionalBuckling { get => _pBLTBuckling; set => _pBLTBuckling = value; }

        /// <summary>
        /// Shear area about 1-principal axis (Chapter 8.2.1)
        /// </summary>
        public double ShearArea1 { get => _shearArea1; set => _shearArea1 = value; }

        /// <summary>
        /// Shear area about 2-principal axis (Chapter 8.2.1)
        /// </summary>
        public double ShearArea2 { get => _shearArea2; set => _shearArea2 = value; }

        #endregion

        #region Constructor

        protected AISCBeamStationResults(ISteelSection section, StationResultBeamForces resultLocationStation, ILoadCase Case, AISCChecker.AISCOptions checkerOptions, StandardAISC standard, string name = "")
            : this(section, resultLocationStation, Case, standard, checkerOptions, name)
        {

        }


        internal AISCBeamStationResults(ISteelSection section, StationResultBeamForces resultLocationStation, ILoadCase Case, StandardAISC standard, AISCChecker.AISCOptions checkerOptions, string name = "")
            : base(section, resultLocationStation, Case, standard, checkerOptions, name)
        {

        }

        internal override double GetMaxWorkingRatio()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
