using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using GPC.Checkers.ReinforcedConcrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Sections.Concrete;
using System.Runtime.Serialization;

namespace GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver
{
	public abstract class ConcreteSectionSolverSLS : ConcreteSectionSolver
	{
		#region Variables

		protected ResultType _forces;

		#endregion

		#region Properties

		public ResultType Forces => _forces;

		#endregion

		#region Public Constructor

		public ConcreteSectionSolverSLS(IConcreteSection section, ResultBeamForces forces)
			:base(section)
		{
			_forces = forces ?? throw new ArgumentNullException(nameof(forces));
		}

		public ConcreteSectionSolverSLS(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			_forces = (ResultBeamForces)info.GetValue("Forces", typeof(ResultBeamForces));
		}

		#endregion

		#region Solver

		public StrainPlane CalculateStrainPlane()
		{
			return CalculateStrainPlane((ResultBeamForces)Forces);
		}

		protected StrainPlane CalculateStrainPlane(ResultBeamForces forces)
		{
			double tolerance = 0.0001;

			// Valori di primo tentativo
			double teta = 0.0;
			Point3d referencePoint = ConcreteSection.Centroid;
			double chi = 0;	// 1e-12;
			double strainReferencePoint = 0;	//= -1e-8;
			int id = 1;

			// piano di primo tentativo. baricentrico e ruotato di teta = 0;
			StrainPlane strainPlane = new StrainPlane(referencePoint, teta, chi, strainReferencePoint, id);

			CalculateForces(strainPlane, out double N, out double Mx, out double My);

			if (Math.Abs(N - forces.N) > tolerance || Math.Abs(Mx - forces.M1) > tolerance || Math.Abs(My - forces.M2) > tolerance)
			{
				chi += 1.0e-10;
				strainPlane = new StrainPlane(referencePoint, teta, chi, strainReferencePoint, id);

				CalculateForces(strainPlane, out N, out Mx, out My);

				do
				{
					Vector3d vector = new Vector3d(forces.M1 - Mx, forces.M2 - My, forces.N - N);

					CalculateIncrement(strainPlane, vector, out double deltaTeta, out double deltaChi, out double deltaStrainRefPoint);

					// piano di nuovo tentativo
					id++;
					teta += deltaTeta;
					chi += deltaChi;
					strainReferencePoint += deltaStrainRefPoint;
					strainPlane = new StrainPlane(referencePoint, teta, chi, strainReferencePoint, id);

					CalculateForces(strainPlane, out N, out Mx, out My);

				} while (Math.Abs(N - forces.N) > tolerance || Math.Abs(Mx - forces.M1) > tolerance || Math.Abs(My - forces.M2) > tolerance);
			}
			return strainPlane;
		}


		protected void CalculateIncrement(StrainPlane inputStrainPlane, Vector3d vector, out double deltaTeta, out double deltaChi, out double deltaStrainRefPoint)
		{
			double dTeta = Math.PI / 90.0;
			double dChi = 1.0e-11;
			double dStrain = 0.00001;

			// derivate parziali rispetto a teta
			StrainPlane strainPlanePlusdTeta = new StrainPlane(inputStrainPlane.ReferencePoint, inputStrainPlane.Teta + dTeta, inputStrainPlane.Chi, 
				inputStrainPlane.StrainReferencePoint);
			StrainPlane strainPlaneMinusdTeta = new StrainPlane(inputStrainPlane.ReferencePoint, inputStrainPlane.Teta - dTeta, inputStrainPlane.Chi,
				inputStrainPlane.StrainReferencePoint);

			CalculateForces(strainPlanePlusdTeta, out double NPlusdTeta, out double MxPlusdTeta, out double MyPlusdTeta);
			CalculateForces(strainPlaneMinusdTeta, out double NMinusdTeta, out double MxMinusdTeta, out double MyMinusdTeta);

			double dNdTeta = (NPlusdTeta - NMinusdTeta) / (2.0 * dTeta);
			double dMxdTeta = (MxPlusdTeta - MxMinusdTeta) / (2.0 * dTeta);
			double dMydTeta = (MyPlusdTeta - MyMinusdTeta) / (2.0 * dTeta);

			// derivate parziali rispetto a Chi
			StrainPlane strainPlanePlusdChi = new StrainPlane(inputStrainPlane.ReferencePoint, inputStrainPlane.Teta, inputStrainPlane.Chi + dChi, 
				inputStrainPlane.StrainReferencePoint);
			StrainPlane strainPlaneMinusChi = new StrainPlane(inputStrainPlane.ReferencePoint, inputStrainPlane.Teta, inputStrainPlane.Chi - dChi,
				inputStrainPlane.StrainReferencePoint);

			CalculateForces(strainPlanePlusdChi, out double NPlusdChi, out double MxPlusdChi, out double MyPlusdChi);
			CalculateForces(strainPlaneMinusChi, out double NMinusdChi, out double MxMinusdChi, out double MyMinusdChi);

			double dNdChi = (NPlusdChi - NMinusdChi) / (2.0 * dChi);
			double dMxdChi = (MxPlusdChi - MxMinusdChi) / (2.0 * dChi);
			double dMydChi = (MyPlusdChi - MyMinusdChi) / (2.0 * dChi);

			// derivate parziali rispetto a epsilon
			StrainPlane strainPlanePlusStrain = new StrainPlane(inputStrainPlane.ReferencePoint, inputStrainPlane.Teta, inputStrainPlane.Chi,
				inputStrainPlane.StrainReferencePoint + dStrain);
			StrainPlane strainPlaneMinusStrain = new StrainPlane(inputStrainPlane.ReferencePoint, inputStrainPlane.Teta, inputStrainPlane.Chi,
				inputStrainPlane.StrainReferencePoint - dStrain);

			CalculateForces(strainPlanePlusStrain, out double NPlusdStrain, out double MxPlusdStrain, out double MyPlusdStrain);
			CalculateForces(strainPlaneMinusStrain, out double NMinusdStrain, out double MxMinusdStrain, out double MyMinusdStrain);

			double dNdStrain = (NPlusdStrain - NMinusdStrain) / (2.0 * dStrain);
			double dMxdStrain = (MxPlusdStrain - MxMinusdStrain) / (2.0 * dStrain);
			double dMydStrain = (MyPlusdStrain - MyMinusdStrain) / (2.0 * dStrain);


			Matrix<double> partialDerivatives = Matrix<double>.Build.Dense(3, 3);

			partialDerivatives[0, 0] = dNdTeta;
			partialDerivatives[1, 0] = dMxdTeta;
			partialDerivatives[2, 0] = dMydTeta;

			partialDerivatives[0, 1] = dNdChi;
			partialDerivatives[1, 1] = dMxdChi;
			partialDerivatives[2, 1] = dMydChi;

			partialDerivatives[0, 2] = dNdStrain;
			partialDerivatives[1, 2] = dMxdStrain;
			partialDerivatives[2, 2] = dMydStrain;


			Matrix<double> inputVector = Matrix<double>.Build.Dense(3, 1);
			inputVector[0, 0] = vector.Z;
			inputVector[1, 0] = vector.X;
			inputVector[2, 0] = vector.Y;

			Matrix<double> results = partialDerivatives.Inverse() * inputVector;

			deltaTeta = results[0, 0];
			deltaChi = results[1, 0];
			deltaStrainRefPoint = results[2, 0];
		}

		#endregion

		#region Override Methods

		public override bool Equals(object obj)
		{
			return obj is ConcreteSectionSolverSLS sLS &&
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
