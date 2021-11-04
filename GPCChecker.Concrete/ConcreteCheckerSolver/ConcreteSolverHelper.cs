using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.Concrete.ConcreteCheckerSolver
{
	internal static class ConcreteSolverHelper
	{
		#region ModelCode2010 

		internal static double CalculateFcd(IConcreteSection concreteSection, StandardModelCode2010 standard)
		{
			if (((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).CompressionStressStrainDiagram == 
				ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.StressBlock)
			{
				double eta;
				if (((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).Fck <= 50.0)
					eta = 1.0;
				else
					eta = 1.0 - (((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).Fck - 50.0) / 200;

				return eta * standard.AlphaCC * ((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).Fck / standard.GammaC;
			}
			else
			{
				return standard.AlphaCC * ((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).Fck / standard.GammaC;
			}
		}

		internal static double CalculateSigmaC(double strain, double Fcd, IConcreteSection concreteSection)
		{
			if (((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).CompressionStressStrainDiagram == 
				ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle)
			{
				if (strain >= 0.0)
					return 0.0;
				else if (strain <= ((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).EpsilonCy)
					return -Fcd;
				else
					return -Fcd * (1 - Math.Pow(1 - Math.Abs(strain / ((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).EpsilonCy), 
						((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).CalculateN()));
			}
			else if (((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.StressBlock)
			{
				if (strain >= 0.0)
					return 0.0;
				if (strain <= ((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).EpsilonCy)
					return -Fcd;
				else
					return 0.0;
			}
			else if (((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear)
			{
				if (strain >= 0.0)
					return 0.0;
				else if (strain <= ((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).EpsilonCy)
					return -Fcd;
				else
					return -Fcd * Math.Abs(strain / ((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial).EpsilonCy);
			}
			else
				throw new ArgumentException("");
		}

		internal static double CalculateStressSteel(ReinforcedConcreteRebar rebar, double strain, StandardModelCode2010 standard)
		{
			if (rebar.EpsilonP == 0)
				return rebar.RebarMaterial.CalculateStress(strain) / standard.GammaS;
			else
				return rebar.RebarMaterial.CalculateStress(strain + rebar.EpsilonP) / standard.GammaSPrestress;
		}

		internal static double CalculateUltimateStrainSteel(ReinforcedConcreteRebar rebar, StandardModelCode2010 standard)
		{
			return rebar.RebarMaterial.EpsilonU * standard.SteelCoefficientStrainTraction;
		}

		internal static double CalculateUltimateStrainSteel(IConcreteSection concreteSection, int rebar, StandardModelCode2010 standard)
		{
			return concreteSection.Rebars[rebar].RebarMaterial.EpsilonU * standard.SteelCoefficientStrainTraction;
		}

		internal static double CalculateYeldingStrainSteel(ReinforcedConcreteRebar rebar)
		{
			return rebar.RebarMaterial.EpsilonY;
		}

		internal static double CalculateYeldingStrainSteel(IConcreteSection concreteSection, int rebar)
		{
			return concreteSection.Rebars[rebar].RebarMaterial.EpsilonY;
		}

		internal static double CalculateUltimateStrainConcreteCompression(IConcreteSection concreteSection)
		{
			return concreteSection.ConcreteMaterial.EpsilonCu;
		}

		internal static double CalculateYeldingStrainConcreteCompression(IConcreteSection concreteSection)
		{
			return concreteSection.ConcreteMaterial.EpsilonCy;
		}

		internal static double CalculateLimitStrainCostantCompression(StandardModelCode2010 standard)
		{
			return standard.ConcreteLimitStrainPureCompression;
		}

		internal static double CalculateUltimateStrainConcreteTension()
		{
			throw new NotImplementedException();
		}

		#endregion

		internal static double CalculateStrain(StrainPlane strainPlane, Point3d pointToTest)
		{
			return strainPlane.StrainReferencePoint + strainPlane.ChiX * pointToTest.X + strainPlane.ChiY * pointToTest.Y;
		}
	}
}
