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

		public SectionSolverSLSModelCode2010(IConcreteSection concreteSection, ResultBeamForces forces, StandardModelCode2010 standard)
			: base(concreteSection, forces, standard)
		{
			
		}

		#endregion

		public override SLSCheckerResultsType PerformSolver()
		{
			StrainPlane strainPlane = Solve();

			return new SLSModelCode2010CheckerResult(ConcreteSection, Forces, strainPlane, ModelCode2010);
		}

		#region Protected Methods

		protected override double GetFck()
        {
			return ConcreteMaterialModelCode2010.Fck;
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

		protected override double GetDesignUltimateStrainSteel(ReinforcedConcreteRebar rebar)
		{
			return SectionSolverHelper.CalculateUltimateDesignStrainSteel(rebar, ModelCode2010);
		}

		protected override double GetDesignUltimateStrainSteel(int rebar)
		{
			return SectionSolverHelper.CalculateUltimateDesignStrainSteel(ConcreteSection, rebar, ModelCode2010);
		}

		protected override double GetDesignYieldingStrainSteel(ReinforcedConcreteRebar rebar)
		{
			return SectionSolverHelper.CalculateYeldingStrainSteel(rebar);
		}

		protected override double GetDesignYieldingStrainSteel(int rebar)
		{
			return SectionSolverHelper.CalculateYeldingStrainSteel(ConcreteSection, rebar);
		}

		protected override double GetUltimateStrainConcreteCompression()
		{
			return SectionSolverHelper.CalculateUltimateStrainConcreteCompression(ConcreteSection);
		}

		protected override double GetYieldingStrainConcreteCompression()
		{
			return SectionSolverHelper.CalculateYeldingStrainConcreteCompression(ConcreteSection);
		}

		protected override double GetYieldingStrainPureCompression()
		{
			return SectionSolverHelper.CalculateLimitStrainCostantCompression(ModelCode2010);
		}

		protected override double GetUltimateStrainConcreteTension()
		{
			return SectionSolverHelper.CalculateUltimateStrainConcreteTension();
		}

		#endregion
	}
}
