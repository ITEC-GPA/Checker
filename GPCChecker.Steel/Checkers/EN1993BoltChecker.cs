using GPC.Checkers.Steel.Results;
using GPC.Model.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Checkers
{
    public class EN1993BoltChecker : BoltChecker
    {
        #region Public Constructor

        public EN1993BoltChecker(BoltGrid boltGrid, List<BoltStresses> boltStresses, StandardEN1993p11 standard, EN1993BoltOptions options) :
            base(boltGrid, boltStresses, standard, options)
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
                var SollAllBolts = _boltGrid.CalculateShearForcesElastic(SolForce.ResBeamForces);

                foreach (var SollBolt in SollAllBolts)
                {
                    // Initialize a result.
                    var CurrentResult = new EN1993BoltResults(SollBolt.Key, new LoadCase("COMB0", LoadCase.LoadCaseTypes.SelfWeight),
                        SollBolt.Value, StandardEN1993, OptionsEN1993);

                    // Add the other results.
                    CurrentResult.ShearResistance = CalculateShearResistance_FvRd(SollBolt.Key.BoltDef);
                    CurrentResult.RatioShear = GetWorkingRatio(SollBolt.Value.GetCombinedShearForce(), CurrentResult.ShearResistance);

                    CurrentResult.TensionResistance = CalculateTensionResistance_FtRd(SollBolt.Key.BoltDef);
                    CurrentResult.RatioTension = GetWorkingRatio(0, CurrentResult.TensionResistance);

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

            #endregion

            #region Constructor

            public EN1993BoltOptions()
            {
                HoleShape = HoleShapeType.NormalRound;
                IsLongitudinalPerpendicular = false;
            }

            #endregion
        }

        #endregion
    }

}
