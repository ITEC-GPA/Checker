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
    public class SectionSolverModelCode2010 : SectionSolver, ISerializable
    {
		#region Properties

		public StandardModelCode2010 StandardModelCode2010 => (StandardModelCode2010)_standard;

        public ConcreteMaterialEuropeanCommon ConcreteMaterialModelCode2010 => (ConcreteMaterialEuropeanCommon)_concreteSection.ConcreteMaterial;

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

		#region Constructor

		public SectionSolverModelCode2010(IConcreteSection section, StandardModelCode2010 standard, bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED)
            : base(section, standard, considerTensileConcrete, id)
        {

        }

		protected SectionSolverModelCode2010(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
		}

		#endregion

		#region Protected Solver Override 

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
                // compressione
                return ConcreteMaterialModelCode2010.CalculateDesignStressConcrete(StandardModelCode2010, strain);            
            else
            {
                // trazione
                if (_considerTensileConcrete)                
                    return ConcreteMaterialModelCode2010.CalculateDesignStressConcrete(StandardModelCode2010, strain);                
                else                
                    return 0;                
            }
        }

        /// <inheritdoc cref="SectionSolver.CalculateStressRebar(ReinforcedConcreteRebar, double)"/>
        internal override double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain)
        {
            return StandardModelCode2010.CalculateDesignStressRebar(rebar, strain);
        }

        protected override double GetReductionFactor(StrainPlane strainPlane)
        {
            return 1.0;
        }

        protected override ForceTuple CalculatePureCompressionReduction(ForceTuple force)
        {
            return force;
        }

        #endregion

        #region Protected Design Concrete 

        protected double CalculateFcd()
        {
            return ConcreteMaterialModelCode2010.CalculateFcd(StandardModelCode2010);
        }

        protected double CalculateFctd()
        {
            return ConcreteMaterialModelCode2010.CalculateFctd(StandardModelCode2010);
        }

        protected double CalculateFcdAccidental()
        {
            return ConcreteMaterialModelCode2010.CalculateFcdAccidental(StandardModelCode2010);
        }

        protected double CalculateFctdAccidental()
        {
            return ConcreteMaterialModelCode2010.CalculateFctdAccidental(StandardModelCode2010);
        }

        protected double CalculateECd()
        {
            return ConcreteMaterialModelCode2010.CalculateECd(StandardModelCode2010);
        }

        #endregion

        #region Protected Design Rebars

        /// <returns>The design rebar yielding stress</returns>
        protected double CalculateFyd(SteelMaterial material)
        {
            return StandardModelCode2010.CalculateFyd(material);
        }

        /// <returns>The design rebar stress related to <paramref name="strain"/></returns>
        protected double CalculateDesignStressRebar(double strain, SteelMaterial material)
        {
            return StandardModelCode2010.CalculateDesignStressRebar(strain, material);
        }

        protected double CalculateUltimateDesignStrainRebar(ReinforcedConcreteRebar rebar)
        {
            return StandardModelCode2010.CalculateUltimateDesignStrainRebar(rebar);
        }

        protected double CalculateUltimateDesignStrainRebar(int rebarId)
        {
            return StandardModelCode2010.CalculateUltimateDesignStrainRebar(ConcreteSection, rebarId);
        }

        protected double CalculateDesignYieldingStressRebar(SteelMaterial material)
        {
            return StandardModelCode2010.CalculateDesignYieldingStressRebar(material);
        }

        protected double CalculateDesignYieldingStrainRebar(SteelMaterial material)
        {
            return StandardModelCode2010.CalculateDesignYieldingStrainRebar(material);
        }

        protected double CalculateDesignUltimateStrainRebar(SteelMaterial material)
        {
            return StandardModelCode2010.CalculateDesignUltimateStrainRebar(material);
        }

        #endregion

        #region Equals, hashcode, operators

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
