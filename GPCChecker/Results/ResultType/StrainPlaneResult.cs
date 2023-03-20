using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using GPC.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checker.Results.ResultType
{
	public class StrainPlaneResult : CheckerResultType, IEquatable<StrainPlaneResult>
	{
		protected readonly StrainPlane _strainPlane;
		protected readonly SectionSolver _sectionSolver;
		protected readonly ResultBeamForces _force;

		protected double _sigmaCMax;
		protected double _sigmaSMax;
		protected double _sigmaPMax;
		protected double _sigmaCMin;
		protected double _sigmaSMin;
		protected double _sigmaPMin;
		protected double _epsilonCMax;
		protected double _epsilonPMax;
		protected double _epsilonSMax;
		protected double _epsilonCMin;
		protected double _epsilonSMin;
		protected double _epsilonPMin;
		protected double _netHeight;
		protected double _neutralAxisDistance;
		protected double _neutralAxisAngle;
		protected bool _linearAnalysis;
		protected double _psiR;
		protected double _psiT;

		public double SigmaCMax { get => _sigmaCMax; internal set => _sigmaCMax = value; }
		public double SigmaCMin { get => _sigmaCMin; internal set => _sigmaCMin = value; }
		public double SigmaSMax { get => _sigmaSMax; internal set => _sigmaSMax = value; }
		public double SigmaSMin { get => _sigmaSMin; internal set => _sigmaSMin = value; }
		public double SigmaPMax { get => _sigmaPMax; internal set => _sigmaPMax = value; }
		public double SigmaPMin { get => _sigmaPMin; internal set => _sigmaPMin = value; }
		public double EpsilonCMax { get => _epsilonCMax; internal set => _epsilonCMax = value; }
		public double EpsilonPMax { get => _epsilonPMax; internal set => _epsilonPMax = value; }
		public double EpsilonSMax { get => _epsilonSMax; internal set => _epsilonSMax = value; }
		public double EpsilonCMin { get => _epsilonCMin; internal set => _epsilonCMin = value; }
		public double EpsilonSMin { get => _epsilonSMin; internal set => _epsilonSMin = value; }
		public double EpsilonPMin { get => _epsilonPMin; internal set => _epsilonPMin = value; }
		public double NetHeight { get => _netHeight; internal set => _netHeight = value; }
		public double NeutralAxisDistance { get => _neutralAxisDistance; internal set => _neutralAxisDistance = value; }
		public double NeutralAxisAngle { get => _neutralAxisAngle; internal set => _neutralAxisAngle = value; }
		public bool LinearAnalysis { get => _linearAnalysis; internal set => _linearAnalysis = value; }
		public double PsiR { get => _psiR; internal set => _psiR = value; }
		public double PsiT { get => _psiT; internal set => _psiT = value; }

		internal StrainPlaneResult(IConcreteSection section, ResultBeamForces force, StrainPlane strainPlane, SectionSolver solver, Standard standard, int id = IDUNASSIGNED)
			: base(section, standard, id)
		{
			_force = force ?? throw new ArgumentNullException(nameof(force));
			_strainPlane = strainPlane;
			_sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
			_sigmaCMax = 0;
			_sigmaSMax = 0;
			_sigmaPMax = 0;
			_sigmaCMin = 0;
			_sigmaSMin = 0;
			_sigmaPMin = 0;
			_epsilonCMax = 0;
			_epsilonPMax = 0;
			_epsilonSMax = 0;
			_epsilonCMin = 0;
			_epsilonSMin = 0;
			_epsilonPMin = 0;
			_netHeight = 0;
			_neutralAxisDistance = 0;
			_neutralAxisAngle = 0;
			_linearAnalysis = false;
			_psiR = 0;
			_psiT = 0;
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as StrainPlaneResult);
		}

		public bool Equals(StrainPlaneResult other)
		{
			return !(other is null) &&
				   _sigmaCMax == other._sigmaCMax &&
				   _sigmaSMax == other._sigmaSMax &&
				   _sigmaPMax == other._sigmaPMax &&
				   _sigmaCMin == other._sigmaCMin &&
				   _sigmaSMin == other._sigmaSMin &&
				   _sigmaPMin == other._sigmaPMin &&
				   _epsilonCMax == other._epsilonCMax &&
				   _epsilonPMax == other._epsilonPMax &&
				   _epsilonSMax == other._epsilonSMax &&
				   _epsilonCMin == other._epsilonCMin &&
				   _epsilonSMin == other._epsilonSMin &&
				   _epsilonPMin == other._epsilonPMin &&
				   _netHeight == other._netHeight &&
				   _neutralAxisDistance == other._neutralAxisDistance &&
				   _neutralAxisAngle == other._neutralAxisAngle;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = -23;
				hashCode = hashCode * -17 + _sigmaCMax.GetHashCode();
				hashCode = hashCode * -17 + _sigmaSMax.GetHashCode();
				hashCode = hashCode * -17 + _sigmaPMax.GetHashCode();
				hashCode = hashCode * -17 + _sigmaCMin.GetHashCode();
				hashCode = hashCode * -17 + _sigmaSMin.GetHashCode();
				hashCode = hashCode * -17 + _sigmaPMin.GetHashCode();
				hashCode = hashCode * -17 + _epsilonCMax.GetHashCode();
				hashCode = hashCode * -17 + _epsilonPMax.GetHashCode();
				hashCode = hashCode * -17 + _epsilonSMax.GetHashCode();
				hashCode = hashCode * -17 + _epsilonCMin.GetHashCode();
				hashCode = hashCode * -17 + _epsilonSMin.GetHashCode();
				hashCode = hashCode * -17 + _epsilonPMin.GetHashCode();
				hashCode = hashCode * -17 + _netHeight.GetHashCode();
				hashCode = hashCode * -17 + _neutralAxisDistance.GetHashCode();
				hashCode = hashCode * -17 + _neutralAxisAngle.GetHashCode();
				return hashCode;
			}
		}

		public static bool operator ==(StrainPlaneResult left, StrainPlaneResult right)
		{
			return EqualityComparer<StrainPlaneResult>.Default.Equals(left, right);
		}

		public static bool operator !=(StrainPlaneResult left, StrainPlaneResult right)
		{
			return !(left == right);
		}

		internal StrainPlaneResult CalculateStrainPlaneResult(bool linearAnalysis = false, double psiR = 0, double psiT = 0)
		{
			double sigmaCMax = double.MinValue;
			double sigmaSMax = double.MinValue;
			double sigmaPMax = double.MinValue;
			double sigmaCMin = double.MaxValue;
			double sigmaSMin = double.MaxValue;
			double sigmaPMin = double.MaxValue;

			double epsilonCMax = double.MinValue;
			double epsilonPMax = double.MinValue;
			double epsilonSMax = double.MinValue;
			double epsilonCMin = double.MaxValue;
			double epsilonSMin = double.MaxValue;
			double epsilonPMin = double.MaxValue;

			double netHeight;
			double neutralAxisDistance = double.NaN;
			double neutralAxisAngle;

			if (_strainPlane != null)
			{
				for (int i = 0; i < _section.Shape.Fill.Count; i++)
				{
					double strain = _strainPlane.GetStrain(_section.Shape.Fill[i]);
					if (strain > epsilonCMax)
					{
						epsilonCMax = strain;

						double sigmaCMaxbuffer;
						if (linearAnalysis)
							sigmaCMaxbuffer = _sectionSolver.CalculateElasticSigmaC(epsilonCMax);
						else
						{
							var buff = _section.ConcreteMaterial.CalculateDesignStressConcrete(_standard, epsilonCMax);
							if (buff <= 0)
								sigmaCMaxbuffer = buff;
							else
							{
								if (_sectionSolver.ConsiderTensileConcrete == true)
									sigmaCMaxbuffer = buff;
								else
									sigmaCMaxbuffer = 0;
							}
						}

						if (sigmaCMaxbuffer > sigmaCMax)
							sigmaCMax = sigmaCMaxbuffer;

					}
					if (strain < epsilonCMin)
					{
						epsilonCMin = strain;
						double sigmaCMinbuffer;
						if (linearAnalysis)
							sigmaCMinbuffer = _sectionSolver.CalculateElasticSigmaC(epsilonCMin);
						else
						{
							var buff = _section.ConcreteMaterial.CalculateDesignStressConcrete(_standard, epsilonCMin);
							if (buff <= 0)
								sigmaCMinbuffer = buff;
							else
							{
								if (_sectionSolver.ConsiderTensileConcrete == true)
									sigmaCMinbuffer = buff;
								else
									sigmaCMinbuffer = 0;
							}
						}

						if (sigmaCMinbuffer < sigmaCMin)
							sigmaCMin = sigmaCMinbuffer;
					}
				}

				for (int r = 0; r < _section.RebarsCount; r++)
				{
					ReinforcedConcreteRebar rebar = _section.Rebars.ElementAt(r);
					double strain = _strainPlane.GetStrain(rebar.Position);

					if (rebar.EpsilonP > 0)
					{
						if (strain > epsilonPMax)
						{
							epsilonPMax = strain;

							if (linearAnalysis)
								sigmaPMax = _sectionSolver.CalculateElasticSigmaS(psiT, rebar, strain);
							else
								sigmaPMax = rebar.RebarMaterial.CalculateDesignStress(_standard, strain, rebar.EpsilonP);
						}
						if (strain < epsilonPMin)
						{
							epsilonPMin = strain;

							if (linearAnalysis)
								sigmaPMin = _sectionSolver.CalculateElasticSigmaS(psiT, rebar, strain);
							else
								sigmaPMin = rebar.RebarMaterial.CalculateDesignStress(_standard, strain, rebar.EpsilonP);
						}
					}
					else
					{
						if (strain > epsilonSMax)
						{
							epsilonSMax = strain;

							if (linearAnalysis)
								sigmaSMax = _sectionSolver.CalculateElasticSigmaS(psiR, rebar, strain);
							else
								sigmaSMax = rebar.RebarMaterial.CalculateDesignStress(_standard, strain, rebar.EpsilonP);
						}
						if (strain < epsilonSMin)
						{
							epsilonSMin = strain;

							if (linearAnalysis)
								sigmaSMin = _sectionSolver.CalculateElasticSigmaS(psiR, rebar, strain);
							else
								sigmaSMin = rebar.RebarMaterial.CalculateDesignStress(_standard, strain, rebar.EpsilonP);
						}
					}
				}

				if (linearAnalysis)
				{
					epsilonSMax *= (1 + psiR);
					epsilonSMin *= (1 + psiR);
					epsilonPMax *= (1 + psiT);
					epsilonPMin *= (1 + psiT);
				}

				var neutralAxis = _strainPlane.GetNeutralAxisRespectReferencePoint();
				if (neutralAxis != null)
					neutralAxis.Move(_strainPlane.ReferencePoint.X, _strainPlane.ReferencePoint.Y);

				double angle = _strainPlane.Teta;
				neutralAxisAngle = angle.ToDegrees();

				var dist = _sectionSolver.CalculateMaxMinSectionDistances(angle);

				double dmaxRebar = dist.dmaxRebar;
				double dmaxConcrete = dist.dmaxConcrete;
				double dminConcrete = dist.dminConcrete;
				int dMaxVertexIndex = dist.dMaxVertexIndex;

				if (_section.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC)
					netHeight = dmaxConcrete - dminConcrete;
				else if (_section.RebarsCount == 0)
					netHeight = 0;
				else
					netHeight = dmaxRebar - dminConcrete;

				if (neutralAxis != null)
					neutralAxisDistance = new Line3d(neutralAxis).DistanceTo(_section.Shape.Fill[dMaxVertexIndex]);
			}
			else
			{
				sigmaCMax = double.NaN;
				sigmaSMax = double.NaN;
				sigmaPMax = double.NaN;
				sigmaCMin = double.NaN;
				sigmaSMin = double.NaN;
				sigmaPMin = double.NaN;
				epsilonCMax = double.NaN;
				epsilonPMax = double.NaN;
				epsilonSMax = double.NaN;
				epsilonCMin = double.NaN;
				epsilonSMin = double.NaN;
				epsilonPMin = double.NaN;
				netHeight = double.NaN;
				neutralAxisDistance = double.NaN;
				neutralAxisAngle = double.NaN;
			}

			return new StrainPlaneResult(_section, _force, _strainPlane, _sectionSolver, _standard, _id)
			{
				SigmaCMax = sigmaCMax,
				SigmaSMax = sigmaSMax,
				SigmaPMax = sigmaPMax,
				SigmaCMin = sigmaCMin,
				SigmaSMin = sigmaSMin,
				SigmaPMin = sigmaPMin,
				EpsilonCMax = epsilonCMax,
				EpsilonSMax = epsilonSMax,
				EpsilonPMax = epsilonPMax,
				EpsilonCMin = epsilonCMin,
				EpsilonSMin = epsilonSMin,
				EpsilonPMin = epsilonPMin,
				NetHeight = netHeight,
				NeutralAxisAngle = neutralAxisAngle,
				NeutralAxisDistance = neutralAxisDistance,
				LinearAnalysis = linearAnalysis,
				PsiR = psiR,
				PsiT = psiT,
			};
		}
	}
}
