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
		#region Variables

		protected ResultType _forces;

		#endregion

		#region Properties

		public ResultType Forces => _forces;

		#endregion

		#region Public Constructor

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

		#endregion

		#region Perform Check

		public virtual SLSCheckerResultsType PerformSolver()
		{
			throw new Exception();
		}

		#endregion

		#region Solver

		public StrainPlane CalculateStrainPlane()
		{
			return CalculateStrainPlane((ResultBeamForces)Forces, SectionSolverOptions.Instance.SLSconvergenceTolerance);
		}

		protected StrainPlane CalculateStrainPlane(ResultBeamForces forces, double tolerance = 1e-5)
		{
			forces = CalculateExternalForces(forces, SectionSolverOptions.Instance.DistanceFromCentroid);			
			CalculateAdimensionalForces(forces, out double adimExternalAxialForce, out double adimExternalendingMomentX, out double adimExternalBendingMomentY);

			// Valori di primo tentativo
			Point3d referencePoint = ConcreteSection.Centroid;
			double chiX = 0;	
			double chiY = 0;	
			double strainReferencePoint = 0;	
			int id = 1;

			// piano di primo tentativo. baricentrico e ruotato di teta = 0;
			StrainPlane strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);
			CalculateForces(strainPlane, out double N, out double Mx, out double My);

			CalculateAdimensionalForces(N, Mx, My, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY);

			if (Math.Abs(adimAxialForce - adimExternalAxialForce) > tolerance || 
				Math.Abs(adimBendingMomentX - adimExternalendingMomentX) > tolerance || 
				Math.Abs(adimBendingMomentY - adimExternalBendingMomentY) > tolerance)
			{
				do
				{
					Vector3d vector = new Vector3d(forces.M1 - Mx, forces.M2 - My, forces.N - N);
					double deltaChiX;
					double deltaChiY;
					double deltaEpsilon0;

					try
					{
						CalculateIncrement(strainPlane, vector, out deltaChiX, out deltaChiY, out deltaEpsilon0);
					}
					catch
					{
						_log.Add("Fail to calculate increment");
						throw new Exception("Fail to calculate increment");
					}

					// piano di nuovo tentativo
					id++;
					chiX += deltaChiX;
					chiY += deltaChiY;
					strainReferencePoint += deltaEpsilon0;
					strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

					CalculateForces(strainPlane, out N, out Mx, out My);

					CalculateAdimensionalForces(N, Mx, My, out adimAxialForce, out adimBendingMomentX, out adimBendingMomentY);

				} while (Math.Abs(adimAxialForce - adimExternalAxialForce) > tolerance ||
						Math.Abs(adimBendingMomentX - adimExternalendingMomentX) > tolerance ||
						Math.Abs(adimBendingMomentY - adimExternalBendingMomentY) > tolerance);
			}
			return strainPlane;
		}

		protected void CalculateIncrement(StrainPlane inputStrainPlane, Vector3d vector, out double deltaChiX, out double deltaChiY, out double deltaStrainRefPoint)
		{
			CalculateAdimensionalForces(vector.Z, vector.X, vector.Y, out double adimAxialVector, out double adimBendingMomentXVector, out double adimBendingMomentYVector);

			double deltaChiXLimit = Math.Abs(CalculateYeldingStrainConcreteCompression() / ConcreteSection.Shape.GetBoundingBox().Size.X);
			double dCX = 0.00001;
			if(adimBendingMomentXVector != 0)
				dCX= 0.001 * Math.Max(adimBendingMomentXVector, 0.00001);

			double dChiX = dCX * deltaChiXLimit;

			double deltaChiYLimit = Math.Abs(CalculateYeldingStrainConcreteCompression() / ConcreteSection.Shape.GetBoundingBox().Size.Y);
			double dCY = 0.00001;
			if (adimBendingMomentYVector != 0)
				dCY = 0.001 * Math.Max(adimBendingMomentYVector, 0.00001);

			double dChiY = dCY * deltaChiYLimit;

			double deltaStrainLimit = 1.0 / (ConcreteSection.Area * GetFck());
			double dS = 0.00001;
			if(adimAxialVector != 0)
				dS = 0.0001 * Math.Max(adimAxialVector, 0.00001);

			double dStrain = dS * deltaStrainLimit;


			// derivate parziali rispetto a ChiX
			StrainPlane strainPlanePlusdChiX = new StrainPlane(inputStrainPlane.ChiX + dChiX, inputStrainPlane.ChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
			StrainPlane strainPlaneMinusdChiX = new StrainPlane(inputStrainPlane.ChiX - dChiX, inputStrainPlane.ChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

			CalculateForces(strainPlanePlusdChiX, out double NPlusdChiX, out double MxPlusdChiX, out double MyPlusdChiX);
			CalculateForces(strainPlaneMinusdChiX, out double NMinusdChiX, out double MxMinusdChiX, out double MyMinusdChiX);

			double dNdChiX = (NPlusdChiX - NMinusdChiX) / (2.0 * dCX);
			double dMxdChiX = (MxPlusdChiX - MxMinusdChiX) / (2.0 * dCX);
			double dMydChiX = (MyPlusdChiX - MyMinusdChiX) / (2.0 * dCX);


			// derivate parziali rispetto a ChiY
			StrainPlane strainPlanePlusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY + dChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
			StrainPlane strainPlaneMinusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY - dChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

			CalculateForces(strainPlanePlusdChiY, out double NPlusdChiY, out double MxPlusdChiY, out double MyPlusdChiY);
			CalculateForces(strainPlaneMinusdChiY, out double NMinusdChiY, out double MxMinusdChiY, out double MyMinusdChiY);

			double dNdChiY = (NPlusdChiY - NMinusdChiY) / (2.0 * dCY);
			double dMxdChiY = (MxPlusdChiY - MxMinusdChiY) / (2.0 * dCY);
			double dMydChiY = (MyPlusdChiY - MyMinusdChiY) / (2.0 * dCY);


			// derivate parziali rispetto a epsilon
			StrainPlane strainPlanePlusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint + dStrain);
			StrainPlane strainPlaneMinusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY, 
				inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint - dStrain);

			CalculateForces(strainPlanePlusStrain, out double NPlusdStrain, out double MxPlusdStrain, out double MyPlusdStrain);
			CalculateForces(strainPlaneMinusStrain, out double NMinusdStrain, out double MxMinusdStrain, out double MyMinusdStrain);

			double dNdStrain = (NPlusdStrain - NMinusdStrain) / (2.0 * dS);
			double dMxdStrain = (MxPlusdStrain - MxMinusdStrain) / (2.0 * dS);
			double dMydStrain = (MyPlusdStrain - MyMinusdStrain) / (2.0 * dS);


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

			deltaChiX = results[0, 0] * deltaChiXLimit;
			deltaChiY = results[1, 0] * deltaChiYLimit;
			deltaStrainRefPoint = results[2, 0] * deltaStrainLimit;
		}

		#endregion

		#region Override Methods

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
