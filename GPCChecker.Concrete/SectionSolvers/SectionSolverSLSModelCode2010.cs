using GPC.Checkers.Concrete.Results;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.Concrete.SectionSolvers
{
	public class SectionSolverSLSModelCode2010 : SectionSolverSLS
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

		public SectionSolverSLSModelCode2010(IConcreteSection concreteSection, ResultBeamForces forces, StandardModelCode2010 standard)
			: base(concreteSection, forces, standard)
		{
			
		}

		#endregion

		#region Protected Methods

		protected override double GetFck()
        {
			return ConcreteMaterialModelCode2010.Fck;
		}


		protected override double CalculateSigmaC(double strain)
		{
			return ModelCode2010Helper.CalculateSigmaC(strain, Fcd, Fctd, ConcreteMaterialModelCode2010) ;
		}

		protected override double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain)
		{
			return SectionSolverHelper.CalculateStressSteel(rebar, strain);
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

		#endregion
	}
}
