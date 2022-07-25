using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using GPC.Utilities.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace GPC.Checkers.Concrete.Results
{
    [Serializable]
    public class FailureDomainResult2d : CheckerResultType, ISerializable
    {
        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum DomainTypes
		{
            [Description("Constant N")]
            ConstantN,

            [Description("Constant eccentricity")]
            ConstantMxMy,
		}

        protected readonly SectionSolver _sectionSolver;
        protected readonly FailureDomain2d _failureDomain2d;
        protected readonly DomainTypes _domainType;
        protected readonly Checkers.SectionChecker.SectionOptions _sectionOption;
        protected List<FailureDomain.FailureDomainForce2d> _forces;

        public FailureDomain2d Domain => _failureDomain2d;

        public FailureDomainResult2d(
            IConcreteSection section,
            FailureDomain2d failureDomain,
            IEnumerable<ResultBeamForces> forces,
            SectionSolver solver,
            Standard standard,
            Checkers.SectionChecker.SectionOptions options,
            int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _failureDomain2d = failureDomain ?? throw new ArgumentNullException(nameof(failureDomain));
            _sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
            _forces = new List<FailureDomain.FailureDomainForce2d>();
            _domainType = failureDomain.DomainType;
            _sectionOption = options;
            if (forces != null)
            {
                var forcesList = forces.ToList();

                for (int i = 0; i < forces.Count(); i++)
                {
					(FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) domainPoint = _failureDomain2d.GetDomainPoint(ConvertForceToPoint(forcesList[i]));
					FailureDomain.FailureDomainForce domainForce = new FailureDomain.FailureDomainForce(forcesList[i], domainPoint.failureDomainPoint);
                    _forces.Add(new FailureDomain.FailureDomainForce2d(domainForce, domainPoint.point2D));
                }
            }
        }

		protected FailureDomainResult2d(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
            _sectionSolver = (SectionSolver)info.GetValue("SectionSolver", typeof(SectionSolver));
            _failureDomain2d = (FailureDomain2d)info.GetValue("FailureDomain2d", typeof(FailureDomain2d));
            _domainType = (DomainTypes)info.GetValue("DomainTypes", typeof(DomainTypes));
            _sectionOption = (Checkers.SectionChecker.SectionOptions)info.GetValue("SectionOption", typeof(Checkers.SectionChecker.SectionOptions));
            _forces = (List<FailureDomain.FailureDomainForce2d>)info.GetValue("Forces", typeof(List<FailureDomain.FailureDomainForce2d>));
        }

        #region Public Async Methods

        /// <summary>
        /// Adds a new single force asynchronously
        /// </summary>
        /// <param name="forces">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        public async Task<FailureDomain.FailureDomainForce2d> AddForceAsync(ResultBeamForces forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Id == -1)
            {
                throw new ArgumentException(nameof(forces));
            }

            FailureDomain.FailureDomainForce2d failureDomainPoint2d = null;

            await Task.Run(() => {
                (FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) domainPoint = _failureDomain2d.GetDomainPoint(ConvertForceToPoint(forces));
                FailureDomain.FailureDomainForce domainForce = new FailureDomain.FailureDomainForce(forces, domainPoint.failureDomainPoint);
                failureDomainPoint2d = new FailureDomain.FailureDomainForce2d(domainForce, domainPoint.point2D);
                _forces.Add(failureDomainPoint2d);
            });

            return failureDomainPoint2d;
        }

        /// <summary>
        /// Adds a new range of forces asynchronously
        /// </summary>
        /// <param name="forces">The forces array</param>
        /// <returns>The corresponding points in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces contains items with Id == -1</exception>
        public async Task<FailureDomain.FailureDomainForce2d[]> AddForcesAsync(IEnumerable<ResultBeamForces> forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Any(f => f.Id == -1))
            {
                throw new ArgumentException(nameof(forces));
            }

            FailureDomain.FailureDomainForce2d[] failureDomainPoint2d = null;

            await Task.Run(() => 
            {
                var forcesList = forces.ToList();
                failureDomainPoint2d = new FailureDomain.FailureDomainForce2d[forcesList.Count];

                Parallel.For(0, forces.Count(), (i) =>
                {
					(FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) domainPoint = _failureDomain2d.GetDomainPoint(ConvertForceToPoint(forcesList[i]));
                    FailureDomain.FailureDomainForce domainForce = new FailureDomain.FailureDomainForce(forcesList[i], domainPoint.failureDomainPoint);
                    failureDomainPoint2d[i] = new FailureDomain.FailureDomainForce2d(domainForce, domainPoint.point2D);
                    _forces.Add(failureDomainPoint2d[i]);
                });
            });

            return failureDomainPoint2d;
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
        public async Task<FailureDomain.FailureDomainForce2d> UpdateForceAsync(int id, ResultBeamForces forces)
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

            FailureDomain.FailureDomainForce2d failureDomainPoint2d = null;

            await Task.Run(() => 
            {
                (FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) domainPoint = _failureDomain2d.GetDomainPoint(ConvertForceToPoint(forces));
                FailureDomain.FailureDomainForce domainForce = new FailureDomain.FailureDomainForce(forces, domainPoint.failureDomainPoint);
                failureDomainPoint2d = new FailureDomain.FailureDomainForce2d(domainForce, domainPoint.point2D);
                _forces.RemoveAt(index);
                _forces.Insert(index, failureDomainPoint2d);
            });

            return failureDomainPoint2d;
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
		internal FailureDomain.FailureDomainForce2d AddForce(ResultBeamForces forces)
        {
            if (forces is null || forces.Id == -1)            
                throw new ArgumentNullException(nameof(forces));            
            else if (forces.Id == -1)            
                throw new ArgumentException(nameof(forces));

            (FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) domainPoint = _failureDomain2d.GetDomainPoint(ConvertForceToPoint(forces));
            FailureDomain.FailureDomainForce domainForce = new FailureDomain.FailureDomainForce(forces, domainPoint.failureDomainPoint);
            FailureDomain.FailureDomainForce2d failureDomainPoint2d = new FailureDomain.FailureDomainForce2d(domainForce, domainPoint.point2D);

            return failureDomainPoint2d;
        }

        /// <summary>
        /// Adds a new range of forces
        /// </summary>
        /// <param name="forces">The forces array</param>
        /// <returns>The corresponding points in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces contains items with Id == -1</exception>
        internal FailureDomain.FailureDomainForce2d[] AddForces(ResultBeamForces[] forces)
        {
            if (forces is null)            
                throw new ArgumentNullException(nameof(forces));            
            else if (forces.Any(f => f.Id == -1))            
                throw new ArgumentException(nameof(forces));            

            var failureDomainPoint = new FailureDomain.FailureDomainForce2d[forces.Length];

            Parallel.For(0, forces.Count(), (i) =>
            {
                failureDomainPoint[i] = AddForce(forces[i]);
            });

            return failureDomainPoint;
        }

        /// <summary>
        /// Update an extisting force racalculating the domain point
        /// </summary>
        /// <param name="id">The id of the original force</param>
        /// <param name="forces">The new force</param>
        /// <returns>The new corresponding point in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        /// <exception cref="KeyNotFoundException">Thrown when did not found a force with the given id</exception>
        internal FailureDomain.FailureDomainForce2d UpdateForce(int id, ResultBeamForces forces)
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

            (FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) domainPoint = _failureDomain2d.GetDomainPoint(ConvertForceToPoint(forces));
            FailureDomain.FailureDomainForce domainForce = new FailureDomain.FailureDomainForce(forces, domainPoint.failureDomainPoint);
            FailureDomain.FailureDomainForce2d failureDomainPoint2d = new FailureDomain.FailureDomainForce2d(domainForce, domainPoint.point2D);

            _forces.RemoveAt(index);
            _forces.Insert(index, failureDomainPoint2d);

            return failureDomainPoint2d;
        }

        /// <summary>
        /// Convert <paramref name="force"/> to a 2d point
        /// </summary>
        /// <param name="force">The force to convert</param>
        /// <returns></returns>
        protected Point2d ConvertForceToPoint(ResultBeamForces force)
        {
            ForceTuple forceTuple = force.ConvertToForceTuple(_sectionOption.ForceReferenceCoordinateSystem);

            if (_domainType == DomainTypes.ConstantN)
                return new Point2d(forceTuple.Mx, forceTuple.My);
            else            
                return new Point2d(forceTuple.N, forceTuple.Mx);            
        }

		#endregion

		#region Force Methods

		/// <summary>
		/// Tells if there is a force with the given id
		/// </summary>
		/// <param name="id">The id to check</param>
		/// <returns>True if the force exists</returns>
		public bool ContainsForceWithId(int id)
        {
            return _forces.Any(force => force.Id == id);
        }

        public IEnumerator<FailureDomain.FailureDomainForce2d> GetEnumerator()
        {
            return _forces.GetEnumerator();
        }

        public FailureDomain.FailureDomainForce2d[] GetFailureDomainForces()
        {
            return _forces.ToArray();
        }

        public void ClearForces()
        {
            _forces.Clear();
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

            return obj is FailureDomainResult2d result &&
                   base.Equals(obj) &&
                   EqualityComparer<SectionSolver>.Default.Equals(_sectionSolver, result._sectionSolver) &&
                   EqualityComparer<FailureDomain2d>.Default.Equals(_failureDomain2d, result._failureDomain2d) &&
                   EqualityComparer<DomainTypes>.Default.Equals(_domainType, result._domainType) &&
                   EqualityComparer<Checkers.SectionChecker.SectionOptions>.Default.Equals(_sectionOption, result._sectionOption);
        }

        public override int GetHashCode()
		{
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _sectionSolver.GetHashCode();
                hashCode = hashCode * -17 + _failureDomain2d.GetHashCode();
                hashCode = hashCode * -17 + _domainType.GetHashCode();
                hashCode = hashCode * -17 + _sectionOption.GetHashCode();
                return hashCode;
            }
        }

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
            info.AddValue("SectionSolver", _sectionSolver);
            info.AddValue("FailureDomain2d", _failureDomain2d);
            info.AddValue("DomainTypes", _domainType);
            info.AddValue("SectionOption", _sectionOption);
            info.AddValue("Forces", _forces);
        }

		#endregion
	}
}