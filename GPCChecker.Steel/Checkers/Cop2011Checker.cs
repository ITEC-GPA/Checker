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
using System.Runtime.Serialization;

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
        protected double _beta;
        protected double _epsilon;

        #endregion

        #region Properties

        /// <summary>
        /// The design strength
        /// </summary>
        internal double Py => _py;

        /// <summary>
        /// The reduced design strength coefficient for effective stress method for slender cross-sections in §7.7
        /// </summary>
        internal double Beta => _beta;

        /// <summary>
        /// Parameter for section classification
        /// </summary>
        internal double Epsilon => _epsilon;

        /// <summary>
        /// Array of Cop2011BeamStationResults
        /// </summary>
        public Cop2011BeamStationResults[] Cop2011BeamStationResults => _beamStationResults.Cast<Cop2011BeamStationResults>().ToArray();

		#endregion

		#region Constructor

		public Cop2011Checker(BeamCheckerAttributes attributes, Cop2011Checker.Cop2011Options options, StandardCopSuos2011 standard)
            : base(attributes, options, standard)
        {
            _py = GetPy(attributes.Sections.Select(i => i.SteelMaterial.Fyk).Min(), attributes.Sections.Select(i => i.SteelMaterial.Fu).Min());
            _beta = 1.0;
            _epsilon = Math.Sqrt(275 / _py);        //value of Epsilon for section classification            
        }

		public Cop2011Checker(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
            _py = info.GetDouble("Py");
            _beta = info.GetDouble("Beta");
            _epsilon = info.GetDouble("Epsilon");
        }

		#endregion

		#region Public Methods

		public override void PerformCheck()
        {
            _beamStationResults = PerformCheck(_beamCheckerAttributes.Sections, _beamCheckerAttributes.Results);
        }

        public async void PerformCheckAsync()
        {
            await Task.Run(() =>
            {
                _beamStationResults = PerformCheck(_beamCheckerAttributes.Sections, _beamCheckerAttributes.Results);
            });
        }

		#endregion

		#region Private PerformCheck Method

		/// <param name="steelSection">section of each station</param>
		/// <param name="beamResult">result for each station and loadcase</param>
		/// <returns></returns>
		protected Cop2011BeamStationResults[] PerformCheck(ISteelSection[] steelSection, BeamResult[] beamResult)
        {
            Cop2011BeamStationResults[] stationResults = new Cop2011BeamStationResults[steelSection.Length * beamResult.Select(i => i.ResultLocations.Length).Sum()];
            int index = 0;

            double py = _py;

            for (int k = 0; k < beamResult.Length; k++)
            {
                for (int j = 0; j < beamResult[k].ResultLocations.Length; j++)
                {
                    for (int i = 0; i < beamResult[k].ResultLocations[j].ResultTypes.Length; i++)
                    {
                        _py = py;

                        try
                        {
                            ResultBeamForces rbf = (ResultBeamForces)beamResult[k].ResultLocations[j].ResultTypes[i];

                            ResultLocationStation resultLocationStation = new ResultLocationStation(new ResultBeamForces[] { rbf },
                                ((ResultLocationStation)beamResult[k].ResultLocations[j]).DistanceFromStartPoint,
                                ((ResultLocationStation)beamResult[k].ResultLocations[j]).ElementLenght);

                            stationResults[index] =
                                new Cop2011BeamStationResults(steelSection[i], resultLocationStation,
                                beamResult[k].Case, (StandardCopSuos2011)_standard, (Cop2011Options)_options, BeamName);

                            SectionClass axialCompSectionClass = CalculateSectionClassDueToCompression(rbf, steelSection[i]);
                            SectionClass bendingCompSectionClass = CalculateSectionClassDueToBending(rbf, steelSection[i]);
                            SectionClass sectionClass = SetWorstClass(axialCompSectionClass, bendingCompSectionClass);

                            if (_beta < 1.0)
                                _py *= Math.Pow(_beta, 2.0);

                            stationResults[index].SetClasses(axialCompSectionClass, bendingCompSectionClass);
                            stationResults[index].SetBucklingLenght(GetLengthAxialBuckling1(), GetLengthAxialBuckling2(), GetLengthLatTorsBuckling());

                            double axialTensionRd = CalculateAxialTensionCapacity(steelSection[i]);
                            double axialTensionWR = GetWorkingRatio(Math.Max(rbf.N, 0), axialTensionRd);

                            double axialCompressionRd = CalculateAxialCompression(steelSection[i]);
                            double axialCompressionWR = GetWorkingRatio(Math.Min(rbf.N, 0), axialCompressionRd);

                            double shear1Rd = CalculateShear1Capacity(rbf, steelSection[i]);
                            double shear1WR = GetWorkingRatio(Math.Abs(rbf.V1), shear1Rd);

                            double shear2Rd = CalculateShear2Capacity(rbf, steelSection[i]);
                            double shear2WR = GetWorkingRatio(Math.Abs(rbf.V2), shear2Rd);

                            double axialBuck1Rd = CalculateAxialBucklingCapacity1Axis(steelSection[i]);
                            double axialBuck1WR;

                            if (((Section)steelSection[i]).GetMinSigma(rbf.N, rbf.M1, rbf.M2) < 0.0)
                                axialBuck1WR = GetWorkingRatio(Math.Min(rbf.N, 0), axialBuck1Rd);
                            else
                                axialBuck1WR = 0.001;

                            double axialBuck2Rd = CalculateAxialBucklingCapacity2Axis(steelSection[i]);
                            double axialBuck2WR;

                            if (((Section)steelSection[i]).GetMinSigma(rbf.N, rbf.M1, rbf.M2) < 0.0)
                                axialBuck2WR = GetWorkingRatio(Math.Min(rbf.N, 0), axialBuck2Rd);
                            else
                                axialBuck2WR = 0.001;

                            double bending1Rd = CalculateBendingMoment1Capacity(rbf, bendingCompSectionClass, steelSection[i]);
                            double bending1WR = GetWorkingRatio(rbf.M1, bending1Rd);

                            double bending2Rd = CalculateBendingMoment2Capacity(rbf, bendingCompSectionClass, steelSection[i]);
                            double bending2WR = GetWorkingRatio(rbf.M2, bending2Rd);

                            double latTorsRd = Math.Min(CalculateLateralTorsionalBucklingMomentCapacity(bendingCompSectionClass, steelSection[i]), bending1Rd);

                            double latTorsWR;
                            double interaction881WR;

                            // equazione 8.81 cap. 8.9.2
                            if (IsNecessaryTheLatTorsBucklingCheck(bendingCompSectionClass, steelSection[i]))
                            {
                                latTorsWR = GetWorkingRatio(CalculateMLTForLatTorsBuckling() * rbf.M1, latTorsRd);

                                // equazione 8.81 cap. 8.9.2
                                if (((Section)steelSection[i]).GetMinSigma(rbf.N, rbf.M1, rbf.M2) < 0.0)
                                {
                                    double result = Math.Abs(Math.Min(rbf.N, 0) / Math.Min(axialBuck1Rd, axialBuck2Rd)) +
                                         Math.Abs(CalculateMLTForLatTorsBuckling() * rbf.M1 / latTorsRd) +
                                         Math.Abs(_options.UniformMomentFactorm2 * rbf.M2 / CalculateBendingMoment2ElasticCapacity(bendingCompSectionClass, steelSection[i]));

                                    if (result < 0.001)
                                        interaction881WR = 0.001;
                                    else
                                        interaction881WR = result;
                                }
                                else
                                    interaction881WR = 0.001;
                            }
                            else
							{
                                interaction881WR = 0.001;
                                latTorsWR = 0.001;
                            }

                            double interaction878WR;

                            // equazione 8.78 cap. 8.9.2
                            if (((Section)steelSection[i]).GetMinSigma(rbf.N, rbf.M1, rbf.M2) < 0.0)      // compressione
                            {
                                double result = Math.Abs(rbf.N / axialCompressionRd) +
                                         Math.Abs(rbf.M1 / bending1Rd) +
                                         Math.Abs(rbf.M2 / bending2Rd);

                                if (result < 0.001)
                                    interaction878WR = 0.001;
                                else
                                    interaction878WR = result;
                            }
                            else
                            {
                                double result = Math.Abs(rbf.N / axialTensionRd) +
                                         Math.Abs(rbf.M1 / bending1Rd) +
                                         Math.Abs(rbf.M2 / bending2Rd);

                                if (result < 0.001)
                                    interaction878WR = 0.001;
                                else
                                    interaction878WR = result;
                            }

                            double interaction879WR;

                            // equazione 8.79 cap. 8.9.2
                            if (((Section)steelSection[i]).GetMinSigma(rbf.N, rbf.M1, rbf.M2) < 0.0)
                            {
                                double result = Math.Abs(Math.Min(rbf.N, 0) / Math.Min(axialBuck1Rd, axialBuck2Rd)) +
                                    Math.Abs(_options.UniformMomentFactorm1 * rbf.M1 / CalculateBendingMoment1ElasticCapacity(bendingCompSectionClass, steelSection[i])) +
                                    Math.Abs(_options.UniformMomentFactorm2 * rbf.M2 / CalculateBendingMoment2ElasticCapacity(bendingCompSectionClass, steelSection[i]));

                                if (result < 0.001)
                                    interaction879WR = 0.001;
                                else
                                    interaction879WR = result;
                            }
                            else
                                interaction879WR = 0.001;

                            double interaction880WR;

                            // equazione 8.80 cap. 8.9.2
                            if (((Section)steelSection[i]).GetMinSigma(rbf.N, rbf.M1, rbf.M2) < 0.0)
                            {
                                // <= Pc segnato cap 8.9.2
                                double result = Math.Abs(Math.Min(rbf.N, 0) / Math.Min(CalculateAxialBucklingCapacity1AxisForInteraction(steelSection[i]),
                                    CalculateAxialBucklingCapacity2AxisForInteraction(steelSection[i]))) +
                                    Math.Abs(_options.UniformMomentFactorm1 * rbf.M1 / CalculateBendingMoment1ElasticCapacity(bendingCompSectionClass, steelSection[i])) +
                                    Math.Abs(_options.UniformMomentFactorm1 * rbf.M2 / CalculateBendingMoment2ElasticCapacity(bendingCompSectionClass, steelSection[i]));

                                if (result < 0.001)
                                    interaction880WR = 0.001;
                                else
                                    interaction880WR = result;
                            }
                            else
                                interaction880WR = 0.001;


                            stationResults[index].SetCapacity(axialTensionRd, axialCompressionRd, axialBuck1Rd, axialBuck2Rd, 
                                shear1Rd, shear2Rd, bending1Rd, bending2Rd, latTorsRd);

                            stationResults[index].SetWorkingRatio(axialTensionWR, axialCompressionWR, axialBuck1WR, axialBuck2WR, 
                                shear1WR, shear2WR, bending1WR, bending2WR, latTorsWR,
                                interaction878WR, interaction879WR, interaction880WR, interaction881WR);

                            stationResults[index].SetPy(_py, _epsilon, _beta);

                            stationResults[index].SetResultsForReportAxialBuckling(GetBucklingCurveXXAxis(steelSection[i]), GetBucklingCurveYYAxis(steelSection[i]),
                                CalculateLambdaAxialBuckling1Axis(steelSection[i]), CalculateLambdaAxialBuckling2Axis(steelSection[i]),
                                CalculatePeForAxialBuckling1Axis(steelSection[i]), CalculatePeForAxialBuckling2Axis(steelSection[i]),
                                CalculatePhiforAxialBuckling1Axis(steelSection[i]), CalculatePhiforAxialBuckling2Axis(steelSection[i]),
                                CalculatePCompression1Axis(steelSection[i]), CalculatePCompression2Axis(steelSection[i]), CalculateLambda0ForAxialBuckling(steelSection[i]),
                                CalculateNForAxialBuckling1Axis(steelSection[i]), CalculateNForAxialBuckling2Axis(steelSection[i]));

                            stationResults[index].SetResultsForReportLTB(CalculateLambdaLTForLatTorsBuckling(steelSection[i], bendingCompSectionClass), 
                                CalculateLambdaL0ForLatTorsBucklingBS5950(steelSection[i].SteelMaterial.E), CalculatePeForLatTorsBuckling(steelSection[i], bendingCompSectionClass), 
                                CalculatePhiLTForLatTorsBuckling(steelSection[i], bendingCompSectionClass),
                                CalculatePbForLatTorsBuckling(bendingCompSectionClass, steelSection[i]));

                            stationResults[index].SetResultForReportShear(GetShearAreaXaxis(steelSection[i]), GetShearAreaYaxis(steelSection[i]));

                            index++;
                        }
                        catch (Exception e)
                        {
                            _errorLog.Add($"Fail check beam {BeamName}, \n" +
                                $"Station {((ResultLocationStation)beamResult[k].ResultLocations[j]).DistanceFromStartPoint} mm from start point, \n" +
                                $"Combination {beamResult[k].Case.Name}. \n" +
                                $"Error: {e.Message}. \n");
                        }
                    }
                }
            }

            return stationResults;
        }

		#endregion

		#region Section Private Method

		#region Axial Tension

		private double CalculateAxialTensionCapacity(ISteelSection section)
        {
            return Py * section.Area;
        }

        #endregion

        #region Axial Compression

        private double CalculateAxialCompression(ISteelSection section)
        {
            return Py * section.Area;
        }

        #endregion

        #region Axial Buckling

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        /// <returns></returns>
        private double CalculateAxialBucklingCapacity1Axis(ISteelSection section)
        {
            return section.Area * CalculatePCompression1Axis(section);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.7.5
        /// </summary>
        /// <returns></returns>
        private double CalculateAxialBucklingCapacity2Axis(ISteelSection section)
        {
            return section.Area * CalculatePCompression2Axis(section);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.9.2
        /// </summary>
        /// <returns></returns>
        private double CalculateAxialBucklingCapacity1AxisForInteraction(ISteelSection section)
        {
            return section.Area * CalculatePCompressionReduced1AxisForInteraction(section);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.9.2
        /// </summary>
        /// <returns></returns>
        private double CalculateAxialBucklingCapacity2AxisForInteraction(ISteelSection section)
        {
            return section.Area * CalculatePCompressionReduces2AxisForInteraction(section);
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.9.2
        /// </summary>
        private double CalculatePCompressionReduced1AxisForInteraction(ISteelSection section)
        {
            return CalculatePCompression1Axis(section) * GetEffectiveLengthAxialBuckling1() / section.R11;
        }

        /// <summary>
        /// CopSuos2011 Chapter 8.9.2
        /// </summary>
        private double CalculatePCompressionReduces2AxisForInteraction(ISteelSection section)
        {
            return CalculatePCompression2Axis(section) * GetEffectiveLengthAxialBuckling2() / section.R22;
        }

        private double CalculatePCompression1Axis(ISteelSection section)
        {
            double pe = CalculatePeForAxialBuckling1Axis(section);
            double phi = CalculatePhiforAxialBuckling1Axis(section);
            double py = Py;
            
            if (section.SectionType == Section.SectionTypes.Welded)
                py -= 20;
            return (pe * py) / (phi + Math.Pow(Math.Pow(phi, 2) - pe * py, 0.5));
        }

        private double CalculatePCompression2Axis(ISteelSection section)
        {
            double pe = CalculatePeForAxialBuckling2Axis(section);
            double phi = CalculatePhiforAxialBuckling2Axis(section);
            double py = Py;
            if (section.SectionType ==Section.SectionTypes.Welded)
                py -= 20;
            return (pe * py) / (phi + Math.Pow(Math.Pow(phi, 2) - pe * py, 0.5));
        }

        /// <summary>
        /// CopSuos2011 Appendix 8.4
        /// </summary>
        private double CalculatePhiforAxialBuckling1Axis(ISteelSection section)
        {
            double lambda0 = 0.2 * Math.Pow(Math.Pow(Math.PI, 2) * ((Section)section).GetE() / Py, 0.5);
            double lambdaXAxis = GetLengthAxialBuckling1() / section.R11;
            double nForAxialBuckXAxis = Math.Max(GetAlphaBucklingCurveXXAxis(section) * (lambdaXAxis - lambda0) / 1000, 0);
            double py = Py;

            if (section.SectionType == Section.SectionTypes.Welded)
                py -= 20;
            return (py + (nForAxialBuckXAxis + 1) * CalculatePeForAxialBuckling1Axis(section)) / 2;
        }

        /// <summary>
        /// CopSuos2011 Appendix 8.4
        /// </summary>
        private double CalculatePhiforAxialBuckling2Axis(ISteelSection section)
        {
            double lambda0 = 0.2 * Math.Pow(Math.Pow(Math.PI, 2) * ((Section)section).GetE() / Py, 0.5);
            double lambdaYAxis = GetLengthAxialBuckling2() / section.R22;
            double nForAxialBuck = Math.Max(GetAlphaBucklingCurveYYAxis(section) * (lambdaYAxis - lambda0) / 1000, 0);
            double py = Py;

            if (section.SectionType == Section.SectionTypes.Welded)
                py -= 20;
            return (py + (nForAxialBuck + 1) * CalculatePeForAxialBuckling2Axis(section)) / 2;
        }

        private double CalculateNForAxialBuckling2Axis(ISteelSection section)
        {
            double lambda0 = 0.2 * Math.Pow(Math.Pow(Math.PI, 2) * ((Section)section).GetE() / Py, 0.5);
            double lambdaYAxis = GetLengthAxialBuckling2() / section.R22;
            return Math.Max(GetAlphaBucklingCurveYYAxis(section) * (lambdaYAxis - lambda0) / 1000, 0);
        }

        private double CalculateNForAxialBuckling1Axis(ISteelSection section)
        {
            double lambda0 = 0.2 * Math.Pow(Math.Pow(Math.PI, 2) * ((Section)section).GetE() / Py, 0.5);
            double lambdaXAxis = GetLengthAxialBuckling1() / section.R11;
            return Math.Max(GetAlphaBucklingCurveXXAxis(section) * (lambdaXAxis - lambda0) / 1000, 0);
        }

        private double CalculateLambda0ForAxialBuckling(ISteelSection section)
        {
            return 0.2 * Math.Pow(Math.Pow(Math.PI, 2) * ((Section)section).GetE() / Py, 0.5);
        }

        private double CalculatePeForAxialBuckling1Axis(ISteelSection section)
        {
            double lambda1Axis = GetLengthAxialBuckling1() / section.R11;
            return (Math.Pow(Math.PI, 2) * section.SteelMaterial.E) / (Math.Pow(lambda1Axis, 2));
        }

        private double CalculatePeForAxialBuckling2Axis(ISteelSection section)
        {
            double lambda2Axis = GetLengthAxialBuckling2() / section.R22;
            return (Math.Pow(Math.PI, 2) * section.SteelMaterial.E) / (Math.Pow(lambda2Axis, 2));
        }

        private double CalculateLambdaAxialBuckling2Axis(ISteelSection section)
        {
            return GetLengthAxialBuckling2() / section.R22; 
        }

        private double CalculateLambdaAxialBuckling1Axis(ISteelSection section)
        {
            return GetLengthAxialBuckling1() / section.R11;
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
            if (section is SteelSectionCHS sectionCHS)
            {
                if (sectionCHS.FormedType == Section.FormedTypes.HotFinished)
                {

                    if (section.SteelMaterial.Fyk >= 460)
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
            else if (section is SteelSectionRHS sectionRHS)
            {
                if (sectionRHS.SectionType == Section.SectionTypes.Welded)
                {
                    if (sectionRHS.ThicknessBottom < 40 && sectionRHS.ThicknessTop < 40 && sectionRHS.ThicknessWebLeft < 40 && sectionRHS.ThicknessWebRight < 40)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.b;
                    else
                        return Cop2011Checker.Cop2011Options.BuckingCurves.c;
                }
                else if (sectionRHS.FormedType ==Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                    return Cop2011Checker.Cop2011Options.BuckingCurves.c;
            }
            else if (section is SteelSectionH sectionH)
            {
                if (sectionH.SectionType == Section.SectionTypes.Rolled)
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
            if (section is SteelSectionCHS sectionCHS)
            {
                if (sectionCHS.FormedType == Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
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
            else if (section is SteelSectionRHS sectionRHS)
            {
                if (sectionRHS.SectionType == Section.SectionTypes.Welded)
                {
                    if (sectionRHS.ThicknessBottom < 40 && sectionRHS.ThicknessTop < 40 && sectionRHS.ThicknessWebLeft < 40 && sectionRHS.ThicknessWebRight < 40)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.b;
                    else
                        return Cop2011Checker.Cop2011Options.BuckingCurves.c;
                }
                else if (sectionRHS.FormedType == Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return Cop2011Checker.Cop2011Options.BuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                    return Cop2011Checker.Cop2011Options.BuckingCurves.c;
            }
            else if (section is SteelSectionH sectionH)
            {
                if (sectionH.SectionType == Section.SectionTypes.Rolled)
                {
                    if ((2.0 * sectionH.Height) / (sectionH.LenghtTopFlange + sectionH.LenghtBottomFlange) > 1.2)
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
        private double CalculateShear2Capacity(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (section is SteelSectionH sectionH)
                if (sectionH.SectionType == Section.SectionTypes.Rolled)
                {
                    if (sectionH.HeightWeb / sectionH.ThicknessWeb > 70.0 * Epsilon)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * GetShearBucklingReduction(section) * GetShearAreaYaxis(section);
                }
                else
                {
                    if (sectionH.HeightWeb / sectionH.ThicknessWeb > 62.0 * Epsilon)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * GetShearBucklingReduction(section) * GetShearAreaYaxis(section);
                }
            if (section is SteelSectionRHS sectionRHS)
                if (sectionRHS.SectionType == Section.SectionTypes.Rolled)
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
            if (section is SteelSectionC sectionC)
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
            if (section is SteelSectionH sech && sech.SectionType == Section.SectionTypes.Rolled)
                return sech.ThicknessWeb * sech.Height;
            if (section is SteelSectionH secH && secH.SectionType == Section.SectionTypes.Welded)
                return secH.ThicknessWeb * secH.HeightWeb;
            if (section is SteelSectionC secC && secC.SectionType == Section.SectionTypes.Rolled)
                return secC.ThicknessWeb * secC.Height;
            if (section is SteelSectionC sectC && sectC.SectionType == Section.SectionTypes.Welded)
                return sectC.ThicknessWeb * (sectC.Height - sectC.ThicknessBottom - sectC.ThicknessTop);
            if (section is SteelSectionRHS sectionRHS)
                return (sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight) * sectionRHS.Heightinternal;
            if (section is SteelSectionCHS sectionCHS)
                return 0.6 * sectionCHS.Area;
            if (section is SteelSectionT sectionT)
                return sectionT.ThicknessWeb * (sectionT.Height - sectionT.ThicknessFlange);
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        private double CalculateShear1Capacity(ResultBeamForces resultBeamForces, ISteelSection section)
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
            if (section is SteelSectionH secH)
                return secH.ThicknessBottomFlange * secH.LenghtBottomFlange + secH.ThicknessTopFlange * secH.LenghtTopFlange;
            if (section is SteelSectionC secC && secC.SectionType == Section.SectionTypes.Rolled)
                return secC.LengthTop * secC.ThicknessTop + secC.LengthBottom * secC.ThicknessBottom;
            if (section is SteelSectionC sectC && sectC.SectionType == Section.SectionTypes.Welded)
                return sectC.LengthTop * sectC.ThicknessTop + sectC.LengthBottom * sectC.ThicknessBottom;
            if (section is SteelSectionRHS sectionRHS)
                return (sectionRHS.ThicknessBottom + sectionRHS.ThicknessTop) * sectionRHS.BaseInternal;
            if (section is SteelSectionCHS sectionCHS)
                return 0.6 * sectionCHS.Area;
            if (section is SteelSectionT sectionT)
                return sectionT.LenghtFlange * sectionT.ThicknessFlange;
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        /// <summary>
        /// ReductPy for shear check in case of torsion
        /// </summary>
        private double CalculateShearReductionDueToTorsion(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (Math.Abs(resultBeamForces.T) > 1)
            {
                if (section is SteelSectionH)
                {
                    double sigmaStVenant = Math.Abs(resultBeamForces.T) / section.Jt;
                    double reduction = Math.Sqrt(1 - (sigmaStVenant / (1.25 * (Py / Math.Sqrt(3)))));
                    return Math.Max(Math.Min(reduction, 1.0), 0.0); 
                }
                else if (section is SteelSectionC)
                {
                    double sigmaStVenant = Math.Abs(resultBeamForces.T) / section.Jt;
                    double sigmaWarp = resultBeamForces.T / section.Jw;
                    double reduction = Math.Sqrt(1 - (sigmaStVenant / (1.25 * (Py / Math.Sqrt(3))))) - (sigmaWarp / (1.25 * (Py / Math.Sqrt(3))));
                    return Math.Max(Math.Min(reduction, 1.0), 0.0);
                }
                else if (section is SteelSectionCHS sectionCHS)
                {
                    double omega = Math.PI * Math.Pow((sectionCHS.Diameter / 2.0 - sectionCHS.Thickness / 2.0), 2.0);
                    double sigmaStVenant = Math.Abs(resultBeamForces.T) / (2.0 * omega * sectionCHS.Thickness);
                    double reduction = 1 - (sigmaStVenant / (1.25 * (Py / Math.Sqrt(3))));
                    return Math.Max(Math.Min(reduction, 1.0), 0.0);
                }
                else if (section is SteelSectionRHS sectionRHS)
                {
                    double omega = Math.PI * Math.Pow((sectionRHS.Height - sectionRHS.ThicknessTop - sectionRHS.ThicknessBottom) *
                        (sectionRHS.Base - sectionRHS.ThicknessWebLeft - sectionRHS.ThicknessWebRight), 2.0);
                    double sigmaStVenant = Math.Abs(resultBeamForces.T) / (2.0 * omega *
                        (sectionRHS.ThicknessBottom + sectionRHS.ThicknessTop + sectionRHS.ThicknessWebRight + sectionRHS.ThicknessWebLeft) / 4.0);
                    double reduction = 1 - (sigmaStVenant / (1.25 * (Py / Math.Sqrt(3))));
                    return Math.Max(Math.Min(reduction, 1.0), 0.0);
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

                if (section.SectionType == Section.SectionTypes.Rolled)
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
            if ((Math.Abs(resultBeamForces.V2) < 0.6 * CalculateShear2Capacity(resultBeamForces, section)))   // low shear condition
            {
                if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                    return Math.Min(Py * section.Wpl1, 1.2 * Py * section.Wel1);
                else if (sectionClass == SectionClass.Class3)
                    return Py * section.Wel1;
                else        // SectionClass.Class4
                    return Py * section.Wel1;
            }
            else // high shear condition
            {
                double rhoMomentShearInteraction = Math.Pow((2 * resultBeamForces.V2 / CalculateShear2Capacity(resultBeamForces, section)) - 1, 2);
                if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                    return Math.Min(Py * (section.Wpl1 - rhoMomentShearInteraction * CalculatePlasticModulusShearXAxis(section)),
                        1.2 * Py * (section.Wel1 - rhoMomentShearInteraction * CalculatePlasticModulusShearXAxis(section) / 1.5));
                else if (sectionClass == SectionClass.Class3)
                    return Py * (section.Wel1 - rhoMomentShearInteraction * CalculatePlasticModulusShearXAxis(section) / 1.5);
                else        // SectionClass.Class4
                    return Py * (section.Wel1 - rhoMomentShearInteraction * CalculatePlasticModulusShearXAxis(section) / 1.5);
            }
        }

        /// <summary>
        /// Return the plastic modulus of shear areain high shear condition chapter 8.2.2.2
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        private double CalculatePlasticModulusShearXAxis(ISteelSection section)
        {
            if (section is SteelSectionH sech && sech.SectionType == Section.SectionTypes.Rolled)
                return (1.0 / 4.0) * sech.Height * Math.Pow(sech.ThicknessWeb, 2);
            if (section is SteelSectionH secH && secH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                return (1.0 / 4.0) * secH.HeightWeb * Math.Pow(secH.ThicknessWeb, 2);
            if (section is SteelSectionC secC && secC.SectionType == Section.SectionTypes.Rolled)
                return (1.0 / 4.0) * secC.ThicknessWeb * Math.Pow(secC.Height, 2);
            if (section is SteelSectionRHS sectionRHS)
                return 2 * (1.0 / 4.0) * ((sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight) / 2) * Math.Pow(sectionRHS.BaseInternal, 2);
            if (section is SteelSectionCHS sectionCHS)
                return 0.6 * (1.0 / 6.0) * (Math.Pow(sectionCHS.Diameter, 3) - Math.Pow(sectionCHS.DiameterInternal, 3));
            if (section is SteelSectionT sectionT)
                return (1.0 / 4.0) * sectionT.ThicknessWeb * Math.Pow((sectionT.Height - sectionT.ThicknessFlange), 3);
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        private double CalculateBendingMoment2Capacity(ResultBeamForces resultBeamForces, SectionClass sectionClass, ISteelSection section)
        {
            if ((Math.Abs(resultBeamForces.V1) < 0.6 * CalculateShear1Capacity(resultBeamForces, section)))   // low shear condition
            {
                if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                    return Math.Min(Py * section.Wpl2, 1.2 * Py * section.Wel2);
                else if (sectionClass == SectionClass.Class3)
                    return Py * section.Wel2;
                else        // SectionClass.Class4
                    return Py * section.Wel2;
            }
            else // high shear condition
            {
                double rhoMomentShearInteraction = Math.Pow((2 * resultBeamForces.V1 / CalculateShear1Capacity(resultBeamForces, section)) - 1, 2);
                if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                    return Math.Min(Py * (section.Wpl2 - rhoMomentShearInteraction * CalculatePlasticModulusShearYAxis(section)),
                        1.2 * Py * (section.Wel2 - rhoMomentShearInteraction * CalculatePlasticModulusShearYAxis(section) / 1.5));
                else if (sectionClass == SectionClass.Class3)
                    return Py * (section.Wel2 - rhoMomentShearInteraction * CalculatePlasticModulusShearYAxis(section) / 1.5);
                else        // SectionClass.Class4
                    return Py * (section.Wel2 - rhoMomentShearInteraction * CalculatePlasticModulusShearYAxis(section) / 1.5);
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
            return Py * section.Wel1;
        }

        private double CalculateBendingMoment2ElasticCapacity(SectionClass sectionClass, ISteelSection section)
        {
            return Py * section.Wel2; 
        }

        #endregion

        #region Lateral Torsional Bucking Capacity

        //TODO: calcolare Mcx chapter 8.3.5.2

        /// <summary>
        /// CopSuos2011 Chapter 8.3.5.2
        /// </summary>
        private double CalculateLateralTorsionalBucklingMomentCapacity(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                return CalculatePbForLatTorsBuckling(sectionClass, section) * section.Wpl1;
            else if (sectionClass == SectionClass.Class3)
                return CalculatePbForLatTorsBuckling(sectionClass, section) * section.Wel1;
            else
                return CalculatePbForLatTorsBuckling(sectionClass, section) * section.Wel1;
        }

        private double CalculatePbForLatTorsBuckling(SectionClass sectionClass, ISteelSection section)
        {
            return Math.Min((CalculatePeForLatTorsBuckling(section, sectionClass) * Py) / (CalculatePhiLTForLatTorsBuckling(section, sectionClass) +
                Math.Pow(Math.Pow(CalculatePhiLTForLatTorsBuckling(section, sectionClass), 2) - CalculatePeForLatTorsBuckling(section, sectionClass) * Py, 0.5)), Py);
        }

        /// <summary>
        /// BS5950 B.2.2
        /// </summary>
        private double CalculatePeForLatTorsBuckling(ISteelSection section, SectionClass sectionClass)
        {            
            return (Math.Pow(Math.PI, 2) * section.SteelMaterial.E) / Math.Pow(CalculateLambdaLTForLatTorsBuckling(section, sectionClass), 2);
        }

        /// <summary>
        /// BS5950 B.2.2
        /// </summary>
        private double CalculatePhiLTForLatTorsBuckling(ISteelSection section, SectionClass sectionClass)
        {
            double nlt;                   //Perry factor BS5950 B.2.2
            double alphaLT = 7.0;         // SAP prende come valore 3 => la norma dice 7
            double lambdaLT = CalculateLambdaLTForLatTorsBuckling(section, sectionClass);
            double lambdaLtBS = CalculateLambdaL0ForLatTorsBucklingBS5950(section.SteelMaterial.E);

            if (section.SectionType == Section.SectionTypes.Rolled)
                nlt = Math.Max(alphaLT * (lambdaLT - lambdaLtBS) / 1000, 0);
            else       
            {
                if (lambdaLT <= lambdaLtBS)
                    nlt = 0.0;
                else if (lambdaLtBS <= lambdaLT && (lambdaLT <= 2 * lambdaLtBS))
                    nlt = 2 * alphaLT * (lambdaLT - lambdaLtBS) / 1000;
                else if (2 * lambdaLtBS <= lambdaLT && (lambdaLT <= 3 * lambdaLtBS))
                    nlt = 2 * alphaLT * lambdaLtBS / 1000;
                else
                    nlt = alphaLT * (lambdaLT - lambdaLtBS) / 1000;
            }

            return (Py + (nlt + 1) * CalculatePeForLatTorsBuckling(section, sectionClass)) / 2;
        }

        /// <summary>
        /// Return limiting equivalent slenderness BS5950 B2.2
        /// </summary>
        private double CalculateLambdaL0ForLatTorsBucklingBS5950(double E)
        {
            return 0.4 * Math.Pow(Math.Pow(Math.PI, 2) * E / Py, 0.5);
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

                if (sectionRHS.Height / sectionRHS.Base <= 1.25 && sectionRHS.Base / sectionRHS.Height <= 1.25)
                    if (lambda <= 770 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 1.33 && sectionRHS.Base / sectionRHS.Height <= 1.33)
                    if (lambda <= 670 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 1.40 && sectionRHS.Base / sectionRHS.Height <= 1.40)
                    if (lambda <= 580 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 1.44 && sectionRHS.Base / sectionRHS.Height <= 1.44)
                    if (lambda <= 550 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 1.50 && sectionRHS.Base / sectionRHS.Height <= 1.50)
                    if (lambda <= 515 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 1.67 && sectionRHS.Base / sectionRHS.Height <= 1.67)
                    if (lambda <= 435 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 1.75 && sectionRHS.Base / sectionRHS.Height <= 1.75)
                    if (lambda <= 410 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 1.80 && sectionRHS.Base / sectionRHS.Height <= 1.80)
                    if (lambda <= 395 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 2.00 && sectionRHS.Base / sectionRHS.Height <= 2.00)
                    if (lambda <= 340 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 2.50 && sectionRHS.Base / sectionRHS.Height <= 2.5)
                    if (lambda <= 275 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 3.00 && sectionRHS.Base / sectionRHS.Height <= 3.0)
                    if (lambda <= 225 * Math.Pow(Epsilon, 2))
                        return false;
                if (sectionRHS.Height / sectionRHS.Base <= 4.00 && sectionRHS.Base / sectionRHS.Height <= 4.0)
                    if (lambda <= 170 * Math.Pow(Epsilon, 2))
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
                bw = section.Wel1 / section.Wpl1;
            else        //class4
                bw = section.Wel1 / section.Wpl1;

            if (section is SectionCHS || section is SectionRHS)
            {
                double gammab = Math.Max((1 - (section.J22 / section.J11)) * (1 - (section.Jt / (2.6 * section.J11))), 0.0);
                double phiB = Math.Sqrt((Math.Pow(section.Wpl1, 2) * gammab) /(section.Area * section.Jt));
                return Math.Max(2.25 * Math.Sqrt(bw * gammab * CalculateLambdaForLatTorsBuckling(section) * phiB), 1.0);
            }
            else
            {
                // CopSuos2011 Chapter 8.3.5.3 u parameter
                double u;
                if (section.SectionType == Section.SectionTypes.Rolled)
                    u = 0.9;
                else if (section.SectionType == Section.SectionTypes.Welded)
                    u = 1.0;
                else
                    throw new NotImplementedException("CalculateUForLatTorsBuckling: not implemented SectionType");

                //CopSuos2011 Chapter 8.3.5.3 x parameter
                double x;
                if (section is SectionH sec)
                    x = sec.Height / ((sec.ThicknessBottomFlange + sec.ThicknessTopFlange) / 2);
                else if (section is SectionC sectionC)
                    x = sectionC.Height / ((sectionC.ThicknessBottom + sectionC.ThicknessTop) / 2);
                else
                    throw new NotImplementedException("CalculateXForLatTorsBuckling: CopSuos2011 not implemented x coefficient for this section");

                double v;
                // CopSuos2011 Chapter 8.3.5.3 equation 8.27
                if (section is SectionH || section is SectionC)
                    v = 1 / Math.Pow(1 + 0.05 * (Math.Pow(CalculateLambdaForLatTorsBuckling(section) / x, 2)), 0.25);
                else
                    throw new NotImplementedException("CalculateXForLatTorsBuckling: CopSuos2011 not implemented v coefficient for this section");    
                
                return u * v * CalculateLambdaForLatTorsBuckling(section) * Math.Sqrt(bw);
            }
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
                le = GetLengthLatTorsBuckling();
            else if (((Cop2011Checker.Cop2011Options)_options).LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.FullyRestrained)
                le = 0.8 * GetLengthLatTorsBuckling();
            else if (((Cop2011Checker.Cop2011Options)_options).LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Unrestrained)
                le = 1.2 * GetLengthLatTorsBuckling() + 2 * section.Height;
            else if (((Cop2011Checker.Cop2011Options)_options).LateralTorsionalBucklingCondition == Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad)
                le = 1.2 * GetLengthLatTorsBuckling();
            else
                throw new NotImplementedException("GetLeForLatTorsBuckling: not implemented LateralTorsionalBucklingConditions");

            return le / Math.Min(section.R11, section.R22);
        }

        #endregion

        #region Section Class

        /// <summary>
        /// Return the <see cref="SectionClass"/> due of bending compression of the section <paramref name="section"/> with the <paramref name="resultBeamForces"/> - Chapter 7
        /// </summary>
        /// <returns></returns>
        private SectionClass CalculateSectionClassDueToBending(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (((Section)section).GetMinSigma(resultBeamForces.N, resultBeamForces.M1, resultBeamForces.M2) < 0.0)
            {
                if (section is SectionH sectionH)
                {
                    SectionClass sectionClass = SetWorstClass(new SectionClass[] {
                        GetClassCompressedOuterFlangeBending(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, section, out double reduction1),
                        GetClassCompressedOuterFlangeBending(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, section, out double reduction2),
                        GetClassCompressedWebBendingMoment(sectionH.HeightWeb, sectionH.ThicknessWeb, resultBeamForces, section, out double reduction3)});
                    _beta = Math.Min(Math.Min(Math.Min(reduction1, reduction2), reduction3), _beta);
                    return sectionClass;
                }
                else if (section is SectionCHS sectionCHS)
                {
                    SectionClass sectionClass = GetClassCHSBending(sectionCHS.Diameter, sectionCHS.Thickness, out double reduction1);
                    _beta = Math.Min(reduction1, _beta);
                    return sectionClass;
                }
                else if (section is SectionRHS sectionRHS)
                {
                    if (Math.Abs(resultBeamForces.M2) >= Math.Abs(resultBeamForces.M1))
                    {
                        //flange are load with constant load     //classification webs                           //from equilibrium of Σ sigma = Ned
                        if (sectionRHS.ThicknessTop == sectionRHS.ThicknessBottom && sectionRHS.ThicknessWebLeft == sectionRHS.ThicknessWebRight)
                        {
                            SectionClass sectionClass = SetWorstClass(new SectionClass[] {
                                GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, resultBeamForces, section, out double reduction1),
                                GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, sectionRHS.Heightinternal, section, out double reduction2)});
                            _beta = Math.Min(Math.Min(reduction1, reduction2), _beta);
                            return sectionClass;
                        }
                        else
                        {
                            SectionClass sectionClass = SetWorstClass(new SectionClass[] {
                                GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, sectionRHS.Heightinternal, section, out double reduction1),
                                GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessTop, sectionRHS.Heightinternal, section, out double reduction2),
                                GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, resultBeamForces, section, out double reduction3),
                                GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight, resultBeamForces, section, out double reduction4) });
                            _beta = Math.Min(Math.Min(Math.Min(Math.Min(reduction1, reduction2), reduction3), reduction4), _beta);
                            return sectionClass;
                        }
                    }
                    else // if (Math.Abs(resultBeamForces.M1) >= Math.Abs(resultBeamForces.M2))
                    {
                        if (sectionRHS.ThicknessWebLeft == sectionRHS.ThicknessWebRight && sectionRHS.ThicknessTop == sectionRHS.ThicknessBottom)
                        {
                            SectionClass sectionClass = SetWorstClass(new SectionClass[]{
                                GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, resultBeamForces, section, out double reduction1),
                                GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessTop, sectionRHS.Heightinternal, section, out double reduction2)});
                            _beta = Math.Min(Math.Min(reduction1, reduction2), _beta);
                            return sectionClass;
                        }
                        else
                        {
                            SectionClass sectionClass = SetWorstClass(new SectionClass[] {
                                GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessTop, sectionRHS.Heightinternal, section, out double reduction1),
                                GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, sectionRHS.Heightinternal, section, out double reduction2),
                                GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, resultBeamForces, section, out double reduction3),
                                GetClassCompressedWebRHS(sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight, resultBeamForces, section, out double reduction4) });
                            _beta = Math.Min(Math.Min(Math.Min(Math.Min(reduction1, reduction2), reduction3), reduction4), _beta);
                            return sectionClass;
                        }
                    }
                }
                else if (section is SectionT sectionT)
                {
                    SectionClass sectionClass = SetWorstClass(new SectionClass[]{
                        GetClassCompressedOuterFlangeBending(sectionT.LenghtFlange / 2, sectionT.ThicknessFlange / 2, section, out double reduction1),
                        GetClassCompressedStemT(sectionT.Height, sectionT.ThicknessWeb, out double reduction2) });
                    _beta = Math.Min(Math.Min(reduction1, reduction2), _beta);
                    return sectionClass;
                }
                else if (section is SectionC sectionC)
                {
                    SectionClass sectionClass = SetWorstClass(new SectionClass[]{
                        GetClassCompressedWebChannel(sectionC.HeightWeb / 2, sectionC.ThicknessWeb / 2, out double reduction1),
                        GetClassCompressedOuterFlangeBending(sectionC.LengthBottom, sectionC.ThicknessBottom, section, out double reduction2),
                        GetClassCompressedOuterFlangeBending(sectionC.LengthTop, sectionC.ThicknessTop, section, out double reduction3)});
                    _beta = Math.Min(Math.Min(Math.Min(reduction1, reduction2), reduction3), _beta);
                    return sectionClass;
                }
                else if (section is SectionL sectionL)
                {
                    SectionClass sectionClass = SetWorstClass(new SectionClass[]{
                        GetClassCompressedOutstandLeg(sectionL.HorizontalLegLength, sectionL.HorizontalLegThickness, out double reduction1),
                        GetClassCompressedOutstandLeg(sectionL.VerticalLegLength, sectionL.VerticalLegThickness, out double reduction2)});
                    _beta = Math.Min(Math.Min(reduction1, reduction2), _beta);
                    return sectionClass;
                }
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
            if (((Section)section).GetMinSigma(resultBeamForces.N, resultBeamForces.M1, resultBeamForces.M2) < 0.0)
            {
                if (section is SectionH sectionH)
                {
                    SectionClass sectionClass = SetWorstClass(new SectionClass[]{
                        GetClassCompressedWebAxialCompression(sectionH.HeightWeb, sectionH.ThicknessWeb, resultBeamForces, section, out double reduction1),
                        GetClassCompressedOuterFlangeAxial(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, out double reduction2),
                        GetClassCompressedOuterFlangeAxial(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, out double reduction3) });
                    _beta = Math.Min(Math.Min(Math.Min(reduction1, reduction2), reduction3), _beta);
                    return sectionClass;
                }
                else if (section is SectionCHS sectionCHS)
                {
                    SectionClass sectionClass = GetClassCHSAxialCompression(sectionCHS.Diameter, sectionCHS.Thickness, out double reduction1);
                    _beta = Math.Min(reduction1, _beta);
                    return sectionClass;
                }

                else if (section is SectionRHS sectionRHS)
                {
                    SectionClass sectionClass = SetWorstClass(new SectionClass[] {
                        GetClassCompressedWebAxialCompression(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, resultBeamForces, section, out double reduction1),
                        GetClassCompressedWebAxialCompression(sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight, resultBeamForces, section, out double reduction2),
                        GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessTop, sectionRHS.Heightinternal, section, out double reduction3),
                        GetClassCompressedFlangeRHS(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, sectionRHS.Heightinternal, section, out double reduction4) });
                    _beta = Math.Min(Math.Min(Math.Min(Math.Min(reduction1, reduction2), reduction3), reduction4), _beta);
                    return sectionClass;
                }
                else if (section is SectionT sectionT)
                {
                    SectionClass sectionClass = SetWorstClass(new SectionClass[]{
                        GetClassCompressedOuterFlangeAxial(sectionT.LenghtFlange / 2, sectionT.ThicknessFlange / 2, out double reduction1),
                        GetClassCompressedStemT(sectionT.Height, sectionT.ThicknessWeb, out double reduction2) });
                    _beta = Math.Min(Math.Min(reduction1, reduction2), _beta);
                    return sectionClass;
                }
                else if (section is SectionC sectionC)
                {
                    SectionClass sectionClass = SetWorstClass(new SectionClass[]{
                        GetClassCompressedWebChannel(sectionC.HeightWeb / 2, sectionC.ThicknessWeb / 2, out double reduction1),
                        GetClassCompressedOuterFlangeAxial(sectionC.LengthBottom, sectionC.ThicknessBottom, out double reduction2),
                        GetClassCompressedOuterFlangeAxial(sectionC.LengthTop, sectionC.ThicknessTop, out double reduction3)});
                    _beta = Math.Min(Math.Min(Math.Min(reduction1, reduction2), reduction3), _beta);
                    return sectionClass;
                }
                else if (section is SectionL sectionL)
                {
                    SectionClass sectionClass = SetWorstClass(new SectionClass[]{
                        GetClassCompressedOuterFlangeAxial(sectionL.HorizontalLegLength, sectionL.HorizontalLegThickness, out double reduction1),
                        GetClassCompressedOuterFlangeAxial(sectionL.VerticalLegLength, sectionL.VerticalLegThickness, out double reduction2)});
                    _beta = Math.Min(Math.Min(reduction1, reduction2), _beta);
                    return sectionClass;
                }
                else
                    throw new NotImplementedException("CalculateSectionClassException: not implemented Section");
            }
            else
                return SectionClass.Class3;
        }

		/// <summary>
		/// CopSuos2011 Table 7.1 Flange, outstand element, bending moment
		/// </summary>
		/// <param name="b"></param>
		/// <param name="t"></param>
		/// <param name="section"></param>
		/// <param name="strengthReduction"></param>
		/// <returns></returns>
		private SectionClass GetClassCompressedOuterFlangeBending(double b, double t, ISteelSection section, out double strengthReduction)
        {
            if (t <= 0 || b <= 0)
                throw new ArgumentException();

            strengthReduction = 1;
            double ctRatio = b / t;
            if (section.SectionType == Section.SectionTypes.Rolled)
            {
                if (ctRatio <= 9.0 * Epsilon)
                    return SectionClass.Class1;
                else if (ctRatio <= 10.0 * Epsilon)
                    return SectionClass.Class2;
                else if (ctRatio <= 15.0 * Epsilon)
                    return SectionClass.Class3;
                else
                {
                    strengthReduction = Math.Abs((15.0 * Epsilon) / ctRatio);
                    return SectionClass.Class4;
                }
            }
            else if (section.SectionType == Section.SectionTypes.Welded)
            {
                if (ctRatio <= 8.0 * Epsilon)
                    return SectionClass.Class1;
                else if (ctRatio <= 9.0 * Epsilon)
                    return SectionClass.Class2;
                else if (ctRatio <= 13.0 * Epsilon)
                    return SectionClass.Class3;
                else
                {
                    strengthReduction = Math.Abs((13.0 * Epsilon) / ctRatio);
                    return SectionClass.Class4;
                }
            }
            else
                throw new NotImplementedException("Calculate Section Class Exception: not implemented Section for GetClassCompressedOuterFlangeBending");
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, outstand element, axial compression
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedOuterFlangeAxial(double b, double t, out double strengthReduction)
        {
            if (t <= 0 || b <= 0)
                throw new ArgumentException();

            strengthReduction = 1;
            double ctRatio = b / t;
            if (ctRatio <= 13.0 * Epsilon)
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs((13.0 * Epsilon) / ctRatio);
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, internal element, bending moment
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedInternalFlangeBending(double b, double t, out double strengthReduction)
        {
            if (t <= 0 || b <= 0)
                throw new ArgumentException();

            strengthReduction = 1;
            double ctRatio = b / t;
            if (ctRatio <= 28.0 * Epsilon)
                return SectionClass.Class1;
            else if (ctRatio <= 32.0 * Epsilon)
                return SectionClass.Class2;
            else if (ctRatio <= 40.0 * Epsilon)
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs((40.0 * Epsilon) / ctRatio);
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, internal element, axial compression
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedInternalFlangeAxial(double b, double t, out double strengthReduction)
        {
            if (t <= 0 || b <= 0)
                throw new ArgumentException();

            strengthReduction = 1;
            double ctRatio = b / t;
            if (ctRatio <= 40.0 * Epsilon)
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs((40.0 * Epsilon) / ctRatio);
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Web of I, H, Box section. Generally
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebBendingMoment(double b, double t, ResultBeamForces resultBeamForces, ISteelSection section, out double strengthReduction)
        {
            if (t <= 0 || b <= 0)
                throw new ArgumentException();

            strengthReduction = 1;
            double ctRatio = b / t;
            if (ctRatio <= Math.Max(80.0 * Epsilon / (1.0 + GetR1(section, resultBeamForces)), 40.0 * Epsilon))
                return SectionClass.Class1;
            if (GetR1(section, resultBeamForces) <= 0 &&
                (ctRatio <= Math.Max(100.0 * Epsilon / (1.0 + GetR1(section, resultBeamForces)), 40.0 * Epsilon)))
                return SectionClass.Class2;
            if (GetR1(section, resultBeamForces) >= 0 &&
                (ctRatio <= Math.Max(100.0 * Epsilon / (1.0 + 1.5 * GetR1(section, resultBeamForces)), 40.0 * Epsilon)))
                return SectionClass.Class2;
            else if (ctRatio <= Math.Max(120.0 * Epsilon / (1.0 + 2.0 * GetR2(resultBeamForces, section)), 40.0 * Epsilon))
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs(Math.Max(120.0 * Epsilon / (1.0 + 2.0 * GetR2(resultBeamForces, section)), 40.0 * Epsilon) / ctRatio);
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Web of I, H, Box section. Axial Compression
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebAxialCompression(double b, double t, ResultBeamForces resultBeamForces, ISteelSection section, out double strengthReduction)
        {
            if (t <= 0 || b <= 0)
                throw new ArgumentException();

            strengthReduction = 1;
            double ctRatio = b / t;            
            if (section.FormedType == Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= Math.Max(120.0 * Epsilon / (1.0 + 2.0 * GetR2(resultBeamForces, section)), 40.0 * Epsilon))
                    return SectionClass.Class3;
                else
                {
                    strengthReduction = Math.Abs(Math.Max(120.0 * Epsilon / (1.0 + 2.0 * GetR2(resultBeamForces, section)), 40.0 * Epsilon) / ctRatio);
                    return SectionClass.Class4;
                }
            }
            else
            {
                if (ctRatio <= Math.Max(105.0 * Epsilon / (1.0 + 2.0 * GetR2(resultBeamForces, section)), 35.0 * Epsilon))
                    return SectionClass.Class3;
                else
                {
                    strengthReduction = Math.Abs(Math.Max(105.0 * Epsilon / (1.0 + 2.0 * GetR2(resultBeamForces, section)), 35.0 * Epsilon) / ctRatio);
                    return SectionClass.Class4;
                }
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 CHS Classification
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCHSBending(double d, double t, out double strengthReduction)
        {
            if (t <= 0 || d <= 0)
                throw new ArgumentException();

            strengthReduction = 1;
            double ctRatio = d / t;
            if (ctRatio <= 40.0 * Epsilon * Epsilon)
                return SectionClass.Class1;
            else if (ctRatio <= 50.0 * Epsilon * Epsilon)
                return SectionClass.Class2;
            else if (ctRatio <= 140 * Epsilon * Epsilon)
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs(140 * Epsilon * Epsilon / ctRatio);
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 CHS Classification
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCHSAxialCompression(double d, double t, out double strengthReduction)
        {
            if (t <= 0 || d <= 0)
                throw new ArgumentException();

            strengthReduction = 1;
            double ctRatio = d / t;
            if (ctRatio <= 80.0 * Epsilon * Epsilon)
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs(80 * Epsilon * Epsilon / ctRatio);
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Flange
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedFlangeRHS(double b, double t, double d, ISteelSection section, out double strengthReduction)
        {
            if (t <= 0 || d <= 0)
                throw new ArgumentException();

            double ctRatio = b / t;
            strengthReduction = 1;
            if (section.FormedType == Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= Math.Min(28.0 * Epsilon, 80 * Epsilon - d / t))
                    return SectionClass.Class1;
                else if (ctRatio <= Math.Min(32.0 * Epsilon, 62.0 * Epsilon - 0.5 * d / t))
                    return SectionClass.Class2;
                else if (ctRatio <= 40.0 * Epsilon)
                    return SectionClass.Class3;
                else
                {
                    strengthReduction = Math.Abs(40.0 * Epsilon / ctRatio);
                    return SectionClass.Class4;
                }
            }
            else if (section.FormedType == Section.FormedTypes.ColdFormed)
            {
                if (ctRatio <= Math.Min(26.0 * Epsilon, 72.0 * Epsilon - d / t))
                    return SectionClass.Class1;
                else if (ctRatio <= Math.Min(28.0 * Epsilon, 54 * Epsilon - 0.5 * d / t))
                    return SectionClass.Class2;
                else if (ctRatio <= 35.0 * Epsilon)
                    return SectionClass.Class3;
                else
                {
                    strengthReduction = Math.Abs(35.0 * Epsilon / ctRatio);
                    return SectionClass.Class4;
                }
            }
            else
                throw new NotImplementedException("CalculateSectionClassException: not implemented Section for GetClassCompressedFlangeRHS");
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Web
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebRHS(double d, double t, ResultBeamForces resultBeamForces, ISteelSection section, out double strengthReduction)
        {
            if (t <= 0 || d <= 0)
                throw new ArgumentException();

            double ctRatio = d / t;
            strengthReduction = 1;
            if (section.FormedType == Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= Math.Max(64.0 * Epsilon / (1.0 + 0.6 * GetR1(section, resultBeamForces)), 40.0 * Epsilon))
                    return SectionClass.Class1;
                else if (ctRatio <= Math.Max(80.0 * Epsilon / (1.0 + GetR1(section, resultBeamForces)), 40.0 * Epsilon))
                    return SectionClass.Class2;
                else if (ctRatio <= Math.Max(120.0 * Epsilon / (1.0 + 2.0 * GetR1(section, resultBeamForces)), 40.0 * Epsilon))
                    return SectionClass.Class3;
                else
                {
                    strengthReduction = Math.Abs(Math.Max(120.0 * Epsilon / (1.0 + 2.0 * GetR1(section, resultBeamForces)), 40.0 * Epsilon) / ctRatio);
                    return SectionClass.Class4;
                }
            }
            else if (section.FormedType == Section.FormedTypes.ColdFormed)
            {
                if (ctRatio <= Math.Max(56.0 * Epsilon / (1.0 + 0.6 * GetR1(section, resultBeamForces)), 35.0 * Epsilon))
                    return SectionClass.Class1;
                else if (ctRatio <= Math.Max(70.0 * Epsilon / (1.0 + GetR1(section, resultBeamForces)), 35.0 * Epsilon))
                    return SectionClass.Class2;
                else if (ctRatio <= Math.Max(105 * Epsilon / (1.0 + 2.0 * GetR1(section, resultBeamForces)), 35.0 * Epsilon))
                    return SectionClass.Class3;
                else
                {
                    strengthReduction = Math.Abs(Math.Max(105 * Epsilon / (1 + 2 * GetR1(section, resultBeamForces)), 35.0 * Epsilon) / ctRatio);
                    return SectionClass.Class4;
                }
            }
            else
                throw new NotImplementedException("CalculateSectionClassException: not implemented Section for GetClassCompressedWebRHS");
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Angle, compression due to bending and axial compression
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedAngleWithAxialCompression(double b, double d, double t, out double strengthReduction)
        {
            double ctRatio = d / t;
            double btRatio = b / t;
            strengthReduction = 1;

            if (btRatio < 15.0 * Epsilon && ctRatio < 15.0 * Epsilon && (b + d) / t < 24.0 * Epsilon)
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs(Math.Min(Math.Min(Math.Min(15.0 * Epsilon / btRatio, 15.0 * Epsilon / ctRatio), 24.0 * Epsilon / (b + d) / t), 1.0));
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Angle, compression due to bending
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedAngleOnlyBending(double b, double d, double t, out double strengthReduction)
        {
            double dtRatio = d / t;
            double btRatio = b / t;
            strengthReduction = 1;
            if (btRatio < 9.0 * Epsilon && dtRatio < 9.0 * Epsilon)
                return SectionClass.Class1;
            else if (btRatio < 10.0 * Epsilon && dtRatio < 10.0 * Epsilon)
                return SectionClass.Class2;
            else if (btRatio < 15.0 * Epsilon && dtRatio < 15.0 * Epsilon)
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs(Math.Min(Math.Min(15.0 * Epsilon / btRatio, 15.0 * Epsilon / dtRatio), 1.0));
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Outstand Leg of an angle 
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedOutstandLeg(double b, double t, out double strengthReduction)
        {
            double btRatio = b / t;
            strengthReduction = 1;
            if (btRatio < 9.0 * Epsilon)
                return SectionClass.Class1;
            else if (btRatio < 10.0 * Epsilon)
                return SectionClass.Class2;
            else if (btRatio < 15.0 * Epsilon)
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs(15.0 * Epsilon / btRatio);
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Stem of a T section
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedStemT(double d, double t, out double strengthReduction)
        {
            double dtRatio = d / t;
            strengthReduction = 1;
            if (dtRatio < 8.0 * Epsilon)
                return SectionClass.Class1;
            else if (dtRatio < 9.0 * Epsilon)
                return SectionClass.Class2;
            else if (dtRatio < 18.0 * Epsilon)
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs(18.0 * Epsilon / dtRatio);
                return SectionClass.Class4;
            }
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Channel of a C section
        /// </summary>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebChannel(double d, double t, out double strengthReduction)
        {
            double dtRatio = d / t;
            strengthReduction = 1;
            if (d / t < 40.0 * Epsilon)
                return SectionClass.Class3;
            else
            {
                strengthReduction = Math.Abs(40.0 * Epsilon / dtRatio);
                return SectionClass.Class4;
            }
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
                    double sigma1 = (-resultBeamForces.N / section.Area) + (resultBeamForces.M1 / sectionH.WelXMax);
                    double sigma2 = (-resultBeamForces.N / section.Area) + (resultBeamForces.M1 / sectionH.WelXMin);
                    return (sigma1 + sigma2) / (2 * Py);
                }
            }

            else if (section is SectionRHS _)
                return (-resultBeamForces.N / (section.Area * Py));

            else
                throw new NotImplementedException("Cop2011 R2 factor (§7.3) not supported Section type");
        }

        #endregion

        #region py

        /// <summary>
        /// Return the py value 
        /// </summary>
        /// <returns></returns>
        private double GetPy(double fyk, double fu)
        {
            if (fyk < 460)
                return Math.Min(fyk / GetGammaM1(fyk), fu / GetGammaM2(fyk));
            else            
                return Math.Min(fyk / GetGammaM1(fyk), fu / GetGammaM2(fyk));
        }

        /// <summary>
        /// Return the GammaM1 factor for reduce the steel fy. CopSuos 2011 Chapter 4 table 4.1
        /// </summary>
        /// <returns></returns>
        private double GetGammaM1(double fyk)
        {
            if (fyk < 460)
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
            if (fyk > 460 && fyk < 690)
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
        private double GetGammaM2(double fyk)
        {
            if (fyk < 460)
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
            if (fyk > 460 && fyk < 690)
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

        #region Equals, hashcode, operators

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is Cop2011Checker checker &&
				   base.Equals(obj) &&
				   _py == checker._py &&
				   _beta == checker._beta &&
				   _epsilon == checker._epsilon;
		}

		public override int GetHashCode()
		{
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _py.GetHashCode();
                hashCode = hashCode * -17 + _beta.GetHashCode();
                hashCode = hashCode * -17 + _epsilon.GetHashCode();
                return hashCode;
            }
		}

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Epsilon", _epsilon);
            info.AddValue("Beta", _beta);
            info.AddValue("Py", _py);
        }

        #endregion

        #region Nested class Options

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

		#endregion
	}
}
