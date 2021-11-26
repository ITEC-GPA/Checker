using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace GPC.Checkers.Concrete.Results
{
    public class FailureDomainResult : CheckerResultType
    {
        protected readonly SectionSolver _sectionSolver;
        protected readonly FailureDomain _failureDomain;
        protected List<FailureDomain.FailureDomainForce> _forces;

        public FailureDomain Domain => _failureDomain;

        public FailureDomainResult(
            IConcreteSection section,
            FailureDomain failureDomain,
            IEnumerable<ResultBeamForces> forces,
            SectionSolver solver,
            Standard standard,
            int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _failureDomain = failureDomain ?? throw new ArgumentNullException(nameof(failureDomain));
            _sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
            _forces = new List<FailureDomain.FailureDomainForce>();

            if (forces != null)
            {
                var forcesList = forces.ToList();

                for (int i = 0; i < forces.Count(); i++)
                {
                    _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], _sectionSolver.CalculateDomainPoint(forcesList[i].ConvertToForceTuple(section.Centroid))));
                }
            }                
        }

        internal FailureDomain.FailureDomainPoint AddForce(ResultBeamForces forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }

            var point = _sectionSolver.CalculateDomainPoint(forces.ConvertToForceTuple(ConcreteSection.Centroid));
            _forces.Add(new FailureDomain.FailureDomainForce(forces, point));
            return point;
        }

        internal FailureDomain.FailureDomainPoint[] AddForces(ResultBeamForces[] forces)
        {
            List<ResultBeamForces> forcesList = forces.ToList();
            var failureDomainPoint = new FailureDomain.FailureDomainPoint[forcesList.Count];

            for (int i = 0; i < forcesList.Count(); i++)
            {
                var point = _sectionSolver.CalculateDomainPoint(forcesList[i].ConvertToForceTuple(ConcreteSection.Centroid));
                _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], point));
                failureDomainPoint[i] = point;
            }

            return failureDomainPoint;
        }

        public async Task<FailureDomain.FailureDomainPoint> AddForceAsync(ResultBeamForces forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            
            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() => {
                failureDomainPoint = _sectionSolver.CalculateDomainPoint(forces.ConvertToForceTuple(ConcreteSection.Centroid));
                _forces.Add(new FailureDomain.FailureDomainForce(forces, failureDomainPoint));
            });

            return failureDomainPoint;
        }

        public async Task<FailureDomain.FailureDomainPoint[]> AddForcesAsync(IEnumerable<ResultBeamForces> forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }

            FailureDomain.FailureDomainPoint[] failureDomainPoint = null;

            await Task.Run(() => {

                var forcesList = forces.ToList();
                failureDomainPoint = new FailureDomain.FailureDomainPoint[forcesList.Count];

                for (int i = 0; i < forcesList.Count(); i++)
                {
                    var point = _sectionSolver.CalculateDomainPoint(forcesList[i].ConvertToForceTuple(ConcreteSection.Centroid));
                    _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], point));
                    failureDomainPoint[i] = point;
                }

            });

            return failureDomainPoint;
        }

        public async Task<FailureDomain.FailureDomainPoint> UpdateForceAsync(int index, ResultBeamForces forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() => {
                var point = _sectionSolver.CalculateDomainPoint(forces.ConvertToForceTuple(ConcreteSection.Centroid));
                failureDomainPoint = point;

                _forces.RemoveAt(index);
                _forces.Insert(index, new FailureDomain.FailureDomainForce(forces, point));
            });

            return failureDomainPoint;
        }

        public IEnumerator<FailureDomain.FailureDomainForce> GetEnumerator()
        {
            return _forces.GetEnumerator();
        }

        public List<string> GetLog()
		{
            return _sectionSolver.GetLog();
		}
    }
}