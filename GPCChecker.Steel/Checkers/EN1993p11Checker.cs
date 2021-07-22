using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Model.Sections;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Results;
using GPC.Model.LoadCases;
using System.ComponentModel;

namespace GPC.Checkers.Steel.Checkers
{
    public class EN1993p11Checker : EuroCodeChecker
    {
        #region Public enum

        public enum SectionClass
        {
            [Description("Class 1")]
            Class1 = 1,
            [Description("Class 2")]
            Class2 = 2,
            [Description("Class 3")]
            Class3 = 3,
            [Description("Class 4")]
            Class4 = 4,
        }

        #endregion


        #region Variables

        protected double _fy;
        protected double _fu;
        protected double _epsilon;



        public double GammaM0 => ((StandardEN1993p11)_standard).GammaM0;

        public double GammaM1 => ((StandardEN1993p11)_standard).GammaM1;

        public double GammaM2 => ((StandardEN1993p11)_standard).GammaM2;

        #endregion


        public EN1993p11Checker(BeamCheckerAttributes attributes, EN1993p11Checker.EN1993p11Options options, StandardEN1990 standardEN1990) 
            : base(attributes, options, standardEN1990)
        {
            _fy = attributes.Sections.Select(i => i.SteelMaterial.Fyk).Min();
            _epsilon = Math.Sqrt(235 / _fy);        //value of Epsilon for section classification
        }


        public override void PerformCheck()
        {
            _beamStationResults = PerformCheck(_beamCheckersAttributes.Sections, _beamCheckersAttributes.Results);
        }

        public async void PerformCheckAsync()
        {
            await Task.Run(() =>
            {
                _beamStationResults = PerformCheck(_beamCheckersAttributes.Sections, _beamCheckersAttributes.Results);
            });
        }

        /// <param name="steelSection">section of each station</param>
        /// <param name="beamResult">result for each station and loadcase</param>
        /// <returns></returns>
        private EN1993p11BeamStationResult[] PerformCheck(ISteelSection[] steelSection, BeamResult[] beamResult)
        {
            EN1993p11BeamStationResult[] stationResults = new EN1993p11BeamStationResult[steelSection.Length * beamResult.Length];

            for (int k = 0; k < beamResult.Length; k++)
            {

            }

            return stationResults;
        }

        #region Private Method



        #region Axial Tension/Compression

        private double CalculateAxialTensionCapacity(ISteelSection section)
        {
            return Math.Min(CalculateNplRd(section), CalculateNulRd(section));
        }

        private double CalculateNplRd(ISteelSection section)
        {
            return section.Area * _fy / GammaM0;
        }
        private double CalculateNulRd(ISteelSection section)
        {
            return 0.90 * GetAreaNet(section) * _fu / GammaM2;
        }

        private double ClaculateAxialCompressionCapacity(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass != SectionClass.Class4)
                return section.Area * _fy / ((StandardEN1993p11)_standard).GammaM0;
            else
                return GetAreaNet(section) * _fy / GammaM0;
        }

        #endregion

        #region Axial Buckling

        protected double GetImperfectionFactor1Axis(ISteelSection section)
        {

        }



        private double GetImperfectionFactorBucklingCurve1Axis(EN1993p11Options.BuckingCurves buckingCurve)
        {
            if (buckingCurve == EN1993p11Options.BuckingCurves.a0)
                return ((StandardEN1993p11)_standard).AlphaImperfectionFactorForCurveA0;

            else if (buckingCurve == EN1993p11Options.BuckingCurves.a)
                return ((StandardEN1993p11)_standard).AlphaImperfectionFactorForCurveA;

            else if (buckingCurve == EN1993p11Options.BuckingCurves.b)
                return ((StandardEN1993p11)_standard).AlphaImperfectionFactorForCurveB;

            else if (buckingCurve == EN1993p11Options.BuckingCurves.c)
                return ((StandardEN1993p11)_standard).AlphaImperfectionFactorForCurveC;

            else if (buckingCurve == EN1993p11Options.BuckingCurves.d)
                return ((StandardEN1993p11)_standard).AlphaImperfectionFactorForCurveD;

            else
                throw new NotImplementedException("GetAlphaBucklingCurve: not implemented BuckingCurve");
        }



        /// <summary>
        /// CopSuos2011 Table 8.7 - Buckling curve
        /// </summary>
        private EN1993p11Options.BuckingCurves GetBucklingCurveYYAxis(ISteelSection section)
        {
            if (section is SteelSectionCHS sectionCHS)
            {
                if (sectionCHS.FormedType == Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                        return EN1993p11Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return EN1993p11Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                {
                    if (sectionCHS.IsDoubleSymmetric)
                        return EN1993p11Options.BuckingCurves.c;
                    else
                        return EN1993p11Options.BuckingCurves.c;
                }
            }

            else if (section is SteelSectionRHS sectionRHS)
            {
                if (sectionRHS.SectionType == Section.SectionTypes.Welded)
                {
                    if (sectionRHS.ThicknessBottom / sectionRHS.Base < 40 && sectionRHS.ThicknessTop / sectionRHS.Base < 40 &&
                        sectionRHS.ThicknessWebLeft / sectionRHS.Height < 30 && sectionRHS.ThicknessWebRight / sectionRHS.Height < 30)
                        return EN1993p11Options.BuckingCurves.b;
                    else
                        return EN1993p11Options.BuckingCurves.c;
                }
                else if (sectionRHS.FormedType == Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                        return EN1993p11Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return EN1993p11Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                    return EN1993p11Options.BuckingCurves.c;
            }

            else if (section is SteelSectionH sectionH)
            {
                if (sectionH.SectionType == Section.SectionTypes.Rolled)
                {
                    if ((2 * sectionH.Height) / (sectionH.LenghtTopFlange + sectionH.LenghtBottomFlange) > 1.2)
                    {
                        if (section.SteelMaterial.Fyk < 460.0)
                        {
                            if (sectionH.ThicknessBottomFlange < 40.0 && sectionH.ThicknessTopFlange < 40.0 && sectionH.ThicknessWeb < 40.0)
                                return EN1993p11Options.BuckingCurves.a;
                            else
                                return EN1993p11Options.BuckingCurves.b;
                        }
                        else
                        {
                            if (sectionH.ThicknessBottomFlange < 40.0 && sectionH.ThicknessTopFlange < 40.0 && sectionH.ThicknessWeb < 40.0)
                                return EN1993p11Options.BuckingCurves.a0;
                            else
                                return EN1993p11Options.BuckingCurves.a0;
                        }
                    }
                    else // (sectionH.IsHSection)
                    {
                        if (sectionH.ThicknessBottomFlange < 100.0 && sectionH.ThicknessTopFlange < 100.0 && sectionH.ThicknessWeb < 100.0)
                            return EN1993p11Options.BuckingCurves.b;
                        else
                            return EN1993p11Options.BuckingCurves.d;
                    }
                }
                else //(sectionH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                        return EN1993p11Options.BuckingCurves.b;
                    else
                        return EN1993p11Options.BuckingCurves.c;
                }
            }

            else if (section is SectionC _ || section is SectionT _)
                return EN1993p11Options.BuckingCurves.c;

            else if (section is SectionL _ )
                return EN1993p11Options.BuckingCurves.b;

            else
                throw new NotImplementedException("GetBucklingCurve: not implemented section");
        }

        /// <summary>
        /// CopSuos2011 Table 8.7 - Buckling curve
        /// </summary>
        private EN1993p11Options.BuckingCurves GetBucklingCurveXXAxis(ISteelSection section)
        {
            if (section is SteelSectionCHS sectionCHS)
            {
                if (sectionCHS.FormedType == Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                        return EN1993p11Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return EN1993p11Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                {
                    if (sectionCHS.IsDoubleSymmetric)
                        return EN1993p11Options.BuckingCurves.a0;
                    else
                        return EN1993p11Options.BuckingCurves.c;
                }
            }
            else if (section is SteelSectionRHS sectionRHS)
            {
                if (sectionRHS.SectionType == Section.SectionTypes.Welded)
                {
                    if (sectionRHS.ThicknessBottom < 40 && sectionRHS.ThicknessTop < 40 && sectionRHS.ThicknessWebLeft < 40 && sectionRHS.ThicknessWebRight < 40)
                        return EN1993p11Options.BuckingCurves.b;
                    else
                        return EN1993p11Options.BuckingCurves.c;
                }
                else if (sectionRHS.FormedType == Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                        return EN1993p11Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return EN1993p11Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                    return EN1993p11Options.BuckingCurves.c;
            }
            else if (section is SteelSectionH sectionH)
            {
                if (sectionH.SectionType == Section.SectionTypes.Rolled)
                {
                    if ((2.0 * sectionH.Height) / (sectionH.LenghtTopFlange + sectionH.LenghtBottomFlange) > 1.2)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return EN1993p11Options.BuckingCurves.b;
                        else
                            return EN1993p11Options.BuckingCurves.c;
                    }
                    else // (sectionH.IsHSection)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return EN1993p11Options.BuckingCurves.c;
                        else
                            return EN1993p11Options.BuckingCurves.d;
                    }
                }
                else //(sectionH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                        return EN1993p11Options.BuckingCurves.c;
                    else
                        return EN1993p11Options.BuckingCurves.d;
                }
            }
            else if (section is SectionC _ || section is SectionL _ || section is SectionT _)
                return EN1993p11Options.BuckingCurves.c;
            else
                throw new NotImplementedException("GetBucklingCurve: not implemented section");
        }

        #endregion

        #region Bending Moment

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.5(2)
        /// </summary>
        private double CalculateMcRd1(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                return section.Wpl1 * _fy / GammaM0;
            else if (sectionClass == SectionClass.Class3)
                return section.Wel1 * _fy / GammaM0;
            else
                return GetWeffMin1(section) * _fy / GammaM0;
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.5(2)
        /// </summary>
        private double CalculateMcRd2(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                return section.Wpl2 * _fy / GammaM0;
            else if (sectionClass == SectionClass.Class3)
                return section.Wel2 * _fy / GammaM0;
            else
                return GetWeffMin2(section) * _fy / GammaM0;
        }

        #endregion

        #region Shear

        private double CalculateShear2Capacity(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            double n;
            if (section.SteelMaterial.Fyk <= 460)
                n = ((StandardEN1993p11)_standard).NShearBucklingLowGradeOfSteel;
            else
                n = ((StandardEN1993p11)_standard).NShearBucklingHighGradeOfSteel; ;

            if (section is SteelSectionH sectionH)
                if (sectionH.SectionType == Section.SectionTypes.Rolled)
                {
                    if (sectionH.HeightWeb / sectionH.ThicknessWeb > 72.0 * _epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * _fy * GetShearArea2(section);
                }
                else
                {
                    if (sectionH.HeightWeb / sectionH.ThicknessWeb > 72.0 * _epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * _fy * (GetShearArea2(section) / Math.Sqrt(3.0)) / GammaM0;
                }
            if (section is SteelSectionRHS sectionRHS)
                if (sectionRHS.SectionType == Section.SectionTypes.Rolled)
                {
                    if (sectionRHS.ThicknessWebLeft / sectionRHS.Heightinternal > 72.0 * _epsilon / n ||
                        sectionRHS.ThicknessWebRight / sectionRHS.Heightinternal > 72.0 * _epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * _fy * (GetShearArea2(section) / Math.Sqrt(3.0)) / GammaM0;
                }
                else
                {
                    if (sectionRHS.ThicknessWebLeft / sectionRHS.Heightinternal > 72.0 * _epsilon / n ||
                        sectionRHS.ThicknessWebRight / sectionRHS.Heightinternal > 72.0 * _epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * _fy * (GetShearArea2(section) / Math.Sqrt(3.0)) / GammaM0;
                }
            if (section is SteelSectionC sectionC)
                if (sectionC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if (sectionC.HeightWeb / sectionC.ThicknessWeb > 72.0 * _epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * _fy * (GetShearArea2(section) / Math.Sqrt(3.0)) / GammaM0;
                }
                else
                {
                    if (sectionC.HeightWeb / sectionC.ThicknessWeb > 72.0 * _epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * _fy * (GetShearArea2(section) / Math.Sqrt(3.0)) / GammaM0;
                }

            return CalculateShearReductionDueToTorsion(resultBeamForces, section) * _fy * (GetShearArea2(section) / Math.Sqrt(3.0)) / GammaM0;
        }

        private double CalculateShear1Capacity(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            return CalculateShearReductionDueToTorsion(resultBeamForces, section) * _fy * GetShearArea1(section) / Math.Sqrt(3.0);
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.6(2)
        /// </summary>
        /// <returns></returns>
        private double CalculateVcRd1(ISteelSection section)
        {
            return GetShearArea1(section) * (_fy / Math.Sqrt(3)) / GammaM0;
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.6(2)
        /// </summary>
        /// <returns></returns>
        private double CalculateVcRd2(ISteelSection section)
        {
            return GetShearArea2(section) * (_fy / Math.Sqrt(3)) / GammaM0;
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.6(3)
        /// </summary>
        private double GetShearArea1(ISteelSection section)
        {
            if (section is SteelSectionH sech && sech.SectionType == Section.SectionTypes.Rolled)
                return sech.Area - (sech.LenghtTopFlange * sech.ThicknessTopFlange) - (sech.LenghtBottomFlange * sech.ThicknessBottomFlange) +
                    (sech.ThicknessWeb + 2 * sech.R) * (sech.ThicknessTopFlange + sech.ThicknessBottomFlange) / 2;

            if (section is SteelSectionH secH && secH.SectionType == Section.SectionTypes.Welded)
                return secH.ThicknessWeb * secH.HeightWeb;

            if (section is SteelSectionC secC && secC.SectionType == Section.SectionTypes.Rolled)
                return secC.Area - (secC.LengthTop * secC.ThicknessTop) - (secC.LengthBottom * secC.ThicknessBottom) +
                    (secC.ThicknessWeb + 2 * secC.R) * (secC.ThicknessTop + secC.ThicknessBottom) / 2;

            if (section is SteelSectionC sectC && sectC.SectionType == Section.SectionTypes.Welded)
                return sectC.ThicknessWeb * (sectC.Height - sectC.ThicknessBottom - sectC.ThicknessTop);

            if (section is SteelSectionRHS sectionRHS && sectionRHS.SectionType == Section.SectionTypes.Welded)
                return (sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight) * sectionRHS.Heightinternal;

            if (section is SteelSectionRHS sectionRhs && sectionRhs.SectionType == Section.SectionTypes.Rolled)
                return sectionRhs.Area * sectionRhs.Height / (sectionRhs.Base + sectionRhs.Height);

            if (section is SteelSectionCHS sectionCHS)
                return 2 * sectionCHS.Area / Math.PI;

            if (section is SteelSectionT sectionT)
                return 0.9 * sectionT.Area * (sectionT.LenghtFlange - sectionT.ThicknessFlange);

            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.6(3)
        /// </summary>
        private double GetShearArea2(ISteelSection section)
        {
            if (section is SteelSectionH sech)
                return sech.LenghtTopFlange * sech.ThicknessTopFlange + sech.LenghtBottomFlange * sech.ThicknessBottomFlange;

            if (section is SteelSectionC secC)
                return secC.LengthTop * secC.ThicknessTop + secC.LengthBottom * secC.ThicknessBottom;

            if (section is SteelSectionRHS sectionRHS && sectionRHS.SectionType == Section.SectionTypes.Welded)
                return sectionRHS.Area - (sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight) * sectionRHS.Heightinternal;

            if (section is SteelSectionRHS sectionRhs && sectionRhs.SectionType == Section.SectionTypes.Rolled)
                return sectionRhs.Area * sectionRhs.Base / (sectionRhs.Base + sectionRhs.Height);

            if (section is SteelSectionCHS sectionCHS)
                return 2 * sectionCHS.Area / Math.PI;

            if (section is SteelSectionT sectionT)
                return sectionT.LenghtFlange * sectionT.ThicknessFlange;

            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        /// <summary>
        /// Shear reduction factor in case of torsion. EN1993-1-1: 2005 Chapter 6.2.7(9)
        /// </summary>
        private double CalculateShearReductionDueToTorsion(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (Math.Abs(resultBeamForces.T) > 1)
            {
                if (section is SteelSectionH)
                {
                    double sigmaStVenant = resultBeamForces.T / section.Jt;
                    return Math.Sqrt(1 - (sigmaStVenant / (1.25 * (_fy / Math.Sqrt(3)))));
                }
                else if (section is SteelSectionC)
                {
                    double sigmaStVenant = resultBeamForces.T / section.Jt;
                    double sigmaWarp = resultBeamForces.T / section.Jw;
                    return Math.Sqrt(1 - (sigmaStVenant / (1.25 * (_fy / Math.Sqrt(3))))) - (sigmaWarp / (1.25 * (_fy / Math.Sqrt(3))));
                }
                else if (section is SteelSectionCHS sectionCHS)
                {
                    double sigmaStVenant = resultBeamForces.T / (2.0 * sectionCHS.Area * sectionCHS.Thickness);
                    return 1 - (sigmaStVenant / (1.25 * (_fy / Math.Sqrt(3))));
                }
                else if (section is SteelSectionRHS sectionRHS)
                {
                    double sigmaStVenant = resultBeamForces.T / (2.0 * sectionRHS.Area *
                        (sectionRHS.ThicknessBottom + sectionRHS.ThicknessTop + sectionRHS.ThicknessWebRight + sectionRHS.ThicknessWebLeft) / 4);
                    return 1 - (sigmaStVenant / (1.25 * (_fy / Math.Sqrt(3))));
                }
                else
                    throw new NotImplementedException("Not implemented section for Torsional moment");
            }

            return 1.0;
        }

        #endregion

        #region Bending and Shear

        /// <summary>
        /// Reduced deisgn plastic resistence moment due to shear forces. Chapter 6.2.8(5)
        /// </summary>
        private double CalculateMVRd1(SectionClass sectionClass, ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
            { if (section is SteelSectionH steelSectionH)
                    return (section.Wpl1 - CalculateRhoForMomentAndShearInteraction1(resultBeamForces, section) *
                        Math.Pow(GetShearArea2(section), 2) / (4 * steelSectionH.ThicknessWeb)) * _fy / GammaM0;
                else if (section is SteelSectionRHS steelSectionRHS)
                    return (section.Wpl1 - CalculateRhoForMomentAndShearInteraction1(resultBeamForces, section) *
                        Math.Pow(GetShearArea2(section), 2) / (4 * (steelSectionRHS.ThicknessWebLeft + steelSectionRHS.ThicknessWebRight))) * _fy / GammaM0;
                else if (section is SteelSectionT steelSectionT)
                    return (section.Wpl1 - CalculateRhoForMomentAndShearInteraction1(resultBeamForces, section) *
                        Math.Pow(GetShearArea2(section), 2) / (4 * (steelSectionT.ThicknessWeb))) * _fy / GammaM0;
            }

            return CalculateMcRd1(sectionClass, section);
        }

        /// <summary>
        /// Reduced deisgn plastic resistence moment due to shear forces. Chapter 6.2.8(5)
        /// </summary>
        private double CalculateMVRd2(SectionClass sectionClass, ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
            {
                if (section is SteelSectionH steelSectionH)
                    return (section.Wpl2 - CalculateRhoForMomentAndShearInteraction2(resultBeamForces, section) *
                        Math.Pow(GetShearArea1(section), 2) / (4 * steelSectionH.ThicknessTopFlange + steelSectionH.ThicknessBottomFlange)) * _fy / GammaM0;
                else if (section is SteelSectionRHS steelSectionRHS)
                    return (section.Wpl2 - CalculateRhoForMomentAndShearInteraction2(resultBeamForces, section) *
                        Math.Pow(GetShearArea1(section), 2) / (4 * (steelSectionRHS.ThicknessTop + steelSectionRHS.ThicknessBottom))) * _fy / GammaM0;
                else if (section is SteelSectionT steelSectionT)
                    return (section.Wpl2 - CalculateRhoForMomentAndShearInteraction2(resultBeamForces, section) *
                        Math.Pow(GetShearArea1(section), 2) / (4 * (steelSectionT.ThicknessWeb))) * _fy / GammaM0;
            }

            return CalculateMcRd1(sectionClass, section);
        }

        /// <summary>
        /// Reduced deisgn plastic resistence moment due to shear forces. Chapter 6.2.8(3)
        /// </summary>
        private double CalculateRhoForMomentAndShearInteraction2(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            return Math.Pow((2 * resultBeamForces.V2 / CalculateShear2Capacity(resultBeamForces, section)) - 1, 2);
        }

        /// <summary>
        /// Reduced deisgn plastic resistence moment due to shear forces. Chapter 6.2.8(3)
        /// </summary>
        private double CalculateRhoForMomentAndShearInteraction1(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            return Math.Pow((2 * resultBeamForces.V1 / CalculateShear1Capacity(resultBeamForces, section)) - 1, 2);
        }

        #endregion

        #region Section Class

        /// <summary>
        /// Return the <see cref="SectionClass"/> due of bending compression of the section <paramref name="section"/> with the <paramref name="resultBeamForces"/> - Chapter 5 EC1993-1-1
        /// </summary>
        /// <returns></returns>
        private SectionClass CalculateSectionClassDueToBending(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (((Section)section).GetMinSigma(resultBeamForces.N, resultBeamForces.M1, resultBeamForces.M2) < 0.0)
            {
                if (section is SectionH sectionH)
                {
                    SectionClass sectionClass1Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionH.LenghtTopFlange/2, sectionH.ThicknessTopFlange),
                        GetClassCompressedOuterPlate(sectionH.LenghtBottomFlange/2, sectionH.ThicknessBottomFlange)});

                    sectionClass1Axis = SetWorstClass(sectionClass1Axis, 
                        GetClassBendingInnerPlate(resultBeamForces, sectionH.HeightWeb, sectionH.ThicknessWeb, section, sectionClass1Axis));

                    SectionClass sectionClass2Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionH.LenghtTopFlange/2, sectionH.ThicknessTopFlange),
                        GetClassCompressedOuterPlate(sectionH.LenghtBottomFlange/2, sectionH.ThicknessBottomFlange)});

                    sectionClass1Axis = SetWorstClass(sectionClass1Axis,
                        GetClassBendingInnerPlate(resultBeamForces, sectionH.HeightWeb, sectionH.ThicknessWeb, section, sectionClass1Axis));

                    return SetWorstClass(sectionClass1Axis, sectionClass2Axis);
                }

                else if (section is SectionCHS sectionCHS)
                {
                    return GetClassCHSBending(sectionCHS.Diameter, sectionCHS.Thickness);
                }

                else if (section is SectionRHS sectionRHS)
                {
                    SectionClass sectionClass1Axis = SetWorstClass(new SectionClass[] {GetClassCompressedInnerPlate(sectionRHS.BaseInternal, sectionRHS.ThicknessTop),
                                                            GetClassCompressedInnerPlate(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom) });

                    sectionClass1Axis = SetWorstClass(new SectionClass[] {sectionClass1Axis, 
                        GetClassBendingInnerPlate(resultBeamForces, sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, section, sectionClass1Axis),
                        GetClassBendingInnerPlate(resultBeamForces, sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight, section, sectionClass1Axis)});

                    SectionClass sectionClass2Axis = SetWorstClass(new SectionClass[] {GetClassCompressedInnerPlate(sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight),
                                                            GetClassCompressedInnerPlate(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft) });

                    sectionClass2Axis = SetWorstClass(new SectionClass[] {sectionClass2Axis, 
                        GetClassBendingInnerPlate(resultBeamForces, sectionRHS.BaseInternal, sectionRHS.ThicknessTop, section, sectionClass2Axis),
                        GetClassBendingInnerPlate(resultBeamForces, sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, section, sectionClass2Axis)});

                    return SetWorstClass(sectionClass1Axis, sectionClass2Axis);
                }

                else if (section is SectionT sectionT)
                {
                    return SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionT.LenghtFlange/2, sectionT.ThicknessFlange),
                        GetClassCompressedOuterPlate(sectionT.HeightWeb, sectionT.ThicknessWeb)});
                }

                else if (section is SectionC sectionC)
                {
                    SectionClass sectionClass1Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionC.LengthTop, sectionC.ThicknessTop),
                        GetClassCompressedOuterPlate(sectionC.LengthBottom, sectionC.ThicknessBottom)});

                    sectionClass1Axis = SetWorstClass(sectionClass1Axis,
                        GetClassBendingInnerPlate(resultBeamForces, sectionC.HeightWeb, sectionC.ThicknessWeb, section, sectionClass1Axis));

                    SectionClass sectionClass2Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionC.LengthTop, sectionC.ThicknessTop),
                        GetClassCompressedOuterPlate(sectionC.LengthBottom, sectionC.ThicknessBottom), 
                        GetClassCompressedInnerPlate(sectionC.HeightWeb, sectionC.ThicknessWeb)});

                    return SetWorstClass(sectionClass1Axis, sectionClass2Axis);
                }

                else if (section is SectionL sectionL)
                {
                    return SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionL.LengthHor, sectionL.ThicknessHor),
                                                            GetClassCompressedOuterPlate(sectionL.LengthVert, sectionL.ThicknessVert)});
                }

                else
                    throw new NotImplementedException("CalculateSectionClassException: not implemented Section");
            }
            else
                return SectionClass.Class1;
        }

        private SectionClass CalculateSectionClassDueToCompression(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (((Section)section).GetMinSigma(resultBeamForces.N, resultBeamForces.M1, resultBeamForces.M2) < 0.0)
            {

                if (section is SectionH sectionH)
                {
                    return SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionH.LenghtTopFlange/2, sectionH.ThicknessTopFlange),
                        GetClassCompressedOuterPlate(sectionH.LenghtBottomFlange/2, sectionH.ThicknessBottomFlange), 
                        GetClassCompressedInnerPlate(sectionH.HeightWeb, sectionH.ThicknessWeb)});
                }

                else if (section is SectionCHS sectionCHS)
                {
                    return GetClassCHSBending(sectionCHS.Diameter, sectionCHS.Thickness);
                }

                else if (section is SectionRHS sectionRHS)
                {
                    SectionClass sectionClass1Axis = SetWorstClass(new SectionClass[] {GetClassCompressedInnerPlate(sectionRHS.BaseInternal, sectionRHS.ThicknessTop),
                                                            GetClassCompressedInnerPlate(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom) });

                    sectionClass1Axis = SetWorstClass(new SectionClass[] {sectionClass1Axis,
                        GetClassBendingInnerPlate(resultBeamForces, sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, section, sectionClass1Axis),
                        GetClassBendingInnerPlate(resultBeamForces, sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight, section, sectionClass1Axis)});

                    SectionClass sectionClass2Axis = SetWorstClass(new SectionClass[] {GetClassCompressedInnerPlate(sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight),
                                                            GetClassCompressedInnerPlate(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft) });

                    sectionClass2Axis = SetWorstClass(new SectionClass[] {sectionClass2Axis,
                        GetClassBendingInnerPlate(resultBeamForces, sectionRHS.BaseInternal, sectionRHS.ThicknessTop, section, sectionClass2Axis),
                        GetClassBendingInnerPlate(resultBeamForces, sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, section, sectionClass2Axis)});

                    return SetWorstClass(sectionClass1Axis, sectionClass2Axis);
                }

                else if (section is SectionT sectionT)
                {
                    return SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionT.LenghtFlange/2, sectionT.ThicknessFlange),
                        GetClassCompressedOuterPlate(sectionT.HeightWeb, sectionT.ThicknessWeb)});
                }

                else if (section is SectionC sectionC)
                {
                    SectionClass sectionClass1Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionC.LengthTop, sectionC.ThicknessTop),
                        GetClassCompressedOuterPlate(sectionC.LengthBottom, sectionC.ThicknessBottom)});

                    sectionClass1Axis = SetWorstClass(sectionClass1Axis,
                        GetClassBendingInnerPlate(resultBeamForces, sectionC.HeightWeb, sectionC.ThicknessWeb, section, sectionClass1Axis));

                    SectionClass sectionClass2Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionC.LengthTop, sectionC.ThicknessTop),
                        GetClassCompressedOuterPlate(sectionC.LengthBottom, sectionC.ThicknessBottom),
                        GetClassCompressedInnerPlate(sectionC.HeightWeb, sectionC.ThicknessWeb)});

                    return SetWorstClass(sectionClass1Axis, sectionClass2Axis);
                }

                else if (section is SectionL sectionL)
                {
                    return SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionL.LengthHor, sectionL.ThicknessHor),
                                                            GetClassCompressedOuterPlate(sectionL.LengthVert, sectionL.ThicknessVert)});
                }

                else
                    throw new NotImplementedException("CalculateSectionClassException: not implemented Section");
            }
            else
                return SectionClass.Class3;
        }

        private SectionClass GetClassCHSBending(double diameter, double thickness)
        {
            if (diameter / thickness <= 50.0 * _epsilon * _epsilon)
                return SectionClass.Class1;

            else if (diameter / thickness <= 70.0 * _epsilon * _epsilon)
                return SectionClass.Class1;

            else if (diameter / thickness <= 90.0 * _epsilon * _epsilon)
                return SectionClass.Class3;

            else
                return SectionClass.Class4;
        }

        protected SectionClass GetClassCompressedOuterPlate(double length, double thickness)
        {
            double ratio = length / thickness;

            if (ratio <= 9.0 * _epsilon)
                return SectionClass.Class1;

            else if (ratio <= 10.0 * _epsilon)
                return SectionClass.Class2;

            else if (ratio <= 14.0 * _epsilon)
                return SectionClass.Class3;

            else
                return SectionClass.Class4;
        }

        protected SectionClass GetClassBendingInnerPlate(ResultBeamForces resultBeamForces, double length, double thickness, ISteelSection section, SectionClass sectionClass)
        {
            double alpha = - resultBeamForces.N / (4.0 * thickness * _fy * length) + 0.5;
            double ctRatio = length / thickness;

            double psi = 1;
            if (sectionClass != SectionClass.Class4)
                psi = -resultBeamForces.N * 2.0 / (section.Area * _fy) - 1.0;

            if (alpha > 0.5 && alpha < 1)
            {
                if (ctRatio <= 396.0 * _epsilon / (13.0 * alpha - 1.0))
                    return SectionClass.Class1;

                else if (ctRatio <= 456.0 * _epsilon / (13.0 * alpha - 1.0))
                    return SectionClass.Class2;

                else
                {
                    if (psi > -1)
                    {
                        if (ctRatio <= 42.0 * _epsilon / (0.67 + 0.33 * psi))
                            return SectionClass.Class3;

                        else
                            return SectionClass.Class4;
                    }

                    else if (psi <= -1)
                    {
                        if (ctRatio <= 62.0 * _epsilon * (1 - psi) * Math.Sqrt(-psi))
                            return SectionClass.Class3;

                        else
                            return SectionClass.Class4;
                    }
                    else
                        return SectionClass.Class4;
                }
            }

            else if (alpha <= 0.5 && alpha > 0)
            {
                if (ctRatio <= 36.0 * _epsilon / alpha)
                    return SectionClass.Class1;

                else if (ctRatio <= 41.5 * _epsilon / alpha)
                    return SectionClass.Class2;

                else
                {
                    if (psi > -1)
                    {
                        if (ctRatio <= 42.0 * _epsilon / (0.67 + 0.33 * psi))
                            return SectionClass.Class3;

                        else
                            return SectionClass.Class4;
                    }
                    else if (psi <= -1)
                    {
                        if (ctRatio <= 62.0 * _epsilon * (1 - psi) * Math.Sqrt(-psi))
                            return SectionClass.Class3;

                        else
                            return SectionClass.Class4;
                    }
                    else
                        return SectionClass.Class4;
                }
            }

            else if (alpha > 1)
            {
                if (psi > -1)
                {
                    if (ctRatio <= 42.0 * _epsilon / (0.67 + 0.33 * psi))
                        return SectionClass.Class3;

                    else
                        return SectionClass.Class4;
                }
                else if (psi <= -1)
                {
                    if (ctRatio <= 62.0 * _epsilon * (1 - psi) * Math.Sqrt(-psi))
                        return SectionClass.Class3;

                    else
                        return SectionClass.Class4;
                }
                else
                    return SectionClass.Class4;
            }

            else
                throw new Exception("Section classification error");
            
        }

        protected SectionClass GetClassCompressedInnerPlate(double length, double thickness)
        {
            double ctRatio = length / thickness;

            if (ctRatio <= 33.0 * _epsilon)
                return SectionClass.Class1;

            else if (ctRatio <= 38.0 * _epsilon)
                return SectionClass.Class2;

            else if (ctRatio <= 42.0 * _epsilon)
                return SectionClass.Class3;

            else
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

        #endregion

        #region Area

        private double GetAreaNet(ISteelSection steelSection)
        {
            // TODO: implementare Area netta
            return steelSection.Area;
        }

        private double GetWeffMin1(ISteelSection steelSection)
        {
            // TODO: implementare WeffMin1
            return steelSection.Wel1;
        }

        private double GetWeffMin2(ISteelSection steelSection)
        {
            // TODO: implementare WeffMin2
            return steelSection.Wel2;
        }

        #endregion

        #endregion

        public class EN1993p11Options : Options
        {
            #region Enumerable

            public enum BuckingCurves
            {
                a0,
                a,
                b,
                c,
                d
            }

            public enum LoadCondition
            {
                Constant,
                SingleForce,
                NotDirectlyLoaded
            }

            public enum SupportCondition
            {
                HingesAtEnds,
                EndsRestrained,
                OneSideRestrained_OneSideHinged
            }

            #endregion


            #region Variables



            #endregion


            #region Properties



            #endregion


            #region Constructor

            public EN1993p11Options(double unbracedLengthFactorAxialBuck1 = 1, double effectiveLengthFactorAxialBuck1 = 1, double unbracedLengthFactorAxialBuck2 = 1,
                double effectiveLengthFactorAxialBuck2 = 1, double UnbracedLengthFactorLatTorsBuck = 1, double effectiveLengthFactorLatTorsBuck = 1,
                double eqvUniformMomentFactorm1 = 1, double eqvUniformMomentFactorm2 = 1, double eqvUniformMomentFactormLT = 1)
                : base(unbracedLengthFactorAxialBuck1, effectiveLengthFactorAxialBuck1, unbracedLengthFactorAxialBuck2,
                      effectiveLengthFactorAxialBuck2, UnbracedLengthFactorLatTorsBuck, effectiveLengthFactorLatTorsBuck, 1, 1, 1, 1,
                      eqvUniformMomentFactorm1, eqvUniformMomentFactorm2, eqvUniformMomentFactormLT)
            {

            }

            #endregion


            #region Setter



            #endregion
        }
    }
}
