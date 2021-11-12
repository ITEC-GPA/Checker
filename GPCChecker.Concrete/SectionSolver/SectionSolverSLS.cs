using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Sections.Concrete;
using System.Runtime.Serialization;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.SectionSolvers
{
	public abstract class SectionSolverSLS : SectionSolver
	{
		protected ResultBeamForces _forces;

		public ResultBeamForces Forces => _forces;


		public SectionSolverSLS(IConcreteSection section, ResultBeamForces forces, Standard standard)
			:base(section, standard)
		{
			_forces = forces ?? throw new ArgumentNullException(nameof(forces));
		}

		public SectionSolverSLS(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			_forces = (ResultBeamForces)info.GetValue("Forces", typeof(ResultBeamForces));
		}

		public virtual SLSCheckerResultsType PerformSolver()
		{
			throw new Exception();
		}


		#region Solver

		public StrainPlane Solve()
		{
			return CalculateStrainPlane(Forces, new Point2d(), SectionSolverOptions.Instance.SLSconvergenceTolerance);  
		}

		protected StrainPlane CalculateStrainPlane(ResultBeamForces externalForces, Point2d forceReferencePoint, double tolerance = 1e-5)
		{
			var targetLocalForces = GetLocalForces(externalForces, forceReferencePoint);
			
			var targetLocalForcesAdmin = CalculateAdimensionalForces(targetLocalForces);

			// Valori di primo tentativo
			Point3d referencePoint = ConcreteSection.Centroid;
			double chiX = 0;	
			double chiY = 0;	
			double strainReferencePoint = 0;	
			int id = 1;

			// piano di primo tentativo. baricentrico e ruotato di teta = 0;
			StrainPlane strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);
			var iterationForces = CalculateForces(strainPlane);

			var iterationForcesAdmin = CalculateAdimensionalForces(iterationForces.N, iterationForces.Mx, iterationForces.My);

			if (Math.Abs(iterationForcesAdmin.N  - targetLocalForcesAdmin.N ) > tolerance || 
				Math.Abs(iterationForcesAdmin.Mx - targetLocalForcesAdmin.Mx) > tolerance || 
				Math.Abs(iterationForcesAdmin.My - targetLocalForcesAdmin.My) > tolerance)
			{
				do
				{
					Vector3d vector = new Vector3d(targetLocalForces.M1 - iterationForces.Mx, targetLocalForces.M2 - iterationForces.My, targetLocalForces.N - iterationForces.N);

					try
					{
                        (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) increment = CalculateIncrement(strainPlane, vector);

						// piano di nuovo tentativo
						id++;
						chiX += increment.deltaChiX;
						chiY += increment.deltaChiY;
						strainReferencePoint += increment.deltaStrainRefPoint;
						strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

						iterationForces = CalculateForces(strainPlane);

						iterationForcesAdmin = CalculateAdimensionalForces(iterationForces.N, iterationForces.Mx, iterationForces.My);
					}
					catch
					{
						_log.Add("Fail to calculate increment");
						throw new Exception("Fail to calculate increment");
					}


				} while (Math.Abs(iterationForcesAdmin.N  - targetLocalForcesAdmin.N ) > tolerance ||
						 Math.Abs(iterationForcesAdmin.Mx - targetLocalForcesAdmin.Mx) > tolerance ||
						 Math.Abs(iterationForcesAdmin.My - targetLocalForcesAdmin.My) > tolerance);
			}
			return strainPlane;
		}

		protected (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) CalculateIncrement(StrainPlane inputStrainPlane, Vector3d vector)
		{
			var vectorAdmin = CalculateAdimensionalForces(vector.Z, vector.X, vector.Y);

			double deltaChiXLimit = Math.Abs(CalculateYeldingStrainConcreteCompression() / ConcreteSection.Shape.GetBoundingBox().Size.X);
			double dCX = 0.00001;
			if(vectorAdmin.Mx != 0)
				dCX= 0.001 * Math.Max(vectorAdmin.Mx, 0.00001);

			double dChiX = dCX * deltaChiXLimit;

			double deltaChiYLimit = Math.Abs(CalculateYeldingStrainConcreteCompression() / ConcreteSection.Shape.GetBoundingBox().Size.Y);
			double dCY = 0.00001;
			if (vectorAdmin.My != 0)
				dCY = 0.001 * Math.Max(vectorAdmin.My, 0.00001);

			double dChiY = dCY * deltaChiYLimit;

			double deltaStrainLimit = 1.0 / (ConcreteSection.Area * GetFck());
			double dS = 0.00001;
			if(vectorAdmin.N != 0)
				dS = 0.0001 * Math.Max(vectorAdmin.N, 0.00001);

			double dStrain = dS * deltaStrainLimit;


			// derivate parziali rispetto a ChiX
			StrainPlane strainPlanePlusdChiX = new StrainPlane(inputStrainPlane.ChiX + dChiX, inputStrainPlane.ChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
			StrainPlane strainPlaneMinusdChiX = new StrainPlane(inputStrainPlane.ChiX - dChiX, inputStrainPlane.ChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

			var forcesPlusdChiX = CalculateForces(strainPlanePlusdChiX);
			var forcesMinusdChiX = CalculateForces(strainPlaneMinusdChiX);

			double dNdChiX = (forcesPlusdChiX.N - forcesMinusdChiX.N) / (2.0 * dCX);
			double dMxdChiX = (forcesPlusdChiX.Mx - forcesMinusdChiX.My) / (2.0 * dCX);
			double dMydChiX = (forcesPlusdChiX.My - forcesMinusdChiX.My) / (2.0 * dCX);


			// derivate parziali rispetto a ChiY
			StrainPlane strainPlanePlusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY + dChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
			StrainPlane strainPlaneMinusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY - dChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

			var forcesPlusdChiY = CalculateForces(strainPlanePlusdChiY);
			var forcesMinusdChiY = CalculateForces(strainPlaneMinusdChiY);

			double dNdChiY  = (forcesPlusdChiY.N - forcesMinusdChiY.N)   / (2.0 * dCY);
			double dMxdChiY = (forcesPlusdChiY.My - forcesMinusdChiY.My) / (2.0 * dCY);
			double dMydChiY = (forcesPlusdChiY.My - forcesMinusdChiY.My) / (2.0 * dCY);



			// derivate parziali rispetto a epsilon
			StrainPlane strainPlanePlusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint + dStrain);
			StrainPlane strainPlaneMinusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint - dStrain);

			var forcesPlusStrain =  CalculateForces(strainPlanePlusStrain);
			var forcesMinusStrain = CalculateForces(strainPlaneMinusStrain);

			double dNdStrain =  (forcesPlusStrain.N  - forcesMinusStrain.N)  / (2.0 * dCY);
			double dMxdStrain = (forcesPlusStrain.My - forcesMinusStrain.My) / (2.0 * dCY);
			double dMydStrain = (forcesPlusStrain.My - forcesMinusStrain.My) / (2.0 * dCY);



			Matrix<double> partialDerivatives = Matrix<double>.Build.Dense(3, 3);

			partialDerivatives[0, 0] = dNdChiX;
			partialDerivatives[1, 0] = dMxdChiX;
			partialDerivatives[2, 0] = dMydChiX;

			partialDerivatives[0, 1] = dNdChiY;
			partialDerivatives[1, 1] = dMxdChiY;
			partialDerivatives[2, 1] = dMydChiY;

			partialDerivatives[0, 2] = dNdStrain;
			partialDerivatives[1, 2] = dMxdStrain;
			partialDerivatives[2, 2] = dMydStrain;


			Matrix<double> inputVector = Matrix<double>.Build.Dense(3, 1);
			inputVector[0, 0] = vector.Z;
			inputVector[1, 0] = vector.X;
			inputVector[2, 0] = vector.Y;

			Matrix<double> results = partialDerivatives.Inverse() * inputVector;

			return (results[0, 0] * deltaChiXLimit, results[1, 0] * deltaChiYLimit, results[2, 0] * deltaStrainLimit);
		}

		#endregion



		#region Equals - hashcode - operators

		public override bool Equals(object obj)
		{
			return obj is SectionSolverSLS sLS &&
				   base.Equals(obj) &&
				   EqualityComparer<ResultType>.Default.Equals(_forces, sLS._forces);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = 23;
				hashCode = hashCode * -17 + base.GetHashCode();
				hashCode = hashCode * -17 + EqualityComparer<ResultType>.Default.GetHashCode(_forces);
				return hashCode;
			}
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Forces", _forces);
		}

		#endregion
	}
}
