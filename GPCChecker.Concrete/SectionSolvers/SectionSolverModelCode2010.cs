using GPC.Checkers.Concrete.Results;
using GPC.Model;
using GPC.Model.Elements;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

[assembly: InternalsVisibleTo("GPCChecker.Test.Concrete")]
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

		public SectionSolverModelCode2010(IConcreteSection section, StandardModelCode2010 standard, bool considerTensileConcrete = false,
			int id = ModelObjectId.IDUNASSIGNED, StandardEN1993p11 standardStructuralSteel = null)
			: base(section, standard, considerTensileConcrete, id, standardStructuralSteel)
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
			if (ConcreteMaterial is ConcreteMaterialEuropeanCommon concreteMaterialEuropeanCommon)
				return concreteMaterialEuropeanCommon.Fck;
			else if (ConcreteMaterial is ConcreteMaterialACI318 concreteMaterialACI318)
				return concreteMaterialACI318.Fc;
			return 0;
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

        protected override double GetDesignYieldingStrainStructuralSteel(ISteelSection steelSection)
        {
            return CalculateDesignYieldingStrainStructuralSteel(steelSection.SteelMaterial);
        }

        protected override double GetDesignUltimateStrainStructuralSteel(ISteelSection steelSection)
        {
            return CalculateDesignUltimateStrainStructuralSteel(steelSection.SteelMaterial);
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
			else if (ConcreteMaterial is ConcreteMaterialEuropeanCommon)
				return ConcreteMaterialModelCode2010.StrainYPureCompression;
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
				// compressione
				return ConcreteMaterial.CalculateDesignStressConcrete(StandardModelCode2010, strain);
			else
			{
				// trazione
				if (_considerTensileConcrete)
					return ConcreteMaterial.CalculateDesignStressConcrete(StandardModelCode2010, strain);
				else
					return 0;
			}
		}

		/// <inheritdoc cref="SectionSolver.CalculateStressRebar(ReinforcedConcreteRebar, double)"/>
		internal override double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain)
		{
			return rebar.RebarMaterial.CalculateDesignStress(StandardModelCode2010, strain + rebar.EpsilonP);
        }

        internal override double CalculateStressStructuralSteel(ISteelSection steelSection, double strain)
        {
			return steelSection.SteelMaterial.CalculateDesignStress(StandardModelCode2010, strain);
        }

        protected override double GetReductionFactor(StrainPlane strainPlane)
		{
			return 1.0;
		}

		protected override double CalculateCompressionAxialForceLimit()
		{
			return double.MinValue;
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

		/// <returns>The design rebar stress related to <paramref name="strain"/></returns>
		protected double CalculateDesignStressRebar(double strain, SteelMaterial material)
		{
			return material.CalculateDesignStress(StandardModelCode2010, strain);
		}

		protected double CalculateUltimateDesignStrainRebar(ReinforcedConcreteRebar rebar)
		{
			return rebar.RebarMaterial.CalculateDesignUltimateStrain(StandardModelCode2010);
		}

		protected double CalculateUltimateDesignStrainRebar(int rebarId)
		{
			return _concreteSection.GetRebarById(rebarId).RebarMaterial.CalculateDesignUltimateStrain(StandardModelCode2010);
		}

		protected double CalculateDesignYieldingStressRebar(SteelMaterial material)
		{
			return material.CalculateDesignYieldingStressTension(StandardModelCode2010);
		}

		protected double CalculateDesignYieldingStrainRebar(SteelMaterial material)
		{
			return material.CalculateDesignYieldingStrainTension(StandardModelCode2010);
		}

		protected double CalculateDesignUltimateStrainRebar(SteelMaterial material)
		{
			return material.CalculateDesignUltimateStrain(StandardModelCode2010);
        }

        protected double CalculateDesignYieldingStrainStructuralSteel(SteelMaterial material)
        {
            return material.CalculateDesignYieldingStrainTension(StandardStructuralSteel as StandardEN1993p11);
        }

        protected double CalculateDesignUltimateStrainStructuralSteel(SteelMaterial material)
        {
            return material.CalculateDesignUltimateStrain(StandardStructuralSteel as StandardEN1993p11);
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
