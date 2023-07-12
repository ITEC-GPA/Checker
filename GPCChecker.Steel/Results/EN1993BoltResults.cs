using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Results
{
    [Serializable]
    public class EN1993BoltResults : BoltResults, ISerializable
    {
        #region Enumerable

        public enum DimensionWarningType
        {
            EdgeLower, // Distance to edge. Lower limit.
            EdgeUpper, // Distance to edge. Upper limit.
            SpacingP1Lower, // Spacing p1, in the direction of the force. Lower limit.
            SpacingP1Upper, // Spacing p1, in the direction of the force. Upper limit.
            SpacingP2Lower, // Spacing p2, orthogonal to the direction of the force. Lower limit.
            SpacingP2Upper // Spacing p2, orthogonal to the direction of the force. Upper limit.
        }

        #endregion

        #region Properties

        public EN1993BoltChecker.EN1993BoltOptions eN1993BoltOptions => Options as EN1993BoltChecker.EN1993BoltOptions;

        public BoltStresses.CombCaseType SollCombCase { get; internal set; }

        /// <summary>
        /// Required shear force.
        /// </summary>
        public double SollShear { get; internal set; }
        public bool SollShearIsNull => SollShear < 1;

        /// <summary>
        /// Required tension force.
        /// </summary>
        public double SollTension { get; internal set; }
        public bool SollTensionIsNull => SollTension < 1;

        /// <summary>
        /// Desgin shear resistance per bolt F_v,Rd.
        /// </summary>
        public bool ShearIsActive { get; internal set; }

        public double ShearResistance { get; internal set; }

        public double ShearRatio { get; internal set; }

        public double ShearAlphaV { get; internal set; }

        /// <summary>
        /// Desgin bearing resistance per bolt F_b,Rd.
        /// </summary>
        public bool BearingIsActive { get; internal set; }

        public double BearingResistance { get; internal set; }

        public double BearingRatio { get; internal set; }

        public double BearingE1 { get; internal set; }

        public double BearingP1 { get; internal set; }

        public double BearingE2 { get; internal set; }

        public double BearingP2 { get; internal set; }

        public double Bearingk1 { get; internal set; }

        public double BearingAlphaB { get; internal set; }

        /// <summary>
        /// Design Slip resistance F_s,Rd.
        /// </summary>
        public bool SlipIsActive { get; internal set; }

        public double SlipResistance { get; internal set; }

        public double SlipRatio { get; internal set; }

        public double SlipKs { get; internal set; }

        public double SlipMu { get; internal set; }

        public double SlipFpc { get; internal set; }

        /// <summary>
        /// Design Slip resistance at serviceability F_s,Rd,ser.
        /// </summary>
        public bool SlipSerIsActive { get; internal set; }

        public double SlipSerResistance { get; internal set; }

        public double SlipSerRatio { get; internal set; }

        public double SlipSerKs { get; internal set; }

        public double SlipSerMu { get; internal set; }

        public double SlipSerFpc { get; internal set; }

        /// <summary>
        /// Design plastic resistance of the net cross-section at bolt holes N_net,Rd.
        /// </summary>
        public bool NetIsActive { get; internal set; }

        public double NetResistance { get; internal set; }

        public double NetRatio { get; internal set; }

        /// <summary>
        /// Desgin tension resistance per bolt F_t,Rd.
        /// </summary>
        public bool TensionIsActive { get; internal set; }

        public double TensionResistance { get; internal set; }

        public double TensionRatio { get; internal set; }

        public double TensionK2 { get; internal set; }

        /// <summary>
        /// Combined shear and tension.
        /// </summary>
        public bool CombinedShearTensionIsActive { get; internal set; }

        public double CombinedShearTensionRatio { get; internal set; }

        /// <summary>
        /// Punching shear resistance B_p,Rd.
        /// </summary>
        public bool PunchingIsActive { get; internal set; }

        public double PunchingResistance { get; internal set; }

        public double PunchingRatio { get; internal set; }

        public double PunchingDm { get; internal set; }

        /// <summary>
        /// Distance warnings, based on UNI EN 1993-1-8:2005 - Table 3.3: Minimum and maximum spacing, end and edge distances.
        /// </summary>
        public bool DistanceIsActive { get; internal set; }

        public double DistanceE1E2Min { get; internal set; }

        public double DistanceE1E2Max { get; internal set; }

        public double DistanceE3E4Min { get; internal set; }

        public double DistanceE3E4Max { get; internal set; }

        public double DistanceP1Min { get; internal set; }

        public double DistanceP1Max { get; internal set; }

        public double DistanceP2Min { get; internal set; }

        public double DistanceP2Max { get; internal set; }

        /// <summary>
        /// true --> to check E1E2 for normal holes.
        /// false --> to check E3E4 for slotted holes.
        /// </summary>
        public bool DistanceE1E2orE3E4 { get; internal set; }

        /// <summary>
        /// true --> bolt is outer.
        /// false --> bolt is inner.
        /// </summary>
        public bool DistanceIsOuter { get; internal set; }

        /// <summary>
        /// Minimum value for edge distance, maybe orthogonal or not.
        /// </summary>
        public Line2d DistanceE1E2SmallerLine { get; internal set; }
        public double DistanceE1E2Smaller => DistanceE1E2SmallerLine?.Length ?? PlateWithBolts.SPACINGMAXVALUE;

        /// <summary>
        /// Maximum value for edge distance, always orthogonal.
        /// </summary>
        public Line2d DistanceE1E2BiggerLine { get; internal set; }
        public double DistanceE1E2Bigger => DistanceE1E2BiggerLine?.Length ?? PlateWithBolts.SPACINGMAXVALUE;

        public Line2d DistanceE3E4Line { get; internal set; }
        public double DistanceE3E4 => DistanceE3E4Line?.Length ?? PlateWithBolts.SPACINGMAXVALUE;

        public Line2d DistanceP1Line { get; internal set; }
        public double DistanceP1 => DistanceP1Line?.Length ?? PlateWithBolts.SPACINGMAXVALUE;

        public Line2d DistanceP2Line { get; internal set; }
        public double DistanceP2 => DistanceP2Line?.Length ?? PlateWithBolts.SPACINGMAXVALUE;

        public bool DistanceE1E2MinCheck
        {
            get
            {
                if (DistanceIsActive && DistanceE1E2orE3E4 && !double.IsNaN(DistanceE1E2Min) && DistanceIsOuter)
                    return DistanceE1E2Smaller > DistanceE1E2Min - GeometryBase.Tolerance;
                else
                    return true;
            }
        }

        public bool DistanceE1E2MaxCheck
        {
            get
            {
                if (DistanceIsActive && DistanceE1E2orE3E4 && !double.IsNaN(DistanceE1E2Max) && DistanceIsOuter)
                    return DistanceE1E2Bigger < DistanceE1E2Max + GeometryBase.Tolerance;
                else
                    return true;
            }
        }

        public bool DistanceE3E4MinCheck
        {
            get
            {
                if (DistanceIsActive && !DistanceE1E2orE3E4 && !double.IsNaN(DistanceE3E4Min) && DistanceIsOuter)
                    return DistanceE3E4 > DistanceE3E4Min - GeometryBase.Tolerance;
                else
                    return true;
            }
        }

        public bool DistanceE3E4MaxCheck
        {
            get
            {
                if (DistanceIsActive && !DistanceE1E2orE3E4 && !double.IsNaN(DistanceE3E4Max) && DistanceIsOuter)
                    return DistanceE3E4 < DistanceE3E4Max + GeometryBase.Tolerance;
                else
                    return true;
            }
        }

        public bool DistanceP1MinCheck
        {
            get
            {
                if (DistanceIsActive && !double.IsNaN(DistanceP1Min) && DistanceP1 != PlateWithBolts.SPACINGMAXVALUE)
                    return DistanceP1 > DistanceP1Min - GeometryBase.Tolerance;
                else
                    return true;
            }
        }

        public bool DistanceP1MaxCheck
        {
            get
            {
                if (DistanceIsActive && !double.IsNaN(DistanceP1Max) && DistanceP1 != PlateWithBolts.SPACINGMAXVALUE)
                    return DistanceP1 < DistanceP1Max + GeometryBase.Tolerance;
                else
                    return true;
            }
        }

        public bool DistanceP2MinCheck
        {
            get
            {
                if (DistanceIsActive && !double.IsNaN(DistanceP2Min) && DistanceP2 != PlateWithBolts.SPACINGMAXVALUE)
                    return DistanceP2 > DistanceP2Min - GeometryBase.Tolerance;
                else
                    return true;
            }
        }

        public bool DistanceP2MaxCheck
        {
            get
            {
                if (DistanceIsActive && !double.IsNaN(DistanceP2Max) && DistanceP2 != PlateWithBolts.SPACINGMAXVALUE)
                    return DistanceP2 < DistanceP2Max + GeometryBase.Tolerance;
                else
                    return true;
            }
        }

        public bool DistanceCheckAll => DistanceE1E2MinCheck && DistanceE1E2MaxCheck && DistanceE3E4MinCheck && DistanceE3E4MaxCheck &&
            DistanceP1MinCheck && DistanceP1MaxCheck && DistanceP2MinCheck && DistanceP2MaxCheck;

        #endregion

        #region Constructor

        public EN1993BoltResults(BoltPosition boltPos, ILoadCase @case, ResultBeamForces beamForces,
            StandardEN1993p11 standard, EN1993BoltChecker.EN1993BoltOptions options)
            : base(boltPos, @case, beamForces, standard, options)
        {
            SetActiveChecks();
            // Values must be calculated, if they remain NaN it means there is an error or lack,
            // or is not required by normative.
            SollCombCase = BoltStresses.CombCaseType.SLU;
            SollShear = Double.NaN;
            SollTension = Double.NaN;

            // ShearIsActive = true; --> Setted in SetActiveChecks().
            ShearResistance = Double.NaN;
            ShearRatio = Double.NaN;
            ShearAlphaV = Double.NaN;

            // BearingIsActive = true; --> Setted in SetActiveChecks().
            BearingResistance = Double.NaN;
            BearingRatio = Double.NaN;
            BearingE1 = Double.NaN;
            BearingP1 = Double.NaN;
            BearingE2 = Double.NaN;
            BearingP2 = Double.NaN;
            Bearingk1 = Double.NaN;
            BearingAlphaB = Double.NaN;

            // SlipIsActive = true; --> Setted in SetActiveChecks().
            SlipResistance = Double.NaN;
            SlipRatio = Double.NaN;
            SlipKs = Double.NaN;
            SlipMu = Double.NaN;
            SlipFpc = Double.NaN;

            // SlipSerIsActive = true; --> Setted in SetActiveChecks().
            SlipSerResistance = Double.NaN;
            SlipSerRatio = Double.NaN;
            SlipSerKs = Double.NaN;
            SlipSerMu = Double.NaN;
            SlipSerFpc = Double.NaN;

            NetIsActive = false; // For now we set it to false until a generic way to calculate it is found.
            NetResistance = Double.NaN;
            NetRatio = Double.NaN;

            TensionIsActive = true;
            TensionResistance = Double.NaN;
            TensionRatio = Double.NaN;
            TensionK2 = Double.NaN;

            // CombinedShearTensionIsActive = true; --> Setted in SetActiveChecks().
            CombinedShearTensionRatio = Double.NaN;

            PunchingIsActive = true;
            PunchingResistance = Double.NaN;
            PunchingRatio = Double.NaN;
            PunchingDm = Double.NaN;

            DistanceIsActive = true;
            // Minimum and maximum values for comparison.
            DistanceE1E2Min = Double.NaN;
            DistanceE1E2Max = Double.NaN;
            DistanceE3E4Min = Double.NaN;
            DistanceE3E4Max = Double.NaN;
            DistanceP1Min = Double.NaN;
            DistanceP1Max = Double.NaN;
            DistanceP2Min = Double.NaN;
            DistanceP2Max = Double.NaN;
            // Values to check.
            DistanceE1E2orE3E4 = true;
            DistanceIsOuter = true;
            DistanceE1E2SmallerLine = null;
            DistanceE1E2BiggerLine = null;
            DistanceE3E4Line = null;
            DistanceP1Line = null;
            DistanceP2Line = null;
        }

        #endregion

        #region Methods

        protected void SetActiveChecks()
        {
            switch (eN1993BoltOptions.ShearConnectionsCategory)
            {
                case EN1993BoltChecker.ShearConnectionsCategoryType.A:
                    ShearIsActive = true;
                    BearingIsActive = true;
                    SlipIsActive = false;
                    SlipSerIsActive = false;
                    // NetIsActive = false;
                    CombinedShearTensionIsActive = true;
                    break;
                case EN1993BoltChecker.ShearConnectionsCategoryType.B:
                    ShearIsActive = true;
                    BearingIsActive = true;
                    SlipIsActive = false;
                    SlipSerIsActive = true;
                    // NetIsActive = false;
                    CombinedShearTensionIsActive = true;
                    break;
                case EN1993BoltChecker.ShearConnectionsCategoryType.C:
                    ShearIsActive = false; // By regulation it should be set to false, but doing so should also set CombinedShearTensionIsActive to false.
                    BearingIsActive = true;
                    SlipIsActive = true;
                    SlipSerIsActive = false;
                    // NetIsActive = true;
                    CombinedShearTensionIsActive = false;
                    break;
                default:
                    ShearIsActive = true;
                    BearingIsActive = true;
                    SlipIsActive = true;
                    SlipSerIsActive = true;
                    // NetIsActive = true;
                    CombinedShearTensionIsActive = true;
                    break;
            }
        }

        // Disable unnecessary verification.
        public void SetUnnecessaryVerification()
        {
            if (SollShearIsNull)
            {
                ShearIsActive = false;
                BearingIsActive = false;
                SlipIsActive = false;
                SlipSerIsActive = false;
                NetIsActive = false;
                DistanceIsActive = false;
            }
            if (SollTensionIsNull)
            {
                TensionIsActive = false;
                PunchingIsActive = false;
            }
            if (SollShearIsNull || SollTensionIsNull)
            {
                CombinedShearTensionIsActive = false;
            }

            if (SollCombCase == BoltStresses.CombCaseType.SLU)
            {
                SlipSerIsActive = false;
            }
            else if (SollCombCase == BoltStresses.CombCaseType.SLS)
            {
                ShearIsActive = false;
                BearingIsActive = false;
                SlipIsActive = false;
                NetIsActive = false;
                TensionIsActive = false;
                CombinedShearTensionIsActive = false;
                PunchingIsActive = false;
            }
        }

        internal override double GetMaxWorkingRatio()
        {
            return CalcMaxRatio();
        }

        /// <summary>
        /// Given a list of results calculates the most onerous situation.
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public static EN1993BoltResults CalcMaxResult(List<BoltResults> boltResults)
        {
            if (boltResults is null || boltResults.Count == 0)
                return null;

            List<EN1993BoltResults> boltResultsEN1993 = boltResults.Cast<EN1993BoltResults>().ToList();

            if (boltResultsEN1993.Count == 0)
                return null;

            EN1993BoltResults maxResult = new EN1993BoltResults(null, new LoadCase("Envelope", Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight), null,
                (StandardEN1993p11)boltResultsEN1993[0].Standard, (EN1993BoltChecker.EN1993BoltOptions)boltResultsEN1993[0].Options)
            {
                // Max required forces.
                SollShear = boltResultsEN1993.Max(br => br.SollShear),
                SollTension = boltResultsEN1993.Max(br => br.SollTension)
            };

            // Shear.
            var itemMaxShearIenum = boltResultsEN1993.Where(br => !double.IsNaN(br.ShearRatio));
            if (itemMaxShearIenum.Count() == 0)
            {
                maxResult.ShearIsActive = false;
            }
            else
            {
                var itemMaxShear = itemMaxShearIenum.OrderByDescending(br => br.ShearRatio).First();
                maxResult.ShearIsActive = true;
                maxResult.ShearResistance = itemMaxShear.ShearResistance;
                maxResult.ShearRatio = itemMaxShear.ShearRatio;
                maxResult.ShearAlphaV = itemMaxShear.ShearAlphaV;
            }

            // Bearing.
            var itemMaxBearingIenum = boltResultsEN1993.Where(br => !double.IsNaN(br.BearingRatio));
            if (itemMaxBearingIenum.Count() == 0)
            {
                maxResult.BearingIsActive = false;
            }
            else
            {
                var itemMaxBearing = itemMaxBearingIenum.OrderByDescending(br => br.BearingRatio).First();
                maxResult.BearingIsActive = true;
                maxResult.BearingResistance = itemMaxBearing.BearingResistance;
                maxResult.BearingRatio = itemMaxBearing.BearingRatio;
                maxResult.BearingE1 = itemMaxBearing.BearingE1;
                maxResult.BearingP1 = itemMaxBearing.BearingP1;
                maxResult.BearingE2 = itemMaxBearing.BearingE2;
                maxResult.BearingP2 = itemMaxBearing.BearingP2;
                maxResult.Bearingk1 = itemMaxBearing.Bearingk1;
                maxResult.BearingAlphaB = itemMaxBearing.BearingAlphaB;
            }

            // Slip.
            var itemMaxSlipIenum = boltResultsEN1993.Where(br => !double.IsNaN(br.SlipRatio));
            if (itemMaxSlipIenum.Count() == 0)
            {
                maxResult.SlipIsActive = false;
            }
            else
            {
                var itemMaxSlip = itemMaxSlipIenum.OrderByDescending(br => br.SlipRatio).First();
                maxResult.SlipIsActive = true;
                maxResult.SlipResistance = itemMaxSlip.SlipResistance;
                maxResult.SlipRatio = itemMaxSlip.SlipRatio;
                maxResult.SlipKs = itemMaxSlip.SlipKs;
                maxResult.SlipMu = itemMaxSlip.SlipMu;
                maxResult.SlipFpc = itemMaxSlip.SlipFpc;
            }

            // Slip service
            var itemMaxSlipSerIenum = boltResultsEN1993.Where(br => !double.IsNaN(br.SlipSerRatio));
            if (itemMaxSlipSerIenum.Count() == 0)
            {
                maxResult.SlipSerIsActive = false;
            }
            else
            {
                var itemMaxSlipSer = itemMaxSlipSerIenum.OrderByDescending(br => br.SlipSerRatio).First();
                maxResult.SlipSerIsActive = true;
                maxResult.SlipSerResistance = itemMaxSlipSer.SlipSerResistance;
                maxResult.SlipSerRatio = itemMaxSlipSer.SlipSerRatio;
                maxResult.SlipSerKs = itemMaxSlipSer.SlipSerKs;
                maxResult.SlipSerMu = itemMaxSlipSer.SlipSerMu;
                maxResult.SlipSerFpc = itemMaxSlipSer.SlipSerFpc;
            }

            // Net.
            var itemMaxNetIenum = boltResultsEN1993.Where(br => !double.IsNaN(br.NetRatio));
            if (itemMaxNetIenum.Count() == 0)
            {
                maxResult.NetIsActive = false;
            }
            else
            {
                var itemMaxNet = itemMaxNetIenum.OrderByDescending(br => br.NetRatio).First();
                maxResult.NetIsActive = true;
                maxResult.NetResistance = itemMaxNet.NetResistance;
                maxResult.NetRatio = itemMaxNet.NetRatio;
            }

            // Tension.
            var itemMaxTensionIenum = boltResultsEN1993.Where(br => !double.IsNaN(br.TensionRatio));
            if (itemMaxTensionIenum.Count() == 0)
            {
                maxResult.TensionIsActive = false;
            }
            else
            {
                var itemMaxTension = itemMaxTensionIenum.OrderByDescending(br => br.TensionRatio).First();
                maxResult.TensionIsActive = true;
                maxResult.TensionResistance = itemMaxTension.TensionResistance;
                maxResult.TensionRatio = itemMaxTension.TensionRatio;
                maxResult.TensionK2 = itemMaxTension.TensionK2;
            }

            // CombinedShearTension
            var itemMaxCombinedShearTensionIenum = boltResultsEN1993.Where(br => !double.IsNaN(br.CombinedShearTensionRatio));
            if (itemMaxCombinedShearTensionIenum.Count() == 0)
            {
                maxResult.CombinedShearTensionIsActive = false;
            }
            else
            {
                var itemMaxCombinedShearTension = itemMaxCombinedShearTensionIenum.OrderByDescending(br => br.CombinedShearTensionRatio).First();
                maxResult.CombinedShearTensionIsActive = true;
                maxResult.CombinedShearTensionRatio = itemMaxCombinedShearTension.CombinedShearTensionRatio;
            }

            // Punching
            var itemMaxPunchingIenum = boltResultsEN1993.Where(br => !double.IsNaN(br.PunchingRatio));
            if (itemMaxPunchingIenum.Count() == 0)
            {
                maxResult.PunchingIsActive = false;
            }
            else
            {
                var itemMaxPunching = itemMaxPunchingIenum.OrderByDescending(br => br.PunchingRatio).First();
                maxResult.PunchingIsActive = true;
                maxResult.PunchingResistance = itemMaxPunching.PunchingResistance;
                maxResult.PunchingRatio = itemMaxPunching.PunchingRatio;
                maxResult.PunchingDm = itemMaxPunching.PunchingDm;
            }
            return maxResult;
        }

        public override double CalcMaxRatio()
        {
            double maxRatio = 0.0;

            if (ShearIsActive)
                maxRatio = Math.Max(maxRatio, ShearRatio);

            if (BearingIsActive)
                maxRatio = Math.Max(maxRatio, BearingRatio);

            if (SlipIsActive)
                maxRatio = Math.Max(maxRatio, SlipRatio);

            if (SlipSerIsActive)
                maxRatio = Math.Max(maxRatio, SlipSerRatio);

            if (NetIsActive)
                maxRatio = Math.Max(maxRatio, NetRatio);

            if (TensionIsActive)
                maxRatio = Math.Max(maxRatio, TensionRatio);

            if (CombinedShearTensionIsActive)
                maxRatio = Math.Max(maxRatio, CombinedShearTensionRatio);

            if (PunchingIsActive)
                maxRatio = Math.Max(maxRatio, PunchingRatio);

            return maxRatio;
        }

        public List<DistanceWarning> GetDistancesWarnings()
        {
            var warnings = new List<DistanceWarning>();

            if (!DistanceE1E2MinCheck)
                warnings.Add(new DistanceWarning(BoltPos.Id, DimensionWarningType.EdgeLower, DistanceE1E2SmallerLine, DistanceE1E2Min, BeamForces));

            if (!DistanceE1E2MaxCheck)
                warnings.Add(new DistanceWarning(BoltPos.Id, DimensionWarningType.EdgeUpper, DistanceE1E2BiggerLine, DistanceE1E2Max, BeamForces));

            if (!DistanceE3E4MinCheck)
                warnings.Add(new DistanceWarning(BoltPos.Id, DimensionWarningType.EdgeLower, DistanceE3E4Line, DistanceE3E4Min, BeamForces));

            if (!DistanceE3E4MaxCheck)
                warnings.Add(new DistanceWarning(BoltPos.Id, DimensionWarningType.EdgeUpper, DistanceE3E4Line, DistanceE3E4Max, BeamForces));

            if (!DistanceP1MinCheck)
                warnings.Add(new DistanceWarning(BoltPos.Id, DimensionWarningType.SpacingP1Lower, DistanceP1Line, DistanceP1Min, BeamForces));

            if (!DistanceP1MaxCheck)
                warnings.Add(new DistanceWarning(BoltPos.Id, DimensionWarningType.SpacingP1Upper, DistanceP1Line, DistanceP1Max, BeamForces));

            if (!DistanceP2MinCheck)
                warnings.Add(new DistanceWarning(BoltPos.Id, DimensionWarningType.SpacingP2Lower, DistanceP2Line, DistanceP2Min, BeamForces));

            if (!DistanceP2MaxCheck)
                warnings.Add(new DistanceWarning(BoltPos.Id, DimensionWarningType.SpacingP2Upper, DistanceP2Line, DistanceP2Max, BeamForces));

            return warnings;
        }

        public static List<DistanceWarning> GetDistancesWarnings(List<BoltResults> boltResults)
        {
            var warnings = new List<DistanceWarning>();
            List<EN1993BoltResults> boltResultsEN1993 = boltResults.Cast<EN1993BoltResults>().ToList();

            foreach (var boltResult in boltResultsEN1993)
                warnings.AddRange(boltResult.GetDistancesWarnings());

            return warnings;
        }

        #endregion

        #region Nested Class

        /// <summary>
        /// Class to record distance warnings.
        /// </summary>
        public class DistanceWarning : IEquatable<DistanceWarning>
        {
            public int BoltId { get; internal set; }

            public DimensionWarningType Type { get; internal set; }

            /// <summary>
            /// From center to edge or other bolt.
            /// </summary>
            public Line2d DistanceLine { get; internal set; }

            public double Distance => DistanceLine?.Length ?? PlateWithBolts.SPACINGMAXVALUE;

            /// <summary>
            /// Comparison distance, lower or upper limit.
            /// </summary>
            public double Limit { get; internal set; }

            /// <summary>
            /// Combination that generates the warning, useful to distinguish which one generates the maximum p1 and p2 spacing.
            /// </summary>
            public ResultBeamForces BeamForces { get; internal set; }

            public DistanceWarning(int boltId, DimensionWarningType type, Line2d distanceLine, double limit, ResultBeamForces beamForces = null)
            {
                BoltId = boltId;
                Type = type;
                DistanceLine = distanceLine;
                Limit = limit;
                BeamForces = beamForces;
            }

            public override string ToString()
            {
                string boltName = $"Bolt {BoltId}, ";
                string dist = Math.Round(Distance, 1).ToString();
                switch (Type)
                {
                    case DimensionWarningType.EdgeLower:
                        return $"{boltName}edge distance, {dist} smaller than {Limit}.";
                    case DimensionWarningType.EdgeUpper:
                        return $"{boltName}edge distance, {dist} bigger than {Limit}.";
                    case DimensionWarningType.SpacingP1Lower:
                        return $"{boltName}spacing p1, {dist} smaller than {Limit}, combination {BeamForces.Id}.";
                    case DimensionWarningType.SpacingP1Upper:
                        return $"{boltName}spacing p1, {dist} bigger than {Limit}, combination {BeamForces.Id}.";
                    case DimensionWarningType.SpacingP2Lower:
                        return $"{boltName}spacing p2, {dist} smaller than {Limit}, combination {BeamForces.Id}.";
                    case DimensionWarningType.SpacingP2Upper:
                        return $"{boltName}spacing p2, {dist} bigger than {Limit}, combination {BeamForces.Id}.";
                    default:
                        return "";
                }
            }

            public double CalculateError()
            {
                switch (Type)
                {
                    case DimensionWarningType.EdgeLower:
                    case DimensionWarningType.SpacingP1Lower:
                    case DimensionWarningType.SpacingP2Lower:
                        return Limit - Distance;
                    case DimensionWarningType.EdgeUpper:
                    case DimensionWarningType.SpacingP1Upper:
                    case DimensionWarningType.SpacingP2Upper:
                        return Distance - Limit;
                    default:
                        return 0.0;
                }
            }

            #region Comparers

            public override bool Equals(object obj)
            {
                return Equals(obj as DistanceWarning);
            }

            public bool Equals(DistanceWarning other)
            {
                return !(other is null) &&
                       BoltId == other.BoltId &&
                       Type == other.Type &&
                       DistanceLine == other.DistanceLine &&
                       Limit == other.Limit;
            }

            public override int GetHashCode()
            {
                int hashCode = -978898138;
                hashCode = hashCode * -1521134295 + BoltId.GetHashCode();
                hashCode = hashCode * -1521134295 + Type.GetHashCode();
                hashCode = hashCode * -1521134295 + DistanceLine.GetHashCode();
                hashCode = hashCode * -1521134295 + Limit.GetHashCode();
                return hashCode;
            }

            public static bool operator ==(DistanceWarning left, DistanceWarning right)
            {
                return EqualityComparer<DistanceWarning>.Default.Equals(left, right);
            }

            public static bool operator !=(DistanceWarning left, DistanceWarning right)
            {
                return !(left == right);
            }

            #endregion
        }

        #endregion
    }
}
