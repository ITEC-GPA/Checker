using GPC.Checker.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
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
	public class SectionSolverACI318 : SectionSolver, ISerializable
	{
		protected bool _haveSpiral;

		#region Properties

		public StandardACI318 StandardACI318 => (StandardACI318)_standard;

		public ConcreteMaterialACI318 ConcreteMaterialACI318 => (ConcreteMaterialACI318)_concreteSection.ConcreteMaterial;

		public bool HaveSpiral { get=> _haveSpiral; set => _haveSpiral = value; }

		#endregion

		#region Constructor

		internal SectionSolverACI318(IConcreteSection section, StandardACI318 standard, bool haveSpiral,
			bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED, Standard standardStructuralSteel = null)
			: base(section, standard, considerTensileConcrete, id, standardStructuralSteel)
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

        internal override double CalculateStressStructuralSteel(ISteelSection steelSection, double strain)
        {
            return steelSection.SteelMaterial.GetStress(strain);
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

			double fyStructuralSteel = 0;
			if(ConcreteSection.IsCompositeSteelConcrete)
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
            double conpressionControlledCoefficient;
            if (_haveSpiral)
                conpressionControlledCoefficient = StandardACI318.PhiCSpiral;
            else
                conpressionControlledCoefficient = StandardACI318.PhiCTied;

            limit = - maximumAxialStrengthCoefficient * conpressionControlledCoefficient * (StandardACI318.ConcreteStrengthReductionFactor * fc *
                (ConcreteSection.Area - ConcreteSection.AreaRebars) + fyA + fyStructuralSteel);

            return limit;
        }

        #region Failure domain limit points

        internal override DeformationFieldsPoint GetP3(BoundaryDistances distances,
			FailureDomainTypes analysisType)
		{
			switch (analysisType)
			{
				case FailureDomainTypes.Elastic:
					return new DeformationFieldsPoint(GetYieldingStrainConcreteCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
						(distances.dmaxConcrete - distances.dminRebar));

				case FailureDomainTypes.Plastic:
					return new DeformationFieldsPoint(GetUltimateStrainConcreteCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
						(distances.dmaxConcrete - distances.dminRebar));

				default:
					return new DeformationFieldsPoint(0.0, null, 0.0);
			}
		}

		#endregion

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

		protected double CalculateDesignYieldingStrainRebar(SteelMaterial material)
		{
			return CalculateDesignYieldingStressRebar(material) / material.E;
		}

		protected double CalculateDesignUltimateStrainRebar(SteelMaterial material)
		{
			return material.StrainUTension;
        }

        protected double CalculateDesignYieldingStrainStructuralSteel(SteelMaterial material)
        {
            return material.Fyk / material.E;
        }

        protected double CalculateDesignUltimateStrainStructuralSteel(SteelMaterial material)
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
