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
using GPC.Model.Materials;

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

        #region Properties

        public StandardEN1993p11 EN1993P11 => (StandardEN1993p11)_standard;

        public double GammaM0 => EN1993P11.GammaM0;

        public double GammaM1 => EN1993P11.GammaM1;

        public double GammaM2 => EN1993P11.GammaM2;

        public SteelMaterial Material => _beamCheckerAttributes.Sections.FirstOrDefault().SteelMaterial;

        public EN1993p11BeamStationResult[] EN1993p11BeamStationResults => _beamStationResults.Cast<EN1993p11BeamStationResult>().ToArray();

        public double Fy => Material.Fyk;

        public double Fu => Material.Fu;

        public double Epsilon => Math.Sqrt(235 / Fy);

		#endregion

		#region Constructor

		public EN1993p11Checker(BeamCheckerAttributes attributes, EN1993p11Checker.EN1993p11Options options, StandardEN1993p11 standardEN1993P11) 
            : base(attributes, options, standardEN1993P11)
        {

        }

		#endregion

		public override void PerformCheck()
        {
            _beamStationResults = PerformCheck(_beamCheckerAttributes.Sections, _beamCheckerAttributes.Results, (EN1993p11Options)_options);
        }

        public async void PerformCheckAsync()
        {
            await Task.Run(() =>
            {
                _beamStationResults = PerformCheck(_beamCheckerAttributes.Sections, _beamCheckerAttributes.Results, (EN1993p11Options)_options);
            });
        }

		/// <param name="steelSection">section of each station</param>
		/// <param name="beamResult">result for each station and loadcase</param>
		/// <param name="options"></param>
		/// <returns></returns>
		protected EN1993p11BeamStationResult[] PerformCheck(ISteelSection[] steelSection, BeamResult[] beamResult, EN1993p11Options options)
        {
            EN1993p11BeamStationResult[] stationResults = new EN1993p11BeamStationResult[steelSection.Length * beamResult.Select(i => i.ResultLocations.Length).Sum()];
            int index = 0;

            for (int k = 0; k < beamResult.Length; k++)
            {
                for (int j = 0; j < beamResult[k].ResultLocations.Length; j++)
                {
                    for (int i = 0; i < beamResult[k].ResultLocations[j].ResultTypes.Length; i++)
                    {
                        try
                        {
                            ResultBeamForces rbf = (ResultBeamForces)beamResult[k].ResultLocations[j].ResultTypes[i];

                            ResultLocationStation resultLocationStation = new ResultLocationStation(new ResultBeamForces[] { rbf },
                                ((ResultLocationStation)beamResult[k].ResultLocations[j]).DistanceFromStartPoint,
                                ((ResultLocationStation)beamResult[k].ResultLocations[j]).ElementLenght);

                            stationResults[index] = new EN1993p11BeamStationResult(steelSection[i], resultLocationStation,
                                beamResult[k].Case, (StandardEN1993p11)_standard, (EN1993p11Options)_options, BeamName);

                            SectionClass axialCompSectionClass;
                            SectionClass bendingCompSectionClass;
                            SectionClass sectionClass;

                            double axialTensionRd;
                            double axialCompressionRd;
                            double shear2Rd;
                            double shear1Rd;
                            double axialBuck1Rd;
                            double axialBuck2Rd;
                            double bending1Rd;
                            double bending2Rd;
                            double latTorsRd;

                            double axialTensionWR;
                            double axialCompressionWR;
                            double axialBuck1WR;
                            double axialBuck2WR;
                            double shear1WR;
                            double shear2WR;
                            double bending1WR;
                            double bending2WR;
                            double latTorsWR;
                            double crossSectionInteraction;
                            double bucklingInteraction1;
                            double bucklingInteraction2;
                            double flextureTorsionInteraction;

                            axialCompSectionClass = CalculateSectionClassDueToCompression(rbf, steelSection[i]);
                            bendingCompSectionClass = CalculateSectionClassDueToBending(rbf, steelSection[i]);

                            if (((Section)steelSection[i]).GetMinSigma(rbf.N, rbf.M1, rbf.M2) >= 0.0)
                                sectionClass = bendingCompSectionClass;
                            else
                            {
                                if (((Section)steelSection[i]).GetMaxSigma(rbf.N, rbf.M1, rbf.M2) > 0.0)
                                    sectionClass = CalculateSectionClassDueToCombinedBendingAndCompression(rbf, steelSection[i]);
                                else
                                    sectionClass = axialCompSectionClass;
                            }

                            axialTensionRd = CalculateAxialTensionCapacity(steelSection[i]);
                            axialTensionWR = GetWorkingRatio(Math.Max(rbf.N, 0), axialTensionRd);

                            axialCompressionRd = CalculateAxialCompressionCapacity(sectionClass, steelSection[i]);
                            axialCompressionWR = GetWorkingRatio(Math.Min(rbf.N, 0), axialCompressionRd);

                            shear1Rd = CalculateShear1Capacity(rbf, steelSection[i]);
                            shear1WR = GetWorkingRatio(Math.Abs(rbf.V1), shear1Rd);

                            shear2Rd = CalculateShear2Capacity(rbf, steelSection[i]);
                            shear2WR = GetWorkingRatio(Math.Abs(rbf.V2), shear2Rd);

                            axialBuck1Rd = CalculateAxialBucklingCapacity1Axis(rbf.N, sectionClass, steelSection[i]);

                            if (((Section)steelSection[i]).GetMinSigma(rbf.N, rbf.M1, rbf.M2) < 0.0)
                                axialBuck1WR = GetWorkingRatio(Math.Min(rbf.N, 0), axialBuck1Rd);
                            else
                                axialBuck1WR = 0.001;

                            axialBuck2Rd = CalculateAxialBucklingCapacity2Axis(rbf.N, sectionClass, steelSection[i]);
                            if (((Section)steelSection[i]).GetMinSigma(rbf.N, rbf.M1, rbf.M2) < 0.0)
                                axialBuck2WR = GetWorkingRatio(Math.Min(rbf.N, 0), axialBuck2Rd);
                            else
                                axialBuck2WR = 0.001;

                            bending1Rd = CalculateBendingMoment1Capacity(rbf, sectionClass, steelSection[i]);
                            bending1WR = GetWorkingRatio(rbf.M1, bending1Rd);

                            bending2Rd = CalculateBendingMoment2Capacity(rbf, sectionClass, steelSection[i]);
                            bending2WR = GetWorkingRatio(rbf.M2, bending2Rd);

                            latTorsRd = CalculateLateralTorsionalBucklingMomentCapacity(rbf, sectionClass, steelSection[i], options);

                            double kwLTB = GetKwForLTB(options.LateralWarpingCondition);
                            double kc = Getkc(options.SupportCondition, options.LoadCondition, options.Psi1Axis);
                            double ncrt = GetNcrT(steelSection[i]);
                            double ncr1 = GetNcrEuler(GetLengthAxialBuckling1(), steelSection[i].J11);
                            double ncr2 = GetNcrEuler(GetLengthAxialBuckling2(), steelSection[i].J22);
                            double ncrtf = GetNcrTF(steelSection[i], ncr1, ncr2, ncrt);

                            double Mcr = CalculateMcr(steelSection[i], rbf, GetLengthCriticalMoment1(), options.SupportCondition, options.LoadCondition,
                                options.LateralSupportCondition, options.LateralWarpingCondition, options.Psi1Axis, options.LoadApplicationPoint);
                            double Mcr0 = CalculateMcr0ForInteraction(steelSection[i], rbf, options.SupportCondition, options.LoadCondition, options.LateralSupportCondition,
                                options.LateralWarpingCondition, options.LoadApplicationPoint);

                            double lambda0limit = CalculateLambda0LimitForFlexuralTorsionaBuckling(rbf, kc, ncrtf, ncrt);
                            double lambdaSignedLTB = GetLambdaSignedLTB(steelSection[i], Mcr);
                            double lambdaSignedLTB0 = GetLambdaSignedLTB(steelSection[i], Mcr0);

                            double alphaLTB = GetImperfectionFactorLT(steelSection[i]);
                            double phiLT = GetPhiForBuckling(alphaLTB, lambdaSignedLTB, EN1993P11.BetaForLateralTorsionalBuckling, EN1993P11.LambdaLT0ForLateralTorsionalBuckling);
                            double chiLT = GetChiLTmod(phiLT, lambdaSignedLTB, EN1993P11.BetaForLateralTorsionalBuckling, 1.0);

                            if (lambdaSignedLTB0 < lambda0limit)
                                latTorsWR = 0.001;
                            else
                                latTorsWR = GetWorkingRatio(rbf.M1, latTorsRd);

                            #region Cross section resistence §EC3 6.2.1

                            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                            {
                                if (steelSection[i] is SteelSectionH || steelSection[i] is SteelSectionRHS)
                                {
                                    CalculateMNRd1(sectionClass, steelSection[i], rbf.N, axialBuck1Rd, axialBuck2Rd, axialCompressionRd,
                                        bending1Rd, bending2Rd, out double mNxRd, out double mNyRd);

                                    if (steelSection[i] is SteelSectionH)
                                    {
                                        double n = Math.Abs(rbf.N) / axialBuck1Rd;

                                        double alpha = 2;
                                        double beta = Math.Max(5 * n, 1);

                                        crossSectionInteraction = Math.Pow(rbf.M1 / mNxRd, alpha) + Math.Pow(rbf.M2 / mNyRd, beta);
                                    }
                                    else if (steelSection[i] is SteelSectionRHS)
                                    {
                                        double n = rbf.N / axialBuck1Rd;

                                        double alpha = Math.Min(1.66 / (1 - 1.13 * Math.Pow(n, 2)), 6);
                                        double beta = alpha;

                                        crossSectionInteraction = Math.Pow(rbf.M1 / mNxRd, alpha) +
                                            Math.Pow(rbf.M2 / mNyRd, beta);
                                        //TODO: completare con torsione
                                    }
                                    else
                                    {
                                        crossSectionInteraction = 0;
                                    }
                                }
                                else if (steelSection[i] is SteelSectionCHS)
                                {
                                    crossSectionInteraction = Math.Abs(rbf.N / axialCompressionRd) +
                                        Math.Pow(Math.Pow(Math.Abs(rbf.M1 / bending1Rd), 2) +
                                        Math.Pow(Math.Abs(rbf.M2 / bending2Rd), 2), 0.5);
                                }
                                else if (steelSection[i] is SteelSectionT)
                                {
                                    crossSectionInteraction = Math.Abs(rbf.N / axialCompressionRd) +
                                        Math.Pow(Math.Abs(rbf.M1 / bending1Rd), 2) +
                                        Math.Pow(Math.Abs(rbf.M2 / bending2Rd), 2);
                                }
                                else
                                {
                                    crossSectionInteraction= Math.Abs(rbf.N / axialCompressionRd) +
                                        Math.Abs(rbf.M1 / bending1Rd) +
                                        Math.Abs(rbf.M2 / bending2Rd);
                                }
                            }
                            else if (sectionClass == SectionClass.Class3)
                            {
                                if (steelSection[i] is SteelSectionCHS)
                                {
                                    crossSectionInteraction = Math.Abs(rbf.N / axialCompressionRd) +
                                        Math.Pow(Math.Pow(Math.Abs(rbf.M1 / bending1Rd), 2) +
                                        Math.Pow(Math.Abs(rbf.M2 / bending2Rd), 2), 0.5);
                                }
                                else if (steelSection[i] is SteelSectionH || steelSection[i] is SteelSectionRHS)
                                {
                                    if (Math.Abs(rbf.T) > 1)
                                    {
                                        crossSectionInteraction = 0;
                                        // TODO: implementare
                                    }
                                    else
                                    {
                                        crossSectionInteraction = Math.Abs(rbf.N / axialCompressionRd) +
                                            Math.Abs(rbf.M1 / bending1Rd) +
                                            Math.Abs(rbf.M2 / bending2Rd);
                                    }
                                }
                                else
                                {
                                    crossSectionInteraction = Math.Abs(rbf.N / axialCompressionRd) +
                                        Math.Abs(rbf.M1 / bending1Rd) +
                                        Math.Abs(rbf.M2 / bending2Rd);
                                }
                            }
                            else
                            {
                                crossSectionInteraction = 0;
                                // TODO: implementare Classe 4
                            }

                            #endregion

                            #region Interaction Coefficients

                            CalculateCoefficientsForInteraction(steelSection[i], sectionClass, rbf, options.LoadCondition,
                                options.SupportCondition, options.LateralSupportCondition, options.LateralWarpingCondition, options.LoadApplicationPoint, options.Psi1Axis,
                                out double epsilony,
                                out double bLT, out double cLT, out double dLT, out double eLT,
                                out double cxx, out double cxy, out double cyx, out double cyy,
                                out double cmx, out double cmy, out double cmLT, out double mux,
                                out double muy, out double wx, out double wy);

                            CalculateKCoefficientForInteraction(steelSection[i], sectionClass, rbf, options.LoadCondition,
                                options.SupportCondition, options.LateralSupportCondition, options.LateralWarpingCondition, options.LoadApplicationPoint, options.Psi1Axis,
                                out double kxx, out double kxy, out double kyx, out double kyy);

                            double chiX;
                            double lambdaSegnato1 = GetLambdaSigned(sectionClass, steelSection[i], ncr1);

                            EN1993p11Options.AxialBuckingCurves buckingCurve1 = GetBucklingCurve1Axis(steelSection[i]);
                            double alpha1 = GetImperfectionFactorBucklingCurve(buckingCurve1);

                            double phi1 = GetPhiForBuckling(alpha1, lambdaSegnato1);
                            chiX = GetChi(phi1, lambdaSegnato1);

                            if (lambdaSegnato1 <= 0.2)
                                chiX = 1.0;

                            stationResults[i + k * steelSection.Length].SetResultsForReportAxialBuckling1Axis(chiX, phi1, lambdaSegnato1, alpha1, ncr1, buckingCurve1);


                            double chiY;
                            double lambdaSegnato2 = GetLambdaSigned(sectionClass, steelSection[i], ncr2);

                            EN1993p11Options.AxialBuckingCurves buckingCurve2 = GetBucklingCurve2Axis(steelSection[i]);
                            double alpha2 = GetImperfectionFactorBucklingCurve(buckingCurve2);

                            double phi2 = GetPhiForBuckling(alpha2, lambdaSegnato2);
                            chiY = GetChi(phi2, lambdaSegnato2);

                            if (lambdaSegnato2 <= 0.2)
                                chiY = 1.0;

                            stationResults[i + k * steelSection.Length].SetResultsForReportAxialBuckling2Axis(chiY, phi2, lambdaSegnato2, alpha2, ncr2, buckingCurve2);


                            double axialcompRk;
                            if (sectionClass != SectionClass.Class4)
                                axialcompRk = steelSection[i].Area * Fy;
                            else
                                axialcompRk = steelSection[i].Area * Fy;       // TODO: implementare

                            double bending1Rk;
                            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                                bending1Rk = steelSection[i].Wpl1 * Fy;
                            else if (sectionClass == SectionClass.Class3)
                                bending1Rk = steelSection[i].Wel1 * Fy;
                            else
                                bending1Rk = steelSection[i].Wel1 * Fy;       // TODO: implementare

                            double bending2Rk;
                            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                                bending2Rk = steelSection[i].Wpl2 * Fy;
                            else if (sectionClass == SectionClass.Class3)
                                bending2Rk = steelSection[i].Wel2 * Fy;
                            else
                                bending2Rk = steelSection[i].Wel2 * Fy;       // TODO: implementare

                            double deltaM1;
                            if (sectionClass != SectionClass.Class4)
                                deltaM1 = 0;
                            else
                                deltaM1 = 0;       // TODO: implementare

                            double deltaM2;
                            if (sectionClass != SectionClass.Class4)
                                deltaM2 = 0;
                            else
                                deltaM2 = 0;       // TODO: implementare

                            double MwEd = 0.0;

                            #endregion

                            #region Buckling resistence interaction / Tension resistence interaction §EC3 6.3.3

                            if (rbf.N < 0.0)
                            {
                                if (sectionClass != SectionClass.Class4)
                                {
                                    bucklingInteraction1 = Math.Abs(rbf.N / (chiX * axialcompRk / GammaM1)) +
                                        Math.Abs(kxx * ((rbf.M1 + deltaM1) / (chiLT * bending1Rk / GammaM1))) +
                                        Math.Abs(kxy * ((rbf.M2 + deltaM2) / (bending2Rk / GammaM1)));

                                    bucklingInteraction2 = Math.Abs(rbf.N / (chiY * axialcompRk / GammaM1)) +
                                        Math.Abs(kyx * ((rbf.M1 + deltaM1) / (chiLT * bending1Rk / GammaM1))) +
                                        Math.Abs(kyy * ((rbf.M2 + deltaM2) / (bending2Rk / GammaM1)));
                                }
                                else
                                {
                                    bucklingInteraction1 = 0;
                                    bucklingInteraction2 = 0;
                                    // TODO: implementare
                                }
                            }
                            else
                            {
                                if (sectionClass != SectionClass.Class4)
                                {
                                    bucklingInteraction1 = Math.Abs(kxx * ((rbf.M1 + deltaM1) / (chiLT * bending1Rk / GammaM1))) +
                                        Math.Abs(kxy * ((rbf.M2 + deltaM2) / (bending2Rk / GammaM1)));

                                    bucklingInteraction2 = Math.Abs(kyx * ((rbf.M1 + deltaM1) / (chiLT * bending1Rk / GammaM1))) +
                                        Math.Abs(kyy * ((rbf.M2 + deltaM2) / (bending2Rk / GammaM1)));
                                }
                                else
                                {
                                    bucklingInteraction1 = 0;
                                    bucklingInteraction2 = 0;
                                    // TODO: implementare
                                }
                            }

                            #endregion

                            #region Flexture and Torsion Interaction §EN1993-6 Annex A

                            double kw = 0.7 - 0.2 * MwEd / (bending2Rk / GammaM1);
                            double kyw = 1 - rbf.M2 / bending2Rk;
                            double kAlpha = 1 / (1 - bending1Rk / Mcr);

                            flextureTorsionInteraction = Math.Abs((rbf.M1 + deltaM1) / (chiLT * bending1Rk / GammaM1)) +
                                cmy * (rbf.M2 + rbf.T) / (bending2Rk / GammaM1) +
                                kw * kyw * kAlpha * MwEd / (bending2Rk / 2 * GammaM1);

                            #endregion

                            #region Report

                            stationResults[i + k * steelSection.Length].SetClasses(sectionClass, axialCompSectionClass, bendingCompSectionClass);

                            stationResults[i + k * steelSection.Length].SetCapacity(axialTensionRd, axialCompressionRd, axialBuck1Rd, axialBuck2Rd, shear1Rd, shear2Rd,
                                bending1Rd, bending2Rd, latTorsRd);

                            stationResults[i + k * steelSection.Length].SetWorkingRatio(axialTensionWR, axialCompressionWR, axialBuck1WR, axialBuck2WR, shear1WR, shear2WR,
                                bending1WR, bending2WR, latTorsWR, crossSectionInteraction, bucklingInteraction1, bucklingInteraction2, flextureTorsionInteraction);

                            stationResults[i + k * steelSection.Length].SetBucklingLenght(GetLengthAxialBuckling1(), GetLengthAxialBuckling2(),
                                GetLengthLatTorsBuckling(), GetLengthCriticalMoment1(), GetLengthCriticalMoment2());


                            stationResults[i + k * steelSection.Length].SetResultsForReportLateralTorsionalBuckling(chiLT, phiLT, lambdaSignedLTB, lambdaSignedLTB0, alphaLTB, Mcr,
                                GetLateralTorsionalBucklingCurve(steelSection[i]), ncrt, ncrtf);

                            stationResults[i + k * steelSection.Length].SetResultForReportInteractionCoefficient(cmx, cmy, mux, muy, wx, wy, cyx, cyy, cmLT, bLT, cLT, dLT, eLT,
                                cxx, cxy, cyx, cyy, epsilony, kxx, kxy, kyx, kyy, kw, kyw, kAlpha);

                            #endregion

                            index++;
                        }
                        catch (Exception e)
                        {
                            _errorLog.Add($"Fail check beam {BeamName}, \n " +
                                $"station {((ResultLocationStation)beamResult[k].ResultLocations[i]).DistanceFromStartPoint} mm from start point, \n" +
                                $"combination {beamResult[k].Case.Name}. \n" +
                                $"Error: {e.Message}");
                        }
                    }
                }
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
            return section.Area * Fy / GammaM0;
        }

        private double CalculateNulRd(ISteelSection section)
        {
            return 0.90 * GetAreaNet(section) * Fu / GammaM2;
        }

        private double CalculateAxialCompressionCapacity(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass != SectionClass.Class4)
                return section.Area * Fy / GammaM0;
            else
                return GetAreaNet(section) * Fy / GammaM0;
        }

        #endregion

        #region Axial Buckling

        protected double CalculateAxialBucklingCapacity1Axis(double nEd, SectionClass sectionClass, ISteelSection section)
        {
            if (Math.Abs(nEd) / GetNcrEuler(GetLengthAxialBuckling1(), section.J11) <= 0.04)
                return CalculateAxialCompressionCapacity(sectionClass, section);

            else                
                return CalculateNbRd1(section, sectionClass);   
        }

        protected double CalculateAxialBucklingCapacity2Axis(double nEd, SectionClass sectionClass, ISteelSection section)
        {
            if (Math.Abs(nEd) / GetNcrEuler(GetLengthAxialBuckling2(), section.J22) <= 0.04)
                return CalculateAxialCompressionCapacity(sectionClass, section);

            else
                return CalculateNbRd2(section, sectionClass);   
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.1.1(3)
        /// </summary>
        protected double CalculateNbRd1(ISteelSection section, SectionClass sectionClass)
        {
            double ncr1 = GetNcrEuler(GetLengthAxialBuckling1(), section.J11);
            double lambdaSegnato = GetLambdaSigned(sectionClass, section, ncr1);
            double chi;

            if (lambdaSegnato <= 0.2)   
                chi = 1.0;
            else
            {
                EN1993p11Options.AxialBuckingCurves buckingCurves = GetBucklingCurve1Axis(section);
                double alpha = GetImperfectionFactorBucklingCurve(buckingCurves);

                double phi = GetPhiForBuckling(alpha, lambdaSegnato);
                chi = GetChi(phi, lambdaSegnato);
            }

            if (sectionClass != SectionClass.Class4)            
                return chi * section.Area * Fy / GammaM1;
            
            else            
                return chi * GetAreaEff(section) * Fy / GammaM1;        
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.1.1(2)
        /// </summary>
        protected double CalculateNbRd2(ISteelSection section, SectionClass sectionClass)
        {
            double ncr2 = GetNcrEuler(GetLengthAxialBuckling2(), section.J22);
            double lambdaSegnato = GetLambdaSigned(sectionClass, section, ncr2);
            double chi;

            if (lambdaSegnato <= 0.2)
                chi = 1.0;
            else
            {
                EN1993p11Options.AxialBuckingCurves buckingCurves = GetBucklingCurve2Axis(section);
                double alpha = GetImperfectionFactorBucklingCurve(buckingCurves);

                double phi = GetPhiForBuckling(alpha, lambdaSegnato);
                chi = GetChi(phi, lambdaSegnato);
            }

            if (sectionClass != SectionClass.Class4)
                return chi * section.Area * Fy / GammaM1;

            else
                return chi * GetAreaEff(section) * Fy / GammaM1;
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.1.2(2)
        /// </summary>
        protected double GetNcrEuler(double length, double J)
        {
            return Math.Pow(Math.PI, 2.0) * Material.E * J / (Math.Pow(length, 2.0));
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.1.2(1)
        /// </summary>
        protected double GetLambdaSigned(SectionClass sectionClass, ISteelSection section, double Ncr)
        {
            if(sectionClass != SectionClass.Class4)
                return Math.Pow(section.Area * Fy / Ncr, 0.5);
            else
                return Math.Pow(section.Area * Fy / Ncr, 0.5);  //TODO: area eff!
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.1.2(2)
        /// </summary>
        protected double GetChi(double phi, double lambdaSigned, double beta = 1.0)
        {
            return Math.Min(1.0 / (phi + Math.Pow(Math.Pow(phi, 2.0) - beta * Math.Pow(lambdaSigned, 2.0), 0.5)), 1.0);
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.1.2(2)
        /// </summary>
        protected double GetPhiForBuckling(double alphaImperfectionFactor, double lambdaSigned, double beta = 1.0, double lambda_LT0 = 0.2) //default beta=1, lambda_LT0 = 0.2
        {
            return 0.5 * (1 + alphaImperfectionFactor * (lambdaSigned - lambda_LT0) + beta * Math.Pow(lambdaSigned, 2.0));
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.1.2(2)
        /// </summary>
        protected double GetImperfectionFactorBucklingCurve(EN1993p11Options.AxialBuckingCurves buckingCurve)
        {
            if (buckingCurve == EN1993p11Options.AxialBuckingCurves.a0)
                return EN1993P11.AlphaImperfectionFactorForCurveA0;

            else if (buckingCurve == EN1993p11Options.AxialBuckingCurves.a)
                return EN1993P11.AlphaImperfectionFactorForCurveA;

            else if (buckingCurve == EN1993p11Options.AxialBuckingCurves.b)
                return EN1993P11.AlphaImperfectionFactorForCurveB;

            else if (buckingCurve == EN1993p11Options.AxialBuckingCurves.c)
                return EN1993P11.AlphaImperfectionFactorForCurveC;

            else if (buckingCurve == EN1993p11Options.AxialBuckingCurves.d)
                return EN1993P11.AlphaImperfectionFactorForCurveD;

            else
                throw new NotImplementedException("GetAlphaBucklingCurve: not implemented BuckingCurve");
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.1.2(2)
        /// </summary>
        protected EN1993p11Options.AxialBuckingCurves GetBucklingCurve2Axis(ISteelSection section)
        {
            if (section is SteelSectionCHS sectionCHS)
            {
                if (sectionCHS.FormedType == Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                        return EN1993p11Options.AxialBuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return EN1993p11Options.AxialBuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                {
                    if (sectionCHS.IsDoubleSymmetric)
                        return EN1993p11Options.AxialBuckingCurves.c;
                    else
                        return EN1993p11Options.AxialBuckingCurves.c;
                }
            }

            else if (section is SteelSectionRHS sectionRHS)
            {
                if (sectionRHS.SectionType == Section.SectionTypes.Welded)
                {
                    if (sectionRHS.ThicknessBottom / sectionRHS.Base < 40 && sectionRHS.ThicknessTop / sectionRHS.Base < 40 &&
                        sectionRHS.ThicknessWebLeft / sectionRHS.Height < 30 && sectionRHS.ThicknessWebRight / sectionRHS.Height < 30)
                        return EN1993p11Options.AxialBuckingCurves.b;
                    else
                        return EN1993p11Options.AxialBuckingCurves.c;
                }
                else if (sectionRHS.FormedType == Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                        return EN1993p11Options.AxialBuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return EN1993p11Options.AxialBuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                    return EN1993p11Options.AxialBuckingCurves.c;
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
                                return EN1993p11Options.AxialBuckingCurves.a;
                            else
                                return EN1993p11Options.AxialBuckingCurves.b;
                        }
                        else
                        {
                            if (sectionH.ThicknessBottomFlange < 40.0 && sectionH.ThicknessTopFlange < 40.0 && sectionH.ThicknessWeb < 40.0)
                                return EN1993p11Options.AxialBuckingCurves.a0;
                            else
                                return EN1993p11Options.AxialBuckingCurves.a;
                        }
                    }
                    else // (sectionH.IsHSection)
                    {
                        if (section.SteelMaterial.Fyk < 460.0)
                        {
                            if (sectionH.ThicknessBottomFlange < 100.0 && sectionH.ThicknessTopFlange < 100.0 && sectionH.ThicknessWeb < 100.0)
                                return EN1993p11Options.AxialBuckingCurves.c;
                            else
                                return EN1993p11Options.AxialBuckingCurves.d;
                        }
                        else
                        {
                            if (sectionH.ThicknessBottomFlange < 100.0 && sectionH.ThicknessTopFlange < 100.0 && sectionH.ThicknessWeb < 100.0)
                                return EN1993p11Options.AxialBuckingCurves.a;
                            else
                                return EN1993p11Options.AxialBuckingCurves.c;
                        }
                    }
                }
                else //(sectionH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return EN1993p11Options.AxialBuckingCurves.c;
                        else
                            return EN1993p11Options.AxialBuckingCurves.d;
                    }
                    else
                    {
                        if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                            return EN1993p11Options.AxialBuckingCurves.c;
                        else
                            return EN1993p11Options.AxialBuckingCurves.d;
                    }
                }
            }

            else if (section is SectionC _ || section is SectionT _)
                return EN1993p11Options.AxialBuckingCurves.c;

            else if (section is SectionL _)
                return EN1993p11Options.AxialBuckingCurves.b;

            else
                throw new NotImplementedException("GetBucklingCurve: not implemented section");
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.1.2(2)
        /// </summary>
        protected EN1993p11Options.AxialBuckingCurves GetBucklingCurve1Axis(ISteelSection section)
        {
            if (section is SteelSectionCHS sectionCHS)
            {
                if (sectionCHS.FormedType == Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                        return EN1993p11Options.AxialBuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return EN1993p11Options.AxialBuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                {
                    if (sectionCHS.IsDoubleSymmetric)
                        return EN1993p11Options.AxialBuckingCurves.c;
                    else
                        return EN1993p11Options.AxialBuckingCurves.c;
                }
            }

            else if (section is SteelSectionRHS sectionRHS)
            {
                if (sectionRHS.SectionType == Section.SectionTypes.Welded)
                {
                    if (sectionRHS.ThicknessBottom / sectionRHS.Base < 40 && sectionRHS.ThicknessTop / sectionRHS.Base < 40 &&
                        sectionRHS.ThicknessWebLeft / sectionRHS.Height < 30 && sectionRHS.ThicknessWebRight / sectionRHS.Height < 30)
                        return EN1993p11Options.AxialBuckingCurves.b;
                    else
                        return EN1993p11Options.AxialBuckingCurves.c;
                }
                else if (sectionRHS.FormedType == Section.FormedTypes.HotFinished)
                {
                    if (section.SteelMaterial.Fyk >= 460)
                        return EN1993p11Options.AxialBuckingCurves.a0;
                    else // (material.Fyk < 460)
                        return EN1993p11Options.AxialBuckingCurves.a;
                }
                else //(sectionCHS.FormedType == Model.Sections.Section.FormedTypes.ColdFormed)
                    return EN1993p11Options.AxialBuckingCurves.c;
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
                                return EN1993p11Options.AxialBuckingCurves.a;
                            else
                                return EN1993p11Options.AxialBuckingCurves.b;
                        }
                        else
                        {
                            if (sectionH.ThicknessBottomFlange < 40.0 && sectionH.ThicknessTopFlange < 40.0 && sectionH.ThicknessWeb < 40.0)
                                return EN1993p11Options.AxialBuckingCurves.a0;
                            else
                                return EN1993p11Options.AxialBuckingCurves.a0;
                        }
                    }
                    else // (sectionH.IsHSection)
                    {
                        if (sectionH.ThicknessBottomFlange < 100.0 && sectionH.ThicknessTopFlange < 100.0 && sectionH.ThicknessWeb < 100.0)
                            return EN1993p11Options.AxialBuckingCurves.b;
                        else
                            return EN1993p11Options.AxialBuckingCurves.d;
                    }
                }
                else //(sectionH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                {
                    if (sectionH.ThicknessBottomFlange < 40 && sectionH.ThicknessTopFlange < 40 && sectionH.ThicknessWeb < 40)
                        return EN1993p11Options.AxialBuckingCurves.b;
                    else
                        return EN1993p11Options.AxialBuckingCurves.c;
                }
            }

            else if (section is SectionC _ || section is SectionT _)
                return EN1993p11Options.AxialBuckingCurves.c;

            else if (section is SectionL _ )
                return EN1993p11Options.AxialBuckingCurves.b;

            else
                throw new NotImplementedException("GetBucklingCurve: not implemented section");
        }

        #endregion

        #region Bending Moment

        protected void CalculateMNRd1(SectionClass sectionClass, ISteelSection section, double Ned, double axialBucklingRd1, double axialBucklingRd2, double axialCompressionRd, 
            double bending1Rd, double bending2Rd, out double mNxRd, out double mNyRd)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
            {
                //double mNxRd;   //MN,y,Rd sulla norma
                //double mNyRd;   //MN,z,Rd sulla norma
                double n1 = Math.Abs(Ned / axialBucklingRd1);
                double n2 = Math.Abs(Ned / axialBucklingRd2);

                if (section is SteelSectionH sectionH)
                {
                    double a = Math.Min((sectionH.Area - (sectionH.LenghtTopFlange * sectionH.ThicknessTopFlange +
                        sectionH.LenghtBottomFlange * sectionH.ThicknessBottomFlange) / sectionH.Area), 0.5);

                    if (Ned <= 0.25 * axialCompressionRd &&
                        Ned <= 0.5 * sectionH.HeightWeb * sectionH.ThicknessWeb * Fy / GammaM0)
                        mNxRd = bending1Rd;

                    else
                        mNxRd = bending1Rd * (1 - n1) / (1 - 0.5 * a);

                    if (Ned <= 0.5 * sectionH.HeightWeb * sectionH.ThicknessWeb * Fy / GammaM0)
                        mNyRd = bending2Rd;

                    else
                    {
                        if (n2 <= a)
                            mNyRd = bending2Rd;
                        else
                            mNyRd = bending2Rd * (1 - Math.Pow((n2 - a) / (1 - a), 2));
                    }
                }
                else if (section is SteelSectionRHS sectionRHS)
                {
                    double aw = Math.Min((sectionRHS.Area - (sectionRHS.Base * sectionRHS.ThicknessTop +
                        sectionRHS.Base * sectionRHS.ThicknessBottom) / sectionRHS.Area), 0.5);
                    double af = Math.Min((sectionRHS.Area - (sectionRHS.Height * sectionRHS.ThicknessWebLeft +
                        sectionRHS.Base * sectionRHS.ThicknessWebRight) / sectionRHS.Area), 0.5);

                    mNxRd = Math.Min(bending1Rd * (1 - n1) / (1 - 0.5 * aw), bending1Rd);
                    mNyRd = Math.Min(bending2Rd * (1 - n2) / (1 - 0.5 * af), bending2Rd);
                }
                else
                    throw new ArgumentException("MN,Rd EN1993-1-1 §6.2.9.1 not implemented this section");
            }
            else
                throw new ArgumentException("MN,Rd EN1993-1-1 §6.2.9.1 not implemented class3 and 4");
        }

        protected double CalculateBendingMoment1Capacity(ResultBeamForces resultBeamForces, SectionClass sectionClass, ISteelSection section)
        {
            double shear2Capacity = CalculateShear2Capacity(resultBeamForces, section);

            if (resultBeamForces.V2 < 0.5 * shear2Capacity)   // low shear condition
            {
                return CalculateMcRd1(sectionClass, section);   // TODO: implementare CalculateBendingMoment1Capacity
            }
            else // high shear condition
            {
                return CalculateMVRd1(sectionClass, resultBeamForces, section);
            }
        }

        protected double CalculateBendingMoment2Capacity(ResultBeamForces resultBeamForces, SectionClass sectionClass, ISteelSection section)
        {
            double shear1Capacity = CalculateShear1Capacity(resultBeamForces, section);

            if (resultBeamForces.V1 < 0.5 * shear1Capacity)   // low shear condition
            {
                return CalculateMcRd2(sectionClass, section);   // TODO: implementare CalculateBendingMoment1Capacity
            }
            else // high shear condition
            {
                return CalculateMVRd2(sectionClass, resultBeamForces, section);
            }
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.5(2)
        /// </summary>
        protected double CalculateMcRd1(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                return section.Wpl1 * Fy / GammaM0;
            else if (sectionClass == SectionClass.Class3)
                return section.Wel1 * Fy / GammaM0;
            else
                return GetWeffMin1(section) * Fy / GammaM0;
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.5(2)
        /// </summary>
        protected double CalculateMcRd2(SectionClass sectionClass, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                return section.Wpl2 * Fy / GammaM0;
            else if (sectionClass == SectionClass.Class3)
                return section.Wel2 * Fy / GammaM0;
            else
                return GetWeffMin2(section) * Fy / GammaM0;
        }

        #endregion

        #region Shear

        protected double CalculateShear2Capacity(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            double n;
            if (section.SteelMaterial.Fyk <= 460)
                n = EN1993P11.NShearBucklingLowGradeOfSteel;
            else
                n = EN1993P11.NShearBucklingHighGradeOfSteel; 

            if (section is SteelSectionH sectionH)
                if (sectionH.SectionType == Section.SectionTypes.Rolled)
                {
                    if (sectionH.HeightWeb / sectionH.ThicknessWeb > 72.0 * Epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * CalculateVbRd1(section);
                }
                else
                {
                    if (sectionH.HeightWeb / sectionH.ThicknessWeb > 72.0 * Epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * CalculateVbRd1(section);
                }
            if (section is SteelSectionRHS sectionRHS)
                if (sectionRHS.SectionType == Section.SectionTypes.Rolled)
                {
                    if (sectionRHS.ThicknessWebLeft / sectionRHS.Heightinternal > 72.0 * Epsilon / n ||
                        sectionRHS.ThicknessWebRight / sectionRHS.Heightinternal > 72.0 * Epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * CalculateVbRd1(section);
                }
                else
                {
                    if (sectionRHS.ThicknessWebLeft / sectionRHS.Heightinternal > 72.0 * Epsilon / n ||
                        sectionRHS.ThicknessWebRight / sectionRHS.Heightinternal > 72.0 * Epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * CalculateVbRd1(section);
                }
            if (section is SteelSectionC sectionC)
                if (sectionC.SectionType == Section.SectionTypes.Rolled)
                {
                    if (sectionC.HeightWeb / sectionC.ThicknessWeb > 72.0 * Epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * CalculateVbRd1(section);
                }
                else
                {
                    if (sectionC.HeightWeb / sectionC.ThicknessWeb > 72.0 * Epsilon / n)
                        return CalculateShearReductionDueToTorsion(resultBeamForces, section) * CalculateVbRd1(section);
                }

            return CalculateShearReductionDueToTorsion(resultBeamForces, section) * CalculateVcRd2(section);
        }

        protected double CalculateShear1Capacity(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            return CalculateShearReductionDueToTorsion(resultBeamForces, section) * Fy * GetShearArea1(section) / Math.Sqrt(3.0);
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.6(2)
        /// </summary>
        /// <returns></returns>
        protected double CalculateVcRd1(ISteelSection section)
        {
            return GetShearArea1(section) * (Fy / Math.Sqrt(3)) / GammaM0;
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.6(2)
        /// </summary>
        /// <returns></returns>
        protected double CalculateVcRd2(ISteelSection section)
        {
            return GetShearArea2(section) * (Fy / Math.Sqrt(3)) / GammaM0;
        }

        protected double CalculateVbRd1(ISteelSection section)
        {
            double n;
            if (section.SteelMaterial.Fyk <= 460)
                n = EN1993P11.NShearBucklingLowGradeOfSteel;
            else
                n = EN1993P11.NShearBucklingHighGradeOfSteel;

            double hw;
            double t;

            if (section is SteelSectionH sectionH)
            {
                hw = sectionH.HeightWeb;
                t = sectionH.ThicknessWeb;
            }
            else if (section is SteelSectionRHS sectionRHS)
            {
                hw = sectionRHS.Heightinternal;
                t = sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight;
            }
            else if (section is SteelSectionC sectionC)
            {
                hw = sectionC.HeightWeb;
                t = sectionC.ThicknessWeb;
            }
            else if (section is SteelSectionT sectionT)
            {
                hw = sectionT.HeightWeb;
                t = sectionT.ThicknessWeb;
            }
            else
                throw new ArgumentException($"ShearBucklingCheck: EN1993-1-5 §5.1 not implemented this section {section}");

            double lambdaSignedW = hw / (86.4 * t * Epsilon);

            double chi;
            if (lambdaSignedW < 0.83 / n)
                chi = n;
            else
                chi = 0.83 / n;

            double vbfRd = 0.0;
            double vbwRd = chi * Fy * hw * t / (Math.Pow(3, 0.5) * GammaM1);

            return Math.Min(vbwRd + vbfRd, n * Fy * hw * t / (Math.Pow(3, 0.5) * GammaM1));
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.6(3)
        /// </summary>
        protected double GetShearArea2(ISteelSection section)
        {
            if (section is SteelSectionH sech && sech.SectionType == Section.SectionTypes.Rolled)
                return sech.Area - (sech.LenghtTopFlange * sech.ThicknessTopFlange) - (sech.LenghtBottomFlange * sech.ThicknessBottomFlange) +
                    (sech.ThicknessWeb + 2 * sech.R) * (sech.ThicknessTopFlange + sech.ThicknessBottomFlange) / 2;

            else if (section is SteelSectionH secH && secH.SectionType == Section.SectionTypes.Welded)
                return secH.ThicknessWeb * secH.HeightWeb;

            else if (section is SteelSectionC secC && secC.SectionType == Section.SectionTypes.Rolled)
                return secC.Area - (secC.LengthTop * secC.ThicknessTop) - (secC.LengthBottom * secC.ThicknessBottom) +
                    (secC.ThicknessWeb + 2 * secC.R1) * (secC.ThicknessTop + secC.ThicknessBottom) / 2;

            else if (section is SteelSectionC sectC && sectC.SectionType == Section.SectionTypes.Welded)
                return sectC.ThicknessWeb * (sectC.Height - sectC.ThicknessBottom - sectC.ThicknessTop);

            else if (section is SteelSectionRHS sectionRHS && sectionRHS.SectionType == Section.SectionTypes.Welded)
                return (sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight) * sectionRHS.Heightinternal;

            else if (section is SteelSectionRHS sectionRhs && sectionRhs.SectionType == Section.SectionTypes.Rolled)
                return sectionRhs.Area * sectionRhs.Height / (sectionRhs.Base + sectionRhs.Height);

            else if (section is SteelSectionCHS sectionCHS)
                return 2 * sectionCHS.Area / Math.PI;

            else if (section is SteelSectionT sectionT)
                return 0.9 * sectionT.Area * (sectionT.LenghtFlange - sectionT.ThicknessFlange);

            else if (section is SteelSectionL sectionL)
                return sectionL.HorizontalLegLength * sectionL.HorizontalLegThickness;

            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.6(3)
        /// </summary>
        protected double GetShearArea1(ISteelSection section)
        {
            if (section is SteelSectionH sech)
                return sech.LenghtTopFlange * sech.ThicknessTopFlange + sech.LenghtBottomFlange * sech.ThicknessBottomFlange;

            else if (section is SteelSectionC secC)
                return secC.LengthTop * secC.ThicknessTop + secC.LengthBottom * secC.ThicknessBottom;

            else if (section is SteelSectionRHS sectionRHS && sectionRHS.SectionType == Section.SectionTypes.Welded)
                return sectionRHS.Area - (sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight) * sectionRHS.Heightinternal;

            else if (section is SteelSectionRHS sectionRhs && sectionRhs.SectionType == Section.SectionTypes.Rolled)
                return sectionRhs.Area * sectionRhs.Base / (sectionRhs.Base + sectionRhs.Height);

            else if (section is SteelSectionCHS sectionCHS)
                return 2 * sectionCHS.Area / Math.PI;

            else if (section is SteelSectionT sectionT)
                return sectionT.LenghtFlange * sectionT.ThicknessFlange;

            else if (section is SteelSectionL sectionL)
                return sectionL.VerticalLegLength * sectionL.VerticalLegThickness;

            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        /// <summary>
        /// Shear reduction factor in case of torsion. EN1993-1-1: 2005 Chapter 6.2.7(9)
        /// </summary>
        protected double CalculateShearReductionDueToTorsion(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (Math.Abs(resultBeamForces.T) > 1)
            {
                if (section is SteelSectionH)
                {
                    double sigmaStVenant = resultBeamForces.T / section.Jt;
                    return Math.Sqrt(1 - (sigmaStVenant / (1.25 * (Fy / Math.Sqrt(3)))));
                }
                else if (section is SteelSectionC)
                {
                    double sigmaStVenant = resultBeamForces.T / section.Jt;
                    double sigmaWarp = resultBeamForces.T / section.Jw;
                    return Math.Sqrt(1 - (sigmaStVenant / (1.25 * (Fy / Math.Sqrt(3))))) - (sigmaWarp / (1.25 * (Fy / Math.Sqrt(3))));
                }
                else if (section is SteelSectionCHS sectionCHS)
                {
                    double sigmaStVenant = resultBeamForces.T / (2.0 * sectionCHS.Area * sectionCHS.Thickness);
                    return 1 - (sigmaStVenant / (1.25 * (Fy / Math.Sqrt(3))));
                }
                else if (section is SteelSectionRHS sectionRHS)
                {
                    double sigmaStVenant = resultBeamForces.T / (2.0 * sectionRHS.Area *
                        (sectionRHS.ThicknessBottom + sectionRHS.ThicknessTop + sectionRHS.ThicknessWebRight + sectionRHS.ThicknessWebLeft) / 4);
                    return 1 - (sigmaStVenant / (1.25 * (Fy / Math.Sqrt(3))));
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
        protected double CalculateMVRd1(SectionClass sectionClass, ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
            { 
                if (section is SteelSectionH steelSectionH)
                    return (section.Wpl1 - CalculateRhoForMomentAndShearInteraction2(resultBeamForces, section) *
                        Math.Pow(GetShearArea2(section), 2) / (4 * steelSectionH.ThicknessWeb)) * Fy / GammaM0;
                else if (section is SteelSectionRHS steelSectionRHS)
                    return (section.Wpl1 - CalculateRhoForMomentAndShearInteraction2(resultBeamForces, section) *
                        Math.Pow(GetShearArea2(section), 2) / (4 * (steelSectionRHS.ThicknessWebLeft + steelSectionRHS.ThicknessWebRight))) * Fy / GammaM0;
                else if (section is SteelSectionT steelSectionT)
                    return (section.Wpl1 - CalculateRhoForMomentAndShearInteraction2(resultBeamForces, section) *
                        Math.Pow(GetShearArea2(section), 2) / (4 * (steelSectionT.ThicknessWeb))) * Fy / GammaM0;
                else if (section is SteelSectionCHS)
                    return (1 - CalculateRhoForMomentAndShearInteraction2(resultBeamForces, section)) * CalculateMcRd1(sectionClass, section);
            }

            return CalculateMcRd1(sectionClass, section);
        }

        /// <summary>
        /// Reduced deisgn plastic resistence moment due to shear forces. Chapter 6.2.8(5)
        /// </summary>
        protected double CalculateMVRd2(SectionClass sectionClass, ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
            {
                if (section is SteelSectionH steelSectionH)
                    return (section.Wpl2 - CalculateRhoForMomentAndShearInteraction1(resultBeamForces, section) *
                        Math.Pow(GetShearArea1(section), 2) / (4 * steelSectionH.ThicknessTopFlange + steelSectionH.ThicknessBottomFlange)) * Fy / GammaM0;
                else if (section is SteelSectionRHS steelSectionRHS)
                    return (section.Wpl2 - CalculateRhoForMomentAndShearInteraction1(resultBeamForces, section) *
                        Math.Pow(GetShearArea1(section), 2) / (4 * (steelSectionRHS.ThicknessTop + steelSectionRHS.ThicknessBottom))) * Fy / GammaM0;
                else if (section is SteelSectionT steelSectionT)
                    return (section.Wpl2 - CalculateRhoForMomentAndShearInteraction1(resultBeamForces, section) *
                        Math.Pow(GetShearArea1(section), 2) / (4 * (steelSectionT.ThicknessWeb))) * Fy / GammaM0;
            }

            return CalculateMcRd1(sectionClass, section);
        }

        /// <summary>
        /// Reduced deisgn plastic resistence moment due to shear forces. Chapter 6.2.8(3)
        /// </summary>
        protected double CalculateRhoForMomentAndShearInteraction2(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            return Math.Pow((2 * resultBeamForces.V2 / CalculateShear2Capacity(resultBeamForces, section)) - 1, 2);
        }

        /// <summary>
        /// Reduced deisgn plastic resistence moment due to shear forces. Chapter 6.2.8(3)
        /// </summary>
        protected double CalculateRhoForMomentAndShearInteraction1(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            return Math.Pow((2 * resultBeamForces.V1 / CalculateShear1Capacity(resultBeamForces, section)) - 1, 2);
        }

        #endregion

        #region Lateral Torsional Buckling

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.2.3(2)
        /// </summary>
        protected double Getkc(EN1993p11Options.SupportConditions supportCondition, EN1993p11Options.LoadConditions loadCondition, double? psi)
        {
            double kc;
            if (loadCondition != EN1993p11Options.LoadConditions.NotDirectlyLoaded)
            {
                if (supportCondition == EN1993p11Options.SupportConditions.HingesAtEnds && 
                    loadCondition == EN1993p11Options.LoadConditions.Constant)                
                    kc = 0.94;
                
                else if (supportCondition == EN1993p11Options.SupportConditions.EndsRestrained && 
                    loadCondition == EN1993p11Options.LoadConditions.Constant)                
                    kc = 0.90;
                
                else if (supportCondition == EN1993p11Options.SupportConditions.OneSideRestrained_OneSideHinged && 
                    loadCondition == EN1993p11Options.LoadConditions.Constant)                
                    kc = 0.91;
                
                else if (supportCondition == EN1993p11Options.SupportConditions.HingesAtEnds && 
                    loadCondition == EN1993p11Options.LoadConditions.SingleForce)                
                    kc = 0.86;
                
                else if (supportCondition == EN1993p11Options.SupportConditions.EndsRestrained && 
                    loadCondition == EN1993p11Options.LoadConditions.SingleForce)                
                    kc = 0.77;
                
                else if (supportCondition == EN1993p11Options.SupportConditions.OneSideRestrained_OneSideHinged && 
                    loadCondition == EN1993p11Options.LoadConditions.SingleForce)                
                    kc = 0.82;
                
                else                
                    throw new Exception("Lateral Torsional Buckling not implemented this combinations of SupportCondition and LoadCondition ");                
            }
            else
            {
                if (psi.HasValue)                
                    kc = 1.0 / (1.33 - 0.33 * psi.Value);                
                else                
                    throw new Exception("Se a psi value = M(x=0)/M(x=L)");                
            }
            return kc;
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.2.3(2)
        /// </summary>
        protected double GetFactorFForLTB(double kc, double lambdaLT)
        {
            return Math.Min(1.0, 1.0 - 0.5 * (1.0 - kc) * (1.0 - 2.0 * Math.Pow(lambdaLT - 0.8, 2.0)));
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.2.2(2)
        /// </summary>
        protected double GetImperfectionFactorLT(ISteelSection section)
        {
            if (section is SteelSectionH sectionH)
            {
                if (sectionH.IsRolled)
                {
                    if (sectionH.Height / sectionH.LenghtBottomFlange <= 2 && sectionH.Height / sectionH.LenghtTopFlange <= 2)
                        return EN1993P11.AlphaLTImperfectionFactorForCurveA;
                    else
                        return EN1993P11.AlphaLTImperfectionFactorForCurveB;
                }
                else
                {
                    if (sectionH.Height / sectionH.LenghtBottomFlange < 2 && sectionH.Height / sectionH.LenghtTopFlange < 2)
                        return EN1993P11.AlphaLTImperfectionFactorForCurveB;
                    else
                        return EN1993P11.AlphaLTImperfectionFactorForCurveC;
                }
            }
            else
                return EN1993P11.AlphaLTImperfectionFactorForCurveD;
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.2.2(2)
        /// </summary>
        protected EN1993p11Options.LateralTorsionalBuckingCurves GetLateralTorsionalBucklingCurve(ISteelSection section)
        {
            if (section is SteelSectionH sectionH)
            {
                if (sectionH.IsRolled)
                {
                    if (sectionH.Height / sectionH.LenghtBottomFlange <= 2 && sectionH.Height / sectionH.LenghtTopFlange <= 2)
                        return EN1993p11Options.LateralTorsionalBuckingCurves.a;
                    else
                        return EN1993p11Options.LateralTorsionalBuckingCurves.b;
                }
                else
                {
                    if (sectionH.Height / sectionH.LenghtBottomFlange < 2 && sectionH.Height / sectionH.LenghtTopFlange < 2)
                        return EN1993p11Options.LateralTorsionalBuckingCurves.b;
                    else
                        return EN1993p11Options.LateralTorsionalBuckingCurves.c;
                }
            }
            else
                return EN1993p11Options.LateralTorsionalBuckingCurves.d;
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.3.2.2(2)
        /// </summary>
        protected double GetImperfectionFactorLTMod(ISteelSection section)
        {
            if (section is SteelSectionH sectionH)
            {
                if (sectionH.IsRolled)
                {
                    if (sectionH.Height / sectionH.LenghtBottomFlange <= 2 && sectionH.Height / sectionH.LenghtTopFlange <= 2)
                        return EN1993P11.AlphaLTImperfectionFactorForCurveB;
                    else
                        return EN1993P11.AlphaLTImperfectionFactorForCurveC;
                }
                else
                {
                    if (sectionH.Height / sectionH.LenghtBottomFlange < 2 && sectionH.Height / sectionH.LenghtTopFlange < 2)
                        return EN1993P11.AlphaLTImperfectionFactorForCurveC;
                    else
                        return EN1993P11.AlphaLTImperfectionFactorForCurveD;
                }
            }
            else
                throw new ArgumentException("Lateral tosional buckling check: can not check the section with method for rolled or equivalent welded sections (eq.6.57)");
        }

        protected double GetLambdaSignedLTB(ISteelSection section, double Mcr)
        {
            if (section is SteelSectionC)
            {
                double lambdaLT = Math.Pow(section.Wpl1 * Fy / Mcr, 0.5);
                double lambdaT;
                if (lambdaLT < 0.50)
                    return Math.Pow(section.Wpl1 * Fy / Mcr, 0.5);
                else if (lambdaLT >= 0.5 && lambdaLT < 0.75)
                    lambdaT = 1.11 - lambdaLT;
                else if (lambdaLT >= 0.75 && lambdaLT < 1.14)
                    lambdaT = 0.69 - 0.44 * lambdaLT;
                else if (lambdaLT >= 1.14)
                    lambdaT = 0.19;
                else
                    throw new ArgumentException("Lateral torsional buckling: not implemented lambdaT");
                return lambdaLT + lambdaT;
            }
            else
                return Math.Pow(section.Wpl1 * Fy / Mcr, 0.5);
        }

        protected double CalculateMcr(ISteelSection section, ResultBeamForces forces, double length,
            EN1993p11Options.SupportConditions supportCondition, EN1993p11Options.LoadConditions loadCondition,
            EN1993p11Options.LateralSupportConditions lateralCondition, EN1993p11Options.LateralWarpingConditions warpingCondition,
            double? psi, EN1993p11Options.LoadApplicationPoints loadApplicationPoints = EN1993p11Options.LoadApplicationPoints.TopSection, 
            double? inputC1 = null, double? inputC2 = null) 
        {
            /*
            * C1 = factor that account for the shape of the moment diagram
            * C2 = factor that account for the point of load application in relation to the shear center
            * C3 = factor that account asymmetry about y-axis
            * 
            * zg = è la distanza tra il punto di applicazione del carico e il centro di taglio:
            *      -positiva se il carico è diretto dall'alto verso il basso ed è applicato all'estradosso. Se invece agisce dal basso verso l'alto il segno va cambiato
            *      -negativa se il carico è diretto dall'alto verso il basso ed è applicato all'intradosso.
            * 
            */

            //calculation of zg calculatet from the top of section to the shear center:
            //zg coordinate of point of application vs coordinate of shear center
            //zj zs (shear center) - 0.5 integral(y^2+z^2) * z / Jy dA

            double G = Material.E / (2.0 * (1.0 + Material.Ni));
            double k = GetKForLTB(lateralCondition);
            double kw = GetKwForLTB(warpingCondition);
            double c1;
            double c2;
            double c3;
            double zg;
            double zj;


            if (loadApplicationPoints == EN1993p11Options.LoadApplicationPoints.TopSection)
            {
                if (section is SteelSectionCHS sectionCHS)
                    zg = sectionCHS.Diameter - sectionCHS.ShearCenter.Y;

                else if (section is SteelSectionRHS sectionRHS)
                    zg = sectionRHS.Height - sectionRHS.ShearCenter.Y;

                else if (section is SteelSectionH sectionH)
                    zg = sectionH.Height - sectionH.ShearCenter.Y;

                else if (section is SteelSectionC sectionC)
                    zg = sectionC.Height - sectionC.ShearCenter.Y;

                else if (section is SteelSectionT sectionT)
                    zg = sectionT.Height - sectionT.ShearCenter.Y;

                else
                    throw new Exception("McrLT not yet supported for this section");
            }
            else
            {
                zg = 0;
            }

            if (inputC1 == null && inputC2 == null)
            {
                if (section.IsDoubleSymmetric)
                {
                    c3 = 0.0;
                    zj = 0.0;

                    if (loadCondition != EN1993p11Options.LoadConditions.NotDirectlyLoaded)
                    {
                        if (supportCondition == EN1993p11Options.SupportConditions.HingesAtEnds)
                        {
                            if (loadCondition == EN1993p11Options.LoadConditions.Constant)
                            {
                                //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                                c1 = 1.127;
                                c2 = 0.454;
                            }
                            else if (loadCondition == EN1993p11Options.LoadConditions.SingleForce)
                            {
                                //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                                c1 = 1.348;
                                c2 = 0.630;
                            }
                            else                            
                                throw new NotSupportedException("Load condition + Support not yet supported in calculation of C1 and C2 for McrLT");                            
                        }
                        else if (supportCondition == EN1993p11Options.SupportConditions.EndsRestrained)
                        {
                            if (loadCondition == EN1993p11Options.LoadConditions.Constant)
                            {
                                //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                                c1 = 2.578;
                                c2 = 1.554;
                            }
                            else if (loadCondition == EN1993p11Options.LoadConditions.SingleForce)
                            {
                                //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                                c1 = 1.683;
                                c2 = 1.645;
                            }
                            else
                                throw new NotSupportedException("Load condition + Support not yet supported in calculation of C1 and C2 for McrLT");
                        }
                        else
                            throw new Exception("McrLT: SupportCondition not supported");
                    }
                    else if (loadCondition == EN1993p11Options.LoadConditions.NotDirectlyLoaded) //Beam not directly loaded but with bending moment at the ends
                    {
                        if (psi.HasValue)
                        {
                            if (k == 1)
                            {
                                //Book: Rules for Member Stability in EN 1993-1-1 - Background documentation and design guidelines - ECCS Techinacl Committee - Stability
                                c1 = Math.Min(1.77 - 1.04 * psi.Value + 0.27 * psi.Value * psi.Value, 2.6);
                                c2 = 0;
                            }

                            else
                            {
                                //ENV 1993-1-1:1992 (F3)
                                c1 = Math.Min(1.88 - 1.40 * psi.Value + 0.52 * psi.Value * psi.Value, 2.7);
                                c2 = 0;
                            }
                        }
                        else
                            throw new Exception("Set the value of psi = M(x=0)/M(x=L)");
                    }
                    else
                        throw new Exception("Load condition + Support not yet supported in calculation of C1 and C2 for McrLT");
                }

                else if (section.IsSymmetricAlongYLocalAxis || section is SteelSectionC)
                {
                    /* note: use of Mcr also for C sections came from :
                    Lateral-torsional Buckling of Steel Channel Beams
                    A parametric study through FE - analysis
                    Master’s Thesis in the Master’s Programme Structural Engineering and Building Technology
                    CARL - MARCUS EKSTRÖM - DAVID WESLEY
                    */
                    if (section is SteelSectionH sectionH)
                    {
                        double Ifc; //inertia along the weak axis of the beam of compression flange
                        double Ift; //inertia along the weak axis of the beam of tension flange
                        if (forces.M1 >= 0)
                        { //tension bottom
                            Ift = 1.0 / 12.0 * sectionH.ThicknessBottomFlange * Math.Pow(sectionH.LenghtBottomFlange, 3.0);
                            Ifc = 1.0 / 12.0 * sectionH.ThicknessTopFlange * Math.Pow(sectionH.LenghtTopFlange, 3.0);
                        }
                        else
                        { //tension up
                            Ifc = 1.0 / 12.0 * sectionH.ThicknessBottomFlange * Math.Pow(sectionH.LenghtBottomFlange, 3.0);
                            Ift = 1.0 / 12.0 * sectionH.ThicknessTopFlange * Math.Pow(sectionH.LenghtTopFlange, 3.0);
                        }
                        //Book: Rules for Member Stability in EN 1993-1-1 - Background documentation and design guidelines - ECCS Techinacl Committee - Stability
                        //pg 230
                        double psif = (Ifc - Ift) / (Ifc + Ift);
                        double hs = sectionH.Height - sectionH.ThicknessBottomFlange / 2.0 - sectionH.ThicknessTopFlange / 2.0; // distance between the shear center of the flanges

                        if (psif >= 0)
                            zj = 0.8 * psif * hs / 2.0; //Wagner coeff

                        else
                            zj = psif * hs / 2.0;

                        //Book: Rules for Member Stability in EN 1993-1-1 - Background documentation and design guidelines - ECCS Techinacl Committee - Stability
                        //pg 233
                        if (psif <= 0.9 && psif >= -0.9)
                        {
                            GetC1C2C3ForMcr(supportCondition, loadCondition, k, psi, psif, out c1, out c2, out c3);
                        }
                        else
                        {
                            throw new Exception("Cannot calc McrLT. Section too asymmetric");
                        }
                    }
                    else if (section is SteelSectionC sectionC)
                    {
                        if (sectionC.LengthBottom == sectionC.LengthTop && sectionC.ThicknessBottom == sectionC.ThicknessTop)
                        {
                            double psif = 0;
                            zj = 0; //z centroid = z shear center + integral is 0 due to symmetry
                            GetC1C2C3ForMcr(supportCondition, loadCondition, k, psi, psif, out c1, out c2, out c3);
                        }
                        else                        
                            throw new Exception("Section not yet supported for calculation of McrLT");                        
                    }
                    else if (section is SteelSectionT sectionT)
                    {
                        double psif;
                        if (forces.M1 > 0)                        
                            psif = 1;
                        
                        else
                            psif = -1;
                        

                        //calculation of Wagner coefficiente: zj = zs - 0.5 integral((y^2 + z^2) * z dA) / Jy
                        //needed for for Mcr calculation
                        int nxFlange = 20;
                        int nyFlange = 4;
                        int nxWeb = 4;
                        int nyWeb = 30;
                        double integral = 0;

                        for (int i = 0; i < nxFlange; i++)
                        {
                            for (int j = 0; j < nyFlange; j++)
                            {
                                double Ai = (sectionT.LenghtFlange / nxFlange) * (sectionT.ThicknessFlange / nyFlange);
                                double zi = sectionT.Height - (2.0 * j + 1.0) / (2.0 * nyFlange) * sectionT.ThicknessFlange - sectionT.Centroid.Y;
                                double yi = -sectionT.LenghtFlange / 2.0 + (2.0 * i + 1.0) / (2.0 * nxFlange) * sectionT.LenghtFlange;

                                integral += (yi * yi + zi * zi) * zi * Ai;
                            }
                        }

                        for (int i = 0; i < nxWeb; i++)
                        {
                            for (int j = 0; j < nyWeb; j++)
                            {
                                double Ai = (sectionT.HeightWeb / nyWeb) * (sectionT.ThicknessWeb / nxWeb);
                                double zi = (2.0 * j + 1.0) / (2.0 * nyWeb) * (sectionT.HeightWeb) - sectionT.Centroid.Y;
                                double yi = -sectionT.ThicknessWeb / 2.0 + (2.0 * i + 1.0) * sectionT.ThicknessWeb / (2.0 * nxWeb);

                                integral += (yi * yi + zi * zi) * zi * Ai;
                            }
                        }
                        zj = (sectionT.ShearCenter.Y - sectionT.Centroid.Y) - 0.5 * integral / sectionT.J11;
                        if (forces.M1 < 0)                        
                            zj = -zj; //check this                           

                        //this should be used if -0.9 < psif < 0.9. There is no data...so...what to do?
                        GetC1C2C3ForMcr(supportCondition, loadCondition, k, psi, psif, out c1, out c2, out c3);
                    }
                    else                    
                        throw new Exception("Section not yet supported for calculation of McrLT");                    
                }

                else //NO sysmmetry                
                    throw new Exception("McrLT can not be calculated without symmetry");                
            }

            else
            {
                c1 = (double)inputC1;
                c2 = (double)inputC2;
                c3 = 0;
                zj = 0;
            }

            return c1 * (Math.Pow(Math.PI, 2.0) * Material.E * section.J22 / Math.Pow(k * length, 2.0)) *
                (Math.Pow(Math.Pow(k / kw, 2.0) * section.Jw / section.J22 +
                Math.Pow(k * length, 2.0) * G * section.Jt / (Math.Pow(Math.PI, 2.0) * Material.E * section.J22) +
                Math.Pow(c2 * zg - c3 * zj, 2.0), 0.5) - (c2 * zg - c3 * zj));
        }

        protected double GetKForLTB(EN1993p11Options.LateralSupportConditions supportCondition)
        {
            if (supportCondition == EN1993p11Options.LateralSupportConditions.EndsRestrained)
                return 0.5;
            else if (supportCondition == EN1993p11Options.LateralSupportConditions.HingesAtEnds)
                return 1.0;
            else if (supportCondition == EN1993p11Options.LateralSupportConditions.OneSideRestrained_OneSideHinged)
                return 0.7;
            else
                throw new ArgumentException("Not implemented coefficient K for this LateralSupportCondition");
        }

        protected double GetKwForLTB(EN1993p11Options.LateralWarpingConditions warpingCondition)
        {
            if (warpingCondition == EN1993p11Options.LateralWarpingConditions.EndsRestrained)
                return 0.5;
            else if (warpingCondition == EN1993p11Options.LateralWarpingConditions.HingesAtEnds)
                return 1.0;
            else if (warpingCondition == EN1993p11Options.LateralWarpingConditions.OneSideRestrained_OneSideHinged)
                return 0.7;
            else
                throw new ArgumentException("Not implemented coefficient K for this LateralSupportCondition");
        }

        /// <summary>
        /// EN1993-1-1 6.3.2.3 Metodo generale
        /// </summary>
        protected double CalculateMbRd(ISteelSection section, SectionClass sectionClass, ResultBeamForces forces,
            EN1993p11Options.SupportConditions supportCondition, EN1993p11Options.LoadConditions loadCondition,
            EN1993p11Options.LateralSupportConditions lateralCondition, EN1993p11Options.LateralWarpingConditions warpingCondition, double psi, 
            EN1993p11Options.LoadApplicationPoints loadApplicationPoint = EN1993p11Options.LoadApplicationPoints.TopSection)
        {
            double alpha = GetImperfectionFactorLT(section);
            double Mcr = CalculateMcr(section, forces, GetLengthCriticalMoment1(), supportCondition, loadCondition, lateralCondition, 
                warpingCondition, psi, loadApplicationPoint);
            double lambdaSigned = GetLambdaSignedLTB(section, Mcr);

            double phi = GetPhiForBuckling(alpha, lambdaSigned, EN1993P11.BetaForLateralTorsionalBuckling, EN1993P11.LambdaLT0ForLateralTorsionalBuckling);
            double chiMod = GetChiLTmod(phi, lambdaSigned, EN1993P11.BetaForLateralTorsionalBuckling, 1.0);

            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                return chiMod * section.Wpl1 * Fy / GammaM1;
            else if (sectionClass == SectionClass.Class3)
                return chiMod * section.Wel1 * Fy / GammaM1;
            else
                return chiMod * GetWeffMin1(section) * Fy / GammaM1;
        }

        /// <summary>
        /// EN1993-1-1 6.3.2.3 Metodo per sezioni laminate o equivalenti saldate
        /// </summary>
        protected double CalculateMbRdMod(ISteelSection section, SectionClass sectionClass, ResultBeamForces forces,
            EN1993p11Options.SupportConditions supportCondition, EN1993p11Options.LoadConditions loadCondition,
            EN1993p11Options.LateralSupportConditions lateralCondition, EN1993p11Options.LateralWarpingConditions warpingCondition, double psi, 
            EN1993p11Options.LoadApplicationPoints loadApplicationPoint)
        {
            double alpha = GetImperfectionFactorLTMod(section);
            double kc = Getkc(supportCondition, loadCondition, psi);
            double Mcr = CalculateMcr(section, forces, GetLengthCriticalMoment1(), supportCondition, loadCondition, lateralCondition, warpingCondition, psi, loadApplicationPoint);
            double lambdaSigned = GetLambdaSignedLTB(section, Mcr);
            double phi = GetPhiForBuckling(alpha, lambdaSigned, EN1993P11.BetaForLateralTorsionalBucklingMod, EN1993P11.LambdaLT0ForLateralTorsionalBucklingMod);
            double f = GetFactorFForLTB(kc, lambdaSigned);
            double chiMod = GetChiLTmod(phi, lambdaSigned, EN1993P11.BetaForLateralTorsionalBucklingMod, f);

            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
                return chiMod * section.Wpl1 * Fy / GammaM1;
            else if (sectionClass == SectionClass.Class3)
                return chiMod * section.Wel1 * Fy / GammaM1;
            else
                return chiMod * GetWeffMin1(section) * Fy / GammaM1;
        }

        protected double GetChiLTmod(double Phi, double lambdaSigned, double beta = 1.0, double f = 1.0) //beta_default = 1, f=1
        {
            double Chi = 1.0 / (Phi + Math.Pow(Math.Pow(Phi, 2.0) - beta * Math.Pow(lambdaSigned, 2.0), 0.5));
            Chi = GetMin(new double[] { Chi, 1.0, 1.0 / Math.Pow(lambdaSigned, 2.0) });

            double ChiLTMod = Math.Min(Chi / f, 1.0);
            ChiLTMod = Math.Min(ChiLTMod, 1.0 / Math.Pow(lambdaSigned, 2.0));
            return ChiLTMod;
        }

        protected void GetC1C2C3ForMcr(EN1993p11Options.SupportConditions supportCondition, EN1993p11Options.LoadConditions loadCondition, 
            double k, double? psi, double psif, out double C1, out double C2, out double C3)
        {
            C1 = 0;
            C2 = 0;
            C3 = 0;
            //tables 63 and 64 of Book: Rules for Member Stability in EN 1993-1-1 - Background documentation and design guidelines - ECCS Techinacl Committee - Stability can be used
            if (supportCondition == EN1993p11Options.SupportConditions.EndsRestrained)
            {
                if (loadCondition == EN1993p11Options.LoadConditions.NotDirectlyLoaded)
                {
                    C2 = 0;
                    double interpolation(double x0, double y0, double x1, double y1, double xc) 
                    { 
                        return (y1 - y0) / (x1 - x0) * (xc - x1) + y1; 
                    }
                    if (k == 1)
                    {
                        if (0.75 <= psi && psi <= 1.0)
                        {
                            C1 = interpolation(0.75, 1.14, 1, 1, psi.Value);
                            C3 = 1.0;
                        }
                        else if (0.5 <= psi && psi <= 0.75)
                        {
                            C1 = interpolation(0.5, 1.31, 0.75, 1.14, psi.Value);
                            C3 = 1.0;
                        }
                        else if (0.25 <= psi && psi <= 0.5)
                        {
                            C1 = interpolation(0.25, 1.52, 0.5, 1.31, psi.Value);
                            C3 = 1.0;
                        }
                        else if (0.0 <= psi && psi <= 0.25)
                        {
                            C1 = interpolation(0.0, 1.77, 0.25, 1.52, psi.Value);
                            C3 = 1.0;
                        }
                        else if (-0.25 <= psi && psi <= 0)
                        {
                            C1 = interpolation(-0.25, 2.06, 0, 1.77, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = 1.0;
                            }
                            else
                            {
                                C3 = interpolation(-0.25, 0.85, 0, 1, psi.Value);
                            }
                        }
                        else if (-0.5 <= psi && psi <= -0.25)
                        {
                            C1 = interpolation(-0.5, 2.35, -0.25, 2.06, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = 1.0;
                            }
                            else
                            {
                                C3 = interpolation(-0.5, 1.3 - 1.2 * psif, -0.25, 0.85, psi.Value);
                            }
                        }
                        else if (-0.75 <= psi && psi <= -0.5)
                        {
                            C1 = interpolation(-0.75, 2.60, -0.50, 2.35, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = 1.0;
                            }
                            else
                            {
                                C3 = interpolation(-0.75, 0.55 - psif, -0.5, 1.3 - 1.2 * psif, psi.Value);
                            }
                        }
                        else if (-1 <= psi && psi <= -0.75)
                        {
                            C1 = interpolation(-1.0, 2.60, -0.75, 2.60, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = interpolation(-1.0, -psif, -0.75, 1.0, psi.Value);
                            }
                            else
                            {
                                C3 = interpolation(-1.0, -psif, -0.75, 0.55 - psif, psi.Value);
                            }
                        }
                        else
                        {
                            throw new Exception("cannot calc McrLT");
                        }
                    }
                    else if (k == 0.5)
                    {
                        if (0.75 <= psi && psi <= 1.0)
                        {
                            C1 = interpolation(0.75, 1.19, 1, 1.05, psi.Value);
                            C3 = interpolation(0.75, 1.017, 1, 1.019, psi.Value);
                        }
                        else if (0.5 <= psi && psi <= 0.75)
                        {
                            C1 = interpolation(0.5, 1.37, 0.75, 1.19, psi.Value);
                            C3 = 1.0;
                        }
                        else if (0.25 <= psi && psi <= 0.5)
                        {
                            C1 = interpolation(0.25, 1.60, 0.5, 1.37, psi.Value);
                            C3 = 1.0;
                        }
                        else if (0.0 <= psi && psi <= 0.25)
                        {
                            C1 = interpolation(0.0, 1.86, 0.25, 1.60, psi.Value);
                            C3 = 1.0;
                        }
                        else if (-0.25 <= psi && psi <= 0)
                        {
                            C1 = interpolation(-0.25, 2.15, 0, 1.86, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = 1.0;
                            }
                            else
                            {
                                C3 = interpolation(-0.25, 0.65, 0, 1, psi.Value);
                            }
                        }
                        else if (-0.5 <= psi && psi <= -0.25)
                        {
                            C1 = interpolation(-0.5, 2.42, -0.25, 2.15, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = interpolation(-0.5, 0.95, -0.25, 1, psi.Value);
                            }
                            else
                            {
                                C3 = interpolation(-0.5, 0.77 - psif, -0.25, 0.65, psi.Value);
                            }
                        }
                        else if (-0.75 <= psi && psi <= -0.5)
                        {
                            C1 = interpolation(-0.75, 2.45, -0.50, 2.42, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = interpolation(-0.75, 0.85, -0.5, 0.95, psi.Value);
                            }
                            else
                            {
                                C3 = interpolation(-0.75, 0.35 - psif, -0.5, 0.77 - psif, psi.Value);
                            }
                        }
                        else if (-1 <= psi && psi <= -0.75)
                        {
                            C1 = 2.45;
                            if (psif <= 0)
                            {
                                C3 = interpolation(-1.0, 0.125 - 0.7 * psif, -0.75, 0.85, psi.Value);
                            }
                            else
                            {
                                C3 = interpolation(-1.0, -0.125 - 0.7 * psif, -0.75, 0.35 - psif, psi.Value);
                            }
                        }
                        else                        
                            throw new Exception("cannot calc McrLT, psi < -1 or psi > 1!");                        
                    }
                    else                    
                        throw new Exception("cannot calc McrLT, k != 1 or k != 0.5");                    
                }
                else                
                    throw new Exception("cannot calc McrLT, no literature");
                
            }
            else if (supportCondition == EN1993p11Options.SupportConditions.HingesAtEnds)
            {
                if (k == 1)
                {
                    if (loadCondition == EN1993p11Options.LoadConditions.Constant)
                    {
                        C1 = 1.127;
                        C2 = 0.45;
                        C3 = 0.525;
                    }
                    else if (loadCondition == EN1993p11Options.LoadConditions.SingleForce)
                    {
                        C1 = 1.35;
                        C2 = 0.59;
                        C3 = 0.411;
                    }
                }
                else if (k == 0.5)
                {
                    if (loadCondition == EN1993p11Options.LoadConditions.Constant)
                    {
                        C1 = 0.97;
                        C2 = 0.36;
                        C3 = 0.478;
                    }
                    else if (loadCondition == EN1993p11Options.LoadConditions.SingleForce)
                    {
                        C1 = 1.05;
                        C2 = 0.48;
                        C3 = 0.338;
                    }
                }
                else                
                    throw new Exception("cannot calc McrLT, no literature");                
            }
            else            
                throw new Exception("cannot calc McrLT, no literature");            
        }

        protected double CalculateLateralTorsionalBucklingMomentCapacity(ResultBeamForces resultBeamForces, SectionClass sectionClass, 
            ISteelSection section, EN1993p11Options options)
        {                       
            if(section is SteelSectionH)
            {
                return CalculateMbRdMod(section, sectionClass, resultBeamForces, options.SupportCondition, options.LoadCondition,
                options.LateralSupportCondition, options.LateralWarpingCondition, options.Psi1Axis, options.LoadApplicationPoint);
            }
            else
                return CalculateMbRd(section, sectionClass, resultBeamForces, options.SupportCondition, options.LoadCondition, 
                options.LateralSupportCondition, options.LateralWarpingCondition, options.Psi1Axis, options.LoadApplicationPoint);
        }

        #endregion

        #region Torsional-flexural buckling

        protected double GetNcrT(ISteelSection section)  //EN 1993-1-3 eq 6.33a => SOLO COLD FORMED
        {
            double G = Material.E / (2.0 * (1.0 + Material.Ni));
            double i0 = Math.Pow(Math.Pow(section.R11, 2.0) + Math.Pow(section.R22, 2.0) + 
                Math.Pow((section.ShearCenter.X - section.Centroid.X), 2.0) + Math.Pow((section.ShearCenter.Y - section.Centroid.Y), 2.0), 0.5);           
            
            return 1.0 / Math.Pow(i0, 2.0) * (G * section.Jt + Math.Pow(Math.PI, 2.0) * Material.E * section.Jw / Math.Pow(GetLengthLatTorsBuckling(), 2.0));                        
        }

        protected double GetNcrTF(ISteelSection section, double Ncr1, double Ncr2, double NcrT) //EN 1993-1-3 eq 6.35
        {
            double x0 = section.ShearCenter.X - section.Centroid.X;
            double y0 = section.ShearCenter.Y - section.Centroid.Y;

            double i0 = Math.Pow(Math.Pow(section.R11, 2.0) + Math.Pow(section.R22, 2.0) + Math.Pow(x0, 2.0) + Math.Pow(y0, 2.0), 0.5);
            double beta;
            double NcrTF;
            if (y0 == 0 && x0 != 0)
            {
                beta = 1.0 - Math.Pow(x0 / i0, 2.0);
                NcrTF = Ncr1 / (2.0 * beta) * (1.0 + NcrT / Ncr1 - Math.Pow(Math.Pow(1.0 - NcrT / Ncr1, 2.0) + 4.0 * Math.Pow(x0 / i0, 2.0) * NcrT / Ncr1, 0.5));
            }
            else if (x0 == 0 && y0 != 0)
            {
                beta = 1.0 - Math.Pow(y0 / i0, 2.0);
                NcrTF = Ncr2 / (2.0 * beta) * (1.0 + NcrT / Ncr2 - Math.Pow(Math.Pow(1.0 - NcrT / Ncr2, 2.0) + 4.0 * Math.Pow(y0 / i0, 2.0) * NcrT / Ncr2, 0.5));
            }
            else if (y0 == 0 && x0 == 0)
            {
                return NcrT;
            }
            else
            {
                /* solve --> x:
                 * (Ncrz * x ) * (Ncry * x) * (NcrT * x) - (Ncrz - x) * x^2 * x0^2/i0^2 - (Ncry - x) * x^2 * y0^2/i0^2 = 0
                 * x = g(x)
                 * x = - (Ncrz - x) / ((NcrT - x) * (Ncry - x)) * x * x * x0*x0/(i0*i0) - (Ncry - x) / ((NcrT - x) * (Ncry - x)) * x * x * y0*x0/(i0*i0) + Ncry;
                 * check if Ncry and Ncrz should be inverted in this equation
                 */
                int iter = 0;
                NcrTF = 0;
                while (iter < 15)
                {
                    iter++;
                    NcrTF = -(Ncr2 - NcrTF) / ((NcrT - NcrTF) * (Ncr1 - NcrTF)) * NcrTF * NcrTF * y0 * y0 / (i0 * i0) - 
                        (Ncr1 - NcrTF) / ((NcrT - NcrTF) * (Ncr1 - NcrTF)) * NcrTF * NcrTF * x0 * y0 / (i0 * i0) + Ncr1;
                }
                //to be checked
            }

            return GetMin(new double[] { NcrTF, Ncr1, Ncr2, NcrT });
        }

        //protected double CalculateMcr0(ISteelSection section, SectionClass sectionClass)
        //{
        //    double ncrT = GetNcrT(section);
        //    double ncrTF = GetNcrTF(section, CalculateAxialBucklingCapacity1Axis(sectionClass, section), CalculateAxialBucklingCapacity2Axis(sectionClass, section), ncrT);

        //    double lambdaT = GetLambdaSigned(sectionClass, section, Math.Min(ncrT, ncrTF));

        //}

        protected double CalculateMcr0ForInteraction(ISteelSection section, ResultBeamForces forces,
            EN1993p11Options.SupportConditions supportCondition, EN1993p11Options.LoadConditions loadConditions = EN1993p11Options.LoadConditions.NotDirectlyLoaded,
            EN1993p11Options.LateralSupportConditions lateralSupportConditions = EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Options.LateralWarpingConditions  lateralWarpingConditions = EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 
                EN1993p11Options.LoadApplicationPoints loadApplicationPoints = EN1993p11Options.LoadApplicationPoints.TopSection)
        {
            return CalculateMcr(section, forces, GetLengthCriticalMoment1(), supportCondition, loadConditions, lateralSupportConditions,
                lateralWarpingConditions, 1.0, loadApplicationPoints, 1.0, 0.0);
        }



        #endregion

        #region Interaction
            
        /// <summary>
        /// EN1993-1-1 mu coefficient for interaction
        /// </summary>
        protected double GetMu(double Ned, double Ncr, double Chi)
        {
            return (1.0 - Ned / Ncr) / (1.0 - Chi * Ned / Ncr);
        }

        /// <summary>
        /// EN1993-1-1 Cmi0 coefficient for interaction
        /// </summary>
        protected double GetCMi0(EN1993p11Options.LoadConditions loadCondition, EN1993p11Options.SupportConditions supportCondition, double? psi, double? MEdMax, double? deflection, double NEd, double Ncr)
        {
            if (loadCondition == EN1993p11Options.LoadConditions.SingleForce && supportCondition == EN1993p11Options.SupportConditions.HingesAtEnds)            
                return 1.0 - 0.18 * NEd / Ncr;
            
            else if (loadCondition == EN1993p11Options.LoadConditions.Constant && supportCondition == EN1993p11Options.SupportConditions.HingesAtEnds)            
                return 1 + 0.03 * NEd / Ncr;
            
            else if (loadCondition == EN1993p11Options.LoadConditions.NotDirectlyLoaded)
            {
                if (psi.HasValue)                
                    return 0.79 + 0.21 * psi.Value + 0.36 * (psi.Value - 0.33) * Math.Abs(NEd) / Ncr;
                
                else                
                    throw new Exception("Set a value to ψ = M(x=0)/M(x=L);");                
            }
            else
            {
                if (deflection.HasValue && MEdMax.HasValue)                
                    return 1.0 + (Math.PI * Math.PI * Material.E * Math.Abs(deflection.Value) / (_beamCheckerAttributes.Length * _beamCheckerAttributes.Length * MEdMax.Value) - 1.0) * NEd / Ncr;
                
                else                
                    throw new Exception("Set delta and Mmax - Table A.2 EN 1993-1-1. Not yet implemented");                
            }
        }

        protected double CalculateLambda0LimitForFlexuralTorsionaBuckling(ResultBeamForces forces, double kc, double NcrFlexTors, double NcrTorsional)
        {
            return 0.2 * Math.Pow(Math.Pow(kc, -2.0), 0.5) * Math.Pow((1.0 - Math.Abs(forces.N) / NcrTorsional) * (1.0 - Math.Abs(forces.N) / NcrFlexTors), 0.25);
        }

        protected void CalculateCmFactorsForInteraction(ISteelSection section, SectionClass sectionClass, ResultBeamForces forces,
            EN1993p11Options.LoadConditions loadCondition, EN1993p11Options.SupportConditions supportCondition, double? psi, out double epsilony,
            out double cmx, out double cmy, out double cmLT, out double mux, out double muy, out double wx, out double wy, out double Mcr0,
            EN1993p11Options.LateralSupportConditions lateralSupportCondition = EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Options.LateralWarpingConditions lateralWarpingCondition = EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                EN1993p11Options.LoadApplicationPoints loadApplicationPoint = EN1993p11Options.LoadApplicationPoints.TopSection)
        {
            Mcr0 = CalculateMcr0ForInteraction(section, forces, supportCondition, loadCondition, lateralSupportCondition,
                lateralWarpingCondition, loadApplicationPoint);
            double lambdaSignedLTB0 = GetLambdaSignedLTB(section, Mcr0);

            double kc = Getkc(supportCondition, loadCondition, psi);

            double ncrt = GetNcrT(section);
            double ncr1 = GetNcrEuler(GetLengthAxialBuckling1(), section.J11);
            double ncr2 = GetNcrEuler(GetLengthAxialBuckling2(), section.J22);
            double ncrtf = GetNcrTF(section, ncr1, ncr2, ncrt);

            double lambda0limit = CalculateLambda0LimitForFlexuralTorsionaBuckling(forces, kc, ncrtf, ncrt);

            double lambdaSegnato1 = GetLambdaSigned(sectionClass, section, ncr1);

            EN1993p11Options.AxialBuckingCurves buckingCurves1 = GetBucklingCurve1Axis(section);
            double alpha1 = GetImperfectionFactorBucklingCurve(buckingCurves1);
            double phi1 = GetPhiForBuckling(alpha1, lambdaSegnato1);
            double chi1 = GetChi(phi1, lambdaSegnato1);

            double lambdaSegnato2 = GetLambdaSigned(sectionClass, section, ncr2);

            EN1993p11Options.AxialBuckingCurves buckingCurves2 = GetBucklingCurve2Axis(section);
            double alpha2 = GetImperfectionFactorBucklingCurve(buckingCurves2);
            double phi2 = GetPhiForBuckling(alpha2, lambdaSegnato2);
            double chi2 = GetChi(phi2, lambdaSegnato2);

            mux = GetMu(forces.N, ncr1, chi1);
            muy = GetMu(forces.N, ncr2, chi2);

            epsilony = CalculateEpsilonyForInteraction(sectionClass, forces, section);

            if (lambdaSignedLTB0 <= lambda0limit)
            {
                cmx = GetCMi0(loadCondition, supportCondition, psi, null, null, forces.N, ncr1);
                cmy = GetCMi0(loadCondition, supportCondition, psi, null, null, forces.N, ncr2);
                cmLT = 1.0;
            }
            else
            {
                double cmx0 = GetCMi0(loadCondition, supportCondition, psi, null, null, forces.N, ncr1);
                double alphaLT = CalculateAlphaLTForInteraction(section);
                
                cmx = cmx0 + (1.0 - cmx0) * Math.Sqrt(epsilony) * alphaLT / (1.0 + Math.Sqrt(epsilony) * alphaLT); 

                cmy = GetCMi0(loadCondition, supportCondition, psi, null, null, forces.N, ncr2);

                cmLT = Math.Max(Math.Pow(cmx, 2) * alphaLT / Math.Sqrt((1.0 - Math.Abs(forces.N) / ncr2) * (1.0 - Math.Abs(forces.N) / ncrt)), 1.0);
            }

            if(sectionClass != SectionClass.Class4)
            {
                wx = Math.Min(section.Wpl1 / section.Wel1, 1.5);
                wy = Math.Min(section.Wpl2 / section.Wel2, 1.5);
            }
            else
            {
                wx = 1.0;
                wy = 1.0;   //TODO: sezioni eff???
            }
        }

        protected void CalculateCoefficientsForInteraction(ISteelSection section, SectionClass sectionClass, ResultBeamForces forces,
            EN1993p11Options.LoadConditions loadCondition, EN1993p11Options.SupportConditions supportCondition,
            EN1993p11Options.LateralSupportConditions lateralCondition, EN1993p11Options.LateralWarpingConditions warpingCondition, EN1993p11Options.LoadApplicationPoints loadApplicationPoint,
            double? psi, out double epsilony, out double bLT, out double cLT, out double dLT, out double eLT,
            out double cxx, out double cxy, out double cyx, out double cyy,
            out double cmx, out double cmy, out double cmLT, out double mux, out double muy, out double wx, out double wy)
        {
            CalculateCmFactorsForInteraction(section, sectionClass, forces, loadCondition, supportCondition, psi, out epsilony,
                out cmx, out cmy, out cmLT, out mux, out muy, out wx, out wy, out double Mcr0, lateralCondition, warpingCondition, loadApplicationPoint);

            double mrd1 = CalculateBendingMoment1Capacity(forces, sectionClass, section);
            double mrd2 = CalculateBendingMoment2Capacity(forces, sectionClass, section);

            double alpha;
            if (section is SteelSectionH)
                alpha = GetImperfectionFactorLTMod(section);
            else
                alpha = GetImperfectionFactorLT(section);
            double kc = Getkc(supportCondition, loadCondition, psi);

            double Mcr = CalculateMcr(section, forces, GetLengthCriticalMoment1(), supportCondition, loadCondition, lateralCondition, warpingCondition, psi, loadApplicationPoint);
            double lambdaSigned = GetLambdaSignedLTB(section, Mcr);
            double phi = GetPhiForBuckling(alpha, lambdaSigned, EN1993P11.BetaForLateralTorsionalBuckling, EN1993P11.LambdaLT0ForLateralTorsionalBuckling);
            double chiMod = GetChiLTmod(phi, lambdaSigned, EN1993P11.BetaForLateralTorsionalBuckling, GetFactorFForLTB(kc, lambdaSigned));
                        
            double ncr1 = GetNcrEuler(GetLengthAxialBuckling1(), section.J11);
            double ncr2 = GetNcrEuler(GetLengthAxialBuckling2(), section.J22);
            
            double lambda1 = GetLambdaSigned(sectionClass, section, ncr1);
            double lambda2 = GetLambdaSigned(sectionClass, section, ncr2);
            double lambdaMax = Math.Max(lambda1, lambda2);

            double lambda0 = GetLambdaSignedLTB(section, Mcr0);
            double alphaLT = CalculateAlphaLTForInteraction(section);

            bLT = 0.5 * alphaLT * lambda0 * lambda0 * Math.Abs(forces.M1) * Math.Abs(forces.M2) / (chiMod * mrd1 * mrd2);
            cLT = 10.0 * alphaLT * lambda0 * lambda0 * Math.Abs(forces.M1) / ((5.0 + Math.Pow(lambda2, 4.0)) * cmx * chiMod * mrd1);
            dLT = 2.0 * alphaLT * lambda0 * Math.Abs(forces.M1) * Math.Abs(forces.M2) / ((0.1 + Math.Pow(lambda2, 4.0)) * cmx * chiMod * mrd1 * cmy * mrd2);
            eLT = 1.7 * alphaLT * lambda0 * Math.Abs(forces.M1) / ((0.1 + Math.Pow(lambda2, 4.0)) * cmx * chiMod * mrd1);

            double npl = Math.Abs(forces.N) / (Material.Fyk * section.Area / GammaM1);

            cxx = Math.Max(1.0 + (wx - 1.0) * ((2.0 - 1.6 / wx * Math.Pow(cmx, 2) * lambdaMax - 1.6 / wx * Math.Pow(cmx, 2) * Math.Pow(lambdaMax, 2)) * npl - bLT),
                section.Wel1 / section.Wpl1);
            cxy = Math.Max(1.0 + (wy - 1.0) * ((2.0 - 14.0 * Math.Pow(cmy, 2) * Math.Pow(lambdaMax, 2) / Math.Pow(wy, 5.0)) * npl - cLT),
                0.6 * Math.Sqrt(wy / wx) * section.Wel2 / section.Wpl2);
            cyx = Math.Max(1.0 + (wx - 1.0) * ((2.0 - 14.0 * Math.Pow(cmx, 2) * Math.Pow(lambdaMax, 2) / Math.Pow(wx, 5.0)) * npl - dLT),
                0.6 * Math.Sqrt(wx / wy) * section.Wel1 / section.Wpl1);
            cyy = Math.Max(1.0 + (wy - 1.0) * ((2.0 - 1.6 / wy * Math.Pow(cmy, 2) * lambdaMax - 1.6 / wy * Math.Pow(cmy, 2) * Math.Pow(lambdaMax, 2)) - eLT) * npl,
                section.Wel2 / section.Wpl2); //RIGHT VERSION
        }

        protected void CalculateKCoefficientForInteraction(ISteelSection section, SectionClass sectionClass, ResultBeamForces forces,
            EN1993p11Options.LoadConditions loadCondition, EN1993p11Options.SupportConditions supportCondition,
            EN1993p11Options.LateralSupportConditions lateralCondition, EN1993p11Options.LateralWarpingConditions warpingCondition, EN1993p11Options.LoadApplicationPoints loadApplicationPoints, double? psi, out double kxx, out double kxy, out double kyx, out double kyy)
        {
            CalculateCoefficientsForInteraction(section, sectionClass, forces, loadCondition, supportCondition,
                lateralCondition, warpingCondition, loadApplicationPoints, psi, out double epsilony, out double bLT, out double cLT, out double dLT, out double eLT,
                out double cxx, out double cxy, out double cyx, out double cyy,
                out double cmx, out double cmy, out double cmLT, out double mux, out double muy, out double wx, out double wy);

            double ncr1 = GetNcrEuler(GetLengthAxialBuckling1(), section.J11);
            double ncr2 = GetNcrEuler(GetLengthAxialBuckling2(), section.J22);

            if (sectionClass == SectionClass.Class1 || sectionClass == SectionClass.Class2)
            {
                kxx = cmx * cmLT * mux / (1.0 - Math.Abs(forces.N) / ncr1) * 1.0 / cxx;
                kxy = cmy * mux / (1.0 - Math.Abs(forces.N) / ncr2) * 1.0 / cxy * 0.6 * Math.Sqrt(wy / wx);
                kyx = cmx * cmLT * muy / (1.0 - Math.Abs(forces.N) / ncr1) * 1.0 / cyx * 0.6 * Math.Sqrt(wx / wy);
                kyy = cmy * muy / (1.0 - Math.Abs(forces.N) / ncr2) * 1.0 / cyy;
            }
            else
            {
                kxx = cmx * cmLT * mux / (1.0 - Math.Abs(forces.N) / ncr1);
                kxy = cmy * mux / (1.0 - Math.Abs(forces.N) / ncr2);
                kyx = cmx * cmLT * muy / (1.0 - Math.Abs(forces.N) / ncr1);
                kyy = cmy * muy / (1.0 - Math.Abs(forces.N) / ncr2);
            }
        }

        protected double CalculateAlphaLTForInteraction(ISteelSection section)
        {
            return Math.Max(1.0 - section.Jt / section.J11, 0); 
        }

        protected double CalculateEpsilonyForInteraction(SectionClass sectionClass, ResultBeamForces forces, ISteelSection section)
        {
            if(sectionClass != SectionClass.Class4)
            {
                return (Math.Abs(forces.M1) / (Math.Abs(forces.N)) * (section.Area / section.Wel1));
            }
            else
            {
                return (Math.Abs(forces.M1) / (Math.Abs(forces.N)) * (section.Area / section.Wel1));    //TODO: implementare
            }
        }

        #endregion

        #region Section Class

        protected SectionClass CalculateSectionClassDueToCombinedBendingAndCompression(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (((Section)section).GetMinSigma(resultBeamForces.N, resultBeamForces.M1, resultBeamForces.M2) < 0.0)
            {
                if (section is SteelSectionH sectionH)
                {
                    SectionClass sectionClass1Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionH.LenghtTopFlange/2 - sectionH.R, sectionH.ThicknessTopFlange),
                        GetClassCompressedOuterPlate(sectionH.LenghtBottomFlange/2 - sectionH.R, sectionH.ThicknessBottomFlange)});

                    sectionClass1Axis = SetWorstClass(sectionClass1Axis,
                        GetClassBendingInnerPlate(resultBeamForces, sectionH.D, sectionH.ThicknessWeb, section, sectionClass1Axis));

                    SectionClass sectionClass2Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionH.LenghtTopFlange/2 - sectionH.R, sectionH.ThicknessTopFlange),
                        GetClassCompressedOuterPlate(sectionH.LenghtBottomFlange/2 - sectionH.R, sectionH.ThicknessBottomFlange)});

                    sectionClass1Axis = SetWorstClass(sectionClass1Axis,
                        GetClassBendingInnerPlate(resultBeamForces, sectionH.D, sectionH.ThicknessWeb, section, sectionClass1Axis));

                    return SetWorstClass(sectionClass1Axis, sectionClass2Axis);
                }

                else if (section is SteelSectionCHS sectionCHS)
                {
                    return GetClassCHSBending(sectionCHS.Diameter, sectionCHS.Thickness);
                }

                else if (section is SteelSectionRHS sectionRHS)
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

                else if (section is SteelSectionT sectionT)
                {
                    return SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionT.LenghtFlange/2 - sectionT.R, sectionT.ThicknessFlange),
                        GetClassCompressedOuterPlate(sectionT.HeightWeb, sectionT.ThicknessWeb)});
                }

                else if (section is SteelSectionC sectionC)
                {
                    SectionClass sectionClass1Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionC.LengthTop - sectionC.R1, sectionC.ThicknessTop),
                        GetClassCompressedOuterPlate(sectionC.LengthBottom - sectionC.R1, sectionC.ThicknessBottom)});

                    sectionClass1Axis = SetWorstClass(sectionClass1Axis,
                        GetClassBendingInnerPlate(resultBeamForces, sectionC.HeightWeb - 2 * sectionC.R1, sectionC.ThicknessWeb, section, sectionClass1Axis));

                    SectionClass sectionClass2Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionC.LengthTop- sectionC.R1, sectionC.ThicknessTop),
                        GetClassCompressedOuterPlate(sectionC.LengthBottom- sectionC.R1, sectionC.ThicknessBottom),
                        GetClassCompressedInnerPlate(sectionC.HeightWeb- 2 * sectionC.R1, sectionC.ThicknessWeb)});

                    return SetWorstClass(sectionClass1Axis, sectionClass2Axis);
                }

                else if (section is SteelSectionL sectionL)
                {
                    return SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionL.HorizontalLegLength, sectionL.HorizontalLegThickness),
                                                            GetClassCompressedOuterPlate(sectionL.VerticalLegLength, sectionL.VerticalLegThickness)});
                }

                else
                    throw new NotImplementedException("CalculateSectionClassException: not implemented Section");
            }
            else
                return SectionClass.Class1;
        }

        /// <summary>
        /// Return the <see cref="SectionClass"/> due of bending compression of the section <paramref name="section"/> with the <paramref name="resultBeamForces"/> - Chapter 5 EC1993-1-1
        /// </summary>
        /// <returns></returns>
        protected SectionClass CalculateSectionClassDueToBending(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (((Section)section).GetMinSigma(resultBeamForces.N, resultBeamForces.M1, resultBeamForces.M2) < 0.0)
            {
                if (section is SteelSectionH sectionH)
                {
                    SectionClass sectionClass1Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionH.LenghtTopFlange/2 - sectionH.R, sectionH.ThicknessTopFlange),
                        GetClassCompressedOuterPlate(sectionH.LenghtBottomFlange/2- sectionH.R, sectionH.ThicknessBottomFlange)});

                    sectionClass1Axis = SetWorstClass(sectionClass1Axis, 
                        GetClassBendingInnerPlate(resultBeamForces, sectionH.D, sectionH.ThicknessWeb, section, sectionClass1Axis));

                    SectionClass sectionClass2Axis = SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionH.LenghtTopFlange/2- sectionH.R, sectionH.ThicknessTopFlange),
                        GetClassCompressedOuterPlate(sectionH.LenghtBottomFlange/2- sectionH.R, sectionH.ThicknessBottomFlange)});

                    sectionClass1Axis = SetWorstClass(sectionClass1Axis,
                        GetClassBendingInnerPlate(resultBeamForces, sectionH.D, sectionH.ThicknessWeb, section, sectionClass1Axis));

                    return SetWorstClass(sectionClass1Axis, sectionClass2Axis);
                }

                else if (section is SectionCHS sectionCHS)
                {
                    return GetClassCHSBending(sectionCHS.Diameter, sectionCHS.Thickness);
                }

                else if (section is SteelSectionRHS sectionRHS)
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

                else if (section is SteelSectionT sectionT)
                {
                    return SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionT.LenghtFlange/2, sectionT.ThicknessFlange),
                        GetClassCompressedOuterPlate(sectionT.HeightWeb, sectionT.ThicknessWeb)});
                }

                else if (section is SteelSectionC sectionC)
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

                else if (section is SteelSectionL sectionL)
                {
                    return SetWorstClass(new SectionClass[] {GetClassCompressedOuterPlate(sectionL.HorizontalLegLength, sectionL.HorizontalLegThickness),
                                                            GetClassCompressedOuterPlate(sectionL.VerticalLegLength, sectionL.VerticalLegThickness)});
                }

                else
                    throw new NotImplementedException("CalculateSectionClassException: not implemented Section");
            }
            else
                return SectionClass.Class1;
        }

        protected SectionClass CalculateSectionClassDueToCompression(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (((Section)section).GetMinSigma(resultBeamForces.N, resultBeamForces.M1, resultBeamForces.M2) < 0.0)
            {
                if (section is SteelSectionH sectionH)
                {
                    return SetWorstClass(new SectionClass[] {
                        GetClassCompressedOuterPlate(sectionH.LenghtTopFlange/2 - sectionH.R, sectionH.ThicknessTopFlange),
                        GetClassCompressedOuterPlate(sectionH.LenghtBottomFlange/2 - sectionH.R, sectionH.ThicknessBottomFlange),
                        GetClassCompressedInnerPlate(sectionH.D, sectionH.ThicknessWeb)});
                }

                else if (section is SectionCHS sectionCHS)
                {
                    return GetClassCHSBending(sectionCHS.Diameter, sectionCHS.Thickness);
                }

                else if (section is SteelSectionRHS sectionRHS)
                {
                    SectionClass sectionClass1Axis = SetWorstClass(new SectionClass[] {
                        GetClassCompressedInnerPlate(sectionRHS.BaseInternal, sectionRHS.ThicknessTop),
                        GetClassCompressedInnerPlate(sectionRHS.BaseInternal, sectionRHS.ThicknessBottom) });

                    //sectionClass1Axis = SetWorstClass(new SectionClass[] {sectionClass1Axis,
                    //    GetClassBendingInnerPlate(resultBeamForces, sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft, section, sectionClass1Axis),
                    //    GetClassBendingInnerPlate(resultBeamForces, sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight, section, sectionClass1Axis)});

                    SectionClass sectionClass2Axis = SetWorstClass(new SectionClass[] {
                        GetClassCompressedInnerPlate(sectionRHS.Heightinternal, sectionRHS.ThicknessWebRight),
                        GetClassCompressedInnerPlate(sectionRHS.Heightinternal, sectionRHS.ThicknessWebLeft) });

                    //sectionClass2Axis = SetWorstClass(new SectionClass[] {sectionClass2Axis,
                    //    GetClassBendingInnerPlate(resultBeamForces, sectionRHS.BaseInternal, sectionRHS.ThicknessTop, section, sectionClass2Axis),
                    //    GetClassBendingInnerPlate(resultBeamForces, sectionRHS.BaseInternal, sectionRHS.ThicknessBottom, section, sectionClass2Axis)});

                    return SetWorstClass(sectionClass1Axis, sectionClass2Axis);
                }

                else if (section is SteelSectionT sectionT)
                {
                    return SetWorstClass(new SectionClass[] {
                        GetClassCompressedOuterPlate(sectionT.LenghtFlange/2, sectionT.ThicknessFlange),
                        GetClassCompressedOuterPlate(sectionT.HeightWeb, sectionT.ThicknessWeb)});
                }

                else if (section is SteelSectionC sectionC)
                {
                    SectionClass sectionClass1Axis = SetWorstClass(new SectionClass[] {
                        GetClassCompressedOuterPlate(sectionC.LengthTop - sectionC.R1, sectionC.ThicknessTop),
                        GetClassCompressedOuterPlate(sectionC.LengthBottom - sectionC.R1, sectionC.ThicknessBottom)});

                    sectionClass1Axis = SetWorstClass(sectionClass1Axis,
                        GetClassBendingInnerPlate(resultBeamForces, sectionC.HeightWeb - 2 * sectionC.R1, sectionC.ThicknessWeb, section, sectionClass1Axis));

                    SectionClass sectionClass2Axis = SetWorstClass(new SectionClass[] {
                        GetClassCompressedOuterPlate(sectionC.LengthTop - sectionC.R1, sectionC.ThicknessTop),
                        GetClassCompressedOuterPlate(sectionC.LengthBottom - sectionC.R1, sectionC.ThicknessBottom),
                        GetClassCompressedInnerPlate(sectionC.HeightWeb - 2 * sectionC.R1, sectionC.ThicknessWeb)});

                    return SetWorstClass(sectionClass1Axis, sectionClass2Axis);
                }

                else if (section is SteelSectionL sectionL)
                {
                    return SetWorstClass(new SectionClass[] {
                        GetClassCompressedOuterPlate(sectionL.HorizontalLegLength, sectionL.HorizontalLegThickness),
                        GetClassCompressedOuterPlate(sectionL.VerticalLegLength, sectionL.VerticalLegThickness)});
                }

                else
                    throw new NotImplementedException("CalculateSectionClassException: not implemented Section");
            }
            else
                return SectionClass.Class1;
        }

        protected SectionClass GetClassCHSBending(double diameter, double thickness)
        {
            if (diameter / thickness <= 50.0 * Epsilon * Epsilon)
                return SectionClass.Class1;

            else if (diameter / thickness <= 70.0 * Epsilon * Epsilon)
                return SectionClass.Class2;

            else if (diameter / thickness <= 90.0 * Epsilon * Epsilon)
                return SectionClass.Class3;

            else
                return SectionClass.Class4;
        }

        protected SectionClass GetClassCompressedOuterPlate(double length, double thickness)
        {
            double ratio = length / thickness;

            if (ratio <= 9.0 * Epsilon)
                return SectionClass.Class1;

            else if (ratio <= 10.0 * Epsilon)
                return SectionClass.Class2;

            else if (ratio <= 14.0 * Epsilon)
                return SectionClass.Class3;

            else
                return SectionClass.Class4;
        }

        protected SectionClass GetClassBendingInnerPlate(ResultBeamForces resultBeamForces, double length, double thickness, ISteelSection section, SectionClass sectionClass)
        {
            double alpha = - resultBeamForces.N / (2.0 * thickness * Fy * length) + 0.5;
            double ctRatio = length / thickness;

            double psi = 1;
            if (sectionClass != SectionClass.Class4)
                psi = -resultBeamForces.N * 2.0 / (section.Area * Fy) - 1.0;

            if (alpha > 0.5 && alpha < 1)
            {
                if (ctRatio <= 396.0 * Epsilon / (13.0 * alpha - 1.0))
                    return SectionClass.Class1;

                else if (ctRatio <= 456.0 * Epsilon / (13.0 * alpha - 1.0))
                    return SectionClass.Class2;

                else
                {
                    if (psi > -1)
                    {
                        if (ctRatio <= 42.0 * Epsilon / (0.67 + 0.33 * psi))
                            return SectionClass.Class3;

                        else
                            return SectionClass.Class4;
                    }

                    else if (psi <= -1)
                    {
                        if (ctRatio <= 62.0 * Epsilon * (1 - psi) * Math.Sqrt(-psi))
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
                if (ctRatio <= 36.0 * Epsilon / alpha)
                    return SectionClass.Class1;

                else if (ctRatio <= 41.5 * Epsilon / alpha)
                    return SectionClass.Class2;

                else
                {
                    if (psi > -1)
                    {
                        if (ctRatio <= 42.0 * Epsilon / (0.67 + 0.33 * psi))
                            return SectionClass.Class3;

                        else
                            return SectionClass.Class4;
                    }
                    else if (psi <= -1)
                    {
                        if (ctRatio <= 62.0 * Epsilon * (1 - psi) * Math.Sqrt(-psi))
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
                    if (ctRatio <= 42.0 * Epsilon / (0.67 + 0.33 * psi))
                        return SectionClass.Class3;

                    else
                        return SectionClass.Class4;
                }
                else if (psi <= -1)
                {
                    if (ctRatio <= 62.0 * Epsilon * (1 - psi) * Math.Sqrt(-psi))
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

            if (ctRatio <= 33.0 * Epsilon)
                return SectionClass.Class1;

            else if (ctRatio <= 38.0 * Epsilon)
                return SectionClass.Class2;

            else if (ctRatio <= 42.0 * Epsilon)
                return SectionClass.Class3;

            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// Return the worst <see cref="SectionClass"/> section between <paramref name="obj1"/> e <paramref name="obj2"/>
        /// </summary>
        /// <returns></returns>
        protected SectionClass SetWorstClass(SectionClass obj1, SectionClass obj2)
        {
            int cl1 = (int)obj1;
            int cl2 = (int)obj2;

            return (SectionClass)Math.Max(cl1, cl2);
        }

        /// <summary>
        /// Return the worst class section between an array of <see cref="SectionClass"/>
        /// </summary>
        /// <returns></returns>
        protected SectionClass SetWorstClass(SectionClass[] obj)
        {
            SectionClass sectionClass = SectionClass.Class1;

            for (int i = 0; i < obj.Count(); i++)
                sectionClass = SetWorstClass(sectionClass, obj[i]);

            return sectionClass;
        }

        #endregion

        #region Area

        protected double GetAreaNet(ISteelSection steelSection)
        {
            // TODO: implementare Area netta
            return steelSection.Area;
        }

        protected double GetWeffMin1(ISteelSection steelSection)
        {
            // TODO: implementare WeffMin1
            return steelSection.Wel1;
        }

        protected double GetWeffMin2(ISteelSection steelSection)
        {
            // TODO: implementare WeffMin2
            return steelSection.Wel2;
        }

        protected double GetAreaEff(ISteelSection section)
        {
            //TODO: implementare area eff
            return section.Area;
        }

        #endregion

        private double GetMin(double[] array)
        {
            double outvalue = double.MaxValue;

            for (int i = 0; i < array.Length; i++)
                if (array[i] < outvalue)
                    outvalue = array[i];

            return outvalue;
        }

        #endregion

        public class EN1993p11Options : Options
        {
            #region Enumerable

            public enum AxialBuckingCurves
            {
                a0,
                a,
                b,
                c,
                d
            }

            public enum LateralTorsionalBuckingCurves
            {
                a,
                b,
                c,
                d
            }

            public enum LoadConditions
            {
                Constant,
                SingleForce,
                NotDirectlyLoaded
            }

            public enum LoadApplicationPoints
            {
                TopSection,
                ShearCenter
            }

            public enum SupportConditions
            {
                HingesAtEnds,
                EndsRestrained,
                OneSideRestrained_OneSideHinged
            }

            public enum LateralSupportConditions
            {
                HingesAtEnds,
                EndsRestrained,
                OneSideRestrained_OneSideHinged
            }

            public enum LateralWarpingConditions
            {
                HingesAtEnds,
                EndsRestrained,
                OneSideRestrained_OneSideHinged
            }

            #endregion

            #region Variables

            private SupportConditions _supportCondition;
            private LateralSupportConditions _lateralSupportCondition;
            private LateralWarpingConditions _lateralWarpingCondition;
            private LoadConditions _loadCondition;
            private LoadApplicationPoints _loadApplicationPoint;

            private double _psi1;   // distribuzione dei momenti lungo la trave
            private double _psi2;   // distribuzione dei momenti lungo la trave

            #endregion

            #region Properties

            public SupportConditions SupportCondition { get => _supportCondition; set => _supportCondition = value; } 

            public LateralSupportConditions LateralSupportCondition { get => _lateralSupportCondition; set => _lateralSupportCondition = value; }

            public LateralWarpingConditions LateralWarpingCondition { get => _lateralWarpingCondition; set => _lateralWarpingCondition = value; }

            public LoadConditions LoadCondition { get => _loadCondition; set => _loadCondition = value; }

            public LoadApplicationPoints LoadApplicationPoint { get => _loadApplicationPoint; set => _loadApplicationPoint = value; }

            /// <summary>
            /// Ratio between bending moment at the ends of the beam about 1-Axis
            /// </summary>
            public double Psi1Axis { get => _psi1; set => _psi1 = value; }

            /// <summary>
            /// Ratio between bending moment at the ends of the beam about 2-Axis
            /// </summary>
            public double Psi2Axis { get => _psi2; set => _psi2 = value; } 

            #endregion

            #region Constructor

            public EN1993p11Options(LoadConditions loadCondition, SupportConditions supportCondition, 
                LateralSupportConditions lateralSupportCondition, LateralWarpingConditions lateralWarpingCondition, double? psi1, double? psi2,
                double unbracedLengthFactorAxialBuck1 = 1, double effectiveLengthFactorAxialBuck1 = 1, double unbracedLengthFactorAxialBuck2 = 1,
                double effectiveLengthFactorAxialBuck2 = 1, double UnbracedLengthFactorLatTorsBuck = 1, double effectiveLengthFactorLatTorsBuck = 1,
                double unbracedLengthFactorCriticalMoment1 = 1, double effectiveLengthFactorCriticalMoment1 = 1,
                double unbracedLengthFactorCriticalMoment2 = 1, double effectiveLengthFactorCriticalMoment2 = 1,
                double eqvUniformMomentFactorm1 = 1, double eqvUniformMomentFactorm2 = 1, double eqvUniformMomentFactormLT = 1,
                LoadApplicationPoints loadApplicationPoint = LoadApplicationPoints.TopSection)
                : base(unbracedLengthFactorAxialBuck1, effectiveLengthFactorAxialBuck1, unbracedLengthFactorAxialBuck2,
                      effectiveLengthFactorAxialBuck2, UnbracedLengthFactorLatTorsBuck, effectiveLengthFactorLatTorsBuck, 
                      unbracedLengthFactorCriticalMoment1, effectiveLengthFactorCriticalMoment1,
                      unbracedLengthFactorCriticalMoment2, effectiveLengthFactorCriticalMoment2,
                      eqvUniformMomentFactorm1, eqvUniformMomentFactorm2, eqvUniformMomentFactormLT)
            {
                _loadCondition = loadCondition;
                _supportCondition = supportCondition;
                _lateralSupportCondition = lateralSupportCondition;
                _lateralWarpingCondition = lateralWarpingCondition;
                _loadApplicationPoint = loadApplicationPoint;

                if (psi1 != null)
                    _psi1 = (double)psi1;
                if (psi2 != null)
                    _psi2 = (double)psi2;
            }

            #endregion

            #region Setter

            public void SetSupportCondition(SupportConditions supportCondition)
            {
                _supportCondition = supportCondition;
            }

            public void SetLateralSupportCondition(LateralSupportConditions lateralSupportCondition)
            {
                _lateralSupportCondition = lateralSupportCondition;
            }

            public void SetLateralWarpingCondition(LateralWarpingConditions lateralWarpingCondition)
            {
                _lateralWarpingCondition = lateralWarpingCondition;
            }

            public void SetLoadCondition(LoadConditions loadCondition)
            {
                _loadCondition = loadCondition;
            }

            public void SetPsi1(double psi)
            {
                _psi1 = psi;
            }

            public void SetPsi2(double psi)
            {
                _psi2 = psi;
            }

            #endregion
        }
    }
}
