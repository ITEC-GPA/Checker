using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;

namespace GPC.Checker.Results.ResultType
{
	public class StrainPlaneResult : CheckerResultType, IEquatable<StrainPlaneResult>
	{
		protected StrainPlane _strainPlane;
		protected SectionSolver _sectionSolver;
		protected readonly ResultBeamForces _force;

		protected double _sigmaCMax;
		protected double _sigmaSMax;
		protected double _sigmaPMax;
        protected double _sigmaSSMax;
        protected double _sigmaCMin;
		protected double _sigmaSMin;
		protected double _sigmaPMin;
        protected double _sigmaSSMin;
        protected double _epsilonCMax;
		protected double _epsilonPMax;
		protected double _epsilonSMax;
        protected double _epsilonSSMax;
        protected double _epsilonCMin;
		protected double _epsilonSMin;
		protected double _epsilonPMin;
        protected double _epsilonSSMin;
        protected double _netHeight;
		protected double _neutralAxisDistance;
		protected double _neutralAxisAngle;
		protected bool _linearAnalysis;
		protected double _psiR;
		protected double _psiT;

		public double SigmaCMax { get => _sigmaCMax; internal set => _sigmaCMax = value; }
		public double SigmaCMin { get => _sigmaCMin; internal set => _sigmaCMin = value; }
		public double SigmaSMax { get => _sigmaSMax; internal set => _sigmaSMax = value; }
        /// <summary>
        /// Max sigma structural steel.
        /// </summary>
        public double SigmaSSMax { get => _sigmaSSMax; internal set => _sigmaSSMax = value; }
        public double SigmaSMin { get => _sigmaSMin; internal set => _sigmaSMin = value; }
		public double SigmaPMax { get => _sigmaPMax; internal set => _sigmaPMax = value; }
		public double SigmaPMin { get => _sigmaPMin; internal set => _sigmaPMin = value; }
        /// <summary>
        /// Min sigma structural steel.
        /// </summary>
        public double SigmaSSMin { get => _sigmaSSMin; internal set => _sigmaSSMin = value; }
        public double EpsilonCMax { get => _epsilonCMax; internal set => _epsilonCMax = value; }
		public double EpsilonPMax { get => _epsilonPMax; internal set => _epsilonPMax = value; }
		public double EpsilonSMax { get => _epsilonSMax; internal set => _epsilonSMax = value; }
        /// <summary>
        /// Max strain structural steel.
        /// </summary>
        public double EpsilonSSMax { get => _epsilonSSMax; internal set => _epsilonSSMax = value; }
        public double EpsilonCMin { get => _epsilonCMin; internal set => _epsilonCMin = value; }
		public double EpsilonSMin { get => _epsilonSMin; internal set => _epsilonSMin = value; }
		public double EpsilonPMin { get => _epsilonPMin; internal set => _epsilonPMin = value; }
        /// <summary>
        /// Min strain structural steel.
        /// </summary>
        public double EpsilonSSMin { get => _epsilonSSMin; internal set => _epsilonSSMin = value; }
        public double NetHeight { get => _netHeight; internal set => _netHeight = value; }
		public double NeutralAxisDistance { get => _neutralAxisDistance; internal set => _neutralAxisDistance = value; }
		public double NeutralAxisAngle { get => _neutralAxisAngle; internal set => _neutralAxisAngle = value; }
		public bool LinearAnalysis { get => _linearAnalysis; internal set => _linearAnalysis = value; }
		public double PsiR { get => _psiR; internal set => _psiR = value; }
		public double PsiT { get => _psiT; internal set => _psiT = value; }
		public ResultBeamForces ResultBeamForce { get => _force; }
		public StrainPlane StrainPlane { get => _strainPlane; internal set => _strainPlane = value; }
        public SectionSolver SectionSolver { get => _sectionSolver; internal set => _sectionSolver = value; }

		internal StrainPlaneResult(IConcreteSection section, ResultBeamForces force, StrainPlane strainPlane, SectionSolver solver, Standard standard,
			int id = IDUNASSIGNED, Standard standardStructuralSteel = null)
			: base(section, standard, id, standardStructuralSteel)
		{
			_force = force ?? throw new ArgumentNullException(nameof(force));
			_strainPlane = strainPlane;
			_sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
			_sigmaCMax = 0;
			_sigmaSMax = 0;
			_sigmaPMax = 0;
            _sigmaSSMax = 0;
            _sigmaCMin = 0;
			_sigmaSMin = 0;
			_sigmaPMin = 0;
            _sigmaSSMin = 0;
            _epsilonCMax = 0;
			_epsilonPMax = 0;
			_epsilonSMax = 0;
            _epsilonSSMax = 0;
            _epsilonCMin = 0;
			_epsilonSMin = 0;
			_epsilonPMin = 0;
            _epsilonSSMin = 0;
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
                   _sigmaSSMax == other._sigmaSSMax &&
                   _sigmaCMin == other._sigmaCMin &&
				   _sigmaSMin == other._sigmaSMin &&
				   _sigmaPMin == other._sigmaPMin &&
                   _sigmaSSMin == other._sigmaSSMin &&
                   _epsilonCMax == other._epsilonCMax &&
				   _epsilonPMax == other._epsilonPMax &&
				   _epsilonSMax == other._epsilonSMax &&
                   _epsilonSSMax == other._epsilonSSMax &&
                   _epsilonCMin == other._epsilonCMin &&
				   _epsilonSMin == other._epsilonSMin &&
				   _epsilonPMin == other._epsilonPMin &&
                   _epsilonSSMin == other._epsilonSSMin &&
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
                hashCode = hashCode * -17 + _sigmaSSMax.GetHashCode();
                hashCode = hashCode * -17 + _sigmaCMin.GetHashCode();
				hashCode = hashCode * -17 + _sigmaSMin.GetHashCode();
				hashCode = hashCode * -17 + _sigmaPMin.GetHashCode();
                hashCode = hashCode * -17 + _sigmaSSMin.GetHashCode();
                hashCode = hashCode * -17 + _epsilonCMax.GetHashCode();
				hashCode = hashCode * -17 + _epsilonPMax.GetHashCode();
				hashCode = hashCode * -17 + _epsilonSMax.GetHashCode();
                hashCode = hashCode * -17 + _epsilonSSMax.GetHashCode();
                hashCode = hashCode * -17 + _epsilonCMin.GetHashCode();
				hashCode = hashCode * -17 + _epsilonSMin.GetHashCode();
				hashCode = hashCode * -17 + _epsilonPMin.GetHashCode();
                hashCode = hashCode * -17 + _epsilonSSMin.GetHashCode();
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
			return _strainPlane.CalculateStrainPlaneResult(_section, _force, _sectionSolver, _standard, linearAnalysis, psiR, psiT);
		}
	}
}
