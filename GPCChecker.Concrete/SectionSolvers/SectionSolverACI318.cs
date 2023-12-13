using GPC.Checker.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using GPC.Checkers.Concrete.Checkers;

[assembly: InternalsVisibleTo("GPCChecker.Test.Concrete")]
namespace GPC.Checkers.Concrete.SectionSolvers
{
    [Serializable]
    public class SectionSolverACI318 : SectionSolver, ISerializable
    {
        protected bool _haveSpiral;

        #region Properties

        public StandardACI318 StandardACI318 => (StandardACI318)_standard;

        public ConcreteMaterialACI318 ConcreteMaterialACI318 => (ConcreteMaterialACI318)_concreteSection.ConcreteMaterial;

        public bool HaveSpiral { get => _haveSpiral; set => _haveSpiral = value; }

        #endregion

        #region Constructor

        internal SectionSolverACI318(IConcreteSection section, SectionChecker.SectionOptions sectionOption, StandardACI318 standard, bool haveSpiral, Point2d integrationReferencePoint, bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED, Standard standardStructuralSteel = null)
            : base(section, standard, considerTensileConcrete, id, integrationReferencePoint, standardStructuralSteel, sectionOption)
        {
            _haveSpiral = haveSpiral;
        }

        protected SectionSolverACI318(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        #endregion

        #region Protected Solver Override 

        protected override double GetFck()
        {
            if (ConcreteMaterial is ConcreteMaterialEuropeanCommon concreteMaterialEuropeanCommon)
                return concreteMaterialEuropeanCommon.Fck;
            else if (ConcreteMaterial is ConcreteMaterialACI318 concreteMaterialACI318)
                return concreteMaterialACI318.Fc;
            return 0;
        }

        protected override double GetUltimateStrainConcreteCompression()
        {
            return ConcreteMaterial.StrainUCompression;
        }

        protected override double GetYieldingStrainConcreteCompression()
        {
            return ConcreteMaterial.StrainYCompression;
        }

        protected override double GetYieldingStrainPureCompression()
        {
            if (ConcreteMaterial is ConcreteMaterialACI318)
                return ConcreteMaterial.StrainUCompression;
            else if (ConcreteMaterial is ConcreteMaterialEuropeanCommon cm)
                return cm.StrainYPureCompression;
            return 0;
        }

        protected override double GetYieldingStrainConcreteTension()
        {
            return ConcreteMaterial.StrainYTension;
        }

        protected override double GetUltimateStrainConcreteTension()
        {
            return ConcreteMaterial.StrainUTension;
        }

        /// <inheritdoc cref="SectionSolver.CalculateSigmaC(double)"/>
        internal override double CalculateSigmaC(double strain)
        {
            if (strain < 0)
                // Compressione
                return ConcreteMaterial.CalculateDesignStressConcrete(StandardACI318, strain);

            else
            {
                // trazione
                if (_considerTensileConcrete)
                    return ConcreteMaterial.CalculateDesignStressConcrete(StandardACI318, strain);
                else
                    return 0;
            }
        }

        /// <inheritdoc cref="SectionSolver.CalculateStressRebar(ReinforcedConcreteRebar, double)"/>
        internal override double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain)
        {
            return rebar.RebarMaterial.GetStress(strain + rebar.EpsilonP);
        }

        /// <inheritdoc cref="SectionSolver.CalculateStressRebar(ReinforcedConcreteRebar, double)"/>
        internal override double CalculateStressRebar(SteelMaterial steelMaterial, double strain, double epsilonP)
        {
            return steelMaterial.GetStress(strain + epsilonP);
        }

        internal override double CalculateStressStructuralSteel(ISteelSection steelSection, double strain)
        {
            return steelSection.SteelMaterial.GetStress(strain);
        }

        protected override double GetReductionFactor(StrainPlane strainPlane)
        {
            var distances = CalculateMaxMinSectionDistances(strainPlane.Teta);
            double strain = strainPlane.GetStrain(ConcreteSection.GetRebarById(distances.dMinRebarId).Position);

            var designYeldingStrain = GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId));

            if (ConcreteSection.IsCompositeSteelConcrete)
            {
                double steelSectionStrain = strainPlane.GetStrain(distances.dMinStrucSteelVertex);

                if (steelSectionStrain > strain)
                {
                    designYeldingStrain = GetDesignYieldingStrainStructuralSteel(ConcreteSection.SteelSections[distances.dminStrucSteelSectionID].Section);
                    strain = steelSectionStrain;
                }
            }

            if (_haveSpiral)
            {
                if (strain < designYeldingStrain)
                    return StandardACI318.PhiCSpiral;
                else if (strain > designYeldingStrain +
                    StandardACI318.PhiDeformationTransitionIncrement)
                    return StandardACI318.PhiT;
                else
                    return Utilities.Maths.Interpolation.GetLinearInterpolation(
                        designYeldingStrain,
                        designYeldingStrain + StandardACI318.PhiDeformationTransitionIncrement,
                        StandardACI318.PhiCSpiral, StandardACI318.PhiT,
                        strain);
            }
            else
            {
                if (strain < designYeldingStrain)
                    return StandardACI318.PhiCTied;
                else if (strain > designYeldingStrain +
                    StandardACI318.PhiDeformationTransitionIncrement)
                    return StandardACI318.PhiT;
                else
                    return Utilities.Maths.Interpolation.GetLinearInterpolation(
                        designYeldingStrain,
                        designYeldingStrain + StandardACI318.PhiDeformationTransitionIncrement,
                        StandardACI318.PhiCTied, StandardACI318.PhiT, strain);
            }
        }

        protected override double CalculateCompressionAxialForceLimit()
        {
            double fyA = 0;
            ReinforcedConcreteRebar[] rebars = ConcreteSection.GetRebars();
            for (int i = 0; i < rebars.Length; i++)
                fyA += rebars[i].Area * Math.Min(rebars[i].RebarMaterial.Fyk, 551.579);

            double fyStructuralSteel = 0;
            if (ConcreteSection.IsCompositeSteelConcrete)
            {
                for (int i = 0; i < ConcreteSection.SteelSections.Count; i++)
                    fyStructuralSteel += ConcreteSection.SteelSections[i].Section.Area * Math.Min(ConcreteSection.SteelSections[i].Section.SteelMaterial.Fyk, 551.579);
            }

            double limit;
            double fc = 0;

            if (ConcreteMaterial is ConcreteMaterialACI318)
                fc = Math.Abs(ConcreteMaterialACI318.Fc);
            else if (ConcreteMaterial is ConcreteMaterialEuropeanCommon ec)
                fc = Math.Abs(ec.Fck);

            // Coefficients in Table 22.4.2.1 - Maximum axial strength.
            double maximumAxialStrengthCoefficient;
            if (ConcreteSection.IsCompositeSteelConcrete)
                maximumAxialStrengthCoefficient = StandardACI318.PhiMaximumCompressiveAxialLoadComposite; // 0.85
            else if (_haveSpiral)
                maximumAxialStrengthCoefficient = StandardACI318.PhiMaximumCompressiveAxialLoadSpiral; // 0.85
            else
                maximumAxialStrengthCoefficient = StandardACI318.PhiMaximumCompressiveAxialLoadTied; // 0.8

            // Coefficients for conpression controlled rupture.
            double compressionControlledCoefficient;
            if (_haveSpiral)
                compressionControlledCoefficient = StandardACI318.PhiCSpiral;
            else
                compressionControlledCoefficient = StandardACI318.PhiCTied;

            limit = -compressionControlledCoefficient * (maximumAxialStrengthCoefficient * (StandardACI318.ConcreteStrengthReductionFactor * fc *
                (ConcreteSection.Area - ConcreteSection.AreaRebars) + fyA + fyStructuralSteel));

            return limit;
        }

        #endregion

        #region Protected Design Rebars

        /// <returns>The design rebar stress related to <paramref name="strain"/></returns>
        protected double CalculateDesignStressRebar(double strain, SteelMaterial material)
        {
            return material.CalculateDesignStress(StandardACI318, strain);
        }

        protected double CalculateUltimateDesignStrainRebar(ReinforcedConcreteRebar rebar)
        {
            return rebar.RebarMaterial.StrainUTension;
        }

        protected double CalculateUltimateDesignStrainRebar(int rebarId)
        {
            return ConcreteSection.GetRebarById(rebarId).RebarMaterial.StrainUTension;
        }

        protected double CalculateDesignYieldingStressRebar(SteelMaterial material)
        {
            return material.Fyk;
        }

        protected override double CalculateDesignYieldingStrainTensionRebar(SteelMaterial material)
        {
            return CalculateDesignYieldingStressRebar(material) / material.E;
        }

        protected override double CalculateDesignUltimateStrainTensionRebar(SteelMaterial material)
        {
            return material.StrainUTension;
        }

        protected override double CalculateDesignYieldingStrainCompressionRebar(SteelMaterial material)
        {
            return -CalculateDesignYieldingStressRebar(material) / material.E;
        }

        protected override double CalculateDesignUltimateStrainCompressionRebar(SteelMaterial material)
        {
            return material.StrainUCompression;
        }

        protected override double CalculateDesignYieldingStrainTensionStructuralSteel(SteelMaterial material)
        {
            return material.Fyk / material.E;
        }

        protected override double CalculateDesignUltimateStrainTensionStructuralSteel(SteelMaterial material)
        {
            return material.StrainUTension;
        }

        protected override double CalculateDesignYieldingStrainCompressionStructuralSteel(SteelMaterial material)
        {
            return -CalculateDesignYieldingStrainTensionStructuralSteel(material);
        }

        protected override double CalculateDesignUltimateStrainCompressionStructuralSteel(SteelMaterial material)
        {
            return material.StrainUCompression;
        }

        #endregion

        #region Equals hashcode operators

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        #endregion
    }
}
