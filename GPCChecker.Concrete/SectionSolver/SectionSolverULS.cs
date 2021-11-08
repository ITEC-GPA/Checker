using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.SectionSolver
{
	public abstract class SectionSolverULS : SectionSolver
	{
		public enum FailureIndices
		{
			Iz1 = 1,
			Iz2 = 2,
			Iz3 = 3,
			Iz4 = 4,
			Iz5 = 5,
			Iz6 = 6,
			Iz7 = 7,
		}


		#region Public Constructor

		public SectionSolverULS(IConcreteSection section, Standard standard)
			:base(section, standard)
		{

		}

		public SectionSolverULS(SerializationInfo info, StreamingContext context)
			:base(info, context)
		{
			
		}

		#endregion

		#region Solver

		/// <summary>
		/// Calculate the failure domain <see cref="FailureDomain"/> of the section
		/// </summary>
		public FailureDomain CalculateFailureDomain()
		{
			return CalculateFailureDomain(SectionSolverOptions.Instance.MomentsDiscretizations, SectionSolverOptions.Instance.AxialForceDiscretizations);
		}

		#region Protected Method

		/// <summary>
		/// Calculate the failure domain <see cref="FailureDomain"/> of the section
		/// </summary>
		/// <param name="momentsDiscretizations">Number of discretizations of X-axis and Y-axis (moment around Z-axis)</param>
		/// <param name="normalDiscretizations">Number of discretizations of Z-axis (axial force)</param>
		/// <returns></returns>
		protected virtual FailureDomain CalculateFailureDomain(int momentsDiscretizations, (SectionSolverULS.FailureIndices, int)[] normalDiscretizations)
		{
			if (momentsDiscretizations < 2 || normalDiscretizations.Select(i => i.Item2).Sum() < 7)
				throw new ArgumentException();

			double deltaTeta = 2 * Math.PI / (momentsDiscretizations);
			momentsDiscretizations++;

			FailureDomain.FailureDomainPoint[][] domainPoints = new FailureDomain.FailureDomainPoint[momentsDiscretizations][];
			(StrainPlane, FailureIndices)[][] strainPlanes = new (StrainPlane, FailureIndices)[momentsDiscretizations][];

			try
			{
				Parallel.For(0, momentsDiscretizations, (i) =>
				{
					strainPlanes[i] = CalculateAllDesignStrainPlanes((i * deltaTeta), normalDiscretizations);
					domainPoints[i] = new FailureDomain.FailureDomainPoint[strainPlanes[i].Length];
				});

				Parallel.For(0, momentsDiscretizations, (i) =>
				{
					Parallel.For(0, strainPlanes[i].Length, (j) =>
					{
						domainPoints[i][j] = CalculatePlasticResistance(strainPlanes[i][j]);
					});
				});
			}
			catch (Exception e)
			{
				_log.Add($"Fail to calculate ULS strain planes" + e.InnerException);
			}

			return new FailureDomain(domainPoints);
		}

		/// <summary>
		/// Calculate the strain planes for angle <paramref name="teta"/>
		/// </summary>
		/// <param name="teta">The angle of rotation of the axis</param>
		/// <param name="zoneSubdivision">Number of subdivision for each failure zone</param>
		/// <returns></returns>
		protected virtual (StrainPlane, FailureIndices)[] CalculateAllDesignStrainPlanes(double teta, (SectionSolverULS.FailureIndices, int)[] zoneSubdivision)
		{
			if (zoneSubdivision.Length != 7)
				throw new ArgumentException("Subdivision must have 6 elements");

			(StrainPlane, FailureIndices)[] strainPlanes = new (StrainPlane, FailureIndices)[zoneSubdivision.Select(i => i.Item2).Sum() + 7 + 1];
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
					int subdivision = zoneSubdivision[i].Item2 + 1;

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
					int subdivision = zoneSubdivision[i].Item2 + 1;

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
					int subdivision = zoneSubdivision[i].Item2 + 1;

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
					int subdivision = zoneSubdivision[i].Item2 + 1;

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
					int subdivision = zoneSubdivision[i].Item2 + 1;

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
					int subdivision = zoneSubdivision[i].Item2 + 1;

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
					int subdivision = zoneSubdivision[i].Item2 + 1;

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
		/// Calculate the <see cref="FailureDomain.FailureDomainPoint"/> respect the strain plane <paramref name="strainPlane"/>
		/// </summary>
		/// <param name="strainPlane"></param>
		/// <param name="N"></param>
		/// <param name="Mx"></param>
		/// <param name="My"></param>
		/// <returns></returns>
		protected virtual FailureDomain.FailureDomainPoint CalculatePlasticResistance((StrainPlane, FailureIndices) strainPlane)
		{
			try
			{
				CalculateConcreteStressResultant(strainPlane.Item1, out double deltaNConcrete, out double deltaMxConcrete, out double deltaMyConcrete);
				CalculateRebarsIntegration(strainPlane.Item1, out double deltaNRebar, out double deltaMxRebar, out double deltaMyRebar);

				double N = deltaNConcrete + deltaNRebar;
				double Mx = -(deltaMxConcrete + deltaMxRebar);
				double My = deltaMyConcrete + deltaMyRebar;

				CalculateExternalForces(N, Mx, My, SectionSolverOptions.Instance.DistanceFromCentroid, out N, out Mx, out My);

				return new FailureDomain.FailureDomainPoint(N, Mx, My, strainPlane.Item2, strainPlane.Item1);
			}
			catch (Exception e)
			{
				_log.Add($"Fail" + e.InnerException);
			}
			return new FailureDomain.FailureDomainPoint(0, 0, 0, FailureIndices.Iz1, strainPlane.Item1);
		}

		#endregion

		#endregion

		#region Public override methods

		public override int GetHashCode()
		{
			unchecked
			{
				return 23 + EqualityComparer<IConcreteSection>.Default.GetHashCode(_concreteSection);
			}
		}

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
		}

		#endregion
	}
}
