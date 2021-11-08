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

namespace GPC.Checkers.Concrete.ConcreteCheckerSolver
{
	internal static class SolverHelper
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

		internal static double CalculateFcd(IConcreteSection concreteSection, Standard standard)
		{
			throw new NotImplementedException();
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

		internal static double CalculateStressSteel(ReinforcedConcreteRebar rebar, double strain, Standard standard)
		{
			throw new NotImplementedException();
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
			return strainPlane.StrainReferencePoint + strainPlane.ChiX * (pointToTest.X - strainPlane.ReferencePoint.X) + 
				strainPlane.ChiY * (pointToTest.Y - strainPlane.ReferencePoint.Y); 
		}

		internal static void CalculateRelativeDistance(IConcreteSection concreteSection, double teta, 
			out int dMinRebarIndex, out int dMaxRebarIndex, out int dMinVertexIndex, out int dMaxVertexIndex)
		{
			double cosTeta = Math.Cos(teta);
			double sinTeta = Math.Sin(teta);

			double dminSteel = double.MaxValue;
			double dmaxSteel = double.MinValue;
			double dmaxConcrete = double.MinValue;
			double dminConcrete = double.MaxValue;

			dMinRebarIndex = -1;
			dMaxRebarIndex = -1;
			dMaxVertexIndex = -1;
			dMinVertexIndex = -1;

			for (int r = 0; r < concreteSection.Rebars.Count(); r++)
			{
				double w1 = (concreteSection.Rebars[r].Position.Y - concreteSection.Centroid.Y) * cosTeta -
					(concreteSection.Rebars[r].Position.X - concreteSection.Centroid.X) * sinTeta;
				if (w1 <= dminSteel)
				{
					dminSteel = w1;
					dMinRebarIndex = r;
				}

				if (w1 >= dmaxSteel)
				{
					dmaxSteel = w1;
					dMaxRebarIndex = r;
				}
			}

			//TODO: implementare con armature lineari

			for (int c = 0; c < concreteSection.Shape.Fill.Count; c++)
			{
				double w1 = (concreteSection.Shape.Fill[c].Y - concreteSection.Centroid.Y) * cosTeta -
					(concreteSection.Shape.Fill[c].X - concreteSection.Centroid.X) * sinTeta;

				if (w1 >= dmaxConcrete)
				{
					dMaxVertexIndex = c;
					dmaxConcrete = w1;
				}

				if (w1 <= dminConcrete)
				{
					dminConcrete = w1;
					dMinVertexIndex = c;
				}
			}
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
				deltaN = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection),
					points, 79);
				deltaMx = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection) *
					(y - concreteSection.Centroid.Y), points, 79);
				deltaMy = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection) *
					(x - concreteSection.Centroid.X), points, 79);
			}

			else if (face.IsQuad)
			{
				deltaN = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection),
					points, 121);
				deltaMx = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection) *
					(y - concreteSection.Centroid.Y), points, 121);
				deltaMy = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0)), Fcd, concreteSection) *
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
				double strain = CalculateStrain(strainPlane, concreteSection.Rebars[i].Position);
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

		internal static void CalculateAdimensionalForces(IConcreteSection concreteSection, double N, double Mx, double My, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY)
		{
			BoundingBox3d bBox = concreteSection.Shape.GetBoundingBox();
			double h = bBox.Size.Y;
			double b = bBox.Size.X;

			adimAxialForce = N / (b * h * concreteSection.ConcreteMaterial.Fck);
			adimBendingMomentX = Mx / (b * h * h * concreteSection.ConcreteMaterial.Fck);
			adimBendingMomentY = My / (b * b * h * concreteSection.ConcreteMaterial.Fck);
		}

		internal static void CalculateAdimensionalForces(IConcreteSection concreteSection, ResultBeamForces forces, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY)
		{
			BoundingBox3d bBox = concreteSection.Shape.GetBoundingBox();
			double h = bBox.Size.Y;
			double b = bBox.Size.X;

			adimAxialForce = forces.N / (b * h * concreteSection.ConcreteMaterial.Fck);
			adimBendingMomentX = forces.M1 / (b * h * h * concreteSection.ConcreteMaterial.Fck);
			adimBendingMomentY = forces.M2 / (b * b * h * concreteSection.ConcreteMaterial.Fck);
		}

	}
}
