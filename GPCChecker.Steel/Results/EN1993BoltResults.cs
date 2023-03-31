using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Results
{
    [Serializable]
    public class EN1993BoltResults : BoltResults, ISerializable
    {
        #region Properties

        public EN1993BoltChecker.EN1993BoltOptions eN1993BoltOptions => Options as EN1993BoltChecker.EN1993BoltOptions;

        public CombCaseType SollCombCase { get; internal set; }

        /// <summary>
        /// External shear sollecitation.
        /// </summary>
        public double SollShear { get; internal set; }
        public bool SollShearIsNull => SollShear < 1;

        /// <summary>
        /// External tension sollecitation.
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

        #endregion

        #region Constructor

        public EN1993BoltResults(BoltGrid.BoltPosition boltPos, ILoadCase @case, ResultBeamForces beamForces,
            StandardEN1993p11 standard, EN1993BoltChecker.EN1993BoltOptions options)
            : base(boltPos, @case, beamForces, standard, options)
        {
            SetActiveChecks();
            // Values must be calculated, if they remain NaN it means there is an error or lack,
            // or is not required by normative.
            SollCombCase = CombCaseType.SLU;
            SollShear = Double.NaN;
            SollTension = Double.NaN;

            ShearResistance = Double.NaN;
            ShearRatio = Double.NaN;
            ShearAlphaV = Double.NaN;

            BearingResistance = Double.NaN;
            BearingRatio = Double.NaN;
            BearingE1 = Double.NaN;
            BearingP1 = Double.NaN;
            BearingE2 = Double.NaN;
            BearingP2 = Double.NaN;
            Bearingk1 = Double.NaN;
            BearingAlphaB = Double.NaN;

            SlipResistance = Double.NaN;
            SlipRatio = Double.NaN;
            SlipKs = Double.NaN;
            SlipMu = Double.NaN;

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

            CombinedShearTensionIsActive = true;
            CombinedShearTensionRatio = Double.NaN;

            PunchingIsActive = true;
            PunchingResistance = Double.NaN;
            PunchingRatio = Double.NaN;
            PunchingDm = Double.NaN;
        }

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

            if (SollCombCase == CombCaseType.SLU)
            {
                SlipSerIsActive = false;
            }
            else if (SollCombCase == CombCaseType.SLS)
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
            throw new NotImplementedException();
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

            if (boltResultsEN1993 .Count == 0 )
                return null;

            EN1993BoltResults maxResult = new EN1993BoltResults(null, new LoadCase("Envelope", Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight), null,
                (StandardEN1993p11)boltResultsEN1993[0].Standard, (EN1993BoltChecker.EN1993BoltOptions)boltResultsEN1993[0].Options);

            // Max sollecitations.
            maxResult.SollShear = boltResultsEN1993.Max(br => br.SollShear);
            maxResult.SollTension = boltResultsEN1993.Max(br => br.SollTension);

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

        #endregion
    }
}
