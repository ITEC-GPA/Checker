using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

			// eccentricità lungo x delle forze esterne
			double exExt = CalculateEccentricity(forces.M1, forces.N);

			// eccentricità lungo x delle forze esterne
			double eyExt = CalculateEccentricity(forces.M2, forces.N);

			// Valori di primo tentativo
			double teta = 0.0;
			Point3d referencePoint = ConcreteSection.Centroid;
			double chi = 0.0;
			double strainReferencePoint = 0.0;
			int id = 1;

			// piano di primo tentativo. baricentrico e ruotato di teta = 0;
			StrainPlane strainPlane = new StrainPlane(referencePoint, teta, chi, strainReferencePoint, id);

			CalculateForces(strainPlane, out double N, out double Mx, out double My);

			Vector3d vector = new Vector3d(Math.Sqrt(forces.M1 - Mx), Math.Sqrt(forces.M2 - My), Math.Sqrt(forces.N - N));

			do
			{

				





			} while (Math.Abs(N - forces.N) > tolerance && Math.Abs(Mx - forces.M1) > tolerance && Math.Abs(My - forces.M2) > tolerance);

			return new StrainPlane(referencePoint, teta, chi, strainReferencePoint, id);
		}

		protected double CalculateEccentricity(double bendingMoment, double axialForce)
		{
			if (Math.Abs(axialForce) > 0.001)
				return bendingMoment / axialForce;

			else			
				return double.MaxValue;
		}


		protected void CalculateIncrement(StrainPlane inputStrainPlane)
		{
			double deltaTeta = 1.0;
			double deltaChi = 0.0001;
			double deltaStrain = 0.001;

			// derivate parziali rispetto a teta
			StrainPlane strainPlanePlusdTeta = new StrainPlane(ConcreteSection.Centroid, inputStrainPlane.Teta + deltaTeta, inputStrainPlane.Chi, 
				inputStrainPlane.StrainReferencePoint);
			StrainPlane strainPlaneMinusdTeta = new StrainPlane(ConcreteSection.Centroid, inputStrainPlane.Teta - deltaTeta, inputStrainPlane.Chi,
				inputStrainPlane.StrainReferencePoint);

			CalculateForces(strainPlanePlusdTeta, out double NPlusdTeta, out double MxPlusdTeta, out double MyPlusdTeta);
			CalculateForces(strainPlaneMinusdTeta, out double NMinusdTeta, out double MxMinusdTeta, out double MyMinusdTeta);

			double dNdTeta = (NPlusdTeta - NMinusdTeta) / (2.0 * deltaTeta);
			double dMxdTeta = (MxPlusdTeta - MxMinusdTeta) / (2.0 * deltaTeta);
			double dMydTeta = (MyPlusdTeta - MyMinusdTeta) / (2.0 * deltaTeta);

			// derivate parziali rispetto a Chi
			StrainPlane strainPlanePlusdChi = new StrainPlane(ConcreteSection.Centroid, inputStrainPlane.Teta, inputStrainPlane.Chi + deltaChi, 
				inputStrainPlane.StrainReferencePoint);
			StrainPlane strainPlaneMinusChi = new StrainPlane(ConcreteSection.Centroid, inputStrainPlane.Teta, inputStrainPlane.Chi - deltaChi,
				inputStrainPlane.StrainReferencePoint);

			CalculateForces(strainPlanePlusdChi, out double NPlusdChi, out double MxPlusdChi, out double MyPlusdChi);
			CalculateForces(strainPlaneMinusChi, out double NMinusdChi, out double MxMinusdChi, out double MyMinusdChi);

			double dNdChi = (NPlusdChi - NMinusdChi) / (2.0 * deltaChi);
			double dMxdChi = (MxPlusdChi - MxMinusdChi) / (2.0 * deltaChi);
			double dMydChi = (MyPlusdChi - MyMinusdChi) / (2.0 * deltaChi);

			// derivate parziali rispetto a epsilon
			StrainPlane strainPlanePlusStrain = new StrainPlane(ConcreteSection.Centroid, inputStrainPlane.Teta, inputStrainPlane.Chi, 
				inputStrainPlane.StrainReferencePoint + deltaStrain);
			StrainPlane strainPlaneMinusStrain = new StrainPlane(ConcreteSection.Centroid, inputStrainPlane.Teta, inputStrainPlane.Chi,
				inputStrainPlane.StrainReferencePoint - deltaStrain);

			CalculateForces(strainPlanePlusStrain, out double NPlusdStrain, out double MxPlusdStrain, out double MyPlusdStrain);
			CalculateForces(strainPlaneMinusStrain, out double NMinusdStrain, out double MxMinusdStrain, out double MyMinusdStrain);

			double dNdStrain = (NPlusdStrain - NMinusdStrain) / (2.0 * deltaStrain);
			double dMxdStrain = (MxPlusdStrain - MxMinusdStrain) / (2.0 * deltaStrain);
			double dMydStrain = (MyPlusdStrain - MyMinusdStrain) / (2.0 * deltaStrain);



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
