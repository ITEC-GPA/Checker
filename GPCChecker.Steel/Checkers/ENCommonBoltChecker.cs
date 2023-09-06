using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Checkers
{
    /// <summary>
    /// The purpose of this class is collect common methods for EN1993 and EN1999.
    /// </summary>
    [Serializable]
    public abstract class ENCommonBoltChecker : BoltChecker
    {
        #region Enum

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

        #endregion

        #region Constructor

        protected ENCommonBoltChecker(PlateWithBolts plateWithBolts, List<BoltStresses> boltStresses, Standard standard, BoltOptions options, int id, string name = "")
            : base(plateWithBolts, boltStresses, standard, options, id, name)
        {
        }

        #endregion

        #region Private properties

        private ENCommonBoltOptions OptionsENCommon => (ENCommonBoltOptions)_options;

        #endregion

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
            return alpha_v * f_ub * area / EnGammaM2;
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
        protected abstract double CalculateCoeffParallel_AlphaB(in double alpha_d, in BoltSection boltSection, in PlateWithBolts plateWithBolts, in double e_1, in double p_1, in double d_0);

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
        protected double CalculateBearingResistance_FbRd(in double k_1, in double alpha_b, in BoltSection boltSection, in PlateWithBolts plateWithBolts)
        {
            return k_1 * alpha_b * plateWithBolts.PlateMaterial.Fu * boltSection.Diameter * plateWithBolts.Thickness / EnGammaM2;
        }

        /// <summary>
        /// Calculate punching shear resistance.
        /// </summary>
        /// <param name="plateWithBolts">Plate.</param>
        /// <returns>B_p,Rd</returns>
        protected abstract double CalculatePunchingShearResistance_BpRd(in PlateWithBolts plateWithBolts, in double d_m, in double d_0);

        /// <summary>
        /// Values of ks.<br/>
        /// UNI EN 1993-1-8:2005 - Table 3.6.<br/>
        /// UN EN 1999-1-1:2023 - Table 10.4.
        /// </summary>
        /// <param name="holeShape"></param>
        /// <param name="slotPerpendicularLoad">True if load is perpendicuar to the load.</param>
        /// <returns>k_s</returns>
        protected double Calculate_ks(in HoleShapeType holeShape, in bool slotPerpendicularLoad)
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
                case ClassFrictionSurfacesType.A:
                    return 0.5;
                case ClassFrictionSurfacesType.B:
                    return 0.4;
                case ClassFrictionSurfacesType.C:
                    return 0.3;
                case ClassFrictionSurfacesType.D:
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
        protected double CalculateNominalClearance(in double nominalBoltDiameter, in HoleShapeType holeShape)
        {
            double holeTolerance = 0.01;

            switch (holeShape)
            {
                case HoleShapeType.NormalRound:
                    if (nominalBoltDiameter < 12.0 + holeTolerance)
                        return 1.0;
                    else if (nominalBoltDiameter < 24.0 + holeTolerance)
                        return 2.0; // For bolt nominal diameter equal to 14 can be 2mm according to 3.6.1.(5) EC 1993-1-8.
                    else
                        return 3.0;

                case HoleShapeType.OversizeRound:
                    if (nominalBoltDiameter < 14.0 + holeTolerance)
                        return 3.0;
                    else if (nominalBoltDiameter < 22.0 + holeTolerance)
                        return 4.0;
                    else if (nominalBoltDiameter < 24.0 + holeTolerance)
                        return 6.0;
                    else
                        return 8.0;

                case HoleShapeType.ShortSlotted:
                    if (nominalBoltDiameter < 14.0 + holeTolerance)
                        return 4.0;
                    else if (nominalBoltDiameter < 22.0 + holeTolerance)
                        return 6.0;
                    else if (nominalBoltDiameter < 24.0 + holeTolerance)
                        return 8.0;
                    else
                        return 10.0;

                case HoleShapeType.LongSlotted:
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
        protected HoleShapeType CalculateHoleType(in BoltPosition boltpos)
        {
            double holeTolerance = 0.01;
            double clearance = boltpos.Hole.MaxLength - boltpos.BoltDef.Diameter;

            if (!boltpos.Hole.IsSlotted)
            {
                double nominnalClearanceNormal = CalculateNominalClearance(boltpos.BoltDef.Diameter, HoleShapeType.NormalRound);

                if (clearance < nominnalClearanceNormal + holeTolerance)
                    return HoleShapeType.NormalRound;
                else
                    return HoleShapeType.OversizeRound;
            }
            else
            {
                double nominnalClearanceShort = CalculateNominalClearance(boltpos.BoltDef.Diameter, HoleShapeType.ShortSlotted);

                if (clearance < nominnalClearanceShort + holeTolerance)
                    return HoleShapeType.ShortSlotted;
                else
                    return HoleShapeType.LongSlotted;
            }
        }

        /// <summary>
        /// Set hole diameter and slot legth from bolt nominal diameter.
        /// </summary>
        /// <param name="boltpos">Hole to change.</param>
        /// <param name="holeShape">Required hole shape.</param>
        protected void SetHoleDiameter(BoltPosition boltpos, in HoleShapeType holeShape)
        {
            double boltNominalDiameter = boltpos.BoltDef.Diameter;

            switch (holeShape)
            {
                case HoleShapeType.OversizeRound:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.OversizeRound);
                    boltpos.Hole.SlotLength = 0.0;
                    break;
                case HoleShapeType.ShortSlotted:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.NormalRound);
                    boltpos.Hole.SlotLength = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.ShortSlotted) - boltpos.Hole.Diameter;
                    break;
                case HoleShapeType.LongSlotted:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.NormalRound);
                    boltpos.Hole.SlotLength = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.LongSlotted) - boltpos.Hole.Diameter;
                    break;
                case HoleShapeType.NormalRound:
                    boltpos.Hole.Diameter = boltNominalDiameter + CalculateNominalClearance(boltNominalDiameter, HoleShapeType.NormalRound);
                    boltpos.Hole.SlotLength = 0.0;
                    break;
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

        #region Nested Class Options

        /// <summary>
        /// Common options between EN1993 and EN1999.
        /// </summary>
        [Serializable]
        public abstract class ENCommonBoltOptions : BoltOptions, ISerializable
        {
            #region Fields

            protected ShearConnectionsCategoryType _shearConnectionsCategory;

            protected TensionConnectionsCategoryType _tensionConnectionsCategory;

            #endregion

            #region Properties

            /// <summary>
            /// UNI EN 1993-1-8:2005 - Table 3.6 and EN 1090-2 Table 11.
            /// Influence on bearing resistance Fb,Rd and tollerances.
            /// </summary>
            public HoleShapeType HoleShape { get; set; }

            /// <summary>
            /// Class of friction surfaces.
            /// </summary>
            public ClassFrictionSurfacesType ClassFrictionSurfaces { get; set; }

            /// <summary>
            /// Shear connection category.
            /// </summary>
            public ShearConnectionsCategoryType ShearConnectionsCategory
            {
                get => _shearConnectionsCategory;
                set
                {
                    _shearConnectionsCategory = value;
                    switch (value)
                    {
                        case ShearConnectionsCategoryType.A:
                            _tensionConnectionsCategory = TensionConnectionsCategoryType.D;
                            break;
                        case ShearConnectionsCategoryType.B:
                        case ShearConnectionsCategoryType.C:
                            _tensionConnectionsCategory = TensionConnectionsCategoryType.E;
                            break;
                    }
                }
            }

            /// <summary>
            /// Tension connection category.
            /// </summary>
            public TensionConnectionsCategoryType TensionConnectionsCategory => _tensionConnectionsCategory;

            #endregion

            #region Constructor

            protected ENCommonBoltOptions()
            {
                HoleShape = HoleShapeType.NormalRound;
                ClassFrictionSurfaces = ClassFrictionSurfacesType.D;
                ShearConnectionsCategory = ShearConnectionsCategoryType.A;
            }

            protected ENCommonBoltOptions(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
                ShearConnectionsCategory = (ShearConnectionsCategoryType)info.GetValue("ShearConnectionsCategory", typeof(ShearConnectionsCategoryType));
                HoleShape = (HoleShapeType)info.GetValue("HoleShape", typeof(HoleShapeType));
                ClassFrictionSurfaces = (ClassFrictionSurfacesType)info.GetValue("ClassFrictionSurfaces", typeof(ClassFrictionSurfacesType));
            }

            #endregion

            #region Methods

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);

                info.AddValue("ShearConnectionsCategory", _shearConnectionsCategory, typeof(ShearConnectionsCategoryType));
                info.AddValue("HoleShape", HoleShape, typeof(HoleShapeType));
                info.AddValue("ClassFrictionSurfaces", ClassFrictionSurfaces, typeof(ClassFrictionSurfacesType));
            }

            #endregion
        }

        #endregion

    }
}
