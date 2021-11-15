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
        public double Fcd => ModelCode2010Helper.CalculateFcd(ConcreteMaterialModelCode2010, StandardModelCode2010);

        /// <summary>
        /// Design tensile strength for persistent design
        /// </summary>
        public double Fctd => ModelCode2010Helper.CalculateFctd(ConcreteMaterialModelCode2010, StandardModelCode2010);

        /// <summary>
        /// Design compressive strength for accidental design
        /// </summary>
        public double FcdAccidental => ModelCode2010Helper.CalculateFcd(ConcreteMaterialModelCode2010, StandardModelCode2010);

        /// <summary>
        /// Design tensile strength for accidental design
        /// </summary>
        public double FctdAccidental => ModelCode2010Helper.CalculateFctdAccidental(ConcreteMaterialModelCode2010, StandardModelCode2010);

        /// <summary>
        /// Modulus of elasticity value for ultimate limit state calculations
        /// </summary>
        public double ECd => ModelCode2010Helper.CalculateECd(ConcreteMaterialModelCode2010, StandardModelCode2010);

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

        protected override double GetDesignYieldingStrainRebar(ReinforcedConcreteRebar rebar)
        {
            return ModelCode2010Helper.CalculateDesignYieldingStrainRebar(rebar.RebarMaterial, StandardModelCode2010);
        }

        protected override double GetDesignYieldingStrainRebar(int rebar)
        {
            return ModelCode2010Helper.CalculateDesignYieldingStrainRebar(ConcreteSection.Rebars[rebar].RebarMaterial, StandardModelCode2010);
        }

        protected override double GetDesignUltimateStrainRebar(ReinforcedConcreteRebar rebar)
        {
            return ModelCode2010Helper.CalculateDesignUltimateStrainRebar(rebar.RebarMaterial, StandardModelCode2010);
        }

        protected override double GetDesignUltimateStrainRebar(int rebar)
        {
            return ModelCode2010Helper.CalculateDesignUltimateStrainRebar(ConcreteSection.Rebars[rebar].RebarMaterial, StandardModelCode2010);
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
        protected override double CalculateSigmaC(double strain)
        {
            return ModelCode2010Helper.CalculateSigmaC(strain, Fcd, Fctd, ConcreteMaterialModelCode2010);
        }

        /// <inheritdoc cref="SectionSolver.CalculateStressRebar(ReinforcedConcreteRebar, double)"/>
        protected override double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain)
        {
            return rebar.RebarMaterial.CalculateStress(strain + rebar.EpsilonP);
        }


        #endregion


    }
}
