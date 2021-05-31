using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.LoadCases;
using GPC.Model.Standards;
using GPC.Model.Sections.Steel;
using System.ComponentModel;
using GPC.Model.Sections;
using GPC.Checkers.Steel.Results;
using GPC.Model.Materials;

namespace GPC.Checkers.Steel.CopSuos2011
{
    public class CopSuos2011BeamCheckerResults : BeamCheckerResults
    {

        #region Public enum

        public enum SectionClass
        {
            [Description("Plastic")] Class1 = 1,
            [Description("Compact")] Class2 = 2,
            [Description("Semi-Compact")] Class3 = 3,
            [Description("Slender")] Class4 = 4,
        }


        #endregion


        #region Variables

        // VARIABILI EREDITATE
        // Combination 
        // BeamResult 
        // WorkingRatio 
        // Section
        // BeamStationCheckerResults
        // Length 
        // Options 

        private readonly double _py;
        private double _nTenrd;
        private double _nComrd;
        private double _txrd;
        private double _tyrd;
        private double _mxrd;
        private double _myrd;
        private double _trd;
        private SectionClass _class;
        private readonly CopSuos2011Checker.CopSuos2011Options.SteelClasses _steelClass;
         
        #endregion


        #region Properties

        public double AxialTensionCapacity
        {
            get => _nTenrd;
            set => _nTenrd = value;
        }

        public double AxialCompressionCapacity
        {
            get => _nComrd;
            set => _nComrd = value;
        }

        public double ShearXCapacity
        {
            get => _txrd;
            set => _txrd = value;
        }

        public double ShearYCapacity
        {
            get => _tyrd;
            set => _tyrd = value;
        }

        public double BendingMomentXCapacity
        {
            get => _mxrd;
            set => _mxrd = value;
        }

        public double BendingMomentYCapacity
        {
            get => _myrd;
            set => _myrd = value;
        }

        public double TorsionMomentCapacity
        {
            get => _trd;
            set => _trd = value;
        }

        public SectionClass Class
        {
            get => _class;
            set => _class = value;
        }

        public SteelMaterial Material => (SteelMaterial)Section.Material();

        public CopSuos2011Checker.CopSuos2011Options.SteelClasses SteelClass => _steelClass;

        public double Py { get => _py; set => Py = value; }

        #endregion


        #region Constructor

        public CopSuos2011BeamCheckerResults(ILoadCase loadCase, ResultBeamForces[] forces, ResultStation[] stations, ISteelSection section, CopSuos2011Checker.CopSuos2011Options options, StandardCopSuos2011 standard)
            : base(loadCase, forces, stations, section, options, standard)
        {
        }

        #endregion


        #region Check

        internal override void PerformCheck()
        {

        }


        private double CalculateAxialWR(ResultBeamForces resultBeamForces, ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            if (MinSigma(section, resultBeamForces.N, resultBeamForces.M2, resultBeamForces.M1) > 0)
                return resultBeamForces.N / CalculateAxialTensionCapacity(section, material, steelClass);
            else
                return Math.Max(resultBeamForces.N / CalculateAxialCompressionCapacityXAxis(section, resultBeamForces,  material, steelClass, lenght),
                                resultBeamForces.N / CalculateAxialCompressionCapacityYAxis(section, resultBeamForces,  material, steelClass, lenght));
        }

        private double CalculateShear2WR(ResultBeamForces resultBeamForces, ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            return resultBeamForces.V2 / CalculateShearYCapacity(section, material, steelClass);
        }

        private double CalculateBendingMoment1WR(ResultBeamForces resultBeamForces, ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            return resultBeamForces.M1 / CalculateBendingMoment1Capacity(resultBeamForces, section, material, steelClass);
        }

        private double CalculateLateralTorsionalBucklingWR(ResultBeamForces resultBeamForces, ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return Math.Max(CalculateMLTForLatTorsBuckling() * resultBeamForces.M1 / CalculateLateralTorsionalBucklingMomentCapacity(resultBeamForces, section, material, steelClass, lenght), );
        }

        #endregion



        #region Section Private Method

        #region Axial Tension

        private double CalculateAxialTensionCapacity(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            return GetPy(material, steelClass) * GetEffettiveArea(section);
        }

        #endregion


        #region Axial Compression

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        /// <returns></returns>
        private double CalculateAxialCompressionCapacityXAxis(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            SectionClass sectionClass = CalculateSectionClass(section, resultBeamForces, material, steelClass);
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2 || sectionClass == SectionClass.Class3)
                return GetCrossSectionalArea(section) * CalculatePCompressionXAxis(section, material, steelClass, lenght);
            else
                return GetEffettiveArea(section) * CalculatePCompressionReducesXAxis(section, material, steelClass, lenght);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        /// <returns></returns>
        private double CalculateAxialCompressionCapacityYAxis(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            SectionClass sectionClass = CalculateSectionClass(section, resultBeamForces, material, steelClass);
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2 || sectionClass == SectionClass.Class3)
                return GetCrossSectionalArea(section) * CalculatePCompressionYAxis(section, material, steelClass, lenght);
            else
                return GetEffettiveArea(section) * CalculatePCompressionReducesYAxis(section, material, steelClass, lenght);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        private double CalculatePCompressionReducesXAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return CalculatePCompressionXAxis(section, material, steelClass, lenght) * CalculateLambdaAxesForAxialBucklingXAxis(section, lenght) *
                Math.Sqrt(GetEffettiveArea(section) / GetCrossSectionalArea(section));
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        private double CalculatePCompressionReducesYAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return CalculatePCompressionYAxis(section, material, steelClass, lenght) * CalculateLambdaAxesForAxialBucklingYAxis(section, lenght) *
                Math.Sqrt(GetEffettiveArea(section) / GetCrossSectionalArea(section));
        }

        private double GetLeAxesForAxialBucklingXAxis(double length)
        {
            return length;
        }

        private double GetLeAxesForAxialBucklingYAxis(double length)
        {
            return length;
        }

        private double CalculateLambdaAxesForAxialBucklingYAxis(ISteelSection section, double lenght)
        {
            return GetLeAxesForAxialBucklingXAxis(lenght) / ((Section)section).InertiaRadiusY;
        }

        private double CalculateLambdaAxesForAxialBucklingXAxis(ISteelSection section, double lenght)
        {
            return GetLeAxesForAxialBucklingXAxis(lenght) / ((Section)section).InertiaRadiusX;
        }

        private double CalculatePCompressionXAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return (CalculatePeforAxialBucklingXAxis(section, material, lenght) * GetPy(material, steelClass)) /
                (CalculatePhiforAxialBucklingXAxis(section, material, steelClass, lenght) + 
                Math.Pow(Math.Pow(CalculatePhiforAxialBucklingXAxis(section, material, steelClass, lenght), 2) + 
                CalculatePeforAxialBucklingXAxis(section, material, lenght) * GetPy(material, steelClass) , 0.5));
        }

        private double CalculatePCompressionYAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return (CalculatePeforAxialBucklingYAxis(section, material, lenght) * GetPy(material, steelClass)) /
                (CalculatePhiforAxialBucklingYAxis(section, material, steelClass, lenght) +
                Math.Pow(Math.Pow(CalculatePhiforAxialBucklingYAxis(section, material, steelClass, lenght), 2) +
                CalculatePeforAxialBucklingYAxis(section, material, lenght) * GetPy(material, steelClass), 0.5));
        }

        private double CalculatePhiforAxialBucklingXAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return (GetPy(material, steelClass) * (CalculateNforAxialBucklingXAxis(section, material, steelClass, lenght) + 1) * CalculatePeforAxialBucklingXAxis(section, material, lenght)) / 2;
        }

        private double CalculatePhiforAxialBucklingYAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return (GetPy(material, steelClass) * (CalculateNforAxialBucklingYAxis(section, material, steelClass, lenght) + 1) * CalculatePeforAxialBucklingYAxis(section, material, lenght)) / 2;
        }

        private double CalculatePeforAxialBucklingXAxis(ISteelSection section, SteelMaterial material, double lenght)
        {
            return (Math.Pow(Math.PI, 2) * material.E) / (Math.Pow(CalculateLambdaforAxialBucklingXAxis(section, lenght), 2));
        }

        private double CalculatePeforAxialBucklingYAxis(ISteelSection section, SteelMaterial material, double lenght)
        {
            return (Math.Pow(Math.PI, 2) * material.E) / (Math.Pow(CalculateLambdaforAxialBucklingYAxis(section, lenght), 2));
        }

        /// <summary>
        /// BS 5950 C1
        /// </summary>
        private double CalculateNforAxialBucklingXAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            double alpha = GetAlphaBucklingCurveXXAxis(section, material, steelClass);
            return alpha * (CalculateLambdaforAxialBucklingXAxis(section, lenght) - CalculateLambda0forAxialBuckling(section, material, steelClass)) / 1000;
        }

        /// <summary>
        /// BS 5950 C1
        /// </summary>
        private double CalculateNforAxialBucklingYAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            double alpha = GetAlphaBucklingCurveYYAxis(section, material, steelClass);
            return Math.Max(alpha * (CalculateLambdaforAxialBucklingYAxis(section, lenght) - CalculateLambda0forAxialBuckling(section, material, steelClass)) / 1000, 0);
        }

        /// <summary>
        /// BS 5950 C1
        /// </summary>
        private double GetAlphaBucklingCurveXXAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            CopSuos2011Checker.CopSuos2011Options.BuckingCurves buckingCurve = GetBucklingCurveXXAxis(section, material, steelClass);

            if (buckingCurve == CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a0)
                return 2.0;     // TODO: check. questo valore non esiste nella norma!
            else if (buckingCurve == CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a)
                return 2.0;
            else if (buckingCurve == CopSuos2011Checker.CopSuos2011Options.BuckingCurves.b)
                return 3.5;
            else if (buckingCurve == CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c)
                return 5.5;
            else if (buckingCurve == CopSuos2011Checker.CopSuos2011Options.BuckingCurves.d)
                return 8.0;
            else
                throw new NotImplementedException("GetAlphaBucklingCurve: not implemented BuckingCurve");
        }

        /// <summary>
        /// BS 5950 C1
        /// </summary>
        private double GetAlphaBucklingCurveYYAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            CopSuos2011Checker.CopSuos2011Options.BuckingCurves buckingCurve = GetBucklingCurveYYAxis(section, material, steelClass);

            if (buckingCurve == CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a0)
                return 2.0;     // TODO: check. questo valore non esiste nella norma!
            else if (buckingCurve == CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a)
                return 2.0;
            else if (buckingCurve == CopSuos2011Checker.CopSuos2011Options.BuckingCurves.b)
                return 3.5;
            else if (buckingCurve == CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c)
                return 5.5;
            else if (buckingCurve == CopSuos2011Checker.CopSuos2011Options.BuckingCurves.d)
                return 8.0;
            else
                throw new NotImplementedException("GetAlphaBucklingCurve: not implemented BuckingCurve");
        }

        /// <summary>
        /// CopSuos2011 Table 8.7 - Buckling curve
        /// </summary>
        private CopSuos2011Checker.CopSuos2011Options.BuckingCurves GetBucklingCurveXXAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (section is SectionCHS sectionCHS)
            {
                if (sectionCHS.FormedType == Model.Sections.Section.FormedTypes.HotFinished)
                {
                    if (material.Fyk >= 460)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                {
                    if (sectionCHS.IsDoubleSymmetric)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a0;
                    else
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
                }
            }
            else if (section is SectionRHS sectionRHS)
            {
                if (sectionRHS.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionRHS.TBottom < 40 && sectionRHS.TTop < 40 && sectionRHS.TWebLeft < 40 && sectionRHS.TWebRight < 40)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.b;
                    else
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
                }
                else if (sectionRHS.FormedType == Model.Sections.Section.FormedTypes.HotFinished)
                {
                    if (material.Fyk >= 460)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                    return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
            }
            else if (section is SectionH sectionH)
            {
                if (sectionH.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if (sectionH.IsISection)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a;
                        else
                            return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.b;
                    }
                    else // (sectionH.IsHSection)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.b;
                        else
                            return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
                    }
                }
                else //(sectionH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.b;
                    else
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.b;
                }
            }
            else if (section is SectionC sectionC || section is SectionL sectionL || section is SectionT sectionT)
                return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
            else
                throw new NotImplementedException("GetBucklingCurve: not implemented section");
        }

        /// <summary>
        /// CopSuos2011 Table 8.7 - Buckling curve
        /// </summary>
        private CopSuos2011Checker.CopSuos2011Options.BuckingCurves GetBucklingCurveYYAxis(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (section is SectionCHS sectionCHS)
            {
                if (sectionCHS.FormedType == Model.Sections.Section.FormedTypes.HotFinished)
                {
                    if (material.Fyk >= 460)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                {
                    if (sectionCHS.IsDoubleSymmetric)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a0;
                    else
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
                }
            }
            else if (section is SectionRHS sectionRHS)
            {
                if (sectionRHS.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionRHS.TBottom < 40 && sectionRHS.TTop < 40 && sectionRHS.TWebLeft < 40 && sectionRHS.TWebRight < 40)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.b;
                    else
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
                }
                else if (sectionRHS.FormedType == Model.Sections.Section.FormedTypes.HotFinished)
                {
                    if (material.Fyk >= 460)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                    return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
            }
            else if (section is SectionH sectionH)
            {
                if (sectionH.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if (sectionH.IsISection)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.b;
                        else
                            return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
                    }
                    else // (sectionH.IsHSection)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
                        else
                            return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.d;
                    }
                }
                else //(sectionH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
                    else
                        return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.d;
                }
            }
            else if (section is SectionC sectionC || section is SectionL sectionL || section is SectionT sectionT)
                return CopSuos2011Checker.CopSuos2011Options.BuckingCurves.c;
            else
                throw new NotImplementedException("GetBucklingCurve: not implemented section");
        }

        /// <summary>
        /// BS 5950 C1
        /// </summary>
        private double CalculateLambdaforAxialBucklingXAxis(ISteelSection section, double length)
        {
            return GetLeAxesForAxialBucklingXAxis(length) / ((Section)section).InertiaRadiusX;
        }

        /// <summary>
        /// BS 5950 C1
        /// </summary>
        private double CalculateLambdaforAxialBucklingYAxis(ISteelSection section, double length)
        {
            return GetLeAxesForAxialBucklingYAxis(length) / ((Section)section).InertiaRadiusY;
        }

        /// <summary>
        /// BS 5950 C1
        /// </summary>
        private double CalculateLambda0forAxialBuckling(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            return 0.2 * Math.Pow(Math.Pow(Math.PI, 2) * ((Section)section).GetE() / GetPy(material, steelClass), 0.5);
        }

        #endregion


        #region Shear Capacity

        /// <summary>
        /// CopSuos 2011 chapter 8.2.1
        /// </summary>
        /// <returns></returns>
        private double CalculateShearYCapacity(ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (section is SectionH sectionH)
                if (sectionH.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if (sectionH.HeightWeb / sectionH.ThicknessWeb > 70.0 * CalculateEpsilonForClassification(material, steelClass))
                        throw new NotImplementedException("Warning: shear buckling resistance must be checked");
                }
                else
                {
                    if (sectionH.HeightWeb / sectionH.ThicknessWeb > 62.0 * CalculateEpsilonForClassification(material, steelClass))
                        throw new NotImplementedException("Warning: shear buckling resistance must be checked");
                }
            if (section is SectionRHS sectionRHS)
                if (sectionRHS.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if (sectionRHS.TWebLeft / sectionRHS.Hinternal > 70.0 * CalculateEpsilonForClassification(material, steelClass) ||
                        sectionRHS.TWebRight / sectionRHS.Hinternal > 70.0 * CalculateEpsilonForClassification(material, steelClass))
                        throw new NotImplementedException("Warning: shear buckling resistance must be checked");
                }
                else
                {
                    if (sectionRHS.TWebLeft / sectionRHS.Hinternal > 62.0 * CalculateEpsilonForClassification(material, steelClass) ||
                        sectionRHS.TWebRight / sectionRHS.Hinternal > 62.0 * CalculateEpsilonForClassification(material, steelClass))
                        throw new NotImplementedException("Warning: shear buckling resistance must be checked");
                }
            if (section is SectionC sectionC)
                if (sectionC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if (sectionC.Hw / sectionC.Tw > 70.0 * CalculateEpsilonForClassification(material, steelClass))
                        throw new NotImplementedException("Warning: shear buckling resistance must be checked");
                }
                else
                {
                    if (sectionC.Hw / sectionC.Tw > 62.0 * CalculateEpsilonForClassification(material, steelClass))
                        throw new NotImplementedException("Warning: shear buckling resistance must be checked");
                }
            return GetPy(material, steelClass) * GetShearArea(section) / Math.Sqrt(3);
        }

        /// <summary>
        /// CopSuos 2011 chapter 8.2.1 Calculate Av parameter
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        private double GetShearArea(ISteelSection section)
        {
            if (section is SectionH sech && sech.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return sech.ThicknessWeb * sech.H;
            if (section is SectionH secH && secH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                return secH.ThicknessWeb * secH.HeightWeb;
            if (section is SectionC secC && secC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return secC.Tw * secC.H;
            if (section is SectionRHS sectionRHS)
                return 2 * ((sectionRHS.TWebLeft + sectionRHS.TWebLeft) / 2) * sectionRHS.Hinternal;
            if (section is SectionCHS sectionCHS)
                return 0.6 * sectionCHS.Area;
            if (section is SectionT sectionT)
                return sectionT.Tw * (sectionT.H - sectionT.Tf);
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        private double CalculateShearXCapacity()
        {
            throw new NotImplementedException();
        }

        #endregion


        #region Bending Moment Capacity

        private double CalculateBendingMoment1Capacity(ResultBeamForces resultBeamForces, ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (resultBeamForces.V2 < 0.6 * CalculateShearXCapacity())   // low shear condition
            {
                if (Class == SectionClass.Class1 || Class == SectionClass.Class2)
                    return Math.Min(GetPy(material, steelClass) * ((Section)section).Wpl2, 1.2 * GetPy(material, steelClass) * ((Section)section).Wel2);
                else if (Class == SectionClass.Class3)
                    return GetPy(material, steelClass) * ((Section)section).Wel2;
                else        // SectionClass.Class4
                    return GetPy(material, steelClass) * CalculateEffettiveElasticModulus();       // TODO: implementare Wel effettivo (vedi 8.2.2)
            }
            else // high shear condition
            {
                if (Class == SectionClass.Class1 || Class == SectionClass.Class2)
                    return Math.Min(GetPy(material, steelClass) * ( ((Section)section).Wpl2 - CalculateRhoMomentShearInteraction(resultBeamForces) * CalculatePlasticModulusShear(section)), 
                                    1.2 * GetPy(material, steelClass) * (((Section)section).Wel2 - CalculateRhoMomentShearInteraction(resultBeamForces) * CalculatePlasticModulusShear(section) / 1.5));
                else if (Class == SectionClass.Class3)
                    return GetPy(material, steelClass) * (((Section)section).Wel2 - CalculateRhoMomentShearInteraction(resultBeamForces) * CalculatePlasticModulusShear(section) / 1.5);
                else        // SectionClass.Class4
                    return GetPy(material, steelClass) * (CalculateEffettiveElasticModulus() - CalculateRhoMomentShearInteraction(resultBeamForces) * CalculatePlasticModulusShear(section) / 1.5);       
            }
        }

        /// <summary>
        /// Return the plastic modulus of shear areain high shear condition chapter 8.2.2.2
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        private double CalculatePlasticModulusShear(ISteelSection section)
        {
            if (section is SectionH sech && sech.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return (1/4) * sech.H * Math.Pow(sech.ThicknessWeb, 2);
            if (section is SectionH secH && secH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                return (1 / 4) * secH.HeightWeb * Math.Pow(secH.ThicknessWeb, 2);
            if (section is SectionC secC && secC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return (1 / 4) * secC.Tw * Math.Pow(secC.H, 2);
            if (section is SectionRHS sectionRHS)
                return  2 *(1/4) * ((sectionRHS.TWebLeft + sectionRHS.TWebLeft) / 2) * Math.Pow(sectionRHS.Hinternal,2);
            if (section is SectionCHS sectionCHS)
                return 0.6 * (1 / 6) * Math.Pow(sectionCHS.D, 3) * Math.Pow(sectionCHS.Dint, 3);
            if (section is SectionT sectionT)
                return (1/4) * sectionT.Tw * Math.Pow((sectionT.H - sectionT.Tf),3);
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        /// <summary>
        /// Return the rho coefficien in CopSuos2011 chapter 8.2.2.2
        /// </summary>
        /// <returns></returns>
        private double CalculateRhoMomentShearInteraction(ResultBeamForces resultBeamForces)
        {
            return Math.Pow((2 * resultBeamForces.V2 / CalculateShearXCapacity()) - 1, 2);
        }

        private double CalculateEffettiveElasticModulus()
        {
            throw new NotImplementedException("Effettive elastic modulus not implemented");
        }

        private double CalculateBendingMoment2Capacity()
        {
            throw new NotImplementedException();
        }

        #endregion


        #region Lateral Torsional Bucking Capacity

        //TODO: calcolare Mcx chapter 8.3.5.2

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.2
        /// </summary>
        private double CalculateLateralTorsionalBucklingMomentCapacity(ResultBeamForces resultBeamForces, ISteelSection section, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            if (CalculateSectionClass(section, resultBeamForces, material, steelClass) == SectionClass.Class1 || CalculateSectionClass(section, resultBeamForces, material, steelClass) == SectionClass.Class2)
                return CalculatePbForLatTorsBuckling(section, resultBeamForces, material, steelClass, lenght) * ((Section)section).Wpl2;
            else if (CalculateSectionClass(section, resultBeamForces, material, steelClass) == SectionClass.Class3)
                return CalculatePbForLatTorsBuckling(section, resultBeamForces, material, steelClass, lenght) * ((Section)section).Wel2;
            else
                throw new NotImplementedException("CalculateBucklingMomentCapacity: not implemented Section Class 4");
        }

        /// <summary>
        /// Calculate the pb CopSuos2011 Chapter 8.3.5.2 with the BS5950 B.2.2 method
        /// </summary>
        private double CalculatePbForLatTorsBuckling(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return Math.Min((CalculatePeForLatTorsBuckling(section, resultBeamForces, material, steelClass, lenght) * GetPy(material, steelClass)) /
                    (CalculatePhiLTForLatTorsBuckling(section, resultBeamForces, material, steelClass, lenght) +
                Math.Pow(Math.Pow(CalculatePhiLTForLatTorsBuckling(section, resultBeamForces, material, steelClass, lenght), 2) -
                CalculatePeForLatTorsBuckling(section, resultBeamForces, material, steelClass, lenght) * GetPy(material, steelClass), 0.5)), GetPy(material, steelClass));
        }

        /// <summary>
        /// BS5950 B.2.2
        /// </summary>
        private double CalculatePeForLatTorsBuckling(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return (Math.Pow(Math.PI, 2) * material.E) / (Math.Pow(CalculateLambdaLTForLatTorsBucklingBS5950(section, resultBeamForces, material, steelClass, lenght), 2));
        }

        /// <summary>
        /// BS5950 B.2.2
        /// </summary>
        private double CalculatePhiLTForLatTorsBuckling(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return (GetPy(material, steelClass) + (CalculateNLTForLatTorsBuckling(section, resultBeamForces, material, steelClass, lenght) + 1) * CalculatePeForLatTorsBuckling(section, resultBeamForces, material, steelClass, lenght)) / 2;
        }

        /// <summary>
        /// Return the Perry factor BS5950 B.2.2
        /// </summary>
        private double CalculateNLTForLatTorsBuckling(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)    // Perry factor BS5950 B.2.2
        {
            double alphaLT = 7.0;         // SAP dice che si trova qua, ma non esiste => BS5950 2.3 e prende come valore 3
            if(((Section)section).SectionType == Model.Sections.Section.SectionTypes.Rolled)            
                return Math.Max(alphaLT * (CalculateLambdaLTForLatTorsBucklingBS5950(section, resultBeamForces, material, steelClass, lenght) - CalculateLambdaL0ForLatTorsBucklingBS5950(material, steelClass)) / 1000, 0);            
            else        //Welded
            {
                if (CalculateLambdaLTForLatTorsBucklingBS5950(section, resultBeamForces, material, steelClass, lenght) <= CalculateLambdaL0ForLatTorsBucklingBS5950(material, steelClass))
                    return 0.0;
                if (CalculateLambdaL0ForLatTorsBucklingBS5950(material, steelClass) <= CalculateLambdaLTForLatTorsBucklingBS5950(section, resultBeamForces, material, steelClass, lenght) && 
                    (CalculateLambdaLTForLatTorsBucklingBS5950(section, resultBeamForces, material, steelClass, lenght) <= 2 * CalculateLambdaL0ForLatTorsBucklingBS5950(material, steelClass)))
                    return 2*alphaLT*(CalculateLambdaLTForLatTorsBucklingBS5950(section, resultBeamForces, material, steelClass, lenght) - CalculateLambdaL0ForLatTorsBucklingBS5950(material, steelClass))/1000;
                if (2 * CalculateLambdaL0ForLatTorsBucklingBS5950(material, steelClass) <= CalculateLambdaLTForLatTorsBucklingBS5950(section, resultBeamForces, material, steelClass, lenght) && 
                    (CalculateLambdaLTForLatTorsBucklingBS5950(section, resultBeamForces, material, steelClass, lenght) <= 3 * CalculateLambdaL0ForLatTorsBucklingBS5950(material, steelClass)))
                    return 2 * alphaLT * CalculateLambdaL0ForLatTorsBucklingBS5950(material, steelClass) / 1000;
                else
                    return alphaLT * (CalculateLambdaLTForLatTorsBucklingBS5950(section, resultBeamForces, material, steelClass, lenght) - CalculateLambdaL0ForLatTorsBucklingBS5950(material, steelClass)) / 1000;
            }
        }

        /// <summary>
        /// Return limiting equivalent slenderness BS5950 B2.2
        /// </summary>
        private double CalculateLambdaL0ForLatTorsBucklingBS5950(SteelMaterial steelMaterial, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            return 0.4 * Math.Pow(Math.Pow(Math.PI, 2) * steelMaterial.E * GetPy(steelMaterial, steelClass), 2);
        }

        private double CalculateLambdaLTForLatTorsBucklingBS5950(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)      // la BS prevede un approccio molto complesso. Uso il metodo semplificato secondo CopSuos2011
        {
            return CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, material, steelClass, lenght);
        }

        /// <summary>
        /// Return true if the CopSuos2011 Chapter 8.3.5 say to check the lateral-torsional buckling. 
        /// </summary>
        private bool IsNecessaryTheLatTorsBucklingCheck(ISteelSection section, SteelMaterial steelMaterial, ResultBeamForces resultBeamForces, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            double epsilon = CalculateEpsilonForClassification(steelMaterial, steelClass);
            if (section is SectionCHS || section is SectionCircular)
                return false;
            else if (section is SectionRHS sectionRHS)
            {
                if (sectionRHS.H / sectionRHS.B < 1.25 || sectionRHS.B / sectionRHS.H < 1.25)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 770*epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 1.33 || sectionRHS.B / sectionRHS.H < 1.33)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 670*epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 1.40 || sectionRHS.B / sectionRHS.H < 1.40)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 580*epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 1.44 || sectionRHS.B / sectionRHS.H < 1.44)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 550*epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 1.50 || sectionRHS.B / sectionRHS.H < 1.50)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 515*epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 1.67 || sectionRHS.B / sectionRHS.H < 1.67)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 435 * epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 1.75 || sectionRHS.B / sectionRHS.H < 1.75)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 410 * epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 1.80 || sectionRHS.B / sectionRHS.H < 1.80)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 395 * epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 2.0 || sectionRHS.B / sectionRHS.H < 2.0)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 340 * epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 2.5 || sectionRHS.B / sectionRHS.H < 2.5)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 275 * epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 3.0 || sectionRHS.B / sectionRHS.H < 3.0)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 225 * epsilon)
                        return false;
                if (sectionRHS.H / sectionRHS.B < 4.0 || sectionRHS.B / sectionRHS.H < 4.0)
                    if (CalculateLambdaForLatTorsBuckling(section, lenght) < 170 * epsilon)
                        return false;
            }
            else if (section is SectionH || section is SectionC )
            {
                if (GetPy(steelMaterial, steelClass) <= 235)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 37.1)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 245)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 36.3)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 255)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 35.6)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 265)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 35.0)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 275)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 34.3)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 315)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 32.1)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 325)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 31.6)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 335)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 31.1)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 345)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 30.6)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 355)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 30.2)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 400)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 28.4)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 410)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 28.1)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 430)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 27.4)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 440)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 27.1)
                        return false;
                if (GetPy(steelMaterial, steelClass) <= 460)
                    if (CalculateLambdaLTForLatTorsBuckling(section, resultBeamForces, steelMaterial, steelClass, lenght) < 26.5)
                        return false;
            }
            return true;
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.4
        /// </summary>
        /// <returns></returns>
        private double GetLeForLatTorsBuckling(double length)
        {
            return length;
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.3 table 8.4b
        /// </summary>
        /// <returns></returns>
        private double CalculateMLTForLatTorsBuckling()
        {
            //TODO: implementare con le stazioni
            // mLt = Math.Max( (0.2+(0.15 * M1 + 0.5 * M2 + 0.15 * M4)) / Mmax, 0.44)
            return 1.0; // a favore di sicurezza si prende il massimo possibile
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.3 equation 8.25
        /// </summary>
        /// <returns></returns>
        private double CalculateLambdaLTForLatTorsBuckling(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, double lenght)
        {
            return CalculateUForLatTorsBuckling(section) * CalculateVForLatTorsBuckling(section, lenght) * 
                CalculateLambdaForLatTorsBuckling(section, lenght) * Math.Sqrt(CalculateBwForLatTorsBuckling(section, resultBeamForces, material, steelClass));
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.3 u parameter
        /// </summary>
        /// <returns></returns>
        private double CalculateUForLatTorsBuckling(ISteelSection section)
        {
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return 0.9;
            else if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Welded)
                return 1.0;
            else
                throw new NotImplementedException("CalculateUForLatTorsBuckling: not implemented SectionType");
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.3 equation 8.27
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        private double CalculateVForLatTorsBuckling(ISteelSection section, double lenght)
        {
            return 1 / Math.Pow(1 + 0.05 * (Math.Pow(CalculateLambdaForLatTorsBuckling(section, lenght) / CalculateXForLatTorsBuckling(section), 2)), 0.25);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.3 equation 8.26
        /// </summary>
        /// <returns></returns>
        private double CalculateLambdaForLatTorsBuckling(ISteelSection section, double lenght)
        {
            return GetLeForLatTorsBuckling(lenght) / Math.Min(((Section)section).InertiaRadiusX, ((Section)section).InertiaRadiusY);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.3 x parameter
        /// </summary>
        /// <returns></returns>
        private double CalculateXForLatTorsBuckling(ISteelSection section)
        {
            if (section is SectionCHS sectionCHS)
                throw new NotImplementedException("CalculateXForLatTorsBuckling");
            if (section is SectionH sectionH)
                return sectionH.H / sectionH.ThicknessWeb;
            if (section is SectionRHS sectionRHS)
                throw new NotImplementedException("CalculateXForLatTorsBuckling");
            if (section is SectionC sectionC)
                return sectionC.H / ((sectionC.ThicknessBottom + sectionC.ThicknessTop) / 2);
            else
                throw new NotImplementedException("CalculateXForLatTorsBuckling: not implemented Section");
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.3 beta w parameter
        /// </summary>
        /// <param name="section"></param>
        /// <param name="resultBeamForces"></param>
        /// <param name="material"></param>
        /// <param name="steelClass"></param>
        /// <returns></returns>
        private double CalculateBwForLatTorsBuckling(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (CalculateSectionClass(section, resultBeamForces, material, steelClass) == SectionClass.Class1 ||
                CalculateSectionClass(section, resultBeamForces, material, steelClass) == SectionClass.Class2)
                return 1.0;
            else if (CalculateSectionClass(section, resultBeamForces, material, steelClass) == SectionClass.Class3)
                return ((Section)section).Wel1 / ((Section)section).Sx;
            else        //class4
                return CalculateEffettiveElasticModulus() / ((Section)section).Sx;
        }

        #endregion


        #region Section Class

        /// <summary>
        /// Return the <see cref="SectionClass"/> of the section <paramref name="section"/> with the <paramref name="resultBeamForces"/> - Chapter 7
        /// </summary>
        /// <param name="section"></param>
        /// <param name="resultBeamForces"></param>
        /// <param name="steelClass"></param>
        /// <param name="material"></param>
        /// <returns></returns>
        private SectionClass CalculateSectionClass(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            SectionClass sectionClass = SectionClass.Class1;

            if (MinSigma(section, resultBeamForces.N, resultBeamForces.M2, resultBeamForces.M1) < 0.0)
            {
                if (section is SectionH sectionH)
                {
                    if (sectionH.IsDoubleSymmetric)
                    {
                        if (Math.Abs(resultBeamForces.M2) > 0 || Math.Abs(resultBeamForces.N) > 0)        // AxialAndBendingStrongDirection                        
                            //classification of flanged for axial force due to bending
                            sectionClass = SetWorstClass(new SectionClass[] {GetClassCompressedOuterFlangeBending(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, sectionH.SectionType, material, steelClass),
                                                                            GetClassCompressedOuterFlangeBending(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, sectionH.SectionType, material, steelClass),
                                                                            GetClassCompressedWebBendingMoment(sectionH.HeightWeb, sectionH.ThicknessWeb, resultBeamForces, section, material, steelClass)});

                        if (Math.Abs(resultBeamForces.M1) > 0)                                            // AxialAndBendingWeakDirection                        
                            //classification only for Compression. Other detailed calculation should be found and implemented
                            sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedInternalFlangeBending(sectionH.HeightWeb, sectionH.ThicknessWeb, material, steelClass),
                                                                            GetClassCompressedInternalFlangeBending(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, material, steelClass),
                                                                            GetClassCompressedInternalFlangeBending(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, material, steelClass) });                    
                    }

                    else if (Math.Abs(resultBeamForces.N) > 0 && Math.Abs(resultBeamForces.M1) == 0 && Math.Abs(resultBeamForces.M2) == 0)
                        //classification only for Compression. Other detailed calculation should be found and implemented
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedInternalFlangeBending(sectionH.HeightWeb, sectionH.ThicknessWeb, material, steelClass),
                                                                        GetClassCompressedInternalFlangeBending(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, material, steelClass),
                                                                        GetClassCompressedInternalFlangeBending(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, material, steelClass) });
                    return sectionClass;
                }

                else if (section is SectionCHS sectionCHS)
                    return GetSectionClassCHS(sectionCHS.D, sectionCHS.T, material, steelClass);

                else if (section is SectionRHS sectionRHS)
                {
                    if (Math.Abs(resultBeamForces.M2) > 0)              // ClassificationAxialBendingStrongAxis
                    {
                        //flange are load with constant load     //classification webs                           //from equilibrium of Σ sigma = Ned
                        if (sectionRHS.TTop == sectionRHS.TBottom && sectionRHS.TWebLeft == sectionRHS.TWebRight && (int)sectionClass <= 3)
                            sectionClass = SetWorstClass(new SectionClass[] { GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, sectionRHS.FormedType, resultBeamForces, section, material, steelClass),
                                                                            GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, sectionRHS.FormedType, material, steelClass)});
                        else
                            sectionClass = SetWorstClass(new SectionClass[] {GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, sectionRHS.FormedType, material, steelClass),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, sectionRHS.FormedType, resultBeamForces, section, material, steelClass),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebRight, sectionRHS.FormedType, resultBeamForces, section, material, steelClass) });
                    }

                    if (Math.Abs(resultBeamForces.M1) > 0)                                // ClassificationAxialBendingWeakAxis
                    {
                        if (sectionRHS.TWebLeft == sectionRHS.TWebRight && sectionRHS.TTop == sectionRHS.TBottom)
                            sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, sectionRHS.FormedType, resultBeamForces, section, material, steelClass),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebRight, sectionRHS.FormedType, resultBeamForces, section, material, steelClass),
                                                                            GetClassCompressedWebRHS(sectionRHS.Binternal, sectionRHS.TTop, sectionRHS.FormedType, resultBeamForces, section, material, steelClass)});
                        else
                            sectionClass = SetWorstClass(new SectionClass[] { GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TTop, sectionRHS.Hinternal, sectionRHS.FormedType, material, steelClass),
                                                                            GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, sectionRHS.FormedType, material, steelClass),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, sectionRHS.FormedType, resultBeamForces, section, material, steelClass),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebRight, sectionRHS.FormedType, resultBeamForces, section, material, steelClass) });
                    }

                    if (Math.Abs(resultBeamForces.N) > 0 && Math.Abs(resultBeamForces.M1) == 0 && Math.Abs(resultBeamForces.M2) == 0)
                        sectionClass = SetWorstClass(new SectionClass[] { GetClassCompressedWebAxialCompression(sectionRHS.Hinternal, sectionRHS.TWebLeft, resultBeamForces, section, material, steelClass),
                                                                        GetClassCompressedWebAxialCompression(sectionRHS.Hinternal, sectionRHS.TWebRight, resultBeamForces, section, material, steelClass),
                                                                        GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TTop, sectionRHS.Hinternal, sectionRHS.FormedType, material, steelClass),
                                                                        GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, sectionRHS.FormedType, material, steelClass) });
                    return sectionClass;
                }

                else if (section is SectionT sectionT)
                {
                    if (Math.Abs(resultBeamForces.M2) > 0 || Math.Abs(resultBeamForces.M2) > 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeBending(sectionT.B / 2, sectionT.Tf / 2, sectionT.SectionType, material, steelClass),
                                                                    GetClassCompressedStemT(sectionT.H, sectionT.Tw, material, steelClass) });

                    if (Math.Abs(resultBeamForces.N) > 0 && Math.Abs(resultBeamForces.M1) == 0 && Math.Abs(resultBeamForces.M2) == 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeAxial(sectionT.B / 2, sectionT.Tf / 2, material, steelClass),
                                                                    GetClassCompressedStemT(sectionT.H, sectionT.Tw, material, steelClass) });
                    return sectionClass;
                }

                else if (section is SectionC sectionC)
                {
                    if (Math.Abs(resultBeamForces.M2) > 0 || Math.Abs(resultBeamForces.M1) > 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedWebChannel(sectionC.Hw / 2, sectionC.Tw / 2, material, steelClass),
                                                                    GetClassCompressedOuterFlangeBending(sectionC.LBottom, sectionC.ThicknessBottom, sectionC.SectionType, material, steelClass),
                                                                    GetClassCompressedOuterFlangeBending(sectionC.LTop, sectionC.ThicknessTop, sectionC.SectionType, material, steelClass)});

                    if (Math.Abs(resultBeamForces.N) > 0 && Math.Abs(resultBeamForces.M1) == 0 && Math.Abs(resultBeamForces.M2) == 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedWebChannel(sectionC.Hw / 2, sectionC.Tw / 2, material, steelClass),
                                                                    GetClassCompressedOuterFlangeAxial(sectionC.LBottom, sectionC.ThicknessBottom, material, steelClass),
                                                                    GetClassCompressedOuterFlangeAxial(sectionC.LTop, sectionC.ThicknessTop, material, steelClass)});
                    return sectionClass;
                }

                else if (section is SectionL sectionL)
                {
                    if (Math.Abs(resultBeamForces.M2) > 0 || Math.Abs(resultBeamForces.M1) > 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOutstandLeg(sectionL.LHor, sectionL.THor, material, steelClass),
                                                                    GetClassCompressedOutstandLeg(sectionL.LVert, sectionL.TVert, material, steelClass)});

                    if (Math.Abs(resultBeamForces.N) > 0 && Math.Abs(resultBeamForces.M1) == 0 && Math.Abs(resultBeamForces.M2) == 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeAxial(sectionL.LHor, sectionL.THor, material, steelClass),
                                                                    GetClassCompressedOuterFlangeAxial(sectionL.LVert, sectionL.TVert, material, steelClass)});

                    return sectionClass;
                }

                else
                    throw new NotImplementedException("CalculateSectionClassException: not implemented Section");
            }
            else
                return SectionClass.Class1;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, outstand element, bending moment
        /// </summary>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <param name="sectionTypes"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedOuterFlangeBending(double b, double t, Section.SectionTypes sectionTypes, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double ctRatio = b / t;
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            if (sectionTypes == Model.Sections.Section.SectionTypes.Rolled)
            {
                if (ctRatio <= 9.0 * epsilon)
                    return SectionClass.Class1;
                else if (ctRatio <= 10.0 * epsilon)
                    return SectionClass.Class2;
                else if (ctRatio <= 15.0 * epsilon)
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else if (sectionTypes == Model.Sections.Section.SectionTypes.Welded)
            {
                if (ctRatio <= 8.0 * epsilon)
                    return SectionClass.Class1;
                else if (ctRatio <= 9.0 * epsilon)
                    return SectionClass.Class2;
                else if (ctRatio <= 13.0 * epsilon)
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else
                throw new NotImplementedException();
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, outstand element, axial compression
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedOuterFlangeAxial(double b, double t, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            double ctRatio = b / t;
            if (ctRatio <= 13.0 * epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, internal element, bending moment
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedInternalFlangeBending(double b, double t, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            double ctRatio = b / t;
            if (ctRatio <= 28.0 * epsilon)
                return SectionClass.Class1;
            else if (ctRatio <= 32.0 * epsilon)
                return SectionClass.Class2;
            else if (ctRatio <= 40.0 * epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, internal element, axial compression
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedInternalFlangeAxial(double b, double t, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            double ctRatio = b / t;
            if (ctRatio <= 40.0 * epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Web of I, H, Box section. Generally
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebBendingMoment(double b, double t, ResultBeamForces resultBeamForces, ISteelSection section, SteelMaterial steelMaterial, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(steelMaterial, steelClass);
            double ctRatio = b / t;
            if (ctRatio <= 80.0 * epsilon / (1 + GetR1(section, resultBeamForces, steelMaterial, steelClass)) && ctRatio >= 40 * epsilon)      //TODO: da completare
                return SectionClass.Class1;
            if (GetR1(section, resultBeamForces, steelMaterial, steelClass) <= 0 && 
                (ctRatio <= 100 * epsilon / (1 + GetR1(section, resultBeamForces, steelMaterial, steelClass)) && ctRatio >= 40 * epsilon))
                return SectionClass.Class2;
            if (GetR1(section, resultBeamForces, steelMaterial, steelClass) >= 0 && 
                (ctRatio <= 100 * epsilon / (1 + 1.5 * GetR1(section, resultBeamForces, steelMaterial, steelClass)) && ctRatio >= 40 * epsilon))
                return SectionClass.Class2;
            else if (ctRatio <= 120 * epsilon / (1 + 2 * GetR2(resultBeamForces, section, steelMaterial, steelClass)) && ctRatio >= 40 * epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Web of I, H, Box section. Axial Compression
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebAxialCompression(double b, double t, ResultBeamForces resultBeamForces, ISteelSection section, SteelMaterial steelMaterial, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(steelMaterial, steelClass);
            double ctRatio = b / t;
            if (ctRatio <= 120 * epsilon / (1 + 2 * GetR2(resultBeamForces, section, steelMaterial,  steelClass)) && ctRatio >= 40 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 CHS Classification
        /// </summary>
        /// <returns></returns>
        private SectionClass GetSectionClassCHS(double D, double t, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            if (D / t <= 40.0 * epsilon * epsilon)
                return SectionClass.Class1;
            else if (D / t <= 50.0 * epsilon * epsilon)
                return SectionClass.Class2;
            else if (D / t <= 140 * epsilon * epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Flange
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedFlangeRHS(double b, double t, double d,  Section.FormedTypes formed, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double ctRatio = b / t;
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            if (formed == Model.Sections.Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= 28.0 * epsilon && ctRatio <= 80 * epsilon - d / t)      // <= 80 * epsilon - d/t    TODO: da completare
                    return SectionClass.Class1;
                else if (ctRatio <= 32.0 * epsilon && ctRatio <= 62.0 * epsilon - 0.5 * d / t)
                    return SectionClass.Class2;
                else if (ctRatio <= 40.0 * epsilon)
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else if (formed == Model.Sections.Section.FormedTypes.ColdFormed)
            {
                if (ctRatio <= 26.0 * epsilon && ctRatio <= 72.0 * epsilon - d / t)
                    return SectionClass.Class1;
                else if (ctRatio <= 28.0 * epsilon && ctRatio <= 54 * epsilon - 0.5 * d / t)
                    return SectionClass.Class2;
                else if (ctRatio <= 35.0 * epsilon)
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else
                throw new NotImplementedException("Not supported Section type");
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Web
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebRHS(double d, double t, Section.FormedTypes formed, ResultBeamForces resultBeamForces, ISteelSection section, SteelMaterial steelMaterial, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double ctRatio = d / t;
            double epsilon = CalculateEpsilonForClassification(steelMaterial, steelClass);
            if (formed == Model.Sections.Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= 64.0 * epsilon / (1 + 0.6 * GetR1(section, resultBeamForces, steelMaterial, steelClass)) && ctRatio >= 40.0 * epsilon)      //TODO: da completare
                    return SectionClass.Class1;
                else if (ctRatio <= 80.0 * epsilon / (1 + GetR1(section, resultBeamForces, steelMaterial, steelClass)) && ctRatio >= 40.0 * epsilon)
                    return SectionClass.Class2;
                else if (ctRatio <= 120 * epsilon / (1 + 2 * GetR1(section, resultBeamForces, steelMaterial, steelClass)) && ctRatio >= 40.0 * epsilon)
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else if (formed == Model.Sections.Section.FormedTypes.ColdFormed)
            {
                if (ctRatio <= 56.0 * epsilon / (1 + 0.6 * GetR1(section, resultBeamForces, steelMaterial, steelClass)) && ctRatio >= 35.0 * epsilon)      //TODO: da completare
                    return SectionClass.Class1;
                else if (ctRatio <= 70.0 * epsilon / (1 + GetR1(section, resultBeamForces, steelMaterial, steelClass)) && ctRatio >= 35.0 * epsilon)
                    return SectionClass.Class2;
                else if (ctRatio <= 105 * epsilon / (1 + 2 * GetR1(section, resultBeamForces, steelMaterial, steelClass)) && ctRatio >= 35.0 * epsilon)
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else
                throw new NotImplementedException("Not supported Section type");
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Angle, compression due to bending and axial compression
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedAngleWithAxialCompression(double b, double d, double t, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            if (b / t < 15.0 * epsilon && d / t < 15.0 * epsilon && (b + d) / t < 24.0 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Angle, compression due to bending
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedAngleOnlyBending(double b, double d, double t, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            if (b / t < 9.0 * epsilon && d / t < 9.0 * epsilon)
                return SectionClass.Class1;
            if (b / t < 10.0 * epsilon && d / t < 10.0 * epsilon)
                return SectionClass.Class2;
            if (b / t < 15.0 * epsilon && d / t < 15.0 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Outstand Leg of an angle 
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedOutstandLeg(double b, double t, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            if (b / t < 9.0 * epsilon)
                return SectionClass.Class1;
            if (b / t < 10.0 * epsilon)
                return SectionClass.Class2;
            if (b / t < 15.0 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Stem of a T section
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedStemT(double d, double t, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            if (d / t < 8.0 * epsilon)
                return SectionClass.Class1;
            if (d / t < 9.0 * epsilon)
                return SectionClass.Class2;
            if (d / t < 18.0 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Channel of a C section
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebChannel(double d, double t, SteelMaterial material, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            double epsilon = CalculateEpsilonForClassification(material, steelClass);
            if (d / t < 40.0 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// Return the worst <see cref="SectionClass"/> section between <paramref name="obj1"/> e <paramref name="obj2"/>
        /// </summary>
        /// <returns></returns>
        private SectionClass SetWorstClass(SectionClass obj1, SectionClass obj2)
        {
            int cl1 = (int)obj1;
            int cl2 = (int)obj2;

            return (SectionClass)Math.Max(cl1, cl2);
        }

        /// <summary>
        /// Return the worst class section between an array of <see cref="SectionClass"/>
        /// </summary>
        /// <returns></returns>
        private SectionClass SetWorstClass(SectionClass[] obj)
        {
            SectionClass sectionClass = SectionClass.Class1;

            for (int i = 0; i < obj.Count(); i++)
                sectionClass = SetWorstClass(sectionClass, obj[i]);

            return sectionClass;
        }

        /// <summary>
        /// CopSuos2011 Chapter 7.3. Calculate R1 paramenter
        /// </summary>
        /// <returns></returns>
        private double GetR1(ISteelSection section, ResultBeamForces resultBeamForces, SteelMaterial steelMaterial, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (section is SectionH sectionH)
            {
                if (sectionH.ThicknessBottomFlange == sectionH.ThicknessTopFlange && sectionH.LenghtBottomFlange == sectionH.LenghtTopFlange)
                {
                    double r1 = (GetFcForClassification(resultBeamForces, section) / (sectionH.HeightWeb * sectionH.ThicknessWeb * GetPy(steelMaterial, steelClass)));
                    r1 = r1 < 1 ? 1 : r1;
                    r1 = r1 > -1 ? -1 : r1;
                    return r1;
                }
                else
                {
                    double r1 = (GetFcForClassification(resultBeamForces, section) / (sectionH.HeightWeb * sectionH.ThicknessWeb * GetPy(steelMaterial, steelClass)) +
                        (((sectionH.LenghtBottomFlange * sectionH.LenghtBottomFlange - sectionH.LenghtTopFlange * sectionH.ThicknessTopFlange) * GetPy(steelMaterial, steelClass)) /
                            (sectionH.HeightWeb * sectionH.ThicknessWeb * GetPy(steelMaterial, steelClass))));
                    r1 = r1 < 1 ? 1 : r1;
                    r1 = r1 > -1 ? -1 : r1;
                    return r1;
                }
            }
            else if (section is SectionRHS sectionRHS)
            {
                double r1 = (GetFcForClassification(resultBeamForces, section) / (2 * sectionRHS.Hinternal * sectionRHS.TWebLeft * GetPy(steelMaterial, steelClass)));
                r1 = r1 < 1 ? 1 : r1;
                r1 = r1 > -1 ? -1 : r1;
                return r1;
            }
            else
                throw new NotImplementedException("Not supported Section type");
        }

        /// <summary>
        /// CopSuos2011 Chapter 7.3. Calculate R2 paramenter
        /// </summary>
        /// <returns></returns>
        private double GetR2(ResultBeamForces resultBeamForces, ISteelSection section, SteelMaterial steelMaterial, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (section is SectionH sectionH)
            {
                if (sectionH.ThicknessBottomFlange == sectionH.ThicknessTopFlange && sectionH.LenghtBottomFlange == sectionH.LenghtTopFlange)
                    return (GetFcForClassification(resultBeamForces, section) / (GetCrossSectionalArea(section) * GetPy(steelMaterial, steelClass)));
                else
                    throw new NotImplementedException("Not supported case");
            }
            else if (section is SectionRHS sectionRHS)
                return (GetFcForClassification(resultBeamForces, section) / (GetCrossSectionalArea(section) * GetPy(steelMaterial, steelClass)));

            else
                throw new NotImplementedException("Not supported Section type");
        }

        /// <summary>
        /// CopSuos2011 Chapter 7.3. Calculate fc for R1 and R2 parameters
        /// </summary>
        /// <returns></returns>
        private double GetFcForClassification(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            return resultBeamForces.N / GetCrossSectionalArea(section);
        }

        /// <summary>
        /// Calculate the cross sectional area
        /// </summary>
        /// <returns></returns>
        private double GetCrossSectionalArea(ISteelSection section)
        {
            return ((Section)section).Area;
        }

        private double GetEffettiveArea(ISteelSection section)
        {
            return GetCrossSectionalArea(section);      //TODO: implementare GetEffettiveArea()
        }

        #endregion


        #region py

        /// <summary>
        /// Return the py value 
        /// </summary>
        /// <returns></returns>
        private double GetPy(SteelMaterial steelMaterial, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (steelMaterial.Fyk < 460)
                return Math.Min(steelMaterial.Fyk / GetGammaM1(steelMaterial.Fyk, steelClass), steelMaterial.Fu / GetGammaM2(steelMaterial.Fyk, steelClass));
            else            // (steelMaterial.Fyk >= 460)
                return Math.Min(steelMaterial.Fyk / GetGammaM1(steelMaterial.Fyk, steelClass), steelMaterial.Fu / GetGammaM2(steelMaterial.Fyk, steelClass));
        }

        /// <summary>
        /// Return the GammaM1 factor for reduce the steel fy. CopSuos 2011 Chapter 4 table 4.1
        /// </summary>
        /// <returns></returns>
        private double GetGammaM1(double Ys, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (Ys < 460)
            {
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1)
                    return 1.0;
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class2)
                    return 1.2;
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1H)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
            }
            if (Ys > 460 && Ys < 690)
            {
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1 || steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class2
                    || steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1H)
                    return 1.0;
            }
            throw new NotImplementedException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
        }

        /// <summary>
        /// Return the GammaM2 factor for reduce the steel fy. CopSuos 2011 Chapter 4 table 4.1
        /// </summary>
        /// <returns></returns>
        private double GetGammaM2(double Ys, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (Ys < 460)
            {
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1)
                    return 1.2;
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class2)
                    return 1.3;
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1H)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
            }
            if (Ys > 460 && Ys < 690)
            {
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1 || steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class2
                    || steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1H)
                    return 1.2;
            }
            throw new NotImplementedException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
        }

        /// <summary>
        /// Return the value of Epsilon for section classification
        /// </summary>
        /// <returns></returns>
        private double CalculateEpsilonForClassification(SteelMaterial steelMaterial, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            return Math.Sqrt(275/GetPy(steelMaterial, steelClass));
        }

        #endregion

        #endregion





    }
}
