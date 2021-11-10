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
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using MathNet.Numerics.LinearAlgebra;

namespace GPC.Checkers.Concrete.SectionSolvers
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

		public virtual double CalculateSafetyFactor(ResultBeamForces forces, out FailureDomain.FailureDomainPoint pointOnDomain)
		{
			return CalculateSafetyFactor(forces, out pointOnDomain, SectionSolverOptions.Instance.ULSconvergenceTolerance);
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

		protected virtual double CalculateSafetyFactor(ResultBeamForces forces, out FailureDomain.FailureDomainPoint pointOnDomain, double angularTolerance = 0.001)
		{
			forces = CalculateExternalForces(forces, SectionSolverOptions.Instance.DistanceFromCentroid);
			//CalculateAdimensionalForces(forces, out double adimExternalAxialForce, out double adimExternalendingMomentX, out double adimExternalBendingMomentY);

			// piano di primo tentativo. campo di rottura 7, immersione di 0.9 e ruotato di teta = 0;
			double immersione = 0.9;
			double teta;
			if (forces.M1 > 0)
				teta = 0;
			else
				teta = Math.PI / 2.0;

			FailureIndices failureIndex = FailureIndices.Iz7;
			StrainPlane strainPlane = CalculateStrainPlane(teta, failureIndex, immersione);

			// Valori di primo tentativo
			teta = strainPlane.Teta;
			int id = 1;

			CalculateForces(strainPlane, out double NRd, out double MxRd, out double MyRd);

			Vector3d vectorForcesEd = new Vector3d(forces.M1, forces.M2, forces.N);

			double deltaTeta;
			double deltaImmersione;

			Vector3d vectorRd = new Vector3d(MxRd, MyRd, NRd);
			double angle = vectorForcesEd.AngleTo(vectorRd);

			do
			{
				try
				{
					CalculateIncrement(NRd, MxRd, MyRd, strainPlane, failureIndex, immersione, vectorForcesEd, angle, out deltaTeta, out deltaImmersione);
				}
				catch (Exception e)
				{
					_log.Add($"Fail to calculate increment, {e.Message}");
					throw new Exception("Fail to calculate increment");
				}

				// piano di nuovo tentativo
				id++;
				teta += deltaTeta;

				if (angle > 4 * angularTolerance)
				{
					immersione += (deltaImmersione - (int)deltaImmersione);
					failureIndex += (int)deltaImmersione;

					if (immersione < 0.0)
					{
						immersione++;
						failureIndex--;
					}
					if (immersione > 1.0)
					{
						immersione--;
						failureIndex++;
					}

					failureIndex = (int)failureIndex < 1 ? FailureIndices.Iz1 : failureIndex;
					failureIndex = (int)failureIndex > 7 ? FailureIndices.Iz7 : failureIndex;

					strainPlane = CalculateStrainPlane(teta, failureIndex, immersione, id);
					CalculateForces(strainPlane, out NRd, out MxRd, out MyRd);

					vectorRd = new Vector3d(MxRd, MyRd, NRd);
					angle = vectorForcesEd.AngleTo(vectorRd);
				}
				else
				{
					double immersioneBuffer1 = immersione + (deltaImmersione - (int)deltaImmersione);
					FailureIndices failureIndexBuffer1 = failureIndex + (int)deltaImmersione;

					if (immersioneBuffer1 < 0.0)
					{
						immersioneBuffer1++;
						failureIndexBuffer1--;
					}
					if (immersioneBuffer1 > 1.0)
					{
						immersioneBuffer1--;
						failureIndexBuffer1++;
					}

					failureIndexBuffer1 = (int)failureIndexBuffer1 < 1 ? FailureIndices.Iz1 : failureIndexBuffer1;
					failureIndexBuffer1 = (int)failureIndexBuffer1 > 7 ? FailureIndices.Iz7 : failureIndexBuffer1;

					StrainPlane strainPlaneBuffer1 = CalculateStrainPlane(teta, failureIndexBuffer1, immersioneBuffer1, id);
					CalculateForces(strainPlaneBuffer1, out double NRdB1, out double MxRdB1, out double MyRdB1);

					Vector3d vectorRdB1 = new Vector3d(MxRdB1, MyRdB1, NRdB1);
					double angleB1 = vectorForcesEd.AngleTo(vectorRdB1);


					double immersioneBuffer2 = immersione - (deltaImmersione - (int)deltaImmersione);
					FailureIndices failureIndexBuffer2 = failureIndex - (int)deltaImmersione;

					if (immersioneBuffer2 < 0.0)
					{
						immersioneBuffer2++;
						failureIndexBuffer2--;
					}
					if (immersioneBuffer2 > 1.0)
					{
						immersioneBuffer2--;
						failureIndexBuffer2++;
					}

					failureIndexBuffer2 = (int)failureIndexBuffer2 < 1 ? FailureIndices.Iz1 : failureIndexBuffer2;
					failureIndexBuffer2 = (int)failureIndexBuffer2 > 7 ? FailureIndices.Iz7 : failureIndexBuffer2;

					StrainPlane strainPlaneBuffer2 = CalculateStrainPlane(teta, failureIndexBuffer2, immersioneBuffer2, id);
					CalculateForces(strainPlaneBuffer2, out double NRdB2, out double MxRdB2, out double MyRdB2);

					Vector3d vectorRdB2 = new Vector3d(MxRdB2, MyRdB2, NRdB2);
					double angleB2 = vectorForcesEd.AngleTo(vectorRdB2);

					if (Math.Abs(angleB1) < Math.Abs(angleB2))
					{
						strainPlane = strainPlaneBuffer1;
						immersione = immersioneBuffer1;

						NRd = NRdB1;
						MxRd = MxRdB1;
						MyRd = MyRdB1;

						vectorRd = vectorRdB1;
						angle = angleB1;
					}
					else
					{
						strainPlane = strainPlaneBuffer2;
						immersione = immersioneBuffer2;

						NRd = NRdB2;
						MxRd = MxRdB2;
						MyRd = MyRdB2;

						vectorRd = vectorRdB2;
						angle = angleB2;
					}
				}


			} while (Math.Abs(angle) > angularTolerance);

			pointOnDomain = new FailureDomain.FailureDomainPoint(NRd, MxRd, MyRd, failureIndex, strainPlane);

			return vectorForcesEd.Length / vectorRd.Length;
		}

		protected void CalculateIncrement(double NRd, double MxRd, double MyRd, StrainPlane inputStrainPlane, FailureIndices failureIndex,
					double immersioneNelCampo, Vector3d externalForces, double deltaAngle,
					out double deltaTeta, out double deltaImmersione)
		{
			Point3d iterationPoint = new Point3d(MxRd, MyRd, NRd);

			Line3d externalForcesLine = new Line3d(new Point3d(0, 0, 0), new Point3d(externalForces.X, externalForces.Y, externalForces.Z));

			double dTeta = 0.01;
			double dImmersione = 0.01; // * Math.Min(deltaAngle, 0.1); 

			// derivate parziali rispetto a teta
			StrainPlane strainPlanePlusdTeta = CalculateStrainPlane(inputStrainPlane.Teta + dTeta, failureIndex, immersioneNelCampo);
			StrainPlane strainPlaneMinusdTeta = CalculateStrainPlane(inputStrainPlane.Teta - dTeta, failureIndex, immersioneNelCampo);

			CalculateForces(strainPlanePlusdTeta, out double NPlusdTeta, out double MxPlusdTeta, out double MyPlusdTeta);
			CalculateForces(strainPlaneMinusdTeta, out double NMinusdTeta, out double MxMinusdTeta, out double MyMinusdTeta);

			double dNdTeta = (NPlusdTeta - NMinusdTeta) / (2.0 * dTeta);
			double dMxdTeta = (MxPlusdTeta - MxMinusdTeta) / (2.0 * dTeta);
			double dMydTeta = (MyPlusdTeta - MyMinusdTeta) / (2.0 * dTeta);

			Vector3d v1 = new Vector3d(dMxdTeta, dMydTeta, dNdTeta);
			v1.Unitize();

			// derivate parziali rispetto a immersione nel campo
			StrainPlane strainPlanePlusdImm = CalculateStrainPlane(inputStrainPlane.Teta, failureIndex, Math.Min(immersioneNelCampo + dImmersione, 1.0));
			StrainPlane strainPlaneMinusdImm = CalculateStrainPlane(inputStrainPlane.Teta, failureIndex, Math.Max(immersioneNelCampo - dImmersione, 0.0));

			CalculateForces(strainPlanePlusdImm, out double NPlusdImm, out double MxPlusdImm, out double MyPlusdImm);
			CalculateForces(strainPlaneMinusdImm, out double NMinusdImm, out double MxMinusdImm, out double MyMinusdImm);

			double dNdImm = (NPlusdImm - NMinusdImm) / (2.0 * dImmersione);
			double dMxdImm = (MxPlusdImm - MxMinusdImm) / (2.0 * dImmersione);
			double dMydImm = (MyPlusdImm - MyMinusdImm) / (2.0 * dImmersione);

			Vector3d v2 = new Vector3d(dMxdImm, dMydImm, dNdImm);
			v2.Unitize();

			// vettore uscente dal punto M di test
			Vector3d gradient = v1 ^ v2;
			gradient.Unitize();

			// vettore che indica la direzione dell'incremento
			Vector3d s = gradient ^ (externalForces ^ gradient);
			s.Unitize();

			// k dell'equazione del piano tangente alla superficie in M
			// A*x + B*y + C*z + k = 0
			double k = -(gradient.X * iterationPoint.X + gradient.Y * iterationPoint.Y + gradient.Z * iterationPoint.Z);

			// piano tangente 
			Plane tangentPlane = new Plane(gradient.X, gradient.Y, gradient.Z, k);

			// punto di intersezione tra raggio delle forze sollecitanti e il piano tangente
			bool intersect = tangentPlane.IntersectWithRay(externalForcesLine, out Point3d intersectionPoint);

			double distanceToTarget;

			if (!intersect)
			{
				deltaTeta = 0.0;
				deltaImmersione = 0.25;
				return;
			}
			else
				distanceToTarget = iterationPoint.DistanceTo(intersectionPoint);

			Matrix<double> partialDerivatives = Matrix<double>.Build.Dense(2, 2);

			partialDerivatives[0, 0] = dMxdTeta;
			partialDerivatives[1, 0] = dNdTeta;

			partialDerivatives[0, 1] = dMxdImm;
			partialDerivatives[1, 1] = dNdImm;

			Matrix<double> inputVector = Matrix<double>.Build.Dense(2, 1);
			inputVector[0, 0] = s.X * distanceToTarget;
			inputVector[1, 0] = s.Z * distanceToTarget;

			Matrix<double> results = partialDerivatives.Inverse() * inputVector;

			deltaTeta = results[0, 0];
			deltaImmersione = results[1, 0] / 2.0;
		}

		protected virtual StrainPlane CalculateStrainPlane(double teta, SectionSolverULS.FailureIndices failureIndex, double immersioneNelCampo, int id = -1)
		{
			if (immersioneNelCampo > 1.0 || immersioneNelCampo < 0.0)
				throw new ArgumentException("ImmersioneNelCampo cannot be greater than 1 and less than 0");

			SolverHelper.CalculateRelativeDistance(ConcreteSection, teta, out int dMinRebarIndex, out int dMaxRebarIndex, out int dMinVertexIndex, out int dMaxVertexIndex);

			double dmaxConcrete = (ConcreteSection.Shape.Fill[dMaxVertexIndex].Y - ConcreteSection.Centroid.Y) * Math.Cos(teta) -
				(ConcreteSection.Shape.Fill[dMaxVertexIndex].X - ConcreteSection.Centroid.X) * Math.Sin(teta);

			double dminConcrete = (ConcreteSection.Shape.Fill[dMinVertexIndex].Y - ConcreteSection.Centroid.Y) * Math.Cos(teta) -
				(ConcreteSection.Shape.Fill[dMinVertexIndex].X - ConcreteSection.Centroid.X) * Math.Sin(teta);

			double dminSteel = (ConcreteSection.Rebars[dMinRebarIndex].Position.Y - ConcreteSection.Centroid.Y) * Math.Cos(teta) -
				(ConcreteSection.Rebars[dMinRebarIndex].Position.X - ConcreteSection.Centroid.X) * Math.Sin(teta);

			if (failureIndex == FailureIndices.Iz1)
			{
				double chiSx = 0;   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = CalculateUltimateStrainSteel(dMinRebarIndex) / (dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Rebars[dMinRebarIndex].Position, teta, chi, CalculateUltimateStrainSteel(dMinRebarIndex), id);
			}
			else if (failureIndex == FailureIndices.Iz2)
			{
				double chiSx = CalculateUltimateStrainSteel(dMinRebarIndex) / (dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = (CalculateUltimateStrainSteel(dMinRebarIndex) + Math.Abs(CalculateYeldingStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Rebars[dMinRebarIndex].Position, teta, chi, CalculateUltimateStrainSteel(dMinRebarIndex), id);
			}
			else if (failureIndex == FailureIndices.Iz3)
			{
				double chiSx = (CalculateUltimateStrainSteel(dMinRebarIndex) + Math.Abs(CalculateYeldingStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = (CalculateUltimateStrainSteel(dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Rebars[dMinRebarIndex].Position, teta, chi, CalculateUltimateStrainSteel(dMinRebarIndex), id);
			}
			else if (failureIndex == FailureIndices.Iz4)
			{
				double chiSx = (CalculateUltimateStrainSteel(dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = (CalculateYeldingStrainSteel(dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Shape.Fill[dMaxVertexIndex], teta, chi, CalculateUltimateStrainConcreteCompression(), id);

			}
			else if (failureIndex == FailureIndices.Iz5)
			{
				double chiSx = (CalculateYeldingStrainSteel(dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Shape.Fill[dMaxVertexIndex], teta, chi, CalculateUltimateStrainConcreteCompression(), id);
			}
			else if (failureIndex == FailureIndices.Iz6)
			{
				double chiSx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminConcrete); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Shape.Fill[dMaxVertexIndex], teta, chi, CalculateUltimateStrainConcreteCompression(), id);
			}
			else if (failureIndex == FailureIndices.Iz7)
			{
				Point2d strainPlaneCenter = new Point2d((dmaxConcrete - (3.0 / 7.0) * (dmaxConcrete - dminConcrete)) * (-Math.Sin(teta)) + ConcreteSection.Centroid.X,
					(dmaxConcrete - (3.0 / 7.0) * (dmaxConcrete - dmaxConcrete - dminConcrete)) * (Math.Cos(teta)) + ConcreteSection.Centroid.Y);

				double chiSx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminConcrete);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = 0.0; // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(strainPlaneCenter, teta, chi, CalculateLimitStrainCostantCompression(), id);
			}
			else
				throw new ArgumentException();
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
