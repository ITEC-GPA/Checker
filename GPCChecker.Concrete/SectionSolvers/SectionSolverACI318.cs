using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Model;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.Concrete.SectionSolvers
{
	[Serializable]
	public class SectionSolverACI318 : SectionSolver, ISerializable
	{
		protected bool _haveSpiral;

		#region Properties

		public StandardACI318 StandardACI318 => (StandardACI318)_standard;

		public ConcreteMaterialACI318 ConcreteMaterialACI318 => (ConcreteMaterialACI318)_concreteSection.ConcreteMaterial;

		#endregion

		#region Constructor

		internal SectionSolverACI318(IConcreteSection section, StandardACI318 standard, bool haveSpiral,
			bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED)
			: base(section, standard, considerTensileConcrete, id)
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
				return ConcreteMaterialACI318.CalculateDesignStressConcrete(StandardACI318, strain);

			else
			{
				// trazione
				if (_considerTensileConcrete)
					return ConcreteMaterialACI318.CalculateDesignStressConcrete(StandardACI318, strain);
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

			if (_haveSpiral)
			{
				if (strain < GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)))
					return StandardACI318.PhiCSpiral;
				else if (strain > GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)) +
					StandardACI318.PhiDeformationTransitionIncrement)
					return StandardACI318.PhiT;
				else
					return Utilities.Maths.Interpolation.GetLinearInterpolation(
						GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)),
						GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)) + StandardACI318.PhiDeformationTransitionIncrement,
						StandardACI318.PhiCSpiral, StandardACI318.PhiT,
						strain);
			}
			else
			{
				if (strain < GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)))
					return StandardACI318.PhiCTied;
				else if (strain > GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)) +
					StandardACI318.PhiDeformationTransitionIncrement)
					return StandardACI318.PhiT;
				else
					return Utilities.Maths.Interpolation.GetLinearInterpolation(
						GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)),
						GetDesignYieldingStrainRebar(ConcreteSection.GetRebarById(distances.dMinRebarId)) + StandardACI318.PhiDeformationTransitionIncrement,
						StandardACI318.PhiCTied, StandardACI318.PhiT, strain);
			}
		}

		protected override ForceTuple CalculatePureCompressionReduction(ForceTuple force)
		{
			double fyA = 0;
			ReinforcedConcreteRebar[] rebars = ConcreteSection.GetRebars();
			for (int i = 0; i < rebars.Length; i++)
			{
				fyA += rebars[i].Area * rebars[i].RebarMaterial.Fyk;
			}

			double limit;
			if (_haveSpiral)
				limit = StandardACI318.PhiMaximumCompressiveAxialLoadSpiral * (StandardACI318.ConcreteStrengthReductionFactor * ConcreteMaterialACI318.Fc *
					(ConcreteSection.Area - ConcreteSection.AreaRebars) + fyA);
			else
				limit = StandardACI318.PhiMaximumCompressiveAxialLoadTied * (StandardACI318.ConcreteStrengthReductionFactor * ConcreteMaterialACI318.Fc *
					(ConcreteSection.Area - ConcreteSection.AreaRebars) + fyA);

			if (force.N < limit)
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

		protected double CalculateDesignYieldingStrainRebar(SteelMaterial material)
		{
			return CalculateDesignYieldingStressRebar(material) / material.E;
		}

		protected double CalculateDesignUltimateStrainRebar(SteelMaterial material)
		{
			return material.StrainUTension;
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
