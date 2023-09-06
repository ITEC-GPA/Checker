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
    public class EN1993BoltChecker : ENCommonBoltChecker
    {
        #region Enum

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

        protected override double EnGammaM2 => StandardEN1993.GammaM2;

        protected override double EnGammaM3 => StandardEN1993.GammaM3;

        protected override double EnGammaM3Ser => StandardEN1993.GammaM3Ser;

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
                        CurRes.BearingAlphaB = CalculateCoeffParallel_AlphaB(AlphaD, SollBolt.Key.BoltDef, _plateWithBolts, CurRes.BearingE1, CurRes.BearingP1, SollBolt.Key.Hole.Diameter);
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
                        CurRes.PunchingResistance = CalculatePunchingShearResistance_BpRd(_plateWithBolts, CurRes.PunchingDm, SollBolt.Key.Hole.Diameter);
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

            _boltResultMax = new EN1993BoltResults(null, new LoadCase("Envelope", Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight), null,
                (StandardEN1993p11)_boltResults[0].Standard, (EN1993BoltOptions)_boltResults[0].Options);
            if (!ENCommonBoltResults.CalcMaxResult(_boltResults.Cast<EN1993BoltResults>().ToList(), (EN1993BoltResults)_boltResultMax))
                _boltResultMax = null;
            _boltDistancesWarning = ENCommonBoltResults.GetDistancesWarnings(_boltResults);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <returns>k_2</returns>
        protected override double CalculateK_2()
        {
            return OptionsEN1993.IsCounterSunkBolt ? 0.63 : 0.9;
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <param name="e_1"></param>
        /// <param name="p_1"></param>
        /// <param name="boltPos"></param>
        /// <returns>α_d</returns>
        protected override double CalculateCoeffParallel_AlphaD(in double e_1, in double p_1, in BoltPosition boltPos)
        {
            double d_0 = boltPos.Hole.Diameter;
            return Math.Min(e_1 / (3.0 * d_0), p_1 / (3.0 * d_0) - 1.0 / 4.0);
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <returns>α_b</returns>
        protected override double CalculateCoeffParallel_AlphaB(in double alpha_d, in BoltSection boltSection, in PlateWithBolts plateWithBolts, in double e_1, in double p_1, in double d_0)
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
        protected override double CalculateCoeffPerpendicular_k1(in double e_2, in double p_2, in BoltPosition boltPos)
        {
            double d_0 = boltPos.Hole.Diameter;
            return Math.Min(Math.Min(2.8 * e_2 / d_0 - 1.7, 1.4 * p_2 / d_0 - 1.7), 2.5);
        }

        /// <summary>
        /// Calculate punching shear resistance.<br/>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <param name="plateWithBolts">Plate.</param>
        /// <returns>B_p,Rd</returns>
        protected override double CalculatePunchingShearResistance_BpRd(in PlateWithBolts plateWithBolts, in double d_m, in double d_0)
        {
            return 0.6 * Math.PI * d_m * plateWithBolts.Thickness * plateWithBolts.PlateMaterial.Fu / EnGammaM2;
        }

        /// <summary>
        /// EN1993: Calculate nominal minimum preloading force -> F_pC.
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns></returns>
        protected override double CalculateSlipPreloading(in BoltSection boltSection)
        {
            return 0.7 * boltSection.BoltMaterial.Fu * boltSection.CalculateAreaEff();
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.3: Minimum and maximum spacing, end and edge distances.
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
        protected override void CalculateDimesionLimits(in double dHole, in double t, in double t_min,
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
        public class EN1993BoltOptions : ENCommonBoltOptions, ISerializable
        {
            #region Properties

            /// <summary>
            /// Exposure condition, used for minimum and maximum spacing, end and edge distances.
            /// </summary>
            public ExposureConditionType ExposureCondition { get; set; }

            #endregion

            #region Constructor

            public EN1993BoltOptions()
            {
                ExposureCondition = ExposureConditionType.SteelExposed;
            }

            public EN1993BoltOptions(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
                ExposureCondition = (ExposureConditionType)info.GetValue("ExposureCondition", typeof(ExposureConditionType));
            }

            #endregion

            #region Methods

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);
                info.AddValue("ExposureCondition", ExposureCondition, typeof(ExposureConditionType));
            }

            #endregion
        }

        #endregion
    }

}
