using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using System.ComponentModel;
using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Results;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;
using GPC.Model.Results;
using GPC.Model.Materials;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Checkers
{
    public class Cop2011Checker : Checker
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

        protected double _py;
        protected double _epsilon;

        #endregion


        #region Properties

        internal SteelMaterial Material => (SteelMaterial)_beamCheckersAttributes[0].Section.Material;

        internal double Py => _py;

        internal double Epsilon => _epsilon;

        #endregion

        public Cop2011Checker(Cop2011BeamCheckerAttribute[] beamCheckers, Cop2011Checker.Cop2011Options options)
            :base(beamCheckers, options)
        {
            _py = GetPy();
            _epsilon = Math.Sqrt(275 / _py);        //value of Epsilon for section classification
        }

        public Cop2011Checker(Cop2011BeamCheckerAttribute[] beamCheckers, Cop2011Checker.Cop2011Options options, StandardCopSuos2011 standard)
            : base(beamCheckers, options, standard)
        {
            _standard = standard;
            _py = GetPy();
            _epsilon = Math.Sqrt(275 / _py);        //value of Epsilon for section classification
        }

        public override void PerformCheck()
        {            
            List<BeamStationCheckerResults> beamStationCheckerResults = new List<BeamStationCheckerResults>();

            for (int i = 0; i < _beamCheckersAttributes.Count(); i++)
            {
                for (int j = 0; j < _beamCheckersAttributes[i].BeamResults.Count(); j++)
                {
                    for (int k = 0; k < _beamCheckersAttributes[i].BeamResults[j].Results.Count(); k++)
                    {
                        try
                        {
                            beamStationCheckerResults.Add(PerformCheck(_beamCheckersAttributes[i].Section, _beamCheckersAttributes[i].BeamResults[j],
                                (ResultBeamForces)_beamCheckersAttributes[i].BeamResults[j].Results[k] , _beamCheckersAttributes[i].Station));
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(ex.Message, ex.InnerException);
                        }
                    }
                }
            }
            _beamStationCheckerResults = beamStationCheckerResults.ToArray();
        }

        private BeamStationCheckerResults PerformCheck(ISteelSection steelSection, BeamResult beamResult, ResultBeamForces resultBeamForces, ResultStation station)
        {
            BeamStationCheckerResults stationResult = new BeamStationCheckerResults(steelSection, beamResult, station);

            double latTorsRd = 0.0;
            double latTorsWR = 0.0;

            SectionClass axialCompSectionClass = CalculateSectionClassDueToCompression(resultBeamForces, steelSection);
            SectionClass bendingCompSectionClass = CalculateSectionClassDueToBending(resultBeamForces, steelSection);

            double axialTensionRd = CalculateAxialTensionCapacity(steelSection);
            double axialCompressionRd = CalculateAxialCompression(steelSection);
            double axialTensionWR = GetWorkingRatio(Math.Max(resultBeamForces.N, 0), axialTensionRd);
            double axialCompressioneWR = GetWorkingRatio(Math.Min(resultBeamForces.N, 0), axialCompressionRd);

            double axialBuck1Rd = CalculateAxialBucklingCapacity1Axis(axialCompSectionClass, steelSection);
            double axialBuck2Rd = CalculateAxialBucklingCapacity2Axis(axialCompSectionClass, steelSection);
            double axialBuck1WR = GetWorkingRatio(resultBeamForces.N, axialBuck1Rd);
            double axialBuck2WR = GetWorkingRatio(resultBeamForces.N, axialBuck2Rd);

            double shear1Rd = CalculateShearXCapacity(resultBeamForces, steelSection);
            double shear2Rd = CalculateShearYCapacity(resultBeamForces, steelSection);
            double shear1WR = GetWorkingRatio(resultBeamForces.V1, shear1Rd);
            double shear2WR = GetWorkingRatio(resultBeamForces.V2, shear2Rd);

            double bending1Rd = CalculateBendingMoment1Capacity(resultBeamForces, bendingCompSectionClass, steelSection);
            double bending2Rd = CalculateBendingMoment2Capacity(resultBeamForces, bendingCompSectionClass, steelSection);
            double bending1WR = GetWorkingRatio(resultBeamForces.M1, bending1Rd);
            double bending2WR = GetWorkingRatio(resultBeamForces.M2, bending2Rd);


            double interaction881WR = 0.01;
            // equazione 8.81 cap. 8.9.2
            if (IsNecessaryTheLatTorsBucklingCheck(bendingCompSectionClass, steelSection))
            {
                latTorsRd = CalculateLateralTorsionalBucklingMomentCapacity(bendingCompSectionClass, steelSection);
                latTorsWR = GetWorkingRatio(CalculateMLTForLatTorsBuckling() * resultBeamForces.M1, latTorsRd);

                // equazione 8.81 cap. 8.9.2
                if (MinSigma(steelSection, resultBeamForces.N, resultBeamForces.M2, resultBeamForces.M1) < 0.0)
                {
                    double result = Math.Abs(resultBeamForces.N) / axialBuck2Rd +
                        (CalculateMLTForLatTorsBuckling() * resultBeamForces.M1 / latTorsRd) +
                        (_options.UniformMomentFactorm2 * resultBeamForces.M2 / CalculateBendingMoment2ElasticCapacity(bendingCompSectionClass, steelSection));
                    if (result < 0.01)
                        interaction881WR = 0.01;
                    else
                        interaction881WR = result;
                }
                else
                    interaction881WR = 0.01;
            }

            // equazione 8.78 cap. 8.9.2
            double interaction878WR;

            if (MinSigma(steelSection, resultBeamForces.N, resultBeamForces.M2, resultBeamForces.M1) < 0.0)      // compressione
            {
                SectionClass sectionClass = SetWorstClass(axialCompSectionClass, bendingCompSectionClass);
                if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2 || sectionClass == SectionClass.Class3)
                {
                    double result = Math.Abs(resultBeamForces.N) / axialCompressionRd +
                        (resultBeamForces.M1 / CalculateBendingMoment1ElasticCapacity(bendingCompSectionClass, steelSection)) +
                        (resultBeamForces.M2 / CalculateBendingMoment2ElasticCapacity(bendingCompSectionClass, steelSection));
                    if (result < 0.01)
                        interaction878WR = 0.01;
                    else
                        interaction878WR = result;
                }
                else
                    interaction878WR = 0.01;     //TODO: implementare CalculateInteractionWR per classe 4
            }
            else
            {
                double result = (resultBeamForces.N / axialTensionRd) +
                        (resultBeamForces.M1 / CalculateBendingMoment1ElasticCapacity(bendingCompSectionClass, steelSection)) +
                        (resultBeamForces.M2 / CalculateBendingMoment2ElasticCapacity(bendingCompSectionClass, steelSection));
                if (result < 0.01)
                    interaction878WR = 0.01;
                else
                    interaction878WR = result;
            }

            // equazione 8.79 cap. 8.9.2
            double interaction879WR;
            if (MinSigma(steelSection, resultBeamForces.N, resultBeamForces.M2, resultBeamForces.M1) < 0.0)
            {
                double result = Math.Abs(resultBeamForces.N) / Math.Min(axialBuck1Rd, axialBuck2Rd) +
                    (_options.UniformMomentFactorm1 * resultBeamForces.M1 / CalculateBendingMoment1ElasticCapacity(bendingCompSectionClass, steelSection)) +
                    (_options.UniformMomentFactorm2 * resultBeamForces.M2 / CalculateBendingMoment2ElasticCapacity(bendingCompSectionClass, steelSection));
                if (result < 0.01)
                    interaction879WR = 0.01;
                else
                    interaction879WR = result;
            }
            else
                interaction879WR = 0.01;

            // equazione 8.80 cap. 8.9.2
            double interaction880WR;
            if (MinSigma(steelSection, resultBeamForces.N, resultBeamForces.M2, resultBeamForces.M1) < 0.0)
            {
                double pc1 = CalculateAxialBucklingCapacity1AxisForInteraction(axialCompSectionClass, steelSection);
                double pc2 = CalculateAxialBucklingCapacity2AxisForInteraction(axialCompSectionClass, steelSection);
                double result = Math.Abs(resultBeamForces.N) / Math.Min(pc1, pc2) +      // <= Pc segnato cap 8.9.2
                    (_options.UniformMomentFactorm1 * resultBeamForces.M1 / CalculateBendingMoment1ElasticCapacity(bendingCompSectionClass, steelSection)) +
                    (_options.UniformMomentFactorm2 * resultBeamForces.M2 / CalculateBendingMoment2ElasticCapacity(bendingCompSectionClass, steelSection));
                if (result < 0.01)
                    interaction880WR = 0.01;
                else
                    interaction880WR = result;
            }
            else
                interaction880WR = 0.01;

            stationResult.SetCapacity(axialTensionRd, axialCompressionRd, axialBuck1Rd, axialBuck2Rd, shear1Rd, shear2Rd, bending1Rd, bending2Rd, latTorsRd);
            stationResult.SetWorkingRatio(axialTensionWR, axialCompressioneWR, axialBuck1WR, axialBuck2WR, shear1WR, shear2WR, bending1WR, bending2WR, latTorsWR,
                interaction878WR, interaction879WR, interaction880WR, interaction881WR);

            stationResult.SetClasses(axialCompSectionClass, bendingCompSectionClass);
            stationResult.SetPy(_py, _epsilon);

            return stationResult;
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
        /// CopSuos2011 Chapter 8.9.2
        /// </summary>
        /// <returns></returns>
        private double CalculateAxialBucklingCapacity1AxisForInteraction(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2 || sectionClass == SectionClass.Class3)
                return section.Area * CalculatePCompressionReducedXAxisForInteraction(section);

            else
                return GetEffettiveArea(section) * CalculatePCompressionReducedXAxis(section);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.9.2
        /// </summary>
        /// <returns></returns>
        private double CalculateAxialBucklingCapacity2AxisForInteraction(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2 || sectionClass == SectionClass.Class3)
                return section.Area * CalculatePCompressionReducesYAxisForInteraction(section);

            else
                return GetEffettiveArea(section) * CalculatePCompressionReducesYAxis(section);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        private double CalculatePCompressionReducedXAxis(ISteelSection section)
        {
            return CalculatePCompressionXAxis(section) * GetLenghtAxialBuckling1() / ((Section)section).InertiaRadiusX *
                Math.Sqrt(GetEffettiveArea(section) / section.Area);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        private double CalculatePCompressionReducesYAxis(ISteelSection section)
        {
            return CalculatePCompressionYAxis(section) * GetLenghtAxialBuckling2() / ((Section)section).InertiaRadiusY *
                Math.Sqrt(GetEffettiveArea(section) / section.Area);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.9.2
        /// </summary>
        private double CalculatePCompressionReducedXAxisForInteraction(ISteelSection section)
        {
            return CalculatePCompressionXAxis(section) * GetEffectiveLenghtAxialBuckling1() / ((Section)section).InertiaRadiusX *
                Math.Sqrt(GetEffettiveArea(section) / section.Area);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.9.2
        /// </summary>
        private double CalculatePCompressionReducesYAxisForInteraction(ISteelSection section)
        {
            return CalculatePCompressionYAxis(section) * GetEffectiveLenghtAxialBuckling2() / ((Section)section).InertiaRadiusY *
                Math.Sqrt(GetEffettiveArea(section) / section.Area);
        }

        private double CalculatePCompressionXAxis(ISteelSection section)
        {
            double pe = CalculatePeforAxialBucklingXAxis(section);
            double phi = CalculatePhiforAxialBucklingXAxis(section);
            double py = Py;
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Welded)
                py -= 20;
            return (pe * py) / (phi + Math.Pow(Math.Pow(phi, 2) - pe * py, 0.5));
        }

        private double CalculatePCompressionYAxis(ISteelSection section)
        {
            double pe = CalculatePeforAxialBucklingYAxis(section);
            double phi = CalculatePhiforAxialBucklingYAxis(section);
            double py = Py;
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Welded)
                py -= 20;
            return (pe * py) / (phi + Math.Pow(Math.Pow(phi, 2) - pe * py, 0.5));
        }

        /// <summary>
        /// CopSuos2011 Appendix 8.4
        /// </summary>
        private double CalculatePhiforAxialBucklingXAxis(ISteelSection section)
        {
            double lambda0 = 0.2 * Math.Pow(Math.Pow(Math.PI, 2) * ((Section)section).GetE() / Py, 0.5);
            double lambdaXAxis = GetLenghtAxialBuckling1() / ((Section)section).InertiaRadiusX;
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
            double lambdaYAxis = GetLenghtAxialBuckling2() / ((Section)section).InertiaRadiusY;
            double nForAxialBuck = Math.Max(GetAlphaBucklingCurveYYAxis(section) * (lambdaYAxis - lambda0) / 1000, 0);
            double py = Py;
            if (((Section)section).SectionType == Model.Sections.Section.SectionTypes.Welded)
                py -= 20;
            return (py + (nForAxialBuck + 1) * CalculatePeforAxialBucklingYAxis(section)) / 2;
        }

        private double CalculatePeforAxialBucklingXAxis(ISteelSection section)
        {
            double lambdaXAxis = GetLenghtAxialBuckling1() / ((Section)section).InertiaRadiusX;
            return (Math.Pow(Math.PI, 2) * Material.E) / (Math.Pow(lambdaXAxis, 2));
        }

        private double CalculatePeforAxialBucklingYAxis(ISteelSection section)
        {
            double lambdaYAxis = GetLenghtAxialBuckling2() / ((Section)section).InertiaRadiusY;
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
            return CalculateShearReductionDueToTorsion(resultBeamForces, section) * Py * GetShearAreaYaxis(section) / Math.Sqrt(3.0);
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
            if (section is SectionC sectC && sectC.SectionType == Model.Sections.Section.SectionTypes.Welded)
                return sectC.ThicknessWeb * (sectC.Height - sectC.ThicknessBottom - sectC.ThicknessTop);
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
            return CalculateShearReductionDueToTorsion(resultBeamForces, section) * Py * GetShearAreaXaxis(section) / Math.Sqrt(3.0);
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
            if (section is SectionC sectC && sectC.SectionType == Model.Sections.Section.SectionTypes.Welded)
                return sectC.LengthTop * sectC.ThicknessTop + sectC.LengthBottom * sectC.ThicknessBottom;
            if (section is SectionRHS sectionRHS)
                return (sectionRHS.ThicknessBottom + sectionRHS.ThicknessTop) * sectionRHS.BaseInternal;
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
                    return Math.Sqrt(1 - (sigmaStVenant / (1.25 * (Py / Math.Sqrt(3))))) - (sigmaWarp / (1.25 * (Py / Math.Sqrt(3))));
                }
                else if (section is SectionCHS sectionCHS)
                {
                    double sigmaStVenant = resultBeamForces.T / (2.0 * sectionCHS.Area * sectionCHS.Thickness);
                    return 1 - (sigmaStVenant / (1.25 * (Py / Math.Sqrt(3))));
                }
                else if (section is SectionRHS sectionRHS)
                {
                    double sigmaStVenant = resultBeamForces.T / (2.0 * sectionRHS.Area *
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

                if (sectionH.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                {
                    if (lambdaW <= 0.9)
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
                    return Math.Min(Py * (((Section)section).Wpl1 - rhoMomentShearInteraction * CalculatePlasticModulusShearXAxis(section)),
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
                return (1.0 / 4.0) * sech.Height * Math.Pow(sech.ThicknessWeb, 2);
            if (section is SectionH secH && secH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                return (1.0 / 4.0) * secH.HeightWeb * Math.Pow(secH.ThicknessWeb, 2);
            if (section is SectionC secC && secC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return (1.0 / 4.0) * secC.ThicknessWeb * Math.Pow(secC.Height, 2);
            if (section is SectionRHS sectionRHS)
                return 2 * (1.0 / 4.0) * ((sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight) / 2) * Math.Pow(sectionRHS.BaseInternal, 2);
            if (section is SectionCHS sectionCHS)
                return 0.6 * (1.0 / 6.0) * (Math.Pow(sectionCHS.Diameter, 3) - Math.Pow(sectionCHS.DiameterInternal, 3));
            if (section is SectionT sectionT)
                return (1.0 / 4.0) * sectionT.ThicknessWeb * Math.Pow((sectionT.Height - sectionT.ThicknessFlange), 3);
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
                double rhoMomentShearInteraction = Math.Pow((2 * resultBeamForces.V1 / CalculateShearXCapacity(resultBeamForces, section)) - 1, 2);
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
                return (1.0 / 4.0) * sech.LenghtTopFlange * Math.Pow(sech.ThicknessTopFlange, 2) +
                    (1.0 / 4.0) * sech.LenghtBottomFlange * Math.Pow(sech.ThicknessBottomFlange, 2);
            if (section is SectionC secC)
                return (1.0 / 4.0) * secC.LengthTop * Math.Pow(secC.ThicknessTop, 2) + (1.0 / 4.0) * secC.LengthBottom * Math.Pow(secC.ThicknessBottom, 2);
            if (section is SectionRHS sectionRHS)
                return 2.0 * (1.0 / 4.0) * ((sectionRHS.ThicknessBottom + sectionRHS.ThicknessTop) / 2) * Math.Pow(sectionRHS.BaseInternal, 2);
            if (section is SectionCHS sectionCHS)
                return 0.6 * (1.0 / 6.0) * (Math.Pow(sectionCHS.Diameter, 3) - Math.Pow(sectionCHS.DiameterInternal, 3));
            if (section is SectionT sectionT)
                return (1.0 / 4.0) * sectionT.LenghtFlange * Math.Pow(sectionT.ThicknessFlange, 2);
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        private double CalculateBendingMoment1ElasticCapacity(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2 || sectionClass == SectionClass.Class3)
                return Py * ((Section)section).Wel1;
            else        // SectionClass.Class4
                return Py * CalculateEffettiveElasticModulus();       // TODO: implementare Wel effettivo (vedi 8.2.2)            
        }

        private double CalculateBendingMoment2ElasticCapacity(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2 || sectionClass == SectionClass.Class3)
                return Py * ((Section)section).Wel2;
            else        // SectionClass.Class4
                return Py * CalculateEffettiveElasticModulus();       // TODO: implementare Wel effettivo (vedi 8.2.2)            
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
                    if (lambda < 770 * Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.33 || sectionRHS.Base / sectionRHS.Height < 1.33)
                    if (lambda < 670 * Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.40 || sectionRHS.Base / sectionRHS.Height < 1.40)
                    if (lambda < 580 * Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.44 || sectionRHS.Base / sectionRHS.Height < 1.44)
                    if (lambda < 550 * Epsilon)
                        return false;
                if (sectionRHS.Height / sectionRHS.Base < 1.50 || sectionRHS.Base / sectionRHS.Height < 1.50)
                    if (lambda < 515 * Epsilon)
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
            else if (section is SectionH || section is SectionC)
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
            return CheckerOptions.UniformMomentFactormLT;
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
                throw new NotImplementedException("CalculateXForLatTorsBuckling: CopSuos2011 not implemented x coefficient for this section");

            // CopSuos2011 Chapter 8.3.5.3 equation 8.27
            if (section is SectionH || section is SectionC)
                v = 1 / Math.Pow(1 + 0.05 * (Math.Pow(CalculateLambdaForLatTorsBuckling(section) / x, 2)), 0.25);
            else
                throw new NotImplementedException("CalculateXForLatTorsBuckling: CopSuos2011 not implemented v coefficient for this section");

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
            if (((Cop2011Checker.Cop2011Options)_options).LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default)
                le = GetLenghtLatTorsBuckling();
            else if (((Cop2011Checker.Cop2011Options)_options).LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.FullyRestrained)
                le = 0.8 * GetLenghtLatTorsBuckling();
            else if (((Cop2011Checker.Cop2011Options)_options).LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Unrestrained)
                le = 1.2 * GetLenghtLatTorsBuckling() + 2 * section.Height;
            else if (((Cop2011Checker.Cop2011Options)_options).LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad)
                le = 1.2 * GetLenghtLatTorsBuckling();
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
                throw new NotImplementedException("CalculateSectionClassException: not implemented Section for GetClassCompressedOuterFlangeBending");
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
                (ctRatio <= Math.Max(100 * Epsilon / (1 + 1.5 * GetR1(section, resultBeamForces)), 40 * Epsilon)))
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
                throw new NotImplementedException("CalculateSectionClassException: not implemented Section for GetClassCompressedFlangeRHS");
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
                if (ctRatio <= Math.Max(56.0 * Epsilon / (1 + 0.6 * GetR1(section, resultBeamForces)), 35.0 * Epsilon))
                    return SectionClass.Class1;
                else if (ctRatio <= Math.Max(70.0 * Epsilon / (1 + GetR1(section, resultBeamForces)), 35.0 * Epsilon))
                    return SectionClass.Class2;
                else if (ctRatio <= Math.Max(105 * Epsilon / (1 + 2 * GetR1(section, resultBeamForces)), 35.0 * Epsilon))
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else
                throw new NotImplementedException("CalculateSectionClassException: not implemented Section for GetClassCompressedWebRHS");
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
                double r1 = (-resultBeamForces.N / (2 * sectionRHS.Heightinternal * (sectionRHS.ThicknessWebRight + sectionRHS.ThicknessWebLeft) / 2 * Py));
                r1 = r1 < 1 ? 1 : r1;
                r1 = r1 > -1 ? -1 : r1;
                return r1;
            }
            else
                throw new NotImplementedException("Cop2011 R1 factor (§7.3) not supported Section type");
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
                {
                    double sigma1 = (-resultBeamForces.N / section.Area) + (resultBeamForces.M1 / sectionH.CalculateWelxTop());
                    double sigma2 = (-resultBeamForces.N / section.Area) + (resultBeamForces.M1 / sectionH.CalculateWelxBottom());
                    return (sigma1 + sigma2) / (2 * Py);
                }
            }

            else if (section is SectionRHS _)
                return (-resultBeamForces.N / (section.Area * Py));

            else
                throw new NotImplementedException("Cop2011 R2 factor (§7.3) not supported Section type");
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
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1)
                    return 1.0;
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class2)
                    return 1.2;
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1H)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
            }
            if (Material.Fyk > 460 && Material.Fyk < 690)
            {
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1 || 
                    ((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class2
                    || ((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1H)
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
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1)
                    return 1.2;
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class2)
                    return 1.3;
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1H)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
            }
            if (Material.Fyk > 460 && Material.Fyk < 690)
            {
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1 || 
                    ((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class2
                    || ((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (((Cop2011Checker.Cop2011Options)_options).SteelClass == Cop2011Checker.Cop2011Options.SteelClasses.Class1H)
                    return 1.2;
            }
            throw new NotImplementedException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
        }

        #endregion

        #endregion


        public class Cop2011Options : Options
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

            /// <summary>
            /// The class of the steel material. See CopSuos2011 table 4.1
            /// </summary>
            public enum SteelClasses
            {
                [Description("Class 1")]
                Class1,
                [Description("Class 2")]
                Class2,
                [Description("Class 3")]
                Class3,
                [Description("Class 1H")]
                Class1H,
            }

            public enum LateralTorsionalBucklingConditions
            {
                [Description("Compressed flange restrained at ends")]
                Default,
                [Description("Compressed flange fully Restrained")]
                FullyRestrained,
                [Description("Compressed flange unrestrained")]
                Unrestrained,
                [Description("Compressed flange unrestrained and under destabilizing loads")]
                DestabilizingLoad,
            }

            #endregion


            #region Variables

            protected SteelClasses _steelClass;
            protected LateralTorsionalBucklingConditions _lateralTorsionalBucklingConditions;

            #endregion


            #region Properties

            public SteelClasses SteelClass { get => _steelClass; internal set => _steelClass = value; }

            public LateralTorsionalBucklingConditions LateralTorsionalBucklingCondition { get => _lateralTorsionalBucklingConditions; internal set => _lateralTorsionalBucklingConditions = value; }

            #endregion


            #region Constructor

            public Cop2011Options(SteelClasses steelGrade = SteelClasses.Class1, LateralTorsionalBucklingConditions latTorsBucklingCondition = LateralTorsionalBucklingConditions.Default,
                double unbracedLengthFactorAxialBuck1 = 1, double effectiveLengthFactorAxialBuck1 = 1, double unbracedLengthFactorAxialBuck2 = 1,
                double effectiveLengthFactorAxialBuck2 = 1, double UnbracedLengthFactorLatTorsBuck = 1, double effectiveLengthFactorLatTorsBuck = 1,
                double eqvUniformMomentFactorm1 = 1, double eqvUniformMomentFactorm2 = 1, double eqvUniformMomentFactormLT = 1)
                : base(unbracedLengthFactorAxialBuck1, effectiveLengthFactorAxialBuck1, unbracedLengthFactorAxialBuck2,
                      effectiveLengthFactorAxialBuck2, UnbracedLengthFactorLatTorsBuck, effectiveLengthFactorLatTorsBuck, 1, 1, 1, 1,
                      eqvUniformMomentFactorm1, eqvUniformMomentFactorm2, eqvUniformMomentFactormLT)
            {
                _steelClass = steelGrade;
                _lateralTorsionalBucklingConditions = latTorsBucklingCondition;
            }

            #endregion


            #region Setter

            public void SetSteelClass(SteelClasses steelGrade)
            {
                _steelClass = steelGrade;
            }

            public void SetLateralTorsionalCondition(LateralTorsionalBucklingConditions lateralTorsionalBucklingConditions)
            {
                _lateralTorsionalBucklingConditions = lateralTorsionalBucklingConditions;
            }

            #endregion
        }

    }
}
