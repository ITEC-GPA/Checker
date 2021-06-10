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
using GPC.Checkers.Steel.Checkers;

namespace GPC.Checkers.Steel.BeamChecker
{
    public class Cop2011BeamChecker : BeamCheckerResults
    {

        #region Public enum

        public enum SectionClass
        {
            [Description("Plastic")] 
            Class1 = 1,
            [Description("Compact")] 
            Class2 = 2,
            [Description("Semi-Compact")]
            Class3 = 3,
            [Description("Slender")] 
            Class4 = 4,
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
        // Stations

        protected double _py;
        protected double _epsilon;

        #endregion


        #region Properties

        internal SteelMaterial Material => (SteelMaterial)Section[0].Material;

        internal Cop2011Checker.Cop2011Options CopSuos2011Options => (Cop2011Checker.Cop2011Options)Options;

        internal double Py => _py;  

        internal double Epsilon => _epsilon;

        #endregion


        #region Constructor

        public Cop2011BeamChecker(Cop2011BeamCheckerOptions cop2011BeamChecker, ILoadCase loadCase, StandardCopSuos2011 standard)
            : base(cop2011BeamChecker, loadCase, standard)
        {
            _py = GetPy();
            _epsilon = Math.Sqrt(275 / _py);        //value of Epsilon for section classification
        }

        #endregion


        #region Check

        internal override void PerformCheck()
        {
            BeamStationCheckerResults[] beamStationCheckerResults = new BeamStationCheckerResults[Stations.Count()];

            for (int i = 0; i < Stations.Count(); i++)
            {
                BeamStationCheckerResults stationResult = new BeamStationCheckerResults(Section[i], LoadCase, ResultBeamForces[i], Stations[i]);

                double latTorsRd = 0.0;
                double latTorsWR = 0.0;

                SectionClass axialCompSectionClass = CalculateSectionClassDueToCompression(ResultBeamForces[i], Section[i]);
                SectionClass bendingCompSectionClass = CalculateSectionClassDueToBending(ResultBeamForces[i], Section[i]);

                double axialTensionRd = CalculateAxialTensionCapacity(Section[i]);
                double axialCompressionRd = CalculateAxialCompression(Section[i]);
                double axialTensionWR = GetWorkingRatio(Math.Max(ResultBeamForces[i].N, 0), axialTensionRd);
                double axialCompressioneWR = GetWorkingRatio(Math.Min(ResultBeamForces[i].N, 0), axialCompressionRd);

                double axialBuck1Rd = CalculateAxialBucklingCapacity1Axis(axialCompSectionClass, Section[i]);
                double axialBuck2Rd = CalculateAxialBucklingCapacity2Axis(axialCompSectionClass, Section[i]);
                double axialBuck1WR = GetWorkingRatio(ResultBeamForces[i].N, axialBuck1Rd);
                double axialBuck2WR = GetWorkingRatio(ResultBeamForces[i].N, axialBuck2Rd);

                double shear1Rd = CalculateShearXCapacity(ResultBeamForces[i], Section[i]);
                double shear2Rd = CalculateShearYCapacity(ResultBeamForces[i], Section[i]);
                double shear1WR = GetWorkingRatio(ResultBeamForces[i].V1, shear1Rd);
                double shear2WR = GetWorkingRatio(ResultBeamForces[i].V2, shear2Rd);

                double bending1Rd = CalculateBendingMoment1Capacity(ResultBeamForces[i], bendingCompSectionClass, Section[i]);
                double bending2Rd = CalculateBendingMoment2Capacity(ResultBeamForces[i], bendingCompSectionClass, Section[i]);
                double bending1WR = GetWorkingRatio(ResultBeamForces[i].M1, bending1Rd);
                double bending2WR = GetWorkingRatio(ResultBeamForces[i].M2, bending2Rd);

                if (IsNecessaryTheLatTorsBucklingCheck(bendingCompSectionClass, Section[i]))
                {
                    latTorsRd = CalculateLateralTorsionalBucklingMomentCapacity(bendingCompSectionClass, Section[i]);
                    latTorsWR = GetWorkingRatio(CalculateMLTForLatTorsBuckling() * Math.Max(ResultBeamForces[i].M1, ResultBeamForces[i].M2), latTorsRd);                    
                }

                double interactionWR;

                if (MinSigma(Section[i], ResultBeamForces[i].N, ResultBeamForces[i].M2, ResultBeamForces[i].M1) < 0.0)      // compressione
                {
                    SectionClass sectionClass = SetWorstClass(axialCompSectionClass, bendingCompSectionClass);
                    if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2 || sectionClass == SectionClass.Class3)
                    {
                        double result = Math.Abs(ResultBeamForces[i].N) / Math.Min(axialBuck1Rd, axialBuck2Rd) + (ResultBeamForces[i].M1 / bending1Rd) + (ResultBeamForces[i].M2 / bending1Rd);
                        if (result < 0.01)
                            interactionWR = 0.01;
                        else
                            interactionWR = result;
                    }
                    else
                        interactionWR = 0.01;     //TODO: implementare CalculateInteractionWR per classe 4
                }
                else
                {
                    double result = (ResultBeamForces[i].N / axialTensionRd) + (ResultBeamForces[i].M1 / bending1Rd) + (ResultBeamForces[i].M2 / bending2Rd);
                    if (result < 0.01)
                        interactionWR = 0.01;
                    else
                        interactionWR = result;
                }

                stationResult.SetCapacity(axialTensionRd, axialCompressionRd, axialBuck1Rd, axialBuck2Rd, shear1Rd, shear2Rd, bending1Rd, bending2Rd, latTorsRd);
                stationResult.SetWorkingRatio(axialTensionWR, axialCompressioneWR, axialBuck1WR, axialBuck2WR, shear1WR, shear2WR, bending1WR, bending2WR, latTorsWR, interactionWR);
                stationResult.SetClasses(axialCompSectionClass, bendingCompSectionClass);

                beamStationCheckerResults[i] = stationResult;
            }

            _beamStationCheckerResults = beamStationCheckerResults;
        }                      

        private double GetWorkingRatio(double force, double capacity)
        {
            double result = Math.Abs(force / capacity);
            if (Math.Abs(capacity) < 0.01)
                throw new ArgumentException("Capacity can not be null");
            if (Math.Abs(force) < 0.01)
                return 0.01;
            if (result < 0.01)
                return 0.01;
            return result;
        }

        #endregion


        #region Section Private Method


        #region Axial Tension

        private double CalculateAxialTensionCapacity(ISteelSection section)
        {
            return Py * GetEffettiveArea(section);
        }

        #endregion


        #region Axial Compression

        private double CalculateAxialCompression(ISteelSection section)
        {
            return Py * GetEffettiveArea(section);
        }

        #endregion


        #region Axial Buckling

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        /// <returns></returns>
        private double CalculateAxialBucklingCapacity1Axis(SectionClass sectionClass, ISteelSection section)
        {
            var a = CalculatePCompressionXAxis(section);
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2 || sectionClass == SectionClass.Class3)            
                return section.Area * CalculatePCompressionXAxis(section);
            
            else
                return GetEffettiveArea(section) * CalculatePCompressionReducedXAxis(section);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        /// <returns></returns>
        private double CalculateAxialBucklingCapacity2Axis(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2 || sectionClass == SectionClass.Class3)           
                return section.Area * CalculatePCompressionYAxis(section);
            
            else
                return GetEffettiveArea(section) * CalculatePCompressionReducesYAxis(section);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        private double CalculatePCompressionReducedXAxis(ISteelSection section)
        {
            return CalculatePCompressionXAxis(section) * BeamOptions.GetLenghtAxialBuckling1() / ((Section)section).InertiaRadiusX *
                Math.Sqrt(GetEffettiveArea(section) / section.Area);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        private double CalculatePCompressionReducesYAxis(ISteelSection section)
        {
            return CalculatePCompressionYAxis(section) * BeamOptions.GetLenghtAxialBuckling2() / ((Section)section).InertiaRadiusY *
                Math.Sqrt(GetEffettiveArea(section) / section.Area);
        }

        private double CalculatePCompressionXAxis(ISteelSection section)
        {
            double pe = CalculatePeforAxialBucklingXAxis(section);
            double phi = CalculatePhiforAxialBucklingXAxis(section);
            double py = Py;
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Welded)
                py -= 20;
            return (pe * py) / (phi + Math.Pow(Math.Pow(phi, 2) - pe * py , 0.5));
        }

        private double CalculatePCompressionYAxis(ISteelSection section)
        {
            double pe = CalculatePeforAxialBucklingYAxis(section);
            double phi = CalculatePhiforAxialBucklingYAxis(section);
            double py = Py;
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Welded)
                py -= 20;
            //double a = (pe * Py) / (phi + Math.Pow(Math.Pow(phi, 2) - pe * Py, 0.5));
            return (pe * py) / (phi + Math.Pow(Math.Pow(phi, 2) - pe * py, 0.5));
        }

        /// <summary>
        /// CopSuos2011 Appendix 8.4
        /// </summary>
        private double CalculatePhiforAxialBucklingXAxis(ISteelSection section)
        {
            double lambda0 = 0.2 * Math.Pow(Math.Pow(Math.PI, 2) * ((Section)section).GetE() / Py, 0.5);
            double lambdaXAxis = BeamOptions.GetLenghtAxialBuckling1() / ((Section)section).InertiaRadiusX;
            double nForAxialBuckXAxis = Math.Max(GetAlphaBucklingCurveXXAxis(section) * (lambdaXAxis - lambda0) / 1000, 0);
            double py = Py;
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Welded)
                py -= 20;
            return (py + (nForAxialBuckXAxis + 1) * CalculatePeforAxialBucklingXAxis(section)) / 2;
        }

        /// <summary>
        /// CopSuos2011 Appendix 8.4
        /// </summary>
        private double CalculatePhiforAxialBucklingYAxis(ISteelSection section)
        {
            double lambda0 = 0.2 * Math.Pow(Math.Pow(Math.PI, 2) * ((Section)section).GetE() / Py, 0.5); 
            double lambdaYAxis = BeamOptions.GetLenghtAxialBuckling2() / ((Section)section).InertiaRadiusY;
            double nForAxialBuck = Math.Max(GetAlphaBucklingCurveYYAxis(section) * (lambdaYAxis - lambda0) / 1000, 0);
            double py = Py;
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Welded)
                py -= 20;
            return (py + (nForAxialBuck + 1) * CalculatePeforAxialBucklingYAxis(section)) / 2;
        }

        private double CalculatePeforAxialBucklingXAxis(ISteelSection section)
        {
            double lambdaXAxis = BeamOptions.GetLenghtAxialBuckling1() / ((Section)section).InertiaRadiusX;
            return (Math.Pow(Math.PI, 2) * Material.E) / (Math.Pow(lambdaXAxis, 2));
        }

        private double CalculatePeforAxialBucklingYAxis(ISteelSection section)
        {
            double lambdaYAxis = BeamOptions.GetLenghtAxialBuckling2() / ((Section)section).InertiaRadiusY;
            return (Math.Pow(Math.PI, 2) * Material.E) / (Math.Pow(lambdaYAxis, 2));
        }

        /// <summary>
        /// CopSuos2011 Appendix 8.4
        /// </summary>
        private double GetAlphaBucklingCurveXXAxis(ISteelSection section)
        {
            Cop2011Checker.Cop2011Options.BuckingCurves buckingCurve = GetBucklingCurveXXAxis(section);

            if (buckingCurve == Cop2011Checker.Cop2011Options.BuckingCurves.a0)
                return 1.8;     
            else if (buckingCurve == Cop2011Checker.Cop2011Options.BuckingCurves.a)
                return 2.0;
            else if (buckingCurve == Cop2011Checker.Cop2011Options.BuckingCurves.b)
                return 3.5;
            else if (buckingCurve == Cop2011Checker.Cop2011Options.BuckingCurves.c)
                return 5.5;
            else if (buckingCurve == Cop2011Checker.Cop2011Options.BuckingCurves.d)
                return 8.0;
            else
                throw new NotImplementedException("GetAlphaBucklingCurve: not implemented BuckingCurve");
        }

        /// <summary>
        /// CopSuos2011 Appendix 8.4
        /// </summary>
        private double GetAlphaBucklingCurveYYAxis(ISteelSection section)
        {
            Cop2011Checker.Cop2011Options.BuckingCurves buckingCurve = GetBucklingCurveYYAxis(section);

            if (buckingCurve == Cop2011Checker.Cop2011Options.BuckingCurves.a0)
                return 1.8;    
            else if (buckingCurve == Cop2011Checker.Cop2011Options.BuckingCurves.a)
                return 2.0;
            else if (buckingCurve == Cop2011Checker.Cop2011Options.BuckingCurves.b)
                return 3.5;
            else if (buckingCurve == Cop2011Checker.Cop2011Options.BuckingCurves.c)
                return 5.5;
            else if (buckingCurve == Cop2011Checker.Cop2011Options.BuckingCurves.d)
                return 8.0;
            else
                throw new NotImplementedException("GetAlphaBucklingCurve: not implemented BuckingCurve");
        }

        /// <summary>
        /// CopSuos2011 Table 8.7 - Buckling curve
        /// </summary>
        private Cop2011Checker.Cop2011Options.BuckingCurves GetBucklingCurveYYAxis(ISteelSection section)
        {
            if (section is SectionCHS sectionCHS)
            {
                if (sectionCHS.FormedType == Model.Sections.Section.FormedTypes.HotFinished)
                {
                    if (Material.Fyk >= 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                {
                    if (sectionCHS.IsDoubleSymmetric)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a0;
                    else
                        return Cop2011Checker.Cop2011Options.BuckingCurves.c;
                }
            }
            else if (section is SectionRHS sectionRHS)
            {
                if (sectionRHS.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionRHS.ThicknessBottom < 40 && sectionRHS.ThicknessTop < 40 && sectionRHS.ThicknessWebLeft < 40 && sectionRHS.ThicknessWebRight < 40)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.b;
                    else
                        return Cop2011Checker.Cop2011Options.BuckingCurves.c;
                }
                else if (sectionRHS.FormedType == Model.Sections.Section.FormedTypes.HotFinished)
                {
                    if (Material.Fyk >= 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                    return Cop2011Checker.Cop2011Options.BuckingCurves.c;
            }
            else if (section is SectionH sectionH)
            {
                if (sectionH.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if ((2 * sectionH.Height) / (sectionH.LenghtTopFlange + sectionH.LenghtBottomFlange) > 1.2)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return Cop2011Checker.Cop2011Options.BuckingCurves.a;
                        else
                            return Cop2011Checker.Cop2011Options.BuckingCurves.b;
                    }
                    else // (sectionH.IsHSection)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return Cop2011Checker.Cop2011Options.BuckingCurves.b;
                        else
                            return Cop2011Checker.Cop2011Options.BuckingCurves.c;
                    }
                }
                else //(sectionH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.b;
                    else
                        return Cop2011Checker.Cop2011Options.BuckingCurves.b;
                }
            }
            else if (section is SectionC _ || section is SectionL _ || section is SectionT _)
                return Cop2011Checker.Cop2011Options.BuckingCurves.c;
            else
                throw new NotImplementedException("GetBucklingCurve: not implemented section");
        }

        /// <summary>
        /// CopSuos2011 Table 8.7 - Buckling curve
        /// </summary>
        private Cop2011Checker.Cop2011Options.BuckingCurves GetBucklingCurveXXAxis(ISteelSection section)
        {
            if (section is SectionCHS sectionCHS)
            {
                if (sectionCHS.FormedType == Model.Sections.Section.FormedTypes.HotFinished)
                {
                    if (Material.Fyk >= 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                {
                    if (sectionCHS.IsDoubleSymmetric)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a0;
                    else
                        return Cop2011Checker.Cop2011Options.BuckingCurves.c;
                }
            }
            else if (section is SectionRHS sectionRHS)
            {
                if (sectionRHS.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionRHS.ThicknessBottom < 40 && sectionRHS.ThicknessTop < 40 && sectionRHS.ThicknessWebLeft < 40 && sectionRHS.ThicknessWebRight < 40)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.b;
                    else
                        return Cop2011Checker.Cop2011Options.BuckingCurves.c;
                }
                else if (sectionRHS.FormedType == Model.Sections.Section.FormedTypes.HotFinished)
                {
                    if (Material.Fyk >= 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                    return Cop2011Checker.Cop2011Options.BuckingCurves.c;
            }
            else if (section is SectionH sectionH)
            {
                if (sectionH.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if ((2 * sectionH.Height) / (sectionH.LenghtTopFlange + sectionH.LenghtBottomFlange) > 1.2)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return Cop2011Checker.Cop2011Options.BuckingCurves.b;
                        else
                            return Cop2011Checker.Cop2011Options.BuckingCurves.c;
                    }
                    else // (sectionH.IsHSection)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return Cop2011Checker.Cop2011Options.BuckingCurves.c;
                        else
                            return Cop2011Checker.Cop2011Options.BuckingCurves.d;
                    }
                }
                else //(sectionH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.c;
                    else
                        return Cop2011Checker.Cop2011Options.BuckingCurves.d;
                }
            }
            else if (section is SectionC _ || section is SectionL _ || section is SectionT _)
                return Cop2011Checker.Cop2011Options.BuckingCurves.c;
            else
                throw new NotImplementedException("GetBucklingCurve: not implemented section");
        }

        #endregion


        #region Shear Capacity

        /// <summary>
        /// CopSuos 2011 chapter 8.2.1
        /// </summary>
        /// <returns></returns>
        private double CalculateShearYCapacity(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (section is SectionH sectionH)
                if (sectionH.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if (sectionH.HeightWeb / sectionH.ThicknessWeb > 70.0 * Epsilon)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * GetShearBucklingReduction(section) * GetShearAreaYaxis(section);
                }
                else
                {
                    if (sectionH.HeightWeb / sectionH.ThicknessWeb > 62.0 * Epsilon)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * GetShearBucklingReduction(section) * GetShearAreaYaxis(section);
                }
            if (section is SectionRHS sectionRHS)
                if (sectionRHS.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if (sectionRHS.ThicknessWebLeft / sectionRHS.Heightinternal > 70.0 * Epsilon ||
                        sectionRHS.ThicknessWebRight / sectionRHS.Heightinternal > 70.0 * Epsilon)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * GetShearBucklingReduction(section) * GetShearAreaYaxis(section);
                }
                else
                {
                    if (sectionRHS.ThicknessWebLeft / sectionRHS.Heightinternal > 62.0 * Epsilon ||
                        sectionRHS.ThicknessWebRight / sectionRHS.Heightinternal > 62.0 * Epsilon)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * GetShearBucklingReduction(section) * GetShearAreaYaxis(section);
                }
            if (section is SectionC sectionC)
                if (sectionC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if (sectionC.HeightWeb / sectionC.ThicknessWeb > 70.0 * Epsilon)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * GetShearBucklingReduction(section) * GetShearAreaYaxis(section);
                }
                else
                {
                    if (sectionC.HeightWeb / sectionC.ThicknessWeb > 62.0 * Epsilon)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * GetShearBucklingReduction(section) * GetShearAreaYaxis(section);
                }
            return CalculateShearReductionDueToTorsion(resultBeamForces, section) * Py * GetShearAreaYaxis(section) / Math.Sqrt(3);
        }

        /// <summary>
        /// CopSuos 2011 chapter 8.2.1 Calculate Av parameter
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        private double GetShearAreaYaxis(ISteelSection section)
        {
            if (section is SectionH sech && sech.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return sech.ThicknessWeb * sech.Height;
            if (section is SectionH secH && secH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                return secH.ThicknessWeb * secH.HeightWeb;
            if (section is SectionC secC && secC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return secC.ThicknessWeb * secC.Height;
            if (section is SectionRHS sectionRHS)
                return (sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight) * sectionRHS.Heightinternal;
            if (section is SectionCHS sectionCHS)
                return 0.6 * sectionCHS.Area;
            if (section is SectionT sectionT)
                return sectionT.ThicknessWeb * (sectionT.Height - sectionT.ThicknessFlange);
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        private double CalculateShearXCapacity(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            return CalculateShearReductionDueToTorsion(resultBeamForces, section) * Py * GetShearAreaXaxis(section) / Math.Sqrt(3);
        }

        /// <summary>
        /// CopSuos 2011 chapter 8.2.1 Calculate Av parameter
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        private double GetShearAreaXaxis(ISteelSection section)
        {
            if (section is SectionH secH)
                return secH.ThicknessBottomFlange * secH.LenghtBottomFlange + secH.ThicknessTopFlange * secH.LenghtTopFlange;
            if (section is SectionC secC && secC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return secC.LengthTop * secC.ThicknessTop + secC.LengthBottom * secC.ThicknessBottom;
            if (section is SectionRHS sectionRHS)
                return (sectionRHS.ThicknessBottom + sectionRHS.ThicknessTop)  * sectionRHS.BaseInternal;
            if (section is SectionCHS sectionCHS)
                return 0.6 * sectionCHS.Area;
            if (section is SectionT sectionT)
                return sectionT.LenghtFlange * sectionT.ThicknessFlange;
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        /// <summary>
        /// ReductPy for shear check in case of torsion
        /// </summary>
        private double CalculateShearReductionDueToTorsion(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (resultBeamForces.T > 1)
            {
                if (section is SectionH _)
                {
                    double sigmaStVenant = resultBeamForces.T / section.Jt;
                    return Math.Sqrt(1 - (sigmaStVenant / (1.25 * (Py / Math.Sqrt(3)))));
                }
                else if (section is SectionC _)
                {
                    double sigmaStVenant = resultBeamForces.T / section.Jt;
                    double sigmaWarp = resultBeamForces.T / section.Jw;
                    return Math.Sqrt(1 - (sigmaStVenant / (1.25 * (Py / Math.Sqrt(3))))) - 1 - (sigmaWarp / (1.25 * (Py / Math.Sqrt(3))));
                }
                else if (section is SectionCHS sectionCHS)
                {
                    double sigmaStVenant = resultBeamForces.T / 2 / sectionCHS.Area / sectionCHS.Thickness;
                    return 1 - (sigmaStVenant / (1.25 * (Py / Math.Sqrt(3))));
                }
                else if (section is SectionRHS sectionRHS)
                {
                    double sigmaStVenant = resultBeamForces.T / (2 * sectionRHS.Area *
                        (sectionRHS.ThicknessBottom + sectionRHS.ThicknessTop + sectionRHS.ThicknessWebRight + sectionRHS.ThicknessWebLeft) / 4);
                    return 1 - (sigmaStVenant / (1.25 * (Py / Math.Sqrt(3))));
                }
                else
                    throw new NotImplementedException("Not implemented section for Torsional moment");
            }
            return 1.0;
        }

        private double GetShearBucklingReduction(ISteelSection section)
        {
            if (section is SectionH sectionH)
            {
                double pv = 0.6 * Py;
                double qe = Math.Pow(1000 / (sectionH.HeightWeb / sectionH.ThicknessWeb), 2);
                double lambdaW = Math.Sqrt(pv / qe);

                if(sectionH.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if(lambdaW <=0.9)                    
                        return pv;                    
                    else                    
                        return 0.9 * pv / lambdaW;                    
                }
                else            //Welded
                {
                    if (lambdaW <= 0.8)
                        return pv;
                    else if (lambdaW > 0.8 && lambdaW < 1.25)
                        return (13.48 - 5.6 * lambdaW) / 9 * pv;
                    else
                        return 0.9 * pv / lambdaW;
                }
            }
            else
                return Py;
        }

        #endregion


        #region Bending Moment Capacity

        private double CalculateBendingMoment1Capacity(ResultBeamForces resultBeamForces, SectionClass sectionClass, ISteelSection section)
        {
            if ((resultBeamForces.V2 < 0.6 * CalculateShearYCapacity(resultBeamForces, section) && resultBeamForces.T < 1) ||
                (resultBeamForces.V2 < 0.5 * CalculateShearYCapacity(resultBeamForces, section) && resultBeamForces.T > 1))   // low shear condition
            {
                if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                    return Math.Min(Py * ((Section)section).Wpl1, 1.2 * Py * ((Section)section).Wel1);
                else if (sectionClass == SectionClass.Class3)
                    return Py * ((Section)section).Wel1;
                else        // SectionClass.Class4
                    return Py * CalculateEffettiveElasticModulus();       // TODO: implementare Wel effettivo (vedi 8.2.2)
            }
            else // high shear condition
            {
                double rhoMomentShearInteraction = Math.Pow((2 * resultBeamForces.V2 / CalculateShearYCapacity(resultBeamForces, section)) - 1, 2);
                if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                    return Math.Min(Py * ( ((Section)section).Wpl1 - rhoMomentShearInteraction * CalculatePlasticModulusShearXAxis(section)), 
                                    1.2 * Py * (((Section)section).Wel1 - rhoMomentShearInteraction * CalculatePlasticModulusShearXAxis(section) / 1.5));
                else if (sectionClass == SectionClass.Class3)
                    return Py * (((Section)section).Wel1 - rhoMomentShearInteraction * CalculatePlasticModulusShearXAxis(section) / 1.5);
                else        // SectionClass.Class4
                    return Py * (CalculateEffettiveElasticModulus() - rhoMomentShearInteraction * CalculatePlasticModulusShearXAxis(section) / 1.5);       
            }
        }

        /// <summary>
        /// Return the plastic modulus of shear areain high shear condition chapter 8.2.2.2
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        private double CalculatePlasticModulusShearXAxis(ISteelSection section)
        {
            if (section is SectionH sech && sech.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return (1/4) * sech.Height * Math.Pow(sech.ThicknessWeb, 2);
            if (section is SectionH secH && secH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                return (1 / 4) * secH.HeightWeb * Math.Pow(secH.ThicknessWeb, 2);
            if (section is SectionC secC && secC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return (1 / 4) * secC.ThicknessWeb * Math.Pow(secC.Height, 2);
            if (section is SectionRHS sectionRHS)
                return  2 *(1/4) * ((sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight) / 2) * Math.Pow(sectionRHS.BaseInternal,2);
            if (section is SectionCHS sectionCHS)
                return 0.6 * (1 / 6) * Math.Pow(sectionCHS.Diameter, 3) * Math.Pow(sectionCHS.DiameterInternal, 3);
            if (section is SectionT sectionT)
                return (1/4) * sectionT.ThicknessWeb * Math.Pow((sectionT.Height - sectionT.ThicknessFlange),3);
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        private double CalculateEffettiveElasticModulus()
        {
            return 1.0;     //TODO: implementare CalculateEffettiveElasticModulus
        }

        private double CalculateBendingMoment2Capacity(ResultBeamForces resultBeamForces, SectionClass sectionClass, ISteelSection section)
        {
            if ((resultBeamForces.V1 < 0.6 * CalculateShearXCapacity(resultBeamForces, section) && resultBeamForces.T < 1) ||
                (resultBeamForces.V1 < 0.5 * CalculateShearXCapacity(resultBeamForces, section) && resultBeamForces.T > 1))   // low shear condition
            {
                if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                    return Math.Min(Py * ((Section)section).Wpl2, 1.2 * Py * ((Section)section).Wel2);
                else if (sectionClass == SectionClass.Class3)
                    return Py * ((Section)section).Wel2;
                else        // SectionClass.Class4
                    return Py * CalculateEffettiveElasticModulus();       // TODO: implementare Wel effettivo (vedi 8.2.2)
            }
            else // high shear condition
            {
                double rhoMomentShearInteraction = Math.Pow((2 * resultBeamForces.V2 / CalculateShearXCapacity(resultBeamForces, section)) - 1, 2);
                if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                    return Math.Min(Py * (((Section)section).Wpl2 - rhoMomentShearInteraction * CalculatePlasticModulusShearYAxis(section)),
                                    1.2 * Py * (((Section)section).Wel2 - rhoMomentShearInteraction * CalculatePlasticModulusShearYAxis(section) / 1.5));
                else if (sectionClass == SectionClass.Class3)
                    return Py * (((Section)section).Wel2 - rhoMomentShearInteraction * CalculatePlasticModulusShearYAxis(section) / 1.5);
                else        // SectionClass.Class4
                    return Py * (CalculateEffettiveElasticModulus() - rhoMomentShearInteraction * CalculatePlasticModulusShearYAxis(section) / 1.5);
            }
        }

        /// <summary>
        /// Return the plastic modulus of shear areain high shear condition chapter 8.2.2.2
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        private double CalculatePlasticModulusShearYAxis(ISteelSection section)
        {
            if (section is SectionH sech)
                return (1 / 4) * sech.LenghtTopFlange * Math.Pow(sech.ThicknessTopFlange, 2) + 
                    (1 / 4) * sech.LenghtBottomFlange * Math.Pow(sech.ThicknessBottomFlange, 2);
            if (section is SectionC secC)
                return (1 / 4) * secC.LengthTop * Math.Pow(secC.ThicknessTop, 2) + (1 / 4) * secC.LengthBottom * Math.Pow(secC.ThicknessBottom, 2);
            if (section is SectionRHS sectionRHS)
                return 2 * (1 / 4) * ((sectionRHS.ThicknessBottom + sectionRHS.ThicknessTop) / 2) * Math.Pow(sectionRHS.BaseInternal, 2);
            if (section is SectionCHS sectionCHS)
                return 0.6 * (1 / 6) * Math.Pow(sectionCHS.Diameter, 3) * Math.Pow(sectionCHS.DiameterInternal, 3);
            if (section is SectionT sectionT)
                return (1 / 4) * sectionT.LenghtFlange * Math.Pow(sectionT.ThicknessFlange, 2);
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        #endregion


        #region Lateral Torsional Bucking Capacity

        //TODO: calcolare Mcx chapter 8.3.5.2

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.2
        /// </summary>
        private double CalculateLateralTorsionalBucklingMomentCapacity(SectionClass sectionClass, ISteelSection section)
        {
            // Calculate the pb CopSuos2011 Chapter 8.3.5.2 with the BS5950 B.2.2 method
            double pb = Math.Min((CalculatePeForLatTorsBuckling(section, sectionClass) * Py) / (CalculatePhiLTForLatTorsBuckling(section, sectionClass) +
                Math.Pow(Math.Pow(CalculatePhiLTForLatTorsBuckling(section, sectionClass), 2) - CalculatePeForLatTorsBuckling(section, sectionClass) * Py, 0.5)), Py);

            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                return pb * ((Section)section).Wpl1;            
            else if (sectionClass == SectionClass.Class3)           
                return pb * ((Section)section).Wel1;            
            else
                throw new NotImplementedException("CalculateBucklingMomentCapacity: not implemented Section Class 4");
        }

        /// <summary>
        /// BS5950 B.2.2
        /// </summary>
        private double CalculatePeForLatTorsBuckling(ISteelSection section, SectionClass sectionClass)
        {
            return (Math.Pow(Math.PI, 2) * Material.E) / (Math.Pow(CalculateLambdaLTForLatTorsBuckling(section, sectionClass), 2));
        }

        /// <summary>
        /// BS5950 B.2.2
        /// </summary>
        private double CalculatePhiLTForLatTorsBuckling(ISteelSection section, SectionClass sectionClass)
        {
            double nlt;         //Perry factor BS5950 B.2.2
            double alphaLT = 7.0;         // SAP prende come valore 3 => la norma dice 7
            double lambdaLT = CalculateLambdaLTForLatTorsBuckling(section, sectionClass);
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Rolled)
                nlt = Math.Max(alphaLT * (lambdaLT - CalculateLambdaL0ForLatTorsBucklingBS5950()) / 1000, 0);
            else        //Welded
            {
                if (lambdaLT <= CalculateLambdaL0ForLatTorsBucklingBS5950())
                    nlt = 0.0;
                else if (CalculateLambdaL0ForLatTorsBucklingBS5950() <= lambdaLT && (lambdaLT <= 2 * CalculateLambdaL0ForLatTorsBucklingBS5950()))
                    nlt = 2 * alphaLT * (lambdaLT - CalculateLambdaL0ForLatTorsBucklingBS5950()) / 1000;
                else if (2 * CalculateLambdaL0ForLatTorsBucklingBS5950() <= lambdaLT && (lambdaLT <= 3 * CalculateLambdaL0ForLatTorsBucklingBS5950()))
                    nlt = 2 * alphaLT * CalculateLambdaL0ForLatTorsBucklingBS5950() / 1000;
                else
                    nlt = alphaLT * (lambdaLT - CalculateLambdaL0ForLatTorsBucklingBS5950()) / 1000;
            }

            return (Py + (nlt + 1) * CalculatePeForLatTorsBuckling(section, sectionClass)) / 2;
        }

        /// <summary>
        /// Return limiting equivalent slenderness BS5950 B2.2
        /// </summary>
        private double CalculateLambdaL0ForLatTorsBucklingBS5950()
        {
            return 0.4 * Math.Pow(Math.Pow(Math.PI, 2) * Material.E / Py, 0.5);
        }

        /// <summary>
        /// Return true if the CopSuos2011 Chapter 8.3.5 say to check the lateral-torsional buckling. 
        /// </summary>
        private bool IsNecessaryTheLatTorsBucklingCheck(SectionClass sectionClass, ISteelSection section)
        {
            if (section is SectionCHS || section is SectionCircular)
                return false;

            else if (section is SectionRHS sectionRHS)
            {
                double lambda = CalculateLambdaForLatTorsBuckling(section);

                if (sectionRHS.Height / sectionRHS.Base < 1.25 || sectionRHS.Base / sectionRHS.Height < 1.25)
                    if (lambda < 770*Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.33 || sectionRHS.Base / sectionRHS.Height < 1.33)
                    if (lambda < 670*Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.40 || sectionRHS.Base / sectionRHS.Height < 1.40)
                    if (lambda < 580*Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.44 || sectionRHS.Base / sectionRHS.Height < 1.44)
                    if (lambda < 550*Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.50 || sectionRHS.Base / sectionRHS.Height < 1.50)
                    if (lambda < 515*Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.67 || sectionRHS.Base / sectionRHS.Height < 1.67)
                    if (lambda < 435 * Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.75 || sectionRHS.Base / sectionRHS.Height < 1.75)
                    if (lambda < 410 * Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.80 || sectionRHS.Base / sectionRHS.Height < 1.80)
                    if (lambda < 395 * Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 2.00 || sectionRHS.Base / sectionRHS.Height < 2.0)
                    if (lambda < 340 * Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 2.50 || sectionRHS.Base / sectionRHS.Height < 2.5)
                    if (lambda < 275 * Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 3.00 || sectionRHS.Base / sectionRHS.Height < 3.0)
                    if (lambda < 225 * Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 4.00 || sectionRHS.Base / sectionRHS.Height < 4.0)
                    if (lambda < 170 * Epsilon)
                        return false;
            }
            else if (section is SectionH || section is SectionC )
            {
                double lambdaLT = CalculateLambdaLTForLatTorsBuckling(section, sectionClass);

                if (Py <= 235)
                    if (lambdaLT < 37.1)
                        return false;
                if (Py <= 245)
                    if (lambdaLT < 36.3)
                        return false;
                if (Py <= 255)
                    if (lambdaLT < 35.6)
                        return false;
                if (Py <= 265)
                    if (lambdaLT < 35.0)
                        return false;
                if (Py <= 275)
                    if (lambdaLT < 34.3)
                        return false;
                if (Py <= 315)
                    if (lambdaLT < 32.1)
                        return false;
                if (Py <= 325)
                    if (lambdaLT < 31.6)
                        return false;
                if (Py <= 335)
                    if (lambdaLT < 31.1)
                        return false;
                if (Py <= 345)
                    if (lambdaLT < 30.6)
                        return false;
                if (Py <= 355)
                    if (lambdaLT < 30.2)
                        return false;
                if (Py <= 400)
                    if (lambdaLT < 28.4)
                        return false;
                if (Py <= 410)
                    if (lambdaLT < 28.1)
                        return false;
                if (Py <= 430)
                    if (lambdaLT < 27.4)
                        return false;
                if (Py <= 440)
                    if (lambdaLT < 27.1)
                        return false;
                if (Py <= 460)
                    if (lambdaLT < 26.5)
                        return false;
            }
            return true;
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.3 table 8.4b
        /// </summary>
        /// <returns></returns>
        private double CalculateMLTForLatTorsBuckling()
        {
            //TODO: implementare CalculateMLTForLatTorsBuckling con le stazioni
            // mLt = Math.Max( (0.2+(0.15 * M1 + 0.5 * M2 + 0.15 * M4)) / Mmax, 0.44)
            return 1.0; // a favore di sicurezza si prende il massimo possibile
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.3 equation 8.25
        /// </summary>
        /// <returns></returns>
        private double CalculateLambdaLTForLatTorsBuckling(ISteelSection section, SectionClass sectionClass)
        {
            // CopSuos2011 Chapter 8.3.5.3 beta w parameter
            double bw;
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                bw = 1.0;
            else if (sectionClass == SectionClass.Class3)
                bw = ((Section)section).Wel1 / ((Section)section).Wpl1;
            else        //class4
                bw = CalculateEffettiveElasticModulus() / ((Section)section).Wpl1;

            // CopSuos2011 Chapter 8.3.5.3 u parameter
            double u;
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Rolled)
                u = 0.9;
            else if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Welded)
                u = 1.0;
            else
                throw new NotImplementedException("CalculateUForLatTorsBuckling: not implemented SectionType");

            double v;
            //CopSuos2011 Chapter 8.3.5.3 x parameter
            double x;
            if (section is SectionH sec)
                x = sec.Height / sec.ThicknessWeb;
            else if (section is SectionC sectionC)
                x = sectionC.Height / ((sectionC.ThicknessBottom + sectionC.ThicknessTop) / 2);
            else
                throw new NotImplementedException("CalculateXForLatTorsBuckling");

            // CopSuos2011 Chapter 8.3.5.3 equation 8.27
            if (section is SectionH sectionH && sectionH.IsDoubleSymmetric)
                v = 1 / Math.Pow(1 + 0.05 * (Math.Pow(CalculateLambdaForLatTorsBuckling(section) / x, 2)), 0.25);
            else
                throw new NotImplementedException("CopSuos2011 not implemented v coefficient for this section");

            return u * v * CalculateLambdaForLatTorsBuckling(section) * Math.Sqrt(bw);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.3 equation 8.26
        /// </summary>
        /// <returns></returns>
        private double CalculateLambdaForLatTorsBuckling(ISteelSection section)
        {
            // CopSuos2011 Chapter 8.3.4
            double le;
            if (CopSuos2011Options.LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default)
                le = BeamOptions.GetLenghtLatTorsBuckling();
            else if (CopSuos2011Options.LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.FullyRestrained)
                le = 0.8 * BeamOptions.GetLenghtLatTorsBuckling();
            else if (CopSuos2011Options.LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Unrestrained)
                le = 1.2 * BeamOptions.GetLenghtLatTorsBuckling() + 2 * section.Height;
            else if (CopSuos2011Options.LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad)
                le = 1.2 * BeamOptions.GetLenghtLatTorsBuckling();
            else
                throw new NotImplementedException("GetLeForLatTorsBuckling: not implemented LateralTorsionalBucklingConditions");

            return le / Math.Min(((Section)section).InertiaRadiusX, ((Section)section).InertiaRadiusY);
        }

        #endregion


        #region Section Class

        /// <summary>
        /// Return the <see cref="SectionClass"/> due of bending compression of the section <paramref name="section"/> with the <paramref name="resultBeamForces"/> - Chapter 7
        /// </summary>
        /// <returns></returns>
        private SectionClass CalculateSectionClassDueToBending(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (MinSigma(section, resultBeamForces.N, resultBeamForces.M2, resultBeamForces.M1) < 0.0)
            {
                if (section is SectionH sectionH)
                    return SetWorstClass(new SectionClass[] {GetClassCompressedOuterFlangeBending(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, section),
                                                                            GetClassCompressedOuterFlangeBending(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, section),
                                                                            GetClassCompressedWebBendingMoment(sectionH.HeightWeb, sectionH.ThicknessWeb, resultBeamForces, section)});

                else if (section is SectionCHS sectionCHS)
                    return GetClassCHSBending(sectionCHS.Diameter, sectionCHS.Thickness);

                else if (section is SectionRHS sectionRHS)
                {
                    if (Math.Abs(resultBeamForces.M2) >= Math.Abs(resultBeamForces.M1))
                    {
                        //flange are load with constant load     //classification webs                           //from equilibrium of Σ sigma = Ned
                        if (sectionRHS.ThicknessTop == sectionRHS.ThicknessBottom && sectionRHS.ThicknessWebLeft == sectionRHS.ThicknessWebRight)
                            return SetWorstClass(new SectionClass[] { GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, resultBeamForces, section),
                                                                    GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, sectionRHS.Heightinternal, section)});
                        else
                            return SetWorstClass(new SectionClass[] {GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, sectionRHS.Heightinternal, section),
                                                                    GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessTop, sectionRHS.Heightinternal, section),
                                                                    GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, resultBeamForces, section),
                                                                    GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight, resultBeamForces, section) });
                    }

                    else // if (Math.Abs(resultBeamForces.M1) >= Math.Abs(resultBeamForces.M2))
                    {
                        if (sectionRHS.ThicknessWebLeft == sectionRHS.ThicknessWebRight && sectionRHS.ThicknessTop == sectionRHS.ThicknessBottom)
                            return SetWorstClass(new SectionClass[]{ GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, resultBeamForces, section),
                                                                    GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessTop, sectionRHS.Heightinternal, section)});
                        else
                            return SetWorstClass(new SectionClass[] { GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessTop, sectionRHS.Heightinternal, section),
                                                                    GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, sectionRHS.Heightinternal, section),
                                                                    GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, resultBeamForces, section),
                                                                    GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight, resultBeamForces, section) });
                    }
                }

                else if (section is SectionT sectionT)
                    return SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeBending(sectionT.LenghtFlange / 2, sectionT.ThicknessFlange / 2, section),
                                                                        GetClassCompressedStemT(sectionT.Height, sectionT.ThicknessWeb) });

                else if (section is SectionC sectionC)
                    return SetWorstClass(new SectionClass[]{ GetClassCompressedWebChannel(sectionC.HeightWeb / 2, sectionC.ThicknessWeb / 2),
                                                                        GetClassCompressedOuterFlangeBending(sectionC.LengthBottom, sectionC.ThicknessBottom, section),
                                                                        GetClassCompressedOuterFlangeBending(sectionC.LengthTop, sectionC.ThicknessTop, section)});

                else if (section is SectionL sectionL)
                    return SetWorstClass(new SectionClass[]{ GetClassCompressedOutstandLeg(sectionL.LengthHor, sectionL.ThicknessHor),
                                                                        GetClassCompressedOutstandLeg(sectionL.LengthVert, sectionL.ThicknessVert)});

                else
                    throw new NotImplementedException("CalculateSectionClassException: not implemented Section");
            }
            else
                return SectionClass.Class1;
        }

        /// <summary>
        /// Return the <see cref="SectionClass"/> due of bending compression of the section <paramref name="section"/> with the <paramref name="resultBeamForces"/> - Chapter 7
        /// </summary>
        /// <returns></returns>
        private SectionClass CalculateSectionClassDueToCompression(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (MinSigma(section, resultBeamForces.N, resultBeamForces.M2, resultBeamForces.M1) < 0.0)
            {
                if (section is SectionH sectionH)                
                    return SetWorstClass(new SectionClass[]{ GetClassCompressedWebAxialCompression(sectionH.HeightWeb, sectionH.ThicknessWeb, resultBeamForces, section),
                                                                        GetClassCompressedOuterFlangeAxial(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange),
                                                                        GetClassCompressedOuterFlangeAxial(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange) });
                
                else if (section is SectionCHS sectionCHS)                
                    return GetClassCHSAxialCompression(sectionCHS.Diameter, sectionCHS.Thickness);
                
                else if (section is SectionRHS sectionRHS)                
                    return SetWorstClass(new SectionClass[] { GetClassCompressedWebAxialCompression(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, resultBeamForces, section),
                                                                        GetClassCompressedWebAxialCompression(sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight, resultBeamForces, section),
                                                                        GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessTop, sectionRHS.Heightinternal, section),
                                                                        GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, sectionRHS.Heightinternal, section) });
                
                else if (section is SectionT sectionT)                
                    return SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeAxial(sectionT.LenghtFlange / 2, sectionT.ThicknessFlange / 2),
                                                                    GetClassCompressedStemT(sectionT.Height, sectionT.ThicknessWeb) });
                
                else if (section is SectionC sectionC)                
                    return SetWorstClass(new SectionClass[]{ GetClassCompressedWebChannel(sectionC.HeightWeb / 2, sectionC.ThicknessWeb / 2),
                                                                    GetClassCompressedOuterFlangeAxial(sectionC.LengthBottom, sectionC.ThicknessBottom),
                                                                    GetClassCompressedOuterFlangeAxial(sectionC.LengthTop, sectionC.ThicknessTop)});
                
                else if (section is SectionL sectionL)                
                    return SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeAxial(sectionL.LengthHor, sectionL.ThicknessHor),
                                                                    GetClassCompressedOuterFlangeAxial(sectionL.LengthVert, sectionL.ThicknessVert)});
                
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
        private SectionClass GetClassCompressedOuterFlangeBending(double b, double t, ISteelSection section)
        {
            double ctRatio = b / t;
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Rolled)
            {
                if (ctRatio <= 9.0 * Epsilon)
                    return SectionClass.Class1;
                else if (ctRatio <= 10.0 * Epsilon)
                    return SectionClass.Class2;
                else if (ctRatio <= 15.0 * Epsilon)
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Welded)
            {
                if (ctRatio <= 8.0 * Epsilon)
                    return SectionClass.Class1;
                else if (ctRatio <= 9.0 * Epsilon)
                    return SectionClass.Class2;
                else if (ctRatio <= 13.0 * Epsilon)
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
        private SectionClass GetClassCompressedOuterFlangeAxial(double b, double t)
        {
            double ctRatio = b / t;
            if (ctRatio <= 13.0 * Epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, internal element, bending moment
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedInternalFlangeBending(double b, double t)
        {
            double ctRatio = b / t;
            if (ctRatio <= 28.0 * Epsilon)
                return SectionClass.Class1;
            else if (ctRatio <= 32.0 * Epsilon)
                return SectionClass.Class2;
            else if (ctRatio <= 40.0 * Epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, internal element, axial compression
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedInternalFlangeAxial(double b, double t)
        {
            double ctRatio = b / t;
            if (ctRatio <= 40.0 * Epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Web of I, H, Box section. Generally
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebBendingMoment(double b, double t, ResultBeamForces resultBeamForces, ISteelSection section)
        {
            double ctRatio = b / t;
            if (ctRatio <= Math.Max(80.0 * Epsilon / (1 + GetR1(section, resultBeamForces)), 40 * Epsilon))
                return SectionClass.Class1;
            if (GetR1(section, resultBeamForces) <= 0 &&
                (ctRatio <= Math.Max(100 * Epsilon / (1 + GetR1(section, resultBeamForces)), 40 * Epsilon)))
                return SectionClass.Class2;
            if (GetR1(section, resultBeamForces) >= 0 && 
                (ctRatio <= Math.Max( 100 * Epsilon / (1 + 1.5 * GetR1(section, resultBeamForces)), 40 * Epsilon)))
                return SectionClass.Class2;
            else if (ctRatio <= Math.Max(120 * Epsilon / (1 + 2 * GetR2(resultBeamForces, section)), 40 * Epsilon))
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Web of I, H, Box section. Axial Compression
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebAxialCompression(double b, double t, ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (((Section)section).FormedType == Model.Sections.Section.FormedTypes.HotFinished)
            {
                double ctRatio = b / t;
                if (ctRatio <= Math.Max(120 * Epsilon / (1 + 2 * GetR2(resultBeamForces, section)), 40 * Epsilon))
                    return SectionClass.Class3;
                return SectionClass.Class4;
            }
            else
            {
                double ctRatio = b / t;
                if (ctRatio <= Math.Max(105 * Epsilon / (1 + 2 * GetR2(resultBeamForces, section)), 35 * Epsilon))
                    return SectionClass.Class3;
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 CHS Classification
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCHSBending(double D, double t)
        {
            if (D / t <= 40.0 * Epsilon * Epsilon)
                return SectionClass.Class1;
            else if (D / t <= 50.0 * Epsilon * Epsilon)
                return SectionClass.Class2;
            else if (D / t <= 140 * Epsilon * Epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 CHS Classification
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCHSAxialCompression(double D, double t)
        {
            if (D / t <= 80 * Epsilon * Epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Flange
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedFlangeRHS(double b, double t, double d, ISteelSection section)
        {
            double ctRatio = b / t;
            if (((Section)section).FormedType == Model.Sections.Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= Math.Min(28.0 * Epsilon, 80 * Epsilon - d / t))      
                    return SectionClass.Class1;
                else if (ctRatio <= Math.Min(32.0 * Epsilon, 62.0 * Epsilon - 0.5 * d / t))
                    return SectionClass.Class2;
                else if (ctRatio <= 40.0 * Epsilon)
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else if (((Section)section).FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
            {
                if (ctRatio <= Math.Min(26.0 * Epsilon, 72.0 * Epsilon - d / t))
                    return SectionClass.Class1;
                else if (ctRatio <= Math.Min(28.0 * Epsilon, 54 * Epsilon - 0.5 * d / t))
                    return SectionClass.Class2;
                else if (ctRatio <= 35.0 * Epsilon)
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
        private SectionClass GetClassCompressedWebRHS(double d, double t, ResultBeamForces resultBeamForces, ISteelSection section)
        {
            double ctRatio = d / t;
            if (((Section)section).FormedType == Model.Sections.Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= Math.Max(64.0 * Epsilon / (1 + 0.6 * GetR1(section, resultBeamForces)), 40.0 * Epsilon)) 
                    return SectionClass.Class1;
                else if (ctRatio <= Math.Max(80.0 * Epsilon / (1 + GetR1(section, resultBeamForces)), 40.0 * Epsilon))
                    return SectionClass.Class2;
                else if (ctRatio <= Math.Max(120 * Epsilon / (1 + 2 * GetR1(section, resultBeamForces)), 40.0 * Epsilon))
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else if (((Section)section).FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
            {
                if (ctRatio <= Math.Max(56.0 * Epsilon / (1 + 0.6 * GetR1(section, resultBeamForces)),35.0 * Epsilon))
                    return SectionClass.Class1;
                else if (ctRatio <= Math.Max(70.0 * Epsilon / (1 + GetR1(section, resultBeamForces)), 35.0 * Epsilon))
                    return SectionClass.Class2;
                else if (ctRatio <= Math.Max(105 * Epsilon / (1 + 2 * GetR1(section, resultBeamForces)), 35.0 * Epsilon))
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
        private SectionClass GetClassCompressedAngleWithAxialCompression(double b, double d, double t)
        {
            if (b / t < 15.0 * Epsilon && d / t < 15.0 * Epsilon && (b + d) / t < 24.0 * Epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Angle, compression due to bending
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedAngleOnlyBending(double b, double d, double t)
        {
            if (b / t < 9.0 * Epsilon && d / t < 9.0 * Epsilon)
                return SectionClass.Class1;
            if (b / t < 10.0 * Epsilon && d / t < 10.0 * Epsilon)
                return SectionClass.Class2;
            if (b / t < 15.0 * Epsilon && d / t < 15.0 * Epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Outstand Leg of an angle 
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedOutstandLeg(double b, double t)
        {
            if (b / t < 9.0 * Epsilon)
                return SectionClass.Class1;
            if (b / t < 10.0 * Epsilon)
                return SectionClass.Class2;
            if (b / t < 15.0 * Epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Stem of a T section
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedStemT(double d, double t)
        {
            if (d / t < 8.0 * Epsilon)
                return SectionClass.Class1;
            if (d / t < 9.0 * Epsilon)
                return SectionClass.Class2;
            if (d / t < 18.0 * Epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Channel of a C section
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebChannel(double d, double t)
        {
            if (d / t < 40.0 * Epsilon)
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
        private double GetR1(ISteelSection section, ResultBeamForces resultBeamForces)
        {
            if (section is SectionH sectionH)
            {
                if (sectionH.ThicknessBottomFlange == sectionH.ThicknessTopFlange && sectionH.LenghtBottomFlange == sectionH.LenghtTopFlange)
                {
                    double r1 = (-resultBeamForces.N / (sectionH.HeightWeb * sectionH.ThicknessWeb * Py));
                    r1 = r1 > 1 ? 1 : r1;
                    r1 = r1 < -1 ? -1 : r1;
                    return r1;
                }
                else
                {
                    double r1 = (-resultBeamForces.N / (sectionH.HeightWeb * sectionH.ThicknessWeb * Py) +
                        (((sectionH.LenghtBottomFlange * sectionH.LenghtBottomFlange - sectionH.LenghtTopFlange * sectionH.ThicknessTopFlange) * Py) /
                            (sectionH.HeightWeb * sectionH.ThicknessWeb * Py)));
                    r1 = r1 < 1 ? 1 : r1;
                    r1 = r1 > -1 ? -1 : r1;
                    return r1;
                }
            }
            else if (section is SectionRHS sectionRHS)
            {
                double r1 = (-resultBeamForces.N / (2 * sectionRHS.Heightinternal * ( sectionRHS.ThicknessWebRight + sectionRHS.ThicknessWebLeft) / 2 * Py));
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
        private double GetR2(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (section is SectionH sectionH)
            {
                if (sectionH.ThicknessBottomFlange == sectionH.ThicknessTopFlange && sectionH.LenghtBottomFlange == sectionH.LenghtTopFlange)
                    return (-resultBeamForces.N / (section.Area * Py));
                else
                    throw new NotImplementedException("Not supported case");
            }
            else if (section is SectionRHS _)           
                return (-resultBeamForces.N / (section.Area * Py));
            
            else
                throw new NotImplementedException("Not supported Section type");
        }

        private double GetEffettiveArea(ISteelSection section)
        {
            return section.Area;      //TODO: implementare GetEffettiveArea()
        }

        #endregion


        #region py

        /// <summary>
        /// Return the py value 
        /// </summary>
        /// <returns></returns>
        private double GetPy()
        {
            if (Material.Fyk < 460)
                return Math.Min(Material.Fyk / GetGammaM1(), Material.Fu / GetGammaM2());
            else            // (steelMaterial.Fyk >= 460)
                return Math.Min(Material.Fyk / GetGammaM1(), Material.Fu / GetGammaM2());
        }

        /// <summary>
        /// Return the GammaM1 factor for reduce the steel fy. CopSuos 2011 Chapter 4 table 4.1
        /// </summary>
        /// <returns></returns>
        private double GetGammaM1()
        {
            if (Material.Fyk < 460)
            {
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1)
                    return 1.0;
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class2)
                    return 1.2;
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1H)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
            }
            if (Material.Fyk > 460 && Material.Fyk < 690)
            {
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1 || ((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class2
                    || ((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1H)
                    return 1.0;
            }
            throw new NotImplementedException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
        }

        /// <summary>
        /// Return the GammaM2 factor for reduce the steel fy. CopSuos 2011 Chapter 4 table 4.1
        /// </summary>
        /// <returns></returns>
        private double GetGammaM2()
        {
            if (Material.Fyk < 460)
            {
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1)
                    return 1.2;
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class2)
                    return 1.3;
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1H)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
            }
            if (Material.Fyk > 460 && Material.Fyk < 690)
            {
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1 || ((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class2
                    || ((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (((Cop2011Checker.Cop2011Options)Options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1H)
                    return 1.2;
            }
            throw new NotImplementedException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
        }

        #endregion

        #endregion


    }
}
