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
				if (Math.Abs(ex - exExt) > tolerance || Math.Abs(ey - eyExt) > tolerance)
				{
					do
					{
						CalculatePlaneIncrements(N, Mx, My, forces.N, forces.M1, forces.M2, out double deltaTeta, out Point3d deltaReferencePoint);
						teta += deltaTeta;
						referencePoint += deltaReferencePoint;
						id++;

						// piano di nuovo tentativo. non più baricentrico e ruotato di teta;
						strainPlane = new StrainPlane(referencePoint, teta, chi, strainReferencePoint, id);

						CalculateForces(strainPlane, out N, out Mx, out My);

						// eccentricità di nuovo tentativo
						ex = CalculateEccentricity(Mx, N);
						ey = CalculateEccentricity(My, N);

					} while (Math.Abs(ex - exExt) > tolerance || Math.Abs(ey - eyExt) > tolerance);
				}

				// abbiamo trovato il teta di inclinazione e la posizione dell'asse neutro. dobbiamo trovare adesso la curvatura chi
				if (Math.Abs(N - forces.N) > tolerance)
				{
					do
					{
						chi += CalculateChiIncrement();
						id++;

						// piano di nuovo tentativo. baricentrico e ruotato di teta;
						strainPlane = new StrainPlane(referencePoint, teta, chi, strainReferencePoint, id);

						CalculateForces(strainPlane, out N, out Mx, out My);

					} while (Math.Abs(N - forces.N) > tolerance);
				}
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

		protected void CalculatePlaneIncrements(double N, double Mx, double My, double NExt, double MxExt, double MyExt, out double deltaTeta, out Point3d deltaReferencePoint)
		{
			double tetaInterno = Math.Atan2(My, Mx);
			double tetaEsterno = Math.Atan2(MxExt, MyExt);

			deltaTeta = tetaEsterno - tetaInterno;

			deltaReferencePoint = new Point3d(N / (ConcreteSection.Area * ConcreteMaterial.E) * CalculateEccentricity(Mx, N), 
				N / (ConcreteSection.Area * ConcreteMaterial.E) * CalculateEccentricity(My, N), 0);
		}

		protected double CalculateChiIncrement()
		{
			return 0.0001;
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
