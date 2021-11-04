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

namespace GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver
{
	public class ConcreteSectionSolverULSModelCode2010 : ConcreteSectionSolverULS
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

		public ConcreteSectionSolverULSModelCode2010(IConcreteSection concreteSection, StandardModelCode2010 standard)
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
			return ConcreteSolverHelper.CalculateFcd(ConcreteSection, ModelCode2010);
		}

		protected override double CalculateSigmaC(double strain)
		{
			if (ConcreteMaterialModelCode2010.CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle)
			{
				if (strain >= 0.0)
					return 0.0;
				else if (strain <= ConcreteMaterial.EpsilonCy)
					return -Fcd;
				else
					return -Fcd * (1 - Math.Pow(1 - Math.Abs(strain / ConcreteMaterial.EpsilonCy), ((ConcreteMaterialEN1992)ConcreteMaterial).CalculateN()));
			}
			else if (ConcreteMaterialModelCode2010.CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.StressBlock)
			{
				if (strain >= 0.0)
					return 0.0;
				else if (strain <= ConcreteMaterial.EpsilonCy)
					return -Fcd;
				else
					return 0.0;
			}
			else if (ConcreteMaterialModelCode2010.CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear)
			{
				if (strain >= 0.0)
					return 0.0;
				else if (strain <= ConcreteMaterial.EpsilonCy)
					return -Fcd;
				else
					return -Fcd * Math.Abs(strain / ConcreteMaterial.EpsilonCy);
			}
			else
				throw new ArgumentException("");
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
	}
}
