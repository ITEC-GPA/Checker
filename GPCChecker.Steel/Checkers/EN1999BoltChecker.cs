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
using static GPC.Checkers.Steel.Checkers.EN1993BoltChecker;

namespace GPC.Checkers.Steel.Checkers
{
    public class EN1999BoltChecker : ENCommonBoltChecker
    {
        #region Public Properties

        public StandardEN1999p11 StandardEN1999 => (StandardEN1999p11)_standard;

        public EN1999BoltOptions OptionsEN1999 => (EN1999BoltOptions)_options;

        public List<EN1999BoltResults> BoltResultsEN1999 => _boltResults.Cast<EN1999BoltResults>().ToList();

        public override ENCommonBoltResults BoltResultMax => (EN1999BoltResults)_boltResultMax;

        protected override double EnGammaM2 => StandardEN1999.GammaM2;

        protected override double EnGammaM3 => StandardEN1999.GammaMs;

        protected override double EnGammaM3Ser => StandardEN1999.GammaMsSer;

        protected override double PlateMaterialFu => ((AluminiumMaterial)_plateWithBolts.PlateMaterial).Fu;

        public override bool IsLessThanMaximumThickness => _plateWithBolts.Thickness < ((AluminiumMaterial)_plateWithBolts.PlateMaterial).ThicknessMax;

        #endregion

        #region Constructor

        public EN1999BoltChecker(PlateWithBolts plateWithBolts, List<BoltStresses> boltStresses, Standard standard, BoltOptions options, int id = IDUNASSIGNED, string name = "")
            : base(plateWithBolts, boltStresses, standard, options, id, name)
        {
        }

        #endregion

        #region Protected Methods

        protected override ENCommonBoltResults BuildENCommonBoltResults(BoltPosition boltPos, ILoadCase loadCase, ResultBeamForces beamForces)
        {
            return new EN1999BoltResults(boltPos, loadCase, beamForces, StandardEN1999, OptionsEN1999);
        }

        /// <summary>
        /// UNI EN 1999-1-1:2023 - Does not specify a value, uses 0.9 for all cases.
        /// </summary>
        /// <returns>k_2</returns>
        protected override double CalculateK_2()
        {
            return 0.9;
        }

        /// <summary>
        /// Not defined in EN1999-1-1:2023.<br/>
        /// Used in CalculateCoeffParallel_AlphaB and if setted to 1.0 does not influence results.
        /// </summary>
        /// <param name="e_1"></param>
        /// <param name="p_1"></param>
        /// <param name="boltPos"></param>
        /// <returns></returns>
        protected override double CalculateCoeffParallel_AlphaD(in double e_1, in double p_1, in BoltPosition boltPos)
        {
            return 1.0;
        }

        /// <summary>
        /// EN1999-1-1:2023 - Table 10.3.
        /// </summary>
        /// <returns>α_b</returns>
        protected override double CalculateCoeffParallel_AlphaB(in double alpha_d, in BoltSection boltSection, in double e_1, in double p_1, in double d_0)
        {
            return Math.Min(Math.Min(e_1 / d_0, p_1 / d_0 - 0.5), Math.Min(3.0 * boltSection.BoltMaterial.Fu / PlateMaterialFu, 3.0));
        }

        /// <summary>
        /// Not defined in EN1999-1-1:2023, you get the same result by setting the value to 1.0.
        /// </summary>
        /// <param name="e_2"></param>
        /// <param name="p_2"></param>
        /// <param name="boltPos"></param>
        /// <returns>k_1</returns>
        protected override double CalculateCoeffPerpendicular_k1(in double e_2, in double p_2, in BoltPosition boltPos)
        {
            return 1.0;
        }

        protected override double CalculatePunchingShearResistance_BpRd(in double plateWithBoltsThickness, in double d_m, in double d_0)
        {
            if (OptionsEN1999.IsCounterSunkBolt)
                return 0.3 * Math.PI * (d_0 + plateWithBoltsThickness) * plateWithBoltsThickness * PlateMaterialFu / EnGammaM2;
            else
                return 0.6 * Math.PI * d_m * plateWithBoltsThickness * PlateMaterialFu / EnGammaM2;
        }

        /// <summary>
        /// EN1999-1-1:2023 - § 10.5.8.4: Calculate nominal minimum preloading force -> F_pA.
        /// </summary>
        /// <param name="boltSection"></param>
        /// <returns></returns>
        protected override double CalculateSlipPreloading(in BoltSection boltSection)
        {
            if (boltSection.BoltMaterial is BoltMaterialEN1993Inox boltMaterialInox)
            {
                return 0.7 * boltMaterialInox.Fyk * boltSection.CalculateAreaEff();
            }
            else
            {
                return 0.7 * boltSection.BoltMaterial.Fu * boltSection.CalculateAreaEff();
            }
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

            switch (OptionsEN1999.ExposureCondition)
            {
                case EN1993BoltChecker.ExposureConditionType.Exposed:
                    e1e2_max = 4.0 * t + 40.0;
                    e3e4_max = Double.NaN;
                    p1_max = Math.Min(14.0 * t, 200.0);
                    p2_max = p1_max;
                    break;
                case EN1993BoltChecker.ExposureConditionType.NotExposed:
                    e1e2_max = Math.Max(12.0 * t, 150.0);
                    e3e4_max = Double.NaN;
                    p1_max = Math.Min(14.0 * t, 200.0);
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
        /// Options specific for EN1999.
        /// </summary>
        [Serializable]
        public class EN1999BoltOptions : ENCommonBoltChecker.ENCommonBoltOptions, ISerializable
        {
            public EN1999BoltOptions()
            {
            }

            public EN1999BoltOptions(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
                int version = info.GetInt32("EN1999BoltOptionsVersion");

                ShearConnectionsCategory = (ShearConnectionsCategoryType)info.GetValue("ShearConnectionsCategory", typeof(ShearConnectionsCategoryType));
                HoleShape = (HoleShapeType)info.GetValue("HoleShape", typeof(HoleShapeType));
                ClassFrictionSurfaces = (ClassFrictionSurfacesType)info.GetValue("ClassFrictionSurfaces", typeof(ClassFrictionSurfacesType));
                if (version == 2)
                {
                    ExposureCondition = (ExposureConditionType)info.GetValue("ExposureCondition", typeof(ExposureConditionType));
                }
            }

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
                info.AddValue("EN1999BoltOptionsVersion", version);

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
