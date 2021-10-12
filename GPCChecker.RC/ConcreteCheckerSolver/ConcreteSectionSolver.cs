using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Model.Materials;
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

		public ConcreteSectionSolver(IConcreteSection section)
		{
			_concreteSection = section;
		}






		protected virtual void CalculatePlasticResistance(StrainPlane strainPlane)
		{

		}

		public virtual StrainPlane[] CalculateAllDesignStrainPlanes(double teta, int subdivision)
		{
			StrainPlane[] strainPlanes = new StrainPlane[6 * subdivision];
			double cosTeta = Math.Cos(teta);
			double sinTeta = Math.Sin(teta);

			// per ogni indice di campo - 1
			for (int i = 0; i < 6; i++)
			{
				if (i == 0)   // campo 1
				{
					double dminSteel = double.MaxValue;
					double dmaxSteel = double.MinValue;
					double dmaxConcrete = double.MinValue;

					Point2d rebarPosition = new Point2d(double.MaxValue, double.MaxValue);
					int rebarIndex = int.MinValue;

					for (int r = 0; r < ConcreteSection.Rebars.Count(); r++)
					{
						double w1 = ConcreteSection.Rebars[r].Position.Y * cosTeta - ConcreteSection.Rebars[r].Position.X * sinTeta;
						if (w1 <= dminSteel)
						{
							dminSteel = w1;
							rebarPosition = ConcreteSection.Rebars[r].Position;
							rebarIndex = r;
						}

						if (w1 >= dmaxSteel)
							dmaxSteel = w1;
					}

					//TODO: implementare con armature lineari

					for (int c = 0; c < ConcreteSection.Shape.Fill.Count; c++)
					{
						double w1 = ConcreteSection.Shape.Fill[c].Y * cosTeta - ConcreteSection.Shape.Fill[c].X * sinTeta;

						if (w1 >= dmaxConcrete)
							dmaxConcrete = w1;
					}

					double chiSx = 0;   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = ConcreteSection.Rebars[rebarIndex].RebarMaterial.EpsilonU / (dmaxSteel - dminSteel); // valore curvatura estremo Dx del campo i-esimo

					double deltaEta = (chiSx - chiDx) / subdivision;

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + (subdivision * deltaEta) * (chiDx - chiSx);
						strainPlanes[i * subdivision + j] = new StrainPlane(rebarPosition, teta, chi, ConcreteSection.Rebars[rebarIndex].RebarMaterial.EpsilonU, FailureIndices.Iz1);
					}
				}
				else if (i == 1)
				{
					double dminSteel = double.MaxValue;
					double dmaxConcrete = double.MinValue;

					Point2d strainPlaneCenter = new Point2d(double.MaxValue, double.MaxValue);
					int rebarIndex = int.MinValue;

					for (int r = 0; r < ConcreteSection.Rebars.Count(); r++)
					{
						double w1 = ConcreteSection.Rebars[r].Position.Y * cosTeta - ConcreteSection.Rebars[r].Position.X * sinTeta;
						if (w1 <= dminSteel)
						{
							dminSteel = w1;
							strainPlaneCenter = ConcreteSection.Rebars[r].Position;
							rebarIndex = r;
						}
					}

					for (int c = 0; c < ConcreteSection.Shape.Fill.Count; c++)
					{
						double w1 = ConcreteSection.Shape.Fill[c].Y * cosTeta - ConcreteSection.Shape.Fill[c].X * sinTeta;

						if (w1 >= dmaxConcrete)
							dmaxConcrete = w1;
					}

					//TODO: implementare con armature lineari

					double chiSx = ConcreteSection.Rebars[rebarIndex].RebarMaterial.EpsilonU / (dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = ConcreteSection.Rebars[rebarIndex].RebarMaterial.EpsilonU + ConcreteSection.ConcreteMaterial.EpsilonU / (dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

					double deltaEta = (chiSx - chiDx) / subdivision;

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + (subdivision * deltaEta) * (chiDx - chiSx);
						strainPlanes[i * subdivision + j] = new StrainPlane(strainPlaneCenter, teta, chi, ConcreteSection.Rebars[rebarIndex].RebarMaterial.EpsilonU, FailureIndices.Iz2);
					}
				}
				else if (i == 2)
				{
					double dminSteel = double.MaxValue;
					double dmaxConcrete = double.MinValue;

					Point2d strainPlaneCenter = new Point2d(double.MaxValue, double.MaxValue);
					int rebarIndex = int.MinValue;

					for (int r = 0; r < ConcreteSection.Rebars.Count(); r++)
					{
						double w1 = ConcreteSection.Rebars[r].Position.Y * cosTeta - ConcreteSection.Rebars[r].Position.X * sinTeta;
						if (w1 <= dminSteel)
						{
							dminSteel = w1;
							rebarIndex = r;
						}
					}

					for (int c = 0; c < ConcreteSection.Shape.Fill.Count; c++)
					{
						double w1 = ConcreteSection.Shape.Fill[c].Y * cosTeta - ConcreteSection.Shape.Fill[c].X * sinTeta;

						if (w1 >= dmaxConcrete)
						{
							dmaxConcrete = w1;
							strainPlaneCenter = ConcreteSection.Shape.Fill[c];
						}
					}

					//TODO: implementare con armature lineari

					double chiSx = ConcreteSection.Rebars[rebarIndex].RebarMaterial.EpsilonU + ConcreteSection.ConcreteMaterial.EpsilonU / (dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = ConcreteSection.Rebars[rebarIndex].RebarMaterial.EpsilonY + ConcreteSection.ConcreteMaterial.EpsilonU / (dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

					double deltaEta = (chiSx - chiDx) / subdivision;

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + (subdivision * deltaEta) * (chiDx - chiSx);
						strainPlanes[i * subdivision + j] = new StrainPlane(strainPlaneCenter, teta, chi, ConcreteSection.ConcreteMaterial.EpsilonU, FailureIndices.Iz3);
					}
				}
				else if (i == 3)
				{
					double dminSteel = double.MaxValue;
					double dmaxConcrete = double.MinValue;

					Point2d strainPlaneCenter = new Point2d(double.MaxValue, double.MaxValue);
					int rebarIndex = int.MinValue;

					for (int r = 0; r < ConcreteSection.Rebars.Count(); r++)
					{
						double w1 = ConcreteSection.Rebars[r].Position.Y * cosTeta - ConcreteSection.Rebars[r].Position.X * sinTeta;
						if (w1 <= dminSteel)
						{
							dminSteel = w1;
							rebarIndex = r;
						}
					}

					for (int c = 0; c < ConcreteSection.Shape.Fill.Count; c++)
					{
						double w1 = ConcreteSection.Shape.Fill[c].Y * cosTeta - ConcreteSection.Shape.Fill[c].X * sinTeta;

						if (w1 >= dmaxConcrete)
						{
							dmaxConcrete = w1;
							strainPlaneCenter = ConcreteSection.Shape.Fill[c];
						}
					}

					//TODO: implementare con armature lineari

					double chiSx = ConcreteSection.Rebars[rebarIndex].RebarMaterial.EpsilonY + ConcreteSection.ConcreteMaterial.EpsilonU / (dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = ConcreteSection.ConcreteMaterial.EpsilonU / (dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

					double deltaEta = (chiSx - chiDx) / subdivision;

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + (subdivision * deltaEta) * (chiDx - chiSx);
						strainPlanes[i * subdivision + j] = new StrainPlane(strainPlaneCenter, teta, chi, ConcreteSection.ConcreteMaterial.EpsilonU, FailureIndices.Iz4);
					}
				}
				else if (i == 4)
				{
					double dminSteel = double.MaxValue;
					double dmaxConcrete = double.MinValue;
					double dminConcrete = double.MinValue;

					Point2d strainPlaneCenter = new Point2d(double.MaxValue, double.MaxValue);

					for (int r = 0; r < ConcreteSection.Rebars.Count(); r++)
					{
						double w1 = ConcreteSection.Rebars[r].Position.Y * cosTeta - ConcreteSection.Rebars[r].Position.X * sinTeta;

						if (w1 <= dminSteel)
							dminSteel = w1;
					}

					for (int c = 0; c < ConcreteSection.Shape.Fill.Count; c++)
					{
						double w1 = ConcreteSection.Shape.Fill[c].Y * cosTeta - ConcreteSection.Shape.Fill[c].X * sinTeta;

						if (w1 >= dmaxConcrete)
						{
							dmaxConcrete = w1;
							strainPlaneCenter = ConcreteSection.Shape.Fill[c];
						}
						if (w1 <= dminConcrete)
							dminConcrete = w1;
					}

					//TODO: implementare con armature lineari

					double chiSx = ConcreteSection.ConcreteMaterial.EpsilonU / (dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = ConcreteSection.ConcreteMaterial.EpsilonU / (dmaxConcrete - dminConcrete); // valore curvatura estremo Dx del campo i-esimo

					double deltaEta = (chiSx - chiDx) / subdivision;

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + (subdivision * deltaEta) * (chiDx - chiSx);
						strainPlanes[i * subdivision + j] = new StrainPlane(strainPlaneCenter, teta, chi, ConcreteSection.ConcreteMaterial.EpsilonU, FailureIndices.Iz5);
					}
				}
				else if (i == 5)
				{
					double dmaxConcrete = double.MinValue;
					double dminConcrete = double.MinValue;

					for (int c = 0; c < ConcreteSection.Shape.Fill.Count; c++)
					{
						double w1 = ConcreteSection.Shape.Fill[c].Y * cosTeta - ConcreteSection.Shape.Fill[c].X * sinTeta;

						if (w1 >= dmaxConcrete)
							dmaxConcrete = w1;

						if (w1 <= dminConcrete)
							dminConcrete = w1;
					}

					Point2d strainPlaneCenter = new Point2d(dmaxConcrete - (3.0 / 7.0) * (dmaxConcrete - dmaxConcrete - dminConcrete) * (-sinTeta),
						dmaxConcrete - (3.0 / 7.0) * (dmaxConcrete - dmaxConcrete - dminConcrete) * (cosTeta));

					double chiSx = ConcreteSection.ConcreteMaterial.EpsilonU / (dmaxConcrete - dminConcrete);   // valore curvatura estremo Sx del campo i-esimo
					double chiDx = 0.0; // valore curvatura estremo Dx del campo i-esimo

					double deltaEta = (chiSx - chiDx) / subdivision;

					for (int j = 0; j < subdivision; j++)
					{
						double chi = chiSx + (subdivision * deltaEta) * (chiDx - chiSx);
						strainPlanes[i * subdivision + j] = new StrainPlane(strainPlaneCenter, teta, chi, ConcreteSection.ConcreteMaterial.EpsilonY, FailureIndices.Iz6);
					}
				}
				else
					throw new ArgumentException();
			}

			return strainPlanes;
		}


		protected abstract double CalculateSigmaC(double strain);

		protected abstract double CalculateSigmaS(double strain);

		protected virtual double CalculateIntegralCostantStressTriangleConcrete(Point2d p1, Point2d p2, Point2d p3, double sigma)
		{
			double area = 0.5 * ((p3.Y + p1.Y) * (p3.X - p1.X) - (p3.Y + p2.Y) * (p3.X - p2.X) - (p2.Y + p1.Y) * (p2.X - p1.X));
			return area * sigma;
		}

		protected virtual double CalculateIntegralVeriableStressTriangleConcrete(Point2d p1, Point2d p2, Point2d p3, StrainPlane strainPlane)
		{
			double resultant = 0.0;
			double[] weight = new double[] { -0.56250, 0.52083333333333, 0.52083333333333, 0.52083333333333 };

			double area = 0.5 * ((p3.Y + p1.Y) * (p3.X - p1.X) - (p3.Y + p2.Y) * (p3.X - p2.X) - (p2.Y + p1.Y) * (p2.X - p1.X));

			// primo punto semplice
			Point2d point1NC = new Point2d(p1.X + (p2.X - p1.X) / 3 + (p3.X - p1.X) / 3, p1.Y + (p2.Y - p1.Y) / 3 + (p3.Y - p1.Y) / 3);
			double point1Strain = CalculateStrain(strainPlane, point1NC);
			double sigmaCPoint1 = CalculateSigmaC(point1Strain);

			resultant += sigmaCPoint1 * (weight[0]);

			// secondo punto semplice
			Point2d point2NC = new Point2d(p1.X + (p2.X - p1.X) / 5 + (p3.X - p1.X) / 5, p1.Y + (p2.Y - p1.Y) / 5 + (p3.Y - p1.Y) / 5);
			double point2Strain = CalculateStrain(strainPlane, point2NC);
			double sigmaCPoint2 = CalculateSigmaC(point2Strain);

			resultant += sigmaCPoint2 * (weight[1]);

			// terzo punto semplice
			Point2d point3NC = new Point2d(p1.X + 3 * (p2.X - p1.X) / 5 + (p3.X - p1.X) / 5, p1.Y + 3 * (p2.Y - p1.Y) / 5 + (p3.Y - p1.Y) / 5);
			double point3Strain = CalculateStrain(strainPlane, point3NC);
			double sigmaCPoint3 = CalculateSigmaC(point3Strain);

			resultant += sigmaCPoint3 * (weight[2]);

			// quarto punto semplice
			Point2d point4NC = new Point2d(p1.X + (p2.X - p1.X) / 5 + 3 * (p3.X - p1.X) / 5, p1.Y + (p2.Y - p1.Y) / 5 + 3 * (p3.Y - p1.Y) / 5);
			double point4Strain = CalculateStrain(strainPlane, point4NC);
			double sigmaCPoint4 = CalculateSigmaC(point4Strain);

			resultant += sigmaCPoint4 * (weight[3]);

			return resultant * area;
		}

		protected virtual double CalculateIntegralVariableStressLineConcrete(Point2d p1, Point2d p2, double thickness, StrainPlane strainPlane)
		{
			double resultant = 0.0;
			double[] weight = new double[] { 0.5555555555, 0.8888888888, 0.5555555555 };

			// primo punto semplice
			Point2d point1NC = new Point2d(p1.X + (p2.X - p1.X) * (1 - 0.77459) / 2.0, p1.Y + (p2.Y - p1.Y) * (1 - 0.77459) / 2.0);
			double point1Strain = CalculateStrain(strainPlane, point1NC);
			double sigmaCPoint1 = CalculateSigmaC(point1Strain);

			resultant += sigmaCPoint1 * (weight[0]);

			// secondo punto semplice
			Point2d point2NC = new Point2d((p2.X - p1.X) / 2.0, (p2.Y - p1.Y) / 2.0);
			double point2Strain = CalculateStrain(strainPlane, point2NC);
			double sigmaCPoint2 = CalculateSigmaC(point2Strain);

			resultant += sigmaCPoint2 * (weight[1]);

			// primo punto semplice
			Point2d point3NC = new Point2d(p1.X + (p2.X - p1.X) * (1 + 0.77459) / 2.0, p1.Y + (p2.Y - p1.Y) * (1 + 0.77459) / 2.0);
			double point3Strain = CalculateStrain(strainPlane, point3NC);
			double sigmaCPoint3 = CalculateSigmaC(point3Strain);

			resultant += sigmaCPoint3 * (weight[2]);

			return resultant * thickness * p1.DistanceTo(p2);
		}

		protected virtual double CalculateIntegralCostantStressLine(Point2d p1, Point2d p2, double thickness, double sigma)
		{
			return p1.DistanceTo(p2) * thickness * sigma;
		}

		/// <summary>
		/// Return the strain value of the <paramref name="pointToTest"/>
		/// </summary>
		protected double CalculateStrain(StrainPlane strainPlane, Point2d pointToTest)
		{
			return strainPlane.EpsilonCenterOfStrainPlane - strainPlane.Chi * ((pointToTest.Y - strainPlane.CenterOfStrainPlane.Y) * Math.Cos(strainPlane.Teta) -
				(pointToTest.X - strainPlane.CenterOfStrainPlane.X) * Math.Sin(strainPlane.Teta));
		}


		/// <summary>
		/// The strain plane - epsilon(x,y) = epsilon0 - chi * ((-sin(teta)*x + cos(teta)*y)
		/// </summary>
		public struct StrainPlane
		{
			public StrainPlane(Point2d centerOfStrainPlane, double teta, double chi, double epsilonCenterOfStrainPlane, FailureIndices failureIndice)
			{
				CenterOfStrainPlane = centerOfStrainPlane;
				Teta = teta;
				Chi = chi;
				EpsilonCenterOfStrainPlane = epsilonCenterOfStrainPlane;
				FailureIndice = failureIndice;
			}

			/// <summary>
			/// The point where is set <see cref="EpsilonCenterOfStrainPlane"/>
			/// </summary>
			public Point2d CenterOfStrainPlane { get; }

			/// <summary>
			/// The angle of rotation of the strain plane respect the X-axis
			/// </summary>
			public double Teta { get; }

			/// <summary>
			/// The curvature of the strain plane
			/// </summary>
			public double Chi { get; }

			/// <summary>
			/// The value of the strain in the <see cref="CenterOfStrainPlane"/>
			/// </summary>
			public double EpsilonCenterOfStrainPlane { get; }

			public FailureIndices FailureIndice { get; }
		}
	}
}
