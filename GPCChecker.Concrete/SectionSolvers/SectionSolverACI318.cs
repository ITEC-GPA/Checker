using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
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

		protected override double CalculateCompressionAxialForceLimit()
		{
			double fyA = 0;
			ReinforcedConcreteRebar[] rebars = ConcreteSection.GetRebars();
			for (int i = 0; i < rebars.Length; i++)
				fyA += rebars[i].Area * Math.Min(rebars[i].RebarMaterial.Fyk, 551.579);

			double limit;
			double fc = 0;

			if (ConcreteMaterial is ConcreteMaterialACI318)
				fc = Math.Abs(ConcreteMaterialACI318.Fc);
			else if (ConcreteMaterial is ConcreteMaterialEuropeanCommon ec)
				fc = Math.Abs(ec.Fck);

			if (_haveSpiral)
				limit = - StandardACI318.PhiMaximumCompressiveAxialLoadSpiral * StandardACI318.PhiCSpiral * (StandardACI318.ConcreteStrengthReductionFactor * fc *
					(ConcreteSection.Area - ConcreteSection.AreaRebars) + fyA);
			else
				limit = - StandardACI318.PhiMaximumCompressiveAxialLoadTied * StandardACI318.PhiCTied * (StandardACI318.ConcreteStrengthReductionFactor * fc *
					(ConcreteSection.Area - ConcreteSection.AreaRebars) + fyA);

			return limit;
		}

		#region Failure domain limit points

		protected override (double epsilon, Point2d point, double distanceFromBaricentre) GetP3((double teta, int dMinRebarId, double dminRebar,
			int dMaxRebarId, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) distances,
			FailureDomainTypes analysisType)
		{
			switch (analysisType)
			{
				case FailureDomainTypes.Elastic:
					return (GetYieldingStrainConcreteCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
						(distances.dmaxConcrete - distances.dminRebar));

				case FailureDomainTypes.Plastic:
					return (GetUltimateStrainConcreteCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
						(distances.dmaxConcrete - distances.dminRebar));

				default:
					return (0.0, null, 0.0);
			}
		}

		#endregion

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
