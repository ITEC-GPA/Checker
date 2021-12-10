using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace GPC.Checkers.Concrete.Results
{
    public class FailureDomainResult2d : CheckerResultType
    {
        public enum DomainTypes
		{
            CostantN,
            CostantMxMy,
		}

        protected readonly SectionSolver _sectionSolver;
        protected readonly FailureDomain2d _failureDomain;
        protected List<FailureDomain.FailureDomainForce> _forces;
        protected readonly DomainTypes _domainType;

        public FailureDomain2d Domain => _failureDomain;

        public FailureDomainResult2d(
            IConcreteSection section,
            FailureDomain2d failureDomain,
            IEnumerable<ResultBeamForces> forces,
            SectionSolver solver,
            Standard standard,
            DomainTypes domainType,
            int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _failureDomain = failureDomain ?? throw new ArgumentNullException(nameof(failureDomain));
            _sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
            _forces = new List<FailureDomain.FailureDomainForce>();
            _domainType = domainType;

            if (forces != null)
            {
                var forcesList = forces.ToList();

                for (int i = 0; i < forces.Count(); i++)
                {
                    _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], _failureDomain.GetDomainPoint(ConvertForceToPoint(forcesList[i])).failureDomainPoint));
                }
            }
        }

        #region Public Async Methods

        /// <summary>
        /// Adds a new single force asynchronously
        /// </summary>
        /// <param name="forces">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        public async Task<FailureDomain.FailureDomainPoint> AddForceAsync(ResultBeamForces forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Id == -1)
            {
                throw new ArgumentException(nameof(forces));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() => {
                _forces.Add(new FailureDomain.FailureDomainForce(forces, _failureDomain.GetDomainPoint(ConvertForceToPoint(forces)).failureDomainPoint));
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
                    var point = _failureDomain.GetDomainPoint(ConvertForceToPoint(forcesList[i]));
                    _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], point.failureDomainPoint));

                    _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], point.failureDomainPoint));
                    failureDomainPoint[i] = point.failureDomainPoint;
                }
            });

            return failureDomainPoint;
        }

        /// <summary>
        /// Update an extisting force racalculating the domain point asynchronously
        /// </summary>
        /// <param name="id">The id of the original force</param>
        /// <param name="forces">The new force</param>
        /// <returns>The new corresponding point in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        /// <exception cref="KeyNotFoundException">Thrown when did not found a force with the given id</exception>
        public async Task<FailureDomain.FailureDomainPoint> UpdateForceAsync(int id, ResultBeamForces forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Id == -1)
            {
                throw new ArgumentException(nameof(forces));
            }

            int index = _forces.FindIndex(f => f.Id == id);
            if (index < 0)
            {
                throw new KeyNotFoundException(nameof(id));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() => {

                var point = _failureDomain.GetDomainPoint(ConvertForceToPoint(forces));

                _forces.RemoveAt(index);
                _forces.Insert(index, new FailureDomain.FailureDomainForce(forces, point.failureDomainPoint));
            });

            return failureDomainPoint;
        }

		#endregion

		#region Internal Methods

		/// <summary>
		/// Adds a new single force
		/// </summary>
		/// <param name="forces">The force to add</param>
		/// <returns>The corresponding domain point</returns>
		/// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
		/// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
		internal FailureDomain.FailureDomainPoint AddForce(ResultBeamForces forces)
        {
            if (forces is null || forces.Id == -1)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Id == -1)
            {
                throw new ArgumentException(nameof(forces));
            }

            var point = _failureDomain.GetDomainPoint(ConvertForceToPoint(forces));
            _forces.Add(new FailureDomain.FailureDomainForce(forces, point.failureDomainPoint));

            return point.failureDomainPoint;
        }

        /// <summary>
        /// Adds a new range of forces
        /// </summary>
        /// <param name="forces">The forces array</param>
        /// <returns>The corresponding points in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces contains items with Id == -1</exception>
        internal FailureDomain.FailureDomainPoint[] AddForces(ResultBeamForces[] forces)
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
                var point = _failureDomain.GetDomainPoint(ConvertForceToPoint(forcesList[i]));
                _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], point.failureDomainPoint));

                failureDomainPoint[i] = point.failureDomainPoint;
            }

            return failureDomainPoint;
        }

        protected Point2d ConvertForceToPoint(ResultBeamForces force)
		{
            ForceTuple forceTuple = force.ConvertToForceTuple(_section.Centroid);

            if (_domainType == DomainTypes.CostantN)
            {
                return new Point2d(forceTuple.Mx, forceTuple.My);
            }
            else
            {
                return new Point2d(Math.Sqrt(Math.Pow(forceTuple.Mx, 2) + Math.Pow(forceTuple.My, 2)), forceTuple.N);
            }
        }

        #endregion

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

        public List<string> GetLog()
		{
            return _sectionSolver.GetLog();
		}
    }
}