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
        protected double _bucklingInteraction1Axis;
        protected double _bucklingInteraction2Axis;

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

        protected double _epsilon;

        protected EN1993p11Checker.EN1993p11Options.LoadConditions _loadCondition1;
        protected EN1993p11Checker.EN1993p11Options.SupportConditions _supportCondition1;
        protected EN1993p11Checker.EN1993p11Options.LoadConditions _loadCondition2;
        protected EN1993p11Checker.EN1993p11Options.SupportConditions _supportCondition2;

        protected EN1993p11Checker.SectionClass _axialCompressionClass;
        protected EN1993p11Checker.SectionClass _bendingCompressionClass;

        protected EN1993p11Checker.EN1993p11Options.AxialBuckingCurves _bucklingCurve1;
        protected EN1993p11Checker.EN1993p11Options.AxialBuckingCurves _bucklingCurve2;

        protected double _lambdaAxialBuckling1;
        protected double _lambdaAxialBuckling2;

        protected double _lambdaLTBuckling;

        protected double _shearArea1;
        protected double _shearArea2;

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
        /// Buckling interactionworking ration about 1-principal axis §6.3.3
        /// </summary>
        public double BucklingInteraction1Axis => _bucklingInteraction1Axis;

        /// <summary>
        /// Buckling interactionworking ration about 2-principal axis §6.3.3
        /// </summary>
        public double BucklingInteraction2Axis => _bucklingInteraction2Axis;
                                       
        /// <summary>
        /// The max working ratio 
        /// </summary>
        public double WorkingRatio => GetMaxWorkingRatio();

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
        public double LengthAxialBuckling1 => _lenghtAxialBuckling1;

        /// <summary>
        /// Length for axial buckling about 2-principal axis check
        /// </summary>
        public double LengthAxialBuckling2 => _lenghtAxialBuckling2;

        /// <summary>
        /// Length for lateral torsional buckling check
        /// </summary>
        public double LengthLaterlaTorsionalBuckling => _lenghtLateralTorsionalBuckling;

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

        /// <summary>
        /// Buckling curve about 1principal axis for axial buckling check (Chapter 8.7.6)
        /// </summary>
        public EN1993p11Checker.EN1993p11Options.AxialBuckingCurves BuckingCurve1 => _bucklingCurve1;

        /// <summary>
        /// Buckling curve about 2-principal axis for axial buckling check (Chapter 8.7.6)
        /// </summary>
        public EN1993p11Checker.EN1993p11Options.AxialBuckingCurves BuckingCurve2 => _bucklingCurve2;

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
        public double LambdaLateralTorsionalBuckling => _lambdaLTBuckling;
                
        /// <summary>
        /// Shear area about 1-principal axis (Chapter 8.2.1)
        /// </summary>
        public double ShearArea1 => _shearArea1;

        /// <summary>
        /// Shear area about 2-principal axis (Chapter 8.2.1)
        /// </summary>
        public double ShearArea2 => _shearArea2;

        #endregion

        #endregion


        #region Constructor

        protected EN1993p11BeamStationResult(ISteelSection section, ResultBeamForces forces, ResultStation station, ILoadCase Case,
                                    EN1993p11Checker.EN1993p11Options checkerOptions, StandardEN1993p11 standard, string name = "") 
                                : this(section, forces, station, Case, standard, checkerOptions, name)
        {

        }


        internal EN1993p11BeamStationResult(ISteelSection section, ResultBeamForces forces, ResultStation station, ILoadCase Case, 
                                            StandardEN1993p11 standard, EN1993p11Checker.EN1993p11Options checkerOptions, string name = "") :
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
                                        double bending2WR, double latTorsWR, double bucklingInteraction1Axis, double bucklingInteraction2Axis)
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
            _bucklingInteraction1Axis = bucklingInteraction1Axis;
            _bucklingInteraction2Axis = bucklingInteraction2Axis;
        }

        internal void SetClasses(EN1993p11Checker.SectionClass axialSectionClass, EN1993p11Checker.SectionClass bendingSectionClass)
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






        internal override double GetMaxWorkingRatio()
        {
            List<double> workingRatioList = new List<double>() { _axialTensionWorkingRatio, _axialCompressionWorkingRatio, _axialBuckling1WorkingRatio, 
                                                                _axialBuckling2WorkingRatio, _shear1WorkingRatio, _shear2WorkingRatio, _bendingMoment1WorkingRatio, 
                                                                _bendingMoment2WorkingRatio, _torsionalMomentWorkingRatio, _lateraTorsionalBucklingWorkingRatio};

            return workingRatioList.Max();
        }

        #endregion
                
    }
}
