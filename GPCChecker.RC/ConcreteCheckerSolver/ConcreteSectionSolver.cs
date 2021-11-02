using GPC.Checkers.ReinforcedConcrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Sections.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver
{
	[Serializable]
	public abstract class ConcreteSectionSolver
	{
		#region Variables

		protected IConcreteSection _concreteSection;
		protected List<string> _log;

		#endregion

		#region Properties

		public IConcreteSection ConcreteSection => _concreteSection;

		public ConcreteMaterial ConcreteMaterial => _concreteSection.ConcreteMaterial;

		#endregion

		#region Public Constructor

		public ConcreteSectionSolver(IConcreteSection section)
		{
			_concreteSection = section;
			_log = new List<string>();
		}

		public ConcreteSectionSolver(SerializationInfo info, StreamingContext context)
		{
			_concreteSection = (IConcreteSection)info.GetValue("ConcreteSection", typeof(IConcreteSection));
			_log = (List<string>)info.GetValue("Log", typeof(List<string>));
		}

		#endregion

		#region Abstract Method

		protected abstract double CalculateYeldingStrainSteel(ReinforcedConcreteRebar rebar);
		protected abstract double CalculateYeldingStrainSteel(int rebar);
		protected abstract double CalculateUltimateStrainSteel(ReinforcedConcreteRebar rebar);
		protected abstract double CalculateUltimateStrainSteel(int rebar);
		protected abstract double CalculateUltimateStrainConcreteCompression();
		protected abstract double CalculateYeldingStrainConcreteCompression();
		protected abstract double CalculateLimitStrainCostantCompression();
		protected abstract double CalculateUltimateStrainConcreteTension();
		protected abstract double CalculateSigmaC(double strain);
		protected abstract double CalculateStressSteel(ReinforcedConcreteRebar rebar, double strain);

		#endregion

		#region Virtual Method

		/// <summary>
		/// Return the strain value of the <paramref name="pointToTest"/>
		/// </summary>
		protected virtual double CalculateStrain(StrainPlane strainPlane, Point3d pointToTest)
		{
			return strainPlane.StrainReferencePoint - strainPlane.Chi * ((pointToTest.Y - strainPlane.ReferencePoint.Y) * Math.Cos(strainPlane.Teta) -
				(pointToTest.X - strainPlane.ReferencePoint.X) * Math.Sin(strainPlane.Teta));
		}

		/// <summary>
		/// Calculate the stress resultant of the concrete part
		/// </summary>
		/// <param name="strainPlane">The strain plane</param>
		/// <param name="deltaN">The axial force resultant</param>
		/// <param name="deltaMx">The bending moment about X-axis resultant</param>
		/// <param name="deltaMy">The bending moment about Y-axis resultant</param>
		protected virtual void CalculateConcreteStressResultant(StrainPlane strainPlane, out double deltaN, out double deltaMx, out double deltaMy)
		{
			double[] deltaNArray = new double[ConcreteSection.Mesh.FacesCount];
			double[] deltaMxArray = new double[ConcreteSection.Mesh.FacesCount];
			double[] deltaMyArray = new double[ConcreteSection.Mesh.FacesCount];

			try
			{
				Parallel.For(0, ConcreteSection.Mesh.FacesCount, (i) =>
				{
					CalculateFaceStressResultant(ConcreteSection.Mesh.Faces[i + 1], strainPlane, out double deltaNBuffer, out double deltaMxBuffer, out double deltaMyBuffer);

					deltaNArray[i] = deltaNBuffer;
					deltaMxArray[i] = deltaMxBuffer;
					deltaMyArray[i] = deltaMyBuffer;
				});
			}
			catch (Exception e)
			{
				_log.Add($"Fail" + e.InnerException);
			}


			deltaN = deltaNArray.Sum();
			deltaMx = deltaMxArray.Sum();
			deltaMy = deltaMyArray.Sum();
		}

		/// <summary>
		/// Calculate the resultants of face <paramref name="face"/>
		/// </summary>
		/// <param name="face"></param>
		/// <param name="strainPlane"></param>
		/// <param name="deltaN"></param>
		/// <param name="deltaMx"></param>
		/// <param name="deltaMy"></param>
		protected virtual void CalculateFaceStressResultant(MeshFace face, StrainPlane strainPlane, out double deltaN, out double deltaMx, out double deltaMy)
		{
			Point3d[] points = ConcreteSection.Mesh.GetFacePoints(face);

			if (face.IsTriangle)
			{
				deltaN = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))),
					points, 79);
				deltaMx = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
					(y - ConcreteSection.Centroid.Y), points, 79);
				deltaMy = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
					(x - ConcreteSection.Centroid.X), points, 79);
			}

			else if (face.IsQuad)
			{
				deltaN = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))),
					points, 121);
				deltaMx = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
					(y - ConcreteSection.Centroid.Y), points, 121);
				deltaMy = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
					(x - ConcreteSection.Centroid.X), points, 121);
			}
			else
				throw new Exception();
		}

		/// <summary>
		/// Calculate the resultant of all the rebars
		/// </summary>
		/// <param name="strainPlane">The strain plane</param>
		/// <param name="deltaN">The axial force resultant</param>
		/// <param name="deltaMx">The bending moment about X-axis resultant</param>
		/// <param name="deltaMy">The bending moment about Y-axis resultant</param>
		protected virtual void CalculateRebarsIntegration(StrainPlane strainPlane, out double deltaN, out double deltaMx, out double deltaMy)
		{
			double[] deltaNArray = new double[ConcreteSection.Rebars.Length];
			double[] deltaMxArray = new double[ConcreteSection.Rebars.Length];
			double[] deltaMyArray = new double[ConcreteSection.Rebars.Length];

			Parallel.For(0, ConcreteSection.Rebars.Length, (i) =>
			{
				double strain = CalculateStrain(strainPlane, ConcreteSection.Rebars[i].Position);
				double sigmaS = CalculateStressSteel(ConcreteSection.Rebars[i], strain);
				double sigmaC = CalculateSigmaC(strain);

				deltaNArray[i] = (sigmaS - sigmaC) * ConcreteSection.Rebars[i].Area;
				deltaMxArray[i] = (sigmaS - sigmaC) * ConcreteSection.Rebars[i].Area * (ConcreteSection.Rebars[i].Position.Y - ConcreteSection.Centroid.Y);
				deltaMyArray[i] = (sigmaS - sigmaC) * ConcreteSection.Rebars[i].Area * (ConcreteSection.Rebars[i].Position.X - ConcreteSection.Centroid.X);
			});

			deltaN = deltaNArray.Sum();
			deltaMx = deltaMxArray.Sum();
			deltaMy = deltaMyArray.Sum();
		}

		/// <summary>
		/// Calculate the <see cref="FailureDomain.FailureDomainPoint"/> respect the strain plane <paramref name="strainPlane"/>
		/// </summary>
		/// <param name="strainPlane"></param>
		/// <returns></returns>
		protected virtual void CalculateForces(StrainPlane strainPlane, out double N, out double Mx, out double My)
		{
			double deltaNConcrete = 0;
			double deltaMxConcrete = 0;
			double deltaMyConcrete = 0;
			double deltaNRebar = 0;
			double deltaMxRebar = 0;
			double deltaMyRebar = 0;

			try
			{
				CalculateConcreteStressResultant(strainPlane, out deltaNConcrete, out deltaMxConcrete, out deltaMyConcrete);
				CalculateRebarsIntegration(strainPlane, out deltaNRebar, out deltaMxRebar, out deltaMyRebar);
			}
			catch (Exception e)
			{
				_log.Add($"Fail" + e.InnerException);
			}

			N = deltaNConcrete + deltaNRebar;
			Mx = -(deltaMxConcrete + deltaMxRebar);
			My = deltaMyConcrete + deltaMyRebar;
		}

		protected virtual void CalculateRelativeDistance(double teta, out int dMinRebarIndex, out int dMaxRebarIndex, out int dMinVertexIndex, out int dMaxVertexIndex)
		{
			double cosTeta = Math.Cos(teta);
			double sinTeta = Math.Sin(teta);

			double dminSteel = double.MaxValue;
			double dmaxSteel = double.MinValue;
			double dmaxConcrete = double.MinValue;
			double dminConcrete = double.MaxValue;

			dMinRebarIndex = -1;
			dMaxRebarIndex = -1;
			dMaxVertexIndex = -1;
			dMinVertexIndex = -1;

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
				{
					dmaxSteel = w1;
					dMaxRebarIndex = r;
				}					
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
		}

		#endregion

		#region Public override methods

		public override bool Equals(object obj)
		{
			return obj is ConcreteSectionSolver solver &&
				   EqualityComparer<IConcreteSection>.Default.Equals(_concreteSection, solver._concreteSection);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return 23 + EqualityComparer<IConcreteSection>.Default.GetHashCode(_concreteSection);
			}
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("ConcreteSection", _concreteSection);
			info.AddValue("Log", _log);
		}

		#endregion
	}
}
