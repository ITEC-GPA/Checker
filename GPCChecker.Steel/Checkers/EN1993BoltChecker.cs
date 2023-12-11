using GPC.Checkers.Steel.Results;
using GPC.Model.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Checkers
{
    public class EN1993BoltChecker : ENCommonBoltChecker
    {
        #region Enum

        // Keep enums in this class for compatibility in serialization.

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
        /// UNI EN 1999-1-1:2023 - 10.5.3.1 Shear connestions.
        /// </summary>
        public enum ShearConnectionsCategoryType
        {
            A, // Category A: Bearing type.
            B, // Category B: Slip-resistant at serviceability limit state.
            C  // Category C: Slip-resistant at ultimate limit state.
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - 3.4.2 Tension connections.
        /// UNI EN 1999-1-1:2023 - 10.5.3.2 Tension connestions.
        /// </summary>
        public enum TensionConnectionsCategoryType
        {
            D, // Category D: non-preloaded.
            E  // Category E: preloaded.
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.3: Minimum and maximum spacing, end and edge distances.
        /// UNI EN 1999-1-1:2023 - Table 10.1: Minimum, regular and maximum spacing, end and edge distances.
        /// </summary>
        public enum ExposureConditionType
        {
            Exposed, // Steel/Aluminium exposed to the weather or other corrosive influences.
            NotExposed, // Steel/Aluminium not exposed to the weather or other corrosive influences.
            Unprotected // Steel used unprotected. Not managed by Aluminium.
        }

        #endregion

        #region Constructor

        public EN1993BoltChecker(PlateWithBolts plateWithBolts, List<BoltStresses> boltStresses, StandardEN1993p11 standard, EN1993BoltOptions options, int id = IDUNASSIGNED, string name = "")
            : base(plateWithBolts, boltStresses, standard, options, id, name)
        { }

        #endregion

        #region Properties

        public StandardEN1993p11 StandardEN1993 => (StandardEN1993p11)_standard;

        public EN1993BoltOptions OptionsEN1993 => (EN1993BoltOptions)_options;

        public List<EN1993BoltResults> BoltResultsEN1993 => _boltResults.Cast<EN1993BoltResults>().ToList();

        public override ENCommonBoltResults BoltResultMax => (EN1993BoltResults)_boltResultMax;

        protected override double PlateMaterialFu => ((SteelMaterial)_plateWithBolts.PlateMaterial).Fu;

        /// <summary>
        /// For steel this check is not done.
        /// </summary>
        public override bool IsLessThanMaximumThickness => true;

        protected override double EnGammaM2 => StandardEN1993.GammaM2;

        protected override double EnGammaM3 => StandardEN1993.GammaM3;

        protected override double EnGammaM3Ser => StandardEN1993.GammaM3Ser;

        #endregion

        #region Protected methods

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
        /// Non è specificato nella EN 1993.
        /// </summary>
        /// <param name="holeShape"></param>
        /// <returns></returns>
        protected override double CalculateShearCoeff_kh(in HoleShapeType holeShape)
        {
            return 1.0;
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4.
        /// </summary>
        /// <returns>α_b</returns>
        protected override double CalculateCoeffParallel_AlphaB(in double alpha_d, in BoltSection boltSection, in double e_1, in double p_1, in double d_0, in EN1993BoltChecker.HoleShapeType holeShape)
        {
            return Math.Min(Math.Min(alpha_d, boltSection.BoltMaterial.Fu / PlateMaterialFu), 1.0);
        }

        /// <summary>
        /// UNI EN 1993-1-8:2005 - Table 3.4, in footer point 1).
        /// </summary>
        /// <param name="holeShape"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        protected override double CalculateBearingCoeff_kh(in HoleShapeType holeShape)
        {
            if (holeShape == HoleShapeType.ShortSlotted || holeShape == HoleShapeType.LongSlotted)
                return 0.6;
            else if (holeShape == HoleShapeType.OversizeRound)
                return 0.8;
            else
                return 1.0;
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
