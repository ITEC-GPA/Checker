using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.ReinforcedConcrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver
{
	public abstract class ConcreteSectionSolver
	{
		public enum FailureIndices
		{
			Iz1 = 1,
			Iz2 = 2,
			Iz3 = 3,
			Iz4 = 4,
			Iz5 = 5,
			Iz6 = 6,
		}

		protected  IConcreteSection _concreteSection;

		public IConcreteSection ConcreteSection => _concreteSection;

		public ConcreteMaterial ConcreteMaterial => _concreteSection.ConcreteMaterial;

		public RebarMaterial[] SteelMaterial => (RebarMaterial[])_concreteSection.Rebars.Select(i => i.RebarMaterial);

		public Mesh Mesh => _concreteSection.Mesh;

		public ConcreteSectionSolver(IConcreteSection section)
		{
			_concreteSection = section;
		}





		protected abstract double CalculateYeldingStrainSteel(ReinforcedConcreteRebar rebar);
		protected abstract double CalculateYeldingStrainSteel(int rebar);
		protected abstract double CalculateUltimateStrainSteel(ReinforcedConcreteRebar rebar);
		protected abstract double CalculateUltimateStrainSteel(int rebar);
		protected abstract double CalculateUltimateStrainConcreteCompression();
		protected abstract double CalculateYeldingStrainConcreteCompression();
		protected abstract double CalculateLimitStrainCostantCompression();
		protected abstract double CalculateUltimateStrainConcreteTension();
		protected abstract double CalculateSigmaC(double strain);
		protected abstract double CalculateSigmaS(ReinforcedConcreteRebar rebar, double strain);



		/// <summary>
		/// Calculate the strain planes for angle <paramref name="teta"/>
		/// </summary>
		/// <param name="teta">The angle of rotation of the axis</param>
		/// <param name="zoneSubdivision">Number of subdivision for each failure zone</param>
		/// <returns></returns>
		protected virtual (StrainPlane, FailureIndices)[] CalculateAllDesignStrainPlanes(double teta, int[] zoneSubdivision)
		{
			if (zoneSubdivision.Length != 7)
				throw new ArgumentException("Subdivision must have 6 elements");

			(StrainPlane, FailureIndices)[] strainPlanes = new (StrainPlane, FailureIndices)[zoneSubdivision.Sum() + 7 + 1];
			double cosTeta = Math.Cos(teta);
			double sinTeta = Math.Sin(teta);
			int subIndex = 0;

			double dminSteel = double.MaxValue;
			double dmaxSteel = double.MinValue;
			double dmaxConcrete = double.MinValue;
			double dminConcrete = double.MaxValue;

			int dMinRebarIndex = -1;
			int dMaxVertexIndex = -1;
			int dMinVertexIndex = -1;

			for (int r = 0; r < ConcreteSection.Rebars.Count(); r++)
			{
				double w1 = (ConcreteSection.Rebars[r].Position.Y - ConcreteSection.Centroid.Y) * cosTeta -
					(ConcreteSection.Rebars[r].Position.X - ConcreteSection.Centroid.X) * sinTeta;
				if (w1 <= dminSteel)
				{
					dminSteel = w1;
					dMinRebarIndex = r;
				}

				if (w1 >= dmaxSteel)
					dmaxSteel = w1;
			}

			//TODO: implementare con armature lineari

			for (int c = 0; c < ConcreteSection.Shape.Fill.Count; c++)
			{
				double w1 = (ConcreteSection.Shape.Fill[c].Y - ConcreteSection.Centroid.Y) * cosTeta -
					(ConcreteSection.Shape.Fill[c].X - ConcreteSection.Centroid.X) * sinTeta;

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

			// per ogni indice di campo - 1
			for (int i = 0; i < 7; i++)
			{
				if (i == 0)   // campo 1
				{
					int subdivision = zoneSubdivision[i] + 1;

					double chiSx = 0;   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = CalculateUltimateStrainSteel(dMinRebarIndex) / (dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + j * (chiDx - chiSx) / subdivision;
						strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Rebars[dMinRebarIndex].Position, teta, chi, 
							CalculateUltimateStrainSteel(dMinRebarIndex), subIndex), FailureIndices.Iz1);
						subIndex++;
					}
				}
				else if (i == 1)
				{
					int subdivision = zoneSubdivision[i] + 1;

					double chiSx = CalculateUltimateStrainSteel(dMinRebarIndex) / (dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = (CalculateUltimateStrainSteel(dMinRebarIndex) + Math.Abs(CalculateYeldingStrainConcreteCompression())) / 
						(dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + j * (chiDx - chiSx) / subdivision;
						strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Rebars[dMinRebarIndex].Position, teta, chi,
							CalculateUltimateStrainSteel(dMinRebarIndex), subIndex), FailureIndices.Iz2);
						subIndex++;
					}
				}
				else if (i == 2)
				{
					int subdivision = zoneSubdivision[i] + 1;

					double chiSx = (CalculateUltimateStrainSteel(dMinRebarIndex) + Math.Abs(CalculateYeldingStrainConcreteCompression())) / 
						(dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = (CalculateUltimateStrainSteel(dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
						(dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + j * (chiDx - chiSx) / subdivision;
						strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Rebars[dMinRebarIndex].Position, teta, chi, 
							CalculateUltimateStrainSteel(dMinRebarIndex), subIndex), FailureIndices.Iz3);
						subIndex++;
					}
				}
				else if (i == 3)
				{
					int subdivision = zoneSubdivision[i] + 1;

					double chiSx = (CalculateUltimateStrainSteel(dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) / 
						(dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = (CalculateYeldingStrainSteel(dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) / 
						(dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + j * (chiDx - chiSx) / subdivision;
						strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Shape.Fill[dMaxVertexIndex], teta, chi, CalculateUltimateStrainConcreteCompression(), subIndex), FailureIndices.Iz4);
						subIndex++;
					}
				}
				else if (i == 4)
				{
					int subdivision = zoneSubdivision[i] + 1;

					double chiSx = (CalculateYeldingStrainSteel(dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) / 
						(dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + j * (chiDx - chiSx) / subdivision;
						strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Shape.Fill[dMaxVertexIndex], teta, chi, 
							CalculateUltimateStrainConcreteCompression(), subIndex), FailureIndices.Iz5);
						subIndex++;
					}
				}
				else if (i == 5)
				{
					int subdivision = zoneSubdivision[i] + 1;

					double chiSx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminConcrete); // valore curvatura estremo Dx del campo i-esimo

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + j * (chiDx - chiSx) / subdivision;
						strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Shape.Fill[dMaxVertexIndex], teta, chi, 
							CalculateUltimateStrainConcreteCompression(), subIndex), FailureIndices.Iz6);
						subIndex++;
					}
				}
				else if (i == 6)
				{
					int subdivision = zoneSubdivision[i] + 1;

					Point2d strainPlaneCenter = new Point2d((dmaxConcrete - (3.0 / 7.0) * (dmaxConcrete - dminConcrete)) * (-sinTeta) + ConcreteSection.Centroid.X,
						(dmaxConcrete - (3.0 / 7.0) * (dmaxConcrete - dmaxConcrete - dminConcrete)) * (cosTeta) + ConcreteSection.Centroid.Y);

					double chiSx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminConcrete);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = 0.0; // valore curvatura estremo Dx del campo i-esimo

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + j * (chiDx - chiSx) / subdivision;
						strainPlanes[subIndex] = (new StrainPlane(strainPlaneCenter, teta, chi, CalculateLimitStrainCostantCompression(), subIndex), FailureIndices.Iz7);
						subIndex++;
					}
					strainPlanes[subIndex] = (new StrainPlane(strainPlaneCenter, teta, chiDx, CalculateLimitStrainCostantCompression(), subIndex), FailureIndices.Iz7);
				}
				else
					throw new ArgumentException();
			}
			
			return strainPlanes;
		}

		/// <summary>
		/// Return the strain value of the <paramref name="pointToTest"/>
		/// </summary>
		protected double CalculateStrain(StrainPlane strainPlane, Point3d pointToTest)
		{
			return strainPlane.StrainReferencePoint - strainPlane.Chi * ((pointToTest.Y - strainPlane.ReferencePoint.Y) * Math.Cos(strainPlane.Teta) -
				(pointToTest.X - strainPlane.ReferencePoint.X) * Math.Sin(strainPlane.Teta));
		}

		/// <summary>
		/// Calculate stress resultant for input <paramref name="face"/>
		/// </summary>
		/// <param name="face">The domain of integration</param>
		/// <param name="strainPlane">The strain plane</param>
		/// <returns></returns>
		protected virtual double CalculateConcreteStress(MeshFace face, StrainPlane strainPlane)
		{
			Point3d[] points = Mesh.GetFacePoints(face);

			if (face.IsTriangle)			
				return GaussIntegration.IntegrationTriangularLinearShapeFunction(GetConcreteStressFunction(strainPlane), points, 33);
			
			else if (face.IsQuad)			
				return GaussIntegration.IntegrationQuadrilateralLinearShapeFunction(GetConcreteStressFunction(strainPlane), points, 49);
			
			else
				throw new ArgumentException();
		}

		/// <summary>
		/// Calculate the stress resultant of the concrete part
		/// </summary>
		/// <param name="strainPlane">The strain plane</param>
		/// <param name="deltaN">The axial force resultant</param>
		/// <param name="deltaMx">The bending moment about X-axis resultant</param>
		/// <param name="deltaMy">The bending moment about Y-axis resultant</param>
		protected virtual void CalculateSolidStress(StrainPlane strainPlane, out double deltaN, out double deltaMx, out double deltaMy)
		{
			double[] deltaNArray = new double[Mesh.FacesCount];
			double[] deltaMxArray = new double[Mesh.FacesCount];
			double[] deltaMyArray = new double[Mesh.FacesCount];

			Parallel.For(0, Mesh.FacesCount, (i) =>
			{
				CalculateStressResultant(Mesh.Faces[i + 1], strainPlane, out double deltaNBuffer, out double deltaMxBuffer, out double deltaMyBuffer);

				deltaNArray[i] = deltaNBuffer;
				deltaMxArray[i] = deltaMxBuffer;
				deltaMyArray[i] = deltaMyBuffer;
			});

			deltaN = deltaNArray.Sum();
			deltaMx = deltaMxArray.Sum();
			deltaMy = deltaMyArray.Sum();
		}

		/// <summary>
		/// Calculate the resultant of all the rebars
		/// </summary>
		/// <param name="strainPlane">The strain plane</param>
		/// <param name="deltaN">The axial force resultant</param>
		/// <param name="deltaMx">The bending moment about X-axis resultant</param>
		/// <param name="deltaMy">The bending moment about Y-axis resultant</param>
		protected virtual void CalculateRebarsIntegration(StrainPlane strainPlane, out double deltaN, out double deltaMx, out double deltaMy)
		{
			double[] deltaNArray = new double[ConcreteSection.Rebars.Length];
			double[] deltaMxArray = new double[ConcreteSection.Rebars.Length];
			double[] deltaMyArray = new double[ConcreteSection.Rebars.Length];

			Parallel.For(0, ConcreteSection.Rebars.Length, (i) =>
			{
				double sigma = CalculateSigmaS(ConcreteSection.Rebars[i], CalculateStrain(strainPlane, ConcreteSection.Rebars[i].Position));

				if (sigma < 0)
				{
					deltaNArray[i] = sigma * ConcreteSection.Rebars[i].Area - sigma / ConcreteSection.CalculateN(ConcreteSection.Rebars[i]) * ConcreteSection.Rebars[i].Area;
					deltaMxArray[i] = sigma * ConcreteSection.Rebars[i].Area * (ConcreteSection.Rebars[i].Position.Y - ConcreteSection.Centroid.Y) -
						sigma / ConcreteSection.CalculateN(ConcreteSection.Rebars[i]) * ConcreteSection.Rebars[i].Area *
						(ConcreteSection.Rebars[i].Position.Y - ConcreteSection.Centroid.Y);
					deltaMyArray[i] = sigma * ConcreteSection.Rebars[i].Area * (ConcreteSection.Rebars[i].Position.X - ConcreteSection.Centroid.X) -
						sigma / ConcreteSection.CalculateN(ConcreteSection.Rebars[i]) * ConcreteSection.Rebars[i].Area *
						(ConcreteSection.Rebars[i].Position.X - ConcreteSection.Centroid.X);
				}

				else
				{
					deltaNArray[i] = sigma * ConcreteSection.Rebars[i].Area;
					deltaMxArray[i] = sigma * ConcreteSection.Rebars[i].Area * (ConcreteSection.Rebars[i].Position.Y - ConcreteSection.Centroid.Y);
					deltaMyArray[i] = sigma * ConcreteSection.Rebars[i].Area * (ConcreteSection.Rebars[i].Position.X - ConcreteSection.Centroid.X);
				}
			});

			deltaN = deltaNArray.Sum();
			deltaMx = deltaMxArray.Sum();
			deltaMy = deltaMyArray.Sum();
		}

		Func<double, double, double> GetConcreteStressFunction(StrainPlane strainPlane)
		{
			return (x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0)));
		}

		Func<double, double, double> GetSteelStressFunction(ReinforcedConcreteRebar rebar, StrainPlane strainPlane)
		{
			return (x, y) => CalculateSigmaS(rebar, CalculateStrain(strainPlane, new Point3d(x, y, 0)));
		}



		public virtual FailureDomain CalculateFailureDomain(int horizontalNumberOfDivision, int[] verticalNumberOfDivision)
		{
			horizontalNumberOfDivision++;

			if (horizontalNumberOfDivision < 2 || verticalNumberOfDivision.Sum() < 6)
				throw new ArgumentException();

			double deltaTeta = 2 * Math.PI / horizontalNumberOfDivision;
			FailureDomain.FailureDomainPoint[][] domainPoints = new FailureDomain.FailureDomainPoint[horizontalNumberOfDivision][];

			Parallel.For(0, horizontalNumberOfDivision, (i) =>
			{
				StrainPlane[] strainPlanes = CalculateAllDesignStrainPlanes((i * deltaTeta), verticalNumberOfDivision);
				domainPoints[i] = new FailureDomain.FailureDomainPoint[strainPlanes.Length];

				Parallel.For(0, strainPlanes.Length, (j) =>
				{
					domainPoints[i][j] = CalculatePlasticResistance(strainPlanes[j], out double _, out double _, out double _);
				});
			});

			return new FailureDomain(domainPoints);
		}

		protected virtual FailureDomain.FailureDomainPoint CalculatePlasticResistance(StrainPlane strainPlane, out double N, out double Mx, out double My)
		{
			CalculateSolidStress(strainPlane, out double deltaNConcrete, out double deltaMxConcrete, out double deltaMyConcrete);
			CalculateRebarsIntegration(strainPlane, out double deltaNRebar, out double deltaMxRebar, out double deltaMyRebar);

			N = deltaNConcrete + deltaNRebar;
			Mx = deltaMxConcrete + deltaMxRebar;
			My = deltaMyConcrete + deltaMyRebar;

			return new FailureDomain.FailureDomainPoint(N, Mx, My, FailureIndices.Iz1, strainPlane);
		}

		protected virtual void CalculateStressResultant(MeshFace face, StrainPlane strainPlane, out double deltaN, out double deltaMx, out double deltaMy)
		{
			Point3d faceCentroid = Mesh.GetFaceCentroid(face);
			double concreteStress = CalculateConcreteStress(face, strainPlane);

			deltaN = concreteStress;
			deltaMx = + concreteStress * (faceCentroid.Y - ConcreteSection.Centroid.Y);
			deltaMy = + concreteStress * (faceCentroid.X - ConcreteSection.Centroid.X);			
		}


		//protected virtual double CalculateSteelStressLine(Point3d p1, Point3d p2, double thickness, StrainPlane strainPlane)
		//{
		//	return GaussIntegration.IntegrationLineLinearShapeFunction(GetSteelStressFunction(strainPlane), new Point3d[] { p1, p2 }, 32) * thickness;
		//}
	}
}
