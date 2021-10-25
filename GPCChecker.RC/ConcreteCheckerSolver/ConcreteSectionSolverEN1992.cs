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
	public class ConcreteSectionSolverEN1992 : ConcreteSectionSolver
	{
		#region Variables

		protected StandardEN1992p11 _standard;

		#endregion

		#region Properties

		public StandardEN1992p11 EN1992P11 => _standard;

		public ConcreteMaterialEN1992 ConcreteMaterialEN1992 => (ConcreteMaterialEN1992)_concreteSection.ConcreteMaterial;

		/// <summary>
		/// Design compressive strength for persistent design
		/// </summary>
		public double Fcd => CalculateFcd();

		/// <summary>
		/// Design compressive strength for accidental design
		/// </summary>
		public double FcdAccidental => EN1992P11.AlphaCC * ConcreteMaterialEN1992.Fck / EN1992P11.GammaCAccidental;

		/// <summary>
		/// Design tensile strength for persistent design
		/// </summary>
		public double Fctd => EN1992P11.AlphaCT * ConcreteMaterialEN1992.Fctk05 / EN1992P11.GammaC;

		/// <summary>
		/// Design tensile strength for accidental design
		/// </summary>
		public double FctdAccidental => EN1992P11.AlphaCT * ConcreteMaterialEN1992.Fctk05 / EN1992P11.GammaCAccidental;

		/// <summary>
		/// Modulus of elasticity value for ultimate limit state calculations
		/// </summary>
		public double ECd => ConcreteMaterialEN1992.E / EN1992P11.GammaCE;

		#endregion

		#region Constructors

		public ConcreteSectionSolverEN1992(IConcreteSection concreteSection, StandardEN1992p11 standard)
			:base(concreteSection)
		{
			if(concreteSection.ConcreteMaterial is ConcreteMaterialEN1992)
			{ }
			else
				throw new ArgumentException("Material must be a ConcreteMaterial");

			_standard = standard;
		}

		#endregion

		protected virtual double CalculateFcd()
		{
			if (((ConcreteMaterialEN1992)ConcreteMaterial).StressStrainDiagram == ConcreteMaterialEN1992.CompressionStressStrainDiagrams.StressBlock)
			{
				double eta;
				if (ConcreteMaterial.Fck <= 50.0)
					eta = 1.0;
				else
					eta = 1.0 - (ConcreteMaterial.Fck - 50.0) / 200;

				return eta * EN1992P11.AlphaCC * ConcreteMaterial.Fck / EN1992P11.GammaC;
			}
			else
			{
				return EN1992P11.AlphaCC * ConcreteMaterial.Fck / EN1992P11.GammaC;
			}
		}

		protected override double CalculateSigmaC(double strain)
		{
			if (((ConcreteMaterialEN1992)ConcreteMaterial).StressStrainDiagram == ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle)
			{
				if (strain > 0.0)
					return 0.0;
				if (Math.Abs(strain) >= ConcreteMaterial.EpsilonCompressionY)
					return -Fcd;
				else
					return -Fcd * (1 - Math.Pow(1 - Math.Abs(strain) / ConcreteMaterial.EpsilonCompressionY, ((ConcreteMaterialEN1992)ConcreteMaterial).CalculateN()));
			}
			else if (((ConcreteMaterialEN1992)ConcreteMaterial).StressStrainDiagram == ConcreteMaterialEN1992.CompressionStressStrainDiagrams.StressBlock)
			{
				if (strain > 0.0)
					return 0.0;
				if (Math.Abs(strain) >= ConcreteMaterial.EpsilonCompressionY)
					return -Fcd;
				else
					return 0.0;
			}
			else if (((ConcreteMaterialEN1992)ConcreteMaterial).StressStrainDiagram == ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear)
			{
				if (strain > 0.0)
					return 0.0;
				if (Math.Abs(strain) >= ConcreteMaterial.EpsilonCompressionY)
					return -Fcd;
				else
					return -Fcd * Math.Abs(strain) / ConcreteMaterial.EpsilonCompressionY;
			}
			else
				throw new ArgumentException("");
		}

		protected override double CalculateSigmaS(ReinforcedConcreteRebar rebar, double strain)
		{
			if(strain < 0)
				return rebar.RebarMaterial.CalculateSigma(strain) / EN1992P11.GammaS;
			else
				return rebar.RebarMaterial.CalculateSigma(strain) / EN1992P11.GammaS;
		}

		protected override double CalculateUltimateStrainSteel(ReinforcedConcreteRebar rebar)
		{
			return rebar.RebarMaterial.EpsilonU * 0.9;
		}

		protected override double CalculateUltimateStrainSteel(int rebar)
		{
			return ConcreteSection.Rebars[rebar].RebarMaterial.EpsilonU * 0.9;
		}

		protected override double CalculateYeldingStrainSteel(ReinforcedConcreteRebar rebar)
		{
			return rebar.RebarMaterial.EpsilonY;
		}

		protected override double CalculateYeldingStrainSteel(int rebar)
		{
			return ConcreteSection.Rebars[rebar].RebarMaterial.EpsilonY;
		}

		protected override double CalculateUltimateStrainConcreteCompression()
		{
			return ConcreteSection.ConcreteMaterial.EpsilonCompressionU;
		}

		protected override double CalculateYeldingStrainConcreteCompression()
		{
			return ConcreteSection.ConcreteMaterial.EpsilonCompressionY;
		}

		protected override double CalculateLimitStrainCostantCompression()
		{
			return - 0.002;
		}

		protected override double CalculateUltimateStrainConcreteTension()
		{
			throw new NotImplementedException();
		}
	}
}
