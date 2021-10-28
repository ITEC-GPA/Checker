using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver
{
	public class ConcreteSectionSolverSLSModelCode2010 : ConcreteSectionSolverSLS
	{
		#region Variables

		protected StandardModelCode2010 _standard;

		#endregion

		#region Properties

		public StandardModelCode2010 ModelCode2010 => _standard;

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
			: base(concreteSection, forces)
		{
			_standard = standard;
		}

		#endregion

		protected virtual double CalculateFcd()
		{
			if (ConcreteMaterialModelCode2010.CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.StressBlock)
			{
				double eta;
				if (ConcreteMaterial.Fck <= 50.0)
					eta = 1.0;
				else
					eta = 1.0 - (ConcreteMaterial.Fck - 50.0) / 200;

				return eta * ModelCode2010.AlphaCC * ConcreteMaterial.Fck / ModelCode2010.GammaC;
			}
			else
			{
				return ModelCode2010.AlphaCC * ConcreteMaterial.Fck / ModelCode2010.GammaC;
			}
		}

		protected override double CalculateSigmaC(double strain)
		{
			if (ConcreteMaterialModelCode2010.CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle)
			{
				if (strain >= 0.0)
					return 0.0;
				else if (strain <= ConcreteMaterial.StrainCompressionY)
					return -Fcd;
				else
					return -Fcd * (1 - Math.Pow(1 - Math.Abs(strain) / ConcreteMaterial.StrainCompressionY, ((ConcreteMaterialEN1992)ConcreteMaterial).CalculateN()));
			}
			else if (ConcreteMaterialModelCode2010.CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.StressBlock)
			{
				if (strain >= 0.0)
					return 0.0;
				if (strain <= ConcreteMaterial.StrainCompressionY)
					return -Fcd;
				else
					return 0.0;
			}
			else if (ConcreteMaterialModelCode2010.CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear)
			{
				if (strain >= 0.0)
					return 0.0;
				else if (strain <= ConcreteMaterial.StrainCompressionY)
					return -Fcd;
				else
					return -Fcd * Math.Abs(strain) / ConcreteMaterial.StrainCompressionY;
			}
			else
				throw new ArgumentException("");
		}

		protected override double CalculateStressSteel(ReinforcedConcreteRebar rebar, double strain)
		{
			if (rebar.EpsilonP == 0)
				return rebar.RebarMaterial.CalculateStress(strain) / ModelCode2010.GammaS;
			else
				return rebar.RebarMaterial.CalculateStress(strain + rebar.EpsilonP) / ModelCode2010.GammaSPrestress;
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
			return ConcreteSection.ConcreteMaterial.StrainCompressionU;
		}

		protected override double CalculateYeldingStrainConcreteCompression()
		{
			return ConcreteSection.ConcreteMaterial.StrainCompressionY;
		}

		protected override double CalculateLimitStrainCostantCompression()
		{
			return ModelCode2010.ConcreteLimitStrainPureCompression;
		}

		protected override double CalculateUltimateStrainConcreteTension()
		{
			throw new NotImplementedException();
		}
	}
}
