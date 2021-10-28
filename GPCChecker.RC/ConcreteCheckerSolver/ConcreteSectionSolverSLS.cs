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
				chi += 1.0e-11;
				strainPlane = new StrainPlane(referencePoint, teta, chi, strainReferencePoint, id);

				CalculateForces(strainPlane, out N, out Mx, out My);

				do
				{
					Vector3d vector = new Vector3d(forces.M1 - Mx, forces.M2 - My, forces.N - N);

					CalculateIncrement(strainPlane, vector, out double deltaTeta, out double deltaChi, out double deltaVerticalDisplacement);

					// piano di nuovo tentativo
					id++;
					teta += deltaTeta;
					chi += deltaChi;
					referencePoint += new Point3d(0, deltaVerticalDisplacement, 0);
					strainPlane = new StrainPlane(referencePoint, teta, chi, strainReferencePoint, id);

					CalculateForces(strainPlane, out N, out Mx, out My);

				} while (Math.Abs(N - forces.N) > tolerance || Math.Abs(Mx - forces.M1) > tolerance || Math.Abs(My - forces.M2) > tolerance);
			}
			return strainPlane;
		}


		protected void CalculateIncrement(StrainPlane inputStrainPlane, Vector3d vector, out double deltaTeta, out double deltaChi, out double deltaVerticalDisplacement)
		{
			double dTeta = Math.PI / 90.0;
			double dChi = 1.0e-14;
			double dDisplacement = 1.0;

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
			StrainPlane strainPlanePlusDispl = new StrainPlane(inputStrainPlane.ReferencePoint + new Point3d(0, dDisplacement, 0), inputStrainPlane.Teta, inputStrainPlane.Chi, 
				inputStrainPlane.StrainReferencePoint);
			StrainPlane strainPlaneMinusDispl = new StrainPlane(inputStrainPlane.ReferencePoint - new Point3d(0, dDisplacement, 0), inputStrainPlane.Teta, inputStrainPlane.Chi,
				inputStrainPlane.StrainReferencePoint);

			CalculateForces(strainPlanePlusDispl, out double NPlusdDispl, out double MxPlusdDispl, out double MyPlusdDispl);
			CalculateForces(strainPlaneMinusDispl, out double NMinusdDispl, out double MxMinusdDispl, out double MyMinusdDispl);

			double dNdDispl = (NPlusdDispl - NMinusdDispl) / (2.0 * dDisplacement);
			double dMxdDispl = (MxPlusdDispl - MxMinusdDispl) / (2.0 * dDisplacement);
			double dMydDispl = (MyPlusdDispl - MyMinusdDispl) / (2.0 * dDisplacement);

			Matrix<double> tetaNumeratore = Matrix<double>.Build.Dense(3,3);

			tetaNumeratore[0, 0] = dNdChi;
			tetaNumeratore[1, 0] = dMxdChi;
			tetaNumeratore[2, 0] = dMydChi;

			tetaNumeratore[0, 1] = dNdDispl;
			tetaNumeratore[1, 1] = dMxdDispl;
			tetaNumeratore[2, 1] = dMydDispl;

			tetaNumeratore[0, 2] = -vector.Z;
			tetaNumeratore[1, 2] = -vector.X;
			tetaNumeratore[2, 2] = -vector.Y;

			Matrix<double> chiNumeratore = Matrix<double>.Build.Dense(3, 3);

			chiNumeratore[0, 0] = dNdTeta;
			chiNumeratore[1, 0] = dMxdTeta;
			chiNumeratore[2, 0] = dMydTeta;

			chiNumeratore[0, 1] = dNdDispl;
			chiNumeratore[1, 1] = dMxdDispl;
			chiNumeratore[2, 1] = dMydDispl;

			chiNumeratore[0, 2] = -vector.Z;
			chiNumeratore[1, 2] = -vector.X;
			chiNumeratore[2, 2] = -vector.Y;

			Matrix<double> strainNumeratore = Matrix<double>.Build.Dense(3, 3);

			strainNumeratore[0, 0] = dNdTeta;
			strainNumeratore[1, 0] = dMxdTeta;
			strainNumeratore[2, 0] = dMydTeta;

			strainNumeratore[0, 1] = dNdChi;
			strainNumeratore[1, 1] = dMxdChi;
			strainNumeratore[2, 1] = dMydChi;

			strainNumeratore[0, 2] = -vector.Z;
			strainNumeratore[1, 2] = -vector.X;
			strainNumeratore[2, 2] = -vector.Y;

			Matrix<double> denominatoreMatrix = Matrix<double>.Build.Dense(3, 3);

			denominatoreMatrix[0, 0] = dNdTeta;
			denominatoreMatrix[1, 0] = dMxdTeta;
			denominatoreMatrix[2, 0] = dMydTeta;

			denominatoreMatrix[0, 1] = dNdChi;
			denominatoreMatrix[1, 1] = dMxdChi;
			denominatoreMatrix[2, 1] = dMydChi;

			denominatoreMatrix[0, 2] = dNdDispl;
			denominatoreMatrix[1, 2] = dMxdDispl;
			denominatoreMatrix[2, 2] = dMydDispl;

			double denominatore = denominatoreMatrix.Determinant();

			deltaTeta = tetaNumeratore.Determinant() / denominatore;
			deltaChi = chiNumeratore.Determinant() / denominatore;
			deltaVerticalDisplacement = strainNumeratore.Determinant() / denominatore;
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
