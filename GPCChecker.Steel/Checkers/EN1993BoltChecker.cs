using GPC.Checkers.Steel.Results;
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
        #region Public Constructor

        public EN1993BoltChecker(PlateWithBolts plateWithBolts, List<BoltStresses> boltStresses, StandardEN1993p11 standard, EN1993BoltOptions options, int id = IDUNASSIGNED, string name = "")
            : base(plateWithBolts, boltStresses, standard, options, id, name)
        { }

        #endregion

        #region Public Properties

        public StandardEN1993p11 StandardEN1993 => (StandardEN1993p11)_standard;

        public EN1993BoltOptions OptionsEN1993 => (EN1993BoltOptions)_options;

        public List<EN1993BoltResults> BoltResultsEN1993 => _boltResults.Cast<EN1993BoltResults>().ToList();

        public override ENCommonBoltResults BoltResultMax => (EN1993BoltResults)_boltResultMax;

        protected override double PlateMaterialFu => ((SteelMaterial)_plateWithBolts.PlateMaterial).Fu;

        protected override double EnGammaM2 => StandardEN1993.GammaM2;

        protected override double EnGammaM3 => StandardEN1993.GammaM3;

        protected override double EnGammaM3Ser => StandardEN1993.GammaM3Ser;

        #endregion

        #region Private Methods

        protected override ENCommonBoltResults BuildENCommonBoltResults(BoltPosition boltPos, ILoadCase loadCase, ResultBeamForces beamForces)
        {
            return new EN1993BoltResults(boltPos, loadCase, beamForces, StandardEN1993, OptionsEN1993);
        }

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
        protected override double CalculateCoeffParallel_AlphaB(in double alpha_d, in BoltSection boltSection, in double e_1, in double p_1, in double d_0)
        {
            return Math.Min(Math.Min(alpha_d, boltSection.BoltMaterial.Fu / PlateMaterialFu), 1.0);
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
        protected override double CalculatePunchingShearResistance_BpRd(in double plateWithBoltsThickness, in double d_m, in double d_0)
        {
            return 0.6 * Math.PI * d_m * plateWithBoltsThickness * PlateMaterialFu / EnGammaM2;
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
                case ExposureConditionType.Exposed:
                    e1e2_max = 4.0 * t + 40.0;
                    e3e4_max = Double.NaN;
                    p1_max = Math.Min(14.0 * t, 200.0);
                    p2_max = p1_max;
                    break;
                case ExposureConditionType.NotExposed:
                    e1e2_max = Double.NaN;
                    e3e4_max = Double.NaN;
                    p1_max = Math.Min(14.0 * t, 200.0);
                    p2_max = p1_max;
                    break;
                case ExposureConditionType.Unprotected:
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
            public EN1993BoltOptions()
            {
            }
        }

        #endregion
    }

}
