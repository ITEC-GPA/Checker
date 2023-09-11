using GPC.Checkers.Steel.Results;
using GPC.Geometry;
using GPC.Model.LoadCases;
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
    /// <summary>
    /// The purpose of this class is collect common methods for EN1993 and EN1999.
    /// </summary>
    [Serializable]
    public abstract class ENCommonBoltChecker : BoltChecker
    {
        #region Constructor

        protected ENCommonBoltChecker(PlateWithBolts plateWithBolts, List<BoltStresses> boltStresses, Standard standard, BoltOptions options, int id, string name = "")
            : base(plateWithBolts, boltStresses, standard, options, id, name)
        {
        }

        #endregion

        #region Properties

        private ENCommonBoltOptions OptionsENCommon => (ENCommonBoltOptions)_options;

        public List<ENCommonBoltResults> BoltResultsENCommon => _boltResults.Cast<ENCommonBoltResults>().ToList();

        public abstract ENCommonBoltResults BoltResultMax { get; }

        /// <summary>
        /// Characteristic value of ultimate tensile strength.
        /// </summary>
        protected abstract double PlateMaterialFu { get; }

        /// <summary>
        /// Partial factor for resistance of cross-sections in tension to fracture.
        /// </summary>
        protected abstract double EnGammaM2 { get; }

        /// <summary>
        /// Slip resistance of connections at ultimate limit state.
        /// </summary>
        protected abstract double EnGammaM3 { get; }

        /// <summary>
        /// Slip resistance of connections at serviceability limit state.
        /// </summary>
        protected abstract double EnGammaM3Ser { get; }

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
                SetHoleDiameter(boltPos, OptionsENCommon.HoleShape);

            // ****** Indipendent from required forces:
            // - Distances eMin for all holes.
            // - Distances eMax for all holes.
            // - Inner or outer type.
            var boltsDistancesEmax = new Dictionary<BoltPosition, Line2d>();
            var boltsDistancesEmin = new Dictionary<BoltPosition, Line2d>();
            var boltsAreOuter = new Dictionary<BoltPosition, bool>();
            foreach (var boltPos in _plateWithBolts.BoltGrid.Bolts)
            {
                boltsDistancesEmin[boltPos] = _plateWithBolts.CalculateClosestEdgePoint(boltPos, out Line2d _);
                boltsDistancesEmax[boltPos] = _plateWithBolts.CalculateFurtherMinimumEdgePoint(boltPos, out Line2d _);
                boltsAreOuter[boltPos] = _plateWithBolts.IsOuuter(boltPos);
            }

            foreach (var SolForce in _boltStresses)
            {
                // ****** Required forces.
                // Calculate all shear forces for each bolt.
                var SollAllBolts = _plateWithBolts.BoltGrid.CalculateShearForcesElastic(SolForce.ResBeamForces);
                // Calculate uniform tension forces for each bolt.
                var SollN = SolForce.ResBeamForces.N > 0.0 ? SolForce.ResBeamForces.N / SollAllBolts.Count() : 0;
                if (SollN > 1) // Positive for tension.
                    foreach (var SollBolt in SollAllBolts)
                        SollBolt.Value.N = SollN;

                foreach (var SollBolt in SollAllBolts)
                {
                    // ****** Initialize a result. ******
                    var CurRes = BuildENCommonBoltResults(SollBolt.Key, SolForce.LoadCase, SollBolt.Value);
                    {
                        CurRes.SollCombCase = SolForce.CombCase;
                        CurRes.SollShear = SollBolt.Value.GetCombinedShearForce();
                        CurRes.SollTension = SollBolt.Value.N;
                    }
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
                    Line2d p1Line = null;
                    Line2d p2Line = null;
                    if (CurRes.BearingIsActive || CurRes.DistanceIsActive)
                    {
                        p1Line = _plateWithBolts.CalculateP1Line(SollBolt.Key, SollBolt.Value);
                        p2Line = _plateWithBolts.CalculateP2Line(SollBolt.Key, SollBolt.Value);
                        CurRes.BearingE1 = _plateWithBolts.CalculateE1(SollBolt.Key, SollBolt.Value);
                        CurRes.BearingP1 = p1Line?.Length ?? PlateWithBolts.SPACINGMAXVALUE;
                        CurRes.BearingE2 = _plateWithBolts.CalculateE2(SollBolt.Key, SollBolt.Value);
                        CurRes.BearingP2 = p2Line?.Length ?? PlateWithBolts.SPACINGMAXVALUE;
                    }
                    if (CurRes.BearingIsActive)
                    {
                        var AlphaD = CalculateCoeffParallel_AlphaD(CurRes.BearingE1, CurRes.BearingP1, SollBolt.Key);
                        CurRes.Bearingk1 = CalculateCoeffPerpendicular_k1(CurRes.BearingE2, CurRes.BearingP2, SollBolt.Key);
                        CurRes.BearingAlphaB = CalculateCoeffParallel_AlphaB(AlphaD, SollBolt.Key.BoltDef, CurRes.BearingE1, CurRes.BearingP1, SollBolt.Key.Hole.Diameter);
                        CurRes.BearingResistance = CalculateBearingResistance_FbRd(CurRes.Bearingk1, CurRes.BearingAlphaB, SollBolt.Key.BoltDef, _plateWithBolts.Thickness);
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
                        CurRes.PunchingResistance = CalculatePunchingShearResistance_BpRd(_plateWithBolts.Thickness, CurRes.PunchingDm, SollBolt.Key.Hole.Diameter);
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

                        var eMin = boltsDistancesEmin[SollBolt.Key];
                        var eMax = boltsDistancesEmax[SollBolt.Key];
                        if (!SollBolt.Key.Hole.IsSlotted)
                        {
                            CurRes.DistanceE1E2orE3E4 = true;
                            CurRes.DistanceE1E2SmallerLine = eMin;
                            CurRes.DistanceE1E2BiggerLine = eMax;
                        }
                        else
                        {
                            CurRes.DistanceE1E2orE3E4 = false;
                            CurRes.DistanceE3E4Line = eMin;
                        }
                        CurRes.DistanceP1Line = p1Line;
                        CurRes.DistanceP2Line = p2Line;
                    }

                    // ****** Save to the results table. ******
                    _boltResults.Add(CurRes);
                }
            }

            _boltResultMax = BuildENCommonBoltResults(null, new LoadCase("Envelope", Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight), null);
            if (!ENCommonBoltResults.CalcMaxResult(_boltResults.Cast<ENCommonBoltResults>().ToList(), (ENCommonBoltResults)_boltResultMax))
                _boltResultMax = null;
            _boltDistancesWarning = ENCommonBoltResults.GetDistancesWarnings(_boltResults);
        }

        /// <summary>
        /// Set hole diameter and slot legth from bolt nominal diameter.
        /// </summary>
        /// <param name="boltpos">Hole to change.</param>
        /// <param name="holeShape">Required hole shape.</param>
        public void SetHoleDiameter(BoltPosition boltpos, in EN1993BoltChecker.HoleShapeType holeShape)
        {
            double boltNominalDiameter = boltpos.BoltDef.Diameter;

            switch (holeShape)
            {
                case EN1993BoltChecker.HoleShapeType.OversizeRound:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, EN1993BoltChecker.HoleShapeType.OversizeRound);
                    boltpos.Hole.SlotLength = 0.0;
                    break;
                case EN1993BoltChecker.HoleShapeType.ShortSlotted:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, EN1993BoltChecker.HoleShapeType.NormalRound);
                    boltpos.Hole.SlotLength = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, EN1993BoltChecker.HoleShapeType.ShortSlotted) - boltpos.Hole.Diameter;
                    break;
                case EN1993BoltChecker.HoleShapeType.LongSlotted:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, EN1993BoltChecker.HoleShapeType.NormalRound);
                    boltpos.Hole.SlotLength = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, EN1993BoltChecker.HoleShapeType.LongSlotted) - boltpos.Hole.Diameter;
                    break;
                case EN1993BoltChecker.HoleShapeType.NormalRound:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, EN1993BoltChecker.HoleShapeType.NormalRound);
                    boltpos.Hole.SlotLength = 0.0;
                    break;
            }
        }

        #endregion

        #region Protected methods

        /// <summary>
        /// Build a bolt result specific per standard code.
        /// </summary>
        /// <param name="boltPos"></param>
        /// <param name="case"></param>
        /// <param name="beamForces"></param>
        /// <returns></returns>
        protected abstract ENCommonBoltResults BuildENCommonBoltResults(BoltPosition boltPos, ILoadCase loadCase, ResultBeamForces beamForces);

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.<br/>
        /// UNI EN 1999-1-1:2023 - Table 10.3.<br/>
        /// For inox bolts: UNI EN 1993-1-4:2021.
        /// </summary>
        /// <param name="boltMaterial">Single bolt material.</param>
        /// <returns>α_v</returns>
        protected double CalculateAlphaV(in SteelMaterial boltMaterial)
        {
            if (boltMaterial is null)
            {
                _errorLog.Add("Error, missing material, α_v=0.5 will be used.");
                return 0.5;
            }

            // In "UNI EN 1993-1-4:2021 §6.2 Note" for Inox bolts.
            if (!OptionsENCommon.ShearPlaneThroughThreadedPortion)
                return 0.6;

            // When aluminum bolts are handled, it will be possible to put α_v=0.5, source "UNI EN 1999-1-1:2023 - Table 10.3."
            if (boltMaterial is BoltMaterialEN1993Inox boltMaterialInox)
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
        /// Calculate shear resistance per shear plane.<br/>
        /// UNI EN 1993-1-8:2005 - Table 3.4.<br/>
        /// UNI EN 1999-1-1:2023 - Table 10.3.<br/>
        /// Rivets are not considered.
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns>F_v,Rd</returns>
        protected double CalculateShearResistance_FvRd(in BoltSection boltSection, in double alpha_v)
        {
            double f_ub = boltSection.BoltMaterial.Fu;
            double area = boltSection.CalculateResistantArea(OptionsENCommon.ShearPlaneThroughThreadedPortion);
            return alpha_v * f_ub * area * OptionsENCommon.NumShearPlane / EnGammaM2;
        }

        /// <summary>
        /// </summary>
        /// <returns>k_2</returns>
        protected abstract double CalculateK_2();

        /// <summary>
        /// Calculate tension resistance.<br/>
        /// UNI EN 1993-1-8:2005 - Table 3.4.<br/>
        /// UNI EN 1999-1-1:2023 - Table 10.3.<br/>
        /// Rivets are not considered.
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns>F_t,Rd</returns>
        protected double CalculateTensionResistance_FtRd(in double k_2, in BoltSection boltSection)
        {
            double f_ub = boltSection.BoltMaterial.Fu;
            double area = boltSection.CalculateAreaEff();
            return k_2 * f_ub * area / EnGammaM2;
        }

        /// <summary>
        /// </summary>
        /// <param name="e_1"></param>
        /// <param name="p_1"></param>
        /// <param name="boltPos"></param>
        /// <returns>α_d</returns>
        protected abstract double CalculateCoeffParallel_AlphaD(in double e_1, in double p_1, in BoltPosition boltPos);

        /// <summary>
        /// </summary>
        /// <param name="e_2"></param>
        /// <param name="p_2"></param>
        /// <param name="boltPos"></param>
        /// <returns>k_1</returns>
        protected abstract double CalculateCoeffPerpendicular_k1(in double e_2, in double p_2, in BoltPosition boltPos);

        /// <summary>
        /// </summary>
        /// <param name="alpha_d">Used by EN1993.</param>
        /// <param name="boltSection"></param>
        /// <param name="plateWithBolts"></param>
        /// <param name="e_1">Used by EN1999.</param>
        /// <param name="p_1">Used by EN1999.</param>
        /// <param name="d_0">Hole diameter. Used by EN1999.</param>
        /// <returns>α_b</returns>
        protected abstract double CalculateCoeffParallel_AlphaB(in double alpha_d, in BoltSection boltSection, in double e_1, in double p_1, in double d_0);

        /// <summary>
        /// Calculate bearing resistance.<br/>
        /// UNI EN 1993-1-8:2005 - Table 3.4.<br/>
        /// UNI EN 1999-1-1:2023 - Table 10.3.<br/>
        /// </summary>
        /// <param name="k_1">Coefficient perpendicular to the direction of load transfer.</param>
        /// <param name="alpha_b">Coefficient in the direction of load transfer.</param>
        /// <param name="boltSection"></param>
        /// <param name="plateWithBolts"></param>
        /// <returns>F_b,Rd</returns>
        protected double CalculateBearingResistance_FbRd(in double k_1, in double alpha_b, in BoltSection boltSection, in double plateWithBoltsThickness)
        {
            return k_1 * alpha_b * PlateMaterialFu * boltSection.Diameter * plateWithBoltsThickness / EnGammaM2;
        }

        /// <summary>
        /// Calculate punching shear resistance.
        /// </summary>
        /// <param name="plateWithBolts">Plate.</param>
        /// <returns>B_p,Rd</returns>
        protected abstract double CalculatePunchingShearResistance_BpRd(in double plateWithBoltsThickness, in double d_m, in double d_0);

        /// <summary>
        /// Values of ks.<br/>
        /// UNI EN 1993-1-8:2005 - Table 3.6.<br/>
        /// UN EN 1999-1-1:2023 - Table 10.4.
        /// </summary>
        /// <param name="holeShape"></param>
        /// <param name="slotPerpendicularLoad">True if load is perpendicuar to the load.</param>
        /// <returns>k_s</returns>
        protected double Calculate_ks(in EN1993BoltChecker.HoleShapeType holeShape, in bool slotPerpendicularLoad)
        {
            if (holeShape == EN1993BoltChecker.HoleShapeType.NormalRound)
                return 1.0;
            else if (holeShape == EN1993BoltChecker.HoleShapeType.OversizeRound)
                return 0.85;
            else if (holeShape == EN1993BoltChecker.HoleShapeType.ShortSlotted)
            {
                if (slotPerpendicularLoad)
                    return 0.85;
                else
                    return 0.76;
            }
            else if (holeShape == EN1993BoltChecker.HoleShapeType.LongSlotted)
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
        /// Slip factor, μ, for pre-loaded bolts.<br/>
        /// UNI EN 1993-1-8:2005 - Table 3.7.<br/>
        /// EN 1090-2:2008 - Table 18 - Classifications for friction surfaces.<br/>
        /// EN 1999-1-1:2023 - "10.5.8.5 Slip factor".<br/>
        /// EN 1090-3:2019 - Annex D - "D.6 Test results" --> slip classification is the same.
        /// </summary>
        /// <returns>μ</returns>
        protected double CalculateSlipFactor_Mu()
        {
            switch (OptionsENCommon.ClassFrictionSurfaces)
            {
                case EN1993BoltChecker.ClassFrictionSurfacesType.A:
                    return 0.5;
                case EN1993BoltChecker.ClassFrictionSurfacesType.B:
                    return 0.4;
                case EN1993BoltChecker.ClassFrictionSurfacesType.C:
                    return 0.3;
                case EN1993BoltChecker.ClassFrictionSurfacesType.D:
                    return 0.2;
            }
            _errorLog.Add($"Error: wrong splip class '{OptionsENCommon.ClassFrictionSurfaces}', μ=0.2 will be used.");
            return 0.2;
        }

        /// <summary>
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns></returns>
        protected abstract double CalculateSlipPreloading(in BoltSection boltSection);

        /// <summary>
        /// Calculate design Slip resistance.<br/>
        /// UNI EN 1993-1-8:2005 - 3.9 Slip-resistant connections using 8.8 or 10.9 bolts.
        /// EN 1999-1-1:2023 - "§10.5.8.6 Combined tension and shear".
        /// </summary>
        /// <param name="k_s"></param>
        /// <param name="mu"></param>
        /// <param name="F_tEd">F_t,Ed (formula 3.8a) or F_t,Ed,ser (formula 3.8b).</param>
        /// <param name="gammaM">γ_M3 or γ_M3,ser</param>
        /// <param name="F_pC">Nominal minimum preloading force.</param>
        /// <returns>F_s,Rd or F_s,Rd,ser</returns>
        protected double CalculateDesignSlipResistance(in double k_s, in double mu, in double F_tEd, in double gammaM, in double F_pC)
        {
            return k_s * OptionsENCommon.NumFricionPlane * mu * (F_pC - 0.8 * F_tEd) / gammaM;
        }

        protected double CalculateDesignSlipResistance_FsRd(in double k_s, in double mu, in double F_tEd, in double F_pC)
            => CalculateDesignSlipResistance(k_s, mu, F_tEd, EnGammaM3, F_pC);
        protected double CalculateDesignSlipResistance_FsRdser(in double k_s, in double mu, in double F_tEdser, in double F_pC)
            => CalculateDesignSlipResistance(k_s, mu, F_tEdser, EnGammaM3Ser, F_pC);

        /// <summary>
        /// Calculate nominal clearances.<br/>
        /// EN 1090-2:2008 - Table 11 - Nominal clearances for bolts and pins (mm).
        /// </summary>
        /// <param name="nominalBoltDiameter"></param>
        /// <param name="holeShape"></param>
        /// <returns></returns>
        protected double CalculateNominalClearance(in double nominalBoltDiameter, in EN1993BoltChecker.HoleShapeType holeShape)
        {
            double holeTolerance = 0.01;

            switch (holeShape)
            {
                case EN1993BoltChecker.HoleShapeType.NormalRound:
                    if (nominalBoltDiameter < 12.0 + holeTolerance)
                        return 1.0;
                    else if (nominalBoltDiameter < 24.0 + holeTolerance)
                        return 2.0; // For bolt nominal diameter equal to 14 can be 2mm according to 3.6.1.(5) EC 1993-1-8.
                    else
                        return 3.0;

                case EN1993BoltChecker.HoleShapeType.OversizeRound:
                    if (nominalBoltDiameter < 14.0 + holeTolerance)
                        return 3.0;
                    else if (nominalBoltDiameter < 22.0 + holeTolerance)
                        return 4.0;
                    else if (nominalBoltDiameter < 24.0 + holeTolerance)
                        return 6.0;
                    else
                        return 8.0;

                case EN1993BoltChecker.HoleShapeType.ShortSlotted:
                    if (nominalBoltDiameter < 14.0 + holeTolerance)
                        return 4.0;
                    else if (nominalBoltDiameter < 22.0 + holeTolerance)
                        return 6.0;
                    else if (nominalBoltDiameter < 24.0 + holeTolerance)
                        return 8.0;
                    else
                        return 10.0;

                case EN1993BoltChecker.HoleShapeType.LongSlotted:
                    return 1.5 * nominalBoltDiameter;

                default:
                    _errorLog.Add($"Error: wrong hole type '{holeShape}'.");
                    return 1.0;
            }
        }

        /// <summary>
        /// Calculate hole type.<br/>
        /// EN 1090-2:2008 - Table 11 - Nominal clearances for bolts and pins (mm).
        /// </summary>
        /// <param name="hole"></param>
        /// <returns></returns>
        protected EN1993BoltChecker.HoleShapeType CalculateHoleType(in BoltPosition boltpos)
        {
            double holeTolerance = 0.01;
            double clearance = boltpos.Hole.MaxLength - boltpos.BoltDef.Diameter;

            if (!boltpos.Hole.IsSlotted)
            {
                double nominnalClearanceNormal = CalculateNominalClearance(boltpos.BoltDef.Diameter, EN1993BoltChecker.HoleShapeType.NormalRound);

                if (clearance < nominnalClearanceNormal + holeTolerance)
                    return EN1993BoltChecker.HoleShapeType.NormalRound;
                else
                    return EN1993BoltChecker.HoleShapeType.OversizeRound;
            }
            else
            {
                double nominnalClearanceShort = CalculateNominalClearance(boltpos.BoltDef.Diameter, EN1993BoltChecker.HoleShapeType.ShortSlotted);

                if (clearance < nominnalClearanceShort + holeTolerance)
                    return EN1993BoltChecker.HoleShapeType.ShortSlotted;
                else
                    return EN1993BoltChecker.HoleShapeType.LongSlotted;
            }
        }

        protected bool CalculateIsSlottedPerpendicular(in Hole hole, in ResultBeamForces resultBeamForces)
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
        /// Minimum and maximum spacing, end and edge distances.
        /// </summary>
        /// <param name="dHole">Hole diameter.</param>
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
        protected abstract void CalculateDimesionLimits(in double dHole, in double t, in double t_min,
            out double e1e2_min, out double e1e2_max,
            out double e3e4_min, out double e3e4_max,
            out double p1_min, out double p1_max,
            out double p2_min, out double p2_max);

        #endregion

        #region Nested Class Options

        /// <summary>
        /// Common options between EN1993 and EN1999.
        /// </summary>
        [Serializable]
        public abstract class ENCommonBoltOptions : BoltOptions, ISerializable
        {
            #region Fields

            protected EN1993BoltChecker.ShearConnectionsCategoryType _shearConnectionsCategory;

            protected EN1993BoltChecker.TensionConnectionsCategoryType _tensionConnectionsCategory;

            #endregion

            #region Properties

            /// <summary>
            /// UNI EN 1993-1-8:2005 - Table 3.6 and EN 1090-2 Table 11.
            /// Influence on bearing resistance Fb,Rd and tollerances.
            /// </summary>
            public EN1993BoltChecker.HoleShapeType HoleShape { get; set; }

            /// <summary>
            /// Class of friction surfaces.
            /// </summary>
            public EN1993BoltChecker.ClassFrictionSurfacesType ClassFrictionSurfaces { get; set; }

            /// <summary>
            /// Shear connection category.
            /// </summary>
            public EN1993BoltChecker.ShearConnectionsCategoryType ShearConnectionsCategory
            {
                get => _shearConnectionsCategory;
                set
                {
                    _shearConnectionsCategory = value;
                    switch (value)
                    {
                        case EN1993BoltChecker.ShearConnectionsCategoryType.A:
                            _tensionConnectionsCategory = EN1993BoltChecker.TensionConnectionsCategoryType.D;
                            break;
                        case EN1993BoltChecker.ShearConnectionsCategoryType.B:
                        case EN1993BoltChecker.ShearConnectionsCategoryType.C:
                            _tensionConnectionsCategory = EN1993BoltChecker.TensionConnectionsCategoryType.E;
                            break;
                    }
                }
            }

            /// <summary>
            /// Tension connection category.
            /// </summary>
            public EN1993BoltChecker.TensionConnectionsCategoryType TensionConnectionsCategory => _tensionConnectionsCategory;

            /// <summary>
            /// Exposure condition, used for minimum and maximum spacing, end and edge distances.
            /// </summary>
            public EN1993BoltChecker.ExposureConditionType ExposureCondition { get; set; }

            #endregion

            #region Constructor

            protected ENCommonBoltOptions()
            {
                HoleShape = EN1993BoltChecker.HoleShapeType.NormalRound;
                ClassFrictionSurfaces = EN1993BoltChecker.ClassFrictionSurfacesType.D;
                ShearConnectionsCategory = EN1993BoltChecker.ShearConnectionsCategoryType.A;
                ExposureCondition = EN1993BoltChecker.ExposureConditionType.Exposed;
            }

            protected ENCommonBoltOptions(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            #endregion
        }

        #endregion
    }
}
