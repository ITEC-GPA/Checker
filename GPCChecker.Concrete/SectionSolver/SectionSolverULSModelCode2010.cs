using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Model.Sections.Concrete;
using GPC.Model.Materials;
using GPC.Geometry;
using GPC.Geometry.Meshes;

namespace GPC.Checkers.Concrete.SectionSolver
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
			:base(concreteSection, standard)
		{
			if(concreteSection.ConcreteMaterial is ConcreteMaterialEN1992)
			{ }
			else
				throw new ArgumentException("Material must be a ConcreteMaterial");
		}

		#endregion

		protected virtual double CalculateFcd()
		{
			return SolverHelper.CalculateFcd(ConcreteSection, ModelCode2010);
		}

		protected override double CalculateSigmaC(double strain)
		{
			return SolverHelper.CalculateSigmaC(strain, Fcd, ConcreteSection);
		}

		protected override double CalculateStressSteel(ReinforcedConcreteRebar rebar, double strain)
		{
			return SolverHelper.CalculateStressSteel(rebar, strain, ModelCode2010);
		}

		protected override double CalculateUltimateStrainSteel(ReinforcedConcreteRebar rebar)
		{
			return SolverHelper.CalculateUltimateStrainSteel(rebar, ModelCode2010);
		}

		protected override double CalculateUltimateStrainSteel(int rebar)
		{
			return SolverHelper.CalculateUltimateStrainSteel(ConcreteSection, rebar, ModelCode2010);
		}

		protected override double CalculateYeldingStrainSteel(ReinforcedConcreteRebar rebar)
		{
			return SolverHelper.CalculateYeldingStrainSteel(rebar);
		}

		protected override double CalculateYeldingStrainSteel(int rebar)
		{
			return SolverHelper.CalculateYeldingStrainSteel(ConcreteSection, rebar);
		}

		protected override double CalculateUltimateStrainConcreteCompression()
		{
			return SolverHelper.CalculateUltimateStrainConcreteCompression(ConcreteSection);
		}

		protected override double CalculateYeldingStrainConcreteCompression()
		{
			return SolverHelper.CalculateYeldingStrainConcreteCompression(ConcreteSection);
		}

		protected override double CalculateLimitStrainCostantCompression()
		{
			return SolverHelper.CalculateLimitStrainCostantCompression(ModelCode2010);
		}

		protected override double CalculateUltimateStrainConcreteTension()
		{
			return SolverHelper.CalculateUltimateStrainConcreteTension();
		}
	}
}
