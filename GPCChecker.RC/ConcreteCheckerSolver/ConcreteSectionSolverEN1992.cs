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
		protected StandardEN1992p11 _standard;

		public StandardEN1992p11 EN1992P11 => _standard;

		public ConcreteMaterialEN1992 ConcreteMaterial => (ConcreteMaterialEN1992)_concreteSection.ConcreteMaterial;

		/// <summary>
		/// Design compressive strength for persistent design
		/// </summary>
		public double Fcd => CalculateFcd();

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
			if (ConcreteMaterial.StressStrainDiagram == ConcreteMaterialEN1992.StressStrainDiagrams.StressBlock)
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
			if (ConcreteMaterial.StressStrainDiagram == ConcreteMaterialEN1992.StressStrainDiagrams.ParabolaRectangle)
			{
				if (strain >= ConcreteMaterial.EpsilonY)
					return Fcd;
				else
					return Fcd * (1 - Math.Pow(1 - strain / ConcreteMaterial.EpsilonY, ConcreteMaterial.CalculateN()));
			}
			else if (ConcreteMaterial.StressStrainDiagram == ConcreteMaterialEN1992.StressStrainDiagrams.Bilinear)
			{
				if (strain >= ConcreteMaterial.EpsilonY)
					return Fcd;
				else
					return 0.0;
			}
			else if (ConcreteMaterial.StressStrainDiagram == ConcreteMaterialEN1992.StressStrainDiagrams.StressBlock)
			{
				if (strain >= ConcreteMaterial.EpsilonY)
					return Fcd;
				else
					return Fcd * strain / ConcreteMaterial.EpsilonY;
			}
			else
				throw new ArgumentException("");
		}

		protected override double CalculateSigmaS(double strain)
		{
			throw new NotImplementedException();
		}
	}
}
