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

        public StandardModelCode2010 ModelCode2010 => (StandardModelCode2010)_standard;

        public ConcreteMaterialModelCode2010 ConcreteMaterialModelCode2010 => (ConcreteMaterialModelCode2010)_concreteSection.ConcreteMaterial;

        /// <summary>
        /// Design compressive strength for persistent design
        /// </summary>
        public double Fcd => CalculateFcd();

        /// <summary>
        /// Design compressive strength for accidental design
        /// </summary>
        public double FcdAccidental => ModelCode2010.AlphaCC * ConcreteMaterialModelCode2010.Fck / ModelCode2010.GammaCAccidental;

        /// <summary>
        /// Design tensile strength for persistent design
        /// </summary>
        public double Fctd => ModelCode2010.AlphaCT * ConcreteMaterialModelCode2010.Fctk05 / ModelCode2010.GammaC;

        /// <summary>
        /// Design tensile strength for accidental design
        /// </summary>
        public double FctdAccidental => ModelCode2010.AlphaCT * ConcreteMaterialModelCode2010.Fctk05 / ModelCode2010.GammaCAccidental;

        /// <summary>
        /// Modulus of elasticity value for ultimate limit state calculations
        /// </summary>
        public double ECd => ConcreteMaterialModelCode2010.E / ModelCode2010.GammaCE;

        #endregion

        #region Constructors

        public SectionSolverULSModelCode2010(IConcreteSection concreteSection, StandardModelCode2010 standard)
            : base(concreteSection, standard)
        {
            if (concreteSection.ConcreteMaterial is ConcreteMaterialEN1992)
            { }
            else
                throw new ArgumentException("Material must be a ConcreteMaterial");
        }

        #endregion

        protected override double GetFck()
        {
            return ConcreteMaterialModelCode2010.Fck;
        }

        protected override double GetStrainYCompression()
        {
            return ConcreteMaterialModelCode2010.StrainYCompression;
        }

        protected override double GetStrainUCompression()
        {
            return ConcreteMaterialModelCode2010.StrainUCompression;
        }

        protected virtual double CalculateFcd()
        {
            return SectionSolverHelper.CalculateFcd(ConcreteSection, ModelCode2010);
        }

        protected override double CalculateSigmaC(double strain)
        {
            return SectionSolverHelper.CalculateSigmaC(strain, Fcd, ConcreteSection);
        }

        protected override double CalculateStressSteel(ReinforcedConcreteRebar rebar, double strain)
        {
            return SectionSolverHelper.CalculateStressSteel(rebar, strain, ModelCode2010);
        }

        protected override double CalculateUltimateStrainSteel(ReinforcedConcreteRebar rebar)
        {
            return SectionSolverHelper.CalculateUltimateStrainSteel(rebar, ModelCode2010);
        }

        protected override double CalculateUltimateStrainSteel(int rebar)
        {
            return SectionSolverHelper.CalculateUltimateStrainSteel(ConcreteSection, rebar, ModelCode2010);
        }

        protected override double CalculateYeldingStrainSteel(ReinforcedConcreteRebar rebar)
        {
            return SectionSolverHelper.CalculateYeldingStrainSteel(rebar);
        }

        protected override double CalculateYeldingStrainSteel(int rebar)
        {
            return SectionSolverHelper.CalculateYeldingStrainSteel(ConcreteSection, rebar);
        }

        protected override double CalculateUltimateStrainConcreteCompression()
        {
            return SectionSolverHelper.CalculateUltimateStrainConcreteCompression(ConcreteSection);
        }

        protected override double CalculateYeldingStrainConcreteCompression()
        {
            return SectionSolverHelper.CalculateYeldingStrainConcreteCompression(ConcreteSection);
        }

        protected override double CalculateLimitStrainCostantCompression()
        {
            return SectionSolverHelper.CalculateLimitStrainCostantCompression(ModelCode2010);
        }

        protected override double CalculateUltimateStrainConcreteTension()
        {
            return SectionSolverHelper.CalculateUltimateStrainConcreteTension();
        }
    }
}
