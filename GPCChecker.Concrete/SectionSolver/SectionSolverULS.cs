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

		public enum FailureZones
		{
			/// <summary>
			/// Around P1. From espSu to 0 
			/// </summary>
			F1  = 1,

			/// <summary>
			/// Around P1. From 0 to espCy 
			/// </summary>
			F2A = 2,

			/// <summary>
			/// Around P1. From espCy to espCu 
			/// </summary>
			F2B = 3,

			/// <summary>
			/// Around P2. From espSu to espSy 
			/// </summary>
			F3A = 4,

			/// <summary>
			/// Around P2. From espSy to 0 
			/// </summary>
			F3B = 5,

			/// <summary>
			/// Around P2. From 0 to 0 
			/// </summary>
			F4 = 6,

			/// <summary>
			/// Around P3. From 0 to espCyCost 
			/// </summary>
			F5 = 7,
		}



		public SectionSolverULS(IConcreteSection section, Standard standard)
			:base(section, standard)
		{

		}

		public SectionSolverULS(SerializationInfo info, StreamingContext context)
			:base(info, context)
		{
			
		}


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
		protected virtual FailureDomain CalculateFailureDomain(int momentsDiscretizations, (SectionSolverULS.FailureZones, int)[] normalDiscretizations)
		{
			if (momentsDiscretizations < 2)
				throw new ArgumentException();

			double deltaTeta = 2 * Math.PI / (momentsDiscretizations);
			momentsDiscretizations++;

			FailureDomain.FailureDomainPoint[][] domainPoints = new FailureDomain.FailureDomainPoint[momentsDiscretizations][];
			(StrainPlane, FailureZones)[][] strainPlanes = new (StrainPlane, FailureZones)[momentsDiscretizations][];

			try
			{
				Parallel.For(0, momentsDiscretizations, (i) =>
				{
					strainPlanes[i] = CalculateAllDesignStrainPlanes((i * deltaTeta), normalDiscretizations, GetStrainYCompression(), GetStrainUCompression());
					domainPoints[i] = new FailureDomain.FailureDomainPoint[strainPlanes[i].Length];

					Parallel.For(0, strainPlanes[i].Length, (j) =>
					{
						domainPoints[i][j] = CalculateForces(strainPlanes[i][j]);
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
		protected virtual (StrainPlane, FailureZones)[] CalculateAllDesignStrainPlanes(double teta, (SectionSolverULS.FailureZones, int)[] zoneSubdivision, 
														double strainYCompression, double strainUCompression)
		{

			(StrainPlane, FailureZones)[] strainPlanes = new (StrainPlane, FailureZones)[zoneSubdivision.Select(i => i.Item2).Sum() + zoneSubdivision.Length + 1];


            (int dMinRebarIndex, double dminRebar, int dMaxRebarIndex, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) sectionDistances 
				= CalculateMaxMinSectionDistances(teta);
						

			int subIndex = 0;

			double concreteRebarMaxDistance = sectionDistances.dmaxConcrete - sectionDistances.dminRebar;
			double concreteMaxDistance = sectionDistances.dmaxConcrete - sectionDistances.dminConcrete;
			double height = sectionDistances.dmaxConcrete + sectionDistances.dminConcrete;


			Point2d p3 = new Point2d((sectionDistances.dmaxConcrete + (height * (1.0 / Math.Abs(strainUCompression) - 1.0 / Math.Abs(strainYCompression)) * Math.Abs(strainYCompression))) * (-Math.Sin(teta)) + ConcreteSection.Centroid.X,
									 (sectionDistances.dmaxConcrete + (height * (1.0 / Math.Abs(strainUCompression) - 1.0 / Math.Abs(strainYCompression)) * Math.Abs(strainYCompression))) * Math.Cos(teta) + ConcreteSection.Centroid.Y);

			double chiSx = 0;
			double chiDx = 0;

			foreach ((FailureZones, int) zone in zoneSubdivision)
            {
				FailureZones failureZones = zone.Item1;
				int subdivision = zone.Item2 + 1;

				switch (failureZones)   // campo 1
				{
					case FailureZones.F1:

						chiSx = 0;   // valore curvatura estremo Sx del campo i-esimo
						chiDx = CalculateUltimateStrainSteel(sectionDistances.dMaxRebarIndex) / concreteRebarMaxDistance; // valore curvatura estremo Dx del campo i-esimo

						for (int j = 0; j < subdivision; j++)
						{
							double chi = chiSx + j * (chiDx - chiSx) / subdivision;
							strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Rebars[sectionDistances.dMinRebarIndex].Position, teta, chi,
								CalculateUltimateStrainSteel(sectionDistances.dMinRebarIndex), subIndex), failureZones);
							subIndex++;
						}

						break;

					case FailureZones.F2A:

						chiSx = CalculateUltimateStrainSteel(sectionDistances.dMinRebarIndex) / concreteRebarMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
						chiDx = (CalculateUltimateStrainSteel(sectionDistances.dMinRebarIndex) + Math.Abs(CalculateYeldingStrainConcreteCompression())) /
							(sectionDistances.dmaxConcrete - sectionDistances.dminRebar); // valore curvatura estremo Dx del campo i-esimo

						for (int j = 0; j < subdivision; j++)
						{
							double chi = chiSx + j * (chiDx - chiSx) / subdivision;
							strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Rebars[sectionDistances.dMinRebarIndex].Position, teta, chi,
								CalculateUltimateStrainSteel(sectionDistances.dMinRebarIndex), subIndex), failureZones);
							subIndex++;
						}

						break;

					case FailureZones.F2B:

						chiSx = (CalculateUltimateStrainSteel(sectionDistances.dMinRebarIndex) + Math.Abs(CalculateYeldingStrainConcreteCompression())) 
							/ concreteRebarMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
						chiDx = (CalculateUltimateStrainSteel(sectionDistances.dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) 
							/ concreteRebarMaxDistance; // valore curvatura estremo Dx del campo i-esimo

						for (int j = 0; j < subdivision; j++)
						{
							double chi = chiSx + j * (chiDx - chiSx) / subdivision;
							strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Rebars[sectionDistances.dMinRebarIndex].Position, teta, chi,
								CalculateUltimateStrainSteel(sectionDistances.dMinRebarIndex), subIndex), failureZones);
							subIndex++;
						}

						break;

					case FailureZones.F3A:

						chiSx = (CalculateUltimateStrainSteel(sectionDistances.dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
							concreteRebarMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
						chiDx = (CalculateYeldingStrainSteel(sectionDistances.dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
							concreteRebarMaxDistance; // valore curvatura estremo Dx del campo i-esimo

						for (int j = 0; j < subdivision; j++)
						{
							double chi = chiSx + j * (chiDx - chiSx) / subdivision;
							strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Shape.Fill[sectionDistances.dMaxVertexIndex], teta, chi, 
								CalculateUltimateStrainConcreteCompression(), subIndex), failureZones);
							subIndex++;
						}

						break;

					case FailureZones.F3B:
						
                        chiSx = (CalculateYeldingStrainSteel(sectionDistances.dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
								concreteRebarMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
						chiDx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / concreteRebarMaxDistance; // valore curvatura estremo Dx del campo i-esimo

						for (int j = 0; j < subdivision; j++)
						{
							double chi = chiSx + j * (chiDx - chiSx) / subdivision;
							strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Shape.Fill[sectionDistances.dMaxVertexIndex], teta, chi,
								CalculateUltimateStrainConcreteCompression(), subIndex), failureZones);
							subIndex++;
						}

						break;

					case FailureZones.F4:

						chiSx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / concreteRebarMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
						chiDx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / concreteMaxDistance; // valore curvatura estremo Dx del campo i-esimo

						for (int j = 0; j < subdivision; j++)
						{
							double chi = chiSx + j * (chiDx - chiSx) / subdivision;
							strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Shape.Fill[sectionDistances.dMaxVertexIndex], teta, chi,
								CalculateUltimateStrainConcreteCompression(), subIndex), failureZones);
							subIndex++;
						}

						break;

					case FailureZones.F5:

						chiSx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / concreteMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
						chiDx = 0.0; // valore curvatura estremo Dx del campo i-esimo

						for (int j = 0; j < subdivision; j++)
						{
							double chi = chiSx + j * (chiDx - chiSx) / subdivision;
							strainPlanes[subIndex] = (new StrainPlane(p3, teta, chi, CalculateLimitStrainCostantCompression(), subIndex), failureZones);
							subIndex++;
						}

						strainPlanes[subIndex] = (new StrainPlane(p3, teta, chiDx, CalculateLimitStrainCostantCompression(), subIndex), failureZones);
						break;

					default:
						throw new NotImplementedException();

				}
			}

			
			return strainPlanes;
		}

		/// <summary>
		/// Calculate the <see cref="FailureDomain.FailureDomainPoint"/> respect the strain plane <paramref name="strainPlane"/>
		/// </summary>
		/// <param name="strainPlane"></param>
		/// <returns></returns>
		protected virtual FailureDomain.FailureDomainPoint CalculateForces((StrainPlane, FailureZones) strainPlane)
		{
			try
			{
				var forces = base.CalculateForces(strainPlane.Item1);

				return new FailureDomain.FailureDomainPoint(forces.N, forces.Mx, forces.My, strainPlane.Item2, strainPlane.Item1);
			}
			catch (Exception e)
			{
				_log.Add($"Fail" + e.InnerException);
				return null;
			}
		}

        #region RETTA USCENTE

        protected virtual double CalculateSafetyFactor(ResultBeamForces externalForces, out FailureDomain.FailureDomainPoint pointOnDomain, double angularTolerance = 0.001)
        {
            externalForces = CalculateExternalForces(externalForces, SectionSolverOptions.Instance.DistanceFromCentroid);
            //CalculateAdimensionalForces(forces, out double adimExternalAxialForce, out double adimExternalendingMomentX, out double adimExternalBendingMomentY);

            // piano di primo tentativo. campo di rottura 7, immersione di 0.9 e ruotato di teta = 0;
            double immersione = 0.9;
            double teta;
            if (externalForces.M1 > 0)
                teta = 0;
            else
                teta = Math.PI / 2.0;

            FailureZones failureIndex = FailureZones.F5;
            StrainPlane strainPlane = CalculateStrainPlane(teta, failureIndex, immersione);

            // Valori di primo tentativo
            teta = strainPlane.Teta;
            int id = 1;

            var forces = CalculateForces(strainPlane);

            Vector3d vectorForcesEd = new Vector3d(forces.Mx, forces.My, forces.N);

            double deltaTeta;
            double deltaImmersione;

            Vector3d vectorRd = new Vector3d(forces.Mx, forces.Mx, forces.N);
            double angle = vectorForcesEd.AngleTo(vectorRd);

            do
            {
                try
                {
                    CalculateIncrement(forces.N, forces.Mx, forces.My, strainPlane, failureIndex, immersione, vectorForcesEd, angle, out deltaTeta, out deltaImmersione);
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

                    failureIndex = (int)failureIndex < 1 ? FailureZones.F1 : failureIndex;
                    failureIndex = (int)failureIndex > 7 ? FailureZones.F5 : failureIndex;

                    strainPlane = CalculateStrainPlane(teta, failureIndex, immersione, id);
                    forces = CalculateForces(strainPlane);

                    vectorRd = new Vector3d(forces.Mx, forces.My, forces.N);
                    angle = vectorForcesEd.AngleTo(vectorRd);
                }
                else
                {
                    double immersioneBuffer1 = immersione + (deltaImmersione - (int)deltaImmersione);
                    FailureZones failureIndexBuffer1 = failureIndex + (int)deltaImmersione;

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

                    failureIndexBuffer1 = (int)failureIndexBuffer1 < 1 ? FailureZones.F1 : failureIndexBuffer1;
                    failureIndexBuffer1 = (int)failureIndexBuffer1 > 7 ? FailureZones.F5 : failureIndexBuffer1;

                    StrainPlane strainPlaneBuffer1 = CalculateStrainPlane(teta, failureIndexBuffer1, immersioneBuffer1, id);
					var forces1 = CalculateForces(strainPlane);

					Vector3d vectorRdB1 = new Vector3d(forces1.Mx, forces1.My, forces1.N);
					double angleB1 = vectorForcesEd.AngleTo(vectorRdB1);


                    double immersioneBuffer2 = immersione - (deltaImmersione - (int)deltaImmersione);
                    FailureZones failureIndexBuffer2 = failureIndex - (int)deltaImmersione;

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

                    failureIndexBuffer2 = (int)failureIndexBuffer2 < 1 ? FailureZones.F1 : failureIndexBuffer2;
                    failureIndexBuffer2 = (int)failureIndexBuffer2 > 7 ? FailureZones.F5 : failureIndexBuffer2;

                    StrainPlane strainPlaneBuffer2 = CalculateStrainPlane(teta, failureIndexBuffer2, immersioneBuffer2, id);
					var forces2 = CalculateForces(strainPlane);

					Vector3d vectorRdB2 = new Vector3d(forces2.Mx, forces2.My, forces2.N);

                    double angleB2 = vectorForcesEd.AngleTo(vectorRdB2);

                    if (Math.Abs(angleB1) < Math.Abs(angleB2))
                    {
                        strainPlane = strainPlaneBuffer1;
                        immersione = immersioneBuffer1;

						forces = (forces1.N, forces1.Mx, forces1.My);

                        vectorRd = vectorRdB1;
                        angle = angleB1;
                    }
                    else
                    {
                        strainPlane = strainPlaneBuffer2;
                        immersione = immersioneBuffer2;

						forces = (forces2.N, forces2.Mx, forces2.My);

						vectorRd = vectorRdB2;
                        angle = angleB2;
                    }
                }


            } while (Math.Abs(angle) > angularTolerance);

            pointOnDomain = new FailureDomain.FailureDomainPoint(forces.N, forces.Mx, forces.My, failureIndex, strainPlane);

            return vectorForcesEd.Length / vectorRd.Length;
        }

        protected void CalculateIncrement(double NRd, double MxRd, double MyRd, StrainPlane inputStrainPlane, FailureZones failureIndex,
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

            var forcesNPlusTeta = CalculateForces(strainPlanePlusdTeta);
			var forcesNMinusTeta = CalculateForces(strainPlaneMinusdTeta);

            double dNdTeta =  (forcesNPlusTeta.N - forcesNPlusTeta.N)	/ (2.0 * dTeta);
            double dMxdTeta = (forcesNPlusTeta.Mx - forcesNPlusTeta.Mx) / (2.0 * dTeta);
            double dMydTeta = (forcesNPlusTeta.My - forcesNPlusTeta.My) / (2.0 * dTeta);

            Vector3d v1 = new Vector3d(dMxdTeta, dMydTeta, dNdTeta);
            v1.Unitize();

            // derivate parziali rispetto a immersione nel campo
            StrainPlane strainPlanePlusdImm = CalculateStrainPlane(inputStrainPlane.Teta, failureIndex, Math.Min(immersioneNelCampo + dImmersione, 1.0));
            StrainPlane strainPlaneMinusdImm = CalculateStrainPlane(inputStrainPlane.Teta, failureIndex, Math.Max(immersioneNelCampo - dImmersione, 0.0));

            var forcesNPlusImm = CalculateForces(strainPlanePlusdImm);
			var forcesNMinusImm = CalculateForces(strainPlaneMinusdImm);

            double dNdImm =  (forcesNPlusImm.N  - forcesNMinusImm.N)  / (2.0 * dImmersione);
            double dMxdImm = (forcesNPlusImm.Mx - forcesNMinusImm.Mx) / (2.0 * dImmersione);
            double dMydImm = (forcesNPlusImm.My - forcesNMinusImm.My) / (2.0 * dImmersione);

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





        protected virtual StrainPlane CalculateStrainPlane(double teta, SectionSolverULS.FailureZones failureIndex, double immersioneNelCampo, int id = -1)
		{
			if (immersioneNelCampo > 1.0 || immersioneNelCampo < 0.0)
				throw new ArgumentException("ImmersioneNelCampo cannot be greater than 1 and less than 0");

			var distances = CalculateMaxMinSectionDistances(teta);

			double dmaxConcrete = (ConcreteSection.Shape.Fill[distances.dMaxVertexIndex].Y - ConcreteSection.Centroid.Y) * Math.Cos(teta) -
				(ConcreteSection.Shape.Fill[distances.dMaxVertexIndex].X - ConcreteSection.Centroid.X) * Math.Sin(teta);

			double dminConcrete = (ConcreteSection.Shape.Fill[distances.dMinVertexIndex].Y - ConcreteSection.Centroid.Y) * Math.Cos(teta) -
				(ConcreteSection.Shape.Fill[distances.dMinVertexIndex].X - ConcreteSection.Centroid.X) * Math.Sin(teta);

			double dminSteel = (ConcreteSection.Rebars[distances.dMinRebarIndex].Position.Y - ConcreteSection.Centroid.Y) * Math.Cos(teta) -
				(ConcreteSection.Rebars[distances.dMinRebarIndex].Position.X - ConcreteSection.Centroid.X) * Math.Sin(teta);

			if (failureIndex == FailureZones.F1)
			{
				double chiSx = 0;   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = CalculateUltimateStrainSteel(distances.dMinRebarIndex) / (dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Rebars[distances.dMinRebarIndex].Position, teta, chi, CalculateUltimateStrainSteel(distances.dMinRebarIndex), id);
			}
			else if (failureIndex == FailureZones.F2A)
			{
				double chiSx = CalculateUltimateStrainSteel(distances.dMinRebarIndex) / (dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = (CalculateUltimateStrainSteel(distances.dMinRebarIndex) + Math.Abs(CalculateYeldingStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Rebars[distances.dMinRebarIndex].Position, teta, chi, CalculateUltimateStrainSteel(distances.dMinRebarIndex), id);
			}
			else if (failureIndex == FailureZones.F2B)
			{
				double chiSx = (CalculateUltimateStrainSteel(distances.dMinRebarIndex) + Math.Abs(CalculateYeldingStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = (CalculateUltimateStrainSteel(distances.dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Rebars[distances.dMinRebarIndex].Position, teta, chi, CalculateUltimateStrainSteel(distances.dMinRebarIndex), id);
			}
			else if (failureIndex == FailureZones.F3A)
			{
				double chiSx = (CalculateUltimateStrainSteel(distances.dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = (CalculateYeldingStrainSteel(distances.dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Shape.Fill[distances.dMaxVertexIndex], teta, chi, CalculateUltimateStrainConcreteCompression(), id);

			}
			else if (failureIndex == FailureZones.F3B)
			{
				double chiSx = (CalculateYeldingStrainSteel(distances.dMinRebarIndex) + Math.Abs(CalculateUltimateStrainConcreteCompression())) /
					(dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminSteel); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Shape.Fill[distances.dMaxVertexIndex], teta, chi, CalculateUltimateStrainConcreteCompression(), id);
			}
			else if (failureIndex == FailureZones.F4)
			{
				double chiSx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminSteel);   // valore curvatura estremo Sx del campo i-esimo
				double chiDx = Math.Abs(CalculateUltimateStrainConcreteCompression()) / (dmaxConcrete - dminConcrete); // valore curvatura estremo Dx del campo i-esimo

				double chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
				return new StrainPlane(ConcreteSection.Shape.Fill[distances.dMaxVertexIndex], teta, chi, CalculateUltimateStrainConcreteCompression(), id);
			}
			else if (failureIndex == FailureZones.F5)
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

		#endregion

		#region Equals - hascode - operators

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
