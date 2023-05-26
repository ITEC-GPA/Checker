using GPC.Checkers.Steel.Results;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Checkers
{
    public class EN1993BoltChecker : BoltChecker
    {
        #region Enumerable

        public enum HoleShapeType
        {
            NormalRound, // Bolts in normal holes.
            OversizeRound, // Bolts in oversized holes.
            ShortSlotted, // Bolts in short slotted holes.
            LongSlotted // Bolts in long slotted holes.
        }

        /// <summary>
        /// Slip factor, μ, for pre-loaded bolts.
        /// UNI EN 1993-1-8:2005 - Table 3.7.
        /// EN 1090-2:2008 - Table 18 - Classifications for friction surfaces.
        /// </summary>
        public enum ClassFrictionSurfacesType
        {
            A,
            B,
            C,
            D
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - 3.4.1 Shear connections.
        /// </summary>
        public enum ShearConnectionsCategoryType
        {
            A, // Category A: Bearing type.
            B, // Category B: Slip-resistant at serviceability limit state.
            C  // Category C: Slip-resistant at ultimate limit state.
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - 3.4.2 Tension connections.
        /// </summary>
        public enum TensionConnectionsCategoryType
        {
            D, // Category D: non-preloaded.
            E  // Category E: preloaded.
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.3: Minimum and maximum spacing, end and edge distances.
        /// </summary>
        public enum ExposureConditionType
        {
            SteelExposed, // Steel exposed to the weather or other corrosive influences
            SteelNotExposed, // Steel not exposed to the weather or other corrosive influences
            SteelUnprotected // Steel used unprotected
        }

        #endregion

        #region Public Constructor

        public EN1993BoltChecker(PlateWithBolts plateWithBolts, List<BoltStresses> boltStresses, StandardEN1993p11 standard, EN1993BoltOptions options, int id = IDUNASSIGNED, string name = "")
            : base(plateWithBolts, boltStresses, standard, options, id, name)
        { }

        #endregion

        #region Public Properties

        public StandardEN1993p11 StandardEN1993 => (StandardEN1993p11)_standard;

        public EN1993BoltOptions OptionsEN1993 => (EN1993BoltOptions)_options;

        public List<EN1993BoltResults> BoltResultsEN1993 => _boltResults.Cast<EN1993BoltResults>().ToList();

        public EN1993BoltResults BoltResultMax => (EN1993BoltResults)_boltResultMax;

        #endregion

        #region Public Methods

        /// <summary>
        /// Permorme all checks.
        /// </summary>
        public override void PerformCheck()
        {
            _boltResults = new List<BoltResults> { };

            // ****** Set hole type.
            foreach (var boltPos in _plateWithBolts.BoltGrid.Bolts)
                SetHoleDiameter(boltPos, OptionsEN1993.HoleShape);

            // ****** Indipendent from sollecitations:
            // - Distances eMin for all holes.
            // - Distances eMax for all holes.
            // - Inner or outer type.
            var boltsDistancesEmax = new Dictionary<BoltPosition, double>();
            var boltsDistancesEmin = new Dictionary<BoltPosition, double>();
            var boltsAreOuter = new Dictionary<BoltPosition, bool>();
            foreach (var boltPos in _plateWithBolts.BoltGrid.Bolts)
            {
                boltsDistancesEmin[boltPos] = _plateWithBolts.CalculateClosestEdgePoint(boltPos, out Line2d _).Length;
                boltsDistancesEmax[boltPos] = _plateWithBolts.CalculateFurtherMinimumEdgePoint(boltPos, out Line2d _)?.Length ?? double.NaN;
                boltsAreOuter[boltPos] = _plateWithBolts.IsOuuter(boltPos);
            }

            foreach (var SolForce in _boltStresses)
            {
                // ****** Sollecitation/Stress.
                // Reduce the forces according to the number of cutting planes.
                var ReducedForces = SolForce.ResBeamForces / OptionsEN1993.NumShearPlane;
                // Calculate all shear forces for each bolt.
                var SollAllBolts = _plateWithBolts.BoltGrid.CalculateShearForcesElastic(ReducedForces);
                // Calculate uniform tension forces for each bolt.
                var SollN = SolForce.ResBeamForces.N > 0.0 ? SolForce.ResBeamForces.N / SollAllBolts.Count() : 0;
                if (SollN > 1) // Positive for tension.
                    foreach (var SollBolt in SollAllBolts)
                        SollBolt.Value.N = SollN;

                foreach (var SollBolt in SollAllBolts)
                {
                    // ****** Initialize a result. ******
                    var CurRes = new EN1993BoltResults(SollBolt.Key, SolForce.LoadCase, SollBolt.Value, StandardEN1993, OptionsEN1993)
                    {
                        SollCombCase = SolForce.CombCase,
                        SollShear = SollBolt.Value.GetCombinedShearForce(),
                        SollTension = SollBolt.Value.N
                    };
                    CurRes.SetUnnecessaryVerification();
                    // var SollShearAngle = CurRes.SollShearIsNull ? 0 : Math.Atan2(SollBolt.Value.V2, SollBolt.Value.V1) + Math.PI;

                    // ****** Shear. ******
                    if (CurRes.ShearIsActive)
                    {
                        CurRes.ShearAlphaV = CalculateAlphaV(SollBolt.Key.BoltDef.BoltMaterial);
                        CurRes.ShearResistance = CalculateShearResistance_FvRd(SollBolt.Key.BoltDef, CurRes.ShearAlphaV);
                        CurRes.ShearRatio = GetWorkingRatio(CurRes.SollShear, CurRes.ShearResistance);
                    }

                    // ****** Bearing. ******
                    if (CurRes.BearingIsActive || CurRes.DistanceIsActive)
                    {
                        CurRes.BearingE1 = _plateWithBolts.CalculateE1(SollBolt.Key, SollBolt.Value);
                        CurRes.BearingP1 = _plateWithBolts.CalculateP1(SollBolt.Key, SollBolt.Value);
                        CurRes.BearingE2 = _plateWithBolts.CalculateE2(SollBolt.Key, SollBolt.Value);
                        CurRes.BearingP2 = _plateWithBolts.CalculateP2(SollBolt.Key, SollBolt.Value);
                    }
                    if (CurRes.BearingIsActive)
                    {
                        var AlphaD = CalculateCoeffParallel_AlphaD(CurRes.BearingE1, CurRes.BearingP1, SollBolt.Key);
                        CurRes.Bearingk1 = CalculateCoeffPerpendicular_k1(CurRes.BearingE2, CurRes.BearingP2, SollBolt.Key);
                        CurRes.BearingAlphaB = CalculateCoeffParallel_AlphaB(AlphaD, SollBolt.Key.BoltDef, _plateWithBolts);
                        CurRes.BearingResistance = CalculateBearingResistance_FbRd(CurRes.Bearingk1, CurRes.BearingAlphaB, SollBolt.Key.BoltDef, _plateWithBolts);
                        CurRes.BearingRatio = GetWorkingRatio(CurRes.SollShear, CurRes.BearingResistance);
                    }

                    // ****** Slip. ******
                    if (CurRes.SlipIsActive)
                    {
                        var holeType = CalculateHoleType(SollBolt.Key);
                        var holeDir = CalculateIsSlottedPerpendicular(SollBolt.Key.Hole, SollBolt.Value);
                        CurRes.SlipKs = Calculate_ks(holeType, holeDir);
                        CurRes.SlipMu = CalculateSlipFactor_Mu();
                        CurRes.SlipFpc = CalculateSlipPreloading(SollBolt.Key.BoltDef);
                        CurRes.SlipResistance = CalculateDesignSlipResistance_FsRd(CurRes.SlipKs, CurRes.SlipMu, CurRes.SollTension, CurRes.SlipFpc);
                        CurRes.SlipRatio = GetWorkingRatio(CurRes.SollShear, CurRes.SlipResistance);
                    }

                    // ****** SlipSer. ******
                    if (CurRes.SlipSerIsActive)
                    {
                        var holeType = CalculateHoleType(SollBolt.Key);
                        var holeDir = CalculateIsSlottedPerpendicular(SollBolt.Key.Hole, SollBolt.Value);
                        CurRes.SlipSerKs = Calculate_ks(holeType, holeDir);
                        CurRes.SlipSerMu = CalculateSlipFactor_Mu();
                        CurRes.SlipSerFpc = CalculateSlipPreloading(SollBolt.Key.BoltDef);
                        CurRes.SlipSerResistance = CalculateDesignSlipResistance_FsRdser(CurRes.SlipSerKs, CurRes.SlipSerMu, CurRes.SollTension, CurRes.SlipSerFpc);
                        CurRes.SlipSerRatio = GetWorkingRatio(CurRes.SollShear, CurRes.SlipSerResistance);
                    }

                    // ****** Net. ******
                    if (CurRes.NetIsActive)
                    {

                    }

                    // ****** Tension. ******
                    if (CurRes.TensionIsActive)
                    {
                        CurRes.TensionK2 = CalculateK_2();
                        CurRes.TensionResistance = CalculateTensionResistance_FtRd(CurRes.TensionK2, SollBolt.Key.BoltDef);
                        CurRes.TensionRatio = GetWorkingRatio(CurRes.SollTension, CurRes.TensionResistance);
                    }

                    // ****** Combined Shear and Tension. ******
                    if (CurRes.CombinedShearTensionIsActive)
                    {
                        CurRes.CombinedShearTensionRatio = CurRes.SollShear / CurRes.ShearResistance + CurRes.SollTension / (1.4 * CurRes.TensionResistance);
                    }

                    // ****** Punching. ******
                    if (CurRes.PunchingIsActive)
                    {
                        CurRes.PunchingDm = SollBolt.Key.BoltDef.CalculateMeanDiameterBoltHead();
                        CurRes.PunchingResistance = CalculatePunchingShearResistance_BpRd(_plateWithBolts, CurRes.PunchingDm);
                        CurRes.PunchingRatio = GetWorkingRatio(CurRes.SollTension, CurRes.PunchingResistance);
                    }

                    // ****** Distances warning. ******
                    if (CurRes.DistanceIsActive)
                    {
                        CalculateDimesionLimits(SollBolt.Key.Hole.Diameter, _plateWithBolts.Thickness, _plateWithBolts.Thickness,
                            out double dE1E2Min, out double dE1E2Max, out double dE3E4Min, out double dE3E4Max,
                            out double dP1Min, out double dP1Max, out double dP2Min, out double dP2Max);
                        CurRes.DistanceE1E2Min = dE1E2Min;
                        CurRes.DistanceE1E2Max = dE1E2Max;
                        CurRes.DistanceE3E4Min = dE3E4Min;
                        CurRes.DistanceE3E4Max = dE3E4Max;
                        CurRes.DistanceP1Min = dP1Min;
                        CurRes.DistanceP1Max = dP1Max;
                        CurRes.DistanceP2Min = dP2Min;
                        CurRes.DistanceP2Max = dP2Max;

                        CurRes.DistanceIsOuter = boltsAreOuter[SollBolt.Key];

                        double eMin = boltsDistancesEmin[SollBolt.Key];
                        double eMax = boltsDistancesEmax[SollBolt.Key];
                        if (!SollBolt.Key.Hole.IsSlotted)
                        {
                            CurRes.DistanceE1E2orE3E4 = true;
                            CurRes.DistanceE1E2Smaller = eMin;
                            CurRes.DistanceE1E2Bigger = eMax;
                        }
                        else
                        {
                            CurRes.DistanceE1E2orE3E4 = false;
                            CurRes.DistanceE3E4 = eMin;
                        }
                        CurRes.DistanceP1 = CurRes.BearingP1;
                        CurRes.DistanceP2 = CurRes.BearingP2;
                    }

                    // ****** Save to the results table. ******
                    _boltResults.Add(CurRes);
                }
            }

            _boltResultMax = EN1993BoltResults.CalcMaxResult(_boltResults);
            _boltDistancesWarning = EN1993BoltResults.GetDistancesWarnings(_boltResults);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// For inox bolts: UNI EN 1993-1-4:2021.
        /// </summary>
        /// <param name="boltMaterial">Single bolt material.</param>
        /// <returns>α_v</returns>
        private double CalculateAlphaV(in SteelMaterial boltMaterial)
        {
            if (boltMaterial is null)
            {
                _errorLog.Add("Error, missing material, α_v=0.5 will be used.");
                return 0.5;
            }

            if (!OptionsEN1993.ShearPlaneThroughThreadedPortion) // In "UNI EN 1993-1-4:2021 §6.2 Note" for Inox bolts.
                return 0.6;

            if (boltMaterial is BoltMaterialEN1993Inox boltMaterialInox) // it is not Inox
            {
                // In UNI EN 1993-1-4:2021 §6.2 (2).
                // Class "50", "70" and "80" as  "4.6", "5.6", "8.8".
                string ClassName = boltMaterialInox.PropertyClass;
                if (ClassName == "50" || ClassName == "70" || ClassName == "80")
                    return 0.6;
                else
                    return 0.5;
            }
            else
            {
                string ClassName = boltMaterial.Name;
                if (ClassName == "4.6" || ClassName == "5.6" || ClassName == "8.8")
                    return 0.6;
                else if (ClassName == "4.8" || ClassName == "5.8" || ClassName == "6.8" || ClassName == "10.9")
                    return 0.5;
                else
                {
                    _errorLog.Add($"Info: class '{ClassName}' not recognized, α_v=0.5 will be used.");
                    return 0.5;
                }
            }
        }

        /// <summary>
        /// Calculate shear resistance per shear plane.
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// Rivets are not considered.
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns>F_v,Rd</returns>
        private double CalculateShearResistance_FvRd(in BoltSection boltSection, in double alpha_v)
        {
            double f_ub = boltSection.BoltMaterial.Fu;
            double area = boltSection.CalculateResistantArea(OptionsEN1993.ShearPlaneThroughThreadedPortion);
            return alpha_v * f_ub * area / StandardEN1993.GammaM2;
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <returns>k_2</returns>
        private double CalculateK_2() => OptionsEN1993.IsCounterSunkBolt ? 0.63 : 0.9;

        /// <summary>
        /// Calculate tension resistance.
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// Rivets are not considered.
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns>F_t,Rd</returns>
        private double CalculateTensionResistance_FtRd(in double k_2, in BoltSection boltSection)
        {
            double f_ub = boltSection.BoltMaterial.Fu;
            double area = boltSection.CalculateAreaEff();
            return k_2 * f_ub * area / StandardEN1993.GammaM2;
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <param name="e_1"></param>
        /// <param name="p_1"></param>
        /// <param name="boltPos"></param>
        /// <returns>α_d</returns>
        private double CalculateCoeffParallel_AlphaD(in double e_1, in double p_1, in BoltPosition boltPos)
        {
            double d_0 = boltPos.Hole.Diameter;
            return Math.Min(e_1 / (3.0 * d_0), p_1 / (3.0 * d_0) - 1.0 / 4.0);
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <param name="alpha_d"></param>
        /// <param name="boltSection"></param>
        /// <param name="plateWithBolts"></param>
        /// <returns>α_b</returns>
        private double CalculateCoeffParallel_AlphaB(in double alpha_d, in BoltSection boltSection, in PlateWithBolts plateWithBolts)
        {
            return Math.Min(Math.Min(alpha_d, boltSection.BoltMaterial.Fu / plateWithBolts.PlateMaterial.Fu), 1.0);
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <param name="e_2"></param>
        /// <param name="p_2"></param>
        /// <param name="boltPos"></param>
        /// <returns>k_1</returns>
        private double CalculateCoeffPerpendicular_k1(in double e_2, in double p_2, in BoltPosition boltPos)
        {
            double d_0 = boltPos.Hole.Diameter;
            return Math.Min(Math.Min(2.8 * e_2 / d_0 - 1.7, 1.4 * p_2 / d_0 - 1.7), 2.5);
        }

        /// <summary>
        /// Calculate bearing resistance.
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <param name="k_1">Coefficient perpendicular to the direction of load transfer.</param>
        /// <param name="alpha_b">Coefficient in the direction of load transfer.</param>
        /// <param name="boltSection"></param>
        /// <param name="plateWithBolts"></param>
        /// <returns>F_b,Rd</returns>
        private double CalculateBearingResistance_FbRd(in double k_1, in double alpha_b, in BoltSection boltSection, in PlateWithBolts plateWithBolts)
        {
            return k_1 * alpha_b * plateWithBolts.PlateMaterial.Fu * boltSection.Diameter * plateWithBolts.Thickness / StandardEN1993.GammaM2;
        }

        /// <summary>
        /// Calculate punching shear resistance.
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <param name="plateWithBolts">Plate.</param>
        /// <returns>B_p,Rd</returns>
        private double CalculatePunchingShearResistance_BpRd(in PlateWithBolts plateWithBolts, in double d_m)
        {
            return 0.6 * Math.PI * d_m * plateWithBolts.Thickness * plateWithBolts.PlateMaterial.Fu / StandardEN1993.GammaM2;
        }

        /// <summary>
        /// Values of ks.
        /// UNI EN 1993-1-8:2005 - Table 3.6.
        /// </summary>
        /// <param name="holeShape"></param>
        /// <param name="slotPerpendicularLoad">True if load is perpendicuar to the load.</param>
        /// <returns>k_s</returns>
        private double Calculate_ks(in HoleShapeType holeShape, in bool slotPerpendicularLoad)
        {
            if (holeShape == HoleShapeType.NormalRound)
                return 1.0;
            else if (holeShape == HoleShapeType.OversizeRound)
                return 0.85;
            else if (holeShape == HoleShapeType.ShortSlotted)
            {
                if (slotPerpendicularLoad)
                    return 0.85;
                else
                    return 0.76;
            }
            else if (holeShape == HoleShapeType.LongSlotted)
            {
                if (slotPerpendicularLoad)
                    return 0.7;
                else
                    return 0.63;
            }
            else
            {
                _errorLog.Add($"Error: missing hole type '{holeShape}', k_s=1.0 will be used.");
                return 1.0;
            }
        }

        /// <summary>
        /// Slip factor, μ, for pre-loaded bolts.
        /// UNI EN 1993-1-8:2005 - Table 3.7.
        /// EN 1090-2:2008 - Table 18 - Classifications for friction surfaces.
        /// </summary>
        /// <returns>μ</returns>
        private double CalculateSlipFactor_Mu()
        {
            switch (OptionsEN1993.ClassFrictionSurfaces)
            {
                case ClassFrictionSurfacesType.A:
                    return 0.5;
                case ClassFrictionSurfacesType.B:
                    return 0.4;
                case ClassFrictionSurfacesType.C:
                    return 0.3;
                case ClassFrictionSurfacesType.D:
                    return 0.2;
            }
            _errorLog.Add($"Error: wrong splip class '{OptionsEN1993.ClassFrictionSurfaces}', μ=0.2 will be used.");
            return 0.2;
        }

        /// <summary>
        /// Calculate nominal minimum preloading force -> F_pC.
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns></returns>
        private double CalculateSlipPreloading(in BoltSection boltSection) => 0.7 * boltSection.BoltMaterial.Fu * boltSection.CalculateAreaEff();

        /// <summary>
        /// Calculate design Slip resistance.
        /// UNI EN 1993-1-8:2005 - 3.9 Slip-resistant connections using 8.8 or 10.9 bolts.
        /// </summary>
        /// <param name="k_s"></param>
        /// <param name="mu"></param>
        /// <param name="F_tEd">F_t,Ed (formula 3.8a) or F_t,Ed,ser (formula 3.8b).</param>
        /// <param name="gammaM">γ_M3 or γ_M3,ser</param>
        /// <param name="F_pC">Nominal minimum preloading force.</param>
        /// <returns>F_s,Rd or F_s,Rd,ser</returns>
        private double CalculateDesignSlipResistance(in double k_s, in double mu, in double F_tEd, in double gammaM, in double F_pC)
        {
            return k_s * OptionsEN1993.NumFricionPlane * mu * (F_pC - 0.8 * F_tEd) / gammaM;
        }
        private double CalculateDesignSlipResistance_FsRd(in double k_s, in double mu, in double F_tEd, in double F_pC)
            => CalculateDesignSlipResistance(k_s, mu, F_tEd, StandardEN1993.GammaM3, F_pC);
        private double CalculateDesignSlipResistance_FsRdser(in double k_s, in double mu, in double F_tEdser, in double F_pC)
            => CalculateDesignSlipResistance(k_s, mu, F_tEdser, StandardEN1993.GammaM3Ser, F_pC);

        /// <summary>
        /// Calculate nominal clearances.
        /// EN 1090-2:2008 - Table 11 - Nominal clearances for bolts and pins (mm).
        /// </summary>
        /// <param name="nominalBoltDiameter"></param>
        /// <param name="holeShape"></param>
        /// <returns></returns>
        private double CalculateNominalClearance(in double nominalBoltDiameter, in HoleShapeType holeShape)
        {
            double holeTolerance = 0.01;

            switch (holeShape)
            {
                case HoleShapeType.NormalRound:
                    if (nominalBoltDiameter < 12.0 + holeTolerance)
                        return 1.0;
                    else if (nominalBoltDiameter < 24.0 + holeTolerance)
                        return 2.0; // For bolt nominal diameter equal to 14 can be 2mm according to 3.6.1.(5) EC 1993-1-8.
                    else
                        return 3.0;

                case HoleShapeType.OversizeRound:
                    if (nominalBoltDiameter < 14.0 + holeTolerance)
                        return 3.0;
                    else if (nominalBoltDiameter < 22.0 + holeTolerance)
                        return 4.0;
                    else if (nominalBoltDiameter < 24.0 + holeTolerance)
                        return 6.0;
                    else
                        return 8.0;

                case HoleShapeType.ShortSlotted:
                    if (nominalBoltDiameter < 14.0 + holeTolerance)
                        return 4.0;
                    else if (nominalBoltDiameter < 22.0 + holeTolerance)
                        return 6.0;
                    else if (nominalBoltDiameter < 24.0 + holeTolerance)
                        return 8.0;
                    else
                        return 10.0;

                case HoleShapeType.LongSlotted:
                    return 1.5 * nominalBoltDiameter;

                default:
                    _errorLog.Add($"Error: wrong hole type '{holeShape}'.");
                    return 1.0;
            }
        }

        /// <summary>
        /// Calculate hole type.
        /// EN 1090-2:2008 - Table 11 - Nominal clearances for bolts and pins (mm).
        /// </summary>
        /// <param name="hole"></param>
        /// <returns></returns>
        private HoleShapeType CalculateHoleType(in BoltPosition boltpos)
        {
            double holeTolerance = 0.01;
            double clearance = boltpos.Hole.MaxLength - boltpos.BoltDef.Diameter;

            if (!boltpos.Hole.IsSlotted)
            {
                double nominnalClearanceNormal = CalculateNominalClearance(boltpos.BoltDef.Diameter, HoleShapeType.NormalRound);

                if (clearance < nominnalClearanceNormal + holeTolerance)
                    return HoleShapeType.NormalRound;
                else
                    return HoleShapeType.OversizeRound;
            }
            else
            {
                double nominnalClearanceShort = CalculateNominalClearance(boltpos.BoltDef.Diameter, HoleShapeType.ShortSlotted);

                if (clearance < nominnalClearanceShort + holeTolerance)
                    return HoleShapeType.ShortSlotted;
                else
                    return HoleShapeType.LongSlotted;
            }
        }

        /// <summary>
        /// Set hole diameter and slot legth from bolt nominal diameter.
        /// </summary>
        /// <param name="boltpos">Hole to change.</param>
        /// <param name="holeShape">Required hole shape.</param>
        public void SetHoleDiameter(BoltPosition boltpos, in HoleShapeType holeShape)
        {
            double boltNominalDiameter = boltpos.BoltDef.Diameter;

            switch (holeShape)
            {
                case HoleShapeType.OversizeRound:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.OversizeRound);
                    boltpos.Hole.SlotLength = 0.0;
                    break;
                case HoleShapeType.ShortSlotted:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.NormalRound);
                    boltpos.Hole.SlotLength = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.ShortSlotted) - boltpos.Hole.Diameter;
                    break;
                case HoleShapeType.LongSlotted:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.NormalRound);
                    boltpos.Hole.SlotLength = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.LongSlotted) - boltpos.Hole.Diameter;
                    break;
                case HoleShapeType.NormalRound:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.NormalRound);
                    boltpos.Hole.SlotLength = 0.0;
                    break;
            }
        }

        private bool CalculateIsSlottedPerpendicular(in Hole hole, in ResultBeamForces resultBeamForces)
        {
            Vector3d v1 = new Vector3d(Math.Cos(hole.Rotation), Math.Sin(hole.Rotation), 0.0);
            Vector3d v2 = new Vector3d(resultBeamForces.V1, resultBeamForces.V2, 0.0);
            v2.Unitize();
            return v1.CrossProduct(v2).Length > Math.Sin(Math.PI * 0.25);
        }

        /// <summary>
        /// Calculate the design plastic resistance of the net cross-section at bolt holes.
        /// UNI EN 1993-1-8:2005 - 3.4.1 Shear connections (1)c).
        /// UNI EN 1993-1-1:2005 - 6.2.3 (4) (formula 6.8).
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns>N_net,Rd</returns>
        //private double CalculatedesignPlasticResistance_NnetRd(in BoltSection boltSection)
        //{
        //    return 0.0; //boltSection.CalculateAreaEff() * boltSection.BoltMaterial.Fyk / StandardEN1993.GammaM0;
        //}



        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.3: Minimum and maximum spacing, end and edge distances.
        /// </summary>
        /// <param name="dHole">Hole diameter.</param>
        /// <param name="expo">Exposure condition.</param>
        /// <param name="t"></param>
        /// <param name="t_min"></param>
        /// <param name="e1e2_min">End distances e1 and e2. Minimum.</param>
        /// <param name="e1e2_max">End distances e1 and e2. Maximum.</param>
        /// <param name="e3e4_min">Distances e3 and e4 in slotted holes. Minimum.</param>
        /// <param name="e3e4_max">Distances e3 and e4 in slotted holes. Maximum.</param>
        /// <param name="p1_min">Spacing p1. Minimum.</param>
        /// <param name="p1_max">Spacing p1. Maximum.</param>
        /// <param name="p2_min">Spacing p2. Minimum.</param>
        /// <param name="p2_max">Spacing p2. Maximum.</param>
        private void CalculateDimesionLimits(in double dHole, in double t, in double t_min,
            out double e1e2_min, out double e1e2_max,
            out double e3e4_min, out double e3e4_max,
            out double p1_min, out double p1_max,
            out double p2_min, out double p2_max)
        {
            e1e2_min = 1.2 * dHole;
            e3e4_min = 1.5 * dHole;
            p1_min = 2.2 * dHole;
            p2_min = 2.4 * dHole;

            switch (OptionsEN1993.ExposureCondition)
            {
                case ExposureConditionType.SteelExposed:
                    e1e2_max = 4.0 * t + 40.0;
                    e3e4_max = Double.NaN;
                    p1_max = Math.Min(14.0 * t, 200.0);
                    p2_max = p1_max;
                    break;
                case ExposureConditionType.SteelNotExposed:
                    e1e2_max = Double.NaN;
                    e3e4_max = Double.NaN;
                    p1_max = Math.Min(14.0 * t, 200.0);
                    p2_max = p1_max;
                    break;
                case ExposureConditionType.SteelUnprotected:
                    e1e2_max = Math.Max(8.0 * t, 125.0);
                    e3e4_max = Double.NaN;
                    p1_max = Math.Min(14.0 * t_min, 175.0);
                    p2_max = p1_max;
                    break;
                default:
                    e1e2_max = Double.NaN;
                    e3e4_max = Double.NaN;
                    p1_max = Double.NaN;
                    p2_max = Double.NaN;
                    break;
            }
        }

        #endregion

        #region Nested Class Options

        /// <summary>
        /// Options specific for EN1993.
        /// </summary>
        [Serializable]
        public class EN1993BoltOptions : BoltOptions, ISerializable
        {
            #region Fields

            protected ShearConnectionsCategoryType _shearConnectionsCategory;

            protected TensionConnectionsCategoryType _tensionConnectionsCategory;

            #endregion

            #region Properties

            /// <summary>
            /// UNI EN 1993-1-8:2005 - Table 3.6 and EN 1090-2 Table 11.
            /// Influence on bearing resistance Fb,Rd and tollerances.
            /// </summary>
            public HoleShapeType HoleShape { get; set; }

            /// <summary>
            /// Class of friction surfaces.
            /// </summary>
            public ClassFrictionSurfacesType ClassFrictionSurfaces { get; set; }

            /// <summary>
            /// Shear connection category.
            /// </summary>
            public ShearConnectionsCategoryType ShearConnectionsCategory
            {
                get => _shearConnectionsCategory;
                set
                {
                    _shearConnectionsCategory = value;
                    switch (value)
                    {
                        case ShearConnectionsCategoryType.A:
                            _tensionConnectionsCategory = TensionConnectionsCategoryType.D;
                            break;
                        case ShearConnectionsCategoryType.B:
                        case ShearConnectionsCategoryType.C:
                            _tensionConnectionsCategory = TensionConnectionsCategoryType.E;
                            break;
                    }
                }
            }

            /// <summary>
            /// Tension connection category.
            /// </summary>
            public TensionConnectionsCategoryType TensionConnectionsCategory => _tensionConnectionsCategory;

            /// <summary>
            /// Exposure condition, used for minimum and maximum spacing, end and edge distances.
            /// </summary>
            public ExposureConditionType ExposureCondition { get; set; }

            #endregion

            #region Constructor

            public EN1993BoltOptions()
            {
                HoleShape = HoleShapeType.NormalRound;
                ClassFrictionSurfaces = ClassFrictionSurfacesType.D;
                ShearConnectionsCategory = ShearConnectionsCategoryType.A;
                ExposureCondition = ExposureConditionType.SteelExposed;
            }

            public EN1993BoltOptions(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
                int version = info.GetInt32("EN1993BoltOptionsVersion");

                ShearConnectionsCategory = (ShearConnectionsCategoryType)info.GetValue("ShearConnectionsCategory", typeof(ShearConnectionsCategoryType));
                HoleShape = (HoleShapeType)info.GetValue("HoleShape", typeof(HoleShapeType));
                ClassFrictionSurfaces = (ClassFrictionSurfacesType)info.GetValue("ClassFrictionSurfaces", typeof(ClassFrictionSurfacesType));
                if (version == 2)
                {
                    ExposureCondition = (ExposureConditionType)info.GetValue("ExposureCondition", typeof(ExposureConditionType));
                }
            }

            #endregion

            #region Methods

            /// <summary>
            /// In version 2:
            /// - added ExposureCondition.
            /// </summary>
            /// <param name="info"></param>
            /// <param name="context"></param>
            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);

                int version = 2;
                info.AddValue("EN1993BoltOptionsVersion", version);

                info.AddValue("ShearConnectionsCategory", _shearConnectionsCategory, typeof(ShearConnectionsCategoryType));
                info.AddValue("HoleShape", HoleShape, typeof(HoleShapeType));
                info.AddValue("ClassFrictionSurfaces", ClassFrictionSurfaces, typeof(ClassFrictionSurfacesType));
                info.AddValue("ExposureCondition", ExposureCondition, typeof(ExposureConditionType));
            }

            #endregion
        }

        #endregion
    }

}
