using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;

namespace GPC.Checkers.Steel.Results
{
    [Serializable]
    public class EN1993BoltResults : BoltResults, ISerializable
    {
        #region Properties

        public EN1993BoltChecker.EN1993BoltOptions eN1993BoltOptions => Options as EN1993BoltChecker.EN1993BoltOptions;

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

            TensionIsActive = false; // For now we set it to false until a generic way to calculate it is found.
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
                case EN1993BoltChecker.EN1993BoltOptions.ShearConnectionsCategoryType.A:
                    ShearIsActive = true;
                    BearingIsActive = true;
                    SlipIsActive = false;
                    SlipSerIsActive = false;
                    // NetIsActive = false;
                    break;
                case EN1993BoltChecker.EN1993BoltOptions.ShearConnectionsCategoryType.B:
                    ShearIsActive = true;
                    BearingIsActive = true;
                    SlipIsActive = false;
                    SlipSerIsActive = true;
                    // NetIsActive = false;
                    break;
                case EN1993BoltChecker.EN1993BoltOptions.ShearConnectionsCategoryType.C:
                    ShearIsActive = true; // By regulation it should be set to false, but doing so should also set CombinedShearTensionIsActive to false.
                    BearingIsActive = true;
                    SlipIsActive = true;
                    SlipSerIsActive = false;
                    // NetIsActive = true;
                    break;
                default:
                    ShearIsActive = true;
                    BearingIsActive = true;
                    SlipIsActive = true;
                    SlipSerIsActive = true;
                    // NetIsActive = true;
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
        }

        #endregion

    }
}
