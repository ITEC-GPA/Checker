using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace GPC.Checkers.Concrete.Results
{
    [Serializable]
    public class FailureDomainResult : CheckerResultType, ISerializable
    {
        #region Variables

        protected readonly SectionSolver _sectionSolver;
        protected readonly FailureDomain _failureDomain;
        protected List<FailureDomain.FailureDomainForce> _forces;
        protected Checkers.SectionChecker.SectionOptions _sectionOption;

        protected int _failureSectionSubdivision;

        #endregion

        #region Properties

        public FailureDomain Domain => _failureDomain;

        internal SectionSolver.FailureDomainTypes FailureDomainAnalysisType => _failureDomain.FailureDomainAnalysisTypes;

        public CoordinateSystem CoordinateSystem => _sectionOption.ForceReferenceCoordinateSystem;

        public SectionSolver.FailureAnalysisTypes FailureAnalysisType => _sectionOption.FailureAnalysisType;

        #endregion

        #region Constructor

        public FailureDomainResult(IConcreteSection section, FailureDomain failureDomain, IEnumerable<ResultBeamForces> forces,
            SectionSolver solver, Standard standard, Checkers.SectionChecker.SectionOptions sectionOption, int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _failureDomain = failureDomain ?? throw new ArgumentNullException(nameof(failureDomain));
            _sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
            _forces = new List<FailureDomain.FailureDomainForce>();
            _sectionOption = sectionOption ?? throw new ArgumentNullException();

            if (forces != null)
            {
                var forcesList = forces.ToList();

                for (int i = 0; i < forces.Count(); i++)
                {
                    if(_failureDomain.FailureDomainAnalysisTypes == SectionSolver.FailureDomainTypes.Plastic)
                        _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], 
                            _sectionSolver.CalculatePlasticDomainPoint(forcesList[i].ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType)));
                    else
                        _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], 
                            _sectionSolver.CalculateElasticDomainPoint(forcesList[i].ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType)));
                }
            }

            _failureSectionSubdivision = 20; 
        }

		protected FailureDomainResult(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
            _sectionSolver = (SectionSolver)info.GetValue("SectionSolver", typeof(SectionSolver));
            _failureDomain = (FailureDomain)info.GetValue("FailureDomain", typeof(FailureDomain));
            _sectionOption = (Checkers.SectionChecker.SectionOptions)info.GetValue("SectionOption", typeof(Checkers.SectionChecker.SectionOptions));
            _forces = (List<FailureDomain.FailureDomainForce>)info.GetValue("Forces", typeof(List<FailureDomain.FailureDomainForce>));
            _failureSectionSubdivision = info.GetInt32("Subdivision");
        }

		#endregion

		#region Public Async Methods

		/// <summary>
		/// Adds a new single force asynchronously
		/// </summary>
		/// <param name="force">The force to add</param>
		/// <returns>The corresponding domain point</returns>
		/// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
		/// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
		public async Task<FailureDomain.FailureDomainPoint> AddForceAsync(ResultBeamForces force)
        {
            if (force is null)
            {
                throw new ArgumentNullException(nameof(force));
            }
            else if (force.Id == -1)
            {
                throw new ArgumentException(nameof(force));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() => {
                if (_failureDomain.FailureDomainAnalysisTypes == SectionSolver.FailureDomainTypes.Plastic)
                    failureDomainPoint = _sectionSolver.CalculatePlasticDomainPoint(force.ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);
                else
                    failureDomainPoint = _sectionSolver.CalculateElasticDomainPoint(force.ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);

                _forces.Add(new FailureDomain.FailureDomainForce(force, failureDomainPoint));
            });

            return failureDomainPoint;
        }

        /// <summary>
        /// Adds a new range of forces asynchronously
        /// </summary>
        /// <param name="forces">The forces array</param>
        /// <returns>The corresponding points in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces contains items with Id == -1</exception>
        public async Task<FailureDomain.FailureDomainPoint[]> AddForcesAsync(IEnumerable<ResultBeamForces> forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Any(f => f.Id == -1))
            {
                throw new ArgumentException(nameof(forces));
            }

            FailureDomain.FailureDomainPoint[] failureDomainPoint = null;

            await Task.Run(() => {

                var forcesList = forces.ToList();
                failureDomainPoint = new FailureDomain.FailureDomainPoint[forcesList.Count];

                for (int i = 0; i < forcesList.Count(); i++)
                {
                    FailureDomain.FailureDomainPoint point;

                    if (_failureDomain.FailureDomainAnalysisTypes == SectionSolver.FailureDomainTypes.Plastic)
						point = _sectionSolver.CalculatePlasticDomainPoint(forcesList[i].ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);
                    else
                        point = _sectionSolver.CalculateElasticDomainPoint(forcesList[i].ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);

                    _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], point));
                    failureDomainPoint[i] = point;
                }

            });

            return failureDomainPoint;
        }

        /// <summary>
        /// Update an extisting force racalculating the domain point asynchronously
        /// </summary>
        /// <param name="id">The id of the original force</param>
        /// <param name="force">The new force</param>
        /// <returns>The new corresponding point in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        /// <exception cref="KeyNotFoundException">Thrown when did not found a force with the given id</exception>
        public async Task<FailureDomain.FailureDomainPoint> UpdateForceAsync(int id, ResultBeamForces force)
        {
            if (force is null)
            {
                throw new ArgumentNullException(nameof(force));
            }
            else if (force.Id == -1)
            {
                throw new ArgumentException(nameof(force));
            }

            int index = _forces.FindIndex(f => f.Id == id);
            if (index < 0)
            {
                throw new KeyNotFoundException(nameof(id));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() => {

                if (_failureDomain.FailureDomainAnalysisTypes == SectionSolver.FailureDomainTypes.Plastic)
                    failureDomainPoint = _sectionSolver.CalculatePlasticDomainPoint(force.ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);
                else
                    failureDomainPoint = _sectionSolver.CalculateElasticDomainPoint(force.ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);

                _forces.RemoveAt(index);
                _forces.Insert(index, new FailureDomain.FailureDomainForce(force, failureDomainPoint));
            });

            return failureDomainPoint;
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant value of axial force
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only axial force of <paramref name="forces"/> is used</remarks>
        public async Task<FailureDomain2d> CalculateDomainConstantAxialForceAsync(ResultBeamForces forces)
		{
            FailureDomain2d failureDomain2D = null;

            await Task.Run(() => {
                failureDomain2D = CalculateDomainConstantAxialForce(forces.ConvertToForceTuple(CoordinateSystem));
            });

            return failureDomain2D;
        }

        /// <summary>
        /// Calculate the plastic failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        public async Task<FailureDomain2d> CalculatePlasticDomainConstantMomentsRatioAsync(ResultBeamForces forces)
        {
            FailureDomain2d failureDomain2D = null;

            await Task.Run(() => {
                failureDomain2D = CalculateFailureDomainCostantMomentsRatio(forces.ConvertToForceTuple(CoordinateSystem));
            });

            return failureDomain2D;
        }

        /// <summary>
        /// Calculate the elastic failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        public async Task<FailureDomain2d> CalculateElasticDomainConstantMomentsRatioAsync(ResultBeamForces forces)
        {
            FailureDomain2d failureDomain2D = null;

            await Task.Run(() => {
                failureDomain2D = CalculateElasticDomainMomentsRatio(forces.ConvertToForceTuple(CoordinateSystem));
            });

            return failureDomain2D;
        }

        /// <summary>
        /// Calculate a new single force asynchronously
        /// </summary>
        /// <param name="force">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <remarks>Force is not added to list of forces</remarks>
        public async Task<FailureDomain.FailureDomainPoint> CalculateForceAsync(ResultBeamForces force)
		{
            if (force is null)
            {
                throw new ArgumentNullException(nameof(force));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() => {
                if (_failureDomain.FailureDomainAnalysisTypes == SectionSolver.FailureDomainTypes.Plastic)
                    failureDomainPoint = _sectionSolver.CalculatePlasticDomainPoint(force.ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);
                else
                    failureDomainPoint = _sectionSolver.CalculateElasticDomainPoint(force.ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);
            });

            return failureDomainPoint;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a new single force
        /// </summary>
        /// <param name="force">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        public FailureDomain.FailureDomainPoint AddForce(ResultBeamForces force)
        {
            if (force is null || force.Id == -1)
            {
                throw new ArgumentNullException(nameof(force));
            }
            else if (force.Id == -1)
            {
                throw new ArgumentException(nameof(force));
            }

            FailureDomain.FailureDomainPoint point;

            if (_failureDomain.FailureDomainAnalysisTypes == SectionSolver.FailureDomainTypes.Plastic)
                point = _sectionSolver.CalculatePlasticDomainPoint(force.ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);
            else
                point = _sectionSolver.CalculateElasticDomainPoint(force.ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);

            _forces.Add(new FailureDomain.FailureDomainForce(force, point));

            return point;
        }

        /// <summary>
        /// Adds a new range of forces
        /// </summary>
        /// <param name="forces">The forces array</param>
        /// <returns>The corresponding points in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces contains items with Id == -1</exception>
        public FailureDomain.FailureDomainPoint[] AddForces(ResultBeamForces[] forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Any(f => f.Id == -1))
            {
                throw new ArgumentException(nameof(forces));
            }

            List<ResultBeamForces> forcesList = forces.ToList();
            var failureDomainPoint = new FailureDomain.FailureDomainPoint[forcesList.Count];

            for (int i = 0; i < forcesList.Count(); i++)
            {
                FailureDomain.FailureDomainPoint point;

                if (_failureDomain.FailureDomainAnalysisTypes == SectionSolver.FailureDomainTypes.Plastic)
                    point = _sectionSolver.CalculatePlasticDomainPoint(forcesList[i].ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);
                else
                    point = _sectionSolver.CalculateElasticDomainPoint(forcesList[i].ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);

                _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], point));
                failureDomainPoint[i] = point;
            }

            return failureDomainPoint;
        }

        /// <summary>
        /// Calculate a new single force
        /// </summary>
        /// <param name="force">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <remarks>Force is not added to list of forces</remarks>
        public FailureDomain.FailureDomainPoint CalculateForce(ResultBeamForces force)
        {
            if (force is null)
            {
                throw new ArgumentNullException(nameof(force));
            }

			FailureDomain.FailureDomainPoint failureDomainPoint;
			if (_failureDomain.FailureDomainAnalysisTypes == SectionSolver.FailureDomainTypes.Plastic)
                    failureDomainPoint = _sectionSolver.CalculatePlasticDomainPoint(force.ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);
                else
                    failureDomainPoint = _sectionSolver.CalculateElasticDomainPoint(force.ConvertToForceTuple(CoordinateSystem), CoordinateSystem, FailureAnalysisType);

            return failureDomainPoint;
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant value of axial force
        /// </summary>
        /// <param name="forces">Input forces</param>
        /// <returns>New FailureDomain2d</returns>
        /// <exception cref="ArgumentException"></exception>
        public virtual FailureDomainResult2d CalculateDomainConstantAxialForce(ResultBeamForces forces)
        {
            return new FailureDomainResult2d(_section, CalculateDomainConstantAxialForce(forces.ConvertToForceTuple(CoordinateSystem)), 
                new ResultBeamForces[] { forces }, _sectionSolver, _standard, _sectionOption);
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        public virtual FailureDomainResult2d CalculateFailureDomainCostantMomentsRatio(ResultBeamForces forces)
        {
            return new FailureDomainResult2d(_section, CalculateFailureDomainCostantMomentsRatio(forces.ConvertToForceTuple(CoordinateSystem)),
                new ResultBeamForces[] { forces }, _sectionSolver, _standard, _sectionOption);
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant value of axial force
        /// </summary>
        /// <param name="forces">Input forces</param>
        /// <returns>New FailureDomain2d</returns>
        /// <exception cref="ArgumentException"></exception>
        protected virtual FailureDomain2d CalculateDomainConstantAxialForce(ForceTuple forces)
        {
            if (forces == null)
                throw new ArgumentException();

            if (forces.N > _failureDomain.DomainPoints.FirstOrDefault().FirstOrDefault().NRd || forces.N < _failureDomain.DomainPoints.LastOrDefault().LastOrDefault().NRd)
                return null;

            FailureDomain.FailureDomainPoint[] points = new FailureDomain.FailureDomainPoint[_failureDomain.DomainPoints.Length];

            for (int i = 0; i < _failureDomain.DomainPoints.Length; i++)
            {
                for (int j = 1; j < _failureDomain.DomainPoints[i].Length; j++)
                {
                    if (forces.N == _failureDomain.DomainPoints[i][j].NRd)
                    {
                        points[i] = _failureDomain.DomainPoints[i][j];
                        break;
                    }

                    if (forces.N > _failureDomain.DomainPoints[i][j].NRd && forces.N < _failureDomain.DomainPoints[i][j - 1].NRd)
                    {
                        double mx = Utilities.Maths.Interpolation.GetLinearInterpolation(_failureDomain.DomainPoints[i][j - 1].NRd, _failureDomain.DomainPoints[i][j].NRd,
                            _failureDomain.DomainPoints[i][j - 1].MxRd, _failureDomain.DomainPoints[i][j].MxRd, forces.N);
                        double my = Utilities.Maths.Interpolation.GetLinearInterpolation(_failureDomain.DomainPoints[i][j - 1].NRd, _failureDomain.DomainPoints[i][j].NRd,
                            _failureDomain.DomainPoints[i][j - 1].MyRd, _failureDomain.DomainPoints[i][j].MyRd, forces.N);

                        points[i] = new FailureDomain.FailureDomainPoint(new ForceTuple(forces.N, mx, my),
                            _failureDomain.DomainPoints[i][j].FailureIndex, _failureDomain.DomainPoints[i][j].StrainPlane);

                        break;
                    }
                }
            }

            return new FailureDomain2d(points, FailureDomainResult2d.DomainTypes.CostantN);
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        protected virtual FailureDomain2d CalculateFailureDomainCostantMomentsRatio(ForceTuple forces)
		{
            if (_failureDomain.FailureDomainAnalysisTypes == SectionSolver.FailureDomainTypes.Plastic)
                return CalculatePlasticDomainCostantMomentsRatio(forces, _failureSectionSubdivision);
            else
                return CalculateElasticDomainMomentsRatio(forces, _failureSectionSubdivision);
        }

        protected virtual FailureDomain2d CalculatePlasticDomainCostantMomentsRatio(ForceTuple forces, int subdivision = 20)
        {
            if (subdivision <= 2)
                throw new Exception();

            ForceTuple[] forceTuples = CalculateRadialForces(forces, subdivision);
            FailureDomain.FailureDomainPoint[] points = new FailureDomain.FailureDomainPoint[forceTuples.Length];

            Parallel.For(0, forceTuples.Length, (i) =>
            {
                points[i] = _sectionSolver.CalculatePlasticDomainPoint(forceTuples[i], CoordinateSystem, FailureAnalysisType);
            });

            return new FailureDomain2d(points, FailureDomainResult2d.DomainTypes.CostantMxMy);
        }

        protected virtual FailureDomain2d CalculateElasticDomainMomentsRatio(ForceTuple forces, int subdivision = 10)
        {
            if (subdivision <= 2)
                throw new Exception();

            FailureDomain.FailureDomainPoint[] points = new FailureDomain.FailureDomainPoint[2 * subdivision + 2];

            ForceTuple[] forceTuples = CalculateRadialForces(forces, subdivision);

            Parallel.For(0, forceTuples.Length, (i) =>
            {
                points[i] = _sectionSolver.CalculateElasticDomainPoint(forceTuples[i], CoordinateSystem, FailureAnalysisType);
            });

            return new FailureDomain2d(points, FailureDomainResult2d.DomainTypes.CostantMxMy);
        }

        protected ForceTuple[] CalculateRadialForces(ForceTuple forces, int subdivision = 10)
		{
            ForceTuple[] forceTuples = new ForceTuple[2 * subdivision];

            double nMax = ConcreteSection.AreaRebars * ConcreteSection.Rebars.FirstOrDefault().RebarMaterial.Fyk / 2.0;
            double nMin = ConcreteSection.Area * ConcreteSection.ConcreteMaterial.StressStrainTableCompression.GetMinimumStress() / 4.0;
            
            for (int i = 0; i < subdivision / 2.0; i++)
            {
                forceTuples[i] = new ForceTuple(
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMax, nMin / 2.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, forces.Mx, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, forces.My, i));

                forceTuples[subdivision / 2 + i] = new ForceTuple(
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMin / 2.0, nMin, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, forces.Mx, 0.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, forces.My, 0.0, i));

                forceTuples[2 * subdivision - 1 - i] = new ForceTuple(
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMax, nMin / 2.0, i + 1),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, -forces.Mx, i + 1),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, -forces.My, i + 1));

                forceTuples[subdivision / 2 + subdivision - 1 - i] = new ForceTuple(
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMin / 2.0, nMin, i + 1),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, -forces.Mx, 0.0, i + 1),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, -forces.My, 0.0, i + 1));
            }

            return forceTuples;
        }

		#endregion

		#region Enumerator Methods

		/// <summary>
		/// Tells if there is a force with the given id
		/// </summary>
		/// <param name="id">The id to check</param>
		/// <returns>True if the force exists</returns>
		public bool ContainsForceWithId(int id)
        {
            return _forces.Any(force => force.Id == id);
        }

        public IEnumerator<FailureDomain.FailureDomainForce> GetEnumerator()
        {
            return _forces.GetEnumerator();
        }

        public FailureDomain.FailureDomainForce[] GetFailureDomainForces()
        {
            return _forces.ToArray();
        }

		#endregion

		#region Equals, hashcode, operators

		public List<string> GetLog()
		{
            return _sectionSolver.GetLog();
		}

		public override bool Equals(object obj)
		{
            if (ReferenceEquals(this, obj))
                return true;

            return obj is FailureDomainResult result &&
				   base.Equals(obj) &&
				   EqualityComparer<SectionSolver>.Default.Equals(_sectionSolver, result._sectionSolver) &&
				   EqualityComparer<FailureDomain>.Default.Equals(_failureDomain, result._failureDomain) &&
				   EqualityComparer<Checkers.SectionChecker.SectionOptions>.Default.Equals(_sectionOption, result._sectionOption);
		}

		public override int GetHashCode()
		{
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _sectionSolver.GetHashCode();
                hashCode = hashCode * -17 + _failureDomain.GetHashCode();
                hashCode = hashCode * -17 + _sectionOption.GetHashCode();
                return hashCode;
            }
        }

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
            info.AddValue("SectionSolver", _sectionSolver);
            info.AddValue("FailureDomain", _failureDomain);
            info.AddValue("SectionOption", _sectionOption);
            info.AddValue("Forces", _forces);
            info.AddValue("Subdivision", _failureSectionSubdivision);
        }

        public static bool operator ==(FailureDomainResult left, FailureDomainResult right)
		{
			return EqualityComparer<FailureDomainResult>.Default.Equals(left, right);
		}

		public static bool operator !=(FailureDomainResult left, FailureDomainResult right)
		{
			return !(left == right);
		}

		#endregion
	}
}