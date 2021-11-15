using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    public class SectionSolverULSModelCode2010 : SectionSolverULS
    {

        #region Properties

        public StandardModelCode2010 StandardModelCode2010 => (StandardModelCode2010)_standard;

        public ConcreteMaterialModelCode2010 ConcreteMaterialModelCode2010 => (ConcreteMaterialModelCode2010)_concreteSection.ConcreteMaterial;

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
        public double FcdAccidental => StandardModelCode2010.AlphaCC * ConcreteMaterialModelCode2010.Fck / StandardModelCode2010.GammaCAccidental;

        /// <summary>
        /// Design tensile strength for accidental design
        /// </summary>
        public double FctdAccidental => StandardModelCode2010.AlphaCT * ConcreteMaterialModelCode2010.Fctk05 / StandardModelCode2010.GammaCAccidental;

        /// <summary>
        /// Modulus of elasticity value for ultimate limit state calculations
        /// </summary>
        public double ECd => ConcreteMaterialModelCode2010.E / StandardModelCode2010.GammaCE;

        #endregion

        #region Constructors

        public SectionSolverULSModelCode2010(IConcreteSection concreteSection, StandardModelCode2010 standard)
            : base(concreteSection, standard)
        {
            if (!(concreteSection.ConcreteMaterial is ConcreteMaterialModelCode2010))
                throw new ArgumentException("Material must be a ConcreteMaterialModelCode2010");

        }

        #endregion


        #region protected Override 

        protected override double GetFck()
        {
            return ConcreteMaterialModelCode2010.Fck;
        }

        protected override double GetDesignYieldingStrainSteel(ReinforcedConcreteRebar rebar)
        {
            return rebar.RebarMaterial.Fyk / StandardModelCode2010.GammaS;
        }

        protected override double GetDesignYieldingStrainSteel(int rebar)
        {
            return ConcreteSection.Rebars[rebar].RebarMaterial.Fyk / StandardModelCode2010.GammaS;
        }

        protected override double GetDesignUltimateStrainSteel(ReinforcedConcreteRebar rebar)
        {
            return rebar.RebarMaterial.StrainU / StandardModelCode2010.SteelCoefficientStrainTension;
        }

        protected override double GetDesignUltimateStrainSteel(int rebar)
        {
            return ConcreteSection.Rebars[rebar].RebarMaterial.StrainU / StandardModelCode2010.SteelCoefficientStrainTension;
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

        protected override double CalculateSigmaC(double strain)
        {
            return SectionSolverHelper.CalculateSigmaC(strain, Fcd, Fctd, ConcreteSection);
        }

        protected override double CalculateStressSteel(ReinforcedConcreteRebar rebar, double strain)
        {
            return rebar.RebarMaterial.CalculateStress(strain + rebar.EpsilonP);
        }


        #endregion


        #region Protected 

        protected virtual double CalculateFcd()
        {
            var material = ConcreteMaterialModelCode2010;

            if (material.CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.StressBlock)
            {
                if (material.Fck > 90)
                    throw new ArgumentException("Fck > 90 not supported by Stress block");

                double eta;
                if (material.Fck <= 50.0)
                    eta = 1.0;
                else
                    eta = 1.0 - (material.Fck - 50.0) / 200;

                return eta * StandardModelCode2010.AlphaCC * material.Fck / StandardModelCode2010.GammaC;
            }
            else
            {
                return StandardModelCode2010.AlphaCC * material.Fck / StandardModelCode2010.GammaC;
            }
        }

        protected virtual double CalculateFctd()
        {
            return StandardModelCode2010.AlphaCT * ConcreteMaterialModelCode2010.Fctk05 / StandardModelCode2010.GammaC;
        }

        #endregion
    }
}
