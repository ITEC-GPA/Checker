using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
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
	internal static class SectionSolverHelper
	{
		#region ModelCode2010 


		internal static double CalculateSigmaC(double strain, double fcd, double fctd, IConcreteSection concreteSection)
		{
			var material = ((ConcreteMaterialModelCode2010)concreteSection.ConcreteMaterial);

			if (strain < 0)
			{
				return material.GetStress(strain) * fcd / material.Fck;
			}
            else
            {
				return material.GetStress(strain) * fctd / material.Fctk;
			}
		}

		internal static double CalculateStressSteel(ReinforcedConcreteRebar rebar, double strain)
		{
			return rebar.RebarMaterial.CalculateStress(strain + rebar.EpsilonP);
		}

		internal static double CalculateUltimateDesignStrainSteel(ReinforcedConcreteRebar rebar, StandardModelCode2010 standard)
		{
			return rebar.RebarMaterial.StrainU * standard.SteelCoefficientStrainTraction;
		}

		internal static double CalculateUltimateDesignStrainSteel(IConcreteSection concreteSection, int rebar, StandardModelCode2010 standard)
		{
			return concreteSection.Rebars[rebar].RebarMaterial.StrainU * standard.SteelCoefficientStrainTraction;
		}

		//internal static double CalculateYeldingStrainSteel(ReinforcedConcreteRebar rebar)
		//{
		//	return rebar.RebarMaterial.StrainU;
		//}

		internal static double CalculateYeldingStrainSteel(IConcreteSection concreteSection, int rebar)
		{
			return concreteSection.Rebars[rebar].RebarMaterial.StrainU;
		}

		internal static double CalculateUltimateStrainConcreteCompression(IConcreteSection concreteSection)
		{
			return concreteSection.ConcreteMaterial;
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

		internal static double CalculatePointStrain(StrainPlane strainPlane, Point3d pointToTest)
		{
			return strainPlane.StrainReferencePoint + strainPlane.ChiX * (pointToTest.X - strainPlane.ReferencePoint.X) + 
				strainPlane.ChiY * (pointToTest.Y - strainPlane.ReferencePoint.Y); 
		}


		internal static void CalculateForces(IConcreteSection concreteSection, StrainPlane strainPlane, Standard standard, out double N, out double Mx, out double My)
		{			
			CalculateConcreteStressResultant(concreteSection, strainPlane, standard, out double deltaNConcrete, out double deltaMxConcrete, out double deltaMyConcrete);
			CalculateRebarsIntegration(concreteSection, strainPlane, standard, out double deltaNRebar, out double deltaMxRebar, out double deltaMyRebar);
						
			N = deltaNConcrete + deltaNRebar;
			Mx = -(deltaMxConcrete + deltaMxRebar);
			My = deltaMyConcrete + deltaMyRebar;
		}

		/// <summary>
		/// Calculate the stress resultant of the concrete part
		/// </summary>
		/// <param name="strainPlane">The strain plane</param>
		/// <param name="deltaN">The axial force resultant</param>
		/// <param name="deltaMx">The bending moment about X-axis resultant</param>
		/// <param name="deltaMy">The bending moment about Y-axis resultant</param>
		internal static void CalculateConcreteStressResultant(IConcreteSection concreteSection, StrainPlane strainPlane, Standard standard, out double deltaN, out double deltaMx, out double deltaMy)
		{
			double[] deltaNArray = new double[concreteSection.Mesh.FacesCount];
			double[] deltaMxArray = new double[concreteSection.Mesh.FacesCount];
			double[] deltaMyArray = new double[concreteSection.Mesh.FacesCount];

			Parallel.For(0, concreteSection.Mesh.FacesCount, (i) =>
			{
				CalculateFaceStressResultant(concreteSection, concreteSection.Mesh.Faces[(int)i + 1], strainPlane, standard, out double deltaNBuffer, out double deltaMxBuffer, out double deltaMyBuffer);

				deltaNArray[i] = deltaNBuffer;
				deltaMxArray[i] = deltaMxBuffer;
				deltaMyArray[i] = deltaMyBuffer;
			});

			deltaN = deltaNArray.Sum();
			deltaMx = deltaMxArray.Sum();
			deltaMy = deltaMyArray.Sum();
		}

		/// <summary>
		/// Calculate the resultants of face <paramref name="face"/>
		/// </summary>
		/// <param name="face"></param>
		/// <param name="strainPlane"></param>
		/// <param name="deltaN"></param>
		/// <param name="deltaMx"></param>
		/// <param name="deltaMy"></param>
		internal static void CalculateFaceStressResultant(IConcreteSection concreteSection, MeshFace face, StrainPlane strainPlane, Standard standard, out double deltaN, out double deltaMx, out double deltaMy)
		{
			Point3d[] points = concreteSection.Mesh.GetFacePoints(face);
			double Fcd = CalculateFcd(concreteSection, standard);

			if (face.IsTriangle)
			{
				deltaN = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculatePointStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection),
					points, 79);
				deltaMx = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculatePointStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection) *
					(y - concreteSection.Centroid.Y), points, 79);
				deltaMy = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculatePointStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection) *
					(x - concreteSection.Centroid.X), points, 79);
			}

			else if (face.IsQuad)
			{
				deltaN = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculatePointStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection),
					points, 121);
				deltaMx = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculatePointStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection) *
					(y - concreteSection.Centroid.Y), points, 121);
				deltaMy = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculatePointStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection) *
					(x - concreteSection.Centroid.X), points, 121);
			}
			else
				throw new Exception();
		}

		/// <summary>
		/// Calculate the resultant of all the rebars
		/// </summary>
		/// <param name="strainPlane">The strain plane</param>
		/// <param name="deltaN">The axial force resultant</param>
		/// <param name="deltaMx">The bending moment about X-axis resultant</param>
		/// <param name="deltaMy">The bending moment about Y-axis resultant</param>
		internal static void CalculateRebarsIntegration(IConcreteSection concreteSection, StrainPlane strainPlane, Standard standard, out double deltaN, out double deltaMx, out double deltaMy)
		{
			double[] deltaNArray = new double[concreteSection.Rebars.Length];
			double[] deltaMxArray = new double[concreteSection.Rebars.Length];
			double[] deltaMyArray = new double[concreteSection.Rebars.Length];
			double Fcd = CalculateFcd(concreteSection, standard);

			Parallel.For(0, concreteSection.Rebars.Length, (i) =>
			{
				double strain = CalculatePointStrain(strainPlane, concreteSection.Rebars[i].Position);
				double sigmaS = CalculateStressSteel(concreteSection.Rebars[i], strain, standard);
				double sigmaC = CalculateSigmaC(strain, Fcd, concreteSection);

				deltaNArray[i] = (sigmaS - sigmaC) * concreteSection.Rebars[i].Area;
				deltaMxArray[i] = (sigmaS - sigmaC) * concreteSection.Rebars[i].Area * (concreteSection.Rebars[i].Position.Y - concreteSection.Centroid.Y);
				deltaMyArray[i] = (sigmaS - sigmaC) * concreteSection.Rebars[i].Area * (concreteSection.Rebars[i].Position.X - concreteSection.Centroid.X);
			});

			deltaN = deltaNArray.Sum();
			deltaMx = deltaMxArray.Sum();
			deltaMy = deltaMyArray.Sum();
		}


	}
}
