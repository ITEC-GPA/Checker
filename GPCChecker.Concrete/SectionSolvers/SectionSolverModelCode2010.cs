using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    [Serializable]
    internal class SectionSolverModelCode2010 : SectionSolver
    {

        public StandardModelCode2010 StandardModelCode2010 => (StandardModelCode2010)_standard;

        public ConcreteMaterialModelCode2010 ConcreteMaterialModelCode2010 => (ConcreteMaterialModelCode2010)_concreteSection.ConcreteMaterial;

        #region Properties

        /// <summary>
        /// Design compressive strength for persistent design
        /// </summary>
        public double Fcd => CalculateFcd();

        /// <summary>
        /// Design tensile strength for persistent design
        /// </summary>
        public double Fctd => CalculateFctd();

        /// <summary>
        /// Design compressive strength for accidental design
        /// </summary>
        public double FcdAccidental => CalculateFcd();

        /// <summary>
        /// Design tensile strength for accidental design
        /// </summary>
        public double FctdAccidental => CalculateFctdAccidental();

        /// <summary>
        /// Modulus of elasticity value for ultimate limit state calculations
        /// </summary>
        public double ECd => CalculateECd();

        #endregion

        internal SectionSolverModelCode2010(IConcreteSection section, StandardModelCode2010 standard, bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED)
            : base(section, standard, considerTensileConcrete, id)
        {

        }

        #region Protected Override 

        protected override double GetFck()
        {
            return ConcreteMaterialModelCode2010.Fck;
        }

        protected override double GetDesignYieldingStrainRebar(ReinforcedConcreteRebar rebar)
        {
            return CalculateDesignYieldingStrainRebar(rebar.RebarMaterial);
        }

        protected override double GetDesignYieldingStrainRebar(int rebarID)
        {
            return CalculateDesignYieldingStrainRebar(ConcreteSection.GetRebarById(rebarID).RebarMaterial);
        }

        protected override double GetDesignUltimateStrainRebar(ReinforcedConcreteRebar rebar)
        {
            return CalculateDesignUltimateStrainRebar(rebar.RebarMaterial);
        }

        protected override double GetDesignUltimateStrainRebar(int rebarID)
        {
            return CalculateDesignUltimateStrainRebar(ConcreteSection.GetRebarById(rebarID).RebarMaterial);
        }

        protected override double GetUltimateStrainConcreteCompression()
        {
            return ConcreteMaterialModelCode2010.StrainUCompression;
        }

        protected override double GetYieldingStrainConcreteCompression()
        {
            return ConcreteMaterialModelCode2010.StrainYCompression;
        }

        protected override double GetYieldingStrainPureCompression()
        {
            return ConcreteMaterialModelCode2010.StrainYPureCompression;
        }

        protected override double GetYieldingStrainConcreteTension()
        {
            return ConcreteMaterialModelCode2010.StrainYTension;
        }

        protected override double GetUltimateStrainConcreteTension()
        {
            return ConcreteMaterialModelCode2010.StrainUTension;
        }

        /// <inheritdoc cref="SectionSolver.CalculateSigmaC(double)"/>
        internal override double CalculateSigmaC(double strain)
        {
            if (strain < 0)
            {
                // compressione
                return ConcreteMaterialModelCode2010.GetStress(strain) * CalculateFcd() / ConcreteMaterialModelCode2010.Fck;
            }
            else
            {
                // trazione
                if (ConsiderTensileConcrete)
                {
                    return ConcreteMaterialModelCode2010.GetStress(strain) * CalculateFctd() / ConcreteMaterialModelCode2010.Fctk;
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <inheritdoc cref="SectionSolver.CalculateStressRebar(ReinforcedConcreteRebar, double)"/>
        internal override double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain)
        {
            double fyd = CalculateDesignYieldingStressRebar(rebar.RebarMaterial);
            double strainYd = CalculateDesignYieldingStrainRebar(rebar.RebarMaterial);

            if (Math.Abs(strain) < strainYd)
                return rebar.RebarMaterial.CalculateStress(strain + rebar.EpsilonP);

            else
            {
                double deltaStress = rebar.RebarMaterial.Fyk - fyd;
                double deltaStrain = deltaStress / rebar.RebarMaterial.E;

                return rebar.RebarMaterial.CalculateStress(strain + Math.Sign(strain) * deltaStrain + rebar.EpsilonP) - Math.Sign(strain) * deltaStress;
            }
        }


        #endregion

        #region Protected Concrete 

        protected double CalculateFcd()
        {
            var material = ConcreteMaterialModelCode2010;
            var standard = StandardModelCode2010;

            if (material.CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.StressBlock)
            {
                if (material.Fck > 90)
                    throw new ArgumentException("Fck > 90 not supported by Stress block");

                double eta;
                if (material.Fck <= 50.0)
                    eta = 1.0;
                else
                    eta = 1.0 - (material.Fck - 50.0) / 200;

                return eta * standard.AlphaCC * material.Fck / standard.GammaC;
            }
            else
            {
                return standard.AlphaCC * material.Fck / standard.GammaC;
            }
        }

        protected double CalculateFctd()
        {
            return StandardModelCode2010.AlphaCT * ConcreteMaterialModelCode2010.Fctk05 / StandardModelCode2010.GammaC;
        }

        protected double CalculateFcdAccidental()
        {
            return StandardModelCode2010.AlphaCC * ConcreteMaterialModelCode2010.Fck / StandardModelCode2010.GammaCAccidental;
        }

        protected double CalculateFctdAccidental()
        {
            return StandardModelCode2010.AlphaCT * ConcreteMaterialModelCode2010.Fctk05 / StandardModelCode2010.GammaCAccidental;
        }

        protected double CalculateECd()
        {
            return ConcreteMaterialModelCode2010.E / StandardModelCode2010.GammaCE;
        }

        #endregion

        #region Protected Rebars

        /// <returns>The design rebar yielding stress</returns>
        protected double CalculateFyd(RebarMaterial material)
        {
            return material.Fyk / StandardModelCode2010.GammaS;
        }

        /// <returns>The design rebar stress related to <paramref name="strain"/></returns>
        protected double CalculateDesignStressRebar(double strain, RebarMaterial material)
        {
            if (strain < CalculateDesignYieldingStrainRebar(material))
            {
                return material.CalculateStress(strain);
            }
            else
            {
                return CalculateFyd(material) + (strain - CalculateDesignYieldingStrainRebar(material)) * material.Et;
            }
        }

        protected double CalculateUltimateDesignStrainRebar(ReinforcedConcreteRebar rebar)
        {
            return rebar.RebarMaterial.StrainU * StandardModelCode2010.SteelCoefficientStrainTension;
        }

        protected double CalculateUltimateDesignStrainRebar(int rebarId)
        {
            return ConcreteSection.GetRebarById(rebarId).RebarMaterial.StrainU * StandardModelCode2010.SteelCoefficientStrainTension;
        }

        protected double CalculateDesignYieldingStressRebar(RebarMaterial material)
        {
            return material.Fyk / StandardModelCode2010.GammaS;
        }

        protected double CalculateDesignYieldingStrainRebar(RebarMaterial material)
        {
            return CalculateDesignYieldingStressRebar(material) / material.E;
        }

        protected double CalculateDesignUltimateStrainRebar(RebarMaterial material)
        {
            return material.StrainU * StandardModelCode2010.SteelCoefficientStrainTension;
        }

        #endregion

    }
}
