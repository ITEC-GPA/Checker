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

namespace GPC.Checkers.Concrete.ConcreteCheckerSolver
{
	public class ConcreteSectionSolverSLSModelCode2010 : ConcreteSectionSolverSLS
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

		public ConcreteSectionSolverSLSModelCode2010(IConcreteSection concreteSection, ResultBeamForces forces, StandardModelCode2010 standard)
			: base(concreteSection, forces, standard)
		{
			
		}

		#endregion

		public override SLSCheckerResults PerformSolver()
		{
			StrainPlane strainPlane = CalculateStrainPlane();

			return new SLSModelCode2010CheckerResult(ConcreteSection, Forces, strainPlane, ModelCode2010);
		}

		#region Protected Methods

		protected virtual double CalculateFcd()
		{
			return ConcreteSolverHelper.CalculateFcd(ConcreteSection, ModelCode2010);
		}

		protected override double CalculateSigmaC(double strain)
		{
			return ConcreteSolverHelper.CalculateSigmaC(strain, Fcd, ConcreteSection);
		}

		protected override double CalculateStressSteel(ReinforcedConcreteRebar rebar, double strain)
		{
			return ConcreteSolverHelper.CalculateStressSteel(rebar, strain, ModelCode2010);
		}

		protected override double CalculateUltimateStrainSteel(ReinforcedConcreteRebar rebar)
		{
			return ConcreteSolverHelper.CalculateUltimateStrainSteel(rebar, ModelCode2010);
		}

		protected override double CalculateUltimateStrainSteel(int rebar)
		{
			return ConcreteSolverHelper.CalculateUltimateStrainSteel(ConcreteSection, rebar, ModelCode2010);
		}

		protected override double CalculateYeldingStrainSteel(ReinforcedConcreteRebar rebar)
		{
			return ConcreteSolverHelper.CalculateYeldingStrainSteel(rebar);
		}

		protected override double CalculateYeldingStrainSteel(int rebar)
		{
			return ConcreteSolverHelper.CalculateYeldingStrainSteel(ConcreteSection, rebar);
		}

		protected override double CalculateUltimateStrainConcreteCompression()
		{
			return ConcreteSolverHelper.CalculateUltimateStrainConcreteCompression(ConcreteSection);
		}

		protected override double CalculateYeldingStrainConcreteCompression()
		{
			return ConcreteSolverHelper.CalculateYeldingStrainConcreteCompression(ConcreteSection);
		}

		protected override double CalculateLimitStrainCostantCompression()
		{
			return ConcreteSolverHelper.CalculateLimitStrainCostantCompression(ModelCode2010);
		}

		protected override double CalculateUltimateStrainConcreteTension()
		{
			return ConcreteSolverHelper.CalculateUltimateStrainConcreteTension();
		}

		#endregion
	}
}
