using GPC.Checkers.Steel.Results;
using GPC.Model.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using static GPC.Checkers.Steel.Checkers.EN1993BoltChecker.EN1993BoltOptions;

namespace GPC.Checkers.Steel.Checkers
{
    public class EN1993BoltChecker : BoltChecker
    {
        #region Public Constructor

        public EN1993BoltChecker(RectangularPlateWithBolts plateWithBolts, List<BoltStresses> boltStresses, StandardEN1993p11 standard, EN1993BoltOptions options) :
            base(plateWithBolts, boltStresses, standard, options)
        { }

        #endregion

        #region Public Properties

        public StandardEN1993p11 StandardEN1993 => (StandardEN1993p11)_standard;

        public EN1993BoltOptions OptionsEN1993 => (EN1993BoltOptions)_options;

        public List<EN1993BoltResults> BoltResultsEN1993 => _boltResults.Cast<EN1993BoltResults>().ToList();

        #endregion

        #region Public Methods

        /// <summary>
        /// Permorme all checks.
        /// </summary>
        public override void PerformCheck()
        {
            _boltResults = new List<BoltResults> { };

            foreach (var SolForce in _boltStresses)
            {
                // Reduce the forces according to the number of cutting planes.
                var ReducedForces = SolForce.ResBeamForces / OptionsEN1993.NumShearPlane;
                // Calculate all shear forces for each bolt.
                var SollAllBolts = _plateWithBolts.BoltGrid.CalculateShearForcesElastic(SolForce.ResBeamForces);
                // Calculate uniform tension forces for each bolt.
                var SollN = SolForce.ResBeamForces.N > 0.0 ? SolForce.ResBeamForces.N / SollAllBolts.Count() : 0;
                if (SollN > 1) // Positive for tension.
                    foreach (var SollBolt in SollAllBolts)
                        SollBolt.Value.N = SollN;

                foreach (var SollBolt in SollAllBolts)
                {
                    // Initialize a result.
                    var CurrentResult = new EN1993BoltResults(SollBolt.Key, SolForce.LoadCase, SollBolt.Value, StandardEN1993, OptionsEN1993);

                    // Add the other results.
                    // Shear.
                    var SollShear = SollBolt.Value.GetCombinedShearForce();
                    CurrentResult.ShearResistance = CalculateShearResistance_FvRd(SollBolt.Key.BoltDef);
                    CurrentResult.RatioShear = GetWorkingRatio(SollShear, CurrentResult.ShearResistance);

                    // Tension.
                    var SollTension = SollBolt.Value.N;
                    CurrentResult.TensionResistance = CalculateTensionResistance_FtRd(SollBolt.Key.BoltDef);
                    CurrentResult.RatioTension = GetWorkingRatio(SollTension, CurrentResult.TensionResistance);

                    // Combined Shear and Tension.
                    CurrentResult.RatioCombinedShearTension = SollShear / CurrentResult.ShearResistance + SollTension / (1.4 * CurrentResult.TensionResistance);

                    // Save to the results table.
                    _boltResults.Add(CurrentResult);
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
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

            string ClassName = boltMaterial.Name;

            if (OptionsEN1993.ShearPlaneThroughThreadedPortion)
            {
                if (ClassName == "4.6" || ClassName == "5.6" || ClassName == "8.8")
                    return 0.6;
                else if (ClassName == "4.8" || ClassName == "5.8" || ClassName == "6.8" || ClassName == "10.9")
                    return 0.5;
                else
                {
                    _errorLog.Add($"Warning: class '{ClassName}' not recognized, α_v=0.5 will be used.");
                    return 0.5;
                }
            }
            else
                return 0.6;
        }

        /// <summary>
        /// Calculate shear resistance per shear plane.
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// Rivets are not considered.
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns>F_v,Rd</returns>
        private double CalculateShearResistance_FvRd(in BoltSection boltSection)
        {
            double alpha_v = CalculateAlphaV(boltSection.BoltMaterial);
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
        private double CalculateTensionResistance_FtRd(in BoltSection boltSection)
        {
            double k_2 = CalculateK_2();
            double f_ub = boltSection.BoltMaterial.Fu;
            double area = boltSection.CalculateAreaEff();
            return k_2 * f_ub * area / StandardEN1993.GammaM2;
        }

        //private double CalculateCoeffParallel_alphad_GlobalSimple(in RectangularPlateWithBolts plateWithBolts)
        //{
        //    double e_1 = Math.Min(Math.Min(plateWithBolts.E_x_left, plateWithBolts.E_x_right), Math.Min(plateWithBolts.E_y_bottom, plateWithBolts.E_y_top));
        //    double p_1 = Math.Min(plateWithBolts.P_x, plateWithBolts.P_y);
        //    double d_0 = RectangularPlateWithBolts.
        //}

        //private double CalculateCoeffPerpendicular_k1_GlobalSimple(in RectangularPlateWithBolts plateWithBolts)
        //{
        //    double e_2 = Math.Min(Math.Min(plateWithBolts.E_x_left, plateWithBolts.E_x_right), Math.Min(plateWithBolts.E_y_bottom, plateWithBolts.E_y_top));
        //    double p_2 = Math.Min(plateWithBolts.P_x, plateWithBolts.P_y);
        //}

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
        /// <param name="boltSection">Current bolt.</param>
        /// <returns>B_p,Rd</returns>
        private double CalculatePunchingShearResistance_BpRd(in PlateWithBolts plateWithBolts, in BoltSection boltSection)
        {
            return 0.6 * Math.PI * boltSection.CalculateMeanDiameterBoltHead() * plateWithBolts.Thickness * plateWithBolts.PlateMaterial.Fu / StandardEN1993.GammaM2;
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
        /// Calculate design Slip resistance.
        /// UNI EN 1993-1-8:2005 - 3.9 Slip-resistant connections using 8.8 or 10.9 bolts.
        /// </summary>
        /// <param name="k_s"></param>
        /// <param name="boltSection"></param>
        /// <param name="F_tEd">F_t,Ed (formula 3.8a) or F_t,Ed,ser (formula 3.8b).</param>
        /// <param name="gammaM">γ_M3 or γ_M3,ser</param>
        /// <returns>F_s,Rd or F_s,Rd,ser</returns>
        private double CalculateDesignSlipResistance(in double k_s, in BoltSection boltSection, in double F_tEd, in double gammaM)
        {
            double F_pC = 0.7 * boltSection.BoltMaterial.Fu * boltSection.CalculateAreaEff();
            return k_s * OptionsEN1993.NumFricionPlane * CalculateSlipFactor_Mu() * (F_pC - 0.8 * F_tEd) / gammaM;
        }
        private double CalculateDesignSlipResistance_FsRd(in double k_s, in BoltSection boltSection, in double F_tEd)
            => CalculateDesignSlipResistance(k_s, boltSection, F_tEd, StandardEN1993.GammaM3);
        private double CalculateDesignSlipResistance_FsRdser(in double k_s, in BoltSection boltSection, in double F_tEdser)
            => CalculateDesignSlipResistance(k_s, boltSection, F_tEdser, StandardEN1993.GammaM3ser);

        /// <summary>
        /// Calculate the design plastic resistance of the net cross-section at bolt holes.
        /// UNI EN 1993-1-8:2005 - 3.4.1 Shear connections (1)c).
        /// UNI EN 1993-1-1:2005 - 6.2.3 (4) (formula 6.8).
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns>N_net,Rd</returns>
        private double CalculatedesignPlasticResistance_NnetRd(in BoltSection boltSection)
        {
            return boltSection.CalculateAreaEff() * boltSection.BoltMaterial.Fyk / StandardEN1993.GammaM0;
        }

        #endregion

        #region Nested Class Options

        /// <summary>
        /// Options specific for EN1993.
        /// </summary>
        public class EN1993BoltOptions : Options
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
            /// </summary>
            public enum ClassFrictionSurfacesType
            {
                A,
                B,
                C,
                D
            }

            #endregion

            #region Properties

            /// <summary>
            /// UNI EN 1993-1-8:2005 - Table 3.6 and EN 1090-2 Table 11.
            /// Influence on bearing resistance Fb,Rd and tollerances, as well as the property IsLongitudinalPerpendicular.
            /// </summary>
            public HoleShapeType HoleShape { get; set; }

            /// <summary>
            /// Is longitudinal axis of the slotted hole perpendicular to the direction of force transfer?
            /// </summary>
            public bool IsLongitudinalPerpendicular { get; set; } // Is longitudinal axis of the slotted hole perpendicular to the direction of force transfer?

            /// <summary>
            /// Class of friction surfaces.
            /// </summary>
            public ClassFrictionSurfacesType ClassFrictionSurfaces { get; set; }

            #endregion

            #region Constructor

            public EN1993BoltOptions()
            {
                HoleShape = HoleShapeType.NormalRound;
                IsLongitudinalPerpendicular = false;
                ClassFrictionSurfaces = ClassFrictionSurfacesType.D;
            }

            #endregion
        }

        #endregion
    }

}
