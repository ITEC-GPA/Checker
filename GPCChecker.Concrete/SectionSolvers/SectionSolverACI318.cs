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
	public class SectionSolverACI318 : SectionSolver, ISerializable
	{
		#region Properties

		public StandardACI318 StandardACI318 => (StandardACI318)_standard;

		public ConcreteMaterialACI318 ConcreteMaterialACI318 => (ConcreteMaterialACI318)_concreteSection.ConcreteMaterial;

		#endregion

		#region Constructor

		internal SectionSolverACI318(IConcreteSection section, StandardACI318 standard, 
			bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED)
			: base(section, standard, considerTensileConcrete, id)
		{

		}

		protected SectionSolverACI318(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}

		#endregion

        #region Protected Solver Override 

        protected override double GetFck()
        {
            return ConcreteMaterialACI318.Fc;
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
            return ConcreteMaterialACI318.StrainUCompression;
        }

        protected override double GetYieldingStrainConcreteCompression()
        {
            return ConcreteMaterialACI318.StrainYCompression;
        }

        protected override double GetYieldingStrainPureCompression()
        {
            return ConcreteMaterialACI318.StrainUCompression;
        }

        protected override double GetYieldingStrainConcreteTension()
        {
            return ConcreteMaterialACI318.StrainYTension;
        }

        protected override double GetUltimateStrainConcreteTension()
        {
            return ConcreteMaterialACI318.StrainUTension;
        }

        /// <inheritdoc cref="SectionSolver.CalculateSigmaC(double)"/>
        internal override double CalculateSigmaC(double strain)
        {
            if (strain < 0)
                // Compressione
                return ConcreteMaterialACI318.GetStress(strain);
            
            else
            {
                // trazione
                if (_considerTensileConcrete)                
                    return ConcreteMaterialACI318.GetStress(strain);                
                else                
                    return 0;                
            }
        }

        /// <inheritdoc cref="SectionSolver.CalculateStressRebar(ReinforcedConcreteRebar, double)"/>
        internal override double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain)
        {
            return rebar.RebarMaterial.GetStress(strain + rebar.EpsilonP);
        }

        protected override double GetReductionFactor(StrainPlane strainPlane)
        {
            var distances = CalculateMaxMinSectionDistances(strainPlane.Teta);
            double strain = strainPlane.GetStrain(ConcreteSection.GetRebarById(distances.dMinRebarId).Position);

            if (strain < GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)))
                return StandardACI318.PhiCTied;
            else if (strain > GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)) + 
                StandardACI318.PhiDeformationTransitionIncrement)
                return StandardACI318.PhiT;
            else
                return Utilities.Maths.Interpolation.GetLinearInterpolation(
                    GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)),
                    GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)) + 
                    StandardACI318.PhiDeformationTransitionIncrement,
                    StandardACI318.PhiCTied, StandardACI318.PhiT,
                    strain);
        }

        protected override ForceTuple CalculatePureCompressionReduction(ForceTuple force)
        {
            double fyA = 0;
            foreach (ReinforcedConcreteRebar rebar in ConcreteSection.GetRebars())
                fyA += rebar.Area * rebar.RebarMaterial.Fyk;

            double limit = 0.80 * (0.85 * ConcreteMaterialACI318.Fc *
                (ConcreteSection.Area - ConcreteSection.AreaRebars) + fyA);

            if(force.N < limit)
                return new ForceTuple(limit, force.Mx, force.My);
            else
                return force;
        }

        #endregion

        #region Protected Design Rebars

        /// <returns>The design rebar yielding stress</returns>
        protected double CalculateFyd(SteelMaterial material)
        {
            return material.Fyk;
        }

        /// <returns>The design rebar stress related to <paramref name="strain"/></returns>
        protected double CalculateDesignStressRebar(double strain, SteelMaterial material)
        {
            if (strain < CalculateDesignYieldingStrainRebar(material))            
                return material.GetStress(strain);            
            else            
                return CalculateFyd(material) + (strain - CalculateDesignYieldingStrainRebar(material)) * material.Et;            
        }

        protected double CalculateUltimateDesignStrainRebar(ReinforcedConcreteRebar rebar)
        {
            return rebar.RebarMaterial.StrainU;
        }

        protected double CalculateUltimateDesignStrainRebar(int rebarId)
        {
            return ConcreteSection.GetRebarById(rebarId).RebarMaterial.StrainU;
        }

        protected double CalculateDesignYieldingStressRebar(SteelMaterial material)
        {
            return material.Fyk;
        }

        protected double CalculateDesignYieldingStrainRebar(SteelMaterial material)
        {
            return CalculateDesignYieldingStressRebar(material) / material.E;
        }

        protected double CalculateDesignUltimateStrainRebar(SteelMaterial material)
        {
            return material.StrainU;
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
